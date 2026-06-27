using CommunityEventManagement.Domain.Exceptions;
using FluentAssertions;

namespace CommunityEventManagement.Tests.Unit.Exceptions;

/// <summary>
/// Tests that custom exceptions carry the expected metadata
/// (ErrorCode, OccurredAt, contextual IDs).
/// </summary>
public class CustomExceptionsTests
{
    [Fact]
    public void CommunityEventException_StoresErrorCodeAndTimestamp()
    {
        var ex = new CommunityEventException("Test error", "TEST_CODE");

        ex.ErrorCode.Should().Be("TEST_CODE");
        ex.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        ex.Message.Should().Be("Test error");
    }

    [Fact]
    public void EventNotFoundException_StoresEventId()
    {
        var ex = new EventNotFoundException(42);

        ex.EventId.Should().Be(42);
        ex.ErrorCode.Should().Be("EVENT_NOT_FOUND");
        ex.Message.Should().Contain("42");
    }

    [Fact]
    public void DuplicateRegistrationException_StoresBothIds()
    {
        var ex = new DuplicateRegistrationException(participantId: 5, eventId: 10);

        ex.ParticipantId.Should().Be(5);
        ex.EventId.Should().Be(10);
        ex.ErrorCode.Should().Be("DUPLICATE_REGISTRATION");
    }

    [Fact]
    public void VenueCapacityException_StoresVenueIdAndMaxCapacity()
    {
        var ex = new VenueCapacityException(venueId: 3, maxCapacity: 50);

        ex.VenueId.Should().Be(3);
        ex.MaxCapacity.Should().Be(50);
        ex.ErrorCode.Should().Be("VENUE_CAPACITY_EXCEEDED");
    }
}