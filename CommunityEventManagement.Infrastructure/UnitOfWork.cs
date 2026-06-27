using CommunityEventManagement.Domain.Interfaces;
using CommunityEventManagement.Domain.Interfaces.Repositories;
using CommunityEventManagement.Infrastructure.Data;
using CommunityEventManagement.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CommunityEventManagement.Infrastructure
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private IDbContextTransaction? _transaction;

        private IEventRepository? _events;
        private IParticipantRepository? _participants;
        private IVenueRepository? _venues;
        private IActivityRepository? _activities;
        private IRegistrationRepository? _registrations;
        private IEventVenueRepository? _eventVenues;
        private IEventActivityRepository? _eventActivities;

        private bool _disposed = false;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public IEventRepository Events => _events ??= new EventRepository(_context);
        public IParticipantRepository Participants => _participants ??= new ParticipantRepository(_context);
        public IVenueRepository Venues => _venues ??= new VenueRepository(_context);
        public IActivityRepository Activities => _activities ??= new ActivityRepository(_context);
        public IRegistrationRepository Registrations => _registrations ??= new RegistrationRepository(_context);
        public IEventVenueRepository EventVenues => _eventVenues ??= new EventVenueRepository(_context);
        public IEventActivityRepository EventActivities => _eventActivities ??= new EventActivityRepository(_context);

        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            try
            {
                if (_transaction != null) await _transaction.CommitAsync();
            }
            finally
            {
                if (_transaction != null)
                {
                    await _transaction.DisposeAsync();
                    _transaction = null;
                }
            }
        }

        public async Task RollbackTransactionAsync()
        {
            try
            {
                if (_transaction != null) await _transaction.RollbackAsync();
            }
            finally
            {
                if (_transaction != null)
                {
                    await _transaction.DisposeAsync();
                    _transaction = null;
                }
            }
        }

        /// <summary>
        /// Detaches an entity from EF's change tracker.
        /// Used after a failed save to prevent the in-memory entity
        /// from being returned on the next query (which causes
        /// "already cancelled" bugs after rollback).
        /// </summary>
        public void Detach<TEntity>(TEntity entity) where TEntity : class
        {
            _context.Entry(entity).State = EntityState.Detached;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _transaction?.Dispose();
                _context.Dispose();
                _disposed = true;
            }
        }
    }
}