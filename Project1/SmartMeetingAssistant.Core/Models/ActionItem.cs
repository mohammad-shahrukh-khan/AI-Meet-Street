using System.ComponentModel.DataAnnotations;

namespace SmartMeetingAssistant.Core.Models;

public class ActionItem
{
    public int Id { get; set; }
    
    public int MeetingId { get; set; }
    public virtual Meeting Meeting { get; set; } = null!;
    
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? AssignedTo { get; set; }
    
    public DateTime? DueDate { get; set; }
    
    public ActionItemPriority Priority { get; set; } = ActionItemPriority.Medium;
    
    public ActionItemStatus Status { get; set; } = ActionItemStatus.Open;
    
    /// <summary>
    /// Confidence score from NLP extraction (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Reference to the transcript segment that generated this action item
    /// </summary>
    public int? SourceTranscriptSegmentId { get; set; }
    public virtual TranscriptSegment? SourceTranscriptSegment { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ActionItemPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum ActionItemStatus
{
    Open,
    InProgress,
    Completed,
    Cancelled
}
