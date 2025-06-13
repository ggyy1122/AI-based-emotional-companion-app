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
        private DispatcherTimer _gameTimer;
        private readonly Random _random = new Random();

        private readonly List<Image> _bullets = new List<Image>();
        private readonly List<Image> _enemies = new List<Image>();
        private readonly List<Vector> _bulletDirections = new List<Vector>();
        private readonly List<Vector> _enemyDirections = new List<Vector>();

        private readonly List<BitmapImage> AllEnemy = new List<BitmapImage>();

        private int _score = 0;
        private int _greatestScore = 0;

        private const double ShotInterval = 1.5;
        private const double BulletSpeed = 300;
        private const double EnemySpeed = 100;
        private double EnemySpawnInterval = 0.0;
        private static readonly double[] EnemyIntervals = new double[] { 0.5, 1.0, 2.0 };

        private DateTime _lastShotTime = DateTime.MinValue;
        private DateTime _lastEnemySpawnTime = DateTime.MinValue;

        private const int StarCount = 300;

        private DateTime _lastRotationTime = DateTime.MinValue;
        private const double MinRotationInterval = 1.0;

        private const double RocketNoseeInterval = 0.75;
        private DateTime _lastSeeTime = DateTime.MinValue;

        List<BitmapImage> boom = new List<BitmapImage>();

        private const string RESOURCES_PATH = "pack://application:,,,/Resources/";

        // Camera and gesture handling with safety
        private HandGesture handges = null;
        private bool _cameraInitialized = false;
        private bool _cameraFailed = false;

        private double __angle;
        private bool isMouse = true;

        public GamePage()
        {
            try
            {
                InitializeComponent();

                // Initialize game timer
                InitializeGameTimer();

                // Initialize score
                InitializeScore();

                // Load game resources
                LoadGameResources();

                // Initialize starfield
                Loaded += (s, e) => InitializeStarfield();

                // Set data context
                DataContext = this;

                // Initialize camera with safety
                InitializeCameraWithFallback();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Game initialization failed: {ex.Message}\n\nThe game will run in mouse mode.",
                               "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Force mouse mode
                SetMouseMode(true);
            }
        }

        private void InitializeGameTimer()
        {
            try
            {
                _gameTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.0 / 60.0)
                };
                _gameTimer.Tick += GameLoop;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize game timer: {ex.Message}");
            }
        }

        private void InitializeScore()
        {
            try
            {
                _greatestScore = LoadScore();
                ScoreText.Text = $"Current Score: {_score} (High Score: {_greatestScore})";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Score initialization failed: {ex.Message}");
                _greatestScore = 0;
                ScoreText.Text = $"Current Score: {_score} (High Score: 0)";
            }
        }

        private void LoadGameResources()
        {
            try
            {
                // Load explosion images
                for (int i = 0; i < 7; i++)
                {
                    boom.Add(new BitmapImage(new Uri($"pack://application:,,,/Resources/boom{i}.png")));
                }

                // Load enemy images
                for (int i = 0; i < 4; i++)
                {
                    AllEnemy.Add(new BitmapImage(new Uri($"pack://application:,,,/Resources/monster{i}.png")));
                }

                EnemySpawnInterval = EnemyIntervals[2];
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load game resources: {ex.Message}\n\nSome graphics may not display correctly.",
                               "Resource Loading Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Create fallback resources if needed
                CreateFallbackResources();
            }
        }

        private void CreateFallbackResources()
        {
            try
            {
                // Create simple fallback images if resources fail to load
                if (boom.Count == 0)
                {
                    // Add at least one fallback boom image
                    for (int i = 0; i < 7; i++)
                    {
                        boom.Add(null); // Will be handled in animation code
                    }
                }

                if (AllEnemy.Count == 0)
                {
                    // Add fallback enemy images
                    for (int i = 0; i < 4; i++)
                    {
                        AllEnemy.Add(null); // Will be handled in enemy creation
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fallback resource creation failed: {ex.Message}");
            }
        }

        private void InitializeCameraWithFallback()
        {
            try
            {
                // Check if camera hardware is available
                if (!CheckCameraAvailability())
                {
                    SetMouseMode(true);
                    return;
                }

                handges = new HandGesture();
                handges.StartCamera();
                _cameraInitialized = true;
                _cameraFailed = false;

                Debug.WriteLine("Camera initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Camera initialization failed: {ex.Message}");

                MessageBox.Show(
                    "Camera initialization failed. The game will use mouse control mode.\n\n" +
                    "Possible reasons:\n" +
                    "1. No camera hardware detected\n" +
                    "2. Camera is being used by another application\n" +
                    "3. Missing camera drivers\n" +
                    "4. Insufficient permissions",
                    "Camera Unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                SetMouseMode(true);
                _cameraFailed = true;
                handges = null;
            }
        }

        private bool CheckCameraAvailability()
        {
            try
            {
                // Simple check for camera availability
                // This is a basic implementation - you might want to use more sophisticated detection
                return true; // Assume available unless proven otherwise
            }
            catch
            {
                return false;
            }
        }

        private void SetMouseMode(bool forceMouseMode)
        {
            try
            {
                IsMouseMode = true;
                IsGestureMode = false;
                isMouse = true;

                // Hide camera related UI elements
                if (CameraImage != null)
                {
                    CameraImage.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set mouse mode: {ex.Message}");
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateCenteredPosition();
                _gameTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start game: {ex.Message}", "Game Start Error");
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveScore();
                _gameTimer?.Stop();

                // Clean up camera resources
                CleanupCameraResources();

                // Clean up UI resources
                GameCanvas?.Children.Clear();
                StarfieldCanvas?.Children.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Page unload cleanup failed: {ex.Message}");
            }
        }

        private void CleanupCameraResources()
        {
            try
            {
                if (handges != null)
                {
                    // If HandGesture has a disposal method, call it
                    if (handges is IDisposable disposableHandges)
                    {
                        disposableHandges.Dispose();
                    }
                    handges = null;
                }
                _cameraInitialized = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Camera cleanup failed: {ex.Message}");
            }
        }

        private void Page_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isMouse) return;

            try
            {
                if ((DateTime.Now - _lastRotationTime).TotalSeconds < MinRotationInterval)
                    return;

                Point mousePos = e.GetPosition(GameCanvas);
                Point towerCenter = new Point(Canvas.GetLeft(TowerBase) + TowerBase.Width / 2,
                                            Canvas.GetTop(TowerBase) + TowerBase.Height / 2);

                double angle = Math.Atan2(mousePos.Y - towerCenter.Y, mousePos.X - towerCenter.X);
                angle = angle * 180 / Math.PI;

                Rotate.CenterX = TowerGun.Width / 2;
                Rotate.CenterY = TowerGun.Height / 2;
                Rotate.Angle = angle;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mouse move handling failed: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (NavigationService?.CanGoBack == true)
                {
                    NavigationService.GoBack();
                }
                else
                {
                    MessageBox.Show("No previous page available!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation failed: {ex.Message}");
            }
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            try
            {
                SpawnEnemies();
                MoveBullets();
                MoveEnemies();
                CheckCollisions();
                RemoveOffscreenObjects();

                // Handle camera/gesture input with safety
                HandleCameraInput(sender, e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Game loop error: {ex.Message}");
                // Don't stop the game, just log the error
            }
        }

        private void HandleCameraInput(object sender, EventArgs e)
        {
            if (!isMouse && handges != null && _cameraInitialized && !_cameraFailed)
            {
                try
                {
                    handges.OnFrame(CameraImage, ref __angle, sender, e);
                    Rotate.CenterX = TowerGun.Width / 2;
                    Rotate.CenterY = TowerGun.Height / 2;
                    Rotate.Angle = -__angle;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Camera frame processing error: {ex.Message}");

                    // Switch to mouse mode on camera error
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("Camera error detected. Switching to mouse mode.",
                                       "Camera Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        SetMouseMode(true);
                        _cameraFailed = true;
                    }));
                }
            }
        }

        private void UpdateCenteredPosition()
        {
            try
            {
                double centerX = GameCanvas.ActualWidth / 2;
                double centerY = GameCanvas.ActualHeight / 2;

                double elementLeft = centerX - (TowerBase.Width / 2);
                double elementTop = centerY - (TowerBase.Height / 2);

                Canvas.SetLeft(TowerBase, elementLeft);
                Canvas.SetTop(TowerBase, elementTop);
                Canvas.SetLeft(TowerGun, elementLeft);
                Canvas.SetTop(TowerGun, elementTop + 10);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update centered position: {ex.Message}");
            }
        }

        private void SpawnEnemies()
        {
            try
            {
                if ((DateTime.Now - _lastEnemySpawnTime).TotalSeconds < EnemySpawnInterval)
                    return;

                _lastEnemySpawnTime = DateTime.Now;

                int edge = _random.Next(4);
                double x = 0, y = 0;

                switch (edge)
                {
                    case 0: // Top
                        x = _random.NextDouble() * GameCanvas.ActualWidth;
                        y = -20;
                        break;
                    case 1: // Right
                        x = GameCanvas.ActualWidth + 20;
                        y = _random.NextDouble() * GameCanvas.ActualHeight;
                        break;
                    case 2: // Bottom
                        x = _random.NextDouble() * GameCanvas.ActualWidth;
                        y = GameCanvas.ActualHeight + 20;
                        break;
                    case 3: // Left
                        x = -20;
                        y = _random.NextDouble() * GameCanvas.ActualHeight;
                        break;
                }

                int index = _random.Next(4);
                var enemy = new Image
                {
                    Source = AllEnemy[index], // This might be null for fallback
                    Width = 30 + 10 * index,
                    Height = 30 + 10 * index,
                };

                // Fallback if image source is null
                if (enemy.Source == null)
                {
                    // Create a simple colored rectangle as fallback
                    enemy.Source = CreateFallbackEnemyImage();
                }

                Canvas.SetLeft(enemy, x);
                Canvas.SetTop(enemy, y);
                GameCanvas.Children.Add(enemy);
                _enemies.Add(enemy);

                Point center = new Point(
                    GameCanvas.ActualWidth / 2 + (_random.NextDouble() - 0.5) * 100,
                    GameCanvas.ActualHeight / 2 + (_random.NextDouble() - 0.5) * 100);
                Vector direction = new Vector(center.X - x, center.Y - y);
                direction.Normalize();
                _enemyDirections.Add(direction);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Enemy spawn failed: {ex.Message}");
            }
        }

        private BitmapImage CreateFallbackEnemyImage()
        {
            try
            {
                // Create a simple colored bitmap as fallback
                // This is a simplified approach - you might want to create actual bitmap
                return new BitmapImage(new Uri("pack://application:,,,/Resources/fallback.png"));
            }
            catch
            {
                return null; // Will result in invisible enemy, but game continues
            }
        }

        private void MoveBullets()
        {
            try
            {
                if (TowerGun.Visibility == Visibility.Collapsed &&
                    (DateTime.Now - _lastSeeTime).TotalSeconds >= RocketNoseeInterval)
                    TowerGun.Visibility = Visibility.Visible;

                if ((DateTime.Now - _lastShotTime).TotalSeconds >= ShotInterval)
                {
                    Shoot();
                    _lastShotTime = DateTime.Now;
                }

                for (int i = _bullets.Count - 1; i >= 0; i--)
                {
                    var bullet = _bullets[i];
                    Vector direction = _bulletDirections[i];

                    double newX = Canvas.GetLeft(bullet) + direction.X * BulletSpeed / 60.0;
                    double newY = Canvas.GetTop(bullet) + direction.Y * BulletSpeed / 60.0;

                    Canvas.SetLeft(bullet, newX);
                    Canvas.SetTop(bullet, newY);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bullet movement failed: {ex.Message}");
            }
        }

        private void Shoot()
        {
            try
            {
                var bullet = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Resources/static_rocket.png")),
                    Width = TowerGun.Width,
                    Height = TowerGun.Height,
                    RenderTransform = new RotateTransform
                    {
                        Angle = Rotate.Angle,
                        CenterX = TowerGun.Width / 2,
                        CenterY = TowerGun.Height / 2
                    }
                };

                Canvas.SetLeft(bullet, Canvas.GetLeft(TowerGun));
                Canvas.SetTop(bullet, Canvas.GetTop(TowerGun));

                double angle = Rotate.Angle * Math.PI / 180;
                Vector direction = new Vector(Math.Cos(angle), Math.Sin(angle));

                GameCanvas.Children.Add(bullet);
                _bullets.Add(bullet);
                _bulletDirections.Add(direction);

                TowerGun.Visibility = Visibility.Collapsed;
                _lastSeeTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Shooting failed: {ex.Message}");
            }
        }

        private void MoveEnemies()
        {
            try
            {
                for (int i = 0; i < _enemies.Count; i++)
                {
                    var enemy = _enemies[i];
                    var direction = _enemyDirections[i];
                    int rotate = _random.Next(3) + 1;

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

                    rotateTransform.Angle += 1 * rotate;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Enemy movement failed: {ex.Message}");
            }
        }

        private void CheckCollisions()
        {
            try
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
                            CreateExplosionAnimation(enemy);

                            GameCanvas.Children.Remove(enemy);
                            _enemies.RemoveAt(j);
                            _enemyDirections.RemoveAt(j);

                            _score++;
                            if (_score > _greatestScore) _greatestScore = _score;
                            ScoreText.Text = $"Current Score: {_score} (High Score: {_greatestScore})";
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Collision detection failed: {ex.Message}");
            }
        }

        private void CreateExplosionAnimation(Image enemy)
        {
            try
            {
                Image animationImage = new Image
                {
                    Width = 100,
                    Height = 100,
                };

                double CenterX = Canvas.GetLeft(enemy) + enemy.Width / 2;
                double CenterY = Canvas.GetTop(enemy) + enemy.Height / 2;

                Canvas.SetLeft(animationImage, CenterX - animationImage.Width / 2);
                Canvas.SetTop(animationImage, CenterY - animationImage.Height / 2);
                GameCanvas.Children.Add(animationImage);

                int currentFrame = 0;
                DispatcherTimer timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.0 / 12)
                };

                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (currentFrame < boom.Count && boom[currentFrame] != null)
                        {
                            animationImage.Source = boom[currentFrame];
                        }
                        currentFrame++;

                        if (currentFrame >= boom.Count)
                        {
                            timer.Stop();
                            GameCanvas.Children.Remove(animationImage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Explosion animation frame failed: {ex.Message}");
                        timer.Stop();
                        GameCanvas.Children.Remove(animationImage);
                    }
                };

                timer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Explosion animation creation failed: {ex.Message}");
            }
        }

        private void RemoveOffscreenObjects()
        {
            try
            {
                // Remove off-screen bullets
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

                // Remove off-screen enemies
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Offscreen object removal failed: {ex.Message}");
            }
        }

        public void SaveScore()
        {
            try
            {
                using (var connection = new SQLiteConnection(App.ConnectionString))
                {
                    connection.Open();

                    using (var createTableCmd = new SQLiteCommand(
                        "CREATE TABLE IF NOT EXISTS GameScores (Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                        "Score INTEGER)",
                        connection))
                    {
                        createTableCmd.ExecuteNonQuery();
                    }

                    using (var insertCmd = new SQLiteCommand(
                        "INSERT INTO GameScores (Score) VALUES (@score)",
                        connection))
                    {
                        insertCmd.Parameters.AddWithValue("@score", _greatestScore);
                        insertCmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Score saving failed: {ex.Message}");
            }
        }

        public static int LoadScore()
        {
            try
            {
                using (var connection = new SQLiteConnection(App.ConnectionString))
                {
                    connection.Open();

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

                    using (var selectCmd = new SQLiteCommand(
                        "SELECT MAX(Score) FROM GameScores",
                        connection))
                    {
                        object result = selectCmd.ExecuteScalar();
                        return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Score loading failed: {ex.Message}");
                return 0;
            }
        }

        private void InitializeStarfield()
        {
            try
            {
                for (int i = 0; i < StarCount; i++)
                {
                    CreateStar();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Starfield initialization failed: {ex.Message}");
            }
        }

        private void CreateStar()
        {
            try
            {
                var star = new Ellipse
                {
                    Width = _random.Next(1, 4),
                    Height = _random.Next(1, 4),
                    Fill = new SolidColorBrush(Colors.White),
                    Opacity = _random.NextDouble() * 0.7 + 0.3
                };

                Canvas.SetLeft(star, _random.Next(0, (int)StarfieldCanvas.ActualWidth));
                Canvas.SetTop(star, _random.Next(0, (int)StarfieldCanvas.ActualHeight));
                StarfieldCanvas.Children.Add(star);

                StartStarAnimation(star);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Star creation failed: {ex.Message}");
            }
        }

        private void StartStarAnimation(Ellipse star)
        {
            try
            {
                var opacityAnim = new DoubleAnimation
                {
                    From = star.Opacity,
                    To = _random.NextDouble() * 0.3,
                    Duration = TimeSpan.FromSeconds(_random.NextDouble() * 3 + 1),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };

                var scaleAnim = new DoubleAnimation
                {
                    From = 1,
                    To = _random.NextDouble() * 0.5 + 0.8,
                    Duration = TimeSpan.FromSeconds(_random.NextDouble() * 5 + 2),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };

                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform());
                star.RenderTransform = transformGroup;

                star.BeginAnimation(OpacityProperty, opacityAnim);
                star.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                star.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Star animation failed: {ex.Message}");
            }
        }

        // Properties with exception handling
        private bool _isSettingsOpen;
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set { _isSettingsOpen = value; OnPropertyChanged(nameof(IsSettingsOpen)); }
        }

        private bool _isMouseMode = true;
        public bool IsMouseMode
        {
            get => _isMouseMode;
            set
            {
                try
                {
                    if (_isMouseMode != value)
                    {
                        _isMouseMode = value;
                        if (value) IsGestureMode = false;
                        OnPropertyChanged(nameof(IsMouseMode));
                        isMouse = true;
                        if (CameraImage != null)
                            CameraImage.Source = null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"IsMouseMode setter failed: {ex.Message}");
                }
            }
        }

        private bool _isGestureMode;
        public bool IsGestureMode
        {
            get => _isGestureMode;
            set
            {
                try
                {
                    if (_isGestureMode != value)
                    {
                        _isGestureMode = value;
                        if (value) IsMouseMode = false;
                        OnPropertyChanged(nameof(IsGestureMode));
                        isMouse = false;

                        // Re-initialize camera if needed and not failed
                        if (value && !_cameraInitialized && !_cameraFailed)
                        {
                            InitializeCameraWithFallback();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"IsGestureMode setter failed: {ex.Message}");
                    // Force back to mouse mode
                    _isGestureMode = false;
                    IsMouseMode = true;
                }
            }
        }

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
            try
            {
                IsSettingsOpen = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Settings button click failed: {ex.Message}");
            }
        }

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsSettingsOpen = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Close settings button click failed: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            try
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PropertyChanged notification failed: {ex.Message}");
            }
        }
    }
}
