using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers;

/// <summary>
/// API controller for workflow management and administration
/// Provides endpoints for managing workflow definitions, instances, and execution
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowDefinitionService _workflowDefinitionService;
    private readonly IWorkflowInstanceService _workflowInstanceService;
    private readonly IWorkflowStepService _workflowStepService;
    private readonly IWorkflowApprovalService _workflowApprovalService;
    private readonly IWorkflowConditionEvaluator _workflowConditionEvaluator;
    private readonly IWorkflowNotificationService _workflowNotificationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IWorkflowEngine workflowEngine,
        IWorkflowDefinitionService workflowDefinitionService,
        IWorkflowInstanceService workflowInstanceService,
        IWorkflowStepService workflowStepService,
        IWorkflowApprovalService workflowApprovalService,
        IWorkflowConditionEvaluator workflowConditionEvaluator,
        IWorkflowNotificationService workflowNotificationService,
        ICurrentUserService currentUserService,
        ILogger<WorkflowController> logger)
    {
        _workflowEngine = workflowEngine;
        _workflowDefinitionService = workflowDefinitionService;
        _workflowInstanceService = workflowInstanceService;
        _workflowStepService = workflowStepService;
        _workflowApprovalService = workflowApprovalService;
        _workflowConditionEvaluator = workflowConditionEvaluator;
        _workflowNotificationService = workflowNotificationService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    #region Workflow Definition Management

    /// <summary>
    /// Gets all workflow definitions with optional filtering
    /// </summary>
    [HttpGet("definitions")]
    public async Task<ActionResult<IEnumerable<WorkflowDefinitionAdminDto>>> GetWorkflowDefinitions(
        [FromQuery] WorkflowDefinitionFilterDto filter)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized();

            _logger.LogInformation("User {UserId} is retrieving workflow definitions", currentUserId);

            var definitions = await _workflowDefinitionService.GetWorkflowDefinitionsAsync(filter.EntityType ?? string.Empty);
            
            // Convert to admin DTOs with additional administrative properties
            var adminDtos = definitions.Select(def => new WorkflowDefinitionAdminDto
            {
                Id = def.Id,
                Name = def.Name,
                Description = def.Description,
                EntityType = def.EntityType?.Name ?? "Unknown",
                Version = def.Version,
                IsActive = def.IsActive,
                Configuration = def.Configuration,
                CreatedDate = def.CreatedAt,
                LastModifiedDate = def.UpdatedAt ?? def.CreatedAt,
                CreatedByName = def.CreatedBy ?? "Unknown",
                LastModifiedByName = def.UpdatedBy ?? def.CreatedBy ?? "Unknown",
                StepCount = def.Steps?.Count ?? 0,
                ActiveInstancesCount = 0, // This would need to be queried separately for performance
                LastUsedDate = null // This would need to be queried from instance data
            });

            // Apply filtering
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                adminDtos = adminDtos.Where(d => d.Name.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                                (d.Description?.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (filter.IsActive.HasValue)
            {
                adminDtos = adminDtos.Where(d => d.IsActive == filter.IsActive.Value);
            }

            if (filter.CreatedAfter.HasValue)
            {
                adminDtos = adminDtos.Where(d => d.CreatedDate >= filter.CreatedAfter.Value);
            }

            if (filter.CreatedBefore.HasValue)
            {
                adminDtos = adminDtos.Where(d => d.CreatedDate <= filter.CreatedBefore.Value);
            }

            // Apply sorting
            adminDtos = filter.SortBy?.ToLower() switch
            {
                "name" => filter.SortDescending ? adminDtos.OrderByDescending(d => d.Name) : adminDtos.OrderBy(d => d.Name),
                "entitytype" => filter.SortDescending ? adminDtos.OrderByDescending(d => d.EntityType) : adminDtos.OrderBy(d => d.EntityType),
                "createddate" => filter.SortDescending ? adminDtos.OrderByDescending(d => d.CreatedDate) : adminDtos.OrderBy(d => d.CreatedDate),
                "isactive" => filter.SortDescending ? adminDtos.OrderByDescending(d => d.IsActive) : adminDtos.OrderBy(d => d.IsActive),
                _ => adminDtos.OrderBy(d => d.Name)
            };

            // Apply pagination
            var paginatedDtos = adminDtos
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            return Ok(paginatedDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow definitions");
            return StatusCode(500, "An error occurred while retrieving workflow definitions");
        }
    }

    /// <summary>
    /// Gets a specific workflow definition by ID
    /// </summary>
    [HttpGet("definitions/{id}")]
    public async Task<ActionResult<WorkflowDefinitionDto>> GetWorkflowDefinition(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized();

            _logger.LogInformation("User {UserId} is retrieving workflow definition {WorkflowId}", currentUserId, id);

            var definition = await _workflowDefinitionService.GetWorkflowDefinitionAsync(id);
            if (definition == null)
                return NotFound("Workflow definition not found");

            var dto = MapToWorkflowDefinitionDto(definition);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow definition {WorkflowId}", id);
            return StatusCode(500, "An error occurred while retrieving the workflow definition");
        }
    }

    /// <summary>
    /// Creates a new workflow definition
    /// </summary>
    [HttpPost("definitions")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin")]
    public async Task<ActionResult<WorkflowDefinitionDto>> CreateWorkflowDefinition(
        CreateWorkflowDefinitionAdminDto createDto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized();

            _logger.LogInformation("User {UserId} is creating workflow definition {WorkflowName}", currentUserId, createDto.Name);

            var workflowDto = new CreateWorkflowDefinitionDto
            {
                Name = createDto.Name,
                Description = createDto.Description,
                EntityType = createDto.EntityType,
                Configuration = createDto.Configuration,
                CreatedById = currentUserId.Value,
                Steps = new List<CreateWorkflowStepDto>(), // Will be added separately
                Transitions = new List<CreateWorkflowTransitionDto>() // Will be added separately
            };

            var definition = await _workflowDefinitionService.CreateWorkflowDefinitionAsync(workflowDto);

            if (!createDto.IsActive)
            {
                await _workflowDefinitionService.SetWorkflowDefinitionActiveAsync(definition.Id, false, currentUserId.Value);
            }

            var responseDto = MapToWorkflowDefinitionDto(definition);
            return CreatedAtAction(
                nameof(GetWorkflowDefinition),
                new { id = definition.Id },
                responseDto
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow definition");
            return StatusCode(500, "An error occurred while creating the workflow definition");
        }
    }

    /// <summary>
    /// Updates an existing workflow definition
    /// </summary>
    [HttpPut("definitions/{id}")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin")]
    public async Task<ActionResult<WorkflowDefinitionDto>> UpdateWorkflowDefinition(
        Guid id, UpdateWorkflowDefinitionAdminDto updateDto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized();

            _logger.LogInformation("User {UserId} is updating workflow definition {WorkflowId}", currentUserId, id);

            var workflowDto = new UpdateWorkflowDefinitionDto
            {
                Name = updateDto.Name,
                Description = updateDto.Description,
                Configuration = updateDto.Configuration,
                LastModifiedById = currentUserId.Value
            };

            var definition = await _workflowDefinitionService.UpdateWorkflowDefinitionAsync(id, workflowDto);
            
            await _workflowDefinitionService.SetWorkflowDefinitionActiveAsync(id, updateDto.IsActive, currentUserId.Value);

            var responseDto = MapToWorkflowDefinitionDto(definition);
            return Ok(responseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow definition {WorkflowId}", id);
            return StatusCode(500, "An error occurred while updating the workflow definition");
        }
    }

    /// <summary>
    /// Deletes a workflow definition
    /// </summary>
    [HttpDelete("definitions/{id}")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin")]
    public async Task<ActionResult> DeleteWorkflowDefinition(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized();

            _logger.LogInformation("User {UserId} is deleting workflow definition {WorkflowId}", currentUserId, id);

            await _workflowDefinitionService.DeleteWorkflowDefinitionAsync(id);
            return Ok(new { message = "Workflow definition deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow definition {WorkflowId}", id);
            return StatusCode(500, "An error occurred while deleting the workflow definition");
        }
    }

    /// <summary>
    /// Validates a workflow definition structure
    /// </summary>
    [HttpPost("definitions/{id}/validate")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin")]
    public async Task<ActionResult<WorkflowValidationResult>> ValidateWorkflowDefinition(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized();

            _logger.LogInformation("User {UserId} is validating workflow definition {WorkflowId}", currentUserId, id);

            var result = await _workflowDefinitionService.ValidateWorkflowDefinitionAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating workflow definition {WorkflowId}", id);
            return StatusCode(500, "An error occurred while validating the workflow definition");
        }
    }

    #endregion

    #region Administration and Statistics

    /// <summary>
    /// Gets workflow administration summary statistics
    /// </summary>
    [HttpGet("administration/summary")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin")]
    public async Task<ActionResult<WorkflowSummaryDto>> GetWorkflowSummary()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized();

            _logger.LogInformation("User {UserId} is retrieving workflow summary", currentUserId);

            // This would typically be implemented with repository methods for better performance
            var summary = new WorkflowSummaryDto
            {
                TotalWorkflowDefinitions = 0, // Would query from repository
                ActiveWorkflowDefinitions = 0, // Would query from repository
                TotalWorkflowInstances = 0, // Would query from repository
                ActiveWorkflowInstances = 0, // Would query from repository
                CompletedWorkflowInstances = 0, // Would query from repository
                FailedWorkflowInstances = 0, // Would query from repository
                InstancesByEntityType = new Dictionary<string, int>(), // Would query from repository
                InstancesByStatus = new Dictionary<string, int>(), // Would query from repository
                LastUpdated = DateTime.UtcNow
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow summary");
            return StatusCode(500, "An error occurred while retrieving the workflow summary");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the current authenticated user ID
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return null;

        return userId;
    }

    /// <summary>
    /// Maps a WorkflowDefinition entity to a WorkflowDefinitionDto
    /// </summary>
    private WorkflowDefinitionDto MapToWorkflowDefinitionDto(WorkflowDefinition definition)
    {
        return new WorkflowDefinitionDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            EntityType = definition.EntityType?.Name ?? "Unknown",
            Configuration = definition.Configuration,
            IsActive = definition.IsActive,
            Version = definition.Version,
            CreatedById = Guid.Empty, // We don't have user ID as Guid, only string username
            CreatedByName = definition.CreatedBy ?? "Unknown",
            CreatedDate = definition.CreatedAt,
            LastModifiedById = null, // We don't have user ID as Guid, only string username
            LastModifiedByName = definition.UpdatedBy,
            LastModifiedDate = definition.UpdatedAt,
            Steps = definition.Steps?.Select(MapToWorkflowStepDto).ToList() ?? new List<WorkflowStepDto>(),
            Transitions = definition.Transitions?.Select(MapToWorkflowTransitionDto).ToList() ?? new List<WorkflowTransitionDto>()
        };
    }

    /// <summary>
    /// Maps a WorkflowStep entity to a WorkflowStepDto
    /// </summary>
    private WorkflowStepDto MapToWorkflowStepDto(WorkflowStep step)
    {
        return new WorkflowStepDto
        {
            Id = step.Id,
            Name = step.Name,
            Description = step.Description,
            StepType = step.StepType,
            Order = step.Order,
            IsRequired = step.IsRequired,
            RequiredRole = step.RequiredRole,
            EstimatedHours = step.EstimatedHours,
            Configuration = step.Configuration != null ? 
                System.Text.Json.JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(step.Configuration) : null
        };
    }

    /// <summary>
    /// Maps a WorkflowTransition entity to a WorkflowTransitionDto
    /// </summary>
    private WorkflowTransitionDto MapToWorkflowTransitionDto(WorkflowTransition transition)
    {
        return new WorkflowTransitionDto
        {
            Id = transition.Id,
            FromStepId = transition.FromStepId,
            ToStepId = transition.ToStepId,
            Name = transition.Name,
            Description = transition.Description,
            IsDefault = transition.IsDefault,
            Priority = transition.Priority,
            Condition = transition.Condition != null ? 
                System.Text.Json.JsonSerializer.Deserialize<WorkflowConditionDto>(transition.Condition) : null
        };
    }

    #endregion
}

#region Request DTOs

/// <summary>
/// Request DTO for starting a workflow
/// </summary>
public class StartWorkflowRequest
{
    public string WorkflowName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public object? DataContext { get; set; }
}

/// <summary>
/// Request DTO for executing a workflow step
/// </summary>
public class ExecuteStepRequest
{
    public object? StepData { get; set; }
}

/// <summary>
/// Request DTO for cancelling a workflow
/// </summary>
public class CancelWorkflowRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for processing a workflow step
/// </summary>
public class ProcessStepRequest
{
    public ErpSystem.Core.Enums.WorkflowStepAction Action { get; set; }
    public object? ResultData { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// Request DTO for assigning a step to a user
/// </summary>
public class AssignStepRequest
{
    public Guid AssignedToId { get; set; }
}

/// <summary>
/// Request DTO for processing an approval
/// </summary>
public class ProcessApprovalRequest
{
    public ErpSystem.Core.Enums.WorkflowApprovalAction Action { get; set; }
    public string? Comments { get; set; }
}

#endregion