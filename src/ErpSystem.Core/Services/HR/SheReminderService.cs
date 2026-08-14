using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SHE REMINDER ENGINE (S) — the slice-13 job engine (FRD §17).
//
// One sweep per tenant:
//   1. auto-expires permits past their planned end instant and risk assessments
//      past ValidUntil (the statuses ShePermitStatus.Expired and
//      SheRiskAssessmentStatus.Expired were dead letters until this service —
//      nothing else ever assigns them);
//   2. evaluates every dated SHE obligation against its reminder ladder and
//      queues due-soon / overdue notifications through the notification-topic
//      pipeline (EntityActivityEvent → NotificationTopics → outbox dispatcher);
//   3. records the run and every dispatched reminder in SheReminderRuns /
//      SheReminderDispatchLogs. The unique (TenantId, DedupeKey) index is the
//      send-once guarantee; a key encodes item + kind + due date + ladder rung
//      (or escalation tier), so each rung fires exactly once and a rescheduled
//      due date re-arms the ladder by producing fresh keys.
//
// Ladder primitives: SheReminderLadder.StatutoryRenewalLadder is the
// FR-ENV-017–019 180/90/60/30/14/7 sequence; slice 17's environmental permit
// register is expected to ride the same EvaluateDated mechanics.
//
// Escalation (FR-SHE-250): overdue items escalate by depth — tier 1 (≤7 days)
// notifies the HR/SHE audience, tiers 2 (≤30) and 3 (>30) publish the
// "OverdueEscalated" activity whose topic also carries SuperAdmin recipients.
// Health-surveillance recalls NEVER escalate to the broader audience — they
// stay on their own restricted topic regardless of tier (medical-adjacent data).
//
// Delivery caveat inherited from the platform: a publish with no matching
// ACTIVE topic row is a silent no-op, so the sweep self-heals its topics per
// tenant first (create-if-missing only — admin edits and deactivations are
// respected, deactivation being the supported mute switch).
// ============================================================================

/// <summary>Reminder ladder primitives shared by the SHE sweeps (and, later, slice 17's renewal ladders).</summary>
public static class SheReminderLadder
{
    /// <summary>The FR-ENV-017–019 statutory renewal ladder.</summary>
    public static readonly int[] StatutoryRenewalLadder = { 180, 90, 60, 30, 14, 7 };

    public static readonly int[] QuarterLadder = { 90, 60, 30, 14, 7 };
    public static readonly int[] TwoMonthLadder = { 60, 30, 14, 7 };
    public static readonly int[] MonthLadder = { 30, 14, 7 };
    public static readonly int[] FortnightLadder = { 14, 7 };
    public static readonly int[] PermitLadder = { 3, 1 };

    /// <summary>
    /// The rung to fire for a not-yet-due item, or null when the due date is
    /// still beyond the widest rung. Picks the smallest threshold ≥ days
    /// remaining, so a sweep that was down for a while fires only the current
    /// rung instead of back-filling stale ones.
    /// </summary>
    public static int? DueSoonRung(int daysRemaining, int[] thresholds)
    {
        var rung = thresholds.Where(t => t >= daysRemaining).DefaultIfEmpty(-1).Min();
        return rung < 0 ? null : rung;
    }

    /// <summary>Escalation tier for an overdue item: 1 (≤7 days), 2 (≤30), 3 (beyond).</summary>
    public static int EscalationTier(int daysOverdue) => daysOverdue <= 7 ? 1 : daysOverdue <= 30 ? 2 : 3;
}

public class SheReminderService : ISheReminderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<SheReminderService> _logger;

    public SheReminderService(
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        ICurrentUserProvider currentUserProvider,
        ILogger<SheReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private const string TopicEntityType = "SafetyCompliance";
    private const string Audience = "Internal";

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Reads scope to the authenticated tenant explicitly; the sweep
    // itself takes its tenant from the caller (the background service enumerates tenants).
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // ── read models ──────────────────────────────────────────────────────────

    public async Task<IEnumerable<SheReminderRunDto>> GetRecentRunsAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var runs = await _unitOfWork.Repository<SheReminderRun>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(count, 1, 100))
            .ToListAsync(cancellationToken);
        return runs.Select(ToRunDto<SheReminderRunDto>).ToList();
    }

    public async Task<IEnumerable<SheReminderLogEntryDto>> GetRecentLogAsync(int days = 14, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var floor = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 90));
        var entries = await _unitOfWork.Repository<SheReminderDispatchLog>()
            .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && l.CreatedAt >= floor)
            .OrderByDescending(l => l.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        return entries.Select(l => new SheReminderLogEntryDto
        {
            Id = l.Id,
            RunId = l.RunId,
            Kind = l.Kind,
            ItemType = l.ItemType,
            EntityId = l.EntityId,
            Reference = l.Reference,
            DueDate = l.DueDate,
            DaysRemaining = l.DaysRemaining,
            EscalationTier = l.EscalationTier,
            DispatchedAt = l.CreatedAt,
        }).ToList();
    }

    private static T ToRunDto<T>(SheReminderRun r) where T : SheReminderRunDto, new() => new()
    {
        Id = r.Id,
        StartedAt = r.StartedAt,
        CompletedAt = r.CompletedAt,
        Trigger = r.Trigger,
        RemindersQueued = r.RemindersQueued,
        PermitsExpired = r.PermitsExpired,
        RiskAssessmentsExpired = r.RiskAssessmentsExpired,
    };

    // ── the sweep ────────────────────────────────────────────────────────────

    private sealed record PendingReminder(
        string Kind,
        string ItemType,
        Guid EntityId,
        string Reference,
        DateTime? DueDate,
        int DaysRemaining,
        int EscalationTier,
        string DedupeKey,
        string Activity,
        string ActionPath,
        Dictionary<string, object>? ExtraTokens = null);

    public async Task<SheReminderRunResultDto> RunSweepForTenantAsync(Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant is required to run the SHE reminder sweep.");

        var now = DateTime.UtcNow;
        var today = now.Date;

        await EnsureTopicsAsync(tenantId, cancellationToken);

        var pending = new List<PendingReminder>();

        var permitsExpired = await SweepPermitsAsync(tenantId, now, today, pending, cancellationToken);
        var rasExpired = await SweepRiskAssessmentsAsync(tenantId, today, pending, cancellationToken);
        await SweepInspectionsAsync(tenantId, today, pending, cancellationToken);
        await SweepEquipmentAsync(tenantId, today, pending, cancellationToken);
        await SweepSignsAsync(tenantId, today, pending, cancellationToken);
        await SweepTrainingCertificatesAsync(tenantId, today, pending, cancellationToken);
        await SweepContractorsAsync(tenantId, today, pending, cancellationToken);
        await SweepRegulatoryAsync(tenantId, today, pending, cancellationToken);
        await SweepEmergencyAsync(tenantId, today, pending, cancellationToken);
        await SweepHealthSurveillanceAsync(tenantId, today, pending, cancellationToken);
        await SweepFirstAidStationsAsync(tenantId, today, pending, cancellationToken);
        await SweepPpeAsync(tenantId, today, pending, cancellationToken);

        // Dedupe against everything any earlier run already dispatched. The unique
        // (TenantId, DedupeKey) index backstops the read-then-write race; one retry
        // re-reads the claimed keys and drops the losers.
        var deduped = await FilterAlreadyDispatchedAsync(tenantId, pending, cancellationToken);

        var run = new SheReminderRun
        {
            TenantId = tenantId,
            StartedAt = now,
            Trigger = trigger,
            TriggeredByUserId = triggeredByUserId,
            PermitsExpired = permitsExpired,
            RiskAssessmentsExpired = rasExpired,
        };
        await _unitOfWork.Repository<SheReminderRun>().AddAsync(run);

        var logRepo = _unitOfWork.Repository<SheReminderDispatchLog>();
        foreach (var p in deduped)
        {
            await logRepo.AddAsync(new SheReminderDispatchLog
            {
                TenantId = tenantId,
                RunId = run.Id,
                Kind = p.Kind,
                ItemType = p.ItemType,
                EntityId = p.EntityId,
                Reference = p.Reference,
                DueDate = p.DueDate,
                DaysRemaining = p.DaysRemaining,
                EscalationTier = p.EscalationTier,
                DedupeKey = p.DedupeKey,
            });
        }

        run.RemindersQueued = deduped.Count;
        run.CompletedAt = DateTime.UtcNow;

        try
        {
            // One save claims the dedupe keys, records the run and flips the expiry
            // statuses atomically.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // A concurrent sweep (manual racing scheduled) claimed some keys first.
            // The scheduled job holds a distributed lock, so this is rare; surface it
            // as a retriable business message rather than a 500.
            _logger.LogWarning(ex, "SHE reminder sweep lost a dedupe race for tenant {TenantId}", tenantId);
            throw new InvalidOperationException("Another SHE reminder sweep is running for this tenant. Try again in a moment.");
        }

        // Publish after the claim commits — the topic handler swallows its own
        // failures, and an unpublished-but-claimed reminder is preferable to a
        // double send.
        foreach (var p in deduped)
        {
            var data = new Dictionary<string, object>
            {
                ["ItemType"] = p.ItemType,
                ["Reference"] = p.Reference,
                ["DueDate"] = p.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ["Days"] = Math.Abs(p.DaysRemaining),
                ["EscalationTier"] = p.EscalationTier,
                ["ActionPath"] = p.ActionPath,
            };
            if (p.ExtraTokens != null)
                foreach (var (k, v) in p.ExtraTokens) data[k] = v;

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = TopicEntityType,
                Activity = p.Activity,
                Audience = Audience,
                EntityId = p.EntityId,
                TriggeredByUserId = triggeredByUserId,
                Data = data,
            }, cancellationToken);
        }

        _logger.LogInformation(
            "SHE reminder sweep ({Trigger}) for tenant {TenantId}: {Queued} queued, {Permits} permits expired, {Ras} risk assessments expired",
            trigger, tenantId, deduped.Count, permitsExpired, rasExpired);

        var result = ToRunDto<SheReminderRunResultDto>(run);
        result.QueuedByKind = deduped
            .GroupBy(p => p.Kind)
            .ToDictionary(g => g.Key, g => g.Count());
        return result;
    }

    private async Task<List<PendingReminder>> FilterAlreadyDispatchedAsync(Guid tenantId, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        if (pending.Count == 0) return pending;

        // In-run duplicates first (defensive — two sweeps should never emit the same key).
        var distinct = pending
            .GroupBy(p => p.DedupeKey)
            .Select(g => g.First())
            .ToList();

        var repo = _unitOfWork.Repository<SheReminderDispatchLog>();
        var already = new HashSet<string>();
        foreach (var chunk in distinct.Select(p => p.DedupeKey).Chunk(500))
        {
            var found = await repo
                .GetQueryable(l => l.TenantId == tenantId && chunk.Contains(l.DedupeKey))
                .Select(l => l.DedupeKey)
                .ToListAsync(cancellationToken);
            foreach (var key in found) already.Add(key);
        }

        return distinct.Where(p => !already.Contains(p.DedupeKey)).ToList();
    }

    // ── ladder evaluation ────────────────────────────────────────────────────

    private static void Evaluate(
        List<PendingReminder> pending,
        DateTime today,
        string kind,
        string itemType,
        int[] thresholds,
        Guid entityId,
        string reference,
        DateTime? dueDate,
        string actionPath,
        string? fixedActivity = null,
        Dictionary<string, object>? extraTokens = null)
    {
        if (dueDate == null) return;
        var due = dueDate.Value.Date;
        var daysRemaining = (due - today).Days;

        if (daysRemaining >= 0)
        {
            var rung = SheReminderLadder.DueSoonRung(daysRemaining, thresholds);
            if (rung == null) return;
            pending.Add(new PendingReminder(kind, itemType, entityId, reference, due, daysRemaining, 0,
                $"{kind}:{entityId:N}:{due:yyyyMMdd}:D{rung}",
                fixedActivity ?? "DueSoon", actionPath, extraTokens));
        }
        else
        {
            var tier = SheReminderLadder.EscalationTier(-daysRemaining);
            pending.Add(new PendingReminder(kind, itemType, entityId, reference, due, daysRemaining, tier,
                $"{kind}:{entityId:N}:{due:yyyyMMdd}:T{tier}",
                fixedActivity ?? (tier == 1 ? "Overdue" : "OverdueEscalated"), actionPath, extraTokens));
        }
    }

    // ── domain sweeps ────────────────────────────────────────────────────────

    private async Task<int> SweepPermitsAsync(Guid tenantId, DateTime now, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        // Approved is included for completeness even though ApproveAsync goes
        // straight to Active — a permit sitting in any live status past its window
        // is spent authorisation either way.
        var live = new[] { ShePermitStatus.Approved, ShePermitStatus.Active, ShePermitStatus.Suspended };
        var permits = await _unitOfWork.Repository<ShePermitToWork>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted && live.Contains(p.Status))
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var permit in permits)
        {
            var endInstant = permit.PlannedEndDate.Date + permit.PlannedEndTime;
            var reference = $"{permit.PermitNumber} — {permit.WorkDescription}";
            var actionPath = $"/hr/safety/permits/{permit.Id}";

            if (endInstant <= now)
            {
                // FR-PTW-003 — auto-expiry. The permit keeps its suspension trail; only
                // the status flips, and the notification says the engine did it.
                permit.Status = ShePermitStatus.Expired;
                permit.UpdatedAt = now;
                await _unitOfWork.Repository<ShePermitToWork>().UpdateAsync(permit);
                expired++;

                pending.Add(new PendingReminder(
                    "PermitExpired", "Permit to work", permit.Id, reference,
                    permit.PlannedEndDate.Date, (permit.PlannedEndDate.Date - today).Days, 0,
                    $"PermitExpired:{permit.Id:N}:{permit.PlannedEndDate:yyyyMMdd}",
                    "PermitExpired", actionPath));
            }
            else
            {
                Evaluate(pending, today, "PermitExpiringSoon", "Permit to work",
                    SheReminderLadder.PermitLadder, permit.Id, reference,
                    permit.PlannedEndDate, actionPath, fixedActivity: "PermitExpiringSoon");
            }
        }
        return expired;
    }

    private async Task<int> SweepRiskAssessmentsAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var live = new[] { SheRiskAssessmentStatus.Approved, SheRiskAssessmentStatus.Active };
        var ras = await _unitOfWork.Repository<SheRiskAssessment>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && live.Contains(r.Status))
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var ra in ras)
        {
            var reference = $"{ra.AssessmentNumber} — {ra.Title}";
            var actionPath = $"/hr/safety/risk-assessments/{ra.Id}";

            if (ra.ValidUntil.HasValue && ra.ValidUntil.Value.Date < today)
            {
                // Makes the slice-3 approve guard's Expired branch reachable at last.
                ra.Status = SheRiskAssessmentStatus.Expired;
                ra.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Repository<SheRiskAssessment>().UpdateAsync(ra);
                expired++;

                pending.Add(new PendingReminder(
                    "RiskAssessmentExpired", "Risk assessment", ra.Id, reference,
                    ra.ValidUntil.Value.Date, (ra.ValidUntil.Value.Date - today).Days, 0,
                    $"RiskAssessmentExpired:{ra.Id:N}:{ra.ValidUntil.Value:yyyyMMdd}",
                    "RiskAssessmentExpired", actionPath));
                continue; // an expired RA needs re-assessment, not a review nudge
            }

            Evaluate(pending, today, "RiskAssessmentReviewDue", "Risk assessment review",
                SheReminderLadder.QuarterLadder, ra.Id, reference, ra.NextReviewDate, actionPath);
        }
        return expired;
    }

    private async Task SweepInspectionsAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<SafetyInspection>();

        // A scheduled inspection that never happened (FR-SHE-015).
        var openStatuses = new[] { SheInspectionStatus.Scheduled, SheInspectionStatus.InProgress };
        var scheduledCutoff = today.AddDays(7);
        var open = await repo
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted &&
                               openStatuses.Contains(i.Status) && i.InspectionDate <= scheduledCutoff)
            .Select(i => new { i.Id, i.InspectionNumber, i.InspectionDate })
            .ToListAsync(cancellationToken);
        foreach (var i in open)
        {
            Evaluate(pending, today, "InspectionDue", "Safety inspection",
                SheReminderLadder.FortnightLadder, i.Id, i.InspectionNumber,
                i.InspectionDate, $"/hr/safety/inspections/{i.Id}");
        }

        // The follow-up cycle a completed inspection scheduled but nobody booked.
        var doneStatuses = new[] { SheInspectionStatus.Completed, SheInspectionStatus.Closed };
        var followCutoff = today.AddDays(30);
        var follow = await repo
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted &&
                               doneStatuses.Contains(i.Status) &&
                               i.NextInspectionDueDate != null && i.NextInspectionDueDate <= followCutoff)
            .Select(i => new { i.Id, i.InspectionNumber, i.NextInspectionDueDate })
            .ToListAsync(cancellationToken);
        foreach (var i in follow)
        {
            Evaluate(pending, today, "InspectionFollowUpDue", "Follow-up inspection",
                SheReminderLadder.MonthLadder, i.Id, $"follow-up of {i.InspectionNumber}",
                i.NextInspectionDueDate, $"/hr/safety/inspections/{i.Id}");
        }
    }

    private async Task SweepEquipmentAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var outOfPlay = new[] { SheSafetyEquipmentStatus.OutOfService, SheSafetyEquipmentStatus.Decommissioned };
        var cutoff = today.AddDays(60);
        var items = await _unitOfWork.Repository<SafetyEquipment>()
            .GetQueryable(e => e.TenantId == tenantId && !e.IsDeleted && !outOfPlay.Contains(e.Status) &&
                               ((e.RequiresRegularInspection && e.NextInspectionDueDate != null && e.NextInspectionDueDate <= cutoff) ||
                                (e.NextMaintenanceDueDate != null && e.NextMaintenanceDueDate <= cutoff) ||
                                (e.RequiresCertification && e.CertificationExpiryDate != null && e.CertificationExpiryDate <= cutoff)))
            .Select(e => new
            {
                e.Id, e.EquipmentNumber, e.Name,
                e.RequiresRegularInspection, e.NextInspectionDueDate,
                e.NextMaintenanceDueDate,
                e.RequiresCertification, e.CertificationExpiryDate,
            })
            .ToListAsync(cancellationToken);

        foreach (var e in items)
        {
            var reference = $"{e.EquipmentNumber} — {e.Name}";
            var actionPath = $"/hr/safety/equipment/{e.Id}";

            if (e.RequiresRegularInspection)
                Evaluate(pending, today, "EquipmentInspectionDue", "Equipment inspection",
                    SheReminderLadder.MonthLadder, e.Id, reference, e.NextInspectionDueDate, actionPath);

            Evaluate(pending, today, "EquipmentMaintenanceDue", "Equipment maintenance",
                SheReminderLadder.MonthLadder, e.Id, reference, e.NextMaintenanceDueDate, actionPath);

            if (e.RequiresCertification)
                Evaluate(pending, today, "EquipmentCertificationExpiring", "Equipment certification",
                    SheReminderLadder.TwoMonthLadder, e.Id, reference, e.CertificationExpiryDate, actionPath);
        }
    }

    private async Task SweepSignsAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var cutoff = today.AddDays(30);
        var signs = await _unitOfWork.Repository<SafetySign>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive &&
                               s.NextInspectionDate != null && s.NextInspectionDate <= cutoff)
            .Select(s => new { s.Id, s.SignCode, s.Description, s.NextInspectionDate })
            .ToListAsync(cancellationToken);
        foreach (var s in signs)
        {
            Evaluate(pending, today, "SignInspectionDue", "Safety sign inspection",
                SheReminderLadder.MonthLadder, s.Id, $"{s.SignCode} — {s.Description}",
                s.NextInspectionDate, "/hr/safety/signs");
        }
    }

    private async Task SweepTrainingCertificatesAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        // FR-SHE-243's renewal notices. Employee.FullName is [NotMapped], so this
        // sweep materialises the (date-filtered) rows with their navs.
        var cutoff = today.AddDays(90);
        var attendances = await _unitOfWork.Repository<SheTrainingAttendance>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && a.Attended &&
                               a.CertificateExpiryDate != null && a.CertificateExpiryDate <= cutoff)
            .Include(a => a.Employee)
            .Include(a => a.Program)
            .ToListAsync(cancellationToken);

        foreach (var a in attendances)
        {
            var who = a.IsEmployee ? a.Employee?.FullName ?? a.AttendanceName : a.AttendanceName;
            Evaluate(pending, today, "TrainingCertificateExpiring", "Safety training certificate",
                SheReminderLadder.QuarterLadder, a.Id, $"{who} — {a.Program?.Title}",
                a.CertificateExpiryDate, $"/hr/safety/training/programs/{a.ProgramId}");
        }
    }

    private async Task SweepContractorsAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var cutoff = today.AddDays(90);

        var contractors = await _unitOfWork.Repository<SheContractor>()
            .GetQueryable(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive &&
                               (c.SheStatus == SheContractorStatus.Approved || c.SheStatus == SheContractorStatus.ConditionalApproval) &&
                               c.PreQualificationExpiryDate != null && c.PreQualificationExpiryDate <= cutoff)
            .Select(c => new { c.Id, c.ContractorCode, c.CompanyName, c.PreQualificationExpiryDate })
            .ToListAsync(cancellationToken);
        foreach (var c in contractors)
        {
            Evaluate(pending, today, "ContractorPreQualificationExpiring", "Contractor pre-qualification",
                SheReminderLadder.QuarterLadder, c.Id, $"{c.ContractorCode} — {c.CompanyName}",
                c.PreQualificationExpiryDate, $"/hr/safety/contractors/{c.Id}");
        }

        var docCutoff = today.AddDays(60);
        var documents = await _unitOfWork.Repository<SheContractorDocument>()
            .GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted &&
                               d.ExpiryDate != null && d.ExpiryDate <= docCutoff &&
                               !d.Contractor.IsDeleted && d.Contractor.IsActive)
            .Include(d => d.Contractor)
            .ToListAsync(cancellationToken);
        foreach (var d in documents)
        {
            var docName = string.IsNullOrWhiteSpace(d.Title) ? d.FileName : d.Title;
            Evaluate(pending, today, "ContractorDocumentExpiring", "Contractor document",
                SheReminderLadder.TwoMonthLadder, d.Id, $"{d.Contractor.CompanyName} — {docName}",
                d.ExpiryDate, $"/hr/safety/contractors/{d.ContractorId}");
        }

        var inductions = await _unitOfWork.Repository<SheContractorInduction>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted && i.InductionPassed &&
                               i.InductionExpiryDate != null && i.InductionExpiryDate <= docCutoff &&
                               !i.Contractor.IsDeleted && i.Contractor.IsActive)
            .Include(i => i.Contractor)
            .ToListAsync(cancellationToken);
        foreach (var i in inductions)
        {
            Evaluate(pending, today, "ContractorInductionExpiring", "Contractor induction",
                SheReminderLadder.TwoMonthLadder, i.Id, $"{i.WorkerName} ({i.Contractor.CompanyName})",
                i.InductionExpiryDate, $"/hr/safety/contractors/{i.ContractorId}");
        }
    }

    private async Task SweepRegulatoryAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        // The statutory 180/90/60/30/14/7 ladder lives here (FR-SHE-181; the
        // FR-ENV renewal registers ride the same primitive from slice 17).
        var cutoff = today.AddDays(180);
        var obligations = await _unitOfWork.Repository<SheRegulatoryObligation>()
            .GetQueryable(o => o.TenantId == tenantId && !o.IsDeleted && o.IsActive &&
                               o.NextReviewDate != null && o.NextReviewDate <= cutoff)
            .Select(o => new { o.Id, o.ObligationCode, o.Title, o.NextReviewDate })
            .ToListAsync(cancellationToken);
        foreach (var o in obligations)
        {
            Evaluate(pending, today, "RegulatoryReviewDue", "Regulatory obligation review",
                SheReminderLadder.StatutoryRenewalLadder, o.Id, $"{o.ObligationCode} — {o.Title}",
                o.NextReviewDate, $"/hr/safety/regulatory/{o.Id}");
        }
    }

    private async Task SweepEmergencyAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var planCutoff = today.AddDays(90);
        var plans = await _unitOfWork.Repository<EmergencyPlan>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted && p.IsActive &&
                               p.NextReviewDate <= planCutoff)
            .Select(p => new { p.Id, p.PlanNumber, p.PlanName, p.NextReviewDate })
            .ToListAsync(cancellationToken);
        foreach (var p in plans)
        {
            Evaluate(pending, today, "EmergencyPlanReviewDue", "Emergency plan review",
                SheReminderLadder.QuarterLadder, p.Id, $"{p.PlanNumber} — {p.PlanName}",
                p.NextReviewDate, $"/hr/safety/emergency/{p.Id}");
        }

        var teamCutoff = today.AddDays(60);
        var members = await _unitOfWork.Repository<EmergencyResponseTeam>()
            .GetQueryable(m => m.TenantId == tenantId && !m.IsDeleted && m.IsActive &&
                               m.CertificateExpiryDate != null && m.CertificateExpiryDate <= teamCutoff)
            .Include(m => m.Employee)
            .ToListAsync(cancellationToken);
        foreach (var m in members)
        {
            Evaluate(pending, today, "ResponseTeamCertificateExpiring", "Response-team certificate",
                SheReminderLadder.TwoMonthLadder, m.Id, $"{m.Employee?.FullName} ({m.Role})",
                m.CertificateExpiryDate, $"/hr/safety/emergency/{m.EmergencyPlanId}");
        }

        var drillCutoff = today.AddDays(30);
        var drills = await _unitOfWork.Repository<EmergencyDrill>()
            .GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted &&
                               d.NextDrillScheduledDate != null && d.NextDrillScheduledDate <= drillCutoff)
            .Select(d => new { d.Id, d.DrillNumber, d.DrillName, d.NextDrillScheduledDate, d.EmergencyPlanId })
            .ToListAsync(cancellationToken);
        foreach (var d in drills)
        {
            Evaluate(pending, today, "DrillDue", "Emergency drill",
                SheReminderLadder.MonthLadder, d.Id, $"{d.DrillNumber} — {d.DrillName}",
                d.NextDrillScheduledDate, $"/hr/safety/emergency/{d.EmergencyPlanId}");
        }
    }

    private async Task SweepHealthSurveillanceAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        // Medical-adjacent: whatever the tier, the recall stays on its own
        // restricted topic — escalation must not widen the audience for health
        // data. The notification body carries no clinical content.
        var cutoff = today.AddDays(60);
        var records = await _unitOfWork.Repository<SheOccupationalHealthSurveillance>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted &&
                               s.NextExaminationDate != null && s.NextExaminationDate <= cutoff)
            .Include(s => s.Employee)
            .ToListAsync(cancellationToken);
        foreach (var s in records)
        {
            Evaluate(pending, today, "HealthSurveillanceDue", "Health surveillance recall",
                SheReminderLadder.TwoMonthLadder, s.Id, $"{s.SurveillanceNumber} — {s.Employee?.FullName}",
                s.NextExaminationDate, "/hr/safety/occupational-health",
                fixedActivity: "HealthSurveillanceDue");
        }
    }

    private async Task SweepFirstAidStationsAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var cutoff = today.AddDays(14);
        var stations = await _unitOfWork.Repository<SheFirstAidStation>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive &&
                               s.NextInspectionDate != null && s.NextInspectionDate <= cutoff)
            .Select(s => new { s.Id, s.StationCode, s.Name, s.NextInspectionDate })
            .ToListAsync(cancellationToken);
        foreach (var s in stations)
        {
            Evaluate(pending, today, "FirstAidInspectionDue", "First-aid station inspection",
                SheReminderLadder.FortnightLadder, s.Id, $"{s.StationCode} — {s.Name}",
                s.NextInspectionDate, "/hr/safety/occupational-health");
        }
    }

    private async Task SweepPpeAsync(Guid tenantId, DateTime today, List<PendingReminder> pending, CancellationToken cancellationToken)
    {
        var cutoff = today.AddDays(30);
        var issuances = await _unitOfWork.Repository<PpeIssuance>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted && !i.IsReturned &&
                               i.ExpiryDate != null && i.ExpiryDate <= cutoff)
            .Include(i => i.Employee)
            .Include(i => i.PpeType)
            .ToListAsync(cancellationToken);
        foreach (var i in issuances)
        {
            Evaluate(pending, today, "PpeIssuanceExpiring", "Issued PPE",
                SheReminderLadder.MonthLadder, i.Id, $"{i.PpeType?.Name} — {i.Employee?.FullName}",
                i.ExpiryDate, "/hr/safety/ppe/issuances");
        }

        // Stock is threshold-triggered, not dated — the dedupe key re-arms weekly,
        // so an item sitting below reorder nags at most once a week (FR-SHE-241's
        // automated reorder alert).
        var lowStock = await _unitOfWork.Repository<PpeInventory>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted &&
                               p.ReorderLevel > 0 && p.QuantityInStock <= p.ReorderLevel)
            .Include(p => p.PpeType)
            .ToListAsync(cancellationToken);
        var calendar = System.Globalization.ISOWeek.GetWeekOfYear(today);
        foreach (var p in lowStock)
        {
            pending.Add(new PendingReminder(
                "PpeStockLow", "PPE stock", p.Id,
                $"{p.ItemCode} — {p.PpeType?.Name} ({p.Brand})",
                null, 0, 0,
                $"PpeStockLow:{p.Id:N}:{today.Year}W{calendar:D2}",
                "PpeStockLow", "/hr/safety/ppe",
                new Dictionary<string, object>
                {
                    ["Stock"] = p.QuantityInStock,
                    ["Reorder"] = p.ReorderLevel,
                }));
        }
    }

    // ── topic self-healing ───────────────────────────────────────────────────

    private sealed record TopicSeed(
        string Activity,
        string Name,
        string Description,
        string TitleTemplate,
        string BodyTemplate,
        bool EscalatesToAdmins = false);

    private static readonly TopicSeed[] TopicSeeds =
    {
        new("DueSoon", "SHE Compliance: Due Soon",
            "System-seeded SHE reminder — a dated safety obligation is approaching.",
            "SHE due soon: {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} is due on {{DueDate}} — {{Days}} day(s) remaining."),
        new("Overdue", "SHE Compliance: Overdue",
            "System-seeded SHE reminder — a dated safety obligation is past due (escalation tier 1).",
            "SHE overdue: {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} was due on {{DueDate}} — {{Days}} day(s) overdue."),
        new("OverdueEscalated", "SHE Compliance: Overdue (Escalated)",
            "System-seeded SHE escalation — an overdue safety obligation has reached tier 2 or 3.",
            "SHE escalation (tier {{EscalationTier}}): {{ItemType}} {{Reference}}",
            "{{ItemType}} {{Reference}} was due on {{DueDate}} and is {{Days}} day(s) overdue — escalation tier {{EscalationTier}}.",
            EscalatesToAdmins: true),
        new("PermitExpiringSoon", "SHE Permit: Expiring Soon",
            "System-seeded SHE reminder — a live permit to work approaches its planned end.",
            "Permit expiring: {{Reference}}",
            "Permit to work {{Reference}} expires on {{DueDate}} — {{Days}} day(s) remaining."),
        new("PermitExpired", "SHE Permit: Auto-Expired",
            "System-seeded SHE notice — the reminder engine expired a permit past its planned end.",
            "Permit expired: {{Reference}}",
            "Permit to work {{Reference}} passed its planned end ({{DueDate}}) and has been automatically expired."),
        new("RiskAssessmentExpired", "SHE Risk Assessment: Auto-Expired",
            "System-seeded SHE notice — the reminder engine expired a risk assessment past its validity.",
            "Risk assessment expired: {{Reference}}",
            "Risk assessment {{Reference}} passed its validity ({{DueDate}}) and has been marked expired."),
        new("HealthSurveillanceDue", "SHE Health Surveillance: Recall Due",
            "System-seeded SHE reminder — an occupational health surveillance recall is due. Restricted audience; never escalated more widely.",
            "Health surveillance due: {{Reference}}",
            "Occupational health surveillance {{Reference}} is due on {{DueDate}}."),
        new("PpeStockLow", "SHE PPE: Stock At Reorder Level",
            "System-seeded SHE reminder — a PPE inventory item is at or below its reorder level.",
            "PPE stock low: {{Reference}}",
            "{{Reference}} is at or below its reorder level ({{Stock}} in stock, reorder at {{Reorder}})."),
    };

    /// <summary>
    /// Creates any missing SafetyCompliance.* topics for the tenant. Create-if-missing
    /// only — existing topics (including deactivated ones: deactivation is the
    /// supported mute switch) are left exactly as the admin configured them.
    /// </summary>
    private async Task EnsureTopicsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var keys = TopicSeeds.Select(s => $"{TopicEntityType}.{s.Activity}.{Audience}").ToArray();
        var existing = await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && keys.Contains(t.Key))
            .Select(t => t.Key)
            .ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var created = false;
        foreach (var seed in TopicSeeds)
        {
            var key = $"{TopicEntityType}.{seed.Activity}.{Audience}";
            if (existingSet.Contains(key)) continue;

            var topic = new NotificationTopic
            {
                TenantId = tenantId,
                Key = key,
                Name = seed.Name,
                Description = seed.Description,
                EntityType = TopicEntityType,
                IsSystem = true,
                IsActive = true,
                EnableInApp = true,
                EnableEmail = false,
                EnableSms = false,
                InAppTitleTemplate = seed.TitleTemplate,
                InAppBodyTemplate = seed.BodyTemplate,
                ActionUrlTemplate = "{{ActionPath}}",
                CreatedBy = "System",
            };
            await topicRepo.AddAsync(topic);

            var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
            await recipientRepo.AddAsync(new NotificationTopicRecipient
            {
                TenantId = tenantId,
                TopicId = topic.Id,
                RecipientKind = "Role",
                RecipientValue = Constants.Roles.Hr,
                IsSystem = true,
                SendInApp = true,
                CreatedBy = "System",
            });
            if (seed.EscalatesToAdmins)
            {
                await recipientRepo.AddAsync(new NotificationTopicRecipient
                {
                    TenantId = tenantId,
                    TopicId = topic.Id,
                    RecipientKind = "Role",
                    RecipientValue = Constants.Roles.SuperAdmin,
                    IsSystem = true,
                    SendInApp = true,
                    CreatedBy = "System",
                });
            }
            created = true;
        }

        if (created)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded missing SafetyCompliance notification topics for tenant {TenantId}", tenantId);
        }
    }
}
