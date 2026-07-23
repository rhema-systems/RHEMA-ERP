using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{

    // Finance-specific repositories
    IAccountRepository Accounts { get; }
    IAccountSegmentStructureRepository AccountSegmentStructures { get; }
    IAccountSegmentValueRepository AccountSegmentValues { get; }
    ISegmentLookupValueRepository SegmentLookupValues { get; }


    /// <summary>
    /// Save all pending changes to the database
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save all pending changes to the database synchronously
    /// </summary>
    int SaveChanges();

    /// <summary>
    /// Begin a database transaction
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get repository for specific entity type
    /// </summary>
    IGenericRepository<T> Repository<T>() where T : BaseEntity;

    /// <summary>
    /// Execute an operation within an execution strategy (for SQL Server retry logic)
    /// </summary>
    Task ExecuteInStrategyAsync(Func<Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a value-returning operation within an execution strategy (for SQL Server retry logic)
    /// </summary>
    Task<T> ExecuteInStrategyAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
}
