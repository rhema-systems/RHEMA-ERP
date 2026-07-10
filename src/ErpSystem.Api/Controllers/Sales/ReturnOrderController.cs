using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize]
[ApiController]
[Route("api/sales/return-orders")]
public class ReturnOrderController : ControllerBase
{
    private readonly IReturnOrderService _service;

    public ReturnOrderController(IReturnOrderService service)
    {
        _service = service;
    }

    // ── Return Orders ──

    [HttpGet]
    public async Task<IActionResult> GetReturnOrders(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] ReturnOrderStatus? status = null,
        [FromQuery] Guid? customerId = null, [FromQuery] Guid? salesOrderId = null,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var result = await _service.GetReturnOrdersAsync(page, pageSize, search, status, customerId, salesOrderId, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetReturnOrder(Guid id)
    {
        var ro = await _service.GetReturnOrderByIdAsync(id);
        return ro == null ? NotFound() : Ok(ro);
    }

    [HttpPost]
    public async Task<IActionResult> CreateReturnOrder([FromBody] CreateReturnOrderDto dto)
    {
        var ro = await _service.CreateReturnOrderAsync(dto);
        return CreatedAtAction(nameof(GetReturnOrder), new { id = ro.Id }, ro);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id) => Ok(await _service.ApproveReturnOrderAsync(id));

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id) => Ok(await _service.ReceiveReturnOrderAsync(id));

    [HttpPost("{id:guid}/inspect")]
    public async Task<IActionResult> Inspect(Guid id, [FromQuery] string? notes = null)
        => Ok(await _service.InspectReturnOrderAsync(id, notes));

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromQuery] string? reason = null)
        => Ok(await _service.RejectReturnOrderAsync(id, reason));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] string? reason = null)
        => Ok(await _service.CancelReturnOrderAsync(id, reason));

    [HttpPost("{id:guid}/issue-credit")]
    public async Task<IActionResult> IssueCreditNote(Guid id)
        => Ok(await _service.CreateCreditNoteFromReturnAsync(id));

    // ── Credit Notes ──

    [HttpGet("~/api/sales/credit-notes")]
    public async Task<IActionResult> GetCreditNotes(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] CreditNoteStatus? status = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var result = await _service.GetCreditNotesAsync(page, pageSize, search, status, customerId, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("~/api/sales/credit-notes/{id:guid}")]
    public async Task<IActionResult> GetCreditNote(Guid id)
    {
        var cn = await _service.GetCreditNoteByIdAsync(id);
        return cn == null ? NotFound() : Ok(cn);
    }

    [HttpPost("~/api/sales/credit-notes")]
    public async Task<IActionResult> CreateCreditNote([FromBody] CreateCreditNoteDto dto)
    {
        var cn = await _service.CreateCreditNoteAsync(dto);
        return CreatedAtAction(nameof(GetCreditNote), new { id = cn.Id }, cn);
    }

    [HttpPost("~/api/sales/credit-notes/{id:guid}/approve")]
    public async Task<IActionResult> ApproveCreditNote(Guid id) => Ok(await _service.ApproveCreditNoteAsync(id));

    [HttpPost("~/api/sales/credit-notes/{id:guid}/post")]
    public async Task<IActionResult> PostCreditNote(Guid id, CancellationToken cancellationToken)
        => Ok(await _service.PostCreditNoteAsync(id, cancellationToken));

    [HttpPost("~/api/sales/credit-notes/{id:guid}/apply")]
    public async Task<IActionResult> ApplyCreditNote(Guid id, [FromQuery] Guid? invoiceId = null)
        => Ok(await _service.ApplyCreditNoteAsync(id, invoiceId));

    [HttpPost("~/api/sales/credit-notes/{id:guid}/void")]
    public async Task<IActionResult> VoidCreditNote(Guid id, [FromQuery] string? reason = null)
        => Ok(await _service.VoidCreditNoteAsync(id, reason));

    // ── Refunds ──

    [HttpGet("~/api/sales/refunds")]
    public async Task<IActionResult> GetRefunds(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] RefundStatus? status = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var result = await _service.GetRefundsAsync(page, pageSize, search, status, customerId, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("~/api/sales/refunds/{id:guid}")]
    public async Task<IActionResult> GetRefund(Guid id)
    {
        var refund = await _service.GetRefundByIdAsync(id);
        return refund == null ? NotFound() : Ok(refund);
    }

    [HttpPost("~/api/sales/refunds")]
    public async Task<IActionResult> CreateRefund([FromBody] CreateRefundDto dto)
    {
        var refund = await _service.CreateRefundAsync(dto);
        return CreatedAtAction(nameof(GetRefund), new { id = refund.Id }, refund);
    }

    [HttpPost("~/api/sales/refunds/{id:guid}/approve")]
    public async Task<IActionResult> ApproveRefund(Guid id) => Ok(await _service.ApproveRefundAsync(id));

    [HttpPost("~/api/sales/refunds/{id:guid}/process")]
    public async Task<IActionResult> ProcessRefund(Guid id, [FromQuery] string? paymentReference = null)
        => Ok(await _service.ProcessRefundAsync(id, paymentReference));

    [HttpPost("~/api/sales/refunds/{id:guid}/reject")]
    public async Task<IActionResult> RejectRefund(Guid id, [FromQuery] string? reason = null)
        => Ok(await _service.RejectRefundAsync(id, reason));
}
