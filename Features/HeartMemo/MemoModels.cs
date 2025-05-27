using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace GameApp.Features.HeartMemo
{
     public class Memo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<MemoVoice> PendingVoices { get; set; } = new ObservableCollection<MemoVoice>();



        private string _title;
        private Brush _emotionColor;
        private DateTime _date; // 添加日期字段

        public int Id { get; set; }

        public DateTime Date
        {
            get => _date;
            set
            {
                if (_date != value)
                {
                    _date = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedDate)); // 通知格式化日期
                }
            }
        }

        // 新增格式化日期属性（供XAML直接绑定）
        public string FormattedDate => Date.ToString("yyyy-MM-dd HH:mm");

        public string Title
        {
            get => _title ?? "无标题";
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PreviewText));
                }
            }
        }

        public string Content { get; set; }

        public Brush EmotionColor
        {
            get => _emotionColor ?? Brushes.Blue;
            set
            {
                if (_emotionColor != value)
                {
                    _emotionColor = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ColorString));
                }
            }
        }

        public ObservableCollection<MemoVoice> Voices { get; set; } = new ObservableCollection<MemoVoice>();

        // 数据库兼容的颜色字符串
        public string ColorString
        {
            get => (EmotionColor as SolidColorBrush)?.Color.ToString() ?? "#FF0000FF";
            set
            {
                try
                {
                    var newColor = (Color)ColorConverter.ConvertFromString(value);
                    if (EmotionColor is not SolidColorBrush brush || brush.Color != newColor)
                    {
                        EmotionColor = new SolidColorBrush(newColor);
                    }
                }
                catch
                {
                    EmotionColor = Brushes.Blue;
                }
            }
        }

        public string PreviewText => Title.Length > 20 ? Title.Substring(0, 20) + "..." : Title;

        public bool HasVoice => Voices.Any();

        // 加载语音列表
        public void LoadVoices()
        {
            Voices.Clear();
            foreach (var voice in MemoVoiceDbService.GetVoicesByMemoId(Id))
            {
                Voices.Add(voice);
            }
            OnPropertyChanged(nameof(HasVoice));
        }

        // 添加新语音
        public void AddVoice(string voiceFilePath)
        {
            var newVoice = MemoVoiceDbService.AddVoice(this.Id, voiceFilePath);
            if (newVoice != null)
            {
                Voices.Add(newVoice);
                OnPropertyChanged(nameof(HasVoice));
            }
        }

        // 删除指定语音
        public bool RemoveVoice(MemoVoice voice)
        {
            if (MemoVoiceDbService.DeleteVoice(voice.Id))
            {
                Voices.Remove(voice);
                OnPropertyChanged(nameof(HasVoice));
                return true;
            }
            return false;
        }

        // 添加调试信息的属性通知
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
           
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 新增：深拷贝方法（如需对象替换时使用）
        public Memo Clone()
        {
            return new Memo
            {
                Id = this.Id,
                Title = this.Title,
                Content = this.Content,
                Date = this.Date,
                EmotionColor = this.EmotionColor,
                // Voices需要单独处理...
            };
        }

        // 新增：数据验证属性
        public bool IsValid => !string.IsNullOrWhiteSpace(Title) && 
                             !string.IsNullOrWhiteSpace(Content) &&
                             EmotionColor != null;
    }

    public class MemoVoice : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _voicePath;

        public int Id { get; set; }
        public int MemoId { get; set; }

        public string VoicePath
        {
            get => _voicePath;
            set
            {
                _voicePath = value;
                OnPropertyChanged();
            }
        }

        // 语音时长（示例属性，可按需实现）
        public TimeSpan Duration { get; set; }

        // 语音文件名（方便显示）
        public string FileName => System.IO.Path.GetFileName(VoicePath);

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}