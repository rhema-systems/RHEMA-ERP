using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/quality-incidents")]
[Authorize]
public class QualityIncidentsController : ControllerBase
{
    private readonly IQualityIncidentService _incidentService;
    private readonly ILogger<QualityIncidentsController> _logger;

    public QualityIncidentsController(
        IQualityIncidentService incidentService,
        ILogger<QualityIncidentsController> logger)
    {
        _incidentService = incidentService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<QualityIncidentDto>> GetById(Guid id)
    {
        try
        {
            var incident = await _incidentService.GetByIdAsync(id);
            if (incident == null)
            {
                return NotFound($"Quality incident with ID {id} not found");
            }
            return Ok(incident);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quality incident {IncidentId}", id);
            return StatusCode(500, "An error occurred while retrieving the quality incident");
        }
    }

    [HttpGet("number/{incidentNumber}")]
    public async Task<ActionResult<QualityIncidentDto>> GetByIncidentNumber(string incidentNumber)
    {
        try
        {
            var incident = await _incidentService.GetByIncidentNumberAsync(incidentNumber);
            if (incident == null)
            {
                return NotFound($"Quality incident with number {incidentNumber} not found");
            }
            return Ok(incident);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quality incident {IncidentNumber}", incidentNumber);
            return StatusCode(500, "An error occurred while retrieving the quality incident");
        }
    }

    [HttpGet("business-partner/{businessPartnerId}")]
    public async Task<ActionResult<IEnumerable<QualityIncidentDto>>> GetByBusinessPartner(Guid businessPartnerId)
    {
        try
        {
            var incidents = await _incidentService.GetByBusinessPartnerAsync(businessPartnerId);
            return Ok(incidents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quality incidents for business partner {BusinessPartnerId}", businessPartnerId);
            return StatusCode(500, "An error occurred while retrieving quality incidents");
        }
    }

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<QualityIncidentDto>>> GetByStatus(string status)
    {
        try
        {
            var incidents = await _incidentService.GetByStatusAsync(status);
            return Ok(incidents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quality incidents by status {Status}", status);
            return StatusCode(500, "An error occurred while retrieving quality incidents");
        }
    }

    [HttpGet("severity/{severity}")]
    public async Task<ActionResult<IEnumerable<QualityIncidentDto>>> GetBySeverity(string severity)
    {
        try
        {
            var incidents = await _incidentService.GetBySeverityAsync(severity);
            return Ok(incidents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quality incidents by severity {Severity}", severity);
            return StatusCode(500, "An error occurred while retrieving quality incidents");
        }
    }

    [HttpGet("open")]
    public async Task<ActionResult<IEnumerable<QualityIncidentDto>>> GetOpenIncidents()
    {
        try
        {
            var incidents = await _incidentService.GetOpenIncidentsAsync();
            return Ok(incidents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving open quality incidents");
            return StatusCode(500, "An error occurred while retrieving open quality incidents");
        }
    }

    [HttpPost]
    public async Task<ActionResult<QualityIncidentDto>> Create([FromBody] CreateQualityIncidentDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var incident = await _incidentService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = incident.Id }, incident);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quality incident");
            return StatusCode(500, "An error occurred while creating the quality incident");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<QualityIncidentDto>> Update(Guid id, [FromBody] UpdateQualityIncidentDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var incident = await _incidentService.UpdateAsync(id, updateDto);
            return Ok(incident);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quality incident {IncidentId}", id);
            return StatusCode(500, "An error occurred while updating the quality incident");
        }
    }

    [HttpPost("{id}/acknowledge")]
    public async Task<ActionResult<QualityIncidentDto>> Acknowledge(Guid id)
    {
        try
        {
            var incident = await _incidentService.AcknowledgeAsync(id);
            return Ok(incident);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging quality incident {IncidentId}", id);
            return StatusCode(500, "An error occurred while acknowledging the quality incident");
        }
    }

    [HttpPost("{id}/resolve")]
    public async Task<ActionResult<QualityIncidentDto>> Resolve(Guid id, [FromBody] UpdateQualityIncidentDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var incident = await _incidentService.ResolveAsync(id, updateDto);
            return Ok(incident);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving quality incident {IncidentId}", id);
            return StatusCode(500, "An error occurred while resolving the quality incident");
        }
    }

    [HttpPost("{id}/supplier-response")]
    public async Task<ActionResult<QualityIncidentDto>> SubmitSupplierResponse(Guid id, [FromBody] SupplierResponseDto responseDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var incident = await _incidentService.SubmitSupplierResponseAsync(id, responseDto);
            return Ok(incident);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting supplier response for quality incident {IncidentId}", id);
            return StatusCode(500, "An error occurred while submitting the supplier response");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _incidentService.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting quality incident {IncidentId}", id);
            return StatusCode(500, "An error occurred while deleting the quality incident");
        }
    }
}

