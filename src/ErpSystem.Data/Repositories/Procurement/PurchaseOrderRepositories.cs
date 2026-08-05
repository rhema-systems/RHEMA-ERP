using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

#region Purchase Order Repository Implementations

public class PurchaseOrderRepository : GenericRepository<PurchaseOrder>, IPurchaseOrderRepository
{
    private readonly IProcurementSettingsRepository _settingsRepository;

    public PurchaseOrderRepository(ApplicationDbContext context, IProcurementSettingsRepository settingsRepository) : base(context)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task<IEnumerable<PurchaseOrder>> GetActiveOrdersAsync()
    {
        return await _dbSet
            .Where(po => po.Status != "Cancelled" && !po.IsDeleted)
            .Include(po => po.BusinessPartner)
            .Include(po => po.Items)
            .OrderByDescending(po => po.OrderDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrder>> GetOrdersByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return new List<PurchaseOrder>();
        }

        return await _dbSet
            .Where(po => po.Status == status && !po.IsDeleted)
            .Include(po => po.BusinessPartner)
            .Include(po => po.Items)
            .OrderByDescending(po => po.OrderDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrder>> GetOrdersBySupplierAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
        {
            return new List<PurchaseOrder>();
        }

        return await _dbSet
            .Where(po => po.BusinessPartnerId == supplierId && !po.IsDeleted)
            .Include(po => po.BusinessPartner)
            .Include(po => po.Items)
            .OrderByDescending(po => po.OrderDate)
            .ToListAsync();
    }

    public async Task<PurchaseOrder?> GetByOrderNumberAsync(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return null;
        }

        return await _dbSet
            .Where(po => po.OrderNumber == orderNumber && !po.IsDeleted)
            .Include(po => po.BusinessPartner)
                .ThenInclude(s => s.Contacts)
            .Include(po => po.Items)
            .Include(po => po.RequestedBy)
            .Include(po => po.ApprovedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<PurchaseOrder?> GetWithItemsAsync(Guid purchaseOrderId)
    {
        if (purchaseOrderId == Guid.Empty)
        {
            return null;
        }

        return await _dbSet
            .Where(po => po.Id == purchaseOrderId && !po.IsDeleted)
            .Include(po => po.BusinessPartner)
                .ThenInclude(s => s.Contacts)
            .Include(po => po.Items)
            .Include(po => po.Receipts)
                .ThenInclude(r => r.Items)
            .Include(po => po.RequestedBy)
            .Include(po => po.ApprovedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PurchaseOrder>> GetOrdersDueAsync(int daysAhead = 7)
    {
        var targetDate = DateTime.UtcNow.Date.AddDays(daysAhead);
        return await _dbSet
            .Where(po => !po.IsDeleted &&
                        (po.Status == "Sent" || po.Status == "Acknowledged") &&
                        po.PromisedDate <= targetDate)
            .Include(po => po.BusinessPartner)
            .Include(po => po.Items)
            .OrderBy(po => po.PromisedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrder>> GetOverdueOrdersAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _dbSet
            .Where(po => !po.IsDeleted &&
                        (po.Status == "Sent" || po.Status == "Acknowledged") &&
                        po.PromisedDate < today)
            .Include(po => po.BusinessPartner)
            .Include(po => po.Items)
            .OrderBy(po => po.PromisedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrder>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
        {
            return new List<PurchaseOrder>();
        }

        return await _dbSet
            .Where(po => !po.IsDeleted &&
                        po.OrderDate >= startDate &&
                        po.OrderDate <= endDate)
            .Include(po => po.BusinessPartner)
            .Include(po => po.Items)
            .OrderByDescending(po => po.OrderDate)
            .ToListAsync();
    }

    public async Task<string> GenerateOrderNumberAsync()
    {
        // Get the format from settings
        var settings = await _context.Set<ProcurementSettings>()
            .FirstOrDefaultAsync(s => !s.IsDeleted);
        
        var format = settings?.PurchaseOrderNumberFormat ?? "PO-{YYYY}-{####}";
        
        return await GenerateNumberFromFormatAsync(format, "PO");
    }

    private async Task<string> GenerateNumberFromFormatAsync(string format, string prefix)
    {
        var now = DateTime.UtcNow;
        
        // Replace date placeholders
        var formattedNumber = format
            .Replace("{YYYY}", now.Year.ToString())
            .Replace("{YY}", now.Year.ToString().Substring(2))
            .Replace("{MM}", now.Month.ToString("D2"))
            .Replace("{DD}", now.Day.ToString("D2"));
        
        // Find sequence placeholder pattern
        var sequenceMatch = System.Text.RegularExpressions.Regex.Match(format, @"\{(#+)\}");
        if (sequenceMatch.Success)
        {
            var sequenceLength = sequenceMatch.Groups[1].Value.Length;
            
            // Extract the prefix pattern (everything before the sequence placeholder)
            var prefixPattern = format.Substring(0, sequenceMatch.Index)
                .Replace("{YYYY}", now.Year.ToString())
                .Replace("{YY}", now.Year.ToString().Substring(2))
                .Replace("{MM}", now.Month.ToString("D2"))
                .Replace("{DD}", now.Day.ToString("D2"));
            
            // Get all orders matching the prefix pattern
            var existingNumbers = await _dbSet
                .Where(po => po.OrderNumber.StartsWith(prefixPattern) && !po.IsDeleted)
                .Select(po => po.OrderNumber)
                .ToListAsync();
            
            int nextSequence = 1;
            if (existingNumbers.Any())
            {
                // Extract sequence numbers and find max
                var maxSequence = existingNumbers
                    .Select(on =>
                    {
                        var sequencePart = on.Substring(prefixPattern.Length);
                        // Remove any suffix after the sequence
                        var suffixStart = format.IndexOf(sequenceMatch.Value) + sequenceMatch.Value.Length;
                        if (suffixStart < format.Length)
                        {
                            var suffix = format.Substring(suffixStart)
                                .Replace("{YYYY}", now.Year.ToString())
                                .Replace("{YY}", now.Year.ToString().Substring(2))
                                .Replace("{MM}", now.Month.ToString("D2"))
                                .Replace("{DD}", now.Day.ToString("D2"));
                            if (sequencePart.EndsWith(suffix))
                            {
                                sequencePart = sequencePart.Substring(0, sequencePart.Length - suffix.Length);
                            }
                        }
                        
                        if (int.TryParse(sequencePart, out int seq))
                        {
                            return seq;
                        }
                        return 0;
                    })
                    .Max();
                
                nextSequence = maxSequence + 1;
            }
            
            // Replace sequence placeholder with formatted number
            formattedNumber = formattedNumber.Replace(sequenceMatch.Value, nextSequence.ToString($"D{sequenceLength}"));
        }
        
        return formattedNumber;
    }

    public async Task<decimal> GetTotalOrderValueBySupplierAsync(Guid supplierId, DateTime startDate, DateTime endDate)
    {
        if (supplierId == Guid.Empty)
        {
            return 0;
        }

        return await _dbSet
            .Where(po => po.BusinessPartnerId == supplierId &&
                        !po.IsDeleted &&
                        po.OrderDate >= startDate &&
                        po.OrderDate <= endDate)
            .SumAsync(po => po.TotalAmount);
    }

    public async Task<IEnumerable<PurchaseOrder>> GetOrdersRequiringApprovalAsync()
    {
        return await _dbSet
            .Where(po => po.Status == "Draft" &&
                        !po.IsDeleted &&
                        po.TotalAmount > 1000) // Orders over $1000 need approval
            .Include(po => po.BusinessPartner)
            .Include(po => po.Items)
            .Include(po => po.RequestedBy)
            .OrderByDescending(po => po.OrderDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets purchase orders by priority (based on required date)
    /// </summary>
    public async Task<IEnumerable<PurchaseOrder>> GetOrdersByPriorityAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _dbSet
            .Where(po => !po.IsDeleted &&
                        (po.Status == "Draft" || po.Status == "Approved" || po.Status == "Sent") &&
                        po.RequiredDate.HasValue)
            .Include(po => po.BusinessPartner)
            .OrderBy(po => po.RequiredDate)
            .ThenByDescending(po => po.TotalAmount)
            .ToListAsync();
    }

    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<PurchaseOrder>> GetPurchaseOrdersAsync(int page, int pageSize, string? search = null, string? status = null, Guid? supplierId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbSet.Where(po => !po.IsDeleted)
            .Include(po => po.BusinessPartner)
            .Include(po => po.Items)
            .Include(po => po.RequestedBy)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(po => po.OrderNumber.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                                    (po.BusinessPartner != null && po.BusinessPartner.PartnerName.Contains(search, StringComparison.CurrentCultureIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(po => po.Status == status);
        }

        if (supplierId.HasValue && supplierId.Value != Guid.Empty)
        {
            query = query.Where(po => po.BusinessPartnerId == supplierId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(po => po.OrderDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(po => po.OrderDate <= endDate.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(po => po.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ErpSystem.Core.DTOs.Common.PagedResult<PurchaseOrder>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PurchaseOrder?> GetPurchaseOrderByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _dbSet
            .Where(po => po.Id == id && !po.IsDeleted)
            .Include(po => po.BusinessPartner)
                .ThenInclude(s => s.Contacts)
            .Include(po => po.Items)
                .ThenInclude(i => i.Warehouse)
            .Include(po => po.Receipts)
            .Include(po => po.RequestedBy)
            .Include(po => po.ApprovedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<PurchaseOrder> CreatePurchaseOrderAsync(PurchaseOrder purchaseOrder)
    {
        return await AddAsync(purchaseOrder);
    }

    public async Task<PurchaseOrder> UpdatePurchaseOrderAsync(PurchaseOrder purchaseOrder)
    {
        await UpdateAsync(purchaseOrder);
        return purchaseOrder;
    }

    public async Task UpdateStatusAsync(Guid purchaseOrderId, string status)
    {
        var purchaseOrder = await GetByIdAsync(purchaseOrderId);
        if (purchaseOrder != null)
        {
            var isCancelled = string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
            var wasCancelled = string.Equals(purchaseOrder.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
            if (isCancelled && !wasCancelled)
                purchaseOrder.CancelledAtUtc = DateTime.UtcNow;
            else if (!isCancelled)
                purchaseOrder.CancelledAtUtc = null;
            purchaseOrder.Status = status;
            await UpdateAsync(purchaseOrder);
        }
    }

    public async Task<IEnumerable<PurchaseOrder>> GetPurchaseOrdersBySupplierId(Guid supplierId)
    {
        return await GetOrdersBySupplierAsync(supplierId);
    }

    public async Task<IEnumerable<PurchaseOrder>> GetPurchaseOrdersByStatus(string status)
    {
        return await GetOrdersByStatusAsync(status);
    }
}

public class PurchaseOrderItemRepository : GenericRepository<PurchaseOrderItem>, IPurchaseOrderItemRepository
{
    public PurchaseOrderItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PurchaseOrderItem>> GetItemsByOrderAsync(Guid purchaseOrderId)
    {
        if (purchaseOrderId == Guid.Empty)
        {
            return new List<PurchaseOrderItem>();
        }

        return await _dbSet
            .Where(poi => poi.PurchaseOrderId == purchaseOrderId && !poi.IsDeleted)
            .Include(poi => poi.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .Include(poi => poi.InventoryItem)
            .Include(poi => poi.Warehouse)
            .OrderBy(poi => poi.ItemDescription)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrderItem>> GetPendingItemsAsync()
    {
        return await _dbSet
            .Where(poi => !poi.IsDeleted &&
                         poi.RemainingQuantity > 0 &&
                         (poi.PurchaseOrder.Status == "Sent" || poi.PurchaseOrder.Status == "Acknowledged"))
            .Include(poi => poi.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .OrderBy(poi => poi.ExpectedDeliveryDate)
            .ThenBy(poi => poi.ItemDescription)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrderItem>> GetItemsByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
        {
            return new List<PurchaseOrderItem>();
        }

        return await _dbSet
            .Where(poi => poi.InventoryItemId == inventoryItemId && !poi.IsDeleted)
            .Include(poi => poi.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .OrderByDescending(poi => poi.PurchaseOrder.OrderDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrderItem>> GetItemsBySupplierAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
        {
            return new List<PurchaseOrderItem>();
        }

        return await _dbSet
            .Where(poi => poi.PurchaseOrder.BusinessPartnerId == supplierId && !poi.IsDeleted)
            .Include(poi => poi.PurchaseOrder)
            .OrderByDescending(poi => poi.PurchaseOrder.OrderDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrderItem>> GetOverdueItemsAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _dbSet
            .Where(poi => !poi.IsDeleted &&
                         poi.RemainingQuantity > 0 &&
                         poi.ExpectedDeliveryDate < today &&
                         (poi.PurchaseOrder.Status == "Sent" || poi.PurchaseOrder.Status == "Acknowledged"))
            .Include(poi => poi.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .OrderBy(poi => poi.ExpectedDeliveryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrderItem>> GetItemsByPurchaseOrderIdAsync(Guid purchaseOrderId)
    {
        return await GetItemsByOrderAsync(purchaseOrderId);
    }

    public async Task<PurchaseOrderItem> CreateItemAsync(PurchaseOrderItem item)
    {
        return await AddAsync(item);
    }

    public async Task<PurchaseOrderItem> UpdateItemAsync(PurchaseOrderItem item)
    {
        await UpdateAsync(item);
        return item;
    }

    public async Task<PurchaseOrderItem?> GetItemByIdAsync(Guid itemId)
    {
        if (itemId == Guid.Empty)
        {
            return null;
        }

        return await _dbSet
            .Where(poi => poi.Id == itemId && !poi.IsDeleted)
            .Include(poi => poi.PurchaseOrder)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteItemAsync(Guid itemId)
    {
        if (itemId == Guid.Empty)
        {
            return;
        }

        var item = await _dbSet.FindAsync(itemId);
        if (item != null)
        {
            _dbSet.Remove(item);
            await _context.SaveChangesAsync();
        }
    }
}

public class PurchaseOrderReceiptRepository : GenericRepository<PurchaseOrderReceipt>, IPurchaseOrderReceiptRepository
{
    public PurchaseOrderReceiptRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PurchaseOrderReceipt>> GetReceiptsByOrderAsync(Guid purchaseOrderId)
    {
        if (purchaseOrderId == Guid.Empty)
        {
            return new List<PurchaseOrderReceipt>();
        }

        return await _dbSet
            .Where(por => por.PurchaseOrderId == purchaseOrderId && !por.IsDeleted)
            .Include(por => por.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .Include(por => por.Items)
            .Include(por => por.ReceivedBy)
            .Include(por => por.InspectedBy)
            .OrderByDescending(por => por.ReceiptDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrderReceipt>> GetReceiptsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
        {
            return new List<PurchaseOrderReceipt>();
        }

        return await _dbSet
            .Where(por => !por.IsDeleted &&
                         por.ReceiptDate >= startDate &&
                         por.ReceiptDate <= endDate)
            .Include(por => por.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .Include(por => por.ReceivedBy)
            .OrderByDescending(por => por.ReceiptDate)
            .ToListAsync();
    }

    public async Task<PurchaseOrderReceipt?> GetByReceiptNumberAsync(string receiptNumber)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            return null;
        }

        return await _dbSet
            .Where(por => por.ReceiptNumber == receiptNumber && !por.IsDeleted)
            .Include(por => por.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .Include(por => por.Items)
            .Include(por => por.ReceivedBy)
            .Include(por => por.InspectedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<PurchaseOrderReceipt?> GetReceiptWithItemsAsync(Guid receiptId)
    {
        if (receiptId == Guid.Empty)
        {
            return null;
        }

        return await _dbSet
            .Where(por => por.Id == receiptId && !por.IsDeleted)
            .Include(por => por.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .Include(por => por.Items)
                .ThenInclude(i => i.PurchaseOrderItem)
            .Include(por => por.ReceivedBy)
            .Include(por => por.InspectedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PurchaseOrderReceipt>> GetReceiptsByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return new List<PurchaseOrderReceipt>();
        }

        return await _dbSet
            .Where(por => por.Status == status && !por.IsDeleted)
            .Include(por => por.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .Include(por => por.ReceivedBy)
            .OrderByDescending(por => por.ReceiptDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrderReceipt>> GetReceiptsRequiringInspectionAsync()
    {
        return await _dbSet
            .Where(por => por.RequiresInspection &&
                         por.Status == "Received" &&
                         !por.IsDeleted)
            .Include(por => por.PurchaseOrder)
                .ThenInclude(po => po.BusinessPartner)
            .Include(por => por.Items)
            .OrderBy(por => por.ReceiptDate)
            .ToListAsync();
    }

    public async Task<string> GenerateReceiptNumberAsync()
    {
        // Get the format from settings (tenant-scoped via global query filter when enabled)
        var settings = await _context.Set<ProcurementSettings>()
            .FirstOrDefaultAsync(s => !s.IsDeleted);

        // Default mirrors the legacy format: "REC{YY}{####}" -> e.g., REC260001
        var format = settings?.PurchaseOrderReceiptNumberFormat ?? "REC{YY}{####}";

        return await GenerateReceiptNumberFromFormatAsync(format);
    }

    private async Task<string> GenerateReceiptNumberFromFormatAsync(string format)
    {
        var now = DateTime.UtcNow;

        // Replace date placeholders
        var formattedNumber = format
            .Replace("{YYYY}", now.Year.ToString())
            .Replace("{YY}", now.Year.ToString().Substring(2))
            .Replace("{MM}", now.Month.ToString("D2"))
            .Replace("{DD}", now.Day.ToString("D2"));

        // Find sequence placeholder pattern
        var sequenceMatch = System.Text.RegularExpressions.Regex.Match(format, @"\{(#+)\}");
        if (!sequenceMatch.Success)
        {
            // No sequence placeholder: treat as-is.
            return formattedNumber;
        }

        var sequenceLength = sequenceMatch.Groups[1].Value.Length;

        // Extract the prefix pattern (everything before the sequence placeholder)
        var prefixPattern = format.Substring(0, sequenceMatch.Index)
            .Replace("{YYYY}", now.Year.ToString())
            .Replace("{YY}", now.Year.ToString().Substring(2))
            .Replace("{MM}", now.Month.ToString("D2"))
            .Replace("{DD}", now.Day.ToString("D2"));

        var existingNumbers = await _dbSet
            .Where(por => por.ReceiptNumber.StartsWith(prefixPattern) && !por.IsDeleted)
            .Select(por => por.ReceiptNumber)
            .ToListAsync();

        int nextSequence = 1;
        if (existingNumbers.Any())
        {
            var maxSequence = existingNumbers
                .Select(rn =>
                {
                    var sequencePart = rn.Substring(prefixPattern.Length);

                    // Remove any suffix after the sequence
                    var suffixStart = format.IndexOf(sequenceMatch.Value) + sequenceMatch.Value.Length;
                    if (suffixStart < format.Length)
                    {
                        var suffix = format.Substring(suffixStart)
                            .Replace("{YYYY}", now.Year.ToString())
                            .Replace("{YY}", now.Year.ToString().Substring(2))
                            .Replace("{MM}", now.Month.ToString("D2"))
                            .Replace("{DD}", now.Day.ToString("D2"));

                        if (sequencePart.EndsWith(suffix))
                        {
                            sequencePart = sequencePart.Substring(0, sequencePart.Length - suffix.Length);
                        }
                    }

                    return int.TryParse(sequencePart, out var seq) ? seq : 0;
                })
                .Max();

            nextSequence = maxSequence + 1;
        }

        // Replace sequence placeholder with formatted number
        formattedNumber = formattedNumber.Replace(sequenceMatch.Value, nextSequence.ToString($"D{sequenceLength}"));

        return formattedNumber;
    }

    public async Task<IEnumerable<PurchaseOrderReceipt>> GetReceiptsByPurchaseOrderIdAsync(Guid purchaseOrderId)
    {
        return await GetReceiptsByOrderAsync(purchaseOrderId);
    }

    public async Task<PurchaseOrderReceipt> CreateReceiptAsync(PurchaseOrderReceipt receipt)
    {
        return await AddAsync(receipt);
    }

    public async Task<PurchaseOrderReceiptItem> CreateReceiptItemAsync(PurchaseOrderReceiptItem receiptItem)
    {
        var result = await _context.Set<PurchaseOrderReceiptItem>().AddAsync(receiptItem);
        return result.Entity;
    }
}

public class PurchaseOrderReceiptItemRepository : GenericRepository<PurchaseOrderReceiptItem>, IPurchaseOrderReceiptItemRepository
{
    public PurchaseOrderReceiptItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PurchaseOrderReceiptItem>> GetItemsByReceiptAsync(Guid receiptId)
    {
        if (receiptId == Guid.Empty)
        {
            return new List<PurchaseOrderReceiptItem>();
        }

        return await _dbSet
            .Where(pori => pori.ReceiptId == receiptId && !pori.IsDeleted)
            .Include(pori => pori.Receipt)
            .Include(pori => pori.PurchaseOrderItem)
            .OrderBy(pori => pori.PurchaseOrderItem.ItemDescription)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrderReceiptItem>> GetItemsByOrderItemAsync(Guid purchaseOrderItemId)
    {
        if (purchaseOrderItemId == Guid.Empty)
        {
            return new List<PurchaseOrderReceiptItem>();
        }

        return await _dbSet
            .Where(pori => pori.PurchaseOrderItemId == purchaseOrderItemId && !pori.IsDeleted)
            .Include(pori => pori.Receipt)
            .Include(pori => pori.PurchaseOrderItem)
            .OrderByDescending(pori => pori.Receipt.ReceiptDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrderReceiptItem>> GetRejectedItemsAsync()
    {
        return await _dbSet
            .Where(pori => pori.RejectedQuantity > 0 && !pori.IsDeleted)
            .Include(pori => pori.Receipt)
                .ThenInclude(r => r.PurchaseOrder)
                    .ThenInclude(po => po.BusinessPartner)
            .Include(pori => pori.PurchaseOrderItem)
            .OrderByDescending(pori => pori.Receipt.ReceiptDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseOrderReceiptItem>> GetItemsByQualityStatusAsync(string qualityStatus)
    {
        if (string.IsNullOrWhiteSpace(qualityStatus))
        {
            return new List<PurchaseOrderReceiptItem>();
        }

        return await _dbSet
            .Where(pori => pori.QualityStatus == qualityStatus && !pori.IsDeleted)
            .Include(pori => pori.Receipt)
                .ThenInclude(r => r.PurchaseOrder)
                    .ThenInclude(po => po.BusinessPartner)
            .Include(pori => pori.PurchaseOrderItem)
            .OrderByDescending(pori => pori.Receipt.ReceiptDate)
            .ToListAsync();
    }
}

#endregion
