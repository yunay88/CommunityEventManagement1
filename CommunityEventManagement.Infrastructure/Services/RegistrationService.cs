using CommunityEventManagement.Domain.Entities;
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

        // Queue data structure — manages waiting lists per event
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

        public async Task<IEnumerable<Registration>> GetRegistrationsByParticipantAsync(int participantId)
        {
            // Forward to canonical method – maintains single source of truth
            return await GetParticipantRegistrationsAsync(participantId);
        }

        public async Task<IEnumerable<Registration>> GetAllRegistrationsAsync()
        {
            try
            {
                return await _unitOfWork.Registrations.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all registrations");
                throw new CommunityEventException("Failed to retrieve registrations.", "RETRIEVAL_ERROR", ex);
            }
            finally { _logger.LogDebug("GetAllRegistrationsAsync completed"); }
        }

        public async Task<Registration?> GetRegistrationByIdAsync(int id)
        {
            try
            {
                return await _unitOfWork.Registrations.GetWithDetailsAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration {Id}", id);
                throw new CommunityEventException($"Failed to retrieve registration {id}.", "RETRIEVAL_ERROR", ex);
            }
        }

        public async Task<IEnumerable<Registration>> GetParticipantRegistrationsAsync(int participantId)
        {
            try
            {
                return await _unitOfWork.Registrations.GetByParticipantAsync(participantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations for participant {Id}", participantId);
                throw new CommunityEventException(
                    $"Failed to retrieve registrations for participant {participantId}.", "RETRIEVAL_ERROR", ex);
            }
        }

        public async Task<IEnumerable<Registration>> GetEventRegistrationsAsync(int eventId)
        {
            try
            {
                return await _unitOfWork.Registrations.GetByEventAsync(eventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations for event {Id}", eventId);
                throw new CommunityEventException(
                    $"Failed to retrieve registrations for event {eventId}.", "RETRIEVAL_ERROR", ex);
            }
        }

        public async Task<Registration> RegisterParticipantAsync(
            int participantId, int eventId, string? notes = null)
        {
            _logger.LogInformation(
                "Registering participant {ParticipantId} for event {EventId}",
                participantId, eventId);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var participant = await _unitOfWork.Participants.GetByIdAsync(participantId)
                    ?? throw new ParticipantNotFoundException(participantId);

                var eventEntity = await _unitOfWork.Events.GetWithDetailsAsync(eventId)
                    ?? throw new EventNotFoundException(eventId);

                if (eventEntity.IsPast())
                    throw new RegistrationException(
                        $"Cannot register for past event '{eventEntity.Name}'.");

                var isDuplicate = await _unitOfWork.Registrations.ExistsAsync(participantId, eventId);
                if (isDuplicate)
                    throw new DuplicateRegistrationException(participantId, eventId);

                var primaryVenue = eventEntity.PrimaryVenue
                    ?? eventEntity.Venues.FirstOrDefault();

                var registration = new Registration(eventId, participantId, notes);

                if (primaryVenue != null && !primaryVenue.HasAvailableSpace())
                {
                    registration.Waitlist();
                    await AddToWaitingListAsync(eventEntity, registration);
                    _logger.LogInformation(
                        "Participant {ParticipantId} added to waiting list for event {EventId}",
                        participantId, eventId);
                }
                else
                {
                    registration.Confirm();
                    _logger.LogInformation(
                        "Registration confirmed for participant {ParticipantId} in event {EventId}",
                        participantId, eventId);
                }

                await _unitOfWork.Registrations.AddAsync(registration);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                // ✅ OBSERVER PATTERN — notify email + audit log observers
                try
                {
                    await _notifier.NotifyRegisteredAsync(
                        registration.Id,
                        participant.Id,
                        participant.GetFullName(),
                        participant.Email,
                        eventEntity.Id,
                        eventEntity.Name,
                        eventEntity.StartDate);
                }
                catch (Exception nex)
                {
                    _logger.LogWarning(nex, "Observer notification failed (non-fatal)");
                }

                if (_notificationService != null)
                {
                    var title = registration.IsConfirmed() ? "Registration Confirmed" : "Added to Waitlist";
                    var msg = registration.IsConfirmed() 
                        ? $"Your reservation for '{eventEntity.Name}' has been successfully confirmed."
                        : $"'{eventEntity.Name}' is currently at capacity. You have been placed on the waitlist queue.";
                    var type = registration.IsConfirmed() ? "RegistrationConfirmed" : "WaitlistPromoted";
                    
                    await _notificationService.CreateUserNotificationAsync(participantId, title, msg, eventId, type);
                }
                return registration;

                
            }
            catch (CommunityEventException)
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Unexpected error registering participant {ParticipantId} for event {EventId}",
                    participantId, eventId);
                throw new CommunityEventException(
                    "An unexpected error occurred during registration.",
                    "REGISTRATION_ERROR", ex);
            }
            finally
            {
                _logger.LogDebug(
                    "RegisterParticipantAsync completed for participant {ParticipantId}",
                    participantId);
            }
        }

        public async Task CancelRegistrationAsync(int registrationId, string? reason = null)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Cancelling registration {Id}", registrationId);

                var registration = await _unitOfWork.Registrations
                    .GetWithDetailsAsync(registrationId)
                    ?? throw new RegistrationException(
                        $"Registration {registrationId} was not found.");

                registration.Cancel(reason);

                // Capture details BEFORE clearing the change tracker
                var regId = registration.Id;
                var regParticipantId = registration.ParticipantId;
                var regParticipantName = registration.Participant?.GetFullName() ?? "Unknown";
                var regParticipantEmail = registration.Participant?.Email ?? "";
                var regEventId = registration.EventId;
                var regEventName = registration.Event?.Name ?? "Unknown";
                var regEventStartDate = registration.Event?.StartDate ?? DateTime.MinValue;

                await _unitOfWork.Registrations.UpdateAsync(registration);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                // ✅ OBSERVER PATTERN — notify email + audit log observers
                try
                {
                    await _notifier.NotifyCancelledAsync(
                        regId, regParticipantId, regParticipantName, regParticipantEmail,
                        regEventId, regEventName, regEventStartDate);
                }
                catch (Exception nex)
                {
                    _logger.LogWarning(nex, "Observer notification failed on cancellation (non-fatal)");
                }

                if (_notificationService != null)
                {
                    await _notificationService.CreateUserNotificationAsync(
                        regParticipantId,
                        "Registration Cancelled",
                        $"Your registration for '{regEventName}' has been cancelled.",
                        regEventId,
                        "RegistrationCancelled");
                }

                _logger.LogInformation("Registration {Id} cancelled successfully", registrationId);
            }
            catch (CommunityEventException)
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Error cancelling registration {Id}", registrationId);
                throw new CommunityEventException(
                    $"Failed to cancel registration {registrationId}.",
                    "CANCEL_ERROR", ex);
            }
            finally
            {
                _logger.LogDebug("CancelRegistrationAsync completed for {Id}", registrationId);
            }
        }

        public async Task ConfirmRegistrationAsync(int registrationId)
        {
            try
            {
                var registration = await _unitOfWork.Registrations
                    .GetByIdAsync(registrationId)
                    ?? throw new RegistrationException(
                        $"Registration {registrationId} was not found.");
                registration.Confirm();
                await _unitOfWork.Registrations.UpdateAsync(registration);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Registration {Id} confirmed successfully", registrationId);
            }
            catch (CommunityEventException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming registration {Id}", registrationId);
                throw new CommunityEventException(
                    $"Failed to confirm registration {registrationId}.",
                    "CONFIRM_ERROR", ex);
            }
        }

        public async Task<bool> IsParticipantRegisteredAsync(int participantId, int eventId)
        {
            try
            {
                return await _unitOfWork.Registrations.ExistsAsync(participantId, eventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error checking registration for participant {ParticipantId} event {EventId}",
                    participantId, eventId);
                return false;
            }
        }

        // ── Private helpers ───────────────────────────────────────────

        private Task AddToWaitingListAsync(Event eventEntity, Registration registration)
        {
            if (!_waitingLists.ContainsKey(eventEntity.Id))
            {
                _waitingLists[eventEntity.Id] = new RegistrationQueue(
                    eventEntity.Id, eventEntity.Name);
            }

            _waitingLists[eventEntity.Id].AddToWaitingList(registration);
            return Task.CompletedTask;
        }
    }
}