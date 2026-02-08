using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Item Supplier management service
/// Handles linking inventory items to suppliers with pricing and lead time information
/// </summary>
public class ItemSupplierService : IItemSupplierService
{
    private readonly IItemSupplierRepository _itemSupplierRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ItemSupplierService> _logger;

    public ItemSupplierService(
        IItemSupplierRepository itemSupplierRepository,
        IInventoryItemRepository inventoryItemRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<ItemSupplierService> logger)
    {
        _itemSupplierRepository = itemSupplierRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<IEnumerable<ItemSupplierDto>> GetByItemAsync(Guid inventoryItemId)
    {
        var suppliers = await _itemSupplierRepository.GetByItemAsync(inventoryItemId);
        var dtos = new List<ItemSupplierDto>();
        
        foreach (var supplier in suppliers)
        {
            dtos.Add(await MapToDtoAsync(supplier));
        }
        
        return dtos;
    }

    public async Task<IEnumerable<ItemSupplierDto>> GetBySupplierAsync(Guid supplierId)
    {
        var suppliers = await _itemSupplierRepository.GetBySupplierAsync(supplierId);
        var dtos = new List<ItemSupplierDto>();
        
        foreach (var supplier in suppliers)
        {
            dtos.Add(await MapToDtoAsync(supplier));
        }
        
        return dtos;
    }

    public async Task<ItemSupplierDto?> GetPreferredSupplierAsync(Guid inventoryItemId)
    {
        var supplier = await _itemSupplierRepository.GetPreferredSupplierAsync(inventoryItemId);
        return supplier == null ? null : await MapToDtoAsync(supplier);
    }

    public async Task<ItemSupplierDto> CreateAsync(CreateItemSupplierDto dto)
    {
        // Check for duplicate
        var existing = await _itemSupplierRepository.GetByItemAndSupplierAsync(dto.InventoryItemId, dto.SupplierId);
        if (existing != null)
        {
            throw new InvalidOperationException("This supplier is already linked to this inventory item.");
        }

        var entity = new ItemSupplier
        {
            InventoryItemId = dto.InventoryItemId,
            SupplierId = dto.SupplierId,
            SupplierItemCode = dto.SupplierItemCode,
            SupplierItemName = dto.SupplierItemName,
            IsPreferred = dto.IsPreferred,
            Priority = dto.Priority,
            UnitPrice = dto.UnitPrice,
            Currency = dto.Currency ?? "USD",
            MinimumOrderQuantity = dto.MinimumOrderQuantity,
            OrderMultiple = dto.OrderMultiple,
            LeadTimeDays = dto.LeadTimeDays,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId
        };

        // If this is marked as preferred, unset other preferred suppliers for this item
        if (dto.IsPreferred)
        {
            await UnsetOtherPreferredAsync(dto.InventoryItemId, null);
        }

        await _itemSupplierRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created item supplier link: Item {ItemId} -> Supplier {SupplierId}", 
            dto.InventoryItemId, dto.SupplierId);

        return await MapToDtoAsync(entity);
    }

    public async Task<ItemSupplierDto> UpdateAsync(Guid id, UpdateItemSupplierDto dto)
    {
        var entity = await _itemSupplierRepository.GetByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"Item supplier with ID {id} not found.");
        }

        entity.SupplierItemCode = dto.SupplierItemCode;
        entity.SupplierItemName = dto.SupplierItemName;
        entity.IsPreferred = dto.IsPreferred;
        entity.Priority = dto.Priority;
        entity.UnitPrice = dto.UnitPrice;
        entity.Currency = dto.Currency ?? "USD";
        entity.MinimumOrderQuantity = dto.MinimumOrderQuantity;
        entity.OrderMultiple = dto.OrderMultiple;
        entity.LeadTimeDays = dto.LeadTimeDays;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        // If this is marked as preferred, unset other preferred suppliers for this item
        if (dto.IsPreferred)
        {
            await UnsetOtherPreferredAsync(entity.InventoryItemId, id);
        }

        await _itemSupplierRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated item supplier: {ItemSupplierId}", id);

        return await MapToDtoAsync(entity);
    }

    public async Task<bool> SetAsPreferredAsync(Guid itemSupplierId)
    {
        var entity = await _itemSupplierRepository.GetByIdAsync(itemSupplierId);
        if (entity == null)
        {
            return false;
        }

        // Unset other preferred suppliers for this item
        await UnsetOtherPreferredAsync(entity.InventoryItemId, itemSupplierId);

        entity.IsPreferred = true;
        entity.UpdatedAt = DateTime.UtcNow;

        await _itemSupplierRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _itemSupplierRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return false;
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        await _itemSupplierRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted item supplier: {ItemSupplierId}", id);

        return true;
    }

    private async Task UnsetOtherPreferredAsync(Guid inventoryItemId, Guid? excludeId)
    {
        var allSuppliers = await _itemSupplierRepository.GetByItemAsync(inventoryItemId);
        foreach (var supplier in allSuppliers.Where(s => s.IsPreferred && s.Id != excludeId))
        {
            supplier.IsPreferred = false;
            supplier.UpdatedAt = DateTime.UtcNow;
            await _itemSupplierRepository.UpdateAsync(supplier);
        }
    }

    private async Task<ItemSupplierDto> MapToDtoAsync(ItemSupplier entity)
    {
        // Get item info
        var item = entity.InventoryItem ?? await _inventoryItemRepository.GetByIdAsync(entity.InventoryItemId);

        // Get supplier (BusinessPartner) info
        var supplier = await _businessPartnerRepository.GetByIdAsync(entity.SupplierId);

        return new ItemSupplierDto
        {
            Id = entity.Id,
            InventoryItemId = entity.InventoryItemId,
            ItemCode = item?.ItemCode ?? string.Empty,
            ItemName = item?.Name ?? string.Empty,
            SupplierId = entity.SupplierId,
            SupplierName = supplier?.PartnerName ?? string.Empty,
            SupplierItemCode = entity.SupplierItemCode,
            SupplierItemName = entity.SupplierItemName,
            IsPreferred = entity.IsPreferred,
            Priority = entity.Priority,
            UnitPrice = entity.UnitPrice,
            Currency = entity.Currency,
            MinimumOrderQuantity = entity.MinimumOrderQuantity,
            OrderMultiple = entity.OrderMultiple,
            LeadTimeDays = entity.LeadTimeDays,
            LastPurchaseDate = null, // TODO: Get from PO history if needed
            LastPurchasePrice = null,
            IsActive = entity.IsActive
        };
    }
}

