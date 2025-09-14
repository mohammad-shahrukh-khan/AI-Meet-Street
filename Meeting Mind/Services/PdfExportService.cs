using MeetingMind.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

namespace MeetingMind.Services
{
    public class PdfExportService
    {
        public void ExportMeetingToPdf(MeetingSession session, string filePath)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Text($"Meeting Summary - {session.StartTime:yyyy-MM-dd HH:mm}").SemiBold().FontSize(18);
                    page.Content().Column(col =>
                    {
                        col.Item().Text("Transcript:").Bold();
                        col.Item().Text(session.Transcript ?? "");
                        col.Item().PaddingVertical(10);
                        col.Item().Text("Summary:").Bold();
                        if (session.Summary != null)
                        {
                            if (session.Summary.BulletedSummary?.Count > 0)
                            {
                                foreach (var s in session.Summary.BulletedSummary)
                                    col.Item().Text(s);
                            }
                            if (session.Summary.KeyDecisions?.Count > 0)
                            {
                                col.Item().Text("Key Decisions:").Bold();
                                foreach (var d in session.Summary.KeyDecisions)
                                    col.Item().Text(d);
                            }
                            if (session.Summary.ActionItems?.Count > 0)
                            {
                                col.Item().Text("Action Items:").Bold();
                                foreach (var a in session.Summary.ActionItems)
                                    col.Item().Text($"{a.Description} (Owner: {a.Owner})");
                            }
                            if (session.Summary.FollowUps?.Count > 0)
                            {
                                col.Item().Text("Follow-ups:").Bold();
                                foreach (var f in session.Summary.FollowUps)
                                    col.Item().Text(f);
                            }
                        }
                    });
                });
            }).GeneratePdf(filePath);
        }
    }
}
