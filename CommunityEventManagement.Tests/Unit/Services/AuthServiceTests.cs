using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Enums;
using CommunityEventManagement.Domain.Exceptions;
using CommunityEventManagement.Domain.Models;
using CommunityEventManagement.Infrastructure.Data;
using CommunityEventManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using CommunityEventManagement.Tests.Unit;  

namespace CommunityEventManagement.Tests.Unit.Services;

public class AuthServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
  var options = TestDbContextFactory.CreateOptions($"AuthTest_{Guid.NewGuid()}");
        _context = new ApplicationDbContext(options);
        _context = new ApplicationDbContext(options);
        _sut = new AuthService(_context, NullLogger<AuthService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    private Administrator CreateAdmin(bool active = true)
    {
        var admin = new Administrator("Sarah", "Admin",
            "admin@test.com", BCrypt.Net.BCrypt.HashPassword("Admin@1234"),
            "System Admin", "IT");
        if (!active) admin.Deactivate();
        _context.Administrators.Add(admin);
        _context.SaveChanges();
        return admin;
    }

    [Fact]
    public async Task Login_WithCorrectBCryptPassword_ReturnsTrue()
    {
        CreateAdmin();

        var result = await _sut.LoginAsync(new LoginModel
        {
            Email = "admin@test.com",
            Password = "Admin@1234"
        });

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsFalse()
    {
        CreateAdmin();

        var result = await _sut.LoginAsync(new LoginModel
        {
            Email = "admin@test.com",
            Password = "WrongPassword"
        });

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsFalse()
    {
        var result = await _sut.LoginAsync(new LoginModel
        {
            Email = "nobody@test.com",
            Password = "anything"
        });

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Login_InactiveUser_ThrowsAuthenticationException()
    {
        CreateAdmin(active: false);

        var act = async () => await _sut.LoginAsync(new LoginModel
        {
            Email = "admin@test.com",
            Password = "Admin@1234"
        });

        await act.Should().ThrowAsync<AuthenticationException>()
            .Where(e => e.Message.Contains("deactivated"));
    }

    [Fact]
    public async Task Login_EmailCaseInsensitive_ReturnsTrue()
    {
        CreateAdmin();

        var result = await _sut.LoginAsync(new LoginModel
        {
            Email = "ADMIN@TEST.COM",  // uppercase
            Password = "Admin@1234"
        });

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_ClearsCurrentUser()
    {
        CreateAdmin();
        await _sut.LoginAsync(new LoginModel
        {
            Email = "admin@test.com",
            Password = "Admin@1234"
        });

        await _sut.LogoutAsync();

        var isAuth = await _sut.IsAuthenticatedAsync();
        isAuth.Should().BeFalse();
    }

    [Fact]
    public async Task IsInRole_AdminUser_ReturnsTrue()
    {
        CreateAdmin();
        await _sut.LoginAsync(new LoginModel
        {
            Email = "admin@test.com",
            Password = "Admin@1234"
        });

        var isAdmin = await _sut.IsInRoleAsync("Admin");
        isAdmin.Should().BeTrue();
    }
}