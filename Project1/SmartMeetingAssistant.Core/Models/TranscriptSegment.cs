using System.ComponentModel.DataAnnotations;

namespace SmartMeetingAssistant.Core.Models;

public class TranscriptSegment
{
    public int Id { get; set; }
    
    public int MeetingId { get; set; }
    public virtual Meeting Meeting { get; set; } = null!;
    
    [Required]
    public string Text { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Speaker { get; set; }
    
    public DateTime Timestamp { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    
    /// <summary>
    /// Confidence score from speech recognition (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Whether this segment is final or still being processed
    /// </summary>
    public bool IsFinal { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
