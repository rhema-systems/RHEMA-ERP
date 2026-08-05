using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/equipment")]
[Authorize]
public class SafetyEquipmentController : SheApiControllerBase
{
    private readonly ISafetyEquipmentService _service;

    public SafetyEquipmentController(ISafetyEquipmentService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SafetyEquipmentSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SafetyEquipmentDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{equipmentNumber}")]
    public async Task<ActionResult<SafetyEquipmentDto?>> GetByNumber(string equipmentNumber)
        => Ok(await _service.GetByNumberAsync(equipmentNumber));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SafetyEquipmentSummaryDto>>> GetByStatus(SheSafetyEquipmentStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<SafetyEquipmentSummaryDto>>> GetByType(SheSafetyEquipmentType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyEquipmentSummaryDto>>> GetByLocation(Guid locationId)
        => Ok(await _service.GetByLocationAsync(locationId));

    [HttpGet("due-for-inspection")]
    public async Task<ActionResult<IEnumerable<SafetyEquipmentSummaryDto>>> GetDueForInspection([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetDueForInspectionAsync(daysAhead));

    [HttpGet("due-for-maintenance")]
    public async Task<ActionResult<IEnumerable<SafetyEquipmentSummaryDto>>> GetDueForMaintenance([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetDueForMaintenanceAsync(daysAhead));

    [HttpGet("expiring-certification")]
    public async Task<ActionResult<IEnumerable<SafetyEquipmentSummaryDto>>> GetExpiringCertification([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetExpiringCertificationAsync(daysAhead));

    [HttpGet("out-of-service")]
    public async Task<ActionResult<IEnumerable<SafetyEquipmentSummaryDto>>> GetOutOfService()
        => Ok(await _service.GetOutOfServiceAsync());

    [HttpPost]
    public async Task<ActionResult<SafetyEquipmentDto>> Create([FromBody] CreateSafetyEquipmentDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SafetyEquipmentDto>> Update(Guid id, [FromBody] UpdateSafetyEquipmentDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // ── Inspections ──
    [HttpGet("{id:guid}/inspections")]
    public async Task<ActionResult<IEnumerable<SafetyEquipmentInspectionDto>>> GetInspections(Guid id)
        => Ok(await _service.GetInspectionsForEquipmentAsync(id));

    [HttpPost("{id:guid}/inspections")]
    public async Task<ActionResult<SafetyEquipmentInspectionDto>> AddInspection(Guid id, [FromBody] CreateSafetyEquipmentInspectionDto dto)
    {
        dto.EquipmentId = id;
        return Ok(await _service.AddInspectionAsync(dto, TenantId, UserId));
    }

    [HttpPut("inspections/{inspectionId:guid}")]
    public async Task<ActionResult<SafetyEquipmentInspectionDto>> UpdateInspection(Guid inspectionId, [FromBody] UpdateSafetyEquipmentInspectionDto dto)
    {
        if (inspectionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInspectionAsync(dto, UserId));
    }

    [HttpPost("inspections/{inspectionId:guid}/actions")]
    public async Task<ActionResult<SafetyEquipmentInspectionActionDto>> AddInspectionAction(Guid inspectionId, [FromBody] CreateSafetyEquipmentInspectionActionDto dto)
    {
        dto.SafetyEquipmentInspectionId = inspectionId;
        return Ok(await _service.AddInspectionActionAsync(dto, TenantId, UserId));
    }

    [HttpPut("inspection-actions/{actionId:guid}")]
    public async Task<ActionResult<SafetyEquipmentInspectionActionDto>> UpdateInspectionAction(Guid actionId, [FromBody] UpdateSafetyEquipmentInspectionActionDto dto)
    {
        if (actionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateInspectionActionAsync(dto, UserId));
    }

    [HttpDelete("inspection-actions/{actionId:guid}")]
    public async Task<IActionResult> DeleteInspectionAction(Guid actionId)
    {
        await _service.DeleteInspectionActionAsync(actionId);
        return NoContent();
    }

    // ── Maintenance ──
    [HttpPost("{id:guid}/maintenance")]
    public async Task<ActionResult<SafetyEquipmentMaintenanceDto>> AddMaintenance(Guid id, [FromBody] CreateSafetyEquipmentMaintenanceDto dto)
    {
        dto.EquipmentId = id;
        return Ok(await _service.AddMaintenanceAsync(dto, TenantId, UserId));
    }

    [HttpPut("maintenance/{maintenanceId:guid}")]
    public async Task<ActionResult<SafetyEquipmentMaintenanceDto>> UpdateMaintenance(Guid maintenanceId, [FromBody] UpdateSafetyEquipmentMaintenanceDto dto)
    {
        if (maintenanceId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateMaintenanceAsync(dto, UserId));
    }

    [HttpDelete("maintenance/{maintenanceId:guid}")]
    public async Task<IActionResult> DeleteMaintenance(Guid maintenanceId)
    {
        await _service.DeleteMaintenanceAsync(maintenanceId);
        return NoContent();
    }
}
