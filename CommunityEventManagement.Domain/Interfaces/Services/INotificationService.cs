using CommunityEventManagement.Domain.Entities;

namespace CommunityEventManagement.Domain.Interfaces.Services
{
    public interface INotificationService
    {
        Task<IEnumerable<Notification>> GetNotificationsForParticipantAsync(int participantId);
        Task<int> GetUnreadCountAsync(int participantId);
        Task MarkAsReadAsync(string notificationId);
        Task MarkAllAsReadAsync(int participantId);
        Task CreateBroadcastNotificationAsync(string title, string message, int? eventId, string type);
        Task CreateUserNotificationAsync(int participantId, string title, string message, int? eventId, string type);
        Task CheckApproachingEventsAsync(IEnumerable<Event> upcomingEvents);
        event Action? OnNotificationUpdated;
    }
}