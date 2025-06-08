using System.Windows;
using GameApp.Services.Voice;
using System.Windows.Media;
using System;

namespace GameApp.Pages
{
    public partial class VoiceInputDialog : Window
    {
        public string MessageText { get; set; }
        public bool SendMessage { get; private set; }

        private bool isPlaceholderText = true;
        private VoiceService _voiceService;
        private bool isListening = false;

        public VoiceInputDialog()
        {
            InitializeComponent();
            MessageText = string.Empty;
            SendMessage = false;

            // Initialize voice service
            InitializeVoiceService();

            // Set focus to text box
            RecognizedTextBox.Focus();

            // Automatically start voice recognition after initialization
            StartVoiceRecognitionAutomatically();
        }

        private void InitializeVoiceService()
        {
            try
            {
                _voiceService = new VoiceService();
                _voiceService.StatusChanged += OnVoiceStatusChanged;
                _voiceService.SpeechRecognized += OnSpeechRecognized;
                _voiceService.ListeningStateChanged += OnListeningStateChanged;

                if (!_voiceService.IsInitialized)
                {
                    MessageBox.Show("Warning: Voice service not initialized", "Voice Service",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Voice service initialization failed: {ex.Message}",
                               "Voice Service Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Automatically start voice recognition when dialog opens
        /// </summary>
        private void StartVoiceRecognitionAutomatically()
        {
            // Use a small delay to ensure the dialog is fully loaded
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_voiceService != null && _voiceService.IsInitialized && !isListening)
                {
                    try
                    {
                        _voiceService.StartListening();
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Failed to start voice recognition automatically: {ex.Message}",
                                       "Voice Recognition Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnVoiceStatusChanged(string status)
        {
            Dispatcher.Invoke(() =>
            {
                // Status change handling without logging
            });
        }

        private void OnSpeechRecognized(string recognizedText)
        {
            Dispatcher.Invoke(() =>
            {
                // Only log for recognized speech
                Console.WriteLine($"*** Speech Recognized: '{recognizedText}' ***");

                if (string.IsNullOrWhiteSpace(recognizedText))
                {
                    return;
                }

                // Clear placeholder text if present
                if (isPlaceholderText)
                {
                    RecognizedTextBox.Text = string.Empty;
                    isPlaceholderText = false;
                    RecognizedTextBox.Foreground = Brushes.Black;
                }

                // Append recognized text
                if (string.IsNullOrWhiteSpace(RecognizedTextBox.Text))
                {
                    RecognizedTextBox.Text = recognizedText;
                }
                else
                {
                    RecognizedTextBox.Text += " " + recognizedText;
                }

                // Move cursor to end
                RecognizedTextBox.CaretIndex = RecognizedTextBox.Text.Length;
                RecognizedTextBox.Focus();
            });
        }

        private void OnListeningStateChanged(bool listening)
        {
            Dispatcher.Invoke(() =>
            {
                isListening = listening;
                UpdateVoiceButton();
            });
        }

        private void UpdateVoiceButton()
        {
            if (isListening)
            {
                VoiceRecognitionButton.Content = "🔴 Stop Recording";
                VoiceRecognitionButton.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
            }
            else
            {
                VoiceRecognitionButton.Content = "🎤 Start Voice Recognition";
                VoiceRecognitionButton.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69));
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop any ongoing voice recognition
            if (isListening)
            {
                _voiceService?.StopListening();
            }

            // Check if it's placeholder text and handle accordingly
            if (isPlaceholderText || string.IsNullOrWhiteSpace(RecognizedTextBox.Text) ||
                RecognizedTextBox.Text == "Type your message here...")
            {
                MessageBox.Show("Please enter a message before sending.", "Empty Message",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageText = RecognizedTextBox.Text.Trim();
            SendMessage = true;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                _voiceService?.StopListening();
            }

            SendMessage = false;
            DialogResult = false;
        }

        private void StartVoiceRecognition_Click(object sender, RoutedEventArgs e)
        {
            if (_voiceService == null || !_voiceService.IsInitialized)
            {
                MessageBox.Show("Voice service is not available. Please check your microphone settings.",
                               "Voice Recognition Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (isListening)
                {
                    _voiceService.StopListening();
                }
                else
                {
                    _voiceService.StartListening();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Voice recognition error: {ex.Message}",
                               "Voice Recognition Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecognizedTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (isPlaceholderText && RecognizedTextBox.Text == "Type your message here...")
            {
                RecognizedTextBox.Text = string.Empty;
                RecognizedTextBox.Foreground = Brushes.Black;
                isPlaceholderText = false;
            }
        }

        private void RecognizedTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RecognizedTextBox.Text))
            {
                RecognizedTextBox.Text = "Type your message here...";
                RecognizedTextBox.Foreground = Brushes.Gray;
                isPlaceholderText = true;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RecognizedTextBox.Foreground = Brushes.Gray;
            RecognizedTextBox.Focus();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isListening)
            {
                _voiceService?.StopListening();
            }
            _voiceService?.Dispose();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _voiceService?.Dispose();
            base.OnClosed(e);
        }
    }
}
