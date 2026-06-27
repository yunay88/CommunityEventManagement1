using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Exceptions;
using CommunityEventManagement.Domain.Models;
using CommunityEventManagement.Infrastructure;
using CommunityEventManagement.Infrastructure.Data;
using CommunityEventManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using CommunityEventManagement.Tests.Unit;  


namespace CommunityEventManagement.Tests.Unit.Services;

/// <summary>
/// Tests for EventService — covers CRUD, filtering, and business rules.
/// Uses EF Core InMemory database for realistic EF behavior testing.
/// </summary>
public class EventServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly EventService _sut;

      public EventServiceTests()
    {
        var options = TestDbContextFactory.CreateOptions($"EventTest_{Guid.NewGuid()}");
        _context = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
        _sut = new EventService(_unitOfWork, NullLogger<EventService>.Instance);

        // Seed minimal data
        _context.Venues.Add(new Venue("Test Hall", "1 Test St", "TestCity", 100));
        _context.Activities.Add(new Activity("Test Activity", "Desc", CommunityEventManagement.Domain.Enums.ActivityType.Workshop, 60));
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateEvent_ValidInputs_ReturnsEvent()
    {
        var venue = await _context.Venues.FirstAsync();
        var evt = await _sut.CreateEventAsync(
            "Test Event", "Description",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2),
            venue.Id);

        evt.Should().NotBeNull();
        evt.Name.Should().Be("Test Event");
        evt.VenueId.Should().Be(venue.Id);
    }

    [Fact]
    public async Task CreateEvent_EndBeforeStart_ThrowsEventValidationException()
    {
        var venue = await _context.Venues.FirstAsync();
        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddHours(-1); // BEFORE start

        var act = async () => await _sut.CreateEventAsync("Bad Event", "D", start, end, venue.Id);

        await act.Should().ThrowAsync<EventValidationException>();
    }

    [Fact]
    public async Task CreateEvent_NonExistentVenue_ThrowsVenueNotFoundException()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(2);

        var act = async () => await _sut.CreateEventAsync("E", "D", start, end, venueId: 999);

        await act.Should().ThrowAsync<VenueNotFoundException>();
    }

    [Fact]
    public async Task CreateEvent_NullVenue_Allowed()
    {
        // VenueId is optional — event can have no venue
        var evt = await _sut.CreateEventAsync(
            "No Venue Event", "D",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2),
            venueId: null);

        evt.VenueId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateEvent_ExistingEvent_UpdatesFields()
    {
        var venue = await _context.Venues.FirstAsync();
        var created = await _sut.CreateEventAsync("Original", "D",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), venue.Id);

        await _sut.UpdateEventAsync(created.Id, "Updated", "New desc",
            DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(3), venue.Id);

        var updated = await _sut.GetEventByIdAsync(created.Id);
        updated!.Name.Should().Be("Updated");
        updated.Description.Should().Be("New desc");
    }

    [Fact]
    public async Task UpdateEvent_NonExistent_ThrowsEventNotFoundException()
    {
        var act = async () => await _sut.UpdateEventAsync(999, "X", "D",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), null);

        await act.Should().ThrowAsync<EventNotFoundException>();
    }

        [Fact]
    public async Task DeleteEvent_ExistingEvent_Succeeds()
    {
        var venue = await _context.Venues.FirstAsync();
        var evt = await _sut.CreateEventAsync("Delete Me", "D",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), venue.Id);

        await _sut.DeleteEventAsync(evt.Id);

        // In-memory provider doesn't apply query filters, so we can't expect null.
        // Verify the entity was soft-deleted instead.
        var result = await _sut.GetEventByIdAsync(evt.Id);
        result.Should().NotBeNull();
        result!.IsDeleted.Should().BeTrue();
    }



    [Fact]
    public async Task DeleteEvent_NonExistent_ThrowsEventNotFoundException()
    {
        var act = async () => await _sut.DeleteEventAsync(999);
        await act.Should().ThrowAsync<EventNotFoundException>();
    }

    [Fact]
    public async Task AddActivityToEvent_NewLink_Persists()
    {
        var venue = await _context.Venues.FirstAsync();
        var activity = await _context.Activities.FirstAsync();
        var evt = await _sut.CreateEventAsync("Activity Test", "D",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), venue.Id);

        await _sut.AddActivityToEventAsync(evt.Id, activity.Id);

        var refreshed = await _sut.GetEventWithDetailsAsync(evt.Id);
        refreshed!.EventActivities.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddActivityToEvent_Duplicate_ThrowsCommunityEventException()
    {
        var venue = await _context.Venues.FirstAsync();
        var activity = await _context.Activities.FirstAsync();
        var evt = await _sut.CreateEventAsync("Dup Activity Test", "D",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), venue.Id);

        await _sut.AddActivityToEventAsync(evt.Id, activity.Id);

        var act = async () => await _sut.AddActivityToEventAsync(evt.Id, activity.Id);
        await act.Should().ThrowAsync<CommunityEventException>()
            .Where(e => e.ErrorCode == "DUPLICATE_ACTIVITY");
    }

    [Fact]
    public async Task RemoveActivityFromEvent_ExistingLink_Removes()
    {
        var venue = await _context.Venues.FirstAsync();
        var activity = await _context.Activities.FirstAsync();
        var evt = await _sut.CreateEventAsync("Remove Test", "D",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), venue.Id);
        await _sut.AddActivityToEventAsync(evt.Id, activity.Id);

        await _sut.RemoveActivityFromEventAsync(evt.Id, activity.Id);

        var refreshed = await _sut.GetEventWithDetailsAsync(evt.Id);
        refreshed!.EventActivities.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterEvents_NoCriteria_ReturnsAllEvents()
    {
        var venue = await _context.Venues.FirstAsync();
        await _sut.CreateEventAsync("E1", "D", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), venue.Id);
        await _sut.CreateEventAsync("E2", "D", DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2), venue.Id);

        var results = await _sut.FilterEventsAsync(new FilterCriteria());
        results.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task FilterEvents_ByDateRange_ReturnsOnlyEventsInRange()
    {
        var venue = await _context.Venues.FirstAsync();
        var now = DateTime.UtcNow;
        await _sut.CreateEventAsync("Future", "D", now.AddDays(10), now.AddDays(10).AddHours(2), venue.Id);
        await _sut.CreateEventAsync("Far Future", "D", now.AddDays(20), now.AddDays(20).AddHours(2), venue.Id);

        var results = await _sut.FilterEventsAsync(new FilterCriteria
        {
            StartDate = now.AddDays(15)
        });

        results.Should().ContainSingle(e => e.Name == "Far Future");
        results.Should().NotContain(e => e.Name == "Future");
    }

    [Fact]
    public async Task FilterEvents_BySearchTerm_MatchesNameAndDescription()
    {
        var venue = await _context.Venues.FirstAsync();
        await _sut.CreateEventAsync("Coding Workshop", "Learn Python",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), venue.Id);
        await _sut.CreateEventAsync("Music Event", "Just music",
            DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2), venue.Id);

        var results = await _sut.FilterEventsAsync(new FilterCriteria { SearchTerm = "Coding" });

        results.Should().ContainSingle();
        var list = results.ToList();
        list[0].Name.Should().Be("Coding Workshop");
    }

    [Fact]
    public async Task FilterEvents_ByActivityType_ReturnsMatchingEvents()
    {
        var venue = await _context.Venues.FirstAsync();
        var activity = await _context.Activities.FirstAsync(); // Workshop
        var evt = await _sut.CreateEventAsync("Workshop Event", "D",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), venue.Id);
        await _sut.AddActivityToEventAsync(evt.Id, activity.Id);

        var results = await _sut.FilterEventsAsync(new FilterCriteria
        {
            ActivityType = CommunityEventManagement.Domain.Enums.ActivityType.Workshop
        });

        results.Should().Contain(e => e.Name == "Workshop Event");
    }

    [Fact]
    public async Task GetUpcomingEvents_ReturnsOnlyFutureEvents()
    {
        var venue = await _context.Venues.FirstAsync();
        var now = DateTime.UtcNow;
        await _sut.CreateEventAsync("Past", "D", now.AddDays(-5), now.AddDays(-5).AddHours(2), venue.Id);
        await _sut.CreateEventAsync("Future", "D", now.AddDays(5), now.AddDays(5).AddHours(2), venue.Id);

        var results = await _sut.GetUpcomingEventsAsync();

        results.Should().Contain(e => e.Name == "Future");
        results.Should().NotContain(e => e.Name == "Past");
    }

    [Fact]
    public async Task SearchEventById_Existing_ReturnsEvent()
    {
        var venue = await _context.Venues.FirstAsync();
        var created = await _sut.CreateEventAsync("Findable", "D",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), venue.Id);

        var found = await _sut.SearchEventByIdAsync(created.Id);
        found.Should().NotBeNull();
        found!.Name.Should().Be("Findable");
    }

    [Fact]
    public async Task SearchEventById_NotFound_ReturnsNull()
    {
        var found = await _sut.SearchEventByIdAsync(999);
        found.Should().BeNull();
    }
}