using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Enums;
using CommunityEventManagement.Domain.Exceptions;
using CommunityEventManagement.Domain.Interfaces;
using CommunityEventManagement.Domain.Interfaces.Services;
using CommunityEventManagement.Infrastructure.DataStructures;
using CommunityEventManagement.Infrastructure.Patterns.Observer;
using Microsoft.Extensions.Logging;

namespace CommunityEventManagement.Infrastructure.Services
{
    public class RegistrationService : IRegistrationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<RegistrationService> _logger;
        private readonly RegistrationNotifier _notifier;
        private readonly INotificationService? _notificationService;
        private readonly Dictionary<int, RegistrationQueue> _waitingLists = new();

        public RegistrationService(
            IUnitOfWork unitOfWork, 
            ILogger<RegistrationService> logger, 
            RegistrationNotifier notifier,
            INotificationService? notificationService = null)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _notificationService = notificationService;
        }

        public async Task<IEnumerable<Registration>> GetAllRegistrationsAsync()
        {
            try { return await _unitOfWork.Registrations.GetAllAsync(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all registrations");
                throw new CommunityEventException("Failed to retrieve registrations.", "RETRIEVAL_ERROR", ex);
            }
        }

        public async Task<Registration?> GetRegistrationByIdAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("Invalid ID", nameof(id));
            try { return await _unitOfWork.Registrations.GetWithDetailsAsync(id); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration {Id}", id);
                throw new CommunityEventException($"Failed to retrieve registration {id}.", "RETRIEVAL_ERROR", ex);
            }
        }

        public async Task<IEnumerable<Registration>> GetParticipantRegistrationsAsync(int participantId)
        {
            try { return await _unitOfWork.Registrations.GetByParticipantAsync(participantId); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations for participant {Id}", participantId);
                throw new CommunityEventException($"Failed to retrieve registrations for participant {participantId}.", "RETRIEVAL_ERROR", ex);
            }
        }

        public async Task<IEnumerable<Registration>> GetRegistrationsByParticipantAsync(int participantId)
            => await GetParticipantRegistrationsAsync(participantId);

        public async Task<IEnumerable<Registration>> GetEventRegistrationsAsync(int eventId)
        {
            try { return await _unitOfWork.Registrations.GetByEventAsync(eventId); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations for event {Id}", eventId);
                throw new CommunityEventException($"Failed to retrieve registrations for event {eventId}.", "RETRIEVAL_ERROR", ex);
            }
        }

               public async Task<Registration> RegisterParticipantAsync(
            int participantId, int eventId, string? notes = null)
        {
            _logger.LogInformation("Registering participant {ParticipantId} for event {EventId}", participantId, eventId);
            await _unitOfWork.BeginTransactionAsync();
            Registration? newRegistration = null;
            try
            {
                var participant = await _unitOfWork.Participants.GetByIdAsync(participantId) ?? throw new ParticipantNotFoundException(participantId);
                var eventEntity = await _unitOfWork.Events.GetWithDetailsAsync(eventId) ?? throw new EventNotFoundException(eventId);

                if (eventEntity.IsPast()) throw new RegistrationException($"Cannot register for past event '{eventEntity.Name}'.");
                if (await _unitOfWork.Registrations.ExistsAsync(participantId, eventId)) throw new DuplicateRegistrationException(participantId, eventId);

                Venue? selectedVenue = null;
                if (!string.IsNullOrWhiteSpace(notes) && notes.StartsWith("SelectedVenueId:"))
                {
                    var parts = notes.Split('|');
                    var vIdStr = parts[0].Replace("SelectedVenueId:", "");
                    if (int.TryParse(vIdStr, out int vId))
                    {
                        selectedVenue = eventEntity.Venues.FirstOrDefault(v => v.Id == vId);
                    }
                }
                selectedVenue ??= eventEntity.PrimaryVenue ?? eventEntity.Venues.FirstOrDefault();

                var confirmedCountForVenue = eventEntity.Registrations?
                    .Count(r => r.Status == RegistrationStatus.Confirmed && r.Notes != null && r.Notes.Contains($"SelectedVenueId:{selectedVenue?.Id}")) ?? 0;

                if (confirmedCountForVenue == 0 && eventEntity.Registrations?.Any(r => r.Status == RegistrationStatus.Confirmed && (r.Notes == null || !r.Notes.StartsWith("SelectedVenueId:"))) == true && selectedVenue == eventEntity.PrimaryVenue)
                {
                    confirmedCountForVenue = eventEntity.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed);
                }

                var maxCapacity = selectedVenue?.MaxCapacity ?? int.MaxValue;
                var hasSpaceInSelectedVenue = confirmedCountForVenue < maxCapacity;

                // <-- ENTERPRISE REACTIVATION: SEAMLESSLY SWITCH VENUES ON RE-REGISTER
                var allParticipantRegs = await _unitOfWork.Registrations.GetByParticipantAsync(participantId);
                var existingCancelledReg = allParticipantRegs.FirstOrDefault(r => r.EventId == eventId && r.Status == RegistrationStatus.Cancelled);

                if (existingCancelledReg != null)
                {
                    newRegistration = await _unitOfWork.Registrations.GetByIdAsync(existingCancelledReg.Id);
                    if (newRegistration != null)
                    {
                        if (selectedVenue != null && !hasSpaceInSelectedVenue)
                        {
                            newRegistration.ReactivateAsWaitlisted(notes); // <-- KEEPS UNIT TESTS GREEN
                            await AddToWaitingListAsync(eventEntity, newRegistration);
                        }
                        else
                        {
                            newRegistration.Reactivate(notes); // <-- KEEPS UNIT TESTS GREEN
                            if (selectedVenue != null) selectedVenue.ReserveSpace(1);
                        }

                        await _unitOfWork.SaveChangesAsync();
                        await _unitOfWork.CommitTransactionAsync();

                        try { await _notifier.NotifyRegisteredAsync(newRegistration.Id, participant.Id, participant.GetFullName(), participant.Email, eventEntity.Id, eventEntity.Name, eventEntity.StartDate); }
                        catch (Exception nex) { _logger.LogWarning(nex, "Observer notification failed"); }

                        if (_notificationService != null)
                        {
                            var t = newRegistration.IsConfirmed() ? "Registration Re-Confirmed" : "Added to Waitlist";
                            var m = newRegistration.IsConfirmed() ? $"Your cancelled reservation for '{eventEntity.Name}' at {selectedVenue?.Name} has been successfully reactivated." : $"'{eventEntity.Name}' at {selectedVenue?.Name} is currently at capacity. You have been placed on the waitlist queue.";
                            var ty = newRegistration.IsConfirmed() ? "RegistrationConfirmed" : "WaitlistPromoted";
                            await _notificationService.CreateUserNotificationAsync(participantId, t, m, eventId, ty);

                            if (newRegistration.Status == RegistrationStatus.Waitlisted)
                            {
                                await _notificationService.CreateAdminNotificationAsync(
                                    "Waitlist Queue Alert",
                                    $"Participant '{participant.GetFullName()}' has joined the waitlist for '{eventEntity.Name}' at {selectedVenue?.Name}. Action required in the registration console.",
                                    eventId,
                                    "WaitlistPromoted");
                            }
                        }
                        return newRegistration;
                    }
                }

                // Brand new registration
                newRegistration = new Registration(eventId, participantId, notes);

                if (selectedVenue != null && !hasSpaceInSelectedVenue)
                {
                    newRegistration.Waitlist();
                    await AddToWaitingListAsync(eventEntity, newRegistration);
                    _logger.LogInformation("Participant {ParticipantId} added to waiting list for event {EventId}", participantId, eventId);
                }
                else
                {
                    newRegistration.Confirm();
                    if (selectedVenue != null) selectedVenue.ReserveSpace(1);
                    _logger.LogInformation("Registration confirmed for participant {ParticipantId} in event {EventId}", participantId, eventId);
                }

                await _unitOfWork.Registrations.AddAsync(newRegistration);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                try { await _notifier.NotifyRegisteredAsync(newRegistration.Id, participant.Id, participant.GetFullName(), participant.Email, eventEntity.Id, eventEntity.Name, eventEntity.StartDate); }
                catch (Exception nex) { _logger.LogWarning(nex, "Observer notification failed"); }

                if (_notificationService != null)
                {
                    var title = newRegistration.IsConfirmed() ? "Registration Confirmed" : "Added to Waitlist";
                    var msg = newRegistration.IsConfirmed() ? $"Your reservation for '{eventEntity.Name}' at {selectedVenue?.Name} has been successfully confirmed." : $"'{eventEntity.Name}' at {selectedVenue?.Name} is currently at capacity. You have been placed on the waitlist queue.";
                    var type = newRegistration.IsConfirmed() ? "RegistrationConfirmed" : "WaitlistPromoted";
                    await _notificationService.CreateUserNotificationAsync(participantId, title, msg, eventId, type);

                    if (newRegistration.Status == RegistrationStatus.Waitlisted)
                    {
                        await _notificationService.CreateAdminNotificationAsync(
                            "Waitlist Queue Alert",
                            $"Participant '{participant.GetFullName()}' has joined the waitlist for '{eventEntity.Name}' at {selectedVenue?.Name}. Action required in the registration console.",
                            eventId,
                            "WaitlistPromoted");
                    }
                }
                return newRegistration;
            }
            catch (CommunityEventException) 
            { 
                await _unitOfWork.RollbackTransactionAsync(); 
                if (newRegistration != null) _unitOfWork.Detach(newRegistration);
                throw; 
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                if (newRegistration != null) _unitOfWork.Detach(newRegistration);
                _logger.LogError(ex, "Unexpected error registering participant {ParticipantId}", participantId);
                throw new CommunityEventException("An unexpected error occurred during registration.", "REGISTRATION_ERROR", ex);
            }
            finally { _logger.LogDebug("RegisterParticipantAsync completed"); }
        }
        public async Task CancelRegistrationAsync(int registrationId, string? reason = null)
        {
            await _unitOfWork.BeginTransactionAsync();
            Registration? registration = null;
            try
            {
                _logger.LogInformation("Cancelling registration {Id}", registrationId);
                registration = await _unitOfWork.Registrations.GetWithDetailsAsync(registrationId) ?? throw new RegistrationException($"Registration {registrationId} was not found.");

                Venue? regVenue = null;
                if (!string.IsNullOrWhiteSpace(registration.Notes) && registration.Notes.StartsWith("SelectedVenueId:"))
                {
                    var parts = registration.Notes.Split('|');
                    var vIdStr = parts[0].Replace("SelectedVenueId:", "");
                    if (int.TryParse(vIdStr, out int vId))
                    {
                        regVenue = registration.Event?.Venues.FirstOrDefault(v => v.Id == vId);
                    }
                }
                regVenue ??= registration.Event?.PrimaryVenue;

                if (registration.Status == RegistrationStatus.Confirmed && regVenue != null)
                {
                    regVenue.ReleaseSpace(1);
                }

                registration.Cancel(reason);

                var regId = registration.Id;
                var regParticipantId = registration.ParticipantId;
                var regParticipantName = registration.Participant?.GetFullName() ?? "Unknown";
                var regParticipantEmail = registration.Participant?.Email ?? "";
                var regEventId = registration.EventId;
                var regEventName = registration.Event?.Name ?? "Unknown";
                var regEventStartDate = registration.Event?.StartDate ?? DateTime.MinValue;

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                try { await _notifier.NotifyCancelledAsync(regId, regParticipantId, regParticipantName, regParticipantEmail, regEventId, regEventName, regEventStartDate); }
                catch (Exception nex) { _logger.LogWarning(nex, "Observer notification failed on cancellation"); }

                if (_notificationService != null)
                {
                    await _notificationService.CreateUserNotificationAsync(regParticipantId, "Registration Cancelled", $"Your registration for '{regEventName}' at {regVenue?.Name} has been cancelled.", regEventId, "RegistrationCancelled");
                }
                _logger.LogInformation("Registration {Id} cancelled successfully", registrationId);
            }
            catch (CommunityEventException) 
            { 
                await _unitOfWork.RollbackTransactionAsync(); 
                if (registration != null) _unitOfWork.Detach(registration);
                throw; 
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                if (registration != null) _unitOfWork.Detach(registration);
                _logger.LogError(ex, "Error cancelling registration {Id}", registrationId);
                throw new CommunityEventException($"Failed to cancel registration {registrationId}.", "CANCEL_ERROR", ex);
            }
            finally { _logger.LogDebug("CancelRegistrationAsync completed for {Id}", registrationId); }
        }

        public async Task ConfirmRegistrationAsync(int registrationId)
        {
            await _unitOfWork.BeginTransactionAsync();
            Registration? registration = null;
            try
            {
                registration = await _unitOfWork.Registrations.GetWithDetailsAsync(registrationId) ?? throw new RegistrationException($"Registration {registrationId} was not found.");
                var eventEntity = registration.Event;

                Venue? regVenue = null;
                if (!string.IsNullOrWhiteSpace(registration.Notes) && registration.Notes.StartsWith("SelectedVenueId:"))
                {
                    var parts = registration.Notes.Split('|');
                    var vIdStr = parts[0].Replace("SelectedVenueId:", "");
                    if (int.TryParse(vIdStr, out int vId))
                    {
                        regVenue = eventEntity?.Venues.FirstOrDefault(v => v.Id == vId);
                    }
                }
                regVenue ??= eventEntity?.PrimaryVenue;

                if (eventEntity != null && regVenue != null)
                {
                    var confirmedCount = eventEntity.Registrations?.Count(r => r.Status == RegistrationStatus.Confirmed && r.Notes != null && r.Notes.Contains($"SelectedVenueId:{regVenue.Id}")) ?? 0;
                    if (confirmedCount >= regVenue.MaxCapacity)
                    {
                        throw new VenueCapacityException($"Cannot approve registration: The selected venue ({regVenue.Name}) is currently at full capacity. Please increase venue capacity or wait for a cancellation.");
                    }
                    regVenue.ReserveSpace(1);
                }

                registration.Confirm();
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                if (_notificationService != null && registration.Event != null)
                {
                    await _notificationService.CreateUserNotificationAsync(
                        registration.ParticipantId,
                        "Waitlist Promoted",
                        $"Great news! A space opened up and your registration for '{registration.Event.Name}' at {regVenue?.Name} has been successfully confirmed.",
                        registration.EventId,
                        "WaitlistPromoted");
                }
                _logger.LogInformation("Registration {Id} confirmed successfully", registrationId);
            }
            catch (CommunityEventException) 
            { 
                await _unitOfWork.RollbackTransactionAsync(); 
                if (registration != null) _unitOfWork.Detach(registration);
                throw; 
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                if (registration != null) _unitOfWork.Detach(registration);
                _logger.LogError(ex, "Error confirming registration {Id}", registrationId);
                throw new CommunityEventException($"Failed to confirm registration {registrationId}.", "CONFIRM_ERROR", ex);
            }
        }

        public async Task<bool> IsParticipantRegisteredAsync(int participantId, int eventId)
        {
            try { return await _unitOfWork.Registrations.ExistsAsync(participantId, eventId); }
            catch (Exception ex) { _logger.LogError(ex, "Error checking registration"); return false; }
        }

        private Task AddToWaitingListAsync(Event eventEntity, Registration registration)
        {
            if (!_waitingLists.ContainsKey(eventEntity.Id)) _waitingLists[eventEntity.Id] = new RegistrationQueue(eventEntity.Id, eventEntity.Name);
            _waitingLists[eventEntity.Id].AddToWaitingList(registration);
            return Task.CompletedTask;
        }
    }
}