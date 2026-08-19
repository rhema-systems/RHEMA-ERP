using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
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

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<CompetencyDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize   = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetActive()
        => Ok(await _service.GetActiveAsync());

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("lookup")]
    public async Task<ActionResult<IEnumerable<CompetencyLookupDto>>> GetLookup()
        => Ok(await _service.GetLookupListAsync());

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompetencyDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("{id:guid}/detail")]
    public async Task<ActionResult<CompetencyDetailDto>> GetDetail(Guid id)
        => Ok(await _service.GetDetailAsync(id));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("code/{code}")]
    public async Task<ActionResult<CompetencyDto?>> GetByCode(string code)
        => Ok(await _service.GetByCodeAsync(code));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetByCategory(CompetencyCategory category)
        => Ok(await _service.GetByCategoryAsync(category));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetByPosition(Guid positionId)
        => Ok(await _service.GetByPositionAsync(positionId));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeAsync(employeeId));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("code-exists")]
    public async Task<ActionResult<bool>> CodeExists([FromQuery] string code, [FromQuery] Guid? excludeId = null)
        => Ok(await _service.CodeExistsAsync(code, excludeId));

    // =========================================================================
    // COMPETENCY CRUD
    // =========================================================================

    [Authorize(Policy = HrPermissions.CompetencyWritePolicy)]
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

    [Authorize(Policy = HrPermissions.CompetencyWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompetencyDto>> Update(Guid id, [FromBody] UpdateCompetencyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.CompetencyAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // SKILL INDICATORS (sub-resource of a competency)
    // =========================================================================

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("{id:guid}/skill-indicators")]
    public async Task<ActionResult<IEnumerable<CompetencySkillIndicatorDto>>> GetSkillIndicators(Guid id)
        => Ok(await _indicatorService.GetByCompetencyIdAsync(id));

    [Authorize(Policy = HrPermissions.CompetencyWritePolicy)]
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

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompetencySkillIndicatorDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("skill/{skillId:guid}")]
    public async Task<ActionResult<IEnumerable<CompetencySkillIndicatorDto>>> GetBySkill(Guid skillId)
        => Ok(await _service.GetBySkillIdAsync(skillId));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("competency/{competencyId:guid}/skill/{skillId:guid}")]
    public async Task<ActionResult<CompetencySkillIndicatorDto?>> GetByPair(Guid competencyId, Guid skillId)
        => Ok(await _service.GetByCompetencyAndSkillAsync(competencyId, skillId));

    [Authorize(Policy = HrPermissions.CompetencyWritePolicy)]
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

    [Authorize(Policy = HrPermissions.CompetencyAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
