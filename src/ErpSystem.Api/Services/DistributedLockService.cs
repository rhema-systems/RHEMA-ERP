using System.Data;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        var effectiveLeaseDuration = leaseDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : leaseDuration;

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<int, IAsyncDisposable?>(0, async (dbContext, _, ct) =>
        {
            var now = DateTime.UtcNow;
            var leaseUntil = now.Add(effectiveLeaseDuration);

            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                try
                {
                    var row = await dbContext.Set<DistributedLock>()
                        .SingleOrDefaultAsync(l => l.LockName == name, ct);

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

                        await dbContext.AddAsync(row, ct);
                        await dbContext.SaveChangesAsync(ct);
                        await tx.CommitAsync(ct);

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

                        dbContext.Update(row);
                        await dbContext.SaveChangesAsync(ct);
                        await tx.CommitAsync(ct);

                        return new Handle(this, name, _instanceId);
                    }

                    await tx.RollbackAsync(ct);
                    return null;
                }
                catch (DbUpdateException ex)
                {
                    // Unique index race on first insert; retry once.
                    await tx.RollbackAsync(ct);
                    _logger.LogDebug(ex, "Distributed lock acquisition race for {LockName} (attempt {Attempt})", name, attempt + 1);
                }
            }

            return null;
        }, null, cancellationToken);
    }

    private async Task ReleaseAsync(string lockName, string instanceId, CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync<int, int>(0, async (dbContext, _, ct) =>
        {
            var now = DateTime.UtcNow;

            await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var row = await dbContext.Set<DistributedLock>().SingleOrDefaultAsync(l => l.LockName == lockName, ct);
            if (row == null)
            {
                await tx.CommitAsync(ct);
                return 0;
            }

            if (!string.Equals(row.AcquiredBy, instanceId, StringComparison.OrdinalIgnoreCase))
            {
                await tx.CommitAsync(ct);
                return 0;
            }

            row.AcquiredBy = null;
            row.AcquiredAtUtc = null;
            row.LastHeartbeatUtc = null;
            row.LeaseUntilUtc = now.AddSeconds(-1);
            row.UpdatedAt = now;

            dbContext.Update(row);
            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return 0;
        }, null, cancellationToken);
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

