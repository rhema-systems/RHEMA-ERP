using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<IEnumerable<ProjectBudgetRevisionDto>> GetBudgetRevisionsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        return (await GetBudgetRevisionEntitiesAsync(projectId))
            .OrderByDescending(x => x.VersionNumber)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<ProjectBudgetRevisionDto> CreateBudgetRevisionAsync(Guid projectId, CreateProjectBudgetRevisionDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        ValidateBudgetRevision(dto);

        var repo = _unitOfWork.Repository<ProjectBudgetRevision>();
        var existing = await GetBudgetRevisionEntitiesAsync(projectId);
        var entity = new ProjectBudgetRevision
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            VersionNumber = existing.Count == 0 ? 1 : existing.Max(x => x.VersionNumber) + 1,
            RevisionName = dto.RevisionName.Trim(),
            RevisionType = string.IsNullOrWhiteSpace(dto.RevisionType) ? "Revision" : dto.RevisionType.Trim(),
            EstimatedBudget = dto.EstimatedBudget,
            ApprovedBudget = dto.ApprovedBudget,
            CommittedCost = dto.CommittedCost,
            ForecastCost = dto.ForecastCost,
            ThresholdWarningPercent = dto.ThresholdWarningPercent,
            ThresholdCriticalPercent = dto.ThresholdCriticalPercent,
            EffectiveDate = dto.EffectiveDate?.Date ?? DateTime.UtcNow.Date,
            ChangeReason = dto.ChangeReason,
            Notes = dto.Notes,
            Status = "Draft",
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "BudgetRevisionCreated", new Dictionary<string, object>
        {
            ["BudgetRevisionId"] = entity.Id,
            ["RevisionName"] = entity.RevisionName,
            ["VersionNumber"] = entity.VersionNumber,
            ["ApprovedBudget"] = entity.ApprovedBudget
        });

        return MapToDto(entity);
    }

    public async Task<ProjectBudgetRevisionDto> SubmitBudgetRevisionAsync(Guid revisionId, Guid userId)
    {
        var repo = _unitOfWork.Repository<ProjectBudgetRevision>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == revisionId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project budget revision with ID {revisionId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);

        if (!string.Equals(entity.Status, "Draft", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(entity.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Budget revision must be in Draft or Rejected status to submit (current status: '{entity.Status}')");
        }

        var workflowResult = await _workflowIntegrationService.SubmitAsync(ProjectBudgetRevisionWorkflowEntityType, entity.Id);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start budget revision workflow");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(ProjectBudgetRevisionWorkflowEntityType);
        adapter.ApplySubmitOutcome(entity, workflowResult.Outcome, userId);
        entity.SubmittedAt = DateTime.UtcNow;
        entity.ApprovedAt = string.Equals(entity.Status, "Approved", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null;
        entity.ApprovedById = string.Equals(entity.Status, "Approved", StringComparison.OrdinalIgnoreCase) ? userId : null;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        if (string.Equals(entity.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            ApplyApprovedBudgetRevisionToProject(project, entity);
        }

        await repo.UpdateAsync(entity);
        await _projectRepository.UpdateAsync(project);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "BudgetRevisionSubmitted", new Dictionary<string, object>
        {
            ["BudgetRevisionId"] = entity.Id,
            ["RevisionName"] = entity.RevisionName,
            ["Status"] = entity.Status
        });

        return MapToDto(entity);
    }

    public async Task<ProjectBudgetRevisionDto> ApproveBudgetRevisionAsync(Guid revisionId, Guid userId, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectBudgetRevision>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == revisionId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project budget revision with ID {revisionId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ApproveWorkflow);

        if (!string.Equals(entity.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Budget revision must be in PendingApproval status to approve (current status: '{entity.Status}')");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(ProjectBudgetRevisionWorkflowEntityType, revisionId, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(ProjectBudgetRevisionWorkflowEntityType, revisionId, userId, "Approve", comments);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to approve budget revision");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(ProjectBudgetRevisionWorkflowEntityType);
        adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, userId, comments);
        entity.ApprovedAt = DateTime.UtcNow;
        entity.ApprovedById = userId;
        entity.RejectionReason = null;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        ApplyApprovedBudgetRevisionToProject(project, entity);

        await repo.UpdateAsync(entity);
        await _projectRepository.UpdateAsync(project);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "BudgetRevisionApproved", new Dictionary<string, object>
        {
            ["BudgetRevisionId"] = entity.Id,
            ["RevisionName"] = entity.RevisionName,
            ["ApprovedBudget"] = entity.ApprovedBudget
        });

        return MapToDto(entity);
    }

    public async Task<ProjectBudgetRevisionDto> RejectBudgetRevisionAsync(Guid revisionId, Guid userId, string reason, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectBudgetRevision>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == revisionId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project budget revision with ID {revisionId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ApproveWorkflow);

        if (!string.Equals(entity.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Budget revision must be in PendingApproval status to reject (current status: '{entity.Status}')");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(ProjectBudgetRevisionWorkflowEntityType, revisionId, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var rejectionText = string.IsNullOrWhiteSpace(comments) ? reason : comments;
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(ProjectBudgetRevisionWorkflowEntityType, revisionId, userId, "Reject", rejectionText);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to reject budget revision");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(ProjectBudgetRevisionWorkflowEntityType);
        adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, userId, rejectionText);
        entity.RejectionReason = rejectionText;
        entity.ApprovedAt = null;
        entity.ApprovedById = null;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "BudgetRevisionRejected", new Dictionary<string, object>
        {
            ["BudgetRevisionId"] = entity.Id,
            ["RevisionName"] = entity.RevisionName,
            ["Reason"] = rejectionText
        });

        return MapToDto(entity);
    }

    public async Task<IEnumerable<ProjectForecastVersionDto>> GetForecastVersionsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        return (await GetForecastVersionEntitiesAsync(projectId))
            .OrderByDescending(x => x.VersionNumber)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<ProjectForecastVersionDto> CreateForecastVersionAsync(Guid projectId, CreateProjectForecastVersionDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        ValidateForecastVersion(dto);

        var repo = _unitOfWork.Repository<ProjectForecastVersion>();
        var existing = await GetForecastVersionEntitiesAsync(projectId);
        var shouldActivate = dto.IsActive || existing.Count == 0;

        if (shouldActivate)
        {
            foreach (var version in existing.Where(x => x.IsActive))
            {
                version.IsActive = false;
                version.UpdatedBy = _currentUserProvider.Username;
                version.LastModifiedById = _currentUserProvider.UserId;
                await repo.UpdateAsync(version);
            }
        }

        var entity = new ProjectForecastVersion
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            VersionNumber = existing.Count == 0 ? 1 : existing.Max(x => x.VersionNumber) + 1,
            VersionName = dto.VersionName.Trim(),
            AsOfDate = dto.AsOfDate?.Date ?? DateTime.UtcNow.Date,
            ForecastCost = dto.ForecastCost,
            EstimateAtCompletion = dto.EstimateAtCompletion,
            ForecastRevenue = dto.ForecastRevenue,
            ForecastMargin = dto.ForecastMargin,
            IsActive = shouldActivate,
            Notes = dto.Notes,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ForecastVersionCreated", new Dictionary<string, object>
        {
            ["ForecastVersionId"] = entity.Id,
            ["VersionName"] = entity.VersionName,
            ["IsActive"] = entity.IsActive
        });

        return MapToDto(entity);
    }

    public async Task<ProjectForecastVersionDto> SetActiveForecastVersionAsync(Guid forecastVersionId)
    {
        var repo = _unitOfWork.Repository<ProjectForecastVersion>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == forecastVersionId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project forecast version with ID {forecastVersionId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);

        var versions = await GetForecastVersionEntitiesAsync(entity.ProjectId);
        foreach (var version in versions)
        {
            var shouldBeActive = version.Id == forecastVersionId;
            if (version.IsActive == shouldBeActive)
            {
                continue;
            }

            version.IsActive = shouldBeActive;
            version.UpdatedBy = _currentUserProvider.Username;
            version.LastModifiedById = _currentUserProvider.UserId;
            await repo.UpdateAsync(version);
        }

        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ForecastVersionActivated", new Dictionary<string, object>
        {
            ["ForecastVersionId"] = entity.Id,
            ["VersionName"] = entity.VersionName
        });

        entity.IsActive = true;
        return MapToDto(entity);
    }

    private async Task<List<ProjectBudgetRevision>> GetBudgetRevisionEntitiesAsync(Guid projectId)
        => (await _unitOfWork.Repository<ProjectBudgetRevision>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();

    private async Task<List<ProjectForecastVersion>> GetForecastVersionEntitiesAsync(Guid projectId)
        => (await _unitOfWork.Repository<ProjectForecastVersion>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();

    private static void ValidateBudgetRevision(CreateProjectBudgetRevisionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RevisionName))
        {
            throw new InvalidOperationException("Budget revision name is required");
        }

        if (dto.EstimatedBudget < 0m || dto.ApprovedBudget < 0m || dto.CommittedCost < 0m || dto.ForecastCost < 0m)
        {
            throw new InvalidOperationException("Budget revision values cannot be negative");
        }

        if (dto.ThresholdWarningPercent <= 0m || dto.ThresholdWarningPercent > 100m)
        {
            throw new InvalidOperationException("Warning threshold must be between 0 and 100");
        }

        if (dto.ThresholdCriticalPercent < dto.ThresholdWarningPercent || dto.ThresholdCriticalPercent > 100m)
        {
            throw new InvalidOperationException("Critical threshold must be greater than or equal to warning threshold and not exceed 100");
        }
    }

    private static void ValidateForecastVersion(CreateProjectForecastVersionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.VersionName))
        {
            throw new InvalidOperationException("Forecast version name is required");
        }

        if (dto.ForecastCost < 0m || dto.EstimateAtCompletion < 0m || dto.ForecastRevenue < 0m)
        {
            throw new InvalidOperationException("Forecast version values cannot be negative");
        }
    }

    private static void ApplyApprovedBudgetRevisionToProject(Project project, ProjectBudgetRevision revision)
    {
        project.EstimatedBudget = revision.EstimatedBudget;
        project.ApprovedBudget = revision.ApprovedBudget;
        project.BudgetStatus = "Approved";
    }

    private static ProjectBudgetRevisionDto MapToDto(ProjectBudgetRevision entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        VersionNumber = entity.VersionNumber,
        RevisionName = entity.RevisionName,
        RevisionType = entity.RevisionType,
        EstimatedBudget = entity.EstimatedBudget,
        ApprovedBudget = entity.ApprovedBudget,
        CommittedCost = entity.CommittedCost,
        ForecastCost = entity.ForecastCost,
        ThresholdWarningPercent = entity.ThresholdWarningPercent,
        ThresholdCriticalPercent = entity.ThresholdCriticalPercent,
        Status = entity.Status,
        EffectiveDate = entity.EffectiveDate,
        SubmittedAt = entity.SubmittedAt,
        ApprovedAt = entity.ApprovedAt,
        ApprovedById = entity.ApprovedById,
        ChangeReason = entity.ChangeReason,
        Notes = entity.Notes,
        RejectionReason = entity.RejectionReason
    };

    private static ProjectForecastVersionDto MapToDto(ProjectForecastVersion entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        VersionNumber = entity.VersionNumber,
        VersionName = entity.VersionName,
        AsOfDate = entity.AsOfDate,
        ForecastCost = entity.ForecastCost,
        EstimateAtCompletion = entity.EstimateAtCompletion,
        ForecastRevenue = entity.ForecastRevenue,
        ForecastMargin = entity.ForecastMargin,
        IsActive = entity.IsActive,
        Notes = entity.Notes
    };
}
