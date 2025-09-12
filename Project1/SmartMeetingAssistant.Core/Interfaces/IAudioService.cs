namespace SmartMeetingAssistant.Core.Interfaces;

public interface IAudioService : IDisposable
{
    event EventHandler<AudioDataEventArgs>? AudioDataReceived;
    event EventHandler<string>? ErrorOccurred;
    
    bool IsRecording { get; }
    
    Task StartRecordingAsync();
    Task StopRecordingAsync();
    Task<string?> SaveRecordingAsync(string filePath);
}

public class AudioDataEventArgs : EventArgs
{
    public byte[] Data { get; }
    public int SampleRate { get; }
    public int Channels { get; }
    public DateTime Timestamp { get; }

    public AudioDataEventArgs(byte[] data, int sampleRate, int channels)
    {
        Data = data;
        SampleRate = sampleRate;
        Channels = channels;
        Timestamp = DateTime.UtcNow;
    }
}
