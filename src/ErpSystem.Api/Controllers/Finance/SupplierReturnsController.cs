using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Api.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/ap/supplier-returns")]
public class SupplierReturnsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly IFinancePostingEngine _financePostingEngine;

    public SupplierReturnsController(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IDocumentNumberingService documentNumberingService,
        IFinancePostingEngine financePostingEngine)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _documentNumberingService = documentNumberingService;
        _financePostingEngine = financePostingEngine;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

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

        // The API repeats the UI remaining-quantity checks because debit-note
        // posting must not rely on client-side source line validation.
        var quantityValidationError = await ValidateSupplierReturnQuantitiesAsync(dto, tenantId, cancellationToken);
        if (quantityValidationError != null)
        {
            return BadRequest(quantityValidationError);
        }

        var currencyCode = NormalizeCurrency(dto.CurrencyCode);
        var exchangeRate = NormalizeExchangeRate(dto.ExchangeRate);
        var returnDate = dto.ReturnDate == default ? DateTime.UtcNow : dto.ReturnDate;
        var returnNumber = await ResolveReturnNumberAsync(tenantId, returnDate, dto.ReturnNumber, cancellationToken);

        var returnNumberExists = await _dbContext.SupplierReturns
            .AnyAsync(r => r.TenantId == tenantId && !r.IsDeleted && r.ReturnNumber == returnNumber, cancellationToken);
        if (returnNumberExists)
        {
            return BadRequest($"Supplier return '{returnNumber}' already exists.");
        }

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

        await PostSupplierDebitNoteThroughFinancePostingEngineAsync(debitNote.Id, cancellationToken);
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

    private async Task<string?> ValidateSupplierReturnQuantitiesAsync(
        CreateSupplierReturnDto dto,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (dto.OriginalVendorInvoiceId.HasValue)
        {
            return await ValidateVendorInvoiceReturnQuantitiesAsync(
                tenantId,
                dto.OriginalVendorInvoiceId.Value,
                dto.Lines,
                cancellationToken);
        }

        if (dto.OriginalFinancePurchaseOrderReceiptId.HasValue)
        {
            return await ValidateFinanceGrvReturnQuantitiesAsync(
                tenantId,
                dto.OriginalFinancePurchaseOrderReceiptId.Value,
                dto.Lines,
                cancellationToken);
        }

        return "Select either an original supplier invoice or an original finance GRV.";
    }

    private async Task<string?> ValidateVendorInvoiceReturnQuantitiesAsync(
        Guid tenantId,
        Guid invoiceId,
        IReadOnlyList<CreateSupplierReturnLineItemDto> lines,
        CancellationToken cancellationToken)
    {
        var sourceLineRows = await _dbContext.Set<VendorInvoiceLineItem>()
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.VendorInvoiceId == invoiceId && !l.IsDeleted)
            .Select(l => new { l.Id, l.Description, l.Quantity })
            .ToListAsync(cancellationToken);

        var sourceLines = sourceLineRows.ToDictionary(
            l => l.Id,
            l => new ReturnSourceLine(l.Description, l.Quantity));

        if (sourceLines.Count == 0)
        {
            return "Original supplier invoice has no returnable lines.";
        }

        var submittedByLine = new Dictionary<Guid, decimal>();
        foreach (var line in lines)
        {
            if (!line.OriginalVendorInvoiceLineItemId.HasValue)
            {
                return $"Return line '{line.Description}' must reference an original supplier invoice line.";
            }

            if (line.OriginalFinancePurchaseOrderItemId.HasValue)
            {
                return $"Return line '{line.Description}' cannot mix supplier invoice and finance GRV source lines.";
            }

            var sourceLineId = line.OriginalVendorInvoiceLineItemId.Value;
            if (!sourceLines.ContainsKey(sourceLineId))
            {
                return $"Return line '{line.Description}' does not belong to the selected supplier invoice.";
            }

            submittedByLine[sourceLineId] = submittedByLine.GetValueOrDefault(sourceLineId) + line.QuantityReturned;
        }

        var sourceLineIds = submittedByLine.Keys.ToList();
        var priorReturnRows = await _dbContext.Set<SupplierReturnLineItem>()
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId &&
                !l.IsDeleted &&
                l.OriginalVendorInvoiceLineItemId.HasValue &&
                sourceLineIds.Contains(l.OriginalVendorInvoiceLineItemId.Value) &&
                l.SupplierReturn.TenantId == tenantId &&
                !l.SupplierReturn.IsDeleted &&
                l.SupplierReturn.OriginalVendorInvoiceId == invoiceId &&
                l.SupplierReturn.Status != SupplierReturnStatus.Cancelled)
            .Select(l => new
            {
                SourceLineId = l.OriginalVendorInvoiceLineItemId!.Value,
                l.QuantityReturned
            })
            .ToListAsync(cancellationToken);

        var priorReturns = priorReturnRows
            .Select(l => new ReturnQuantity(l.SourceLineId, l.QuantityReturned))
            .ToList();

        return ValidateSubmittedReturnQuantities(sourceLines, submittedByLine, priorReturns);
    }

    private async Task<string?> ValidateFinanceGrvReturnQuantitiesAsync(
        Guid tenantId,
        Guid receiptId,
        IReadOnlyList<CreateSupplierReturnLineItemDto> lines,
        CancellationToken cancellationToken)
    {
        var sourceLineRows = await _dbContext.FinancePurchaseOrderReceiptItems
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId &&
                !l.IsDeleted &&
                l.FinancePurchaseOrderReceiptId == receiptId &&
                l.FinancePurchaseOrderReceipt.TenantId == tenantId &&
                !l.FinancePurchaseOrderReceipt.IsDeleted)
            .Select(l => new
            {
                l.FinancePurchaseOrderItemId,
                Description = l.FinancePurchaseOrderItem.Description,
                Quantity = l.QuantityReceived
            })
            .ToListAsync(cancellationToken);

        var sourceLines = sourceLineRows
            .GroupBy(l => l.FinancePurchaseOrderItemId)
            .ToDictionary(
                g => g.Key,
                g => new ReturnSourceLine(
                    g.First().Description,
                    g.Sum(l => l.Quantity)));

        if (sourceLines.Count == 0)
        {
            return "Original finance GRV has no returnable lines.";
        }

        var submittedByLine = new Dictionary<Guid, decimal>();
        foreach (var line in lines)
        {
            if (!line.OriginalFinancePurchaseOrderItemId.HasValue)
            {
                return $"Return line '{line.Description}' must reference an original finance PO line from the selected GRV.";
            }

            if (line.OriginalVendorInvoiceLineItemId.HasValue)
            {
                return $"Return line '{line.Description}' cannot mix finance GRV and supplier invoice source lines.";
            }

            var sourceLineId = line.OriginalFinancePurchaseOrderItemId.Value;
            if (!sourceLines.ContainsKey(sourceLineId))
            {
                return $"Return line '{line.Description}' does not belong to the selected finance GRV.";
            }

            submittedByLine[sourceLineId] = submittedByLine.GetValueOrDefault(sourceLineId) + line.QuantityReturned;
        }

        var sourceLineIds = submittedByLine.Keys.ToList();
        var priorReturnRows = await _dbContext.Set<SupplierReturnLineItem>()
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId &&
                !l.IsDeleted &&
                l.OriginalFinancePurchaseOrderItemId.HasValue &&
                sourceLineIds.Contains(l.OriginalFinancePurchaseOrderItemId.Value) &&
                l.SupplierReturn.TenantId == tenantId &&
                !l.SupplierReturn.IsDeleted &&
                l.SupplierReturn.OriginalFinancePurchaseOrderReceiptId == receiptId &&
                l.SupplierReturn.Status != SupplierReturnStatus.Cancelled)
            .Select(l => new
            {
                SourceLineId = l.OriginalFinancePurchaseOrderItemId!.Value,
                l.QuantityReturned
            })
            .ToListAsync(cancellationToken);

        var priorReturns = priorReturnRows
            .Select(l => new ReturnQuantity(l.SourceLineId, l.QuantityReturned))
            .ToList();

        return ValidateSubmittedReturnQuantities(sourceLines, submittedByLine, priorReturns);
    }

    private static string? ValidateSubmittedReturnQuantities(
        IReadOnlyDictionary<Guid, ReturnSourceLine> sourceLines,
        IReadOnlyDictionary<Guid, decimal> submittedByLine,
        IEnumerable<ReturnQuantity> priorReturns)
    {
        var priorReturnedByLine = priorReturns
            .GroupBy(l => l.SourceLineId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.QuantityReturned));

        foreach (var (sourceLineId, submittedQuantity) in submittedByLine)
        {
            var sourceLine = sourceLines[sourceLineId];
            var priorReturned = priorReturnedByLine.GetValueOrDefault(sourceLineId);
            var remainingQuantity = sourceLine.Quantity - priorReturned;
            if (submittedQuantity > remainingQuantity)
            {
                return $"Return quantity for line '{sourceLine.Description}' exceeds the remaining returnable quantity.";
            }
        }

        return null;
    }

    private sealed record ReturnSourceLine(string Description, decimal Quantity);

    private sealed record ReturnQuantity(Guid SourceLineId, decimal QuantityReturned);

    private async Task PostSupplierDebitNoteThroughFinancePostingEngineAsync(Guid debitNoteId, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var debitNote = await _dbContext.SupplierDebitNotes
            .Include(d => d.Vendor)
            .Include(d => d.LineItems.Where(l => !l.IsDeleted))
            .Include(d => d.SupplierReturn)
                .ThenInclude(r => r!.LineItems.Where(l => !l.IsDeleted))
            .Include(d => d.OriginalVendorInvoice)
                .ThenInclude(i => i!.LineItems)
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == debitNoteId && !d.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Supplier debit note was not found for this tenant.");

        if (debitNote.Status == SupplierDebitNoteStatus.Posted && debitNote.JournalEntryId.HasValue)
        {
            return;
        }

        var supplierReturn = debitNote.SupplierReturn
            ?? throw new InvalidOperationException($"Supplier debit note {debitNote.DebitNoteNumber} is not linked to a supplier return.");

        var settings = await _dbContext.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings not configured for this tenant.");

        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
        var currencyCode = NormalizeCurrency(debitNote.CurrencyCode, functionalCurrency);
        var exchangeRate = NormalizeExchangeRate(debitNote.ExchangeRate);
        var invoiceLinked = debitNote.OriginalVendorInvoiceId.HasValue;
        var grvLinked = supplierReturn.OriginalFinancePurchaseOrderReceiptId.HasValue && !invoiceLinked;
        if (!invoiceLinked && !grvLinked)
        {
            throw new InvalidOperationException($"Supplier return {supplierReturn.ReturnNumber} must reference either a vendor invoice or a finance GRV.");
        }

        var controlAccountId = invoiceLinked
            ? debitNote.Vendor.DefaultApAccountId ?? settings.ControlAccountApId
            : settings.ControlAccountGRVAccrualId;
        if (!controlAccountId.HasValue)
        {
            throw new InvalidOperationException(invoiceLinked
                ? "AP Control Account not configured."
                : "GRV Accrual Control Account is not configured in Finance Settings.");
        }

        var returnLinesByIndex = supplierReturn.LineItems.Where(l => !l.IsDeleted).OrderBy(l => l.CreatedAt).ToList();
        var debitLinesByIndex = debitNote.LineItems.Where(l => !l.IsDeleted).OrderBy(l => l.CreatedAt).ToList();
        var creditLines = new List<FinancePostingLineDto>();
        var lineNumber = 2;

        Dictionary<Guid, VendorInvoiceLineItem>? vendorInvoiceLineById = null;
        if (invoiceLinked)
        {
            vendorInvoiceLineById = debitNote.OriginalVendorInvoice?.LineItems?
                .Where(l => !l.IsDeleted)
                .ToDictionary(l => l.Id, l => l)
                ?? new Dictionary<Guid, VendorInvoiceLineItem>();
        }

        Dictionary<Guid, FinancePurchaseOrderItem>? financePoLineById = null;
        if (grvLinked)
        {
            var financePoLineIds = returnLinesByIndex
                .Where(r => r.OriginalFinancePurchaseOrderItemId.HasValue)
                .Select(r => r.OriginalFinancePurchaseOrderItemId!.Value)
                .ToList();

            financePoLineById = await _dbContext.FinancePurchaseOrderItems
                .Where(l => l.TenantId == tenantId && financePoLineIds.Contains(l.Id) && !l.IsDeleted)
                .ToDictionaryAsync(l => l.Id, cancellationToken);
        }

        for (var index = 0; index < debitLinesByIndex.Count; index++)
        {
            var debitLine = debitLinesByIndex[index];
            var returnLine = index < returnLinesByIndex.Count ? returnLinesByIndex[index] : null;
            var lineSourceAmount = decimal.Round(debitLine.LineTotal - debitLine.TaxAmount, 2, MidpointRounding.AwayFromZero);
            if (lineSourceAmount <= 0m)
            {
                continue;
            }

            Guid creditAccountId;
            if (invoiceLinked)
            {
                VendorInvoiceLineItem? sourceLine = null;
                if (returnLine?.OriginalVendorInvoiceLineItemId.HasValue == true && vendorInvoiceLineById != null)
                {
                    vendorInvoiceLineById.TryGetValue(returnLine.OriginalVendorInvoiceLineItemId.Value, out sourceLine);
                }

                if (string.Equals(sourceLine?.LineItemType, "Inventory", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sourceLine?.LineItemType, "Product", StringComparison.OrdinalIgnoreCase))
                {
                    creditAccountId = settings.ControlAccountInventoryId
                        ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.");
                }
                else
                {
                    creditAccountId = sourceLine?.GLAccountId
                        ?? debitNote.Vendor.DefaultExpenseAccountId
                        ?? throw new InvalidOperationException($"No expense account could be resolved for supplier return line '{debitLine.Description}'.");
                }
            }
            else
            {
                FinancePurchaseOrderItem? sourceLine = null;
                if (returnLine?.OriginalFinancePurchaseOrderItemId.HasValue == true && financePoLineById != null)
                {
                    financePoLineById.TryGetValue(returnLine.OriginalFinancePurchaseOrderItemId.Value, out sourceLine);
                }

                if (sourceLine == null)
                {
                    throw new InvalidOperationException($"No finance PO line could be resolved for supplier return line '{debitLine.Description}'.");
                }

                creditAccountId = sourceLine.LineType == 1
                    ? settings.ControlAccountInventoryId
                        ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.")
                    : sourceLine.GlAccountId
                        ?? throw new InvalidOperationException($"No GL account specified for finance PO return line '{sourceLine.Description}'.");
            }

            creditLines.Add(BuildPostingLine(
                creditAccountId,
                $"Supplier return {debitNote.DebitNoteNumber} - {debitLine.Description}",
                0m,
                ToFunctionalAmount(lineSourceAmount, currencyCode, functionalCurrency, exchangeRate),
                lineSourceAmount,
                currencyCode,
                functionalCurrency,
                exchangeRate,
                debitNote.DebitNoteDate,
                debitNote.DebitNoteNumber,
                lineNumber++,
                "AP-SupplierReturn-Line"));
        }

        if (invoiceLinked && debitNote.TaxAmount > 0m)
        {
            var taxAccountId = settings.ControlAccountTaxId
                ?? throw new InvalidOperationException("Tax Control Account not configured.");

            creditLines.Add(BuildPostingLine(
                taxAccountId,
                $"Reverse input tax - {debitNote.DebitNoteNumber}",
                0m,
                ToFunctionalAmount(debitNote.TaxAmount, currencyCode, functionalCurrency, exchangeRate),
                debitNote.TaxAmount,
                currencyCode,
                functionalCurrency,
                exchangeRate,
                debitNote.DebitNoteDate,
                debitNote.DebitNoteNumber,
                lineNumber++,
                "AP-SupplierReturn-Tax"));
        }

        var creditFunctionalTotal = decimal.Round(creditLines.Sum(l => l.CreditAmount), 2, MidpointRounding.AwayFromZero);
        if (creditFunctionalTotal <= 0m)
        {
            throw new InvalidOperationException($"Supplier debit note {debitNote.DebitNoteNumber} has no positive-value lines to post.");
        }

        var controlSourceAmount = invoiceLinked ? debitNote.TotalAmount : debitNote.SubTotal;
        var lines = new List<FinancePostingLineDto>
        {
            BuildPostingLine(
                controlAccountId.Value,
                invoiceLinked
                    ? $"Supplier debit note {debitNote.DebitNoteNumber}"
                    : $"Reverse GRV accrual - {debitNote.DebitNoteNumber}",
                creditFunctionalTotal,
                0m,
                controlSourceAmount,
                currencyCode,
                functionalCurrency,
                exchangeRate,
                debitNote.DebitNoteDate,
                debitNote.DebitNoteNumber,
                1,
                invoiceLinked ? "AP-SupplierDebitNote-Control" : "AP-GRV-Return-Control")
        };
        lines.AddRange(creditLines);

        var result = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "AP",
            SourceDocumentType = "SupplierDebitNote",
            SourceDocumentId = debitNote.Id,
            SourceDocumentTenantId = tenantId,
            PostingAction = "PostSupplierDebitNote",
            SourceDocumentReference = debitNote.DebitNoteNumber,
            Description = $"Supplier Debit Note {debitNote.DebitNoteNumber} - {debitNote.Vendor.PartnerName}",
            PostingDate = debitNote.DebitNoteDate,
            JournalType = "System Generated",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = $"SupplierDebitNote:{tenantId:N}:{debitNote.Id:N}:Post",
            Lines = lines
        }, cancellationToken);

        debitNote.JournalEntryId = result.JournalEntryId;
        debitNote.Status = SupplierDebitNoteStatus.Posted;
        debitNote.UpdatedAt = DateTime.UtcNow;
        debitNote.UpdatedBy = _currentUserService.UserName ?? "system";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private SupplierReturnDto MapToDto(SupplierReturn supplierReturn)
    {
        var debitNote = _dbContext.SupplierDebitNotes
            .AsNoTracking()
            .Where(d => d.TenantId == supplierReturn.TenantId && d.SupplierReturnId == supplierReturn.Id && !d.IsDeleted)
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

    private async Task<string> GenerateReturnNumberAsync(Guid tenantId, DateTime returnDate, CancellationToken cancellationToken)
    {
        return await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            FinanceDocumentTypes.APSupplierReturn,
            tenantId,
            returnDate,
            nameof(SupplierReturn),
            cancellationToken: cancellationToken);
    }

    private async Task<string> ResolveReturnNumberAsync(
        Guid tenantId,
        DateTime returnDate,
        string? requestedReturnNumber,
        CancellationToken cancellationToken)
    {
        var manualReturnNumber = requestedReturnNumber?.Trim();
        if (string.IsNullOrWhiteSpace(manualReturnNumber))
        {
            return await GenerateReturnNumberAsync(tenantId, returnDate, cancellationToken);
        }

        var definitions = await _documentNumberingService.GetDefinitionsAsync(
            DocumentNumberingModules.Finance,
            tenantId,
            cancellationToken);

        var definition = definitions
            .Where(d => d.DocumentType == FinanceDocumentTypes.APSupplierReturn
                && d.IsActive
                && d.IsDefault
                && (d.EffectiveFrom == null || d.EffectiveFrom <= returnDate)
                && (d.EffectiveTo == null || d.EffectiveTo >= returnDate))
            .OrderByDescending(d => d.EffectiveFrom ?? DateTime.MinValue)
            .FirstOrDefault();

        if (definition?.AllowManualEntry == true)
        {
            return manualReturnNumber;
        }

        return await GenerateReturnNumberAsync(tenantId, returnDate, cancellationToken);
    }

    private static decimal ToBaseAmount(decimal foreignAmount, decimal exchangeRate)
        => decimal.Round(foreignAmount * NormalizeExchangeRate(exchangeRate), 2, MidpointRounding.AwayFromZero);

    private static decimal NormalizeExchangeRate(decimal exchangeRate)
        => exchangeRate <= 0m ? 1m : exchangeRate;

    private static string NormalizeCurrency(string? currencyCode)
        => string.IsNullOrWhiteSpace(currencyCode) ? "GHS" : currencyCode.Trim().ToUpperInvariant();

    private static string NormalizeCurrency(string? currencyCode, string fallback)
        => string.IsNullOrWhiteSpace(currencyCode) ? fallback.Trim().ToUpperInvariant() : currencyCode.Trim().ToUpperInvariant();

    private static decimal ToFunctionalAmount(decimal sourceAmount, string transactionCurrency, string functionalCurrency, decimal exchangeRate)
        => string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase)
            ? decimal.Round(sourceAmount, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(sourceAmount * exchangeRate, 2, MidpointRounding.AwayFromZero);

    private static FinancePostingLineDto BuildPostingLine(
        Guid accountId,
        string description,
        decimal debitAmount,
        decimal creditAmount,
        decimal sourceAmount,
        string transactionCurrency,
        string functionalCurrency,
        decimal exchangeRate,
        DateTime rateDate,
        string reference,
        int lineNumber,
        string tag)
    {
        var sameCurrency = string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            Description = description,
            DebitAmount = decimal.Round(debitAmount, 2, MidpointRounding.AwayFromZero),
            CreditAmount = decimal.Round(creditAmount, 2, MidpointRounding.AwayFromZero),
            TransactionCurrency = transactionCurrency,
            TransactionDebitAmount = debitAmount > 0m ? decimal.Round(sourceAmount, 2, MidpointRounding.AwayFromZero) : 0m,
            TransactionCreditAmount = creditAmount > 0m ? decimal.Round(sourceAmount, 2, MidpointRounding.AwayFromZero) : 0m,
            ForeignCurrencyAmount = sameCurrency ? null : decimal.Round(sourceAmount, 2, MidpointRounding.AwayFromZero),
            ExchangeRate = sameCurrency ? null : exchangeRate,
            ExchangeRateDate = sameCurrency ? null : rateDate.Date,
            SourceReferenceNumber = reference,
            LineNumber = lineNumber,
            TransactionTag = tag
        };
    }
}
