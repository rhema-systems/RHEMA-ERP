using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<ProjectCustomerVariationDto> CreateJobCardFromCustomerVariationAsync(Guid variationId, CreateProjectMaintenanceFollowThroughDto? dto = null)
    {
        var entity = await GetProjectCustomerVariationEntityAsync(variationId);
        var project = await GetProjectForOperationAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        if (entity.JobCardId.HasValue)
        {
            throw new InvalidOperationException("This customer variation is already linked to a job card.");
        }

        var unit = entity.ProjectUnitId.HasValue ? await GetProjectUnitEntityAsync(entity.ProjectUnitId.Value) : null;
        var maintenanceAsset = await ResolveProjectMaintenanceAssetForActionAsync(entity.ProjectId, dto?.MaintenanceAssetId);
        var maintenanceType = await ResolveProjectMaintenanceTypeForActionAsync(dto?.MaintenanceTypeId, preferredName: "Corrective");
        var priorityLevel = await ResolveProjectPriorityLevelForActionAsync(dto?.PriorityLevelId, preferredName: "Medium");

        var createdJobCard = await _jobCardService.CreateJobCardAsync(new CreateJobCardDto
        {
            Title = $"Variation - {entity.Title}",
            Description = BuildVariationMaintenanceDescription(project, entity, unit),
            ProblemDescription = TrimOrNull(entity.Description) ?? $"Customer variation request for project {project.ProjectCode}.",
            AssetId = maintenanceAsset.Id,
            MaintenanceTypeId = maintenanceType.Id,
            PriorityLevelId = priorityLevel.Id,
            MaintenanceLocation = entity.Timing == "PostHandover" ? "External" : "Internal",
            RequiredCompletionDate = entity.TargetCompletionDate,
            EstimatedHours = maintenanceType.EstimatedHours > 0 ? maintenanceType.EstimatedHours : 4,
            EstimatedCost = EstimateVariationFollowThroughCost(entity),
            PreferredTechnicianId = dto?.AssignedTechnicianId,
            PreferredTeamId = dto?.AssignedTeamId,
            RequiresShutdown = maintenanceType.RequiresShutdown,
            RequiresSafetyPermit = maintenanceType.RequiresSafetyPermit,
            SafetyRequirements = TrimOrNull(maintenanceType.SafetyRequirements),
            SpecialInstructions = BuildVariationMaintenanceInstructions(entity)
        });

        entity.JobCardId = createdJobCard.Id;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectCustomerVariation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return await GetProjectCustomerVariationDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task<ProjectCustomerVariationDto> CreateWorkOrderFromCustomerVariationAsync(Guid variationId, CreateProjectMaintenanceFollowThroughDto? dto = null)
    {
        var entity = await GetProjectCustomerVariationEntityAsync(variationId);
        var project = await GetProjectForOperationAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        if (entity.WorkOrderId.HasValue)
        {
            throw new InvalidOperationException("This customer variation is already linked to a work order.");
        }

        var unit = entity.ProjectUnitId.HasValue ? await GetProjectUnitEntityAsync(entity.ProjectUnitId.Value) : null;
        var linkedJobCard = await EnsureTenantJobCardExistsAsync(entity.JobCardId);
        var maintenanceAsset = await ResolveProjectMaintenanceAssetForActionAsync(entity.ProjectId, dto?.MaintenanceAssetId, linkedJobCard);
        var maintenanceType = await ResolveProjectMaintenanceTypeForActionAsync(dto?.MaintenanceTypeId, preferredName: "Corrective", fallbackId: linkedJobCard?.MaintenanceTypeId);
        var priorityLevel = await ResolveProjectPriorityLevelForActionAsync(dto?.PriorityLevelId, preferredName: "Medium", fallbackId: linkedJobCard?.PriorityLevelId);
        var workOrderType = await ResolveProjectWorkOrderTypeForActionAsync(dto?.WorkOrderTypeId, preferredName: "Standard");
        var billingType = ResolveFollowThroughBillingType(dto?.BillingType, defaultBillingType: "Repairs");
        var estimatedCost = EstimateVariationFollowThroughCost(entity);

        var createdWorkOrder = await _workOrderService.CreateWorkOrderAsync(new CreateWorkOrderDto
        {
            Title = $"Variation - {entity.Title}",
            Description = BuildVariationMaintenanceDescription(project, entity, unit),
            AssetId = maintenanceAsset.Id,
            WorkOrderTypeId = workOrderType.Id,
            MaintenanceTypeId = maintenanceType.Id,
            PriorityLevelId = priorityLevel.Id,
            JobCardId = linkedJobCard?.Id,
            MaintenanceLocation = entity.Timing == "PostHandover" ? "External" : "Internal",
            RequestedCompletionDate = entity.TargetCompletionDate,
            EstimatedCost = estimatedCost,
            EstimatedHours = maintenanceType.EstimatedHours > 0 ? maintenanceType.EstimatedHours : 4,
            AssignedTechnicianId = dto?.AssignedTechnicianId ?? linkedJobCard?.AssignedTechnicianId,
            AssignedTeamId = dto?.AssignedTeamId ?? linkedJobCard?.AssignedTeamId,
            SafetyRequirements = linkedJobCard?.SafetyRequirements ?? TrimOrNull(maintenanceType.SafetyRequirements),
            RequiresPermit = maintenanceType.RequiresSafetyPermit,
            RequiresLockout = maintenanceType.RequiresShutdown,
            BillingType = billingType,
            FixedAmount = billingType == "Maintenance" ? estimatedCost : 0,
            Instructions = BuildVariationMaintenanceInstructions(entity)
        });

        entity.WorkOrderId = createdWorkOrder.Id;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectCustomerVariation>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return await GetProjectCustomerVariationDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task<ProjectDefectLiabilityCaseDto> CreateJobCardFromDefectLiabilityCaseAsync(Guid defectLiabilityCaseId, CreateProjectMaintenanceFollowThroughDto? dto = null)
    {
        var entity = await GetProjectDefectLiabilityCaseEntityAsync(defectLiabilityCaseId);
        var project = await GetProjectForOperationAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        if (entity.JobCardId.HasValue)
        {
            throw new InvalidOperationException("This defect-liability case is already linked to a job card.");
        }

        var unit = entity.ProjectUnitId.HasValue ? await GetProjectUnitEntityAsync(entity.ProjectUnitId.Value) : null;
        var maintenanceAsset = await ResolveProjectMaintenanceAssetForActionAsync(entity.ProjectId, dto?.MaintenanceAssetId);
        var maintenanceType = await ResolveProjectMaintenanceTypeForActionAsync(dto?.MaintenanceTypeId, preferredName: "Corrective");
        var priorityLevel = await ResolveProjectPriorityLevelForActionAsync(dto?.PriorityLevelId, preferredName: "High");

        var createdJobCard = await _jobCardService.CreateJobCardAsync(new CreateJobCardDto
        {
            Title = $"Defect - {entity.Title}",
            Description = BuildDefectMaintenanceDescription(project, entity, unit),
            ProblemDescription = TrimOrNull(entity.Description) ?? $"Defect liability issue reported for project {project.ProjectCode}.",
            AssetId = maintenanceAsset.Id,
            MaintenanceTypeId = maintenanceType.Id,
            PriorityLevelId = priorityLevel.Id,
            MaintenanceLocation = "Internal",
            RequiredCompletionDate = entity.TargetResolutionDate,
            EstimatedHours = maintenanceType.EstimatedHours > 0 ? maintenanceType.EstimatedHours : 2,
            EstimatedCost = EstimateDefectFollowThroughCost(entity),
            PreferredTechnicianId = dto?.AssignedTechnicianId,
            PreferredTeamId = dto?.AssignedTeamId,
            RequiresShutdown = maintenanceType.RequiresShutdown,
            RequiresSafetyPermit = maintenanceType.RequiresSafetyPermit,
            SafetyRequirements = TrimOrNull(maintenanceType.SafetyRequirements),
            SpecialInstructions = BuildDefectMaintenanceInstructions(entity)
        });

        entity.JobCardId = createdJobCard.Id;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectDefectLiabilityCase>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return await GetProjectDefectLiabilityCaseDtoAsync(entity.ProjectId, entity.Id);
    }

    public async Task<ProjectDefectLiabilityCaseDto> CreateWorkOrderFromDefectLiabilityCaseAsync(Guid defectLiabilityCaseId, CreateProjectMaintenanceFollowThroughDto? dto = null)
    {
        var entity = await GetProjectDefectLiabilityCaseEntityAsync(defectLiabilityCaseId);
        var project = await GetProjectForOperationAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        if (entity.WorkOrderId.HasValue)
        {
            throw new InvalidOperationException("This defect-liability case is already linked to a work order.");
        }

        var unit = entity.ProjectUnitId.HasValue ? await GetProjectUnitEntityAsync(entity.ProjectUnitId.Value) : null;
        var linkedJobCard = await EnsureTenantJobCardExistsAsync(entity.JobCardId);
        var maintenanceAsset = await ResolveProjectMaintenanceAssetForActionAsync(entity.ProjectId, dto?.MaintenanceAssetId, linkedJobCard);
        var maintenanceType = await ResolveProjectMaintenanceTypeForActionAsync(dto?.MaintenanceTypeId, preferredName: "Corrective", fallbackId: linkedJobCard?.MaintenanceTypeId);
        var priorityLevel = await ResolveProjectPriorityLevelForActionAsync(dto?.PriorityLevelId, preferredName: "High", fallbackId: linkedJobCard?.PriorityLevelId);
        var workOrderType = await ResolveProjectWorkOrderTypeForActionAsync(dto?.WorkOrderTypeId, preferredName: "Emergency");
        var billingType = ResolveFollowThroughBillingType(dto?.BillingType, defaultBillingType: "Repairs");
        var estimatedCost = EstimateDefectFollowThroughCost(entity);

        var createdWorkOrder = await _workOrderService.CreateWorkOrderAsync(new CreateWorkOrderDto
        {
            Title = $"Defect - {entity.Title}",
            Description = BuildDefectMaintenanceDescription(project, entity, unit),
            AssetId = maintenanceAsset.Id,
            WorkOrderTypeId = workOrderType.Id,
            MaintenanceTypeId = maintenanceType.Id,
            PriorityLevelId = priorityLevel.Id,
            JobCardId = linkedJobCard?.Id,
            MaintenanceLocation = "Internal",
            RequestedCompletionDate = entity.TargetResolutionDate,
            EstimatedCost = estimatedCost,
            EstimatedHours = maintenanceType.EstimatedHours > 0 ? maintenanceType.EstimatedHours : 2,
            AssignedTechnicianId = dto?.AssignedTechnicianId ?? linkedJobCard?.AssignedTechnicianId,
            AssignedTeamId = dto?.AssignedTeamId ?? linkedJobCard?.AssignedTeamId,
            SafetyRequirements = linkedJobCard?.SafetyRequirements ?? TrimOrNull(maintenanceType.SafetyRequirements),
            RequiresPermit = maintenanceType.RequiresSafetyPermit,
            RequiresLockout = maintenanceType.RequiresShutdown,
            BillingType = billingType,
            FixedAmount = billingType == "Maintenance" ? estimatedCost : 0,
            Instructions = BuildDefectMaintenanceInstructions(entity)
        });

        entity.WorkOrderId = createdWorkOrder.Id;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _unitOfWork.Repository<ProjectDefectLiabilityCase>().UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return await GetProjectDefectLiabilityCaseDtoAsync(entity.ProjectId, entity.Id);
    }

    private async Task<MaintenanceAsset> ResolveProjectMaintenanceAssetForActionAsync(Guid projectId, Guid? requestedAssetId, JobCard? linkedJobCard = null)
    {
        if (linkedJobCard != null)
        {
            return await GetMaintenanceAssetEntityAsync(linkedJobCard.AssetId);
        }

        var assetLinks = (await _unitOfWork.Repository<ProjectAssetLink>().FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId
                && x.MaintenanceAssetId.HasValue))
            .ToList();

        if (requestedAssetId.HasValue)
        {
            if (!assetLinks.Any(x => x.MaintenanceAssetId == requestedAssetId.Value))
            {
                throw new InvalidOperationException("The selected maintenance asset is not linked to this project.");
            }

            return await GetMaintenanceAssetEntityAsync(requestedAssetId.Value);
        }

        var linkedAssetIds = assetLinks
            .Where(x => x.MaintenanceAssetId.HasValue)
            .Select(x => x.MaintenanceAssetId!.Value)
            .Distinct()
            .ToList();

        if (linkedAssetIds.Count == 0)
        {
            throw new InvalidOperationException("Link a maintenance asset to this project in the Access tab before creating maintenance follow-through.");
        }

        var defaultAsset = (await _unitOfWork.Repository<MaintenanceAsset>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && linkedAssetIds.Contains(x.Id)))
            .OrderBy(x => x.Name)
            .ThenBy(x => x.AssetNumber)
            .FirstOrDefault();

        return defaultAsset ?? throw new InvalidOperationException("The linked maintenance asset could not be resolved.");
    }

    private async Task<MaintenanceType> ResolveProjectMaintenanceTypeForActionAsync(Guid? requestedId, string preferredName, Guid? fallbackId = null)
    {
        if (requestedId.HasValue)
        {
            return await GetMaintenanceTypeEntityAsync(requestedId.Value);
        }

        if (fallbackId.HasValue)
        {
            return await GetMaintenanceTypeEntityAsync(fallbackId.Value);
        }

        var options = (await _unitOfWork.Repository<MaintenanceType>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.IsActive))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();

        return options.FirstOrDefault(x =>
                   string.Equals(x.Name, preferredName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(x.Category, preferredName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(x.MaintenanceClass, preferredName, StringComparison.OrdinalIgnoreCase))
               ?? options.FirstOrDefault()
               ?? throw new InvalidOperationException("No active maintenance types are configured for this tenant.");
    }

    private async Task<PriorityLevel> ResolveProjectPriorityLevelForActionAsync(Guid? requestedId, string preferredName, Guid? fallbackId = null)
    {
        if (requestedId.HasValue)
        {
            return await GetPriorityLevelEntityAsync(requestedId.Value);
        }

        if (fallbackId.HasValue)
        {
            return await GetPriorityLevelEntityAsync(fallbackId.Value);
        }

        var options = (await _unitOfWork.Repository<PriorityLevel>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.IsActive))
            .OrderBy(x => x.Level)
            .ThenBy(x => x.Name)
            .ToList();

        return options.FirstOrDefault(x => string.Equals(x.Name, preferredName, StringComparison.OrdinalIgnoreCase))
               ?? options.FirstOrDefault()
               ?? throw new InvalidOperationException("No active maintenance priority levels are configured for this tenant.");
    }

    private async Task<WorkOrderType> ResolveProjectWorkOrderTypeForActionAsync(Guid? requestedId, string preferredName)
    {
        if (requestedId.HasValue)
        {
            return await GetWorkOrderTypeEntityAsync(requestedId.Value);
        }

        var options = (await _unitOfWork.Repository<WorkOrderType>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.IsActive))
            .OrderBy(x => x.DefaultPriority)
            .ThenBy(x => x.Name)
            .ToList();

        return options.FirstOrDefault(x => string.Equals(x.Name, preferredName, StringComparison.OrdinalIgnoreCase))
               ?? options.FirstOrDefault(x => string.Equals(x.Name, "Standard", StringComparison.OrdinalIgnoreCase))
               ?? options.FirstOrDefault()
               ?? throw new InvalidOperationException("No active work order types are configured for this tenant.");
    }

    private async Task<MaintenanceAsset> GetMaintenanceAssetEntityAsync(Guid maintenanceAssetId)
        => await _unitOfWork.Repository<MaintenanceAsset>().FirstOrDefaultAsync(x =>
               x.Id == maintenanceAssetId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException("The selected maintenance asset could not be found.");

    private async Task<MaintenanceType> GetMaintenanceTypeEntityAsync(Guid maintenanceTypeId)
        => await _unitOfWork.Repository<MaintenanceType>().FirstOrDefaultAsync(x =>
               x.Id == maintenanceTypeId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException("The selected maintenance type could not be found.");

    private async Task<PriorityLevel> GetPriorityLevelEntityAsync(Guid priorityLevelId)
        => await _unitOfWork.Repository<PriorityLevel>().FirstOrDefaultAsync(x =>
               x.Id == priorityLevelId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException("The selected maintenance priority level could not be found.");

    private async Task<WorkOrderType> GetWorkOrderTypeEntityAsync(Guid workOrderTypeId)
        => await _unitOfWork.Repository<WorkOrderType>().FirstOrDefaultAsync(x =>
               x.Id == workOrderTypeId
               && x.TenantId == _currentUserProvider.TenantId)
           ?? throw new InvalidOperationException("The selected work order type could not be found.");

    private static decimal EstimateVariationFollowThroughCost(ProjectCustomerVariation entity)
        => entity.ApprovedAmount ?? entity.QuotedAmount ?? entity.EstimatedAmount ?? 0m;

    private static decimal EstimateDefectFollowThroughCost(ProjectDefectLiabilityCase entity)
        => entity.RectificationCost ?? entity.ChargeableAmount ?? 0m;

    private static string ResolveFollowThroughBillingType(string? requestedBillingType, string defaultBillingType)
        => string.Equals(requestedBillingType, "Maintenance", StringComparison.OrdinalIgnoreCase)
            ? "Maintenance"
            : defaultBillingType;

    private static string BuildVariationMaintenanceDescription(Project project, ProjectCustomerVariation entity, ProjectUnit? unit)
    {
        var segments = new List<string>
        {
            $"Project {project.ProjectCode}: {project.Title}"
        };

        if (unit != null)
        {
            segments.Add(unit.Code is { Length: > 0 }
                ? $"Unit {unit.Code} - {unit.Name}"
                : $"Unit {unit.Name}");
        }

        if (!string.IsNullOrWhiteSpace(entity.VariationType))
        {
            segments.Add($"Variation type: {entity.VariationType}");
        }

        if (!string.IsNullOrWhiteSpace(entity.Description))
        {
            segments.Add(entity.Description.Trim());
        }

        return string.Join(Environment.NewLine, segments);
    }

    private static string? BuildVariationMaintenanceInstructions(ProjectCustomerVariation entity)
    {
        var details = new List<string>();
        if (entity.RequiresScheduleAdjustment)
        {
            details.Add($"Schedule impact: {entity.ScheduleImpactDays ?? 0} day(s).");
        }

        if (!string.IsNullOrWhiteSpace(entity.Notes))
        {
            details.Add(entity.Notes.Trim());
        }

        return details.Count == 0 ? null : string.Join(Environment.NewLine, details);
    }

    private static string BuildDefectMaintenanceDescription(Project project, ProjectDefectLiabilityCase entity, ProjectUnit? unit)
    {
        var segments = new List<string>
        {
            $"Project {project.ProjectCode}: {project.Title}"
        };

        if (unit != null)
        {
            segments.Add(unit.Code is { Length: > 0 }
                ? $"Unit {unit.Code} - {unit.Name}"
                : $"Unit {unit.Name}");
        }

        segments.Add(entity.IsWarrantyRelated ? "Warranty-related defect liability case." : "Chargeable defect liability case.");

        if (!string.IsNullOrWhiteSpace(entity.Description))
        {
            segments.Add(entity.Description.Trim());
        }

        return string.Join(Environment.NewLine, segments);
    }

    private static string? BuildDefectMaintenanceInstructions(ProjectDefectLiabilityCase entity)
    {
        var details = new List<string>();
        if (entity.WarrantyExpiryDate.HasValue)
        {
            details.Add($"Warranty expiry: {entity.WarrantyExpiryDate:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(entity.Notes))
        {
            details.Add(entity.Notes.Trim());
        }

        return details.Count == 0 ? null : string.Join(Environment.NewLine, details);
    }
}
