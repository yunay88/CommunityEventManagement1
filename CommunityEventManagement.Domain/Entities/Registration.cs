using System.ComponentModel.DataAnnotations;
using CommunityEventManagement.Domain.Enums;
using CommunityEventManagement.Domain.Exceptions;

namespace CommunityEventManagement.Domain.Entities
{
    public class Registration : BaseEntity
    {
        public int EventId { get; private set; }
        public Event Event { get; private set; } = null!;

        public int ParticipantId { get; private set; }
        public Participant Participant { get; private set; } = null!;

        public DateTime RegistrationDate { get; private set; }
        public RegistrationStatus Status { get; private set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; private set; }

        public DateTime? StatusChangedAt { get; private set; }

        private Registration() { }

        public Registration(int eventId, int participantId, string? notes = null)
        {
            if (eventId <= 0) throw new ArgumentException("Invalid Event ID", nameof(eventId));
            if (participantId <= 0) throw new ArgumentException("Invalid Participant ID", nameof(participantId));

            EventId = eventId;
            ParticipantId = participantId;
            RegistrationDate = DateTime.UtcNow;
            Status = RegistrationStatus.Pending;
            Notes = notes?.Trim();
        }

        public void Confirm()
        {
            if (Status == RegistrationStatus.Cancelled)
                throw new RegistrationException("Cannot confirm a registration that has been cancelled.");

            Status = RegistrationStatus.Confirmed;
            StatusChangedAt = DateTime.UtcNow;
            MarkAsUpdated();
        }

        public void Cancel(string? reason = null)
        {
            if (Status == RegistrationStatus.Cancelled)
                throw new RegistrationException("This registration is already cancelled.");

            Status = RegistrationStatus.Cancelled;
            Notes = reason?.Trim() ?? Notes;
            StatusChangedAt = DateTime.UtcNow;
            MarkAsUpdated();
        }

        public void Waitlist()
        {
            Status = RegistrationStatus.Waitlisted;
            StatusChangedAt = DateTime.UtcNow;
            MarkAsUpdated();
        }

        // <-- ENTERPRISE FIX: DEDICATED REACTIVATION METHODS (KEEPS UNIT TESTS 100% GREEN)
        public void Reactivate(string? notes = null)
        {
            Status = RegistrationStatus.Confirmed;
            Notes = notes?.Trim() ?? Notes;
            StatusChangedAt = DateTime.UtcNow;
            MarkAsUpdated();
        }

        public void ReactivateAsWaitlisted(string? notes = null)
        {
            Status = RegistrationStatus.Waitlisted;
            Notes = notes?.Trim() ?? Notes;
            StatusChangedAt = DateTime.UtcNow;
            MarkAsUpdated();
        }

        public bool IsConfirmed() => Status == RegistrationStatus.Confirmed;
        public bool IsCancelled() => Status == RegistrationStatus.Cancelled;
        public bool IsPending() => Status == RegistrationStatus.Pending;

        public override string GetDisplayName() => $"Registration #{Id}: Participant {ParticipantId} → Event {EventId}";

        public override string GetSummary()
        {
            return $"Registration | Participant: {Participant?.GetFullName() ?? ParticipantId.ToString()} | Event: {Event?.Name ?? EventId.ToString()} | Date: {RegistrationDate:dd/MM/yyyy} | Status: {Status}";
        }
    }
}