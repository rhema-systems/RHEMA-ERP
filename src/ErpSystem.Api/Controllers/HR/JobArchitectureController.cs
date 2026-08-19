using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/hr/job-architecture")]
[Authorize]
public class JobArchitectureController : ControllerBase
{
    private readonly IJobArchitectureService _service;

    public JobArchitectureController(IJobArchitectureService service)
    {
        _service = service;
    }

    // ── Families ──────────────────────────────────────────────────────────────

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("families")]
    public async Task<ActionResult<IEnumerable<JobFamilyDto>>> GetFamilies() => Ok(await _service.GetFamiliesAsync());

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("families/active")]
    public async Task<ActionResult<IEnumerable<JobFamilyDto>>> GetActiveFamilies() => Ok(await _service.GetActiveFamiliesAsync());

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("families")]
    public async Task<ActionResult<JobFamilyDto>> CreateFamily([FromBody] CreateJobFamilyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateFamilyAsync(dto);
        return CreatedAtAction(nameof(GetFamilies), new { }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("families/{id:guid}")]
    public async Task<ActionResult<JobFamilyDto>> UpdateFamily(Guid id, [FromBody] UpdateJobFamilyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        return Ok(await _service.UpdateFamilyAsync(dto));
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("families/{id:guid}")]
    public async Task<IActionResult> DeleteFamily(Guid id) { await _service.DeleteFamilyAsync(id); return NoContent(); }

    // ── Sub-families ──────────────────────────────────────────────────────────

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("families/{familyId:guid}/sub-families")]
    public async Task<ActionResult<IEnumerable<JobSubFamilyDto>>> GetSubFamilies(Guid familyId) => Ok(await _service.GetSubFamiliesAsync(familyId));

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("sub-families/active")]
    public async Task<ActionResult<IEnumerable<JobSubFamilyDto>>> GetActiveSubFamilies() => Ok(await _service.GetActiveSubFamiliesAsync());

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("families/{familyId:guid}/sub-families")]
    public async Task<ActionResult<JobSubFamilyDto>> CreateSubFamily(Guid familyId, [FromBody] CreateJobSubFamilyDto dto)
    {
        dto.JobFamilyId = familyId;
        var created = await _service.CreateSubFamilyAsync(dto);
        return CreatedAtAction(nameof(GetSubFamilies), new { familyId }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("sub-families/{id:guid}")]
    public async Task<ActionResult<JobSubFamilyDto>> UpdateSubFamily(Guid id, [FromBody] UpdateJobSubFamilyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        return Ok(await _service.UpdateSubFamilyAsync(dto));
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("sub-families/{id:guid}")]
    public async Task<IActionResult> DeleteSubFamily(Guid id) { await _service.DeleteSubFamilyAsync(id); return NoContent(); }

    // ── Levels ────────────────────────────────────────────────────────────────

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("levels")]
    public async Task<ActionResult<IEnumerable<JobLevelDto>>> GetLevels() => Ok(await _service.GetLevelsAsync());

    [Authorize(Policy = HrPermissions.JobArchitectureReadPolicy)]
    [HttpGet("levels/active")]
    public async Task<ActionResult<IEnumerable<JobLevelDto>>> GetActiveLevels() => Ok(await _service.GetActiveLevelsAsync());

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPost("levels")]
    public async Task<ActionResult<JobLevelDto>> CreateLevel([FromBody] CreateJobLevelDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _service.CreateLevelAsync(dto);
        return CreatedAtAction(nameof(GetLevels), new { }, created);
    }

    [Authorize(Policy = HrPermissions.JobArchitectureWritePolicy)]
    [HttpPut("levels/{id:guid}")]
    public async Task<ActionResult<JobLevelDto>> UpdateLevel(Guid id, [FromBody] UpdateJobLevelDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        return Ok(await _service.UpdateLevelAsync(dto));
    }

    [Authorize(Policy = HrPermissions.JobArchitectureAdminPolicy)]
    [HttpDelete("levels/{id:guid}")]
    public async Task<IActionResult> DeleteLevel(Guid id) { await _service.DeleteLevelAsync(id); return NoContent(); }
}
