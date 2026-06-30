using System.Text.Json;
using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Interfaces;
using CommunityEventManagement.Domain.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection; // <-- REQUIRED FOR ISERVICESCOPEFACTORY
using Microsoft.Extensions.Logging;

namespace CommunityEventManagement.Infrastructure.Services
{
    /// <summary>
    /// Implements real-time Notification messaging backed by an asynchronous JSON document store.
    /// Demonstrates Polyglot Persistence and safe Scoped Service consumption from a Singleton via IServiceScopeFactory.
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly string _filePath = "NotificationsData.json";
        private readonly List<Notification> _notifications = new();
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly ILogger<NotificationService> _logger;
        private readonly IServiceScopeFactory _scopeFactory; // <-- INJECTED SCOPE FACTORY

        public event Action? OnNotificationUpdated;

        public NotificationService(ILogger<NotificationService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            LoadNotificationsFromFile();
        }

        private void LoadNotificationsFromFile()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var data = JsonSerializer.Deserialize<List<Notification>>(json);
                    if (data != null) _notifications.AddRange(data);
                }
                else
                {
                    _notifications.Add(new Notification
                    {
                        Title = "Welcome to Community Events",
                        Message = "Explore upcoming events, secure your place, and receive real-time schedule updates.",
                        Type = "EventCreated",
                        CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                    });
                    SaveNotificationsToFileAsync().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notification document database.");
            }
        }

        private async Task SaveNotificationsToFileAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(_notifications, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_filePath, json);
                OnNotificationUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing notification document database.");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Filters notifications so participants see personal alerts + broadcasts created AFTER their registration date.
        /// </summary>
        public async Task<IEnumerable<Notification>> GetNotificationsForParticipantAsync(int participantId)
        {
            DateTime regDate = DateTime.MinValue;
            using (var scope = _scopeFactory.CreateScope())
            {
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var participant = await uow.Participants.GetByIdAsync(participantId);
                if (participant != null) regDate = participant.CreatedAt;
            }

            return _notifications
                .Where(n => n.ParticipantId == participantId || (n.ParticipantId == null && n.CreatedAt >= regDate))
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
        }

        public async Task<int> GetUnreadCountAsync(int participantId)
        {
            DateTime regDate = DateTime.MinValue;
            using (var scope = _scopeFactory.CreateScope())
            {
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var participant = await uow.Participants.GetByIdAsync(participantId);
                if (participant != null) regDate = participant.CreatedAt;
            }

            return _notifications
                .Count(n => (n.ParticipantId == participantId || (n.ParticipantId == null && n.CreatedAt >= regDate)) && !n.IsRead);
        }

        public async Task MarkAsReadAsync(string notificationId)
        {
            var notif = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notif != null && !notif.IsRead)
            {
                notif.IsRead = true;
                await SaveNotificationsToFileAsync();
            }
        }

        public async Task MarkAllAsReadAsync(int participantId)
        {
            DateTime regDate = DateTime.MinValue;
            using (var scope = _scopeFactory.CreateScope())
            {
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var participant = await uow.Participants.GetByIdAsync(participantId);
                if (participant != null) regDate = participant.CreatedAt;
            }

            bool modified = false;
            foreach (var notif in _notifications.Where(n => (n.ParticipantId == participantId || (n.ParticipantId == null && n.CreatedAt >= regDate)) && !n.IsRead))
            {
                notif.IsRead = true;
                modified = true;
            }
            if (modified) await SaveNotificationsToFileAsync();
        }

        public async Task CreateBroadcastNotificationAsync(string title, string message, int? eventId, string type)
        {
            _notifications.Add(new Notification { Title = title, Message = message, EventId = eventId, Type = type, ParticipantId = null });
            await SaveNotificationsToFileAsync();
        }

        public async Task CreateUserNotificationAsync(int participantId, string title, string message, int? eventId, string type)
        {
            _notifications.Add(new Notification { ParticipantId = participantId, Title = title, Message = message, EventId = eventId, Type = type });
            await SaveNotificationsToFileAsync();
        }

        public async Task CheckApproachingEventsAsync(IEnumerable<Event> upcomingEvents)
        {
            var approaching = upcomingEvents.Where(e => e.StartDate > DateTime.UtcNow && e.StartDate <= DateTime.UtcNow.AddHours(48)).ToList();
            bool added = false;
            foreach (var evt in approaching)
            {
                if (!_notifications.Any(n => n.EventId == evt.Id && n.Type == "EventApproaching"))
                {
                    _notifications.Add(new Notification
                    {
                        Title = "Event Approaching",
                        Message = $"Reminder: '{evt.Name}' is starting on {evt.StartDate:dd MMM yyyy, HH:mm}. Don't miss it!",
                        EventId = evt.Id,
                        Type = "EventApproaching"
                    });
                    added = true;
                }
            }
            if (added) await SaveNotificationsToFileAsync();
        }
    }
}