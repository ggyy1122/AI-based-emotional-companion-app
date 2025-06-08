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

            System.Diagnostics.Debug.WriteLine("=== VoiceInputDialog Constructor Started ===");

            // Initialize voice service
            InitializeVoiceService();

            // Set focus to text box
            RecognizedTextBox.Focus();

            System.Diagnostics.Debug.WriteLine("=== VoiceInputDialog Constructor Complete ===");
        }

        private void InitializeVoiceService()
        {
            System.Diagnostics.Debug.WriteLine("Attempting to initialize VoiceService...");

            try
            {
                _voiceService = new VoiceService();
                System.Diagnostics.Debug.WriteLine("VoiceService instance created successfully");

                _voiceService.StatusChanged += OnVoiceStatusChanged;
                _voiceService.SpeechRecognized += OnSpeechRecognized;
                _voiceService.ListeningStateChanged += OnListeningStateChanged;

                System.Diagnostics.Debug.WriteLine("VoiceService events subscribed");
                System.Diagnostics.Debug.WriteLine($"VoiceService IsInitialized: {_voiceService.IsInitialized}");

                if (_voiceService.IsInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("Voice service initialized successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Warning: Voice service created but not initialized");
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Voice service initialization failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                MessageBox.Show($"Voice service initialization failed: {ex.Message}",
                               "Voice Service", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnVoiceStatusChanged(string status)
        {
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[EVENT] Voice Status: {status}");
            });
        }

        private void OnSpeechRecognized(string recognizedText)
        {
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[EVENT] Speech recognized: '{recognizedText}'");

                if (string.IsNullOrWhiteSpace(recognizedText))
                {
                    System.Diagnostics.Debug.WriteLine("Warning: Recognized text is empty or null");
                    return;
                }

                // Clear placeholder text if present
                if (isPlaceholderText)
                {
                    RecognizedTextBox.Text = string.Empty;
                    isPlaceholderText = false;
                    RecognizedTextBox.Foreground = Brushes.Black;
                    System.Diagnostics.Debug.WriteLine("Placeholder text cleared");
                }

                // Append recognized text (or replace if you prefer)
                if (string.IsNullOrWhiteSpace(RecognizedTextBox.Text))
                {
                    RecognizedTextBox.Text = recognizedText;
                    System.Diagnostics.Debug.WriteLine($"Set initial text: '{recognizedText}'");
                }
                else
                {
                    // Append with a space
                    RecognizedTextBox.Text += " " + recognizedText;
                    System.Diagnostics.Debug.WriteLine($"Appended text: '{recognizedText}'. Full text: '{RecognizedTextBox.Text}'");
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
                System.Diagnostics.Debug.WriteLine($"[EVENT] Listening state changed to: {listening}");
                UpdateVoiceButton();
            });
        }

        private void UpdateVoiceButton()
        {
            if (isListening)
            {
                VoiceRecognitionButton.Content = "🔴 Stop Recording";
                VoiceRecognitionButton.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red color
                System.Diagnostics.Debug.WriteLine("Button updated to 'Stop Recording' mode");
            }
            else
            {
                VoiceRecognitionButton.Content = "🎤 Start Voice Recognition";
                VoiceRecognitionButton.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Green color
                System.Diagnostics.Debug.WriteLine("Button updated to 'Start Recognition' mode");
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Send button clicked");

            // Stop any ongoing voice recognition
            if (isListening)
            {
                System.Diagnostics.Debug.WriteLine("Stopping voice recognition before sending message");
                _voiceService?.StopListening();
            }

            // Check if it's placeholder text and handle accordingly
            if (isPlaceholderText || string.IsNullOrWhiteSpace(RecognizedTextBox.Text) ||
                RecognizedTextBox.Text == "Type your message here...")
            {
                System.Diagnostics.Debug.WriteLine("Send failed: No message content");
                MessageBox.Show("Please enter a message before sending.", "Empty Message",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageText = RecognizedTextBox.Text.Trim();
            SendMessage = true;
            System.Diagnostics.Debug.WriteLine($"Message prepared for sending: '{MessageText}'");

            // Close dialog immediately without confirmation
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Cancel button clicked");

            // Stop any ongoing voice recognition
            if (isListening)
            {
                System.Diagnostics.Debug.WriteLine("Stopping voice recognition on cancel");
                _voiceService?.StopListening();
            }

            SendMessage = false;
            DialogResult = false;
        }

        private void StartVoiceRecognition_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== Voice recognition button clicked ===");
            System.Diagnostics.Debug.WriteLine($"VoiceService null check: {_voiceService == null}");

            if (_voiceService != null)
            {
                System.Diagnostics.Debug.WriteLine($"VoiceService IsInitialized: {_voiceService.IsInitialized}");
            }

            if (_voiceService == null || !_voiceService.IsInitialized)
            {
                System.Diagnostics.Debug.WriteLine("Error: Voice service is not available or not initialized");
                MessageBox.Show("Voice service is not available. Please check your microphone settings.",
                               "Voice Recognition Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (isListening)
                {
                    // Stop listening
                    System.Diagnostics.Debug.WriteLine("Stopping voice recognition...");
                    _voiceService.StopListening();
                    System.Diagnostics.Debug.WriteLine("StopListening() method called");
                }
                else
                {
                    // Start listening
                    System.Diagnostics.Debug.WriteLine("Starting voice recognition...");
                    _voiceService.StartListening();
                    System.Diagnostics.Debug.WriteLine("StartListening() method called");

                    // Check if listening state changed
                    System.Diagnostics.Debug.WriteLine($"Current listening state after start: {isListening}");
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Voice recognition error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

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
                System.Diagnostics.Debug.WriteLine("TextBox focused: Placeholder text cleared");
            }
        }

        private void RecognizedTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RecognizedTextBox.Text))
            {
                RecognizedTextBox.Text = "Type your message here...";
                RecognizedTextBox.Foreground = Brushes.Gray;
                isPlaceholderText = true;
                System.Diagnostics.Debug.WriteLine("TextBox lost focus: Placeholder text restored");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initial placeholder appearance
            RecognizedTextBox.Foreground = Brushes.Gray;
            RecognizedTextBox.Focus();
            System.Diagnostics.Debug.WriteLine("VoiceInputDialog window loaded");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("VoiceInputDialog window closing - cleaning up resources");

            // Clean up voice service
            if (isListening)
            {
                System.Diagnostics.Debug.WriteLine("Stopping voice recognition on window close");
                _voiceService?.StopListening();
            }
            _voiceService?.Dispose();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            // Ensure cleanup
            System.Diagnostics.Debug.WriteLine("VoiceInputDialog window closed - final cleanup");
            _voiceService?.Dispose();
            base.OnClosed(e);
        }
    }
}
