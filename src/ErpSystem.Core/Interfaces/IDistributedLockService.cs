namespace ErpSystem.Core.Interfaces;

public interface IDistributedLockService
{
    /// <summary>
    /// Tries to acquire a lease lock. Returns null if another instance currently holds the lock.
    /// </summary>
    Task<IAsyncDisposable?> TryAcquireAsync(string lockName, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
}

