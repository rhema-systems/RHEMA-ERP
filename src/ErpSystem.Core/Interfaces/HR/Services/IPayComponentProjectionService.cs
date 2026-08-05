using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Projects payroll's component master into the HR pay-component table.
///
/// HR pulls; payroll never pushes. Payroll is another team's module and the integration rule is
/// that HR code reaches into payroll data rather than a hook being added to payroll's save path,
/// so a component created in payroll surfaces in HR on the next HR read, not instantly. That
/// latency is accepted.
///
/// Ownership is split, unlike the salary-structure mirror. The projection owns everything payroll
/// models — code, name, type, calculation basis, default amount, taxability, active flag — and
/// overwrites it on every pass. It never touches <c>IsPensionable</c>,
/// <c>StatutoryTreatment</c>, <c>AffectsGrossPay</c> or the effective-date window, because payroll
/// has no equivalent for any of them and HR emoluments and the leave-encashment rate read them.
/// </summary>
public interface IPayComponentProjectionService
{
    /// <summary>
    /// Reconciles only when payroll has changed since the last pass, debounced so a screen that
    /// cascades several component reads does not re-fingerprint on each one.
    /// </summary>
    Task<PayComponentProjectionResultDto> EnsureCurrentAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Forces a full pass regardless of the change fingerprint. Idempotent.</summary>
    Task<PayComponentProjectionResultDto> ReconcileAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
