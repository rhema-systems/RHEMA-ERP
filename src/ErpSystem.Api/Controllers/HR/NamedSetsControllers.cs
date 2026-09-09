using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

// ═════════════════════════════════════════════════════════════════════════════
//  DEMO FEEDBACK ROUND 2, LANE C3 — THE THREE NAMED-SET MASTERS (plan § 6.4)
//
//  Benefit group, skill set, certification set. Same shape, same tiers as every
//  other HR reference master (C2's certifications, the certifying bodies): read
//  to list, write to add or change, admin to remove. No new permission is
//  introduced — the HR employee family covers reference data, and inventing a
//  fourth dialect of the same idea is what the permissions review warned about.
//
//  Each master is a list plus a member collection, so each controller carries a
//  nested member route: /{id}/members, and /{id}/members/{memberId}.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>A named bundle of benefit policies, attachable to a position (register row P-3).</summary>
[ApiController]
[Route("api/hr/reference/benefit-groups")]
[Authorize(Policy = "InternalOnly")]
public class BenefitGroupsController : ControllerBase
{
    private readonly IBenefitGroupService _service;
    private readonly ILogger<BenefitGroupsController> _logger;

    public BenefitGroupsController(IBenefitGroupService service, ILogger<BenefitGroupsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private ActionResult Rejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Benefit group rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<BenefitGroupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BenefitGroupDto>>> GetAll(
        [FromQuery] bool activeOnly = false, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(activeOnly, search, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(BenefitGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BenefitGroupDto>> GetById(Guid id, CancellationToken ct)
    {
        try { return Ok(await _service.GetAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(BenefitGroupDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<BenefitGroupDto>> Create([FromBody] CreateBenefitGroupDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating a benefit group"); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(BenefitGroupDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BenefitGroupDto>> Update(Guid id, [FromBody] UpdateBenefitGroupDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating a benefit group"); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _service.DeleteAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "removing a benefit group"); }
    }

    // ── Members ──────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/members")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<BenefitGroupMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BenefitGroupMemberDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        try { return Ok(await _service.GetMembersAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(BenefitGroupMemberDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<BenefitGroupMemberDto>> AddMember(Guid id, [FromBody] BenefitGroupMemberInputDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.AddMemberAsync(id, dto, ct);
            return CreatedAtAction(nameof(GetMembers), new { id }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "adding a benefit to a group"); }
    }

    [HttpPut("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(BenefitGroupMemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BenefitGroupMemberDto>> UpdateMember(
        Guid id, Guid memberId, [FromBody] BenefitGroupMemberInputDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateMemberAsync(id, memberId, dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "changing a benefit in a group"); }
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct)
    {
        try { await _service.RemoveMemberAsync(id, memberId, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "removing a benefit from a group"); }
    }
}

/// <summary>A named bundle of skills, attachable to a position (register row S-3).</summary>
[ApiController]
[Route("api/hr/reference/skill-sets")]
[Authorize(Policy = "InternalOnly")]
public class SkillSetsController : ControllerBase
{
    private readonly ISkillSetService _service;
    private readonly ILogger<SkillSetsController> _logger;

    public SkillSetsController(ISkillSetService service, ILogger<SkillSetsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private ActionResult Rejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Skill set rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<SkillSetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SkillSetDto>>> GetAll(
        [FromQuery] bool activeOnly = false, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(activeOnly, search, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(SkillSetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SkillSetDto>> GetById(Guid id, CancellationToken ct)
    {
        try { return Ok(await _service.GetAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(SkillSetDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SkillSetDto>> Create([FromBody] CreateSkillSetDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating a skill set"); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(SkillSetDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SkillSetDto>> Update(Guid id, [FromBody] UpdateSkillSetDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating a skill set"); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _service.DeleteAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "removing a skill set"); }
    }

    // ── Members ──────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/members")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<SkillSetMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SkillSetMemberDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        try { return Ok(await _service.GetMembersAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(SkillSetMemberDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SkillSetMemberDto>> AddMember(Guid id, [FromBody] SkillSetMemberInputDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.AddMemberAsync(id, dto, ct);
            return CreatedAtAction(nameof(GetMembers), new { id }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "adding a skill to a set"); }
    }

    [HttpPut("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(SkillSetMemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SkillSetMemberDto>> UpdateMember(
        Guid id, Guid memberId, [FromBody] SkillSetMemberInputDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateMemberAsync(id, memberId, dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "changing a skill in a set"); }
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct)
    {
        try { await _service.RemoveMemberAsync(id, memberId, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "removing a skill from a set"); }
    }
}

/// <summary>A named bundle of credentials, attachable to a position (plan § 1.5).</summary>
[ApiController]
[Route("api/hr/reference/certification-sets")]
[Authorize(Policy = "InternalOnly")]
public class CertificationSetsController : ControllerBase
{
    private readonly ICertificationSetService _service;
    private readonly ILogger<CertificationSetsController> _logger;

    public CertificationSetsController(ICertificationSetService service, ILogger<CertificationSetsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private ActionResult Rejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Certification set rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<CertificationSetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertificationSetDto>>> GetAll(
        [FromQuery] bool activeOnly = false, [FromQuery] string? search = null, CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(activeOnly, search, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(CertificationSetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CertificationSetDto>> GetById(Guid id, CancellationToken ct)
    {
        try { return Ok(await _service.GetAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CertificationSetDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CertificationSetDto>> Create([FromBody] CreateCertificationSetDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating a certification set"); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CertificationSetDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificationSetDto>> Update(Guid id, [FromBody] UpdateCertificationSetDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating a certification set"); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _service.DeleteAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "removing a certification set"); }
    }

    // ── Members ──────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/members")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<CertificationSetMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertificationSetMemberDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        try { return Ok(await _service.GetMembersAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CertificationSetMemberDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CertificationSetMemberDto>> AddMember(Guid id, [FromBody] CertificationSetMemberInputDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.AddMemberAsync(id, dto, ct);
            return CreatedAtAction(nameof(GetMembers), new { id }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "adding a credential to a set"); }
    }

    [HttpPut("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CertificationSetMemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificationSetMemberDto>> UpdateMember(
        Guid id, Guid memberId, [FromBody] CertificationSetMemberInputDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateMemberAsync(id, memberId, dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "changing a credential in a set"); }
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct)
    {
        try { await _service.RemoveMemberAsync(id, memberId, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "removing a credential from a set"); }
    }
}
