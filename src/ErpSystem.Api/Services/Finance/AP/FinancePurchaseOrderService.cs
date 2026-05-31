using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Finance.AP;

public class FinancePurchaseOrderService : IFinancePurchaseOrderService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITaxCalculationEngine _taxEngine;
    private readonly ITenantSettingsService _tenantSettingsService;
    private readonly ICurrentUserService _currentUser;

    public FinancePurchaseOrderService(ApplicationDbContext dbContext, ITaxCalculationEngine taxEngine, ITenantSettingsService tenantSettingsService, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _taxEngine = taxEngine;
        _tenantSettingsService = tenantSettingsService;
        _currentUser = currentUser;
    }

    public FinancePurchaseOrderService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        _taxEngine = null!;
        _tenantSettingsService = null!;
        _currentUser = null!;
    }

    private Guid TenantId => _currentUser?.TenantId ?? Guid.Empty;

    public async Task<FinancePurchaseOrder> CreateAsync(CreateFinancePurchaseOrderDto dto)
    {
        var purchaseOrder = new FinancePurchaseOrder
        {
            TenantId = TenantId,
            OrderNumber = dto.OrderNumber,
            VendorId = dto.VendorId,
            OrderDate = dto.OrderDate == default ? DateTime.UtcNow : dto.OrderDate,
            ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
            Status = FinancePurchaseOrderStatus.Draft,
            CurrencyCode = dto.CurrencyCode,
            ExchangeRate = dto.ExchangeRate,
            Remarks = dto.Remarks,
            TaxGroupId = dto.TaxGroupId,
            DiscountAmount = dto.DiscountAmount ?? 0,
            Items = dto.Items.Select(line => new FinancePurchaseOrderItem
            {
                TenantId = TenantId,
                LineType = line.LineType,
                InventoryItemId = line.InventoryItemId,
                WarehouseId = line.WarehouseId,
                GlAccountId = line.GlAccountId,
                Description = line.Description,
                OrderedQuantity = line.OrderedQuantity,
                UnitPrice = line.UnitPrice,
                CurrencyCode = line.CurrencyCode,
                ExchangeRate = line.ExchangeRate,
                TaxCode = line.TaxCode,
                TaxGroupId = line.TaxGroupId,
                TaxRate = line.TaxRate,
                TaxAmount = line.TaxAmount,
                DiscountPercentage = line.DiscountPercentage ?? 0,
                DiscountAmount = line.DiscountAmount ?? 0,
                LineTotal = line.LineTotal
            }).ToList()
        };

        purchaseOrder.OrderDate = purchaseOrder.OrderDate == default ? DateTime.UtcNow : purchaseOrder.OrderDate;

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

        var currency = (purchaseOrder.CurrencyCode ?? baseCurrency).Trim().ToUpperInvariant();
        decimal exchangeRate = purchaseOrder.ExchangeRate;

        if (currency == baseCurrency.ToUpperInvariant())
        {
            exchangeRate = 1.0m;
        }
        else
        {
            if (exchangeRate <= 0.0m)
            {
                throw new ArgumentException($"Exchange rate must be greater than zero for foreign currency '{purchaseOrder.CurrencyCode}'.");
            }
        }

        purchaseOrder.CurrencyCode = currency;
        purchaseOrder.ExchangeRate = exchangeRate;
        
        decimal total = 0;
        decimal totalDiscount = 0;

        foreach (var line in purchaseOrder.Items)
        {
            if (line.OrderedQuantity <= 0)
                throw new InvalidOperationException("Ordered quantity must be greater than 0.");
                
            if (line.UnitPrice < 0)
                throw new InvalidOperationException("Unit price cannot be negative.");

            if (line.LineType == FinancePurchaseOrderLineType.Inventory)
            {
                if (!line.InventoryItemId.HasValue)
                    throw new InvalidOperationException("Inventory line must have an InventoryItemId.");
                if (!line.WarehouseId.HasValue)
                    throw new InvalidOperationException("Inventory line must have a WarehouseId/Location.");
                if (line.GlAccountId.HasValue)
                    throw new InvalidOperationException("Inventory line cannot have a GlAccountId as the primary target.");
            }
            else if (line.LineType == FinancePurchaseOrderLineType.GLAccount)
            {
                if (!line.GlAccountId.HasValue)
                    throw new InvalidOperationException("GL Account line must have a GlAccountId.");
                if (line.InventoryItemId.HasValue)
                    throw new InvalidOperationException("GL Account line cannot have an InventoryItemId.");
            }

            var grossAmount = line.OrderedQuantity * line.UnitPrice;
            line.DiscountAmount = grossAmount * (line.DiscountPercentage / 100);
            var lineNetAmount = grossAmount - line.DiscountAmount;
            line.LineTotal = lineNetAmount;
            
            var lineTaxGroupId = line.TaxGroupId ?? purchaseOrder.TaxGroupId;
            decimal lineTax = 0;
            decimal resolvedTaxRate = line.TaxRate;

            if (lineTaxGroupId.HasValue)
            {
                var transactionType = line.LineType == FinancePurchaseOrderLineType.Inventory 
                    ? TaxTransactionType.PurchaseOfGoods 
                    : TaxTransactionType.PurchaseOfServices;

                var taxRequest = new TaxCalculationRequestDto
                {
                    TransactionType = transactionType,
                    BaseAmount = lineNetAmount,
                    TaxGroupId = lineTaxGroupId.Value,
                    BusinessPartnerId = purchaseOrder.VendorId
                };

                var taxResult = await _taxEngine.CalculateTaxesAsync(taxRequest);
                lineTax = taxResult.TotalTaxAmount;
                resolvedTaxRate = taxResult.EffectiveTaxRate;
                line.TaxCode = taxResult.TaxGroupName ?? line.TaxCode;
            }
            else if (line.TaxRate > 0)
            {
                lineTax = lineNetAmount * (line.TaxRate / 100);
            }

            line.TaxGroupId = lineTaxGroupId;
            line.TaxRate = resolvedTaxRate;
            line.TaxAmount = lineTax;
            
            total += lineNetAmount + lineTax;
            totalDiscount += line.DiscountAmount;
        }

        purchaseOrder.DiscountAmount = dto.DiscountAmount ?? totalDiscount;
        purchaseOrder.TotalAmount = total;

        _dbContext.FinancePurchaseOrders.Add(purchaseOrder);
        await _dbContext.SaveChangesAsync();

        return purchaseOrder;
    }

    public async Task<FinancePurchaseOrder> GetByIdAsync(Guid id)
    {
        var po = await _dbContext.FinancePurchaseOrders
            .Include(p => p.Vendor)
            .Include(p => p.Items)
                .ThenInclude(i => i.InventoryItem)
            .Include(p => p.Items)
                .ThenInclude(i => i.GlAccount)
            .FirstOrDefaultAsync(p => p.Id == id);
            
        if (po == null)
            throw new KeyNotFoundException($"Finance Purchase Order with ID {id} not found.");
            
        return po;
    }

    public async Task<IEnumerable<FinancePurchaseOrder>> GetAllAsync(Guid tenantId)
    {
        return await _dbContext.FinancePurchaseOrders
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Vendor)
            .OrderByDescending(p => p.OrderDate)
            .ToListAsync();
    }

    public async Task ApproveAsync(Guid id)
    {
        var po = await _dbContext.FinancePurchaseOrders.FindAsync(id);
        if (po == null)
            throw new KeyNotFoundException($"Finance Purchase Order with ID {id} not found.");

        if (po.Status != FinancePurchaseOrderStatus.Draft)
            throw new InvalidOperationException($"Cannot approve PO in status {po.Status}. Only Draft POs can be approved.");

        po.Status = FinancePurchaseOrderStatus.Approved;
        await _dbContext.SaveChangesAsync();
    }
}
