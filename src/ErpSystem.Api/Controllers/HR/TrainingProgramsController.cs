using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-programs")]
[Authorize]
public class TrainingProgramsController : ControllerBase
{
    private readonly ITrainingProgramService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingProgramsController(ITrainingProgramService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrainingProgramSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    public async Task<ActionResult<Core.DTOs.Common.PagedResult<TrainingProgramSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingProgramDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("code/{programCode}")]
    public async Task<ActionResult<TrainingProgramDto?>> GetByProgramCode(string programCode, CancellationToken ct)
        => Ok(await _service.GetByProgramCodeAsync(programCode, ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<TrainingProgramSummaryDto>>> GetActive(CancellationToken ct)
        => Ok(await _service.GetActiveAsync(ct));

    [HttpGet("category/{categoryOptionId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingProgramSummaryDto>>> GetByCategory(Guid categoryOptionId, CancellationToken ct)
        => Ok(await _service.GetByCategoryAsync(categoryOptionId, ct));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<TrainingProgramSummaryDto>>> GetByType(TrainingType type, CancellationToken ct)
        => Ok(await _service.GetByTypeAsync(type, ct));

    [HttpGet("with-certificate")]
    public async Task<ActionResult<IEnumerable<TrainingProgramSummaryDto>>> GetWithCertificate(CancellationToken ct)
        => Ok(await _service.GetWithCertificateAsync(ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<TrainingProgramDto>> Create([FromBody] CreateTrainingProgramDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TrainingProgramDto>> Update(Guid id, [FromBody] UpdateTrainingProgramDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // MATERIALS SUB-OPERATIONS
    // =========================================================================

    [HttpPost("{id:guid}/materials")]
    public async Task<ActionResult<TrainingMaterialDto>> AddMaterial(Guid id, [FromBody] CreateTrainingMaterialDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ProgramId = id;
        return Ok(await _service.AddMaterialAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/materials")]
    public async Task<ActionResult<IEnumerable<TrainingMaterialDto>>> GetMaterials(Guid id, CancellationToken ct)
        => Ok(await _service.GetMaterialsAsync(id, ct));

    [HttpPut("materials/{materialId:guid}")]
    public async Task<ActionResult<TrainingMaterialDto>> UpdateMaterial(Guid materialId, [FromBody] UpdateTrainingMaterialDto dto, CancellationToken ct)
    {
        if (materialId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateMaterialAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("materials/{materialId:guid}")]
    public async Task<IActionResult> DeleteMaterial(Guid materialId, CancellationToken ct)
    {
        await _service.DeleteMaterialAsync(materialId, ct);
        return NoContent();
    }

    // =========================================================================
    // COMPETENCIES SUB-OPERATIONS
    // =========================================================================

    [HttpPost("{id:guid}/competencies")]
    public async Task<ActionResult<TrainingProgramCompetencyDto>> AddCompetency(Guid id, [FromBody] CreateTrainingProgramCompetencyDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ProgramId = id;
        return Ok(await _service.AddCompetencyAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/competencies")]
    public async Task<ActionResult<IEnumerable<TrainingProgramCompetencyDto>>> GetCompetencies(Guid id, CancellationToken ct)
        => Ok(await _service.GetCompetenciesAsync(id, ct));

    [HttpDelete("competencies/{competencyId:guid}")]
    public async Task<IActionResult> DeleteCompetency(Guid competencyId, CancellationToken ct)
    {
        await _service.DeleteCompetencyAsync(competencyId, ct);
        return NoContent();
    }

    // =========================================================================
    // SKILLS SUB-OPERATIONS
    // =========================================================================

    [HttpPost("{id:guid}/skills")]
    public async Task<ActionResult<TrainingProgramSkillDto>> AddSkill(Guid id, [FromBody] CreateTrainingProgramSkillDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ProgramId = id;
        return Ok(await _service.AddSkillAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/skills")]
    public async Task<ActionResult<IEnumerable<TrainingProgramSkillDto>>> GetSkills(Guid id, CancellationToken ct)
        => Ok(await _service.GetSkillsAsync(id, ct));

    [HttpDelete("skills/{skillId:guid}")]
    public async Task<IActionResult> DeleteSkill(Guid skillId, CancellationToken ct)
    {
        await _service.DeleteSkillAsync(skillId, ct);
        return NoContent();
    }
}
