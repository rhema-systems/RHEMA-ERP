using System.Collections.Concurrent;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Projects payroll's component master into the HR pay-component table. See
/// <see cref="IPayComponentProjectionService"/> for why this is a pull, not a push.
///
/// Mapping:
///   PayrollComponent.Code/Name        -> PayComponent.Code/Name
///   PayrollComponent.ComponentType    -> PayComponent.ComponentType   (see MapComponentType)
///   PayrollComponent.CalculationType  -> PayComponent.CalculationBasis
///   PayrollComponent.Amount | Rate    -> PayComponent.DefaultAmount   (whichever the basis uses)
///   PayrollComponent.Taxable          -> PayComponent.IsTaxable
///   PayrollComponent.IncludeInGross   -> (seeds AffectsGrossPay on create only)
///   PayrollComponent.IsActive         -> PayComponent.IsActive
///
/// Payroll rows are read with <c>AsNoTracking</c> and never written.
/// </summary>
public class PayComponentProjectionService : IPayComponentProjectionService
{
    /// <summary>
    /// Provenance marker written into <see cref="PayComponent.Description"/> so the projection can
    /// tell its own rows apart from any component HR defined before this bridge existed. Only
    /// marked rows are ever updated or deactivated; anything else is left alone, which matters
    /// because <c>LeaveTypeAllowance</c> and <c>BenefitPolicy</c> hold live foreign keys to them.
    /// </summary>
    private const string ProjectionMarker = "Defined in Payroll";

    /// <summary>
    /// Effective-from stamped on a newly mirrored component. Payroll models no effective dating,
    /// so anything later than "always" would silently exclude the component from historical
    /// emolument and encashment calculations, which filter on this window. HR can narrow it
    /// afterwards through the HR-attributes endpoint.
    /// </summary>
    private static readonly DateTime OpenEffectiveFrom = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Last payroll-side fingerprint successfully projected, per tenant. Process-local by design:
    /// on a cold start the first read performs a full reconcile, which is idempotent, so a lost
    /// cache costs one extra pass and never produces wrong data.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, string> LastProjectedFingerprint = new();

    /// <summary>When the fingerprint was last computed, per tenant. Mirrors the salary bridge.</summary>
    private static readonly ConcurrentDictionary<Guid, DateTime> LastCheckedAtUtc = new();

    private static readonly TimeSpan CheckDebounce = TimeSpan.FromSeconds(5);

    private readonly IGenericRepository<PayrollComponent> _payrollComponentRepository;
    private readonly IGenericRepository<PayComponent> _payComponentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PayComponentProjectionService> _logger;

    public PayComponentProjectionService(
        IGenericRepository<PayrollComponent> payrollComponentRepository,
        IGenericRepository<PayComponent> payComponentRepository,
        IUnitOfWork unitOfWork,
        ILogger<PayComponentProjectionService> logger)
    {
        _payrollComponentRepository = payrollComponentRepository;
        _payComponentRepository = payComponentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PayComponentProjectionResultDto> EnsureCurrentAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return new PayComponentProjectionResultDto { SkippedAsUnchanged = true };

        var unchanged = new PayComponentProjectionResultDto { SkippedAsUnchanged = true };
        var now = DateTime.UtcNow;

        if (LastCheckedAtUtc.TryGetValue(tenantId, out var lastChecked) && now - lastChecked < CheckDebounce)
            return unchanged;

        var fingerprint = await ComputePayrollFingerprintAsync(tenantId, cancellationToken);
        LastCheckedAtUtc[tenantId] = now;

        if (LastProjectedFingerprint.TryGetValue(tenantId, out var previous) && previous == fingerprint)
            return unchanged;

        var result = await ReconcileAsync(tenantId, cancellationToken);
        LastProjectedFingerprint[tenantId] = fingerprint;
        return result;
    }

    public async Task<PayComponentProjectionResultDto> ReconcileAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant is required to project pay components.");

        var result = new PayComponentProjectionResultDto();

        var payrollComponents = await _payrollComponentRepository
            .GetQueryable(c => c.TenantId == tenantId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Resolve each payroll component to a stable mirror code. Payroll does not enforce
        // uniqueness on Code, so collisions are reported and skipped rather than one silently
        // overwriting the other.
        var sourceByCode = new Dictionary<string, PayrollComponent>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in payrollComponents.OrderBy(c => c.Code).ThenBy(c => c.Id))
        {
            var mapped = MapComponentType(source.ComponentType);
            if (mapped is null)
            {
                // Employer contributions are employer cost, not something the employee is paid or
                // deducted, so they have no place in HR emoluments and no HR enum member to hold
                // them. Skipping is deliberate — mapping them to Deduction would overstate what
                // comes off the employee's payslip.
                result.Warnings.Add(
                    $"Payroll component '{source.Name}' ({source.Code}) is an employer contribution, " +
                    "which HR emoluments do not model, and was skipped.");
                continue;
            }

            var code = string.IsNullOrWhiteSpace(source.Code) ? source.Id.ToString("N")[..8] : source.Code.Trim();

            if (sourceByCode.TryGetValue(code, out var existing))
            {
                result.Warnings.Add(
                    $"Payroll component '{source.Name}' ({source.Id}) shares code '{code}' with " +
                    $"'{existing.Name}' ({existing.Id}) and was skipped. Give them distinct codes in payroll.");
                continue;
            }

            sourceByCode[code] = source;
        }

        var mirrors = await _payComponentRepository
            .GetQueryable(c => c.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var mirrorByCode = new Dictionary<string, PayComponent>(StringComparer.OrdinalIgnoreCase);
        foreach (var mirror in mirrors)
            mirrorByCode.TryAdd(mirror.Code, mirror);

        var now = DateTime.UtcNow;
        var touched = new HashSet<Guid>();

        foreach (var (code, source) in sourceByCode)
        {
            var componentType = MapComponentType(source.ComponentType)!.Value;
            var basis = MapCalculationBasis(source.CalculationType);
            var defaultAmount = ResolveDefaultAmount(source, basis);

            if (!mirrorByCode.TryGetValue(code, out var mirror))
            {
                mirror = new PayComponent
                {
                    TenantId = tenantId,
                    Code = code,
                    CreatedAt = now,
                    // HR-owned fields, seeded once and never touched again by the projection.
                    EffectiveFrom = OpenEffectiveFrom,
                    EffectiveTo = null,
                    IsPensionable = false,
                    StatutoryTreatment = TaxTreatmentType.PAYE,
                    AffectsGrossPay = source.IncludeInGross,
                };

                ApplyPayrollOwnedFields(mirror, source, componentType, basis, defaultAmount);
                await _payComponentRepository.AddAsync(mirror);
                result.Created++;
                touched.Add(mirror.Id);
                continue;
            }

            // Never adopt a component HR defined itself — it may be referenced by a leave-type
            // allowance or a benefit policy that assumes HR-authored values.
            if (!IsProjectionOwned(mirror))
            {
                result.Warnings.Add(
                    $"HR already defines a pay component with code '{code}', so the payroll component " +
                    $"'{source.Name}' was not mirrored. Rename one of them to resolve the clash.");
                continue;
            }

            touched.Add(mirror.Id);

            if (HasPayrollOwnedChanges(mirror, source, componentType, basis, defaultAmount))
            {
                ApplyPayrollOwnedFields(mirror, source, componentType, basis, defaultAmount);
                mirror.UpdatedAt = now;
                await _payComponentRepository.UpdateAsync(mirror);
                result.Updated++;
            }
        }

        // Components withdrawn from payroll are deactivated, never deleted: HR foreign keys point
        // at them and deleting would break live rows.
        foreach (var mirror in mirrors)
        {
            if (touched.Contains(mirror.Id) || !IsProjectionOwned(mirror) || !mirror.IsActive)
                continue;

            mirror.IsActive = false;
            mirror.UpdatedAt = now;
            await _payComponentRepository.UpdateAsync(mirror);
            result.Deactivated++;
        }

        if (result.Created > 0 || result.Updated > 0 || result.Deactivated > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Pay component projection for tenant {TenantId}: {Created} created, {Updated} updated, {Deactivated} deactivated.",
                tenantId, result.Created, result.Updated, result.Deactivated);
        }

        foreach (var warning in result.Warnings)
            _logger.LogWarning("Pay component projection: {Warning}", warning);

        return result;
    }

    /// <summary>
    /// A row belongs to the projection only if it carries the provenance marker. Anything else is
    /// HR's own and is never updated or deactivated here.
    /// </summary>
    private static bool IsProjectionOwned(PayComponent component) =>
        component.Description is not null &&
        component.Description.Contains(ProjectionMarker, StringComparison.OrdinalIgnoreCase);

    private static void ApplyPayrollOwnedFields(
        PayComponent mirror,
        PayrollComponent source,
        PayComponentType componentType,
        PayComponentCalculationBasis basis,
        decimal? defaultAmount)
    {
        mirror.Name = source.Name;
        mirror.Description = ProjectionMarker;
        mirror.ComponentType = componentType;
        mirror.CalculationBasis = basis;
        mirror.DefaultAmount = defaultAmount;
        mirror.IsTaxable = source.Taxable;
        mirror.IsActive = source.IsActive;
    }

    private static bool HasPayrollOwnedChanges(
        PayComponent mirror,
        PayrollComponent source,
        PayComponentType componentType,
        PayComponentCalculationBasis basis,
        decimal? defaultAmount) =>
        mirror.Name != source.Name ||
        mirror.ComponentType != componentType ||
        mirror.CalculationBasis != basis ||
        mirror.DefaultAmount != defaultAmount ||
        mirror.IsTaxable != source.Taxable ||
        mirror.IsActive != source.IsActive;

    /// <summary>
    /// Payroll has five component types, HR has three. Contributions collapse onto the HR meaning
    /// that matches what reaches the employee: an employee contribution is money off the payslip,
    /// so it maps to Deduction; an employer contribution is not, so it maps to nothing and the
    /// caller skips it.
    /// </summary>
    private static PayComponentType? MapComponentType(PayrollComponentType type) => type switch
    {
        PayrollComponentType.Allowance => PayComponentType.Allowance,
        PayrollComponentType.Deduction => PayComponentType.Deduction,
        PayrollComponentType.Benefit => PayComponentType.BenefitInKindNotional,
        PayrollComponentType.EmployeeContribution => PayComponentType.Deduction,
        PayrollComponentType.EmployerContribution => null,
        _ => PayComponentType.Allowance,
    };

    private static PayComponentCalculationBasis MapCalculationBasis(PayrollCalculationType type) => type switch
    {
        PayrollCalculationType.PercentageOfBasic => PayComponentCalculationBasis.PercentageOfBasic,
        _ => PayComponentCalculationBasis.FixedAmount,
    };

    /// <summary>
    /// Payroll keeps the fixed figure in <c>Amount</c> and the percentage in <c>Rate</c>; HR has a
    /// single field whose meaning follows the basis, so the right one is picked here.
    /// </summary>
    private static decimal? ResolveDefaultAmount(PayrollComponent source, PayComponentCalculationBasis basis) =>
        basis == PayComponentCalculationBasis.PercentageOfBasic
            ? (source.Rate == 0 ? null : source.Rate)
            : (source.Amount == 0 ? null : source.Amount);

    /// <summary>
    /// Cheap change-detection over the payroll-owned fields only. Deliberately excludes anything
    /// HR owns, so an HR edit never triggers a pointless reconcile.
    /// </summary>
    private async Task<string> ComputePayrollFingerprintAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _payrollComponentRepository
            .GetQueryable(c => c.TenantId == tenantId)
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new
            {
                c.Id, c.Code, c.Name, c.ComponentType, c.CalculationType,
                c.Amount, c.Rate, c.Taxable, c.IsActive,
            })
            .ToListAsync(cancellationToken);

        return string.Join('|', rows.Select(r =>
            $"{r.Id:N}:{r.Code}:{r.Name}:{(int)r.ComponentType}:{(int)r.CalculationType}:{r.Amount}:{r.Rate}:{r.Taxable}:{r.IsActive}"));
    }
}
