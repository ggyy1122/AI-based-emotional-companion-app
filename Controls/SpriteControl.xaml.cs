using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GameApp.Windows;
using WpfAnimatedGif;

namespace GameApp.Controls
{
    public partial class SpriteControl : UserControl
    {
        // 这几个字段用于拖拽状态跟踪
        private bool _isDragging = false;
        private Point _mouseStartPos;
        private Point _elementStartPos;

        public SpriteControl()
        {
            InitializeComponent();
            SwitchSprite("/Resources/Sprite/sprite1.gif");  // 默认显示的图片路径
           // SpriteImage.MouseLeftButtonDown += SpriteImage_MouseLeftButtonDown;
        }

        // 外部调用切换图片（gif或jpg）
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

            // 清除动画，避免动画干扰拖拽
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

                // 更新控件位置，实现拖拽
                Canvas.SetLeft(this, _elementStartPos.X + offsetX);
                Canvas.SetTop(this, _elementStartPos.Y + offsetY);
            }
        }

        private void SpriteImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }

        // 双击弹窗方法
        // 双击弹窗方法（非阻塞）
        private void ShowTalkDialog()
        {
            // 直接实例化已经写好的 SimpleElfChatWindow
            var elfWindow = new SimpleElfChatWindow
            {
                Owner = Window.GetWindow(this) // 设置父窗口
            };

            // 显示窗口（非阻塞）
            elfWindow.Show();
        }
    }
}
