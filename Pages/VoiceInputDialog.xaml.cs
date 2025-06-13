using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GameApp.Services.Voice;
using System.Diagnostics;

namespace GameApp.Pages
{
    public partial class VoiceInputDialog : Window
    {
        private VoiceService _voiceService;
        private DispatcherTimer _recordingTimer;
        private int _recordingSeconds;
        private bool _isListening;
        private string _placeholderText = "Type your message here...";

        public string MessageText { get; private set; } = "";
        public bool SendMessage { get; private set; } = false;

        public VoiceInputDialog()
        {
            InitializeComponent();
            InitializeVoiceService();
            SetupUI();
        }

        private void InitializeVoiceService()
        {
            try
            {
                _voiceService = new VoiceService();

                // Subscribe to the correct events from VoiceService
                _voiceService.RecognitionCompleted += OnSpeechRecognized;
                _voiceService.RecognitionError += OnRecognitionError;
                _voiceService.RecordingStarted += OnRecordingStarted;
                _voiceService.RecordingStopped += OnRecordingStopped;
                _voiceService.StatusChanged += OnStatusChanged;

                // Initialize recording timer
                _recordingTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _recordingTimer.Tick += OnRecordingTimerTick;

                Debug.WriteLine("VoiceInputDialog: Voice service initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VoiceInputDialog: Voice service initialization failed: {ex.Message}");
                VoiceRecognitionButton.IsEnabled = false;
                VoiceRecognitionButton.Content = "🚫 Voice Unavailable";
            }
        }

        private void SetupUI()
        {
            // Set placeholder text appearance
            RecognizedTextBox.Foreground = Brushes.Gray;
            RecognizedTextBox.Text = _placeholderText;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                RecognizedTextBox.Focus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Window_Loaded error: {ex.Message}");
            }
        }

        #region Voice Recognition Event Handlers

        private void OnSpeechRecognized(object sender, string recognizedText)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(recognizedText))
                    {
                        // Clear placeholder text and set recognized text
                        RecognizedTextBox.Foreground = Brushes.Black;
                        RecognizedTextBox.Text = recognizedText;
                        MessageText = recognizedText;

                        Debug.WriteLine($"Speech recognized: {recognizedText}");
                    }

                    StopListening();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnSpeechRecognized error: {ex.Message}");
                }
            });
        }

        private void OnRecognitionError(object sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    Debug.WriteLine($"Recognition error: {error}");
                    StopListening();

                    // Show error message briefly
                    ShowStatusMessage($"Voice recognition failed: {error}", Brushes.Red);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnRecognitionError error: {ex.Message}");
                }
            });
        }

        private void OnRecordingStarted(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    _isListening = true;
                    _recordingSeconds = 0;
                    _recordingTimer.Start();

                    VoiceRecognitionButton.Content = "🛑 Stop Recording";
                    VoiceRecognitionButton.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red color

                    ShowStatusMessage("🎤 Listening... Speak now", Brushes.Green);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnRecordingStarted error: {ex.Message}");
                }
            });
        }

        private void OnRecordingStopped(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    StopListening();
                    ShowStatusMessage("Processing speech...", Brushes.Blue);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnRecordingStopped error: {ex.Message}");
                }
            });
        }

        private void OnStatusChanged(string status)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    ShowStatusMessage(status, Brushes.Gray);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnStatusChanged error: {ex.Message}");
                }
            });
        }

        #endregion

        #region UI Event Handlers

        private async void StartVoiceRecognition_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isListening)
                {
                    ShowStatusMessage("Starting voice recognition...", Brushes.Blue);
                    await _voiceService.StartRecordingAsync();
                }
                else
                {
                    await _voiceService.StopRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Voice recognition button error: {ex.Message}");
                ShowStatusMessage($"Voice recognition failed: {ex.Message}", Brushes.Red);
                StopListening();
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = RecognizedTextBox.Text?.Trim();

                // Don't send if it's placeholder text or empty
                if (!string.IsNullOrWhiteSpace(text) && text != _placeholderText)
                {
                    MessageText = text;
                    SendMessage = true;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowStatusMessage("Please enter a message first", Brushes.Orange);
                    RecognizedTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Send button error: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SendMessage = false;
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cancel button error: {ex.Message}");
            }
        }

        private void RecognizedTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RecognizedTextBox.Text == _placeholderText)
                {
                    RecognizedTextBox.Text = "";
                    RecognizedTextBox.Foreground = Brushes.Black;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GotFocus error: {ex.Message}");
            }
        }

        private void RecognizedTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(RecognizedTextBox.Text))
                {
                    RecognizedTextBox.Text = _placeholderText;
                    RecognizedTextBox.Foreground = Brushes.Gray;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LostFocus error: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private void StopListening()
        {
            try
            {
                _isListening = false;
                _recordingTimer?.Stop();

                VoiceRecognitionButton.Content = "🎤 Start Voice Recognition";
                VoiceRecognitionButton.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Green color
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StopListening error: {ex.Message}");
            }
        }

        private void ShowStatusMessage(string message, Brush color)
        {
            try
            {
                // You can implement a status display here if needed
                // For now, just log to debug
                Debug.WriteLine($"Status: {message}");

                // If you have a status TextBlock in your XAML, uncomment and use this:
                // StatusTextBlock.Text = message;
                // StatusTextBlock.Foreground = color;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowStatusMessage error: {ex.Message}");
            }
        }

        private void OnRecordingTimerTick(object sender, EventArgs e)
        {
            try
            {
                _recordingSeconds++;
                // Update UI with recording time if needed
                Debug.WriteLine($"Recording: {_recordingSeconds}s");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Recording timer error: {ex.Message}");
            }
        }

        #endregion

        #region Window Events

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Stop any ongoing recording
                if (_isListening && _voiceService != null)
                {
                    await _voiceService.StopRecordingAsync();
                }

                // Clean up resources
                _recordingTimer?.Stop();
                _voiceService?.Dispose();

                Debug.WriteLine("VoiceInputDialog: Cleanup completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Window closing error: {ex.Message}");
            }
        }

        #endregion
    }
}
