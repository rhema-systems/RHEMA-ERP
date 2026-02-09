using System.Data;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services;

/// <summary>
/// Simple SQL/EF-backed lease lock for leader election.
/// Uses a single row per lock name and serializable transaction to avoid races.
/// </summary>
public sealed class DistributedLockService : IDistributedLockService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DistributedLockService> _logger;
    private readonly string _instanceId;

    public DistributedLockService(ApplicationDbContext db, ILogger<DistributedLockService> logger, IHostEnvironment env)
    {
        _db = db;
        _logger = logger;
        _instanceId = $"{env.EnvironmentName}:{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(string lockName, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lockName)) return null;

        var name = lockName.Trim();
        var now = DateTime.UtcNow;
        var leaseUntil = now.Add(leaseDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : leaseDuration);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                var row = await _db.Set<DistributedLock>()
                    .SingleOrDefaultAsync(l => l.LockName == name, cancellationToken);

                if (row == null)
                {
                    row = new DistributedLock
                    {
                        Id = Guid.NewGuid(),
                        LockName = name,
                        AcquiredBy = _instanceId,
                        AcquiredAtUtc = now,
                        LastHeartbeatUtc = now,
                        LeaseUntilUtc = leaseUntil,
                        CreatedAt = now,
                    };

                    await _db.AddAsync(row, cancellationToken);
                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    return new Handle(this, name, _instanceId);
                }

                if (row.LeaseUntilUtc <= now || string.IsNullOrWhiteSpace(row.AcquiredBy) || string.Equals(row.AcquiredBy, _instanceId, StringComparison.OrdinalIgnoreCase))
                {
                    var wasExpired = row.LeaseUntilUtc <= now || string.IsNullOrWhiteSpace(row.AcquiredBy);
                    row.AcquiredBy = _instanceId;
                    row.LeaseUntilUtc = leaseUntil;
                    row.LastHeartbeatUtc = now;
                    if (wasExpired) row.AcquiredAtUtc = now;
                    row.UpdatedAt = now;

                    _db.Update(row);
                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    return new Handle(this, name, _instanceId);
                }

                await tx.RollbackAsync(cancellationToken);
                return null;
            }
            catch (DbUpdateException ex)
            {
                // Unique index race on first insert; retry once.
                await tx.RollbackAsync(cancellationToken);
                _logger.LogDebug(ex, "Distributed lock acquisition race for {LockName} (attempt {Attempt})", name, attempt + 1);
            }
        }

        return null;
    }

    private async Task ReleaseAsync(string lockName, string instanceId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var row = await _db.Set<DistributedLock>().SingleOrDefaultAsync(l => l.LockName == lockName, cancellationToken);
        if (row == null)
        {
            await tx.CommitAsync(cancellationToken);
            return;
        }

        if (!string.Equals(row.AcquiredBy, instanceId, StringComparison.OrdinalIgnoreCase))
        {
            await tx.CommitAsync(cancellationToken);
            return;
        }

        row.AcquiredBy = null;
        row.AcquiredAtUtc = null;
        row.LastHeartbeatUtc = null;
        row.LeaseUntilUtc = now.AddSeconds(-1);
        row.UpdatedAt = now;

        _db.Update(row);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private sealed class Handle : IAsyncDisposable
    {
        private readonly DistributedLockService _svc;
        private readonly string _lockName;
        private readonly string _instanceId;
        private int _disposed;

        public Handle(DistributedLockService svc, string lockName, string instanceId)
        {
            _svc = svc;
            _lockName = lockName;
            _instanceId = instanceId;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            try
            {
                await _svc.ReleaseAsync(_lockName, _instanceId);
            }
            catch
            {
                // swallow; lock will expire by lease anyway
            }
        }
    }
}

