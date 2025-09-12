using System.ComponentModel.DataAnnotations;

namespace SmartMeetingAssistant.Core.Models;

public class KeyPoint
{
    public int Id { get; set; }
    
    public int MeetingId { get; set; }
    public virtual Meeting Meeting { get; set; } = null!;
    
    [Required]
    [MaxLength(500)]
    public string Summary { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Details { get; set; }
    
    public KeyPointCategory Category { get; set; } = KeyPointCategory.General;
    
    /// <summary>
    /// Confidence score from NLP extraction (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Reference to the transcript segment that generated this key point
    /// </summary>
    public int? SourceTranscriptSegmentId { get; set; }
    public virtual TranscriptSegment? SourceTranscriptSegment { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum KeyPointCategory
{
    General,
    Technical,
    Business,
    Strategic,
    Risk,
    Opportunity
}
