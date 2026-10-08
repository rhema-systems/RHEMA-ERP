using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize(Policy = SalesPermissions.Read)]
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
    [Authorize(Policy = SalesPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateQuoteDto dto)
    {
        var quote = await _quoteService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = quote.Id }, quote);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = SalesPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuoteDto dto)
    {
        var quote = await _quoteService.UpdateAsync(id, dto);
        return Ok(quote);
    }

    [HttpPost("{id:guid}/send")]
    [Authorize(Policy = SalesPermissions.Manage)]
    public async Task<IActionResult> Send(Guid id)
    {
        var quote = await _quoteService.SendAsync(id);
        return Ok(quote);
    }

    [HttpPost("{id:guid}/accept")]
    [Authorize(Policy = SalesPermissions.Approve)]
    public async Task<IActionResult> Accept(Guid id)
    {
        var quote = await _quoteService.AcceptAsync(id);
        return Ok(quote);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = SalesPermissions.Manage)]
    public async Task<IActionResult> Reject(Guid id, [FromQuery] string? reason = null)
    {
        var quote = await _quoteService.RejectAsync(id, reason);
        return Ok(quote);
    }

    [HttpPost("{id:guid}/convert-to-sales-order")]
    [Authorize(Policy = SalesPermissions.Manage)]
    public async Task<IActionResult> ConvertToSalesOrder(Guid id)
    {
        var salesOrderId = await _quoteService.ConvertToSalesOrderAsync(id);
        return Ok(new { salesOrderId });
    }

    [HttpPost("from-property-opportunity/{opportunityId:guid}")]
    [Authorize(Policy = SalesPermissions.Manage)]
    public async Task<IActionResult> CreateFromPropertyOpportunity(
        Guid opportunityId,
        CancellationToken cancellationToken)
    {
        var quote = await _quoteService.CreatePropertyOpportunityQuoteAsync(
            opportunityId,
            cancellationToken);
        return Ok(quote);
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> GetPdf(Guid id, [FromQuery] bool download = false)
    {
        var quote = await _quoteService.GetByIdAsync(id);
        if (quote is null)
        {
            return NotFound();
        }

        var pdf = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10));
                page.Header().Column(column =>
                {
                    column.Item().Text("RHEMA-ERP").Bold().FontSize(18).FontColor(Colors.Blue.Darken2);
                    column.Item().Text("SALES QUOTE").Bold().FontSize(14);
                    column.Item().Text($"Quote {quote.DocumentNumber}");
                });
                page.Content().PaddingVertical(18).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text($"Opportunity: {quote.OpportunityName ?? quote.OpportunityId.ToString()}");
                            left.Item().Text($"Party: {quote.CustomerName ?? "Property enquiry prospect"}");
                        });
                        row.ConstantItem(180).Column(right =>
                        {
                            right.Item().Text($"Issued: {quote.CreatedAt:dd MMM yyyy}");
                            right.Item().Text($"Valid until: {quote.ValidUntil:dd MMM yyyy}");
                            right.Item().Text($"Currency: {quote.Currency}");
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(quote.Proposal))
                    {
                        column.Item().Text(quote.Proposal);
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });
                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(5).Text("Description").Bold();
                            header.Cell().BorderBottom(1).Padding(5).AlignRight().Text("Qty").Bold();
                            header.Cell().BorderBottom(1).Padding(5).AlignRight().Text("Unit price").Bold();
                            header.Cell().BorderBottom(1).Padding(5).AlignRight().Text("Amount").Bold();
                        });
                        foreach (var line in quote.LineItems)
                        {
                            table.Cell().BorderBottom(0.5f).Padding(5).Text(line.Description);
                            table.Cell().BorderBottom(0.5f).Padding(5).AlignRight().Text($"{line.Quantity:N2} {line.Unit}");
                            table.Cell().BorderBottom(0.5f).Padding(5).AlignRight().Text($"{line.UnitPrice:N2}");
                            table.Cell().BorderBottom(0.5f).Padding(5).AlignRight().Text($"{line.LineTotal:N2}");
                        }
                    });

                    column.Item().AlignRight().Column(totals =>
                    {
                        totals.Item().Text($"Subtotal: {quote.Currency} {quote.SubTotal:N2}");
                        totals.Item().Text($"Tax: {quote.Currency} {quote.TaxAmount:N2}");
                        totals.Item().Text($"Total: {quote.Currency} {quote.TotalAmount:N2}").Bold().FontSize(12);
                    });
                });
                page.Footer().AlignCenter().Text("Generated from the governed RHEMA-ERP Sales quote record.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();

        var fileName = $"{quote.DocumentNumber}.pdf";
        return download
            ? File(pdf, "application/pdf", fileName)
            : File(pdf, "application/pdf");
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
