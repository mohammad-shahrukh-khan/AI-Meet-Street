using NAudio.Wave;
using SmartMeetingAssistant.Core.Interfaces;

namespace SmartMeetingAssistant.Services;

public class AudioService : IAudioService
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _waveFileWriter;
    private MemoryStream? _recordingStream;
    private bool _isRecording;
    private readonly object _lock = new();

    public event EventHandler<AudioDataEventArgs>? AudioDataReceived;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsRecording
    {
        get
        {
            lock (_lock)
            {
                return _isRecording;
            }
        }
    }

    public Task StartRecordingAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    if (_isRecording)
                        return;

                    _recordingStream = new MemoryStream();
                    
                    _waveIn = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz, 16-bit, mono
                        BufferMilliseconds = 64 // Small buffer for real-time processing
                    };

                    _waveFileWriter = new WaveFileWriter(_recordingStream, _waveIn.WaveFormat);

                    _waveIn.DataAvailable += OnDataAvailable;
                    _waveIn.RecordingStopped += OnRecordingStopped;

                    _waveIn.StartRecording();
                    _isRecording = true;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to start recording: {ex.Message}");
            }
        });
    }

    public Task StopRecordingAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    if (!_isRecording)
                        return;

                    _waveIn?.StopRecording();
                    _isRecording = false;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to stop recording: {ex.Message}");
            }
        });
    }

    public async Task<string?> SaveRecordingAsync(string filePath)
    {
        try
        {
            if (_recordingStream == null || _recordingStream.Length == 0)
                return null;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save the recorded audio to file
            _recordingStream.Position = 0;
            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await _recordingStream.CopyToAsync(fileStream);

            return filePath;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to save recording: {ex.Message}");
            return null;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            if (_waveFileWriter != null)
            {
                _waveFileWriter.Write(e.Buffer, 0, e.BytesRecorded);
            }

            // Raise event for real-time processing
            var audioData = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, audioData, e.BytesRecorded);
            
            AudioDataReceived?.Invoke(this, new AudioDataEventArgs(
                audioData, 
                _waveIn?.WaveFormat.SampleRate ?? 16000, 
                _waveIn?.WaveFormat.Channels ?? 1));
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error processing audio data: {ex.Message}");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            ErrorOccurred?.Invoke(this, $"Recording stopped with error: {e.Exception.Message}");
        }

        lock (_lock)
        {
            _isRecording = false;
        }

        // Cleanup
        _waveFileWriter?.Dispose();
        _waveFileWriter = null;
    }

    public void Dispose()
    {
        try
        {
            StopRecordingAsync().Wait(TimeSpan.FromSeconds(5));
            
            _waveIn?.Dispose();
            _waveFileWriter?.Dispose();
            _recordingStream?.Dispose();
        }
        catch (Exception ex)
        {
            // Log but don't throw in Dispose
            ErrorOccurred?.Invoke(this, $"Error during cleanup: {ex.Message}");
        }
    }
}
