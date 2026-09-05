using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// PPE management (FRD §8) — HR only, with one exception: any authenticated employee can read
/// their own issuance record via <c>GET issuances/mine</c> (what was issued to me, what I still
/// hold, what has expired), stamped from the token's employee — the self-or-HR ownership read
/// for this area. The HR-gated per-employee variant serves the HR screens; broader employee
/// self-service stays with the employee-portal work (area 25).
/// </summary>
[ApiController]
[Route("api/safety/ppe")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class PpeManagementController : SheApiControllerBase
{
    // Gated per action rather than on the class: authorize attributes stack as AND, so a class-level
    // role requirement could not be relaxed for the one self-service read employees need.
    private readonly IPpeManagementService _service;

    public PpeManagementController(IPpeManagementService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Types ──
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<PpeTypeDto>>> GetTypes([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetTypesAsync(activeOnly));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("types/{id:guid}")]
    public async Task<ActionResult<PpeTypeDto>> GetType(Guid id)
        => Ok(await _service.GetTypeAsync(id));

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpPost("types")]
    public async Task<ActionResult<PpeTypeDto>> CreateType([FromBody] CreatePpeTypeDto dto)
        => Ok(await _service.CreateTypeAsync(dto, TenantId, UserId));

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpPut("types/{id:guid}")]
    public async Task<ActionResult<PpeTypeDto>> UpdateType(Guid id, [FromBody] UpdatePpeTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateTypeAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("types/{id:guid}")]
    public async Task<IActionResult> DeleteType(Guid id)
    {
        await _service.DeleteTypeAsync(id);
        return NoContent();
    }

    // ── Inventory ──
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("inventory")]
    public async Task<ActionResult<IEnumerable<PpeInventoryDto>>> GetInventory()
        => Ok(await _service.GetInventoryAsync());

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("inventory/{id:guid}")]
    public async Task<ActionResult<PpeInventoryDto>> GetInventoryItem(Guid id)
        => Ok(await _service.GetInventoryItemAsync(id));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("inventory/by-type/{ppeTypeId:guid}")]
    public async Task<ActionResult<IEnumerable<PpeInventoryDto>>> GetInventoryByType(Guid ppeTypeId)
        => Ok(await _service.GetInventoryByTypeAsync(ppeTypeId));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("inventory/below-reorder")]
    public async Task<ActionResult<IEnumerable<PpeInventoryDto>>> GetBelowReorderLevel()
        => Ok(await _service.GetBelowReorderLevelAsync());

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("inventory")]
    public async Task<ActionResult<PpeInventoryDto>> CreateInventory([FromBody] CreatePpeInventoryDto dto)
        => Ok(await _service.CreateInventoryAsync(dto, TenantId, UserId));

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPut("inventory/{id:guid}")]
    public async Task<ActionResult<PpeInventoryDto>> UpdateInventory(Guid id, [FromBody] UpdatePpeInventoryDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInventoryAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("inventory/{id:guid}/restock")]
    public async Task<IActionResult> Restock(Guid id, [FromBody] RestockPpeInventoryDto dto)
    {
        dto.PpeInventoryId = id;
        await _service.RestockAsync(dto, UserId);
        return Ok(new { message = "Stock replenished." });
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("inventory/{id:guid}")]
    public async Task<IActionResult> DeleteInventory(Guid id)
    {
        await _service.DeleteInventoryAsync(id);
        return NoContent();
    }

    // ── Issuance ──
    /// <summary>Open to any authenticated employee — their own issuance record, resolved from the
    /// token's employee. The caller cannot read anyone else's record here; HR uses the
    /// by-employee variant.</summary>
    [HttpGet("issuances/mine")]
    public async Task<ActionResult<IEnumerable<PpeIssuanceDto>>> GetMyIssuances()
        => Ok(await _service.GetIssuancesByEmployeeAsync(UserId));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("issuances/by-employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<PpeIssuanceDto>>> GetIssuancesByEmployee(Guid employeeId)
        => Ok(await _service.GetIssuancesByEmployeeAsync(employeeId));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("issuances/outstanding")]
    public async Task<ActionResult<IEnumerable<PpeIssuanceDto>>> GetOutstandingIssuances()
        => Ok(await _service.GetOutstandingIssuancesAsync());

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("issuances/overdue-returns")]
    public async Task<ActionResult<IEnumerable<PpeIssuanceDto>>> GetOverdueReturns()
        => Ok(await _service.GetOverdueReturnsAsync());

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("issuances")]
    public async Task<ActionResult<PpeIssuanceDto>> Issue([FromBody] CreatePpeIssuanceDto dto)
        => Ok(await _service.IssueAsync(dto, TenantId, UserId));

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("issuances/{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnPpeIssuanceDto dto)
    {
        dto.IssuanceId = id;
        await _service.ReturnAsync(dto, UserId);
        return Ok(new { message = "PPE return recorded." });
    }

    // ── Job-role requirements ──
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("requirements")]
    public async Task<ActionResult<IEnumerable<JobRolePpeRequirementDto>>> GetRequirements()
        => Ok(await _service.GetRequirementsAsync());

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("requirements/by-role/{jobRoleCode}")]
    public async Task<ActionResult<IEnumerable<JobRolePpeRequirementDto>>> GetRequirementsByJobRole(string jobRoleCode)
        => Ok(await _service.GetRequirementsByJobRoleAsync(jobRoleCode));

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpPost("requirements")]
    public async Task<ActionResult<JobRolePpeRequirementDto>> AddRequirement([FromBody] CreateJobRolePpeRequirementDto dto)
        => Ok(await _service.AddRequirementAsync(dto, TenantId, UserId));

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpPut("requirements/{id:guid}")]
    public async Task<ActionResult<JobRolePpeRequirementDto>> UpdateRequirement(Guid id, [FromBody] UpdateJobRolePpeRequirementDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateRequirementAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("requirements/{id:guid}")]
    public async Task<IActionResult> DeleteRequirement(Guid id)
    {
        await _service.DeleteRequirementAsync(id);
        return NoContent();
    }
}
