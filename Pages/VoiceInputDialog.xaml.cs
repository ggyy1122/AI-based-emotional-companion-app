using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GameApp.Services.Voice;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GameApp.Pages
{
    public partial class VoiceInputDialog : Window
    {
        private VoiceService _voiceService;
        private DispatcherTimer _recordingTimer;
        private int _recordingSeconds;
        private bool _isListening;
        private string _placeholderText = "Type your message here...";

        // Mode management
        private bool _isAppendMode = false; // false = Replace, true = Append

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

            // Initialize mode button
            UpdateModeButton();
        }

        private void UpdateModeButton()
        {
            if (_isAppendMode)
            {
                ModeToggleButton.Content = "Mode: Append";
                ModeToggleButton.Background = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange
                ModeToggleButton.ToolTip = "Voice input will be added to existing text. Click to switch to Replace mode.";
            }
            else
            {
                ModeToggleButton.Content = "Mode: Replace";
                ModeToggleButton.Background = new SolidColorBrush(Color.FromRgb(138, 43, 226)); // Purple
                ModeToggleButton.ToolTip = "Voice input will replace all text. Click to switch to Append mode.";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                RecognizedTextBox.Focus();

                // Auto-start voice recognition when dialog opens
                AutoStartVoiceRecognition();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Window_Loaded error: {ex.Message}");
            }
        }

        private async void AutoStartVoiceRecognition()
        {
            try
            {
                // Wait a brief moment to ensure the UI is fully loaded
                await Task.Delay(500);

                if (_voiceService != null && _voiceService.IsInitialized && !_isListening)
                {
                    string modeText = _isAppendMode ? "append" : "replace";
                    ShowStatusMessage($"Auto-starting voice recognition in {modeText} mode...",
                                    new SolidColorBrush(Color.FromRgb(23, 162, 184)));

                    await _voiceService.StartRecordingAsync();
                }
                else
                {
                    ShowStatusMessage("Voice service not available for auto-start",
                                    new SolidColorBrush(Color.FromRgb(255, 193, 7)));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutoStartVoiceRecognition error: {ex.Message}");
                ShowStatusMessage($"Auto-start failed: {ex.Message}",
                                new SolidColorBrush(Color.FromRgb(220, 53, 69)));
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
                        HandleRecognizedText(recognizedText);
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

        private void HandleRecognizedText(string recognizedText)
        {
            try
            {
                // Check if current text is placeholder
                bool hasPlaceholder = RecognizedTextBox.Text == _placeholderText;

                if (hasPlaceholder || !_isAppendMode)
                {
                    // Replace mode or placeholder text present
                    RecognizedTextBox.Foreground = Brushes.Black;
                    RecognizedTextBox.Text = recognizedText;
                    ShowStatusMessage($"Text replaced: \"{GetPreviewText(recognizedText)}\"",
                                    new SolidColorBrush(Color.FromRgb(40, 167, 69)));
                }
                else
                {
                    // Append mode and there's existing content
                    string currentText = RecognizedTextBox.Text.Trim();

                    if (string.IsNullOrEmpty(currentText))
                    {
                        // No existing content, just set the recognized text
                        RecognizedTextBox.Text = recognizedText;
                    }
                    else
                    {
                        // Add recognized text with appropriate spacing
                        string separator = DetermineTextSeparator(currentText, recognizedText);
                        RecognizedTextBox.Text = currentText + separator + recognizedText;
                    }

                    RecognizedTextBox.Foreground = Brushes.Black;
                    ShowStatusMessage($"Text appended: \"{GetPreviewText(recognizedText)}\"",
                                    new SolidColorBrush(Color.FromRgb(40, 167, 69)));
                }

                // Move cursor to end and update message text
                RecognizedTextBox.CaretIndex = RecognizedTextBox.Text.Length;
                MessageText = RecognizedTextBox.Text;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HandleRecognizedText error: {ex.Message}");
            }
        }

        private string DetermineTextSeparator(string existingText, string newText)
        {
            if (string.IsNullOrEmpty(existingText)) return "";

            char lastChar = existingText[existingText.Length - 1];
            char firstChar = char.ToLower(newText[0]);

            // If existing text ends with punctuation or whitespace, add space
            if (char.IsPunctuation(lastChar) || char.IsWhiteSpace(lastChar))
            {
                return " ";
            }

            // If new text starts with punctuation, no separator needed
            if (char.IsPunctuation(firstChar))
            {
                return "";
            }

            // Default: add space
            return " ";
        }

        private string GetPreviewText(string text, int maxLength = 30)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        }

        private void OnRecognitionError(object sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    Debug.WriteLine($"Recognition error: {error}");
                    StopListening();
                    ShowStatusMessage($"Recognition failed: {error}",
                                    new SolidColorBrush(Color.FromRgb(220, 53, 69)));
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

                    // Update voice button to show stop
                    VoiceRecognitionButton.Content = "🛑 Stop Recording";
                    VoiceRecognitionButton.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red

                    // Show recording status
                    string modeText = _isAppendMode ? "append to" : "replace";
                    ShowStatusMessage($"🎤 Listening... Will {modeText} text",
                                    new SolidColorBrush(Color.FromRgb(40, 167, 69)));

                    // Show recording time
                    if (RecordingTimeTextBlock != null)
                    {
                        RecordingTimeTextBlock.Visibility = Visibility.Visible;
                        RecordingTimeTextBlock.Text = "Recording: 0s";
                    }
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
                    ShowStatusMessage("Processing speech...",
                                    new SolidColorBrush(Color.FromRgb(23, 162, 184)));
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
                    // Start recording immediately
                    string modeText = _isAppendMode ? "append" : "replace";
                    ShowStatusMessage($"Starting voice recognition in {modeText} mode...",
                                    new SolidColorBrush(Color.FromRgb(23, 162, 184)));
                    await _voiceService.StartRecordingAsync();
                }
                else
                {
                    // Stop recording
                    await _voiceService.StopRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Voice recognition button error: {ex.Message}");
                ShowStatusMessage($"Voice recognition failed: {ex.Message}",
                                new SolidColorBrush(Color.FromRgb(220, 53, 69)));
                StopListening();
            }
        }

        private void ModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isAppendMode = !_isAppendMode;
                UpdateModeButton();

                string newMode = _isAppendMode ? "Append" : "Replace";
                ShowStatusMessage($"Mode changed to: {newMode}",
                                new SolidColorBrush(Color.FromRgb(23, 162, 184)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mode toggle error: {ex.Message}");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear text and reset to placeholder
                RecognizedTextBox.Text = _placeholderText;
                RecognizedTextBox.Foreground = Brushes.Gray;
                MessageText = "";

                ShowStatusMessage("Text cleared",
                                new SolidColorBrush(Color.FromRgb(108, 117, 125)));
                RecognizedTextBox.Focus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Clear button error: {ex.Message}");
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
                    ShowStatusMessage("Please enter a message first",
                                    new SolidColorBrush(Color.FromRgb(255, 193, 7)));
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

                // Reset voice button
                VoiceRecognitionButton.Content = "🎤 Start Voice Recognition";
                VoiceRecognitionButton.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Green

                // Hide recording time
                if (RecordingTimeTextBlock != null)
                {
                    RecordingTimeTextBlock.Visibility = Visibility.Collapsed;
                }
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
                Debug.WriteLine($"Status: {message}");

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = message;
                    StatusTextBlock.Foreground = color;
                }
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
                Debug.WriteLine($"Recording: {_recordingSeconds}s");

                if (RecordingTimeTextBlock != null)
                {
                    RecordingTimeTextBlock.Text = $"Recording: {_recordingSeconds}s";
                }
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
