using SmartMeetingAssistant.Core.Models;

namespace SmartMeetingAssistant.Core.Interfaces;

public interface IExportService
{
    Task<string> ExportToPdfAsync(Meeting meeting, string filePath);
    Task<string> ExportToWordAsync(Meeting meeting, string filePath);
    Task<string> ExportToMarkdownAsync(Meeting meeting, string filePath);
    Task<string> ExportToJsonAsync(Meeting meeting, string filePath);
}

public class ExportOptions
{
    public bool IncludeTranscript { get; set; } = true;
    public bool IncludeActionItems { get; set; } = true;
    public bool IncludeKeyPoints { get; set; } = true;
    public bool IncludeDecisions { get; set; } = true;
    public bool IncludeTimestamps { get; set; } = true;
    public bool IncludeConfidenceScores { get; set; } = false;
    public string? CompanyLogo { get; set; }
    public string? CompanyName { get; set; }
}
