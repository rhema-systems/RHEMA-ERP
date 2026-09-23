using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The fixed windows the orientation triggers run on (round 4, lane I). Constants, not settings:
/// they define what the triggers MEAN, and a tenant changing them would change which past events
/// count — see <see cref="OrientationEnrollmentTriggerService"/>.
/// </summary>
public static class OrientationTriggerWindows
{
    /// <summary>
    /// How long after joining a person counts as a new hire, for the NewHires population. Ninety
    /// days is the probation length most offers here carry.
    /// </summary>
    public const int NewHireWindowDays = 90;

    /// <summary>
    /// How long a dated trigger stays live after its day comes. A rule fires on the first
    /// evaluation on or after <c>date + delay</c>, and not more than this many days after it.
    /// </summary>
    /// <remarks>
    /// This is what stops a new rule — or an import of the existing workforce — from enrolling
    /// everyone ever hired or transferred, while still catching the hire whose hook failed, the
    /// rule added a week after somebody joined, and the movement implemented ahead of its
    /// effective date.
    /// </remarks>
    public const int CatchUpDays = 30;

    /// <summary>Employment types the Contractors population means.</summary>
    public static readonly EmploymentType[] ContractorTypes =
    [
        EmploymentType.Contract,
        EmploymentType.FixedTerm,
        EmploymentType.Consultant,
        EmploymentType.Freelance,
    ];

    /// <summary>
    /// Which trigger an implemented movement is. A lateral move and a secondment put a person in a
    /// new place, which is what a transfer orientation exists for; a demotion, an acting appointment
    /// and a redesignation fire nothing.
    /// </summary>
    public static OrientationEnrollmentTrigger? TriggerFor(StaffMovementType type) => type switch
    {
        StaffMovementType.Promotion => OrientationEnrollmentTrigger.OnPromotion,
        StaffMovementType.Transfer or StaffMovementType.LateralMove or StaffMovementType.Secondment
            => OrientationEnrollmentTrigger.OnTransfer,
        _ => null,
    };

    public static readonly StaffMovementType[] TriggeringMovementTypes =
    [
        StaffMovementType.Promotion,
        StaffMovementType.Transfer,
        StaffMovementType.LateralMove,
        StaffMovementType.Secondment,
    ];

    /// <summary>Hire, transfer and promotion have a date to count a delay from; the rest do not.</summary>
    public static bool IsDated(OrientationEnrollmentTrigger trigger) =>
        trigger is OrientationEnrollmentTrigger.OnHire
            or OrientationEnrollmentTrigger.OnTransfer
            or OrientationEnrollmentTrigger.OnPromotion;

    public static DateOnly FiresFrom(DateOnly triggerDate, int delayDays) => triggerDate.AddDays(Math.Max(0, delayDays));

    public static DateOnly FiresUntil(DateOnly triggerDate, int delayDays) => FiresFrom(triggerDate, delayDays).AddDays(CatchUpDays);

    public static bool IsDue(DateOnly triggerDate, int delayDays, DateOnly today) =>
        today >= FiresFrom(triggerDate, delayDays) && today <= FiresUntil(triggerDate, delayDays);

    // ── Recurrence (round 4, lane I-b) ────────────────────────────────────────────────────────

    public static int MonthsOf(OrientationRecurrenceFrequency frequency) => frequency switch
    {
        OrientationRecurrenceFrequency.Monthly => 1,
        OrientationRecurrenceFrequency.Quarterly => 3,
        OrientationRecurrenceFrequency.SemiAnnually => 6,
        OrientationRecurrenceFrequency.Annually => 12,
        OrientationRecurrenceFrequency.Biennially => 24,
        _ => 12,
    };

    /// <summary>The day the next cycle is due to be COMPLETED: one period after the last completion.</summary>
    public static DateOnly Anniversary(DateOnly completedOn, OrientationRecurrenceFrequency frequency) =>
        completedOn.AddMonths(MonthsOf(frequency));

    /// <summary>
    /// The day the next cycle OPENS: the anniversary less the programme's completion deadline, so
    /// that the ordinary due date (enrolment + deadline) falls on the anniversary itself. An annual
    /// programme with a 14-day deadline reopens 351 days after completion and is due on day 365.
    /// Never before the day after the completion it renews.
    /// </summary>
    public static DateOnly OpensOn(DateOnly completedOn, OrientationRecurrenceFrequency frequency, int? deadlineDays)
    {
        var opens = Anniversary(completedOn, frequency).AddDays(-Math.Max(0, deadlineDays ?? 0));
        var earliest = completedOn.AddDays(1);
        return opens < earliest ? earliest : opens;
    }

    public static string Describe(OrientationRecurrenceFrequency frequency) => frequency switch
    {
        OrientationRecurrenceFrequency.Monthly => "monthly",
        OrientationRecurrenceFrequency.Quarterly => "quarterly",
        OrientationRecurrenceFrequency.SemiAnnually => "every six months",
        OrientationRecurrenceFrequency.Annually => "annually",
        OrientationRecurrenceFrequency.Biennially => "every two years",
        _ => frequency.ToString(),
    };

    // ── The effective window (round 4, lane I-b) ──────────────────────────────────────────────

    /// <summary>
    /// Whether a programme is in effect on a day: its effective-from has come and its effective-to
    /// has not passed (both inclusive, both optional). Lane I shipped without this — the dates were
    /// stored and read by nothing, so a programme whose effective period had ended went on
    /// enrolling people for as long as it stayed Active.
    /// </summary>
    public static bool IsInEffect(DateTime? effectiveFrom, DateTime? effectiveTo, DateOnly today) =>
        (effectiveFrom is not { } starts || DateOnly.FromDateTime(starts) <= today)
        && (effectiveTo is not { } ends || DateOnly.FromDateTime(ends) >= today);
}

/// <summary>
/// Orientation audience rules, made to fire (round 4, lane I3) and made to explain themselves (I5).
/// </summary>
/// <remarks>
/// <para><b>What fires what.</b></para>
/// <list type="bullet">
///   <item><b>OnHire</b> — an employee is created (the form, the import, a confirmed hire). Counts
///   from the employment date, and from nothing else: a person with no employment date has no hire
///   date, and no hire rule fires for them until one is set. The record's creation date was tried
///   as a stand-in and rejected — on UAT every one of the 221 active employees without a date was a
///   harness fixture created in the last three days, and a seeded or imported record is created
///   nowhere near the day anybody joined.</item>
///   <item><b>OnTransfer / OnPromotion</b> — a movement is implemented (transfer, lateral move and
///   secondment are transfers). Counts from the movement's effective date.</item>
///   <item><b>OnProgramPublish</b> — the programme becomes Active (or, if published early, comes into effect).</item>
///   <item><b>Scheduled</b> — the nightly sweep: everyone the rule reaches who is not yet on it.</item>
///   <item><b>Manual</b> — only HR's "enrol the audience now" on the programme, which also re-runs
///   the publish and scheduled rules.</item>
/// </list>
///
/// <para><b>Dated triggers fire within a window</b> — on or after the date plus the rule's delay,
/// and no more than <see cref="OrientationTriggerWindows.CatchUpDays"/> later. The event hook
/// fires the rules whose day has already come; the sweep fires the rest when theirs does.</para>
///
/// <para><b>A programme's rules are evaluated together.</b> The inclusive rules of the firing
/// trigger are unioned; the programme's exclusion rules — whatever trigger they carry — are
/// subtracted. A person reached by several rules is enrolled once, by the rule with the shortest
/// delay, and the enrollment records that rule, the event and its date.</para>
///
/// <para><b>Four things stop an enrollment,</b> and each is counted in the result:</para>
/// <list type="bullet">
///   <item>any earlier enrollment on the programme — <i>including one HR withdrew</i>. A rule must
///   never put back somebody a person took off; that is also what makes a re-run harmless;</item>
///   <item>an exclusion rule;</item>
///   <item>a mandatory prerequisite programme not yet completed (or exempted). A manual enrollment
///   is HR's judgement and is not gated; an automatic one is not a judgement, so it waits. An
///   ADVISORY prerequisite gates nothing — which is why the seeded "all employees, annually"
///   compliance rule (whose onboarding prerequisite the seeder made advisory) enrolled every active
///   employee on its first run on UAT: 1,135 rows. That is the rule doing what it says; a scheduled
///   rule on "everyone" is a tenant-wide enrolment, and the rules tab shows its reach before it is
///   saved;</item>
///   <item>the programme not being Active, or the person not being an active employee.</item>
/// </list>
///
/// <para><b>Recurrence (lane I-b).</b> Rules never enrol anybody twice, so a recurring programme's
/// next cycle is not a rule's to open: <see cref="RenewAsync"/> does it on the nightly sweep, one
/// period after the last completion, for the people the programme is still for. See its remarks.</para>
///
/// <para><b>The effective window (lane I-b).</b> "Active" means Active AND in its effective dates:
/// nothing enrols anybody into a programme outside them — not a trigger, not a renewal, not HR's
/// "enrol audience now". A programme published ahead of its effective-from date runs its publish
/// rules on the nightly sweep once it comes into effect.</para>
/// </remarks>
public sealed class OrientationEnrollmentTriggerService : IOrientationEnrollmentTriggerService
{
    /// <summary>A result lists at most this many enrollments; its counts are always complete.</summary>
    private const int MaxListedEnrolments = 500;

    private const string SystemActor = "system:orientation-triggers";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrAudienceResolver _audienceResolver;
    private readonly IOnboardingTemplateApplicabilityService _onboardingApplicability;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IOnboardingOrientationNotices _notices;
    private readonly ILogger<OrientationEnrollmentTriggerService> _logger;

    public OrientationEnrollmentTriggerService(
        IUnitOfWork unitOfWork,
        IHrAudienceResolver audienceResolver,
        IOnboardingTemplateApplicabilityService onboardingApplicability,
        ICurrentUserProvider currentUserProvider,
        IOnboardingOrientationNotices notices,
        ILogger<OrientationEnrollmentTriggerService> logger)
    {
        _unitOfWork = unitOfWork;
        _audienceResolver = audienceResolver;
        _onboardingApplicability = onboardingApplicability;
        _currentUserProvider = currentUserProvider;
        _notices = notices;
        _logger = logger;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private Guid CurrentTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid? CurrentUserIdOrNull()
    {
        try
        {
            var id = _currentUserProvider.UserId;
            return id == Guid.Empty ? null : id;
        }
        catch
        {
            // A background host has no user; the provider may throw rather than return empty.
            return null;
        }
    }

    // ====================================================================
    // EVENT HOOKS — called after the host's own work has committed
    // ====================================================================

    public async Task<OrientationTriggerRunResultDto> OnEmployeeHiredAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        const string label = "Hire";
        try
        {
            var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
                .Where(e => e.Id == employeeId && !e.IsDeleted)
                .Select(e => new { e.TenantId, e.IsActive, e.DateEmployed })
                .FirstOrDefaultAsync(cancellationToken);
            if (employee is null || !employee.IsActive) return Empty(label);

            // No employment date, no hire date — see the class remarks.
            if (employee.DateEmployed is not { } anchor) return Empty(label);

            var today = Today;
            var programmes = await LoadProgrammesAsync(employee.TenantId, null, activeOnly: true, includeInactiveRules: false, cancellationToken);

            // The cheap exit that makes this safe to call once per row of a 7,000-row import of the
            // existing workforce: an employment date years ago can be due for no rule, so nothing
            // below runs.
            if (!programmes.SelectMany(p => p.Rules).Any(r =>
                    r.IsInclusive && r.Trigger == OrientationEnrollmentTrigger.OnHire
                    && OrientationTriggerWindows.IsDue(anchor, r.EnrollmentDelayDays, today)))
                return Empty(label);

            var run = new RunState(employee.TenantId, today, preview: false, CurrentUserIdOrNull());
            var result = await EvaluateAsync(run, programmes,
                r => r.Trigger == OrientationEnrollmentTrigger.OnHire,
                new Dictionary<Guid, DateOnly?> { [employeeId] = anchor },
                _ => OrientationEnrollmentTrigger.OnHire, label, cancellationToken);
            return await CommitAsync(run, result, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(label, ex, "employee {Id}", employeeId);
        }
    }

    public async Task<OrientationTriggerRunResultDto> OnMovementImplementedAsync(
        Guid movementId, CancellationToken cancellationToken = default)
    {
        const string label = "Movement";
        try
        {
            var movement = await _unitOfWork.Repository<StaffMovement>().GetQueryable()
                .Where(m => m.Id == movementId && !m.IsDeleted)
                .Select(m => new { m.TenantId, m.EmployeeId, m.MovementType, m.EffectiveDate, m.Status })
                .FirstOrDefaultAsync(cancellationToken);
            if (movement is null || movement.Status != StaffMovementStatus.Implemented) return Empty(label);

            if (OrientationTriggerWindows.TriggerFor(movement.MovementType) is not { } trigger) return Empty(label);

            var today = Today;
            var anchor = DateOnly.FromDateTime(movement.EffectiveDate);
            var programmes = await LoadProgrammesAsync(movement.TenantId, null, activeOnly: true, includeInactiveRules: false, cancellationToken);
            if (!programmes.SelectMany(p => p.Rules).Any(r =>
                    r.IsInclusive && r.Trigger == trigger
                    && OrientationTriggerWindows.IsDue(anchor, r.EnrollmentDelayDays, today)))
                return Empty(TriggerLabel(trigger));

            var run = new RunState(movement.TenantId, today, preview: false, CurrentUserIdOrNull());
            var result = await EvaluateAsync(run, programmes,
                r => r.Trigger == trigger,
                new Dictionary<Guid, DateOnly?> { [movement.EmployeeId] = anchor },
                _ => trigger, TriggerLabel(trigger), cancellationToken);
            return await CommitAsync(run, result, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(label, ex, "movement {Id}", movementId);
        }
    }

    public async Task<OrientationTriggerRunResultDto> OnProgramPublishedAsync(
        Guid programId, Guid? actingUserId, CancellationToken cancellationToken = default)
    {
        const string label = "Publish";
        try
        {
            var tenantId = await _unitOfWork.Repository<OrientationProgram>().GetQueryable()
                .Where(p => p.Id == programId && !p.IsDeleted)
                .Select(p => (Guid?)p.TenantId)
                .FirstOrDefaultAsync(cancellationToken);
            if (tenantId is not { } tid) return Empty(label);

            var programmes = await LoadProgrammesAsync(tid, programId, activeOnly: true, includeInactiveRules: false, cancellationToken);
            var run = new RunState(tid, Today, preview: false, actingUserId);
            var result = await EvaluateAsync(run, programmes,
                r => r.Trigger == OrientationEnrollmentTrigger.OnProgramPublish, null,
                _ => OrientationEnrollmentTrigger.OnProgramPublish, label, cancellationToken);
            return await CommitAsync(run, result, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(label, ex, "programme {Id}", programId);
        }
    }

    // ====================================================================
    // HR ACTIONS
    // ====================================================================

    public async Task<OrientationTriggerRunResultDto> EnrolAudienceNowAsync(
        Guid programId, Guid actingUserId, bool preview, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId();

        var programme = await _unitOfWork.Repository<OrientationProgram>().GetQueryable()
            .Where(p => p.Id == programId && p.TenantId == tenantId && !p.IsDeleted)
            .Select(p => new { p.Status, p.EffectiveFrom, p.EffectiveTo })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Orientation program with ID '{programId}' not found.");

        if (programme.Status != OrientationProgramStatus.Active)
            throw new InvalidOperationException(
                "Only an active programme enrols anyone. Publish it first — publishing runs its publish rules by itself.");

        // Lane I-b: outside its effective dates a programme enrols nobody — by hand included.
        var today = Today;
        if (programme.EffectiveFrom is { } starts && DateOnly.FromDateTime(starts) > today)
            throw new InvalidOperationException(
                $"This programme is not in effect until {DateOnly.FromDateTime(starts):d MMM yyyy}. Its audience can be enrolled from then, or change the effective-from date.");
        if (programme.EffectiveTo is { } ends && DateOnly.FromDateTime(ends) < today)
            throw new InvalidOperationException(
                $"This programme's effective period ended on {DateOnly.FromDateTime(ends):d MMM yyyy}, so it enrols nobody. Extend the effective-to date to enrol its audience.");

        var programmes = await LoadProgrammesAsync(tenantId, programId, activeOnly: true, includeInactiveRules: false, cancellationToken);
        var run = new RunState(tenantId, Today, preview, actingUserId);

        // Every rule WITHOUT a date to count from. The dated ones are about an event — a hire, a
        // move — and pressing a button is not one; running them here would enrol everybody who has
        // ever been hired.
        var result = await EvaluateAsync(run, programmes,
            r => !OrientationTriggerWindows.IsDated(r.Trigger), null,
            _ => OrientationEnrollmentTrigger.Manual, "Manual", cancellationToken);
        return await CommitAsync(run, result, cancellationToken);
    }

    public async Task<OrientationTriggerRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, bool preview = false,
        CancellationToken cancellationToken = default)
    {
        var today = Today;
        var programmes = await LoadProgrammesAsync(tenantId, null, activeOnly: true, includeInactiveRules: false, cancellationToken);
        var run = new RunState(tenantId, today, preview, triggeredByUserId);
        var total = Empty($"Sweep ({trigger})");
        total.IsPreview = preview;

        if (programmes.Count == 0) return total;

        var inclusive = programmes.SelectMany(p => p.Rules).Where(r => r.IsInclusive).ToList();

        // 1 — scheduled rules: everyone they reach who is not yet on the programme.
        if (inclusive.Any(r => r.Trigger == OrientationEnrollmentTrigger.Scheduled))
            Merge(total, await EvaluateAsync(run, programmes,
                r => r.Trigger == OrientationEnrollmentTrigger.Scheduled, null,
                _ => OrientationEnrollmentTrigger.Scheduled, "Scheduled", cancellationToken));

        // 2 — hires whose day has come (a delay elapsed, a hook that failed, a rule added since).
        var hireRules = inclusive.Where(r => r.Trigger == OrientationEnrollmentTrigger.OnHire).ToList();
        if (hireRules.Count > 0)
        {
            var reach = hireRules.Max(r => Math.Max(0, r.EnrollmentDelayDays)) + OrientationTriggerWindows.CatchUpDays;
            var cutoff = today.AddDays(-reach);

            var hires = await _unitOfWork.Repository<Employee>().GetQueryable()
                .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive
                         && e.DateEmployed != null && e.DateEmployed >= cutoff)
                .Select(e => new { e.Id, e.DateEmployed })
                .ToListAsync(cancellationToken);

            if (hires.Count > 0)
                Merge(total, await EvaluateAsync(run, programmes,
                    r => r.Trigger == OrientationEnrollmentTrigger.OnHire,
                    hires.ToDictionary(h => h.Id, h => h.DateEmployed),
                    _ => OrientationEnrollmentTrigger.OnHire, "Hire", cancellationToken));
        }

        // 3 — transfers and promotions whose effective date has come.
        foreach (var movementTrigger in new[] { OrientationEnrollmentTrigger.OnTransfer, OrientationEnrollmentTrigger.OnPromotion })
        {
            var rules = inclusive.Where(r => r.Trigger == movementTrigger).ToList();
            if (rules.Count == 0) continue;

            var reach = rules.Max(r => Math.Max(0, r.EnrollmentDelayDays)) + OrientationTriggerWindows.CatchUpDays;
            var cutoffAt = today.AddDays(-reach).ToDateTime(TimeOnly.MinValue);
            var types = OrientationTriggerWindows.TriggeringMovementTypes
                .Where(t => OrientationTriggerWindows.TriggerFor(t) == movementTrigger).ToList();

            var moves = await _unitOfWork.Repository<StaffMovement>().GetQueryable()
                .Where(m => m.TenantId == tenantId && !m.IsDeleted
                         && m.Status == StaffMovementStatus.Implemented
                         && types.Contains(m.MovementType)
                         && m.EffectiveDate >= cutoffAt)
                .Select(m => new { m.EmployeeId, m.EffectiveDate })
                .ToListAsync(cancellationToken);
            if (moves.Count == 0) continue;

            // The latest movement is the one a person's orientation is about.
            var candidates = moves.GroupBy(m => m.EmployeeId)
                .ToDictionary(g => g.Key, g => (DateOnly?)DateOnly.FromDateTime(g.Max(m => m.EffectiveDate)));

            Merge(total, await EvaluateAsync(run, programmes,
                r => r.Trigger == movementTrigger, candidates,
                _ => movementTrigger, TriggerLabel(movementTrigger), cancellationToken));
        }

        // 4 — publish rules of a programme published AHEAD of its effective date (lane I-b). The
        //     publish hook fires nothing for a programme not yet in effect, so the rules run here on
        //     the sweeps of its first CatchUpDays in effect. De-duplication makes the repeats
        //     harmless; the cost is that somebody joining the audience during those days is caught
        //     too, which a publish rule otherwise would not do.
        var justInEffect = programmes
            .Where(p => p.Program.EffectiveFrom is { } starts
                     && DateOnly.FromDateTime(starts) <= today
                     && DateOnly.FromDateTime(starts) >= today.AddDays(-OrientationTriggerWindows.CatchUpDays))
            .ToList();
        if (justInEffect.Any(p => p.Rules.Any(r => r.IsInclusive && r.Trigger == OrientationEnrollmentTrigger.OnProgramPublish)))
            Merge(total, await EvaluateAsync(run, justInEffect,
                r => r.Trigger == OrientationEnrollmentTrigger.OnProgramPublish, null,
                _ => OrientationEnrollmentTrigger.OnProgramPublish, "Publish (came into effect)", cancellationToken));

        // 5 — the next cycle of recurring programmes (lane I-b).
        if (programmes.Any(p => p.Program.Recurs is not null))
            Merge(total, await RenewAsync(run, programmes, cancellationToken));

        return await CommitAsync(run, total, cancellationToken);
    }

    // ====================================================================
    // REACH, NAMES, VALIDATION
    // ====================================================================

    public async Task<OrientationAudienceReachDto> CountReachAsync(
        OrientationAudienceReachRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId();
        await HrAudienceTargets.ValidateAsync(_unitOfWork, tenantId, request.TargetType, request.TargetEntityId,
            allowEmployee: true, cancellationToken);

        var cache = new AudienceCache();
        var reached = await MatchAsync(tenantId, request.TargetType, request.TargetEntityId, request.Population, Today, cache, cancellationToken);
        var names = await HrAudienceTargets.ResolveNamesAsync(_unitOfWork, tenantId,
            [(request.TargetType, request.TargetEntityId)], cancellationToken);

        return new OrientationAudienceReachDto
        {
            Count = reached.Count,
            Description = DescribeRule(request.TargetType, request.TargetEntityId, request.Population, names),
        };
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountRuleReachAsync(
        Guid tenantId, IEnumerable<OrientationAudienceRule> rules, CancellationToken cancellationToken = default)
    {
        var cache = new AudienceCache();
        var today = Today;
        var counts = new Dictionary<Guid, int>();
        foreach (var rule in rules)
            counts[rule.Id] = (await MatchAsync(tenantId, rule.TargetType, rule.TargetEntityId, rule.Population, today, cache, cancellationToken)).Count;
        return counts;
    }

    public Task<IReadOnlyDictionary<Guid, string>> ResolveTargetNamesAsync(
        Guid tenantId, IEnumerable<(HrAudienceTargetType Type, Guid? Id)> targets, CancellationToken cancellationToken = default)
        => HrAudienceTargets.ResolveNamesAsync(_unitOfWork, tenantId, targets, cancellationToken);

    public Task ValidateTargetAsync(
        Guid tenantId, HrAudienceTargetType targetType, Guid? targetEntityId, bool allowEmployee,
        CancellationToken cancellationToken = default)
        => HrAudienceTargets.ValidateAsync(_unitOfWork, tenantId, targetType, targetEntityId, allowEmployee, cancellationToken);

    // ====================================================================
    // DIAGNOSTIC (I5)
    // ====================================================================

    public async Task<OrientationTriggerDiagnosisDto> DiagnoseAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId();
        var today = Today;

        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted)
            .Select(e => new
            {
                e.Id, e.FirstName, e.LastName, e.EmployeeNumber, e.IsActive, e.DateEmployed,
                e.OrganizationUnitId, e.OrganizationLevelId, e.PositionId, e.LocationId, e.EmploymentType,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        var hireDate = employee.DateEmployed;
        var cache = new AudienceCache();

        var placementNames = await HrAudienceTargets.ResolveNamesAsync(_unitOfWork, tenantId,
        [
            (HrAudienceTargetType.OrganizationUnit, employee.OrganizationUnitId),
            (HrAudienceTargetType.OrganizationLevel, employee.OrganizationLevelId),
            (HrAudienceTargetType.Position, employee.PositionId),
            (HrAudienceTargetType.Location, employee.LocationId),
        ], cancellationToken);
        string? NameOf(Guid? id) => id is { } g && placementNames.TryGetValue(g, out var n) ? n : null;

        var diagnosis = new OrientationTriggerDiagnosisDto
        {
            EmployeeId = employee.Id,
            EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(),
            EmployeeNumber = employee.EmployeeNumber,
            IsActive = employee.IsActive,
            AsOf = today,
            HireDate = hireDate,
            OrganizationUnitName = NameOf(employee.OrganizationUnitId),
            OrganizationLevelName = NameOf(employee.OrganizationLevelId),
            PositionTitle = NameOf(employee.PositionId),
            LocationName = NameOf(employee.LocationId),
            EmploymentType = employee.EmploymentType.ToString(),
        };

        foreach (var population in new[]
                 {
                     OrientationAudiencePopulation.NewHires,
                     OrientationAudiencePopulation.Management,
                     OrientationAudiencePopulation.Contractors,
                 })
        {
            if ((await PopulationAsync(tenantId, population, today, cache, cancellationToken)).Contains(employee.Id))
                diagnosis.Populations.Add(population.ToString());
        }

        var movementTypes = OrientationTriggerWindows.TriggeringMovementTypes;
        var movements = await _unitOfWork.Repository<StaffMovement>().GetQueryable()
            .Where(m => m.TenantId == tenantId && m.EmployeeId == employeeId && !m.IsDeleted
                     && m.Status == StaffMovementStatus.Implemented && movementTypes.Contains(m.MovementType))
            .OrderByDescending(m => m.EffectiveDate)
            .Select(m => new { m.Id, m.MovementNumber, m.MovementType, m.EffectiveDate })
            .Take(10)
            .ToListAsync(cancellationToken);
        diagnosis.RecentMovements = movements.Select(m => new OrientationTriggerMovementDto
        {
            MovementId = m.Id,
            MovementNumber = m.MovementNumber,
            MovementType = m.MovementType.ToString(),
            Trigger = OrientationTriggerWindows.TriggerFor(m.MovementType)!.Value,
            EffectiveDate = DateOnly.FromDateTime(m.EffectiveDate),
        }).ToList();

        DateOnly? LatestMovement(OrientationEnrollmentTrigger trigger) =>
            diagnosis.RecentMovements.Where(m => m.Trigger == trigger)
                .Select(m => (DateOnly?)m.EffectiveDate).FirstOrDefault();

        // Every programme with a rule, plus any the person is already on.
        var enrolments = await _unitOfWork.Repository<EmployeeOrientation>().GetQueryable()
            .Where(e => e.TenantId == tenantId && e.EmployeeId == employeeId && !e.IsDeleted)
            .OrderByDescending(e => e.EnrolledAt).ThenByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id, e.ProgramId, e.EnrollmentStatus, e.EnrollmentSource, e.AudienceRuleId, e.TriggerEvent,
                e.CompletionStatus, e.CompletedAt,
            })
            .ToListAsync(cancellationToken);
        var enrolmentByProgramme = enrolments.GroupBy(e => e.ProgramId).ToDictionary(g => g.Key, g => g.First());

        var programmes = await LoadProgrammesAsync(tenantId, null, activeOnly: false, includeInactiveRules: true, cancellationToken);
        programmes = programmes.Where(p => p.Rules.Count > 0 || enrolmentByProgramme.ContainsKey(p.Program.Id)).ToList();

        var ruleNames = await HrAudienceTargets.ResolveNamesAsync(_unitOfWork, tenantId,
            programmes.SelectMany(p => p.Rules).Select(r => (r.TargetType, r.TargetEntityId)), cancellationToken);
        var allRuleNames = programmes.SelectMany(p => p.Rules).ToDictionary(r => r.Id, r => r.RuleName);
        var completed = await CompletedProgrammesAsync(tenantId, employeeId, cancellationToken);

        foreach (var programme in programmes.OrderBy(p => p.Program.Code))
        {
            var pd = new OrientationProgramDiagnosisDto
            {
                ProgramId = programme.Program.Id,
                ProgramCode = programme.Program.Code,
                ProgramTitle = programme.Program.Title,
                ProgramStatus = programme.Program.Status,
            };

            foreach (var rule in programme.Rules.OrderByDescending(r => r.IsInclusive).ThenBy(r => r.RuleName))
            {
                var axis = await AxisAsync(tenantId, rule.TargetType, rule.TargetEntityId, cache, cancellationToken);
                var populationSet = rule.Population == OrientationAudiencePopulation.Anyone
                    ? null
                    : await PopulationAsync(tenantId, rule.Population, today, cache, cancellationToken);

                var rd = new OrientationRuleDiagnosisDto
                {
                    RuleId = rule.Id,
                    RuleName = rule.RuleName,
                    IsInclusive = rule.IsInclusive,
                    IsActive = rule.IsActive,
                    Trigger = rule.Trigger,
                    TargetType = rule.TargetType,
                    TargetName = HrAudienceTargets.Describe(rule.TargetType, rule.TargetEntityId, ruleNames),
                    Population = rule.Population,
                    EnrollmentDelayDays = rule.EnrollmentDelayDays,
                    MatchesTarget = axis.Contains(employee.Id),
                    MatchesPopulation = populationSet is null || populationSet.Contains(employee.Id),
                };

                DateOnly? triggerDate = rule.Trigger switch
                {
                    OrientationEnrollmentTrigger.OnHire => hireDate,
                    OrientationEnrollmentTrigger.OnTransfer or OrientationEnrollmentTrigger.OnPromotion => LatestMovement(rule.Trigger),
                    _ => null,
                };

                if (OrientationTriggerWindows.IsDated(rule.Trigger))
                {
                    if (triggerDate is not { } d)
                    {
                        rd.Timing = "NoEvent";
                        rd.Explanation = rule.Trigger switch
                        {
                            OrientationEnrollmentTrigger.OnHire =>
                                "This person has no employment date, so the hire rule has nothing to count from. Set it on their record; the nightly sweep fires within the window.",
                            OrientationEnrollmentTrigger.OnPromotion =>
                                "This person has no implemented promotion for the rule to count from.",
                            _ => "This person has no implemented transfer, lateral move or secondment for the rule to count from.",
                        };
                    }
                    else
                    {
                        rd.TriggerDate = d;
                        rd.FiresFrom = OrientationTriggerWindows.FiresFrom(d, rule.EnrollmentDelayDays);
                        rd.FiresUntil = OrientationTriggerWindows.FiresUntil(d, rule.EnrollmentDelayDays);
                        rd.Timing = today < rd.FiresFrom ? "NotYet" : today > rd.FiresUntil ? "Lapsed" : "Due";
                        rd.Explanation = rd.Timing switch
                        {
                            "NotYet" => $"Fires from {rd.FiresFrom:d MMM yyyy} ({TriggerLabel(rule.Trigger).ToLowerInvariant()} on {d:d MMM yyyy} + {rule.EnrollmentDelayDays} day(s)).",
                            "Lapsed" => $"Its window closed on {rd.FiresUntil:d MMM yyyy} — dated rules fire for {OrientationTriggerWindows.CatchUpDays} days after their day, so they never reach back into history.",
                            _ => $"Due: its window runs {rd.FiresFrom:d MMM yyyy} to {rd.FiresUntil:d MMM yyyy}.",
                        };
                    }
                }
                else
                {
                    (rd.Timing, rd.Explanation) = rule.Trigger switch
                    {
                        OrientationEnrollmentTrigger.Scheduled => ("Nightly", "Fires on every nightly sweep for anyone it reaches who is not yet on the programme."),
                        OrientationEnrollmentTrigger.OnProgramPublish when programme.Program.EffectiveFrom is { } starts
                                                                          && DateOnly.FromDateTime(starts) > today
                            => ("AtPublish", $"Fires when the programme comes into effect on {DateOnly.FromDateTime(starts):d MMM yyyy} — the nightly sweep runs it then."),
                        OrientationEnrollmentTrigger.OnProgramPublish => ("AtPublish", "Fired when the programme was published; after that only HR's \"Enrol audience now\" runs it again."),
                        _ => ("OnlyByHr", "Fires only when HR presses \"Enrol audience now\" on the programme."),
                    };
                }

                if (!rd.MatchesTarget)
                    rd.Explanation = $"Does not reach this person: they are not in {rd.TargetName}. " + rd.Explanation;
                else if (!rd.MatchesPopulation)
                    rd.Explanation = $"Reaches their placement, but they are not in the {PopulationLabel(rule.Population).ToLowerInvariant()} population. " + rd.Explanation;

                pd.Rules.Add(rd);
            }

            pd.MissingPrerequisites = programme.Prerequisites
                .Where(p => !completed.Contains(p.Id)).Select(p => p.Title).ToList();

            LatestEnrolment? existing = null;
            if (enrolmentByProgramme.TryGetValue(programme.Program.Id, out var en))
            {
                string? byRule = en.AudienceRuleId is { } rid && allRuleNames.TryGetValue(rid, out var rn) ? rn : null;
                existing = new LatestEnrolment(en.Id, en.EnrollmentStatus, en.EnrollmentSource, byRule,
                    en.CompletionStatus, en.CompletedAt);
            }

            Decide(pd, programme, existing, employee.IsActive, today);

            diagnosis.Programs.Add(pd);
        }

        // The onboarding half: which template, and whether a plan exists already.
        diagnosis.Onboarding = await _onboardingApplicability.FindApplicableForEmployeeAsync(tenantId, employeeId, cancellationToken);
        var plan = await _unitOfWork.Repository<OnboardingPlan>().GetQueryable()
            .Where(p => p.TenantId == tenantId && p.EmployeeId == employeeId && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.TemplatePlanId, p.TemplateSelectionReason })
            .FirstOrDefaultAsync(cancellationToken);
        if (plan is not null)
        {
            diagnosis.OnboardingPlanId = plan.Id;
            diagnosis.OnboardingPlanSelectionReason = plan.TemplateSelectionReason;
            if (plan.TemplatePlanId is { } templateId)
                diagnosis.OnboardingPlanTemplateName = await _unitOfWork.Repository<OnboardingPlanTemplate>().GetQueryable()
                    .Where(t => t.Id == templateId).Select(t => t.Name).FirstOrDefaultAsync(cancellationToken);
        }

        return diagnosis;
    }

    /// <summary>The programme-level verdict, in the order a person would ask the questions.</summary>
    private static void Decide(
        OrientationProgramDiagnosisDto pd, ProgrammeRules programme, LatestEnrolment? enrolment,
        bool employeeActive, DateOnly today)
    {
        var notLive = programme.Program.NotLiveBecause(today);
        var reaching = pd.Rules.Where(r => r.IsActive && r.IsInclusive && r.MatchesTarget && r.MatchesPopulation).ToList();
        var exclusion = pd.Rules.FirstOrDefault(r => r.IsActive && !r.IsInclusive && r.MatchesTarget && r.MatchesPopulation);

        if (enrolment is { } en)
        {
            pd.EnrollmentId = en.Id;
            pd.EnrollmentStatus = en.Status.ToString();
            pd.EnrollmentSource = en.Source.ToString();
            pd.EnrolledByRuleName = en.RuleName;

            // Lane I-b: an enrolment HR ended is HR's to undo — and only HR's.
            if (EmployeeOrientationService.IsEndedByHr(en.Status))
            {
                pd.Verdict = "EndedByHr";
                pd.Explanation = $"HR ended their enrolment ({en.Status}). No rule and no renewal ever puts them back — HR can re-enrol them by hand.";
                return;
            }

            pd.Verdict = "Enrolled";
            pd.Explanation = en.Source switch
            {
                OrientationEnrollmentSource.AutoRule =>
                    $"Enrolled automatically{(en.RuleName is null ? "" : $" by \"{en.RuleName}\"")}; status {en.Status}. No rule enrols anyone twice.",
                OrientationEnrollmentSource.Recurrence =>
                    $"On the next cycle of this recurring programme, opened automatically; status {en.Status}.",
                _ => $"Already on the programme ({en.Source}, {en.Status}). No rule enrols anyone twice.",
            };

            if (programme.Program.Recurs is { } frequency)
                ExplainRenewal(pd, programme, en, frequency, notLive, reaching, exclusion, employeeActive, today);
            return;
        }

        if (notLive is not null)
        {
            pd.Verdict = "ProgramNotActive";
            pd.Explanation = notLive;
            return;
        }

        if (!employeeActive)
        {
            pd.Verdict = "NotInAudience";
            pd.Explanation = "This person is not an active employee, and rules only reach active employees.";
            return;
        }

        if (reaching.Count == 0)
        {
            pd.Verdict = "NotInAudience";
            pd.Explanation = pd.Rules.Any(r => r.IsInclusive && r.IsActive)
                ? "None of the programme's active rules reaches this person."
                : "The programme has no active rule that enrols anyone — everyone on it is enrolled by hand.";
            return;
        }

        if (exclusion is not null)
        {
            pd.Verdict = "Excluded";
            pd.Explanation = $"Reached by \"{reaching[0].RuleName}\", but the exclusion \"{exclusion.RuleName}\" keeps them out — whatever the trigger.";
            return;
        }

        // A missing mandatory prerequisite blocks EVERY rule that could enrol them now — a due or
        // nightly rule and HR's "enrol audience now" alike (EvaluateAsync holds all of them back).
        // It is checked before the by-hand branch below on purpose: telling HR to press a button
        // that would enrol nobody is the wrong answer, and the harness caught this diagnostic
        // giving it (round 4 lane I, E14).
        var couldFire = reaching.FirstOrDefault(r => r.Timing is "Due" or "Nightly" or "OnlyByHr" or "AtPublish");
        if (couldFire is not null && pd.MissingPrerequisites.Count > 0)
        {
            pd.Verdict = "WaitingOnPrerequisite";
            pd.Explanation = $"\"{couldFire.RuleName}\" reaches them, but they must first complete {string.Join(", ", pd.MissingPrerequisites)}. " +
                             (couldFire.Timing is "Due" or "Nightly"
                                 ? "The nightly sweep enrols them once they have."
                                 : "\"Enrol audience now\" will reach them once they have.");
            return;
        }

        var firing = reaching.FirstOrDefault(r => r.Timing is "Due" or "Nightly");
        if (firing is not null)
        {

            pd.Verdict = "WouldEnrolNow";
            pd.Explanation = firing.Timing == "Nightly"
                ? $"\"{firing.RuleName}\" reaches them; tonight's sweep enrols them."
                : $"\"{firing.RuleName}\" is due and reaches them, but no enrollment exists — the event hook did not run or the rule is newer than the event. Tonight's sweep enrols them.";
            return;
        }

        var waiting = reaching.Where(r => r.Timing == "NotYet").OrderBy(r => r.FiresFrom).FirstOrDefault();
        if (waiting is not null)
        {
            pd.Verdict = "WaitingForDate";
            pd.Explanation = $"\"{waiting.RuleName}\" reaches them and fires from {waiting.FiresFrom:d MMM yyyy}.";
            return;
        }

        var byHr = reaching.FirstOrDefault(r => r.Timing is "OnlyByHr" or "AtPublish");
        if (byHr is not null)
        {
            pd.Verdict = "OnlyWhenHrEnrols";
            pd.Explanation = $"\"{byHr.RuleName}\" reaches them, but it fires only at publication or when HR presses \"Enrol audience now\".";
            return;
        }

        if (reaching.Any(r => r.Timing == "Lapsed"))
        {
            pd.Verdict = "WindowLapsed";
            pd.Explanation = reaching.First(r => r.Timing == "Lapsed").Explanation;
            return;
        }

        pd.Verdict = "NoTriggeringEvent";
        pd.Explanation = reaching[0].Explanation;
    }

    /// <summary>
    /// For a recurring programme, what happens after the person's latest enrolment — the same
    /// questions <see cref="RenewAsync"/> asks, in the same order (lane I-b).
    /// </summary>
    private static void ExplainRenewal(
        OrientationProgramDiagnosisDto pd, ProgrammeRules programme, LatestEnrolment en,
        OrientationRecurrenceFrequency frequency, string? notLive,
        List<OrientationRuleDiagnosisDto> reaching, OrientationRuleDiagnosisDto? exclusion,
        bool employeeActive, DateOnly today)
    {
        var every = OrientationTriggerWindows.Describe(frequency);

        if (EmployeeOrientationService.IsEndedByHr(en.Status))
        {
            pd.Explanation += $" The programme recurs {every}, but an enrolment HR ended ({en.Status}) is never renewed over by the system — HR can re-enrol them by hand.";
            return;
        }

        if (en.Completion != OrientationCompletionStatus.Completed || en.CompletedAt is not { } completedAt)
        {
            pd.Explanation += $" The programme recurs {every}: the next cycle opens one period after this one is completed.";
            return;
        }

        var completedOn = DateOnly.FromDateTime(completedAt);
        var deadline = programme.Program.CompletionDeadlineDays;
        var opens = OrientationTriggerWindows.OpensOn(completedOn, frequency, deadline);
        var anniversary = OrientationTriggerWindows.Anniversary(completedOn, frequency);
        pd.LastCompletedOn = completedOn;
        pd.NextCycleOpensOn = opens;
        pd.NextCycleDueOn = anniversary;
        var when = deadline is > 0
            ? $"opens on {opens:d MMM yyyy} and is due on {anniversary:d MMM yyyy} ({every} after completion; it opens {deadline} days early so the deadline falls on the anniversary)"
            : $"opens on {opens:d MMM yyyy} ({every} after completion)";

        if (notLive is not null)
        {
            pd.Explanation = $"Completed on {completedOn:d MMM yyyy}. {notLive} No next cycle opens while it is not.";
            return;
        }

        if (today < opens)
        {
            pd.Verdict = "RenewalScheduled";
            pd.Explanation = $"Completed on {completedOn:d MMM yyyy}. The programme recurs {every}: the next cycle {when}.";
            return;
        }

        if (!employeeActive)
        {
            pd.Verdict = "NotInAudience";
            pd.Explanation = $"Completed on {completedOn:d MMM yyyy} and the next cycle is due, but they are not an active employee, so it is not renewed.";
            return;
        }

        // A programme with no active inclusive rule is managed by hand: everyone who completed it renews.
        var hasRules = pd.Rules.Any(r => r.IsActive && r.IsInclusive);
        if (hasRules && reaching.Count == 0)
        {
            pd.Verdict = "NotInAudience";
            pd.Explanation = $"Completed on {completedOn:d MMM yyyy} and the next cycle {when} — but none of the programme's rules reaches them any longer, so it is not renewed.";
            return;
        }

        if (exclusion is not null)
        {
            pd.Verdict = "Excluded";
            pd.Explanation = $"Completed on {completedOn:d MMM yyyy} and the next cycle is due, but the exclusion \"{exclusion.RuleName}\" keeps them out of it.";
            return;
        }

        pd.Verdict = "RenewalDue";
        pd.Explanation = $"Completed on {completedOn:d MMM yyyy}. The next cycle {when} — tonight's sweep enrols them.";
    }

    /// <summary>A person's latest enrolment on one programme, as the diagnostic reads it.</summary>
    private sealed record LatestEnrolment(
        Guid Id, OrientationEnrollmentStatus Status, OrientationEnrollmentSource Source, string? RuleName,
        OrientationCompletionStatus Completion, DateTime? CompletedAt);

    // ====================================================================
    // THE EVALUATOR
    // ====================================================================

    /// <summary>
    /// Runs the selected inclusive rules of each programme over the candidates (null = everyone)
    /// and stages the enrollments. Never saves — <see cref="CommitAsync"/> does, once.
    /// </summary>
    private async Task<OrientationTriggerRunResultDto> EvaluateAsync(
        RunState run,
        IReadOnlyList<ProgrammeRules> programmes,
        Func<OrientationAudienceRule, bool> selectRule,
        IReadOnlyDictionary<Guid, DateOnly?>? candidates,
        Func<OrientationAudienceRule, OrientationEnrollmentTrigger> firedAs,
        string label,
        CancellationToken cancellationToken)
    {
        var result = Empty(label);
        result.IsPreview = run.Preview;

        foreach (var programme in programmes)
        {
            var includes = programme.Rules.Where(r => r.IsInclusive && selectRule(r))
                .OrderBy(r => r.EnrollmentDelayDays).ThenBy(r => r.RuleName).ToList();
            if (includes.Count == 0) continue;

            result.ProgramsEvaluated++;
            result.RulesEvaluated += includes.Count;

            // Who each rule reaches — the first (shortest-delay) rule that is due wins the person.
            var chosen = new Dictionary<Guid, (OrientationAudienceRule Rule, DateOnly? TriggerDate)>();
            foreach (var rule in includes)
            {
                var reached = await MatchAsync(run.TenantId, rule.TargetType, rule.TargetEntityId, rule.Population, run.Today, run.Cache, cancellationToken);
                foreach (var employeeId in reached)
                {
                    if (chosen.ContainsKey(employeeId)) continue;

                    DateOnly? triggerDate = null;
                    if (candidates is not null && !candidates.TryGetValue(employeeId, out triggerDate)) continue;

                    if (OrientationTriggerWindows.IsDated(rule.Trigger)
                        && (triggerDate is not { } d || !OrientationTriggerWindows.IsDue(d, rule.EnrollmentDelayDays, run.Today)))
                        continue;

                    chosen[employeeId] = (rule, triggerDate);
                }
            }
            if (chosen.Count == 0) continue;

            // Exclusions apply whatever trigger they were written against.
            var excluded = new HashSet<Guid>();
            foreach (var rule in programme.Rules.Where(r => !r.IsInclusive))
                excluded.UnionWith(await MatchAsync(run.TenantId, rule.TargetType, rule.TargetEntityId, rule.Population, run.Today, run.Cache, cancellationToken));
            foreach (var employeeId in chosen.Keys.Where(excluded.Contains).ToList())
            {
                chosen.Remove(employeeId);
                result.Excluded++;
            }

            // Anyone ever enrolled — a withdrawal included — is not a rule's to enrol again.
            var enrolled = await EnrolledAsync(run, programme.Program.Id, cancellationToken);
            foreach (var employeeId in chosen.Keys.Where(id => enrolled.Contains(id) || run.StagedThisRun.Contains((programme.Program.Id, id))).ToList())
            {
                chosen.Remove(employeeId);
                result.AlreadyEnrolled++;
            }

            // A mandatory prerequisite gates an automatic enrollment (not a manual one — see remarks).
            foreach (var prerequisite in programme.Prerequisites)
            {
                if (chosen.Count == 0) break;
                var done = await CompletedAsync(run, prerequisite.Id, cancellationToken);
                foreach (var employeeId in chosen.Keys.Where(id => !done.Contains(id)).ToList())
                {
                    chosen.Remove(employeeId);
                    result.WaitingOnPrerequisite++;
                }
            }

            var repository = _unitOfWork.Repository<EmployeeOrientation>();
            foreach (var (employeeId, (rule, triggerDate)) in chosen)
            {
                var trigger = firedAs(rule);
                var entity = new EmployeeOrientation
                {
                    TenantId = run.TenantId,
                    ProgramId = programme.Program.Id,
                    EmployeeId = employeeId,
                    SessionId = null, // automatic enrollment is self-paced; a session is HR's choice
                    EnrollmentStatus = OrientationEnrollmentStatus.Confirmed,
                    EnrollmentSource = OrientationEnrollmentSource.AutoRule,
                    EnrolledAt = DateTime.UtcNow,
                    EnrolledByEmployeeId = null,
                    CompletionStatus = OrientationCompletionStatus.NotStarted,
                    AudienceRuleId = rule.Id,
                    TriggerEvent = trigger,
                    TriggerDate = triggerDate,
                    CreatedById = run.ActingUserId,
                    CreatedBy = run.ActingUserId?.ToString() ?? SystemActor,
                };
                if (programme.Program.CompletionDeadlineDays is > 0)
                    entity.NextDueDate = entity.EnrolledAt.AddDays(programme.Program.CompletionDeadlineDays.Value);

                if (!run.Preview)
                {
                    await repository.AddAsync(entity);
                    // Lane K-b: staged with the enrolment, committed by the same save.
                    await _notices.EnrolledAsync(entity, programme.Program.Notice, null,
                        $"Enrolled by the audience rule \"{rule.RuleName}\".", cancellationToken);
                }
                run.StagedThisRun.Add((programme.Program.Id, employeeId));

                result.Enrolled++;
                if (result.Enrolments.Count < MaxListedEnrolments)
                    result.Enrolments.Add(new OrientationTriggerEnrolmentDto
                    {
                        EnrollmentId = run.Preview ? null : entity.Id,
                        EmployeeId = employeeId,
                        ProgramId = programme.Program.Id,
                        ProgramTitle = programme.Program.Title,
                        RuleId = rule.Id,
                        RuleName = rule.RuleName,
                        TriggerEvent = trigger,
                        TriggerDate = triggerDate,
                    });
            }
        }

        return result;
    }

    /// <summary>
    /// Opens the next cycle of every recurring programme for the people whose last cycle is due
    /// (round 4, lane I-b). Stages only — <see cref="CommitAsync"/> saves.
    /// </summary>
    /// <remarks>
    /// <para><b>Who is renewed.</b> A person whose LATEST enrolment on the programme is Completed,
    /// and whose next cycle has opened (<see cref="OrientationTriggerWindows.OpensOn"/>). The latest
    /// enrolment decides everything: an unfinished current cycle is still the current cycle; a
    /// withdrawal or cancellation is HR's act and is never undone by the system; a Failed attempt is
    /// not a completion.</para>
    ///
    /// <para><b>…and whom the programme is still for.</b> If the programme has active inclusive rules,
    /// one of them — whatever its trigger — must still reach the person; a programme with none is
    /// managed by hand, and everyone who completed it is renewed. Exclusions apply either way. So a
    /// recurring site-safety refresher does not follow somebody who has moved to Finance, and a
    /// programme "for new hires" never renews at all, because a year on they are not new hires.</para>
    ///
    /// <para><b>Not gated by prerequisites.</b> They were met for the first cycle.</para>
    ///
    /// <para><b>No catch-up window</b>, unlike the dated triggers: a renewal creates exactly one open
    /// cycle, after which the person's latest enrolment is no longer Completed, so a completion from
    /// years ago renews once, not once per missed period. Making a programme recurring therefore
    /// renews, on the next sweep, everyone whose last completion is older than one period — which is
    /// the point of making it recurring; the sweep preview shows the number first.</para>
    /// </remarks>
    private async Task<OrientationTriggerRunResultDto> RenewAsync(
        RunState run, IReadOnlyList<ProgrammeRules> programmes, CancellationToken cancellationToken)
    {
        var result = Empty("Renewal");
        result.IsPreview = run.Preview;
        var repository = _unitOfWork.Repository<EmployeeOrientation>();

        foreach (var programme in programmes)
        {
            if (programme.Program.Recurs is not { } frequency) continue;

            var rows = await repository.GetQueryable()
                .Where(e => e.TenantId == run.TenantId && e.ProgramId == programme.Program.Id && !e.IsDeleted)
                .Select(e => new { e.EmployeeId, e.EnrolledAt, e.CreatedAt, e.EnrollmentStatus, e.CompletionStatus, e.CompletedAt })
                .ToListAsync(cancellationToken);

            var due = rows
                .GroupBy(e => e.EmployeeId)
                .Select(g => g.OrderByDescending(e => e.EnrolledAt).ThenByDescending(e => e.CreatedAt).First())
                // ⚠ A completed enrolment HR then withdrew still reads Completed; the enrolment
                //   status is what says HR ended it, and the system never renews over that.
                .Where(e => !EmployeeOrientationService.IsEndedByHr(e.EnrollmentStatus))
                .Where(e => e.CompletionStatus == OrientationCompletionStatus.Completed && e.CompletedAt is not null)
                .Select(e => (e.EmployeeId, CompletedOn: DateOnly.FromDateTime(e.CompletedAt!.Value)))
                .Where(e => run.Today >= OrientationTriggerWindows.OpensOn(e.CompletedOn, frequency, programme.Program.CompletionDeadlineDays))
                .ToList();

            result.ProgramsEvaluated++;
            if (due.Count == 0) continue;

            // Still the programme's audience: any active inclusive rule reaches them — or, with no
            // rules at all, anyone active. Exclusions subtract either way.
            var inclusive = programme.Rules.Where(r => r.IsInclusive).ToList();
            HashSet<Guid> audience;
            if (inclusive.Count == 0)
            {
                audience = await AxisAsync(run.TenantId, HrAudienceTargetType.AllEmployees, null, run.Cache, cancellationToken);
            }
            else
            {
                audience = [];
                foreach (var rule in inclusive)
                    audience.UnionWith(await MatchAsync(run.TenantId, rule.TargetType, rule.TargetEntityId, rule.Population, run.Today, run.Cache, cancellationToken));
            }
            result.RulesEvaluated += inclusive.Count;

            var excluded = new HashSet<Guid>();
            foreach (var rule in programme.Rules.Where(r => !r.IsInclusive))
                excluded.UnionWith(await MatchAsync(run.TenantId, rule.TargetType, rule.TargetEntityId, rule.Population, run.Today, run.Cache, cancellationToken));

            foreach (var (employeeId, completedOn) in due)
            {
                if (!audience.Contains(employeeId)) { result.LeftAudience++; continue; }
                if (excluded.Contains(employeeId)) { result.Excluded++; continue; }
                if (!run.StagedThisRun.Add((programme.Program.Id, employeeId))) { result.AlreadyEnrolled++; continue; }

                var entity = new EmployeeOrientation
                {
                    TenantId = run.TenantId,
                    ProgramId = programme.Program.Id,
                    EmployeeId = employeeId,
                    SessionId = null,
                    EnrollmentStatus = OrientationEnrollmentStatus.Confirmed,
                    EnrollmentSource = OrientationEnrollmentSource.Recurrence,
                    EnrolledAt = DateTime.UtcNow,
                    EnrolledByEmployeeId = null,
                    CompletionStatus = OrientationCompletionStatus.NotStarted,
                    AudienceRuleId = null,
                    TriggerEvent = null,
                    TriggerDate = completedOn,
                    CreatedById = run.ActingUserId,
                    CreatedBy = run.ActingUserId?.ToString() ?? SystemActor,
                };
                // The ordinary due date. Because the cycle opened `deadline` days before the
                // anniversary, enrolment + deadline IS the anniversary when it opens on time — and
                // never a date already past when it opens late.
                if (programme.Program.CompletionDeadlineDays is > 0)
                    entity.NextDueDate = entity.EnrolledAt.AddDays(programme.Program.CompletionDeadlineDays.Value);

                if (!run.Preview)
                {
                    await repository.AddAsync(entity);
                    await _notices.EnrolledAsync(entity, programme.Program.Notice, null,
                        $"{programme.Program.Title} recurs {OrientationTriggerWindows.Describe(frequency)}: this is your next cycle.",
                        cancellationToken);
                }

                result.Enrolled++;
                result.Renewed++;
                if (result.Enrolments.Count < MaxListedEnrolments)
                    result.Enrolments.Add(new OrientationTriggerEnrolmentDto
                    {
                        EnrollmentId = run.Preview ? null : entity.Id,
                        EmployeeId = employeeId,
                        ProgramId = programme.Program.Id,
                        ProgramTitle = programme.Program.Title,
                        RuleId = null,
                        RuleName = $"Renewal — recurs {OrientationTriggerWindows.Describe(frequency)}",
                        TriggerEvent = null,
                        TriggerDate = completedOn,
                        IsRenewal = true,
                    });
            }
        }

        return result;
    }

    private async Task<OrientationTriggerRunResultDto> CommitAsync(
        RunState run, OrientationTriggerRunResultDto result, CancellationToken cancellationToken)
    {
        if (!run.Preview && result.Enrolled > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Orientation triggers ({Trigger}) for tenant {TenantId}: {Enrolled} enrolled, {Already} already enrolled, " +
                "{Excluded} excluded, {Waiting} waiting on a prerequisite.",
                result.Trigger, run.TenantId, result.Enrolled, result.AlreadyEnrolled, result.Excluded, result.WaitingOnPrerequisite);
        }

        if (result.Enrolments.Count > 0)
        {
            var names = await _unitOfWork.ResolveEmployeesAsync(run.TenantId, result.Enrolments.Select(e => (Guid?)e.EmployeeId));
            foreach (var e in result.Enrolments)
                if (names.TryGetValue(e.EmployeeId, out var n)) { e.EmployeeName = n.Name; e.EmployeeNumber = n.Number; }
        }
        return result;
    }

    // ====================================================================
    // AUDIENCE SETS
    // ====================================================================

    private async Task<HashSet<Guid>> MatchAsync(
        Guid tenantId, HrAudienceTargetType type, Guid? targetId, OrientationAudiencePopulation population,
        DateOnly today, AudienceCache cache, CancellationToken cancellationToken)
    {
        var axis = await AxisAsync(tenantId, type, targetId, cache, cancellationToken);
        if (population == OrientationAudiencePopulation.Anyone) return axis;

        var people = await PopulationAsync(tenantId, population, today, cache, cancellationToken);
        return axis.Where(people.Contains).ToHashSet();
    }

    /// <summary>Where a target reaches, through the shared resolver. Cached for the run; never mutate the result.</summary>
    private async Task<HashSet<Guid>> AxisAsync(
        Guid tenantId, HrAudienceTargetType type, Guid? targetId, AudienceCache cache, CancellationToken cancellationToken)
    {
        if (cache.Axis.TryGetValue((type, targetId), out var hit)) return hit;

        var ids = await _audienceResolver.ResolveForTenantAsync(tenantId,
            [new HrAudienceRule(type, targetId, IsExclusion: false)], cancellationToken);
        var set = ids.ToHashSet();
        cache.Axis[(type, targetId)] = set;
        return set;
    }

    /// <summary>The derived populations — computed from data, never typed in.</summary>
    private async Task<HashSet<Guid>> PopulationAsync(
        Guid tenantId, OrientationAudiencePopulation population, DateOnly today, AudienceCache cache,
        CancellationToken cancellationToken)
    {
        if (cache.Populations.TryGetValue(population, out var hit)) return hit;

        var employees = _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive);

        HashSet<Guid> set;
        switch (population)
        {
            case OrientationAudiencePopulation.NewHires:
                var cutoff = today.AddDays(-OrientationTriggerWindows.NewHireWindowDays);
                set = (await employees
                        .Where(e => e.DateEmployed != null && e.DateEmployed >= cutoff)
                        .Select(e => e.Id).ToListAsync(cancellationToken))
                    .ToHashSet();
                break;

            case OrientationAudiencePopulation.Management:
                var heads = await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                    .Where(u => u.TenantId == tenantId && !u.IsDeleted && u.HeadEmployeeId != null)
                    .Select(u => u.HeadEmployeeId!.Value).ToListAsync(cancellationToken);
                var managers = await employees
                    .Where(e => e.ManagerId != null)
                    .Select(e => e.ManagerId!.Value).Distinct().ToListAsync(cancellationToken);
                set = heads.Concat(managers).ToHashSet();
                break;

            case OrientationAudiencePopulation.Contractors:
                var types = OrientationTriggerWindows.ContractorTypes;
                set = (await employees.Where(e => types.Contains(e.EmploymentType))
                        .Select(e => e.Id).ToListAsync(cancellationToken))
                    .ToHashSet();
                break;

            default:
                set = [];
                break;
        }

        cache.Populations[population] = set;
        return set;
    }

    private async Task<HashSet<Guid>> EnrolledAsync(RunState run, Guid programmeId, CancellationToken cancellationToken)
    {
        if (run.Cache.Enrolled.TryGetValue(programmeId, out var hit)) return hit;
        var set = (await _unitOfWork.Repository<EmployeeOrientation>().GetQueryable()
                .Where(e => e.TenantId == run.TenantId && e.ProgramId == programmeId && !e.IsDeleted)
                .Select(e => e.EmployeeId).ToListAsync(cancellationToken))
            .ToHashSet();
        run.Cache.Enrolled[programmeId] = set;
        return set;
    }

    private async Task<HashSet<Guid>> CompletedAsync(RunState run, Guid programmeId, CancellationToken cancellationToken)
    {
        if (run.Cache.Completed.TryGetValue(programmeId, out var hit)) return hit;
        var set = (await _unitOfWork.Repository<EmployeeOrientation>().GetQueryable()
                .Where(e => e.TenantId == run.TenantId && e.ProgramId == programmeId && !e.IsDeleted
                         && (e.CompletionStatus == OrientationCompletionStatus.Completed
                             || e.CompletionStatus == OrientationCompletionStatus.Exempted))
                .Select(e => e.EmployeeId).ToListAsync(cancellationToken))
            .ToHashSet();
        run.Cache.Completed[programmeId] = set;
        return set;
    }

    private async Task<HashSet<Guid>> CompletedProgrammesAsync(Guid tenantId, Guid employeeId, CancellationToken cancellationToken)
        => (await _unitOfWork.Repository<EmployeeOrientation>().GetQueryable()
                .Where(e => e.TenantId == tenantId && e.EmployeeId == employeeId && !e.IsDeleted
                         && (e.CompletionStatus == OrientationCompletionStatus.Completed
                             || e.CompletionStatus == OrientationCompletionStatus.Exempted))
                .Select(e => e.ProgramId).ToListAsync(cancellationToken))
            .ToHashSet();

    // ====================================================================
    // LOADING
    // ====================================================================

    /// <remarks>
    /// <paramref name="activeOnly"/> means LIVE: Active and in its effective window today (lane I-b).
    /// Every trigger, the renewals and HR's "enrol audience now" load through here, so a programme
    /// outside its effective dates cannot enrol anybody by any route.
    /// </remarks>
    private async Task<List<ProgrammeRules>> LoadProgrammesAsync(
        Guid tenantId, Guid? programId, bool activeOnly, bool includeInactiveRules, CancellationToken cancellationToken)
    {
        var today = Today;
        var programmes = (await _unitOfWork.Repository<OrientationProgram>().GetQueryable()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted
                         && (programId == null || p.Id == programId)
                         && (!activeOnly || p.Status == OrientationProgramStatus.Active))
                .Select(p => new ProgrammeInfo(p.Id, p.ProgramCode, p.Title, p.Status, p.CompletionDeadlineDays,
                    p.IsRecurring, p.RecurrenceFrequency, p.EffectiveFrom, p.EffectiveTo, p.EnableReminders))
                .ToListAsync(cancellationToken))
            .Where(p => !activeOnly || p.IsInEffect(today))
            .ToList();
        if (programmes.Count == 0) return [];

        var ids = programmes.Select(p => p.Id).ToList();

        var rules = await _unitOfWork.Repository<OrientationAudienceRule>().GetQueryable()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && ids.Contains(r.ProgramId)
                     && (includeInactiveRules || r.IsActive))
            .ToListAsync(cancellationToken);

        var prerequisites = await _unitOfWork.Repository<OrientationPrerequisite>().GetQueryable()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.IsMandatory && ids.Contains(p.ProgramId))
            .Select(p => new { p.ProgramId, p.PrerequisiteProgramId, p.PrerequisiteProgram.Title })
            .ToListAsync(cancellationToken);

        return programmes.Select(p => new ProgrammeRules(
                p,
                rules.Where(r => r.ProgramId == p.Id).ToList(),
                prerequisites.Where(x => x.ProgramId == p.Id).Select(x => (x.PrerequisiteProgramId, x.Title)).ToList()))
            .ToList();
    }

    // ====================================================================
    // SMALL THINGS
    // ====================================================================

    private static OrientationTriggerRunResultDto Empty(string label) => new() { Trigger = label, AsOf = Today };

    private OrientationTriggerRunResultDto Failed(string label, Exception ex, string what, Guid id)
    {
        // Best-effort by contract: the host's own work has committed and must be reported as done.
        // Detach whatever this evaluation staged, so the host's next save does not trip over it.
        _logger.LogError(ex, "Orientation trigger ({Trigger}) failed for " + what + "; the nightly sweep will retry.", label, id);
        _unitOfWork.ClearTrackedChanges();
        var result = Empty(label);
        result.Error = ex.Message;
        return result;
    }

    private static void Merge(OrientationTriggerRunResultDto into, OrientationTriggerRunResultDto from)
    {
        into.ProgramsEvaluated += from.ProgramsEvaluated;
        into.RulesEvaluated += from.RulesEvaluated;
        into.Enrolled += from.Enrolled;
        into.AlreadyEnrolled += from.AlreadyEnrolled;
        into.Excluded += from.Excluded;
        into.WaitingOnPrerequisite += from.WaitingOnPrerequisite;
        into.Renewed += from.Renewed;
        into.LeftAudience += from.LeftAudience;
        foreach (var e in from.Enrolments)
            if (into.Enrolments.Count < MaxListedEnrolments) into.Enrolments.Add(e);
    }

    private static string TriggerLabel(OrientationEnrollmentTrigger trigger) => trigger switch
    {
        OrientationEnrollmentTrigger.OnHire => "Hire",
        OrientationEnrollmentTrigger.OnTransfer => "Transfer",
        OrientationEnrollmentTrigger.OnPromotion => "Promotion",
        OrientationEnrollmentTrigger.OnProgramPublish => "Publish",
        OrientationEnrollmentTrigger.Scheduled => "Scheduled",
        _ => "Manual",
    };

    private static string PopulationLabel(OrientationAudiencePopulation population) => population switch
    {
        OrientationAudiencePopulation.NewHires => "New hires",
        OrientationAudiencePopulation.Management => "Management",
        OrientationAudiencePopulation.Contractors => "Contractors",
        _ => "Anyone",
    };

    private static string DescribeRule(
        HrAudienceTargetType type, Guid? targetId, OrientationAudiencePopulation population, IReadOnlyDictionary<Guid, string> names)
    {
        var where = HrAudienceTargets.Describe(type, targetId, names);
        return population == OrientationAudiencePopulation.Anyone
            ? where
            : type == HrAudienceTargetType.AllEmployees
                ? PopulationLabel(population)
                : $"{PopulationLabel(population)} — {where}";
    }

    private sealed record ProgrammeInfo(
        Guid Id, string Code, string Title, OrientationProgramStatus Status, int? CompletionDeadlineDays,
        bool IsRecurring, OrientationRecurrenceFrequency? RecurrenceFrequency,
        DateTime? EffectiveFrom, DateTime? EffectiveTo, bool EnableReminders)
    {
        /// <summary>What an enrolment notice needs of the programme (lane K-b).</summary>
        public OrientationNoticeProgramme Notice => new(Id, Title, EnableReminders);

        public bool IsInEffect(DateOnly today) => OrientationTriggerWindows.IsInEffect(EffectiveFrom, EffectiveTo, today);

        /// <summary>Recurring with a frequency — a flag without a frequency renews nothing.</summary>
        public OrientationRecurrenceFrequency? Recurs => IsRecurring ? RecurrenceFrequency : null;

        /// <summary>Why the programme is not live today, or null when it is.</summary>
        public string? NotLiveBecause(DateOnly today)
        {
            if (Status != OrientationProgramStatus.Active)
                return $"The programme is {Status}; only an active programme's rules fire.";
            if (EffectiveFrom is { } starts && DateOnly.FromDateTime(starts) > today)
                return $"The programme is not in effect until {DateOnly.FromDateTime(starts):d MMM yyyy}; its rules fire from then.";
            if (EffectiveTo is { } ends && DateOnly.FromDateTime(ends) < today)
                return $"The programme's effective period ended on {DateOnly.FromDateTime(ends):d MMM yyyy}; it enrols nobody after that.";
            return null;
        }
    }

    private sealed record ProgrammeRules(
        ProgrammeInfo Program,
        List<OrientationAudienceRule> Rules,
        List<(Guid Id, string Title)> Prerequisites);

    /// <summary>Sets computed once per run — an org tree and a workforce do not change mid-sweep.</summary>
    private sealed class AudienceCache
    {
        public Dictionary<(HrAudienceTargetType, Guid?), HashSet<Guid>> Axis { get; } = new();
        public Dictionary<OrientationAudiencePopulation, HashSet<Guid>> Populations { get; } = new();
        public Dictionary<Guid, HashSet<Guid>> Enrolled { get; } = new();
        public Dictionary<Guid, HashSet<Guid>> Completed { get; } = new();
    }

    private sealed class RunState(Guid tenantId, DateOnly today, bool preview, Guid? actingUserId)
    {
        public Guid TenantId { get; } = tenantId;
        public DateOnly Today { get; } = today;
        public bool Preview { get; } = preview;
        public Guid? ActingUserId { get; } = actingUserId;
        public AudienceCache Cache { get; } = new();

        /// <summary>What this run has already staged — so a person reached by a hire rule and a
        /// scheduled rule of one programme in the same sweep is enrolled once, preview or not.</summary>
        public HashSet<(Guid ProgramId, Guid EmployeeId)> StagedThisRun { get; } = new();
    }
}
