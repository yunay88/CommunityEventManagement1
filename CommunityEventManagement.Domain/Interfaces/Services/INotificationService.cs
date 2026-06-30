using CommunityEventManagement.Domain.Entities;

namespace CommunityEventManagement.Domain.Interfaces.Services
{
    public interface INotificationService
    {
        Task<IEnumerable<Notification>> GetNotificationsForParticipantAsync(int participantId);
        Task<int> GetUnreadCountAsync(int participantId);
        Task MarkAsReadAsync(string notificationId);
        Task MarkAllAsReadAsync(int participantId);

        // <-- ENTERPRISE ADMIN ROUTING METHODS
        Task<IEnumerable<Notification>> GetAdminNotificationsAsync();
        Task<int> GetAdminUnreadCountAsync();
        Task MarkAdminAsReadAsync(string notificationId);
        Task MarkAllAdminAsReadAsync();
        Task CreateAdminNotificationAsync(string title, string message, int? eventId, string type);

        Task CreateBroadcastNotificationAsync(string title, string message, int? eventId, string type);
        Task CreateUserNotificationAsync(int participantId, string title, string message, int? eventId, string type);
        Task CheckApproachingEventsAsync(IEnumerable<Event> upcomingEvents);
        event Action? OnNotificationUpdated;
    }
}