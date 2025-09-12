using SmartMeetingAssistant.Core.Models;

namespace SmartMeetingAssistant.Core.Interfaces;

public interface ITranscriptionService : IDisposable
{
    event EventHandler<TranscriptionEventArgs>? TranscriptionReceived;
    event EventHandler<string>? ErrorOccurred;
    
    bool IsTranscribing { get; }
    
    Task StartTranscriptionAsync();
    Task StopTranscriptionAsync();
    Task<string> TranscribeAudioFileAsync(string filePath);
    void ProcessAudioData(byte[] audioData, int sampleRate, int channels);
}

public class TranscriptionEventArgs : EventArgs
{
    public TranscriptSegment Segment { get; }
    public bool IsFinal { get; }
    public double Confidence { get; }

    public TranscriptionEventArgs(TranscriptSegment segment, bool isFinal, double confidence)
    {
        Segment = segment;
        IsFinal = isFinal;
        Confidence = confidence;
    }
}
