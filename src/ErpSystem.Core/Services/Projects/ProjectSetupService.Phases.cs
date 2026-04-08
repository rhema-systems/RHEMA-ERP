using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectSetupService
{
    private static readonly (string Code, string Name, bool IsStageGateRequired)[] DefaultPhaseTemplateBlueprints =
    [
        ("FEASIBILITY", "Feasibility", true),
        ("CONCEPT_DESIGN", "Concept Design", true),
        ("DETAILED_DESIGN", "Detailed Design", true),
        ("APPROVALS", "Approvals & Permits", true),
        ("PROCUREMENT", "Procurement", true),
        ("CONSTRUCTION", "Construction", false),
        ("COMMISSIONING", "Testing & Commissioning", true),
        ("HANDOVER", "Handover", true),
        ("DEFECTS_LIABILITY", "Defects Liability", false)
    ];

    private static readonly (string PhaseCode, string Code, string Name, string RequirementType, string Scope, int? MinimumCount, int? MaximumCount, bool IsBlocking, int SortOrder)[] DefaultStageGateBlueprints =
    [
        ("CONCEPT_DESIGN", "CONCEPT_DOCS", "Concept Documents", ProjectStageGateRequirementTypes.Documents, ProjectStageGateScopes.Project, 1, null, true, 10),
        ("DETAILED_DESIGN", "DETAIL_DESIGN_DOCS", "Detailed Design Documents", ProjectStageGateRequirementTypes.Documents, ProjectStageGateScopes.Project, 2, null, true, 10),
        ("APPROVALS", "APPROVED_PERMITS", "Approved Statutory Approvals", ProjectStageGateRequirementTypes.ApprovedApprovals, ProjectStageGateScopes.Project, 1, null, true, 10),
        ("PROCUREMENT", "PROC_PACKAGES", "Configured Packages", ProjectStageGateRequirementTypes.Packages, ProjectStageGateScopes.Project, 1, null, true, 10),
        ("PROCUREMENT", "PROC_BOQ", "Configured BOQ Lines", ProjectStageGateRequirementTypes.BoqItems, ProjectStageGateScopes.Project, 1, null, true, 20),
        ("COMMISSIONING", "COMMISSIONING_COMPLETE", "Completed Commissioning Items", ProjectStageGateRequirementTypes.CompletedCommissioningItems, ProjectStageGateScopes.Project, 1, null, true, 10),
        ("HANDOVER", "HANDOVER_COMPLETE", "Completed Handover Items", ProjectStageGateRequirementTypes.CompletedHandoverItems, ProjectStageGateScopes.Project, 1, null, true, 10),
        ("HANDOVER", "SNAG_CLOSED", "Open Snag Limit", ProjectStageGateRequirementTypes.OpenSnagItems, ProjectStageGateScopes.Project, null, 0, true, 20)
    ];

    public async Task<IEnumerable<ProjectPhaseTemplateDto>> GetProjectPhaseTemplatesAsync(Guid? projectTypeId = null)
    {
        EnsureAdministrationAccess();
        await EnsureDefaultPhaseLibraryAsync();

        var templates = (await _unitOfWork.Repository<ProjectPhaseTemplate>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && (!projectTypeId.HasValue || x.ProjectTypeId == null || x.ProjectTypeId == projectTypeId.Value)))
            .OrderBy(x => x.ProjectTypeId.HasValue ? 1 : 0)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();

        var templateIds = templates.Select(template => template.Id).ToList();
        var rules = templateIds.Count == 0
            ? []
            : (await _unitOfWork.Repository<ProjectStageGateRule>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && templateIds.Contains(x.ProjectPhaseTemplateId)))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToList();

        var phaseTemplates = templates.ToDictionary(x => x.Id);
        var projectTypes = (await _projectTypeRepository.GetAllAsync()).ToDictionary(x => x.Id);
        var rulesByTemplateId = rules.GroupBy(x => x.ProjectPhaseTemplateId).ToDictionary(group => group.Key, group => group.ToList());

        return templates.Select(template => MapToDto(template, phaseTemplates, projectTypes, rulesByTemplateId));
    }

    public async Task<ProjectPhaseTemplateDto> CreateProjectPhaseTemplateAsync(CreateProjectPhaseTemplateDto dto)
    {
        EnsureAdministrationAccess();
        var parentTemplate = await ValidatePhaseTemplateParentAsync(dto.ParentPhaseTemplateId, dto.ProjectTypeId);
        await ValidateProjectTypeReferenceAsync(dto.ProjectTypeId ?? parentTemplate?.ProjectTypeId);
        await ValidateProjectPhaseTemplateCodeAsync(dto.ProjectTypeId ?? parentTemplate?.ProjectTypeId, dto.Code, null);

        var entity = new ProjectPhaseTemplate
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectTypeId = dto.ProjectTypeId ?? parentTemplate?.ProjectTypeId,
            ParentPhaseTemplateId = dto.ParentPhaseTemplateId,
            Code = NormalizeOptionalCode(dto.Code),
            Name = NormalizeRequiredName(dto.Name, "Phase template name"),
            Description = dto.Description?.Trim(),
            DefaultStatus = NormalizePhaseStatus(dto.DefaultStatus),
            SortOrder = dto.SortOrder,
            IsOptional = dto.IsOptional,
            IsStageGateRequired = dto.IsStageGateRequired,
            IsActive = dto.IsActive,
            AppliesToDeliveryStructure = NormalizeOptionalValue(dto.AppliesToDeliveryStructure),
            AppliesToDevelopmentType = dto.AppliesToDevelopmentType?.Trim(),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectPhaseTemplate>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return await GetProjectPhaseTemplateDtoAsync(entity.Id);
    }

    public async Task<ProjectPhaseTemplateDto> UpdateProjectPhaseTemplateAsync(Guid id, UpdateProjectPhaseTemplateDto dto)
    {
        EnsureAdministrationAccess();
        var repository = _unitOfWork.Repository<ProjectPhaseTemplate>();
        var entity = await repository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Project phase template with ID {id} not found.");

        if (dto.ParentPhaseTemplateId == id)
        {
            throw new InvalidOperationException("A phase template cannot be its own parent.");
        }

        var parentTemplate = await ValidatePhaseTemplateParentAsync(dto.ParentPhaseTemplateId, dto.ProjectTypeId);
        var effectiveProjectTypeId = dto.ProjectTypeId ?? parentTemplate?.ProjectTypeId;
        await ValidateProjectTypeReferenceAsync(effectiveProjectTypeId);
        await ValidateProjectPhaseTemplateCodeAsync(effectiveProjectTypeId, dto.Code, id);

        entity.ProjectTypeId = effectiveProjectTypeId;
        entity.ParentPhaseTemplateId = dto.ParentPhaseTemplateId;
        entity.Code = NormalizeOptionalCode(dto.Code);
        entity.Name = NormalizeRequiredName(dto.Name, "Phase template name");
        entity.Description = dto.Description?.Trim();
        entity.DefaultStatus = NormalizePhaseStatus(dto.DefaultStatus);
        entity.SortOrder = dto.SortOrder;
        entity.IsOptional = dto.IsOptional;
        entity.IsStageGateRequired = dto.IsStageGateRequired;
        entity.IsActive = dto.IsActive;
        entity.AppliesToDeliveryStructure = NormalizeOptionalValue(dto.AppliesToDeliveryStructure);
        entity.AppliesToDevelopmentType = dto.AppliesToDevelopmentType?.Trim();
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return await GetProjectPhaseTemplateDtoAsync(entity.Id);
    }

    public async Task DeleteProjectPhaseTemplateAsync(Guid id)
    {
        EnsureAdministrationAccess();

        var templateRepository = _unitOfWork.Repository<ProjectPhaseTemplate>();
        var ruleRepository = _unitOfWork.Repository<ProjectStageGateRule>();
        var templates = (await templateRepository.FindAsync(x => x.TenantId == _currentUserProvider.TenantId)).ToList();
        var target = templates.FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException($"Project phase template with ID {id} not found.");
        var descendantIds = CollectPhaseTemplateDescendantIds(id, templates);
        descendantIds.Add(id);

        var rules = (await ruleRepository.FindAsync(x => x.TenantId == _currentUserProvider.TenantId && descendantIds.Contains(x.ProjectPhaseTemplateId))).ToList();
        if (rules.Count > 0)
        {
            await ruleRepository.DeleteRangeAsync(rules);
        }

        var toDelete = templates.Where(x => descendantIds.Contains(x.Id)).ToList();
        await templateRepository.DeleteRangeAsync(toDelete);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectStageGateRuleDto>> GetProjectStageGateRulesAsync(Guid? projectPhaseTemplateId = null)
    {
        EnsureAdministrationAccess();
        await EnsureDefaultPhaseLibraryAsync();

        var templates = (await _unitOfWork.Repository<ProjectPhaseTemplate>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId)).ToDictionary(x => x.Id);
        var rules = (await _unitOfWork.Repository<ProjectStageGateRule>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && (!projectPhaseTemplateId.HasValue || x.ProjectPhaseTemplateId == projectPhaseTemplateId.Value)))
            .OrderBy(x => templates.TryGetValue(x.ProjectPhaseTemplateId, out var template) ? template.SortOrder : int.MaxValue)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();

        return rules.Select(rule => MapToDto(rule, templates));
    }

    public async Task<ProjectStageGateRuleDto> CreateProjectStageGateRuleAsync(CreateProjectStageGateRuleDto dto)
    {
        EnsureAdministrationAccess();
        var template = await GetOwnedPhaseTemplateAsync(dto.ProjectPhaseTemplateId);
        await ValidateProjectStageGateRuleCodeAsync(dto.ProjectPhaseTemplateId, dto.Code, null);

        var entity = new ProjectStageGateRule
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectPhaseTemplateId = dto.ProjectPhaseTemplateId,
            Code = NormalizeRequiredCode(dto.Code, "Stage gate rule code"),
            Name = NormalizeRequiredName(dto.Name, "Stage gate rule name"),
            Description = dto.Description?.Trim(),
            RequirementType = NormalizeRequirementType(dto.RequirementType),
            Scope = NormalizeScope(dto.Scope),
            MinimumCount = dto.MinimumCount,
            MaximumCount = dto.MaximumCount,
            IsBlocking = dto.IsBlocking,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        ValidateStageGateThresholds(entity.MinimumCount, entity.MaximumCount);

        await _unitOfWork.Repository<ProjectStageGateRule>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(entity, new Dictionary<Guid, ProjectPhaseTemplate> { [template.Id] = template });
    }

    public async Task<ProjectStageGateRuleDto> UpdateProjectStageGateRuleAsync(Guid id, UpdateProjectStageGateRuleDto dto)
    {
        EnsureAdministrationAccess();
        var repository = _unitOfWork.Repository<ProjectStageGateRule>();
        var entity = await repository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Project stage gate rule with ID {id} not found.");
        var template = await GetOwnedPhaseTemplateAsync(dto.ProjectPhaseTemplateId);
        await ValidateProjectStageGateRuleCodeAsync(dto.ProjectPhaseTemplateId, dto.Code, id);

        entity.ProjectPhaseTemplateId = dto.ProjectPhaseTemplateId;
        entity.Code = NormalizeRequiredCode(dto.Code, "Stage gate rule code");
        entity.Name = NormalizeRequiredName(dto.Name, "Stage gate rule name");
        entity.Description = dto.Description?.Trim();
        entity.RequirementType = NormalizeRequirementType(dto.RequirementType);
        entity.Scope = NormalizeScope(dto.Scope);
        entity.MinimumCount = dto.MinimumCount;
        entity.MaximumCount = dto.MaximumCount;
        entity.IsBlocking = dto.IsBlocking;
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        ValidateStageGateThresholds(entity.MinimumCount, entity.MaximumCount);

        await repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(entity, new Dictionary<Guid, ProjectPhaseTemplate> { [template.Id] = template });
    }

    public async Task DeleteProjectStageGateRuleAsync(Guid id)
    {
        EnsureAdministrationAccess();
        await _unitOfWork.Repository<ProjectStageGateRule>().DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<ProjectPhaseTemplateDto> GetProjectPhaseTemplateDtoAsync(Guid id)
    {
        var template = await _unitOfWork.Repository<ProjectPhaseTemplate>().GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Project phase template with ID {id} not found.");
        var templates = (await _unitOfWork.Repository<ProjectPhaseTemplate>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId)).ToDictionary(x => x.Id);
        var projectTypes = (await _projectTypeRepository.GetAllAsync()).ToDictionary(x => x.Id);
        var rules = (await _unitOfWork.Repository<ProjectStageGateRule>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && x.ProjectPhaseTemplateId == id))
            .GroupBy(x => x.ProjectPhaseTemplateId)
            .ToDictionary(group => group.Key, group => group.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList());

        return MapToDto(template, templates, projectTypes, rules);
    }

    private async Task EnsureDefaultPhaseLibraryAsync()
    {
        var templateRepository = _unitOfWork.Repository<ProjectPhaseTemplate>();
        var ruleRepository = _unitOfWork.Repository<ProjectStageGateRule>();

        var existingTemplates = (await templateRepository.FindAsync(x => x.TenantId == _currentUserProvider.TenantId)).ToList();
        if (existingTemplates.Count == 0)
        {
            foreach (var (phase, index) in DefaultPhaseTemplateBlueprints.Select((phase, index) => (phase, index)))
            {
                await templateRepository.AddAsync(new ProjectPhaseTemplate
                {
                    TenantId = _currentUserProvider.TenantId,
                    Code = phase.Code,
                    Name = phase.Name,
                    DefaultStatus = ProjectPhaseStatuses.NotStarted,
                    SortOrder = index * 10,
                    IsOptional = false,
                    IsStageGateRequired = phase.IsStageGateRequired,
                    IsActive = true,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                });
            }

            await _unitOfWork.SaveChangesAsync();
            existingTemplates = (await templateRepository.FindAsync(x => x.TenantId == _currentUserProvider.TenantId)).ToList();
        }

        var existingRules = (await ruleRepository.FindAsync(x => x.TenantId == _currentUserProvider.TenantId)).ToList();
        if (existingRules.Count > 0)
        {
            return;
        }

        var templateLookup = existingTemplates
            .Where(template => !string.IsNullOrWhiteSpace(template.Code))
            .ToDictionary(template => template.Code!, template => template, StringComparer.OrdinalIgnoreCase);

        foreach (var blueprint in DefaultStageGateBlueprints)
        {
            if (!templateLookup.TryGetValue(blueprint.PhaseCode, out var template))
            {
                continue;
            }

            await ruleRepository.AddAsync(new ProjectStageGateRule
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectPhaseTemplateId = template.Id,
                Code = blueprint.Code,
                Name = blueprint.Name,
                RequirementType = blueprint.RequirementType,
                Scope = blueprint.Scope,
                MinimumCount = blueprint.MinimumCount,
                MaximumCount = blueprint.MaximumCount,
                IsBlocking = blueprint.IsBlocking,
                SortOrder = blueprint.SortOrder,
                IsActive = true,
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<ProjectPhaseTemplate?> ValidatePhaseTemplateParentAsync(Guid? parentPhaseTemplateId, Guid? projectTypeId)
    {
        if (!parentPhaseTemplateId.HasValue)
        {
            return null;
        }

        var parent = await GetOwnedPhaseTemplateAsync(parentPhaseTemplateId.Value);
        if (projectTypeId.HasValue && parent.ProjectTypeId.HasValue && parent.ProjectTypeId != projectTypeId)
        {
            throw new InvalidOperationException("Parent phase template must belong to the same project type scope.");
        }

        return parent;
    }

    private async Task<ProjectPhaseTemplate> GetOwnedPhaseTemplateAsync(Guid phaseTemplateId)
        => await _unitOfWork.Repository<ProjectPhaseTemplate>().FirstOrDefaultAsync(x => x.Id == phaseTemplateId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project phase template with ID {phaseTemplateId} not found.");

    private async Task ValidateProjectTypeReferenceAsync(Guid? projectTypeId)
    {
        if (!projectTypeId.HasValue)
        {
            return;
        }

        _ = await _projectTypeRepository.GetByIdAsync(projectTypeId.Value)
            ?? throw new InvalidOperationException($"Project type with ID {projectTypeId} not found.");
    }

    private async Task ValidateProjectPhaseTemplateCodeAsync(Guid? projectTypeId, string? code, Guid? currentId)
    {
        var normalizedCode = NormalizeOptionalCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return;
        }

        var existing = await _unitOfWork.Repository<ProjectPhaseTemplate>().FirstOrDefaultAsync(x =>
            x.TenantId == _currentUserProvider.TenantId
            && x.ProjectTypeId == projectTypeId
            && x.Code != null
            && x.Code == normalizedCode);

        if (existing != null && existing.Id != currentId)
        {
            throw new InvalidOperationException($"A phase template with code '{normalizedCode}' already exists for the selected scope.");
        }
    }

    private async Task ValidateProjectStageGateRuleCodeAsync(Guid projectPhaseTemplateId, string code, Guid? currentId)
    {
        var normalizedCode = NormalizeRequiredCode(code, "Stage gate rule code");
        var existing = await _unitOfWork.Repository<ProjectStageGateRule>().FirstOrDefaultAsync(x =>
            x.TenantId == _currentUserProvider.TenantId
            && x.ProjectPhaseTemplateId == projectPhaseTemplateId
            && x.Code == normalizedCode);

        if (existing != null && existing.Id != currentId)
        {
            throw new InvalidOperationException($"A stage gate rule with code '{normalizedCode}' already exists for the selected phase template.");
        }
    }

    private static void ValidateStageGateThresholds(int? minimumCount, int? maximumCount)
    {
        if (minimumCount.HasValue && minimumCount.Value < 0)
        {
            throw new InvalidOperationException("Minimum count cannot be negative.");
        }

        if (maximumCount.HasValue && maximumCount.Value < 0)
        {
            throw new InvalidOperationException("Maximum count cannot be negative.");
        }

        if (minimumCount.HasValue && maximumCount.HasValue && minimumCount.Value > maximumCount.Value)
        {
            throw new InvalidOperationException("Minimum count cannot exceed maximum count.");
        }
    }

    private static HashSet<Guid> CollectPhaseTemplateDescendantIds(Guid templateId, IReadOnlyCollection<ProjectPhaseTemplate> templates)
    {
        var children = templates.Where(x => x.ParentPhaseTemplateId == templateId).Select(x => x.Id).ToList();
        var ids = new HashSet<Guid>(children);
        foreach (var childId in children)
        {
            ids.UnionWith(CollectPhaseTemplateDescendantIds(childId, templates));
        }

        return ids;
    }

    private static ProjectPhaseTemplateDto MapToDto(
        ProjectPhaseTemplate entity,
        IReadOnlyDictionary<Guid, ProjectPhaseTemplate> templateLookup,
        IReadOnlyDictionary<Guid, ProjectType> projectTypeLookup,
        IReadOnlyDictionary<Guid, List<ProjectStageGateRule>> rulesByTemplateId)
        => new()
        {
            Id = entity.Id,
            ProjectTypeId = entity.ProjectTypeId,
            ProjectTypeName = entity.ProjectTypeId.HasValue && projectTypeLookup.TryGetValue(entity.ProjectTypeId.Value, out var projectType) ? projectType.Name : null,
            ParentPhaseTemplateId = entity.ParentPhaseTemplateId,
            ParentPhaseTemplateName = entity.ParentPhaseTemplateId.HasValue && templateLookup.TryGetValue(entity.ParentPhaseTemplateId.Value, out var parentTemplate) ? parentTemplate.Name : null,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            DefaultStatus = entity.DefaultStatus,
            SortOrder = entity.SortOrder,
            IsOptional = entity.IsOptional,
            IsStageGateRequired = entity.IsStageGateRequired,
            IsActive = entity.IsActive,
            AppliesToDeliveryStructure = entity.AppliesToDeliveryStructure,
            AppliesToDevelopmentType = entity.AppliesToDevelopmentType,
            StageGateRules = rulesByTemplateId.TryGetValue(entity.Id, out var rules)
                ? rules.Select(rule => MapToDto(rule, templateLookup)).ToList()
                : []
        };

    private static ProjectStageGateRuleDto MapToDto(ProjectStageGateRule entity, IReadOnlyDictionary<Guid, ProjectPhaseTemplate> templateLookup)
        => new()
        {
            Id = entity.Id,
            ProjectPhaseTemplateId = entity.ProjectPhaseTemplateId,
            ProjectPhaseTemplateName = templateLookup.TryGetValue(entity.ProjectPhaseTemplateId, out var template) ? template.Name : string.Empty,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            RequirementType = entity.RequirementType,
            Scope = entity.Scope,
            MinimumCount = entity.MinimumCount,
            MaximumCount = entity.MaximumCount,
            IsBlocking = entity.IsBlocking,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive
        };

    private static string NormalizeRequiredCode(string? code, string label)
        => string.IsNullOrWhiteSpace(code)
            ? throw new InvalidOperationException($"{label} is required.")
            : code.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequiredName(string? value, string label)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{label} is required.")
            : value.Trim();

    private static string NormalizePhaseStatus(string? status)
        => string.IsNullOrWhiteSpace(status) ? ProjectPhaseStatuses.NotStarted : status.Trim();

    private static string NormalizeRequirementType(string? requirementType)
    {
        var normalized = string.IsNullOrWhiteSpace(requirementType) ? ProjectStageGateRequirementTypes.Packages : requirementType.Trim();
        return normalized;
    }

    private static string NormalizeScope(string? scope)
    {
        var normalized = string.IsNullOrWhiteSpace(scope) ? ProjectStageGateScopes.Phase : scope.Trim();
        return normalized;
    }
}
