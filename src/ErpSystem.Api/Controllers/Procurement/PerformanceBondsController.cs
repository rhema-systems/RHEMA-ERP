using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/performance-bonds")]
[Authorize]
public class PerformanceBondsController : ControllerBase
{
    private readonly IPerformanceBondService _service;
    private readonly ILogger<PerformanceBondsController> _logger;

    public PerformanceBondsController(
        IPerformanceBondService service,
        ILogger<PerformanceBondsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get performance bond request by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PerformanceBondRequestDto>> GetById(Guid id)
    {
        var request = await _service.GetByIdAsync(id);
        if (request == null) return NotFound();
        return Ok(request);
    }

    /// <summary>
    /// Get performance bond request by award ID
    /// </summary>
    [HttpGet("by-award/{awardId}")]
    public async Task<ActionResult<PerformanceBondRequestDto>> GetByAwardId(Guid awardId)
    {
        var request = await _service.GetByAwardIdAsync(awardId);
        if (request == null) return NotFound();
        return Ok(request);
    }

    /// <summary>
    /// Get performance bond request by bid ID
    /// </summary>
    [HttpGet("by-bid/{bidId}")]
    public async Task<ActionResult<PerformanceBondRequestDto>> GetByBidId(Guid bidId)
    {
        var request = await _service.GetByBidIdAsync(bidId);
        if (request == null) return NotFound();
        return Ok(request);
    }

    /// <summary>
    /// Get performance bond requests by business partner ID
    /// </summary>
    [HttpGet("by-business-partner/{businessPartnerId}")]
    public async Task<ActionResult<IEnumerable<PerformanceBondRequestDto>>> GetByBusinessPartnerId(Guid businessPartnerId)
    {
        var requests = await _service.GetByBusinessPartnerIdAsync(businessPartnerId);
        return Ok(requests);
    }

    /// <summary>
    /// Get pending performance bond requests
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<PerformanceBondRequestDto>>> GetPending()
    {
        var requests = await _service.GetPendingRequestsAsync();
        return Ok(requests);
    }

    /// <summary>
    /// Get performance bond requests by status
    /// </summary>
    [HttpGet("by-status/{status}")]
    public async Task<ActionResult<IEnumerable<PerformanceBondRequestDto>>> GetByStatus(string status)
    {
        var requests = await _service.GetByStatusAsync(status);
        return Ok(requests);
    }

    /// <summary>
    /// Create a new performance bond request (internal user sends to business partner)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<PerformanceBondRequestDto>> CreateRequest(
        IFormFile? templateFile,
        [FromForm] Guid tenderAwardId,
        [FromForm] Guid tenderBidId,
        [FromForm] Guid businessPartnerId,
        [FromForm] string? notes = null)
    {
        try
        {
            string? templateFilePath = null;
            string? templateFileName = null;
            string? templateFileType = null;
            long? templateFileSize = null;

            if (templateFile != null && templateFile.Length > 0)
            {
                var uploadsFolder = Path.Combine("uploads", "performance-bonds", "templates");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}_{templateFile.FileName}";
                templateFilePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(templateFilePath, FileMode.Create))
                {
                    await templateFile.CopyToAsync(stream);
                }

                templateFileName = templateFile.FileName;
                templateFileType = templateFile.ContentType;
                templateFileSize = templateFile.Length;
            }

            var dto = new CreatePerformanceBondRequestDto
            {
                TenderAwardId = tenderAwardId,
                TenderBidId = tenderBidId,
                BusinessPartnerId = businessPartnerId,
                Notes = notes
            };

            var result = await _service.CreateRequestAsync(dto, templateFilePath, templateFileName, templateFileType, templateFileSize);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating performance bond request");
            return StatusCode(500, "An error occurred while creating the performance bond request");
        }
    }

    /// <summary>
    /// Submit completed performance bond document (business partner submits)
    /// </summary>
    [HttpPost("{id}/submit")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<PerformanceBondRequestDto>> SubmitBond(
        Guid id,
        IFormFile file,
        [FromForm] string? notes = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            var uploadsFolder = Path.Combine("uploads", "performance-bonds", "submissions");
            Directory.CreateDirectory(uploadsFolder);
            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var dto = new SubmitPerformanceBondDto { Notes = notes };
            var result = await _service.SubmitBondAsync(id, dto, filePath, file.FileName, file.ContentType, file.Length);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting performance bond");
            return StatusCode(500, "An error occurred while submitting the performance bond");
        }
    }

    /// <summary>
    /// Review performance bond (approve or reject)
    /// </summary>
    [HttpPost("{id}/review")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PerformanceBondRequestDto>> ReviewBond(Guid id, [FromBody] ReviewPerformanceBondDto dto)
    {
        try
        {
            var result = await _service.ReviewBondAsync(id, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing performance bond");
            return StatusCode(500, "An error occurred while reviewing the performance bond");
        }
    }

    /// <summary>
    /// Download performance bond template
    /// </summary>
    [HttpGet("{id}/download-template")]
    public async Task<IActionResult> DownloadTemplate(Guid id)
    {
        try
        {
            var request = await _service.GetByIdAsync(id);
            if (request == null) return NotFound();

            // Get full request to access file path
            var fullRequest = await _service.GetByIdAsync(id);
            if (fullRequest == null || string.IsNullOrEmpty(fullRequest.TemplateFileName))
            {
                return NotFound("No template file available");
            }

            // Find the file path from the database
            var filePath = Path.Combine("uploads", "performance-bonds", "templates");
            var files = Directory.GetFiles(filePath, $"*{fullRequest.TemplateFileName}*");

            if (files.Length == 0)
            {
                // Try to find by searching more broadly
                files = Directory.GetFiles(filePath);
                files = files.Where(f => f.EndsWith(fullRequest.TemplateFileName)).ToArray();
            }

            if (files.Length == 0)
            {
                return NotFound("Template file not found on server");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(files[0]);
            return File(fileBytes, fullRequest.TemplateFileType ?? "application/octet-stream", fullRequest.TemplateFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading performance bond template");
            return StatusCode(500, "An error occurred while downloading the template");
        }
    }

    /// <summary>
    /// Download submitted performance bond document
    /// </summary>
    [HttpGet("{id}/download-submission")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<IActionResult> DownloadSubmission(Guid id)
    {
        try
        {
            var request = await _service.GetByIdAsync(id);
            if (request == null) return NotFound();

            if (string.IsNullOrEmpty(request.SubmittedFileName))
            {
                return NotFound("No submission file available");
            }

            var filePath = Path.Combine("uploads", "performance-bonds", "submissions");
            var files = Directory.GetFiles(filePath, $"*{request.SubmittedFileName}*");

            if (files.Length == 0)
            {
                files = Directory.GetFiles(filePath);
                files = files.Where(f => f.EndsWith(request.SubmittedFileName)).ToArray();
            }

            if (files.Length == 0)
            {
                return NotFound("Submission file not found on server");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(files[0]);
            return File(fileBytes, request.SubmittedFileType ?? "application/octet-stream", request.SubmittedFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading performance bond submission");
            return StatusCode(500, "An error occurred while downloading the submission");
        }
    }
}

