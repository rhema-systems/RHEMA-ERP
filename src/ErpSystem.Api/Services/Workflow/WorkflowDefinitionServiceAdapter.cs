using System.Linq;
using ErpSystem.Core.DTOs.Workflow;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Workflow;

/// <summary>
/// Adapter to bridge ErpSystem.Core.Interfaces.Workflow.IWorkflowDefinitionService 
/// with ErpSystem.Core.Interfaces.Services.IWorkflowDefinitionService
/// </summary>
public class WorkflowDefinitionServiceAdapter : ErpSystem.Core.Interfaces.Workflow.IWorkflowDefinitionService
{
    private static readonly JsonSerializerOptions WorkflowJsonOptions = CreateWorkflowJsonOptions();

    private readonly ErpSystem.Core.Interfaces.Services.IWorkflowDefinitionService _coreService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly IWorkflowStepRepository _workflowStepRepository;
    private readonly IWorkflowTransitionRepository _workflowTransitionRepository;
    private readonly IWorkflowEntityTypeRepository _workflowEntityTypeRepository;
    private readonly ILogger<WorkflowDefinitionServiceAdapter> _logger;

    public WorkflowDefinitionServiceAdapter(
        ErpSystem.Core.Interfaces.Services.IWorkflowDefinitionService coreService,
        ICurrentUserService currentUserService,
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IWorkflowStepRepository workflowStepRepository,
        IWorkflowTransitionRepository workflowTransitionRepository,
        IWorkflowEntityTypeRepository workflowEntityTypeRepository,
        ILogger<WorkflowDefinitionServiceAdapter> logger)
    {
        _coreService = coreService;
        _currentUserService = currentUserService;
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _workflowStepRepository = workflowStepRepository;
        _workflowTransitionRepository = workflowTransitionRepository;
        _workflowEntityTypeRepository = workflowEntityTypeRepository;
        _logger = logger;
    }

    public async Task<WorkflowDefinition> CreateWorkflowDefinitionAsync(CreateWorkflowDefinitionDto createDto)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant ID is required to create workflow definitions");
        }

        var entityType = await ResolveOrCreateEntityTypeAsync(createDto.EntityType, tenantId);
        var existing = await _workflowDefinitionRepository.GetByNameAsync(createDto.Name, tenantId);
        if (existing != null)
        {
            throw new InvalidOperationException($"A workflow definition with name '{createDto.Name}' already exists");
        }

        var createdById = Guid.TryParse(_currentUserService.UserId, out var parsedUserId)
            ? parsedUserId
            : Guid.Empty;

        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            DefinitionKey = Guid.NewGuid(),
            Name = createDto.Name,
            Description = createDto.Description,
            EntityTypeId = entityType.Id,
            Configuration = createDto.Configuration,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = createdById,
            IsActive = false,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft,
            Version = 1
        };

        await _workflowDefinitionRepository.AddAsync(definition);
        await _workflowDefinitionRepository.SaveChangesAsync();

        if (createDto.Steps.Any())
        {
            await CreateStepsAndTransitionsAsync(definition, createDto, tenantId);
        }

        return await _workflowDefinitionRepository.GetWithDetailsAsync(definition.Id) ?? definition;
    }

    public async Task<WorkflowDefinition> UpdateWorkflowDefinitionAsync(Guid id, UpdateWorkflowDefinitionDto updateDto)
    {
        var definition = await _workflowDefinitionRepository.GetWithDetailsAsync(id) ?? throw new InvalidOperationException($"Workflow definition with ID {id} not found");
        EnsureCurrentTenant(definition);

        WorkflowDefinitionLifecyclePolicy.EnsureEditable(definition);

        var hasActiveInstances = await _workflowDefinitionRepository
            .GetQueryable(d => d.Id == id && d.TenantId == definition.TenantId)
            .SelectMany(d => d.Instances)
            .AnyAsync(i =>
                !i.IsDeleted &&
                (i.Status == WorkflowInstanceStatus.Created ||
                 i.Status == WorkflowInstanceStatus.InProgress ||
                 i.Status == WorkflowInstanceStatus.Waiting ||
                 i.Status == WorkflowInstanceStatus.Suspended));

        if (hasActiveInstances)
        {
            throw new InvalidOperationException(
                "This workflow has live instances and cannot be edited. Complete or cancel the live instances before editing, or create a separate workflow for future records.");
        }

        if (!string.IsNullOrWhiteSpace(updateDto.Name))
        {
            definition.Name = updateDto.Name;
        }

        if (!string.IsNullOrWhiteSpace(updateDto.EntityType))
        {
            var entityType = await ResolveOrCreateEntityTypeAsync(updateDto.EntityType.Trim(), definition.TenantId);
            definition.EntityTypeId = entityType.Id;
            definition.EntityType = entityType;
        }

        definition.Description = updateDto.Description;
        definition.Configuration = updateDto.Configuration;
        definition.UpdatedAt = DateTime.UtcNow;
        definition.UpdatedBy = _currentUserService.UserName ?? "System";
        definition.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var modifiedById)
            ? modifiedById
            : null;

        await _workflowDefinitionRepository.UpdateAsync(definition);
        await _workflowDefinitionRepository.SaveChangesAsync();

        if (updateDto.Steps != null && updateDto.Steps.Any())
        {
            await _workflowTransitionRepository.DeleteRangeAsync(t => t.WorkflowDefinitionId == definition.Id);
            await _workflowTransitionRepository.SaveChangesAsync();
            await _workflowStepRepository.DeleteRangeAsync(s => s.WorkflowDefinitionId == definition.Id);
            await _workflowStepRepository.SaveChangesAsync();

            await CreateStepsAndTransitionsAsync(definition, new CreateWorkflowDefinitionDto
            {
                Name = definition.Name,
                Description = definition.Description,
                EntityType = definition.EntityType?.Name ?? updateDto.EntityType ?? string.Empty,
                Configuration = definition.Configuration,
                CreatedById = modifiedById,
                Steps = updateDto.Steps ?? new List<CreateWorkflowStepDto>(),
                Transitions = updateDto.Transitions ?? new List<CreateWorkflowTransitionDto>()
            }, definition.TenantId);
        }

        return await _workflowDefinitionRepository.GetWithDetailsAsync(definition.Id) ?? definition;
    }

    public async Task<WorkflowDefinition> CloneWorkflowDefinitionDraftAsync(
        Guid sourceDefinitionId,
        string? changeSummary,
        Guid createdById)
    {
        var source = await _workflowDefinitionRepository.GetWithDetailsAsync(sourceDefinitionId)
                     ?? throw new InvalidOperationException($"Workflow definition with ID {sourceDefinitionId} not found");
        EnsureCurrentTenant(source);

        var definitionKey = source.DefinitionKey == Guid.Empty ? source.Id : source.DefinitionKey;
        var versions = await _workflowDefinitionRepository.GetVersionsAsync(definitionKey, source.TenantId);

        if (versions.Any(d => d.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Draft && d.Id != source.Id))
        {
            throw new InvalidOperationException("This workflow already has an editable draft. Open that draft instead of creating another version.");
        }

        if (source.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Draft)
        {
            return source;
        }

        var nextVersion = Math.Max(source.Version + 1, WorkflowDefinitionLifecyclePolicy.GetNextVersion(versions));
        var draft = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            DefinitionKey = definitionKey,
            Name = source.Name,
            Description = source.Description,
            EntityTypeId = source.EntityTypeId,
            Version = nextVersion,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft,
            IsActive = false,
            Configuration = source.Configuration,
            ChangeSummary = string.IsNullOrWhiteSpace(changeSummary) ? $"Drafted from version {source.Version}" : changeSummary.Trim(),
            SupersedesDefinitionId = source.Id,
            TenantId = source.TenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = createdById
        };

        await _workflowDefinitionRepository.AddAsync(draft);

        var sourceSteps = source.Steps
            .OrderBy(step => step.Order)
            .ThenBy(step => step.Id)
            .ToList();

        var clonedSteps = sourceSteps.Select(step => new WorkflowStep
        {
            WorkflowDefinitionId = draft.Id,
            Name = step.Name,
            Description = step.Description,
            StepType = step.StepType,
            Order = step.Order,
            IsRequired = step.IsRequired,
            RequiredRole = step.RequiredRole,
            EstimatedHours = step.EstimatedHours,
            IsStartStep = step.IsStartStep,
            IsEndStep = step.IsEndStep,
            Configuration = step.Configuration,
            TenantId = draft.TenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = draft.CreatedBy,
            CreatedById = createdById
        }).ToList();

        var stepIdMap = new Dictionary<Guid, Guid>();
        if (clonedSteps.Count > 0)
        {
            var addedSteps = (await _workflowStepRepository.AddRangeAsync(clonedSteps)).ToList();
            stepIdMap = sourceSteps
                .Zip(addedSteps, (sourceStep, clonedStep) => new { SourceId = sourceStep.Id, ClonedId = clonedStep.Id })
                .ToDictionary(item => item.SourceId, item => item.ClonedId);
        }

        var clonedTransitions = source.Transitions
            .Where(transition => stepIdMap.ContainsKey(transition.FromStepId) && stepIdMap.ContainsKey(transition.ToStepId))
            .Select(transition => new WorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowDefinitionId = draft.Id,
                FromStepId = stepIdMap[transition.FromStepId],
                ToStepId = stepIdMap[transition.ToStepId],
                Name = transition.Name,
                Description = transition.Description,
                Condition = transition.Condition,
                IsDefault = transition.IsDefault,
                Priority = transition.Priority,
                TenantId = draft.TenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = draft.CreatedBy,
                CreatedById = createdById
            }).ToList();

        if (clonedTransitions.Count > 0)
        {
            await _workflowTransitionRepository.AddRangeAsync(clonedTransitions);
        }

        await _workflowDefinitionRepository.SaveChangesAsync();

        return await _workflowDefinitionRepository.GetWithDetailsAsync(draft.Id) ?? draft;
    }

    public async Task<WorkflowDefinition> PublishWorkflowDefinitionAsync(Guid id, Guid publishedById)
    {
        var tenantId = RequireTenantId();
        var draft = await GetDefinitionForLifecycleValidationAsync(id, tenantId);
        if (draft.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && draft.IsActive)
        {
            return draft;
        }

        WorkflowDefinitionLifecyclePolicy.EnsureCanPublish(draft);

        var (isValid, errors) = await _coreService.ValidateDefinitionAsync(draft);
        if (!isValid)
        {
            throw new InvalidOperationException($"Cannot publish invalid workflow definition: {string.Join(", ", errors)}");
        }

        var now = DateTime.UtcNow;
        var definitionKey = draft.DefinitionKey == Guid.Empty ? draft.Id : draft.DefinitionKey;
        var family = await _workflowDefinitionRepository
            .GetQueryable(d => d.TenantId == tenantId && d.DefinitionKey == definitionKey)
            .ToListAsync();

        foreach (var current in family.Where(d => d.Id != draft.Id &&
                                                   d.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published))
        {
            current.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired;
            current.IsActive = false;
            current.RetiredAt = now;
            current.RetiredById = publishedById;
            current.UpdatedAt = now;
            current.UpdatedBy = _currentUserService.UserName ?? "System";
            current.LastModifiedById = publishedById;
            await _workflowDefinitionRepository.UpdateAsync(current);
        }

        var trackedDraft = await _workflowDefinitionRepository.GetByIdAsync(id)
                           ?? throw new InvalidOperationException($"Workflow definition with ID {id} not found");
        trackedDraft.DefinitionKey = definitionKey;
        trackedDraft.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published;
        trackedDraft.IsActive = true;
        trackedDraft.PublishedAt = now;
        trackedDraft.PublishedById = publishedById;
        trackedDraft.RetiredAt = null;
        trackedDraft.RetiredById = null;
        trackedDraft.UpdatedAt = now;
        trackedDraft.UpdatedBy = _currentUserService.UserName ?? "System";
        trackedDraft.LastModifiedById = publishedById;
        await _workflowDefinitionRepository.UpdateAsync(trackedDraft);
        await _workflowDefinitionRepository.SaveChangesAsync();

        return await _workflowDefinitionRepository.GetWithDetailsAsync(id) ?? trackedDraft;
    }

    public async Task<WorkflowDefinition> RetireWorkflowDefinitionAsync(Guid id, Guid retiredById)
    {
        var definition = await _workflowDefinitionRepository.GetByIdAsync(id)
                         ?? throw new InvalidOperationException($"Workflow definition with ID {id} not found");
        EnsureCurrentTenant(definition);

        if (definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Retired)
        {
            return definition;
        }

        if (definition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Published)
        {
            throw new InvalidOperationException("Only a published workflow version can be retired.");
        }

        definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired;
        definition.IsActive = false;
        definition.RetiredAt = DateTime.UtcNow;
        definition.RetiredById = retiredById;
        definition.UpdatedAt = DateTime.UtcNow;
        definition.UpdatedBy = _currentUserService.UserName ?? "System";
        definition.LastModifiedById = retiredById;
        await _workflowDefinitionRepository.UpdateAsync(definition);
        await _workflowDefinitionRepository.SaveChangesAsync();
        return await _workflowDefinitionRepository.GetWithDetailsAsync(id) ?? definition;
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> GetWorkflowDefinitionVersionsAsync(Guid id)
    {
        var source = await _workflowDefinitionRepository.GetByIdAsync(id)
                     ?? throw new InvalidOperationException($"Workflow definition with ID {id} not found");
        EnsureCurrentTenant(source);
        var definitionKey = source.DefinitionKey == Guid.Empty ? source.Id : source.DefinitionKey;

        return await _workflowDefinitionRepository
            .GetQueryable(d => d.TenantId == source.TenantId && d.DefinitionKey == definitionKey)
            .AsNoTracking()
            .Include(d => d.Instances)
            .OrderByDescending(d => d.Version)
            .ToListAsync();
    }

    public async Task<WorkflowDefinitionComparisonDto> CompareWorkflowDefinitionsAsync(
        Guid fromDefinitionId,
        Guid toDefinitionId)
    {
        var from = await _workflowDefinitionRepository.GetWithDetailsAsync(fromDefinitionId)
                   ?? throw new InvalidOperationException($"Workflow definition with ID {fromDefinitionId} not found");
        var to = await _workflowDefinitionRepository.GetWithDetailsAsync(toDefinitionId)
                 ?? throw new InvalidOperationException($"Workflow definition with ID {toDefinitionId} not found");
        EnsureCurrentTenant(from);
        EnsureCurrentTenant(to);

        var fromKey = from.DefinitionKey == Guid.Empty ? from.Id : from.DefinitionKey;
        var toKey = to.DefinitionKey == Guid.Empty ? to.Id : to.DefinitionKey;
        if (fromKey != toKey)
        {
            throw new InvalidOperationException("Workflow versions can only be compared within the same definition family.");
        }

        var changes = new List<string>();
        var potentiallyBreaking = false;
        AddPropertyChange(changes, "Name", from.Name, to.Name);
        AddPropertyChange(changes, "Description", from.Description, to.Description);
        if (!string.Equals(from.Configuration, to.Configuration, StringComparison.Ordinal))
        {
            changes.Add("Workflow configuration changed.");
        }

        var fromSteps = from.Steps
            .GroupBy(step => step.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var toSteps = to.Steps
            .GroupBy(step => step.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var removed in fromSteps.Keys.Except(toSteps.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(name => name))
        {
            changes.Add($"Step removed: {removed}.");
            potentiallyBreaking = true;
        }
        foreach (var added in toSteps.Keys.Except(fromSteps.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(name => name))
        {
            changes.Add($"Step added: {added}.");
        }
        foreach (var name in fromSteps.Keys.Intersect(toSteps.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(name => name))
        {
            var oldStep = fromSteps[name];
            var newStep = toSteps[name];
            if (oldStep.StepType != newStep.StepType || oldStep.Order != newStep.Order ||
                oldStep.IsRequired != newStep.IsRequired || oldStep.RequiredRole != newStep.RequiredRole ||
                oldStep.Configuration != newStep.Configuration)
            {
                changes.Add($"Step changed: {name}.");
                potentiallyBreaking = true;
            }
        }

        var fromStepNames = from.Steps.ToDictionary(step => step.Id, step => step.Name);
        var toStepNames = to.Steps.ToDictionary(step => step.Id, step => step.Name);
        var fromTransitions = from.Transitions
            .Select(transition => TransitionSignature(transition, fromStepNames))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toTransitions = to.Transitions
            .Select(transition => TransitionSignature(transition, toStepNames))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removedTransitions = fromTransitions.Except(toTransitions, StringComparer.OrdinalIgnoreCase).Count();
        var addedTransitions = toTransitions.Except(fromTransitions, StringComparer.OrdinalIgnoreCase).Count();
        if (removedTransitions > 0)
        {
            changes.Add($"{removedTransitions} transition(s) removed or changed.");
            potentiallyBreaking = true;
        }
        if (addedTransitions > 0)
        {
            changes.Add($"{addedTransitions} transition(s) added or changed.");
        }

        return new WorkflowDefinitionComparisonDto
        {
            DefinitionKey = fromKey,
            FromDefinitionId = from.Id,
            FromVersion = from.Version,
            ToDefinitionId = to.Id,
            ToVersion = to.Version,
            HasPotentiallyBreakingChanges = potentiallyBreaking,
            Changes = changes
        };
    }

    public async Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(Guid id)
    {
        return await _coreService.GetDefinitionAsync(id);
    }

    public async Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string name, string entityType)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var definitions = await _coreService.GetActiveDefinitionsAsync(tenantId);
        return definitions.FirstOrDefault(d => d.Name == name);
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetWorkflowDefinitionsAsync(string entityType)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        return await _coreService.GetActiveDefinitionsAsync(tenantId);
    }

    public async Task<WorkflowValidationResult> ValidateWorkflowDefinitionAsync(Guid workflowDefinitionId)
    {
        var definition = await _coreService.GetDefinitionAsync(workflowDefinitionId);
        if (definition == null)
        {
            return new WorkflowValidationResult
            {
                IsValid = false,
                Errors = new List<WorkflowValidationError>
            {
                new WorkflowValidationError
                {
                    Code = "NOT_FOUND",
                    Message = $"Workflow definition with ID {workflowDefinitionId} not found"
                }
            }
            };
        }

        var (isValid, errors) = await _coreService.ValidateDefinitionAsync(definition);
        return new WorkflowValidationResult
        {
            IsValid = isValid,
            Errors = errors.Select(e => new WorkflowValidationError
            {
                Code = "VALIDATION_ERROR",
                Message = e
            }).ToList()
        };
    }

    public async Task SetWorkflowDefinitionActiveAsync(Guid id, bool isActive, Guid modifiedById)
    {
        if (!isActive)
        {
            var definition = await _workflowDefinitionRepository.GetByIdAsync(id)
                             ?? throw new InvalidOperationException($"Workflow definition with ID {id} not found");
            EnsureCurrentTenant(definition);
            if (definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
            {
                await RetireWorkflowDefinitionAsync(id, modifiedById);
            }
            return;
        }
        await PublishWorkflowDefinitionAsync(id, modifiedById);
    }

    private async Task<WorkflowDefinition> GetDefinitionForLifecycleValidationAsync(Guid id, Guid tenantId)
    {
        return await _workflowDefinitionRepository
            .GetQueryable(d => d.Id == id && d.TenantId == tenantId)
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(wd => wd.Steps.OrderBy(s => s.Order))
            .ThenInclude(s => s.OutgoingTransitions)
            .Include(wd => wd.Steps)
            .ThenInclude(s => s.IncomingTransitions)
            .Include(wd => wd.EntityType)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Workflow definition with ID {id} not found");
    }

    private Guid RequireTenantId()
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        return tenantId != Guid.Empty
            ? tenantId
            : throw new InvalidOperationException("Tenant ID is required for workflow definition lifecycle operations");
    }

    private void EnsureCurrentTenant(WorkflowDefinition definition)
    {
        if (definition.TenantId != RequireTenantId())
        {
            throw new InvalidOperationException("Workflow definition not found for the current tenant.");
        }
    }

    private static void AddPropertyChange(List<string> changes, string property, string? from, string? to)
    {
        if (!string.Equals(from?.Trim(), to?.Trim(), StringComparison.Ordinal))
        {
            changes.Add($"{property} changed.");
        }
    }

    private static string TransitionSignature(
        WorkflowTransition transition,
        IReadOnlyDictionary<Guid, string> stepNames) =>
        $"{stepNames.GetValueOrDefault(transition.FromStepId, transition.FromStepId.ToString())}|" +
        $"{stepNames.GetValueOrDefault(transition.ToStepId, transition.ToStepId.ToString())}|" +
        $"{transition.Name}|{transition.Condition}|{transition.IsDefault}|{transition.Priority}";

    public async Task DeleteWorkflowDefinitionAsync(Guid id)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant ID is required to delete workflow definitions");
        }

        // IMPORTANT: We only allow deletion when the definition has never been used.
        // Because we have a global soft-delete query filter, deleting a definition that has instances would
        // hide it from instance monitoring/history (and can break lookups that rely on definition navigation).
        var definition = await _workflowDefinitionRepository
            .GetQueryable()
            .Include(d => d.Instances)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId)
            ?? throw new InvalidOperationException($"Workflow definition with ID {id} not found");

        if (definition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft workflow version can be deleted. Published history must be retired and retained for audit.");
        }

        if (definition.Instances?.Any() == true)
        {
            throw new InvalidOperationException("Cannot delete a workflow definition that has workflow instances. Deactivate it instead.");
        }

        definition.IsActive = false;
        definition.IsDeleted = true;
        definition.DeletedAt = DateTime.UtcNow;
        definition.DeletedBy = _currentUserService.UserName ?? "System";
        definition.UpdatedAt = DateTime.UtcNow;
        definition.UpdatedBy = _currentUserService.UserName ?? "System";
        definition.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var modifiedById)
            ? modifiedById
            : null;

        await _workflowDefinitionRepository.UpdateAsync(definition);
        await _workflowDefinitionRepository.SaveChangesAsync();
    }

    private async Task CreateStepsAndTransitionsAsync(
        WorkflowDefinition definition,
        CreateWorkflowDefinitionDto template,
        Guid tenantId)
    {
        var orderedSteps = template.Steps.OrderBy(s => s.Order).ToList();
        if (!orderedSteps.Any())
        {
            return;
        }

        var stepEntities = new List<WorkflowStep>();
        for (var i = 0; i < orderedSteps.Count; i++)
        {
            var stepDto = orderedSteps[i];
            var stepId = stepDto.Id.HasValue && stepDto.Id.Value != Guid.Empty
                ? stepDto.Id.Value
                : Guid.NewGuid();

            var stepEntity = new WorkflowStep
            {
                Id = stepId,
                WorkflowDefinitionId = definition.Id,
                Name = stepDto.Name,
                Description = stepDto.Description,
                StepType = stepDto.StepType,
                Order = stepDto.Order,
                IsRequired = stepDto.IsRequired,
                RequiredRole = stepDto.RequiredRole,
                EstimatedHours = stepDto.EstimatedHours,
                IsStartStep = i == 0,
                IsEndStep = i == orderedSteps.Count - 1,
                Configuration = stepDto.Configuration != null
                    ? JsonSerializer.Serialize(stepDto.Configuration, WorkflowJsonOptions)
                    : null,
                TenantId = tenantId
            };

            stepEntities.Add(stepEntity);
        }

        await _workflowStepRepository.AddRangeAsync(stepEntities);
        await _workflowStepRepository.SaveChangesAsync();

        var transitions = new List<WorkflowTransition>();
        if (template.Transitions.Any())
        {
            var stepIds = stepEntities.Select(s => s.Id).ToHashSet();
            foreach (var transitionDto in template.Transitions)
            {
                if (!stepIds.Contains(transitionDto.FromStepId) || !stepIds.Contains(transitionDto.ToStepId))
                {
                    continue;
                }

                transitions.Add(new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    WorkflowDefinitionId = definition.Id,
                    FromStepId = transitionDto.FromStepId,
                    ToStepId = transitionDto.ToStepId,
                    Name = transitionDto.Name,
                    Description = transitionDto.Description,
                    Condition = transitionDto.Condition != null
                        ? JsonSerializer.Serialize(transitionDto.Condition, WorkflowJsonOptions)
                        : null,
                    IsDefault = transitionDto.IsDefault,
                    Priority = transitionDto.Priority,
                    TenantId = tenantId
                });
            }
        }

        if (!transitions.Any())
        {
            for (var i = 0; i < stepEntities.Count - 1; i++)
            {
                var from = stepEntities[i];
                var to = stepEntities[i + 1];
                transitions.Add(new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    WorkflowDefinitionId = definition.Id,
                    FromStepId = from.Id,
                    ToStepId = to.Id,
                    Name = $"{from.Name} to {to.Name}",
                    Description = "Auto-generated transition",
                    IsDefault = true,
                    Priority = 0,
                    TenantId = tenantId
                });
            }
        }

        if (transitions.Any())
        {
            await _workflowTransitionRepository.AddRangeAsync(transitions);
            await _workflowTransitionRepository.SaveChangesAsync();
        }
    }

    private async Task<WorkflowEntityType> ResolveOrCreateEntityTypeAsync(string entityType, Guid tenantId)
    {
        var existing = await _workflowEntityTypeRepository.GetByNameAsync(entityType, tenantId);
        if (existing != null)
        {
            return existing;
        }

        var activeTypes = await _workflowEntityTypeRepository.GetActiveEntityTypesAsync(tenantId);
        existing = activeTypes.FirstOrDefault(et => EntityTypeMatches(et, entityType));
        if (existing != null)
        {
            return existing;
        }

        var code = GenerateEntityTypeCode(entityType);
        var entity = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            Name = entityType,
            Code = code,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            IsActive = true
        };

        await _workflowEntityTypeRepository.AddAsync(entity);
        await _workflowEntityTypeRepository.SaveChangesAsync();
        return entity;
    }

    private static string GenerateEntityTypeCode(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return "ENTITY";
        }

        var codeChars = new List<char>();
        for (var i = 0; i < entityType.Length; i++)
        {
            var ch = entityType[i];
            if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
            {
                if (codeChars.LastOrDefault() != '_')
                {
                    codeChars.Add('_');
                }
                continue;
            }

            if (char.IsUpper(ch) && i > 0 && char.IsLower(entityType[i - 1]))
            {
                codeChars.Add('_');
            }

            codeChars.Add(char.ToUpperInvariant(ch));
        }

        var code = new string(codeChars.ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(code) ? "ENTITY" : code;
    }

    private static bool EntityTypeMatches(WorkflowEntityType entityType, string requestedType)
    {
        var requested = NormalizeEntityTypeKey(requestedType);
        if (string.IsNullOrWhiteSpace(requested))
        {
            return false;
        }

        return NormalizeEntityTypeKey(entityType.Code) == requested ||
               NormalizeEntityTypeKey(entityType.Name) == requested ||
               NormalizeEntityTypeKey(entityType.DisplayName) == requested;
    }

    private static JsonSerializerOptions CreateWorkflowJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string NormalizeEntityTypeKey(string? value)
        => new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
