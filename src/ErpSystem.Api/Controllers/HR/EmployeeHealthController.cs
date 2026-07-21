using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/employee-health")]
[Authorize]
public class EmployeeHealthController : MedicalControllerBase
{
    private readonly IEmployeeHealthService _service;

    public EmployeeHealthController(IEmployeeHealthService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    // =========================================================================
    // HEALTH PROFILES
    // =========================================================================

    [HttpGet("profiles")]
    public async Task<ActionResult<IEnumerable<EmployeeHealthProfileDto>>> GetAllProfiles(CancellationToken ct)
        => Ok(await _service.GetAllProfilesAsync(ct));

    [HttpGet("profiles/{id:guid}")]
    public async Task<ActionResult<EmployeeHealthProfileDto>> GetProfile(Guid id, CancellationToken ct)
        => Ok(await _service.GetProfileByIdAsync(id, ct));

    [HttpGet("profiles/{id:guid}/details")]
    public async Task<ActionResult<EmployeeHealthProfileDetailDto>> GetProfileWithDetails(Guid id, CancellationToken ct)
        => Ok(await _service.GetProfileWithDetailsAsync(id, ct));

    [HttpGet("employees/{employeeId:guid}/profile")]
    public async Task<ActionResult<EmployeeHealthProfileDto?>> GetProfileByEmployee(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetProfileByEmployeeAsync(employeeId, ct));

    [HttpPost("profiles")]
    public async Task<ActionResult<EmployeeHealthProfileDto>> CreateProfile(
        [FromBody] CreateEmployeeHealthProfileDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateProfileAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetProfile), new { id = created.Id }, created);
    }

    [HttpPut("profiles/{id:guid}")]
    public async Task<ActionResult<EmployeeHealthProfileDto>> UpdateProfile(
        Guid id,
        [FromBody] UpdateEmployeeHealthProfileDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateProfileAsync(dto, userId, ct));
    }

    [HttpDelete("profiles/{id:guid}")]
    public async Task<IActionResult> DeleteProfile(Guid id, CancellationToken ct)
    {
        await _service.DeleteProfileAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // CONDITIONS
    // =========================================================================

    [HttpGet("profiles/{healthProfileId:guid}/conditions")]
    public async Task<ActionResult<IEnumerable<EmployeeHealthConditionDto>>> GetConditions(
        Guid healthProfileId,
        [FromQuery] bool onlyActive = false,
        CancellationToken ct = default)
        => Ok(onlyActive
            ? await _service.GetActiveConditionsAsync(healthProfileId, ct)
            : await _service.GetConditionsAsync(healthProfileId, ct));

    [HttpPost("conditions")]
    public async Task<ActionResult<EmployeeHealthConditionDto>> AddCondition(
        [FromBody] CreateEmployeeHealthConditionDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddConditionAsync(dto, tenantId, userId, ct);
        return Ok(created);
    }

    [HttpPut("conditions/{id:guid}")]
    public async Task<ActionResult<EmployeeHealthConditionDto>> UpdateCondition(
        Guid id,
        [FromBody] UpdateEmployeeHealthConditionDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateConditionAsync(dto, userId, ct));
    }

    [HttpDelete("conditions/{id:guid}")]
    public async Task<IActionResult> DeleteCondition(Guid id, CancellationToken ct)
    {
        await _service.DeleteConditionAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // ALLERGIES
    // =========================================================================

    [HttpGet("profiles/{healthProfileId:guid}/allergies")]
    public async Task<ActionResult<IEnumerable<EmployeeAllergyDto>>> GetAllergies(
        Guid healthProfileId,
        CancellationToken ct)
        => Ok(await _service.GetAllergiesAsync(healthProfileId, ct));

    [HttpPost("allergies")]
    public async Task<ActionResult<EmployeeAllergyDto>> AddAllergy(
        [FromBody] CreateEmployeeAllergyDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddAllergyAsync(dto, tenantId, userId, ct);
        return Ok(created);
    }

    [HttpPut("allergies/{id:guid}")]
    public async Task<ActionResult<EmployeeAllergyDto>> UpdateAllergy(
        Guid id,
        [FromBody] UpdateEmployeeAllergyDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateAllergyAsync(dto, userId, ct));
    }

    [HttpDelete("allergies/{id:guid}")]
    public async Task<IActionResult> DeleteAllergy(Guid id, CancellationToken ct)
    {
        await _service.DeleteAllergyAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // MEDICAL EXAMS
    // =========================================================================

    [HttpGet("exams/{id:guid}")]
    public async Task<ActionResult<EmployeeMedicalExamDto>> GetExam(Guid id, CancellationToken ct)
        => Ok(await _service.GetExamByIdAsync(id, ct));

    [HttpGet("exams/{id:guid}/details")]
    public async Task<ActionResult<EmployeeMedicalExamDetailDto>> GetExamWithDocuments(Guid id, CancellationToken ct)
        => Ok(await _service.GetExamWithDocumentsAsync(id, ct));

    [HttpGet("profiles/{healthProfileId:guid}/exams")]
    public async Task<ActionResult<IEnumerable<EmployeeMedicalExamSummaryDto>>> GetExamsByProfile(
        Guid healthProfileId,
        CancellationToken ct)
        => Ok(await _service.GetExamsByProfileAsync(healthProfileId, ct));

    [HttpGet("exams/due")]
    public async Task<ActionResult<IEnumerable<EmployeeMedicalExamSummaryDto>>> GetExamsDue(
        [FromQuery] int daysAhead = 30,
        CancellationToken ct = default)
        => Ok(await _service.GetExamsDueAsync(daysAhead, ct));

    [HttpPost("exams")]
    public async Task<ActionResult<EmployeeMedicalExamDto>> CreateExam(
        [FromBody] CreateEmployeeMedicalExamDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateExamAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetExam), new { id = created.Id }, created);
    }

    [HttpPut("exams/{id:guid}")]
    public async Task<ActionResult<EmployeeMedicalExamDto>> UpdateExam(
        Guid id,
        [FromBody] UpdateEmployeeMedicalExamDto dto,
        CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateExamAsync(dto, userId, ct));
    }

    [HttpDelete("exams/{id:guid}")]
    public async Task<IActionResult> DeleteExam(Guid id, CancellationToken ct)
    {
        await _service.DeleteExamAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // EXAM DOCUMENTS
    // =========================================================================

    [HttpGet("exams/{examId:guid}/documents")]
    public async Task<ActionResult<IEnumerable<EmployeeMedicalExamDocumentDto>>> GetExamDocuments(
        Guid examId,
        CancellationToken ct)
        => Ok(await _service.GetExamDocumentsAsync(examId, ct));

    [HttpPost("exam-documents")]
    public async Task<ActionResult<EmployeeMedicalExamDocumentDto>> AddExamDocument(
        [FromBody] CreateEmployeeMedicalExamDocumentDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.AddExamDocumentAsync(dto, tenantId, userId, ct);
        return Ok(created);
    }

    [HttpDelete("exam-documents/{id:guid}")]
    public async Task<IActionResult> DeleteExamDocument(Guid id, CancellationToken ct)
    {
        await _service.DeleteExamDocumentAsync(id, ct);
        return NoContent();
    }
}
