using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using SmartMeetingAssistant.Core.Interfaces;
using SmartMeetingAssistant.Core.Models;

namespace SmartMeetingAssistant.Services;

public class TranscriptionService : ITranscriptionService
{
    private readonly string _subscriptionKey;
    private readonly string _region;
    private SpeechRecognizer? _speechRecognizer;
    private AudioConfig? _audioConfig;
    private PushAudioInputStream? _pushStream;
    private bool _isTranscribing;
    private readonly object _lock = new();

    public event EventHandler<TranscriptionEventArgs>? TranscriptionReceived;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsTranscribing
    {
        get
        {
            lock (_lock)
            {
                return _isTranscribing;
            }
        }
    }

    public TranscriptionService(string subscriptionKey, string region)
    {
        _subscriptionKey = subscriptionKey ?? throw new ArgumentNullException(nameof(subscriptionKey));
        _region = region ?? throw new ArgumentNullException(nameof(region));
    }

    public async Task StartTranscriptionAsync()
    {
        try
        {
            lock (_lock)
            {
                if (_isTranscribing)
                    return;
            }

            // Create speech configuration
            var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);
            speechConfig.SpeechRecognitionLanguage = "en-US";
            speechConfig.EnableDictation();

            // Create push audio input stream
            _pushStream = AudioInputStream.CreatePushStream();
            _audioConfig = AudioConfig.FromStreamInput(_pushStream);

            // Create speech recognizer
            _speechRecognizer = new SpeechRecognizer(speechConfig, _audioConfig);

            // Subscribe to events
            _speechRecognizer.Recognizing += OnRecognizing;
            _speechRecognizer.Recognized += OnRecognized;
            _speechRecognizer.SessionStopped += OnSessionStopped;
            _speechRecognizer.Canceled += OnCanceled;

            // Start continuous recognition
            await _speechRecognizer.StartContinuousRecognitionAsync();

            lock (_lock)
            {
                _isTranscribing = true;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to start transcription: {ex.Message}");
        }
    }

    public async Task StopTranscriptionAsync()
    {
        try
        {
            if (_speechRecognizer != null)
            {
                await _speechRecognizer.StopContinuousRecognitionAsync();
            }

            lock (_lock)
            {
                _isTranscribing = false;
            }

            // Close the push stream
            _pushStream?.Close();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to stop transcription: {ex.Message}");
        }
    }

    public async Task<string> TranscribeAudioFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Audio file not found: {filePath}");

            var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);
            speechConfig.SpeechRecognitionLanguage = "en-US";

            using var audioConfig = AudioConfig.FromWavFileInput(filePath);
            using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

            var result = await recognizer.RecognizeOnceAsync();

            if (result.Reason == ResultReason.RecognizedSpeech)
            {
                return result.Text;
            }
            else if (result.Reason == ResultReason.NoMatch)
            {
                return "No speech could be recognized from the audio file.";
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = CancellationDetails.FromResult(result);
                throw new Exception($"Speech recognition was cancelled: {cancellation.Reason}. Details: {cancellation.ErrorDetails}");
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to transcribe audio file: {ex.Message}");
            return string.Empty;
        }
    }

    public void ProcessAudioData(byte[] audioData, int sampleRate, int channels)
    {
        try
        {
            if (_pushStream != null && _isTranscribing)
            {
                _pushStream.Write(audioData);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to process audio data: {ex.Message}");
        }
    }

    private void OnRecognizing(object sender, SpeechRecognitionEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Result.Text))
        {
            var segment = new TranscriptSegment
            {
                Text = e.Result.Text,
                Timestamp = DateTime.Now,
                StartTime = TimeSpan.FromTicks(e.Result.OffsetInTicks),
                EndTime = TimeSpan.FromTicks(e.Result.OffsetInTicks + e.Result.Duration.Ticks),
                Confidence = 0.0, // Intermediate results don't have confidence scores
                IsFinal = false
            };

            TranscriptionReceived?.Invoke(this, new TranscriptionEventArgs(segment, false, 0.0));
        }
    }

    private void OnRecognized(object sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
        {
            // Calculate confidence score from the result
            var confidence = CalculateConfidence(e.Result);

            var segment = new TranscriptSegment
            {
                Text = e.Result.Text,
                Timestamp = DateTime.Now,
                StartTime = TimeSpan.FromTicks(e.Result.OffsetInTicks),
                EndTime = TimeSpan.FromTicks(e.Result.OffsetInTicks + e.Result.Duration.Ticks),
                Confidence = confidence,
                IsFinal = true
            };

            TranscriptionReceived?.Invoke(this, new TranscriptionEventArgs(segment, true, confidence));
        }
        else if (e.Result.Reason == ResultReason.NoMatch)
        {
            // Handle no match case if needed
        }
    }

    private void OnSessionStopped(object sender, SessionEventArgs e)
    {
        lock (_lock)
        {
            _isTranscribing = false;
        }
    }

    private void OnCanceled(object sender, SpeechRecognitionCanceledEventArgs e)
    {
        var errorMessage = $"Speech recognition canceled: {e.Reason}";
        if (e.Reason == CancellationReason.Error)
        {
            errorMessage += $". Error details: {e.ErrorDetails}";
        }

        ErrorOccurred?.Invoke(this, errorMessage);

        lock (_lock)
        {
            _isTranscribing = false;
        }
    }

    private double CalculateConfidence(SpeechRecognitionResult result)
    {
        // Azure Speech Services doesn't directly provide confidence scores for continuous recognition
        // We can estimate based on the result properties or use a default high confidence for recognized speech
        
        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            // For now, return a high confidence for successfully recognized speech
            // In a production system, you might want to analyze the audio quality, duration, etc.
            return 0.85;
        }

        return 0.0;
    }

    public void Dispose()
    {
        try
        {
            StopTranscriptionAsync().Wait(TimeSpan.FromSeconds(5));
            
            _speechRecognizer?.Dispose();
            _audioConfig?.Dispose();
            _pushStream?.Dispose();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error during cleanup: {ex.Message}");
        }
    }
}
