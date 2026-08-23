using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Service implementation for workflow definition management
/// </summary>
public class WorkflowDefinitionService : IWorkflowDefinitionService
{
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly IWorkflowStepRepository _workflowStepRepository;
    private readonly IWorkflowTransitionRepository _workflowTransitionRepository;
    private readonly IWorkflowEntityTypeRepository _workflowEntityTypeRepository;
    private readonly ILogger<WorkflowDefinitionService> _logger;

    public WorkflowDefinitionService(
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IWorkflowStepRepository workflowStepRepository,
        IWorkflowTransitionRepository workflowTransitionRepository,
        IWorkflowEntityTypeRepository workflowEntityTypeRepository,
        ILogger<WorkflowDefinitionService> logger)
    {
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _workflowStepRepository = workflowStepRepository;
        _workflowTransitionRepository = workflowTransitionRepository;
        _workflowEntityTypeRepository = workflowEntityTypeRepository;
        _logger = logger;
    }

    public async Task<WorkflowDefinition> CreateDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating workflow definition: {Name}", definition.Name);

        // Validate the definition
        var validationResult = await ValidateDefinitionAsync(definition, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Workflow definition validation failed: {string.Join(", ", validationResult.ValidationErrors)}");
        }

        // Ensure entity type exists
        var entityType = await _workflowEntityTypeRepository.GetByIdAsync(definition.EntityTypeId) ?? throw new InvalidOperationException($"Entity type with ID {definition.EntityTypeId} not found");

        // Check for duplicate name
        var existingDefinition = await _workflowDefinitionRepository.GetByNameAsync(definition.Name, definition.TenantId, cancellationToken);
        if (existingDefinition != null)
        {
            throw new InvalidOperationException($"A workflow definition with name '{definition.Name}' already exists");
        }

        // Set default values
        definition.Id = Guid.NewGuid();
        definition.DefinitionKey = definition.DefinitionKey == Guid.Empty ? Guid.NewGuid() : definition.DefinitionKey;
        definition.CreatedAt = DateTime.UtcNow;
        definition.IsActive = false; // Start inactive until explicitly activated
        definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft;
        definition.Version = 1;

        // Save the definition
        await _workflowDefinitionRepository.AddAsync(definition);
        await _workflowDefinitionRepository.SaveChangesAsync();

        _logger.LogInformation("Created workflow definition: {Id} - {Name}", definition.Id, definition.Name);
        return definition;
    }

    public async Task<WorkflowDefinition> UpdateDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating workflow definition: {Id}", definition.Id);

        var existingDefinition = await _workflowDefinitionRepository.GetWithDetailsAsync(definition.Id, cancellationToken) ?? throw new InvalidOperationException($"Workflow definition with ID {definition.Id} not found");

        WorkflowDefinitionLifecyclePolicy.EnsureEditable(existingDefinition);

        // Check if definition is being used by active instances
        if (existingDefinition.Instances?.Any(i =>
                i.Status == WorkflowInstanceStatus.Created ||
                i.Status == WorkflowInstanceStatus.InProgress ||
                i.Status == WorkflowInstanceStatus.Waiting ||
                i.Status == WorkflowInstanceStatus.Suspended) == true)
        {
            throw new InvalidOperationException(
                "This workflow has live instances and cannot be edited. Complete or cancel the live instances before editing, or create a separate workflow for future records.");
        }

        // Validate the updated definition
        var validationResult = await ValidateDefinitionAsync(definition, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Workflow definition validation failed: {string.Join(", ", validationResult.ValidationErrors)}");
        }

        // Update properties
        existingDefinition.Name = definition.Name;
        existingDefinition.Description = definition.Description;
        existingDefinition.EntityTypeId = definition.EntityTypeId;
        existingDefinition.UpdatedAt = DateTime.UtcNow;

        await _workflowDefinitionRepository.UpdateAsync(existingDefinition);
        await _workflowDefinitionRepository.SaveChangesAsync();

        _logger.LogInformation("Updated workflow definition: {Id}", definition.Id);
        return existingDefinition;
    }

    public async Task<WorkflowDefinition?> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _workflowDefinitionRepository.GetWithDetailsAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetActiveDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _workflowDefinitionRepository.GetActiveDefinitionsAsync(tenantId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetDefinitionsByEntityTypeAsync(Guid entityTypeId, CancellationToken cancellationToken = default)
    {
        return await _workflowDefinitionRepository.GetActiveByEntityTypeAsync(entityTypeId, cancellationToken);
    }

    public async Task ActivateDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activating workflow definition: {Id}", definitionId);

        var definition = await _workflowDefinitionRepository.GetWithDetailsAsync(definitionId, cancellationToken) ?? throw new InvalidOperationException($"Workflow definition with ID {definitionId} not found");

        // Validate before activation
        var validationResult = await ValidateDefinitionAsync(definition, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Cannot activate invalid workflow definition: {string.Join(", ", validationResult.ValidationErrors)}");
        }

        WorkflowDefinitionLifecyclePolicy.EnsureCanPublish(definition);

        definition.IsActive = true;
        definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published;
        definition.PublishedAt = DateTime.UtcNow;
        definition.UpdatedAt = DateTime.UtcNow;

        await _workflowDefinitionRepository.UpdateAsync(definition);
        await _workflowDefinitionRepository.SaveChangesAsync();

        _logger.LogInformation("Activated workflow definition: {Id}", definitionId);
    }

    public async Task DeactivateDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deactivating workflow definition: {Id}", definitionId);

        var definition = await _workflowDefinitionRepository.GetByIdAsync(definitionId) ?? throw new InvalidOperationException($"Workflow definition with ID {definitionId} not found");
        definition.IsActive = false;
        if (definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published)
        {
            definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired;
            definition.RetiredAt = DateTime.UtcNow;
        }
        definition.UpdatedAt = DateTime.UtcNow;

        await _workflowDefinitionRepository.UpdateAsync(definition);
        await _workflowDefinitionRepository.SaveChangesAsync();

        _logger.LogInformation("Deactivated workflow definition: {Id}", definitionId);
    }

    public async Task<(bool IsValid, IEnumerable<string> ValidationErrors)> ValidateDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Basic validation
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            errors.Add("Workflow definition name is required");
        }

        if (definition.EntityTypeId == Guid.Empty)
        {
            errors.Add("Entity type ID is required");
        }

        if (definition.TenantId == Guid.Empty)
        {
            errors.Add("Tenant ID is required");
        }

        // Validate entity type exists
        if (definition.EntityTypeId != Guid.Empty)
        {
            var entityType = await _workflowEntityTypeRepository.GetByIdAsync(definition.EntityTypeId);
            if (entityType == null)
            {
                errors.Add($"Entity type with ID {definition.EntityTypeId} does not exist");
            }
            else if (!entityType.IsActive)
            {
                errors.Add($"Entity type '{entityType.DisplayName}' is not active");
            }
        }

        var activeSteps = definition.Steps?
            .Where(step => !step.IsDeleted)
            .ToList() ?? new List<WorkflowStep>();
        if (activeSteps.Count == 0)
        {
            errors.Add("Workflow must have at least one active step");
        }
        else
        {
            await ValidateWorkflowStructureAsync(activeSteps, errors, cancellationToken);
        }

        return (errors.Count == 0, errors);
    }

    private async Task ValidateWorkflowStructureAsync(
        IReadOnlyList<WorkflowStep> steps,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        // Must have exactly one start step
        var startSteps = steps.Where(s => s.IsStartStep).ToList();
        if (startSteps.Count == 0)
        {
            errors.Add("Workflow must have exactly one start step");
        }
        else if (startSteps.Count > 1)
        {
            errors.Add("Workflow can have only one start step");
        }

        // Must have at least one end step
        var endSteps = steps.Where(s => s.IsEndStep).ToList();
        if (!endSteps.Any())
        {
            errors.Add("Workflow must have at least one end step");
        }

        // Validate step names are unique
        var duplicateNames = steps.GroupBy(s => s.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateNames.Any())
        {
            errors.Add($"Duplicate step names found: {string.Join(", ", duplicateNames)}");
        }

        // Validate step orders are unique and sequential
        var stepOrders = steps.Select(s => s.StepOrder).OrderBy(o => o).ToList();
        for (int i = 0; i < stepOrders.Count; i++)
        {
            if (stepOrders[i] != i + 1)
            {
                errors.Add("Step orders must be sequential starting from 1");
                break;
            }
        }

        // Approval steps must declare approvers. Without approver rules, the engine cannot generate approval rows
        // and some fallback behaviors (AssignedTo) can unintentionally allow the wrong user to approve.
        var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        serializerOptions.Converters.Add(new JsonStringEnumConverter());

        static void ValidateConfiguredChecklist(
            WorkflowStep step,
            WorkflowStepConfigurationDto? config,
            List<string> errors)
        {
            var checklist = config?.QualityConfig?.QualityChecks ?? new List<WorkflowQualityCheckDto>();
            var duplicateKeys = checklist
                .Select(item => WorkflowChecklistEvidenceValidator.NormalizeKey(item.Id, item.Name))
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateKeys.Count > 0)
            {
                errors.Add($"Step '{step.Name}' has duplicate checklist item keys: {string.Join(", ", duplicateKeys)}");
            }

            foreach (var item in checklist)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    errors.Add($"Step '{step.Name}' has a checklist item without a name");
                    continue;
                }

                if (item.RequiresDocument && string.IsNullOrWhiteSpace(item.DocumentType))
                {
                    errors.Add($"Checklist item '{item.Name}' in step '{step.Name}' must define a document type");
                }

                if (item.RequiresDocument && string.IsNullOrWhiteSpace(item.DocumentName))
                {
                    errors.Add($"Checklist item '{item.Name}' in step '{step.Name}' must define a document name");
                }
            }
        }

        foreach (var step in steps.Where(s => s.StepType == WorkflowStepType.Approval))
        {
            if (string.IsNullOrWhiteSpace(step.Configuration))
            {
                errors.Add($"Approval step '{step.Name}' must have at least one approver user or role configured");
                continue;
            }

            try
            {
                var config = JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(step.Configuration, serializerOptions);
                var rules = config?.ApprovalConfig?.ApproverRules;
                if (rules == null || rules.Count == 0)
                {
                    errors.Add($"Approval step '{step.Name}' must have at least one approver user or role configured");
                }
                else
                {
                    var minimumApprovals = Math.Max(config?.ApprovalConfig?.MinApprovalsRequired ?? 1, 1);
                    if (minimumApprovals > rules.Count)
                    {
                        errors.Add($"Approval step '{step.Name}' requires {minimumApprovals} approvals but only {rules.Count} approval slots are configured");
                    }
                }

                var conflictRules = config?.ApprovalConfig?.ConflictRules ?? new List<WorkflowApprovalConflictRuleDto>();
                foreach (var rule in conflictRules)
                {
                    if (string.IsNullOrWhiteSpace(rule.Name))
                    {
                        errors.Add($"Approval step '{step.Name}' has a segregation-of-duties rule without a name");
                    }

                    if (rule.ActorSource == WorkflowApprovalActorSource.SpecificStepActor)
                    {
                        if (string.IsNullOrWhiteSpace(rule.SourceStepName))
                        {
                            errors.Add($"SOD rule '{rule.Name}' in approval step '{step.Name}' must define a source step");
                        }
                        else if (!steps.Any(candidate => string.Equals(candidate.Name, rule.SourceStepName, StringComparison.OrdinalIgnoreCase)))
                        {
                            errors.Add($"SOD rule '{rule.Name}' in approval step '{step.Name}' references unknown step '{rule.SourceStepName}'");
                        }
                        else if (steps.First(candidate => string.Equals(candidate.Name, rule.SourceStepName, StringComparison.OrdinalIgnoreCase)).StepOrder >= step.StepOrder)
                        {
                            errors.Add($"SOD rule '{rule.Name}' in approval step '{step.Name}' must reference an earlier workflow step");
                        }
                    }

                    if (rule.ActorSource == WorkflowApprovalActorSource.ContextUser && string.IsNullOrWhiteSpace(rule.ContextField))
                    {
                        errors.Add($"SOD rule '{rule.Name}' in approval step '{step.Name}' must define a workflow context user field");
                    }
                }

                ValidateConfiguredChecklist(step, config, errors);
            }
            catch
            {
                errors.Add($"Approval step '{step.Name}' has an invalid configuration payload");
            }
        }

        foreach (var step in steps.Where(s => s.StepType == WorkflowStepType.Manual && !string.IsNullOrWhiteSpace(s.Configuration)))
        {
            try
            {
                var config = JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(step.Configuration, serializerOptions);
                ValidateConfiguredChecklist(step, config, errors);
            }
            catch
            {
                errors.Add($"Task step '{step.Name}' has an invalid configuration payload");
            }
        }

        // Validate transitions if they exist
        if (steps.Any(s => s.OutgoingTransitions?.Any(transition => !transition.IsDeleted) == true))
        {
            await ValidateTransitionsAsync(steps, errors, cancellationToken);
        }

        // Validate that all non-end steps have outgoing transitions
        var stepsWithoutTransitions = steps
            .Where(s => !s.IsEndStep &&
                        (s.OutgoingTransitions?.All(transition => transition.IsDeleted) ?? true))
            .ToList();
        if (stepsWithoutTransitions.Any())
        {
            errors.Add($"Non-end steps must have at least one outgoing transition: {string.Join(", ", stepsWithoutTransitions.Select(s => s.Name))}");
        }
    }

    private static async Task ValidateTransitionsAsync(
        IReadOnlyList<WorkflowStep> steps,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var allTransitions = steps
            .SelectMany(s => s.OutgoingTransitions ?? Enumerable.Empty<WorkflowTransition>())
            .Where(transition => !transition.IsDeleted)
            .ToList();
        var stepIds = steps.Select(s => s.Id).ToHashSet();

        foreach (var transition in allTransitions)
        {
            // Validate FromStep and ToStep exist in the definition
            if (!stepIds.Contains(transition.FromStepId))
            {
                errors.Add($"Transition references invalid FromStep ID: {transition.FromStepId}");
            }

            if (!stepIds.Contains(transition.ToStepId))
            {
                errors.Add($"Transition references invalid ToStep ID: {transition.ToStepId}");
            }

            // Validate transition names are not empty
            if (string.IsNullOrWhiteSpace(transition.Name))
            {
                errors.Add("Transition names cannot be empty");
            }
        }

        // Check for unreachable steps (except start step)
        var startStep = steps.FirstOrDefault(s => s.IsStartStep);
        if (startStep != null)
        {
            var reachableStepIds = new HashSet<Guid> { startStep.Id };
            var queue = new Queue<Guid>(new[] { startStep.Id });

            while (queue.Count > 0)
            {
                var currentStepId = queue.Dequeue();
                var transitions = allTransitions.Where(t => t.FromStepId == currentStepId);

                foreach (var transition in transitions)
                {
                    if (!reachableStepIds.Contains(transition.ToStepId))
                    {
                        reachableStepIds.Add(transition.ToStepId);
                        queue.Enqueue(transition.ToStepId);
                    }
                }
            }

            var unreachableSteps = steps.Where(s => !reachableStepIds.Contains(s.Id)).ToList();
            if (unreachableSteps.Any())
            {
                errors.Add($"Unreachable steps found: {string.Join(", ", unreachableSteps.Select(s => s.Name))}");
            }
        }
    }
}
