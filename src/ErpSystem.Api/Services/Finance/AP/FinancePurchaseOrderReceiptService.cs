using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Finance.AP;

public class FinancePurchaseOrderReceiptService : IFinancePurchaseOrderReceiptService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IInventoryReceiptService _inventoryReceiptService;
    private readonly ITaxCalculationEngine _taxEngine;
    private readonly ITenantSettingsService _tenantSettingsService;

    public FinancePurchaseOrderReceiptService(
        ApplicationDbContext dbContext,
        IInventoryReceiptService inventoryReceiptService,
        ITaxCalculationEngine taxEngine,
        ITenantSettingsService tenantSettingsService)
    {
        _dbContext = dbContext;
        _inventoryReceiptService = inventoryReceiptService;
        _taxEngine = taxEngine;
        _tenantSettingsService = tenantSettingsService;
    }

    public FinancePurchaseOrderReceiptService(
        ApplicationDbContext dbContext,
        IInventoryReceiptService inventoryReceiptService)
    {
        _dbContext = dbContext;
        _inventoryReceiptService = inventoryReceiptService;
        _taxEngine = null!;
        _tenantSettingsService = null!;
    }

    public async Task<FinancePurchaseOrderReceipt> ReceiveAsync(FinancePurchaseOrderReceipt receipt)
    {
        var po = await _dbContext.FinancePurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == receipt.FinancePurchaseOrderId);

        if (po == null)
            throw new KeyNotFoundException($"Purchase Order with ID {receipt.FinancePurchaseOrderId} not found.");

        if (po.Status != FinancePurchaseOrderStatus.Approved && po.Status != FinancePurchaseOrderStatus.PartiallyReceived)
            throw new InvalidOperationException($"Cannot receive against PO in status {po.Status}. Must be Approved or PartiallyReceived.");

        receipt.ReceiptDate = receipt.ReceiptDate == default ? DateTime.UtcNow : receipt.ReceiptDate;
        // receipt.Status removed because property does not exist on entity

        var inventoryLines = new List<FinanceReceiptInventoryLine>();
        bool fullyReceived = true;

        foreach (var receiptItem in receipt.Items)
        {
            var poLine = po.Items.FirstOrDefault(i => i.Id == receiptItem.FinancePurchaseOrderItemId);
            if (poLine == null)
                throw new InvalidOperationException($"PO Line {receiptItem.FinancePurchaseOrderItemId} not found.");

            if (receiptItem.QuantityReceived <= 0)
                throw new InvalidOperationException("Received quantity must be greater than 0.");

            if (poLine.ReceivedQuantity + receiptItem.QuantityReceived > poLine.OrderedQuantity)
                throw new InvalidOperationException($"Cannot over-receive. PO Line Ordered: {poLine.OrderedQuantity}, Already Received: {poLine.ReceivedQuantity}, Attempting: {receiptItem.QuantityReceived}");

            receiptItem.DiscountPercentage = poLine.DiscountPercentage;
            receiptItem.DiscountAmount = receiptItem.QuantityReceived * poLine.UnitPrice * (poLine.DiscountPercentage / 100);

            poLine.ReceivedQuantity += receiptItem.QuantityReceived;

            if (poLine.ReceivedQuantity < poLine.OrderedQuantity)
                fullyReceived = false;

            if (poLine.LineType == FinancePurchaseOrderLineType.Inventory && poLine.InventoryItemId.HasValue && poLine.WarehouseId.HasValue)
            {
                inventoryLines.Add(new FinanceReceiptInventoryLine
                {
                    InventoryItemId = poLine.InventoryItemId.Value,
                    WarehouseId = poLine.WarehouseId.Value,
                    QuantityReceived = receiptItem.QuantityReceived,
                    Reference = receipt.ReceiptNumber
                });
            }
        }

        po.Status = fullyReceived ? FinancePurchaseOrderStatus.Received : FinancePurchaseOrderStatus.PartiallyReceived;

        _dbContext.FinancePurchaseOrderReceipts.Add(receipt);
        await _dbContext.SaveChangesAsync();

        if (inventoryLines.Any())
        {
            await _inventoryReceiptService.ProcessFinanceReceiptAsync(receipt.TenantId, receipt.Id, inventoryLines);
        }

        return receipt;
    }

    public async Task<FinancePurchaseOrderReceipt> ReceiveAsync(CreateFinancePurchaseReceiptDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (dto.Lines == null || !dto.Lines.Any())
            throw new InvalidOperationException("At least one receipt line is required.");

        // Check if at least one line has QuantityReceived > 0
        if (!dto.Lines.Any(l => l.QuantityReceived > 0))
            throw new InvalidOperationException("At least one line must have a received quantity greater than 0.");

        var receipt = new FinancePurchaseOrderReceipt
        {
            FinancePurchaseOrderId = dto.FinancePurchaseOrderId,
            ReceiptNumber = dto.ReceiptNumber,
            ReceiptDate = dto.ReceiptDate == default ? DateTime.UtcNow : dto.ReceiptDate,
            Remarks = dto.Remarks,
            Items = dto.Lines
                .Where(l => l.QuantityReceived > 0)
                .Select(l => new FinancePurchaseOrderReceiptItem
                {
                    FinancePurchaseOrderItemId = l.FinancePurchaseOrderItemId,
                    QuantityReceived = l.QuantityReceived
                })
                .ToList()
        };

        return await ReceiveAsync(receipt);
    }

    public async Task<IEnumerable<FinancePurchaseOrderReceipt>> GetAllAsync()
    {
        return await _dbContext.FinancePurchaseOrderReceipts
            .Include(r => r.Items)
                .ThenInclude(i => i.FinancePurchaseOrderItem)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync();
    }

    public async Task<FinancePurchaseOrderReceipt> GetByIdAsync(Guid id)
    {
        var receipt = await _dbContext.FinancePurchaseOrderReceipts
            .Include(r => r.Items)
                .ThenInclude(i => i.FinancePurchaseOrderItem)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        if (receipt == null)
            throw new KeyNotFoundException($"Receipt with ID {id} not found.");
            
        return receipt;
    }

    public async Task<IEnumerable<FinancePurchaseOrderReceipt>> GetByPurchaseOrderIdAsync(Guid poId)
    {
        return await _dbContext.FinancePurchaseOrderReceipts
            .Where(r => r.FinancePurchaseOrderId == poId)
            .Include(r => r.Items)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync();
    }

    public async Task<VendorInvoice> ConvertToVendorInvoiceAsync(Guid receiptId, Guid currentUserId)
    {
        var receipt = await _dbContext.FinancePurchaseOrderReceipts
            .Include(r => r.FinancePurchaseOrder)
            .Include(r => r.Items)
                .ThenInclude(i => i.FinancePurchaseOrderItem)
            .FirstOrDefaultAsync(r => r.Id == receiptId);

        if (receipt == null)
            throw new KeyNotFoundException($"Receipt with ID {receiptId} not found.");

        var po = receipt.FinancePurchaseOrder;

        // Resolve base currency
        var baseCurrency = "GHS";
        try
        {
            baseCurrency = await _tenantSettingsService.GetBaseCurrencyAsync() ?? "GHS";
        }
        catch
        {
            // Default fallback
        }

        var currency = (po.CurrencyCode ?? baseCurrency).Trim().ToUpperInvariant();
        decimal exchangeRate = po.ExchangeRate;

        if (currency == baseCurrency.ToUpperInvariant())
        {
            exchangeRate = 1.0m;
        }
        else
        {
            if (exchangeRate <= 0.0m)
            {
                throw new ArgumentException($"Exchange rate must be greater than zero for foreign currency '{po.CurrencyCode}'.");
            }
        }

        var invoice = new VendorInvoice
        {
            TenantId = receipt.TenantId,
            BusinessPartnerId = po.VendorId,
            InvoiceDate = DateTime.UtcNow,
            InvoiceNumber = $"INV-{receipt.ReceiptNumber}",
            Status = VendorInvoiceStatus.Draft,
            TaxGroupId = po.TaxGroupId,
            CurrencyCode = currency,
            ExchangeRate = exchangeRate
        };

        decimal subTotal = 0;
        decimal taxTotal = 0;
        decimal discountTotal = 0;

        foreach (var receiptItem in receipt.Items)
        {
            var remainingToInvoice = receiptItem.QuantityReceived - receiptItem.InvoicedQuantity;
            if (remainingToInvoice <= 0)
                continue; // Skip lines that are fully invoiced

            var poLine = receiptItem.FinancePurchaseOrderItem;
            var lineTaxGroupId = poLine.TaxGroupId ?? po.TaxGroupId;
            decimal lineTax = 0;
            string? resolvedTaxCode = poLine.TaxCode;
            decimal resolvedTaxRate = poLine.TaxRate;

            var lineGross = remainingToInvoice * poLine.UnitPrice;
            var lineDiscount = lineGross * (poLine.DiscountPercentage / 100);
            var lineNet = lineGross - lineDiscount;

            if (lineTaxGroupId.HasValue)
            {
                var transactionType = poLine.LineType == FinancePurchaseOrderLineType.Inventory
                    ? TaxTransactionType.PurchaseOfGoods
                    : TaxTransactionType.PurchaseOfServices;

                var taxRequest = new TaxCalculationRequestDto
                {
                    TransactionType = transactionType,
                    BaseAmount = lineNet,
                    TaxGroupId = lineTaxGroupId.Value,
                    BusinessPartnerId = po.VendorId
                };

                // Authoritative backend recalculation
                var taxResult = await _taxEngine.CalculateTaxesAsync(taxRequest);
                lineTax = taxResult.TotalTaxAmount;
                resolvedTaxRate = taxResult.EffectiveTaxRate;
                resolvedTaxCode = taxResult.TaxGroupName ?? poLine.TaxCode;
            }
            else if (poLine.TaxRate > 0)
            {
                lineTax = lineNet * (poLine.TaxRate / 100);
            }

            var invoiceLine = new VendorInvoiceLineItem
            {
                TenantId = receipt.TenantId,
                Quantity = remainingToInvoice,
                UnitPrice = poLine.UnitPrice,
                Description = poLine.Description,
                TaxGroupId = lineTaxGroupId,
                TaxRate = resolvedTaxRate,
                TaxAmount = lineTax,
                TaxCode = resolvedTaxCode,
                DiscountPercentage = poLine.DiscountPercentage,
                DiscountAmount = lineDiscount,
                PurchaseOrderItemId = poLine.Id
            };

            subTotal += lineNet;
            taxTotal += invoiceLine.TaxAmount;
            discountTotal += lineDiscount;

            invoice.LineItems.Add(invoiceLine);

            // Update quantities
            receiptItem.InvoicedQuantity += remainingToInvoice;
            poLine.InvoicedQuantity += remainingToInvoice;
        }

        if (!invoice.LineItems.Any())
            throw new InvalidOperationException("No uninvoiced items remaining on this receipt.");

        invoice.SubTotal = subTotal;
        invoice.TaxAmount = taxTotal;
        invoice.DiscountAmount = discountTotal;
        invoice.TotalAmount = subTotal + taxTotal;
        invoice.BaseCurrencyAmount = invoice.TotalAmount * invoice.ExchangeRate;

        // Prevent double conversion indicator at header level if fully invoiced
        bool fullyInvoiced = receipt.Items.All(i => i.InvoicedQuantity >= i.QuantityReceived);
        if (fullyInvoiced)
        {
            // Link if the model has a property for it, else just rely on quantities.
        }

        // Update PO status
        bool poFullyInvoiced = po.Items.All(i => i.InvoicedQuantity >= i.OrderedQuantity);
        bool poPartiallyInvoiced = po.Items.Any(i => i.InvoicedQuantity > 0);
        po.Status = poFullyInvoiced ? FinancePurchaseOrderStatus.Invoiced : (poPartiallyInvoiced ? FinancePurchaseOrderStatus.PartiallyInvoiced : po.Status);

        _dbContext.VendorInvoices.Add(invoice);
        await _dbContext.SaveChangesAsync();

        return invoice;
    }
}
