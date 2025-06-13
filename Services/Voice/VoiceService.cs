using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using NAudio.Wave;
using RestSharp;
using Newtonsoft.Json;
using System.Diagnostics;
using GameApp.Services.Voice.Models;
using System.Speech.Synthesis;

namespace GameApp.Services.Voice
{
    public class VoiceService : IDisposable
    {
        private readonly VoiceServiceSetting _settings;
        private WaveInEvent _waveIn;
        private MemoryStream _recordingStream;
        private bool _isRecording;
        private bool _isDisposed;
        private DateTime _lastSoundTime;
        private readonly object _recordingLock = new object();

        // TTS (Text-to-Speech) support
        private SpeechSynthesizer _speechSynthesizer;
        private bool _isInitialized;

        // Events for compatibility with AIChatPage
        public event EventHandler<string> RecognitionCompleted;
        public event EventHandler<string> RecognitionError;
        public event EventHandler RecordingStarted;
        public event EventHandler RecordingStopped;
        public event Action<string> StatusChanged;

        // Properties for compatibility
        public bool IsRecording => _isRecording;
        public bool IsInitialized => _isInitialized;
        public VoiceServiceSetting Settings => _settings;

        public VoiceService(VoiceServiceSetting settings = null)
        {
            _settings = settings ?? new VoiceServiceSetting();
            InitializeServices();
        }

        private void InitializeServices()
        {
            try
            {
                // Initialize speech recognition
                InitializeAudioCapture();

                // Initialize text-to-speech
                InitializeTextToSpeech();

                _isInitialized = true;
                StatusChanged?.Invoke("Voice service initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize voice services: {ex.Message}");
                StatusChanged?.Invoke($"Voice service initialization failed: {ex.Message}");
                _isInitialized = false;
            }
        }

        private void InitializeAudioCapture()
        {
            try
            {
                var waveFormat = new WaveFormat(_settings.SampleRate, _settings.BitsPerSample, _settings.Channels);
                _waveIn = new WaveInEvent
                {
                    WaveFormat = waveFormat,
                    BufferMilliseconds = 100
                };

                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize audio capture: {ex.Message}");
                RecognitionError?.Invoke(this, $"Audio initialization failed: {ex.Message}");
            }
        }

        private void InitializeTextToSpeech()
        {
            try
            {
                _speechSynthesizer = new SpeechSynthesizer();
                _speechSynthesizer.SetOutputToDefaultAudioDevice();

                // Set default voice settings
                _speechSynthesizer.Rate = 0; // Normal speed
                _speechSynthesizer.Volume = 80; // 80% volume
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize TTS: {ex.Message}");
                StatusChanged?.Invoke($"Text-to-speech initialization failed: {ex.Message}");
            }
        }

        #region Speech Recognition Methods

        public async Task<bool> EnsureTokenAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_settings.BaiduToken))
                    return true;

                StatusChanged?.Invoke("Obtaining Baidu access token...");

                var client = new RestClient("https://aip.baidubce.com");
                var request = new RestRequest("/oauth/2.0/token", Method.Post);

                request.AddParameter("grant_type", "client_credentials");
                request.AddParameter("client_id", _settings.BaiduApiKey);
                request.AddParameter("client_secret", _settings.BaiduSecretKey);

                var response = await client.ExecuteAsync(request);

                if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
                {
                    var tokenResponse = JsonConvert.DeserializeObject<BaiduTokenResponse>(response.Content);

                    if (!string.IsNullOrEmpty(tokenResponse.AccessToken))
                    {
                        _settings.BaiduToken = tokenResponse.AccessToken;
                        Debug.WriteLine("Baidu token obtained successfully");
                        StatusChanged?.Invoke("Access token obtained successfully");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine($"Token request failed: {tokenResponse.Error} - {tokenResponse.ErrorDescription}");
                        StatusChanged?.Invoke($"Token request failed: {tokenResponse.ErrorDescription}");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Token request exception: {ex.Message}");
                StatusChanged?.Invoke($"Token request failed: {ex.Message}");
                return false;
            }
        }

        public async Task StartRecordingAsync()
        {
            if (_isRecording || _isDisposed)
                return;

            try
            {
                StatusChanged?.Invoke("Starting voice recording...");

                // Ensure we have a valid token
                if (!await EnsureTokenAsync())
                {
                    RecognitionError?.Invoke(this, "Failed to obtain Baidu access token");
                    return;
                }

                lock (_recordingLock)
                {
                    _recordingStream?.Dispose();
                    _recordingStream = new MemoryStream();
                    _isRecording = true;
                    _lastSoundTime = DateTime.Now;
                }

                _waveIn?.StartRecording();
                RecordingStarted?.Invoke(this, EventArgs.Empty);
                StatusChanged?.Invoke("Recording... Speak now");

                Debug.WriteLine("Voice recording started");

                // Start timeout monitoring
                _ = Task.Run(MonitorRecording);
            }
            catch (Exception ex)
            {
                _isRecording = false;
                Debug.WriteLine($"Failed to start recording: {ex.Message}");
                RecognitionError?.Invoke(this, $"Recording start failed: {ex.Message}");
                StatusChanged?.Invoke($"Recording failed: {ex.Message}");
            }
        }

        public async Task StopRecordingAsync()
        {
            if (!_isRecording || _isDisposed)
                return;

            try
            {
                StatusChanged?.Invoke("Stopping recording...");
                _waveIn?.StopRecording();

                // Wait a moment for the recording to stop
                await Task.Delay(100);

                await ProcessRecordingAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to stop recording: {ex.Message}");
                RecognitionError?.Invoke(this, $"Recording stop failed: {ex.Message}");
                StatusChanged?.Invoke($"Stop recording failed: {ex.Message}");
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (!_isRecording || _recordingStream == null)
                return;

            try
            {
                lock (_recordingLock)
                {
                    _recordingStream.Write(e.Buffer, 0, e.BytesRecorded);

                    // Check for sound activity
                    if (HasSoundActivity(e.Buffer, e.BytesRecorded))
                    {
                        _lastSoundTime = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Data available error: {ex.Message}");
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            RecordingStopped?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke("Recording stopped");
            Debug.WriteLine("Recording stopped");
        }

        private bool HasSoundActivity(byte[] buffer, int bytesRecorded)
        {
            try
            {
                double sum = 0;
                for (int i = 0; i < bytesRecorded; i += 2)
                {
                    if (i + 1 < bytesRecorded)
                    {
                        short sample = BitConverter.ToInt16(buffer, i);
                        sum += Math.Abs(sample);
                    }
                }

                double average = sum / (bytesRecorded / 2);
                return average > (_settings.SilenceThreshold * short.MaxValue);
            }
            catch
            {
                return false;
            }
        }

        private async Task MonitorRecording()
        {
            try
            {
                var recordingStartTime = DateTime.Now;

                while (_isRecording)
                {
                    await Task.Delay(100);

                    var timeSinceLastSound = DateTime.Now - _lastSoundTime;
                    var totalRecordingTime = DateTime.Now - recordingStartTime;

                    // Stop if silence duration exceeded or timeout reached
                    if (timeSinceLastSound.TotalMilliseconds > _settings.SilenceDurationMs ||
                        totalRecordingTime.TotalMilliseconds > _settings.RecordingTimeoutMs)
                    {
                        await StopRecordingAsync();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Recording monitor error: {ex.Message}");
            }
        }

        private async Task ProcessRecordingAsync()
        {
            if (_recordingStream == null || _recordingStream.Length == 0)
            {
                _isRecording = false;
                StatusChanged?.Invoke("No audio recorded");
                return;
            }

            try
            {
                byte[] audioData;
                lock (_recordingLock)
                {
                    audioData = _recordingStream.ToArray();
                    _isRecording = false;
                }

                Debug.WriteLine($"Processing {audioData.Length} bytes of audio data");
                StatusChanged?.Invoke("Processing audio...");

                var recognizedText = await RecognizeSpeechAsync(audioData);

                if (!string.IsNullOrEmpty(recognizedText))
                {
                    StatusChanged?.Invoke($"Recognized: {recognizedText}");
                    RecognitionCompleted?.Invoke(this, recognizedText);
                }
                else
                {
                    StatusChanged?.Invoke("No speech recognized");
                    RecognitionError?.Invoke(this, "No speech recognized");
                }
            }
            catch (Exception ex)
            {
                _isRecording = false;
                Debug.WriteLine($"Processing error: {ex.Message}");
                StatusChanged?.Invoke($"Processing failed: {ex.Message}");
                RecognitionError?.Invoke(this, $"Processing failed: {ex.Message}");
            }
        }

        private async Task<string> RecognizeSpeechAsync(byte[] audioData)
        {
            try
            {
                var client = new RestClient("https://vop.baidu.com");
                var request = new RestRequest("/server_api", Method.Post);
                request.AddHeader("Content-Type", "application/json");

                var requestBody = new BaiduSpeechRequest
                {
                    Format = _settings.AudioFormat,
                    Rate = _settings.SampleRate,
                    Channel = _settings.Channels,
                    Cuid = "user_" + Guid.NewGuid().ToString("N"),
                    Token = _settings.BaiduToken,
                    Speech = Convert.ToBase64String(audioData),
                    Len = audioData.Length
                };

                request.AddJsonBody(requestBody);

                var response = await client.ExecuteAsync(request);

                if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
                {
                    var speechResponse = JsonConvert.DeserializeObject<BaiduSpeechResponse>(response.Content);

                    if (speechResponse.IsSuccess)
                    {
                        Debug.WriteLine($"Recognition successful: {speechResponse.RecognizedText}");
                        return speechResponse.RecognizedText;
                    }
                    else
                    {
                        Debug.WriteLine($"Recognition failed: {speechResponse.ErrMsg}");
                        return null;
                    }
                }

                Debug.WriteLine($"API request failed: {response.ErrorMessage}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Recognition error: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Text-to-Speech Methods (for AIChatPage compatibility)

        /// <summary>
        /// Speak plain text (for user messages)
        /// </summary>
        public void SpeakText(string text)
        {
            if (!_isInitialized || _speechSynthesizer == null || string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                StopSpeaking(); // Stop any current speech
                _speechSynthesizer.SpeakAsync(text);
                StatusChanged?.Invoke($"Speaking: {MarkdownCleaner.GetPreviewText(text)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Text-to-speech error: {ex.Message}");
                StatusChanged?.Invoke($"Speech failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Speak markdown text (for AI messages) - converts markdown to speech-friendly text
        /// </summary>
        public void SpeakMarkdownText(string markdownText)
        {
            if (!_isInitialized || _speechSynthesizer == null || string.IsNullOrWhiteSpace(markdownText))
                return;

            try
            {
                // Convert markdown to speech-friendly text
                string speechText = MarkdownCleaner.ConvertToSpeechText(markdownText);
                speechText = MarkdownCleaner.OptimizeForSpeech(speechText);

                if (!string.IsNullOrWhiteSpace(speechText))
                {
                    StopSpeaking(); // Stop any current speech
                    _speechSynthesizer.SpeakAsync(speechText);
                    StatusChanged?.Invoke($"Speaking: {MarkdownCleaner.GetPreviewText(speechText)}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Markdown text-to-speech error: {ex.Message}");
                StatusChanged?.Invoke($"Speech failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop any current speech
        /// </summary>
        public void StopSpeaking()
        {
            try
            {
                _speechSynthesizer?.SpeakAsyncCancelAll();
                StatusChanged?.Invoke("Speech stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stop speaking error: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                // Stop and dispose recording resources
                _waveIn?.StopRecording();
                _waveIn?.Dispose();
                _recordingStream?.Dispose();

                // Stop and dispose TTS resources
                _speechSynthesizer?.SpeakAsyncCancelAll();
                _speechSynthesizer?.Dispose();

                StatusChanged?.Invoke("Voice service disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Dispose error: {ex.Message}");
            }
        }

        #endregion
    }
}
