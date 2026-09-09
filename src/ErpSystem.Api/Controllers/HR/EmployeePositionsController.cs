using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class EmployeePositionsController : ControllerBase
{
    private readonly IEmployeePositionService _service;
    private readonly ICertificationService _certifications;
    private readonly IPositionNamedSetService _namedSets;
    private readonly ICurrentUserService _currentUserService;

    public EmployeePositionsController(
        IEmployeePositionService service,
        ICertificationService certifications,
        IPositionNamedSetService namedSets,
        ICurrentUserService currentUserService)
    {
        _service = service;
        _certifications = certifications;
        _namedSets = namedSets;
        _currentUserService = currentUserService;
    }

    private Guid TenantId()
        => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");

    /// <summary>What the post must hold (round 2, lane C2). The position save sends the whole set.</summary>
    [HttpGet("{id:guid}/certification-requirements")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<PositionCertificationRequirementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PositionCertificationRequirementDto>>> GetCertificationRequirements(Guid id, CancellationToken ct)
        => Ok(await _certifications.GetPositionRequirementsAsync(id, ct));

    // ── The effective reads (round 2, lane C3, plan § 6.4.3) ─────────────────────────────────
    //
    // What the post ACTUALLY requires: its attached named sets unioned with its individual rows,
    // each line naming where it came from. These are the reads the position form uses to grey out
    // an item a set already provides, and the same reads that benefit enrolment, succession
    // matching and certification compliance now use in place of the raw tables.

    /// <summary>Benefits from attached groups and individual rows, with each line's source.</summary>
    [HttpGet("{id:guid}/effective-benefits")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EffectiveBenefitDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EffectiveBenefitDto>>> GetEffectiveBenefits(Guid id, CancellationToken ct)
    {
        try { return Ok(await _namedSets.GetEffectiveBenefitsAsync(id, TenantId(), ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Skills from attached sets and individual rows, strongest requirement winning.</summary>
    [HttpGet("{id:guid}/effective-skills")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EffectiveSkillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EffectiveSkillDto>>> GetEffectiveSkills(Guid id, CancellationToken ct)
    {
        try { return Ok(await _namedSets.GetEffectiveSkillsAsync(id, TenantId(), ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Credentials from attached sets and individual rows; mandatory anywhere is mandatory.</summary>
    [HttpGet("{id:guid}/effective-certifications")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EffectiveCertificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EffectiveCertificationDto>>> GetEffectiveCertifications(Guid id, CancellationToken ct)
    {
        try { return Ok(await _namedSets.GetEffectiveCertificationsAsync(id, TenantId(), ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    #region CRUD

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePositionDto>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionDto>>> GetActive()
        => Ok(await _service.GetActivePositionsAsync());

    [HttpGet("code/{code}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePositionDto>> GetByCode(string code)
    {
        var result = await _service.GetByCodeAsync(code);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeePositionDto>> Create([FromBody] CreateEmployeePositionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreatePositionAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A business rule, not a fault. The global middleware would answer 400 with a canned
            // sentence; the reports-to rules (C1) exist to explain themselves.
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeePositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeePositionDto>> Update(Guid id, [FromBody] UpdateEmployeePositionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var updated = await _service.UpdatePositionAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeletePositionAsync(id);
        return NoContent();
    }

    #endregion

    /// <summary>
    /// Positions in a unit; with <c>includeAncestors=true</c>, in every unit above it as well —
    /// the reports-to option source (demo feedback round 2, C1). Had no caller before that slice.
    /// </summary>
    [HttpGet("organization-unit/{organizationUnitId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionDto>>> GetByOrganizationUnit(Guid organizationUnitId, [FromQuery] bool includeAncestors = false)
    {
        return Ok(await _service.GetByOrganizationUnitAsync(organizationUnitId, includeAncestors));
    }

    [HttpGet("department/{departmentId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeePositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeePositionDto>>> GetByDepartment(Guid departmentId)
    {
        return Ok(await _service.GetByDepartmentAsync(departmentId));
    }
}
