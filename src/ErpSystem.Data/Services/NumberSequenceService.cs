using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Services;

/// <summary>
/// Per-tenant, per-key, per-year reference-number generator backed by the <see cref="NumberSequence"/>
/// table. Increment is guarded by an optimistic concurrency token plus a bounded retry loop, so it is
/// safe under concurrent creation and never reuses a number after a soft delete. Replaces the old
/// <c>Count()+1</c> generators in the training services.
/// </summary>
public class NumberSequenceService : INumberSequenceService
{
    private const int MaxRetries = 8;

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<NumberSequenceService> _logger;

    public NumberSequenceService(
        ApplicationDbContext context,
        ICurrentUserProvider currentUser,
        ILogger<NumberSequenceService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string key, CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;
        var value = await NextAsync(key, year, cancellationToken);
        return $"{key}-{year}-{value:D3}";
    }

    public Task<long> NextAsync(string key, int? year = null, CancellationToken cancellationToken = default)
        => NextAsync(key, _currentUser.TenantId, year, cancellationToken);

    // Anonymous callers (public career portal) have no tenant claim, so the tenant is passed in from
    // the X-Tenant-Id header instead. Both overloads share one implementation so they cannot drift.
    public async Task<long> NextAsync(string key, Guid tenantId, int? year = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Sequence key is required.", nameof(key));

        // A sequence row stamped with Guid.Empty violates the NumberSequences -> Tenants FK, which
        // surfaces as an opaque 500 rather than anything actionable. Fail with the real reason.
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException(
                $"Cannot generate a '{key}' number without a tenant. The caller must supply one explicitly " +
                "when there is no authenticated tenant claim (e.g. the public career portal).");

        // Year 0 is the "not year-scoped" bucket, for numbers that do not print a year and must keep
        // counting up across year boundaries (e.g. APP-0000001).
        var yearBucket = year ?? 0;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                long value;

                var sequence = await _context.Set<NumberSequence>()
                    .FirstOrDefaultAsync(
                        s => s.TenantId == tenantId && s.SequenceKey == key && s.Year == yearBucket,
                        cancellationToken);

                if (sequence == null)
                {
                    // First number for this tenant/key/year. A concurrent creator racing us will lose
                    // on the (TenantId, SequenceKey, Year) unique index and we retry below.
                    sequence = new NumberSequence
                    {
                        TenantId = tenantId,
                        SequenceKey = key,
                        Year = yearBucket,
                        NextValue = 1
                    };
                    _context.Set<NumberSequence>().Add(sequence);
                    value = 1;
                }
                else
                {
                    sequence.NextValue += 1;
                    value = sequence.NextValue;
                }

                await _context.SaveChangesAsync(cancellationToken);
                return value;
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException)
            {
                // Lost a race (duplicate insert or stale row-version). Drop our tracked sequence
                // entries and retry with a fresh read.
                foreach (var entry in _context.ChangeTracker.Entries<NumberSequence>().ToList())
                    entry.State = EntityState.Detached;

                if (attempt == MaxRetries - 1)
                {
                    _logger.LogError(ex, "Failed to generate number for key {Key} after {Attempts} attempts", key, MaxRetries);
                    throw;
                }
            }
        }

        // Unreachable — the loop either returns or throws on the final attempt.
        throw new InvalidOperationException($"Unable to generate a reference number for '{key}'.");
    }

    public Task<long> AdvanceToAtLeastAsync(
        string key, long minimum, int? year = null, CancellationToken cancellationToken = default)
        => AdvanceToAtLeastAsync(key, minimum, _currentUser.TenantId, year, cancellationToken);

    // Both overloads share one implementation so they cannot drift — see the note on NextAsync.
    public async Task<long> AdvanceToAtLeastAsync(
        string key, long minimum, Guid tenantId, int? year = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Sequence key is required.", nameof(key));

        if (tenantId == Guid.Empty)
            throw new InvalidOperationException(
                $"Cannot advance the '{key}' counter without a tenant.");

        if (minimum < 1) return await PeekAsync(key, tenantId, year, cancellationToken);

        var yearBucket = year ?? 0;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var sequence = await _context.Set<NumberSequence>()
                    .FirstOrDefaultAsync(
                        s => s.TenantId == tenantId && s.SequenceKey == key && s.Year == yearBucket,
                        cancellationToken);

                if (sequence == null)
                {
                    sequence = new NumberSequence
                    {
                        TenantId = tenantId,
                        SequenceKey = key,
                        Year = yearBucket,
                        NextValue = minimum
                    };
                    _context.Set<NumberSequence>().Add(sequence);
                }
                else if (sequence.NextValue >= minimum)
                {
                    // Already ahead. Never wound back: a lower watermark reissues numbers that are
                    // already in the register, which is the failure this exists to prevent.
                    return sequence.NextValue;
                }
                else
                {
                    sequence.NextValue = minimum;
                }

                await _context.SaveChangesAsync(cancellationToken);
                return minimum;
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException)
            {
                foreach (var entry in _context.ChangeTracker.Entries<NumberSequence>().ToList())
                    entry.State = EntityState.Detached;

                if (attempt == MaxRetries - 1)
                {
                    _logger.LogError(ex,
                        "Failed to advance sequence {Key} to {Minimum} after {Attempts} attempts",
                        key, minimum, MaxRetries);
                    throw;
                }
            }
        }

        throw new InvalidOperationException($"Unable to advance the '{key}' counter.");
    }

    /// <summary>Where the counter stands, without moving it. Zero when it has never been used.</summary>
    public Task<long> PeekAsync(string key, int? year = null, CancellationToken cancellationToken = default)
        => PeekAsync(key, _currentUser.TenantId, year, cancellationToken);

    private async Task<long> PeekAsync(string key, Guid tenantId, int? year, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException($"Cannot read the '{key}' counter without a tenant.");

        var yearBucket = year ?? 0;
        var sequence = await _context.Set<NumberSequence>().AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId && s.SequenceKey == key && s.Year == yearBucket,
                cancellationToken);
        return sequence?.NextValue ?? 0;
    }
}
