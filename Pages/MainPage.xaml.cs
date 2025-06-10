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

    }
}
