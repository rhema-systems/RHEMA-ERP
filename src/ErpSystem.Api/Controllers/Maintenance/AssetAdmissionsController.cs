using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/asset-admissions")]
[Authorize]
public class AssetAdmissionsController : ControllerBase
{
    private readonly IAssetAdmissionService _admissionService;

    public AssetAdmissionsController(IAssetAdmissionService admissionService)
    {
        _admissionService = admissionService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetAdmissionDto>>> GetAdmissions([FromQuery] AdmissionQueryParameters query)
    {
        var result = await _admissionService.GetAdmissionsAsync(query);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetAdmissionDto>> GetAdmissionById(Guid id)
    {
        var result = await _admissionService.GetAdmissionByIdAsync(id);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<AssetAdmissionDto>> CreateAdmission([FromBody] CreateAssetAdmissionDto dto)
    {
        var created = await _admissionService.CreateAdmissionAsync(dto);
        return CreatedAtAction(nameof(GetAdmissionById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssetAdmissionDto>> UpdateAdmission(Guid id, [FromBody] UpdateAssetAdmissionDto dto)
    {
        var updated = await _admissionService.UpdateAdmissionAsync(id, dto);
        return Ok(updated);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelAdmission(Guid id, [FromBody] CancelAdmissionRequest request)
    {
        await _admissionService.CancelAdmissionAsync(id, request.Reason);
        return NoContent();
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<AssetAdmissionDto>>> GetActiveAdmissions()
    {
        var result = await _admissionService.GetActiveAdmissionsAsync();
        return Ok(result);
    }

    [HttpGet("by-asset/{assetId:guid}")]
    public async Task<ActionResult<IEnumerable<AssetAdmissionDto>>> GetByAsset(Guid assetId)
    {
        var result = await _admissionService.GetAdmissionsByAssetAsync(assetId);
        return Ok(result);
    }

    [HttpGet("by-work-order/{workOrderId:guid}")]
    public async Task<ActionResult<IEnumerable<AssetAdmissionDto>>> GetByWorkOrder(Guid workOrderId)
    {
        var result = await _admissionService.GetAdmissionsByWorkOrderAsync(workOrderId);
        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdmissionStatsDto>> GetStats([FromQuery] AdmissionStatsQuery query)
    {
        var result = await _admissionService.GetAdmissionStatsAsync(query);
        return Ok(result);
    }

    [HttpGet("downtime-report")]
    public async Task<ActionResult<DowntimeSummaryDto>> GetDowntimeReport([FromQuery] DowntimeReportQuery query)
    {
        var result = await _admissionService.GetAdmissionDowntimeReportAsync(query);
        return Ok(result);
    }

    [HttpPost("{id:guid}/photos")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<object>> UploadAdmissionPhoto(Guid id, IFormFile file)
    {
        // For P1 we keep this minimal: delegate to a shared file upload service in a later phase.
        // For now, return a stub path so the UI flow is unblocked.
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        var fakePath = $"/uploads/maintenance/admissions/{id}/{file.FileName}";
        return Ok(new { filePath = fakePath });
    }

}

public class CancelAdmissionRequest
{
    public string? Reason { get; set; }
}
