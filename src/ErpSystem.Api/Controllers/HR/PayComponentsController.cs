using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Pay component master (allowances / deductions) used by emoluments and leave encashment.
/// </summary>
[ApiController]
[Route("api/hr/pay-components")]
[Authorize]
public class PayComponentsController : ControllerBase
{
    private readonly IEmolumentService _service;
    private readonly ILogger<PayComponentsController> _logger;

    public PayComponentsController(IEmolumentService service, ILogger<PayComponentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PayComponentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PayComponentDto>>> GetAll([FromQuery] bool activeOnly = true)
        => Ok(await _service.GetPayComponentsAsync(activeOnly));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayComponentDto>> GetById(Guid id)
    {
        try { return Ok(await _service.GetPayComponentByIdAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [ProducesResponseType(typeof(PayComponentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PayComponentDto>> Create([FromBody] CreatePayComponentDto dto)
    {
        try
        {
            var result = await _service.CreatePayComponentAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pay component");
            return StatusCode(500, "An error occurred while creating the pay component");
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayComponentDto>> Update(Guid id, [FromBody] UpdatePayComponentDto dto)
    {
        try { return Ok(await _service.UpdatePayComponentAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        try { await _service.DeactivatePayComponentAsync(id); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }
}
