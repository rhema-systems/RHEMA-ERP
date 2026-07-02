using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/ap/supplier-returns")]
public class SupplierReturnsController : ControllerBase
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly ISubledgerPostingService _subledgerPostingService;

    public SupplierReturnsController(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IDocumentNumberingService documentNumberingService,
        ISubledgerPostingService subledgerPostingService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _documentNumberingService = documentNumberingService;
        _subledgerPostingService = subledgerPostingService;
    }

    private Guid TenantId => _currentUserService.TenantId ?? DefaultTenantId;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupplierReturnDto>>> GetAll(CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var returns = await BaseQuery(tenantId)
            .OrderByDescending(r => r.ReturnDate)
            .ThenByDescending(r => r.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return Ok(returns.Select(MapToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierReturnDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var supplierReturn = await BaseQuery(TenantId)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return supplierReturn == null ? NotFound() : Ok(MapToDto(supplierReturn));
    }

    [HttpPost]
    public async Task<ActionResult<SupplierReturnDto>> Create(
        [FromBody] CreateSupplierReturnDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.OriginalVendorInvoiceId.HasValue == dto.OriginalFinancePurchaseOrderReceiptId.HasValue)
        {
            return BadRequest("Select either an original supplier invoice or an original finance GRV.");
        }

        if (dto.Lines.Count == 0)
        {
            return BadRequest("At least one return line is required.");
        }

        var tenantId = TenantId;
        var vendor = await _dbContext.BusinessPartners
            .FirstOrDefaultAsync(v =>
                v.TenantId == tenantId &&
                v.Id == dto.VendorId &&
                !v.IsDeleted &&
                (v.PartnerType == "Supplier" || v.PartnerType == "Contractor" || v.PartnerType == "Both"),
                cancellationToken);

        if (vendor == null)
        {
            return BadRequest("Supplier not found.");
        }

        if (dto.OriginalVendorInvoiceId.HasValue)
        {
            var invoiceExists = await _dbContext.Set<VendorInvoice>()
                .AnyAsync(i => i.TenantId == tenantId && i.Id == dto.OriginalVendorInvoiceId.Value && !i.IsDeleted, cancellationToken);
            if (!invoiceExists)
            {
                return BadRequest("Original supplier invoice not found.");
            }
        }

        if (dto.OriginalFinancePurchaseOrderReceiptId.HasValue)
        {
            var receiptExists = await _dbContext.FinancePurchaseOrderReceipts
                .AnyAsync(r => r.TenantId == tenantId && r.Id == dto.OriginalFinancePurchaseOrderReceiptId.Value && !r.IsDeleted, cancellationToken);
            if (!receiptExists)
            {
                return BadRequest("Original finance GRV not found.");
            }
        }

        var currencyCode = NormalizeCurrency(dto.CurrencyCode);
        var exchangeRate = NormalizeExchangeRate(dto.ExchangeRate);
        var returnDate = dto.ReturnDate == default ? DateTime.UtcNow : dto.ReturnDate;
        var returnNumber = string.IsNullOrWhiteSpace(dto.ReturnNumber)
            ? await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.APSupplierReturn,
                tenantId,
                returnDate,
                nameof(SupplierReturn),
                cancellationToken: cancellationToken)
            : dto.ReturnNumber.Trim();

        var supplierReturn = new SupplierReturn
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReturnNumber = returnNumber,
            VendorId = vendor.Id,
            VendorName = string.IsNullOrWhiteSpace(dto.VendorName) ? vendor.PartnerName : dto.VendorName.Trim(),
            OriginalVendorInvoiceId = dto.OriginalVendorInvoiceId,
            OriginalFinancePurchaseOrderReceiptId = dto.OriginalFinancePurchaseOrderReceiptId,
            ReturnDate = returnDate,
            Reason = dto.Reason,
            CurrencyCode = currencyCode,
            ExchangeRate = exchangeRate,
            Status = SupplierReturnStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "system"
        };

        foreach (var lineDto in dto.Lines)
        {
            if (lineDto.QuantityReturned <= 0)
            {
                return BadRequest($"Return quantity must be greater than zero for line '{lineDto.Description}'.");
            }

            var lineAmounts = CalculateLineAmounts(
                lineDto.QuantityReturned,
                lineDto.UnitPrice,
                lineDto.TaxAmount,
                lineDto.DiscountPercentage,
                lineDto.DiscountAmount,
                lineDto.LineTotal);

            supplierReturn.LineItems.Add(new SupplierReturnLineItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SupplierReturnId = supplierReturn.Id,
                OriginalVendorInvoiceLineItemId = lineDto.OriginalVendorInvoiceLineItemId,
                OriginalFinancePurchaseOrderItemId = lineDto.OriginalFinancePurchaseOrderItemId,
                Description = lineDto.Description,
                QuantityReturned = lineDto.QuantityReturned,
                UnitPrice = lineDto.UnitPrice,
                TaxGroupId = lineDto.TaxGroupId,
                TaxRate = lineDto.TaxRate,
                TaxAmount = lineAmounts.TaxAmount,
                DiscountPercentage = lineDto.DiscountPercentage,
                DiscountAmount = lineAmounts.DiscountAmount,
                LineTotal = lineAmounts.LineTotal,
                CreatedAt = supplierReturn.CreatedAt,
                CreatedBy = supplierReturn.CreatedBy
            });

            supplierReturn.SubTotal += lineAmounts.NetAmount;
            supplierReturn.TaxAmount += lineAmounts.TaxAmount;
            supplierReturn.DiscountAmount += lineAmounts.DiscountAmount;
        }

        supplierReturn.TotalAmount = supplierReturn.SubTotal + supplierReturn.TaxAmount;
        supplierReturn.BaseCurrencyAmount = ToBaseAmount(supplierReturn.TotalAmount, exchangeRate);

        _dbContext.SupplierReturns.Add(supplierReturn);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await BaseQuery(tenantId)
            .AsSplitQuery()
            .FirstAsync(r => r.Id == supplierReturn.Id, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = supplierReturn.Id }, MapToDto(created));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<SupplierReturnDto>> Approve(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var supplierReturn = await _dbContext.SupplierReturns
            .Include(r => r.Vendor)
            .Include(r => r.LineItems.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted, cancellationToken);

        if (supplierReturn == null)
        {
            return NotFound();
        }

        if (supplierReturn.Status == SupplierReturnStatus.Approved)
        {
            var existing = await BaseQuery(tenantId).AsSplitQuery().FirstAsync(r => r.Id == id, cancellationToken);
            return Ok(MapToDto(existing));
        }

        if (supplierReturn.Status != SupplierReturnStatus.Draft)
        {
            return BadRequest("Only draft supplier returns can be approved.");
        }

        var debitNoteNumber = await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            FinanceDocumentTypes.APSupplierDebitNote,
            tenantId,
            supplierReturn.ReturnDate,
            nameof(SupplierDebitNote),
            cancellationToken: cancellationToken);

        var debitNote = new SupplierDebitNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DebitNoteNumber = debitNoteNumber,
            VendorId = supplierReturn.VendorId,
            SupplierReturnId = supplierReturn.Id,
            OriginalVendorInvoiceId = supplierReturn.OriginalVendorInvoiceId,
            DebitNoteDate = DateTime.UtcNow,
            CurrencyCode = supplierReturn.CurrencyCode,
            ExchangeRate = supplierReturn.ExchangeRate,
            SubTotal = supplierReturn.SubTotal,
            TaxAmount = supplierReturn.TaxAmount,
            DiscountAmount = supplierReturn.DiscountAmount,
            TotalAmount = supplierReturn.TotalAmount,
            BaseCurrencyAmount = supplierReturn.BaseCurrencyAmount,
            Status = SupplierDebitNoteStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "system"
        };

        foreach (var line in supplierReturn.LineItems.Where(l => !l.IsDeleted))
        {
            debitNote.LineItems.Add(new SupplierDebitNoteLineItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SupplierDebitNoteId = debitNote.Id,
                Description = line.Description,
                Quantity = line.QuantityReturned,
                UnitPrice = line.UnitPrice,
                TaxGroupId = line.TaxGroupId,
                TaxRate = line.TaxRate,
                TaxAmount = line.TaxAmount,
                DiscountPercentage = line.DiscountPercentage,
                DiscountAmount = line.DiscountAmount,
                LineTotal = line.LineTotal,
                CreatedAt = debitNote.CreatedAt,
                CreatedBy = debitNote.CreatedBy
            });
        }

        supplierReturn.Status = SupplierReturnStatus.Approved;
        supplierReturn.UpdatedAt = DateTime.UtcNow;
        supplierReturn.UpdatedBy = _currentUserService.UserName ?? "system";

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.SupplierDebitNotes.Add(debitNote);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _subledgerPostingService.PostSupplierDebitNoteAsync(debitNote.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var refreshed = await BaseQuery(tenantId)
            .AsSplitQuery()
            .FirstAsync(r => r.Id == id, cancellationToken);

        return Ok(MapToDto(refreshed));
    }

    private IQueryable<SupplierReturn> BaseQuery(Guid tenantId)
        => _dbContext.SupplierReturns
            .Include(r => r.LineItems.Where(l => !l.IsDeleted))
            .Include(r => r.OriginalVendorInvoice)
            .Include(r => r.OriginalFinancePurchaseOrderReceipt)
            .Include(r => r.Vendor)
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

    private SupplierReturnDto MapToDto(SupplierReturn supplierReturn)
    {
        var debitNote = _dbContext.SupplierDebitNotes
            .AsNoTracking()
            .Where(d => d.SupplierReturnId == supplierReturn.Id && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new SupplierDebitNoteDto
            {
                Id = d.Id,
                DebitNoteNumber = d.DebitNoteNumber,
                JournalEntryId = d.JournalEntryId,
                Status = (int)d.Status,
                StatusName = d.Status.ToString()
            })
            .FirstOrDefault();

        return new SupplierReturnDto
        {
            Id = supplierReturn.Id,
            ReturnNumber = supplierReturn.ReturnNumber,
            VendorId = supplierReturn.VendorId,
            VendorName = supplierReturn.VendorName,
            OriginalVendorInvoiceId = supplierReturn.OriginalVendorInvoiceId,
            OriginalFinancePurchaseOrderReceiptId = supplierReturn.OriginalFinancePurchaseOrderReceiptId,
            OriginalVendorInvoice = supplierReturn.OriginalVendorInvoice == null
                ? null
                : new LinkedVendorInvoiceDto
                {
                    Id = supplierReturn.OriginalVendorInvoice.Id,
                    InvoiceNumber = supplierReturn.OriginalVendorInvoice.InvoiceNumber
                },
            OriginalFinancePurchaseOrderReceipt = supplierReturn.OriginalFinancePurchaseOrderReceipt == null
                ? null
                : new LinkedFinancePurchaseOrderReceiptDto
                {
                    Id = supplierReturn.OriginalFinancePurchaseOrderReceipt.Id,
                    ReceiptNumber = supplierReturn.OriginalFinancePurchaseOrderReceipt.ReceiptNumber
                },
            ReturnDate = supplierReturn.ReturnDate,
            Reason = supplierReturn.Reason,
            CurrencyCode = supplierReturn.CurrencyCode,
            ExchangeRate = supplierReturn.ExchangeRate,
            SubTotal = supplierReturn.SubTotal,
            TaxAmount = supplierReturn.TaxAmount,
            DiscountAmount = supplierReturn.DiscountAmount,
            TotalAmount = supplierReturn.TotalAmount,
            BaseCurrencyAmount = supplierReturn.BaseCurrencyAmount,
            Status = (int)supplierReturn.Status,
            StatusName = supplierReturn.Status.ToString(),
            DebitNote = debitNote,
            LineItems = supplierReturn.LineItems
                .Where(l => !l.IsDeleted)
                .OrderBy(l => l.CreatedAt)
                .Select(l => new SupplierReturnLineItemDto
                {
                    Id = l.Id,
                    OriginalVendorInvoiceLineItemId = l.OriginalVendorInvoiceLineItemId,
                    OriginalFinancePurchaseOrderItemId = l.OriginalFinancePurchaseOrderItemId,
                    Description = l.Description,
                    QuantityReturned = l.QuantityReturned,
                    UnitPrice = l.UnitPrice,
                    TaxGroupId = l.TaxGroupId,
                    TaxRate = l.TaxRate,
                    TaxAmount = l.TaxAmount,
                    DiscountPercentage = l.DiscountPercentage,
                    DiscountAmount = l.DiscountAmount,
                    LineTotal = l.LineTotal
                })
                .ToList(),
            CreatedAt = supplierReturn.CreatedAt
        };
    }

    private static (decimal NetAmount, decimal TaxAmount, decimal DiscountAmount, decimal LineTotal) CalculateLineAmounts(
        decimal quantity,
        decimal unitPrice,
        decimal taxAmount,
        decimal discountPercentage,
        decimal discountAmount,
        decimal submittedLineTotal)
    {
        var grossAmount = quantity * unitPrice;
        var calculatedDiscount = discountAmount > 0m ? discountAmount : grossAmount * (discountPercentage / 100m);
        var netAmount = grossAmount - calculatedDiscount;
        var lineTotal = submittedLineTotal > 0m ? submittedLineTotal : netAmount + taxAmount;

        return (
            decimal.Round(netAmount, 2, MidpointRounding.AwayFromZero),
            decimal.Round(taxAmount, 2, MidpointRounding.AwayFromZero),
            decimal.Round(calculatedDiscount, 2, MidpointRounding.AwayFromZero),
            decimal.Round(lineTotal, 2, MidpointRounding.AwayFromZero));
    }

    private static decimal ToBaseAmount(decimal foreignAmount, decimal exchangeRate)
        => decimal.Round(foreignAmount * NormalizeExchangeRate(exchangeRate), 2, MidpointRounding.AwayFromZero);

    private static decimal NormalizeExchangeRate(decimal exchangeRate)
        => exchangeRate <= 0m ? 1m : exchangeRate;

    private static string NormalizeCurrency(string? currencyCode)
        => string.IsNullOrWhiteSpace(currencyCode) ? "GHS" : currencyCode.Trim().ToUpperInvariant();
}
