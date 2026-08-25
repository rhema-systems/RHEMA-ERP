using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Direct employee benefit enrollments — the in-force ledger payroll consumes — plus reconciliation
/// from position entitlements and the Benefit→Payroll bridge.
/// </summary>
/// <remarks>
/// W3 slice 7: an enrollment is about its employee, so their own balance, utilizations,
/// dependents and beneficiaries are self-or-permission (an employee maintains their own
/// beneficiary nominations and files their own utilization claims, the medical-area decision);
/// enrolling, status changes, claim decisions, reconciliation and the payroll bridge are the
/// compensation tiers.
/// </remarks>
[ApiController]
[Route("api/hr/employee-benefit-enrollments")]
[Authorize(Policy = "InternalOnly")]
public class EmployeeBenefitEnrollmentsController : ControllerBase
{
    private readonly IEmployeeBenefitEnrollmentService _service;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<EmployeeBenefitEnrollmentsController> _logger;

    public EmployeeBenefitEnrollmentsController(
        IEmployeeBenefitEnrollmentService service,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuthorizationService authorization,
        ILogger<EmployeeBenefitEnrollmentsController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _db = db;
        _currentUserService = currentUserService;
        _authorization = authorization;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Self-or-permission (W3), as on LeavesController — see the remarks there.</summary>
    private async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
            return true;
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>Self-or-permission resolved through the enrollment's employee.</summary>
    private async Task<bool> CanActOnEnrollmentAsync(Guid enrollmentId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty &&
            _currentUserService.TenantId is Guid tenantId)
        {
            var mine = await _db.Set<Core.Entities.HR.EmployeeBenefitEnrollment>()
                .AsNoTracking()
                .AnyAsync(e => e.Id == enrollmentId && e.TenantId == tenantId && e.EmployeeId == me);
            if (mine) return true;
        }
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>Gets an enrollment by id (with dependents and beneficiaries).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeBenefitEnrollmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBenefitEnrollmentDto>> GetByIdAsync([FromRoute] Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null) return NotFound();
        // W3: self-or-permission — ownership is only knowable after the fetch.
        if (!await SelfOrPolicyAsync(result.EmployeeId, HrPermissions.CompensationReadPolicy))
            return Forbid();
        return Ok(result);
    }

    /// <summary>Gets all enrollments for an employee.</summary>
    [HttpGet("by-employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeBenefitEnrollmentListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeBenefitEnrollmentListDto>>> GetByEmployeeAsync([FromRoute] Guid employeeId)
    {
        // W3: self-or-permission — an employee sees their own benefits.
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.CompensationReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeAsync(employeeId));
    }

    /// <summary>Gets all enrollments for a benefit policy.</summary>
    [HttpGet("by-policy/{policyId:guid}")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeBenefitEnrollmentListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeBenefitEnrollmentListDto>>> GetByPolicyAsync([FromRoute] Guid policyId)
        => Ok(await _service.GetByPolicyAsync(policyId));

    /// <summary>Creates a direct (manual) enrollment.</summary>
    [HttpPost]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)] // enrolling is the desk's act; self-enrollment arrives with area 25 if TDC wants it
    [ProducesResponseType(typeof(EmployeeBenefitEnrollmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeBenefitEnrollmentDto>> CreateAsync([FromBody] CreateEmployeeBenefitEnrollmentDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _service.CreateAsync(dto);
            return Created($"/api/hr/employee-benefit-enrollments/{result.Id}", result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Updates an enrollment's editable fields.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(EmployeeBenefitEnrollmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBenefitEnrollmentDto>> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateEmployeeBenefitEnrollmentDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _service.UpdateAsync(id, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Transitions an enrollment's status (approve, suspend, terminate, etc.).</summary>
    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(EmployeeBenefitEnrollmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBenefitEnrollmentDto>> ChangeStatusAsync([FromRoute] Guid id, [FromBody] EnrollmentStatusChangeDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _service.ChangeStatusAsync(id, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Materializes / syncs an employee's enrollments from their position entitlements.</summary>
    [HttpPost("reconcile")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReconcileAsync([FromQuery] Guid employeeId)
    {
        if (employeeId == Guid.Empty)
        {
            return BadRequest(new { message = "employeeId is required." });
        }

        try
        {
            var created = await _service.ReconcilePositionEnrollmentsAsync(employeeId);
            return Ok(new { created });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Benefit→Payroll bridge: flattens an employee's active enrollments into payroll-ready lines.
    /// This is the single contract the (separate) payroll module consumes.
    /// </summary>
    [HttpGet("payroll-lines")]
    [Authorize(Policy = HrPermissions.CompensationReadPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeBenefitPayrollLineDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeBenefitPayrollLineDto>>> GetPayrollLinesAsync(
        [FromQuery] Guid employeeId,
        [FromQuery] DateTime? asOf)
    {
        if (employeeId == Guid.Empty)
        {
            return BadRequest(new { message = "employeeId is required." });
        }

        var results = await _service.GetEmployeeBenefitPayrollLinesAsync(employeeId, asOf ?? DateTime.UtcNow);
        return Ok(results);
    }

    /// <summary>Gets the coverage balance (limit / used / remaining + current period) for an enrollment.</summary>
    [HttpGet("{id:guid}/balance")]
    [ProducesResponseType(typeof(EnrollmentBalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrollmentBalanceDto>> GetBalanceAsync([FromRoute] Guid id)
    {
        if (!await CanActOnEnrollmentAsync(id, HrPermissions.CompensationReadPolicy))
            return Forbid();

        try
        {
            return Ok(await _service.GetBalanceAsync(id));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Lists the utilization/claim ledger for an enrollment.</summary>
    [HttpGet("{id:guid}/utilizations")]
    [ProducesResponseType(typeof(IReadOnlyList<BenefitUtilizationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BenefitUtilizationDto>>> GetUtilizationsAsync([FromRoute] Guid id)
        => Ok(await _service.GetUtilizationsAsync(id));

    /// <summary>Records a new (Pending) utilization/claim against an enrollment's coverage limit.</summary>
    [HttpPost("{id:guid}/utilizations")]
    [ProducesResponseType(typeof(BenefitUtilizationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BenefitUtilizationDto>> RecordUtilizationAsync([FromRoute] Guid id, [FromBody] CreateBenefitUtilizationDto dto)
    {
        if (!await CanActOnEnrollmentAsync(id, HrPermissions.CompensationWritePolicy))
            return Forbid();

        if (!await CanActOnEnrollmentAsync(id, HrPermissions.CompensationReadPolicy))
            return Forbid();

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        dto.EnrollmentId = id;

        try
        {
            var result = await _service.RecordUtilizationAsync(dto);
            return Created($"/api/hr/employee-benefit-enrollments/{id}/utilizations", result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Transitions a utilization/claim's status (approve, reject, pay, cancel).</summary>
    // W3: deciding/paying a claim is the desk's act, never the claimant's.
    [HttpPost("utilizations/{claimId:guid}/status")]
    [Authorize(Policy = HrPermissions.CompensationWritePolicy)]
    [ProducesResponseType(typeof(BenefitUtilizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BenefitUtilizationDto>> ChangeClaimStatusAsync([FromRoute] Guid claimId, [FromBody] ClaimStatusChangeDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _service.ChangeClaimStatusAsync(claimId, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ─────────────────────── covered dependents ───────────────────────

    /// <summary>Lists the dependents covered under an enrollment (inactive ones included).</summary>
    [HttpGet("{id:guid}/dependents")]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentDependentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EnrollmentDependentDto>>> GetDependentsAsync([FromRoute] Guid id)
    {
        if (!await CanActOnEnrollmentAsync(id, HrPermissions.CompensationReadPolicy))
            return Forbid();

        try
        {
            return Ok(await _service.GetDependentsAsync(id));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Extends cover to one of the employee's registered dependents.</summary>
    /// <response code="400">The dependent does not exist or is not registered to this employee.</response>
    /// <response code="409">
    /// The policy covers staff only, the dependent is deceased or already covered, the policy's
    /// dependent cap is met, or the enrollment is no longer editable.
    /// </response>
    [HttpPost("{id:guid}/dependents")]
    [ProducesResponseType(typeof(EnrollmentDependentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrollmentDependentDto>> AddDependentAsync([FromRoute] Guid id, [FromBody] CreateEnrollmentDependentDto dto)
    {
        if (!await CanActOnEnrollmentAsync(id, HrPermissions.CompensationWritePolicy))
            return Forbid();

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _service.AddDependentAsync(id, dto);
            return Created($"/api/hr/employee-benefit-enrollments/{id}/dependents/{result.Id}", result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Amends a covered dependent's coverage window, or ends their cover.</summary>
    [HttpPut("{id:guid}/dependents/{dependentBenefitId:guid}")]
    [ProducesResponseType(typeof(EnrollmentDependentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrollmentDependentDto>> UpdateDependentAsync(
        [FromRoute] Guid id,
        [FromRoute] Guid dependentBenefitId,
        [FromBody] UpdateEnrollmentDependentDto dto)
    {
        if (!await CanActOnEnrollmentAsync(id, HrPermissions.CompensationWritePolicy))
            return Forbid();

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(await _service.UpdateDependentAsync(id, dependentBenefitId, dto));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Removes a dependent from an enrollment. One with claims against it is kept as inactive rather
    /// than deleted; <c>deleted</c> in the response says which happened.
    /// </summary>
    [HttpDelete("{id:guid}/dependents/{dependentBenefitId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveDependentAsync([FromRoute] Guid id, [FromRoute] Guid dependentBenefitId)
    {
        if (!await CanActOnEnrollmentAsync(id, HrPermissions.CompensationWritePolicy))
            return Forbid();

        try
        {
            var deleted = await _service.RemoveDependentAsync(id, dependentBenefitId);
            return Ok(new
            {
                deleted,
                message = deleted
                    ? "Cover removed."
                    : "Cover ended. The dependent has claims on this enrollment, so the record was retained.",
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ───────────────────────── beneficiaries ──────────────────────────

    /// <summary>Lists an enrollment's named beneficiaries.</summary>
    [HttpGet("{id:guid}/beneficiaries")]
    [ProducesResponseType(typeof(IReadOnlyList<BenefitBeneficiaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BenefitBeneficiaryDto>>> GetBeneficiariesAsync([FromRoute] Guid id)
    {
        if (!await CanActOnEnrollmentAsync(id, HrPermissions.CompensationReadPolicy))
            return Forbid();

        try
        {
            return Ok(await _service.GetBeneficiariesAsync(id));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Replaces the whole beneficiary set. Sent as a set because the shares must total 100 — see
    /// <see cref="ReplaceBenefitBeneficiariesDto"/>. An empty list clears the nomination.
    /// </summary>
    /// <response code="400">The shares do not total 100, or a dependent is named twice.</response>
    [HttpPut("{id:guid}/beneficiaries")]
    [ProducesResponseType(typeof(IReadOnlyList<BenefitBeneficiaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<BenefitBeneficiaryDto>>> ReplaceBeneficiariesAsync(
        [FromRoute] Guid id,
        [FromBody] ReplaceBenefitBeneficiariesDto dto)
    {
        if (!await CanActOnEnrollmentAsync(id, HrPermissions.CompensationWritePolicy))
            return Forbid();

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            return Ok(await _service.ReplaceBeneficiariesAsync(id, dto));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
