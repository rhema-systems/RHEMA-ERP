using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Services.Workflow;

/// <summary>
/// Adapter to bridge ErpSystem.Core.Interfaces.Workflow.IWorkflowDefinitionService 
/// with ErpSystem.Core.Interfaces.Services.IWorkflowDefinitionService
/// </summary>
public class WorkflowDefinitionServiceAdapter : ErpSystem.Core.Interfaces.Workflow.IWorkflowDefinitionService
{
    private readonly ErpSystem.Core.Interfaces.Services.IWorkflowDefinitionService _coreService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowDefinitionServiceAdapter> _logger;

    public WorkflowDefinitionServiceAdapter(
        ErpSystem.Core.Interfaces.Services.IWorkflowDefinitionService coreService,
        ICurrentUserService currentUserService,
        ILogger<WorkflowDefinitionServiceAdapter> logger)
    {
        _coreService = coreService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<WorkflowDefinition> CreateWorkflowDefinitionAsync(CreateWorkflowDefinitionDto createDto)
    {
        // Map the DTO to entity - for now, create a basic workflow definition
        var definition = new WorkflowDefinition
        {
            Name = createDto.Name,
            Description = createDto.Description,
            EntityTypeId = Guid.Empty, // Need to resolve entity type from string
            TenantId = _currentUserService.TenantId ?? Guid.Empty,
            CreatedBy = _currentUserService.UserId?.ToString() ?? "System",
            IsActive = false
        };

        return await _coreService.CreateDefinitionAsync(definition);
    }

    public async Task<WorkflowDefinition> UpdateWorkflowDefinitionAsync(Guid id, UpdateWorkflowDefinitionDto updateDto)
    {
        var existingDefinition = await _coreService.GetDefinitionAsync(id);
        if (existingDefinition == null)
        {
            throw new InvalidOperationException($"Workflow definition with ID {id} not found");
        }

        existingDefinition.Name = updateDto.Name;
        existingDefinition.Description = updateDto.Description;

        return await _coreService.UpdateDefinitionAsync(existingDefinition);
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
        if (isActive)
        {
            await _coreService.ActivateDefinitionAsync(id);
        }
        else
        {
            await _coreService.DeactivateDefinitionAsync(id);
        }
    }

    public async Task DeleteWorkflowDefinitionAsync(Guid id)
    {
        var definition = await _coreService.GetDefinitionAsync(id);
        if (definition == null)
        {
            throw new InvalidOperationException($"Workflow definition with ID {id} not found");
        }

        // For now, just deactivate instead of deleting
        await _coreService.DeactivateDefinitionAsync(id);
        _logger.LogWarning("DeleteWorkflowDefinitionAsync called but only deactivation is implemented. ID: {Id}", id);
    }
}
