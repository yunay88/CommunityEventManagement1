using CommunityEventManagement.Domain.Entities;

namespace CommunityEventManagement.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Repository for the EventVenue junction entity.
    /// </summary>
    public interface IEventVenueRepository
    {
        Task<EventVenue?> GetByIdAsync(int eventId, int venueId);
        Task<IEnumerable<EventVenue>> GetAllAsync();
        Task AddAsync(EventVenue entity);
        Task AddRangeAsync(IEnumerable<EventVenue> entities);

        Task<IEnumerable<EventVenue>> GetByEventAsync(int eventId);
        Task<IEnumerable<EventVenue>> GetByVenueAsync(int venueId);
        Task<EventVenue?> GetPrimaryForEventAsync(int eventId);
        Task<bool> ExistsAsync(int eventId, int venueId);
        Task RemoveLinkAsync(int eventId, int venueId);
    }
}