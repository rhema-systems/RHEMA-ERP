using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
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
    private readonly IFileStorageService _storageService;
    private readonly ILogger<AssetAdmissionsController> _logger;

    public AssetAdmissionsController(
        IAssetAdmissionService admissionService,
        IFileStorageService storageService,
        ILogger<AssetAdmissionsController> logger)
    {
        _admissionService = admissionService;
        _storageService = storageService;
        _logger = logger;
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
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            // Validate file extension (images only)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = $"File type {extension} is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}" });
            }

            // Generate unique file path
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = $"maintenance/admissions/{id}/{fileName}";

            // Upload file using storage service
            using var stream = file.OpenReadStream();
            var uploadedPath = await _storageService.UploadFileAsync(stream, file.FileName, filePath);

            if (string.IsNullOrEmpty(uploadedPath))
            {
                return StatusCode(500, new { message = "Failed to upload file" });
            }

            _logger.LogInformation("Uploaded admission photo for admission {AdmissionId}: {FilePath}", id, uploadedPath);

            return Ok(new { filePath = uploadedPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading photo for admission {AdmissionId}", id);
            return StatusCode(500, new { message = "Failed to upload photo", error = ex.Message });
        }
    }

}

public class CancelAdmissionRequest
{
    public string? Reason { get; set; }
}
