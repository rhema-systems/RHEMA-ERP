using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<IEnumerable<ProjectPackageDto>> GetProjectPackagesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var packages = (await GetProjectPackageEntitiesAsync(projectId)).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList();
        var boqItems = (await GetProjectBoqItemEntitiesAsync(projectId)).ToList();
        return await MapProjectPackagesAsync(projectId, packages, boqItems);
    }

    public async Task<ProjectPackageDto> AddProjectPackageAsync(Guid projectId, CreateProjectPackageDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        await ValidateProjectPackageAsync(projectId, dto);

        var siblings = (await _unitOfWork.Repository<ProjectPackage>().FindAsync(x =>
            x.ProjectId == projectId
            && x.TenantId == _currentUserProvider.TenantId)).ToList();

        var entity = new ProjectPackage
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPhaseId = dto.ProjectPhaseId,
            Code = TrimOrNull(dto.Code),
            Name = dto.Name.Trim(),
            Description = TrimOrNull(dto.Description),
            PackageType = NormalizeProjectPackageType(dto.PackageType),
            Status = NormalizeProjectPackageStatus(dto.Status),
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            ProcurementRoute = TrimOrNull(dto.ProcurementRoute),
            ContractStrategy = TrimOrNull(dto.ContractStrategy),
            BusinessPartnerId = dto.BusinessPartnerId,
            TenderId = dto.TenderId,
            ContractId = dto.ContractId,
            ProcurementPlanItemId = dto.ProcurementPlanItemId,
            PurchaseRequisitionId = dto.PurchaseRequisitionId,
            PurchaseOrderId = dto.PurchaseOrderId,
            BudgetAmount = dto.BudgetAmount,
            CommittedAmount = dto.CommittedAmount,
            ActualAmount = dto.ActualAmount,
            ForecastAmount = dto.ForecastAmount,
            Currency = await ResolveProjectCurrencyAsync(dto.Currency),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectPackage>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectPackageDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectPackageDto> UpdateProjectPackageAsync(Guid packageId, UpdateProjectPackageDto dto)
    {
        var entity = await GetProjectPackageEntityAsync(packageId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);
        await ValidateProjectPackageAsync(entity.ProjectId, dto);

        entity.ProjectPhaseId = dto.ProjectPhaseId;
        entity.Code = TrimOrNull(dto.Code);
        entity.Name = dto.Name.Trim();
        entity.Description = TrimOrNull(dto.Description);
        entity.PackageType = NormalizeProjectPackageType(dto.PackageType);
        entity.Status = NormalizeProjectPackageStatus(dto.Status);
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.ProcurementRoute = TrimOrNull(dto.ProcurementRoute);
        entity.ContractStrategy = TrimOrNull(dto.ContractStrategy);
        entity.BusinessPartnerId = dto.BusinessPartnerId;
        entity.TenderId = dto.TenderId;
        entity.ContractId = dto.ContractId;
        entity.ProcurementPlanItemId = dto.ProcurementPlanItemId;
        entity.PurchaseRequisitionId = dto.PurchaseRequisitionId;
        entity.PurchaseOrderId = dto.PurchaseOrderId;
        entity.BudgetAmount = dto.BudgetAmount;
        entity.CommittedAmount = dto.CommittedAmount;
        entity.ActualAmount = dto.ActualAmount;
        entity.ForecastAmount = dto.ForecastAmount;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectPackage>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectPackageDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectPackageAsync(Guid packageId)
    {
        var entity = await GetProjectPackageEntityAsync(packageId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);

        var boqItems = (await _unitOfWork.Repository<ProjectBoqItem>().FindAsync(x =>
            x.ProjectPackageId == packageId
            && x.TenantId == _currentUserProvider.TenantId)).ToList();

        if (boqItems.Count > 0)
        {
            await _unitOfWork.Repository<ProjectBoqItem>().DeleteRangeAsync(boqItems);
        }

        await _unitOfWork.Repository<ProjectPackage>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectBoqItemDto>> GetProjectBoqItemsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var packages = (await GetProjectPackageEntitiesAsync(projectId)).ToList();
        var packageLookup = packages.ToDictionary(x => x.Id);
        return (await GetProjectBoqItemEntitiesAsync(projectId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LineNumber)
            .Select(x => MapToDto(x, packageLookup.TryGetValue(x.ProjectPackageId, out var package) ? package : null))
            .ToList();
    }

    public async Task<ProjectBoqItemDto> AddProjectBoqItemAsync(Guid projectId, CreateProjectBoqItemDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        var package = await GetProjectPackageEntityAsync(dto.ProjectPackageId);
        if (package.ProjectId != projectId)
        {
            throw new InvalidOperationException("The selected package does not belong to this project.");
        }

        await ValidateProjectBoqItemAsync(projectId, dto);

        var siblings = (await _unitOfWork.Repository<ProjectBoqItem>().FindAsync(x =>
            x.ProjectPackageId == dto.ProjectPackageId
            && x.TenantId == _currentUserProvider.TenantId)).ToList();

        var entity = new ProjectBoqItem
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectPackageId = dto.ProjectPackageId,
            LineNumber = TrimOrNull(dto.LineNumber) ?? (siblings.Count + 1).ToString(),
            ItemCode = TrimOrNull(dto.ItemCode),
            ItemType = NormalizeProjectBoqItemType(dto.ItemType),
            Description = dto.Description.Trim(),
            Quantity = dto.Quantity,
            UnitOfMeasure = TrimOrNull(dto.UnitOfMeasure),
            UnitRate = dto.UnitRate,
            BudgetAmount = ResolveBoqBudgetAmount(dto.BudgetAmount, dto.Quantity, dto.UnitRate),
            CommittedAmount = dto.CommittedAmount,
            ActualAmount = dto.ActualAmount,
            ForecastAmount = dto.ForecastAmount,
            Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? package.Currency),
            InventoryItemId = dto.InventoryItemId,
            TenderItemId = dto.TenderItemId,
            ProcurementPlanItemId = dto.ProcurementPlanItemId,
            PurchaseRequisitionItemId = dto.PurchaseRequisitionItemId,
            PurchaseOrderItemId = dto.PurchaseOrderItemId,
            Notes = TrimOrNull(dto.Notes),
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectBoqItem>().AddAsync(entity);
        await SyncProjectPackageAmountsFromBoqAsync(entity.ProjectPackageId);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity, package);
    }

    public async Task<ProjectBoqItemDto> UpdateProjectBoqItemAsync(Guid boqItemId, UpdateProjectBoqItemDto dto)
    {
        var entity = await GetProjectBoqItemEntityAsync(boqItemId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);

        var package = await GetProjectPackageEntityAsync(dto.ProjectPackageId);
        if (package.ProjectId != entity.ProjectId)
        {
            throw new InvalidOperationException("The selected package does not belong to this project.");
        }

        await ValidateProjectBoqItemAsync(entity.ProjectId, dto);

        var previousPackageId = entity.ProjectPackageId;
        entity.ProjectPackageId = dto.ProjectPackageId;
        entity.LineNumber = TrimOrNull(dto.LineNumber) ?? entity.LineNumber;
        entity.ItemCode = TrimOrNull(dto.ItemCode);
        entity.ItemType = NormalizeProjectBoqItemType(dto.ItemType);
        entity.Description = dto.Description.Trim();
        entity.Quantity = dto.Quantity;
        entity.UnitOfMeasure = TrimOrNull(dto.UnitOfMeasure);
        entity.UnitRate = dto.UnitRate;
        entity.BudgetAmount = ResolveBoqBudgetAmount(dto.BudgetAmount, dto.Quantity, dto.UnitRate);
        entity.CommittedAmount = dto.CommittedAmount;
        entity.ActualAmount = dto.ActualAmount;
        entity.ForecastAmount = dto.ForecastAmount;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? package.Currency);
        entity.InventoryItemId = dto.InventoryItemId;
        entity.TenderItemId = dto.TenderItemId;
        entity.ProcurementPlanItemId = dto.ProcurementPlanItemId;
        entity.PurchaseRequisitionItemId = dto.PurchaseRequisitionItemId;
        entity.PurchaseOrderItemId = dto.PurchaseOrderItemId;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectBoqItem>().UpdateAsync(entity);
        await SyncProjectPackageAmountsFromBoqAsync(entity.ProjectPackageId);
        if (previousPackageId != entity.ProjectPackageId)
        {
            await SyncProjectPackageAmountsFromBoqAsync(previousPackageId);
        }

        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity, package);
    }

    public async Task DeleteProjectBoqItemAsync(Guid boqItemId)
    {
        var entity = await GetProjectBoqItemEntityAsync(boqItemId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);
        var packageId = entity.ProjectPackageId;
        await _unitOfWork.Repository<ProjectBoqItem>().DeleteAsync(entity);
        await SyncProjectPackageAmountsFromBoqAsync(packageId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectTenderLookupDto>> GetTenderLookupAsync(string? search = null)
    {
        EnsureInternalAuthenticatedProjectAccess();
        var normalizedSearch = search?.Trim();
        return (await _unitOfWork.Repository<Tender>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && (string.IsNullOrWhiteSpace(normalizedSearch)
                    || x.TenderNumber.Contains(normalizedSearch)
                    || x.Title.Contains(normalizedSearch))))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new ProjectTenderLookupDto
            {
                Id = x.Id,
                TenderNumber = x.TenderNumber,
                Title = x.Title,
                Status = x.Status,
                Currency = x.Currency,
                EstimatedValue = x.EstimatedValue
            })
            .ToList();
    }

    public async Task<IEnumerable<ProjectProcurementPlanItemLookupDto>> GetProcurementPlanItemLookupAsync(Guid projectId, string? search = null)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var normalizedSearch = search?.Trim();
        var items = (await _unitOfWork.Repository<ProcurementPlanItem>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && (string.IsNullOrWhiteSpace(normalizedSearch)
                    || x.ItemDescription.Contains(normalizedSearch)
                    || (x.ItemCategory != null && x.ItemCategory.Contains(normalizedSearch))
                    || (x.PreferredSupplierName != null && x.PreferredSupplierName.Contains(normalizedSearch)))))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToList();

        var planIds = items.Select(x => x.ProcurementPlanId).Distinct().ToList();
        var planLookup = planIds.Count == 0
            ? new Dictionary<Guid, ProcurementPlan>()
            : (await _unitOfWork.Repository<ProcurementPlan>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && planIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);

        return items.Select(x => new ProjectProcurementPlanItemLookupDto
        {
            Id = x.Id,
            Label = BuildProcurementPlanItemLabel(x, planLookup.TryGetValue(x.ProcurementPlanId, out var plan) ? plan : null),
            ItemDescription = x.ItemDescription,
            Status = x.Status,
            Currency = x.Currency,
            EstimatedTotalCost = x.EstimatedTotalCost
        }).ToList();
    }

    public async Task<IEnumerable<ProjectPurchaseRequisitionLookupDto>> GetPurchaseRequisitionLookupAsync(Guid projectId, string? search = null)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var normalizedSearch = search?.Trim();
        return (await _unitOfWork.Repository<PurchaseRequisition>().FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId
                && (string.IsNullOrWhiteSpace(normalizedSearch)
                    || x.RequisitionNumber.Contains(normalizedSearch)
                    || (x.Notes != null && x.Notes.Contains(normalizedSearch))
                    || (x.Justification != null && x.Justification.Contains(normalizedSearch)))))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new ProjectPurchaseRequisitionLookupDto
            {
                Id = x.Id,
                RequisitionNumber = x.RequisitionNumber,
                Status = x.Status,
                Currency = x.Currency,
                TotalAmount = x.TotalAmount
            })
            .ToList();
    }

    public async Task<IEnumerable<ProjectPurchaseOrderLookupDto>> GetPurchaseOrderLookupAsync(Guid projectId, string? search = null)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var normalizedSearch = search?.Trim();
        var requisitionIds = (await _unitOfWork.Repository<PurchaseRequisition>().FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId))
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        if (requisitionIds.Count == 0)
        {
            return [];
        }

        var orders = (await _unitOfWork.Repository<PurchaseOrder>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.SourceRequisitionId.HasValue
                && requisitionIds.Contains(x.SourceRequisitionId.Value)
                && (string.IsNullOrWhiteSpace(normalizedSearch)
                    || x.OrderNumber.Contains(normalizedSearch)
                    || (x.ReferenceNumber != null && x.ReferenceNumber.Contains(normalizedSearch)))))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToList();

        var partnerIds = orders.Select(x => x.BusinessPartnerId).Distinct().ToList();
        var partnerLookup = partnerIds.Count == 0
            ? new Dictionary<Guid, BusinessPartner>()
            : (await _unitOfWork.Repository<BusinessPartner>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && partnerIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);

        return orders.Select(x => new ProjectPurchaseOrderLookupDto
        {
            Id = x.Id,
            OrderNumber = x.OrderNumber,
            Status = x.Status,
            Currency = x.Currency,
            TotalAmount = x.TotalAmount,
            BusinessPartnerId = x.BusinessPartnerId,
            BusinessPartnerName = partnerLookup.TryGetValue(x.BusinessPartnerId, out var partner)
                ? partner.PartnerName ?? partner.PrimaryContactName
                : null
        }).ToList();
    }

    private async Task ValidateProjectPackageAsync(Guid projectId, CreateProjectPackageDto dto)
    {
        if (dto.ProjectPhaseId.HasValue)
        {
            var phase = await GetProjectPhaseEntityAsync(dto.ProjectPhaseId.Value);
            if (phase.ProjectId != projectId)
            {
                throw new InvalidOperationException("The selected phase does not belong to this project.");
            }
        }

        await EnsureTenantEntityExistsAsync<BusinessPartner>(dto.BusinessPartnerId, "business partner");
        await EnsureTenantEntityExistsAsync<Tender>(dto.TenderId, "tender");
        await EnsureTenantEntityExistsAsync<Contract>(dto.ContractId, "contract");
        await EnsureTenantEntityExistsAsync<ProcurementPlanItem>(dto.ProcurementPlanItemId, "procurement plan item");
        await EnsureTenantEntityExistsAsync<PurchaseRequisition>(dto.PurchaseRequisitionId, "purchase requisition");
        await EnsureTenantEntityExistsAsync<PurchaseOrder>(dto.PurchaseOrderId, "purchase order");
    }

    private async Task ValidateProjectBoqItemAsync(Guid projectId, CreateProjectBoqItemDto dto)
    {
        var package = await GetProjectPackageEntityAsync(dto.ProjectPackageId);
        if (package.ProjectId != projectId)
        {
            throw new InvalidOperationException("The selected package does not belong to this project.");
        }

        await EnsureTenantEntityExistsAsync<InventoryItem>(dto.InventoryItemId, "inventory item");
        await EnsureTenantEntityExistsAsync<TenderItem>(dto.TenderItemId, "tender item");
        await EnsureTenantEntityExistsAsync<ProcurementPlanItem>(dto.ProcurementPlanItemId, "procurement plan item");
        await EnsureTenantEntityExistsAsync<PurchaseRequisitionItem>(dto.PurchaseRequisitionItemId, "purchase requisition item");
        await EnsureTenantEntityExistsAsync<PurchaseOrderItem>(dto.PurchaseOrderItemId, "purchase order item");
    }

    private async Task EnsureTenantEntityExistsAsync<T>(Guid? id, string label) where T : TenantEntity
    {
        if (!id.HasValue)
        {
            return;
        }

        var entity = await _unitOfWork.Repository<T>().FirstOrDefaultAsync(x => x.Id == id.Value && x.TenantId == _currentUserProvider.TenantId);
        if (entity == null)
        {
            throw new InvalidOperationException($"The selected {label} could not be found.");
        }
    }

    private async Task<List<ProjectPackageDto>> MapProjectPackagesAsync(Guid projectId, IReadOnlyCollection<ProjectPackage> packages, IReadOnlyCollection<ProjectBoqItem> boqItems)
    {
        var referenceContext = await BuildProjectPackageReferenceContextAsync(projectId, packages, boqItems);
        var boqByPackage = boqItems
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LineNumber)
            .GroupBy(x => x.ProjectPackageId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => MapToDto(item, referenceContext.PackageLookup.TryGetValue(item.ProjectPackageId, out var package) ? package : null)).ToList());

        return packages
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => MapToDto(
                x,
                referenceContext.PhaseLookup,
                referenceContext.BusinessPartnerLookup,
                referenceContext.TenderLookup,
                referenceContext.ContractLookup,
                referenceContext.ProcurementPlanLookup,
                referenceContext.PurchaseRequisitionLookup,
                referenceContext.PurchaseOrderLookup,
                boqByPackage.TryGetValue(x.Id, out var items) ? items : []))
            .ToList();
    }

    private async Task<ProjectPackageReferenceContext> BuildProjectPackageReferenceContextAsync(Guid projectId, IReadOnlyCollection<ProjectPackage> packages, IReadOnlyCollection<ProjectBoqItem> boqItems)
    {
        var phases = (await GetProjectPhaseEntitiesAsync(projectId)).ToDictionary(x => x.Id);
        var packageLookup = packages.ToDictionary(x => x.Id);

        var businessPartnerIds = packages.Where(x => x.BusinessPartnerId.HasValue).Select(x => x.BusinessPartnerId!.Value).Distinct().ToList();
        var tenderIds = packages.Where(x => x.TenderId.HasValue).Select(x => x.TenderId!.Value).Distinct().ToList();
        var contractIds = packages.Where(x => x.ContractId.HasValue).Select(x => x.ContractId!.Value).Distinct().ToList();
        var procurementPlanItemIds = packages.Where(x => x.ProcurementPlanItemId.HasValue).Select(x => x.ProcurementPlanItemId!.Value)
            .Concat(boqItems.Where(x => x.ProcurementPlanItemId.HasValue).Select(x => x.ProcurementPlanItemId!.Value))
            .Distinct()
            .ToList();
        var purchaseRequisitionIds = packages.Where(x => x.PurchaseRequisitionId.HasValue).Select(x => x.PurchaseRequisitionId!.Value).Distinct().ToList();
        var purchaseOrderIds = packages.Where(x => x.PurchaseOrderId.HasValue).Select(x => x.PurchaseOrderId!.Value).Distinct().ToList();

        var businessPartners = businessPartnerIds.Count == 0
            ? new Dictionary<Guid, BusinessPartner>()
            : (await _unitOfWork.Repository<BusinessPartner>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && businessPartnerIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);
        var tenders = tenderIds.Count == 0
            ? new Dictionary<Guid, Tender>()
            : (await _unitOfWork.Repository<Tender>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && tenderIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);
        var contracts = contractIds.Count == 0
            ? new Dictionary<Guid, Contract>()
            : (await _unitOfWork.Repository<Contract>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && contractIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);
        var procurementPlanItems = procurementPlanItemIds.Count == 0
            ? new Dictionary<Guid, ProcurementPlanItem>()
            : (await _unitOfWork.Repository<ProcurementPlanItem>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && procurementPlanItemIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);
        var purchaseRequisitions = purchaseRequisitionIds.Count == 0
            ? new Dictionary<Guid, PurchaseRequisition>()
            : (await _unitOfWork.Repository<PurchaseRequisition>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && purchaseRequisitionIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);
        var purchaseOrders = purchaseOrderIds.Count == 0
            ? new Dictionary<Guid, PurchaseOrder>()
            : (await _unitOfWork.Repository<PurchaseOrder>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && purchaseOrderIds.Contains(x.Id)))
                .ToDictionary(x => x.Id);

        return new ProjectPackageReferenceContext(
            phases,
            packageLookup,
            businessPartners,
            tenders,
            contracts,
            procurementPlanItems,
            purchaseRequisitions,
            purchaseOrders);
    }

    private async Task<ProjectPackageDto> GetProjectPackageDtoAsync(Guid projectId, Guid packageId)
    {
        var packages = (await GetProjectPackageEntitiesAsync(projectId)).ToList();
        var boqItems = (await GetProjectBoqItemEntitiesAsync(projectId)).ToList();
        return (await MapProjectPackagesAsync(projectId, packages, boqItems))
            .Single(x => x.Id == packageId);
    }

    private async Task<List<ProjectPackage>> GetProjectPackageEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectPackage>();
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

    private async Task<List<ProjectBoqItem>> GetProjectBoqItemEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectBoqItem>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LineNumber)
            .ToList();
    }

    private async Task<ProjectPackage> GetProjectPackageEntityAsync(Guid packageId)
        => await _unitOfWork.Repository<ProjectPackage>().FirstOrDefaultAsync(x =>
               x.Id == packageId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project package with ID {packageId} not found");

    private async Task<ProjectBoqItem> GetProjectBoqItemEntityAsync(Guid boqItemId)
        => await _unitOfWork.Repository<ProjectBoqItem>().FirstOrDefaultAsync(x =>
               x.Id == boqItemId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project BOQ item with ID {boqItemId} not found");

    private async Task SyncProjectPackageAmountsFromBoqAsync(Guid packageId)
    {
        var package = await GetProjectPackageEntityAsync(packageId);
        var items = (await _unitOfWork.Repository<ProjectBoqItem>().FindAsync(x =>
            x.ProjectPackageId == packageId
            && x.TenantId == _currentUserProvider.TenantId)).ToList();

        package.BudgetAmount = items.Sum(x => x.BudgetAmount ?? 0m);
        package.CommittedAmount = items.Sum(x => x.CommittedAmount ?? 0m);
        package.ActualAmount = items.Sum(x => x.ActualAmount ?? 0m);
        package.ForecastAmount = items.Sum(x => x.ForecastAmount ?? 0m);
        package.UpdatedBy = _currentUserProvider.Username;
        package.LastModifiedById = _currentUserProvider.UserId;
        await _unitOfWork.Repository<ProjectPackage>().UpdateAsync(package);
    }

    private static decimal? ResolveBoqBudgetAmount(decimal? requestedBudget, decimal quantity, decimal? unitRate)
        => requestedBudget ?? (unitRate.HasValue ? decimal.Round(quantity * unitRate.Value, 2) : null);

    private static string NormalizeProjectPackageStatus(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectPackageStatuses.Planned,
            _ => value.Trim()
        };

    private static string NormalizeProjectPackageType(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectPackageTypes.WorkPackage,
            _ => value.Trim()
        };

    private static string NormalizeProjectBoqItemType(string? value)
        => value?.Trim() switch
        {
            null or "" => ProjectBoqItemTypes.Item,
            _ => value.Trim()
        };

    private static string BuildProcurementPlanItemLabel(ProcurementPlanItem item, ProcurementPlan? plan)
        => plan == null
            ? item.ItemDescription
            : $"{plan.PlanNumber} · {item.ItemDescription}";

    private static ProjectPackageDto MapToDto(
        ProjectPackage entity,
        IReadOnlyDictionary<Guid, ProjectPhase> phaseLookup,
        IReadOnlyDictionary<Guid, BusinessPartner> businessPartnerLookup,
        IReadOnlyDictionary<Guid, Tender> tenderLookup,
        IReadOnlyDictionary<Guid, Contract> contractLookup,
        IReadOnlyDictionary<Guid, ProcurementPlanItem> procurementPlanLookup,
        IReadOnlyDictionary<Guid, PurchaseRequisition> purchaseRequisitionLookup,
        IReadOnlyDictionary<Guid, PurchaseOrder> purchaseOrderLookup,
        IReadOnlyList<ProjectBoqItemDto> boqItems)
    {
        var phase = entity.ProjectPhaseId.HasValue && phaseLookup.TryGetValue(entity.ProjectPhaseId.Value, out var resolvedPhase)
            ? resolvedPhase
            : null;

        return new ProjectPackageDto
        {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ProjectPhaseId = entity.ProjectPhaseId,
        ProjectPhaseName = phase?.Name,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        PackageType = entity.PackageType,
        Status = entity.Status,
        IsPhaseCommerciallyAligned = phase == null || IsPackageCommerciallyAlignedToPhase(phase, entity),
        PhaseCommercialSyncStatus = ResolvePackagePhaseCommercialSyncStatus(phase, entity),
        PhaseCommercialSyncMessage = BuildPackagePhaseCommercialSyncMessage(phase, entity),
        RecommendedNextStatus = ResolveRecommendedNextStatus(phase, entity),
        RecommendedNextAction = BuildRecommendedPackageNextAction(phase, entity),
        SortOrder = entity.SortOrder,
        ProcurementRoute = entity.ProcurementRoute,
        ContractStrategy = entity.ContractStrategy,
        BusinessPartnerId = entity.BusinessPartnerId,
        BusinessPartnerName = entity.BusinessPartnerId.HasValue && businessPartnerLookup.TryGetValue(entity.BusinessPartnerId.Value, out var partner)
            ? partner.PartnerName ?? partner.PrimaryContactName
            : null,
        TenderId = entity.TenderId,
        TenderNumber = entity.TenderId.HasValue && tenderLookup.TryGetValue(entity.TenderId.Value, out var tender) ? tender.TenderNumber : null,
        TenderTitle = entity.TenderId.HasValue && tenderLookup.TryGetValue(entity.TenderId.Value, out tender) ? tender.Title : null,
        ContractId = entity.ContractId,
        ContractNumber = entity.ContractId.HasValue && contractLookup.TryGetValue(entity.ContractId.Value, out var contract) ? contract.ContractNumber : null,
        ContractTitle = entity.ContractId.HasValue && contractLookup.TryGetValue(entity.ContractId.Value, out contract) ? contract.ContractTitle : null,
        ProcurementPlanItemId = entity.ProcurementPlanItemId,
        ProcurementPlanItemLabel = entity.ProcurementPlanItemId.HasValue && procurementPlanLookup.TryGetValue(entity.ProcurementPlanItemId.Value, out var planItem)
            ? planItem.ItemDescription
            : null,
        PurchaseRequisitionId = entity.PurchaseRequisitionId,
        PurchaseRequisitionNumber = entity.PurchaseRequisitionId.HasValue && purchaseRequisitionLookup.TryGetValue(entity.PurchaseRequisitionId.Value, out var requisition)
            ? requisition.RequisitionNumber
            : null,
        PurchaseOrderId = entity.PurchaseOrderId,
        PurchaseOrderNumber = entity.PurchaseOrderId.HasValue && purchaseOrderLookup.TryGetValue(entity.PurchaseOrderId.Value, out var order)
            ? order.OrderNumber
            : null,
        BudgetAmount = entity.BudgetAmount,
        CommittedAmount = entity.CommittedAmount,
        ActualAmount = entity.ActualAmount,
        ForecastAmount = entity.ForecastAmount,
        Currency = entity.Currency,
        Notes = entity.Notes,
        BoqItems = boqItems.ToList()
        };
    }

    private static ProjectBoqItemDto MapToDto(ProjectBoqItem entity, ProjectPackage? package) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ProjectPackageId = entity.ProjectPackageId,
        PackageCode = package?.Code,
        PackageName = package?.Name,
        LineNumber = entity.LineNumber,
        ItemCode = entity.ItemCode,
        ItemType = entity.ItemType,
        Description = entity.Description,
        Quantity = entity.Quantity,
        UnitOfMeasure = entity.UnitOfMeasure,
        UnitRate = entity.UnitRate,
        BudgetAmount = entity.BudgetAmount,
        CommittedAmount = entity.CommittedAmount,
        ActualAmount = entity.ActualAmount,
        ForecastAmount = entity.ForecastAmount,
        Currency = entity.Currency,
        InventoryItemId = entity.InventoryItemId,
        TenderItemId = entity.TenderItemId,
        ProcurementPlanItemId = entity.ProcurementPlanItemId,
        PurchaseRequisitionItemId = entity.PurchaseRequisitionItemId,
        PurchaseOrderItemId = entity.PurchaseOrderItemId,
        Notes = entity.Notes,
        SortOrder = entity.SortOrder
    };

    private sealed record ProjectPackageReferenceContext(
        IReadOnlyDictionary<Guid, ProjectPhase> PhaseLookup,
        IReadOnlyDictionary<Guid, ProjectPackage> PackageLookup,
        IReadOnlyDictionary<Guid, BusinessPartner> BusinessPartnerLookup,
        IReadOnlyDictionary<Guid, Tender> TenderLookup,
        IReadOnlyDictionary<Guid, Contract> ContractLookup,
        IReadOnlyDictionary<Guid, ProcurementPlanItem> ProcurementPlanLookup,
        IReadOnlyDictionary<Guid, PurchaseRequisition> PurchaseRequisitionLookup,
        IReadOnlyDictionary<Guid, PurchaseOrder> PurchaseOrderLookup);
}
