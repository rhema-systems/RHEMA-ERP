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

    public async Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchItemsAsync(
        SalesSaleableSource source,
        string? search = null,
        int take = 50)
    {
        var units = await _projectService.GetReleasedProjectUnitsForSalesAsync(search, take);

        return units.Select(unit => new SalesSaleableItemDto
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
}
