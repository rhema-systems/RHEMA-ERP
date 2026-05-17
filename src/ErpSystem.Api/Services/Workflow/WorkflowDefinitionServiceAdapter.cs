using System.Linq;
using ErpSystem.Core.DTOs.Workflow;
using System.Text.Json;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Workflow;

/// <summary>
/// Adapter to bridge ErpSystem.Core.Interfaces.Workflow.IWorkflowDefinitionService 
/// with ErpSystem.Core.Interfaces.Services.IWorkflowDefinitionService
/// </summary>
public class WorkflowDefinitionServiceAdapter : ErpSystem.Core.Interfaces.Workflow.IWorkflowDefinitionService
{
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
            Name = createDto.Name,
            Description = createDto.Description,
            EntityTypeId = entityType.Id,
            Configuration = createDto.Configuration,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = createdById,
            IsActive = false,
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

        definition.Description = updateDto.Description;
        definition.Configuration = updateDto.Configuration;
        definition.UpdatedAt = DateTime.UtcNow;
        definition.UpdatedBy = _currentUserService.UserName ?? "System";
        definition.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var modifiedById)
            ? modifiedById
            : null;

        if (updateDto.Steps != null && updateDto.Steps.Any())
        {
            definition.Version = Math.Max(1, definition.Version + 1);
        }

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
                EntityType = definition.EntityType?.Name ?? string.Empty,
                Configuration = definition.Configuration,
                CreatedById = modifiedById,
                Steps = updateDto.Steps ?? new List<CreateWorkflowStepDto>(),
                Transitions = updateDto.Transitions ?? new List<CreateWorkflowTransitionDto>()
            }, definition.TenantId);
        }

        return await _workflowDefinitionRepository.GetWithDetailsAsync(definition.Id) ?? definition;
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
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant ID is required to update workflow definition status");
        }

        if (!isActive)
        {
            // Deactivation does not require workflow structure validation.
            var definition = await _workflowDefinitionRepository.GetByIdAsync(id)
                             ?? throw new InvalidOperationException($"Workflow definition with ID {id} not found");
            definition.IsActive = false;
            definition.UpdatedAt = DateTime.UtcNow;
            definition.UpdatedBy = _currentUserService.UserName ?? "System";
            definition.LastModifiedById = modifiedById;
            await _workflowDefinitionRepository.UpdateAsync(definition);
            await _workflowDefinitionRepository.SaveChangesAsync();
            return;
        }

        // IMPORTANT:
        // When a workflow definition is updated, we soft-delete and recreate steps/transitions in the same request scope.
        // EF may keep the previous (now soft-deleted) steps in the tracked navigation collection, which then causes
        // activation validation to falsely report duplicate step names / multiple start steps / non-sequential orders.
        // To avoid that, validate using a no-tracking query that reflects the persisted database state.
        var definitionForValidation = await _workflowDefinitionRepository
            .GetQueryable(d => d.Id == id && d.TenantId == tenantId)
            // Use identity resolution to avoid duplicate step entities when including transitions.
            .AsNoTrackingWithIdentityResolution()
            .AsSplitQuery()
            .Include(wd => wd.Steps.OrderBy(s => s.Order))
            .ThenInclude(s => s.OutgoingTransitions)
            .Include(wd => wd.Steps)
            .ThenInclude(s => s.IncomingTransitions)
            .Include(wd => wd.EntityType)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Workflow definition with ID {id} not found");

        var (isValid, errors) = await _coreService.ValidateDefinitionAsync(definitionForValidation);
        if (!isValid)
        {
            throw new InvalidOperationException(
                $"Cannot activate invalid workflow definition: {string.Join(", ", errors)}");
        }

        var definitionToActivate = await _workflowDefinitionRepository.GetByIdAsync(id)
                                 ?? throw new InvalidOperationException($"Workflow definition with ID {id} not found");
        definitionToActivate.IsActive = true;
        definitionToActivate.UpdatedAt = DateTime.UtcNow;
        definitionToActivate.UpdatedBy = _currentUserService.UserName ?? "System";
        definitionToActivate.LastModifiedById = modifiedById;
        await _workflowDefinitionRepository.UpdateAsync(definitionToActivate);
        await _workflowDefinitionRepository.SaveChangesAsync();
    }

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
                    ? JsonSerializer.Serialize(stepDto.Configuration)
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
                        ? JsonSerializer.Serialize(transitionDto.Condition)
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

    private static string NormalizeEntityTypeKey(string? value)
        => new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
