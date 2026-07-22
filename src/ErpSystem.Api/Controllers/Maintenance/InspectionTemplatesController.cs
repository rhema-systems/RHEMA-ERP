using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/inspection-templates")]
[Authorize]
public class InspectionTemplatesController : ControllerBase
{
    private readonly IInspectionTemplateService _service;
    private readonly ILogger<InspectionTemplatesController> _logger;
    private readonly IConfiguration _configuration;

    public InspectionTemplatesController(
        IInspectionTemplateService service,
        ILogger<InspectionTemplatesController> logger,
        IConfiguration configuration)
    {
        _service = service;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<InspectionTemplateDto>>> GetAll(
        [FromQuery] bool activeOnly = false,
        [FromQuery] string? category = null,
        [FromQuery] string? sheetType = null,
        [FromQuery] string? frequency = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? templateScope = null,
        [FromQuery] string? fleetInspectionKind = null,
        [FromQuery] Guid? assignedAssetCategoryId = null,
        [FromQuery] Guid? assignedAssetId = null,
        [FromQuery] bool? isQrEnabled = null,
        [FromQuery] bool? mobileOfflineEnabled = null)
    {
        try
        {
            return Ok(await _service.GetTemplatesAsync(new InspectionTemplateFilterDto
            {
                SearchTerm = searchTerm,
                Category = category,
                SheetType = sheetType,
                Frequency = frequency,
                TemplateScope = templateScope,
                FleetInspectionKind = fleetInspectionKind,
                AssignedAssetCategoryId = assignedAssetCategoryId,
                AssignedAssetId = assignedAssetId,
                IsQrEnabled = isQrEnabled,
                MobileOfflineEnabled = mobileOfflineEnabled,
                IsActive = activeOnly ? true : null
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspection templates");
            return StatusCode(500, "An error occurred while retrieving templates");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InspectionTemplateDto>> GetById(Guid id)
    {
        try
        {
            var item = await _service.GetTemplateByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspection template {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the template");
        }
    }

    [HttpGet("{id:guid}/qr-package")]
    public async Task<ActionResult<InspectionTemplateQrPackageDto>> GetQrPackage(
        Guid id,
        [FromQuery] Guid? assetId = null,
        [FromQuery] Guid? assetCategoryId = null,
        [FromQuery] Guid? fleetTripId = null,
        [FromQuery] string? inspectionKind = null,
        [FromQuery] bool includeEmbeddedPayload = true,
        [FromQuery] int maxQrPayloadBytes = 2500)
    {
        try
        {
            var frontendUrl = _configuration["FrontendUrl"];
            if (string.IsNullOrWhiteSpace(frontendUrl))
            {
                frontendUrl = Request.Headers.Origin.FirstOrDefault();
            }
            if (string.IsNullOrWhiteSpace(frontendUrl))
            {
                frontendUrl = $"{Request.Scheme}://{Request.Host}";
            }

            var package = await _service.GetQrPackageAsync(id, new InspectionTemplateQrPackageRequestDto
            {
                AssetId = assetId,
                AssetCategoryId = assetCategoryId,
                FleetTripId = fleetTripId,
                InspectionKind = inspectionKind,
                FrontendBaseUrl = frontendUrl,
                IncludeEmbeddedPayload = includeEmbeddedPayload,
                MaxQrPayloadBytes = maxQrPayloadBytes
            });

            return Ok(package);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating inspection template QR package {Id}", id);
            return StatusCode(500, "An error occurred while generating the QR package");
        }
    }

    [HttpPost]
    public async Task<ActionResult<InspectionTemplateDto>> Create([FromBody] CreateInspectionTemplateDto dto)
    {
        try
        {
            var created = await _service.CreateTemplateAsync(dto);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inspection template");
            return StatusCode(500, "An error occurred while creating the template");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InspectionTemplateDto>> Update(Guid id, [FromBody] UpdateInspectionTemplateDto dto)
    {
        try
        {
            var updated = await _service.UpdateTemplateAsync(id, dto);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inspection template {Id}", id);
            return StatusCode(500, "An error occurred while updating the template");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteTemplateAsync(id);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inspection template {Id}", id);
            return StatusCode(500, "An error occurred while deleting the template");
        }
    }
}

