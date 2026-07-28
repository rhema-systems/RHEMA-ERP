using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.Finance;
using System.Data;

namespace ErpSystem.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{

    // Finance-specific repositories
    IAccountRepository Accounts { get; }
    IAccountSegmentStructureRepository AccountSegmentStructures { get; }
    IAccountSegmentValueRepository AccountSegmentValues { get; }
    ISegmentLookupValueRepository SegmentLookupValues { get; }

    /// <summary>
    /// Indicates that this scoped unit of work is already participating in a
    /// caller-owned database transaction.
    /// </summary>
    bool HasActiveTransaction { get; }


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
    /// Begin a database transaction at the requested isolation level.
    /// Finance settlement posting uses serializable isolation where a concurrent
    /// posting could otherwise over-settle the same source document.
    /// </summary>
    Task BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detach all tracked entities after a rejected or rolled-back operation so a later
    /// audit write cannot accidentally flush pending business changes.
    /// </summary>
    void ClearTrackedChanges();

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
