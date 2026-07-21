using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/ppe")]
[Authorize]
public class PpeManagementController : SheApiControllerBase
{
    private readonly IPpeManagementService _service;

    public PpeManagementController(IPpeManagementService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Types ──
    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<PpeTypeDto>>> GetTypes([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetTypesAsync(activeOnly));

    [HttpGet("types/{id:guid}")]
    public async Task<ActionResult<PpeTypeDto>> GetType(Guid id)
        => Ok(await _service.GetTypeAsync(id));

    [HttpPost("types")]
    public async Task<ActionResult<PpeTypeDto>> CreateType([FromBody] CreatePpeTypeDto dto)
        => Ok(await _service.CreateTypeAsync(dto, TenantId, UserId));

    [HttpPut("types/{id:guid}")]
    public async Task<ActionResult<PpeTypeDto>> UpdateType(Guid id, [FromBody] UpdatePpeTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateTypeAsync(dto, UserId));
    }

    [HttpDelete("types/{id:guid}")]
    public async Task<IActionResult> DeleteType(Guid id)
    {
        await _service.DeleteTypeAsync(id);
        return NoContent();
    }

    // ── Inventory ──
    [HttpGet("inventory/{id:guid}")]
    public async Task<ActionResult<PpeInventoryDto>> GetInventoryItem(Guid id)
        => Ok(await _service.GetInventoryItemAsync(id));

    [HttpGet("inventory/by-type/{ppeTypeId:guid}")]
    public async Task<ActionResult<IEnumerable<PpeInventoryDto>>> GetInventoryByType(Guid ppeTypeId)
        => Ok(await _service.GetInventoryByTypeAsync(ppeTypeId));

    [HttpGet("inventory/below-reorder")]
    public async Task<ActionResult<IEnumerable<PpeInventoryDto>>> GetBelowReorderLevel()
        => Ok(await _service.GetBelowReorderLevelAsync());

    [HttpPost("inventory")]
    public async Task<ActionResult<PpeInventoryDto>> CreateInventory([FromBody] CreatePpeInventoryDto dto)
        => Ok(await _service.CreateInventoryAsync(dto, TenantId, UserId));

    [HttpPut("inventory/{id:guid}")]
    public async Task<ActionResult<PpeInventoryDto>> UpdateInventory(Guid id, [FromBody] UpdatePpeInventoryDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInventoryAsync(dto, UserId));
    }

    [HttpPost("inventory/{id:guid}/restock")]
    public async Task<IActionResult> Restock(Guid id, [FromBody] RestockPpeInventoryDto dto)
    {
        dto.PpeInventoryId = id;
        await _service.RestockAsync(dto, UserId);
        return Ok(new { message = "Stock replenished." });
    }

    [HttpDelete("inventory/{id:guid}")]
    public async Task<IActionResult> DeleteInventory(Guid id)
    {
        await _service.DeleteInventoryAsync(id);
        return NoContent();
    }

    // ── Issuance ──
    [HttpGet("issuances/by-employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<PpeIssuanceDto>>> GetIssuancesByEmployee(Guid employeeId)
        => Ok(await _service.GetIssuancesByEmployeeAsync(employeeId));

    [HttpGet("issuances/outstanding")]
    public async Task<ActionResult<IEnumerable<PpeIssuanceDto>>> GetOutstandingIssuances()
        => Ok(await _service.GetOutstandingIssuancesAsync());

    [HttpGet("issuances/overdue-returns")]
    public async Task<ActionResult<IEnumerable<PpeIssuanceDto>>> GetOverdueReturns()
        => Ok(await _service.GetOverdueReturnsAsync());

    [HttpPost("issuances")]
    public async Task<ActionResult<PpeIssuanceDto>> Issue([FromBody] CreatePpeIssuanceDto dto)
        => Ok(await _service.IssueAsync(dto, TenantId, UserId));

    [HttpPost("issuances/{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnPpeIssuanceDto dto)
    {
        dto.IssuanceId = id;
        await _service.ReturnAsync(dto, UserId);
        return Ok(new { message = "PPE return recorded." });
    }

    // ── Job-role requirements ──
    [HttpGet("requirements/by-role/{jobRoleCode}")]
    public async Task<ActionResult<IEnumerable<JobRolePpeRequirementDto>>> GetRequirementsByJobRole(string jobRoleCode)
        => Ok(await _service.GetRequirementsByJobRoleAsync(jobRoleCode));

    [HttpPost("requirements")]
    public async Task<ActionResult<JobRolePpeRequirementDto>> AddRequirement([FromBody] CreateJobRolePpeRequirementDto dto)
        => Ok(await _service.AddRequirementAsync(dto, TenantId, UserId));

    [HttpPut("requirements/{id:guid}")]
    public async Task<ActionResult<JobRolePpeRequirementDto>> UpdateRequirement(Guid id, [FromBody] UpdateJobRolePpeRequirementDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateRequirementAsync(dto, UserId));
    }

    [HttpDelete("requirements/{id:guid}")]
    public async Task<IActionResult> DeleteRequirement(Guid id)
    {
        await _service.DeleteRequirementAsync(id);
        return NoContent();
    }
}
