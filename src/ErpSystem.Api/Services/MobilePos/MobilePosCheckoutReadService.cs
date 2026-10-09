using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosCheckoutReadService
{
    Task<IReadOnlyList<MobilePosCatalogueItemDto>> SearchCatalogueAsync(
        string installationId,
        string? search,
        int limit,
        CancellationToken cancellationToken);

    Task<MobilePosSalePreviewDto> PreviewAsync(
        MobilePosSalePreviewRequestDto request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Supplies the online catalogue and a server-calculated sale preview. The mobile client renders
/// these values and sends them back for final revalidation; it never owns tax or price rules.
/// </summary>
public sealed class MobilePosCheckoutReadService : IMobilePosCheckoutReadService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMobilePosFoundationService _foundation;
    private readonly ITaxCalculationEngine _taxes;

    public MobilePosCheckoutReadService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IMobilePosFoundationService foundation,
        ITaxCalculationEngine taxes)
    {
        _db = db;
        _currentUser = currentUser;
        _foundation = foundation;
        _taxes = taxes;
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A current tenant is required for Mobile POS.");

    public async Task<IReadOnlyList<MobilePosCatalogueItemDto>> SearchCatalogueAsync(
        string installationId,
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        var bootstrap = await _foundation.GetBootstrapAsync(
            Required(installationId, 200, "installation ID"), cancellationToken);
        var tenantId = TenantId;
        var take = Math.Clamp(limit, 1, 50);
        var term = search?.Trim();
        if (term?.Length > 100)
            throw Reject("MOBILE_POS_CATALOGUE_SEARCH_INVALID", "Catalogue search cannot exceed 100 characters.");

        var store = await _db.MobilePosStores.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == bootstrap.Store.Id && !item.IsDeleted,
            cancellationToken) ?? throw Reject("MOBILE_POS_STORE_NOT_FOUND", "The assigned Mobile POS store was not found.");

        var query = _db.InventoryItems.AsNoTracking().Where(item =>
            item.TenantId == tenantId && !item.IsDeleted && item.Status == ItemStatus.Active
            && item.SalesAccountId.HasValue);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalizedTerm = term.ToUpper();
            query = query.Where(item => item.ItemCode.ToUpper().Contains(normalizedTerm)
                || item.Name.ToUpper().Contains(normalizedTerm)
                || (item.Barcode != null && item.Barcode.ToUpper().Contains(normalizedTerm))
                || (item.AlternateBarcode != null && item.AlternateBarcode.ToUpper().Contains(normalizedTerm))
                || (item.QRCode != null && item.QRCode.ToUpper().Contains(normalizedTerm)));
        }

        var items = await query
            .OrderBy(item => item.ItemCode)
            .ThenBy(item => item.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
        var stockIds = items.Where(item => item.ItemType == ItemType.StockItem)
            .Select(item => item.Id).ToArray();
        var quantities = store.WarehouseId.HasValue && stockIds.Length > 0
            ? await _db.WarehouseQuantities.AsNoTracking()
                .Where(value => value.TenantId == tenantId
                    && value.WarehouseId == store.WarehouseId.Value
                    && stockIds.Contains(value.InventoryItemId)
                    && !value.IsDeleted)
                .ToDictionaryAsync(value => value.InventoryItemId, value => value.AvailableStock, cancellationToken)
            : new Dictionary<Guid, decimal>();

        return items.Select(item =>
        {
            decimal? available = item.ItemType == ItemType.StockItem
                ? quantities.GetValueOrDefault(item.Id)
                : null;
            return new MobilePosCatalogueItemDto
            {
                InventoryItemId = item.Id,
                ItemCode = item.ItemCode,
                Name = item.Name,
                Description = item.Description,
                Barcode = item.Barcode,
                AlternateBarcode = item.AlternateBarcode,
                QrCode = item.QRCode,
                ItemType = item.ItemType.ToString(),
                UnitOfMeasureId = item.UnitOfMeasureId,
                UnitOfMeasureCode = item.UnitOfMeasure,
                UnitPrice = item.SalePrice,
                CurrencyCode = store.CurrencyCode,
                DefaultTaxGroupId = item.DefaultTaxGroupId,
                AvailableQuantity = available,
                IsAvailable = item.ItemType != ItemType.StockItem
                    || (store.WarehouseId.HasValue && available > 0m),
                ChangedAtUtc = item.UpdatedAt ?? item.CreatedAt
            };
        }).ToArray();
    }

    public async Task<MobilePosSalePreviewDto> PreviewAsync(
        MobilePosSalePreviewRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bootstrap = await _foundation.GetBootstrapAsync(
            Required(request.InstallationId, 200, "installation ID"), cancellationToken);
        if (!bootstrap.CurrentTillSessionId.HasValue)
            throw Reject("MOBILE_POS_TILL_SESSION_REQUIRED", "Open your assigned till session before previewing a sale.");
        ValidatePreviewRequest(request);

        var tenantId = TenantId;
        var now = DateTime.UtcNow;
        var store = await _db.MobilePosStores.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == bootstrap.Store.Id && !item.IsDeleted,
            cancellationToken) ?? throw Reject("MOBILE_POS_STORE_NOT_FOUND", "The assigned Mobile POS store was not found.");
        if (store.Status != MobilePosStoreStatus.Active)
            throw Reject("MOBILE_POS_STORE_INACTIVE", "The assigned Mobile POS store is not active.");

        var selectedPartnerId = request.BusinessPartnerId ?? store.DefaultWalkInBusinessPartnerId;
        var selectedRoleId = request.BusinessPartnerRoleId ?? store.DefaultWalkInBusinessPartnerRoleId;
        var customerRole = await _db.EligibleMobilePosCustomerRoles(tenantId, now)
            .AsNoTracking()
            .Include(role => role.BusinessPartner)
            .SingleOrDefaultAsync(role => role.Id == selectedRoleId
                && role.BusinessPartnerId == selectedPartnerId, cancellationToken)
            ?? throw Reject("MOBILE_POS_CUSTOMER_INELIGIBLE", "The selected customer is not active, approved, and transaction ready.");
        if (!string.IsNullOrWhiteSpace(customerRole.BusinessPartner.Currency)
            && !string.Equals(customerRole.BusinessPartner.Currency, store.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw Reject("MOBILE_POS_CUSTOMER_CURRENCY_MISMATCH",
                $"The selected customer uses {customerRole.BusinessPartner.Currency}; this store transacts in {store.CurrencyCode}.");
        }

        var itemIds = request.Lines.Select(line => line.InventoryItemId).Distinct().ToArray();
        var items = await _db.InventoryItems.AsNoTracking()
            .Where(item => item.TenantId == tenantId && itemIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (items.Count != itemIds.Length)
            throw Reject("MOBILE_POS_ITEM_NOT_FOUND", "One or more sale items were not found in the current tenant.");

        var currencyDecimalPlaces = await _db.Currencies.AsNoTracking()
            .Where(currency => currency.TenantId == tenantId && !currency.IsDeleted && currency.IsActive
                && currency.CurrencyCode == store.CurrencyCode)
            .Select(currency => (int?)currency.DecimalPlaces)
            .SingleOrDefaultAsync(cancellationToken) ?? 2;
        CurrencyMinorUnitPolicy.Validate(store.CurrencyCode, currencyDecimalPlaces);

        var previewLines = new List<MobilePosSalePreviewLineDto>(request.Lines.Count);
        var taxLines = new List<TaxDocumentLineRequestDto>(request.Lines.Count);
        foreach (var input in request.Lines)
        {
            var item = items[input.InventoryItemId];
            if (item.Status != ItemStatus.Active || !item.SalesAccountId.HasValue)
                throw Reject("MOBILE_POS_ITEM_UNAVAILABLE", $"Item {item.ItemCode} is not ready for sale.");
            if (item.ItemType == ItemType.StockItem && !store.WarehouseId.HasValue)
                throw Reject("MOBILE_POS_WAREHOUSE_REQUIRED", $"Store {store.Code} needs a default warehouse before selling stock items.");

            var gross = RoundMoney(input.Quantity * item.SalePrice, currencyDecimalPlaces);
            var discount = InvoiceTradeDiscountPolicy.CalculateLineDiscount(
                gross, input.DiscountPercentage, "Mobile POS preview line", currencyDecimalPlaces);
            var net = RoundMoney(gross - discount, currencyDecimalPlaces);
            var line = new MobilePosSalePreviewLineDto
            {
                ClientLineId = input.ClientLineId,
                InventoryItemId = item.Id,
                ItemCode = item.ItemCode,
                Description = item.Name,
                Quantity = input.Quantity,
                UnitPrice = item.SalePrice,
                GrossAmount = gross,
                DiscountPercentage = input.DiscountPercentage,
                DiscountAmount = discount,
                NetAmount = net,
                TaxGroupId = item.DefaultTaxGroupId,
                TaxTreatment = TaxTreatment.Standard,
                UnitOfMeasureId = item.UnitOfMeasureId,
                UnitOfMeasureCode = item.UnitOfMeasure
            };
            previewLines.Add(line);
            if (net > 0m)
            {
                taxLines.Add(new TaxDocumentLineRequestDto
                {
                    DocumentLineId = input.ClientLineId,
                    BaseAmount = net,
                    TaxGroupId = item.DefaultTaxGroupId,
                    TransactionType = item.ItemType == ItemType.Service
                        ? TaxTransactionType.SaleOfServices
                        : TaxTransactionType.SaleOfGoods
                });
            }
        }

        var taxResult = taxLines.Count == 0
            ? new TaxCalculationResultDto
            {
                CurrencyCode = store.CurrencyCode,
                CurrencyDecimalPlaces = currencyDecimalPlaces
            }
            : await _taxes.CalculateDocumentTaxesAsync(new TaxDocumentCalculationRequestDto
            {
                CurrencyCode = store.CurrencyCode,
                TransactionDate = now.Date,
                BusinessPartnerId = selectedPartnerId,
                BusinessPartnerRole = BusinessPartnerRoleType.Customer,
                Lines = taxLines
            }, cancellationToken);

        foreach (var line in previewLines)
        {
            line.TaxAmount = RoundMoney(taxResult.TaxBreakdowns
                .Where(tax => tax.DocumentLineId == line.ClientLineId)
                .Sum(tax => tax.TaxAmount), currencyDecimalPlaces);
            line.LineTotal = RoundMoney(line.NetAmount + line.TaxAmount, currencyDecimalPlaces);
        }

        var subTotal = RoundMoney(previewLines.Sum(line => line.NetAmount), currencyDecimalPlaces);
        var taxAmount = RoundMoney(previewLines.Sum(line => line.TaxAmount), currencyDecimalPlaces);
        var discountAmount = RoundMoney(previewLines.Sum(line => line.DiscountAmount), currencyDecimalPlaces);
        return new MobilePosSalePreviewDto
        {
            BusinessPartnerId = selectedPartnerId,
            BusinessPartnerRoleId = selectedRoleId,
            CustomerCode = customerRole.BusinessPartner.PartnerCode,
            CustomerName = customerRole.BusinessPartner.PartnerName,
            UsedStoreDefaultCustomer = selectedPartnerId == store.DefaultWalkInBusinessPartnerId
                && selectedRoleId == store.DefaultWalkInBusinessPartnerRoleId,
            CurrencyCode = store.CurrencyCode,
            CurrencyDecimalPlaces = currencyDecimalPlaces,
            SubTotal = subTotal,
            TaxAmount = taxAmount,
            DiscountAmount = discountAmount,
            TotalAmount = RoundMoney(subTotal + taxAmount, currencyDecimalPlaces),
            CalculatedAtUtc = now,
            Lines = previewLines
        };
    }

    private static void ValidatePreviewRequest(MobilePosSalePreviewRequestDto request)
    {
        if (request.BusinessPartnerId.HasValue != request.BusinessPartnerRoleId.HasValue)
            throw Reject("MOBILE_POS_CUSTOMER_IDENTITY_INCOMPLETE", "Select both the customer and its Customer role, or leave both blank for the store walk-in customer.");
        if (request.Lines.Count is < 1 or > 200)
            throw Reject("MOBILE_POS_LINES_INVALID", "A sale must contain between 1 and 200 lines.");
        if (request.Lines.Any(line => line.ClientLineId == Guid.Empty || line.InventoryItemId == Guid.Empty
                || line.Quantity <= 0m || line.DiscountPercentage is < 0m or > 100m)
            || request.Lines.Select(line => line.ClientLineId).Distinct().Count() != request.Lines.Count)
        {
            throw Reject("MOBILE_POS_LINE_INVALID", "Every preview line needs a unique line ID, an item, positive quantity, and a discount from 0 to 100 percent.");
        }
    }

    private static decimal RoundMoney(decimal value, int decimalPlaces) =>
        CurrencyMinorUnitPolicy.Round(value, decimalPlaces);

    private static string Required(string? value, int maximumLength, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
            throw Reject("MOBILE_POS_REQUIRED_VALUE_INVALID", $"The {label} must contain 1 to {maximumLength} characters.");
        return normalized;
    }

    private static MobilePosCommandRejectedException Reject(string code, string detail) => new(code, detail);
}
