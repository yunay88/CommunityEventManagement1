using CommunityEventManagement.Infrastructure.Patterns.Observer;
using CommunityEventManagement.Infrastructure.Patterns.Observer.Observers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommunityEventManagement.Tests.Unit.Patterns;

public class ObserverTests
{
    [Fact]
    public async Task Notifier_WithNoObservers_DoesNotThrow()
    {
        var notifier = new RegistrationNotifier(NullLogger<RegistrationNotifier>.Instance);
        await notifier.NotifyRegisteredAsync(1, 1, "Alice", "alice@t.com",
            10, "Event", DateTime.UtcNow);
        // Just verify it doesn't throw
    }

    [Fact]
    public async Task Notifier_MultipleObservers_AllReceiveNotification()
    {
        var notifier = new RegistrationNotifier(NullLogger<RegistrationNotifier>.Instance);
        var observer1 = new EmailNotificationObserver(NullLogger<EmailNotificationObserver>.Instance);
        var observer2 = new AuditLogObserver(NullLogger<AuditLogObserver>.Instance);

        notifier.Subscribe(observer1);
        notifier.Subscribe(observer2);

        await notifier.NotifyRegisteredAsync(1, 1, "Alice", "alice@t.com",
            10, "Event", DateTime.UtcNow);

        observer1.NotificationCount.Should().Be(1);
        observer1.GetLastNotification().Should().Contain("alice@t.com");
        observer2.TotalEntries.Should().Be(1);
    }

    [Fact]
    public async Task Notifier_Unsubscribe_ObserverNoLongerReceives()
    {
        var notifier = new RegistrationNotifier(NullLogger<RegistrationNotifier>.Instance);
        var observer = new EmailNotificationObserver(NullLogger<EmailNotificationObserver>.Instance);

        notifier.Subscribe(observer);
        notifier.Unsubscribe(observer);

        await notifier.NotifyRegisteredAsync(1, 1, "Alice", "a@t.com",
            10, "Event", DateTime.UtcNow);

        observer.NotificationCount.Should().Be(0);
    }

    [Fact]
    public async Task Notifier_DuplicateSubscribe_OnlyNotifiesOnce()
    {
        var notifier = new RegistrationNotifier(NullLogger<RegistrationNotifier>.Instance);
        var observer = new EmailNotificationObserver(NullLogger<EmailNotificationObserver>.Instance);

        notifier.Subscribe(observer);
        notifier.Subscribe(observer);  // duplicate

        await notifier.NotifyRegisteredAsync(1, 1, "A", "a@t.com",
            10, "E", DateTime.UtcNow);

        observer.NotificationCount.Should().Be(1);
    }

    [Fact]
    public void ObserverCount_TracksSubscriptions()
    {
        var notifier = new RegistrationNotifier(NullLogger<RegistrationNotifier>.Instance);
        notifier.ObserverCount.Should().Be(0);

        notifier.Subscribe(new EmailNotificationObserver(NullLogger<EmailNotificationObserver>.Instance));
        notifier.ObserverCount.Should().Be(1);

        notifier.Subscribe(new AuditLogObserver(NullLogger<AuditLogObserver>.Instance));
        notifier.ObserverCount.Should().Be(2);
    }
}