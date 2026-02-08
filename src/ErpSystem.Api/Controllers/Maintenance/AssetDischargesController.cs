using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/asset-discharges")]
[Authorize]
public class AssetDischargesController : ControllerBase
{
    private readonly IAssetDischargeService _dischargeService;
    private readonly IFileStorageService _storageService;
    private readonly ILogger<AssetDischargesController> _logger;

    public AssetDischargesController(
        IAssetDischargeService dischargeService,
        IFileStorageService storageService,
        ILogger<AssetDischargesController> logger)
    {
        _dischargeService = dischargeService;
        _storageService = storageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AssetDischargeDto>>> GetDischarges([FromQuery] DischargeQueryParameters query)
    {
        var result = await _dischargeService.GetDischargesAsync(query);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetDischargeDto>> GetDischargeById(Guid id)
    {
        var result = await _dischargeService.GetDischargeByIdAsync(id);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("by-admission/{admissionId:guid}")]
    public async Task<ActionResult<IEnumerable<AssetDischargeDto>>> GetByAdmission(Guid admissionId)
    {
        var result = await _dischargeService.GetDischargesByAdmissionAsync(admissionId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<AssetDischargeDto>> CreateDischarge([FromBody] CreateAssetDischargeDto dto)
    {
        var created = await _dischargeService.CreateDischargeAsync(dto);
        return CreatedAtAction(nameof(GetDischargeById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssetDischargeDto>> UpdateDischarge(Guid id, [FromBody] UpdateAssetDischargeDto dto)
    {
        var updated = await _dischargeService.UpdateDischargeAsync(id, dto);
        return Ok(updated);
    }

    [HttpPost("{id:guid}/generate-certificate")]
    public async Task<ActionResult<object>> GenerateCertificate(Guid id)
    {
        var cert = await _dischargeService.GenerateCompletionCertificateAsync(id);
        return Ok(new { cert.CertificateNumber });
    }

    [HttpPost("{id:guid}/photos")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<object>> UploadDischargePhoto(Guid id, IFormFile file)
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
            var filePath = $"maintenance/discharges/{id}/{fileName}";

            // Upload file using storage service
            using var stream = file.OpenReadStream();
            var uploadedPath = await _storageService.UploadFileAsync(stream, file.FileName, filePath);

            if (string.IsNullOrEmpty(uploadedPath))
            {
                return StatusCode(500, new { message = "Failed to upload file" });
            }

            _logger.LogInformation("Uploaded discharge photo for discharge {DischargeId}: {FilePath}", id, uploadedPath);

            return Ok(new { filePath = uploadedPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading photo for discharge {DischargeId}", id);
            return StatusCode(500, new { message = "Failed to upload photo", error = ex.Message });
        }
    }

}

