using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Physical Count management service
/// Handles physical inventory counts, variances, and adjustments
/// </summary>
public class PhysicalCountService : IPhysicalCountService
{
    private readonly IPhysicalCountRepository _countRepository;
    private readonly IPhysicalCountItemRepository _countItemRepository;
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IWarehouseQuantityRepository _warehouseQuantityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PhysicalCountService> _logger;

    public PhysicalCountService(
        IPhysicalCountRepository countRepository,
        IPhysicalCountItemRepository countItemRepository,
        IInventoryItemRepository itemRepository,
        IWarehouseRepository warehouseRepository,
        IWarehouseQuantityRepository warehouseQuantityRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<PhysicalCountService> logger)
    {
        _countRepository = countRepository;
        _countItemRepository = countItemRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _warehouseQuantityRepository = warehouseQuantityRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    #region Query Operations

    public async Task<IEnumerable<PhysicalCountDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var counts = await _countRepository.GetByDateRangeAsync(
            fromDate ?? DateTime.UtcNow.AddMonths(-3),
            toDate ?? DateTime.UtcNow);
        return counts.Select(MapToDto);
    }

    public async Task<IEnumerable<PhysicalCountDto>> GetByWarehouseAsync(Guid warehouseId)
    {
        var counts = await _countRepository.GetByWarehouseAsync(warehouseId);
        return counts.Select(MapToDto);
    }

    public async Task<IEnumerable<PhysicalCountDto>> GetInProgressAsync()
    {
        var counts = await _countRepository.GetInProgressAsync();
        return counts.Select(MapToDto);
    }

    public async Task<PhysicalCountDetailDto?> GetByIdAsync(Guid id)
    {
        var count = await _countRepository.GetWithItemsAsync(id);
        return count != null ? MapToDetailDto(count) : null;
    }

    public async Task<PhysicalCountDetailDto?> GetByCountNumberAsync(string countNumber)
    {
        var count = await _countRepository.GetByCountNumberAsync(countNumber);
        if (count == null) return null;
        var fullCount = await _countRepository.GetWithItemsAsync(count.Id);
        return fullCount != null ? MapToDetailDto(fullCount) : null;
    }

    public async Task<IEnumerable<PhysicalCountDto>> GetFilteredAsync(PhysicalCountFilterDto filter)
    {
        var counts = await _countRepository.GetByDateRangeAsync(
            filter.FromDate ?? DateTime.UtcNow.AddMonths(-6),
            filter.ToDate ?? DateTime.UtcNow.AddDays(1));

        var query = counts.AsQueryable();

        if (filter.WarehouseId.HasValue)
            query = query.Where(c => c.WarehouseId == filter.WarehouseId.Value);

        if (!string.IsNullOrEmpty(filter.Status))
            query = query.Where(c => c.Status == filter.Status);

        if (!string.IsNullOrEmpty(filter.CountType))
            query = query.Where(c => c.CountType.ToString() == filter.CountType);

        if (!string.IsNullOrEmpty(filter.CountNumber))
            query = query.Where(c => c.CountNumber.Contains(filter.CountNumber, StringComparison.OrdinalIgnoreCase));

        return query
            .OrderByDescending(c => c.CountDate)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(MapToDto);
    }

    #endregion

    #region Create & Manage Counts

    public async Task<PhysicalCountDto> CreateAsync(CreatePhysicalCountDto dto, Guid userId)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId)
            ?? throw new ArgumentException($"Warehouse {dto.WarehouseId} not found");

        // Get TenantId from current user, fallback to warehouse's TenantId if available
        var tenantId = _currentUserService.TenantId
            ?? warehouse.TenantId;

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required to create a physical count");
        }

        var count = new PhysicalCount
        {
            CountNumber = await GenerateCountNumberAsync(),
            WarehouseId = dto.WarehouseId,
            CountType = dto.CountType,
            CountDate = DateTime.UtcNow,
            LocationId = dto.LocationId,
            CategoryId = dto.CategoryId,
            FreezeInventory = dto.FreezeInventory,
            Notes = dto.Notes,
            Status = "Draft",
            InitiatedById = userId,
            TenantId = tenantId
        };

        await _countRepository.AddAsync(count);
        await _unitOfWork.SaveChangesAsync();

        // Populate count items based on warehouse inventory
        await PopulateCountItemsAsync(count);

        _logger.LogInformation("Created physical count {CountNumber} for warehouse {WarehouseId}", count.CountNumber, dto.WarehouseId);
        return MapToDto(count);
    }

    public async Task<bool> StartCountAsync(Guid countId, Guid userId)
    {
        var count = await _countRepository.GetByIdAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        if (count.Status != "Draft")
            throw new InvalidOperationException("Count must be in Draft status to start");

        count.Status = "InProgress";
        count.StartedDate = DateTime.UtcNow;
        count.CountedById = userId;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Started physical count {CountNumber}", count.CountNumber);
        return true;
    }

    public async Task<bool> RecordCountItemAsync(RecordCountItemDto dto, Guid userId)
    {
        var item = await _countItemRepository.GetByIdAsync(dto.PhysicalCountItemId)
            ?? throw new ArgumentException($"Physical count item {dto.PhysicalCountItemId} not found");

        item.CountedQuantity = dto.CountedQuantity;
        item.VarianceQuantity = dto.CountedQuantity - item.SystemQuantity;
        item.VarianceValue = item.VarianceQuantity * item.UnitCost;
        item.IsCounted = true;
        item.CountedAt = DateTime.UtcNow;
        item.CountedById = userId;
        item.LotNumber = dto.LotNumber;
        item.SerialNumber = dto.SerialNumber;
        item.Notes = dto.Notes;
        item.CountAttempts++;

        await _countItemRepository.UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync();

        // Update count summary
        await UpdateCountSummaryAsync(item.PhysicalCountId);

        return true;
    }

    public async Task<bool> RecordCountItemsAsync(IEnumerable<RecordCountItemDto> items, Guid userId)
    {
        Guid? countId = null;
        foreach (var dto in items)
        {
            var item = await _countItemRepository.GetByIdAsync(dto.PhysicalCountItemId);
            if (item == null) continue;

            countId ??= item.PhysicalCountId;

            item.CountedQuantity = dto.CountedQuantity;
            item.VarianceQuantity = dto.CountedQuantity - item.SystemQuantity;
            item.VarianceValue = item.VarianceQuantity * item.UnitCost;
            item.IsCounted = true;
            item.CountedAt = DateTime.UtcNow;
            item.CountedById = userId;
            item.LotNumber = dto.LotNumber;
            item.SerialNumber = dto.SerialNumber;
            item.Notes = dto.Notes;
            item.CountAttempts++;

            await _countItemRepository.UpdateAsync(item);
        }

        await _unitOfWork.SaveChangesAsync();

        if (countId.HasValue)
            await UpdateCountSummaryAsync(countId.Value);

        return true;
    }

    public async Task<bool> CompleteCountAsync(Guid countId, Guid userId)
    {
        var count = await _countRepository.GetWithItemsAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        if (count.Status != "InProgress")
            throw new InvalidOperationException("Count must be In Progress to complete");

        // Check if all items have been counted
        var uncounted = count.Items.Count(i => !i.IsCounted);
        if (uncounted > 0)
            throw new InvalidOperationException($"{uncounted} items have not been counted yet");

        count.Status = "PendingApproval";
        count.CompletedDate = DateTime.UtcNow;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Completed physical count {CountNumber}", count.CountNumber);
        return true;
    }

    public async Task<bool> ApproveVariancesAsync(Guid countId, Guid userId)
    {
        var count = await _countRepository.GetByIdAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        if (count.Status != "PendingApproval")
            throw new InvalidOperationException("Count must be Pending Approval to approve");

        count.Status = "Approved";
        count.ApprovedById = userId;
        count.ApprovedDate = DateTime.UtcNow;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Approved variances for physical count {CountNumber}", count.CountNumber);
        return true;
    }

    public async Task<bool> PostAdjustmentsAsync(Guid countId, Guid userId)
    {
        var count = await _countRepository.GetWithItemsAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        if (count.Status != "Approved")
            throw new InvalidOperationException("Count must be Approved to post adjustments");

        // Apply adjustments to warehouse quantities
        foreach (var item in count.Items.Where(i => i.VarianceQuantity != 0))
        {
            var whQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(count.WarehouseId, item.InventoryItemId);
            if (whQty != null)
            {
                whQty.CurrentStock = item.CountedQuantity;
                whQty.AvailableStock = item.CountedQuantity - whQty.AllocatedStock;
                whQty.LastMovementDate = DateTime.UtcNow;
                await _warehouseQuantityRepository.UpdateAsync(whQty);
            }
        }

        count.Status = "Posted";
        count.PostedById = userId;
        count.PostedDate = DateTime.UtcNow;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Posted adjustments for physical count {CountNumber}", count.CountNumber);
        return true;
    }

    public async Task<bool> CancelAsync(Guid countId, string reason, Guid userId)
    {
        var count = await _countRepository.GetByIdAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        if (count.Status == "Posted")
            throw new InvalidOperationException("Cannot cancel a posted count");

        count.Status = "Cancelled";
        count.Notes = $"{count.Notes}\nCancelled: {reason}";
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Cancelled physical count {CountNumber}: {Reason}", count.CountNumber, reason);
        return true;
    }

    public async Task<IEnumerable<PhysicalCountItemDto>> GetItemsWithVarianceAsync(Guid countId)
    {
        var items = await _countItemRepository.GetItemsWithVarianceAsync(countId);
        return items.Select(MapItemToDto);
    }

    public async Task<PhysicalCountDto> UpdateAsync(Guid countId, UpdatePhysicalCountDto dto, Guid userId)
    {
        var count = await _countRepository.GetByIdAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        if (count.Status != "Draft")
            throw new InvalidOperationException("Can only update counts in Draft status");

        if (dto.Notes != null) count.Notes = dto.Notes;
        if (dto.FreezeInventory.HasValue) count.FreezeInventory = dto.FreezeInventory.Value;
        if (dto.BlindCount.HasValue) count.BlindCount = dto.BlindCount.Value;
        if (dto.IncludeZeroStock.HasValue) count.IncludeZeroStock = dto.IncludeZeroStock.Value;

        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated physical count {CountNumber}", count.CountNumber);
        return MapToDto(count);
    }

    public async Task<PhysicalCountItemDto> AddCountItemAsync(Guid countId, AddCountItemDto dto, Guid userId)
    {
        var count = await _countRepository.GetByIdAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        if (count.Status != "Draft" && count.Status != "InProgress")
            throw new InvalidOperationException("Cannot add items to a completed count");

        var inventoryItem = await _itemRepository.GetByIdAsync(dto.InventoryItemId)
            ?? throw new ArgumentException($"Inventory item {dto.InventoryItemId} not found");

        // Get system quantity from warehouse
        decimal systemQty = dto.SystemQuantity ?? 0;
        if (!dto.SystemQuantity.HasValue)
        {
            var whQty = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(count.WarehouseId, dto.InventoryItemId);
            systemQty = whQty?.CurrentStock ?? 0;
        }

        var countItem = new PhysicalCountItem
        {
            PhysicalCountId = countId,
            InventoryItemId = dto.InventoryItemId,
            LocationId = dto.LocationId,
            ItemCode = inventoryItem.ItemCode,
            ItemName = inventoryItem.Name,
            UnitOfMeasure = inventoryItem.UnitOfMeasure,
            SystemQuantity = systemQty,
            UnitCost = inventoryItem.StandardCost,
            LotNumber = dto.LotNumber,
            SerialNumber = dto.SerialNumber,
            TenantId = count.TenantId
        };

        await _countItemRepository.AddAsync(countItem);
        count.TotalItems++;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Added item {ItemCode} to physical count {CountNumber}", inventoryItem.ItemCode, count.CountNumber);
        return MapItemToDto(countItem);
    }

    public async Task<bool> RemoveCountItemAsync(Guid countItemId, Guid userId)
    {
        var item = await _countItemRepository.GetByIdAsync(countItemId)
            ?? throw new ArgumentException($"Physical count item {countItemId} not found");

        var count = await _countRepository.GetByIdAsync(item.PhysicalCountId)
            ?? throw new ArgumentException($"Physical count not found");

        if (count.Status != "Draft")
            throw new InvalidOperationException("Cannot remove items from a started count");

        await _countItemRepository.DeleteAsync(item);
        count.TotalItems--;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Removed item from physical count {CountNumber}", count.CountNumber);
        return true;
    }

    public async Task<bool> RejectVariancesAsync(Guid countId, string reason, Guid userId)
    {
        var count = await _countRepository.GetByIdAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        if (count.Status != "PendingApproval")
            throw new InvalidOperationException("Count must be Pending Approval to reject");

        count.Status = "InProgress"; // Send back for recount
        count.Notes = $"{count.Notes}\nRejected: {reason}";
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Rejected variances for physical count {CountNumber}: {Reason}", count.CountNumber, reason);
        return true;
    }

    public async Task<PhysicalCountExportDto> ExportCountSheetAsync(Guid countId)
    {
        var count = await _countRepository.GetWithItemsAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        return new PhysicalCountExportDto
        {
            Id = count.Id,
            CountNumber = count.CountNumber,
            WarehouseName = count.Warehouse?.Name ?? string.Empty,
            CountType = count.CountType.ToString(),
            Status = count.Status,
            CountDateFormatted = count.CountDate.ToString("yyyy-MM-dd"),
            Items = count.Items.Select(i => new PhysicalCountItemExportDto
            {
                ItemCode = i.ItemCode ?? string.Empty,
                ItemName = i.ItemName ?? string.Empty,
                UnitOfMeasure = i.UnitOfMeasure ?? string.Empty,
                LocationName = i.Location?.LocationCode,
                SystemQuantity = i.SystemQuantity,
                CountedQuantity = i.CountedQuantity,
                VarianceQuantity = i.VarianceQuantity,
                VarianceValue = i.VarianceValue,
                LotNumber = i.LotNumber,
                SerialNumber = i.SerialNumber,
                IsCounted = i.IsCounted,
                Notes = i.Notes
            }).ToList()
        };
    }

    public async Task<ImportCountResultDto> ImportCountSheetAsync(Guid countId, IEnumerable<ImportCountItemDto> items, Guid userId)
    {
        var count = await _countRepository.GetWithItemsAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        if (count.Status != "Draft" && count.Status != "InProgress")
            throw new InvalidOperationException("Cannot import items to a completed count");

        var result = new ImportCountResultDto();
        var itemsList = items.ToList();
        result.TotalRows = itemsList.Count;

        foreach (var importItem in itemsList)
        {
            var countItem = count.Items.FirstOrDefault(i =>
                i.ItemCode?.Equals(importItem.ItemCode, StringComparison.OrdinalIgnoreCase) == true);

            if (countItem == null)
            {
                result.ErrorCount++;
                result.Errors.Add($"Item code '{importItem.ItemCode}' not found in count sheet");
                continue;
            }

            countItem.CountedQuantity = importItem.CountedQuantity;
            countItem.VarianceQuantity = importItem.CountedQuantity - countItem.SystemQuantity;
            countItem.VarianceValue = countItem.VarianceQuantity * countItem.UnitCost;
            countItem.IsCounted = true;
            countItem.CountedAt = DateTime.UtcNow;
            countItem.CountedById = userId;
            countItem.LotNumber = importItem.LotNumber;
            countItem.SerialNumber = importItem.SerialNumber;
            countItem.Notes = importItem.Notes;
            countItem.CountAttempts++;

            await _countItemRepository.UpdateAsync(countItem);
            result.SuccessCount++;
        }

        await _unitOfWork.SaveChangesAsync();
        await UpdateCountSummaryAsync(countId);

        _logger.LogInformation("Imported {SuccessCount} items for physical count {CountNumber}", result.SuccessCount, count.CountNumber);
        return result;
    }

    public async Task<VarianceReportDto> GetVarianceReportAsync(Guid countId)
    {
        var count = await _countRepository.GetWithItemsAsync(countId)
            ?? throw new ArgumentException($"Physical count {countId} not found");

        var varianceItems = count.Items.Where(i => i.IsCounted && i.VarianceQuantity != 0).ToList();

        return new VarianceReportDto
        {
            CountNumber = count.CountNumber,
            WarehouseName = count.Warehouse?.Name ?? string.Empty,
            CountType = count.CountType.ToString(),
            CountDateFormatted = count.CountDate.ToString("yyyy-MM-dd"),
            TotalItems = count.TotalItems,
            ItemsWithVariance = varianceItems.Count,
            TotalSystemValue = count.Items.Sum(i => i.SystemQuantity * i.UnitCost),
            TotalCountedValue = count.Items.Where(i => i.IsCounted).Sum(i => i.CountedQuantity * i.UnitCost),
            TotalVarianceValue = varianceItems.Sum(i => i.VarianceValue),
            VariancePercentage = count.TotalSystemQuantity != 0
                ? (count.TotalVarianceQuantity / count.TotalSystemQuantity) * 100
                : 0,
            Items = varianceItems.Select(i => new VarianceItemDto
            {
                ItemCode = i.ItemCode ?? string.Empty,
                ItemName = i.ItemName ?? string.Empty,
                LocationName = i.Location?.LocationCode,
                SystemQuantity = i.SystemQuantity,
                CountedQuantity = i.CountedQuantity,
                VarianceQuantity = i.VarianceQuantity,
                UnitCost = i.UnitCost,
                VarianceValue = i.VarianceValue,
                VariancePercent = i.SystemQuantity != 0
                    ? (i.VarianceQuantity / i.SystemQuantity) * 100
                    : 0,
                Notes = i.Notes
            }).OrderByDescending(i => Math.Abs(i.VarianceValue)).ToList()
        };
    }

    #endregion

    #region Private Helpers

    private async Task<string> GenerateCountNumberAsync()
    {
        var date = DateTime.UtcNow;
        var prefix = $"PC-{date:yyyyMMdd}";
        var counts = await _countRepository.GetByDateRangeAsync(date.Date, date.Date.AddDays(1));
        var sequence = counts.Count() + 1;
        return $"{prefix}-{sequence:D4}";
    }

    private async Task PopulateCountItemsAsync(PhysicalCount count)
    {
        var warehouseQtys = await _warehouseQuantityRepository.GetByWarehouseAsync(count.WarehouseId);
        var addedCount = 0;

        foreach (var whQty in warehouseQtys)
        {
            var item = await _itemRepository.GetByIdAsync(whQty.InventoryItemId);
            if (item == null) continue;

            // Apply category filter if specified
            if (count.CategoryId.HasValue && item.CategoryId != count.CategoryId) continue;

            var countItem = new PhysicalCountItem
            {
                PhysicalCountId = count.Id,
                InventoryItemId = whQty.InventoryItemId,
                LocationId = count.LocationId, // Use the count's location if specified
                ItemCode = item.ItemCode,
                ItemName = item.Name,
                UnitOfMeasure = item.UnitOfMeasure,
                SystemQuantity = whQty.CurrentStock,
                UnitCost = item.StandardCost,
                TenantId = count.TenantId // Inherit TenantId from parent count
            };

            await _countItemRepository.AddAsync(countItem);
            addedCount++;
        }

        count.TotalItems = addedCount;
        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task UpdateCountSummaryAsync(Guid countId)
    {
        var count = await _countRepository.GetWithItemsAsync(countId);
        if (count == null) return;

        count.CountedItems = count.Items.Count(i => i.IsCounted);
        count.VarianceItems = count.Items.Count(i => i.IsCounted && i.VarianceQuantity != 0);
        count.TotalSystemQuantity = count.Items.Sum(i => i.SystemQuantity);
        count.TotalCountedQuantity = count.Items.Where(i => i.IsCounted).Sum(i => i.CountedQuantity);
        count.TotalVarianceQuantity = count.Items.Where(i => i.IsCounted).Sum(i => i.VarianceQuantity);
        count.TotalVarianceValue = count.Items.Where(i => i.IsCounted).Sum(i => i.VarianceValue);

        await _countRepository.UpdateAsync(count);
        await _unitOfWork.SaveChangesAsync();
    }

    #endregion

    #region Mapping

    private static PhysicalCountDto MapToDto(PhysicalCount count)
    {
        return new PhysicalCountDto
        {
            Id = count.Id,
            CountNumber = count.CountNumber,
            WarehouseId = count.WarehouseId,
            WarehouseName = count.Warehouse?.Name ?? string.Empty,
            CountType = count.CountType,
            Status = count.Status,
            CountDate = count.CountDate,
            StartedDate = count.StartedDate,
            CompletedDate = count.CompletedDate,
            LocationId = count.LocationId,
            LocationName = count.Location?.LocationCode,
            CategoryId = count.CategoryId,
            FreezeInventory = count.FreezeInventory,
            TotalItems = count.TotalItems,
            CountedItems = count.CountedItems,
            ItemsWithVariance = count.VarianceItems,
            TotalVarianceValue = count.TotalVarianceValue,
            InitiatedByName = count.InitiatedBy?.FullName,
            Notes = count.Notes,
            CreatedAtFormatted = count.CreatedAt.ToString("yyyy-MM-dd HH:mm")
        };
    }

    private static PhysicalCountDetailDto MapToDetailDto(PhysicalCount count)
    {
        return new PhysicalCountDetailDto
        {
            Id = count.Id,
            CountNumber = count.CountNumber,
            WarehouseId = count.WarehouseId,
            WarehouseName = count.Warehouse?.Name ?? string.Empty,
            CountType = count.CountType,
            Status = count.Status,
            CountDate = count.CountDate,
            StartedDate = count.StartedDate,
            CompletedDate = count.CompletedDate,
            LocationId = count.LocationId,
            LocationName = count.Location?.LocationCode,
            CategoryId = count.CategoryId,
            FreezeInventory = count.FreezeInventory,
            TotalItems = count.TotalItems,
            CountedItems = count.CountedItems,
            ItemsWithVariance = count.VarianceItems,
            TotalVarianceValue = count.TotalVarianceValue,
            InitiatedByName = count.InitiatedBy?.FullName,
            Notes = count.Notes,
            CreatedAtFormatted = count.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            ApprovedByName = count.ApprovedBy?.FullName,
            ApprovedDate = count.ApprovedDate,
            Items = count.Items.Select(MapItemToDto).ToList()
        };
    }

    private static PhysicalCountItemDto MapItemToDto(PhysicalCountItem item)
    {
        return new PhysicalCountItemDto
        {
            Id = item.Id,
            InventoryItemId = item.InventoryItemId,
            ItemCode = item.ItemCode ?? string.Empty,
            ItemName = item.ItemName ?? string.Empty,
            LocationId = item.LocationId,
            LocationName = item.Location?.LocationCode,
            SystemQuantity = item.SystemQuantity,
            CountedQuantity = item.CountedQuantity,
            VarianceQuantity = item.VarianceQuantity,
            VarianceValue = item.VarianceValue,
            VariancePercent = item.SystemQuantity != 0 
                ? (item.VarianceQuantity / item.SystemQuantity) * 100 
                : 0,
            UnitOfMeasure = item.UnitOfMeasure ?? string.Empty,
            LotNumber = item.LotNumber,
            SerialNumber = item.SerialNumber,
            IsCounted = item.IsCounted,
            CountedAt = item.CountedAt,
            CountedByName = item.CountedBy?.FullName,
            Notes = item.Notes
        };
    }

    #endregion
}

