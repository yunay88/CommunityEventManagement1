namespace CommunityEventManagement.Domain.Entities
{
    /// <summary>
    /// Notification domain entity — represents system messages and alerts.
    /// Demonstrates: Polyglot Persistence (Document/JSON persistence).
    /// </summary>
    public class Notification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int? ParticipantId { get; set; } // If null, it's a broadcast to ALL participants!
        public int? EventId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // EventCreated, EventUpdated, EventApproaching, RegistrationConfirmed, WaitlistPromoted
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;

        public string GetTimeAgo()
        {
            var span = DateTime.UtcNow - CreatedAt;
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }

        public string GetIconClass() => Type switch
        {
            "EventCreated" => "bi-calendar-plus text-primary",
            "EventUpdated" => "bi-calendar-check text-warning",
            "EventApproaching" => "bi-clock-history text-info",
            "RegistrationConfirmed" => "bi-shield-check text-success",
            "WaitlistPromoted" => "bi-award text-success",
            "RegistrationCancelled" => "bi-x-circle text-danger",
            _ => "bi-bell text-secondary"
        };
    }
}