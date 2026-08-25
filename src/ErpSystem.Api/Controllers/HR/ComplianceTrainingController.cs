using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/compliance-training")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class ComplianceTrainingController : ControllerBase
{
    private readonly IComplianceTrainingService _service;
    private readonly ICurrentUserService _currentUser;

    // Simple request model for assigning compliance requirements
    public sealed record AssignComplianceRequest(Guid EmployeeId, Guid RequirementId);

    // Simple request model for fulfilling compliance records
    public sealed record FulfillComplianceRequest(Guid NominationId);

    public ComplianceTrainingController(
        IComplianceTrainingService service,
        ICurrentUserService currentUser,
        IAuthorizationService authorization)
    {
        _service = service;
        _currentUser = currentUser;
        _authorization = authorization;
    }

    private readonly IAuthorizationService _authorization;

    /// <summary>Self-or-permission (W3), as on LeavesController — see the remarks there.</summary>
    private async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (_currentUser.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
            return true;
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    // =========================================================================
    // REQUIREMENTS
    // =========================================================================

    [HttpGet("requirements")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<ComplianceTrainingRequirementDto>>> GetAllRequirements(CancellationToken ct)
        => Ok(await _service.GetAllRequirementsAsync(ct));

    [HttpGet("requirements/{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<ComplianceTrainingRequirementDto>> GetRequirementById(Guid id, CancellationToken ct)
        => Ok(await _service.GetRequirementByIdAsync(id, ct));

    [HttpGet("requirements/active")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<ComplianceTrainingRequirementDto>>> GetActiveRequirements(CancellationToken ct)
        => Ok(await _service.GetActiveRequirementsAsync(ct));

    [HttpGet("requirements/program/{programId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<ComplianceTrainingRequirementDto>>> GetRequirementsByProgram(Guid programId, CancellationToken ct)
        => Ok(await _service.GetRequirementsByProgramAsync(programId, ct));

    [HttpPost("requirements")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<ComplianceTrainingRequirementDto>> CreateRequirement([FromBody] CreateComplianceTrainingRequirementDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateRequirementAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetRequirementById), new { id = created.Id }, created);
    }

    [HttpPut("requirements/{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<ComplianceTrainingRequirementDto>> UpdateRequirement(Guid id, [FromBody] UpdateComplianceTrainingRequirementDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateRequirementAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("requirements/{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> DeleteRequirement(Guid id, CancellationToken ct)
    {
        await _service.DeleteRequirementAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // COMPLIANCE RECORDS — QUERIES
    // =========================================================================

    /// <summary>The caller's own compliance record — "am I compliant?".</summary>
    /// <remarks>
    /// The module's /mine convention. Compliance is personal and the id-bearing route below is
    /// org-wide, so without this a self-service screen would have to pass someone's employee id to
    /// read their compliance status.
    /// </remarks>
    [HttpGet("records/mine")]
    public async Task<ActionResult<IEnumerable<EmployeeComplianceRecordDto>>> GetMyRecords(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return Forbid();
        return Ok(await _service.GetRecordsForEmployeeAsync(employeeId.Value, ct));
    }

    [HttpGet("records/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeComplianceRecordDto>>> GetRecordsForEmployee(Guid employeeId, CancellationToken ct)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(await _service.GetRecordsForEmployeeAsync(employeeId, ct));
    }

    // Returns summaries, not full records — the declared type used to say EmployeeComplianceRecordDto,
    // which is a contract any generated client would get wrong.
    [HttpGet("records/requirement/{requirementId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmployeeComplianceRecordSummaryDto>>> GetRecordsForRequirement(Guid requirementId, CancellationToken ct)
        => Ok(await _service.GetRecordsForRequirementAsync(requirementId, ct));

    [HttpGet("records/overdue")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmployeeComplianceRecordSummaryDto>>> GetOverdueRecords(CancellationToken ct)
        => Ok(await _service.GetOverdueRecordsAsync(ct));

    [HttpGet("records/non-compliant")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmployeeComplianceRecordSummaryDto>>> GetNonCompliantRecords(CancellationToken ct)
        => Ok(await _service.GetNonCompliantRecordsAsync(ct));

    // =========================================================================
    // COMPLIANCE RECORDS — WORKFLOW
    // =========================================================================

    [HttpPost("records/assign")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<EmployeeComplianceRecordDto>> AssignRequirementToEmployee([FromBody] AssignComplianceRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AssignRequirementToEmployeeAsync(request.EmployeeId, request.RequirementId, tenantId.Value, employeeId.Value, ct));
    }

    [HttpPost("records/{id:guid}/exempt")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<IActionResult> ExemptEmployee(Guid id, [FromBody] ExemptEmployeeComplianceDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.RecordId = id;
        // Returns the updated record rather than a bare message: the service already builds the
        // full DTO through its includes chain, and discarding it forced a refetch.
        return Ok(await _service.ExemptEmployeeAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("records/{id:guid}/fulfill")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<IActionResult> MarkFulfilled(Guid id, [FromBody] FulfillComplianceRequest request, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        // Returns the updated record rather than a bare message: the service already builds the
        // full DTO through its includes chain, and discarding it forced a refetch.
        return Ok(await _service.MarkFulfilledAsync(id, request.NominationId, employeeId.Value, ct));
    }
}
