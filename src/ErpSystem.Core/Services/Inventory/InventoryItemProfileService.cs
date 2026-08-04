using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Authoritative TDC item-master profile validator. Transaction-derived stock,
/// weighted-average cost and last-purchase cost are deliberately outside this boundary.
/// </summary>
public sealed class InventoryItemProfileService : IInventoryItemProfileService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryItemIdentifierService _identifiers;

    public InventoryItemProfileService(
        IUnitOfWork unitOfWork,
        IInventoryItemIdentifierService identifiers)
    {
        _unitOfWork = unitOfWork;
        _identifiers = identifiers;
    }

    public async Task NormalizeAndValidateAsync(
        InventoryItem item,
        Guid? existingItemId,
        CancellationToken cancellationToken = default)
    {
        if (item.TenantId == Guid.Empty)
            throw Invalid("ITEM_TENANT_REQUIRED", "A tenant-scoped item profile is required.");

        item.ItemCode = Required(item.ItemCode, 100, "ITEM_CODE_REQUIRED", "Stock code").ToUpperInvariant();
        item.Name = Required(item.Name, 200, "ITEM_NAME_REQUIRED", "Description/name");
        item.Description = Optional(item.Description, 1000);
        item.UnitOfMeasure = Required(item.UnitOfMeasure, 20, "ITEM_UOM_REQUIRED", "Unit of measure").ToUpperInvariant();
        item.Barcode = _identifiers.Normalize(item.Barcode);
        item.AlternateBarcode = _identifiers.Normalize(item.AlternateBarcode);
        item.QRCode = _identifiers.Normalize(item.QRCode);

        if (!Enum.IsDefined(item.ItemType))
            throw Invalid("ITEM_TYPE_INVALID", "Item type is invalid.");
        if (!Enum.IsDefined(item.Status))
            throw Invalid("ITEM_STATUS_INVALID", "Item status is invalid.");
        if (!Enum.IsDefined(item.ValuationMethod))
            throw Invalid("ITEM_VALUATION_METHOD_INVALID", "Valuation method is invalid.");

        NonNegative(item.MinimumLevel, "ITEM_MINIMUM_LEVEL_INVALID", "Minimum level");
        NonNegative(item.MaximumLevel, "ITEM_MAXIMUM_LEVEL_INVALID", "Maximum level");
        NonNegative(item.ReorderLevel, "ITEM_REORDER_LEVEL_INVALID", "Reorder level");
        NonNegative(item.ReorderQuantity, "ITEM_REORDER_QUANTITY_INVALID", "Reorder quantity");
        NonNegative(item.SafetyStock, "ITEM_SAFETY_STOCK_INVALID", "Safety stock");
        NonNegative(item.StandardCost, "ITEM_STANDARD_COST_INVALID", "Standard cost");
        NonNegative(item.SalePrice, "ITEM_SALE_PRICE_INVALID", "Sale price");

        if (item.MaximumLevel > 0 && item.MinimumLevel > item.MaximumLevel)
            throw Invalid("ITEM_STOCK_LEVEL_RANGE_INVALID", "Minimum level cannot exceed maximum level.");
        if (item.MaximumLevel > 0 && item.ReorderLevel > item.MaximumLevel)
            throw Invalid("ITEM_REORDER_LEVEL_RANGE_INVALID", "Reorder level cannot exceed maximum level.");
        if (item.LeadTimeDays < 0 || item.SafetyLeadTimeDays < 0)
            throw Invalid("ITEM_LEAD_TIME_INVALID", "Lead-time values cannot be negative.");
        if (item.IsExpirationTracked && (!item.ShelfLifeDays.HasValue || item.ShelfLifeDays.Value < 1))
            throw Invalid("ITEM_SHELF_LIFE_REQUIRED", "Expiration-tracked items require a positive shelf life.");
        if (item.ValuationMethod == ValuationMethod.StandardCost && item.StandardCost <= 0)
            throw Invalid("ITEM_STANDARD_COST_REQUIRED", "Standard-cost items require a positive standard cost.");

        if (existingItemId.HasValue)
        {
            var priorValuation = await _unitOfWork.Repository<InventoryItem>()
                .GetQueryable(value => value.TenantId == item.TenantId && value.Id == existingItemId.Value && !value.IsDeleted)
                .AsNoTracking()
                .Select(value => new { value.IsValuationLocked, value.ValuationMethod })
                .SingleOrDefaultAsync(cancellationToken);
            if (priorValuation?.IsValuationLocked == true && priorValuation.ValuationMethod != item.ValuationMethod)
                throw Invalid("ITEM_VALUATION_LOCKED", "Valuation method cannot change after inventory transactions have locked the item.");
        }

        var categoryExists = await _unitOfWork.Repository<InventoryCategory>()
            .GetQueryable(value => value.TenantId == item.TenantId && value.Id == item.CategoryId && !value.IsDeleted && value.IsActive)
            .AnyAsync(cancellationToken);
        if (!categoryExists)
            throw Invalid("ITEM_CATEGORY_NOT_FOUND", "Category must identify an active current-tenant inventory category.");

        var uomExists = await _unitOfWork.Repository<UnitOfMeasure>()
            .GetQueryable(value => value.TenantId == item.TenantId && !value.IsDeleted && value.IsActive && value.Code == item.UnitOfMeasure)
            .AnyAsync(cancellationToken);
        if (!uomExists)
            throw Invalid("ITEM_UOM_NOT_FOUND", "Unit of measure must identify an active current-tenant unit.");

        if (item.UnitOfMeasureScheduleId.HasValue)
        {
            var scheduleExists = await _unitOfWork.Repository<UnitOfMeasureSchedule>()
                .GetQueryable(value => value.TenantId == item.TenantId && value.Id == item.UnitOfMeasureScheduleId.Value && !value.IsDeleted && value.IsActive)
                .AnyAsync(cancellationToken);
            if (!scheduleExists)
                throw Invalid("ITEM_UOM_SCHEDULE_NOT_FOUND", "Unit-of-measure schedule must identify an active current-tenant schedule.");
        }

        var duplicateCode = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(value => value.TenantId == item.TenantId && !value.IsDeleted && value.ItemCode == item.ItemCode &&
                (!existingItemId.HasValue || value.Id != existingItemId.Value))
            .AnyAsync(cancellationToken);
        if (duplicateCode)
            throw Invalid("ITEM_CODE_DUPLICATE", $"Stock code '{item.ItemCode}' already exists in the current tenant.");

        await _identifiers.ValidateItemIdentifiersAsync(
            item.TenantId,
            existingItemId,
            item.Barcode,
            item.AlternateBarcode,
            item.QRCode,
            cancellationToken);
    }

    private static string Required(string? value, int maxLength, string code, string label)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw Invalid(code, $"{label} is required.");
        if (normalized.Length > maxLength)
            throw Invalid(code, $"{label} cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static string? Optional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maxLength)
            throw Invalid("ITEM_DESCRIPTION_INVALID", $"Description cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static void NonNegative(decimal value, string code, string label)
    {
        if (value < 0) throw Invalid(code, $"{label} cannot be negative.");
    }

    private static InventoryItemProfileValidationException Invalid(string code, string message) => new(code, message);
}
