using CommunityEventManagement.Domain.Interfaces.Repositories;

namespace CommunityEventManagement.Domain.Interfaces
{
    /// <summary>
    /// Unit of Work pattern interface.
    /// Coordinates multiple repository operations as a single transaction.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        IEventRepository Events { get; }
        IParticipantRepository Participants { get; }
        IVenueRepository Venues { get; }
        IActivityRepository Activities { get; }
        IRegistrationRepository Registrations { get; }
        IEventVenueRepository EventVenues { get; }
        IEventActivityRepository EventActivities { get; }

        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();

        // ── NEW: detach an entity from the change tracker
        void Detach<TEntity>(TEntity entity) where TEntity : class;
    }
}