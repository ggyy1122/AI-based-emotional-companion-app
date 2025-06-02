using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GameApp.Services.OpenAI;
using GameApp.Services.Interfaces;
using Markdig.Wpf;

namespace GameApp.Pages
{
    public partial class AIChatPage : Page
    {
        // Flag to track if text is placeholder
        private bool isPlaceholderText = true;

        // AI service
        private readonly IAIService _aiService;

        // Cancellation token source for stopping responses
        private CancellationTokenSource _cancelTokenSource;

        // Flag to track if response is in progress
        private bool isResponseInProgress = false;

        public AIChatPage()
        {
            InitializeComponent();

            // Initialize AI service
            _aiService = new OpenAIService();

            // Initialize cancellation token source
            _cancelTokenSource = new CancellationTokenSource();

            // Set up event handlers for buttons
            AttachButton.Click += AttachButton_Click;
            VoiceInputButton.Click += VoiceInputButton_Click;
            StopButton.Click += StopButton_Click;
            SendButton.Click += SendButton_Click;

            // Setup placeholder text behavior
            MessageInput.GotFocus += MessageInput_GotFocus;
            MessageInput.LostFocus += MessageInput_LostFocus;

            // Add initial message (welcome message) with markdown
            AddAIMessage("Hello! I'm your AI assistant. How can I help you today?\n\nFeel free to ask me anything!");

            // Set up key event for text box
            MessageInput.KeyDown += MessageInput_KeyDown;
        }

        private void BackToMainPage_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService.CanGoBack)
                this.NavigationService.GoBack();
            else
                this.NavigationService.Navigate(new MainPage());
        }

        #region Message Display Methods

        /// <summary>
        /// Add a user message to the chat window with proper chat bubble styling
        /// </summary>
        private void AddUserMessage(string message)
        {
            // Create message container with chat bubble styling
            Border messageBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A90E2")),
                Padding = new Thickness(12),
                Margin = new Thickness(80, 5, 10, 5), // More space on left, less on right
                HorizontalAlignment = HorizontalAlignment.Right,
                CornerRadius = new CornerRadius(18, 18, 4, 18), // Chat bubble style
                MaxWidth = 1000 // Limit width for better chat appearance
            };

            // Use TextBox for user messages (selectable text)
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

            // Remove focus border
            messageText.FocusVisualStyle = null;

            // Add context menu for copying
            var contextMenu = new ContextMenu();
            var copyItem = new MenuItem { Header = "Copy" };
            copyItem.Click += (s, e) => Clipboard.SetText(messageText.Text);
            contextMenu.Items.Add(copyItem);
            messageText.ContextMenu = contextMenu;

            // Add text to container
            messageBorder.Child = messageText;

            // Add to message panel
            MessagesPanel.Children.Add(messageBorder);

            // Scroll to bottom
            ChatScrollViewer.ScrollToEnd();
        }

        /// <summary>
        /// Add an AI message to the chat window with markdown support
        /// </summary>
        private void AddAIMessage(string message)
        {
            try
            {
                // Create message container with chat bubble styling
                Border messageBorder = new Border
                {
                    Background = new SolidColorBrush(Colors.LightGray),
                    Padding = new Thickness(12),
                    Margin = new Thickness(10, 5, 80, 5), // Less space on left, more on right
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(18, 18, 18, 4), // Chat bubble style
                    MaxWidth = 1000 // Limit width for better chat appearance
                };

                // Use MarkdownViewer for AI messages
                var markdownViewer = new MarkdownViewer
                {
                    Markdown = message,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    FontSize = 14,
                };

                // Add context menu for copying
                var contextMenu = new ContextMenu();
                var copyItem = new MenuItem { Header = "Copy" };
                copyItem.Click += (s, e) => Clipboard.SetText(message);
                contextMenu.Items.Add(copyItem);
                markdownViewer.ContextMenu = contextMenu;

                // Add to container
                messageBorder.Child = markdownViewer;

                // Add to message panel
                MessagesPanel.Children.Add(messageBorder);

                // Scroll to bottom
                ChatScrollViewer.ScrollToEnd();
            }
            catch (Exception)
            {
                // Fallback to TextBox if MarkdownViewer fails
                AddAIMessageFallback(message);
            }
        }

        /// <summary>
        /// Fallback method when markdown rendering fails
        /// </summary>
        private void AddAIMessageFallback(string message)
        {
            Border messageBorder = new Border
            {
                Background = new SolidColorBrush(Colors.LightGray),
                Padding = new Thickness(12),
                Margin = new Thickness(10, 5, 80, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(18, 18, 18, 4),
                MaxWidth = 1000 // Limit width for better chat appearance
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

        /// <summary>
        /// Process a message and generate a streaming response
        /// </summary>
        private async void ProcessMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || isPlaceholderText || isResponseInProgress)
                return;

            // Set response in progress
            isResponseInProgress = true;

            // Enable stop button
            StopButton.IsEnabled = true;

            // Reset cancellation token
            _cancelTokenSource = new CancellationTokenSource();

            try
            {
                // Add user message
                AddUserMessage(message);

                // Clear input
                MessageInput.Text = string.Empty;
                isPlaceholderText = false;

                // Create a streaming message container with markdown support
                MarkdownViewer streamingViewer;
                Border streamingBorder = StreamingTextHelper.CreateStreamingMarkdownContainer(
                    MessagesPanel, ChatScrollViewer, out streamingViewer);

                // Helper for updating the streaming text
                var streamingHelper = new StreamingTextHelper(
                    streamingViewer,
                    streamingBorder,
                    ChatScrollViewer);

                // Use streaming completion
                await _aiService.StreamCompletionAsync(
                    message,
                    (partialResponse) => streamingHelper.UpdateStreamingMarkdown(partialResponse),
                    _cancelTokenSource.Token);
            }
            catch (Exception ex)
            {
                AddAIMessage($"**Sorry, an error occurred:** {ex.Message}");
            }
            finally
            {
                // Reset response in progress
                isResponseInProgress = false;

                // Disable stop button
                StopButton.IsEnabled = false;
            }
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
            // Stop the streaming response
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
            // Get message text
            string message = MessageInput.Text;

            // Process message
            ProcessMessage(message);
        }

        private void MessageInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Send message on Enter key (without Shift for newline)
            if (e.Key == System.Windows.Input.Key.Enter && !e.KeyboardDevice.IsKeyDown(System.Windows.Input.Key.LeftShift))
            {
                e.Handled = true; // Prevent newline
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
