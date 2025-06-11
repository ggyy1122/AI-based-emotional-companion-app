using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using GameApp.Windows;
using WpfAnimatedGif;
using GameApp.Services.AIChat;
using System.Threading.Tasks;

namespace GameApp.Controls
{
    public partial class SpriteControl : UserControl
    {
        private bool _isDragging = false;
        private Point _mouseStartPos;
        private Point _elementStartPos;
        private string _currentMood = "高兴";
        private readonly string[] _spriteUris =
        {
            "/Resources/Sprite/sprite1.gif",
            "/Resources/Sprite/sprite2.png",
            "/Resources/Sprite/sprite3.gif",
            "/Resources/Sprite/sprite4.png"
        };
        private int _currentSpriteIndex = 2;

        private readonly string[] _messages =
        {
            "你好呀~", "天气真好！", "困了...", "快来玩！", "嘻嘻嘻", "我在等你哦"
        };

        private readonly Random _random = new Random();
        private readonly OpenAIService _aiService = new OpenAIService();

        public SpriteControl()
        {
            InitializeComponent();
            SwitchSprite("/Resources/Sprite/sprite3.gif");
            SpriteImage.MouseRightButtonDown += SpriteImage_MouseRightButtonDown;

            // 定时器：随机间隔弹消息泡
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(_random.Next(5, 10));
            timer.Tick += async (s, e) =>
            {
                await ShowRandomBubbleAsync();
                timer.Interval = TimeSpan.FromSeconds(_random.Next(5, 10));
            };
            timer.Start();
        }

        // 设置心情（同步方法）
        public void SetMood(string mood)
        {
            _currentMood = mood;
        }

        // 异步显示气泡（使用当前心情）
        private async Task ShowRandomBubbleAsync()
        {
            try
            {
                string aiMessage = await _aiService.GenerateGreetingAsync(_currentMood);
                ShowBubble(aiMessage);
            }
            catch
            {
                ShowBubble(_messages[_random.Next(_messages.Length)]);
            }
        }
        private void SpriteImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _currentSpriteIndex = (_currentSpriteIndex + 1) % _spriteUris.Length;
            SwitchSprite(_spriteUris[_currentSpriteIndex]);
            e.Handled = true;
        }

        public void SwitchSprite(string uri)
        {
            var ext = System.IO.Path.GetExtension(uri)?.ToLower();

            if (ext == ".gif")
            {
                var imageUri = new Uri(uri, UriKind.RelativeOrAbsolute);
                var image = new BitmapImage(imageUri);
                ImageBehavior.SetAnimatedSource(SpriteImage, image);
            }
            else
            {
                ImageBehavior.SetAnimatedSource(SpriteImage, null);
                SpriteImage.Source = new BitmapImage(new Uri(uri, UriKind.RelativeOrAbsolute));
            }
        }

        private void SpriteImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ShowTalkDialog();
                return;
            }

            _isDragging = true;
            _mouseStartPos = e.GetPosition(null);
            _elementStartPos = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
            this.BeginAnimation(Canvas.LeftProperty, null);
            this.BeginAnimation(Canvas.TopProperty, null);
            ((UIElement)sender).CaptureMouse();
        }

        private void SpriteImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPos = e.GetPosition(null);
                var offsetX = currentPos.X - _mouseStartPos.X;
                var offsetY = currentPos.Y - _mouseStartPos.Y;
                Canvas.SetLeft(this, _elementStartPos.X + offsetX);
                Canvas.SetTop(this, _elementStartPos.Y + offsetY);
            }
        }

        private void SpriteImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }

        private void ShowTalkDialog()
        {
            var elfWindow = new SimpleElfChatWindow
            {
                Owner = Window.GetWindow(this)
            };
            elfWindow.Show();
        }

        private void ShowBubble(string message)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                Background = System.Windows.Media.Brushes.LightYellow,
                Padding = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 120
            };

            var border = new Border
            {
                Child = textBlock,
                Background = System.Windows.Media.Brushes.LightYellow,
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Opacity = 0.9,
                Padding = new Thickness(5)
            };

            double left = SpriteImage.ActualWidth / 2 - 30;
            double top = -40;
            Canvas.SetLeft(border, left);
            Canvas.SetTop(border, top);

            BubbleCanvas.Children.Add(border);

            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(1)))
            {
                BeginTime = TimeSpan.FromSeconds(2)
            };

            var moveUp = new DoubleAnimation(top, top - 20, new Duration(TimeSpan.FromSeconds(1)))
            {
                BeginTime = TimeSpan.FromSeconds(2)
            };

            fadeOut.Completed += (s, e) => BubbleCanvas.Children.Remove(border);

            border.BeginAnimation(OpacityProperty, fadeOut);
            border.BeginAnimation(Canvas.TopProperty, moveUp);
        }
    }
}