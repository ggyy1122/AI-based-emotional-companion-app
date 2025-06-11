using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GameApp.Models.AIChat;
using GameApp.Services.AIChat;
using GameApp.Services.Interfaces;

namespace GameApp.Windows
{
    public partial class SimpleElfChatWindow : Window
    {
        private readonly SessionManager _sessionManager = SessionManager.Instance;
        private readonly IAIService _aiService = new OpenAIService();
        private CancellationTokenSource _cancelTokenSource;
        private bool isResponseInProgress = false;
        private ChatSession _currentSession;
        private Border _thinkingMessageBorder; // 用于跟踪"Thinking..."消息

        public SimpleElfChatWindow()
        {
            InitializeComponent();

            _cancelTokenSource = new CancellationTokenSource();

            // 获取或创建精灵会话
            _currentSession = _sessionManager.Sessions.FirstOrDefault(s => s.IsSpecialElfSession == 1);
            if (_currentSession != null)
            {
                _sessionManager.SwitchToSession(_currentSession);
            }
            else
            {
                _currentSession = _sessionManager.CreateNewSession("精灵对话");
                _currentSession.IsSpecialElfSession = 1;
                _sessionManager.AddAssistantMessage("你好！我是你的精灵助手。");
            }

            // 订阅会话变更事件
            _sessionManager.SessionChanged += OnSessionChanged;
            this.Closed += (s, e) => _sessionManager.SessionChanged -= OnSessionChanged;

            // 初始加载消息
            LoadMessages();
        }

        private void OnSessionChanged(ChatSession newSession)
        {
            Dispatcher.Invoke(() =>
            {
                // 检查是否是当前精灵会话
                if (newSession.IsSpecialElfSession == 1)
                {
                    _currentSession = newSession;
                    LoadMessages();
                }
            });
        }

        private async void ProcessMessage(string message)
        {
            if (isResponseInProgress) return;

            isResponseInProgress = true;
            _cancelTokenSource = new CancellationTokenSource();

            try
            {
                // 添加用户消息到会话并立即显示
                _sessionManager.AddUserMessage(message);
                Dispatcher.Invoke(() => AddMessageToContainer(new ChatMessage(ChatRole.User, message)));

                // 显示"Thinking..."消息
                Dispatcher.Invoke(() => {
                    _thinkingMessageBorder = CreateThinkingMessage();
                    MessageContainer.Children.Add(_thinkingMessageBorder);
                    MessageScrollViewer.ScrollToEnd();
                });

                // 使用AI服务获取回复
                string fullResponse = await _aiService.GetCompletionAsync(
                    message,
                    _cancelTokenSource.Token);

                if (!string.IsNullOrEmpty(fullResponse))
                {
                    // 移除"Thinking..."消息
                    Dispatcher.Invoke(() => {
                        if (_thinkingMessageBorder != null && MessageContainer.Children.Contains(_thinkingMessageBorder))
                        {
                            MessageContainer.Children.Remove(_thinkingMessageBorder);
                        }
                    });

                    // 添加AI回复到会话并立即显示
                    _sessionManager.AddAssistantMessage(fullResponse);
                    Dispatcher.Invoke(() => AddMessageToContainer(new ChatMessage(ChatRole.Assistant, fullResponse)));
                }
            }
            catch (OperationCanceledException)
            {
                string stoppedMessage = "*(对话已停止)*";
                Dispatcher.Invoke(() => {
                    if (_thinkingMessageBorder != null && MessageContainer.Children.Contains(_thinkingMessageBorder))
                    {
                        MessageContainer.Children.Remove(_thinkingMessageBorder);
                    }
                });
                _sessionManager.AddAssistantMessage(stoppedMessage);
                Dispatcher.Invoke(() => AddMessageToContainer(new ChatMessage(ChatRole.Assistant, stoppedMessage)));
            }
            catch (Exception ex)
            {
                string errorMessage = $"​**出错:​**​ {ex.Message}";
                Dispatcher.Invoke(() => {
                    if (_thinkingMessageBorder != null && MessageContainer.Children.Contains(_thinkingMessageBorder))
                    {
                        MessageContainer.Children.Remove(_thinkingMessageBorder);
                    }
                });
                _sessionManager.AddAssistantMessage(errorMessage);
                Dispatcher.Invoke(() => AddMessageToContainer(new ChatMessage(ChatRole.Assistant, errorMessage)));
            }
            finally
            {
                isResponseInProgress = false;
                _thinkingMessageBorder = null;
            }
        }

        private Border CreateThinkingMessage()
        {
            var border = new Border
            {
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Colors.LightGray)
            };

            var textBlock = new TextBlock
            {
                Text = "Thinking...",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontStyle = FontStyles.Italic
            };

            border.Child = textBlock;
            return border;
        }

        private void LoadMessages()
        {
            MessageContainer.Children.Clear();

            if (_sessionManager.CurrentSession == null)
                return;

            foreach (var message in _sessionManager.CurrentSession.Messages)
            {
                AddMessageToContainer(message);
            }

            MessageScrollViewer.ScrollToEnd();
        }

        private void AddMessageToContainer(ChatMessage message)
        {
            Dispatcher.Invoke(() =>
            {
                var border = new Border
                {
                    Margin = new Thickness(5),
                    Padding = new Thickness(10),
                    CornerRadius = new CornerRadius(5),
                    Background = message.Role == ChatRole.User
                        ? new SolidColorBrush(Color.FromRgb(74, 144, 226))
                        : new SolidColorBrush(Colors.LightGray)
                };

                var textBlock = new TextBlock
                {
                    Text = message.Content,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White
                };

                border.Child = textBlock;
                MessageContainer.Children.Add(border);
                MessageScrollViewer.ScrollToEnd();
            });
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendMessage();
                e.Handled = true; // 防止回车键的默认行为（如换行）
            }
        }

        private void SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(InputBox.Text))
            {
                ProcessMessage(InputBox.Text);
                InputBox.Text = "";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancelTokenSource?.Cancel();
            _cancelTokenSource?.Dispose();
            base.OnClosed(e);
        }
    }
}