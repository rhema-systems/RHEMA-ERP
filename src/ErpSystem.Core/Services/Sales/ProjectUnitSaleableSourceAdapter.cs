using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Sales;

namespace ErpSystem.Core.Services.Sales;

public class ProjectUnitSaleableSourceAdapter : ISalesSaleableSourceAdapter
{
    private readonly IProjectService _projectService;

    public ProjectUnitSaleableSourceAdapter(IProjectService projectService)
    {
        _projectService = projectService;
    }

    public string AdapterKey => "project-units";

    public IReadOnlyCollection<SalesSaleableSourceFilterDefinitionDto> FilterDefinitions { get; } =
    [
        new() { Field = "projectCode", DisplayName = "Project code" },
        new() { Field = "unitType", DisplayName = "Unit type" },
        new() { Field = "unitStatus", DisplayName = "Unit status" },
        new() { Field = "commercialStatus", DisplayName = "Commercial status" },
        new() { Field = "handoverStatus", DisplayName = "Handover status" },
        new() { Field = "currency", DisplayName = "Currency" }
    ];

    public async Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchItemsAsync(
        SalesSaleableSource source,
        string? search = null,
        int take = 50)
    {
        var normalizedTake = Math.Clamp(take, 1, 100);
        var units = await _projectService.GetReleasedProjectUnitsForSalesAsync(
            search,
            Math.Max(normalizedTake * 5, 100));
        var filters = SaleableSourceFilterSettings.Parse(source.SettingsJson);

        return units
            .Where(unit => MatchesFilters(unit, filters))
            .Take(normalizedTake)
            .Select(unit => new SalesSaleableItemDto
        {
            SourceId = source.Id,
            SourceCode = source.Code,
            SourceType = source.SourceType,
            AdapterKey = source.AdapterKey,
            SourceItemId = unit.ProjectUnitId.ToString(),
            ItemCode = unit.ProjectUnitCode,
            ItemName = unit.ProjectUnitName,
            ItemType = unit.ProjectUnitType,
            Status = unit.ProjectUnitStatus,
            CommercialStatus = unit.CommercialStatus,
            CustomerId = unit.CustomerBusinessPartnerId,
            CustomerName = unit.CustomerBusinessPartnerName,
            EstimatedValue = unit.BasePrice,
            Currency = unit.Currency,
            AreaSquareMeters = unit.AreaSquareMeters,
            PropertyReference = unit.PropertyReference,
            CanCreateSalesOrder = unit.CanCreateSalesOrder,
            CanCreateSalesAgreement = unit.CanCreateSalesAgreement,
            CanCreateLeaseAgreement = unit.CanCreateLeaseAgreement,
            SuggestedOrderType = unit.SuggestedOrderType,
            SuggestedAgreementType = unit.SuggestedAgreementType,
            SuggestedLeaseAgreementType = unit.SuggestedLeaseAgreementType,
            ProjectId = unit.ProjectId,
            ProjectCode = unit.ProjectCode,
            ProjectTitle = unit.ProjectTitle,
            ProjectUnitId = unit.ProjectUnitId,
            ProjectUnitCode = unit.ProjectUnitCode,
            ProjectUnitName = unit.ProjectUnitName,
            HandoverStatus = unit.HandoverStatus
        }).ToList();
    }

    private static bool MatchesFilters(
        ErpSystem.Core.DTOs.Projects.ProjectReleasedUnitSalesLookupDto unit,
        IReadOnlyCollection<SaleableSourceFilterValue> filters)
        => filters.All(filter => SaleableSourceFilterSettings.Normalize(filter.Field) switch
        {
            "projectcode" => SaleableSourceFilterSettings.TextMatches(unit.ProjectCode, filter.Value),
            "unittype" or "type" => SaleableSourceFilterSettings.TextMatches(unit.ProjectUnitType, filter.Value),
            "unitstatus" or "status" => unit.ProjectUnitStatus.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            "commercialstatus" => unit.CommercialStatus.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            "handoverstatus" => unit.HandoverStatus.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            "currency" => unit.Currency.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            _ => true
        });
}
