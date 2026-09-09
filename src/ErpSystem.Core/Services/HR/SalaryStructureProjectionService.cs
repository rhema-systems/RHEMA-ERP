using System.Collections.Concurrent;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Projects payroll's salary structure into the HR salary tables. See
/// <see cref="ISalaryStructureProjectionService"/> for why this is a pull, not a push.
///
/// Mapping:
///   PayrollGrade       -> SalaryGrade   (Code = payroll GradeId, Name = GradeName)
///   (synthesized)      -> SalaryLevel   (one per grade; HR is 3-tier, payroll is 2-tier)
///   PayrollGradeNotch  -> SalaryNotch   (NotchNumber parsed from Notch, SalaryAmount = Value)
///
/// Payroll rows are read with <c>AsNoTracking</c> and never written.
/// </summary>
public class SalaryStructureProjectionService : ISalaryStructureProjectionService
{
    /// <summary>
    /// Provenance marker written into <see cref="SalaryGrade.Description"/> so the projection can tell
    /// its own rows apart from grades that predate it (the TDC organogram seeder creates M1–M5/S1–S3,
    /// and positions already reference them). Only marked rows are ever deactivated — anything the
    /// projection did not create is left untouched.
    /// </summary>
    private const string ProjectionMarker = "Defined in Payroll";

    /// <summary>
    /// Last payroll-side fingerprint successfully projected, per tenant. Process-local by design: on a
    /// cold start the first read performs a full reconcile, which is idempotent, so a lost cache costs
    /// one extra pass and never produces wrong data.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, string> LastProjectedFingerprint = new();

    /// <summary>
    /// When the payroll fingerprint was last computed, per tenant. A single screen typically cascades
    /// grade -> levels -> notches, so without this every picker load would re-run the fingerprint query
    /// several times over.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, DateTime> LastCheckedAtUtc = new();

    private static readonly TimeSpan CheckDebounce = TimeSpan.FromSeconds(5);

    private readonly IGenericRepository<PayrollGrade> _payrollGradeRepository;
    private readonly IGenericRepository<PayrollGradeNotch> _payrollGradeNotchRepository;
    private readonly IGenericRepository<SalaryGrade> _salaryGradeRepository;
    private readonly IGenericRepository<SalaryLevel> _salaryLevelRepository;
    private readonly IGenericRepository<SalaryNotch> _salaryNotchRepository;
    private readonly IUnitOfWork _unitOfWork;
    // Whether this tenant's structure is payroll's to project at all (lane G).
    private readonly ICompanyHrPolicySettingsService _policySettings;
    private readonly ILogger<SalaryStructureProjectionService> _logger;

    public SalaryStructureProjectionService(
        IGenericRepository<PayrollGrade> payrollGradeRepository,
        IGenericRepository<PayrollGradeNotch> payrollGradeNotchRepository,
        IGenericRepository<SalaryGrade> salaryGradeRepository,
        IGenericRepository<SalaryLevel> salaryLevelRepository,
        IGenericRepository<SalaryNotch> salaryNotchRepository,
        IUnitOfWork unitOfWork,
        ICompanyHrPolicySettingsService policySettings,
        ILogger<SalaryStructureProjectionService> logger)
    {
        _policySettings = policySettings;
        _payrollGradeRepository = payrollGradeRepository;
        _payrollGradeNotchRepository = payrollGradeNotchRepository;
        _salaryGradeRepository = salaryGradeRepository;
        _salaryLevelRepository = salaryLevelRepository;
        _salaryNotchRepository = salaryNotchRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SalaryStructureProjectionResult> EnsureCurrentAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return new SalaryStructureProjectionResult { SkippedAsUnchanged = true };
        }

        var unchanged = new SalaryStructureProjectionResult { SkippedAsUnchanged = true };
        var now = DateTime.UtcNow;

        // ⚠ Checked BEFORE the debounce and the fingerprint. A tenant that maintains its own
        // structure must never be projected over, not even by a read that happens to arrive first.
        if (await IsHrMasteredAsync(tenantId, cancellationToken))
            return unchanged;

        if (LastCheckedAtUtc.TryGetValue(tenantId, out var lastChecked) && now - lastChecked < CheckDebounce)
        {
            return unchanged;
        }

        var fingerprint = await ComputePayrollFingerprintAsync(tenantId, cancellationToken);
        LastCheckedAtUtc[tenantId] = now;

        if (LastProjectedFingerprint.TryGetValue(tenantId, out var previous) && previous == fingerprint)
        {
            return unchanged;
        }

        var result = await ReconcileAsync(tenantId, cancellationToken);
        LastProjectedFingerprint[tenantId] = fingerprint;
        return result;
    }

    public async Task<SalaryStructureProjectionResult> ReconcileAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("A tenant is required to project the salary structure.");
        }

        var warnings = new List<string>();

        // The explicit sync arrives here directly. Refuse rather than skip: somebody pressed a
        // button expecting payroll's rows to land, and silently doing nothing would look like success.
        if (await IsHrMasteredAsync(tenantId, cancellationToken))
            throw new InvalidOperationException(
                "This organisation maintains its salary structure in HR, so there is nothing to sync from "
                + "Payroll. Change the salary structure source under HR policy settings to make Payroll the master again.");

        var payrollGrades = await _payrollGradeRepository
            .GetQueryable(g => g.TenantId == tenantId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var payrollNotches = await _payrollGradeNotchRepository
            .GetQueryable(n => n.TenantId == tenantId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var notchesByGradeId = payrollNotches
            .GroupBy(n => n.PayrollGradeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Resolve each payroll grade to a stable mirror code. Payroll does not enforce uniqueness on
        // GradeId, so collisions are reported and skipped rather than silently overwriting one another.
        var sourceByCode = new Dictionary<string, PayrollGrade>(StringComparer.OrdinalIgnoreCase);
        foreach (var payrollGrade in payrollGrades.OrderBy(g => g.OrderField ?? int.MaxValue).ThenBy(g => g.Id))
        {
            var code = ResolveGradeCode(payrollGrade);
            if (sourceByCode.TryGetValue(code, out var existing))
            {
                warnings.Add(
                    $"Payroll grade '{payrollGrade.GradeName}' ({payrollGrade.Id}) shares code '{code}' with " +
                    $"'{existing.GradeName}' ({existing.Id}) and was skipped. Give the grades distinct codes in payroll.");
                continue;
            }

            sourceByCode[code] = payrollGrade;
        }

        var mirrorGrades = await _salaryGradeRepository
            .GetQueryable(g => g.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var mirrorGradeIds = mirrorGrades.Select(g => g.Id).ToList();

        var mirrorLevels = await _salaryLevelRepository
            .GetQueryable(l => l.TenantId == tenantId && mirrorGradeIds.Contains(l.SalaryGradeId))
            .ToListAsync(cancellationToken);

        var mirrorLevelIds = mirrorLevels.Select(l => l.Id).ToList();

        var mirrorNotches = await _salaryNotchRepository
            .GetQueryable(n => n.TenantId == tenantId && mirrorLevelIds.Contains(n.SalaryLevelId))
            .ToListAsync(cancellationToken);

        var mirrorGradeByCode = new Dictionary<string, SalaryGrade>(StringComparer.OrdinalIgnoreCase);
        foreach (var grade in mirrorGrades)
        {
            mirrorGradeByCode.TryAdd(grade.Code, grade);
        }

        var levelsByGradeId = mirrorLevels
            .GroupBy(l => l.SalaryGradeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Sequence).ToList());

        var notchesByLevelId = mirrorNotches
            .GroupBy(n => n.SalaryLevelId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var gradesCreated = 0;
        var gradesUpdated = 0;
        var levelsCreated = 0;
        var levelsUpdated = 0;
        var notchesCreated = 0;
        var notchesUpdated = 0;
        var notchesDeactivated = 0;

        var now = DateTime.UtcNow;
        var touchedGradeIds = new HashSet<Guid>();

        foreach (var (code, source) in sourceByCode)
        {
            var sourceNotches = notchesByGradeId.TryGetValue(source.Id, out var found)
                ? found
                : new List<PayrollGradeNotch>();

            var (minSalary, maxSalary) = ResolveGradeBand(source, sourceNotches);

            // --- Grade ---------------------------------------------------------------------------
            if (!mirrorGradeByCode.TryGetValue(code, out var grade))
            {
                grade = new SalaryGrade
                {
                    TenantId = tenantId,
                    Code = code,
                    CreatedAt = now
                };

                ApplyGrade(grade, source, minSalary, maxSalary);
                await _salaryGradeRepository.AddAsync(grade);
                mirrorGradeByCode[code] = grade;
                gradesCreated++;
            }
            else if (ApplyGrade(grade, source, minSalary, maxSalary))
            {
                grade.UpdatedAt = now;
                await _salaryGradeRepository.UpdateAsync(grade);
                gradesUpdated++;
            }

            touchedGradeIds.Add(grade.Id);

            // --- Level (synthesized: HR is 3-tier, payroll is 2-tier) ------------------------------
            var existingLevels = levelsByGradeId.TryGetValue(grade.Id, out var levels) ? levels : new List<SalaryLevel>();
            var level = existingLevels.FirstOrDefault();

            if (level == null)
            {
                level = new SalaryLevel
                {
                    TenantId = tenantId,
                    SalaryGradeId = grade.Id,
                    Sequence = 1,
                    CreatedAt = now
                };

                ApplyLevel(level, code, source, minSalary, maxSalary);
                await _salaryLevelRepository.AddAsync(level);
                levelsByGradeId[grade.Id] = new List<SalaryLevel> { level };
                levelsCreated++;
            }
            else if (ApplyLevel(level, code, source, minSalary, maxSalary))
            {
                level.UpdatedAt = now;
                await _salaryLevelRepository.UpdateAsync(level);
                levelsUpdated++;
            }

            // --- Notches -------------------------------------------------------------------------
            var existingNotches = notchesByLevelId.TryGetValue(level.Id, out var notches)
                ? notches
                : new List<SalaryNotch>();

            var existingByNumber = new Dictionary<int, SalaryNotch>();
            foreach (var notch in existingNotches)
            {
                existingByNumber.TryAdd(notch.NotchNumber, notch);
            }

            var seenNotchNumbers = new HashSet<int>();

            foreach (var sourceNotch in sourceNotches.OrderBy(n => n.OrderField ?? int.MaxValue).ThenBy(n => n.Notch))
            {
                var notchNumber = ResolveNotchNumber(sourceNotch);
                if (notchNumber == null)
                {
                    warnings.Add(
                        $"Payroll notch '{sourceNotch.Notch}' on grade '{source.GradeName}' has no numeric " +
                        "notch or order and was skipped.");
                    continue;
                }

                if (!seenNotchNumbers.Add(notchNumber.Value))
                {
                    warnings.Add(
                        $"Payroll grade '{source.GradeName}' has more than one notch numbered " +
                        $"{notchNumber.Value}; only the first was projected.");
                    continue;
                }

                var amount = sourceNotch.Value ?? 0m;

                if (!existingByNumber.TryGetValue(notchNumber.Value, out var mirrorNotch))
                {
                    mirrorNotch = new SalaryNotch
                    {
                        TenantId = tenantId,
                        SalaryLevelId = level.Id,
                        NotchNumber = notchNumber.Value,
                        SalaryAmount = amount,
                        IsActive = true,
                        CreatedAt = now
                    };

                    await _salaryNotchRepository.AddAsync(mirrorNotch);
                    notchesCreated++;
                }
                else if (mirrorNotch.SalaryAmount != amount || !mirrorNotch.IsActive)
                {
                    mirrorNotch.SalaryAmount = amount;
                    mirrorNotch.IsActive = true;
                    mirrorNotch.UpdatedAt = now;
                    await _salaryNotchRepository.UpdateAsync(mirrorNotch);
                    notchesUpdated++;
                }
            }

            // Notches withdrawn in payroll are deactivated, never deleted — HR rows may be referenced
            // by salary assignments, movements and offers.
            foreach (var orphan in existingNotches.Where(n => n.IsActive && !seenNotchNumbers.Contains(n.NotchNumber)))
            {
                orphan.IsActive = false;
                orphan.UpdatedAt = now;
                await _salaryNotchRepository.UpdateAsync(orphan);
                notchesDeactivated++;
            }
        }

        // Grades withdrawn in payroll are deactivated for the same reason. Only rows this projection
        // owns are touched: grades that predate it (seeded TDC grades, for example) are left alone, so
        // enabling the bridge never silently deactivates existing structure.
        var gradesDeactivated = 0;
        foreach (var orphan in mirrorGrades.Where(g => g.IsActive && !touchedGradeIds.Contains(g.Id) && IsProjected(g)))
        {
            orphan.IsActive = false;
            orphan.UpdatedAt = now;
            await _salaryGradeRepository.UpdateAsync(orphan);
            gradesDeactivated++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var result = new SalaryStructureProjectionResult
        {
            GradesCreated = gradesCreated,
            GradesUpdated = gradesUpdated,
            GradesDeactivated = gradesDeactivated,
            LevelsCreated = levelsCreated,
            LevelsUpdated = levelsUpdated,
            NotchesCreated = notchesCreated,
            NotchesUpdated = notchesUpdated,
            NotchesDeactivated = notchesDeactivated,
            Warnings = warnings
        };

        if (result.ChangedAnything)
        {
            _logger.LogInformation(
                "Salary structure projected from payroll for tenant {TenantId}: " +
                "grades +{GradesCreated}/~{GradesUpdated}/-{GradesDeactivated}, " +
                "notches +{NotchesCreated}/~{NotchesUpdated}/-{NotchesDeactivated}",
                tenantId, gradesCreated, gradesUpdated, gradesDeactivated,
                notchesCreated, notchesUpdated, notchesDeactivated);
        }

        foreach (var warning in warnings)
        {
            _logger.LogWarning("Salary structure projection ({TenantId}): {Warning}", tenantId, warning);
        }

        return result;
    }

    private async Task<bool> IsHrMasteredAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var settings = await _policySettings.GetForTenantAsync(tenantId, cancellationToken);
        return settings.SalaryStructureSource == SalaryStructureSource.Hr;
    }

    /// <summary>
    /// Cheap change detector: row counts plus the latest timestamp on each payroll table. Enough to
    /// notice inserts, updates and soft deletes without reading the rows themselves.
    /// </summary>
    private async Task<string> ComputePayrollFingerprintAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var grades = _payrollGradeRepository.GetQueryable(g => g.TenantId == tenantId);
        var notches = _payrollGradeNotchRepository.GetQueryable(n => n.TenantId == tenantId);

        var gradeCount = await grades.CountAsync(cancellationToken);
        var gradeStamp = await grades.MaxAsync(g => (DateTime?)(g.UpdatedAt ?? g.CreatedAt), cancellationToken);
        var notchCount = await notches.CountAsync(cancellationToken);
        var notchStamp = await notches.MaxAsync(n => (DateTime?)(n.UpdatedAt ?? n.CreatedAt), cancellationToken);

        return string.Join(
            '|',
            gradeCount,
            gradeStamp?.Ticks ?? 0,
            notchCount,
            notchStamp?.Ticks ?? 0);
    }

    /// <summary>Returns true when the mirror row needed changing.</summary>
    private static bool ApplyGrade(SalaryGrade grade, PayrollGrade source, decimal minSalary, decimal maxSalary)
    {
        var name = Truncate(
            string.IsNullOrWhiteSpace(source.GradeName) ? grade.Code : source.GradeName.Trim(),
            100);
        var description = BuildGradeDescription(source);
        var effectiveDate = source.StartDate ?? grade.EffectiveDate;
        if (effectiveDate == default)
        {
            effectiveDate = source.CreatedAt;
        }

        var changed =
            grade.Name != name ||
            grade.Description != description ||
            grade.MinSalary != minSalary ||
            grade.MaxSalary != maxSalary ||
            grade.EffectiveDate != effectiveDate ||
            grade.EndDate != source.EndDate ||
            grade.IsActive != source.IsActive;

        grade.Name = name;
        grade.Description = description;
        grade.MinSalary = minSalary;
        grade.MaxSalary = maxSalary;
        grade.EffectiveDate = effectiveDate;
        grade.EndDate = source.EndDate;
        grade.IsActive = source.IsActive;

        return changed;
    }

    /// <summary>Returns true when the mirror row needed changing.</summary>
    private static bool ApplyLevel(SalaryLevel level, string code, PayrollGrade source, decimal minSalary, decimal maxSalary)
    {
        var name = Truncate(
            string.IsNullOrWhiteSpace(source.GradeName) ? code : source.GradeName.Trim(),
            100);
        var midSalary = source.MidPoint ?? decimal.Round((minSalary + maxSalary) / 2m, 2);
        if (midSalary < minSalary) midSalary = minSalary;
        if (midSalary > maxSalary) midSalary = maxSalary;

        var changed =
            level.Code != code ||
            level.Name != name ||
            level.MinSalary != minSalary ||
            level.MidSalary != midSalary ||
            level.MaxSalary != maxSalary ||
            level.Sequence != 1 ||
            level.IsActive != source.IsActive;

        level.Code = code;
        level.Name = name;
        level.MinSalary = minSalary;
        level.MidSalary = midSalary;
        level.MaxSalary = maxSalary;
        level.Sequence = 1;
        level.IsActive = source.IsActive;

        return changed;
    }

    /// <summary>
    /// Payroll's Min/Max are optional. When absent, derive the band from the grade's own notches so the
    /// mirror stays internally coherent (HR validates notches against the grade band).
    /// </summary>
    private static (decimal Min, decimal Max) ResolveGradeBand(PayrollGrade source, IReadOnlyCollection<PayrollGradeNotch> notches)
    {
        var notchValues = notches
            .Where(n => n.Value.HasValue)
            .Select(n => n.Value!.Value)
            .ToList();

        var min = source.MinValue ?? (notchValues.Count > 0 ? notchValues.Min() : 0m);
        var max = source.MaxValue ?? (notchValues.Count > 0 ? notchValues.Max() : 0m);

        if (notchValues.Count > 0)
        {
            // Never let the band exclude a real notch amount.
            min = Math.Min(min, notchValues.Min());
            max = Math.Max(max, notchValues.Max());
        }

        if (min > max)
        {
            (min, max) = (max, min);
        }

        return (min, max);
    }

    private static string ResolveGradeCode(PayrollGrade source)
    {
        var code = source.GradeId?.Trim();
        if (!string.IsNullOrWhiteSpace(code))
        {
            return Truncate(code, 50);
        }

        // Payroll allows a blank grade code; fall back to something stable and unique per grade.
        return $"PG-{source.Id.ToString("N")[..8].ToUpperInvariant()}";
    }

    /// <summary>
    /// The payroll notch code is HR's only stable notch identity — <c>SalaryNotch</c> has no text field
    /// to carry it. Notches are therefore keyed on the parsed number and are deliberately NOT
    /// renumbered: renumbering would silently repoint existing HR foreign keys at a different amount.
    /// </summary>
    private static int? ResolveNotchNumber(PayrollGradeNotch notch)
    {
        if (int.TryParse(notch.Notch?.Trim(), out var parsed) && parsed > 0)
        {
            return parsed;
        }

        if (notch.OrderField is > 0)
        {
            return notch.OrderField.Value;
        }

        return null;
    }

    private static bool IsProjected(SalaryGrade grade)
        => grade.Description?.StartsWith(ProjectionMarker, StringComparison.Ordinal) == true;

    private static string BuildGradeDescription(PayrollGrade source)
    {
        var parts = new List<string> { ProjectionMarker };

        if (!string.IsNullOrWhiteSpace(source.GradeType))
        {
            parts.Add(source.GradeType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(source.CurrencyCode))
        {
            parts.Add(source.CurrencyCode.Trim());
        }

        return string.Join(" · ", parts);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
