using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<IEnumerable<ProjectPhaseGateEvaluationDto>> GetProjectPhaseGateEvaluationsAsync(Guid projectId)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var phases = (await GetProjectPhaseEntitiesAsync(projectId)).ToList();
        if (phases.Count == 0)
        {
            return [];
        }

        var profile = project.DevelopmentProfile
            ?? await _unitOfWork.Repository<ProjectDevelopmentProfile>()
                .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId);

        var templates = (await GetApplicableProjectPhaseTemplateEntitiesAsync(project, profile)).ToList();
        var templateIds = templates.Select(template => template.Id).ToList();
        var rules = templateIds.Count == 0
            ? []
            : (await _unitOfWork.Repository<ProjectStageGateRule>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && x.IsActive
                    && templateIds.Contains(x.ProjectPhaseTemplateId)))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToList();

        var rulesByTemplateId = rules.GroupBy(x => x.ProjectPhaseTemplateId).ToDictionary(group => group.Key, group => group.ToList());
        var packages = (await _unitOfWork.Repository<ProjectPackage>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var packageIds = packages.Select(x => x.Id).ToList();
        var boqItems = packageIds.Count == 0
            ? []
            : (await _unitOfWork.Repository<ProjectBoqItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
                .ToList();
        var approvalItems = (await _unitOfWork.Repository<ProjectApprovalRegisterItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var documents = (await _unitOfWork.Repository<ProjectDocument>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var commissioningItems = (await _unitOfWork.Repository<ProjectCommissioningItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var handoverItems = (await _unitOfWork.Repository<ProjectHandoverItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var snagItems = (await _unitOfWork.Repository<ProjectSnagItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();

        return phases.Select(phase =>
        {
            var template = FindMatchingPhaseTemplate(phase, templates);
            var phaseRules = template != null && rulesByTemplateId.TryGetValue(template.Id, out var configuredRules)
                ? configuredRules
                : [];

            var requirementResults = phaseRules
                .Select(rule => EvaluateStageGateRule(rule, phase, packages, boqItems, approvalItems, documents, commissioningItems, handoverItems, snagItems))
                .ToList();

            var hasConfiguredRules = requirementResults.Count > 0;
            var blockingFailureCount = requirementResults.Count(result => result.IsBlocking && !result.IsSatisfied);
            var isReady = phase.IsStageGateRequired
                ? hasConfiguredRules && blockingFailureCount == 0
                : blockingFailureCount == 0;

            return new ProjectPhaseGateEvaluationDto
            {
                ProjectPhaseId = phase.Id,
                ProjectPhaseCode = phase.Code,
                ProjectPhaseName = phase.Name,
                ProjectPhaseTemplateId = template?.Id,
                ProjectPhaseTemplateName = template?.Name,
                IsStageGateRequired = phase.IsStageGateRequired,
                HasConfiguredRules = hasConfiguredRules,
                IsReady = isReady,
                BlockingFailureCount = blockingFailureCount,
                RequirementResults = requirementResults
            };
        }).ToList();
    }

    public async Task<ProjectPhaseProgressionResultDto> AdvanceProjectPhaseAsync(Guid phaseId, AdvanceProjectPhaseDto dto)
    {
        var phase = await GetProjectPhaseEntityAsync(phaseId);
        await RequireProjectAsync(phase.ProjectId, ProjectAccessOperation.ManagePlan);

        var allPhases = (await GetProjectPhaseEntitiesAsync(phase.ProjectId)).ToList();
        var repository = _unitOfWork.Repository<ProjectPhase>();
        var now = DateTime.UtcNow;
        var normalizedStatus = NormalizeProjectPhaseStatus(phase.Status);
        var packageStatusUpdateCount = 0;

        if (IsProjectPhaseTerminal(normalizedStatus))
        {
            throw new InvalidOperationException($"Phase '{phase.Name}' is already in a terminal status and cannot be advanced.");
        }

        ProjectPhaseGateEvaluationDto? gateEvaluation = null;
        ProjectPhase? nextPhase = null;
        var nextPhaseStarted = false;
        string action;
        string message;

        if (string.Equals(normalizedStatus, ProjectPhaseStatuses.NotStarted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedStatus, ProjectPhaseStatuses.Blocked, StringComparison.OrdinalIgnoreCase))
        {
            await EnsurePhaseCanStartAsync(phase, dto.OverrideStageGate, dto.OverrideReason, allPhases);
            phase.Status = ProjectPhaseStatuses.InProgress;
            phase.ActualStartDate ??= now;
            packageStatusUpdateCount += await ApplyPhasePackageStatusNudgesAsync(phase, phaseCompleted: false);
            action = "Started";
            message = $"Phase '{phase.Name}' started successfully.";
        }
        else
        {
            gateEvaluation = await EnsurePhaseCanCompleteAsync(phase, dto.OverrideStageGate, dto.OverrideReason);
            phase.Status = ProjectPhaseStatuses.Completed;
            phase.ActualStartDate ??= now;
            phase.ActualEndDate = now;
            packageStatusUpdateCount += await ApplyPhasePackageStatusNudgesAsync(phase, phaseCompleted: true);
            action = "Completed";
            message = $"Phase '{phase.Name}' completed successfully.";

            if (dto.StartNextPhase)
            {
                nextPhase = GetNextProjectPhaseSibling(phase, allPhases);
                if (nextPhase != null
                    && !IsProjectPhaseTerminal(nextPhase.Status)
                    && !string.Equals(nextPhase.Status, ProjectPhaseStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
                {
                    nextPhase.Status = ProjectPhaseStatuses.InProgress;
                    nextPhase.ActualStartDate ??= now;
                    nextPhase.UpdatedBy = _currentUserProvider.Username;
                    nextPhase.LastModifiedById = _currentUserProvider.UserId;
                    packageStatusUpdateCount += await ApplyPhasePackageStatusNudgesAsync(nextPhase, phaseCompleted: false);
                    await repository.UpdateAsync(nextPhase);
                    nextPhaseStarted = true;
                    message = $"Phase '{phase.Name}' completed and '{nextPhase.Name}' started.";
                }
            }
        }

        phase.UpdatedBy = _currentUserProvider.Username;
        phase.LastModifiedById = _currentUserProvider.UserId;
        await repository.UpdateAsync(phase);
        await _unitOfWork.SaveChangesAsync();

        if (packageStatusUpdateCount > 0)
        {
            message = $"{message} {packageStatusUpdateCount} linked package(s) were nudged to the next commercial status.";
        }

        return new ProjectPhaseProgressionResultDto
        {
            Action = action,
            Message = message,
            OverrideUsed = dto.OverrideStageGate,
            StageGateEvaluated = gateEvaluation != null,
            StageGatePassed = gateEvaluation?.IsReady ?? true,
            NextPhaseStarted = nextPhaseStarted,
            PackageStatusUpdateCount = packageStatusUpdateCount,
            Phase = MapToDto(phase),
            NextPhase = nextPhase == null ? null : MapToDto(nextPhase),
            GateEvaluation = gateEvaluation
        };
    }

    private async Task EnsurePhaseCanStartAsync(
        ProjectPhase phase,
        bool overrideStageGate,
        string? overrideReason,
        IReadOnlyCollection<ProjectPhase>? phases = null)
    {
        var scopedPhases = phases ?? await GetProjectPhaseEntitiesAsync(phase.ProjectId);
        var predecessor = GetLastUnresolvedPredecessorPhase(phase, scopedPhases);
        if (predecessor == null || overrideStageGate)
        {
            return;
        }

        var reasonSuffix = string.IsNullOrWhiteSpace(overrideReason) ? string.Empty : $" Override note: {overrideReason.Trim()}";
        throw new InvalidOperationException(
            $"Cannot start phase '{phase.Name}' until predecessor phase '{predecessor.Name}' is completed, waived, or cancelled.{reasonSuffix}");
    }

    private async Task<ProjectPhaseGateEvaluationDto?> EnsurePhaseCanCompleteAsync(ProjectPhase phase, bool overrideStageGate, string? overrideReason)
    {
        var evaluation = (await GetProjectPhaseGateEvaluationsAsync(phase.ProjectId))
            .FirstOrDefault(item => item.ProjectPhaseId == phase.Id);

        if (phase.IsStageGateRequired && (evaluation == null || !evaluation.IsReady))
        {
            if (overrideStageGate)
            {
                return evaluation;
            }

            var reasonSuffix = string.IsNullOrWhiteSpace(overrideReason) ? string.Empty : $" Override note: {overrideReason.Trim()}";
            throw new InvalidOperationException(BuildStageGateFailureMessage(phase, evaluation) + reasonSuffix);
        }

        return evaluation;
    }

    private async Task<bool> SeedConfiguredConstructionPhasesAsync(Project project)
    {
        if (await HasProjectPhasesAsync(project.Id))
        {
            return false;
        }

        var profile = project.DevelopmentProfile
            ?? await _unitOfWork.Repository<ProjectDevelopmentProfile>()
                .FirstOrDefaultAsync(x => x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId);
        var templates = (await GetApplicableProjectPhaseTemplateEntitiesAsync(project, profile)).ToList();
        var rootTemplates = templates
            .Where(template => !template.ParentPhaseTemplateId.HasValue)
            .OrderBy(template => template.SortOrder)
            .ThenBy(template => template.Name)
            .ToList();

        if (rootTemplates.Count == 0)
        {
            return false;
        }

        var repository = _unitOfWork.Repository<ProjectPhase>();
        foreach (var template in rootTemplates)
        {
            await AddProjectPhaseFromConfiguredTemplateAsync(repository, project.Id, template, templates, null);
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private async Task AddProjectPhaseFromConfiguredTemplateAsync(
        IGenericRepository<ProjectPhase> repository,
        Guid projectId,
        ProjectPhaseTemplate template,
        IReadOnlyCollection<ProjectPhaseTemplate> templates,
        Guid? parentPhaseId)
    {
        var phase = new ProjectPhase
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ParentPhaseId = parentPhaseId,
            Code = template.Code,
            Name = template.Name,
            Description = template.Description,
            Status = NormalizeProjectPhaseStatus(template.DefaultStatus),
            SortOrder = template.SortOrder,
            IsOptional = template.IsOptional,
            IsStageGateRequired = template.IsStageGateRequired,
            IsTemplateSeeded = true,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await repository.AddAsync(phase);

        var children = templates
            .Where(child => child.ParentPhaseTemplateId == template.Id)
            .OrderBy(child => child.SortOrder)
            .ThenBy(child => child.Name)
            .ToList();

        foreach (var child in children)
        {
            await AddProjectPhaseFromConfiguredTemplateAsync(repository, projectId, child, templates, phase.Id);
        }
    }

    private async Task<List<ProjectPhaseTemplate>> GetApplicableProjectPhaseTemplateEntitiesAsync(Project project, ProjectDevelopmentProfile? profile)
    {
        var templateRepository = _unitOfWork.Repository<ProjectPhaseTemplate>();
        if (templateRepository == null)
        {
            return [];
        }

        var templates = ((await templateRepository.FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.IsActive
                && (!x.ProjectTypeId.HasValue || x.ProjectTypeId == project.ProjectTypeId)))
            ?? [])
            .ToList();

        if (templates.Count == 0)
        {
            return [];
        }

        var applicableTemplates = templates
            .Where(template => MatchesPhaseTemplateApplicability(template, profile))
            .ToList();

        if (!project.ProjectTypeId.HasValue)
        {
            return applicableTemplates
                .Where(template => !template.ProjectTypeId.HasValue)
                .ToList();
        }

        var projectTypeTemplates = applicableTemplates
            .Where(template => template.ProjectTypeId == project.ProjectTypeId)
            .ToList();

        return projectTypeTemplates.Count > 0
            ? projectTypeTemplates
            : applicableTemplates.Where(template => !template.ProjectTypeId.HasValue).ToList();
    }

    private static bool MatchesPhaseTemplateApplicability(ProjectPhaseTemplate template, ProjectDevelopmentProfile? profile)
    {
        if (!string.IsNullOrWhiteSpace(template.AppliesToDeliveryStructure))
        {
            var deliveryStructure = NormalizeDeliveryStructure(profile?.DeliveryStructure);
            if (!string.Equals(template.AppliesToDeliveryStructure, deliveryStructure, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(template.AppliesToDevelopmentType))
        {
            var developmentType = profile?.DevelopmentType?.Trim();
            if (!string.Equals(template.AppliesToDevelopmentType, developmentType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static ProjectPhaseTemplate? FindMatchingPhaseTemplate(ProjectPhase phase, IReadOnlyCollection<ProjectPhaseTemplate> templates)
    {
        if (!string.IsNullOrWhiteSpace(phase.Code))
        {
            var matchedByCode = templates.FirstOrDefault(template =>
                !string.IsNullOrWhiteSpace(template.Code)
                && string.Equals(template.Code, phase.Code, StringComparison.OrdinalIgnoreCase));
            if (matchedByCode != null)
            {
                return matchedByCode;
            }
        }

        return templates.FirstOrDefault(template =>
            string.Equals(template.Name, phase.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectPhase? GetLastUnresolvedPredecessorPhase(ProjectPhase phase, IReadOnlyCollection<ProjectPhase> phases)
    {
        var siblings = phases
            .Where(item => item.ProjectId == phase.ProjectId && item.ParentPhaseId == phase.ParentPhaseId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToList();

        var currentIndex = siblings.FindIndex(item => item.Id == phase.Id);
        if (currentIndex <= 0)
        {
            return null;
        }

        return siblings
            .Take(currentIndex)
            .Reverse<ProjectPhase>()
            .FirstOrDefault(item => !item.IsOptional && !IsProjectPhaseTerminal(item.Status));
    }

    private static ProjectPhase? GetNextProjectPhaseSibling(ProjectPhase phase, IReadOnlyCollection<ProjectPhase> phases)
    {
        var siblings = phases
            .Where(item => item.ProjectId == phase.ProjectId && item.ParentPhaseId == phase.ParentPhaseId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToList();

        var currentIndex = siblings.FindIndex(item => item.Id == phase.Id);
        if (currentIndex < 0 || currentIndex >= siblings.Count - 1)
        {
            return null;
        }

        return siblings[currentIndex + 1];
    }

    private static bool IsProjectPhaseTerminal(string? status)
        => string.Equals(status, ProjectPhaseStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ProjectPhaseStatuses.Waived, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ProjectPhaseStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);

    private static string BuildStageGateFailureMessage(ProjectPhase phase, ProjectPhaseGateEvaluationDto? evaluation)
    {
        if (evaluation == null)
        {
            return $"Phase '{phase.Name}' cannot be completed because its stage gate evaluation could not be found.";
        }

        if (!evaluation.HasConfiguredRules)
        {
            return $"Phase '{phase.Name}' cannot be completed because its required stage gate is not configured yet.";
        }

        var failures = evaluation.RequirementResults
            .Where(result => result.IsBlocking && !result.IsSatisfied)
            .Select(result => $"{result.RuleName} ({result.Message})")
            .ToList();

        if (failures.Count == 0)
        {
            return $"Phase '{phase.Name}' cannot be completed because its stage gate requirements are not satisfied yet.";
        }

        return $"Phase '{phase.Name}' cannot be completed because these stage gate requirements are still failing: {string.Join("; ", failures)}.";
    }

    private static ProjectPhaseGateRequirementResultDto EvaluateStageGateRule(
        ProjectStageGateRule rule,
        ProjectPhase phase,
        IReadOnlyCollection<ProjectPackage> packages,
        IReadOnlyCollection<ProjectBoqItem> boqItems,
        IReadOnlyCollection<ProjectApprovalRegisterItem> approvalItems,
        IReadOnlyCollection<ProjectDocument> documents,
        IReadOnlyCollection<ProjectCommissioningItem> commissioningItems,
        IReadOnlyCollection<ProjectHandoverItem> handoverItems,
        IReadOnlyCollection<ProjectSnagItem> snagItems)
    {
        var actualCount = rule.RequirementType switch
        {
            ProjectStageGateRequirementTypes.Packages => CountPackages(rule.Scope, phase.Id, packages),
            ProjectStageGateRequirementTypes.BoqItems => CountBoqItems(rule.Scope, phase.Id, packages, boqItems),
            ProjectStageGateRequirementTypes.ApprovedApprovals => CountApprovedApprovals(rule.Scope, phase.Id, approvalItems),
            ProjectStageGateRequirementTypes.Documents => documents.Count,
            ProjectStageGateRequirementTypes.CompletedCommissioningItems => commissioningItems.Count(item =>
                string.Equals(item.Status, ProjectCommissioningItemStatuses.Completed, StringComparison.OrdinalIgnoreCase)),
            ProjectStageGateRequirementTypes.CompletedHandoverItems => handoverItems.Count(item =>
                string.Equals(item.Status, ProjectHandoverItemStatuses.Completed, StringComparison.OrdinalIgnoreCase)),
            ProjectStageGateRequirementTypes.OpenSnagItems => snagItems.Count(item =>
                !string.Equals(item.Status, ProjectSnagStatuses.Closed, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Status, ProjectSnagStatuses.Waived, StringComparison.OrdinalIgnoreCase)),
            _ => 0
        };

        var minimumSatisfied = !rule.MinimumCount.HasValue || actualCount >= rule.MinimumCount.Value;
        var maximumSatisfied = !rule.MaximumCount.HasValue || actualCount <= rule.MaximumCount.Value;
        var isSatisfied = minimumSatisfied && maximumSatisfied;

        return new ProjectPhaseGateRequirementResultDto
        {
            StageGateRuleId = rule.Id,
            RuleCode = rule.Code,
            RuleName = rule.Name,
            RequirementType = rule.RequirementType,
            Scope = rule.Scope,
            MinimumCount = rule.MinimumCount,
            MaximumCount = rule.MaximumCount,
            ActualCount = actualCount,
            IsBlocking = rule.IsBlocking,
            IsSatisfied = isSatisfied,
            Message = BuildStageGateMessage(rule, actualCount, isSatisfied)
        };
    }

    private static int CountPackages(string scope, Guid phaseId, IReadOnlyCollection<ProjectPackage> packages)
        => string.Equals(scope, ProjectStageGateScopes.Phase, StringComparison.OrdinalIgnoreCase)
            ? packages.Count(packageItem => packageItem.ProjectPhaseId == phaseId)
            : packages.Count;

    private static int CountBoqItems(string scope, Guid phaseId, IReadOnlyCollection<ProjectPackage> packages, IReadOnlyCollection<ProjectBoqItem> boqItems)
    {
        if (!string.Equals(scope, ProjectStageGateScopes.Phase, StringComparison.OrdinalIgnoreCase))
        {
            return boqItems.Count;
        }

        var phasePackageIds = packages
            .Where(packageItem => packageItem.ProjectPhaseId == phaseId)
            .Select(packageItem => packageItem.Id)
            .ToHashSet();
        return boqItems.Count(item => phasePackageIds.Contains(item.ProjectPackageId));
    }

    private static int CountApprovedApprovals(string scope, Guid phaseId, IReadOnlyCollection<ProjectApprovalRegisterItem> approvalItems)
    {
        var scopedItems = string.Equals(scope, ProjectStageGateScopes.Phase, StringComparison.OrdinalIgnoreCase)
            ? approvalItems.Where(item => item.ProjectPhaseId == phaseId)
            : approvalItems;

        return scopedItems.Count(item =>
            string.Equals(item.Status, ProjectApprovalRegisterStatuses.Approved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Status, "ConditionallyApproved", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildStageGateMessage(ProjectStageGateRule rule, int actualCount, bool isSatisfied)
    {
        var comparison = new List<string>();
        if (rule.MinimumCount.HasValue)
        {
            comparison.Add($"min {rule.MinimumCount.Value}");
        }

        if (rule.MaximumCount.HasValue)
        {
            comparison.Add($"max {rule.MaximumCount.Value}");
        }

        var targetText = comparison.Count > 0 ? string.Join(", ", comparison) : "configured threshold";
        var statusText = isSatisfied ? "Ready" : "Needs attention";
        return $"{statusText}: actual {actualCount}, target {targetText}.";
    }
}
