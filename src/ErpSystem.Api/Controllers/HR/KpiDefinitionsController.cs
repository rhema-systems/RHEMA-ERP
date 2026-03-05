using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KpiDefinitionsController : ControllerBase
{
    private readonly IKpiDefinitionService _kpiDefinitionService;
    private readonly ILogger<KpiDefinitionsController> _logger;

    public KpiDefinitionsController(IKpiDefinitionService kpiDefinitionService, ILogger<KpiDefinitionsController> logger)
    {
        _kpiDefinitionService = kpiDefinitionService;
        _logger = logger;
    }

    /// <summary>
    /// Get all KPI definitions
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<KpiDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _kpiDefinitionService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all KPI definitions");
            return StatusCode(500, "An error occurred while retrieving KPI definitions");
        }
    }

    /// <summary>
    /// Get KPI definitions with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<KpiDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _kpiDefinitionService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged KPI definitions");
            return StatusCode(500, "An error occurred while retrieving KPI definitions");
        }
    }

    /// <summary>
    /// Get KPI definition by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(KpiDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _kpiDefinitionService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving KPI definition with ID {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the KPI definition");
        }
    }

    /// <summary>
    /// Create a new KPI definition
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(KpiDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateKpiDefinitionDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _kpiDefinitionService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating KPI definition");
            return StatusCode(500, "An error occurred while creating the KPI definition");
        }
    }

    /// <summary>
    /// Update an existing KPI definition
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(KpiDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKpiDefinitionDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _kpiDefinitionService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating KPI definition with ID {Id}", id);
            return StatusCode(500, "An error occurred while updating the KPI definition");
        }
    }

    /// <summary>
    /// Delete a KPI definition
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _kpiDefinitionService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting KPI definition with ID {Id}", id);
            return StatusCode(500, "An error occurred while deleting the KPI definition");
        }
    }
}
