using System;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading.Tasks;

namespace GameApp.Services.Voice
{
    /// <summary>
    /// Voice service providing speech recognition and text-to-speech functionality
    /// </summary>
    public class VoiceService : IDisposable
    {
        private SpeechRecognitionEngine _recognizer;
        private SpeechSynthesizer _synthesizer;
        private bool _isListening = false;
        private bool _isSpeaking = false;
        private bool _isInitialized = false;

        // Events
        public event Action<string> SpeechRecognized;
        public event Action<string> StatusChanged;
        public event Action SpeechStarted;
        public event Action SpeechCompleted;
        public event Action<bool> ListeningStateChanged;

        // Properties
        public bool IsListening => _isListening;
        public bool IsSpeaking => _isSpeaking;
        public bool IsInitialized => _isInitialized;

        public VoiceService()
        {
            try
            {
                InitializeServices();
                _isInitialized = true;
                StatusChanged?.Invoke("Voice service initialized successfully");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Voice service initialization failed: {ex.Message}");
            }
        }

        private void InitializeServices()
        {
            // Initialize speech recognition
            _recognizer = new SpeechRecognitionEngine();

            // Load dictation grammar for free-form speech recognition
            var grammar = new DictationGrammar();
            grammar.Name = "dictation";
            grammar.Enabled = true;
            _recognizer.LoadGrammar(grammar);

            // Set up recognition events
            _recognizer.SpeechRecognized += OnSpeechRecognized;
            _recognizer.SpeechRecognitionRejected += OnSpeechRejected;
            _recognizer.RecognizeCompleted += OnRecognizeCompleted;
            _recognizer.SpeechDetected += OnSpeechDetected;

            // Set input to default microphone
            _recognizer.SetInputToDefaultAudioDevice();

            // Initialize text-to-speech
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();

            // Configure TTS settings
            _synthesizer.Rate = 0;     // Normal speed (-10 to 10)
            _synthesizer.Volume = 80;  // Volume (0 to 100)

            // Set up TTS events
            _synthesizer.SpeakStarted += OnSpeakStarted;
            _synthesizer.SpeakCompleted += OnSpeakCompleted;
            _synthesizer.SpeakProgress += OnSpeakProgress;

            // Try to select a better voice if available
            try
            {
                _synthesizer.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
            }
            catch
            {
                // Use default voice if selection fails
            }
        }

        #region Speech Recognition

        /// <summary>
        /// Start listening for voice input
        /// </summary>
        public void StartListening()
        {
            if (!_isInitialized)
            {
                StatusChanged?.Invoke("Voice service not initialized");
                return;
            }

            if (!_isListening)
            {
                try
                {
                    _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                    _isListening = true;
                    ListeningStateChanged?.Invoke(true);
                    StatusChanged?.Invoke("Listening for speech...");
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"Failed to start speech recognition: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Stop listening for voice input
        /// </summary>
        public void StopListening()
        {
            if (_isListening)
            {
                try
                {
                    _recognizer.RecognizeAsyncStop();
                    _isListening = false;
                    ListeningStateChanged?.Invoke(false);
                    StatusChanged?.Invoke("Speech recognition stopped");
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"Failed to stop speech recognition: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Toggle listening state
        /// </summary>
        public void ToggleListening()
        {
            if (_isListening)
            {
                StopListening();
            }
            else
            {
                StartListening();
            }
        }

        private void OnSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // Set confidence threshold to improve accuracy
            if (e.Result.Confidence > 0)
            {
                // Stop listening after recognition
                StopListening();

                SpeechRecognized?.Invoke(e.Result.Text);
                StatusChanged?.Invoke($"Recognized: {e.Result.Text} (Confidence: {e.Result.Confidence:P})");
            }
            else
            {
                StatusChanged?.Invoke($"Low confidence recognition rejected: {e.Result.Confidence:P}");
            }
        }

        private void OnSpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            StatusChanged?.Invoke("Speech recognition failed, please try again");
        }

        private void OnRecognizeCompleted(object sender, RecognizeCompletedEventArgs e)
        {
            _isListening = false;
            ListeningStateChanged?.Invoke(false);
            StatusChanged?.Invoke("Speech recognition completed");
        }

        private void OnSpeechDetected(object sender, SpeechDetectedEventArgs e)
        {
            StatusChanged?.Invoke("Speech detected...");
        }

        #endregion

        #region Text-to-Speech

        /// <summary>
        /// Speak Markdown text (automatically cleaned for TTS)
        /// </summary>
        /// <param name="markdownText">Markdown formatted text</param>
        public void SpeakMarkdownText(string markdownText)
        {
            if (!_isInitialized || string.IsNullOrWhiteSpace(markdownText))
                return;

            try
            {
                // Stop current speech
                StopSpeaking();

                // Clean Markdown and optimize for speech
                string speechText = MarkdownCleaner.ConvertToSpeechText(markdownText);
                speechText = MarkdownCleaner.OptimizeForSpeech(speechText);

                if (!string.IsNullOrWhiteSpace(speechText))
                {
                    StatusChanged?.Invoke($"Speaking: {MarkdownCleaner.GetPreviewText(speechText)}");
                    _synthesizer.SpeakAsync(speechText);
                }
                else
                {
                    StatusChanged?.Invoke("No text content to speak");
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Speech synthesis failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Speak plain text
        /// </summary>
        /// <param name="text">Plain text to speak</param>
        public void SpeakText(string text)
        {
            if (!_isInitialized || string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                StopSpeaking();
                StatusChanged?.Invoke($"Speaking: {MarkdownCleaner.GetPreviewText(text)}");
                _synthesizer.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Speech synthesis failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Pause current speech
        /// </summary>
        public void PauseSpeaking()
        {
            if (_isSpeaking)
            {
                try
                {
                    _synthesizer.Pause();
                    StatusChanged?.Invoke("Speech paused");
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"Failed to pause speech: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Resume paused speech
        /// </summary>
        public void ResumeSpeaking()
        {
            try
            {
                _synthesizer.Resume();
                StatusChanged?.Invoke("Speech resumed");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Failed to resume speech: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop current speech
        /// </summary>
        public void StopSpeaking()
        {
            try
            {
                _synthesizer.SpeakAsyncCancelAll();
                _isSpeaking = false;
                StatusChanged?.Invoke("Speech stopped");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Failed to stop speech: {ex.Message}");
            }
        }

        private void OnSpeakStarted(object sender, SpeakStartedEventArgs e)
        {
            _isSpeaking = true;
            SpeechStarted?.Invoke();
            StatusChanged?.Invoke("Speech started");
        }

        private void OnSpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            _isSpeaking = false;
            SpeechCompleted?.Invoke();
            StatusChanged?.Invoke("Speech completed");
        }

        private void OnSpeakProgress(object sender, SpeakProgressEventArgs e)
        {
            // Optional: Track speech progress
            // StatusChanged?.Invoke($"Speaking: {e.Text}");
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Set speech rate (-10 to 10)
        /// </summary>
        /// <param name="rate">Speech rate</param>
        public void SetSpeechRate(int rate)
        {
            if (_synthesizer != null)
            {
                _synthesizer.Rate = Math.Max(-10, Math.Min(10, rate));
                StatusChanged?.Invoke($"Speech rate set to {rate}");
            }
        }

        /// <summary>
        /// Set speech volume (0 to 100)
        /// </summary>
        /// <param name="volume">Speech volume</param>
        public void SetSpeechVolume(int volume)
        {
            if (_synthesizer != null)
            {
                _synthesizer.Volume = Math.Max(0, Math.Min(100, volume));
                StatusChanged?.Invoke($"Speech volume set to {volume}");
            }
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            try
            {
                _recognizer?.RecognizeAsyncStop();
                _recognizer?.Dispose();
                _synthesizer?.SpeakAsyncCancelAll();
                _synthesizer?.Dispose();
            }
            catch (Exception)
            {
                // Ignore disposal exceptions
            }
        }

        #endregion
    }
}
