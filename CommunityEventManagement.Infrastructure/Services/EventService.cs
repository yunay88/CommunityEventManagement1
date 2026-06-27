using CommunityEventManagement.Domain.Algorithms;
using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Exceptions;
using CommunityEventManagement.Domain.Interfaces;
using CommunityEventManagement.Domain.Interfaces.Services;
using CommunityEventManagement.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CommunityEventManagement.Infrastructure.Services
{
    /// <summary>
    /// Event service — all business logic for events.
    ///
    /// Demonstrates:
    ///   - Interface implementation (IEventService)
    ///   - Dependency injection via constructor
    ///   - try-catch-finally exception handling
    ///   - Custom exceptions thrown for business rule violations
    ///   - Algorithm usage: BinarySearch and InsertionSort
    ///   - Logging throughout
    /// </summary>
    public class EventService : IEventService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EventService> _logger;

        // Constructor injection — demonstrates Dependency Injection pattern
        public EventService(IUnitOfWork unitOfWork, ILogger<EventService> logger)
        {
            _unitOfWork = unitOfWork
                ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<Event>> GetAllEventsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving all events");
                return await _unitOfWork.Events.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all events");
                throw new CommunityEventException(
                    "Failed to retrieve events.", "RETRIEVAL_ERROR", ex);
            }
            finally
            {
                // finally block — always executes (demonstrates try-catch-finally)
                _logger.LogDebug("GetAllEventsAsync completed");
            }
        }

        public async Task<IEnumerable<Event>> GetUpcomingEventsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving upcoming events");
                return await _unitOfWork.Events.GetUpcomingEventsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving upcoming events");
                throw new CommunityEventException(
                    "Failed to retrieve upcoming events.", "RETRIEVAL_ERROR", ex);
            }
            finally
            {
                _logger.LogDebug("GetUpcomingEventsAsync completed");
            }
        }

        public async Task<IEnumerable<Event>> FilterEventsAsync(FilterCriteria criteria)
        {
            if (criteria == null)
                throw new ArgumentNullException(nameof(criteria));

            try
            {
                _logger.LogInformation(
                    "Filtering events with criteria: {Summary}",
                    criteria.GetFilterSummary());

                return await _unitOfWork.Events.FilterEventsAsync(criteria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering events");
                throw new CommunityEventException(
                    "Failed to filter events.", "FILTER_ERROR", ex);
            }
            finally
            {
                _logger.LogDebug("FilterEventsAsync completed");
            }
        }

        public async Task<Event?> GetEventByIdAsync(int id)
        {
            // Validation — guard clause
            if (id <= 0)
                throw new ArgumentException(
                    "Event ID must be a positive number.", nameof(id));

            try
            {
                return await _unitOfWork.Events.GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving event {EventId}", id);
                throw new CommunityEventException(
                    $"Failed to retrieve event {id}.", "RETRIEVAL_ERROR", ex);
            }
        }

        public async Task<Event?> GetEventWithDetailsAsync(int id)
        {
            if (id <= 0)
                throw new ArgumentException(
                    "Event ID must be a positive number.", nameof(id));

            try
            {
                return await _unitOfWork.Events.GetWithDetailsAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving event details {EventId}", id);
                throw new CommunityEventException(
                    $"Failed to retrieve event details for {id}.", "RETRIEVAL_ERROR", ex);
            }
        }

        public async Task<Event> CreateEventAsync(
            string name,
            string description,
            DateTime startDate,
            DateTime endDate,
            int? venueId)
        {
            try
            {
                _logger.LogInformation("Creating event: {EventName}", name);

                // Business rule: validate venue exists if provided
                if (venueId.HasValue)
                {
                    var venueExists = await _unitOfWork.Venues.ExistsAsync(venueId.Value);
                    if (!venueExists)
                        throw new VenueNotFoundException(venueId.Value);
                }

                // Event constructor validates dates — throws EventValidationException
                var eventEntity = new Event(name, description, startDate, endDate, venueId);

                await _unitOfWork.Events.AddAsync(eventEntity);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Event created successfully. ID: {EventId}", eventEntity.Id);

                return eventEntity;
            }
            catch (CommunityEventException)
            {
                // Re-throw domain exceptions without wrapping
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating event: {EventName}", name);
                throw new CommunityEventException(
                    $"Failed to create event '{name}'.", "CREATE_ERROR", ex);
            }
            finally
            {
                _logger.LogDebug("CreateEventAsync completed for: {EventName}", name);
            }
        }

                public async Task<Event> CreateEventWithVenuesAsync(
            string name,
            string description,
            DateTime startDate,
            DateTime endDate,
            List<int> venueIds,
            int primaryVenueId,
            List<int> activityIds)
        {
            try
            {
                _logger.LogInformation(
                    "Creating event with {VenueCount} venues, primary={PrimaryVenueId}",
                    venueIds?.Count ?? 0, primaryVenueId);

                // Validate venues exist
                if (venueIds == null || venueIds.Count == 0)
                    throw new EventValidationException("Venues", "At least one venue must be selected.");
                if (!venueIds.Contains(primaryVenueId))
                    throw new EventValidationException("Venues", "Primary venue must be in the selected list.");

                foreach (var venueId in venueIds)
                {
                    var venueExists = await _unitOfWork.Venues.ExistsAsync(venueId);
                    if (!venueExists)
                        throw new VenueNotFoundException(venueId);
                }

                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var eventEntity = new Event(name, description, startDate, endDate);
                    await _unitOfWork.Events.AddAsync(eventEntity);
                    await _unitOfWork.SaveChangesAsync();

                    // Add EventVenue rows for each selected venue
                    foreach (var venueId in venueIds)
                    {
                        var isPrimary = venueId == primaryVenueId;
                        await _unitOfWork.EventVenues.AddAsync(new Domain.Entities.EventVenue
                        {
                            EventId = eventEntity.Id,
                            VenueId = venueId,
                            IsPrimary = isPrimary,
                            DisplayOrder = venueIds.IndexOf(venueId)
                        });
                    }

                    // Add EventActivity rows (Fixes CS8602 Null Dereference Warning)
                    var safeActivityIds = activityIds ?? new List<int>();
                    foreach (var activityId in safeActivityIds)
                    {
                        if (await _unitOfWork.Activities.ExistsAsync(activityId))
                        {
                            await _unitOfWork.EventActivities.AddAsync(new Domain.Entities.EventActivity
                            {
                                EventId = eventEntity.Id,
                                ActivityId = activityId,
                                OrderInEvent = safeActivityIds.IndexOf(activityId) + 1,
                                AddedAt = DateTime.UtcNow
                            });
                        }
                    }

                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitTransactionAsync();

                    _logger.LogInformation(
                        "Event created successfully with M:N venues + activities. ID: {EventId}",
                        eventEntity.Id);

                    return eventEntity;
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (CommunityEventException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating event with multiple venues");
                throw new CommunityEventException(
                    "Failed to create event with venues.", "CREATE_ERROR", ex);
            }
            finally { _logger.LogDebug("CreateEventWithVenuesAsync completed"); }
        }

        public async Task UpdateEventWithVenuesAsync(
            int id,
            string name,
            string description,
            DateTime startDate,
            DateTime endDate,
            List<int> venueIds,
            int primaryVenueId,
            List<int> activityIds)
        {
            try
            {
                _logger.LogInformation("Updating event {EventId} with M:N venues + activities", id);

                if (venueIds == null || venueIds.Count == 0)
                    throw new EventValidationException("Venues", "At least one venue must be selected.");

                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var eventEntity = await _unitOfWork.Events.GetByIdAsync(id)
                        ?? throw new EventNotFoundException(id);

                    eventEntity.UpdateDetails(name, description, startDate, endDate, null);
                    await _unitOfWork.Events.UpdateAsync(eventEntity);

                    // Replace EventVenue links (Fixes CS0103 _context error using existing GetByEventAsync)
                    var existingLinks = await _unitOfWork.EventVenues.GetByEventAsync(id);
                    foreach (var link in existingLinks)
                    {
                        await _unitOfWork.EventVenues.RemoveLinkAsync(link.EventId, link.VenueId);
                    }

                    for (int i = 0; i < venueIds.Count; i++)
                    {
                        await _unitOfWork.EventVenues.AddAsync(new Domain.Entities.EventVenue
                        {
                            EventId = id,
                            VenueId = venueIds[i],
                            IsPrimary = venueIds[i] == primaryVenueId,
                            DisplayOrder = i
                        });
                    }

                    // Replace EventActivity links (Fixes CS0103 _context error using existing GetAllAsync)
                    var allActivities = await _unitOfWork.EventActivities.GetAllAsync();
                    var existingActivities = allActivities.Where(ea => ea.EventId == id).ToList();
                    foreach (var act in existingActivities)
                    {
                        await _unitOfWork.EventActivities.RemoveLinkAsync(act.EventId, act.ActivityId);
                    }

                    // Add new EventActivity links (Fixes CS8602 Null Dereference Warning)
                    var safeActivityIdsUpdate = activityIds ?? new List<int>();
                    foreach (var activityId in safeActivityIdsUpdate)
                    {
                        if (await _unitOfWork.Activities.ExistsAsync(activityId))
                        {
                            await _unitOfWork.EventActivities.AddAsync(new Domain.Entities.EventActivity
                            {
                                EventId = id,
                                ActivityId = activityId,
                                OrderInEvent = (safeActivityIdsUpdate.IndexOf(activityId) + 1),
                                AddedAt = DateTime.UtcNow
                            });
                        }
                    }

                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitTransactionAsync();

                    _logger.LogInformation("Event {EventId} updated successfully", id);
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            catch (CommunityEventException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event {EventId}", id);
                throw new CommunityEventException(
                    $"Failed to update event {id}.", "UPDATE_ERROR", ex);
            }
            finally { _logger.LogDebug("UpdateEventWithVenuesAsync completed for {EventId}", id); }
        }




        public async Task UpdateEventAsync(
            int id,
            string name,
            string description,
            DateTime startDate,
            DateTime endDate,
            int? venueId)
        {
            try
            {
                _logger.LogInformation("Updating event {EventId}", id);

                var eventEntity = await _unitOfWork.Events.GetByIdAsync(id)
                    ?? throw new EventNotFoundException(id);

                // Business rule: validate venue exists if provided
                if (venueId.HasValue)
                {
                    var venueExists = await _unitOfWork.Venues.ExistsAsync(venueId.Value);
                    if (!venueExists)
                        throw new VenueNotFoundException(venueId.Value);
                }

                eventEntity.UpdateDetails(name, description, startDate, endDate, venueId);

                await _unitOfWork.Events.UpdateAsync(eventEntity);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Event {EventId} updated successfully", id);
            }
            catch (CommunityEventException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event {EventId}", id);
                throw new CommunityEventException(
                    $"Failed to update event {id}.", "UPDATE_ERROR", ex);
            }
            finally
            {
                _logger.LogDebug("UpdateEventAsync completed for event {EventId}", id);
            }
        }

        public async Task DeleteEventAsync(int id)
        {
            try
            {
                _logger.LogInformation("Deleting event {EventId}", id);

                var exists = await _unitOfWork.Events.ExistsAsync(id);
                if (!exists)
                    throw new EventNotFoundException(id);

                await _unitOfWork.Events.DeleteAsync(id);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Event {EventId} deleted successfully", id);
            }
            catch (CommunityEventException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting event {EventId}", id);
                throw new CommunityEventException(
                    $"Failed to delete event {id}.", "DELETE_ERROR", ex);
            }
            finally
            {
                _logger.LogDebug("DeleteEventAsync completed for event {EventId}", id);
            }
        }

                public async Task AddActivityToEventAsync(int eventId, int activityId, int order = 0)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var eventExists = await _unitOfWork.Events.ExistsAsync(eventId);
                if (!eventExists) throw new EventNotFoundException(eventId);

                var activityExists = await _unitOfWork.Activities.ExistsAsync(activityId);
                if (!activityExists) throw new ActivityNotFoundException(activityId);

                if (await _unitOfWork.EventActivities.ExistsAsync(eventId, activityId))
                    throw new CommunityEventException(
                        $"Activity {activityId} is already part of event {eventId}.",
                        "DUPLICATE_ACTIVITY");

                var eventActivity = new EventActivity
                {
                    EventId = eventId,
                    ActivityId = activityId,
                    OrderInEvent = order,
                    AddedAt = DateTime.UtcNow
                };

               
                await _unitOfWork.EventActivities.AddAsync(eventActivity);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation(
                    "Activity {ActivityId} added to event {EventId}", activityId, eventId);
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
                    "Error adding activity {ActivityId} to event {EventId}",
                    activityId, eventId);
                throw new CommunityEventException(
                    "Failed to add activity to event.", "UPDATE_ERROR", ex);
            }
        }

        public async Task RemoveActivityFromEventAsync(int eventId, int activityId)
        {
            try
            {
                _logger.LogInformation(
                    "Removing activity {ActivityId} from event {EventId}",
                    activityId, eventId);

                // UG #4 FIX: actually remove the junction row
                await _unitOfWork.EventActivities.RemoveLinkAsync(eventId, activityId);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (CommunityEventException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing activity from event");
                throw new CommunityEventException(
                    "Failed to remove activity from event.", "UPDATE_ERROR", ex);
            }
        }


        public async Task<IEnumerable<Event>> GetEventsByVenueAsync(int venueId)
        {
            try
            {
                return await _unitOfWork.Events.GetByVenueAsync(venueId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving events for venue {VenueId}", venueId);
                throw new CommunityEventException(
                    $"Failed to retrieve events for venue {venueId}.", "RETRIEVAL_ERROR", ex);
            }
        }

        public async Task<IEnumerable<Event>> GetEventsByParticipantAsync(int participantId)
        {
            try
            {
                return await _unitOfWork.Events.GetByParticipantAsync(participantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving events for participant {ParticipantId}", participantId);
                throw new CommunityEventException(
                    $"Failed to retrieve events for participant {participantId}.",
                    "RETRIEVAL_ERROR", ex);
            }
        }

        /// <summary>
        /// Searches for an event by ID using Binary Search algorithm.
        /// List must be sorted by ID before calling this method.
        /// Demonstrates: Algorithm usage in service layer.
        /// </summary>
        public async Task<Event?> SearchEventByIdAsync(int eventId)
        {
            try
            {
                var allEvents = (await _unitOfWork.Events.GetAllAsync()).ToList();

                // Sort by ID first (requirement for Binary Search)
                SortAlgorithms.InsertionSort(allEvents, e => e.Id);

                // Now Binary Search on sorted list
                int index = SearchAlgorithms.BinarySearch(allEvents, eventId, e => e.Id);

                return index >= 0 ? allEvents[index] : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for event {EventId}", eventId);
                throw new CommunityEventException(
                    $"Failed to search for event {eventId}.", "SEARCH_ERROR", ex);
            }
        }

        /// <summary>
        /// Returns events sorted by start date using Insertion Sort.
        /// Demonstrates: Sorting algorithm usage in service layer.
        /// </summary>
        public async Task<IEnumerable<Event>> GetEventsSortedByDateAsync(bool descending = false)
        {
            try
            {
                var events = (await _unitOfWork.Events.GetAllAsync()).ToList();
                SortAlgorithms.InsertionSort(events, e => e.StartDate, descending);
                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sorted events");
                throw new CommunityEventException(
                    "Failed to retrieve sorted events.", "RETRIEVAL_ERROR", ex);
            }
        }
    }
}