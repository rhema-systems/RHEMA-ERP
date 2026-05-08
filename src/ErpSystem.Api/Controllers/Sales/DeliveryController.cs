using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

/// <summary>
/// Manages Delivery Notes — the fulfillment arm of the order-to-cash cycle.
/// Tracks goods dispatch, shipment, and delivery confirmation.
/// </summary>
[Authorize]
[ApiController]
[Route("api/sales/deliveries")]
public class DeliveryController : ControllerBase
{
    private readonly IDeliveryService _deliveryService;

    public DeliveryController(IDeliveryService deliveryService)
    {
        _deliveryService = deliveryService;
    }

    // ── CRUD ────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves a paginated list of Delivery Notes with filtering.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<DeliveryNoteSummaryDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] DeliveryNoteStatus? status = null,
        [FromQuery] Guid? salesOrderId = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _deliveryService.GetDeliveryNotesAsync(
            page, pageSize, search, status, salesOrderId, customerId, startDate, endDate);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single Delivery Note by ID with full details.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DeliveryNoteDetailDto>> GetById(Guid id)
    {
        var dn = await _deliveryService.GetDeliveryNoteByIdAsync(id);
        return dn == null ? NotFound() : Ok(dn);
    }

    /// <summary>
    /// Creates a new Delivery Note from a confirmed Sales Order.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DeliveryNoteDetailDto>> Create([FromBody] CreateDeliveryNoteDto dto)
    {
        try
        {
            var dn = await _deliveryService.CreateDeliveryNoteAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = dn.Id }, dn);
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Lifecycle Actions ───────────────────────────────────────────────

    /// <summary>
    /// Marks a Delivery Note as packed and ready for shipment.
    /// </summary>
    [HttpPost("{id}/pack")]
    public async Task<ActionResult<DeliveryNoteDetailDto>> Pack(Guid id)
    {
        try { return Ok(await _deliveryService.MarkAsPackedAsync(id)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Marks a Delivery Note as shipped with optional carrier/tracking details.
    /// </summary>
    [HttpPost("{id}/ship")]
    public async Task<ActionResult<DeliveryNoteDetailDto>> Ship(
        Guid id,
        [FromQuery] string? carrierName = null,
        [FromQuery] string? trackingNumber = null)
    {
        try { return Ok(await _deliveryService.MarkAsShippedAsync(id, carrierName, trackingNumber)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Confirms delivery — deducts stock from inventory and updates SO delivered quantities.
    /// </summary>
    [HttpPost("{id}/confirm")]
    public async Task<ActionResult<DeliveryNoteDetailDto>> Confirm(Guid id, [FromBody] ConfirmDeliveryDto dto)
    {
        try { return Ok(await _deliveryService.ConfirmDeliveryAsync(id, dto)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Cancels a Delivery Note (only if not yet delivered).
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<DeliveryNoteDetailDto>> Cancel(Guid id, [FromBody] string? reason = null)
    {
        try { return Ok(await _deliveryService.CancelDeliveryNoteAsync(id, reason)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Utilities ───────────────────────────────────────────────────────

    /// <summary>
    /// Gets deliverable lines from a Sales Order (lines with remaining quantity).
    /// </summary>
    [HttpGet("deliverable-lines/{salesOrderId}")]
    public async Task<ActionResult<List<SalesOrderLineDto>>> GetDeliverableLines(Guid salesOrderId)
    {
        try { return Ok(await _deliveryService.GetDeliverableLinesAsync(salesOrderId)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }
}
