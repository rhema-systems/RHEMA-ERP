using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/waste")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheWasteManagementController : SheApiControllerBase
{
    private readonly ISheWasteManagementService _service;

    public SheWasteManagementController(ISheWasteManagementService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    // ── Waste types ──
    [HttpGet("types")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheWasteTypeDto>>> GetWasteTypes([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetWasteTypesAsync(activeOnly));

    [HttpGet("types/classification/{classification}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheWasteTypeDto>>> GetWasteTypesByClassification(SheWasteClassification classification)
        => Ok(await _service.GetWasteTypesByClassificationAsync(classification));

    [HttpPost("types")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheWasteTypeDto>> CreateWasteType([FromBody] CreateSheWasteTypeDto dto)
        => Ok(await _service.CreateWasteTypeAsync(dto, TenantId, UserId));

    [HttpPut("types/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheWasteTypeDto>> UpdateWasteType(Guid id, [FromBody] UpdateSheWasteTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateWasteTypeAsync(dto, UserId));
    }

    [HttpDelete("types/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteWasteType(Guid id)
    {
        await _service.DeleteWasteTypeAsync(id);
        return NoContent();
    }

    // ── Disposal records ──
    [HttpGet("records/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheWasteDisposalRecordDto>> GetDisposalRecord(Guid id)
        => Ok(await _service.GetDisposalRecordAsync(id));

    [HttpGet("records/by-waste-type/{wasteTypeId:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheWasteDisposalRecordSummaryDto>>> GetByWasteType(Guid wasteTypeId)
        => Ok(await _service.GetDisposalRecordsByWasteTypeAsync(wasteTypeId));

    [HttpGet("records/date-range")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheWasteDisposalRecordSummaryDto>>> GetByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetDisposalRecordsByDateRangeAsync(from, to));

    [HttpGet("records/by-contractor/{contractorId:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheWasteDisposalRecordSummaryDto>>> GetByContractor(Guid contractorId)
        => Ok(await _service.GetDisposalRecordsByContractorAsync(contractorId));

    [HttpPost("records")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheWasteDisposalRecordDto>> CreateDisposalRecord([FromBody] CreateSheWasteDisposalRecordDto dto)
    {
        var created = await _service.CreateDisposalRecordAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetDisposalRecord), new { id = created.Id }, created);
    }

    [HttpPut("records/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheWasteDisposalRecordDto>> UpdateDisposalRecord(Guid id, [FromBody] UpdateSheWasteDisposalRecordDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateDisposalRecordAsync(dto, UserId));
    }

    [HttpDelete("records/{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteDisposalRecord(Guid id)
    {
        await _service.DeleteDisposalRecordAsync(id);
        return NoContent();
    }
}
