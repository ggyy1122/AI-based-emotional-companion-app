using System.Windows;
using System.Windows.Controls;
using GameApp.Pages;

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
        private void GoToSecondPage_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new EmotionBook());
        }
    }
}
