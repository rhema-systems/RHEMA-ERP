using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Repositories.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace ErpSystem.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<Type, object> _repositories;
    private IDbContextTransaction? _transaction;
    private bool _ownsTransaction;
    private bool _disposed = false;
    private bool _useExecutionStrategy = false;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        _repositories = new Dictionary<Type, object>();
    }

    //FINANCE
    private IAccountRepository? _accountRepository;
    private IAccountSegmentStructureRepository? _accountSegmentStructureRepository;
    private IAccountSegmentValueRepository? _accountSegmentValueRepository;
    private ISegmentLookupValueRepository? _segmentLookupValueRepository;

    public IAccountRepository Accounts =>
        _accountRepository ??= new AccountRepository(_context);

    public IAccountSegmentStructureRepository AccountSegmentStructures =>
        _accountSegmentStructureRepository ??= new AccountSegmentStructureRepository(_context);

    public IAccountSegmentValueRepository AccountSegmentValues =>
        _accountSegmentValueRepository ??= new AccountSegmentValueRepository(_context);

    public ISegmentLookupValueRepository SegmentLookupValues =>
        _segmentLookupValueRepository ??= new SegmentLookupValueRepository(_context);

    public bool HasActiveTransaction =>
        _transaction is not null || _context.Database.CurrentTransaction is not null;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public int SaveChanges()
    {
        return _context.SaveChanges();
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => await BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

    public async Task BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("Transaction is already started");
        }

        // A Finance posting service may be called inside a controller-owned transaction.
        // Join it rather than starting or committing a nested transaction that would split the unit of work.
        if (_context.Database.CurrentTransaction != null)
        {
            _transaction = _context.Database.CurrentTransaction;
            _ownsTransaction = false;
            return;
        }

        _transaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        _ownsTransaction = true;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction to commit");
        }

        try
        {
            await SaveChangesAsync(cancellationToken);
            if (_ownsTransaction)
            {
                await _transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            // Rollback only if transaction is still active
            if (_transaction != null && _ownsTransaction)
            {
                await _transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
            }
            throw;
        }
        finally
        {
            // Dispose transaction if it still exists
            if (_transaction != null && _ownsTransaction)
            {
                await _transaction.DisposeAsync();
            }

            _transaction = null;
            _ownsTransaction = false;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction to rollback");
        }

        try
        {
            if (_ownsTransaction)
            {
                await _transaction.RollbackAsync(cancellationToken);
                // Do not allow a later audit save on this DbContext to re-persist entities
                // that were rolled back from the transactional Finance posting attempt.
                _context.ChangeTracker.Clear();
            }
        }
        finally
        {
            if (_ownsTransaction)
            {
                await _transaction.DisposeAsync();
            }
            _transaction = null;
            _ownsTransaction = false;
        }
    }

    public void ClearTrackedChanges()
    {
        _context.ChangeTracker.Clear();
    }

    public IGenericRepository<T> Repository<T>() where T : BaseEntity
    {
        var type = typeof(T);
        
        if (!_repositories.ContainsKey(type))
        {
            _repositories[type] = new GenericRepository<T>(_context);
        }
        
        return (IGenericRepository<T>)_repositories[type];
    }
    
    public async Task ExecuteInStrategyAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(operation);
    }

    public async Task<T> ExecuteInStrategyAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(operation);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
