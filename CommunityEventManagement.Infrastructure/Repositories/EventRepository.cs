using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Interfaces.Repositories;
using CommunityEventManagement.Domain.Models;
using CommunityEventManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CommunityEventManagement.Infrastructure.Repositories
{
    public class EventRepository : Repository<Event>, IEventRepository
    {
        public EventRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Event?> GetWithDetailsAsync(int id)
        {
            return await _context.Events
                .Include(e => e.EventVenuesCollection).ThenInclude(ev => ev.Venue)
                .Include(e => e.RegistrationsCollection).ThenInclude(r => r.Participant)
                .Include(e => e.EventActivitiesCollection).ThenInclude(ea => ea.Activity)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<IEnumerable<Event>> GetUpcomingEventsAsync()
        {
            return await _context.Events
                .Include(e => e.EventVenuesCollection).ThenInclude(ev => ev.Venue)
                .Include(e => e.EventActivitiesCollection).ThenInclude(ea => ea.Activity)
                .Where(e => e.StartDate > DateTime.Now)
                .OrderBy(e => e.StartDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<Event>> FilterEventsAsync(FilterCriteria criteria)
        {
            var query = _context.Events
                .Include(e => e.EventVenuesCollection).ThenInclude(ev => ev.Venue)
                .Include(e => e.EventActivitiesCollection).ThenInclude(ea => ea.Activity)
                .AsQueryable();

            if (criteria.StartDate.HasValue)
                query = query.Where(e => e.StartDate >= criteria.StartDate.Value);
            if (criteria.EndDate.HasValue)
                query = query.Where(e => e.EndDate <= criteria.EndDate.Value);
            if (criteria.VenueId.HasValue)
                query = query.Where(e => e.VenueId == criteria.VenueId.Value);

            // M:N venue filter
            if (criteria.VenueIds != null && criteria.VenueIds.Any())
            {
                var venueIds = criteria.VenueIds;
                query = query.Where(e =>
                    e.EventVenuesCollection.Any(ev => venueIds.Contains(ev.VenueId)) ||
                    (e.VenueId.HasValue && venueIds.Contains(e.VenueId.Value)));
            }

            if (criteria.ActivityType.HasValue)
                query = query.Where(e => e.EventActivitiesCollection
                    .Any(ea => ea.Activity != null && ea.Activity.Type == criteria.ActivityType.Value));

            if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
            {
                var term = criteria.SearchTerm.ToLower();
                query = query.Where(e =>
                    e.Name.ToLower().Contains(term) ||
                    e.Description.ToLower().Contains(term));
            }

            return await query.OrderBy(e => e.StartDate).AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<Event>> GetByVenueAsync(int venueId)
        {
            return await _context.Events
                .Include(e => e.EventVenuesCollection).ThenInclude(ev => ev.Venue)
                .Where(e => e.VenueId == venueId ||
                            e.EventVenuesCollection.Any(ev => ev.VenueId == venueId))
                .OrderBy(e => e.StartDate).AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<Event>> GetByParticipantAsync(int participantId)
        {
            return await _context.Events
                .Include(e => e.EventVenuesCollection).ThenInclude(ev => ev.Venue)
                .Include(e => e.RegistrationsCollection)
                .Where(e => e.RegistrationsCollection.Any(r => r.ParticipantId == participantId))
                .OrderBy(e => e.StartDate).AsNoTracking().ToListAsync();
        }

        public override async Task<IEnumerable<Event>> GetAllAsync()
        {
            return await _context.Events
                .Include(e => e.EventVenuesCollection).ThenInclude(ev => ev.Venue)
                .Include(e => e.EventActivitiesCollection).ThenInclude(ea => ea.Activity)
                .OrderBy(e => e.StartDate).AsNoTracking().ToListAsync();
        }
    }
}