using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Interfaces.Repositories;
using CommunityEventManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CommunityEventManagement.Infrastructure.Repositories
{
    public class EventActivityRepository : IEventActivityRepository
    {
        protected readonly ApplicationDbContext _context;

        public EventActivityRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<EventActivity?> GetByIdAsync(int eventId, int activityId)
        {
            return await _context.EventActivities
                .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.ActivityId == activityId);
        }

        public async Task<IEnumerable<EventActivity>> GetAllAsync()
        {
            return await _context.EventActivities.AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<EventActivity>> GetByEventAsync(int eventId)
        {
            return await _context.EventActivities
                .Include(ea => ea.Activity)
                .Where(ea => ea.EventId == eventId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task AddAsync(EventActivity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            await _context.EventActivities.AddAsync(entity);
        }

        public async Task<bool> ExistsAsync(int eventId, int activityId)
        {
            return await _context.EventActivities
                .AnyAsync(ea => ea.EventId == eventId && ea.ActivityId == activityId);
        }

        public async Task RemoveLinkAsync(int eventId, int activityId)
        {
            var link = await _context.EventActivities
                .FirstOrDefaultAsync(ea => ea.EventId == eventId && ea.ActivityId == activityId);

            if (link != null)
            {
                _context.EventActivities.Remove(link);
                await Task.CompletedTask;
            }
        }
    }
}