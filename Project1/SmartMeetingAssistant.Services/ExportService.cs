using iTextSharp.text;
using iTextSharp.text.pdf;
using Markdig;
using SmartMeetingAssistant.Core.Interfaces;
using SmartMeetingAssistant.Core.Models;
using System.Text;
using System.Text.Json;

namespace SmartMeetingAssistant.Services;

public class ExportService : IExportService
{
    public async Task<string> ExportToPdfAsync(Meeting meeting, string filePath)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStream = new FileStream(filePath, FileMode.Create);
            var document = new Document(PageSize.A4, 36, 36, 54, 54);
            var writer = PdfWriter.GetInstance(document, fileStream);

            document.Open();

            // Title
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.Black);
            var title = new Paragraph($"Meeting Report: {meeting.Title}", titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            document.Add(title);

            // Meeting details
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.Black);
            var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.Black);

            document.Add(new Paragraph($"Date: {meeting.StartTime:yyyy-MM-dd HH:mm}", normalFont) { SpacingAfter = 5 });
            if (meeting.EndTime.HasValue)
            {
                document.Add(new Paragraph($"Duration: {meeting.EndTime.Value - meeting.StartTime:hh\\:mm\\:ss}", normalFont) { SpacingAfter = 5 });
            }
            if (!string.IsNullOrEmpty(meeting.Participants))
            {
                document.Add(new Paragraph($"Participants: {meeting.Participants}", normalFont) { SpacingAfter = 10 });
            }

            // Agenda
            if (!string.IsNullOrEmpty(meeting.Agenda))
            {
                document.Add(new Paragraph("Agenda", boldFont) { SpacingAfter = 5 });
                document.Add(new Paragraph(meeting.Agenda, normalFont) { SpacingAfter = 15 });
            }

            // Action Items
            if (meeting.ActionItems.Any())
            {
                document.Add(new Paragraph("Action Items", boldFont) { SpacingAfter = 10 });
                foreach (var item in meeting.ActionItems)
                {
                    var actionText = $"• {item.Description}";
                    if (!string.IsNullOrEmpty(item.AssignedTo))
                        actionText += $" (Assigned to: {item.AssignedTo})";
                    if (item.DueDate.HasValue)
                        actionText += $" (Due: {item.DueDate.Value:yyyy-MM-dd})";
                    
                    document.Add(new Paragraph(actionText, normalFont) { SpacingAfter = 5 });
                }
                document.Add(new Paragraph(" ", normalFont) { SpacingAfter = 10 });
            }

            // Key Points
            if (meeting.KeyPoints.Any())
            {
                document.Add(new Paragraph("Key Points", boldFont) { SpacingAfter = 10 });
                foreach (var point in meeting.KeyPoints)
                {
                    document.Add(new Paragraph($"• {point.Summary}", normalFont) { SpacingAfter = 5 });
                    if (!string.IsNullOrEmpty(point.Details))
                    {
                        document.Add(new Paragraph($"  {point.Details}", normalFont) { SpacingAfter = 5 });
                    }
                }
                document.Add(new Paragraph(" ", normalFont) { SpacingAfter = 10 });
            }

            // Decisions
            if (meeting.Decisions.Any())
            {
                document.Add(new Paragraph("Decisions", boldFont) { SpacingAfter = 10 });
                foreach (var decision in meeting.Decisions)
                {
                    var decisionText = $"• {decision.Summary}";
                    if (!string.IsNullOrEmpty(decision.DecisionMaker))
                        decisionText += $" (Decision by: {decision.DecisionMaker})";
                    
                    document.Add(new Paragraph(decisionText, normalFont) { SpacingAfter = 5 });
                    if (!string.IsNullOrEmpty(decision.Details))
                    {
                        document.Add(new Paragraph($"  {decision.Details}", normalFont) { SpacingAfter = 5 });
                    }
                }
                document.Add(new Paragraph(" ", normalFont) { SpacingAfter = 10 });
            }

            // Transcript
            if (meeting.TranscriptSegments.Any())
            {
                document.Add(new Paragraph("Meeting Transcript", boldFont) { SpacingAfter = 10 });
                foreach (var segment in meeting.TranscriptSegments.OrderBy(s => s.Timestamp))
                {
                    var timestamp = segment.Timestamp.ToString("HH:mm:ss");
                    var transcriptText = $"[{timestamp}] {segment.Text}";
                    document.Add(new Paragraph(transcriptText, normalFont) { SpacingAfter = 3 });
                }
            }

            document.Close();
            return filePath;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to export to PDF: {ex.Message}", ex);
        }
    }

    public async Task<string> ExportToWordAsync(Meeting meeting, string filePath)
    {
        // For now, we'll create a simple text-based format
        // In a full implementation, you'd use a library like DocumentFormat.OpenXml
        try
        {
            var content = GenerateReportContent(meeting);
            await File.WriteAllTextAsync(filePath, content);
            return filePath;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to export to Word: {ex.Message}", ex);
        }
    }

    public async Task<string> ExportToMarkdownAsync(Meeting meeting, string filePath)
    {
        try
        {
            var markdown = new StringBuilder();
            
            // Title
            markdown.AppendLine($"# Meeting Report: {meeting.Title}");
            markdown.AppendLine();

            // Meeting details
            markdown.AppendLine($"**Date:** {meeting.StartTime:yyyy-MM-dd HH:mm}");
            if (meeting.EndTime.HasValue)
            {
                markdown.AppendLine($"**Duration:** {meeting.EndTime.Value - meeting.StartTime:hh\\:mm\\:ss}");
            }
            if (!string.IsNullOrEmpty(meeting.Participants))
            {
                markdown.AppendLine($"**Participants:** {meeting.Participants}");
            }
            markdown.AppendLine();

            // Agenda
            if (!string.IsNullOrEmpty(meeting.Agenda))
            {
                markdown.AppendLine("## Agenda");
                markdown.AppendLine();
                markdown.AppendLine(meeting.Agenda);
                markdown.AppendLine();
            }

            // Action Items
            if (meeting.ActionItems.Any())
            {
                markdown.AppendLine("## Action Items");
                markdown.AppendLine();
                foreach (var item in meeting.ActionItems)
                {
                    var actionText = $"- {item.Description}";
                    if (!string.IsNullOrEmpty(item.AssignedTo))
                        actionText += $" (Assigned to: {item.AssignedTo})";
                    if (item.DueDate.HasValue)
                        actionText += $" (Due: {item.DueDate.Value:yyyy-MM-dd})";
                    
                    markdown.AppendLine(actionText);
                }
                markdown.AppendLine();
            }

            // Key Points
            if (meeting.KeyPoints.Any())
            {
                markdown.AppendLine("## Key Points");
                markdown.AppendLine();
                foreach (var point in meeting.KeyPoints)
                {
                    markdown.AppendLine($"- **{point.Summary}**");
                    if (!string.IsNullOrEmpty(point.Details))
                    {
                        markdown.AppendLine($"  {point.Details}");
                    }
                }
                markdown.AppendLine();
            }

            // Decisions
            if (meeting.Decisions.Any())
            {
                markdown.AppendLine("## Decisions");
                markdown.AppendLine();
                foreach (var decision in meeting.Decisions)
                {
                    var decisionText = $"- **{decision.Summary}**";
                    if (!string.IsNullOrEmpty(decision.DecisionMaker))
                        decisionText += $" (Decision by: {decision.DecisionMaker})";
                    
                    markdown.AppendLine(decisionText);
                    if (!string.IsNullOrEmpty(decision.Details))
                    {
                        markdown.AppendLine($"  {decision.Details}");
                    }
                }
                markdown.AppendLine();
            }

            // Transcript
            if (meeting.TranscriptSegments.Any())
            {
                markdown.AppendLine("## Meeting Transcript");
                markdown.AppendLine();
                foreach (var segment in meeting.TranscriptSegments.OrderBy(s => s.Timestamp))
                {
                    var timestamp = segment.Timestamp.ToString("HH:mm:ss");
                    markdown.AppendLine($"**[{timestamp}]** {segment.Text}");
                    markdown.AppendLine();
                }
            }

            await File.WriteAllTextAsync(filePath, markdown.ToString());
            return filePath;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to export to Markdown: {ex.Message}", ex);
        }
    }

    public async Task<string> ExportToJsonAsync(Meeting meeting, string filePath)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(meeting, options);
            await File.WriteAllTextAsync(filePath, json);
            return filePath;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to export to JSON: {ex.Message}", ex);
        }
    }

    private string GenerateReportContent(Meeting meeting)
    {
        var content = new StringBuilder();
        
        content.AppendLine($"MEETING REPORT: {meeting.Title.ToUpper()}");
        content.AppendLine(new string('=', 50));
        content.AppendLine();

        content.AppendLine($"Date: {meeting.StartTime:yyyy-MM-dd HH:mm}");
        if (meeting.EndTime.HasValue)
        {
            content.AppendLine($"Duration: {meeting.EndTime.Value - meeting.StartTime:hh\\:mm\\:ss}");
        }
        if (!string.IsNullOrEmpty(meeting.Participants))
        {
            content.AppendLine($"Participants: {meeting.Participants}");
        }
        content.AppendLine();

        if (!string.IsNullOrEmpty(meeting.Agenda))
        {
            content.AppendLine("AGENDA");
            content.AppendLine(new string('-', 20));
            content.AppendLine(meeting.Agenda);
            content.AppendLine();
        }

        if (meeting.ActionItems.Any())
        {
            content.AppendLine("ACTION ITEMS");
            content.AppendLine(new string('-', 20));
            foreach (var item in meeting.ActionItems)
            {
                var actionText = $"• {item.Description}";
                if (!string.IsNullOrEmpty(item.AssignedTo))
                    actionText += $" (Assigned to: {item.AssignedTo})";
                if (item.DueDate.HasValue)
                    actionText += $" (Due: {item.DueDate.Value:yyyy-MM-dd})";
                
                content.AppendLine(actionText);
            }
            content.AppendLine();
        }

        if (meeting.KeyPoints.Any())
        {
            content.AppendLine("KEY POINTS");
            content.AppendLine(new string('-', 20));
            foreach (var point in meeting.KeyPoints)
            {
                content.AppendLine($"• {point.Summary}");
                if (!string.IsNullOrEmpty(point.Details))
                {
                    content.AppendLine($"  {point.Details}");
                }
            }
            content.AppendLine();
        }

        if (meeting.Decisions.Any())
        {
            content.AppendLine("DECISIONS");
            content.AppendLine(new string('-', 20));
            foreach (var decision in meeting.Decisions)
            {
                var decisionText = $"• {decision.Summary}";
                if (!string.IsNullOrEmpty(decision.DecisionMaker))
                    decisionText += $" (Decision by: {decision.DecisionMaker})";
                
                content.AppendLine(decisionText);
                if (!string.IsNullOrEmpty(decision.Details))
                {
                    content.AppendLine($"  {decision.Details}");
                }
            }
            content.AppendLine();
        }

        if (meeting.TranscriptSegments.Any())
        {
            content.AppendLine("MEETING TRANSCRIPT");
            content.AppendLine(new string('-', 30));
            foreach (var segment in meeting.TranscriptSegments.OrderBy(s => s.Timestamp))
            {
                var timestamp = segment.Timestamp.ToString("HH:mm:ss");
                content.AppendLine($"[{timestamp}] {segment.Text}");
            }
        }

        return content.ToString();
    }
}
