using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class SalesSetupService : ISalesSetupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IReadOnlyDictionary<string, ISalesSaleableSourceAdapter> _adapters;
    private readonly ILogger<SalesSetupService> _logger;

    public SalesSetupService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IEnumerable<ISalesSaleableSourceAdapter> adapters,
        ILogger<SalesSetupService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _adapters = adapters.ToDictionary(x => x.AdapterKey, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<SalesSaleableSourceDto>> GetSaleableSourcesAsync(bool includeInactive = false)
    {
        await EnsureDefaultSaleableSourcesAsync();

        var sources = (await _unitOfWork.Repository<SalesSaleableSource>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && (includeInactive || x.IsActive)))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .Select(MapToDto)
            .ToList();

        return sources;
    }

    public Task<IReadOnlyCollection<SalesSaleableSourceAdapterDefinitionDto>> GetSaleableSourceAdapterDefinitionsAsync()
    {
        IReadOnlyCollection<SalesSaleableSourceAdapterDefinitionDto> definitions = _adapters.Values
            .OrderBy(adapter => adapter.AdapterKey, StringComparer.OrdinalIgnoreCase)
            .Select(adapter => new SalesSaleableSourceAdapterDefinitionDto
            {
                AdapterKey = adapter.AdapterKey,
                Filters = adapter.FilterDefinitions
            })
            .ToArray();
        return Task.FromResult(definitions);
    }

    public async Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchSaleableItemsAsync(Guid sourceId, string? search = null, int take = 50)
    {
        await EnsureDefaultSaleableSourcesAsync();

        var source = await _unitOfWork.Repository<SalesSaleableSource>().FirstOrDefaultAsync(x =>
                x.Id == sourceId
                && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Saleable source with ID {sourceId} was not found.");

        if (!source.IsActive)
        {
            throw new InvalidOperationException("The selected saleable source is inactive.");
        }

        if (!_adapters.TryGetValue(source.AdapterKey, out var adapter))
        {
            throw new InvalidOperationException($"No saleable source adapter is registered for '{source.AdapterKey}'.");
        }

        var normalizedTake = Math.Clamp(take, 1, 100);
        var items = (await adapter.SearchItemsAsync(source, search, normalizedTake)).ToList();
        await HydrateActiveAllocationStateAsync(source, items);
        return items;
    }

    public async Task<SalesSaleableSourceDto> CreateSaleableSourceAsync(UpsertSalesSaleableSourceDto dto)
    {
        var repository = _unitOfWork.Repository<SalesSaleableSource>();
        var normalizedCode = NormalizeCode(dto.Code);
        var duplicate = await repository.FirstOrDefaultAsync(x =>
            x.TenantId == _currentUserProvider.TenantId
            && x.Code == normalizedCode);

        if (duplicate != null)
        {
            throw new InvalidOperationException($"A saleable source with code '{normalizedCode}' already exists.");
        }

        var entity = new SalesSaleableSource
        {
            TenantId = _currentUserProvider.TenantId,
            Code = normalizedCode,
            DisplayName = NormalizeRequired(dto.DisplayName, "Display name"),
            Description = NormalizeOptional(dto.Description),
            SourceType = NormalizeRequired(dto.SourceType, "Source type"),
            AdapterKey = NormalizeRequired(dto.AdapterKey, "Adapter key"),
            IsActive = dto.IsActive,
            Icon = NormalizeIcon(dto.Icon),
            ColorCode = NormalizeColor(dto.ColorCode),
            SortOrder = dto.SortOrder,
            SupportedTransactionTypes = NormalizeTransactionTypes(dto.SupportedTransactionTypes),
            DefaultCurrency = NormalizeCurrency(dto.DefaultCurrency),
            DefaultWorkflowEntityType = NormalizeOptional(dto.DefaultWorkflowEntityType),
            AllowSalesOrders = dto.AllowSalesOrders,
            AllowSalesAgreements = dto.AllowSalesAgreements,
            AllowReservations = dto.AllowReservations,
            RequiresExternalModule = dto.RequiresExternalModule,
            SettingsJson = NormalizeOptional(dto.SettingsJson),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Sales saleable source {Code} for tenant {TenantId}", entity.Code, entity.TenantId);
        return MapToDto(entity);
    }

    public async Task<SalesSaleableSourceDto> UpdateSaleableSourceAsync(Guid id, UpsertSalesSaleableSourceDto dto)
    {
        var repository = _unitOfWork.Repository<SalesSaleableSource>();
        var entity = await repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Saleable source with ID {id} was not found.");

        if (entity.TenantId != _currentUserProvider.TenantId)
        {
            throw new InvalidOperationException("The selected saleable source belongs to a different tenant.");
        }

        var normalizedCode = NormalizeCode(dto.Code);
        var duplicate = await repository.FirstOrDefaultAsync(x =>
            x.TenantId == _currentUserProvider.TenantId
            && x.Code == normalizedCode
            && x.Id != id);

        if (duplicate != null)
        {
            throw new InvalidOperationException($"A saleable source with code '{normalizedCode}' already exists.");
        }

        entity.Code = normalizedCode;
        entity.DisplayName = NormalizeRequired(dto.DisplayName, "Display name");
        entity.Description = NormalizeOptional(dto.Description);
        entity.SourceType = NormalizeRequired(dto.SourceType, "Source type");
        entity.AdapterKey = NormalizeRequired(dto.AdapterKey, "Adapter key");
        entity.IsActive = dto.IsActive;
        entity.Icon = NormalizeIcon(dto.Icon);
        entity.ColorCode = NormalizeColor(dto.ColorCode);
        entity.SortOrder = dto.SortOrder;
        entity.SupportedTransactionTypes = NormalizeTransactionTypes(dto.SupportedTransactionTypes);
        entity.DefaultCurrency = NormalizeCurrency(dto.DefaultCurrency);
        entity.DefaultWorkflowEntityType = NormalizeOptional(dto.DefaultWorkflowEntityType);
        entity.AllowSalesOrders = dto.AllowSalesOrders;
        entity.AllowSalesAgreements = dto.AllowSalesAgreements;
        entity.AllowReservations = dto.AllowReservations;
        entity.RequiresExternalModule = dto.RequiresExternalModule;
        entity.SettingsJson = NormalizeOptional(dto.SettingsJson);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated Sales saleable source {Code} for tenant {TenantId}", entity.Code, entity.TenantId);
        return MapToDto(entity);
    }

    public async Task<SalesSaleableSourceDto> SetSaleableSourceActiveStateAsync(Guid id, bool isActive)
    {
        var repository = _unitOfWork.Repository<SalesSaleableSource>();
        var entity = await repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Saleable source with ID {id} was not found.");

        if (entity.TenantId != _currentUserProvider.TenantId)
        {
            throw new InvalidOperationException("The selected saleable source belongs to a different tenant.");
        }

        entity.IsActive = isActive;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteSaleableSourceAsync(Guid id)
    {
        var repository = _unitOfWork.Repository<SalesSaleableSource>();
        var entity = await repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Saleable source with ID {id} was not found.");

        if (entity.TenantId != _currentUserProvider.TenantId)
        {
            throw new InvalidOperationException("The selected saleable source belongs to a different tenant.");
        }

        if (entity.IsSystemSource)
        {
            throw new InvalidOperationException("System saleable sources cannot be deleted. Deactivate the source instead.");
        }

        entity.DeletedBy = _currentUserProvider.Username;
        await repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task SeedDefaultSaleableSourcesAsync()
    {
        await EnsureDefaultSaleableSourcesAsync();
    }

    private async Task EnsureDefaultSaleableSourcesAsync()
    {
        var repository = _unitOfWork.Repository<SalesSaleableSource>();
        var existing = (await repository.FindAsync(x => x.TenantId == _currentUserProvider.TenantId))
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        var changedAny = false;
        foreach (var legacySource in existing.Values.Where(RetireLegacyProjectUnitSource))
        {
            legacySource.UpdatedBy = _currentUserProvider.Username;
            legacySource.LastModifiedById = _currentUserProvider.UserId;
            await repository.UpdateAsync(legacySource);
            changedAny = true;
        }

        foreach (var source in GetDefaultSources())
        {
            if (existing.TryGetValue(source.Code, out var current))
            {
                var sourceChanged = UpgradeLandManagementPlaceholder(current);
                sourceChanged |= ReconcileEstatePortalSource(current);
                if (sourceChanged)
                {
                    current.UpdatedBy = _currentUserProvider.Username;
                    current.LastModifiedById = _currentUserProvider.UserId;
                    await repository.UpdateAsync(current);
                    changedAny = true;
                }
                continue;
            }

            source.TenantId = _currentUserProvider.TenantId;
            source.CreatedBy = _currentUserProvider.Username;
            source.CreatedById = _currentUserProvider.UserId;
            await repository.AddAsync(source);
            changedAny = true;
        }

        if (changedAny)
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private static bool UpgradeLandManagementPlaceholder(SalesSaleableSource source)
    {
        if (!source.Code.Equals("LAND_MANAGEMENT", StringComparison.OrdinalIgnoreCase)
            || !source.AdapterKey.Equals("land-management", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.SettingsJson?.Trim(),
                "{\"source\":\"land-management\",\"integrationStatus\":\"pending-merge\"}",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        source.Description = "Published Estate land demarcations available for configured Sales transactions.";
        source.IsActive = true;
        source.RequiresExternalModule = false;
        source.SettingsJson = LandManagementSettingsJson;
        return true;
    }

    private const string LandManagementSettingsJson =
        "{\"source\":\"land-management\",\"filters\":[{\"field\":\"isPublishedToExternalPortal\",\"value\":\"true\"},{\"field\":\"externalListingStatus\",\"value\":\"Published\"}]}";

    private const string PropertyRegisterSettingsJson =
        "{\"source\":\"property-register\",\"filters\":[{\"field\":\"isPublishedToExternalPortal\",\"value\":\"true\"},{\"field\":\"assetType\",\"value\":\"Property\"}]}";

    private const string FacilityRegisterSettingsJson =
        "{\"source\":\"property-register\",\"filters\":[{\"field\":\"isPublishedToExternalPortal\",\"value\":\"true\"},{\"field\":\"assetType\",\"value\":\"Facility\"}]}";

    private static bool RetireLegacyProjectUnitSource(SalesSaleableSource source)
    {
        if (!source.IsSystemSource
            || !source.Code.Equals("PROJECT_UNITS", StringComparison.OrdinalIgnoreCase)
            || !source.AdapterKey.Equals("project-units", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string description = "Project units are now published through the Estate Property Register and selected from the Property or Facility sales source.";
        var changed = source.IsActive || !string.Equals(source.Description, description, StringComparison.Ordinal);
        source.IsActive = false;
        source.Description = description;
        return changed;
    }

    private static bool ReconcileEstatePortalSource(SalesSaleableSource source)
    {
        if (!source.IsSystemSource
            || !source.AdapterKey.Equals("property-register", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string displayName;
        string description;
        string sourceType;
        string settingsJson;
        if (source.Code.Equals("PROPERTY_REGISTER", StringComparison.OrdinalIgnoreCase))
        {
            displayName = "Properties";
            description = "Estate assets marked for external portal publication and classified as Property, including project units.";
            sourceType = "PropertyRegister";
            settingsJson = PropertyRegisterSettingsJson;
        }
        else if (source.Code.Equals("FACILITY_REGISTER", StringComparison.OrdinalIgnoreCase))
        {
            displayName = "Facilities";
            description = "Estate assets marked for external portal publication and classified as Facility.";
            sourceType = "FacilityRegister";
            settingsJson = FacilityRegisterSettingsJson;
        }
        else
        {
            return false;
        }

        var changed = !source.IsActive
            || source.RequiresExternalModule
            || !string.Equals(source.DisplayName, displayName, StringComparison.Ordinal)
            || !string.Equals(source.Description, description, StringComparison.Ordinal)
            || !string.Equals(source.SourceType, sourceType, StringComparison.Ordinal)
            || !string.Equals(source.SettingsJson, settingsJson, StringComparison.Ordinal);

        source.DisplayName = displayName;
        source.Description = description;
        source.SourceType = sourceType;
        source.IsActive = true;
        source.RequiresExternalModule = false;
        source.SettingsJson = settingsJson;
        return changed;
    }

    private static IReadOnlyCollection<SalesSaleableSource> GetDefaultSources() =>
    [
        new SalesSaleableSource
        {
            Code = "INVENTORY",
            DisplayName = "Inventory",
            Description = "Inventory items and stock locations that can be sold through sales orders.",
            SourceType = "Inventory",
            AdapterKey = "inventory",
            IsActive = false,
            Icon = "Package",
            ColorCode = "#16A34A",
            SortOrder = 20,
            SupportedTransactionTypes = "SalesOrder,Reservation",
            DefaultCurrency = "GHS",
            DefaultWorkflowEntityType = "SalesOrder",
            AllowSalesOrders = true,
            AllowSalesAgreements = false,
            AllowReservations = true,
            IsSystemSource = true,
            SettingsJson = "{\"source\":\"inventory\"}"
        },
        new SalesSaleableSource
        {
            Code = "PROPERTY_REGISTER",
            DisplayName = "Properties",
            Description = "Estate assets marked for external portal publication and classified as Property, including project units.",
            SourceType = "PropertyRegister",
            AdapterKey = "property-register",
            IsActive = true,
            Icon = "Home",
            ColorCode = "#7C3AED",
            SortOrder = 30,
            SupportedTransactionTypes = "SalesOrder,SalesAgreement,LeaseAgreement,Reservation",
            DefaultCurrency = "GHS",
            DefaultWorkflowEntityType = "SalesAgreement",
            AllowSalesOrders = true,
            AllowSalesAgreements = true,
            AllowReservations = true,
            IsSystemSource = true,
            RequiresExternalModule = false,
            SettingsJson = PropertyRegisterSettingsJson
        },
        new SalesSaleableSource
        {
            Code = "FACILITY_REGISTER",
            DisplayName = "Facilities",
            Description = "Estate assets marked for external portal publication and classified as Facility.",
            SourceType = "FacilityRegister",
            AdapterKey = "property-register",
            IsActive = true,
            Icon = "Building2",
            ColorCode = "#0F766E",
            SortOrder = 35,
            SupportedTransactionTypes = "SalesOrder,SalesAgreement,LeaseAgreement,Reservation",
            DefaultCurrency = "GHS",
            DefaultWorkflowEntityType = "SalesAgreement",
            AllowSalesOrders = true,
            AllowSalesAgreements = true,
            AllowReservations = true,
            IsSystemSource = true,
            RequiresExternalModule = false,
            SettingsJson = FacilityRegisterSettingsJson
        },
        new SalesSaleableSource
        {
            Code = "ASSET_REGISTER",
            DisplayName = "Asset Register",
            Description = "Asset register items that are approved for disposal or sale.",
            SourceType = "AssetRegister",
            AdapterKey = "asset-register",
            IsActive = false,
            Icon = "Boxes",
            ColorCode = "#EA580C",
            SortOrder = 40,
            SupportedTransactionTypes = "SalesOrder,Reservation",
            DefaultCurrency = "GHS",
            DefaultWorkflowEntityType = "SalesOrder",
            AllowSalesOrders = true,
            AllowSalesAgreements = false,
            AllowReservations = true,
            IsSystemSource = true,
            SettingsJson = "{\"source\":\"asset-register\"}"
        },
        new SalesSaleableSource
        {
            Code = "LAND_MANAGEMENT",
            DisplayName = "Land Management Plots",
            Description = "Published Estate land demarcations available for configured Sales transactions.",
            SourceType = "LandManagement",
            AdapterKey = "land-management",
            IsActive = true,
            Icon = "Map",
            ColorCode = "#0891B2",
            SortOrder = 50,
            SupportedTransactionTypes = "SalesOrder,SalesAgreement,LeaseAgreement,Reservation,PlotAllocation",
            DefaultCurrency = "GHS",
            DefaultWorkflowEntityType = "SalesAllocation",
            AllowSalesOrders = true,
            AllowSalesAgreements = true,
            AllowReservations = true,
            RequiresExternalModule = false,
            IsSystemSource = true,
            SettingsJson = LandManagementSettingsJson
        }
    ];

    private static SalesSaleableSourceDto MapToDto(SalesSaleableSource source) => new()
    {
        Id = source.Id,
        TenantId = source.TenantId,
        Code = source.Code,
        DisplayName = source.DisplayName,
        Description = source.Description,
        SourceType = source.SourceType,
        AdapterKey = source.AdapterKey,
        IsActive = source.IsActive,
        Icon = source.Icon,
        ColorCode = source.ColorCode,
        SortOrder = source.SortOrder,
        SupportedTransactionTypes = source.SupportedTransactionTypes,
        DefaultCurrency = source.DefaultCurrency,
        DefaultWorkflowEntityType = source.DefaultWorkflowEntityType,
        AllowSalesOrders = source.AllowSalesOrders,
        AllowSalesAgreements = source.AllowSalesAgreements,
        AllowReservations = source.AllowReservations,
        RequiresExternalModule = source.RequiresExternalModule,
        IsSystemSource = source.IsSystemSource,
        SettingsJson = source.SettingsJson,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt
    };

    private async Task HydrateActiveAllocationStateAsync(
        SalesSaleableSource source,
        List<SalesSaleableItemDto> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var sourceItemIds = items
            .Select(item => item.SourceItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sourceItemIds.Count == 0)
        {
            return;
        }

        var activeStatuses = new[] { "Reserved", "Allocated", "Sold", "Leased", "PendingApproval", "Approved" };
        var allocationQuery = _unitOfWork.Repository<SalesAllocation>()
            .GetQueryable()
            .Where(allocation =>
                allocation.TenantId == _currentUserProvider.TenantId
                && sourceItemIds.Contains(allocation.SourceItemId)
                && !allocation.IsDeleted
                && activeStatuses.Contains(allocation.Status));

        if (source.AdapterKey.Equals("property-register", StringComparison.OrdinalIgnoreCase))
        {
            allocationQuery = allocationQuery.Where(allocation =>
                allocation.AdapterKey == "property-register"
                || allocation.AdapterKey == "project-units");
        }
        else
        {
            allocationQuery = allocationQuery.Where(allocation =>
                allocation.SaleableSourceId == source.Id);
        }

        var allocations = await allocationQuery
            .OrderByDescending(allocation => allocation.CreatedAt)
            .ToListAsync();

        if (allocations.Count == 0)
        {
            return;
        }

        var allocationLookup = allocations
            .GroupBy(allocation => allocation.SourceItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!allocationLookup.TryGetValue(item.SourceItemId, out var allocation))
            {
                continue;
            }

            item.ActiveAllocationId = allocation.Id;
            item.ActiveAllocationStatus = allocation.Status;
            item.ActiveAllocationReservedUntil = allocation.ReservedUntil;
            item.ActiveAllocationCustomerName = allocation.CustomerName;
            item.HasActiveAllocation = true;
            item.CanCreateSalesOrder = false;
            item.SalesOrderIneligibilityReason =
                $"This item already has an active {allocation.Status} allocation.";
            item.CanCreateSalesAgreement = false;
            item.CanCreateLeaseAgreement = false;
            item.CommercialStatus = string.IsNullOrWhiteSpace(item.CommercialStatus)
                ? allocation.Status
                : $"{item.CommercialStatus} / {allocation.Status}";
        }
    }

    private static string NormalizeCode(string value)
    {
        var normalized = NormalizeRequired(value, "Code")
            .Replace("-", "_")
            .Replace(" ", "_")
            .ToUpperInvariant();

        return normalized.Length > 50 ? normalized[..50] : normalized;
    }

    private static string NormalizeRequired(string value, string label)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{label} is required.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeIcon(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Package" : value.Trim();

    private static string NormalizeColor(string? value)
        => string.IsNullOrWhiteSpace(value) ? "#2563EB" : value.Trim();

    private static string NormalizeCurrency(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "GHS" : value.Trim().ToUpperInvariant();
        return normalized.Length > 10 ? normalized[..10] : normalized;
    }

    private static string NormalizeTransactionTypes(string? value)
        => string.IsNullOrWhiteSpace(value) ? "SalesOrder" : value.Trim();
}
