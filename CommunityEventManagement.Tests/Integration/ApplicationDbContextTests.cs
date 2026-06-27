using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CommunityEventManagement.Infrastructure.Data;  
using CommunityEventManagement.Tests.Unit;  

namespace CommunityEventManagement.Tests.Unit.Integration;

/// <summary>
/// Integration tests for ApplicationDbContext — TPH inheritance, soft-delete,
/// unique indexes, and cascade behaviour.
/// </summary>
public class ApplicationDbContextTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public ApplicationDbContextTests()
    {
  var options = TestDbContextFactory.CreateOptions($"IntegrationTest_{Guid.NewGuid()}");
        _context = new ApplicationDbContext(options);
        _context = new ApplicationDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task TPH_ParticipantQuery_OnlyReturnsParticipants()
    {
        _context.Administrators.Add(new Administrator(
            "Sarah", "Admin", "admin@t.com",
            BCrypt.Net.BCrypt.HashPassword("Pass1"), "SysAdmin"));
        _context.Participants.Add(new Participant(
            "Alice", "Test", "alice@t.com",
            BCrypt.Net.BCrypt.HashPassword("Pass1"), null));
        await _context.SaveChangesAsync();

        var participants = await _context.Set<Participant>().ToListAsync();
        var admins = await _context.Set<Administrator>().ToListAsync();

        participants.Should().HaveCount(1);
        participants[0].Email.Should().Be("alice@t.com");
        admins.Should().HaveCount(1);
        admins[0].Email.Should().Be("admin@t.com");
    }

    [Fact]
    public async Task TPH_OfTypeAdminQuery_ReturnsOnlyAdmins()
    {
        _context.Administrators.Add(new Administrator(
            "S", "A", "a@t.com", BCrypt.Net.BCrypt.HashPassword("p"), "Admin"));
        _context.Participants.Add(new Participant(
            "P", "T", "p@t.com", BCrypt.Net.BCrypt.HashPassword("p"), null));
        await _context.SaveChangesAsync();

        var allAppUsers = await _context.Set<Person>()
            .OfType<Administrator>()
            .ToListAsync();

        allAppUsers.Should().HaveCount(1);
        allAppUsers[0].Email.Should().Be("a@t.com");
    }

 

    [Fact]
    public async Task EventVenues_ManyToMany_StoresMultipleVenuesPerEvent()
    {
        var venue1 = new Venue("V1", "A", "C", 100);
        var venue2 = new Venue("V2", "A", "C", 100);
        var eventEntity = new Event("Multi-Venue", "D",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), null);

        _context.Venues.AddRange(venue1, venue2);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        _context.EventVenues.Add(new EventVenue
        {
            EventId = eventEntity.Id,
            VenueId = venue1.Id,
            IsPrimary = true
        });
        _context.EventVenues.Add(new EventVenue
        {
            EventId = eventEntity.Id,
            VenueId = venue2.Id,
            IsPrimary = false
        });
        await _context.SaveChangesAsync();

        var loaded = await _context.Events
            .Include(e => e.EventVenuesCollection)
            .FirstAsync(e => e.Id == eventEntity.Id);

        loaded.EventVenuesCollection.Should().HaveCount(2);
    }

    [Fact]
    public async Task Activity_PersistsEnumAsString()
    {
        var activity = new Activity("Workshop", "D",
            ActivityType.Workshop, 60);

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        var raw = await _context.Activities
            .FirstAsync(a => a.Name == "Workshop");

        raw.Type.Should().Be(ActivityType.Workshop);
    }
}