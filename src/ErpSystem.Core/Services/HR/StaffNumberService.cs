using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Decides what staff number an employee gets, from the tenant's own numbering rules.
/// </summary>
/// <remarks>
/// <para>Replaces a format compiled into <c>EmployeeRepository.GenerateEmployeeNumberAsync</c> as
/// <c>{year}{sequence:D4}</c>, which was neither configurable nor atomic — it read the maximum live
/// number and added one, so a soft-deleted employee's number was reissued to the next hire and
/// rejected by the unfiltered unique index as a 500 on employee creation.</para>
///
/// <para><b>There is no global auto/manual switch.</b> The absence of a rule for a register means
/// manual: HR types the number. A company-level toggle beside the per-register flag would be a
/// second source of truth for one fact, and the two would eventually disagree.</para>
/// </remarks>
public interface IStaffNumberService
{
    /// <summary>
    /// Settles the staff number for a new employee, honouring the register's rule.
    /// </summary>
    /// <param name="employmentType">Which register the employee is entering by.</param>
    /// <param name="suppliedNumber">What the caller sent, if anything.</param>
    Task<string> ResolveForCreateAsync(
        EmploymentType employmentType, string? suppliedNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a staff number exactly as given, and advances the register's counter past it.
    /// </summary>
    /// <remarks>
    /// ⚠ For DATA LOADS only, and it exists so seeding is not a step somebody remembers. Loading an
    /// existing employee is not the same act as creating a new one: the number already exists and is
    /// not ours to issue. But a counter left at zero afterwards would hand <c>00001</c> to the first
    /// real hire while the loaded staff sit in the ten-thousands, so the load advances it.
    /// </remarks>
    Task<string> AcceptImportedAsync(
        EmploymentType employmentType, string suppliedNumber, CancellationToken cancellationToken = default);

    /// <summary>The rule that governs a register, or <c>null</c> when the register is manual.</summary>
    Task<StaffNumberFormat?> GetRuleAsync(
        EmploymentType employmentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Where a rule's counter stands against the numbers already in the register, without moving it.
    /// </summary>
    /// <remarks>
    /// ⚠ The question this answers is <b>"will the next hire be given a number somebody already
    /// has?"</b> A counter only knows about numbers it issued; every number that arrived by data
    /// load, seed or migration is invisible to it. On a freshly loaded tenant the counter stands at
    /// zero while the register holds thousands of numbers, and nothing says so until a create fails
    /// on the unique index.
    /// </remarks>
    Task<StaffNumberCounterStateDto> InspectCounterAsync(Guid formatId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a rule's counter past every number already in the register that the rule could issue.
    /// </summary>
    /// <remarks>
    /// The bulk form of <see cref="AcceptImportedAsync"/>, and the one that copes with a load that
    /// did not come through this service at all — SQL, a seeder, a restored database. Idempotent,
    /// and forward-only.
    /// </remarks>
    Task<StaffNumberCounterStateDto> ReconcileCounterAsync(Guid formatId, CancellationToken cancellationToken = default);
}

public class StaffNumberService : IStaffNumberService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INumberSequenceService _numberSequence;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<StaffNumberService> _logger;

    public StaffNumberService(
        IUnitOfWork unitOfWork,
        INumberSequenceService numberSequence,
        ICurrentUserService currentUser,
        ILogger<StaffNumberService> logger)
    {
        _unitOfWork = unitOfWork;
        _numberSequence = numberSequence;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid TenantId =>
        _currentUser.TenantId ?? throw new InvalidOperationException("No tenant is associated with the current user.");

    /// <summary>
    /// The rule for a register: its own, else the tenant's default, else none.
    /// </summary>
    /// <remarks>
    /// ⚠ Only ACTIVE rules are considered. A retired rule must stop governing new hires immediately —
    /// that is what retiring it means — while the rows it already numbered keep their numbers.
    /// </remarks>
    public async Task<StaffNumberFormat?> GetRuleAsync(
        EmploymentType employmentType, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var rules = await _unitOfWork.Repository<StaffNumberFormat>()
            .GetQueryable()
            .Where(r => r.TenantId == tenantId && r.IsActive && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        return rules.FirstOrDefault(r => r.AppliesToEmploymentType == employmentType)
            ?? rules.FirstOrDefault(r => r.AppliesToEmploymentType == null);
    }

    public async Task<string> ResolveForCreateAsync(
        EmploymentType employmentType, string? suppliedNumber, CancellationToken cancellationToken = default)
    {
        var supplied = suppliedNumber?.Trim();
        var rule = await GetRuleAsync(employmentType, cancellationToken);

        // No rule: the register is manual by construction. Nothing is configured, so nothing is
        // invented — the number must come from the caller.
        if (rule is null)
            return RequireSupplied(supplied, employmentType, configured: false);

        if (!rule.AutoGenerate)
            return RequireSupplied(supplied, employmentType, configured: true, rule: rule);

        // ⚠ Auto: a supplied number is REFUSED, not honoured. Accepting one lets a hand-typed value
        // occupy a number the sequence is about to issue, and the collision surfaces later as a
        // unique-index violation on somebody else's create — a defect whose cause is invisible from
        // where it appears. A data load goes through AcceptImportedAsync instead.
        if (!string.IsNullOrWhiteSpace(supplied))
            throw new InvalidOperationException(
                $"Staff numbers for {Describe(employmentType)} are issued by the system, so one cannot be "
                + $"supplied. Remove it and the next number in the '{rule.Name}' series will be used, or "
                + "load existing employees through the import, which keeps the numbers they already have.");

        var year = DateTime.UtcNow.Year;
        var next = await _numberSequence.NextAsync(
            rule.SequenceKey, rule.IncludeYear ? year : null, cancellationToken);

        return rule.Compose(next, year);
    }

    public async Task<string> AcceptImportedAsync(
        EmploymentType employmentType, string suppliedNumber, CancellationToken cancellationToken = default)
    {
        var supplied = suppliedNumber?.Trim();
        if (string.IsNullOrWhiteSpace(supplied))
            throw new InvalidOperationException("An imported employee must carry the staff number they already have.");

        var rule = await GetRuleAsync(employmentType, cancellationToken);
        if (rule is null || !rule.AutoGenerate) return supplied;

        // Advance the counter past what was just loaded, so the first hire after the load does not
        // collide with, or sort below, the staff it imported. Best-effort by design: a number that
        // does not fit the rule's shape carries no counter to learn from, and a data load must not
        // fail because a legacy number was formatted differently.
        var year = DateTime.UtcNow.Year;
        if (!rule.TryReadSequence(supplied, year, out var value)) return supplied;

        await _numberSequence.AdvanceToAtLeastAsync(
            rule.SequenceKey, value, rule.IncludeYear ? year : null, cancellationToken);

        return supplied;
    }

    // ── the counter against the register ────────────────────────────────────

    public Task<StaffNumberCounterStateDto> InspectCounterAsync(
        Guid formatId, CancellationToken cancellationToken = default)
        => ExamineCounterAsync(formatId, reconcile: false, cancellationToken);

    public Task<StaffNumberCounterStateDto> ReconcileCounterAsync(
        Guid formatId, CancellationToken cancellationToken = default)
        => ExamineCounterAsync(formatId, reconcile: true, cancellationToken);

    /// <summary>
    /// Reads the register through one rule's eyes, and optionally moves the counter to match.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Selection is by the SHAPE of the number, not by employment type.</b> The rule
    /// records which register somebody entered by, so a converted employee keeps a number their
    /// current employment type would never produce — partitioning the scan by
    /// <c>Employee.EmploymentType</c> would both miss numbers this rule can reissue and count
    /// numbers it cannot. What matters is exactly "which existing numbers could this rule hand out
    /// again", and that is a question about the string.</para>
    ///
    /// <para>⚠ <b>Soft-deleted employees are included.</b> The unique index on
    /// <c>(TenantId, EmployeeNumber)</c> is unfiltered, so a leaver's number is still taken — the
    /// defect that made employee creation fail for the rest of the year once anybody was deleted.
    /// A watermark that ignored tombstones would walk straight back into it.</para>
    /// </remarks>
    private async Task<StaffNumberCounterStateDto> ExamineCounterAsync(
        Guid formatId, bool reconcile, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var rule = await _unitOfWork.Repository<StaffNumberFormat>()
            .GetQueryable()
            .FirstOrDefaultAsync(r => r.Id == formatId && !r.IsDeleted, cancellationToken);

        if (rule is null || rule.TenantId != tenantId)
            throw new ArgumentException($"Staff number format with ID '{formatId}' not found.");

        var year = DateTime.UtcNow.Year;
        int? bucket = rule.IncludeYear ? year : null;

        // ⚠ GetQueryableIncludingDeleted, and NOT GetQueryable().IgnoreQueryFilters() — which is a
        // no-op here. The generic repository bakes an explicit `Where(e => !e.IsDeleted)` into
        // GetQueryable(), and no filter switch removes a predicate that is written into the query.
        // The first cut of this read used it and silently under-counted tombstones, which is the
        // exact failure the whole read exists to prevent.
        var numbers = await _unitOfWork.Repository<Employee>()
            .GetQueryableIncludingDeleted(e => e.TenantId == tenantId && e.EmployeeNumber != "")
            .Select(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);

        long highest = 0;
        string? highestNumber = null;
        var matched = 0;
        foreach (var number in numbers)
        {
            if (!rule.TryReadSequence(number, year, out var value)) continue;
            matched++;
            if (value <= highest) continue;
            highest = value;
            highestNumber = number.Trim();
        }

        var standsAt = reconcile && highest > 0
            ? await _numberSequence.AdvanceToAtLeastAsync(rule.SequenceKey, highest, bucket, cancellationToken)
            : await _numberSequence.PeekAsync(rule.SequenceKey, bucket, cancellationToken);

        if (reconcile)
            _logger.LogInformation(
                "Staff number counter '{Key}' reconciled for rule {Rule}: {Matched} numbers read, watermark now {Value}.",
                rule.SequenceKey, rule.Name, matched, standsAt);

        var nextNumber = rule.Compose(standsAt + 1, year);
        var taken = new HashSet<string>(
            numbers.Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);

        return new StaffNumberCounterStateDto
        {
            FormatId = rule.Id,
            FormatName = rule.Name,
            SequenceKey = rule.SequenceKey,
            AutoGenerate = rule.AutoGenerate,
            IsActive = rule.IsActive,
            YearBucket = bucket,
            CounterStandsAt = standsAt,
            NextNumber = nextNumber,
            // ⚠ The whole point of the read. A rule can be perfectly configured and still be about
            // to hand out a number somebody is already using.
            NextNumberIsInUse = rule.AutoGenerate && taken.Contains(nextNumber),
            NumbersInRegister = matched,
            NumbersNotMatchingFormat = numbers.Count - matched,
            HighestInRegister = highest,
            HighestNumberInRegister = highestNumber,
            CounterIsBehind = rule.AutoGenerate && highest > standsAt,
        };
    }

    private static string RequireSupplied(
        string? supplied, EmploymentType employmentType, bool configured, StaffNumberFormat? rule = null)
    {
        if (!string.IsNullOrWhiteSpace(supplied)) return supplied!;

        throw new InvalidOperationException(configured
            ? $"A staff number is required for {Describe(employmentType)}: the '{rule!.Name}' series is set to "
              + "be entered by hand rather than issued by the system."
            : $"A staff number is required for {Describe(employmentType)}. No numbering series is configured "
              + "for this register, so the system does not issue one — set one up under staff numbering, "
              + "or enter the number manually.");
    }

    private static string Describe(EmploymentType type) => type switch
    {
        EmploymentType.Permanent => "permanent staff",
        EmploymentType.Contract => "contract staff",
        EmploymentType.Casual => "casual staff",
        EmploymentType.Temporary => "temporary staff",
        _ => $"{type} staff",
    };
}
