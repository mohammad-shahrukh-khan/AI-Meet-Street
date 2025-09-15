using System;
using System.Collections.Generic;

namespace MeetingMind.Models
{
    public class MeetingSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
    public string? Transcript { get; set; }
    public MeetingSummary? Summary { get; set; }
    }

    public class MeetingSummary
    {
        public List<string> KeyDecisions { get; set; } = new();
        public List<ActionItem> ActionItems { get; set; } = new();
        public List<string> FollowUps { get; set; } = new();
        public List<string> BulletedSummary { get; set; } = new();
    }

    public class ActionItem
    {
    public string? Description { get; set; }
    public string? Owner { get; set; }
    }
}
