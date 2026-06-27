using CommunityEventManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CommunityEventManagement.Infrastructure.Data
{
    /// <summary>
    /// Entity Framework Core Database Context.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Event> Events { get; set; } = null!;
        public DbSet<Participant> Participants { get; set; } = null!;
        public DbSet<Administrator> Administrators { get; set; } = null!;
        public DbSet<Venue> Venues { get; set; } = null!;
        public DbSet<Activity> Activities { get; set; } = null!;
        public DbSet<Registration> Registrations { get; set; } = null!;
        public DbSet<EventActivity> EventActivities { get; set; } = null!;
        public DbSet<EventVenue> EventVenues { get; set; } = null!;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── EventActivity
            modelBuilder.Entity<EventActivity>()
                .HasKey(ea => new { ea.EventId, ea.ActivityId });
            modelBuilder.Entity<EventActivity>()
                .HasOne(ea => ea.Event)
                .WithMany(e => e.EventActivitiesCollection)
                .HasForeignKey(ea => ea.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<EventActivity>()
                .HasOne(ea => ea.Activity)
                .WithMany(a => a.EventActivities)
                .HasForeignKey(ea => ea.ActivityId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // ── EventVenue: M:N junction
            modelBuilder.Entity<EventVenue>()
                .HasKey(ev => new { ev.EventId, ev.VenueId });
            modelBuilder.Entity<EventVenue>()
                .HasOne(ev => ev.Event)
                .WithMany(e => e.EventVenuesCollection)
                .HasForeignKey(ev => ev.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<EventVenue>()
                .HasOne(ev => ev.Venue)
                .WithMany(v => v.EventVenues)
                .HasForeignKey(ev => ev.VenueId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<EventVenue>()
                .HasIndex(ev => new { ev.EventId, ev.VenueId })
                .IsUnique()
                .HasDatabaseName("IX_EventVenues_EventId_VenueId");
            modelBuilder.Entity<EventVenue>()
                .Property(ev => ev.IsPrimary)
                .HasDefaultValue(false);
            modelBuilder.Entity<EventVenue>()
                .Property(ev => ev.AssignedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // ── Registration FKs — CASCADE (so admin can delete events/participants)
            modelBuilder.Entity<Registration>()
                .HasOne(r => r.Event)
                .WithMany(e => e.RegistrationsCollection)
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Registration>()
                .HasOne(r => r.Participant)
                .WithMany(p => p.RegistrationsCollection)
                .HasForeignKey(r => r.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Registration>()
                .HasIndex(r => new { r.ParticipantId, r.EventId })
                .IsUnique();

            // ── Event.VenueId (legacy scalar — no nav)
            modelBuilder.Entity<Event>()
                .Property(e => e.VenueId)
                .IsRequired(false);

            // ── Venue: ignore legacy 1:N navigation (we use M:N EventVenue now)
            modelBuilder.Entity<Venue>()
                .Ignore(v => v.Events);

            // ── TPH discriminator for Person hierarchy
            modelBuilder.Entity<Person>()
                .HasDiscriminator<string>("UserType")
                .HasValue<Participant>("Participant")
                .HasValue<Administrator>("Administrator");

            modelBuilder.Entity<Event>().Ignore("Venues");
            //modelBuilder.Entity<Event>().Ignore("PrimaryVenue");
            //modelBuilder.Entity<Event>().Ignore("VenueNamesDisplay");

            // ── Soft delete global query filters
            // CRITICAL: Filter on Person (base) ONLY in TPH.
            // Putting filters on derived types breaks OfType<AppUser>() queries.
            modelBuilder.Entity<Event>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Venue>().HasQueryFilter(v => !v.IsDeleted);
            modelBuilder.Entity<Activity>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<Registration>().HasQueryFilter(r => !r.IsDeleted);

            // ← THIS IS THE CRITICAL ONE — without it, login breaks
            modelBuilder.Entity<Person>()
                .HasQueryFilter(p => !p.IsDeleted);

            // ── Indexes
            modelBuilder.Entity<Event>().HasIndex(e => e.StartDate).HasDatabaseName("IX_Events_StartDate");
            modelBuilder.Entity<Event>().HasIndex(e => e.VenueId).HasDatabaseName("IX_Events_VenueId");
            modelBuilder.Entity<Participant>().HasIndex(p => p.Email).IsUnique().HasDatabaseName("IX_Participants_Email");
            modelBuilder.Entity<Registration>().HasIndex(r => r.EventId).HasDatabaseName("IX_Registrations_EventId");
            modelBuilder.Entity<Registration>().HasIndex(r => r.ParticipantId).HasDatabaseName("IX_Registrations_ParticipantId");

            // ── Property configs
            modelBuilder.Entity<Event>().Property(e => e.Name).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<Venue>().Property(v => v.Name).HasMaxLength(100).IsRequired();
            modelBuilder.Entity<Activity>().Property(a => a.Name).HasMaxLength(100).IsRequired();

            modelBuilder.Entity<Registration>().Property(r => r.Status).HasConversion<string>();
            modelBuilder.Entity<Activity>().Property(a => a.Type).HasConversion<string>();
            modelBuilder.Entity<AppUser>().Property(u => u.Role).HasConversion<string>();
        }
    }
}