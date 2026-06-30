using CommunityEventManagement.Domain.Interfaces;
using CommunityEventManagement.Domain.Interfaces.Services;
using CommunityEventManagement.Infrastructure;
using CommunityEventManagement.Infrastructure.Data;
using CommunityEventManagement.Infrastructure.Patterns.Factory;
using CommunityEventManagement.Infrastructure.Patterns.Facade;
using CommunityEventManagement.Infrastructure.Patterns.Observer;
using CommunityEventManagement.Infrastructure.Patterns.Observer.Observers;
using CommunityEventManagement.Infrastructure.Repositories;
using CommunityEventManagement.Infrastructure.Services;
using CommunityEventManagement.Web.Services;
using Microsoft.EntityFrameworkCore;
using CommunityEventManagement.Domain.Interfaces.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=CommunityEvents.db"),
    ServiceLifetime.Scoped);

// ── Repositories
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IParticipantRepository, ParticipantRepository>();
builder.Services.AddScoped<IVenueRepository, VenueRepository>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<IRegistrationRepository, RegistrationRepository>();
builder.Services.AddScoped<IEventVenueRepository, EventVenueRepository>();        // ← NEW
builder.Services.AddScoped<IEventActivityRepository, EventActivityRepository>();  // ← NEW

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IParticipantService, ParticipantService>();
builder.Services.AddScoped<IVenueService, VenueService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<INotificationService, NotificationService>(); 


builder.Services.AddScoped<EventFactory>();
builder.Services.AddScoped<ParticipantFactory>();
builder.Services.AddScoped<EventManagementFacade>();

builder.Services.AddSingleton<EmailNotificationObserver>();
builder.Services.AddSingleton<AuditLogObserver>();
builder.Services.AddSingleton<RegistrationNotifier>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<RegistrationNotifier>>();
    var notifier = new RegistrationNotifier(logger);
    notifier.Subscribe(provider.GetRequiredService<EmailNotificationObserver>());
    notifier.Subscribe(provider.GetRequiredService<AuditLogObserver>());
    return notifier;
});

builder.Services.AddScoped<AuthStateService>();

builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await SeedData.InitialiseAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<CommunityEventManagement.Web.Components.App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();