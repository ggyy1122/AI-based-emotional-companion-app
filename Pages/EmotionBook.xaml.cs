using GameApp.Services.HeartMemo;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace GameApp.Pages
{
    public partial class EmotionBook : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<Memo> _allMemos = new ObservableCollection<Memo>();
        public ObservableCollection<Memo> AllMemos
        {
            get => _allMemos;
            private set
            {
                _allMemos = value;
                OnPropertyChanged();
            }
        }

        // 修改CurrentMemo属性
        private Memo _currentMemo;
        public Memo CurrentMemo
        {
            get => _currentMemo;
            set
            {
                _currentMemo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecordingStatus)); // 添加这行
            }
        }


        public static IReadOnlyList<Brush> _availableColors { get; } = new List<Brush>
        {
            Brushes.Red, Brushes.Orange, Brushes.Yellow,
            Brushes.Green, Brushes.Blue, Brushes.Purple, Brushes.Pink
        };

        // 录音相关字段
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;
        private string _tempAudioPath;
        private DispatcherTimer _recordingTimer;
        private TimeSpan _recordingDuration;
        // 新增字段：用于跟踪每个备忘录的录音状态
        private Dictionary<int, string> _memoRecordingStatus = new Dictionary<int, string>();

        // 修改RecordingStatus属性
        private string _recordingStatus;
        public string RecordingStatus
        {
            get => _currentMemo != null && _memoRecordingStatus.TryGetValue(_currentMemo.Id, out var status)
                   ? status
                   : "准备录音";
            set
            {
                if (_currentMemo != null)
                {
                    _memoRecordingStatus[_currentMemo.Id] = value;
                    OnPropertyChanged();
                }
            }
        }
        public EmotionBook()
        {
            InitializeComponent();
            this.DataContext = this;
            LoadMemos();
            CurrentMemo = AllMemos.FirstOrDefault() ?? CreateNewMemo();
            InitRecording();
        }

        // 修改LoadMemos方法（在foreach循环内添加初始化状态）
        private void LoadMemos()
        {
            try
            {
                MemoDbService.EnsureTableExists();
                MemoVoiceDbService.EnsureTableExists();
                var memos = MemoDbService.GetAllMemos()
                    .OrderByDescending(m => m.Date)
                    .ToList();

                AllMemos = new ObservableCollection<Memo>(memos);

                foreach (var memo in AllMemos)
                {
                    memo.PropertyChanged += Memo_PropertyChanged;
                    memo.EmotionColor = _availableColors.FirstOrDefault(c => c.ToString() == memo.EmotionColor?.ToString())
                                        ?? _availableColors[0];
                    memo.Voices = new ObservableCollection<MemoVoice>(MemoVoiceDbService.GetVoicesByMemoId(memo.Id));

                    // 新增：初始化录音状态
                    _memoRecordingStatus[memo.Id] = "准备录音";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载备忘录失败: {ex.Message}");
                LoadTestData();
            }
        }


        private void Memo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Memo.Date))
            {
                ResortMemos();
            }
        }

        private void ResortMemos()
        {
            var sorted = AllMemos
                .OrderByDescending(m => m.Date)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (!ReferenceEquals(AllMemos[i], sorted[i]))
                {
                    AllMemos.Move(AllMemos.IndexOf(sorted[i]), i);
                }
            }
        }

        private void LoadTestData()
        {
            AllMemos = new ObservableCollection<Memo>
            {
                new Memo { Id=1, Title="开心的一天", Content="今天天气很好...", Date=DateTime.Now.AddHours(-2), EmotionColor=Brushes.Red },
                new Memo { Id=2, Title="项目进展", Content="完成了模块A...", Date=DateTime.Now.AddHours(-1), EmotionColor=Brushes.Green },
                new Memo { Id=3, Title="会议记录", Content="讨论了需求变更...", Date=DateTime.Now, EmotionColor=Brushes.Blue }
            };
            foreach (var memo in AllMemos)
            {
                memo.PropertyChanged += Memo_PropertyChanged;
            }
        }

        private Memo CreateNewMemo()
        {
            return new Memo
            {
                Title = "新备忘录",
                Content = "",
                EmotionColor = _availableColors[0],
                Date = DateTime.Now
            };
        }

        private void SaveMemo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentMemo.Title))
                {
                    MessageBox.Show("请输入备忘录标题");
                    return;
                }

                CurrentMemo.Date = DateTime.Now;

                if (CurrentMemo.Id == 0)
                {
                    CurrentMemo.Id = MemoDbService.SaveMemo(CurrentMemo);
                    CurrentMemo.PropertyChanged += Memo_PropertyChanged;
                    AllMemos.Insert(0, CurrentMemo);
                }
                else
                {
                    MemoDbService.UpdateMemo(CurrentMemo);
                }

                if (!string.IsNullOrEmpty(_tempAudioPath) && File.Exists(_tempAudioPath))
                {
                    AddRecordingToMemo();
                }

                MessageBox.Show("保存成功！");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}");
            }
        }

        private void MemoListItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Memo memo)
            {
                CurrentMemo = memo;
            }
        }

        private void NewMemo_Click(object sender, RoutedEventArgs e)
        {
            var newMemo = CreateNewMemo();
            CurrentMemo = newMemo;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

        private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            if (SidebarColumn.Width.Value == 0)
            {
                SidebarColumn.Width = new GridLength(250);
            }
            else
            {
                SidebarColumn.Width = new GridLength(0);
            }
        }

        // 修改InitRecording方法中的计时器回调
        private void InitRecording()
        {
            _recordingTimer = new DispatcherTimer();
            _recordingTimer.Interval = TimeSpan.FromSeconds(1);
            _recordingTimer.Tick += (s, e) =>
            {
                _recordingDuration = _recordingDuration.Add(TimeSpan.FromSeconds(1));
                RecordingStatus = $"录音中... {_recordingDuration:mm\\:ss}";
            };
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_waveIn == null)
            {
                StartRecording();
                RecordButton.Content = "⏹ 停止";
                RecordButton.Background = Brushes.Red;
            }
            else
            {
                StopRecording();
                RecordButton.Content = "🎤 录音";
                RecordButton.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF673AB7");
            }
        }

        private void StartRecording()
        {
            try
            {
                // 修改1：直接创建.wav文件
                _tempAudioPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");

                // 修改2：确保目录存在
                Directory.CreateDirectory(Path.GetDirectoryName(_tempAudioPath));

                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(44100, 1)
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                // 直接创建.wav文件
                _writer = new WaveFileWriter(_tempAudioPath, _waveIn.WaveFormat);
                _waveIn.StartRecording();

                _recordingDuration = TimeSpan.Zero;
                _recordingTimer.Start();
                RecordingStatus = "录音中... 00:00";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"开始录音失败: {ex.Message}");
                CleanupRecording();
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            _writer.Write(e.Buffer, 0, e.BytesRecorded);
            _writer.Flush();
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            _recordingTimer.Stop();
            CleanupRecording();

            if (e.Exception != null)
            {
                MessageBox.Show($"录音错误: {e.Exception.Message}");
                return;
            }

            if (File.Exists(_tempAudioPath))
            {
                AddRecordingToMemo();
            }
        }

        private void StopRecording()
        {
            _waveIn?.StopRecording();
        }

        private void CleanupRecording()
        {
            _writer?.Dispose();
            _writer = null;
            _waveIn?.Dispose();
            _waveIn = null;
        }

        private void AddRecordingToMemo()
        {
            try
            {
                if (string.IsNullOrEmpty(_tempAudioPath) || !File.Exists(_tempAudioPath))
                {
                    MessageBox.Show("临时录音文件不存在");
                    return;
                }

                // 确保是.wav文件
                if (!_tempAudioPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    var newPath = _tempAudioPath + ".wav";
                    File.Move(_tempAudioPath, newPath);
                    _tempAudioPath = newPath;
                }

                // 将完整路径传给服务层
                var voice = MemoVoiceDbService.AddVoice(CurrentMemo.Id, _tempAudioPath);

                if (voice != null)
                {
                    CurrentMemo.Voices.Add(voice);
                    RecordingStatus = "录音已保存";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存录音失败: {ex.Message}");
            }
            finally
            {
                try { if (File.Exists(_tempAudioPath)) File.Delete(_tempAudioPath); }
                catch { /* 静默处理 */ }
                _tempAudioPath = null;
            }
        }

        private WaveOutEvent _currentPlayer; // 新增字段用于保持播放器实例

        private void PlayVoice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MemoVoice voice)
            {
                try
                {
                    // 停止当前正在播放的音频
                    _currentPlayer?.Stop();
                    _currentPlayer?.Dispose();

                    // 获取完整路径
                    var fullPath = MemoVoiceDbService.GetVoiceFullPath(voice.VoicePath);
                    if (!File.Exists(fullPath))
                    {
                        RecordingStatus = "录音文件丢失";
                        MessageBox.Show($"找不到录音文件\n路径：{fullPath}");
                        return;
                    }

                    // 创建新的播放器实例
                    _currentPlayer = new WaveOutEvent();
                    var reader = new WaveFileReader(fullPath); // 不要放在using块中

                    RecordingStatus = $"正在播放: {Path.GetFileNameWithoutExtension(voice.VoicePath)}";

                    _currentPlayer.Init(reader);
                    _currentPlayer.PlaybackStopped += (s, args) =>
                    {
                        reader.Dispose(); // 在这里释放reader
                        RecordingStatus = "播放完成";
                    };

                    _currentPlayer.Play();
                }
                catch (Exception ex)
                {
                    RecordingStatus = "播放失败";
                    MessageBox.Show($"播放失败: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
        private void DeleteVoice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MemoVoice voice)
            {
                if (MessageBox.Show("确定删除此录音吗？", "确认", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    if (MemoVoiceDbService.DeleteVoiceWithFile(voice.Id))
                    {
                        CurrentMemo.Voices.Remove(voice);
                        MessageBox.Show("录音已删除");
                    }
                    else
                    {
                        MessageBox.Show("删除失败");
                    }
                }
            }
        }
        // 在类中添加删除方法（放在DeleteVoice_Click方法附近）
        private void DeleteMemo_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentMemo == null) return;

            if (MessageBox.Show($"确定要永久删除备忘录【{CurrentMemo.Title}】吗？",
                               "确认删除",
                               MessageBoxButton.YesNo,
                               MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {

                    MemoDbService.DeleteMemoWithVoices(CurrentMemo.Id);


                    AllMemos.Remove(CurrentMemo);


                    CurrentMemo = AllMemos.FirstOrDefault() ?? CreateNewMemo();

                    MessageBox.Show("删除成功");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
