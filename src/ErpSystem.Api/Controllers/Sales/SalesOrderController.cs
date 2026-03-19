using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

/// <summary>
/// Manages Sales Orders throughout the order-to-cash lifecycle.
/// Supports creation, approval workflow, confirmation, delivery tracking, and invoicing.
/// </summary>
[Authorize]
[ApiController]
[Route("api/sales/orders")]
public class SalesOrderController : ControllerBase
{
    private readonly ISalesOrderService _salesOrderService;

    public SalesOrderController(ISalesOrderService salesOrderService)
    {
        _salesOrderService = salesOrderService;
    }

    // ── CRUD ────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves a paginated list of Sales Orders with filtering.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SalesOrderSummaryDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] SalesOrderStatus? status = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? salesRepId = null,
        [FromQuery] SalesOrderType? orderType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? priority = null)
    {
        var result = await _salesOrderService.GetSalesOrdersAsync(
            page, pageSize, search, status, customerId, salesRepId, orderType, startDate, endDate, priority);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single Sales Order by ID with full details.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<SalesOrderDetailDto>> GetById(Guid id)
    {
        var so = await _salesOrderService.GetSalesOrderByIdAsync(id);
        return so == null ? NotFound() : Ok(so);
    }

    /// <summary>
    /// Creates a new Sales Order in Draft status.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SalesOrderDetailDto>> Create([FromBody] CreateSalesOrderDto dto)
    {
        try
        {
            var so = await _salesOrderService.CreateSalesOrderAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = so.Id }, so);
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Updates an existing Draft Sales Order.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<SalesOrderDetailDto>> Update(Guid id, [FromBody] UpdateSalesOrderDto dto)
    {
        try { return Ok(await _salesOrderService.UpdateSalesOrderAsync(id, dto)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Lifecycle Actions ───────────────────────────────────────────────

    /// <summary>
    /// Submits a Draft Sales Order for approval.
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<ActionResult<SalesOrderDetailDto>> Submit(Guid id)
    {
        try { return Ok(await _salesOrderService.SubmitForApprovalAsync(id)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Approves or rejects a pending Sales Order.
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult<SalesOrderDetailDto>> Approve(Guid id, [FromBody] SalesOrderApprovalDto dto)
    {
        try { return Ok(await _salesOrderService.ProcessApprovalAsync(id, dto)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Confirms an approved Sales Order — validates credit limit and reserves stock.
    /// </summary>
    [HttpPost("{id}/confirm")]
    public async Task<ActionResult<SalesOrderDetailDto>> Confirm(Guid id)
    {
        try { return Ok(await _salesOrderService.ConfirmSalesOrderAsync(id)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Cancels a Sales Order and releases reserved stock.
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<SalesOrderDetailDto>> Cancel(Guid id, [FromBody] CancelSalesOrderDto dto)
    {
        try { return Ok(await _salesOrderService.CancelSalesOrderAsync(id, dto)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Puts a Sales Order on hold.
    /// </summary>
    [HttpPost("{id}/hold")]
    public async Task<ActionResult<SalesOrderDetailDto>> Hold(Guid id, [FromBody] string? reason = null)
    {
        try { return Ok(await _salesOrderService.PutOnHoldAsync(id, reason)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Releases a Sales Order from hold.
    /// </summary>
    [HttpPost("{id}/release")]
    public async Task<ActionResult<SalesOrderDetailDto>> Release(Guid id)
    {
        try { return Ok(await _salesOrderService.ReleaseFromHoldAsync(id)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Closes a fully delivered and invoiced Sales Order.
    /// </summary>
    [HttpPost("{id}/close")]
    public async Task<ActionResult<SalesOrderDetailDto>> Close(Guid id)
    {
        try { return Ok(await _salesOrderService.CloseSalesOrderAsync(id)); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Conversion ──────────────────────────────────────────────────────

    /// <summary>
    /// Converts an accepted Quote into a Sales Order.
    /// </summary>
    [HttpPost("convert-from-quote/{quoteId}")]
    public async Task<ActionResult<SalesOrderDetailDto>> ConvertFromQuote(Guid quoteId)
    {
        try
        {
            var so = await _salesOrderService.ConvertQuoteToSalesOrderAsync(quoteId);
            return CreatedAtAction(nameof(GetById), new { id = so.Id }, so);
        }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Generates an Invoice from a confirmed Sales Order.
    /// </summary>
    [HttpPost("{id}/generate-invoice")]
    public async Task<ActionResult<Guid>> GenerateInvoice(Guid id)
    {
        try
        {
            var invoiceId = await _salesOrderService.GenerateInvoiceAsync(id);
            return Ok(new { invoiceId });
        }
        catch (NotImplementedException ex) { return StatusCode(501, new { error = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Utilities ───────────────────────────────────────────────────────

    /// <summary>
    /// Validates whether a customer has sufficient credit for an order amount.
    /// </summary>
    [HttpGet("validate-credit/{businessPartnerId}")]
    public async Task<ActionResult> ValidateCredit(Guid businessPartnerId, [FromQuery] decimal amount)
    {
        var isValid = await _salesOrderService.ValidateCreditLimitAsync(businessPartnerId, amount);
        var balance = await _salesOrderService.GetCustomerOutstandingBalanceAsync(businessPartnerId);
        return Ok(new { isValid, outstandingBalance = balance, requestedAmount = amount });
    }
}
