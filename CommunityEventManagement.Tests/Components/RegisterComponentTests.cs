using Bunit;
using CommunityEventManagement.Web.Components.Pages.Auth;
using CommunityEventManagement.Web.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityEventManagement.Tests.Components;

public class RegisterComponentTests : TestContext
{
    public RegisterComponentTests()
    {
        Services.AddSingleton<AuthStateService>(new FakeAuthStateService());
    }

    [Fact]
    public void Renders_AllFormFields()
    {
        var cut = RenderComponent<Register>();

        cut.Find("input[placeholder='First name']").Should().NotBeNull();
        cut.Find("input[placeholder='Last name']").Should().NotBeNull();
        cut.Find("input[placeholder*='example.com']").Should().NotBeNull();
        cut.Find("input[placeholder*='characters']").Should().NotBeNull();  // password
    }

    [Fact]
    public void Renders_HasSignInLink()
    {
        var cut = RenderComponent<Register>();

        cut.Find("a[href='/login']").Should().NotBeNull();
        cut.Find("a[href='/login']").TextContent.Should().Contain("Sign in");
    }

    [Fact]
    public void Renders_CreateAccountButton()
    {
        var cut = RenderComponent<Register>();

        cut.Find("button[type='submit']").TextContent.Should().Contain("Create Account");
    }

    private class FakeAuthStateService : AuthStateService
    {
        public FakeAuthStateService() : base(null!, null!) { ClearGlobalCache(); }

        public override Task<RegisterResult> RegisterParticipantAsync(
            string firstName, string lastName, string email,
            string? phoneNumber, string password)
            => Task.FromResult(new RegisterResult { Success = false, ErrorMessage = "fake" });
    }
}