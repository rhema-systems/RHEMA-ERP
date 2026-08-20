using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The separation reminder sweep (area 9b slice 10, FR-HR-111).
/// </summary>
/// <remarks>
/// <para>Five kinds. Two are FR-HR-111's own — a retirement or a contract expiry approaching with
/// no separation raised — and three come from the pipeline this area built: a clearance with
/// mandatory lines unanswered, a settlement sitting with Internal Audit, and a settlement passed
/// and never completed.</para>
///
/// <para>⚠ <b>The last one is the point.</b> A settlement that Internal Audit passed and nobody
/// completed means the employee is still on strength — the exact shape of the defect this whole
/// area was opened on, where a termination was recorded and never applied. Turning it into a
/// reminder the next morning is what stops it becoming a discovery years later.</para>
///
/// <para><b>Nothing is sent from here.</b> The sweep decides what is due and writes a dispatch row;
/// delivery is the notification engine's, as it is for the other five reminder services. A dispatch
/// row is a record of what was raised, and it deliberately holds no foreign keys to the employee or
/// the separation — it must survive them being tidied away.</para>
/// </remarks>
public class SeparationReminderService : ISeparationReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICompanyHrPolicyProvider _policyProvider;
    private readonly ISeparationService _separations;
    private readonly ILogger<SeparationReminderService> _logger;

    public SeparationReminderService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ICompanyHrPolicyProvider policyProvider,
        ISeparationService separations,
        ILogger<SeparationReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _policyProvider = policyProvider;
        _separations = separations;
        _logger = logger;
    }

    public const string KindRetirement = "RetirementApproaching";
    public const string KindContractExpiry = "ContractExpiring";
    public const string KindClearanceOutstanding = "ClearanceOutstanding";
    public const string KindSettlementAwaitingReview = "SettlementAwaitingReview";
    public const string KindSettlementNotCompleted = "SettlementApprovedNotCompleted";

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    /// <summary>
    /// How urgent this has become. Part of the dedupe key, so crossing a tier raises a fresh
    /// reminder rather than repeating a stale one.
    /// </summary>
    private static int TierFor(int daysRemaining) => daysRemaining switch
    {
        > 30 => 0,     // on the horizon
        >= 0 => 1,     // due within the month
        > -30 => 2,    // overdue
        _ => 3,        // overdue by more than a month, and nobody has acted
    };

    /// <inheritdoc />
    public async Task<IEnumerable<SeparationReminderItemDto>> PreviewAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var candidates = await FindCandidatesAsync(tenantId, cancellationToken);

        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var alreadyRaised = (await _unitOfWork.Repository<SeparationReminderDispatchLog>().GetQueryable()
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId && !d.IsDeleted && keys.Contains(d.DedupeKey))
                .Select(d => d.DedupeKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
            candidate.Item.AlreadyRaised = alreadyRaised.Contains(candidate.DedupeKey);

        return candidates.Select(c => c.Item).ToList();
    }

    /// <inheritdoc />
    public async Task<SeparationReminderRunResultDto> RunSweepAsync(
        string trigger = "Manual", CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var run = new SeparationReminderRun
        {
            TenantId = tenantId,
            StartedAt = DateTime.UtcNow,
            Trigger = string.IsNullOrWhiteSpace(trigger) ? "Manual" : trigger.Trim(),
            TriggeredByUserId = _currentUserProvider.UserId == Guid.Empty ? null : _currentUserProvider.UserId,
        };

        await _unitOfWork.Repository<SeparationReminderRun>().AddAsync(run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var candidates = await FindCandidatesAsync(tenantId, cancellationToken);

        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var alreadyRaised = (await _unitOfWork.Repository<SeparationReminderDispatchLog>().GetQueryable()
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId && !d.IsDeleted && keys.Contains(d.DedupeKey))
                .Select(d => d.DedupeKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new SeparationReminderRunResultDto
        {
            RunId = run.Id,
            StartedAt = run.StartedAt,
            Trigger = run.Trigger,
            CandidatesFound = candidates.Count,
        };

        foreach (var candidate in candidates)
        {
            if (!alreadyRaised.Add(candidate.DedupeKey))
            {
                // Already out at this tier — running the sweep twice in a morning must be harmless.
                result.SuppressedAsDuplicate++;
                candidate.Item.AlreadyRaised = true;
                continue;
            }

            await _unitOfWork.Repository<SeparationReminderDispatchLog>().AddAsync(
                new SeparationReminderDispatchLog
                {
                    TenantId = tenantId,
                    RunId = run.Id,
                    Kind = candidate.Item.Kind,
                    EmployeeId = candidate.Item.EmployeeId,
                    SeparationId = candidate.Item.SeparationId,
                    Reference = candidate.Item.Reference,
                    DueDate = candidate.Item.DueDate,
                    DaysRemaining = candidate.Item.DaysRemaining,
                    EscalationTier = candidate.Item.EscalationTier,
                    DedupeKey = candidate.DedupeKey,
                });

            result.RemindersQueued++;
            result.ByKind[candidate.Item.Kind] = result.ByKind.GetValueOrDefault(candidate.Item.Kind) + 1;
            result.Raised.Add(candidate.Item);
        }

        run.RemindersQueued = result.RemindersQueued;
        run.CompletedAt = DateTime.UtcNow;
        result.CompletedAt = run.CompletedAt;

        await _unitOfWork.Repository<SeparationReminderRun>().UpdateAsync(run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Separation reminder sweep {RunId}: {Found} candidates, {Queued} queued, {Suppressed} already out",
            run.Id, result.CandidatesFound, result.RemindersQueued, result.SuppressedAsDuplicate);

        return result;
    }

    private sealed record Candidate(SeparationReminderItemDto Item, string DedupeKey);

    /// <summary>Everything due, across all five kinds.</summary>
    private async Task<List<Candidate>> FindCandidatesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var found = new List<Candidate>();

        Candidate Make(string kind, Guid employeeId, string name, string? number,
                       Guid? separationId, string? reference, DateOnly? due, int days, string message)
        {
            var tier = TierFor(days);
            return new Candidate(
                new SeparationReminderItemDto
                {
                    Kind = kind,
                    EmployeeId = employeeId,
                    EmployeeName = name,
                    EmployeeNumber = number,
                    SeparationId = separationId,
                    Reference = reference,
                    DueDate = due,
                    DaysRemaining = days,
                    EscalationTier = tier,
                    Message = message,
                },
                // Kind + subject + due date + tier. The tier is in the key on purpose: an item that
                // ages into the next tier is a NEW reminder, not a repeat of the old one.
                $"{kind}|{separationId?.ToString() ?? employeeId.ToString()}|{due:yyyy-MM-dd}|T{tier}");
        }

        // ── FR-HR-111: retirement and contract expiry, where no exit has been raised ──
        foreach (var r in await _separations.GetUpcomingRetirementsAsync(
                     settings.RetirementCountdownLeadDays, includeOverdue: true, cancellationToken))
        {
            if (r.ExistingSeparationId is not null) continue;

            found.Add(Make(KindRetirement, r.EmployeeId, r.EmployeeName, r.EmployeeNumber,
                null, null, r.RetirementDate, r.DaysUntilRetirement,
                r.IsOverdue
                    ? $"Reached the retirement age of {r.RetirementAge} on {r.RetirementDate:yyyy-MM-dd} and is still on strength. Raise the retirement."
                    : $"Reaches the retirement age of {r.RetirementAge} on {r.RetirementDate:yyyy-MM-dd}. No separation has been raised."));
        }

        foreach (var c in await _separations.GetUpcomingContractExpiriesAsync(
                     settings.ContractExpiryLeadDays, includeOverdue: true, cancellationToken))
        {
            if (c.ExistingSeparationId is not null) continue;

            found.Add(Make(KindContractExpiry, c.EmployeeId, c.EmployeeName, c.EmployeeNumber,
                null, c.ContractNumber, c.ContractEndDate, c.DaysUntilExpiry,
                c.IsOverdue
                    ? $"Contract {c.ContractNumber} ran out on {c.ContractEndDate:yyyy-MM-dd} and the employee is still on strength."
                    : $"Contract {c.ContractNumber} runs to {c.ContractEndDate:yyyy-MM-dd}. No separation has been raised."));
        }

        // ── The pipeline's own three ──────────────────────────────────────────
        var open = await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .Include(s => s.Employee)
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                        && (s.Status == SeparationStatus.ClearanceInProgress
                            || s.Status == SeparationStatus.SettlementUnderReview
                            || s.Status == SeparationStatus.SettlementApproved))
            .ToListAsync(cancellationToken);

        var separationIds = open.Select(s => s.Id).ToList();

        var outstandingByEparation = (await _unitOfWork.Repository<SeparationClearanceItem>().GetQueryable()
                .AsNoTracking()
                .Where(i => i.TenantId == tenantId && !i.IsDeleted
                            && separationIds.Contains(i.SeparationId)
                            && i.IsMandatory
                            && (i.Status == ClearanceItemStatus.Pending || i.Status == ClearanceItemStatus.Blocked))
                .Select(i => i.SeparationId)
                .ToListAsync(cancellationToken))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var s in open)
        {
            var name = s.Employee is null ? string.Empty : $"{s.Employee.FirstName} {s.Employee.LastName}".Trim();
            var number = s.Employee?.EmployeeNumber;

            // Days are counted against the effective date where there is one — the day the exit was
            // supposed to take effect is the day the paperwork should have been done by.
            var days = s.EffectiveDate is { } effective ? effective.DayNumber - today.DayNumber : 0;

            switch (s.Status)
            {
                case SeparationStatus.ClearanceInProgress when outstandingByEparation.TryGetValue(s.Id, out var count):
                    found.Add(Make(KindClearanceOutstanding, s.EmployeeId, name, number,
                        s.Id, s.SeparationNumber, s.EffectiveDate, days,
                        $"{count} mandatory clearance line(s) are still outstanding on {s.SeparationNumber}."));
                    break;

                case SeparationStatus.SettlementUnderReview:
                    found.Add(Make(KindSettlementAwaitingReview, s.EmployeeId, name, number,
                        s.Id, s.SeparationNumber, s.EffectiveDate, days,
                        $"The settlement for {s.SeparationNumber} is with Internal Audit and has not been reviewed."));
                    break;

                case SeparationStatus.SettlementApproved:
                    // ⚠ The one that matters. Passed audit, never completed — so the employee is
                    // still on strength with an approved exit sitting behind them.
                    found.Add(Make(KindSettlementNotCompleted, s.EmployeeId, name, number,
                        s.Id, s.SeparationNumber, s.EffectiveDate, days,
                        $"{s.SeparationNumber} has been approved and paid but never completed — "
                        + "the employee is still recorded as active."));
                    break;
            }
        }

        return found
            .OrderByDescending(c => c.Item.EscalationTier)
            .ThenBy(c => c.Item.DaysRemaining)
            .ToList();
    }
}
