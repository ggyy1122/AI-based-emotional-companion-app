using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GameApp.Pages;
using TowerDefenseGame;

namespace GameApp
{
    /// <summary>
    /// MainPage.xaml 的交互逻辑
    /// </summary>
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
        }
        private void GoToEmotionBook_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new EmotionBook());
        }
        private void GoToSpiritDashboard_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new SpiritDashboard());
        }
        private void GoToGame_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new GamePage());
        }
        private void GoToAIChatPage_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new AIChatPage());
        }

        //点击背景事件
        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 判断点击的是不是MySprite
            if (e.OriginalSource == MySprite || IsDescendantOf(e.OriginalSource as DependencyObject, MySprite))
            {
                // 点击了MySprite，不移动
                return;
            }

            var clickPosition = e.GetPosition(MainCanvas);

            double currentX = Canvas.GetLeft(MySprite);
            double currentY = Canvas.GetTop(MySprite);

            double targetX = clickPosition.X - MySprite.Width / 2;
            double targetY = clickPosition.Y - MySprite.Height / 2;

            var animX = new DoubleAnimation
            {
                From = currentX,
                To = targetX,
                Duration = TimeSpan.FromSeconds(0.5)
            };

            var animY = new DoubleAnimation
            {
                From = currentY,
                To = targetY,
                Duration = TimeSpan.FromSeconds(0.5)
            };

            MySprite.BeginAnimation(Canvas.LeftProperty, animX);
            MySprite.BeginAnimation(Canvas.TopProperty, animY);
        }

        // 递归判断一个控件是否是另一个控件的子控件
        private bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            while (child != null)
            {
                if (child == parent)
                    return true;
                child = VisualTreeHelper.GetParent(child);
            }
            return false;
        }


        private Button _lastSelectedButton; // 记录上次选中的按钮

        private async void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button currentButton && currentButton.Tag is string mood)
            {
                // 如果点击的是同一个按钮，不做处理
                if (_lastSelectedButton == currentButton)
                    return;

                // 恢复上一个按钮的大小
                if (_lastSelectedButton != null)
                {
                    _lastSelectedButton.FontSize = 24;
                    ((Image)_lastSelectedButton.Content).Width = 24;
                    ((Image)_lastSelectedButton.Content).Height = 24;
                }

                // 设置当前按钮为选中状态（变大）
                currentButton.FontSize = 28;
                ((Image)currentButton.Content).Width = 32;
                ((Image)currentButton.Content).Height = 32;

                // 更新最后选中的按钮
                _lastSelectedButton = currentButton;

                // 设置精灵心情
                MySprite.SetMood(mood);

                // 保持原有200ms动画效果
                await Task.Delay(200);
            }
        }
    }
}
