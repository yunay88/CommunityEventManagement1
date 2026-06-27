using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CommunityEventManagement.Infrastructure.Data
{
    public static class SeedData
    {
        public static async Task InitialiseAsync(ApplicationDbContext context)
        {
            await context.Database.MigrateAsync();
            if (await context.Venues.AnyAsync()) return;

            // ── Venues
            var venues = new List<Venue>
            {
                new Venue("City Community Hall", "123 High Street", "Sunderland", 200),
                new Venue("Riverside Sports Centre", "45 River Road", "Sunderland", 500),
                new Venue("Central Library Meeting Room", "67 Library Lane", "Newcastle", 50),
                new Venue("Innovation Hub", "89 Tech Park", "Durham", 150),
                new Venue("Online Platform", "Virtual", "Remote", 1000)
            };
            await context.Venues.AddRangeAsync(venues);
            await context.SaveChangesAsync();

            // ── Activities
            var activities = new List<Activity>
            {
                new Activity("Intro to Coding", "Hands-on beginner programming session", ActivityType.Workshop, 120),
                new Activity("Advanced C# Techniques", "Deep dive into advanced C# features", ActivityType.Workshop, 90),
                new Activity("Tech Trends Talk", "Latest trends in technology for 2024", ActivityType.Talk, 60),
                new Activity("Community Quiz Night", "Fun general knowledge quiz for all ages", ActivityType.Game, 90),
                new Activity("Local Art Exhibition", "Showcase of local artists", ActivityType.Exhibition, 180),
                new Activity("Professional Networking", "Meet local professionals", ActivityType.Networking, 60),
                new Activity("Dance Performance", "Local dance groups", ActivityType.Performance, 45),
                new Activity("Open Mic Night", "Music and spoken word", ActivityType.Performance, 120)
            };
            await context.Activities.AddRangeAsync(activities);
            await context.SaveChangesAsync();

            // ── Admin
            var admin = new Administrator("Sarah", "Johnson",
                "admin@communityevents.com", BCryptHash("Admin@1234"),
                "System Administrator", "IT");
            await context.Administrators.AddAsync(admin);
            await context.SaveChangesAsync();

            // ── Participants (BCrypt-hashed passwords)
            var participants = new List<Participant>
            {
                new Participant("Alice", "Thompson", "alice@example.com",
                    BCryptHash("Password@1"), "07700111111"),
                new Participant("Bob", "Martinez", "bob@example.com",
                    BCryptHash("Password@1"), "07700222222"),
                new Participant("Carol", "Williams", "carol@example.com",
                    BCryptHash("Password@1"), "07700333333"),
                new Participant("David", "Brown", "david@example.com",
                    BCryptHash("Password@1")),
                new Participant("Emma", "Davis", "emma@example.com",
                    BCryptHash("Password@1"), "07700555555")
            };
            await context.Participants.AddRangeAsync(participants);
            await context.SaveChangesAsync();

            // ── Events
            var now = DateTime.Now;
            var events = new List<Event>
            {
                new Event("Summer Tech Festival", "Annual tech festival",
                    now.AddDays(10), now.AddDays(12), venues[0].Id),
                new Event("Community Games Day", "Fun day for all the family",
                    now.AddDays(20), now.AddDays(20).AddHours(8), venues[1].Id),
                new Event("Winter Networking Evening", "Professional networking",
                    now.AddDays(30), now.AddDays(30).AddHours(3), venues[0].Id),
                new Event("Art and Culture Week", "Celebrating local art",
                    now.AddDays(45), now.AddDays(52), venues[2].Id),
                new Event("Online Learning Workshop", "Remote learning",
                    now.AddDays(5), now.AddDays(5).AddHours(2), venues[4].Id),
                new Event("Past Community Meetup", "Historical record",
                    now.AddDays(-30), now.AddDays(-29), venues[0].Id)
            };
            await context.Events.AddRangeAsync(events);
            await context.SaveChangesAsync();

            // ── EventActivities (many-to-many)
            var eventActivities = new List<EventActivity>
            {
                new EventActivity { EventId = events[0].Id, ActivityId = activities[0].Id, OrderInEvent = 1 },
                new EventActivity { EventId = events[0].Id, ActivityId = activities[2].Id, OrderInEvent = 2 },
                new EventActivity { EventId = events[0].Id, ActivityId = activities[5].Id, OrderInEvent = 3 },
                new EventActivity { EventId = events[1].Id, ActivityId = activities[3].Id, OrderInEvent = 1 },
                new EventActivity { EventId = events[2].Id, ActivityId = activities[5].Id, OrderInEvent = 1 },
                new EventActivity { EventId = events[2].Id, ActivityId = activities[2].Id, OrderInEvent = 2 },
                new EventActivity { EventId = events[3].Id, ActivityId = activities[4].Id, OrderInEvent = 1 },
                new EventActivity { EventId = events[3].Id, ActivityId = activities[6].Id, OrderInEvent = 2 },
                new EventActivity { EventId = events[4].Id, ActivityId = activities[1].Id, OrderInEvent = 1 }
            };
            await context.EventActivities.AddRangeAsync(eventActivities);
            await context.SaveChangesAsync();

            // ── EventVenues (M:N — demonstrates "various Venues" per brief)
            var eventVenues = new List<EventVenue>
            {
                new EventVenue { EventId = events[0].Id, VenueId = venues[0].Id, IsPrimary = true,  DisplayOrder = 1 },
                new EventVenue { EventId = events[0].Id, VenueId = venues[3].Id, IsPrimary = false, DisplayOrder = 2 },
                new EventVenue { EventId = events[1].Id, VenueId = venues[1].Id, IsPrimary = true,  DisplayOrder = 1 },
                new EventVenue { EventId = events[2].Id, VenueId = venues[0].Id, IsPrimary = true,  DisplayOrder = 1 },
                new EventVenue { EventId = events[2].Id, VenueId = venues[4].Id, IsPrimary = false, DisplayOrder = 2 },
                new EventVenue { EventId = events[4].Id, VenueId = venues[4].Id, IsPrimary = true,  DisplayOrder = 1 }
            };
            await context.EventVenues.AddRangeAsync(eventVenues);
            await context.SaveChangesAsync();

            // ── Registrations
            var registrations = new List<Registration>
            {
                new Registration(events[0].Id, participants[0].Id, "Very excited for this!"),
                new Registration(events[0].Id, participants[1].Id),
                new Registration(events[0].Id, participants[2].Id),
                new Registration(events[1].Id, participants[0].Id),
                new Registration(events[1].Id, participants[3].Id),
                new Registration(events[2].Id, participants[4].Id, "Looking forward to networking"),
                new Registration(events[4].Id, participants[1].Id),
                new Registration(events[4].Id, participants[2].Id)
            };
            foreach (var reg in registrations) reg.Confirm();
            await context.Registrations.AddRangeAsync(registrations);
            await context.SaveChangesAsync();
        }

        // Real BCrypt hashing (replaces broken base64 placeholder)
        private static string BCryptHash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
        }
    }
}