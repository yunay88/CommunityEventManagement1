using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Exceptions;
using CommunityEventManagement.Infrastructure.Patterns.Factory;
using FluentAssertions;

namespace CommunityEventManagement.Tests.Unit.Services;

public class ParticipantFactoryTests
{
    private readonly ParticipantFactory _sut = new();

    [Fact]
    public void Create_ValidRequest_HashesPasswordWithBCrypt()
    {
        var request = new CreateParticipantRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@test.com",
            Password = "SecurePass123",
            PhoneNumber = "07700123456"
        };

        var participant = _sut.Create(request);

        participant.PasswordHash.Should().NotBe("SecurePass123");
        participant.PasswordHash.Should().StartWith("$2");  // BCrypt format
        BCrypt.Net.BCrypt.Verify("SecurePass123", participant.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public void Create_WeakPassword_Throws()
    {
        var request = new CreateParticipantRequest
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@test.com",
            Password = "123"  // too short
        };

        var act = () => _sut.Create(request);
        act.Should().Throw<RegistrationException>();
    }

    [Fact]
    public void Create_InvalidEmail_Throws()
    {
        var request = new CreateParticipantRequest
        {
            FirstName = "Bad",
            LastName = "Email",
            Email = "not-an-email",
            Password = "ValidPass1"
        };

        var act = () => _sut.Create(request);
        act.Should().Throw<RegistrationException>();
    }

    [Fact]
    public void Create_InvalidPhone_Throws()
    {
        var request = new CreateParticipantRequest
        {
            FirstName = "Bad",
            LastName = "Phone",
            Email = "bp@test.com",
            Password = "ValidPass1",
            PhoneNumber = "abc"
        };

        var act = () => _sut.Create(request);
        act.Should().Throw<RegistrationException>();
    }

    [Fact]
    public void CreateWithHashedPassword_DoesNotRehash()
    {
        var precomputedHash = BCrypt.Net.BCrypt.HashPassword("password");
        var participant = _sut.CreateWithHashedPassword(
            "Jane", "Doe", "jane@test.com", precomputedHash);

        participant.PasswordHash.Should().Be(precomputedHash);
    }
}