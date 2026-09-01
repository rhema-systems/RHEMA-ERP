using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Raises reminders for employee identification cards that are about to expire, or have.
/// </summary>
/// <remarks>
/// <para><b>Why it exists.</b> <c>EmployeeIdentificationCard.ExpiryDate</c> has been recorded since
/// the area shipped and <b>nothing read it</b> — no sweep, no reminder, no report. Lane 3b added
/// <c>IdentificationType.ExpiryNotificationLeadDays</c> and this together, because a lead time
/// nothing acts on is a setting that only looks like a feature.</para>
///
/// <para><b>Lead days are per TYPE.</b> A passport needs more warning than a works pass, and the
/// warning belongs to the document rather than the person holding it. A type with no lead days
/// raises nothing — which is the correct reading for an ID that does not expire.</para>
///
/// <para><b>Two tiers, and they move ownership rather than volume.</b> Tier 1, inside the lead
/// window, routes to the card holder: their document, their renewal. Tier 2, once the date has
/// passed, routes to HR, because it has stopped being personal admin and become a compliance gap.
/// ⚠ HR sees BOTH throughout — the read is gated on a policy, not filtered by recipient, so this
/// log is HR's view of what is expiring. Raising a second row addressed to HR at tier 1 would
/// double the log and make "how many cards are expiring" ambiguous.</para>
/// </remarks>
public interface IIdentificationExpiryReminderService
{
    /// <summary>What the sweep would raise now, without raising it.</summary>
    Task<IEnumerable<IdentificationExpiryReminderItemDto>> PreviewAsync(CancellationToken ct = default);

    /// <summary>Runs the sweep for the current tenant.</summary>
    Task<IdentificationExpiryRunResultDto> RunSweepAsync(string trigger = "Manual", CancellationToken ct = default);

    /// <summary>Runs the sweep for a named tenant — the background service has no user to resolve one from.</summary>
    Task<IdentificationExpiryRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken ct = default);
}

public class IdentificationExpiryReminderService : IIdentificationExpiryReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<IdentificationExpiryReminderService> _logger;

    public IdentificationExpiryReminderService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<IdentificationExpiryReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private sealed record Candidate(IdentificationExpiryReminderItemDto Item, string DedupeKey);

    public async Task<IEnumerable<IdentificationExpiryReminderItemDto>> PreviewAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var candidates = await FindCandidatesAsync(tenantId, ct);

        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var alreadyRaised = (await _unitOfWork.Repository<IdentificationExpiryDispatchLog>().GetQueryable()
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId && !d.IsDeleted && keys.Contains(d.DedupeKey))
                .Select(d => d.DedupeKey)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var c in candidates)
            c.Item.AlreadyRaised = alreadyRaised.Contains(c.DedupeKey);

        return candidates.Select(c => c.Item).ToList();
    }

    public Task<IdentificationExpiryRunResultDto> RunSweepAsync(
        string trigger = "Manual", CancellationToken ct = default)
        => RunSweepForTenantAsync(
            GetTenantId(),
            trigger,
            _currentUserProvider.UserId == Guid.Empty ? null : _currentUserProvider.UserId,
            ct);

    public async Task<IdentificationExpiryRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken ct = default)
    {
        var candidates = await FindCandidatesAsync(tenantId, ct);

        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var alreadyRaised = (await _unitOfWork.Repository<IdentificationExpiryDispatchLog>().GetQueryable()
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId && !d.IsDeleted && keys.Contains(d.DedupeKey))
                .Select(d => d.DedupeKey)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fresh = candidates.Where(c => !alreadyRaised.Contains(c.DedupeKey)).ToList();

        var run = new IdentificationExpiryReminderRun
        {
            TenantId = tenantId,
            StartedAt = DateTime.UtcNow,
            Trigger = trigger,
            TriggeredByUserId = triggeredByUserId,
            RemindersQueued = fresh.Count,
            CompletedAt = DateTime.UtcNow,
        };
        await _unitOfWork.Repository<IdentificationExpiryReminderRun>().AddAsync(run);

        foreach (var c in fresh)
        {
            await _unitOfWork.Repository<IdentificationExpiryDispatchLog>().AddAsync(
                new IdentificationExpiryDispatchLog
                {
                    TenantId = tenantId,
                    RunId = run.Id,
                    Kind = c.Item.Kind,
                    EmployeeId = c.Item.EmployeeId,
                    EmployeeIdentificationCardId = c.Item.EmployeeIdentificationCardId,
                    IdentificationTypeId = c.Item.IdentificationTypeId,
                    Reference = c.Item.Reference,
                    DueDate = c.Item.DueDate,
                    DaysRemaining = c.Item.DaysRemaining,
                    EscalationTier = c.Item.EscalationTier,
                    RoutedToEmployeeId = c.Item.RoutedToEmployeeId,
                    DedupeKey = c.DedupeKey,
                });
        }

        // The run and its rows are claimed together, so a second sweep starting mid-flight cannot
        // see an empty log and raise the same reminders again.
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Identification expiry sweep for tenant {TenantId}: {Considered} card(s) due, {Queued} new reminder(s).",
            tenantId, candidates.Count, fresh.Count);

        return new IdentificationExpiryRunResultDto
        {
            RunId = run.Id,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Trigger = run.Trigger,
            CardsConsidered = candidates.Count,
            RemindersQueued = fresh.Count,
            AlreadyRaised = candidates.Count - fresh.Count,
        };
    }

    /// <summary>
    /// Every card whose type asks for a warning and whose expiry is inside it, or past.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Joined to the TYPE, not filtered on the card alone: the lead time lives on the type,
    /// and a type with none raises nothing. <c>HasExpiryDate = false</c> is also honoured, because a
    /// card whose type does not expire has no deadline whatever its date column happens to hold.</para>
    ///
    /// <para>⚠ Inactive employees are INCLUDED. A leaver holding an unreturned company ID is exactly
    /// the case HR needs surfaced; excluding them would make the sweep quietest about the situation
    /// it exists to catch.</para>
    /// </remarks>
    private async Task<List<Candidate>> FindCandidatesAsync(Guid tenantId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var rows = await _unitOfWork.Repository<EmployeeIdentificationCard>().GetQueryable()
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.ExpiryDate != null)
            .Join(_unitOfWork.Repository<IdentificationType>().GetQueryable().AsNoTracking()
                    .Where(t => t.TenantId == tenantId && !t.IsDeleted
                             && t.HasExpiryDate && t.ExpiryNotificationLeadDays != null),
                  c => c.IdentificationTypeId,
                  t => t.Id,
                  (c, t) => new
                  {
                      Card = c,
                      TypeName = t.Name,
                      LeadDays = t.ExpiryNotificationLeadDays!.Value,
                  })
            .ToListAsync(ct);

        var candidates = new List<Candidate>();

        foreach (var r in rows)
        {
            var due = r.Card.ExpiryDate!.Value;
            var daysRemaining = due.DayNumber - today.DayNumber;

            // Outside the window and not yet expired: nothing to say.
            if (daysRemaining > r.LeadDays) continue;

            var expired = daysRemaining < 0;
            var tier = expired ? 2 : 1;
            var kind = expired ? "IdentificationExpired" : "IdentificationExpiring";

            // Tier 1 is the holder's to act on; tier 2 has become HR's. Routing is an ownership
            // stamp — HR sees every row either way.
            var routedTo = expired ? (Guid?)null : r.Card.EmployeeId;

            candidates.Add(new Candidate(
                new IdentificationExpiryReminderItemDto
                {
                    Kind = kind,
                    EmployeeId = r.Card.EmployeeId,
                    EmployeeIdentificationCardId = r.Card.Id,
                    IdentificationTypeId = r.Card.IdentificationTypeId,
                    IdentificationTypeName = r.TypeName,
                    DocumentNumber = r.Card.DocumentNumber,
                    Reference = $"{r.TypeName} {r.Card.DocumentNumber}".Trim(),
                    DueDate = due,
                    DaysRemaining = daysRemaining,
                    LeadDays = r.LeadDays,
                    EscalationTier = tier,
                    RoutedToEmployeeId = routedTo,
                },
                // Kind + card + due date + tier. The due date is in the key on purpose: renewing the
                // document changes it, which re-arms the ladder for the new deadline.
                $"{kind}:{r.Card.Id}:{due:yyyy-MM-dd}:T{tier}"));
        }

        return candidates
            .OrderBy(c => c.Item.DaysRemaining)
            .ThenBy(c => c.Item.Reference)
            .ToList();
    }
}
