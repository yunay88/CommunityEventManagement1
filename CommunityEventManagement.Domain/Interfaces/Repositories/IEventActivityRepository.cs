using CommunityEventManagement.Domain.Entities;

namespace CommunityEventManagement.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Repository for the EventActivity junction entity.
    /// </summary>
    public interface IEventActivityRepository
    {
        Task<EventActivity?> GetByIdAsync(int eventId, int activityId);
        Task<IEnumerable<EventActivity>> GetAllAsync();
        Task AddAsync(EventActivity entity);
        Task<bool> ExistsAsync(int eventId, int activityId);
        Task RemoveLinkAsync(int eventId, int activityId);
    }
}