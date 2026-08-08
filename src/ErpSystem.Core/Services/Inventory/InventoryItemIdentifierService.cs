using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Single tenant-scoped identifier authority shared by item maintenance,
/// imports, maker-checker application, and scanner consumers.
/// </summary>
public sealed class InventoryItemIdentifierService : IInventoryItemIdentifierService
{
    private readonly IUnitOfWork _unitOfWork;

    public InventoryItemIdentifierService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public string? Normalize(string? identifier)
    {
        var value = identifier?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value.ToUpperInvariant();
    }

    public async Task ValidateItemIdentifiersAsync(
        Guid tenantId,
        Guid? inventoryItemId,
        string? barcode,
        string? alternateBarcode,
        string? qrCode,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        var identifiers = new[] { Normalize(barcode), Normalize(alternateBarcode), Normalize(qrCode) }
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        var repeated = identifiers
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (repeated is not null)
        {
            throw Conflict(repeated.Key);
        }

        foreach (var identifier in identifiers)
        {
            var duplicateItem = await _unitOfWork.Repository<InventoryItem>()
                .GetQueryable(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    (!inventoryItemId.HasValue || item.Id != inventoryItemId.Value) &&
                    (item.Barcode == identifier || item.AlternateBarcode == identifier || item.QRCode == identifier))
                .AnyAsync(cancellationToken);

            var duplicateUnit = await _unitOfWork.Repository<ItemUnitOfMeasure>()
                .GetQueryable(unit =>
                    unit.TenantId == tenantId &&
                    !unit.IsDeleted &&
                    unit.Barcode == identifier)
                .AnyAsync(cancellationToken);

            if (duplicateItem || duplicateUnit)
            {
                throw Conflict(identifier);
            }
        }
    }

    public async Task ValidateUnitIdentifierAsync(
        Guid tenantId,
        Guid inventoryItemId,
        Guid? itemUnitOfMeasureId,
        string? barcode,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        if (inventoryItemId == Guid.Empty)
        {
            throw new ArgumentException("Inventory item is required.", nameof(inventoryItemId));
        }

        var identifier = Normalize(barcode);
        if (identifier is null)
        {
            return;
        }

        var duplicateItem = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(item =>
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                (item.Barcode == identifier || item.AlternateBarcode == identifier || item.QRCode == identifier))
            .AnyAsync(cancellationToken);

        var duplicateUnit = await _unitOfWork.Repository<ItemUnitOfMeasure>()
            .GetQueryable(unit =>
                unit.TenantId == tenantId &&
                !unit.IsDeleted &&
                (!itemUnitOfMeasureId.HasValue || unit.Id != itemUnitOfMeasureId.Value) &&
                unit.Barcode == identifier)
            .AnyAsync(cancellationToken);

        if (duplicateItem || duplicateUnit)
        {
            throw Conflict(identifier);
        }
    }

    public async Task<InventoryIdentifierMatchDto?> ResolveAsync(
        Guid tenantId,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId);
        var normalized = Normalize(identifier)
            ?? throw new ArgumentException("Identifier is required.", nameof(identifier));

        var item = await _unitOfWork.Repository<InventoryItem>()
            .GetQueryable(value =>
                value.TenantId == tenantId &&
                !value.IsDeleted &&
                (value.Barcode == normalized || value.AlternateBarcode == normalized || value.QRCode == normalized))
            .AsNoTracking()
            .Select(value => new
            {
                value.Id,
                value.ItemCode,
                value.Name,
                value.Barcode,
                value.AlternateBarcode,
                value.QRCode
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (item is not null)
        {
            var kind = item.Barcode == normalized
                ? "PrimaryBarcode"
                : item.AlternateBarcode == normalized
                    ? "AlternateBarcode"
                    : "QRCode";
            return new InventoryIdentifierMatchDto
            {
                InventoryItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.Name,
                Identifier = normalized,
                IdentifierKind = kind
            };
        }

        return await _unitOfWork.Repository<ItemUnitOfMeasure>()
            .GetQueryable(value =>
                value.TenantId == tenantId &&
                !value.IsDeleted &&
                value.IsActive &&
                value.Barcode == normalized &&
                !value.InventoryItem.IsDeleted)
            .AsNoTracking()
            .Select(value => new InventoryIdentifierMatchDto
            {
                InventoryItemId = value.InventoryItemId,
                ItemCode = value.InventoryItem.ItemCode,
                ItemName = value.InventoryItem.Name,
                Identifier = normalized,
                IdentifierKind = "UnitBarcode",
                ItemUnitOfMeasureId = value.Id,
                UnitOfMeasureId = value.UnitOfMeasureId,
                UnitCode = value.UnitOfMeasure.Code,
                ConversionToBase = value.ConversionToBase
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static InventoryIdentifierConflictException Conflict(string identifier) =>
        new(identifier, $"Identifier '{identifier}' is already assigned in this tenant. Primary, alternate, QR, and unit barcodes must be unique across the complete item master.");

    private static void EnsureTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("A valid tenant context is required for inventory identifier access.");
        }
    }
}
