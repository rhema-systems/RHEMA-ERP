using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// System-wide reusable reason-code lookup (leave adjustments, encashments, and beyond).
/// </summary>
[ApiController]
[Route("api/reason-codes")]
[Authorize(Policy = "InternalOnly")]
public class ReasonCodesController : ControllerBase
{
    private readonly IReasonCodeService _service;
    private readonly ILogger<ReasonCodesController> _logger;

    public ReasonCodesController(IReasonCodeService service, ILogger<ReasonCodesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReasonCodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ReasonCodeDto>>> GetAll(
        [FromQuery] ReasonCodeCategory? category = null, [FromQuery] bool activeOnly = false)
        => Ok(await _service.GetAllAsync(category, activeOnly));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReasonCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReasonCodeDto>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(ReasonCodeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReasonCodeDto>> Create([FromBody] CreateReasonCodeDto dto)
    {
        try
        {
            var created = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(ReasonCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReasonCodeDto>> Update(Guid id, [FromBody] UpdateReasonCodeDto dto)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, dto));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        try
        {
            await _service.DeactivateAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }
}
