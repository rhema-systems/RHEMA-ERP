using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
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
                CanCreateSalesAgreement = !x.Unit.SalesAgreementId.HasValue && CanUnitStartSalesHandoff(new ProjectUnit
                {
                    IsReleasedForMarket = x.Unit.IsReleasedForMarket,
                    CustomerBusinessPartnerId = x.Unit.CustomerBusinessPartnerId,
                    Status = x.Unit.Status
                }),
                CanCreateLeaseAgreement = !x.Unit.SalesAgreementId.HasValue && !x.Unit.SalesOrderId.HasValue && CanUnitStartSalesHandoff(new ProjectUnit
                {
                    IsReleasedForMarket = x.Unit.IsReleasedForMarket,
                    CustomerBusinessPartnerId = x.Unit.CustomerBusinessPartnerId,
                    Status = x.Unit.Status
                }),
                CanCreateSalesOrder = !x.Unit.SalesOrderId.HasValue && CanUnitStartSalesHandoff(new ProjectUnit
                {
                    IsReleasedForMarket = x.Unit.IsReleasedForMarket,
                    CustomerBusinessPartnerId = x.Unit.CustomerBusinessPartnerId,
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
        var (building, floor) = await ResolveProjectHierarchyAsync(projectId, dto.ProjectBuildingId, dto.ProjectFloorId);
        var releaseBatch = await ValidateProjectUnitReleaseBatchAsync(projectId, dto.ProjectUnitReleaseBatchId, building?.Id, floor?.Id);
        var isReleasedForMarket = dto.IsReleasedForMarket || salesAgreement != null || salesOrder != null;
        var normalizedStatus = NormalizeProjectUnitStatus(dto.Status);
        ValidateProjectUnitCommercialControls(isReleasedForMarket, normalizedStatus, dto.HandoverDate, salesAgreement, salesOrder);
        var effectiveStatus = AlignProjectUnitStatusForRelease(normalizedStatus, isReleasedForMarket);

        var siblings = (await GetProjectUnitEntitiesAsync(projectId)).ToList();
        var entity = new ProjectUnit
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectBuildingId = building?.Id,
            ProjectFloorId = floor?.Id,
            ProjectUnitReleaseBatchId = releaseBatch?.Id,
            IsReleasedForMarket = isReleasedForMarket,
            ReleasedAt = isReleasedForMarket ? DateTime.UtcNow : null,
            ReleasedById = isReleasedForMarket ? _currentUserProvider.UserId : null,
            CustomerBusinessPartnerId = dto.CustomerBusinessPartnerId,
            SalesAgreementId = salesAgreement?.Id,
            SalesOrderId = salesOrder?.Id,
            Code = TrimOrNull(dto.Code),
            Name = dto.Name.Trim(),
            UnitType = NormalizeProjectUnitType(dto.UnitType),
            Status = effectiveStatus,
            BlockName = building?.Name ?? TrimOrNull(dto.BlockName),
            FloorLabel = floor?.Name ?? TrimOrNull(dto.FloorLabel),
            AreaSquareMeters = dto.AreaSquareMeters,
            ValuationRate = dto.ValuationRate,
            BasePrice = dto.BasePrice,
            Currency = await ResolveProjectCurrencyAsync(dto.Currency),
            HandoverDate = dto.HandoverDate,
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectUnit>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
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
        var (building, floor) = await ResolveProjectHierarchyAsync(entity.ProjectId, dto.ProjectBuildingId, dto.ProjectFloorId);
        var releaseBatch = await ValidateProjectUnitReleaseBatchAsync(entity.ProjectId, dto.ProjectUnitReleaseBatchId, building?.Id, floor?.Id);
        var isReleasedForMarket = dto.IsReleasedForMarket || salesAgreement != null || salesOrder != null;
        var normalizedStatus = NormalizeProjectUnitStatus(dto.Status);
        ValidateProjectUnitCommercialControls(isReleasedForMarket, normalizedStatus, dto.HandoverDate, salesAgreement, salesOrder);
        var effectiveStatus = AlignProjectUnitStatusForRelease(normalizedStatus, isReleasedForMarket);

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
        entity.CustomerBusinessPartnerId = dto.CustomerBusinessPartnerId;
        entity.SalesAgreementId = salesAgreement?.Id;
        entity.SalesOrderId = salesOrder?.Id;
        entity.Code = TrimOrNull(dto.Code);
        entity.Name = dto.Name.Trim();
        entity.UnitType = NormalizeProjectUnitType(dto.UnitType);
        entity.Status = effectiveStatus;
        entity.BlockName = building?.Name ?? TrimOrNull(dto.BlockName);
        entity.FloorLabel = floor?.Name ?? TrimOrNull(dto.FloorLabel);
        entity.AreaSquareMeters = dto.AreaSquareMeters;
        entity.ValuationRate = dto.ValuationRate;
        entity.BasePrice = dto.BasePrice;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? entity.Currency);
        entity.HandoverDate = dto.HandoverDate;
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectUnit>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
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

    public async Task DeleteProjectUnitAsync(Guid unitId)
    {
        var entity = await GetProjectUnitEntityAsync(unitId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
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
            .Select(x => MapToDto(x, customerLookup, salesAgreementLookup, salesOrderLookup, buildingLookup, floorLookup, releaseBatchLookup, releaseUsers))
            .ToList();
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
        if (!isReleasedForMarket && salesAgreement != null)
        {
            throw new InvalidOperationException("Release the unit before linking a sales agreement.");
        }

        if (!isReleasedForMarket && salesOrder != null)
        {
            throw new InvalidOperationException("Release the unit before linking a sales order.");
        }

        if (ProjectUnitStatusEquals(status, ProjectUnitStatuses.Reserved) && !isReleasedForMarket)
        {
            throw new InvalidOperationException("Only released units can be marked as reserved.");
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
        IReadOnlyDictionary<Guid, ApplicationUser> releaseUserLookup)
    {
        customerLookup.TryGetValue(entity.CustomerBusinessPartnerId ?? Guid.Empty, out var customer);
        salesAgreementLookup.TryGetValue(entity.SalesAgreementId ?? Guid.Empty, out var salesAgreement);
        salesOrderLookup.TryGetValue(entity.SalesOrderId ?? Guid.Empty, out var salesOrder);
        buildingLookup.TryGetValue(entity.ProjectBuildingId ?? Guid.Empty, out var building);
        floorLookup.TryGetValue(entity.ProjectFloorId ?? Guid.Empty, out var floor);
        releaseBatchLookup.TryGetValue(entity.ProjectUnitReleaseBatchId ?? Guid.Empty, out var releaseBatch);

        var commercialStatus = DeriveProjectUnitCommercialStatus(entity, salesAgreement, salesOrder);
        var commercialIntent = DeriveProjectUnitCommercialIntent(salesAgreement, salesOrder);
        var handoverStatus = DeriveProjectUnitHandoverStatus(entity, commercialStatus);

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
            Notes = entity.Notes
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
