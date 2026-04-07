using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<ProjectUnitDto> CreateSalesAgreementFromProjectUnitAsync(Guid unitId)
    {
        var unit = await GetProjectUnitEntityAsync(unitId);
        await RequireProjectAsync(unit.ProjectId, ProjectAccessOperation.ManageFinancials);
        EnsureUnitIsReadyForSalesHandoff(unit);

        if (unit.SalesAgreementId.HasValue)
        {
            throw new InvalidOperationException("This unit is already linked to a sales agreement.");
        }

        var project = await _projectRepository.GetByIdAsync(unit.ProjectId)
            ?? throw new InvalidOperationException("The parent project could not be found.");

        var agreement = await _salesAgreementService.CreateAsync(new CreateSalesAgreementDto
        {
            BusinessPartnerId = unit.CustomerBusinessPartnerId!.Value,
            AgreementTitle = BuildProjectUnitAgreementTitle(project, unit),
            AgreementType = SalesAgreementType.General.ToString(),
            StartDate = DateTime.UtcNow.Date,
            AgreedValue = unit.BasePrice ?? 0m,
            MinimumCommitment = unit.BasePrice ?? 0m,
            MaximumCommitment = unit.BasePrice ?? 0m,
            Currency = unit.Currency,
            PropertyReference = BuildProjectUnitPropertyReference(project, unit),
            PropertyType = MapProjectUnitPropertyType(unit.UnitType)?.ToString(),
            PropertyDescription = BuildProjectUnitPropertyDescription(project, unit),
            PropertyLocation = BuildProjectUnitPropertyLocation(unit),
            Notes = $"Created from project unit {unit.Name} in project {project.ProjectCode}."
        });

        unit.SalesAgreementId = agreement.Id;
        unit.Status = ProjectUnitStatuses.Reserved;
        unit.UpdatedBy = _currentUserProvider.Username;
        unit.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectUnit>().UpdateAsync(unit);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectUnitDtoAsync(unit.ProjectId, unit.Id);
    }

    public async Task<ProjectUnitDto> CreateLeaseAgreementFromProjectUnitAsync(Guid unitId)
    {
        var unit = await GetProjectUnitEntityAsync(unitId);
        await RequireProjectAsync(unit.ProjectId, ProjectAccessOperation.ManageFinancials);
        EnsureUnitIsReadyForSalesHandoff(unit);

        if (unit.SalesAgreementId.HasValue)
        {
            throw new InvalidOperationException("This unit is already linked to a sales agreement.");
        }

        if (unit.SalesOrderId.HasValue)
        {
            throw new InvalidOperationException("This unit is already linked to a sales order.");
        }

        var project = await _projectRepository.GetByIdAsync(unit.ProjectId)
            ?? throw new InvalidOperationException("The parent project could not be found.");
        var agreementType = ResolveProjectUnitLeaseAgreementType(unit.UnitType);

        var agreement = await _salesAgreementService.CreateAsync(new CreateSalesAgreementDto
        {
            BusinessPartnerId = unit.CustomerBusinessPartnerId!.Value,
            AgreementTitle = BuildProjectUnitLeaseAgreementTitle(project, unit, agreementType),
            AgreementType = agreementType.ToString(),
            StartDate = unit.HandoverDate?.Date ?? DateTime.UtcNow.Date,
            AgreedValue = 0m,
            MinimumCommitment = 0m,
            MaximumCommitment = 0m,
            Currency = unit.Currency,
            PropertyReference = BuildProjectUnitPropertyReference(project, unit),
            PropertyType = MapProjectUnitPropertyType(unit.UnitType)?.ToString(),
            PropertyDescription = BuildProjectUnitPropertyDescription(project, unit),
            PropertyLocation = BuildProjectUnitPropertyLocation(unit),
            PaymentSchedule = "Define rent, deposit, and billing schedule in Sales.",
            Notes = $"Created as {(agreementType == SalesAgreementType.TenancyAgreement ? "tenancy" : "lease")} handoff from project unit {unit.Name} in project {project.ProjectCode}. Review commercial terms before activation."
        });

        unit.SalesAgreementId = agreement.Id;
        unit.Status = ProjectUnitStatuses.Reserved;
        unit.UpdatedBy = _currentUserProvider.Username;
        unit.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectUnit>().UpdateAsync(unit);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectUnitDtoAsync(unit.ProjectId, unit.Id);
    }

    public async Task<ProjectUnitDto> CreateSalesOrderFromProjectUnitAsync(Guid unitId)
    {
        var unit = await GetProjectUnitEntityAsync(unitId);
        await RequireProjectAsync(unit.ProjectId, ProjectAccessOperation.ManageFinancials);
        EnsureUnitIsReadyForSalesHandoff(unit);

        if (unit.SalesOrderId.HasValue)
        {
            throw new InvalidOperationException("This unit is already linked to a sales order.");
        }

        var project = await _projectRepository.GetByIdAsync(unit.ProjectId)
            ?? throw new InvalidOperationException("The parent project could not be found.");

        var salesOrder = await _salesOrderService.CreateSalesOrderAsync(new CreateSalesOrderDto
        {
            BusinessPartnerId = unit.CustomerBusinessPartnerId!.Value,
            OrderType = SalesOrderType.PropertySale,
            Currency = unit.Currency,
            PropertyReference = BuildProjectUnitPropertyReference(project, unit),
            PropertyType = MapProjectUnitPropertyType(unit.UnitType),
            ReferenceNumber = project.ProjectCode,
            ExternalNotes = $"Created from project unit {unit.Name}.",
            Lines =
            [
                new CreateSalesOrderLineDto
                {
                    Description = BuildProjectUnitSalesLineDescription(project, unit),
                    Quantity = 1,
                    UnitPrice = unit.BasePrice ?? 0m,
                    Unit = unit.AreaSquareMeters.HasValue ? "Unit" : "Lot"
                }
            ]
        });

        unit.SalesOrderId = salesOrder.Id;
        unit.Status = ProjectUnitStatuses.Reserved;
        unit.UpdatedBy = _currentUserProvider.Username;
        unit.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectUnit>().UpdateAsync(unit);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectUnitDtoAsync(unit.ProjectId, unit.Id);
    }

    public async Task<ProjectUnitDto> LinkSalesAgreementToProjectUnitAsync(Guid unitId, Guid salesAgreementId)
    {
        var unit = await GetProjectUnitEntityAsync(unitId);
        await RequireProjectAsync(unit.ProjectId, ProjectAccessOperation.ManageFinancials);
        EnsureUnitIsReadyForSalesLink(unit);

        var salesAgreement = await EnsureTenantSalesAgreementExistsAsync(salesAgreementId)
            ?? throw new InvalidOperationException("The sales agreement could not be found.");

        if (unit.CustomerBusinessPartnerId.HasValue && unit.CustomerBusinessPartnerId != salesAgreement.BusinessPartnerId)
        {
            throw new InvalidOperationException("The selected sales agreement belongs to a different customer.");
        }

        if (unit.SalesAgreementId.HasValue && unit.SalesAgreementId != salesAgreement.Id)
        {
            throw new InvalidOperationException("This unit is already linked to a different sales agreement.");
        }

        unit.CustomerBusinessPartnerId ??= salesAgreement.BusinessPartnerId;
        unit.SalesAgreementId = salesAgreement.Id;
        unit.Status = ProjectUnitStatuses.Reserved;
        unit.UpdatedBy = _currentUserProvider.Username;
        unit.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectUnit>().UpdateAsync(unit);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectUnitDtoAsync(unit.ProjectId, unit.Id);
    }

    public async Task<ProjectUnitDto> LinkSalesOrderToProjectUnitAsync(Guid unitId, Guid salesOrderId)
    {
        var unit = await GetProjectUnitEntityAsync(unitId);
        await RequireProjectAsync(unit.ProjectId, ProjectAccessOperation.ManageFinancials);
        EnsureUnitIsReadyForSalesLink(unit);

        var salesOrder = await EnsureTenantSalesOrderExistsAsync(salesOrderId)
            ?? throw new InvalidOperationException("The sales order could not be found.");

        if (unit.CustomerBusinessPartnerId.HasValue && unit.CustomerBusinessPartnerId != salesOrder.BusinessPartnerId)
        {
            throw new InvalidOperationException("The selected sales order belongs to a different customer.");
        }

        if (unit.SalesOrderId.HasValue && unit.SalesOrderId != salesOrder.Id)
        {
            throw new InvalidOperationException("This unit is already linked to a different sales order.");
        }

        unit.CustomerBusinessPartnerId ??= salesOrder.BusinessPartnerId;
        unit.SalesOrderId = salesOrder.Id;
        unit.Status = ProjectUnitStatuses.Reserved;
        unit.UpdatedBy = _currentUserProvider.Username;
        unit.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectUnit>().UpdateAsync(unit);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectUnitDtoAsync(unit.ProjectId, unit.Id);
    }

    private static void EnsureUnitIsReadyForSalesHandoff(ProjectUnit unit)
    {
        if (!CanUnitStartSalesHandoff(unit))
        {
            if (!unit.IsReleasedForMarket)
            {
                throw new InvalidOperationException("Release the unit before creating sales handoff records.");
            }

            if (!unit.CustomerBusinessPartnerId.HasValue)
            {
                throw new InvalidOperationException("Assign a customer to the unit before creating sales handoff records.");
            }

            throw new InvalidOperationException("This unit can no longer be handed off to Sales from the project workspace.");
        }
    }

    private static void EnsureUnitIsReadyForSalesLink(ProjectUnit unit)
    {
        if (!unit.IsReleasedForMarket)
        {
            throw new InvalidOperationException("Release the unit before linking it to Sales.");
        }

        if (ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.HandedOver)
            || ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.Occupied)
            || ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.Archived)
            || ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.Sold)
            || ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.Leased))
        {
            throw new InvalidOperationException("This unit can no longer be linked to new Sales drafts.");
        }
    }

    private static bool CanUnitStartSalesHandoff(ProjectUnit unit)
        => unit.IsReleasedForMarket
            && unit.CustomerBusinessPartnerId.HasValue
            && !ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.HandedOver)
            && !ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.Occupied)
            && !ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.Archived);

    private static string BuildProjectUnitAgreementTitle(Project project, ProjectUnit unit)
        => $"{project.Title} - {unit.Name} Agreement";

    private static string BuildProjectUnitLeaseAgreementTitle(Project project, ProjectUnit unit, SalesAgreementType agreementType)
        => agreementType == SalesAgreementType.TenancyAgreement
            ? $"{project.Title} - {unit.Name} Tenancy"
            : $"{project.Title} - {unit.Name} Lease";

    private static string BuildProjectUnitPropertyReference(Project project, ProjectUnit unit)
        => string.IsNullOrWhiteSpace(unit.Code)
            ? $"{project.ProjectCode}:{unit.Id:N}"
            : $"{project.ProjectCode}:{unit.Code}";

    private static string BuildProjectUnitPropertyDescription(Project project, ProjectUnit unit)
    {
        var areaPart = unit.AreaSquareMeters.HasValue ? $" ({unit.AreaSquareMeters.Value:0.##} sqm)" : string.Empty;
        return $"{unit.Name}{areaPart} in project {project.Title}";
    }

    private static string? BuildProjectUnitPropertyLocation(ProjectUnit unit)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(unit.BlockName) ? null : $"Block {unit.BlockName}",
            string.IsNullOrWhiteSpace(unit.FloorLabel) ? null : unit.FloorLabel
        }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string BuildProjectUnitSalesLineDescription(Project project, ProjectUnit unit)
        => $"{unit.Name} - {project.Title}{(unit.AreaSquareMeters.HasValue ? $" ({unit.AreaSquareMeters.Value:0.##} sqm)" : string.Empty)}";

    private static PropertyType? MapProjectUnitPropertyType(string? unitType)
        => unitType?.Trim() switch
        {
            ProjectUnitTypes.Apartment => PropertyType.ResidentialApartment,
            ProjectUnitTypes.OfficeSuite or ProjectUnitTypes.RetailShop => PropertyType.CommercialUnit,
            ProjectUnitTypes.Warehouse => PropertyType.IndustrialUnit,
            ProjectUnitTypes.WholeBuilding => PropertyType.MixedUse,
            _ => null
        };

    private static SalesAgreementType ResolveProjectUnitLeaseAgreementType(string? unitType)
        => unitType?.Trim() switch
        {
            ProjectUnitTypes.Apartment => SalesAgreementType.TenancyAgreement,
            _ => SalesAgreementType.LeaseAgreement
        };
}
