using CommunityEventManagement.Domain.Exceptions;
using CommunityEventManagement.Infrastructure.Patterns.Factory;
using FluentAssertions;

namespace CommunityEventManagement.Tests.Unit.Services;

public class EventFactoryTests
{
    private readonly EventFactory _sut = new();

    [Fact]
    public void Create_ValidRequest_ReturnsEvent()
    {
        var request = new CreateEventRequest
        {
            Name = "Valid Event",
            Description = "Good",
            StartDate = DateTime.UtcNow.AddDays(7),
            EndDate = DateTime.UtcNow.AddDays(7).AddHours(3),
            VenueId = null
        };

        var evt = _sut.Create(request);

        evt.Should().NotBeNull();
        evt.Name.Should().Be("Valid Event");
        evt.StartDate.Should().Be(request.StartDate);
    }

    [Fact]
    public void Create_NullRequest_ThrowsArgumentNullException()
    {
        var act = () => _sut.Create(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_MissingName_ThrowsEventValidationException()
    {
        var request = new CreateEventRequest
        {
            Name = "",  // empty
            Description = "D",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        var act = () => _sut.Create(request);
        act.Should().Throw<EventValidationException>();
    }

    [Fact]
    public void Create_EndBeforeStart_ThrowsEventValidationException()
    {
        var start = DateTime.UtcNow.AddDays(2);
        var request = new CreateEventRequest
        {
            Name = "Bad Event",
            Description = "D",
            StartDate = start,
            EndDate = start.AddHours(-1) // BEFORE start
        };

        var act = () => _sut.Create(request);
        act.Should().Throw<EventValidationException>();
    }

    [Fact]
    public void Create_StartDateInPast_ThrowsEventValidationException()
    {
        var request = new CreateEventRequest
        {
            Name = "Past Event",
            Description = "D",
            StartDate = DateTime.UtcNow.AddDays(-5),  // past
            EndDate = DateTime.UtcNow.AddDays(-5).AddHours(2)
        };

        var act = () => _sut.Create(request);
        act.Should().Throw<EventValidationException>();
    }

    [Fact]
    public void Validate_AllFieldsValid_ReturnsEmptyList()
    {
        var errors = _sut.Validate(new CreateEventRequest
        {
            Name = "Valid",
            Description = "D",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(1).AddHours(2)
        }).ToList();

        errors.Should().BeEmpty();
    }
}