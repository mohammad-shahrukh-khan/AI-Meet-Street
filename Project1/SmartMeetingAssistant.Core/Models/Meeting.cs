using System.ComponentModel.DataAnnotations;

namespace SmartMeetingAssistant.Core.Models;

public class Meeting
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Participants { get; set; }
    
    [MaxLength(1000)]
    public string? Agenda { get; set; }
    
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    public MeetingStatus Status { get; set; } = MeetingStatus.Planned;
    
    [MaxLength(50)]
    public string? AudioFilePath { get; set; }
    
    // Navigation properties
    public virtual ICollection<TranscriptSegment> TranscriptSegments { get; set; } = new List<TranscriptSegment>();
    public virtual ICollection<ActionItem> ActionItems { get; set; } = new List<ActionItem>();
    public virtual ICollection<KeyPoint> KeyPoints { get; set; } = new List<KeyPoint>();
    public virtual ICollection<Decision> Decisions { get; set; } = new List<Decision>();
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum MeetingStatus
{
    Planned,
    InProgress,
    Completed,
    Cancelled
}
