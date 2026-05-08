using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize]
[ApiController]
[Route("api/sales/quotes")]
public class QuoteController : ControllerBase
{
    private readonly IQuoteService _quoteService;

    public QuoteController(IQuoteService quoteService)
    {
        _quoteService = quoteService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? opportunityId = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _quoteService.GetAllAsync(page, pageSize, search, status, opportunityId, customerId, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var quote = await _quoteService.GetByIdAsync(id);
        return quote == null ? NotFound() : Ok(quote);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuoteDto dto)
    {
        var quote = await _quoteService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = quote.Id }, quote);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuoteDto dto)
    {
        var quote = await _quoteService.UpdateAsync(id, dto);
        return Ok(quote);
    }

    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id)
    {
        var quote = await _quoteService.SendAsync(id);
        return Ok(quote);
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        var quote = await _quoteService.AcceptAsync(id);
        return Ok(quote);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromQuery] string? reason = null)
    {
        var quote = await _quoteService.RejectAsync(id, reason);
        return Ok(quote);
    }

    [HttpPost("{id:guid}/convert-to-sales-order")]
    public async Task<IActionResult> ConvertToSalesOrder(Guid id)
    {
        var salesOrderId = await _quoteService.ConvertToSalesOrderAsync(id);
        return Ok(new { salesOrderId });
    }

    [HttpGet("by-opportunity/{opportunityId:guid}")]
    public async Task<IActionResult> GetByOpportunity(Guid opportunityId)
    {
        var quotes = await _quoteService.GetByOpportunityAsync(opportunityId);
        return Ok(quotes);
    }

    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiring([FromQuery] int daysAhead = 7)
    {
        var quotes = await _quoteService.GetExpiringQuotesAsync(daysAhead);
        return Ok(quotes);
    }
}
