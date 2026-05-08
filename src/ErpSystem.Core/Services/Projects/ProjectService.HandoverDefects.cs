using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<IEnumerable<ProjectCommissioningItemDto>> GetProjectCommissioningItemsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectCommissioningItemsAsync((await GetProjectCommissioningItemEntitiesAsync(projectId)).ToList(), (await GetProjectUnitEntitiesAsync(projectId)).ToList());
    }

    public async Task<ProjectCommissioningItemDto> AddProjectCommissioningItemAsync(Guid projectId, CreateProjectCommissioningItemDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        var unit = await ValidateProjectCloseoutUnitAsync(projectId, dto.ProjectUnitId);
        var siblings = (await GetProjectCommissioningItemEntitiesAsync(projectId)).ToList();

        var entity = new ProjectCommissioningItem
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectUnitId = unit?.Id,
            Title = dto.Title.Trim(),
            SystemArea = TrimOrNull(dto.SystemArea),
            Status = NormalizeProjectCommissioningItemStatus(dto.Status),
            RequiresRegulatoryInspection = dto.RequiresRegulatoryInspection,
            PlannedDate = dto.PlannedDate,
            CompletedDate = dto.CompletedDate,
            CertificateReference = TrimOrNull(dto.CertificateReference),
            ResponsibleParty = TrimOrNull(dto.ResponsibleParty),
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectCommissioningItem>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectCommissioningItemDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectCommissioningItemDto> UpdateProjectCommissioningItemAsync(Guid commissioningItemId, UpdateProjectCommissioningItemDto dto)
    {
        var entity = await GetProjectCommissioningItemEntityAsync(commissioningItemId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        var unit = await ValidateProjectCloseoutUnitAsync(entity.ProjectId, dto.ProjectUnitId);

        entity.ProjectUnitId = unit?.Id;
        entity.Title = dto.Title.Trim();
        entity.SystemArea = TrimOrNull(dto.SystemArea);
        entity.Status = NormalizeProjectCommissioningItemStatus(dto.Status);
        entity.RequiresRegulatoryInspection = dto.RequiresRegulatoryInspection;
        entity.PlannedDate = dto.PlannedDate;
        entity.CompletedDate = dto.CompletedDate;
        entity.CertificateReference = TrimOrNull(dto.CertificateReference);
        entity.ResponsibleParty = TrimOrNull(dto.ResponsibleParty);
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectCommissioningItem>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectCommissioningItemDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectCommissioningItemAsync(Guid commissioningItemId)
    {
        var entity = await GetProjectCommissioningItemEntityAsync(commissioningItemId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        await _unitOfWork.Repository<ProjectCommissioningItem>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectHandoverItemDto>> GetProjectHandoverItemsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectHandoverItemsAsync((await GetProjectHandoverItemEntitiesAsync(projectId)).ToList(), (await GetProjectUnitEntitiesAsync(projectId)).ToList());
    }

    public async Task<ProjectHandoverItemDto> AddProjectHandoverItemAsync(Guid projectId, CreateProjectHandoverItemDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var unit = await ValidateProjectCloseoutUnitAsync(projectId, dto.ProjectUnitId);
        var handoverBatch = await ValidateProjectUnitHandoverBatchAsync(projectId, dto.ProjectUnitHandoverBatchId, unit?.Id);
        var siblings = (await GetProjectHandoverItemEntitiesAsync(projectId)).ToList();

        var entity = new ProjectHandoverItem
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectUnitId = unit?.Id,
            ProjectUnitHandoverBatchId = handoverBatch?.Id,
            HandoverType = NormalizeProjectHandoverItemType(dto.HandoverType),
            Title = dto.Title.Trim(),
            Status = NormalizeProjectHandoverItemStatus(dto.Status),
            ResponsibleParty = TrimOrNull(dto.ResponsibleParty),
            ReferenceNumber = TrimOrNull(dto.ReferenceNumber),
            TargetDate = dto.TargetDate,
            CompletedDate = dto.CompletedDate,
            SortOrder = dto.SortOrder ?? (siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectHandoverItem>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectHandoverItemDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectHandoverItemDto> UpdateProjectHandoverItemAsync(Guid handoverItemId, UpdateProjectHandoverItemDto dto)
    {
        var entity = await GetProjectHandoverItemEntityAsync(handoverItemId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        var unit = await ValidateProjectCloseoutUnitAsync(entity.ProjectId, dto.ProjectUnitId);
        var handoverBatch = await ValidateProjectUnitHandoverBatchAsync(entity.ProjectId, dto.ProjectUnitHandoverBatchId, unit?.Id);

        entity.ProjectUnitId = unit?.Id;
        entity.ProjectUnitHandoverBatchId = handoverBatch?.Id;
        entity.HandoverType = NormalizeProjectHandoverItemType(dto.HandoverType);
        entity.Title = dto.Title.Trim();
        entity.Status = NormalizeProjectHandoverItemStatus(dto.Status);
        entity.ResponsibleParty = TrimOrNull(dto.ResponsibleParty);
        entity.ReferenceNumber = TrimOrNull(dto.ReferenceNumber);
        entity.TargetDate = dto.TargetDate;
        entity.CompletedDate = dto.CompletedDate;
        entity.SortOrder = dto.SortOrder ?? entity.SortOrder;
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectHandoverItem>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectHandoverItemDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectHandoverItemAsync(Guid handoverItemId)
    {
        var entity = await GetProjectHandoverItemEntityAsync(handoverItemId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectHandoverItem>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectSnagItemDto>> GetProjectSnagItemsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapProjectSnagItemsAsync((await GetProjectSnagItemEntitiesAsync(projectId)).ToList(), (await GetProjectUnitEntitiesAsync(projectId)).ToList());
    }

    public async Task<ProjectSnagItemDto> AddProjectSnagItemAsync(Guid projectId, CreateProjectSnagItemDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        var unit = await ValidateProjectCloseoutUnitAsync(projectId, dto.ProjectUnitId);

        var entity = new ProjectSnagItem
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectUnitId = unit?.Id,
            Title = dto.Title.Trim(),
            Description = TrimOrNull(dto.Description),
            Severity = NormalizeProjectSnagSeverity(dto.Severity),
            Status = NormalizeProjectSnagStatus(dto.Status),
            ReportedDate = dto.ReportedDate ?? DateTime.UtcNow,
            TargetClosureDate = dto.TargetClosureDate,
            ClosedDate = dto.ClosedDate,
            RaisedByName = TrimOrNull(dto.RaisedByName),
            ResponsibleParty = TrimOrNull(dto.ResponsibleParty),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectSnagItem>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectSnagItemDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectSnagItemDto> UpdateProjectSnagItemAsync(Guid snagItemId, UpdateProjectSnagItemDto dto)
    {
        var entity = await GetProjectSnagItemEntityAsync(snagItemId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        var unit = await ValidateProjectCloseoutUnitAsync(entity.ProjectId, dto.ProjectUnitId);

        entity.ProjectUnitId = unit?.Id;
        entity.Title = dto.Title.Trim();
        entity.Description = TrimOrNull(dto.Description);
        entity.Severity = NormalizeProjectSnagSeverity(dto.Severity);
        entity.Status = NormalizeProjectSnagStatus(dto.Status);
        entity.ReportedDate = dto.ReportedDate ?? entity.ReportedDate;
        entity.TargetClosureDate = dto.TargetClosureDate;
        entity.ClosedDate = dto.ClosedDate;
        entity.RaisedByName = TrimOrNull(dto.RaisedByName);
        entity.ResponsibleParty = TrimOrNull(dto.ResponsibleParty);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectSnagItem>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectSnagItemDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectSnagItemAsync(Guid snagItemId)
    {
        var entity = await GetProjectSnagItemEntityAsync(snagItemId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        await _unitOfWork.Repository<ProjectSnagItem>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectDefectLiabilityCaseDto>> GetProjectDefectLiabilityCasesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var units = (await GetProjectUnitEntitiesAsync(projectId)).ToList();
        return await MapProjectDefectLiabilityCasesAsync((await GetProjectDefectLiabilityCaseEntitiesAsync(projectId)).ToList(), units);
    }

    public async Task<ProjectDefectLiabilityCaseDto> AddProjectDefectLiabilityCaseAsync(Guid projectId, CreateProjectDefectLiabilityCaseDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        var unit = await ValidateProjectCloseoutUnitAsync(projectId, dto.ProjectUnitId);
        await EnsureTenantBusinessPartnerExistsAsync(dto.CustomerBusinessPartnerId, "customer");
        var jobCard = await EnsureTenantJobCardExistsAsync(dto.JobCardId);
        var workOrder = await EnsureTenantWorkOrderExistsAsync(dto.WorkOrderId);

        var entity = new ProjectDefectLiabilityCase
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ProjectUnitId = unit?.Id,
            CustomerBusinessPartnerId = dto.CustomerBusinessPartnerId ?? unit?.CustomerBusinessPartnerId,
            JobCardId = jobCard?.Id,
            WorkOrderId = workOrder?.Id,
            Title = dto.Title.Trim(),
            Description = TrimOrNull(dto.Description),
            Status = NormalizeProjectDefectLiabilityStatus(dto.Status),
            ReportedDate = dto.ReportedDate ?? DateTime.UtcNow,
            TargetResolutionDate = dto.TargetResolutionDate,
            ResolvedDate = dto.ResolvedDate,
            IsWarrantyRelated = dto.IsWarrantyRelated,
            WarrantyCategory = TrimOrNull(dto.WarrantyCategory),
            WarrantyExpiryDate = dto.WarrantyExpiryDate,
            FirstResponseDate = dto.FirstResponseDate,
            ResponseSlaDays = dto.ResponseSlaDays,
            ResolutionSlaDays = dto.ResolutionSlaDays,
            RectificationCost = dto.RectificationCost,
            ChargeableAmount = dto.ChargeableAmount,
            Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? unit?.Currency),
            Notes = TrimOrNull(dto.Notes),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectDefectLiabilityCase>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectDefectLiabilityCaseDtoAsync(projectId, entity.Id);
    }

    public async Task<ProjectDefectLiabilityCaseDto> UpdateProjectDefectLiabilityCaseAsync(Guid defectLiabilityCaseId, UpdateProjectDefectLiabilityCaseDto dto)
    {
        var entity = await GetProjectDefectLiabilityCaseEntityAsync(defectLiabilityCaseId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        var unit = await ValidateProjectCloseoutUnitAsync(entity.ProjectId, dto.ProjectUnitId);
        await EnsureTenantBusinessPartnerExistsAsync(dto.CustomerBusinessPartnerId, "customer");
        var jobCard = await EnsureTenantJobCardExistsAsync(dto.JobCardId);
        var workOrder = await EnsureTenantWorkOrderExistsAsync(dto.WorkOrderId);

        entity.ProjectUnitId = unit?.Id;
        entity.CustomerBusinessPartnerId = dto.CustomerBusinessPartnerId ?? unit?.CustomerBusinessPartnerId;
        entity.JobCardId = jobCard?.Id;
        entity.WorkOrderId = workOrder?.Id;
        entity.Title = dto.Title.Trim();
        entity.Description = TrimOrNull(dto.Description);
        entity.Status = NormalizeProjectDefectLiabilityStatus(dto.Status);
        entity.ReportedDate = dto.ReportedDate ?? entity.ReportedDate;
        entity.TargetResolutionDate = dto.TargetResolutionDate;
        entity.ResolvedDate = dto.ResolvedDate;
        entity.IsWarrantyRelated = dto.IsWarrantyRelated;
        entity.WarrantyCategory = TrimOrNull(dto.WarrantyCategory);
        entity.WarrantyExpiryDate = dto.WarrantyExpiryDate;
        entity.FirstResponseDate = dto.FirstResponseDate;
        entity.ResponseSlaDays = dto.ResponseSlaDays;
        entity.ResolutionSlaDays = dto.ResolutionSlaDays;
        entity.RectificationCost = dto.RectificationCost;
        entity.ChargeableAmount = dto.ChargeableAmount;
        entity.Currency = await ResolveProjectCurrencyAsync(dto.Currency ?? unit?.Currency ?? entity.Currency);
        entity.Notes = TrimOrNull(dto.Notes);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectDefectLiabilityCase>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return await GetProjectDefectLiabilityCaseDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task DeleteProjectDefectLiabilityCaseAsync(Guid defectLiabilityCaseId)
    {
        var entity = await GetProjectDefectLiabilityCaseEntityAsync(defectLiabilityCaseId);
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        await _unitOfWork.Repository<ProjectDefectLiabilityCase>().DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<ProjectUnit?> ValidateProjectCloseoutUnitAsync(Guid projectId, Guid? projectUnitId)
    {
        if (!projectUnitId.HasValue)
        {
            return null;
        }

        var unit = await GetProjectUnitEntityAsync(projectUnitId.Value);
        if (unit.ProjectId != projectId)
        {
            throw new InvalidOperationException("Selected unit must belong to the current project.");
        }

        return unit;
    }

    private async Task<List<ProjectCommissioningItem>> GetProjectCommissioningItemEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectCommissioningItem>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToList();
    }

    private async Task<List<ProjectHandoverItem>> GetProjectHandoverItemEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectHandoverItem>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToList();
    }

    private async Task<List<ProjectSnagItem>> GetProjectSnagItemEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectSnagItem>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.ReportedDate)
            .ThenBy(x => x.Title)
            .ToList();
    }

    private async Task<List<ProjectDefectLiabilityCase>> GetProjectDefectLiabilityCaseEntitiesAsync(Guid projectId)
    {
        var repository = _unitOfWork.Repository<ProjectDefectLiabilityCase>();
        if (repository == null)
        {
            return [];
        }

        return (await repository.FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.ReportedDate)
            .ThenBy(x => x.Title)
            .ToList();
    }

    private async Task<ProjectCommissioningItem> GetProjectCommissioningItemEntityAsync(Guid commissioningItemId)
        => await _unitOfWork.Repository<ProjectCommissioningItem>().FirstOrDefaultAsync(x =>
               x.Id == commissioningItemId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project commissioning item with ID {commissioningItemId} not found");

    private async Task<ProjectHandoverItem> GetProjectHandoverItemEntityAsync(Guid handoverItemId)
        => await _unitOfWork.Repository<ProjectHandoverItem>().FirstOrDefaultAsync(x =>
               x.Id == handoverItemId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project handover item with ID {handoverItemId} not found");

    private async Task<ProjectSnagItem> GetProjectSnagItemEntityAsync(Guid snagItemId)
        => await _unitOfWork.Repository<ProjectSnagItem>().FirstOrDefaultAsync(x =>
               x.Id == snagItemId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project snag item with ID {snagItemId} not found");

    private async Task<ProjectDefectLiabilityCase> GetProjectDefectLiabilityCaseEntityAsync(Guid defectLiabilityCaseId)
        => await _unitOfWork.Repository<ProjectDefectLiabilityCase>().FirstOrDefaultAsync(x =>
               x.Id == defectLiabilityCaseId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException($"Project defect liability case with ID {defectLiabilityCaseId} not found");

    private async Task<ProjectCommissioningItemDto> GetProjectCommissioningItemDtoAsync(Guid projectId, Guid commissioningItemId)
        => (await MapProjectCommissioningItemsAsync((await GetProjectCommissioningItemEntitiesAsync(projectId)).ToList(), (await GetProjectUnitEntitiesAsync(projectId)).ToList()))
            .Single(x => x.Id == commissioningItemId);

    private async Task<ProjectHandoverItemDto> GetProjectHandoverItemDtoAsync(Guid projectId, Guid handoverItemId)
        => (await MapProjectHandoverItemsAsync((await GetProjectHandoverItemEntitiesAsync(projectId)).ToList(), (await GetProjectUnitEntitiesAsync(projectId)).ToList()))
            .Single(x => x.Id == handoverItemId);

    private async Task<ProjectSnagItemDto> GetProjectSnagItemDtoAsync(Guid projectId, Guid snagItemId)
        => (await MapProjectSnagItemsAsync((await GetProjectSnagItemEntitiesAsync(projectId)).ToList(), (await GetProjectUnitEntitiesAsync(projectId)).ToList()))
            .Single(x => x.Id == snagItemId);

    private async Task<ProjectDefectLiabilityCaseDto> GetProjectDefectLiabilityCaseDtoAsync(Guid projectId, Guid defectLiabilityCaseId)
        => (await MapProjectDefectLiabilityCasesAsync((await GetProjectDefectLiabilityCaseEntitiesAsync(projectId)).ToList(), (await GetProjectUnitEntitiesAsync(projectId)).ToList()))
            .Single(x => x.Id == defectLiabilityCaseId);

    private Task<List<ProjectCommissioningItemDto>> MapProjectCommissioningItemsAsync(IReadOnlyCollection<ProjectCommissioningItem> items, IReadOnlyCollection<ProjectUnit> units)
    {
        var unitLookup = units.ToDictionary(x => x.Id);
        return Task.FromResult(items
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .Select(x => MapToDto(
                x,
                x.ProjectUnitId.HasValue && unitLookup.TryGetValue(x.ProjectUnitId.Value, out var unit) ? unit : null))
            .ToList());
    }

    private async Task<List<ProjectHandoverItemDto>> MapProjectHandoverItemsAsync(IReadOnlyCollection<ProjectHandoverItem> items, IReadOnlyCollection<ProjectUnit> units)
    {
        var unitLookup = units.ToDictionary(x => x.Id);
        var batchLookup = (await GetProjectUnitHandoverBatchLookupAsync(items
                .Where(x => x.ProjectUnitHandoverBatchId.HasValue)
                .Select(x => x.ProjectUnitHandoverBatchId!.Value)
                .Distinct()))
            .ToDictionary(x => x.Id);

        return items
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .Select(x => MapToDto(
                x,
                x.ProjectUnitId.HasValue && unitLookup.TryGetValue(x.ProjectUnitId.Value, out var unit) ? unit : null,
                x.ProjectUnitHandoverBatchId.HasValue && batchLookup.TryGetValue(x.ProjectUnitHandoverBatchId.Value, out var batch) ? batch : null))
            .ToList();
    }

    private Task<List<ProjectSnagItemDto>> MapProjectSnagItemsAsync(IReadOnlyCollection<ProjectSnagItem> items, IReadOnlyCollection<ProjectUnit> units)
    {
        var unitLookup = units.ToDictionary(x => x.Id);
        return Task.FromResult(items
            .OrderByDescending(x => x.ReportedDate)
            .ThenBy(x => x.Title)
            .Select(x => MapToDto(
                x,
                x.ProjectUnitId.HasValue && unitLookup.TryGetValue(x.ProjectUnitId.Value, out var unit) ? unit : null))
            .ToList());
    }

    private async Task<List<ProjectDefectLiabilityCaseDto>> MapProjectDefectLiabilityCasesAsync(IReadOnlyCollection<ProjectDefectLiabilityCase> items, IReadOnlyCollection<ProjectUnit> units)
    {
        var unitLookup = units.ToDictionary(x => x.Id);
        var customerLookup = await GetBusinessPartnerLookupAsync(
            items.Where(x => x.CustomerBusinessPartnerId.HasValue).Select(x => x.CustomerBusinessPartnerId!.Value)
                .Concat(units.Where(x => x.CustomerBusinessPartnerId.HasValue).Select(x => x.CustomerBusinessPartnerId!.Value))
                .Distinct());
        var jobCardLookup = await GetJobCardLookupAsync(items
            .Where(x => x.JobCardId.HasValue)
            .Select(x => x.JobCardId!.Value)
            .Distinct());
        var workOrderLookup = await GetWorkOrderLookupAsync(items
            .Where(x => x.WorkOrderId.HasValue)
            .Select(x => x.WorkOrderId!.Value)
            .Distinct());

        return items
            .OrderByDescending(x => x.ReportedDate)
            .ThenBy(x => x.Title)
            .Select(x => MapToDto(
                x,
                x.ProjectUnitId.HasValue && unitLookup.TryGetValue(x.ProjectUnitId.Value, out var unit) ? unit : null,
                customerLookup,
                jobCardLookup,
                workOrderLookup))
            .ToList();
    }

    private static ProjectCommissioningItemDto MapToDto(ProjectCommissioningItem entity, ProjectUnit? unit) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ProjectUnitId = entity.ProjectUnitId,
        ProjectUnitCode = unit?.Code,
        ProjectUnitName = unit?.Name,
        Title = entity.Title,
        SystemArea = entity.SystemArea,
        Status = entity.Status,
        RequiresRegulatoryInspection = entity.RequiresRegulatoryInspection,
        PlannedDate = entity.PlannedDate,
        CompletedDate = entity.CompletedDate,
        CertificateReference = entity.CertificateReference,
        ResponsibleParty = entity.ResponsibleParty,
        SortOrder = entity.SortOrder,
        Notes = entity.Notes
    };

    private static ProjectHandoverItemDto MapToDto(ProjectHandoverItem entity, ProjectUnit? unit, ProjectUnitHandoverBatch? batch) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ProjectUnitId = entity.ProjectUnitId,
        ProjectUnitCode = unit?.Code,
        ProjectUnitName = unit?.Name,
        ProjectUnitHandoverBatchId = entity.ProjectUnitHandoverBatchId,
        ProjectUnitHandoverBatchCode = batch?.Code,
        ProjectUnitHandoverBatchName = batch?.Name,
        ProjectUnitHandoverBatchStatus = batch?.Status,
        HandoverType = entity.HandoverType,
        Title = entity.Title,
        Status = entity.Status,
        ResponsibleParty = entity.ResponsibleParty,
        ReferenceNumber = entity.ReferenceNumber,
        TargetDate = entity.TargetDate,
        CompletedDate = entity.CompletedDate,
        SortOrder = entity.SortOrder,
        Notes = entity.Notes
    };

    private static ProjectSnagItemDto MapToDto(ProjectSnagItem entity, ProjectUnit? unit) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ProjectUnitId = entity.ProjectUnitId,
        ProjectUnitCode = unit?.Code,
        ProjectUnitName = unit?.Name,
        Title = entity.Title,
        Description = entity.Description,
        Severity = entity.Severity,
        Status = entity.Status,
        ReportedDate = entity.ReportedDate,
        TargetClosureDate = entity.TargetClosureDate,
        ClosedDate = entity.ClosedDate,
        RaisedByName = entity.RaisedByName,
        ResponsibleParty = entity.ResponsibleParty,
        Notes = entity.Notes
    };

    private static ProjectDefectLiabilityCaseDto MapToDto(
        ProjectDefectLiabilityCase entity,
        ProjectUnit? unit,
        IReadOnlyDictionary<Guid, BusinessPartner> customerLookup,
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
            ? customer.PartnerName
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
        Status = entity.Status,
        ReportedDate = entity.ReportedDate,
        TargetResolutionDate = entity.TargetResolutionDate,
        ResolvedDate = entity.ResolvedDate,
        IsWarrantyRelated = entity.IsWarrantyRelated,
        WarrantyCategory = entity.WarrantyCategory,
        WarrantyExpiryDate = entity.WarrantyExpiryDate,
        FirstResponseDate = entity.FirstResponseDate,
        ResponseSlaDays = entity.ResponseSlaDays,
        ResolutionSlaDays = entity.ResolutionSlaDays,
        RectificationCost = entity.RectificationCost,
        ChargeableAmount = entity.ChargeableAmount,
        Currency = entity.Currency,
        Notes = entity.Notes
    };

    private static string NormalizeProjectCommissioningItemStatus(string? value)
        => value?.Trim() switch
        {
            ProjectCommissioningItemStatuses.InProgress => ProjectCommissioningItemStatuses.InProgress,
            ProjectCommissioningItemStatuses.ReadyForInspection => ProjectCommissioningItemStatuses.ReadyForInspection,
            ProjectCommissioningItemStatuses.Completed => ProjectCommissioningItemStatuses.Completed,
            ProjectCommissioningItemStatuses.Waived => ProjectCommissioningItemStatuses.Waived,
            _ => ProjectCommissioningItemStatuses.Planned
        };

    private static string NormalizeProjectHandoverItemStatus(string? value)
        => value?.Trim() switch
        {
            ProjectHandoverItemStatuses.InPreparation => ProjectHandoverItemStatuses.InPreparation,
            ProjectHandoverItemStatuses.Ready => ProjectHandoverItemStatuses.Ready,
            ProjectHandoverItemStatuses.Completed => ProjectHandoverItemStatuses.Completed,
            ProjectHandoverItemStatuses.Waived => ProjectHandoverItemStatuses.Waived,
            _ => ProjectHandoverItemStatuses.Planned
        };

    private static string NormalizeProjectHandoverItemType(string? value)
        => value?.Trim() switch
        {
            ProjectHandoverItemTypes.PracticalCompletion => ProjectHandoverItemTypes.PracticalCompletion,
            ProjectHandoverItemTypes.AsBuiltDrawing => ProjectHandoverItemTypes.AsBuiltDrawing,
            ProjectHandoverItemTypes.OperationManual => ProjectHandoverItemTypes.OperationManual,
            ProjectHandoverItemTypes.KeyHandover => ProjectHandoverItemTypes.KeyHandover,
            ProjectHandoverItemTypes.OccupancyCertificate => ProjectHandoverItemTypes.OccupancyCertificate,
            ProjectHandoverItemTypes.FinalCompletion => ProjectHandoverItemTypes.FinalCompletion,
            _ => ProjectHandoverItemTypes.Other
        };

    private static string NormalizeProjectSnagStatus(string? value)
        => value?.Trim() switch
        {
            ProjectSnagStatuses.InProgress => ProjectSnagStatuses.InProgress,
            ProjectSnagStatuses.ReadyForVerification => ProjectSnagStatuses.ReadyForVerification,
            ProjectSnagStatuses.Closed => ProjectSnagStatuses.Closed,
            ProjectSnagStatuses.Waived => ProjectSnagStatuses.Waived,
            _ => ProjectSnagStatuses.Open
        };

    private static string NormalizeProjectSnagSeverity(string? value)
        => value?.Trim() switch
        {
            ProjectSnagSeverities.Low => ProjectSnagSeverities.Low,
            ProjectSnagSeverities.High => ProjectSnagSeverities.High,
            ProjectSnagSeverities.Critical => ProjectSnagSeverities.Critical,
            _ => ProjectSnagSeverities.Medium
        };

    private static string NormalizeProjectDefectLiabilityStatus(string? value)
        => value?.Trim() switch
        {
            ProjectDefectLiabilityStatuses.UnderReview => ProjectDefectLiabilityStatuses.UnderReview,
            ProjectDefectLiabilityStatuses.InProgress => ProjectDefectLiabilityStatuses.InProgress,
            ProjectDefectLiabilityStatuses.Resolved => ProjectDefectLiabilityStatuses.Resolved,
            ProjectDefectLiabilityStatuses.Closed => ProjectDefectLiabilityStatuses.Closed,
            ProjectDefectLiabilityStatuses.WarrantyExpired => ProjectDefectLiabilityStatuses.WarrantyExpired,
            _ => ProjectDefectLiabilityStatuses.Reported
        };
}
