using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Entities;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="ILeaveReminderService"/>
/// <remarks>
/// <para><b>Send-once.</b> Every candidate produces a dedupe key encoding the record, the kind, the
/// date and the escalation tier. The unique <c>(TenantId, DedupeKey)</c> index is the guarantee: a
/// sweep claims the key by inserting the log row and only publishes afterwards. Moving a date
/// produces fresh keys, which re-arms the ladder — deliberate, because leave that has been
/// rescheduled genuinely is a new thing to chase.</para>
///
/// <para><b>Publish after commit.</b> The rule every HR engine follows: an unpublished-but-claimed
/// reminder is one missed notification, whereas publishing first risks sending the same thing twice,
/// for ever, on every sweep.</para>
///
/// <para><b>⚠ It moves no balances.</b> Carry-over and forfeiture belong to
/// <c>LeaveYearEndService</c>, which is deliberately not hosted because those two acts change
/// people's entitlements. This engine only says a date is coming, which is why it can be scheduled
/// when the year-end cannot.</para>
/// </remarks>
public class LeaveReminderService : ILeaveReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<LeaveReminderService> _logger;
    private readonly ICompanyHrPolicyProvider _policyProvider;

    public LeaveReminderService(
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        ICurrentUserProvider currentUserProvider,
        ILogger<LeaveReminderService> logger,
        ICompanyHrPolicyProvider policyProvider)
    {
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _policyProvider = policyProvider;
    }

    private const string TopicEntityType = "LeaveReminder";
    private const string Audience = "Internal";

    // ⚠ The five reminder windows are no longer constants here — they live on
    // CompanyHrPolicySettings and are read per tenant in FindCandidatesAsync (residue plan G2).
    //
    // They used to be private consts documented as "the working assumption, not TDC's number", which
    // was honest and still wrong: the cadence at which a system nags people is exactly the kind of
    // thing one client wants weekly and another fortnightly, and changing it cost a deploy. This is
    // a product, not one client's build.
    //
    //   LeaveStartingReminderDays          was 7
    //   LeaveClosureGraceDays              was 2
    //   LeaveUndecidedChaseDays            was 5
    //   MandatoryLeaveChaseFromMonth       was 9
    //   LeaveCarryOverExpiryReminderDays   was 30
    //
    // The defaults on the entity are those same numbers, so nothing changes for an existing tenant
    // until somebody edits the settings page.

    /// <summary>
    /// How far back the first sweep looks. Without this the first run on an established database
    /// queues every historical breach at once — area 9's first live run queued 275, of which 242
    /// were history. A reminder about leave that ended two years ago is noise.
    /// </summary>
    private const int BacklogHorizonDays = 90;

    private Guid RequireTenant()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // ---- the sweep ---------------------------------------------------------

    public async Task<LeaveReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var run = new LeaveReminderRun
        {
            TenantId = tenantId,
            StartedAt = now,
            Trigger = trigger,
            TriggeredByUserId = triggeredByUserId,
        };
        await _unitOfWork.Repository<LeaveReminderRun>().AddAsync(run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var candidates = (await FindCandidatesAsync(tenantId, now, cancellationToken)).ToList();

        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var alreadySent = await _unitOfWork.Repository<LeaveReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && keys.Contains(l.DedupeKey))
            .Select(l => l.DedupeKey)
            .ToListAsync(cancellationToken);
        var sentSet = new HashSet<string>(alreadySent, StringComparer.Ordinal);

        var fresh = candidates.Where(c => !sentSet.Contains(c.DedupeKey)).ToList();

        foreach (var c in fresh)
        {
            await _unitOfWork.Repository<LeaveReminderDispatchLog>().AddAsync(new LeaveReminderDispatchLog
            {
                TenantId = tenantId,
                RunId = run.Id,
                Kind = c.Kind,
                ItemType = c.ItemType,
                EntityId = c.EntityId,
                EmployeeId = c.EmployeeId,
                Reference = c.Reference,
                DueDate = c.DueDate,
                DaysRemaining = c.DaysRemaining,
                EscalationTier = c.EscalationTier,
                DedupeKey = c.DedupeKey,
            });
        }

        run.RemindersQueued = fresh.Count;
        run.CompletedAt = DateTime.UtcNow;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The unique index did its job: another sweep claimed the same keys first.
            _logger.LogWarning(ex, "Leave reminder sweep lost a dedupe race for tenant {TenantId}", tenantId);
            throw new InvalidOperationException(
                "Another leave reminder sweep is running for this tenant. Try again in a moment.");
        }

        await EnsureTopicsAsync(tenantId, cancellationToken);

        // Publish only after the claim commits — see the remarks on this class.
        foreach (var c in fresh)
        {
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = TopicEntityType,
                Activity = c.EscalationTier > 0 ? "Overdue" : "DueSoon",
                Audience = Audience,
                EntityId = c.EntityId,
                TriggeredByUserId = triggeredByUserId,
                Data = new Dictionary<string, object>
                {
                    ["ItemType"] = c.ItemType,
                    ["Reference"] = c.Reference,
                    ["DueDate"] = c.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    ["Days"] = Math.Abs(c.DaysRemaining),
                    ["EscalationTier"] = c.EscalationTier,
                    ["ActionPath"] = c.ActionPath,
                },
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Leave reminder sweep for tenant {TenantId} queued {Count} reminder(s) ({Trigger}); {Skipped} already sent",
            tenantId, fresh.Count, trigger, candidates.Count - fresh.Count);

        return new LeaveReminderRunResultDto
        {
            RunId = run.Id,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Trigger = run.Trigger,
            RemindersQueued = fresh.Count,
            AlreadySent = candidates.Count - fresh.Count,
        };
    }

    public async Task<IEnumerable<LeaveReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var at = asOf ?? DateTime.UtcNow;

        var candidates = (await FindCandidatesAsync(tenantId, at, cancellationToken)).ToList();
        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var sent = new HashSet<string>(await _unitOfWork.Repository<LeaveReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && keys.Contains(l.DedupeKey))
            .Select(l => l.DedupeKey)
            .ToListAsync(cancellationToken), StringComparer.Ordinal);

        return candidates.Select(c => new LeaveReminderPreviewItemDto
        {
            Kind = c.Kind, ItemType = c.ItemType, EntityId = c.EntityId, EmployeeId = c.EmployeeId,
            EmployeeName = c.EmployeeName, Reference = c.Reference, DueDate = c.DueDate,
            DaysRemaining = c.DaysRemaining, EscalationTier = c.EscalationTier,
            DedupeKey = c.DedupeKey, AlreadySent = sent.Contains(c.DedupeKey),
        }).ToList();
    }

    public async Task<IEnumerable<LeaveReminderRunDto>> GetRecentRunsAsync(
        int count = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        return await _unitOfWork.Repository<LeaveReminderRun>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(count, 1, 200))
            .Select(r => new LeaveReminderRunDto
            {
                Id = r.Id, StartedAt = r.StartedAt, CompletedAt = r.CompletedAt,
                Trigger = r.Trigger, RemindersQueued = r.RemindersQueued,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<LeaveReminderLogEntryDto>> GetRecentLogAsync(
        int days = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365));
        return await _unitOfWork.Repository<LeaveReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && l.CreatedAt >= since)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LeaveReminderLogEntryDto
            {
                Id = l.Id, Kind = l.Kind, ItemType = l.ItemType, EntityId = l.EntityId,
                EmployeeId = l.EmployeeId, Reference = l.Reference, DueDate = l.DueDate,
                DaysRemaining = l.DaysRemaining, EscalationTier = l.EscalationTier,
                SentAt = l.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    // ---- notification topics ----------------------------------------------

    private sealed record TopicSeed(
        string Activity, string Name, string Description, string TitleTemplate, string BodyTemplate);

    /// <remarks>
    /// ⚠ The templates name the leave TYPE and the request number and nothing else — no reason, no
    /// diagnosis, no balance. A notification reaches more people than the record does, and sick
    /// leave makes that a confidentiality matter rather than a matter of taste.
    /// </remarks>
    private static readonly TopicSeed[] TopicSeeds =
    {
        new("DueSoon", "Leave: Due soon",
            "System-seeded leave reminder — a dated leave obligation is approaching.",
            "Leave due soon: {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} is due on {{DueDate}} — {{Days}} day(s) remaining."),
        new("Overdue", "Leave: Overdue",
            "System-seeded leave reminder — a leave obligation is past due.",
            "Leave overdue: {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} was due on {{DueDate}} — {{Days}} day(s) overdue (tier {{EscalationTier}})."),
    };

    private async Task EnsureTopicsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var keys = TopicSeeds.Select(s => $"{TopicEntityType}.{s.Activity}.{Audience}").ToArray();

        var existing = await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && keys.Contains(t.Key))
            .Select(t => t.Key)
            .ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        if (existingSet.Count == TopicSeeds.Length) return;

        var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
        foreach (var seed in TopicSeeds)
        {
            var key = $"{TopicEntityType}.{seed.Activity}.{Audience}";
            if (existingSet.Contains(key)) continue;

            var topic = new NotificationTopic
            {
                TenantId = tenantId, Key = key, Name = seed.Name, Description = seed.Description,
                EntityType = TopicEntityType, IsSystem = true, IsActive = true,
                EnableInApp = true, EnableEmail = false, EnableSms = false,
                InAppTitleTemplate = seed.TitleTemplate,
                InAppBodyTemplate = seed.BodyTemplate,
                ActionUrlTemplate = "{{ActionPath}}",
                CreatedBy = "System",
            };
            await topicRepo.AddAsync(topic);
            await recipientRepo.AddAsync(new NotificationTopicRecipient
            {
                TenantId = tenantId, TopicId = topic.Id,
                RecipientKind = "Role", RecipientValue = Constants.Roles.Hr,
                IsSystem = true, SendInApp = true, CreatedBy = "System",
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---- candidates --------------------------------------------------------

    private sealed record Candidate(
        string Kind, string ItemType, Guid EntityId, Guid? EmployeeId, string? EmployeeName,
        string Reference, DateTime? DueDate, int DaysRemaining, int EscalationTier,
        string DedupeKey, string ActionPath);

    /// <summary>
    /// Escalation ladder shared by every kind: due-soon is tier 0, then 1/2/3 as an overdue item
    /// ages. Expressed once so the kinds cannot drift apart.
    /// </summary>
    private static int TierFor(int daysRemaining) => daysRemaining switch
    {
        >= 0 => 0,
        >= -7 => 1,
        >= -30 => 2,
        _ => 3,
    };

    private async Task<IEnumerable<Candidate>> FindCandidatesAsync(
        Guid tenantId, DateTime at, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(at);
        var backlogFloor = today.AddDays(-BacklogHorizonDays);
        var results = new List<Candidate>();

        // ⚠ GetForTenantAsync, not GetAsync: this runs from the nightly host, which loops every
        // tenant with no signed-in user to infer one from. GetAsync would throw there — or worse,
        // read some other tenant's windows.
        var policy = await _policyProvider.GetForTenantAsync(tenantId, cancellationToken);

        // ── 1. Approved leave about to start, and nobody has said whether it is still going ────
        //
        // The other half of the confirm action (R-7). Once somebody answers, the leave stops being
        // chased — which is why ObservanceConfirmedDate is in the filter rather than only on screen.
        var startHorizon = today.AddDays(policy.LeaveStartingReminderDays);
        var starting = await _unitOfWork.Repository<LeaveRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && r.Status == LeaveStatus.Approved
                            && r.ObservanceConfirmedDate == null
                            && r.StartDate >= today
                            && r.StartDate <= startHorizon)
            .Select(r => new { r.Id, r.RequestNumber, r.EmployeeId, r.StartDate, LeaveTypeName = r.LeaveType!.Name })
            .ToListAsync(cancellationToken);

        foreach (var r in starting)
        {
            var days = r.StartDate.DayNumber - today.DayNumber;
            results.Add(new Candidate(
                "LeaveStartingSoon", "Leave request", r.Id, r.EmployeeId, null,
                $"{r.RequestNumber} · {r.LeaveTypeName}",
                r.StartDate.ToDateTime(TimeOnly.MinValue), days, 0,
                $"LeaveStartingSoon:{r.Id}:{r.StartDate:yyyy-MM-dd}:0",
                $"/hr/leave/requests/{r.Id}"));
        }

        // ── 2. Leave that ended and was never closed ──────────────────────────────────────────
        //
        // `Close` existed, refused before the end date, and nobody was ever prompted to use it — so
        // approved leave stayed approved for ever (R-10). This is the prompt.
        var closureDue = today.AddDays(-policy.LeaveClosureGraceDays);
        var unclosed = await _unitOfWork.Repository<LeaveRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && (r.Status == LeaveStatus.Approved || r.Status == LeaveStatus.InProgress)
                            && r.ClosureDate == null
                            && r.EndDate <= closureDue
                            && r.EndDate >= backlogFloor)
            .Select(r => new { r.Id, r.RequestNumber, r.EmployeeId, r.EndDate, LeaveTypeName = r.LeaveType!.Name })
            .ToListAsync(cancellationToken);

        foreach (var r in unclosed)
        {
            var days = r.EndDate.DayNumber - today.DayNumber;
            var tier = TierFor(days);
            results.Add(new Candidate(
                "LeaveNotClosed", "Leave request", r.Id, r.EmployeeId, null,
                $"{r.RequestNumber} · {r.LeaveTypeName}",
                r.EndDate.ToDateTime(TimeOnly.MinValue), days, tier,
                $"LeaveNotClosed:{r.Id}:{r.EndDate:yyyy-MM-dd}:{tier}",
                $"/hr/leave/requests/{r.Id}"));
        }

        // ── 3. A request nobody has decided ───────────────────────────────────────────────────
        //
        // Chased on its REQUEST date, not its start date: a request filed months ahead and ignored
        // is exactly the case worth catching, and waiting for the start date to approach would
        // catch it far too late.
        var pendingSince = at.AddDays(-policy.LeaveUndecidedChaseDays);
        var undecided = await _unitOfWork.Repository<LeaveRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.ChangesSuggested)
                            && r.RequestDate <= pendingSince
                            && r.RequestDate >= backlogFloor.ToDateTime(TimeOnly.MinValue))
            .Select(r => new { r.Id, r.RequestNumber, r.EmployeeId, r.RequestDate, r.Status, LeaveTypeName = r.LeaveType!.Name })
            .ToListAsync(cancellationToken);

        foreach (var r in undecided)
        {
            var waiting = (int)(at - r.RequestDate).TotalDays;
            var days = -waiting;
            var tier = TierFor(days);
            // ChangesSuggested is waiting on the EMPLOYEE, not the approver — same chase, different
            // person, and the item type is what says which.
            var item = r.Status == LeaveStatus.ChangesSuggested
                ? "Leave request awaiting the employee"
                : "Leave request awaiting a decision";
            results.Add(new Candidate(
                "RequestAwaitingDecision", item, r.Id, r.EmployeeId, null,
                $"{r.RequestNumber} · {r.LeaveTypeName}",
                r.RequestDate, days, tier,
                $"RequestAwaitingDecision:{r.Id}:{r.RequestDate:yyyy-MM-dd}:{tier}",
                $"/hr/leave/requests/{r.Id}"));
        }

        // ── 4. Mandatory leave still outstanding late in the year ─────────────────────────────
        //
        // The compliance register already answers Taken / Scheduled / Outstanding and nobody was
        // ever told about the Outstanding ones (L-23). Only chased from month 9, so it lands with
        // a quarter of the year left to act.
        if (today.Month >= policy.MandatoryLeaveChaseFromMonth)
        {
            var year = LeaveYear.For(today, policy.LeaveYearStartMonth);
            var yearEnd = LeaveYear.EndOf(year, policy.LeaveYearStartMonth);

            var mandatory = await _unitOfWork.Repository<LeaveBalance>()
                .GetQueryable(b => b.TenantId == tenantId && !b.IsDeleted
                                && b.Year == year
                                && b.LeaveType!.MandatoryAnnualLeave
                                && b.LeaveType.IsActive
                                && b.UsedDays + b.PendingDays < b.EntitledDays)
                .Select(b => new
                {
                    b.Id, b.EmployeeId, b.EntitledDays, b.UsedDays, b.PendingDays,
                    LeaveTypeName = b.LeaveType!.Name,
                })
                .ToListAsync(cancellationToken);

            foreach (var b in mandatory)
            {
                var outstanding = b.EntitledDays - b.UsedDays - b.PendingDays;
                var days = yearEnd.DayNumber - today.DayNumber;
                results.Add(new Candidate(
                    "MandatoryLeaveOutstanding", "Mandatory leave", b.Id, b.EmployeeId, null,
                    $"{b.LeaveTypeName} · {outstanding:0.##} day(s) still to take",
                    yearEnd.ToDateTime(TimeOnly.MinValue), days, 0,
                    // The year, not the date, so this fires once per employee per leave type per
                    // year rather than every day from September.
                    $"MandatoryLeaveOutstanding:{b.Id}:{year}:0",
                    "/hr/leave/compliance"));
            }
        }

        // ── 5. Carry-over about to expire ─────────────────────────────────────────────────────
        //
        // ⚠ This only WARNS. The balance is moved by LeaveYearEndService, which is deliberately not
        // scheduled — see this class's remarks.
        // ⚠ The leave year we are IN, which is not today's calendar year once a tenant starts
        // its leave year anywhere but January.
        var currentLeaveYear = LeaveYear.For(today, policy.LeaveYearStartMonth);

        var carryOver = await _unitOfWork.Repository<LeaveBalance>()
            .GetQueryable(b => b.TenantId == tenantId && !b.IsDeleted
                            && b.Year == currentLeaveYear
                            && b.CarriedOverDays > 0
                            && b.LeaveType!.AllowCarryOver
                            && b.LeaveType.CarryOverExpiryMonths != null)
            .Select(b => new
            {
                b.Id, b.EmployeeId, b.CarriedOverDays, b.UsedDays,
                LeaveTypeName = b.LeaveType!.Name,
                ExpiryMonths = b.LeaveType.CarryOverExpiryMonths!.Value,
            })
            .ToListAsync(cancellationToken);

        foreach (var b in carryOver)
        {
            // "Usable only in the first three months" for ExpiryMonths = 3 means it lapses at the
            // END of the third month OF THE LEAVE YEAR — which is March only when the leave year
            // starts in January.
            var expiry = LeaveYear.StartOf(currentLeaveYear, policy.LeaveYearStartMonth)
                .AddMonths(b.ExpiryMonths).AddDays(-1);
            var days = expiry.DayNumber - today.DayNumber;
            if (days > policy.LeaveCarryOverExpiryReminderDays || days < -BacklogHorizonDays) continue;

            // Already used more than the carried amount, so there is nothing left to lose.
            if (b.UsedDays >= b.CarriedOverDays) continue;

            var tier = TierFor(days);
            results.Add(new Candidate(
                "CarryOverExpiring", "Carry-over", b.Id, b.EmployeeId, null,
                $"{b.LeaveTypeName} · {b.CarriedOverDays - b.UsedDays:0.##} carried day(s) lapse on {expiry:d MMM}",
                expiry.ToDateTime(TimeOnly.MinValue), days, tier,
                $"CarryOverExpiring:{b.Id}:{expiry:yyyy-MM-dd}:{tier}",
                "/hr/leave/balances"));
        }

        // Names in one pass rather than one query per candidate.
        var employeeIds = results.Where(r => r.EmployeeId.HasValue)
            .Select(r => r.EmployeeId!.Value).Distinct().ToList();

        if (employeeIds.Count > 0)
        {
            var names = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Employee>()
                .GetQueryable(e => e.TenantId == tenantId && !e.IsDeleted && employeeIds.Contains(e.Id))
                .Select(e => new { e.Id, e.FirstName, e.LastName })
                .ToListAsync(cancellationToken);

            var byId = names.ToDictionary(n => n.Id, n => $"{n.FirstName} {n.LastName}".Trim());

            for (var i = 0; i < results.Count; i++)
            {
                if (results[i].EmployeeId is Guid id && byId.TryGetValue(id, out var name))
                    results[i] = results[i] with { EmployeeName = name };
            }
        }

        return results;
    }
}
