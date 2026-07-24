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

    // [HR-MODULE-PORT] The two members below were added for HR services. They are ADDITIVE — existing
    // Finance/Inventory/other implementers and callers are unaffected. See HR_MODULE_PORT_PLAN.md.

    /// <summary>
    /// Execute an operation inside a database transaction under a retrying execution strategy.
    /// Saves changes and commits on success; rolls back on exception.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detach all tracked entities from the change tracker (e.g. between bulk batches).
    /// </summary>
    void ClearChangeTracker();

    /// <summary>
    /// Execute a value-returning operation within an execution strategy (for SQL Server retry logic)
    /// </summary>
    Task<T> ExecuteInStrategyAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
}
