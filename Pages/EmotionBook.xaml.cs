using System.Windows;
using System.Windows.Controls;

namespace GameApp.Pages
{
    public partial class EmotionBook : Page
    {
        public EmotionBook()
        {
            InitializeComponent();
        }

        private void BackToMainPage_Click(object sender, RoutedEventArgs e)
        {
            // 返回上一个页面
            if (this.NavigationService.CanGoBack)
                this.NavigationService.GoBack();
            else
                this.NavigationService.Navigate(new MainPage());
        }
    }
}
