using System.Windows;
using System.Windows.Controls;
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
    }
}
