using CommunityEventManagement.Domain.Enums;

namespace CommunityEventManagement.Domain.Models
{
    /// <summary>
    /// Encapsulates all possible filter options for querying events.
    /// </summary>
    public class FilterCriteria
    {
        public string? SearchTerm { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Legacy single-venue filter
        public int? VenueId { get; set; }

        /// <summary>
        /// M:N venue filter — event matches if ANY of its assigned venues is in this list.
        /// </summary>
        public List<int> VenueIds { get; set; } = new();

        public ActivityType? ActivityType { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public bool HasVenueFilter => VenueId.HasValue || VenueIds.Any();

        public bool HasFilters =>
            StartDate.HasValue ||
            EndDate.HasValue ||
            HasVenueFilter ||
            ActivityType.HasValue ||
            !string.IsNullOrWhiteSpace(SearchTerm);

        public string GetFilterSummary()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(SearchTerm))
                parts.Add($"Search: '{SearchTerm}'");
            if (StartDate.HasValue)
                parts.Add($"From: {StartDate:dd/MM/yyyy}");
            if (EndDate.HasValue)
                parts.Add($"To: {EndDate:dd/MM/yyyy}");
            if (VenueId.HasValue)
                parts.Add($"Venue ID: {VenueId}");
            else if (VenueIds.Any())
                parts.Add($"Venues: {VenueIds.Count} selected");
            if (ActivityType.HasValue)
                parts.Add($"Activity: {ActivityType}");
            return parts.Count > 0
                ? string.Join(" | ", parts)
                : "No filters applied";
        }
    }
}