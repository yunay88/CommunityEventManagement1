using System.ComponentModel.DataAnnotations;

namespace CommunityEventManagement.Domain.Entities
{
    /// <summary>
    /// EventVenue — junction entity for the many-to-many relationship
    /// between Event and Venue. The brief states that an Event may take
    /// place at "various Venues" (plural), so this junction is required.
    ///
    /// Composite primary key: (EventId, VenueId).
    /// Does NOT inherit from BaseEntity because it has no surrogate Id.
    /// </summary>
    public class EventVenue
    {
        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        public int VenueId { get; set; }
        public Venue Venue { get; set; } = null!;

        public bool IsPrimary { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        [Range(0, 100)]
        public int DisplayOrder { get; set; }
    }
}