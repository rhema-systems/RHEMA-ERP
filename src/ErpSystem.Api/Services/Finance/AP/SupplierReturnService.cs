using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Finance.AP
{
    public class SupplierReturnService : ISupplierReturnService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ISubledgerPostingService _subledgerPostingService;
        private readonly IInventoryReturnService _inventoryReturnService;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<SupplierReturnService> _logger;

        public SupplierReturnService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ISubledgerPostingService subledgerPostingService,
            IInventoryReturnService inventoryReturnService,
            ITenantSettingsService tenantSettingsService,
            ILogger<SupplierReturnService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _subledgerPostingService = subledgerPostingService;
            _inventoryReturnService = inventoryReturnService;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
        private string UserName => _currentUser.UserName ?? "system";

        public async Task<SupplierReturn> CreateReturnAsync(CreateSupplierReturnDto dto)
        {
            // Validate supplier
            var vendor = await _unitOfWork.Repository<BusinessPartner>()
                .FirstOrDefaultAsync(bp => bp.TenantId == TenantId && bp.Id == dto.VendorId);
            if (vendor == null)
                throw new ArgumentException($"Vendor with Id '{dto.VendorId}' not found.");

            // Resolve base currency
            var baseCurrency = "GHS";
            try
            {
                baseCurrency = await _tenantSettingsService.GetBaseCurrencyAsync() ?? "GHS";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve base currency from tenant settings. Defaulting to GHS.");
            }

            var returnNumber = dto.ReturnNumber;
            if (string.IsNullOrWhiteSpace(returnNumber))
            {
                returnNumber = await GenerateReturnNumberAsync(CancellationToken.None);
            }

            var supplierReturn = new SupplierReturn
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ReturnNumber = returnNumber,
                VendorId = dto.VendorId,
                VendorName = vendor.PartnerName,
                OriginalVendorInvoiceId = dto.OriginalVendorInvoiceId,
                OriginalFinancePurchaseOrderReceiptId = dto.OriginalFinancePurchaseOrderReceiptId,
                ReturnDate = dto.ReturnDate,
                Reason = dto.Reason,
                Status = SupplierReturnStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            decimal exchangeRate = 1.0m;
            string currencyCode = "GHS";

            // If Invoice-linked
            if (dto.OriginalVendorInvoiceId.HasValue)
            {
                var invoice = await _unitOfWork.Repository<VendorInvoice>()
                    .GetQueryable(i => i.TenantId == TenantId && i.Id == dto.OriginalVendorInvoiceId.Value)
                    .Include(i => i.LineItems)
                    .FirstOrDefaultAsync();

                if (invoice == null)
                    throw new ArgumentException($"Original vendor invoice '{dto.OriginalVendorInvoiceId.Value}' not found.");

                if (invoice.Status == VendorInvoiceStatus.Draft || invoice.Status == VendorInvoiceStatus.Voided || invoice.Status == VendorInvoiceStatus.Rejected)
                    throw new InvalidOperationException("Cannot return goods against a draft, voided, or rejected invoice.");

                // Lock exchange rate & currency code
                currencyCode = invoice.CurrencyCode;
                exchangeRate = invoice.ExchangeRate;

                // Validate exchange rate override / currency mismatch
                if (dto.ExchangeRate != invoice.ExchangeRate || dto.CurrencyCode != invoice.CurrencyCode)
                {
                    throw new ArgumentException("Exchange rate override is not allowed. It must match the original source document.");
                }

                decimal subTotal = 0;
                decimal taxAmount = 0;
                decimal discountAmount = 0;

                foreach (var dtoLine in dto.Lines)
                {
                    if (!dtoLine.OriginalVendorInvoiceLineItemId.HasValue)
                        throw new ArgumentException("Line item is missing OriginalVendorInvoiceLineItemId link.");

                    var originalLine = invoice.LineItems.FirstOrDefault(li => li.Id == dtoLine.OriginalVendorInvoiceLineItemId.Value);
                    if (originalLine == null)
                        throw new ArgumentException($"Invoice line '{dtoLine.OriginalVendorInvoiceLineItemId.Value}' not found on invoice.");

                    if (dtoLine.QuantityReturned <= 0)
                        throw new ArgumentException("Quantity returned must be greater than zero.");

                    // Over-return validation
                    var alreadyReturnedList = await _unitOfWork.Repository<SupplierReturnLineItem>()
                        .GetQueryable(rl => rl.OriginalVendorInvoiceLineItemId == dtoLine.OriginalVendorInvoiceLineItemId.Value && rl.SupplierReturn.Status != SupplierReturnStatus.Cancelled)
                        .Select(rl => rl.QuantityReturned)
                        .ToListAsync();
                    var alreadyReturned = alreadyReturnedList.Sum();

                    var remaining = originalLine.Quantity - alreadyReturned;
                    if (dtoLine.QuantityReturned > remaining)
                        throw new ArgumentException($"Cannot return {dtoLine.QuantityReturned} units; only {remaining} units remain returnable on invoice line.");

                    // Unit price must lock to the original invoice line unit price
                    if (dtoLine.UnitPrice != originalLine.UnitPrice)
                        throw new ArgumentException($"Unit price must match the original invoice line's unit price of {originalLine.UnitPrice}.");

                    // Proportional tax reclaim calculation
                    decimal lineTax = 0;
                    if (originalLine.Quantity > 0)
                    {
                        lineTax = (dtoLine.QuantityReturned / originalLine.Quantity) * originalLine.TaxAmount;
                    }

                    // Proportional discount reversal calculation
                    decimal lineDiscount = 0;
                    if (originalLine.Quantity > 0)
                    {
                        lineDiscount = Math.Round((dtoLine.QuantityReturned / originalLine.Quantity) * originalLine.DiscountAmount, 2);
                    }

                    var lineGrossAmount = dtoLine.QuantityReturned * originalLine.UnitPrice;
                    var lineSubTotal = lineGrossAmount - lineDiscount;
                    var lineTotal = lineSubTotal + lineTax;

                    var returnLine = new SupplierReturnLineItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        SupplierReturnId = supplierReturn.Id,
                        OriginalVendorInvoiceLineItemId = dtoLine.OriginalVendorInvoiceLineItemId,
                        Description = originalLine.Description,
                        QuantityReturned = dtoLine.QuantityReturned,
                        UnitPrice = originalLine.UnitPrice,
                        TaxGroupId = originalLine.TaxGroupId,
                        TaxRate = originalLine.TaxRate,
                        TaxAmount = lineTax,
                        DiscountPercentage = originalLine.DiscountPercentage,
                        DiscountAmount = lineDiscount,
                        LineTotal = lineTotal,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = UserName
                    };

                    supplierReturn.LineItems.Add(returnLine);
                    subTotal += lineSubTotal;
                    taxAmount += lineTax;
                    discountAmount += lineDiscount;
                }

                supplierReturn.SubTotal = subTotal;
                supplierReturn.TaxAmount = taxAmount;
                supplierReturn.DiscountAmount = discountAmount;
                supplierReturn.TotalAmount = subTotal + taxAmount;
                supplierReturn.CurrencyCode = currencyCode;
                supplierReturn.ExchangeRate = exchangeRate;
                supplierReturn.BaseCurrencyAmount = supplierReturn.TotalAmount * exchangeRate;
            }
            // If GRV-only (uninvoiced receipt)
            else if (dto.OriginalFinancePurchaseOrderReceiptId.HasValue)
            {
                var receipt = await _unitOfWork.Repository<FinancePurchaseOrderReceipt>()
                    .GetQueryable(r => r.TenantId == TenantId && r.Id == dto.OriginalFinancePurchaseOrderReceiptId.Value)
                    .Include(r => r.Items)
                    .Include(r => r.FinancePurchaseOrder)
                        .ThenInclude(po => po.Items)
                    .FirstOrDefaultAsync();

                if (receipt == null)
                    throw new ArgumentException($"Original Purchase Order Receipt (GRV) '{dto.OriginalFinancePurchaseOrderReceiptId.Value}' not found.");

                // Lock exchange rate & currency code to PO
                currencyCode = receipt.FinancePurchaseOrder.CurrencyCode;
                exchangeRate = receipt.FinancePurchaseOrder.ExchangeRate;

                // Validate exchange rate override / currency mismatch
                if (dto.ExchangeRate != receipt.FinancePurchaseOrder.ExchangeRate || dto.CurrencyCode != receipt.FinancePurchaseOrder.CurrencyCode)
                {
                    throw new ArgumentException("Exchange rate override is not allowed. It must match the original source document.");
                }

                decimal subTotal = 0;
                decimal discountAmount = 0;

                foreach (var dtoLine in dto.Lines)
                {
                    if (!dtoLine.OriginalFinancePurchaseOrderItemId.HasValue)
                        throw new ArgumentException("Line item is missing OriginalFinancePurchaseOrderItemId link.");

                    var receiptItem = receipt.Items.FirstOrDefault(ri => ri.FinancePurchaseOrderItemId == dtoLine.OriginalFinancePurchaseOrderItemId.Value);
                    if (receiptItem == null)
                        throw new ArgumentException($"PO item '{dtoLine.OriginalFinancePurchaseOrderItemId.Value}' not found on Purchase Receipt.");

                    var poItem = receipt.FinancePurchaseOrder.Items.FirstOrDefault(pi => pi.Id == dtoLine.OriginalFinancePurchaseOrderItemId.Value);
                    if (poItem == null)
                        throw new ArgumentException("Associated PO item not found.");

                    if (dtoLine.QuantityReturned <= 0)
                        throw new ArgumentException("Quantity returned must be greater than zero.");

                    // Over-return validation
                    var alreadyReturnedList = await _unitOfWork.Repository<SupplierReturnLineItem>()
                        .GetQueryable(rl => rl.SupplierReturn.OriginalFinancePurchaseOrderReceiptId == dto.OriginalFinancePurchaseOrderReceiptId.Value && rl.OriginalFinancePurchaseOrderItemId == dtoLine.OriginalFinancePurchaseOrderItemId.Value && rl.SupplierReturn.Status != SupplierReturnStatus.Cancelled)
                        .Select(rl => rl.QuantityReturned)
                        .ToListAsync();
                    var alreadyReturned = alreadyReturnedList.Sum();

                    var remaining = receiptItem.QuantityReceived - alreadyReturned;
                    if (dtoLine.QuantityReturned > remaining)
                        throw new ArgumentException($"Cannot return {dtoLine.QuantityReturned} units; only {remaining} units remain returnable on GRV line.");

                    // Unit price must lock to PO item unit price
                    if (dtoLine.UnitPrice != poItem.UnitPrice)
                        throw new ArgumentException($"Unit price must match the original PO item's unit price of {poItem.UnitPrice}.");

                    // Proportional discount reversal calculation
                    decimal lineDiscount = 0;
                    if (poItem.OrderedQuantity > 0)
                    {
                        lineDiscount = Math.Round((dtoLine.QuantityReturned / poItem.OrderedQuantity) * poItem.DiscountAmount, 2);
                    }

                    var lineGrossAmount = dtoLine.QuantityReturned * poItem.UnitPrice;
                    var lineSubTotal = lineGrossAmount - lineDiscount;

                    var returnLine = new SupplierReturnLineItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        SupplierReturnId = supplierReturn.Id,
                        OriginalFinancePurchaseOrderItemId = dtoLine.OriginalFinancePurchaseOrderItemId,
                        Description = poItem.Description,
                        QuantityReturned = dtoLine.QuantityReturned,
                        UnitPrice = poItem.UnitPrice,
                        TaxGroupId = null,
                        TaxRate = 0,
                        TaxAmount = 0,
                        DiscountPercentage = poItem.DiscountPercentage,
                        DiscountAmount = lineDiscount,
                        LineTotal = lineSubTotal,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = UserName
                    };

                    supplierReturn.LineItems.Add(returnLine);
                    subTotal += lineSubTotal;
                    discountAmount += lineDiscount;
                }

                supplierReturn.SubTotal = subTotal;
                supplierReturn.TaxAmount = 0;
                supplierReturn.DiscountAmount = discountAmount;
                supplierReturn.TotalAmount = subTotal;
                supplierReturn.CurrencyCode = currencyCode;
                supplierReturn.ExchangeRate = exchangeRate;
                supplierReturn.BaseCurrencyAmount = supplierReturn.TotalAmount * exchangeRate;
            }
            else
            {
                throw new ArgumentException("Return must be linked to either an approved Vendor Invoice or a Purchase Receipt (GRV).");
            }

            await _unitOfWork.Repository<SupplierReturn>().AddAsync(supplierReturn);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created Supplier Return {ReturnNumber} for Vendor {VendorId}", returnNumber, dto.VendorId);
            return supplierReturn;
        }

        public async Task<SupplierReturn> GetByIdAsync(Guid id)
        {
            var ret = await _unitOfWork.Repository<SupplierReturn>()
                .GetQueryable(r => r.TenantId == TenantId && r.Id == id)
                .Include(r => r.Vendor)
                .Include(r => r.LineItems)
                .FirstOrDefaultAsync();

            if (ret == null)
                throw new KeyNotFoundException($"Supplier return with Id '{id}' not found.");

            return ret;
        }

        public async Task<IEnumerable<SupplierReturn>> GetAllAsync()
        {
            return await _unitOfWork.Repository<SupplierReturn>()
                .GetQueryable(r => r.TenantId == TenantId)
                .Include(r => r.Vendor)
                .Include(r => r.LineItems)
                .OrderByDescending(r => r.ReturnDate)
                .ToListAsync();
        }

        public async Task<SupplierReturn> ApproveReturnAsync(Guid id)
        {
            var ret = await _unitOfWork.Repository<SupplierReturn>()
                .GetQueryable(r => r.TenantId == TenantId && r.Id == id)
                .Include(r => r.Vendor)
                .Include(r => r.LineItems)
                .FirstOrDefaultAsync();

            if (ret == null)
                throw new KeyNotFoundException($"Supplier return with Id '{id}' not found.");

            if (ret.Status != SupplierReturnStatus.Draft)
                throw new InvalidOperationException("Approved or cancelled returns cannot be approved again.");

            ret.Status = SupplierReturnStatus.Approved;
            ret.UpdatedAt = DateTime.UtcNow;
            ret.UpdatedBy = UserName;

            // Invoice-linked Split Reversal Posting
            if (ret.OriginalVendorInvoiceId.HasValue)
            {
                var invoice = await _unitOfWork.Repository<VendorInvoice>()
                    .FirstOrDefaultAsync(i => i.TenantId == TenantId && i.Id == ret.OriginalVendorInvoiceId.Value);

                if (invoice == null)
                    throw new InvalidOperationException("Linked vendor invoice not found.");

                // Double-return quantity validation again to be safe
                foreach (var line in ret.LineItems)
                {
                    var originalLine = await _unitOfWork.Repository<VendorInvoiceLineItem>()
                        .FirstOrDefaultAsync(li => li.Id == line.OriginalVendorInvoiceLineItemId!.Value);
                    
                    if (originalLine != null)
                    {
                        var alreadyReturnedList = await _unitOfWork.Repository<SupplierReturnLineItem>()
                            .GetQueryable(rl => rl.OriginalVendorInvoiceLineItemId == line.OriginalVendorInvoiceLineItemId.Value && rl.SupplierReturnId != ret.Id && rl.SupplierReturn.Status == SupplierReturnStatus.Approved)
                            .Select(rl => rl.QuantityReturned)
                            .ToListAsync();
                        var alreadyReturned = alreadyReturnedList.Sum();

                        var remaining = originalLine.Quantity - alreadyReturned;
                        if (line.QuantityReturned > remaining)
                            throw new InvalidOperationException($"Cannot approve return. Double-return detected for line '{line.Description}'; only {remaining} units are returnable.");
                    }
                }

                // Create Supplier Debit Note
                var dbnNumber = await GenerateDebitNoteNumberAsync(CancellationToken.None);
                var debitNote = new SupplierDebitNote
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    DebitNoteNumber = dbnNumber,
                    VendorId = ret.VendorId,
                    SupplierReturnId = ret.Id,
                    OriginalVendorInvoiceId = ret.OriginalVendorInvoiceId,
                    DebitNoteDate = DateTime.UtcNow,
                    CurrencyCode = ret.CurrencyCode,
                    ExchangeRate = ret.ExchangeRate,
                    SubTotal = ret.SubTotal,
                    TaxAmount = ret.TaxAmount,
                    DiscountAmount = ret.DiscountAmount,
                    TotalAmount = ret.TotalAmount,
                    BaseCurrencyAmount = ret.BaseCurrencyAmount,
                    Status = SupplierDebitNoteStatus.Approved,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                };

                foreach (var line in ret.LineItems)
                {
                    var dbnLine = new SupplierDebitNoteLineItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
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
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = UserName
                    };
                    debitNote.LineItems.Add(dbnLine);
                }

                await _unitOfWork.Repository<SupplierDebitNote>().AddAsync(debitNote);

                // Update invoice paid amount
                invoice.PaidAmount += debitNote.TotalAmount;
                if (invoice.PaidAmount >= invoice.TotalAmount)
                {
                    invoice.Status = VendorInvoiceStatus.Paid;
                }
                else if (invoice.PaidAmount > 0)
                {
                    invoice.Status = VendorInvoiceStatus.PartiallyPaid;
                }

                await _unitOfWork.Repository<VendorInvoice>().UpdateAsync(invoice);
                await _unitOfWork.SaveChangesAsync();

                // Post GL reversals
                await _subledgerPostingService.PostSupplierDebitNoteAsync(debitNote.Id, CancellationToken.None);
            }
            // GRV-only Split Reversal Posting
            else if (ret.OriginalFinancePurchaseOrderReceiptId.HasValue)
            {
                var receipt = await _unitOfWork.Repository<FinancePurchaseOrderReceipt>()
                    .GetQueryable(r => r.TenantId == TenantId && r.Id == ret.OriginalFinancePurchaseOrderReceiptId.Value)
                    .Include(r => r.Items)
                    .Include(r => r.FinancePurchaseOrder)
                        .ThenInclude(po => po.Items)
                    .FirstOrDefaultAsync();

                if (receipt == null)
                    throw new InvalidOperationException("Linked Purchase Receipt not found.");

                var po = receipt.FinancePurchaseOrder;

                var inventoryLines = new List<FinanceReceiptInventoryLine>();

                foreach (var line in ret.LineItems)
                {
                    var receiptItem = receipt.Items.FirstOrDefault(ri => ri.FinancePurchaseOrderItemId == line.OriginalFinancePurchaseOrderItemId!.Value);
                    var poItem = po.Items.FirstOrDefault(pi => pi.Id == line.OriginalFinancePurchaseOrderItemId!.Value);

                    if (poItem != null && receiptItem != null)
                    {
                        // 1. Double-return validation again
                        var alreadyReturnedList = await _unitOfWork.Repository<SupplierReturnLineItem>()
                            .GetQueryable(rl => rl.SupplierReturn.OriginalFinancePurchaseOrderReceiptId == ret.OriginalFinancePurchaseOrderReceiptId.Value && rl.OriginalFinancePurchaseOrderItemId == line.OriginalFinancePurchaseOrderItemId.Value && rl.SupplierReturnId != ret.Id && rl.SupplierReturn.Status == SupplierReturnStatus.Approved)
                            .Select(rl => rl.QuantityReturned)
                            .ToListAsync();
                        var alreadyReturned = alreadyReturnedList.Sum();

                        var remaining = receiptItem.QuantityReceived - alreadyReturned;
                        if (line.QuantityReturned > remaining)
                            throw new InvalidOperationException($"Cannot approve return. Double-return detected for PO Item '{line.Description}'; only {remaining} units remain.");

                        // 2. Reduce received quantities
                        poItem.ReceivedQuantity -= line.QuantityReturned;
                        if (poItem.ReceivedQuantity < 0) poItem.ReceivedQuantity = 0;

                        await _unitOfWork.Repository<FinancePurchaseOrderItem>().UpdateAsync(poItem);

                        // 3. For inventory line items, build list for inventory boundary
                        if (poItem.LineType == FinancePurchaseOrderLineType.Inventory)
                        {
                            inventoryLines.Add(new FinanceReceiptInventoryLine
                            {
                                InventoryItemId = poItem.InventoryItemId ?? Guid.Empty,
                                WarehouseId = poItem.WarehouseId ?? Guid.Empty,
                                QuantityReceived = line.QuantityReturned,
                                Reference = ret.ReturnNumber
                            });
                        }
                    }
                }

                // Re-evaluate PO status
                bool anyReceived = po.Items.Any(i => i.ReceivedQuantity > 0);
                bool allFullyReceived = po.Items.All(i => i.ReceivedQuantity >= i.OrderedQuantity);

                if (allFullyReceived)
                {
                    po.Status = FinancePurchaseOrderStatus.Received;
                }
                else if (anyReceived)
                {
                    po.Status = FinancePurchaseOrderStatus.PartiallyReceived;
                }
                else
                {
                    po.Status = FinancePurchaseOrderStatus.Approved;
                }

                await _unitOfWork.Repository<FinancePurchaseOrder>().UpdateAsync(po);
                await _unitOfWork.SaveChangesAsync();

                // Call IInventoryReturnService
                if (inventoryLines.Any())
                {
                    await _inventoryReturnService.ProcessSupplierReturnAsync(TenantId, ret.Id, inventoryLines);
                }
            }

            await _unitOfWork.Repository<SupplierReturn>().UpdateAsync(ret);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Approved Supplier Return {ReturnNumber}", ret.ReturnNumber);
            return ret;
        }

        private async Task<string> GenerateReturnNumberAsync(CancellationToken cancellationToken)
        {
            var prefix = "SR";
            var currentYear = DateTime.UtcNow.Year;

            var lastReturn = await _unitOfWork.Repository<SupplierReturn>()
                .GetQueryable(r =>
                    r.TenantId == TenantId &&
                    r.ReturnNumber.StartsWith($"{prefix}-{currentYear}"))
                .OrderByDescending(r => r.ReturnNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastReturn == null)
                return $"{prefix}-{currentYear}-00001";

            var parts = lastReturn.ReturnNumber.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var lastSeq))
                return $"{prefix}-{currentYear}-{(lastSeq + 1):00000}";

            return $"{prefix}-{currentYear}-{Guid.NewGuid().ToString().Substring(0, 5)}";
        }

        private async Task<string> GenerateDebitNoteNumberAsync(CancellationToken cancellationToken)
        {
            var prefix = "SDN";
            var currentYear = DateTime.UtcNow.Year;

            var lastDbn = await _unitOfWork.Repository<SupplierDebitNote>()
                .GetQueryable(d =>
                    d.TenantId == TenantId &&
                    d.DebitNoteNumber.StartsWith($"{prefix}-{currentYear}"))
                .OrderByDescending(d => d.DebitNoteNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastDbn == null)
                return $"{prefix}-{currentYear}-00001";

            var parts = lastDbn.DebitNoteNumber.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var lastSeq))
                return $"{prefix}-{currentYear}-{(lastSeq + 1):00000}";

            return $"{prefix}-{currentYear}-{Guid.NewGuid().ToString().Substring(0, 5)}";
        }
    }
}
