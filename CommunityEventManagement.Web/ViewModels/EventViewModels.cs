using System.ComponentModel.DataAnnotations;
using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Enums;   // ← ADD THIS (fixes CS0103)

namespace CommunityEventManagement.Web.ViewModels
{
    public class EventViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? VenueName { get; set; }
        public string? VenueLocation { get; set; }
        public List<VenueInfo> Venues { get; set; } = new();
        public int ParticipantCount { get; set; }
        public bool IsUpcoming { get; set; }
        public bool IsActive { get; set; }
        public bool IsPast { get; set; }
        public string DurationFormatted { get; set; } = string.Empty;
        public List<string> ActivityNames { get; set; } = new();
        public string StatusBadge => IsActive ? "Live" : IsUpcoming ? "Upcoming" : "Past";
        public string StatusColour => IsActive ? "success" : IsUpcoming ? "primary" : "secondary";
        public string VenueNamesDisplay =>
            Venues.Any() ? string.Join(", ", Venues.Select(v => v.Name)) : "No venue";

        public static EventViewModel FromEntity(Event e)
        {
            var eventVenues = e.EventVenues?.ToList() ?? new List<EventVenue>();
            var venues = eventVenues
                .Where(ev => ev.Venue != null)
                .Select(ev => new VenueInfo
                {
                    Id = ev.Venue!.Id,
                    Name = ev.Venue.Name ?? "",
                    Location = ev.Venue.GetLocation() ?? "",
                    City = ev.Venue.City ?? "",
                    IsPrimary = ev.IsPrimary,
                    Capacity = ev.Venue.MaxCapacity,
                    CurrentCapacity = ev.Venue.CurrentCapacity
                }).ToList();

            var primary = eventVenues.FirstOrDefault(ev => ev.IsPrimary)?.Venue;

            var activities = e.EventActivities?
                .Where(ea => ea.Activity != null)
                .Select(ea => ea.Activity!.Name ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? new List<string>();

            var registrations = e.Registrations?
                .Where(r => r.Status == RegistrationStatus.Confirmed)   // ← NOW works with `using`
                .Count() ?? 0;

            return new EventViewModel
            {
                Id = e.Id,
                Name = e.Name ?? "",
                Description = e.Description ?? "",
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                VenueName = primary?.Name,
                VenueLocation = primary?.GetLocation(),
                Venues = venues,
                ParticipantCount = registrations,
                IsUpcoming = e.IsUpcoming(),
                IsActive = e.IsActive(),
                IsPast = e.IsPast(),
                DurationFormatted = FormatDuration(e.GetDuration()),
                ActivityNames = activities
            };
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1) return $"{(int)duration.TotalDays} day(s)";
            if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours} hour(s)";
            return $"{(int)duration.TotalMinutes} minute(s)";
        }
    }

    public class VenueInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public int Capacity { get; set; }

        public int CurrentCapacity { get; set; }
    }

    public class CreateEventModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Event name is required")]
        [StringLength(100, MinimumLength = 3,
            ErrorMessage = "Event name must be between 3 and 100 characters")]
        [Display(Name = "Event Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date is required")]
        [Display(Name = "Start Date & Time")]
        public DateTime StartDate { get; set; } = DateTime.Now.AddDays(7);

        [Required(ErrorMessage = "End date is required")]
        [Display(Name = "End Date & Time")]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(7).AddHours(3);

        [Display(Name = "Venues")]
        public List<int> SelectedVenueIds { get; set; } = new();

        [Display(Name = "Primary Venue")]
        public int? PrimaryVenueId { get; set; }

        [Display(Name = "Legacy Venue Id")]
        public int? VenueId { get; set; }

        [Display(Name = "Activities")]
        public List<int> SelectedActivityIds { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDate <= StartDate)
                yield return new ValidationResult("End date must be after start date.", new[] { nameof(EndDate) });
            if (StartDate < DateTime.Now.AddMinutes(-5))
                yield return new ValidationResult("Start date cannot be in the past.", new[] { nameof(StartDate) });
            if ((EndDate - StartDate).TotalMinutes < 30)
                yield return new ValidationResult("Event must be at least 30 minutes long.", new[] { nameof(EndDate) });
            if (SelectedVenueIds.Any() && PrimaryVenueId.HasValue &&
                !SelectedVenueIds.Contains(PrimaryVenueId.Value))
                yield return new ValidationResult("Primary venue must be one of the selected venues.", new[] { nameof(PrimaryVenueId) });
        }

        public static CreateEventModel FromEntity(Event e)
        {
            return new CreateEventModel
            {
                Id = e.Id,
                Name = e.Name ?? "",
                Description = e.Description ?? "",
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                VenueId = e.VenueId,
                SelectedVenueIds = e.EventVenues?.Select(ev => ev.VenueId).ToList() ?? new(),
                PrimaryVenueId = e.EventVenues?.FirstOrDefault(ev => ev.IsPrimary)?.VenueId,
                SelectedActivityIds = e.EventActivities?.Select(ea => ea.ActivityId).ToList() ?? new()
            };
        }
    }

    public class EventFilterModel
    {
        [Display(Name = "Search")]
        public string? SearchTerm { get; set; }

        [Display(Name = "From Date")]
        public DateTime? StartDate { get; set; }

        [Display(Name = "To Date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Venue")]
        public int? VenueId { get; set; }

        [Display(Name = "Venues (any)")]
        public List<int> SelectedVenueIds { get; set; } = new();

        [Display(Name = "Activity Type")]
        public ActivityType? ActivityType { get; set; }

        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(SearchTerm) ||
            StartDate.HasValue || EndDate.HasValue ||
            VenueId.HasValue || SelectedVenueIds.Any() ||
            ActivityType.HasValue;

        public Domain.Models.FilterCriteria ToFilterCriteria()
        {
            var ids = new List<int>(SelectedVenueIds);
            if (VenueId.HasValue && !ids.Contains(VenueId.Value))
                ids.Add(VenueId.Value);
            return new Domain.Models.FilterCriteria
            {
                SearchTerm = SearchTerm,
                StartDate = StartDate,
                EndDate = EndDate,
                VenueId = VenueId,
                VenueIds = ids,
                ActivityType = ActivityType
            };
        }
    }

    public class EventDetailViewModel : EventViewModel
    {
        // No `new` keyword — these don't override anything in the parent.
        public List<RegistrationViewModel> Registrations { get; set; } = new();
        public List<ActivityViewModel> Activities { get; set; } = new();
        public int? VenueMaxCapacity { get; set; }
        public decimal VenueOccupancyRate { get; set; }

        public new static EventDetailViewModel FromEntity(Event e)
        {
            var detail = new EventDetailViewModel
            {
                Id = e.Id,
                Name = e.Name ?? "",
                Description = e.Description ?? "",
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                ParticipantCount = 0,
                IsUpcoming = e.IsUpcoming(),
                IsActive = e.IsActive(),
                IsPast = e.IsPast(),
                DurationFormatted = FormatDuration(e.GetDuration())
            };

            // Venues — null-safe
            var eventVenues = e.EventVenues?.ToList() ?? new List<EventVenue>();
            detail.Venues = eventVenues
                .Where(ev => ev.Venue != null)
                .Select(ev => new VenueInfo
                {
                    Id = ev.Venue!.Id,
                    Name = ev.Venue.Name ?? "",
                    Location = ev.Venue.GetLocation() ?? "",
                    City = ev.Venue.City ?? "",
                    IsPrimary = ev.IsPrimary,
                    Capacity = ev.Venue.MaxCapacity,
                     CurrentCapacity = ev.Venue.CurrentCapacity
                }).ToList();

            var primary = eventVenues.FirstOrDefault(ev => ev.IsPrimary)?.Venue;
            detail.VenueName = primary?.Name;
            detail.VenueLocation = primary?.GetLocation();
            detail.VenueMaxCapacity = primary?.MaxCapacity;
            detail.VenueOccupancyRate = primary?.GetOccupancyRate() ?? 0;

            // Activities — null-safe
            detail.Activities = (e.EventActivities ?? new List<EventActivity>())
                .Where(ea => ea.Activity != null)
                .Select(ea => ActivityViewModel.FromEntity(ea.Activity!))
                .ToList();
            detail.ActivityNames = detail.Activities.Select(a => a.Name).ToList();

            // Registrations — null-safe
            detail.Registrations = (e.Registrations ?? new List<Registration>())
                .Select(RegistrationViewModel.FromEntity)
                .ToList();
            detail.ParticipantCount = detail.Registrations
                .Count(r => r.Status == RegistrationStatus.Confirmed);

            return detail;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1) return $"{(int)duration.TotalDays} day(s)";
            if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours} hour(s)";
            return $"{(int)duration.TotalMinutes} minute(s)";
        }
    }
}