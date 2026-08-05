using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Application service for direct, in-force employee benefit enrollments and the Benefit→Payroll
/// bridge. Enrollments are the single ledger payroll consumes; position/grade entitlements are
/// reconciled into them and may then be overridden at the employee level.
/// </summary>
public interface IEmployeeBenefitEnrollmentService
{
    /// <summary>Gets an enrollment by id, including dependents and beneficiaries.</summary>
    Task<EmployeeBenefitEnrollmentDto?> GetByIdAsync(Guid id);

    /// <summary>Gets all enrollments for an employee.</summary>
    Task<IReadOnlyList<EmployeeBenefitEnrollmentListDto>> GetByEmployeeAsync(Guid employeeId);

    /// <summary>Gets all enrollments for a benefit policy.</summary>
    Task<IReadOnlyList<EmployeeBenefitEnrollmentListDto>> GetByPolicyAsync(Guid benefitPolicyId);

    /// <summary>
    /// Creates a direct (manual) enrollment, resolving the assessed/taxable values and contribution
    /// split from the policy's valuation &amp; tax rules unless an explicit override is supplied.
    /// </summary>
    Task<EmployeeBenefitEnrollmentDto> CreateAsync(CreateEmployeeBenefitEnrollmentDto dto);

    /// <summary>Updates an enrollment's editable fields (re-resolves values unless overridden).</summary>
    Task<EmployeeBenefitEnrollmentDto> UpdateAsync(Guid id, UpdateEmployeeBenefitEnrollmentDto dto);

    /// <summary>Transitions an enrollment's status (approve, suspend, terminate, etc.).</summary>
    Task<EmployeeBenefitEnrollmentDto> ChangeStatusAsync(Guid id, EnrollmentStatusChangeDto dto);

    /// <summary>
    /// Materializes / syncs an employee's enrollments from their position's benefit entitlements.
    /// Idempotent: creates missing Position-sourced enrollments, refreshes non-overridden ones, and
    /// leaves manual/overridden/opted-out enrollments untouched. Returns the number created.
    /// </summary>
    Task<int> ReconcilePositionEnrollmentsAsync(Guid employeeId);

    /// <summary>
    /// The Benefit→Payroll bridge: flattens an employee's active enrollments into payroll-ready lines
    /// (gross/taxable value, employer/employee split, pensionable flag, frequency, currency).
    /// </summary>
    Task<IReadOnlyList<EmployeeBenefitPayrollLineDto>> GetEmployeeBenefitPayrollLinesAsync(Guid employeeId, DateTime asOf);

    /// <summary>Lists the utilization/claim ledger for an enrollment (most recent first).</summary>
    Task<IReadOnlyList<BenefitUtilizationDto>> GetUtilizationsAsync(Guid enrollmentId);

    /// <summary>
    /// Computes the coverage balance for an enrollment in its current usage period, applying the lazy
    /// periodic reset (advances the period window and recomputes the used amount).
    /// </summary>
    Task<EnrollmentBalanceDto> GetBalanceAsync(Guid enrollmentId);

    /// <summary>
    /// Records a new (Pending) utilization/claim against an enrollment's coverage limit, optionally
    /// attributed to a covered dependent.
    /// </summary>
    Task<BenefitUtilizationDto> RecordUtilizationAsync(CreateBenefitUtilizationDto dto);

    /// <summary>
    /// Transitions a utilization/claim's status. Approving/paying a claim that would exceed the
    /// remaining balance is rejected; cache amounts are refreshed afterwards.
    /// </summary>
    Task<BenefitUtilizationDto> ChangeClaimStatusAsync(Guid claimId, ClaimStatusChangeDto dto);

    // ─────────────────────── covered dependents ───────────────────────

    /// <summary>Lists the dependents covered under an enrollment, inactive ones included.</summary>
    Task<IReadOnlyList<EnrollmentDependentDto>> GetDependentsAsync(Guid enrollmentId);

    /// <summary>
    /// Extends cover to one of the employee's registered dependents. Rejected when the policy covers
    /// staff only, when the dependent belongs to someone else, when they are already covered, or when
    /// the policy's dependent cap is already met.
    /// </summary>
    Task<EnrollmentDependentDto> AddDependentAsync(Guid enrollmentId, CreateEnrollmentDependentDto dto);

    /// <summary>Amends a covered dependent's coverage window or ends their cover.</summary>
    Task<EnrollmentDependentDto> UpdateDependentAsync(Guid enrollmentId, Guid dependentBenefitId, UpdateEnrollmentDependentDto dto);

    /// <summary>
    /// Removes a dependent from an enrollment. A dependent with claims against this enrollment is
    /// deactivated rather than deleted, so the claim ledger keeps its attribution; returns true when
    /// the row was deleted outright and false when it was retained as inactive.
    /// </summary>
    Task<bool> RemoveDependentAsync(Guid enrollmentId, Guid dependentBenefitId);

    // ───────────────────────── beneficiaries ──────────────────────────

    /// <summary>Lists an enrollment's named beneficiaries.</summary>
    Task<IReadOnlyList<BenefitBeneficiaryDto>> GetBeneficiariesAsync(Guid enrollmentId);

    /// <summary>
    /// Replaces the whole beneficiary set atomically. Set-at-a-time is deliberate — see
    /// <see cref="ReplaceBenefitBeneficiariesDto"/> for why the 100% rule forces it.
    /// </summary>
    Task<IReadOnlyList<BenefitBeneficiaryDto>> ReplaceBeneficiariesAsync(Guid enrollmentId, ReplaceBenefitBeneficiariesDto dto);
}
