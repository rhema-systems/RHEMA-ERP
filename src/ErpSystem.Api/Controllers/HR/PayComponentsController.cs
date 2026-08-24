using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Pay component master (allowances / deductions) used by emoluments and leave encashment.
/// </summary>
[ApiController]
[Route("api/hr/pay-components")]
[Authorize(Policy = "InternalOnly")]
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

    /// <summary>
    /// Forces a payroll → HR projection pass. Reads reconcile on their own, so this is for when
    /// someone has just changed payroll and does not want to wait for the debounce.
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(PayComponentProjectionResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PayComponentProjectionResultDto>> Sync(CancellationToken ct)
    {
        try { return Ok(await _service.SyncPayComponentsAsync(ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Updates the attributes HR owns — pension, tax treatment, gross-pay effect and effective
    /// dating. Payroll models none of them, so they survive every projection pass.
    /// </summary>
    [HttpPatch("{id:guid}/hr-attributes")]
    [ProducesResponseType(typeof(PayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayComponentDto>> UpdateHrAttributes(
        Guid id, [FromBody] UpdatePayComponentHrAttributesDto dto)
    {
        try { return Ok(await _service.UpdatePayComponentHrAttributesAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Payroll-owned writes ─────────────────────────────────────────────────────
    // New components must come from Payroll, so create is always refused — otherwise the two
    // parallel component sets would keep diverging. Update and deactivate are refused only for
    // mirrored rows; components HR defined itself (the six the emolument seeder creates) stay
    // editable, since Payroll does not know about them and never will.

    /// <summary>Not supported — create the component in Payroll, then sync.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayComponentDto>> Create([FromBody] CreatePayComponentDto dto)
    {
        try { return Ok(await _service.CreatePayComponentAsync(dto)); }
        catch (PayrollOwnedException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Full update. Returns 409 for a mirrored component — use PATCH hr-attributes for the fields
    /// HR owns on those.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PayComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayComponentDto>> Update(Guid id, [FromBody] UpdatePayComponentDto dto)
    {
        try { return Ok(await _service.UpdatePayComponentAsync(id, dto)); }
        catch (PayrollOwnedException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Deactivates an HR-defined component. Returns 409 for a mirrored one.</summary>
    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        try { await _service.DeactivatePayComponentAsync(id); return NoContent(); }
        catch (PayrollOwnedException ex) { return Conflict(new { message = ex.Message }); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }
}
