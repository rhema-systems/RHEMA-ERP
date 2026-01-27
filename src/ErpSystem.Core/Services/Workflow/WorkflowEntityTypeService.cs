using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Service implementation for workflow entity type management
/// </summary>
public class WorkflowEntityTypeService : IWorkflowEntityTypeService
{
    private readonly IWorkflowEntityTypeRepository _workflowEntityTypeRepository;
    private readonly ILogger<WorkflowEntityTypeService> _logger;

    public WorkflowEntityTypeService(
        IWorkflowEntityTypeRepository workflowEntityTypeRepository,
        ILogger<WorkflowEntityTypeService> logger)
    {
        _workflowEntityTypeRepository = workflowEntityTypeRepository;
        _logger = logger;
    }

    public async Task<WorkflowEntityType> CreateEntityTypeAsync(WorkflowEntityType entityType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating workflow entity type: {Name}", entityType.Name);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(entityType.Name))
        {
            throw new ArgumentException("Entity type name is required");
        }


        if (entityType.TenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID is required");
        }

        // Check for duplicate name
        var existingEntityType = await _workflowEntityTypeRepository.GetByNameAsync(entityType.Name, entityType.TenantId, cancellationToken);
        if (existingEntityType != null)
        {
            throw new InvalidOperationException($"An entity type with name '{entityType.Name}' already exists");
        }

        // Set default values
        entityType.Id = Guid.NewGuid();
        entityType.CreatedAt = DateTime.UtcNow;
        entityType.IsActive = true;

        // Save the entity type
        await _workflowEntityTypeRepository.AddAsync(entityType);
        await _workflowEntityTypeRepository.SaveChangesAsync();

        _logger.LogInformation("Created workflow entity type: {Id} - {Name}", entityType.Id, entityType.Name);
        return entityType;
    }

    public async Task<WorkflowEntityType> UpdateEntityTypeAsync(WorkflowEntityType entityType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating workflow entity type: {Id}", entityType.Id);

        var existingEntityType = await _workflowEntityTypeRepository.GetByIdAsync(entityType.Id) ?? throw new InvalidOperationException($"Workflow entity type with ID {entityType.Id} not found");

        // Check for duplicate name (excluding current entity type)
        var duplicateEntityType = await _workflowEntityTypeRepository.GetByNameAsync(entityType.Name, entityType.TenantId, cancellationToken);
        if (duplicateEntityType != null && duplicateEntityType.Id != entityType.Id)
        {
            throw new InvalidOperationException($"An entity type with name '{entityType.Name}' already exists");
        }

        // Update properties
        existingEntityType.Name = entityType.Name;
        existingEntityType.Description = entityType.Description;
        existingEntityType.UpdatedAt = DateTime.UtcNow;

        await _workflowEntityTypeRepository.SaveChangesAsync();

        _logger.LogInformation("Updated workflow entity type: {Id}", entityType.Id);
        return existingEntityType;
    }

    public async Task<WorkflowEntityType?> GetEntityTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _workflowEntityTypeRepository.GetByIdAsync(id);
    }

    public async Task<WorkflowEntityType?> GetEntityTypeByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _workflowEntityTypeRepository.GetByNameAsync(name, tenantId, cancellationToken);
    }

    public async Task<IEnumerable<WorkflowEntityType>> GetActiveEntityTypesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _workflowEntityTypeRepository.GetActiveEntityTypesAsync(tenantId, cancellationToken);
    }

    public async Task ActivateEntityTypeAsync(Guid entityTypeId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activating workflow entity type: {Id}", entityTypeId);

        var entityType = await _workflowEntityTypeRepository.GetByIdAsync(entityTypeId) ?? throw new InvalidOperationException($"Workflow entity type with ID {entityTypeId} not found");
        if (entityType.IsActive)
        {
            _logger.LogInformation("Workflow entity type {Id} is already active", entityTypeId);
            return;
        }

        entityType.IsActive = true;
        entityType.UpdatedAt = DateTime.UtcNow;

        await _workflowEntityTypeRepository.SaveChangesAsync();

        _logger.LogInformation("Activated workflow entity type: {Id}", entityTypeId);
    }

    public async Task DeactivateEntityTypeAsync(Guid entityTypeId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deactivating workflow entity type: {Id}", entityTypeId);

        var entityType = await _workflowEntityTypeRepository.GetByIdAsync(entityTypeId) ?? throw new InvalidOperationException($"Workflow entity type with ID {entityTypeId} not found");
        if (!entityType.IsActive)
        {
            _logger.LogInformation("Workflow entity type {Id} is already inactive", entityTypeId);
            return;
        }

        // Check if entity type is being used by active workflow definitions
        if (entityType.WorkflowDefinitions?.Any(wd => wd.IsActive) == true)
        {
            throw new InvalidOperationException("Cannot deactivate entity type that is used by active workflow definitions");
        }

        entityType.IsActive = false;
        entityType.UpdatedAt = DateTime.UtcNow;

        await _workflowEntityTypeRepository.SaveChangesAsync();

        _logger.LogInformation("Deactivated workflow entity type: {Id}", entityTypeId);
    }
}
