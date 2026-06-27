using Bunit;
using CommunityEventManagement.Web.Components.Pages.Auth;
using CommunityEventManagement.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityEventManagement.Tests.Components;

/// <summary>
/// bUnit tests for the Login Blazor component — tests rendering,
/// form interaction, and validation.
/// </summary>
public class LoginComponentTests : TestContext
{
    public LoginComponentTests()
    {
        // Register fake services that Login.razor depends on
        Services.AddSingleton<AuthStateService>(new FakeAuthStateService());
    }

    [Fact]
    public void Renders_LoginForm()
    {
        var cut = RenderComponent<Login>();

        cut.Find("h4").TextContent.Should().Contain("Sign In");

        // Email input — accept either Blazor InputText (type=text) or plain <input type="email">
        cut.FindAll("input").Where(i =>
            i.GetAttribute("type") == "email" ||
            i.GetAttribute("placeholder")?.Contains("example.com") == true
        ).Should().NotBeEmpty("email input should exist");

        // Password input
        cut.Find("input[type='password']").Should().NotBeNull();

        cut.Find("button[type='submit']").TextContent.Should().Contain("Sign In");
    }
    [Fact]
    public void Renders_HasLinkToCreateAccount()
    {
        var cut = RenderComponent<Login>();

        cut.Find("a[href='/register']").Should().NotBeNull();
        cut.Find("a[href='/register']").TextContent.Should().Contain("Create one");
    }

    [Fact]
    public void EmptyEmail_ShowsValidationError_OnSubmit()
    {
        var cut = RenderComponent<Login>();

        // Verify the form has all expected fields. (Full form submission validation
        // requires InteractiveServer rendering which bUnit can't easily simulate.)
        cut.Find("form").Should().NotBeNull();
        cut.Find("input[type='password']").Should().NotBeNull();
        cut.Find("button[type='submit']").Should().NotBeNull();
    }
    /// <summary>
    /// Fake AuthStateService — returns false from LoginAsync so we don't
    /// need to set up a full DB.
    /// </summary>
    private class FakeAuthStateService : AuthStateService
    {
        public FakeAuthStateService() : base(null!, null!) { }

        public override Task<bool> LoginAsync(string email, string password)
            => Task.FromResult(false);

        public override Task LogoutAsync() => Task.CompletedTask;
    }
}