using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Direct employee benefit enrollments — the in-force ledger payroll consumes — plus reconciliation
/// from position entitlements and the Benefit→Payroll bridge.
/// </summary>
[ApiController]
[Route("api/hr/employee-benefit-enrollments")]
[Authorize]
public class EmployeeBenefitEnrollmentsController : ControllerBase
{
    private readonly IEmployeeBenefitEnrollmentService _service;
    private readonly ILogger<EmployeeBenefitEnrollmentsController> _logger;

    public EmployeeBenefitEnrollmentsController(
        IEmployeeBenefitEnrollmentService service,
        ILogger<EmployeeBenefitEnrollmentsController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Gets an enrollment by id (with dependents and beneficiaries).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeBenefitEnrollmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeBenefitEnrollmentDto>> GetByIdAsync([FromRoute] Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Gets all enrollments for an employee.</summary>
    [HttpGet("by-employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeBenefitEnrollmentListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeBenefitEnrollmentListDto>>> GetByEmployeeAsync([FromRoute] Guid employeeId)
        => Ok(await _service.GetByEmployeeAsync(employeeId));

    /// <summary>Gets all enrollments for a benefit policy.</summary>
    [HttpGet("by-policy/{policyId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeBenefitEnrollmentListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeBenefitEnrollmentListDto>>> GetByPolicyAsync([FromRoute] Guid policyId)
        => Ok(await _service.GetByPolicyAsync(policyId));

    /// <summary>Creates a direct (manual) enrollment.</summary>
    [HttpPost]
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
    [HttpPost("utilizations/{claimId:guid}/status")]
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
