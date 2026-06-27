using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Exceptions;
using FluentAssertions;

namespace CommunityEventManagement.Tests.Unit.Domain;

public class VenueTests
{
    [Fact]
    public void Constructor_ValidInputs_CreatesVenue()
    {
        var venue = new Venue("Hall A", "1 St", "Sunderland", 200);

        venue.Name.Should().Be("Hall A");
        venue.MaxCapacity.Should().Be(200);
        venue.CurrentCapacity.Should().Be(200);  // initialised to max
    }

    [Fact]
    public void Constructor_ZeroCapacity_ThrowsVenueCapacityException()
    {
        var act = () => new Venue("X", "Y", "Z", 0);
        act.Should().Throw<VenueCapacityException>();
    }

    [Fact]
    public void Constructor_NegativeCapacity_ThrowsVenueCapacityException()
    {
        var act = () => new Venue("X", "Y", "Z", -5);
        act.Should().Throw<VenueCapacityException>();
    }

    [Fact]
    public void Constructor_EmptyName_Throws()
    {
        var act = () => new Venue("", "Y", "Z", 100);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReserveSpace_DecreasesCurrentCapacity()
    {
        var venue = new Venue("Hall", "St", "City", 100);
        venue.ReserveSpace(10);
        venue.CurrentCapacity.Should().Be(90);
    }

    [Fact]
    public void ReserveSpace_FullVenue_ThrowsVenueCapacityException()
    {
        var venue = new Venue("Hall", "St", "City", 5);
        venue.ReserveSpace(5);  // now full

        var act = () => venue.ReserveSpace(1);
        act.Should().Throw<VenueCapacityException>();
    }

    [Fact]
    public void ReleaseSpace_IncreasesCurrentCapacity()
    {
        var venue = new Venue("Hall", "St", "City", 100);
        venue.ReserveSpace(50);
        venue.ReleaseSpace(20);
        venue.CurrentCapacity.Should().Be(70);
    }

    [Fact]
    public void ReleaseSpace_CappedAtMaxCapacity()
    {
        var venue = new Venue("Hall", "St", "City", 100);
        venue.ReleaseSpace(50);  // cannot exceed max
        venue.CurrentCapacity.Should().Be(100);
    }

    [Fact]
    public void HasAvailableSpace_VariousStates_ReturnsCorrect()
    {
        var venue = new Venue("Hall", "St", "City", 5);
        venue.HasAvailableSpace().Should().BeTrue();
        venue.HasAvailableSpace(5).Should().BeTrue();

        venue.ReserveSpace(5);
        venue.HasAvailableSpace().Should().BeFalse();
        venue.HasAvailableSpace(1).Should().BeFalse();
    }

    [Fact]
    public void GetOccupancyRate_ComputesCorrectly()
    {
        var venue = new Venue("Hall", "St", "City", 100);
        venue.ReserveSpace(25);  // 75 remaining, 25 used
        venue.GetOccupancyRate().Should().Be(25m);
    }
}