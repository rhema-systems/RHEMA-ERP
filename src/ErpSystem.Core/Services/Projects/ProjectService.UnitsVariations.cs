using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<IEnumerable<ProjectUnitDto>> GetProjectUnitsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectUnitsAsync((await GetProjectUnitEntitiesAsync(projectId)).ToList());
    }

    public async Task<IEnumerable<ProjectReleasedUnitSalesLookupDto>> GetReleasedProjectUnitsForSalesAsync(string? search = null, int take = 50)
    {
        EnsureInternalAuthenticatedProjectAccess();

        var accessibleProjects = (await GetAccessibleProjectsAsync(take: Math.Max(take * 5, 500))).ToList();
        if (accessibleProjects.Count == 0)
        {
            return [];
        }

        var projectLookup = accessibleProjects.ToDictionary(x => x.Id);
        var accessibleProjectIds = projectLookup.Keys.ToList();
        var unitRepository = _unitOfWork.Repository<ProjectUnit>();
        if (unitRepository == null)
        {
            return [];
        }

        var releasedUnits = (await unitRepository.FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.IsReleasedForMarket
                && accessibleProjectIds.Contains(x.ProjectId)))
            .ToList();

        if (releasedUnits.Count == 0)
        {
            return [];
        }

        var mappedUnits = await MapProjectUnitsAsync(releasedUnits);
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var filtered = mappedUnits
            .Select(unit =>
            {
                projectLookup.TryGetValue(unit.ProjectId, out var project);
                return new { Unit = unit, Project = project };
            })
            .Where(x => x.Project != null)
            .Where(x =>
            {
                if (string.IsNullOrWhiteSpace(normalizedSearch))
                {
                    return true;
                }

                var term = normalizedSearch!;
                return ContainsTerm(x.Project!.ProjectCode, term)
                    || ContainsTerm(x.Project.Title, term)
                    || ContainsTerm(x.Unit.Code, term)
                    || ContainsTerm(x.Unit.Name, term)
                    || ContainsTerm(x.Unit.CustomerBusinessPartnerName, term)
                    || ContainsTerm(BuildProjectUnitPropertyReference(x.Project, new ProjectUnit
                    {
                        Code = x.Unit.Code,
                        Id = x.Unit.Id
                    }), term);
            })
            .OrderBy(x => x.Project!.ProjectCode)
            .ThenBy(x => x.Unit.Code ?? x.Unit.Name)
            .Take(Math.Max(1, take))
            .Select(x => new ProjectReleasedUnitSalesLookupDto
            {
                ProjectId = x.Project!.Id,
                ProjectCode = x.Project.ProjectCode,
                ProjectTitle = x.Project.Title,
                ProjectUnitId = x.Unit.Id,
                ProjectUnitCode = x.Unit.Code,
                ProjectUnitName = x.Unit.Name,
                ProjectUnitType = x.Unit.UnitType,
                ProjectUnitStatus = x.Unit.Status,
                CommercialStatus = x.Unit.CommercialStatus,
                CommercialIntent = x.Unit.CommercialIntent,
                HandoverStatus = x.Unit.HandoverStatus,
                CustomerBusinessPartnerId = x.Unit.CustomerBusinessPartnerId,
                CustomerBusinessPartnerName = x.Unit.CustomerBusinessPartnerName,
                AreaSquareMeters = x.Unit.AreaSquareMeters,
                BasePrice = x.Unit.BasePrice,
                Currency = x.Unit.Currency,
                PropertyReference = BuildProjectUnitPropertyReference(x.Project, new ProjectUnit
                {
                    Id = x.Unit.Id,
                    Code = x.Unit.Code
                }),
                SuggestedAgreementTitle = BuildProjectUnitAgreementTitle(x.Project, new ProjectUnit
                {
                    Name = x.Unit.Name
                }),
                SuggestedAgreementType = SalesAgreementType.General.ToString(),
                SuggestedLeaseAgreementTitle = BuildProjectUnitLeaseAgreementTitle(
                    x.Project,
                    new ProjectUnit
                    {
                        Name = x.Unit.Name
                    },
                    ResolveProjectUnitLeaseAgreementType(x.Unit.UnitType)),
                SuggestedLeaseAgreementType = ResolveProjectUnitLeaseAgreementType(x.Unit.UnitType).ToString(),
                SuggestedOrderType = SalesOrderType.PropertySale.ToString(),
                SuggestedPropertyType = MapProjectUnitPropertyType(x.Unit.UnitType)?.ToString(),
                SuggestedPropertyDescription = BuildProjectUnitPropertyDescription(x.Project, new ProjectUnit
                {
                    Name = x.Unit.Name,
                    AreaSquareMeters = x.Unit.AreaSquareMeters
                }),
                SuggestedPropertyLocation = BuildProjectUnitPropertyLocation(new ProjectUnit
                {
                    BlockName = x.Unit.BlockName,
                    FloorLabel = x.Unit.FloorLabel
                }),
                SuggestedSalesOrderLineDescription = BuildProjectUnitSalesLineDescription(x.Project, new ProjectUnit
                {
                    Name = x.Unit.Name,
                    AreaSquareMeters = x.Unit.AreaSquareMeters
                }),
                CanCreateSalesAgreement = CanUnitLinkAgreement(new ProjectUnit
                {
                    IsReleasedForMarket = x.Unit.IsReleasedForMarket,
                    CustomerBusinessPartnerId = x.Unit.CustomerBusinessPartnerId,
                    SalesAgreementId = x.Unit.SalesAgreementId,
                    SalesOrderId = x.Unit.SalesOrderId,
                    Status = x.Unit.Status
                }),
                CanCreateLeaseAgreement = CanUnitLinkAgreement(new ProjectUnit
                {
                    IsReleasedForMarket = x.Unit.IsReleasedForMarket,
                    CustomerBusinessPartnerId = x.Unit.CustomerBusinessPartnerId,
                    SalesAgreementId = x.Unit.SalesAgreementId,
                    SalesOrderId = x.Unit.SalesOrderId,
                    Status = x.Unit.Status
                }),
                CanCreateSalesOrder = CanUnitLinkSalesOrder(new ProjectUnit
                {
                    IsReleasedForMarket = x.Unit.IsReleasedForMarket,
                    CustomerBusinessPartnerId = x.Unit.CustomerBusinessPartnerId,
                    SalesAgreementId = x.Unit.SalesAgreementId,
                    SalesOrderId = x.Unit.SalesOrderId,
                    Status = x.Unit.Status
                }),
                SalesAgreementNumber = x.Unit.SalesAgreementNumber,
                SalesOrderNumber = x.Unit.SalesOrderNumber
            })
            .ToList();

        return filtered;
    }

    public async Task<ProjectUnitDto> AddProjectUnitAsync(Guid projectId, CreateProjectUnitDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        await EnsureTenantBusinessPartnerExistsAsync(dto.CustomerBusinessPartnerId, "customer");
        var salesAgreement = await EnsureTenantSalesAgreementExistsAsync(dto.SalesAgreementId);
        var salesOrder = await EnsureTenantSalesOrderExistsAsync(dto.SalesOrderId);
        ValidateCustomerLinkedSalesRecords(dto.CustomerBusinessPartnerId, salesAgreement, salesOrder);
        var unitTypeTemplate = await ValidateProjectUnitTypeTemplateAsync(dto.ProjectUnitTypeTemplateId);
        var (building, floor) = await ResolveProjectHierarchyAsync(projectId, dto.ProjectBuildingId, dto.ProjectFloorId);
        var releaseBatch = await ValidateProjectUnitReleaseBatchAsync(projectId, dto.ProjectUnitReleaseBatchId, building?.Id, floor?.Id);
        var isReleasedForMarket = dto.IsReleasedForMarket;
        var normalizedStatus = NormalizeProjectUnitStatus(dto.Status);
        ValidateProjectUnitCommercialControls(isReleasedForMarket, normalizedStatus, dto.HandoverDate, salesAgreement, salesOrder);
        var effectiveStatus = AlignProjectUnitStatusForRelease(normalizedStatus, isReleasedForMarket);
        var effectiveUnitType = unitTypeTemplate?.DefaultProjectUnitType ?? dto.UnitType;
        var resolvedAmenities = await ResolveProjectUnitAmenitiesAsync(unitTypeTemplate, dto.Amenities);
        var totalAmenityCost = CalculateProjectUnitAmenityTotalCost(resolvedAmenities);

        var siblings = (await GetProjectUnitEntitiesAsync(projectId)).ToList();
        var entity = new ProjectUnit
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectBuildingId = building?.Id,
            ProjectFloorId = floor?.Id,
            ProjectUnitReleaseBatchId = releaseBatch?.Id,
            ProjectUnitTypeTemplateId = unitTypeTemplate?.Id,
            IsReleasedForMarket = isReleasedForMarket,
            ReleasedAt = isReleasedForMarket ? DateTime.UtcNow : null,
            ReleasedById = isReleasedForMarket ? _currentUserProvider.UserId : null,
            CustomerBusinessPartnerId = dto.CustomerBusinessPartnerId,
            SalesAgreementId = salesAgreement?.Id,
            SalesOrderId = salesOrder?.Id,
            Code = TrimOrNull(dto.Code),
            Name = dto.Name.Trim(),
            UnitType = NormalizeProjectUnitType(effectiveUnitType),
            Status = effectiveStatus,
            BlockName = building?.Name ?? TrimOrNull(dto.BlockName),
            FloorLabel = floor?.Name ?? TrimOrNull(dto.FloorLabel),
            AreaSquareMeters = dto.AreaSquareMeters,
            ValuationRate = dto.ValuationRate,
            BasePrice = totalAmenityCost > 0m ? totalAmenityCost : dto.BasePrice,
            Currency = await ResolveProjectCurrencyAsync(dto.Currency),
            HandoverDate = dto.HandoverDate,
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectUnit>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await ReplaceProjectUnitAmenitiesAsync(entity.Id, resolvedAmenities);
        return await GetProjectUnitDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectUnitDto> UpdateProjectUnitAsync(Guid unitId, UpdateProjectUnitDto dto)
    {
        var entity = await GetProjectUnitEntityAsync(unitId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await EnsureTenantBusinessPartnerExistsAsync(dto.CustomerBusinessPartnerId, "customer");
        var salesAgreement = await EnsureTenantSalesAgreementExistsAsync(dto.SalesAgreementId);
        var salesOrder = await EnsureTenantSalesOrderExistsAsync(dto.SalesOrderId);
        ValidateCustomerLinkedSalesRecords(dto.CustomerBusinessPartnerId, salesAgreement, salesOrder);
        var unitTypeTemplate = await ValidateProjectUnitTypeTemplateAsync(dto.ProjectUnitTypeTemplateId);
        var (building, floor) = await ResolveProjectHierarchyAsync(entity.ProjectId, dto.ProjectBuildingId, dto.ProjectFloorId);
        var releaseBatch = await ValidateProjectUnitReleaseBatchAsync(entity.ProjectId, dto.ProjectUnitReleaseBatchId, building?.Id, floor?.Id);
        var isReleasedForMarket = dto.IsReleasedForMarket;
        var normalizedStatus = NormalizeProjectUnitStatus(dto.Status);
        ValidateProjectUnitCommercialControls(isReleasedForMarket, normalizedStatus, dto.HandoverDate, salesAgreement, salesOrder);
        var effectiveStatus = AlignProjectUnitStatusForRelease(normalizedStatus, isReleasedForMarket);
        var effectiveUnitType = unitTypeTemplate?.DefaultProjectUnitType ?? dto.UnitType;
        var resolvedAmenities = await ResolveProjectUnitAmenitiesAsync(unitTypeTemplate, dto.Amenities);
        var totalAmenityCost = CalculateProjectUnitAmenityTotalCost(resolvedAmenities);

        if (entity.IsReleasedForMarket != isReleasedForMarket)
        {
            ValidateProjectUnitReleaseTransition(entity, isReleasedForMarket);
        }

        entity.IsReleasedForMarket = isReleasedForMarket;
        entity.ReleasedAt = isReleasedForMarket
            ? entity.ReleasedAt ?? DateTime.UtcNow
            : null;
        entity.ReleasedById = isReleasedForMarket
            ? entity.ReleasedById ?? _currentUserProvider.UserId
            : null;
        entity.ProjectBuildingId = building?.Id;
        entity.ProjectFloorId = floor?.Id;
        entity.ProjectUnitReleaseBatchId = releaseBatch?.Id;
        entity.ProjectUnitTypeTemplateId = unitTypeTemplate?.Id;
        entity.CustomerBusinessPartnerId = dto.CustomerBusinessPartnerId;
        entity.SalesAgreementId = salesAgreement?.Id;
        entity.SalesOrderId = salesOrder?.Id;
        entity.Code = TrimOrNull(dto.Code);
        entity.Name = dto.Name.Trim();
        entity.UnitType = NormalizeProjectUnitType(effectiveUnitType);
        entity.Status = effectiveStatus;
        entity.BlockName = building?.Name ?? TrimOrNull(dto.BlockName);
        entity.FloorLabel = floor?.Name ?? TrimOrNull(dto.FloorLabel);
        entity.AreaSquareMeters = dto.AreaSquareMeters;
        entity.ValuationRate = dto.ValuationRate;
        entity.BasePrice = totalAmenityCost > 0m ? totalAmenityCost : dto.BasePrice;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? entity.Currency);
        entity.HandoverDate = dto.HandoverDate;
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectUnit>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await ReplaceProjectUnitAmenitiesAsync(entity.Id, resolvedAmenities);
        return await GetProjectUnitDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task<ProjectUnitDto> ReleaseProjectUnitAsync(Guid unitId)
    {
        var entity = await GetProjectUnitEntityAsync(unitId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);

        if (!entity.IsReleasedForMarket)
        {
            entity.IsReleasedForMarket = true;
            entity.ReleasedAt = DateTime.UtcNow;
            entity.ReleasedById = _currentUserProvider.UserId;
            entity.Status = AlignProjectUnitStatusForRelease(entity.Status, true);
            entity.UpdatedBy = _currentUserProvider.Username;
            entity.LastModifiedById = _currentUserProvider.UserId;

            await _unitOfWork.Repository<ProjectUnit>().UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync();
        }

        return await GetProjectUnitDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task<ProjectUnitDto> WithdrawProjectUnitReleaseAsync(Guid unitId)
    {
        var entity = await GetProjectUnitEntityAsync(unitId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        ValidateProjectUnitReleaseTransition(entity, false);

        entity.IsReleasedForMarket = false;
        entity.ReleasedAt = null;
        entity.ReleasedById = null;
        entity.Status = AlignProjectUnitStatusForRelease(entity.Status, false);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectUnit>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectUnitDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task<EstateManagedAssetDto> PublishProjectUnitToEstateAsync(Guid unitId)
    {
        var unit = await GetProjectUnitEntityAsync(unitId);
        var project = await RequireProjectAsync(unit.ProjectId, ProjectAccessOperation.ManageFinancials);

        // Estate/Project integration: only released, handed-over, or occupied Project units can enter Estate management.
        var isReadyForEstate = unit.IsReleasedForMarket
            || ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.HandedOver)
            || ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.Occupied);

        if (!isReadyForEstate)
        {
            throw new InvalidOperationException("Release or hand over the project unit before publishing it to Estate management.");
        }

        var handoff = new ProjectUnitEstateHandoffDto
        {
            ProjectId = project.Id,
            ProjectCode = project.ProjectCode,
            ProjectTitle = project.Title,
            ProjectUnitId = unit.Id,
            ProjectUnitCode = unit.Code,
            ProjectUnitName = unit.Name,
            UnitType = unit.UnitType,
            UnitStatus = unit.Status,
            BlockName = unit.BlockName,
            FloorLabel = unit.FloorLabel,
            Location = project.Title,
            AreaSquareMeters = unit.AreaSquareMeters,
            ValuationAmount = unit.BasePrice,
            Currency = unit.Currency,
            IsAvailableForLease = !ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.Sold),
            IsAvailableForSale = !ProjectUnitStatusEquals(unit.Status, ProjectUnitStatuses.Leased),
            HandoverDate = unit.HandoverDate,
            Notes = unit.Notes
        };

        return await _estateManagedAssetService.PublishProjectUnitAsync(handoff);
    }

    public async Task DeleteProjectUnitAsync(Guid unitId)
    {
        var entity = await GetProjectUnitEntityAsync(unitId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await ReplaceProjectUnitAmenitiesAsync(entity.Id, []);
        await _unitOfWork.Repository<ProjectUnit>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectCustomerVariationDto>> GetCustomerVariationsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var units = (await GetProjectUnitEntitiesAsync(projectId)).ToList();
        return await MapProjectCustomerVariationsAsync(projectId, (await GetProjectCustomerVariationEntitiesAsync(projectId)).ToList(), units);
    }

    public async Task<ProjectCustomerVariationDto> AddCustomerVariationAsync(Guid projectId, CreateProjectCustomerVariationDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var unit = await ValidateProjectCustomerVariationAsync(projectId, dto.ProjectUnitId, dto.CustomerBusinessPartnerId);
        var effectiveCustomerId = dto.CustomerBusinessPartnerId ?? unit?.CustomerBusinessPartnerId;
        var salesAgreement = await EnsureTenantSalesAgreementExistsAsync(dto.SalesAgreementId ?? unit?.SalesAgreementId);
        var salesOrder = await EnsureTenantSalesOrderExistsAsync(dto.SalesOrderId ?? unit?.SalesOrderId);
        var jobCard = await EnsureTenantJobCardExistsAsync(dto.JobCardId);
        var workOrder = await EnsureTenantWorkOrderExistsAsync(dto.WorkOrderId);
        ValidateCustomerLinkedSalesRecords(effectiveCustomerId, salesAgreement, salesOrder);

        var entity = new ProjectCustomerVariation
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectUnitId = unit?.Id,
            CustomerBusinessPartnerId = effectiveCustomerId,
            SalesAgreementId = salesAgreement?.Id,
            SalesOrderId = salesOrder?.Id,
            JobCardId = jobCard?.Id,
            WorkOrderId = workOrder?.Id,
            Title = dto.Title.Trim(),
            Description = TrimOrNull(dto.Description),
            VariationType = TrimOrNull(dto.VariationType) ?? "Alteration",
            Timing = NormalizeProjectCustomerVariationTiming(dto.Timing),
            Status = NormalizeProjectCustomerVariationStatus(dto.Status),
            RequestDate = dto.RequestDate ?? DateTime.UtcNow,
            TargetCompletionDate = dto.TargetCompletionDate,
            CompletedDate = dto.CompletedDate,
            EstimatedAmount = dto.EstimatedAmount,
            QuotedAmount = dto.QuotedAmount,
            ApprovedAmount = dto.ApprovedAmount,
            BilledAmount = dto.BilledAmount,
            Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? unit?.Currency),
            RequiresScheduleAdjustment = dto.RequiresScheduleAdjustment,
            ScheduleImpactDays = dto.ScheduleImpactDays,
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectCustomerVariation>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectCustomerVariationDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectCustomerVariationDto> UpdateCustomerVariationAsync(Guid variationId, UpdateProjectCustomerVariationDto dto)
    {
        var entity = await GetProjectCustomerVariationEntityAsync(variationId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        var unit = await ValidateProjectCustomerVariationAsync(entity.ProjectId, dto.ProjectUnitId, dto.CustomerBusinessPartnerId);
        var effectiveCustomerId = dto.CustomerBusinessPartnerId ?? unit?.CustomerBusinessPartnerId;
        var salesAgreement = await EnsureTenantSalesAgreementExistsAsync(dto.SalesAgreementId ?? unit?.SalesAgreementId);
        var salesOrder = await EnsureTenantSalesOrderExistsAsync(dto.SalesOrderId ?? unit?.SalesOrderId);
        var jobCard = await EnsureTenantJobCardExistsAsync(dto.JobCardId);
        var workOrder = await EnsureTenantWorkOrderExistsAsync(dto.WorkOrderId);
        ValidateCustomerLinkedSalesRecords(effectiveCustomerId, salesAgreement, salesOrder);

        entity.ProjectUnitId = unit?.Id;
        entity.CustomerBusinessPartnerId = effectiveCustomerId;
        entity.SalesAgreementId = salesAgreement?.Id;
        entity.SalesOrderId = salesOrder?.Id;
        entity.JobCardId = jobCard?.Id;
        entity.WorkOrderId = workOrder?.Id;
        entity.Title = dto.Title.Trim();
        entity.Description = TrimOrNull(dto.Description);
        entity.VariationType = TrimOrNull(dto.VariationType) ?? "Alteration";
        entity.Timing = NormalizeProjectCustomerVariationTiming(dto.Timing);
        entity.Status = NormalizeProjectCustomerVariationStatus(dto.Status);
        entity.RequestDate = dto.RequestDate ?? entity.RequestDate;
        entity.TargetCompletionDate = dto.TargetCompletionDate;
        entity.CompletedDate = dto.CompletedDate;
        entity.EstimatedAmount = dto.EstimatedAmount;
        entity.QuotedAmount = dto.QuotedAmount;
        entity.ApprovedAmount = dto.ApprovedAmount;
        entity.BilledAmount = dto.BilledAmount;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? unit?.Currency ?? entity.Currency);
        entity.RequiresScheduleAdjustment = dto.RequiresScheduleAdjustment;
        entity.ScheduleImpactDays = dto.ScheduleImpactDays;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectCustomerVariation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectCustomerVariationDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteCustomerVariationAsync(Guid variationId)
    {
        var entity = await GetProjectCustomerVariationEntityAsync(variationId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await _unitOfWork.Repository<ProjectCustomerVariation>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<List<ProjectUnit>> GetProjectUnitEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectUnit>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<List<ProjectCustomerVariation>> GetProjectCustomerVariationEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectCustomerVariation>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.RequestDate)
            .ThenBy(x => x.Title)
            .ToList();
    }

    private async Task<ProjectUnit> GetProjectUnitEntityAsync(Guid unitId)
        => await _unitOfWork.Repository<ProjectUnit>().FirstOrDefaultAsync(x =>
               x.Id == unitId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project unit with ID {unitId} not found");

    private async Task<ProjectCustomerVariation> GetProjectCustomerVariationEntityAsync(Guid variationId)
        => await _unitOfWork.Repository<ProjectCustomerVariation>().FirstOrDefaultAsync(x =>
               x.Id == variationId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project customer variation with ID {variationId} not found");

    private async Task<ProjectUnitDto> GetProjectUnitDtoAsync(Guid projectId, Guid unitId)
        => (await MapProjectUnitsAsync((await GetProjectUnitEntitiesAsync(projectId)).ToList()))
            .Single(x => x.Id == unitId);

    private async Task<ProjectCustomerVariationDto> GetProjectCustomerVariationDtoAsync(Guid projectId, Guid variationId)
    {
        var units = (await GetProjectUnitEntitiesAsync(projectId)).ToList();
        return (await MapProjectCustomerVariationsAsync(projectId, (await GetProjectCustomerVariationEntitiesAsync(projectId)).ToList(), units))
            .Single(x => x.Id == variationId);
    }

    private async Task<List<ProjectUnitDto>> MapProjectUnitsAsync(IReadOnlyCollection<ProjectUnit> units)
    {
        var unitIds = units.Select(x => x.Id).ToList();
        var unitTypeTemplateIds = units
            .Where(x => x.ProjectUnitTypeTemplateId.HasValue)
            .Select(x => x.ProjectUnitTypeTemplateId!.Value)
            .Distinct()
            .ToList();
        var unitTypeTemplateLookup = unitTypeTemplateIds.Count == 0
            ? new Dictionary<Guid, ProjectUnitTypeTemplate>()
            : (await _unitOfWork.Repository<ProjectUnitTypeTemplate>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && unitTypeTemplateIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);
        var amenityLookup = unitIds.Count == 0
            ? new Dictionary<Guid, List<ProjectUnitAmenity>>()
            : (await _unitOfWork.Repository<ProjectUnitAmenity>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && unitIds.Contains(x.ProjectUnitId)))
                .GroupBy(x => x.ProjectUnitId)
                .ToDictionary(group => group.Key, group => group.OrderBy(x => x.SortOrder).ThenBy(x => x.AmenityName).ToList());
        var customerLookup = await GetBusinessPartnerLookupAsync(units
            .Where(x => x.CustomerBusinessPartnerId.HasValue)
            .Select(x => x.CustomerBusinessPartnerId!.Value)
            .Distinct());
        var salesAgreementLookup = await GetSalesAgreementLookupAsync(units
            .Where(x => x.SalesAgreementId.HasValue)
            .Select(x => x.SalesAgreementId!.Value)
            .Distinct());
        var salesOrderLookup = await GetSalesOrderLookupAsync(units
            .Where(x => x.SalesOrderId.HasValue)
            .Select(x => x.SalesOrderId!.Value)
            .Distinct());
        var buildingLookup = (await GetBusinessHierarchyBuildingLookupAsync(units
                .Where(x => x.ProjectBuildingId.HasValue)
                .Select(x => x.ProjectBuildingId!.Value)
                .Distinct()))
            .ToDictionary(x => x.Id);
        var floorLookup = (await GetBusinessHierarchyFloorLookupAsync(units
                .Where(x => x.ProjectFloorId.HasValue)
                .Select(x => x.ProjectFloorId!.Value)
                .Distinct()))
            .ToDictionary(x => x.Id);
        var releaseBatchLookup = (await GetProjectUnitReleaseBatchLookupAsync(units
                .Where(x => x.ProjectUnitReleaseBatchId.HasValue)
                .Select(x => x.ProjectUnitReleaseBatchId!.Value)
                .Distinct()))
            .ToDictionary(x => x.Id);
        var releaseUsers = (await _userService.GetUsersByIdsAsync(units
                .Where(x => x.ReleasedById.HasValue)
                .Select(x => x.ReleasedById!.Value)
                .Distinct()))
            .ToDictionary(x => x.Id);

        return units
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => MapToDto(x, customerLookup, salesAgreementLookup, salesOrderLookup, buildingLookup, floorLookup, releaseBatchLookup, unitTypeTemplateLookup, amenityLookup, releaseUsers))
            .ToList();
    }

    private static ProjectUnitAmenityDto MapToDto(ProjectUnitAmenity entity) => new()
    {
        Id = entity.Id,
        InventoryItemId = entity.InventoryItemId,
        ItemCode = entity.ItemCode,
        AmenityName = entity.AmenityName,
        Quantity = entity.Quantity,
        UnitCost = entity.UnitCost,
        TotalCost = decimal.Round(entity.Quantity * entity.UnitCost, 2, MidpointRounding.AwayFromZero),
        SortOrder = entity.SortOrder
    };

    private async Task<ProjectUnitTypeTemplate?> ValidateProjectUnitTypeTemplateAsync(Guid? projectUnitTypeTemplateId)
    {
        if (!projectUnitTypeTemplateId.HasValue)
        {
            return null;
        }

        return await _unitOfWork.Repository<ProjectUnitTypeTemplate>().FirstOrDefaultAsync(x =>
                   x.Id == projectUnitTypeTemplateId.Value
                   && x.TenantId == _currentUserProvider.TenantId)
               ?? throw new InvalidOperationException("The selected unit type template was not found.");
    }

    private async Task<List<ProjectUnitAmenity>> ResolveProjectUnitAmenitiesAsync(
        ProjectUnitTypeTemplate? unitTypeTemplate,
        IEnumerable<CreateProjectUnitAmenityDto>? amenityDtos)
    {
        var payload = (amenityDtos ?? Enumerable.Empty<CreateProjectUnitAmenityDto>())
            .Where(x => x.InventoryItemId.HasValue || !string.IsNullOrWhiteSpace(x.AmenityName))
            .ToList();

        if (payload.Count > 0)
        {
            var inventoryIds = payload.Where(x => x.InventoryItemId.HasValue).Select(x => x.InventoryItemId!.Value).Distinct().ToList();
            var inventoryLookup = inventoryIds.Count == 0
                ? new Dictionary<Guid, InventoryItem>()
                : (await _unitOfWork.Repository<InventoryItem>().FindAsync(x =>
                        x.TenantId == _currentUserProvider.TenantId
                        && inventoryIds.Contains(x.Id)))
                    .ToDictionary(x => x.Id);

            return payload.Select((dto, index) =>
            {
                InventoryItem? inventoryItem = null;
                if (dto.InventoryItemId.HasValue && !inventoryLookup.TryGetValue(dto.InventoryItemId.Value, out inventoryItem))
                {
                    throw new InvalidOperationException("One or more selected amenity inventory items could not be found.");
                }

                var quantity = dto.Quantity > 0m ? decimal.Round(dto.Quantity, 2, MidpointRounding.AwayFromZero) : 1m;
                var unitCost = dto.UnitCost > 0m
                    ? decimal.Round(dto.UnitCost, 2, MidpointRounding.AwayFromZero)
                    : ResolveProjectUnitAmenityUnitCost(inventoryItem);

                return new ProjectUnitAmenity
                {
                    TenantId = _currentUserProvider.TenantId,
                    InventoryItemId = dto.InventoryItemId,
                    ItemCode = TrimOrNull(dto.ItemCode) ?? inventoryItem?.ItemCode,
                    AmenityName = (TrimOrNull(dto.AmenityName) ?? inventoryItem?.Name ?? "Amenity").Trim(),
                    Quantity = quantity,
                    UnitCost = unitCost,
                    SortOrder = dto.SortOrder ?? ((index + 1) * 10),
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                };
            }).ToList();
        }

        if (unitTypeTemplate == null)
        {
            return [];
        }

        var templateAmenities = (await _unitOfWork.Repository<ProjectUnitTypeTemplateAmenity>().FindAsync(x =>
                x.ProjectUnitTypeTemplateId == unitTypeTemplate.Id
                && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.AmenityName)
            .ToList();

        return templateAmenities.Select(templateAmenity => new ProjectUnitAmenity
        {
            TenantId = _currentUserProvider.TenantId,
            InventoryItemId = templateAmenity.InventoryItemId,
            ItemCode = templateAmenity.ItemCode,
            AmenityName = templateAmenity.AmenityName,
            Quantity = templateAmenity.Quantity,
            UnitCost = templateAmenity.UnitCost,
            SortOrder = templateAmenity.SortOrder,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        }).ToList();
    }

    private async Task ReplaceProjectUnitAmenitiesAsync(Guid unitId, IReadOnlyCollection<ProjectUnitAmenity> amenities)
    {
        var repository = _unitOfWork.Repository<ProjectUnitAmenity>();
        var existing = (await repository.FindAsync(x =>
                x.ProjectUnitId == unitId
                && x.TenantId == _currentUserProvider.TenantId))
            .ToList();
        if (existing.Count > 0)
        {
            await repository.DeleteRangeAsync(existing);
            await _unitOfWork.SaveChangesAsync();
        }

        if (amenities.Count == 0)
        {
            return;
        }

        foreach (var amenity in amenities)
        {
            amenity.ProjectUnitId = unitId;
            await repository.AddAsync(amenity);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private static decimal CalculateProjectUnitAmenityTotalCost(IEnumerable<ProjectUnitAmenity> amenities)
        => decimal.Round(amenities.Sum(x => x.Quantity * x.UnitCost), 2, MidpointRounding.AwayFromZero);

    private static decimal ResolveProjectUnitAmenityUnitCost(InventoryItem? inventoryItem)
    {
        if (inventoryItem == null)
        {
            return 0m;
        }

        var resolved = inventoryItem.StandardCost > 0m
            ? inventoryItem.StandardCost
            : inventoryItem.AverageCost > 0m
                ? inventoryItem.AverageCost
                : inventoryItem.LastPurchaseCost;
        return decimal.Round(Math.Max(0m, resolved), 2, MidpointRounding.AwayFromZero);
    }

    private async Task<List<ProjectCustomerVariationDto>> MapProjectCustomerVariationsAsync(Guid projectId, IReadOnlyCollection<ProjectCustomerVariation> variations, IReadOnlyCollection<ProjectUnit> units)
    {
        var unitLookup = units.ToDictionary(x => x.Id);
        var customerLookup = await GetBusinessPartnerLookupAsync(
            variations.Where(x => x.CustomerBusinessPartnerId.HasValue).Select(x => x.CustomerBusinessPartnerId!.Value)
                .Concat(units.Where(x => x.CustomerBusinessPartnerId.HasValue).Select(x => x.CustomerBusinessPartnerId!.Value))
                .Distinct());
        var salesAgreementLookup = await GetSalesAgreementLookupAsync(
            variations.Where(x => x.SalesAgreementId.HasValue).Select(x => x.SalesAgreementId!.Value)
                .Concat(units.Where(x => x.SalesAgreementId.HasValue).Select(x => x.SalesAgreementId!.Value))
                .Distinct());
        var salesOrderLookup = await GetSalesOrderLookupAsync(
            variations.Where(x => x.SalesOrderId.HasValue).Select(x => x.SalesOrderId!.Value)
                .Concat(units.Where(x => x.SalesOrderId.HasValue).Select(x => x.SalesOrderId!.Value))
                .Distinct());
        var jobCardLookup = await GetJobCardLookupAsync(variations
            .Where(x => x.JobCardId.HasValue)
            .Select(x => x.JobCardId!.Value)
            .Distinct());
        var workOrderLookup = await GetWorkOrderLookupAsync(variations
            .Where(x => x.WorkOrderId.HasValue)
            .Select(x => x.WorkOrderId!.Value)
            .Distinct());

        return variations
            .OrderByDescending(x => x.RequestDate)
            .ThenBy(x => x.Title)
            .Select(x => MapToDto(
                x,
                x.ProjectUnitId.HasValue && unitLookup.TryGetValue(x.ProjectUnitId.Value, out var unit) ? unit : null,
                customerLookup,
                salesAgreementLookup,
                salesOrderLookup,
                jobCardLookup,
                workOrderLookup))
            .ToList();
    }

    private async Task<Dictionary<Guid, BusinessPartner>> GetBusinessPartnerLookupAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, BusinessPartner>();
        }

        var partners = await _unitOfWork.Repository<BusinessPartner>().FindAsync(x =>
            x.TenantId == _currentUserProvider.TenantId
            && distinctIds.Contains(x.Id));

        return (partners ?? Enumerable.Empty<BusinessPartner>())
            .ToDictionary(x => x.Id);
    }

    private async Task<ProjectUnit?> ValidateProjectCustomerVariationAsync(Guid projectId, Guid? projectUnitId, Guid? customerBusinessPartnerId)
    {
        ProjectUnit? unit = null;
        if (projectUnitId.HasValue)
        {
            unit = await GetProjectUnitEntityAsync(projectUnitId.Value);
            if (unit.ProjectId != projectId)
            {
                throw new InvalidOperationException("The selected unit does not belong to this project.");
            }
        }

        await EnsureTenantBusinessPartnerExistsAsync(customerBusinessPartnerId, "customer");
        return unit;
    }

    private async Task EnsureTenantBusinessPartnerExistsAsync(Guid? businessPartnerId, string label)
    {
        if (!businessPartnerId.HasValue)
        {
            return;
        }

        var partner = await _unitOfWork.Repository<BusinessPartner>().FirstOrDefaultAsync(x =>
            x.Id == businessPartnerId.Value
            && x.TenantId == _currentUserProvider.TenantId);

        if (partner == null)
        {
            throw new InvalidOperationException($"The selected {label} could not be found.");
        }
    }

    private static string NormalizeProjectUnitType(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectUnitTypes.Unit,
            _ => value.Trim()
        };

    private static string NormalizeProjectUnitStatus(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectUnitStatuses.Planned,
            _ => value.Trim()
        };

    private static string NormalizeProjectCustomerVariationTiming(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectCustomerVariationTimings.PreHandover,
            _ => value.Trim()
        };

    private static string NormalizeProjectCustomerVariationStatus(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectCustomerVariationStatuses.Requested,
            _ => value.Trim()
        };

    private static void ValidateCustomerLinkedSalesRecords(Guid? customerBusinessPartnerId, SalesAgreement? salesAgreement, SalesOrder? salesOrder)
    {
        if (customerBusinessPartnerId.HasValue && salesAgreement != null && salesAgreement.BusinessPartnerId != customerBusinessPartnerId.Value)
        {
            throw new InvalidOperationException("The selected sales agreement does not belong to the selected customer.");
        }

        if (customerBusinessPartnerId.HasValue && salesOrder != null && salesOrder.BusinessPartnerId != customerBusinessPartnerId.Value)
        {
            throw new InvalidOperationException("The selected sales order does not belong to the selected customer.");
        }
    }

    private static void ValidateProjectUnitCommercialControls(
        bool isReleasedForMarket,
        string status,
        DateTime? handoverDate,
        SalesAgreement? salesAgreement,
        SalesOrder? salesOrder)
    {
        if (!isReleasedForMarket && salesOrder != null)
        {
            throw new InvalidOperationException("Release the unit before linking a sales order.");
        }

        if (ProjectUnitStatusEquals(status, ProjectUnitStatuses.Reserved)
            && !isReleasedForMarket
            && salesAgreement == null
            && salesOrder == null)
        {
            throw new InvalidOperationException("Reserved units must be linked to a sales or lease record.");
        }

        if (ProjectUnitStatusEquals(status, ProjectUnitStatuses.Sold) && salesOrder == null)
        {
            throw new InvalidOperationException("Sold units must be linked to a sales order.");
        }

        if (ProjectUnitStatusEquals(status, ProjectUnitStatuses.Leased)
            && salesOrder == null
            && (salesAgreement == null || !IsLeaseAgreementType(salesAgreement.AgreementType)))
        {
            throw new InvalidOperationException("Leased units must be linked to a lease or tenancy agreement, or to a lease sales order.");
        }

        if ((ProjectUnitStatusEquals(status, ProjectUnitStatuses.HandedOver) || ProjectUnitStatusEquals(status, ProjectUnitStatuses.Occupied))
            && !handoverDate.HasValue)
        {
            throw new InvalidOperationException("Set a handover date before marking a unit as handed over or occupied.");
        }
    }

    private static void ValidateProjectUnitReleaseTransition(ProjectUnit entity, bool targetReleasedState)
    {
        if (targetReleasedState || !entity.IsReleasedForMarket)
        {
            return;
        }

        if (entity.SalesAgreementId.HasValue || entity.SalesOrderId.HasValue)
        {
            throw new InvalidOperationException("Cannot withdraw market release while the unit is linked to sales records.");
        }

        if (ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.Reserved)
            || ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.Sold)
            || ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.Leased)
            || ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.HandedOver)
            || ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.Occupied))
        {
            throw new InvalidOperationException("Only unallocated available units can be withdrawn from market release.");
        }
    }

    private static string AlignProjectUnitStatusForRelease(string? status, bool isReleasedForMarket)
    {
        var normalizedStatus = NormalizeProjectUnitStatus(status);
        if (isReleasedForMarket && ProjectUnitStatusEquals(normalizedStatus, ProjectUnitStatuses.Planned))
        {
            return ProjectUnitStatuses.Available;
        }

        if (!isReleasedForMarket && ProjectUnitStatusEquals(normalizedStatus, ProjectUnitStatuses.Available))
        {
            return ProjectUnitStatuses.Planned;
        }

        return normalizedStatus;
    }

    private static ProjectUnitDto MapToDto(
        ProjectUnit entity,
        IReadOnlyDictionary<Guid, BusinessPartner> customerLookup,
        IReadOnlyDictionary<Guid, SalesAgreement> salesAgreementLookup,
        IReadOnlyDictionary<Guid, SalesOrder> salesOrderLookup,
        IReadOnlyDictionary<Guid, ProjectBuilding> buildingLookup,
        IReadOnlyDictionary<Guid, ProjectFloor> floorLookup,
        IReadOnlyDictionary<Guid, ProjectUnitReleaseBatch> releaseBatchLookup,
        IReadOnlyDictionary<Guid, ProjectUnitTypeTemplate> unitTypeTemplateLookup,
        IReadOnlyDictionary<Guid, List<ProjectUnitAmenity>> amenityLookup,
        IReadOnlyDictionary<Guid, ApplicationUser> releaseUserLookup)
    {
        customerLookup.TryGetValue(entity.CustomerBusinessPartnerId ?? Guid.Empty, out var customer);
        salesAgreementLookup.TryGetValue(entity.SalesAgreementId ?? Guid.Empty, out var salesAgreement);
        salesOrderLookup.TryGetValue(entity.SalesOrderId ?? Guid.Empty, out var salesOrder);
        buildingLookup.TryGetValue(entity.ProjectBuildingId ?? Guid.Empty, out var building);
        floorLookup.TryGetValue(entity.ProjectFloorId ?? Guid.Empty, out var floor);
        releaseBatchLookup.TryGetValue(entity.ProjectUnitReleaseBatchId ?? Guid.Empty, out var releaseBatch);
        unitTypeTemplateLookup.TryGetValue(entity.ProjectUnitTypeTemplateId ?? Guid.Empty, out var unitTypeTemplate);

        var commercialStatus = DeriveProjectUnitCommercialStatus(entity, salesAgreement, salesOrder);
        var commercialIntent = DeriveProjectUnitCommercialIntent(salesAgreement, salesOrder);
        var handoverStatus = DeriveProjectUnitHandoverStatus(entity, commercialStatus);
        var inventoryStatus = DeriveProjectUnitInventoryStatus(entity, commercialStatus, salesOrder);
        var amenityDtos = amenityLookup.TryGetValue(entity.Id, out var amenities)
            ? amenities.Select(MapToDto).ToList()
            : [];

        return new ProjectUnitDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            ProjectBuildingId = entity.ProjectBuildingId,
            ProjectBuildingCode = building?.Code,
            ProjectBuildingName = building?.Name,
            ProjectFloorId = entity.ProjectFloorId,
            ProjectFloorCode = floor?.Code,
            ProjectFloorName = floor?.Name,
            ProjectUnitReleaseBatchId = entity.ProjectUnitReleaseBatchId,
            ProjectUnitReleaseBatchCode = releaseBatch?.Code,
            ProjectUnitReleaseBatchName = releaseBatch?.Name,
            ProjectUnitReleaseBatchStatus = releaseBatch?.Status,
            ProjectUnitTypeTemplateId = entity.ProjectUnitTypeTemplateId,
            ProjectUnitTypeTemplateName = unitTypeTemplate?.Name,
            IsReleasedForMarket = entity.IsReleasedForMarket,
            ReleasedAt = entity.ReleasedAt,
            ReleasedByDisplayName = entity.ReleasedById.HasValue && releaseUserLookup.TryGetValue(entity.ReleasedById.Value, out var releasedBy)
                ? ResolveDisplayName(releasedBy)
                : null,
            CustomerBusinessPartnerId = entity.CustomerBusinessPartnerId,
            CustomerBusinessPartnerName = customer?.PartnerName ?? customer?.PrimaryContactName,
            SalesAgreementId = entity.SalesAgreementId,
            SalesAgreementNumber = salesAgreement?.DocumentNumber,
            SalesAgreementTitle = salesAgreement?.AgreementTitle,
            SalesAgreementType = salesAgreement?.AgreementType.ToString(),
            SalesAgreementStatus = salesAgreement?.AgreementStatus.ToString(),
            SalesOrderId = entity.SalesOrderId,
            SalesOrderNumber = salesOrder?.DocumentNumber,
            SalesOrderStatus = salesOrder?.OrderStatus.ToString(),
            CommercialStatus = commercialStatus,
            CommercialIntent = commercialIntent,
            InventoryStatus = inventoryStatus,
            HandoverStatus = handoverStatus,
            Code = entity.Code,
            Name = entity.Name,
            UnitType = entity.UnitType,
            Status = entity.Status,
            BlockName = entity.BlockName,
            FloorLabel = entity.FloorLabel,
            AreaSquareMeters = entity.AreaSquareMeters,
            ValuationRate = entity.ValuationRate,
            BasePrice = entity.BasePrice,
            Currency = entity.Currency,
            HandoverDate = entity.HandoverDate,
            SortOrder = entity.SortOrder,
            TotalAmenityCost = amenityDtos.Sum(x => x.TotalCost),
            Notes = entity.Notes,
            Amenities = amenityDtos
        };
    }

    private static string DeriveProjectUnitCommercialStatus(ProjectUnit entity, SalesAgreement? salesAgreement, SalesOrder? salesOrder)
    {
        if (ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.Archived))
        {
            return ProjectUnitStatuses.Archived;
        }

        if (ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.Occupied))
        {
            return ProjectUnitStatuses.Occupied;
        }

        if (ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.HandedOver))
        {
            return ProjectUnitStatuses.HandedOver;
        }

        if (salesOrder != null)
        {
            return salesOrder.OrderStatus switch
            {
                SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyDelivered or SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced or SalesOrderStatus.Closed => ResolveTransactedCommercialStatus(entity.Status, salesOrder.OrderType),
                SalesOrderStatus.PendingApproval or SalesOrderStatus.Draft or SalesOrderStatus.OnHold => ProjectUnitStatuses.Reserved,
                _ => FallbackProjectUnitCommercialStatus(entity, salesAgreement)
            };
        }

        if (ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.Leased))
        {
            return ProjectUnitStatuses.Leased;
        }

        if (ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.Sold))
        {
            return ProjectUnitStatuses.Sold;
        }

        return FallbackProjectUnitCommercialStatus(entity, salesAgreement);
    }

    private static string FallbackProjectUnitCommercialStatus(ProjectUnit entity, SalesAgreement? salesAgreement)
    {
        if (salesAgreement != null)
        {
            if (IsLeaseAgreementType(salesAgreement.AgreementType))
            {
                return salesAgreement.AgreementStatus switch
                {
                    SalesAgreementStatus.Active or SalesAgreementStatus.Expiring or SalesAgreementStatus.Renewed => ProjectUnitStatuses.Leased,
                    SalesAgreementStatus.PendingApproval or SalesAgreementStatus.Draft or SalesAgreementStatus.Suspended => ProjectUnitStatuses.Reserved,
                    _ => AlignProjectUnitStatusForRelease(entity.Status, entity.IsReleasedForMarket)
                };
            }

            return salesAgreement.AgreementStatus switch
                {
                    SalesAgreementStatus.Active or SalesAgreementStatus.PendingApproval or SalesAgreementStatus.Expiring or SalesAgreementStatus.Renewed or SalesAgreementStatus.Suspended => ProjectUnitStatuses.Reserved,
                    _ => AlignProjectUnitStatusForRelease(entity.Status, entity.IsReleasedForMarket)
                };
        }

        return AlignProjectUnitStatusForRelease(entity.Status, entity.IsReleasedForMarket);
    }

    private static string DeriveProjectUnitInventoryStatus(ProjectUnit entity, string commercialStatus, SalesOrder? salesOrder)
    {
        if (!entity.IsReleasedForMarket)
        {
            return ProjectUnitInventoryStatuses.PendingRelease;
        }

        if (salesOrder != null)
        {
            return salesOrder.OrderStatus switch
            {
                SalesOrderStatus.Delivered or SalesOrderStatus.Invoiced or SalesOrderStatus.Closed => ProjectUnitInventoryStatuses.Invoiced,
                _ => ProjectUnitInventoryStatuses.Allocated
            };
        }

        if (ProjectUnitStatusEquals(commercialStatus, ProjectUnitStatuses.Sold))
        {
            return ProjectUnitInventoryStatuses.Invoiced;
        }

        return ProjectUnitInventoryStatuses.Released;
    }

    private static string? DeriveProjectUnitCommercialIntent(SalesAgreement? salesAgreement, SalesOrder? salesOrder)
    {
        if (salesOrder != null)
        {
            return salesOrder.OrderType == SalesOrderType.LeaseAgreement
                ? ProjectUnitCommercialIntents.Lease
                : ProjectUnitCommercialIntents.Sale;
        }

        if (salesAgreement != null)
        {
            return IsLeaseAgreementType(salesAgreement.AgreementType)
                ? ProjectUnitCommercialIntents.Lease
                : ProjectUnitCommercialIntents.Sale;
        }

        return null;
    }

    private static string DeriveProjectUnitHandoverStatus(ProjectUnit entity, string commercialStatus)
    {
        if (ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.Occupied))
        {
            return ProjectUnitHandoverStatuses.Occupied;
        }

        if (ProjectUnitStatusEquals(entity.Status, ProjectUnitStatuses.HandedOver))
        {
            return ProjectUnitHandoverStatuses.HandedOver;
        }

        if (entity.HandoverDate.HasValue)
        {
            return entity.HandoverDate.Value.Date > DateTime.UtcNow.Date
                ? ProjectUnitHandoverStatuses.Scheduled
                : ProjectUnitHandoverStatuses.Due;
        }

        if (ProjectUnitStatusEquals(commercialStatus, ProjectUnitStatuses.Sold)
            || ProjectUnitStatusEquals(commercialStatus, ProjectUnitStatuses.Leased)
            || ProjectUnitStatusEquals(commercialStatus, ProjectUnitStatuses.Reserved))
        {
            return ProjectUnitHandoverStatuses.Pending;
        }

        return ProjectUnitHandoverStatuses.NotScheduled;
    }

    private static string ResolveTransactedCommercialStatus(string? currentStatus, SalesOrderType? orderType = null)
        => orderType == SalesOrderType.LeaseAgreement || ProjectUnitStatusEquals(currentStatus, ProjectUnitStatuses.Leased)
            ? ProjectUnitStatuses.Leased
            : ProjectUnitStatuses.Sold;

    private static bool IsLeaseAgreementType(SalesAgreementType agreementType)
        => agreementType == SalesAgreementType.LeaseAgreement
            || agreementType == SalesAgreementType.TenancyAgreement;

    private static bool ProjectUnitStatusEquals(string? left, string right)
        => string.Equals(left?.Trim(), right, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsTerm(string? value, string term)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string? ResolveDisplayName(ApplicationUser? user)
    {
        if (user == null)
        {
            return null;
        }

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return string.IsNullOrWhiteSpace(user.UserName) ? user.Email : user.UserName;
    }

    private static ProjectCustomerVariationDto MapToDto(
        ProjectCustomerVariation entity,
        ProjectUnit? unit,
        IReadOnlyDictionary<Guid, BusinessPartner> customerLookup,
        IReadOnlyDictionary<Guid, SalesAgreement> salesAgreementLookup,
        IReadOnlyDictionary<Guid, SalesOrder> salesOrderLookup,
        IReadOnlyDictionary<Guid, JobCard> jobCardLookup,
        IReadOnlyDictionary<Guid, WorkOrder> workOrderLookup) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ProjectUnitId = entity.ProjectUnitId,
        ProjectUnitCode = unit?.Code,
        ProjectUnitName = unit?.Name,
        CustomerBusinessPartnerId = entity.CustomerBusinessPartnerId,
        CustomerBusinessPartnerName = entity.CustomerBusinessPartnerId.HasValue && customerLookup.TryGetValue(entity.CustomerBusinessPartnerId.Value, out var customer)
            ? customer.PartnerName ?? customer.PrimaryContactName
            : null,
        SalesAgreementId = entity.SalesAgreementId,
        SalesAgreementNumber = entity.SalesAgreementId.HasValue && salesAgreementLookup.TryGetValue(entity.SalesAgreementId.Value, out var salesAgreement)
            ? salesAgreement.DocumentNumber
            : null,
        SalesAgreementTitle = entity.SalesAgreementId.HasValue && salesAgreementLookup.TryGetValue(entity.SalesAgreementId.Value, out salesAgreement)
            ? salesAgreement.AgreementTitle
            : null,
        SalesOrderId = entity.SalesOrderId,
        SalesOrderNumber = entity.SalesOrderId.HasValue && salesOrderLookup.TryGetValue(entity.SalesOrderId.Value, out var salesOrder)
            ? salesOrder.DocumentNumber
            : null,
        SalesOrderStatus = entity.SalesOrderId.HasValue && salesOrderLookup.TryGetValue(entity.SalesOrderId.Value, out salesOrder)
            ? salesOrder.OrderStatus.ToString()
            : null,
        JobCardId = entity.JobCardId,
        JobCardNumber = entity.JobCardId.HasValue && jobCardLookup.TryGetValue(entity.JobCardId.Value, out var jobCard)
            ? jobCard.JobCardNumber
            : null,
        JobCardTitle = entity.JobCardId.HasValue && jobCardLookup.TryGetValue(entity.JobCardId.Value, out jobCard)
            ? jobCard.Title
            : null,
        WorkOrderId = entity.WorkOrderId,
        WorkOrderNumber = entity.WorkOrderId.HasValue && workOrderLookup.TryGetValue(entity.WorkOrderId.Value, out var workOrder)
            ? workOrder.WorkOrderNumber
            : null,
        WorkOrderTitle = entity.WorkOrderId.HasValue && workOrderLookup.TryGetValue(entity.WorkOrderId.Value, out workOrder)
            ? workOrder.Title
            : null,
        Title = entity.Title,
        Description = entity.Description,
        VariationType = entity.VariationType,
        Timing = entity.Timing,
        Status = entity.Status,
        RequestDate = entity.RequestDate,
        TargetCompletionDate = entity.TargetCompletionDate,
        CompletedDate = entity.CompletedDate,
        EstimatedAmount = entity.EstimatedAmount,
        QuotedAmount = entity.QuotedAmount,
        ApprovedAmount = entity.ApprovedAmount,
        BilledAmount = entity.BilledAmount,
        Currency = entity.Currency,
        RequiresScheduleAdjustment = entity.RequiresScheduleAdjustment,
        ScheduleImpactDays = entity.ScheduleImpactDays,
        Notes = entity.Notes
    };
}
