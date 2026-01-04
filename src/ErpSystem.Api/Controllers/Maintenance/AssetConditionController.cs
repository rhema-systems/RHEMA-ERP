using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

/// <summary>
/// Controller for managing asset condition checklists and inspections.
/// Handles templates, condition records, and item results for admission/discharge inspections.
/// </summary>
[ApiController]
[Route("api/maintenance/asset-conditions")]
[Authorize]
public class AssetConditionController : ControllerBase
{
    private readonly IAssetConditionService _service;
    private readonly ILogger<AssetConditionController> _logger;

    public AssetConditionController(IAssetConditionService service, ILogger<AssetConditionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    #region Template Endpoints

    /// <summary>
    /// Get all asset condition checklist templates
    /// </summary>
    [HttpGet("templates")]
    public async Task<ActionResult<IEnumerable<AssetConditionChecklistTemplateDto>>> GetAllTemplates(
        [FromQuery] bool includeInactive = false)
    {
        try
        {
            var templates = await _service.GetAllTemplatesAsync(includeInactive);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset condition templates");
            return StatusCode(500, "An error occurred while retrieving templates");
        }
    }

    /// <summary>
    /// Get a specific template by ID with all checklist items
    /// </summary>
    [HttpGet("templates/{id}")]
    public async Task<ActionResult<AssetConditionChecklistTemplateDto>> GetTemplateById(Guid id)
    {
        try
        {
            var template = await _service.GetTemplateWithItemsAsync(id);
            return Ok(template);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Template with ID {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template {TemplateId}", id);
            return StatusCode(500, "An error occurred while retrieving the template");
        }
    }

    /// <summary>
    /// Get templates for a specific asset category
    /// </summary>
    [HttpGet("templates/by-asset-category/{assetCategoryId}")]
    public async Task<ActionResult<IEnumerable<AssetConditionChecklistTemplateDto>>> GetTemplatesByAssetCategory(Guid assetCategoryId)
    {
        try
        {
            var templates = await _service.GetTemplatesByAssetCategoryAsync(assetCategoryId);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting templates for asset category {AssetCategoryId}", assetCategoryId);
            return StatusCode(500, "An error occurred while retrieving templates");
        }
    }

    /// <summary>
    /// Get the default template for a specific asset category
    /// </summary>
    [HttpGet("templates/default/{assetCategoryId}")]
    public async Task<ActionResult<AssetConditionChecklistTemplateDto>> GetDefaultTemplateForAssetCategory(Guid assetCategoryId)
    {
        try
        {
            var template = await _service.GetDefaultTemplateForAssetCategoryAsync(assetCategoryId);
            if (template == null)
                return NotFound($"No default template found for asset category {assetCategoryId}");
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting default template for asset category {AssetCategoryId}", assetCategoryId);
            return StatusCode(500, "An error occurred while retrieving the template");
        }
    }

    /// <summary>
    /// Create a new asset condition checklist template
    /// </summary>
    [HttpPost("templates")]
    public async Task<ActionResult<AssetConditionChecklistTemplateDto>> CreateTemplate(
        [FromBody] CreateAssetConditionTemplateDto dto)
    {
        try
        {
            var template = await _service.CreateTemplateAsync(dto);
            return CreatedAtAction(nameof(GetTemplateById), new { id = template.Id }, template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating asset condition template");
            return StatusCode(500, "An error occurred while creating the template");
        }
    }

    /// <summary>
    /// Update an existing template
    /// </summary>
    [HttpPut("templates/{id}")]
    public async Task<ActionResult<AssetConditionChecklistTemplateDto>> UpdateTemplate(
        Guid id, [FromBody] UpdateAssetConditionTemplateDto dto)
    {
        try
        {
            var template = await _service.UpdateTemplateAsync(id, dto);
            return Ok(template);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Template with ID {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {TemplateId}", id);
            return StatusCode(500, "An error occurred while updating the template");
        }
    }

    /// <summary>
    /// Delete a template (soft delete)
    /// </summary>
    [HttpDelete("templates/{id}")]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        try
        {
            await _service.DeleteTemplateAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Template with ID {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {TemplateId}", id);
            return StatusCode(500, "An error occurred while deleting the template");
        }
    }

    #endregion

    #region Record Endpoints

    /// <summary>
    /// Get all asset condition records with pagination
    /// </summary>
    [HttpGet("records")]
    public async Task<ActionResult<IEnumerable<AssetConditionRecordSummaryDto>>> GetAllRecords(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var records = await _service.GetAllRecordsAsync(page, pageSize);
            return Ok(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset condition records");
            return StatusCode(500, "An error occurred while retrieving records");
        }
    }

    /// <summary>
    /// Get a specific condition record by ID with all item results
    /// </summary>
    [HttpGet("records/{id}")]
    public async Task<ActionResult<AssetConditionRecordDto>> GetRecordById(Guid id)
    {
        try
        {
            var record = await _service.GetRecordWithDetailsAsync(id);
            return Ok(record);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Asset condition record with ID {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting record {RecordId}", id);
            return StatusCode(500, "An error occurred while retrieving the record");
        }
    }

    /// <summary>
    /// Get all condition records for a specific asset
    /// </summary>
    [HttpGet("records/by-asset/{assetId}")]
    public async Task<ActionResult<IEnumerable<AssetConditionRecordSummaryDto>>> GetRecordsByAsset(Guid assetId)
    {
        try
        {
            var records = await _service.GetRecordsByAssetAsync(assetId);
            return Ok(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting records for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while retrieving records");
        }
    }

    /// <summary>
    /// Get the admission condition record for a specific admission
    /// </summary>
    [HttpGet("records/by-admission/{admissionId}")]
    public async Task<ActionResult<AssetConditionRecordDto>> GetAdmissionRecordForAdmission(Guid admissionId)
    {
        try
        {
            var record = await _service.GetAdmissionRecordForAdmissionAsync(admissionId);
            if (record == null)
                return NotFound($"No admission condition record found for admission {admissionId}");
            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admission record for admission {AdmissionId}", admissionId);
            return StatusCode(500, "An error occurred while retrieving the record");
        }
    }

    /// <summary>
    /// Get the discharge condition record for a specific admission
    /// </summary>
    [HttpGet("records/discharge-by-admission/{admissionId}")]
    public async Task<ActionResult<AssetConditionRecordDto>> GetDischargeRecordForAdmission(Guid admissionId)
    {
        try
        {
            var record = await _service.GetDischargeRecordForAdmissionAsync(admissionId);
            if (record == null)
                return NotFound($"No discharge condition record found for admission {admissionId}");
            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discharge record for admission {AdmissionId}", admissionId);
            return StatusCode(500, "An error occurred while retrieving the record");
        }
    }

    /// <summary>
    /// Get the admission condition record for a specific job card
    /// </summary>
    [HttpGet("records/by-job-card/{jobCardId}")]
    public async Task<ActionResult<AssetConditionRecordDto>> GetAdmissionRecordForJobCard(Guid jobCardId)
    {
        try
        {
            var record = await _service.GetAdmissionRecordForJobCardAsync(jobCardId);
            if (record == null)
                return NotFound($"No admission condition record found for job card {jobCardId}");
            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admission record for job card {JobCardId}", jobCardId);
            return StatusCode(500, "An error occurred while retrieving the record");
        }
    }

    /// <summary>
    /// Get the discharge condition record for a specific job card
    /// </summary>
    [HttpGet("records/discharge-by-job-card/{jobCardId}")]
    public async Task<ActionResult<AssetConditionRecordDto>> GetDischargeRecordForJobCard(Guid jobCardId)
    {
        try
        {
            var record = await _service.GetDischargeRecordForJobCardAsync(jobCardId);
            if (record == null)
                return NotFound($"No discharge condition record found for job card {jobCardId}");
            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discharge record for job card {JobCardId}", jobCardId);
            return StatusCode(500, "An error occurred while retrieving the record");
        }
    }

    /// <summary>
    /// Start a new asset condition inspection
    /// </summary>
    [HttpPost("records/start")]
    public async Task<ActionResult<AssetConditionRecordDto>> StartConditionInspection(
        [FromBody] CreateAssetConditionRecordDto dto)
    {
        try
        {
            var record = await _service.StartConditionInspectionAsync(dto);
            return CreatedAtAction(nameof(GetRecordById), new { id = record.Id }, record);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting condition inspection");
            return StatusCode(500, "An error occurred while starting the inspection");
        }
    }

    /// <summary>
    /// Submit or update a result for a specific checklist item
    /// </summary>
    [HttpPost("records/{recordId}/items")]
    public async Task<ActionResult<AssetConditionItemResultDto>> SubmitItemResult(
        Guid recordId, [FromBody] SubmitAssetConditionItemDto dto)
    {
        try
        {
            var result = await _service.SubmitItemResultAsync(recordId, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting item result for record {RecordId}", recordId);
            return StatusCode(500, "An error occurred while submitting the item result");
        }
    }

    /// <summary>
    /// Complete a condition inspection
    /// </summary>
    [HttpPost("records/{recordId}/complete")]
    public async Task<ActionResult<AssetConditionRecordDto>> CompleteConditionInspection(
        Guid recordId, [FromBody] CompleteAssetConditionRecordDto dto)
    {
        try
        {
            var record = await _service.CompleteConditionInspectionAsync(recordId, dto);
            return Ok(record);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Asset condition record with ID {recordId} not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing condition inspection {RecordId}", recordId);
            return StatusCode(500, "An error occurred while completing the inspection");
        }
    }

    /// <summary>
    /// Cancel a condition inspection
    /// </summary>
    [HttpPost("records/{recordId}/cancel")]
    public async Task<ActionResult> CancelConditionInspection(Guid recordId)
    {
        try
        {
            await _service.CancelConditionInspectionAsync(recordId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Asset condition record with ID {recordId} not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling condition inspection {RecordId}", recordId);
            return StatusCode(500, "An error occurred while cancelling the inspection");
        }
    }

    /// <summary>
    /// Link a condition record to an admission
    /// </summary>
    [HttpPost("records/{recordId}/link-admission/{admissionId}")]
    public async Task<ActionResult> LinkToAdmission(Guid recordId, Guid admissionId)
    {
        try
        {
            await _service.LinkToAdmissionAsync(recordId, admissionId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Asset condition record with ID {recordId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking condition record {RecordId} to admission {AdmissionId}", recordId, admissionId);
            return StatusCode(500, "An error occurred while linking to admission");
        }
    }

    /// <summary>
    /// Link a condition record to a discharge
    /// </summary>
    [HttpPost("records/{recordId}/link-discharge/{dischargeId}")]
    public async Task<ActionResult> LinkToDischarge(Guid recordId, Guid dischargeId)
    {
        try
        {
            await _service.LinkToDischargeAsync(recordId, dischargeId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Asset condition record with ID {recordId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking condition record {RecordId} to discharge {DischargeId}", recordId, dischargeId);
            return StatusCode(500, "An error occurred while linking to discharge");
        }
    }

    /// <summary>
    /// Upload a photo to a checklist item result
    /// </summary>
    [HttpPost("records/{recordId}/items/{itemResultId}/photos")]
    public async Task<ActionResult<AssetConditionRecordDto>> UploadItemPhoto(
        Guid recordId, Guid itemResultId, [FromBody] AddPhotoDto dto)
    {
        try
        {
            var record = await _service.UploadItemPhotoAsync(recordId, itemResultId, dto.PhotoPath);
            return Ok(record);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading photo to item result {ItemResultId}", itemResultId);
            return StatusCode(500, "An error occurred while uploading the photo");
        }
    }

    #endregion
}

/// <summary>
/// DTO for adding a photo to an item result
/// </summary>
public class AddPhotoDto
{
    public string PhotoPath { get; set; } = string.Empty;
}

