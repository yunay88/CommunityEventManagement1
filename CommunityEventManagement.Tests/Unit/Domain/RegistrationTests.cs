using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Enums;
using CommunityEventManagement.Domain.Exceptions;
using FluentAssertions;

namespace CommunityEventManagement.Tests.Unit.Domain;

/// <summary>
/// Tests the Registration state machine:
/// Pending → Confirmed → Cancelled
/// Pending → Waitlisted → Confirmed
/// </summary>
public class RegistrationTests
{
    [Fact]
    public void NewRegistration_StatusIsPending()
    {
        var reg = new Registration(eventId: 1, participantId: 1);
        reg.Status.Should().Be(RegistrationStatus.Pending);
        reg.RegistrationDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Confirm_FromPending_SetsToConfirmed()
    {
        var reg = new Registration(1, 1);
        reg.Confirm();
        reg.Status.Should().Be(RegistrationStatus.Confirmed);
        reg.StatusChangedAt.Should().NotBeNull();
        reg.StatusChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Confirm_FromConfirmed_NoOp()
    {
        var reg = new Registration(1, 1);
        reg.Confirm();  // Pending → Confirmed
        var beforeChangedAt = reg.StatusChangedAt;

        reg.Confirm();  // Confirmed → Confirmed (no change)

        reg.Status.Should().Be(RegistrationStatus.Confirmed);
        reg.StatusChangedAt.Should().Be(beforeChangedAt);
    }

    [Fact]
    public void Confirm_FromCancelled_ThrowsRegistrationException()
    {
        var reg = new Registration(1, 1);
        reg.Cancel();

        var act = () => reg.Confirm();

        act.Should().Throw<RegistrationException>()
            .Where(e => e.Message.Contains("cancelled"));
    }

    [Fact]
    public void Cancel_FromPending_SetsToCancelled()
    {
        var reg = new Registration(1, 1);
        reg.Cancel("changed mind");
        reg.Status.Should().Be(RegistrationStatus.Cancelled);
        reg.Notes.Should().Be("changed mind");
    }

    [Fact]
    public void Cancel_FromConfirmed_AlsoSucceeds()
    {
        var reg = new Registration(1, 1);
        reg.Confirm();
        reg.Cancel();
        reg.Status.Should().Be(RegistrationStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromCancelled_Throws()
    {
        var reg = new Registration(1, 1);
        reg.Cancel();

        var act = () => reg.Cancel();

        act.Should().Throw<RegistrationException>()
            .Where(e => e.Message.Contains("already cancelled"));
    }

    [Fact]
    public void Waitlist_FromPending_SetsToWaitlisted()
    {
        var reg = new Registration(1, 1);
        reg.Waitlist();
        reg.Status.Should().Be(RegistrationStatus.Waitlisted);
    }

    [Fact]
    public void Constructor_InvalidEventId_Throws()
    {
        var act = () => new Registration(eventId: 0, participantId: 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_InvalidParticipantId_Throws()
    {
        var act = () => new Registration(eventId: 1, participantId: 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsConfirmed_IsPending_IsCancelled_PredicatesWork()
    {
        var reg = new Registration(1, 1);
        reg.IsPending().Should().BeTrue();
        reg.IsConfirmed().Should().BeFalse();
        reg.IsCancelled().Should().BeFalse();

        reg.Confirm();
        reg.IsConfirmed().Should().BeTrue();
        reg.IsPending().Should().BeFalse();
    }
}