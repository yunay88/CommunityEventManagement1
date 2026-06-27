using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Interfaces.Repositories;
using CommunityEventManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CommunityEventManagement.Infrastructure.Repositories
{
    public class EventVenueRepository : IEventVenueRepository
    {
        protected readonly ApplicationDbContext _context;

        public EventVenueRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<EventVenue?> GetByIdAsync(int eventId, int venueId)
        {
            return await _context.EventVenues
                .Include(ev => ev.Venue)
                .FirstOrDefaultAsync(ev => ev.EventId == eventId && ev.VenueId == venueId);
        }

        public async Task<IEnumerable<EventVenue>> GetAllAsync()
        {
            return await _context.EventVenues
                .Include(ev => ev.Event)
                .Include(ev => ev.Venue)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task AddAsync(EventVenue entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            await _context.EventVenues.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<EventVenue> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));
            await _context.EventVenues.AddRangeAsync(entities);
        }

        public async Task<IEnumerable<EventVenue>> GetByEventAsync(int eventId)
        {
            return await _context.EventVenues
                .Include(ev => ev.Venue)
                .Where(ev => ev.EventId == eventId)
                .OrderBy(ev => ev.DisplayOrder)
                .ThenBy(ev => ev.AssignedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IEnumerable<EventVenue>> GetByVenueAsync(int venueId)
        {
            return await _context.EventVenues
                .Include(ev => ev.Event)
                .Where(ev => ev.VenueId == venueId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<EventVenue?> GetPrimaryForEventAsync(int eventId)
        {
            return await _context.EventVenues
                .Include(ev => ev.Venue)
                .FirstOrDefaultAsync(ev => ev.EventId == eventId && ev.IsPrimary);
        }

        public async Task<bool> ExistsAsync(int eventId, int venueId)
        {
            return await _context.EventVenues
                .AnyAsync(ev => ev.EventId == eventId && ev.VenueId == venueId);
        }

        public async Task RemoveLinkAsync(int eventId, int venueId)
        {
            var link = await _context.EventVenues
                .FirstOrDefaultAsync(ev => ev.EventId == eventId && ev.VenueId == venueId);

            if (link != null)
            {
                _context.EventVenues.Remove(link);
                await Task.CompletedTask;
            }
        }
    }
}