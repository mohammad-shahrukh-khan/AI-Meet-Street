using System.ComponentModel.DataAnnotations;

namespace SmartMeetingAssistant.Core.Models;

public class Question
{
    public int Id { get; set; }
    
    public int MeetingId { get; set; }
    public virtual Meeting Meeting { get; set; } = null!;
    
    [Required]
    [MaxLength(500)]
    public string QuestionText { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Context { get; set; }
    
    public QuestionType Type { get; set; } = QuestionType.Clarification;
    
    public QuestionPriority Priority { get; set; } = QuestionPriority.Medium;
    
    /// <summary>
    /// Confidence score from NLP extraction (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Whether this question has been addressed/answered
    /// </summary>
    public bool IsAnswered { get; set; } = false;
    
    /// <summary>
    /// The answer/response to this question if available
    /// </summary>
    [MaxLength(1000)]
    public string? Answer { get; set; }
    
    /// <summary>
    /// Reference to the transcript segment that generated this question
    /// </summary>
    public int? SourceTranscriptSegmentId { get; set; }
    public virtual TranscriptSegment? SourceTranscriptSegment { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; set; }
}

public enum QuestionType
{
    Clarification,      // Need more information or explanation
    Concern,           // Potential issue or worry
    Suggestion,        // Suggested improvement or alternative
    FollowUp,          // Follow-up question based on discussion
    Technical,         // Technical doubt or question
    Process,           // Process-related question
    Timeline,          // Timeline or deadline related
    Resource           // Resource or budget related
}

public enum QuestionPriority
{
    Low,
    Medium,
    High,
    Urgent
}
