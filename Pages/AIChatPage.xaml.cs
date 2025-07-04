using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GameApp.Services.AIChat;
using GameApp.Services.Interfaces;
using GameApp.Services.Voice;
using GameApp.Models.AIChat;
using Markdig.Wpf;

namespace GameApp.Pages
{
    public partial class AIChatPage : Page
    {
        // Flag to track if text is placeholder
        private bool isPlaceholderText = true;

        // Services
        private readonly IAIService _aiService;
        private readonly SessionManager _sessionManager;
        private VoiceService _voiceService;

        // Cancellation token source for stopping responses
        private CancellationTokenSource _cancelTokenSource;

        // Flag to track if response is in progress
        private bool isResponseInProgress = false;

        // Flag to track if the current message was from voice input
        private bool isVoiceInputMessage = false;

        private bool _isInFavoriteView = false;         // 当前是否在会话预览
        private bool _isFromFavoritePreview = false;     // 是否由预览进入某会话

        public AIChatPage()
        {
            InitializeComponent();

            // Initialize services
            _aiService = new OpenAIService();
            _sessionManager = SessionManager.Instance;

            // Initialize voice service
            InitializeVoiceService();

            // Initialize cancellation token source
            _cancelTokenSource = new CancellationTokenSource();

            // Set up event handlers
            AttachButton.Click += AttachButton_Click;
            VoiceInputButton.Click += VoiceInputButton_Click;
            StopButton.Click += StopButton_Click;
            SendButton.Click += SendButton_Click;
            MessageInput.GotFocus += MessageInput_GotFocus;
            MessageInput.LostFocus += MessageInput_LostFocus;
            MessageInput.KeyDown += MessageInput_KeyDown;

            // Initialize session UI
            InitializeSessionUI();

            // Add cleanup event
            this.Unloaded += Page_Unloaded;
        }

        #region Voice Service Initialization

        /// <summary>
        /// Initialize voice service with error handling
        /// </summary>
        private void InitializeVoiceService()
        {
            try
            {
                _voiceService = new VoiceService();
                _voiceService.StatusChanged += OnVoiceStatusChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Voice service initialization failed: {ex.Message}",
                               "Voice Service", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Handle voice status changes
        /// </summary>
        private void OnVoiceStatusChanged(string status)
        {
            // Optional: Display status in debug or status bar
            System.Diagnostics.Debug.WriteLine($"Voice Status: {status}");
        }

        /// <summary>
        /// Cleanup resources when page is unloaded
        /// </summary>
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _voiceService?.Dispose();
            _cancelTokenSource?.Dispose();
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Initialize session UI components
        /// </summary>
        private void InitializeSessionUI()
        {
            // Bind sessions to ListBox
            SessionsList.ItemsSource = _sessionManager.Sessions;

            // Subscribe to session manager events
            _sessionManager.SessionChanged += OnSessionChanged;

            // Load current session
            if (_sessionManager.CurrentSession != null)
            {
                SessionsList.SelectedItem = _sessionManager.CurrentSession;
                LoadSessionMessages(_sessionManager.CurrentSession);
                UpdateCurrentSessionTitle(_sessionManager.CurrentSession.Name);
            }
        }

        /// <summary>
        /// Handle session change events
        /// </summary>
        private void OnSessionChanged(ChatSession newSession)
        {
            Dispatcher.Invoke(() =>
            {
                LoadSessionMessages(newSession);
                UpdateCurrentSessionTitle(newSession.Name);
                SessionsList.SelectedItem = newSession;
            });
        }

        /// <summary>
        /// Load messages from session into chat UI
        /// </summary>
        private void LoadSessionMessages(ChatSession session)
        {
            // Clear UI
            MessagesPanel.Children.Clear();

            // Clear and rebuild AI service conversation history
            _aiService.ClearConversationHistory();

            // Load messages to UI and sync with AI service
            foreach (var message in session.Messages)
            {
                // Add to AI service history
                _aiService.AddToConversationHistory(message.Role, message.Content);

                // Add to UI
                if (message.Role == ChatRole.User)
                {
                    AddUserMessage(message.Content);
                }
                else if (message.Role == ChatRole.Assistant)
                {
                    AddAIMessage(message.Content);
                }
            }
        }

        /// <summary>
        /// Update header title with current session name
        /// </summary>
        private void UpdateCurrentSessionTitle(string sessionName)
        {
            CurrentSessionTitle.Text = sessionName ?? "AI Assistant";
        }

        /// <summary>
        /// Create new chat session
        /// </summary>
        private void NewChatButton_Click(object sender, RoutedEventArgs e)
        {
            _sessionManager.CreateNewSession();
        }

        /// <summary>
        /// Handle session selection change
        /// </summary>
        private void SessionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SessionsList.SelectedItem is ChatSession selectedSession)
            {

                ShowChatArea();
                _sessionManager.SwitchToSession(selectedSession);
            }

        }

        /// <summary>
        /// Show context menu for session operations
        /// </summary>
        private void SessionMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ChatSession session)
            {
                ShowSessionContextMenu(button, session);
            }
        }

        /// <summary>
        /// Display context menu with session options
        /// </summary>
        private void ShowSessionContextMenu(Button button, ChatSession session)
        {
            var contextMenu = new ContextMenu();

            // Rename option
            var renameItem = new MenuItem { Header = "Rename" };
            renameItem.Click += (s, e) => RenameSession(session);
            contextMenu.Items.Add(renameItem);

            // Delete option (only if more than one session)
            if (session.IsSpecialElfSession == 0)
            {
                var deleteItem = new MenuItem { Header = "Delete" };
                deleteItem.Click += (s, e) => DeleteSession(session);
                contextMenu.Items.Add(deleteItem);
            }

            // Clear messages option
            var clearItem = new MenuItem { Header = "Clear Messages" };
            clearItem.Click += (s, e) => ClearSessionMessages(session);
            contextMenu.Items.Add(clearItem);

            // Add favorite/unfavorite option
            var favoriteItem = new MenuItem
            {
                Header = session.IsFavorite ? "Unfavorite" : "Favorite"
            };
            favoriteItem.Click += (s, e) =>
            {
                // Toggle favorite status in SessionManager (also updates database)
                var isNowFavorite = SessionManager.Instance.ToggleFavorite(session);
                // Update menu text immediately
                favoriteItem.Header = isNowFavorite ? "Unfavorite" : "Favorite";
                // Optionally: refresh UI of favorite list if needed
            };
            contextMenu.Items.Add(favoriteItem);

            contextMenu.PlacementTarget = button;
            contextMenu.IsOpen = true;
        }

        /// <summary>
        /// Rename a session using simple dialog
        /// </summary>
        private void RenameSession(ChatSession session)
        {
            var newName = SimpleDialog.ShowInput("Rename Session", "Enter new session name:", session.Name);

            if (!string.IsNullOrWhiteSpace(newName))
            {
                _sessionManager.RenameSession(session, newName);
                UpdateCurrentSessionTitle(session.Name);
            }
        }

        /// <summary>
        /// Delete a session with confirmation
        /// </summary>
        private void DeleteSession(ChatSession session)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete '{session.Name}'?",
                "Delete Session",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _sessionManager.DeleteSession(session);
            }
        }

        /// <summary>
        /// Clear messages from session with confirmation
        /// </summary>
        private void ClearSessionMessages(ChatSession session)
        {
            var result = MessageBox.Show(
                $"Clear all messages in '{session.Name}'?",
                "Clear Messages",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Clear session messages
                _sessionManager.ClearSession(session);

                // Clear AI service conversation history to reset context
                _aiService.ClearConversationHistory();

                // Refresh UI if this is the current session
                if (_sessionManager.CurrentSession == session)
                {
                    LoadSessionMessages(session);
                }
            }
        }

        #endregion

        #region Message Processing

        private void BackToMainPage_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService.CanGoBack)
                this.NavigationService.GoBack();
            else
                this.NavigationService.Navigate(new MainPage());
        }

        /// <summary>
        /// Process a message and generate response
        /// </summary>
        private async void ProcessMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || isPlaceholderText || isResponseInProgress)
                return;

            isResponseInProgress = true;
            StopButton.IsEnabled = true;
            _cancelTokenSource = new CancellationTokenSource();

            StreamingTextHelper streamingHelper = null;

            try
            {
                // Add user message to session and UI
                _sessionManager.AddUserMessage(message);
                AddUserMessage(message);

                // Clear input
                MessageInput.Text = string.Empty;
                isPlaceholderText = false;

                // Create streaming container
                MarkdownViewer streamingViewer;
                Border streamingBorder = StreamingTextHelper.CreateStreamingMarkdownContainer(
                    MessagesPanel, ChatScrollViewer, out streamingViewer);

                streamingHelper = new StreamingTextHelper(streamingViewer, streamingBorder, ChatScrollViewer);

                string fullResponse = string.Empty;

                // Use streaming completion
                await _aiService.StreamCompletionAsync(
                    message,
                    (partialResponse) =>
                    {
                        fullResponse = partialResponse;
                        streamingHelper.UpdateStreamingMarkdown(partialResponse);
                    },
                    _cancelTokenSource.Token);

                // Add complete response to session
                if (!string.IsNullOrEmpty(fullResponse))
                {
                    _sessionManager.AddAssistantMessage(fullResponse);

                    // Fix context menu for streaming message
                    Dispatcher.Invoke(() =>
                    {
                        RebindContextMenuForStreamingMessage(streamingBorder, streamingViewer, fullResponse);
                    });

                    if (isVoiceInputMessage)
                    {
                        await PlayVoiceResponseAsync(fullResponse);
                        isVoiceInputMessage = false; // Reset flag
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation - add "(stopped)" indicator
                streamingHelper?.AddStoppedIndicator();

                // Get the final content with stopped indicator for session storage
                var finalContent = streamingHelper?.GetCurrentContent() ?? "*(stopped)*";
                _sessionManager.AddAssistantMessage(finalContent);

                // Fix context menu for cancelled message
                if (streamingHelper != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var lastBorder = MessagesPanel.Children.OfType<Border>()
                            .LastOrDefault(b => b.HorizontalAlignment == HorizontalAlignment.Left);
                        if (lastBorder?.Child is MarkdownViewer lastViewer)
                        {
                            RebindContextMenuForStreamingMessage(lastBorder, lastViewer, finalContent);
                        }
                    });
                }

                isVoiceInputMessage = false;
            }
            catch (Exception ex)
            {
                var errorMessage = $"**Sorry, an error occurred:** {ex.Message}";
                AddAIMessage(errorMessage);
                _sessionManager.AddAssistantMessage(errorMessage);

                isVoiceInputMessage = false;
            }
            finally
            {
                isResponseInProgress = false;
                StopButton.IsEnabled = false;
            }
        }

        #endregion

        #region Voice Actions

        /// <summary>
        /// Read aloud plain text (for user messages)
        /// </summary>
        private void ReadAloudPlainText(string text)
        {
            if (_voiceService != null && _voiceService.IsInitialized)
            {
                try
                {
                    _voiceService.SpeakText(text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to read message aloud: {ex.Message}",
                                   "Read Aloud Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Voice service is not available or not initialized.\nPlease check your audio settings.",
                               "Read Aloud", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Read aloud markdown text (for AI messages)
        /// </summary>
        private void ReadAloudMarkdownText(string markdownText)
        {
            if (_voiceService != null && _voiceService.IsInitialized)
            {
                try
                {
                    _voiceService.SpeakMarkdownText(markdownText);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to read message aloud: {ex.Message}",
                                   "Read Aloud Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Voice service is not available or not initialized.\nPlease check your audio settings.",
                               "Read Aloud", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Play AI response as voice automatically with delay
        /// </summary>
        private async Task PlayVoiceResponseAsync(string responseText)
        {
            try
            {
                // Add a small delay to ensure the response is fully displayed
                await Task.Delay(10);

                if (_voiceService != null && _voiceService.IsInitialized)
                {
                    // Use async method to avoid blocking UI
                    await Task.Run(() =>
                    {
                        try
                        {
                            _voiceService.SpeakMarkdownText(responseText);
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                System.Diagnostics.Debug.WriteLine($"Auto voice playback failed: {ex.Message}");
                            });
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Voice service not available for auto-playback");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in auto voice playback: {ex.Message}");
            }
        }

        #endregion

        #region Context Menu Creation

        /// <summary>
        /// Create enhanced context menu for user messages
        /// </summary>
        private ContextMenu CreateUserMessageContextMenu(string messageContent)
        {
            var contextMenu = new ContextMenu();

            // Copy option
            var copyItem = new MenuItem { Header = "Copy" };
            copyItem.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(messageContent);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy text: {ex.Message}", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            contextMenu.Items.Add(copyItem);

            // Add separator
            contextMenu.Items.Add(new Separator());

            // Read Aloud option for plain text
            var readAloudItem = new MenuItem
            {
                Header = "🔊 Read Aloud",
                IsEnabled = _voiceService?.IsInitialized == true
            };
            readAloudItem.Click += (s, e) => ReadAloudPlainText(messageContent);
            contextMenu.Items.Add(readAloudItem);

            return contextMenu;
        }

        /// <summary>
        /// Create enhanced context menu for AI messages
        /// </summary>
        private ContextMenu CreateAIMessageContextMenu(string messageContent)
        {
            var contextMenu = new ContextMenu();

            // Copy option
            var copyItem = new MenuItem { Header = "Copy" };
            copyItem.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(messageContent);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy text: {ex.Message}", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            contextMenu.Items.Add(copyItem);

            // Add separator
            contextMenu.Items.Add(new Separator());

            // Read Aloud option for markdown text
            var readAloudItem = new MenuItem
            {
                Header = "🔊 Read Aloud",
                IsEnabled = _voiceService?.IsInitialized == true
            };
            readAloudItem.Click += (s, e) => ReadAloudMarkdownText(messageContent);
            contextMenu.Items.Add(readAloudItem);

            return contextMenu;
        }

        /// <summary>
        /// Re-bind context menu for streaming messages after completion
        /// </summary>
        private void RebindContextMenuForStreamingMessage(Border messageBorder, MarkdownViewer markdownViewer, string messageContent)
        {
            try
            {
                // Create context menu for AI messages (markdown)
                var contextMenu = CreateAIMessageContextMenu(messageContent);

                // Apply context menu to both container and markdown viewer
                markdownViewer.ContextMenu = contextMenu;
                messageBorder.ContextMenu = contextMenu;

                // Store message content for event handler
                markdownViewer.Tag = messageContent;

                // Remove old event handlers to avoid duplicates
                markdownViewer.MouseRightButtonUp -= OnMarkdownViewerRightClick;

                // Add right-click event handler
                markdownViewer.MouseRightButtonUp += OnMarkdownViewerRightClick;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to rebind context menu: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle right-click on markdown viewer
        /// </summary>
        private void OnMarkdownViewerRightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is MarkdownViewer markdownViewer)
            {
                var messageContent = markdownViewer.Tag as string ?? "";
                var contextMenu = CreateAIMessageContextMenu(messageContent);

                contextMenu.PlacementTarget = markdownViewer;
                contextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        #endregion

        #region Message Display Methods

        private void AddUserMessage(string message)
        {
            Border messageBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A90E2")),
                Padding = new Thickness(12),
                Margin = new Thickness(80, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Right,
                CornerRadius = new CornerRadius(18, 18, 4, 18),
                MaxWidth = 1000
            };

            TextBox messageText = new TextBox
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                IsTabStop = false,
                Cursor = Cursors.IBeam,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                FontSize = 14,
                Tag = message // Store message content
            };

            messageText.FocusVisualStyle = null;

            // Create enhanced context menu for user messages (plain text)
            var contextMenu = CreateUserMessageContextMenu(message);
            messageText.ContextMenu = contextMenu;
            messageBorder.ContextMenu = contextMenu;

            messageBorder.Child = messageText;
            MessagesPanel.Children.Add(messageBorder);
            ChatScrollViewer.ScrollToEnd();
        }

        private void AddAIMessage(string message)
        {
            try
            {
                Border messageBorder = new Border
                {
                    Background = new SolidColorBrush(Colors.LightGray),
                    Padding = new Thickness(12),
                    Margin = new Thickness(10, 5, 80, 5),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(18, 18, 18, 4),
                    MaxWidth = 1000
                };

                var markdownViewer = new MarkdownViewer
                {
                    Markdown = message,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    FontSize = 14,
                    Tag = message // Store message content
                };

                // Create enhanced context menu for AI messages (markdown)
                var contextMenu = CreateAIMessageContextMenu(message);
                markdownViewer.ContextMenu = contextMenu;
                messageBorder.ContextMenu = contextMenu;

                // Add right-click event handler
                markdownViewer.MouseRightButtonUp += OnMarkdownViewerRightClick;

                messageBorder.Child = markdownViewer;
                MessagesPanel.Children.Add(messageBorder);
                ChatScrollViewer.ScrollToEnd();
            }
            catch (Exception)
            {
                AddAIMessageFallback(message);
            }
        }

        private void AddAIMessageFallback(string message)
        {
            Border messageBorder = new Border
            {
                Background = new SolidColorBrush(Colors.LightGray),
                Padding = new Thickness(12),
                Margin = new Thickness(10, 5, 80, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(18, 18, 18, 4),
                MaxWidth = 1000
            };

            TextBox messageText = new TextBox
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Black,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                IsTabStop = false,
                Cursor = Cursors.IBeam,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                FontSize = 14,
                Tag = message // Store message content
            };

            messageText.FocusVisualStyle = null;

            // Create enhanced context menu for AI messages (markdown, but using fallback)
            var contextMenu = CreateAIMessageContextMenu(message);
            messageText.ContextMenu = contextMenu;
            messageBorder.ContextMenu = contextMenu;

            messageBorder.Child = messageText;
            MessagesPanel.Children.Add(messageBorder);
            ChatScrollViewer.ScrollToEnd();
        }

        #endregion

        #region Button Actions

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Attach file feature will be implemented here.");
        }

        // Replace the existing VoiceInputButton_Click method (around line 730)

        private void VoiceInputButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show voice input dialog which will automatically start listening
                var voiceDialog = new VoiceInputDialog()
                {
                    Owner = Window.GetWindow(this),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                // Show dialog and get result
                bool? result = voiceDialog.ShowDialog();

                if (result == true && voiceDialog.SendMessage && !string.IsNullOrWhiteSpace(voiceDialog.MessageText))
                {
                    // Set the message in the input box
                    MessageInput.Text = voiceDialog.MessageText;
                    isPlaceholderText = false;

                    // ADD THIS LINE: Mark this as a voice input message
                    isVoiceInputMessage = true;

                    // Process the message immediately
                    ProcessMessage(voiceDialog.MessageText);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening voice input dialog: {ex.Message}",
                               "Voice Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (isResponseInProgress)
            {
                _aiService.StopStreaming();
                _cancelTokenSource.Cancel();
                isResponseInProgress = false;
                StopButton.IsEnabled = false;
            }

            // Also stop any current speech
            _voiceService?.StopSpeaking();
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageInput.Text;
            isVoiceInputMessage = false;
            ProcessMessage(message);
        }

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !e.KeyboardDevice.IsKeyDown(Key.LeftShift))
            {
                e.Handled = true;
                SendButton_Click(sender, e);
            }
        }
        private bool _isSidebarCollapsed = false;
        // 新增的侧栏切换按钮事件
        private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            // 修正：通过x:Name访问主Grid（需在XAML中为Grid添加Name）
            MainGrid.ColumnDefinitions[0].Width = _isSidebarCollapsed
                ? new GridLength(280)  // 展开
                : new GridLength(0);   // 收起
            _isSidebarCollapsed = !_isSidebarCollapsed;
        }


        //收藏按钮
        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            _isInFavoriteView = true;
            //  _isFromFavoritePreview = false;
            ShowFavoritePreview();
        }

        private void ShowFavoritePreview()
        {
            ChatAreaGrid.Visibility = Visibility.Collapsed;
            FavoritePreviewGrid.Visibility = Visibility.Visible;

            List<ChatSession> favorites;
            using (var db = new ChatDbService())
            {
                favorites = db.LoadFavoriteSessions();
                FavoritePreviewList.ItemsSource = favorites;
            }
            if (favorites == null || favorites.Count == 0)
            {
                ShowChatArea();
                MessageBox.Show("暂无收藏的会话", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // 隐藏聊天区返回按钮
            BackToPreviewButton.Visibility = Visibility.Collapsed;
        }
        private void FavoritePreviewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FavoritePreviewList.SelectedItem is ChatSession selectedSession)
            {
                // 用Id到SessionManager里查找原始对象
                var realSession = _sessionManager.Sessions
                    .FirstOrDefault(s => s.Id == selectedSession.Id);

                if (realSession != null)
                {
                    _isFromFavoritePreview = true;
                    _sessionManager.SwitchToSession(realSession);
                    ShowChatArea();
                }
                else
                {
                    MessageBox.Show("该会话已被删除或不存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                FavoritePreviewList.SelectedItem = null;
            }
        }
        private void ShowChatArea()
        {
            ChatAreaGrid.Visibility = Visibility.Visible;
            FavoritePreviewGrid.Visibility = Visibility.Collapsed;

            // 只要是从预览进来的就显示返回按钮
            BackToPreviewButton.Visibility = _isFromFavoritePreview ? Visibility.Visible : Visibility.Collapsed;

            // 正常加载聊天内容
            if (_sessionManager.CurrentSession != null)
            {
                LoadSessionMessages(_sessionManager.CurrentSession);
                UpdateCurrentSessionTitle(_sessionManager.CurrentSession.Name);
            }
        }
        private void BackToPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            _isFromFavoritePreview = false;
            ShowFavoritePreview();
        }

        #endregion

        #region Input Placeholder Handling

        private void MessageInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (MessageInput.Text == "Type your message here...")
            {
                MessageInput.Text = string.Empty;
                isPlaceholderText = false;
            }
        }

        private void MessageInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageInput.Text))
            {
                MessageInput.Text = "Type your message here...";
                isPlaceholderText = true;
            }
        }

        #endregion
    }
}
