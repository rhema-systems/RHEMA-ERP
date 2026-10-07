using System.Globalization;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Entities;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
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
/// <para><b>Who is told (round 5, lane I).</b> Each reminder goes to the people who can act on it,
/// in the app and by email, through a topic of its own — <c>LeaveReminder.{Kind}.{Audience}</c>, so
/// each audience reads its own words:</para>
/// <list type="bullet">
/// <item>leave starting soon, and carried days about to lapse — the employee;</item>
/// <item>a request waiting — whoever its current approval step is asking, as the engine's own
/// "approval required" notice does (named approvers, and everyone holding the step's role), never
/// the employee whose leave it is; a request sent back with other dates — the employee;</item>
/// <item>leave ended and not closed, once the return is reported — the line manager (the
/// supervisor, or failing one the nearest head of unit, as in lane D's confirm rule);</item>
/// <item>annual leave not yet planned or taken (the September chase) — the employee, their
/// supervisor in ONE message naming all their people, and HR in one summary;</item>
/// <item>"you can now take annual leave" — the employee, and HR in one summary.</item>
/// </list>
/// <para>Anything with nobody else to go to — no return reported, no approver asked, an employee or
/// a line manager with no login — goes to HR, saying why. Before this every leave reminder went, in
/// the app only, to the HR role, and the event carried no employee to route by. Admins can change
/// any topic's recipients and wording on the Notification Topics screen.</para>
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
    private readonly ILeaveUsageReader _usage;
    private readonly ILeaveEntitlementService _entitlement;
    private readonly IHrWorkingDayCalculator _workingDays;
    private readonly IHrClosureCalendar _closures;
    private readonly UserManager<ApplicationUser> _userManager;

    public LeaveReminderService(
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        ICurrentUserProvider currentUserProvider,
        ILogger<LeaveReminderService> logger,
        ICompanyHrPolicyProvider policyProvider,
        ILeaveUsageReader usage,
        ILeaveEntitlementService entitlement,
        IHrWorkingDayCalculator workingDays,
        IHrClosureCalendar closures,
        UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _policyProvider = policyProvider;
        _usage = usage;
        _entitlement = entitlement;
        _workingDays = workingDays;
        _closures = closures;
        _userManager = userManager;
    }

    private const string TopicEntityType = "LeaveReminder";

    // The reminder kinds, as the dispatch log and the topic keys name them.
    private const string KindStartingSoon = "LeaveStartingSoon";
    private const string KindNotClosed = "LeaveNotClosed";
    private const string KindAwaitingDecision = "RequestAwaitingDecision";
    private const string KindAnnualOutstanding = "AnnualLeaveOutstanding";
    private const string KindCarryOverExpiring = "CarryOverExpiring";
    private const string KindAnnualAvailable = "AnnualLeaveAvailable";

    // Not a sweep reminder: said once, by the act that recounted the leave (company-schedule final
    // closure, lane 1c). It shares the topics because these are leave's words and recipient rules.
    private const string KindRecharged = "LeaveRecharged";

    // Who a message is for: the last part of the topic key.
    private const string ToEmployee = "Employee";
    private const string ToManager = "Manager";
    private const string ToConfirmer = "Confirmer";
    private const string ToApprover = "Approver";
    private const string ToHr = "Hr";
    private const string ToHrDigest = "HrDigest";

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

    /// <summary>How many people a supervisor's September message names before "and N more".</summary>
    private const int ManagerDigestNames = 10;

    private Guid RequireTenant()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private static string FormatDate(DateTime? date)
        => date?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

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

        // Publish only after the claim commits — see the remarks on this class. One event per
        // message, each on its audience's own topic.
        foreach (var c in fresh)
        {
            foreach (var delivery in c.Deliveries)
            {
                await PublishAsync(tenantId, c.Kind, delivery.Audience, c.EntityId, triggeredByUserId,
                    ItemData(c, delivery), cancellationToken);
            }
        }

        // The September chase reaches a supervisor as ONE message naming their people, not one per
        // person: per-item messages flood whoever they all share (round 4 lane K-a's lesson).
        foreach (var team in fresh
                     .Where(c => c.ManagerEmployeeId is not null)
                     .GroupBy(c => (c.Kind, Manager: c.ManagerEmployeeId!.Value)))
        {
            var people = team.OrderBy(c => c.EmployeeName, StringComparer.OrdinalIgnoreCase).ToList();
            var named = people.Take(ManagerDigestNames).Select(c =>
                c.Data.TryGetValue("Outstanding", out var days) ? $"{c.EmployeeName} ({days} day(s))" : c.EmployeeName);
            var list = string.Join(", ", named)
                       + (people.Count > ManagerDigestNames ? $", and {people.Count - ManagerDigestNames} more" : string.Empty);

            await PublishAsync(tenantId, team.Key.Kind, ToManager, null, triggeredByUserId, new Dictionary<string, object>
            {
                ["ManagerEmployeeId"] = team.Key.Manager,
                ["Count"] = people.Count,
                ["People"] = list,
                ["DueDate"] = FormatDate(people[0].DueDate),
                ["ActionPath"] = "/me/team",
            }, cancellationToken);
        }

        // The company-wide chases reach HR once per run, as a count: the September chase alone can
        // name two thousand people. The count says how many could not be told directly.
        foreach (var digest in fresh.Where(c => c.DigestForHr).GroupBy(c => c.Kind))
        {
            var count = digest.Count();
            var notReached = digest.Count(c => c.Deliveries.All(d => d.Audience != ToEmployee));
            await PublishAsync(tenantId, digest.Key, ToHrDigest, null, triggeredByUserId, new Dictionary<string, object>
            {
                ["Count"] = count,
                ["Reach"] = notReached == 0
                    ? "Each of them was told directly."
                    : $"{notReached} of them have no login and could not be told directly.",
                ["DueDate"] = FormatDate(digest.First().DueDate),
                ["ActionPath"] = "/hr/leave/balances",
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
            SentTo = AudiencesOf(c),
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

    // ---- publishing ---------------------------------------------------------

    private Task PublishAsync(
        Guid tenantId, string kind, string audience, Guid? entityId, Guid? triggeredByUserId,
        Dictionary<string, object> data, CancellationToken cancellationToken)
        => _appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = tenantId,
            EntityType = TopicEntityType,
            Activity = kind,
            Audience = audience,
            EntityId = entityId,
            TriggeredByUserId = triggeredByUserId,
            Data = data,
        }, cancellationToken);

    public async Task NotifyLeaveRechargedAsync(
        Guid tenantId, LeaveRechargeLineDto line, string because, CancellationToken cancellationToken = default)
    {
        await EnsureTopicsAsync(tenantId, cancellationToken);

        // The same test the sweep makes: an employee with an active login is told; anybody else's
        // news goes to HR, who can tell them — a message to nobody is what lane I closed.
        var reachable = await _userManager.Users.AnyAsync(
            u => u.TenantId == tenantId && u.IsActive && u.EmployeeId == line.EmployeeId, cancellationToken);

        // ⚠ The leave TYPE and number, never a reason: a notice travels further than the record.
        var data = new Dictionary<string, object>
        {
            ["Reference"] = $"{line.RequestNumber} · {line.LeaveTypeName}",
            ["OldDays"] = line.OldDays.ToString("0.##", CultureInfo.InvariantCulture),
            ["NewDays"] = line.NewDays.ToString("0.##", CultureInfo.InvariantCulture),
            ["Because"] = because,
            ["EmployeeName"] = line.EmployeeName,
            ["EmployeeId"] = line.EmployeeId,
        };
        data["ActionPath"] = reachable ? $"/me/leave/{line.RequestId}" : $"/hr/leave/requests/{line.RequestId}";
        if (!reachable) data["Why"] = "They have no login to be told, so please let them know.";

        await PublishAsync(
            tenantId, KindRecharged, reachable ? ToEmployee : ToHr, line.RequestId,
            _currentUserProvider.UserId == Guid.Empty ? null : _currentUserProvider.UserId,
            data, cancellationToken);
    }

    /// <summary>What one message about one item carries: the item's facts, then its audience's own.</summary>
    private static Dictionary<string, object> ItemData(Candidate c, Delivery delivery)
    {
        var data = new Dictionary<string, object>(c.Data)
        {
            ["ItemType"] = c.ItemType,
            ["Reference"] = c.Reference,
            ["DueDate"] = FormatDate(c.DueDate),
            ["Days"] = Math.Abs(c.DaysRemaining),
            ["EscalationTier"] = c.EscalationTier,
            ["EmployeeName"] = c.EmployeeName ?? string.Empty,
            ["ActionPath"] = delivery.ActionPath,
        };
        if (c.EmployeeId is Guid employeeId)
            data["EmployeeId"] = employeeId;
        foreach (var (name, value) in delivery.Data)
            data[name] = value;
        return data;
    }

    /// <summary>The audiences one item reaches, as the preview reports them.</summary>
    private static List<string> AudiencesOf(Candidate c)
    {
        var to = c.Deliveries.Select(d => d.Audience).ToList();
        if (c.ManagerEmployeeId is not null) to.Add(ToManager);
        if (c.DigestForHr) to.Add(ToHrDigest);
        return to;
    }

    // ---- notification topics ----------------------------------------------

    /// <summary>
    /// One topic per kind and audience, <c>LeaveReminder.{Kind}.{Audience}</c>: the rule that finds
    /// its recipients and the words it says to them. The email uses the same title and text.
    /// </summary>
    private sealed record TopicSeed(
        string Kind, string Audience, string Name, string Description,
        string TitleTemplate, string BodyTemplate, (string Kind, string Value)[] Recipients);

    // ⚠ Declared before TopicSeeds: static fields initialise in textual order, and the seeds read these.
    private static readonly (string, string)[] SubjectEmployee = { ("UserFromEmployeeIdData", "EmployeeId") };
    private static readonly (string, string)[] HrRole = { ("Role", Constants.Roles.Hr) };

    /// <remarks>
    /// ⚠ The templates name the leave TYPE and the request number and nothing else — no reason, no
    /// diagnosis. A notification reaches more people than the record does, and sick leave makes that
    /// a confidentiality matter rather than a matter of taste. The employee's name goes only to the
    /// people who act on their leave: their line manager, the approver, HR.
    /// </remarks>
    private static readonly TopicSeed[] TopicSeeds =
    {
        new(KindStartingSoon, ToEmployee, "Leave: starting soon (employee)",
            "Sent to the employee a few days before approved leave starts, until somebody says it is still going ahead.",
            "Your leave starts soon: {{Reference}}",
            "{{Reference}} starts on {{DueDate}}, in {{Days}} day(s). If you are still going, open it under My Leave and choose Yes, still going. If your plans have changed, tell HR.",
            SubjectEmployee),
        new(KindStartingSoon, ToHr, "Leave: starting soon, employee not reachable (HR)",
            "Sent to HR when approved leave starts soon and the employee has no login to be asked about it.",
            "Leave starting soon: {{EmployeeName}}",
            "{{EmployeeName}}'s leave {{Reference}} starts on {{DueDate}}, in {{Days}} day(s). {{Why}}",
            HrRole),
        new(KindNotClosed, ToConfirmer, "Leave: return to confirm (line manager)",
            "Sent to the employee's line manager — their supervisor, or failing one the nearest head of unit — when they have reported being back and nobody has confirmed it.",
            "Confirm a return from leave: {{EmployeeName}}",
            "{{EmployeeName}} has reported being back from {{Reference}}, which ended on {{DueDate}}. Open the request and confirm the return to close the leave.",
            new[] { ("UserFromEmployeeIdData", "ConfirmerEmployeeId") }),
        new(KindNotClosed, ToHr, "Leave: not closed (HR)",
            "Sent to HR when leave has ended and not been closed, and nobody else can be asked to close it.",
            "Leave not closed: {{EmployeeName}}",
            "{{EmployeeName}}'s leave {{Reference}} ended on {{DueDate}}, {{Days}} day(s) ago, and has not been closed. {{Why}}",
            HrRole),
        new(KindAwaitingDecision, ToApprover, "Leave: waiting for your decision (approver)",
            "Sent to the people the request's current approval step is asking — named approvers, and everyone holding the step's role, as the approval notice itself — never to the employee whose leave it is.",
            "Leave waiting for your decision: {{EmployeeName}}",
            "{{EmployeeName}}'s request {{Reference}} has waited {{Days}} day(s) for a decision. Open it to approve it, reject it or suggest other dates.",
            new[] { ("UsersFromData", "ApproverUserIds") }),
        new(KindAwaitingDecision, ToEmployee, "Leave: suggested dates to answer (employee)",
            "Sent to the employee when their approver suggested other dates and they have not answered.",
            "Your leave request needs your answer: {{Reference}}",
            "Your approver suggested other dates for {{Reference}}. Open it under My Leave to accept them or suggest your own.",
            SubjectEmployee),
        new(KindAwaitingDecision, ToHr, "Leave: request waiting, nobody else to ask (HR)",
            "Sent to HR when a leave request has waited and nobody else can be asked to move it on.",
            "Leave request waiting: {{EmployeeName}}",
            "{{EmployeeName}}'s request {{Reference}} has waited {{Days}} day(s). {{Why}}",
            HrRole),
        new(KindAnnualOutstanding, ToEmployee, "Leave: annual leave not yet planned (employee)",
            "Sent once a leave year, from the chase month, to each employee with annual leave not yet planned or taken.",
            "Plan your annual leave",
            "You have {{Outstanding}} day(s) of {{LeaveTypeName}} not yet planned or taken this leave year, which ends on {{DueDate}}. Plan them under My Leave, in the leave planner.",
            SubjectEmployee),
        new(KindAnnualOutstanding, ToManager, "Leave: annual leave not yet planned (supervisor)",
            "Sent to a supervisor once per run, naming the people who report to them with annual leave not yet planned or taken.",
            "Annual leave not yet planned: {{Count}} of your team",
            "{{Count}} of the people who report to you have annual leave not yet planned or taken this leave year, which ends on {{DueDate}}: {{People}}.",
            new[] { ("UserFromEmployeeIdData", "ManagerEmployeeId") }),
        new(KindAnnualOutstanding, ToHrDigest, "Leave: annual leave not yet planned (HR summary)",
            "Sent to HR once per run, counting the employees the chase reached in that run.",
            "Annual leave not yet planned or taken: {{Count}} employee(s)",
            "{{Count}} employee(s) have annual leave not yet planned or taken this leave year, which ends on {{DueDate}}. {{Reach}} The leave balances page lists everybody's annual leave.",
            HrRole),
        new(KindCarryOverExpiring, ToEmployee, "Leave: carried days about to lapse (employee)",
            "Sent to the employee when carried-over days lapse soon and leave taken or booked in time does not cover them.",
            "Carried-over leave about to lapse",
            "{{AtRisk}} carried-over day(s) of {{LeaveTypeName}} can be used only until {{DueDate}}. Book them under My Leave, or they lapse.",
            SubjectEmployee),
        new(KindCarryOverExpiring, ToHr, "Leave: carried days about to lapse, employee not reachable (HR)",
            "Sent to HR when carried-over days lapse soon and the employee has no login to be told.",
            "Carried-over leave about to lapse: {{EmployeeName}}",
            "{{EmployeeName}}: {{AtRisk}} carried-over day(s) of {{LeaveTypeName}} can be used only until {{DueDate}}. {{Why}}",
            HrRole),
        new(KindAnnualAvailable, ToEmployee, "Leave: annual leave now available (employee)",
            "Sent once, when an employee has served the qualifying period for annual leave.",
            "You can now take annual leave",
            "You have served the qualifying period for {{LeaveTypeName}}: from {{DueDate}} you can plan it and take it. Your days are under My Leave.",
            SubjectEmployee),
        new(KindAnnualAvailable, ToHrDigest, "Leave: annual leave now available (HR summary)",
            "Sent to HR once per run, counting the employees who have just qualified for annual leave.",
            "Now able to take annual leave: {{Count}} employee(s)",
            "{{Count}} employee(s) have served the qualifying period and can now take annual leave. {{Reach}}",
            HrRole),
        new(KindRecharged, ToEmployee, "Leave: recounted after a closure or holiday changed (employee)",
            "Sent to the employee when their granted leave is recounted because a business closure or public holiday under it was added, moved or removed.",
            "Your leave was recounted: {{Reference}}",
            "{{Reference}} now counts {{NewDays}} day(s) instead of {{OldDays}}, because {{Because}}. Your balance has been updated to match.",
            SubjectEmployee),
        new(KindRecharged, ToHr, "Leave: recounted, employee not reachable (HR)",
            "Sent to HR when an employee's granted leave is recounted and the employee has no login to be told.",
            "Leave recounted: {{EmployeeName}}",
            "{{EmployeeName}}'s leave {{Reference}} now counts {{NewDays}} day(s) instead of {{OldDays}}, because {{Because}}. {{Why}}",
            HrRole),
    };

    /// <summary>The two topics every leave reminder used to go to: the HR role only, in the app only.</summary>
    private static readonly string[] LegacyTopicKeys =
    {
        $"{TopicEntityType}.DueSoon.Internal",
        $"{TopicEntityType}.Overdue.Internal",
    };

    private async Task EnsureTopicsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var keys = TopicSeeds.Select(s => $"{TopicEntityType}.{s.Kind}.{s.Audience}").ToArray();

        var existing = await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && keys.Contains(t.Key))
            .Select(t => t.Key)
            .ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        // ⚠ Round 5, lane I: nothing publishes to the old pair any more, so they are switched off
        // rather than left looking live on the Notification Topics screen.
        var legacy = await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && t.IsSystem && t.IsActive
                            && LegacyTopicKeys.Contains(t.Key))
            .ToListAsync(cancellationToken);

        if (existingSet.Count == TopicSeeds.Length && legacy.Count == 0) return;

        foreach (var old in legacy)
        {
            old.IsActive = false;
            old.Description = "Replaced by one topic per leave reminder and audience (round 5, lane I). Nothing publishes here any more.";
            await topicRepo.UpdateAsync(old);
        }

        var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
        foreach (var seed in TopicSeeds)
        {
            var key = $"{TopicEntityType}.{seed.Kind}.{seed.Audience}";
            if (existingSet.Contains(key)) continue;

            var topic = new NotificationTopic
            {
                TenantId = tenantId, Key = key, Name = seed.Name, Description = seed.Description,
                EntityType = TopicEntityType, IsSystem = true, IsActive = true,
                EnableInApp = true, EnableEmail = true, EnableSms = false,
                InAppTitleTemplate = seed.TitleTemplate,
                InAppBodyTemplate = seed.BodyTemplate,
                ActionUrlTemplate = "{{ActionPath}}",
                CreatedBy = "System",
            };
            await topicRepo.AddAsync(topic);

            foreach (var (kind, value) in seed.Recipients)
            {
                await recipientRepo.AddAsync(new NotificationTopicRecipient
                {
                    TenantId = tenantId, TopicId = topic.Id,
                    RecipientKind = kind, RecipientValue = value,
                    IsSystem = true, SendInApp = true, SendEmail = true, CreatedBy = "System",
                });
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---- candidates --------------------------------------------------------

    private static readonly IReadOnlyDictionary<string, object> NoData = new Dictionary<string, object>();

    /// <summary>
    /// One message about an item: the audience it is for, the page it opens on, and the data that
    /// audience's recipient rule and words read.
    /// </summary>
    private sealed record Delivery(string Audience, string ActionPath, IReadOnlyDictionary<string, object> Data)
    {
        public Delivery(string audience, string actionPath) : this(audience, actionPath, NoData) { }
    }

    /// <summary>A message to HR, saying why it is HR's (the <c>{{Why}}</c> of the HR topics).</summary>
    private static Delivery ToHrBecause(string actionPath, string why)
        => new(ToHr, actionPath, new Dictionary<string, object> { ["Why"] = why });

    private sealed record Candidate(
        string Kind, string ItemType, Guid EntityId, Guid? EmployeeId, string? EmployeeName,
        string Reference, DateTime? DueDate, int DaysRemaining, int EscalationTier,
        string DedupeKey, IReadOnlyList<Delivery> Deliveries)
    {
        /// <summary>Counted into HR's one summary per run for its kind.</summary>
        public bool DigestForHr { get; init; }

        /// <summary>Named in this supervisor's one message per run (the September chase).</summary>
        public Guid? ManagerEmployeeId { get; init; }

        /// <summary>The figures every message about this item quotes.</summary>
        public IReadOnlyDictionary<string, object> Data { get; init; } = NoData;
    }

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

        // Who can be told at all: employees with an active login. A reminder for anybody else goes
        // to HR, which can pick up the phone — a message to nobody is the defect this lane closes.
        var reachable = new HashSet<Guid>(await _userManager.Users
            .Where(u => u.TenantId == tenantId && u.IsActive && u.EmployeeId != null)
            .Select(u => u.EmployeeId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken));

        // ── 1. Approved leave about to start, and nobody has said whether it is still going ────
        //
        // The other half of the confirm action (R-7). Once somebody answers, the leave stops being
        // chased — which is why ObservanceConfirmedDate is in the filter rather than only on screen.
        // Round 5, lane I: the employee is asked — it is their leave, and My Leave has the button.
        var startHorizon = today.AddDays(policy.LeaveStartingReminderDays);
        var starting = await _unitOfWork.Repository<LeaveRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && r.Status == LeaveStatus.Approved
                            && r.ObservanceConfirmedDate == null
                            && r.StartDate >= today
                            && r.StartDate <= startHorizon
                            // The annual part of a split (round 5, lane H) begins while the employee
                            // is already away on its first part: that part was the one asked about.
                            && r.SplitFromRequestId == null)
            .Select(r => new { r.Id, r.RequestNumber, r.EmployeeId, r.StartDate, LeaveTypeName = r.LeaveType!.Name })
            .ToListAsync(cancellationToken);

        foreach (var r in starting)
        {
            var days = r.StartDate.DayNumber - today.DayNumber;
            var delivery = reachable.Contains(r.EmployeeId)
                ? new Delivery(ToEmployee, $"/me/leave/{r.Id}")
                : ToHrBecause($"/hr/leave/requests/{r.Id}",
                    "They have no login, so they could not be asked whether it is still going ahead.");
            results.Add(new Candidate(
                KindStartingSoon, "Leave request", r.Id, r.EmployeeId, null,
                $"{r.RequestNumber} · {r.LeaveTypeName}",
                r.StartDate.ToDateTime(TimeOnly.MinValue), days, 0,
                $"{KindStartingSoon}:{r.Id}:{r.StartDate:yyyy-MM-dd}:0",
                new[] { delivery }));
        }

        // ── 2. Leave that ended and was never closed ──────────────────────────────────────────
        //
        // `Close` existed, refused before the end date, and nobody was ever prompted to use it — so
        // approved leave stayed approved for ever (R-10). This is the prompt.
        var closureDue = today.AddDays(-policy.LeaveClosureGraceDays);
        // ⚠ Round 5, lane H: a request whose absence goes on as annual leave is not over when its
        // own end date passes. It is closed with its annual part, so chasing it would ask for a
        // confirmation the service routes to the part still running.
        var splitRequests = _unitOfWork.Repository<LeaveRequest>().GetQueryable();
        var unclosed = await _unitOfWork.Repository<LeaveRequest>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted
                            && (r.Status == LeaveStatus.Approved || r.Status == LeaveStatus.InProgress)
                            && r.ClosureDate == null
                            && r.EndDate <= closureDue
                            && r.EndDate >= backlogFloor
                            && !splitRequests.Any(a => a.SplitFromRequestId == r.Id && !a.IsDeleted
                                                    && (a.Status == LeaveStatus.Approved || a.Status == LeaveStatus.InProgress)
                                                    && a.ClosureDate == null))
            .Select(r => new
            {
                r.Id, r.RequestNumber, r.EmployeeId, r.EndDate, LeaveTypeName = r.LeaveType!.Name,
                Reported = r.ResumptionReportedDate != null,
                r.Employee.ManagerId, r.Employee.OrganizationUnitId,
            })
            .ToListAsync(cancellationToken);

        // Round 5, lane I: a reported return is the line manager's to confirm — lane D's rule, which
        // lets the supervisor or any head of unit above confirm; the reminder asks the NEAREST. With
        // no return reported, or no line manager who can be told, it is HR's.
        var units = unclosed.Any(r => r.Reported)
            ? await UnitEdgesAsync(tenantId, cancellationToken)
            : new Dictionary<Guid, UnitEdge>();

        foreach (var r in unclosed)
        {
            var days = r.EndDate.DayNumber - today.DayNumber;
            var tier = TierFor(days);
            var path = $"/hr/leave/requests/{r.Id}";
            Delivery delivery;
            if (!r.Reported)
                delivery = ToHrBecause(path, "No return has been reported.");
            else if (NearestLineAuthority(r.EmployeeId, r.ManagerId, r.OrganizationUnitId, units) is Guid confirmer
                     && reachable.Contains(confirmer))
                delivery = new Delivery(ToConfirmer, path,
                    new Dictionary<string, object> { ["ConfirmerEmployeeId"] = confirmer });
            else
                delivery = ToHrBecause(path,
                    "The return has been reported, and there is no line manager with a login to confirm it.");

            results.Add(new Candidate(
                KindNotClosed, "Leave request", r.Id, r.EmployeeId, null,
                $"{r.RequestNumber} · {r.LeaveTypeName}",
                r.EndDate.ToDateTime(TimeOnly.MinValue), days, tier,
                $"{KindNotClosed}:{r.Id}:{r.EndDate:yyyy-MM-dd}:{tier}",
                new[] { delivery }));
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

        var asked = await ApproversAskedAsync(
            tenantId, undecided.Where(r => r.Status == LeaveStatus.Pending).Select(r => r.Id).ToList(),
            cancellationToken);

        foreach (var r in undecided)
        {
            var waiting = (int)(at - r.RequestDate).TotalDays;
            var days = -waiting;
            var tier = TierFor(days);
            var path = $"/hr/leave/requests/{r.Id}";
            // ChangesSuggested is waiting on the EMPLOYEE, not the approver — same chase, different
            // person, and the item type is what says which.
            var item = r.Status == LeaveStatus.ChangesSuggested
                ? "Leave request awaiting the employee"
                : "Leave request awaiting a decision";

            Delivery delivery;
            if (r.Status == LeaveStatus.ChangesSuggested)
            {
                delivery = reachable.Contains(r.EmployeeId)
                    ? new Delivery(ToEmployee, $"/me/leave/{r.Id}")
                    : ToHrBecause(path, "It waits on their answer to suggested dates, and they have no login to be told.");
            }
            else
            {
                // Never the employee whose leave it is: they cannot decide it, and the approvals queue
                // never lists it to them either.
                var approvers = asked.TryGetValue(r.Id, out var users)
                    ? users.Where(u => u.EmployeeId != r.EmployeeId).Select(u => u.UserId).Distinct().ToList()
                    : new List<Guid>();
                delivery = approvers.Count > 0
                    ? new Delivery(ToApprover, path, new Dictionary<string, object> { ["ApproverUserIds"] = approvers })
                    : ToHrBecause(path, "No approver has been asked for it, so it falls to HR.");
            }

            results.Add(new Candidate(
                KindAwaitingDecision, item, r.Id, r.EmployeeId, null,
                $"{r.RequestNumber} · {r.LeaveTypeName}",
                r.RequestDate, days, tier,
                $"{KindAwaitingDecision}:{r.Id}:{r.RequestDate:yyyy-MM-dd}:{tier}",
                new[] { delivery }));
        }

        // ── 4. Annual leave not yet planned or taken, late in the leave year (decision B6) ──────
        //
        // Reads the tenant's Annual leave type (round 5, A4: the kind replaced the
        // MandatoryAnnualLeave flag). Chased from month MandatoryLeaveChaseFromMonth OF THE LEAVE
        // YEAR (round 5, lane C4 — the calendar month is the same thing only for a January start),
        // once per employee per leave year.
        var leaveYear = LeaveYear.For(today, policy.LeaveYearStartMonth);
        var leaveYearStart = LeaveYear.StartOf(leaveYear, policy.LeaveYearStartMonth);
        var leaveYearEnd = LeaveYear.EndOf(leaveYear, policy.LeaveYearStartMonth);
        var annual = await _unitOfWork.Repository<LeaveType>()
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive && t.Category == LeaveTypeCategory.Annual)
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (annual is not null && LeaveYear.MonthOf(today, policy.LeaveYearStartMonth) >= policy.MandatoryLeaveChaseFromMonth)
        {
            results.AddRange(await AnnualLeaveOutstandingAsync(
                tenantId, policy.LeaveYearStartMonth, annual, leaveYear, leaveYearStart, leaveYearEnd, today, reachable,
                cancellationToken));
        }

        // ── 5. Carry-over about to expire ─────────────────────────────────────────────────────
        //
        // ⚠ This only WARNS. The balance is moved by LeaveYearEndService, which is deliberately not
        // scheduled — see this class's remarks. Round 5, lane I: the employee is told; the days are
        // theirs to take.
        // ⚠ The leave year we are IN, which is not today's calendar year once a tenant starts
        // its leave year anywhere but January.
        var carryOver = await _unitOfWork.Repository<LeaveBalance>()
            .GetQueryable(b => b.TenantId == tenantId && !b.IsDeleted
                            && b.Year == leaveYear
                            && b.CarriedOverDays > 0
                            && b.LeaveType!.AllowCarryOver
                            && b.LeaveType.CarryOverExpiryMonths != null)
            .Select(b => new
            {
                b.Id, b.EmployeeId, b.LeaveTypeId, b.CarriedOverDays,
                LeaveTypeName = b.LeaveType!.Name,
                ExpiryMonths = b.LeaveType.CarryOverExpiryMonths!.Value,
            })
            .ToListAsync(cancellationToken);

        // "Usable only in the first three months" for ExpiryMonths = 3 means it lapses at the END of
        // the third month OF THE LEAVE YEAR — which is March only when the leave year starts in
        // January. `expiry` is that last usable day.
        var inWindow = carryOver
            .Select(b => new { b, expiry = leaveYearStart.AddMonths(b.ExpiryMonths).AddDays(-1) })
            .Select(x => new { x.b, x.expiry, days = x.expiry.DayNumber - today.DayNumber })
            .Where(x => x.days <= policy.LeaveCarryOverExpiryReminderDays && x.days >= -BacklogHorizonDays)
            .ToList();

        // ⚠ Round 5, lane G: what is at risk is the carried days NOT covered by leave taken or booked
        // on or before the last usable day — carried days are used first. It used to compare the
        // whole year's used days, so leave booked for after the lapse hid the warning while the days
        // lapsed anyway. The year-end run and the leave owed report read the same reader, so this
        // warns about exactly the days the run will remove.
        foreach (var group in inWindow.GroupBy(x => (x.b.LeaveTypeId, x.expiry)))
        {
            var usage = await _usage.ReadAsync(
                tenantId, group.Key.LeaveTypeId, leaveYearStart, leaveYearEnd, group.Key.expiry,
                group.Select(x => x.b.EmployeeId).Distinct().ToList(), cancellationToken);

            foreach (var x in group)
            {
                var usedInTime = usage.TryGetValue(x.b.EmployeeId, out var u) ? u.TakenThrough : 0m;
                var atRisk = x.b.CarriedOverDays - Math.Min(x.b.CarriedOverDays, usedInTime);

                // Covered by leave in time, so there is nothing left to lose.
                if (atRisk <= 0) continue;

                var tier = TierFor(x.days);
                var delivery = reachable.Contains(x.b.EmployeeId)
                    ? new Delivery(ToEmployee, "/me/leave")
                    : ToHrBecause("/hr/leave/balances", "They have no login, so they could not be told directly.");
                results.Add(new Candidate(
                    KindCarryOverExpiring, "Carry-over", x.b.Id, x.b.EmployeeId, null,
                    $"{x.b.LeaveTypeName} · {atRisk:0.##} carried day(s) lapse on {x.expiry:d MMM}",
                    x.expiry.ToDateTime(TimeOnly.MinValue), x.days, tier,
                    $"{KindCarryOverExpiring}:{x.b.Id}:{x.expiry:yyyy-MM-dd}:{tier}",
                    new[] { delivery })
                {
                    Data = new Dictionary<string, object>
                    {
                        ["AtRisk"] = atRisk.ToString("0.##", CultureInfo.InvariantCulture),
                        ["LeaveTypeName"] = x.b.LeaveTypeName,
                    },
                });
            }
        }

        // ── 6. "You can now take annual leave" (decision B6, round 5, lane I) ─────────────────
        //
        // Once, on the day an employee still serving has served annual leave's qualifying period —
        // the day the entitlement service's AccessibleFrom names. A day the sweep did not run is
        // caught up within the backlog window; the key is the date, so it never repeats.
        if (annual?.MinServiceMonthsToAccess is int gateMonths && gateMonths > 0)
        {
            var windowStart = today.AddDays(-BacklogHorizonDays);
            // AddMonths clamps month ends, so the hire range is widened by a few days and the exact
            // qualifying date is checked below.
            var hiredFrom = windowStart.AddMonths(-gateMonths).AddDays(-3);
            var hiredTo = today.AddMonths(-gateMonths).AddDays(3);
            var joiners = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Employee>()
                .GetQueryable()
                .Where(HrServingEmployees.Predicate)
                .Where(e => e.TenantId == tenantId && e.DateEmployed != null
                         && e.DateEmployed >= hiredFrom && e.DateEmployed <= hiredTo)
                .Select(e => new { e.Id, Hired = e.DateEmployed!.Value })
                .ToListAsync(cancellationToken);

            foreach (var e in joiners)
            {
                var from = e.Hired.AddMonths(gateMonths);
                if (from > today || from < windowStart) continue;

                results.Add(new Candidate(
                    KindAnnualAvailable, "Annual leave", e.Id, e.Id, null,
                    $"{annual.Name} · may be taken from {from:d MMM yyyy}",
                    from.ToDateTime(TimeOnly.MinValue), from.DayNumber - today.DayNumber, 0,
                    $"{KindAnnualAvailable}:{e.Id}:{from:yyyy-MM-dd}:0",
                    reachable.Contains(e.Id) ? new[] { new Delivery(ToEmployee, "/me/leave") } : Array.Empty<Delivery>())
                {
                    DigestForHr = true,
                    Data = new Dictionary<string, object> { ["LeaveTypeName"] = annual.Name },
                });
            }
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

    /// <summary>
    /// Sweep 4 (decision B6): everybody still serving who may take annual leave and has days of it
    /// not yet planned or taken this leave year.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Everybody, not only those with a balance record</b> (round 5, lane I). The old sweep
    /// read the records, and most people have none until their first request opens one — exactly the
    /// people this chase is for: on UAT, 97 records against 2,377 people serving. Where there is no
    /// record the year's days are worked out live, as the balances page's annual view does (lane J),
    /// and nothing is created.</para>
    ///
    /// <para><b>"Not yet planned or taken"</b> is the year's available days — the record's, which
    /// already takes off what is used, requested and cashed in; or the entitlement where there is no
    /// record — less the days of annual leave plans submitted, approved or sent back with other dates
    /// and not yet raised as a request (a raised plan's days are in the request's figures already).
    /// Plan days are counted as a request for those dates would charge them.</para>
    ///
    /// <para>Anybody still inside the qualifying period is skipped: they cannot plan it yet, and
    /// sweep 6 tells them the day they can.</para>
    /// </remarks>
    private async Task<IEnumerable<Candidate>> AnnualLeaveOutstandingAsync(
        Guid tenantId, int leaveYearStartMonth, LeaveType annual, int year, DateOnly yearStart, DateOnly yearEnd, DateOnly today,
        IReadOnlySet<Guid> reachable, CancellationToken cancellationToken)
    {
        var people = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Employee>()
            .GetQueryable()
            .Where(HrServingEmployees.Predicate)
            .Where(e => e.TenantId == tenantId && (e.DateEmployed == null || e.DateEmployed <= yearEnd))
            .Select(e => new
            {
                e.Id, e.ManagerId, e.DateEmployed, e.TerminationDate,
                StaffLevelId = e.Position != null ? e.Position.StaffLevelId : null,
            })
            .ToListAsync(cancellationToken);
        if (people.Count == 0) return Array.Empty<Candidate>();

        // One row per employee and type is the rule; should a stray second one exist, the type-level
        // row is the balance (lane G's one pot per type) — as the balances page reads it.
        var records = (await _unitOfWork.Repository<LeaveBalance>()
                .GetQueryable(b => b.TenantId == tenantId && !b.IsDeleted && b.LeaveTypeId == annual.Id && b.Year == year)
                .ToListAsync(cancellationToken))
            .GroupBy(b => b.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.LeaveSubTypeId != null).ThenBy(b => b.CreatedAt).First());

        // ⚠ ForTenant: the nightly host has no signed-in user, and the plain batch read takes its
        // tenant and leave year from one — the first scheduled run on UAT threw here while every
        // manual run passed.
        var snapshots = await _entitlement.GetSnapshotsForTenantAsync(
            tenantId, leaveYearStartMonth,
            people.Select(p => new LeaveAccrualSubject(
                p.Id, p.DateEmployed,
                p.TerminationDate is DateTime left ? DateOnly.FromDateTime(left) : null,
                p.StaffLevelId)).ToList(),
            annual.Id, year, today, cancellationToken);

        var requests = _unitOfWork.Repository<LeaveRequest>().GetQueryable();
        var plans = await _unitOfWork.Repository<LeavePlan>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted
                            && p.LeaveTypeId == annual.Id && p.Year == year
                            && (p.Status == LeavePlanStatus.Submitted
                                || p.Status == LeavePlanStatus.Approved
                                || p.Status == LeavePlanStatus.ChangesSuggested)
                            && !requests.Any(r => r.LeavePlanId == p.Id && !r.IsDeleted
                                               && r.Status != LeaveStatus.Cancelled
                                               && r.Status != LeaveStatus.Rejected))
            .Select(p => new { p.EmployeeId, p.StartDate, p.EndDate })
            .ToListAsync(cancellationToken);

        var planned = new Dictionary<Guid, decimal>();
        if (plans.Count > 0)
        {
            var holidays = await _workingDays.GetHolidayDatesAsync(tenantId, yearStart, yearEnd, cancellationToken);
            // Each planner's own site and unit closures too (company-schedule final closure, lane 1b):
            // the plan will be charged as leave is, and a closure of their site is not a day of leave.
            var ownClosures = await _closures.GetClosureDatesAsync(
                tenantId, plans.Select(p => p.EmployeeId).Distinct().ToList(), yearStart, yearEnd, cancellationToken);
            foreach (var p in plans)
            {
                var daysOff = ownClosures.TryGetValue(p.EmployeeId, out var own) && own.Count > 0
                    ? new HashSet<DateOnly>(holidays.Concat(own))
                    : holidays;
                planned[p.EmployeeId] = planned.GetValueOrDefault(p.EmployeeId)
                    + LeaveChargeableDays.Between(p.StartDate, p.EndDate, annual, daysOff).Count;
            }
        }

        var results = new List<Candidate>();
        foreach (var person in people)
        {
            if (!snapshots.TryGetValue(person.Id, out var snapshot) || !snapshot.IsAccessible) continue;

            records.TryGetValue(person.Id, out var record);
            var outstanding = (record?.AvailableDays ?? snapshot.AnnualEntitledDays)
                              - planned.GetValueOrDefault(person.Id);
            if (outstanding <= 0) continue;

            // The supervisor, named in their one message per run. Heads of unit are not copied:
            // "My team" — where the message leads — lists direct reports, and a head's message would
            // name everybody in their unit.
            Guid? manager = person.ManagerId is Guid m && m != person.Id && reachable.Contains(m) ? m : null;

            results.Add(new Candidate(
                KindAnnualOutstanding, "Annual leave", record?.Id ?? person.Id, person.Id, null,
                $"{annual.Name} · {outstanding:0.##} day(s) not yet planned or taken",
                yearEnd.ToDateTime(TimeOnly.MinValue), yearEnd.DayNumber - today.DayNumber, 0,
                // The employee and the year, not a date or a record, so this fires once per employee
                // per leave year — record or none.
                $"{KindAnnualOutstanding}:{person.Id}:{year}:0",
                reachable.Contains(person.Id)
                    ? new[] { new Delivery(ToEmployee, "/me/leave/planner") }
                    : Array.Empty<Delivery>())
            {
                DigestForHr = true,
                ManagerEmployeeId = manager,
                Data = new Dictionary<string, object>
                {
                    ["LeaveTypeName"] = annual.Name,
                    ["Outstanding"] = outstanding.ToString("0.##", CultureInfo.InvariantCulture),
                },
            });
        }

        return results;
    }

    // ---- who is being asked -------------------------------------------------

    /// <summary>
    /// For each waiting request, the users its current approval step is asking: the pending
    /// approvals of the step the instance is on, in their lowest open group — a named user, or
    /// everyone holding a role. The same set the engine lets decide (<c>CanUserApproveAsync</c>) and
    /// its own "approval required" notice reaches.
    /// </summary>
    private async Task<Dictionary<Guid, List<(Guid UserId, Guid? EmployeeId)>>> ApproversAskedAsync(
        Guid tenantId, List<Guid> requestIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<(Guid UserId, Guid? EmployeeId)>>();
        if (requestIds.Count == 0) return result;

        var rows = await _unitOfWork.Repository<WorkflowApproval>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted
                            && a.Status == WorkflowApprovalStatus.Pending
                            && !a.StepInstance.IsDeleted
                            && (a.StepInstance.Status == WorkflowStepInstanceStatus.Pending
                                || a.StepInstance.Status == WorkflowStepInstanceStatus.InProgress)
                            && !a.StepInstance.WorkflowInstance.IsDeleted
                            && (a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.Created
                                || a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.InProgress
                                || a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.Waiting
                                || a.StepInstance.WorkflowInstance.Status == WorkflowInstanceStatus.Suspended)
                            && requestIds.Contains(a.StepInstance.WorkflowInstance.EntityId))
            .Select(a => new
            {
                RequestId = a.StepInstance.WorkflowInstance.EntityId,
                a.StepInstanceId,
                OnCurrentStep = a.StepInstance.WorkflowInstance.CurrentStepId == a.StepInstance.WorkflowStepId,
                StepCreatedAt = a.StepInstance.CreatedAt,
                a.ApproverId, a.ApproverRole, a.ApprovalGroup,
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return result;

        // The step the engine calls current: the one the instance points at, else the newest open
        // one (WorkflowStepInstanceRepository.GetCurrentStepAsync). Then its lowest open group.
        var current = rows
            .GroupBy(r => r.RequestId)
            .Select(g =>
            {
                var step = g.OrderByDescending(r => r.OnCurrentStep).ThenByDescending(r => r.StepCreatedAt).First().StepInstanceId;
                var onStep = g.Where(r => r.StepInstanceId == step).ToList();
                var group = onStep.Min(r => r.ApprovalGroup);
                return (RequestId: g.Key, Rows: onStep.Where(r => r.ApprovalGroup == group).ToList());
            })
            .ToList();

        var roleHolders = new Dictionary<string, List<(Guid UserId, Guid? EmployeeId)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in current.SelectMany(c => c.Rows).Select(r => r.ApproverRole)
                     .Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r!.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            roleHolders[role] = (await _userManager.GetUsersInRoleAsync(role))
                .Where(u => u.IsActive && u.TenantId == tenantId)
                .Select(u => (u.Id, u.EmployeeId))
                .ToList();
        }

        var namedIds = current.SelectMany(c => c.Rows)
            .Where(r => r.ApproverId is Guid id && id != Guid.Empty)
            .Select(r => r.ApproverId!.Value)
            .Distinct()
            .ToList();
        var named = namedIds.Count == 0
            ? new Dictionary<Guid, Guid?>()
            : (await _userManager.Users
                    .Where(u => namedIds.Contains(u.Id) && u.IsActive && u.TenantId == tenantId)
                    .Select(u => new { u.Id, u.EmployeeId })
                    .ToListAsync(cancellationToken))
                .ToDictionary(u => u.Id, u => u.EmployeeId);

        foreach (var (requestId, stepRows) in current)
        {
            var users = new List<(Guid UserId, Guid? EmployeeId)>();
            foreach (var row in stepRows)
            {
                if (row.ApproverId is Guid userId && named.TryGetValue(userId, out var employeeId))
                    users.Add((userId, employeeId));
                if (!string.IsNullOrWhiteSpace(row.ApproverRole) && roleHolders.TryGetValue(row.ApproverRole.Trim(), out var holders))
                    users.AddRange(holders);
            }
            result[requestId] = users;
        }

        return result;
    }

    // ---- line authority -----------------------------------------------------

    private sealed record UnitEdge(Guid? ParentUnitId, Guid? HeadEmployeeId);

    private async Task<Dictionary<Guid, UnitEdge>> UnitEdgesAsync(Guid tenantId, CancellationToken cancellationToken)
        => (await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.OrganizationUnit>()
                .GetQueryable(u => u.TenantId == tenantId && !u.IsDeleted)
                .Select(u => new { u.Id, u.ParentUnitId, u.HeadEmployeeId })
                .ToListAsync(cancellationToken))
            .ToDictionary(u => u.Id, u => new UnitEdge(u.ParentUnitId, u.HeadEmployeeId));

    /// <summary>
    /// The nearest person with line authority over an employee: their supervisor, or failing one
    /// the head of their unit, or of the nearest unit above it that has a head. Nobody is their own.
    /// </summary>
    /// <remarks>
    /// Lane D's rule (<c>LeaveService.IsLineAuthorityAsync</c>) lets all of them confirm a return;
    /// a reminder asks the first. Cycle-guarded, as the audience resolver's walk is.
    /// </remarks>
    private static Guid? NearestLineAuthority(
        Guid employeeId, Guid? managerId, Guid? unitId, IReadOnlyDictionary<Guid, UnitEdge> units)
    {
        if (managerId is Guid manager && manager != employeeId) return manager;

        var seen = new HashSet<Guid>();
        var current = unitId;
        while (current is Guid id && seen.Add(id) && units.TryGetValue(id, out var unit))
        {
            if (unit.HeadEmployeeId is Guid head && head != employeeId) return head;
            current = unit.ParentUnitId;
        }
        return null;
    }
}
