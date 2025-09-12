using Whisper.net;
using Whisper.net.Ggml;
using NAudio.Wave;
using SmartMeetingAssistant.Core.Interfaces;
using SmartMeetingAssistant.Core.Models;

namespace SmartMeetingAssistant.Services;

/// <summary>
/// Free, local speech recognition using OpenAI Whisper
/// High accuracy, completely offline, no API costs
/// </summary>
public class WhisperTranscriptionService : ITranscriptionService
{
    private readonly object _lock = new();
    private bool _isTranscribing;
    private readonly List<byte> _audioBuffer = new();
    private Timer? _processingTimer;
    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _whisperProcessor;
    private readonly string _modelPath;

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

    public WhisperTranscriptionService()
    {
        // Model will be downloaded automatically on first use
        _modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "SmartMeetingAssistant", "whisper-models");
        
        Directory.CreateDirectory(_modelPath);
        InitializeWhisper();
    }

    private async void InitializeWhisper()
    {
        try
        {
            Console.WriteLine("Initializing Whisper...");
            
            // Download model if not exists (this happens automatically)
            var modelFileName = "ggml-base.bin"; // Base model - good balance of speed/accuracy
            var modelFilePath = Path.Combine(_modelPath, modelFileName);

            Console.WriteLine($"Model path: {modelFilePath}");

            if (!File.Exists(modelFilePath))
            {
                Console.WriteLine("Downloading Whisper model... This may take a few minutes.");
                ErrorOccurred?.Invoke(this, "Downloading Whisper model... Please wait.");
                
                // Model will be downloaded automatically by Whisper.NET
                using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
                using var fileWriter = File.OpenWrite(modelFilePath);
                await modelStream.CopyToAsync(fileWriter);
                
                Console.WriteLine("Model download completed.");
            }

            _whisperFactory = WhisperFactory.FromPath(modelFilePath);
            Console.WriteLine("Whisper initialized successfully!");
            ErrorOccurred?.Invoke(this, "Whisper ready for transcription.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Whisper initialization failed: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Failed to initialize Whisper: {ex.Message}");
        }
    }

    public async Task StartTranscriptionAsync()
    {
        try
        {
            lock (_lock)
            {
                if (_isTranscribing)
                    return;
                
                _isTranscribing = true;
                _audioBuffer.Clear();
            }

            // Wait for Whisper to initialize if needed
            int retryCount = 0;
            while (_whisperFactory == null && retryCount < 10)
            {
                await Task.Delay(1000);
                retryCount++;
            }

            if (_whisperFactory == null)
            {
                throw new InvalidOperationException("Whisper failed to initialize");
            }

            _whisperProcessor = _whisperFactory.CreateBuilder()
                .WithLanguage("en")
                .Build();

            // Start processing audio buffer every 3 seconds
            _processingTimer = new Timer(ProcessAudioBuffer, null, 
                TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));

        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to start Whisper transcription: {ex.Message}");
        }
    }

    public async Task StopTranscriptionAsync()
    {
        try
        {
            lock (_lock)
            {
                _isTranscribing = false;
            }

            _processingTimer?.Dispose();
            _processingTimer = null;

            // Process any remaining audio
            if (_audioBuffer.Count > 0)
            {
                await ProcessBufferedAudio();
            }
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

            if (_whisperFactory == null)
            {
                await Task.Delay(2000); // Wait for initialization
                if (_whisperFactory == null)
                    throw new InvalidOperationException("Whisper not initialized");
            }

            using var processor = _whisperFactory.CreateBuilder()
                .WithLanguage("en")
                .Build();

            using var fileStream = File.OpenRead(filePath);
            var results = new List<string>();

            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                results.Add(result.Text);
            }

            return string.Join(" ", results);
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
            if (_isTranscribing)
            {
                lock (_lock)
                {
                    _audioBuffer.AddRange(audioData);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to process audio data: {ex.Message}");
        }
    }

    private async void ProcessAudioBuffer(object? state)
    {
        try
        {
            if (!_isTranscribing || _whisperProcessor == null)
            {
                Console.WriteLine($"Skipping processing: IsTranscribing={_isTranscribing}, HasProcessor={_whisperProcessor != null}");
                return;
            }

            List<byte> bufferCopy;
            lock (_lock)
            {
                // Need at least 3 seconds of audio (16kHz * 2 bytes * 3 seconds)
                Console.WriteLine($"Audio buffer size: {_audioBuffer.Count} bytes");
                if (_audioBuffer.Count < 96000) 
                {
                    Console.WriteLine("Not enough audio data for processing");
                    return;
                }

                bufferCopy = new List<byte>(_audioBuffer);
                _audioBuffer.Clear();
                Console.WriteLine($"Processing {bufferCopy.Count} bytes of audio");
            }

            await ProcessAudioChunk(bufferCopy.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Audio processing error: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Error processing audio buffer: {ex.Message}");
        }
    }

    private async Task ProcessAudioChunk(byte[] audioData)
    {
        try
        {
            Console.WriteLine("Starting Whisper processing...");
            if (_whisperProcessor == null)
            {
                Console.WriteLine("Whisper processor is null!");
                return;
            }

            // Convert byte array to WAV file in memory
            using var memoryStream = new MemoryStream();
            using var writer = new WaveFileWriter(memoryStream, new WaveFormat(16000, 16, 1));
            
            // Write audio data
            writer.Write(audioData, 0, audioData.Length);
            writer.Flush();
            
            // Reset stream position for reading
            memoryStream.Position = 0;
            Console.WriteLine($"Created WAV stream with {memoryStream.Length} bytes");

            // Process with Whisper
            var results = new List<string>();
            Console.WriteLine("Processing with Whisper...");
            await foreach (var result in _whisperProcessor.ProcessAsync(memoryStream))
            {
                Console.WriteLine($"Whisper result: '{result.Text}'");
                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    results.Add(result.Text.Trim());
                }
            }

            Console.WriteLine($"Got {results.Count} results from Whisper");

            // Send results to UI
            foreach (var text in results)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Console.WriteLine($"Sending transcription: '{text}'");
                    var segment = new TranscriptSegment
                    {
                        Text = text,
                        Timestamp = DateTime.Now,
                        StartTime = TimeSpan.Zero,
                        EndTime = TimeSpan.FromSeconds(3),
                        Confidence = 0.9, // Whisper doesn't provide confidence scores
                        IsFinal = true
                    };

                    TranscriptionReceived?.Invoke(this, new TranscriptionEventArgs(segment, true, 0.9));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Whisper processing error: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Error processing audio chunk with Whisper: {ex.Message}");
        }
    }

    private async Task ProcessBufferedAudio()
    {
        try
        {
            List<byte> bufferCopy;
            lock (_lock)
            {
                if (_audioBuffer.Count == 0)
                    return;

                bufferCopy = new List<byte>(_audioBuffer);
                _audioBuffer.Clear();
            }

            await ProcessAudioChunk(bufferCopy.ToArray());
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error processing final audio buffer: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            StopTranscriptionAsync().Wait(TimeSpan.FromSeconds(5));
            _processingTimer?.Dispose();
            _whisperProcessor?.Dispose();
            _whisperFactory?.Dispose();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error during cleanup: {ex.Message}");
        }
    }
}
