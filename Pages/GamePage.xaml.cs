using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Data.SQLite;
using GameApp;
using HandAngleDemo;
using System.ComponentModel;

namespace TowerDefenseGame
{
    public partial class GamePage : Page, INotifyPropertyChanged
    {
        private readonly DispatcherTimer _gameTimer;//定时器
        private readonly Random _random = new Random();//随机数生成器

        //private readonly List<Ellipse> _bullets = new List<Ellipse>();//子弹
        private readonly List<Image> _bullets = new List<Image>();//子弹
        private readonly List<Image> _enemies = new List<Image>();//敌人
        private readonly List<Vector> _bulletDirections = new List<Vector>();//子弹的移动方向
        private readonly List<Vector> _enemyDirections = new List<Vector>();//敌人的移动方向

        private readonly List<BitmapImage> AllEnemy = new List<BitmapImage>();

        private int _score = 0;//得分
        private int _greatestScore = 0;

        private const double ShotInterval = 1.5; // 射击间隔(秒)
        private const double BulletSpeed = 300; // 子弹速度(像素/秒)
        private const double EnemySpeed = 100; // 敌人速度(像素/秒)
        private double EnemySpawnInterval = 0.0; // 敌人生成间隔(秒)
        private static readonly double[] EnemyIntervals = new double[]
        {
            0.5,
            1.0,
            2.0
        };

        private DateTime _lastShotTime = DateTime.MinValue;//上次射击时间
        private DateTime _lastEnemySpawnTime = DateTime.MinValue;//上次敌人生成时间

        //private static readonly string ScoreFilePath = "highscore.txt";

        private const int StarCount = 300; // 背景的星光数量

        private DateTime _lastRotationTime = DateTime.MinValue;//旋转间隔
        private const double MinRotationInterval = 1.0; // 60FPS

        private const double RocketNoseeInterval = 0.75;
        private DateTime _lastSeeTime = DateTime.MinValue;

        List<BitmapImage> boom = new List<BitmapImage>();

        private const string RESOURCES_PATH = "pack://application:,,,/Resources/";

        // 手势识别相关字段
        private HandGesture? handges = null;
        private bool _isHandGestureReady = false;

        private double __angle;

        private bool isMouse = true;

        public GamePage()
        {
            InitializeComponent();

            // 设置游戏计时器，60FPS
            _gameTimer = new DispatcherTimer//定时器，用于周期性任务
            {
                Interval = TimeSpan.FromSeconds(1.0 / 60.0)//每隔这段间隔执行一次Tick
            };
            _gameTimer.Tick += GameLoop;//将Tick与游戏主循环绑定，使得每次触发Tick，都会执行GameLoop
            _greatestScore = LoadScore();
            ScoreText.Text = $"当前分数: {_score} (最高分数: {_greatestScore})";
            Loaded += (s, e) => InitializeStarfield();

            for (int i = 0; i < 7; i++)
            {
                boom.Add(new BitmapImage(new Uri($"pack://application:,,,/Resources/boom{i}.png")));
            }
            for (int i = 0; i < 4; i++)
            {
                AllEnemy.Add(new BitmapImage(new Uri($"pack://application:,,,/Resources/monster{i}.png")));
            }
            EnemySpawnInterval = EnemyIntervals[2];
            DataContext = this;
            // 手势模型不在这里加载，延迟异步加载
        }

        private async void InitHandGestureAsync()
        {
            // 显示加载中提示，假定有LoadingText控件
           // if (LoadingText != null)
              //  LoadingText.Visibility = Visibility.Visible;
            await System.Threading.Tasks.Task.Run(() =>
            {
                handges = new HandGesture();
                handges.StartCamera();
            });
            _isHandGestureReady = true;
          //  if (LoadingText != null)
              //  LoadingText.Visibility = Visibility.Collapsed;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)//窗口载入后，开启游戏主循环
        {
            UpdateCenteredPosition();//炮塔居中
            _gameTimer.Start();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)//关闭时，停止
        {
            SaveScore();
            _gameTimer.Stop();
        }

        private void Page_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isMouse) return;
            if ((DateTime.Now - _lastRotationTime).TotalSeconds < MinRotationInterval)
                return;
            //_lastRotationTime = DateTime.Now;
            // 获取鼠标相对于炮塔的位置
            Point mousePos = e.GetPosition(GameCanvas);
            Point towerCenter = new Point(Canvas.GetLeft(TowerBase) + TowerBase.Width / 2, Canvas.GetTop(TowerBase) + TowerBase.Height / 2);
            // 计算角度
            double angle = Math.Atan2(mousePos.Y - towerCenter.Y, mousePos.X - towerCenter.X);
            angle = angle * 180 / Math.PI;
            // 旋转炮管
            //ScoreText.Text = $"当前分数: {_score} (最高分数: {TowerBase.Height})";

            Rotate.CenterX = TowerGun.Width / 2;
            Rotate.CenterY = TowerGun.Height / 2;
            Rotate.Angle = angle;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService?.CanGoBack == true)
            {
                NavigationService.GoBack();
            }
            else
            {
                MessageBox.Show("没有上一页了！");
            }
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            SpawnEnemies();
            MoveBullets();
            MoveEnemies();
            CheckCollisions();
            RemoveOffscreenObjects();

            if (!isMouse && HandGestureManager.Instance.IsReady)
            {
                if (handges == null)
                    handges = HandGestureManager.Instance.HandGestureInstance;
                if (handges != null)
                {
                    handges.OnFrame(CameraImage, ref __angle, sender, e);
                    Rotate.CenterX = TowerGun.Width / 2;
                    Rotate.CenterY = TowerGun.Height / 2;
                    Rotate.Angle = -__angle;
                }
            }
        }

        private void UpdateCenteredPosition()
        {
            // 计算 Canvas 的中心坐标
            double centerX = GameCanvas.ActualWidth / 2;
            double centerY = GameCanvas.ActualHeight / 2;
            //保证炮塔居中
            double elementLeft = centerX - (TowerBase.Width / 2);
            double elementTop = centerY - (TowerBase.Height / 2);

            // 设置 Canvas.Left 和 Canvas.Top
            Canvas.SetLeft(TowerBase, elementLeft);
            Canvas.SetTop(TowerBase, elementTop);
            Canvas.SetLeft(TowerGun, elementLeft);
            Canvas.SetTop(TowerGun, elementTop + 10);
        }

        private void SpawnEnemies()//敌人的生成
        {
            if ((DateTime.Now - _lastEnemySpawnTime).TotalSeconds < EnemySpawnInterval)
                return;//如果没到间隔时间，就不生成

            _lastEnemySpawnTime = DateTime.Now;

            // 随机选择从哪条边生成敌人
            int edge = _random.Next(4);
            double x = 0, y = 0;

            switch (edge)
            {
                case 0: // 上边
                    x = _random.NextDouble() * GameCanvas.ActualWidth;
                    y = -20;
                    break;
                case 1: // 右边
                    x = GameCanvas.ActualWidth + 20;
                    y = _random.NextDouble() * GameCanvas.ActualHeight;
                    break;
                case 2: // 下边
                    x = _random.NextDouble() * GameCanvas.ActualWidth;
                    y = GameCanvas.ActualHeight + 20;
                    break;
                case 3: // 左边
                    x = -20;
                    y = _random.NextDouble() * GameCanvas.ActualHeight;
                    break;
            }

            // 创建敌人
            int index = _random.Next(4);
            var enemy = new Image
            {
                Source = AllEnemy[index],
                Width = 30 + 10 * index,
                Height = 30 + 10 * index,
            };

            Canvas.SetLeft(enemy, x);
            Canvas.SetTop(enemy, y);
            GameCanvas.Children.Add(enemy);
            _enemies.Add(enemy);

            // 随机移动方向（朝向中心点附近）
            Point center = new Point(
                GameCanvas.ActualWidth / 2 + (_random.NextDouble() - 0.5) * 100,
                GameCanvas.ActualHeight / 2 + (_random.NextDouble() - 0.5) * 100);
            Vector direction = new Vector(center.X - x, center.Y - y);
            direction.Normalize();
            _enemyDirections.Add(direction);
        }

        private void MoveBullets()//子弹的移动
        {
            if (TowerGun.Visibility == Visibility.Collapsed && (DateTime.Now - _lastSeeTime).TotalSeconds >= RocketNoseeInterval)
                TowerGun.Visibility = Visibility.Visible;
            // 自动射击
            if ((DateTime.Now - _lastShotTime).TotalSeconds >= ShotInterval)
            {//如果到了间隔就生成子弹
                Shoot();
                _lastShotTime = DateTime.Now;
            }

            // 移动所有子弹
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                // 获取子弹和对应方向
                var bullet = _bullets[i];
                Vector direction = _bulletDirections[i];

                // 更新位置
                double newX = Canvas.GetLeft(bullet) + direction.X * BulletSpeed / 60.0;
                double newY = Canvas.GetTop(bullet) + direction.Y * BulletSpeed / 60.0;

                Canvas.SetLeft(bullet, newX);
                Canvas.SetTop(bullet, newY);
            }
        }

        private void Shoot()
        {
            // 创建子弹
            var bullet = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/Resources/static_rocket.png")),
                Width = TowerGun.Width,
                Height = TowerGun.Height,
                RenderTransform = new RotateTransform
                {
                    Angle = Rotate.Angle,
                    CenterX = TowerGun.Width / 2,  // 绝对坐标：Width * 0.5
                    CenterY = TowerGun.Height / 2 // 绝对坐标：Height * 0.8
                }
            };


            //从炮塔中心发射
            Canvas.SetLeft(bullet, Canvas.GetLeft(TowerGun));
            Canvas.SetTop(bullet, Canvas.GetTop(TowerGun));

            double angle = Rotate.Angle * Math.PI / 180;
            // 计算方向向量（单位向量）
            Vector direction = new Vector(Math.Cos(angle), Math.Sin(angle));

            GameCanvas.Children.Add(bullet);
            _bullets.Add(bullet);
            _bulletDirections.Add(direction);

            TowerGun.Visibility = Visibility.Collapsed;
            _lastSeeTime = DateTime.Now;
        }

        private void MoveEnemies()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                var direction = _enemyDirections[i];
                int rotate = _random.Next(3) + 1;
                // 随机改变方向（增加不可预测性）
                if (_random.NextDouble() < 0.02)
                {
                    direction.X += (_random.NextDouble() - 0.5) * 0.5;
                    direction.Y += (_random.NextDouble() - 0.5) * 0.5;
                    direction.Normalize();
                    _enemyDirections[i] = direction;
                }

                Canvas.SetLeft(enemy, Canvas.GetLeft(enemy) + direction.X * EnemySpeed / 60.0);
                Canvas.SetTop(enemy, Canvas.GetTop(enemy) + direction.Y * EnemySpeed / 60.0);

                var rotateTransform = enemy.RenderTransform as RotateTransform;
                if (rotateTransform == null)
                {
                    rotateTransform = new RotateTransform();
                    enemy.RenderTransform = rotateTransform;
                }

                // 角度增加
                rotateTransform.Angle += 1 * rotate;
            }
        }

        private void CheckCollisions()
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var bullet = _bullets[i];
                Rect bulletRect = new Rect(Canvas.GetLeft(bullet), Canvas.GetTop(bullet), bullet.Width, bullet.Height);

                for (int j = _enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = _enemies[j];
                    Rect enemyRect = new Rect(
                        Canvas.GetLeft(enemy),
                        Canvas.GetTop(enemy),
                        enemy.Width,
                        enemy.Height);

                    if (bulletRect.IntersectsWith(enemyRect))
                    {
                        // 命中敌人
                        //// 爆炸动画，定义当前帧索引和计时器
                        Image? animationImage = new Image
                        {
                            Width = 100,
                            Height = 100,
                        };
                        // 计算 referenceImage 的中心坐标
                        double CenterX = Canvas.GetLeft(enemy) + enemy.Width / 2;
                        double CenterY = Canvas.GetTop(enemy) + enemy.Height / 2;

                        // 设置 animationImage 的中心与 referenceImage 中心重合
                        Canvas.SetLeft(animationImage, CenterX - animationImage.Width / 2);
                        Canvas.SetTop(animationImage, CenterY - animationImage.Height / 2);
                        GameCanvas.Children.Add(animationImage);
                        int currentFrame = 0;
                        DispatcherTimer? timer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(1.0 / 12) // 12FPS
                        };

                        // 修改Tick事件逻辑
                        timer.Tick += (s, e) =>
                        {
                            // 更新当前帧
                            animationImage.Source = boom[currentFrame];
                            currentFrame++;
                            // 检查是否到达最后一帧
                            if (currentFrame >= boom.Count)
                            {
                                timer.Stop();  // 停止计时器
                                timer = null;  // 释放引用
                                animationImage.Source = null;
                                animationImage = null;
                            }
                        };

                        // 开始播放
                        timer.Start();

                        GameCanvas.Children.Remove(enemy);
                        _enemies.RemoveAt(j);
                        _enemyDirections.RemoveAt(j);

                        // 增加分数
                        _score++;
                        if (_score > _greatestScore) _greatestScore = _score;
                        ScoreText.Text = $"当前分数: {_score} (最高分数: {_greatestScore})";
                        break;
                    }
                }
            }
        }

        private void RemoveOffscreenObjects()
        {
            // 移除超出边界的子弹
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var bullet = _bullets[i];
                double left = Canvas.GetLeft(bullet);
                double top = Canvas.GetTop(bullet);

                if (left < -50 || left > GameCanvas.ActualWidth + 50 ||
                    top < -50 || top > GameCanvas.ActualHeight + 50)
                {
                    GameCanvas.Children.Remove(bullet);
                    _bullets.RemoveAt(i);
                    _bulletDirections.RemoveAt(i);
                }
            }

            // 移除超出边界的敌人
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                double left = Canvas.GetLeft(enemy);
                double top = Canvas.GetTop(enemy);

                if (left < -100 || left > GameCanvas.ActualWidth + 100 ||
                    top < -100 || top > GameCanvas.ActualHeight + 100)
                {
                    GameCanvas.Children.Remove(enemy);
                    _enemies.RemoveAt(i);
                    _enemyDirections.RemoveAt(i);
                }
            }
        }

        public void SaveScore() // 保存当前分数
        {
            using (var connection = new SQLiteConnection(App.ConnectionString))
            {
                connection.Open();

                // 创建表（如果不存在），添加Timestamp列用于记录保存时间
                using (var createTableCmd = new SQLiteCommand(
                    "CREATE TABLE IF NOT EXISTS GameScores (Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "Score INTEGER)",
                    connection))
                {
                    createTableCmd.ExecuteNonQuery();
                }

                // 插入新记录
                using (var insertCmd = new SQLiteCommand(
                    "INSERT INTO GameScores (Score) VALUES (@score)",
                    connection))
                {
                    insertCmd.Parameters.AddWithValue("@score", _greatestScore);
                    insertCmd.ExecuteNonQuery();
                }
            }
        }

        public static int LoadScore() // 载入最高分
        {
            using (var connection = new SQLiteConnection(App.ConnectionString))
            {
                connection.Open();

                // 检查表是否存在
                bool tableExists = false;
                using (var checkCmd = new SQLiteCommand(
                    "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='GameScores'",
                    connection))
                {
                    tableExists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
                }

                if (!tableExists)
                {
                    return 0;
                }

                // 查询最高分
                using (var selectCmd = new SQLiteCommand(
                    "SELECT MAX(Score) FROM GameScores",
                    connection))
                {
                    object result = selectCmd.ExecuteScalar();
                    return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
                }
            }
        }

        private void InitializeStarfield()
        {
            for (int i = 0; i < StarCount; i++)
            {
                CreateStar();
            }
        }

        private void CreateStar()
        {
            // 创建星光元素
            var star = new Ellipse
            {
                Width = _random.Next(1, 4),
                Height = _random.Next(1, 4),
                Fill = new SolidColorBrush(Colors.White),
                Opacity = _random.NextDouble() * 0.7 + 0.3 // 初始透明度30%~100%
            };

            // 随机位置
            Canvas.SetLeft(star, _random.Next(0, (int)StarfieldCanvas.ActualWidth));
            Canvas.SetTop(star, _random.Next(0, (int)StarfieldCanvas.ActualHeight));
            StarfieldCanvas.Children.Add(star);

            // 设置闪烁动画
            StartStarAnimation(star);
        }

        private void StartStarAnimation(Ellipse star)
        {
            // 透明度动画
            var opacityAnim = new DoubleAnimation
            {
                From = star.Opacity,
                To = _random.NextDouble() * 0.3,
                Duration = TimeSpan.FromSeconds(_random.NextDouble() * 3 + 1),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            // 大小动画
            var scaleAnim = new DoubleAnimation
            {
                From = 1,
                To = _random.NextDouble() * 0.5 + 0.8, // 80%~130%缩放
                Duration = TimeSpan.FromSeconds(_random.NextDouble() * 5 + 2),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            // 使用变换组
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform());
            star.RenderTransform = transformGroup;

            // 启动动画
            star.BeginAnimation(OpacityProperty, opacityAnim);
            star.RenderTransform.BeginAnimation(
                ScaleTransform.ScaleXProperty, scaleAnim);
            star.RenderTransform.BeginAnimation(
                ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private bool _isSettingsOpen;
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set { _isSettingsOpen = value; OnPropertyChanged(nameof(IsSettingsOpen)); }
        }

        // 游戏模式
        private bool _isMouseMode = true;
        public bool IsMouseMode
        {
            get => _isMouseMode;
            set
            {
                if (_isMouseMode != value)
                {
                    _isMouseMode = value;
                    if (value) IsGestureMode = false;
                    OnPropertyChanged(nameof(IsMouseMode));
                    isMouse = true;
                    CameraImage.Source = null;
                }
            }
        }

        private bool _isGestureMode;
        public bool IsGestureMode
        {
            get => _isGestureMode;
            set
            {
                if (_isGestureMode != value)
                {
                    _isGestureMode = value;
                    if (value)
                    {
                        IsMouseMode = false;
                        if (!_isHandGestureReady)
                            InitHandGestureAsync();
                    }
                    OnPropertyChanged(nameof(IsGestureMode));
                    isMouse = false;
                }
            }
        }

        // 出怪速度
        private bool _isFast;
        public bool IsFast
        {
            get => _isFast;
            set
            {
                if (_isFast != value)
                {
                    _isFast = value;
                    if (value)
                    {
                        IsMedium = false;
                        IsSlow = false;
                    }
                    OnPropertyChanged(nameof(IsFast));
                    EnemySpawnInterval = EnemyIntervals[0];
                }
            }
        }

        private bool _isMedium;
        public bool IsMedium
        {
            get => _isMedium;
            set
            {
                if (_isMedium != value)
                {
                    _isMedium = value;
                    if (value)
                    {
                        IsFast = false;
                        IsSlow = false;
                    }
                    OnPropertyChanged(nameof(IsMedium));
                    EnemySpawnInterval = EnemyIntervals[1];
                }
            }
        }

        private bool _isSlow = true;
        public bool IsSlow
        {
            get => _isSlow;
            set
            {
                if (_isSlow != value)
                {
                    _isSlow = value;
                    if (value)
                    {
                        IsFast = false;
                        IsMedium = false;
                    }
                    OnPropertyChanged(nameof(IsSlow));
                    EnemySpawnInterval = EnemyIntervals[2];
                }
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            IsSettingsOpen = true;
        }

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            IsSettingsOpen = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}