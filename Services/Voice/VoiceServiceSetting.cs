using System.ComponentModel;

namespace GameApp.Services.Voice
{
    public class VoiceServiceSetting : INotifyPropertyChanged
    {
        // Baidu API Settings
        private string _baiduApiKey = "TNaUadU6ClVo0BXcZntAVCjN";
        private string _baiduSecretKey = "EWQvLl3rSulum44V8cwoIvJ4edqkbizz";
        private string _baiduToken = "";

        // Audio Settings
        private int _sampleRate = 16000;
        private int _channels = 1;
        private int _bitsPerSample = 16;
        private string _audioFormat = "pcm";

        // Recognition Settings
        private string _language = "zh";
        private bool _enablePunctuation = true;
        private bool _enableWordInfo = false;

        // Recording Settings
        private int _recordingTimeoutMs = 10000; // 10 seconds max
        private double _silenceThreshold = 0.01;
        private int _silenceDurationMs = 2000; // 2 seconds silence to stop

        public string BaiduApiKey
        {
            get => _baiduApiKey;
            set { _baiduApiKey = value; OnPropertyChanged(nameof(BaiduApiKey)); }
        }

        public string BaiduSecretKey
        {
            get => _baiduSecretKey;
            set { _baiduSecretKey = value; OnPropertyChanged(nameof(BaiduSecretKey)); }
        }

        public string BaiduToken
        {
            get => _baiduToken;
            set { _baiduToken = value; OnPropertyChanged(nameof(BaiduToken)); }
        }

        public int SampleRate
        {
            get => _sampleRate;
            set { _sampleRate = value; OnPropertyChanged(nameof(SampleRate)); }
        }

        public int Channels
        {
            get => _channels;
            set { _channels = value; OnPropertyChanged(nameof(Channels)); }
        }

        public int BitsPerSample
        {
            get => _bitsPerSample;
            set { _bitsPerSample = value; OnPropertyChanged(nameof(BitsPerSample)); }
        }

        public string AudioFormat
        {
            get => _audioFormat;
            set { _audioFormat = value; OnPropertyChanged(nameof(AudioFormat)); }
        }

        public string Language
        {
            get => _language;
            set { _language = value; OnPropertyChanged(nameof(Language)); }
        }

        public bool EnablePunctuation
        {
            get => _enablePunctuation;
            set { _enablePunctuation = value; OnPropertyChanged(nameof(EnablePunctuation)); }
        }

        public bool EnableWordInfo
        {
            get => _enableWordInfo;
            set { _enableWordInfo = value; OnPropertyChanged(nameof(EnableWordInfo)); }
        }

        public int RecordingTimeoutMs
        {
            get => _recordingTimeoutMs;
            set { _recordingTimeoutMs = value; OnPropertyChanged(nameof(RecordingTimeoutMs)); }
        }

        public double SilenceThreshold
        {
            get => _silenceThreshold;
            set { _silenceThreshold = value; OnPropertyChanged(nameof(SilenceThreshold)); }
        }

        public int SilenceDurationMs
        {
            get => _silenceDurationMs;
            set { _silenceDurationMs = value; OnPropertyChanged(nameof(SilenceDurationMs)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
