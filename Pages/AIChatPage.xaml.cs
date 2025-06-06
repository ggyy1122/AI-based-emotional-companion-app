using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameApp.Services.AIChat;
using GameApp.Services.Interfaces;
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

        // Cancellation token source for stopping responses
        private CancellationTokenSource _cancelTokenSource;

        // Flag to track if response is in progress
        private bool isResponseInProgress = false;

        public AIChatPage()
        {
            InitializeComponent();

            // Initialize services
            _aiService = new OpenAIService();
            _sessionManager = SessionManager.Instance;

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
        }

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
            if (_sessionManager.Sessions.Count > 1)
            {
                var deleteItem = new MenuItem { Header = "Delete" };
                deleteItem.Click += (s, e) => DeleteSession(session);
                contextMenu.Items.Add(deleteItem);
            }

            // Clear messages option
            var clearItem = new MenuItem { Header = "Clear Messages" };
            clearItem.Click += (s, e) => ClearSessionMessages(session);
            contextMenu.Items.Add(clearItem);

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

        #region Existing Methods (Message Display, Button Actions, etc.)

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
                }
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation - add "(stopped)" indicator
                streamingHelper?.AddStoppedIndicator();

                // Get the final content with stopped indicator for session storage
                var finalContent = streamingHelper?.GetCurrentContent() ?? "*(stopped)*";
                _sessionManager.AddAssistantMessage(finalContent);
            }
            catch (Exception ex)
            {
                var errorMessage = $"**Sorry, an error occurred:** {ex.Message}";
                AddAIMessage(errorMessage);
                _sessionManager.AddAssistantMessage(errorMessage);
            }
            finally
            {
                isResponseInProgress = false;
                StopButton.IsEnabled = false;
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
                Cursor = System.Windows.Input.Cursors.IBeam,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                FontSize = 14,
            };

            messageText.FocusVisualStyle = null;

            var contextMenu = new ContextMenu();
            var copyItem = new MenuItem { Header = "Copy" };
            copyItem.Click += (s, e) => Clipboard.SetText(messageText.Text);
            contextMenu.Items.Add(copyItem);
            messageText.ContextMenu = contextMenu;

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
                };

                var contextMenu = new ContextMenu();
                var copyItem = new MenuItem { Header = "Copy" };
                copyItem.Click += (s, e) => Clipboard.SetText(message);
                contextMenu.Items.Add(copyItem);
                markdownViewer.ContextMenu = contextMenu;

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
                Cursor = System.Windows.Input.Cursors.IBeam,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                FontSize = 14,
            };

            messageText.FocusVisualStyle = null;

            var contextMenu = new ContextMenu();
            var copyItem = new MenuItem { Header = "Copy" };
            copyItem.Click += (s, e) => Clipboard.SetText(messageText.Text);
            contextMenu.Items.Add(copyItem);
            messageText.ContextMenu = contextMenu;

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

        private void VoiceInputButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Voice input feature will be implemented here.");
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
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageInput.Text;
            ProcessMessage(message);
        }

        private void MessageInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && !e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.LeftShift))
            {
                e.Handled = true;
                SendButton_Click(sender, e);
            }
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
