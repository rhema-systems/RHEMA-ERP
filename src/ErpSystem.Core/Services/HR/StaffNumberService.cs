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
        // does not match the rule's shape simply carries no counter to learn from, and a data load
        // must not fail because a legacy number was formatted differently.
        var trailing = ExtractTrailingSequence(supplied, rule);
        if (trailing is not { } value) return supplied;

        var year = DateTime.UtcNow.Year;
        for (var guard = 0; guard < 10_000; guard++)
        {
            var next = await _numberSequence.NextAsync(
                rule.SequenceKey, rule.IncludeYear ? year : null, cancellationToken);
            if (next > value) return supplied;
        }

        _logger.LogWarning(
            "Staff number sequence '{Key}' could not be advanced past imported value {Value} within 10,000 steps; "
            + "the counter may need seeding by hand.", rule.SequenceKey, value);
        return supplied;
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

    /// <summary>
    /// Reads the counter out of an existing number, so an import can advance past it.
    /// </summary>
    /// <remarks>
    /// Takes the trailing digit run, after any suffix the rule prints. Deliberately forgiving: this
    /// runs over legacy data that predates the rule, and returning null simply means "nothing to
    /// learn from this one".
    /// </remarks>
    private static long? ExtractTrailingSequence(string number, StaffNumberFormat rule)
    {
        var text = number;
        if (!string.IsNullOrEmpty(rule.Suffix) && text.EndsWith(rule.Suffix, StringComparison.OrdinalIgnoreCase))
            text = text[..^rule.Suffix.Length];
        if (!string.IsNullOrEmpty(rule.Separator))
            text = text.TrimEnd(rule.Separator.ToCharArray());

        var end = text.Length;
        var start = end;
        while (start > 0 && char.IsDigit(text[start - 1])) start--;
        if (start == end) return null;

        return long.TryParse(text[start..end], out var value) ? value : null;
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
