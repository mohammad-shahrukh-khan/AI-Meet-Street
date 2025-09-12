using SmartMeetingAssistant.Core.Models;

namespace SmartMeetingAssistant.Core.Interfaces;

public interface INLPService
{
    Task<List<ActionItem>> ExtractActionItemsAsync(string text, int meetingId);
    Task<List<KeyPoint>> ExtractKeyPointsAsync(string text, int meetingId);
    Task<List<Decision>> ExtractDecisionsAsync(string text, int meetingId);
    Task<List<Question>> ExtractQuestionsAsync(string text, int meetingId);
    Task<string> SummarizeTranscriptAsync(List<TranscriptSegment> segments);
    Task<List<string>> ExtractTopicsAsync(string text);
    Task<double> AnalyzeSentimentAsync(string text);
}

public class NLPInsights
{
    public List<ActionItem> ActionItems { get; set; } = new();
    public List<KeyPoint> KeyPoints { get; set; } = new();
    public List<Decision> Decisions { get; set; } = new();
    public List<Question> Questions { get; set; } = new();
    public List<string> Topics { get; set; } = new();
    public double OverallSentiment { get; set; }
    public string Summary { get; set; } = string.Empty;
}
