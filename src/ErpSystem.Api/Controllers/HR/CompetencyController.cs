using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

// ============================================================================
// COMPETENCY CONTROLLER
// ============================================================================

[ApiController]
[Route("api/competencies")]
[Authorize]
public class CompetencyController : ControllerBase
{
    private readonly ICompetencyService _service;
    private readonly ICompetencySkillIndicatorService _indicatorService;
    private readonly ICurrentUserService _currentUser;

    public CompetencyController(
        ICompetencyService service,
        ICompetencySkillIndicatorService indicatorService,
        ICurrentUserService currentUser)
    {
        _service          = service;
        _indicatorService = indicatorService;
        _currentUser      = currentUser;
    }

    // =========================================================================
    // COMPETENCY QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<PagedResult<CompetencyDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize   = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetActive()
        => Ok(await _service.GetActiveAsync());

    [HttpGet("lookup")]
    public async Task<ActionResult<IEnumerable<CompetencyLookupDto>>> GetLookup()
        => Ok(await _service.GetLookupListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompetencyDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("{id:guid}/detail")]
    public async Task<ActionResult<CompetencyDetailDto>> GetDetail(Guid id)
        => Ok(await _service.GetDetailAsync(id));

    [HttpGet("code/{code}")]
    public async Task<ActionResult<CompetencyDto?>> GetByCode(string code)
        => Ok(await _service.GetByCodeAsync(code));

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetByCategory(CompetencyCategory category)
        => Ok(await _service.GetByCategoryAsync(category));

    [HttpGet("position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetByPosition(Guid positionId)
        => Ok(await _service.GetByPositionAsync(positionId));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeAsync(employeeId));

    [HttpGet("code-exists")]
    public async Task<ActionResult<bool>> CodeExists([FromQuery] string code, [FromQuery] Guid? excludeId = null)
        => Ok(await _service.CodeExistsAsync(code, excludeId));

    // =========================================================================
    // COMPETENCY CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<CompetencyDto>> Create([FromBody] CreateCompetencyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompetencyDto>> Update(Guid id, [FromBody] UpdateCompetencyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // SKILL INDICATORS (sub-resource of a competency)
    // =========================================================================

    [HttpGet("{id:guid}/skill-indicators")]
    public async Task<ActionResult<IEnumerable<CompetencySkillIndicatorDto>>> GetSkillIndicators(Guid id)
        => Ok(await _indicatorService.GetByCompetencyIdAsync(id));

    [HttpPost("{id:guid}/skill-indicators")]
    public async Task<ActionResult<CompetencySkillIndicatorDto>> AddSkillIndicator(
        Guid id, [FromBody] CreateCompetencySkillIndicatorDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CompetencyId = id;
        var created = await _indicatorService.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetSkillIndicators), new { id }, created);
    }
}

// ============================================================================
// COMPETENCY SKILL INDICATOR CONTROLLER
// (standalone endpoints for skill-first lookups and direct indicator management)
// ============================================================================

[ApiController]
[Route("api/competency-skill-indicators")]
[Authorize]
public class CompetencySkillIndicatorController : ControllerBase
{
    private readonly ICompetencySkillIndicatorService _service;
    private readonly ICurrentUserService _currentUser;

    public CompetencySkillIndicatorController(
        ICompetencySkillIndicatorService service,
        ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompetencySkillIndicatorDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("skill/{skillId:guid}")]
    public async Task<ActionResult<IEnumerable<CompetencySkillIndicatorDto>>> GetBySkill(Guid skillId)
        => Ok(await _service.GetBySkillIdAsync(skillId));

    [HttpGet("competency/{competencyId:guid}/skill/{skillId:guid}")]
    public async Task<ActionResult<CompetencySkillIndicatorDto?>> GetByPair(Guid competencyId, Guid skillId)
        => Ok(await _service.GetByCompetencyAndSkillAsync(competencyId, skillId));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompetencySkillIndicatorDto>> Update(
        Guid id, [FromBody] UpdateCompetencySkillIndicatorDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
