using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Enums;
using CommunityEventManagement.Domain.Exceptions;
using CommunityEventManagement.Infrastructure;
using CommunityEventManagement.Infrastructure.Data;
using CommunityEventManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using CommunityEventManagement.Tests.Unit;  

namespace CommunityEventManagement.Tests.Unit.Services;

public class RegistrationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly RegistrationService _sut;
    private Participant _alice = null!;
    private Event _futureEvent = null!;
    private Event _pastEvent = null!;

    public RegistrationServiceTests()
    {
  var options = TestDbContextFactory.CreateOptions($"RegTest_{Guid.NewGuid()}");
        _context = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
        _sut = new RegistrationService(_unitOfWork, NullLogger<RegistrationService>.Instance);

        SeedData();
    }

    private void SeedData()
    {
        var venue = new Venue("Test Hall", "1 St", "City", 100);
        _context.Venues.Add(venue);
        _context.SaveChanges();

        _alice = new Participant("Alice", "Test", "alice@test.com",
            BCrypt.Net.BCrypt.HashPassword("Password@1"), "07700");

        var bob = new Participant("Bob", "Test", "bob@test.com",
            BCrypt.Net.BCrypt.HashPassword("Password@1"), null);

        _context.Participants.Add(_alice);
        _context.Participants.Add(bob);
        _context.SaveChanges();

        _futureEvent = new Event("Future Event", "D",
            DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(5).AddHours(2), venue.Id);
        _pastEvent = new Event("Past Event", "D",
            DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(-5).AddHours(2), venue.Id);

        _context.Events.Add(_futureEvent);
        _context.Events.Add(_pastEvent);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Register_FutureEvent_ReturnsConfirmed()
    {
        var reg = await _sut.RegisterParticipantAsync(_alice.Id, _futureEvent.Id);

        reg.Should().NotBeNull();
        reg.Status.Should().Be(RegistrationStatus.Confirmed);
        reg.RegistrationDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Register_Duplicate_ThrowsDuplicateRegistrationException()
    {
        await _sut.RegisterParticipantAsync(_alice.Id, _futureEvent.Id);

        var act = async () => await _sut.RegisterParticipantAsync(_alice.Id, _futureEvent.Id);

        await act.Should().ThrowAsync<DuplicateRegistrationException>();
    }

    [Fact]
    public async Task Register_PastEvent_ThrowsRegistrationException()
    {
        var act = async () => await _sut.RegisterParticipantAsync(_alice.Id, _pastEvent.Id);

        await act.Should().ThrowAsync<RegistrationException>()
            .Where(e => e.Message.Contains("past"));
    }

    [Fact]
    public async Task Register_NonExistentEvent_ThrowsEventNotFoundException()
    {
        var act = async () => await _sut.RegisterParticipantAsync(_alice.Id, 999);

        await act.Should().ThrowAsync<EventNotFoundException>();
    }

    [Fact]
    public async Task Register_NonExistentParticipant_ThrowsParticipantNotFoundException()
    {
        var act = async () => await _sut.RegisterParticipantAsync(999, _futureEvent.Id);

        await act.Should().ThrowAsync<ParticipantNotFoundException>();
    }

    [Fact]
    public async Task Cancel_ConfirmedRegistration_UpdatesStatusToCancelled()
    {
        var reg = await _sut.RegisterParticipantAsync(_alice.Id, _futureEvent.Id);
        reg.Confirm();

        await _sut.CancelRegistrationAsync(reg.Id, "Changed mind");

        var refreshed = await _sut.GetRegistrationByIdAsync(reg.Id);
        refreshed!.Status.Should().Be(RegistrationStatus.Cancelled);
        refreshed.Notes.Should().Be("Changed mind");
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ThrowsRegistrationException()
    {
        var reg = await _sut.RegisterParticipantAsync(_alice.Id, _futureEvent.Id);
        await _sut.CancelRegistrationAsync(reg.Id);

        var act = async () => await _sut.CancelRegistrationAsync(reg.Id);

        await act.Should().ThrowAsync<RegistrationException>()
            .Where(e => e.Message.Contains("already cancelled"));
    }

    [Fact]
    public async Task Cancel_NonExistent_ThrowsRegistrationException()
    {
        var act = async () => await _sut.CancelRegistrationAsync(999);
        await act.Should().ThrowAsync<RegistrationException>();
    }

    [Fact]
    public async Task Confirm_FromPending_SetsToConfirmed()
    {
        // Create registration without auto-confirm
        var reg = new Registration(_futureEvent.Id, _alice.Id);
        _context.Registrations.Add(reg);
        await _context.SaveChangesAsync();

        await _sut.ConfirmRegistrationAsync(reg.Id);

        var refreshed = await _sut.GetRegistrationByIdAsync(reg.Id);
        refreshed!.Status.Should().Be(RegistrationStatus.Confirmed);
    }

    [Fact]
    public async Task IsParticipantRegistered_TrueAfterRegister_FalseAfterCancel()
    {
        var reg = await _sut.RegisterParticipantAsync(_alice.Id, _futureEvent.Id);

        (await _sut.IsParticipantRegisteredAsync(_alice.Id, _futureEvent.Id))
            .Should().BeTrue();

        await _sut.CancelRegistrationAsync(reg.Id);

        (await _sut.IsParticipantRegisteredAsync(_alice.Id, _futureEvent.Id))
            .Should().BeFalse();
    }

    [Fact]
    public async Task GetEventRegistrations_ReturnsAllForEvent()
    {
        var bob = await _context.Participants.FirstAsync(p => p.Email == "bob@test.com");
        await _sut.RegisterParticipantAsync(_alice.Id, _futureEvent.Id);
        await _sut.RegisterParticipantAsync(bob.Id, _futureEvent.Id);

        var regs = await _sut.GetEventRegistrationsAsync(_futureEvent.Id);
        regs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetParticipantRegistrations_ReturnsOnlyOwn()
    {
        var bob = await _context.Participants.FirstAsync(p => p.Email == "bob@test.com");
        await _sut.RegisterParticipantAsync(_alice.Id, _futureEvent.Id);
        await _sut.RegisterParticipantAsync(bob.Id, _futureEvent.Id);

        var aliceRegs = await _sut.GetParticipantRegistrationsAsync(_alice.Id);
        var bobRegs = await _sut.GetParticipantRegistrationsAsync(bob.Id);

        aliceRegs.Should().HaveCount(1);
        bobRegs.Should().HaveCount(1);
        aliceRegs.First().ParticipantId.Should().Be(_alice.Id);
        bobRegs.First().ParticipantId.Should().Be(bob.Id);
    }
}