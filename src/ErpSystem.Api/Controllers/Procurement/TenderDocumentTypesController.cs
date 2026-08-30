using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class TenderDocumentTypesController : ControllerBase
{
    private readonly ITenderDocumentTypeService _service;
    private readonly ILogger<TenderDocumentTypesController> _logger;

    public TenderDocumentTypesController(
        ITenderDocumentTypeService service,
        ILogger<TenderDocumentTypesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Get all document types
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<TenderDocumentTypeDto>>> GetAll()
    {
        try
        {
            var documentTypes = await _service.GetAllAsync();
            return Ok(documentTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document types");
            return StatusCode(500, "An error occurred while retrieving document types");
        }
    }

    /// <summary>
    /// Get active document types
    /// </summary>
    [HttpGet("active")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<TenderDocumentTypeDto>>> GetActive()
    {
        try
        {
            var documentTypes = await _service.GetActiveAsync();
            return Ok(documentTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active document types");
            return StatusCode(500, "An error occurred while retrieving active document types");
        }
    }

    /// <summary>
    /// Get document type by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<TenderDocumentTypeDto>> GetById(Guid id)
    {
        try
        {
            var documentType = await _service.GetByIdAsync(id);
            if (documentType == null)
            {
                return NotFound($"Document type with ID '{id}' not found");
            }
            return Ok(documentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document type {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the document type");
        }
    }

    /// <summary>
    /// Get document types by category
    /// </summary>
    [HttpGet("category/{category}")]
    [Authorize(Policy = "procurement.records.read")]
    public async Task<ActionResult<IEnumerable<TenderDocumentTypeDto>>> GetByCategory(string category)
    {
        try
        {
            var documentTypes = await _service.GetByCategoryAsync(category);
            return Ok(documentTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document types for category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving document types");
        }
    }

    /// <summary>
    /// Create document type
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderDocumentTypeDto>> Create([FromBody] CreateTenderDocumentTypeDto dto)
    {
        try
        {
            var documentType = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = documentType.Id }, documentType);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document type");
            return StatusCode(500, "An error occurred while creating the document type");
        }
    }

    /// <summary>
    /// Update document type
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult<TenderDocumentTypeDto>> Update(Guid id, [FromBody] UpdateTenderDocumentTypeDto dto)
    {
        try
        {
            var documentType = await _service.UpdateAsync(id, dto);
            return Ok(documentType);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document type {Id}", id);
            return StatusCode(500, "An error occurred while updating the document type");
        }
    }

    /// <summary>
    /// Delete document type
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "procurement.tender.administer")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document type {Id}", id);
            return StatusCode(500, "An error occurred while deleting the document type");
        }
    }
}

