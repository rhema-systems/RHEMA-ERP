using System.Security.Claims;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly IWorkflowService _workflowService;
    private readonly IWorkflowDefinitionService _workflowDefinitionService;
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly IWorkflowInstanceService _workflowInstanceService;
    private readonly IWorkflowStepService _workflowStepService;
    private readonly IWorkflowApprovalService _workflowApprovalService;
    private readonly IWorkflowConditionEvaluator _workflowConditionEvaluator;
    private readonly IWorkflowNotificationService _workflowNotificationService;
    private readonly IWorkflowInstanceRepository _workflowInstanceRepository;
    private readonly IWorkflowStepInstanceRepository _workflowStepInstanceRepository;
    private readonly IWorkflowApprovalRepository _workflowApprovalRepository;
    private readonly IWorkflowEntityTypeRepository _workflowEntityTypeRepository;
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IAppEventBus _appEventBus;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IWorkflowEngine workflowEngine,
        IWorkflowService workflowService,
        IWorkflowDefinitionService workflowDefinitionService,
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IWorkflowInstanceService workflowInstanceService,
        IWorkflowStepService workflowStepService,
        IWorkflowApprovalService workflowApprovalService,
        IWorkflowConditionEvaluator workflowConditionEvaluator,
        IWorkflowNotificationService workflowNotificationService,
        IWorkflowInstanceRepository workflowInstanceRepository,
        IWorkflowStepInstanceRepository workflowStepInstanceRepository,
        IWorkflowApprovalRepository workflowApprovalRepository,
        IWorkflowEntityTypeRepository workflowEntityTypeRepository,
        ErpSystem.Data.ApplicationDbContext db,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IAppEventBus appEventBus,
        ICurrentUserService currentUserService,
        ILogger<WorkflowController> logger)
    {
        _workflowEngine = workflowEngine;
        _workflowService = workflowService;
        _workflowDefinitionService = workflowDefinitionService;
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _workflowInstanceService = workflowInstanceService;
        _workflowStepService = workflowStepService;
        _workflowApprovalService = workflowApprovalService;
        _workflowConditionEvaluator = workflowConditionEvaluator;
        _workflowNotificationService = workflowNotificationService;
        _workflowInstanceRepository = workflowInstanceRepository;
        _workflowStepInstanceRepository = workflowStepInstanceRepository;
        _workflowApprovalRepository = workflowApprovalRepository;
        _workflowEntityTypeRepository = workflowEntityTypeRepository;
        _db = db;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _appEventBus = appEventBus;
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
            if (filter.Page < 1)
            {
                filter.Page = 1;
            }

            if (filter.PageSize <= 0)
            {
                filter.PageSize = 25;
            }

            if (filter.PageSize > 100)
            {
                filter.PageSize = 100;
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("User {UserId} is retrieving workflow definitions", currentUserId);

            // IMPORTANT: Admin listing must include inactive workflows too (so users can activate / continue editing).
            // Do not rely on the IWorkflowDefinitionService.GetWorkflowDefinitionsAsync contract here because it
            // returns active definitions only (used by runtime execution).
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                return Unauthorized();
            }

            var defQuery = _workflowDefinitionRepository
                .GetQueryable()
                .AsNoTracking()
                .Where(d => !d.IsDeleted && d.TenantId == tenantId.Value);

            if (!string.IsNullOrWhiteSpace(filter.EntityType))
            {
                var et = filter.EntityType.Trim();
                defQuery = defQuery.Where(d => d.EntityType.Name == et || d.EntityType.Code == et);
            }

            if (filter.IsActive.HasValue)
            {
                defQuery = defQuery.Where(d => d.IsActive == filter.IsActive.Value);
            }

            if (filter.CreatedAfter.HasValue)
            {
                defQuery = defQuery.Where(d => d.CreatedAt >= filter.CreatedAfter.Value);
            }

            if (filter.CreatedBefore.HasValue)
            {
                defQuery = defQuery.Where(d => d.CreatedAt <= filter.CreatedBefore.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var s = filter.SearchTerm.Trim();
                defQuery = defQuery.Where(d =>
                    d.Name.Contains(s) ||
                    (d.Description != null && d.Description.Contains(s)));
            }

            var adminQuery = defQuery.Select(def => new WorkflowDefinitionAdminDto
            {
                Id = def.Id,
                Name = def.Name,
                Description = def.Description,
                EntityType = def.EntityType.Name,
                Version = def.Version,
                IsActive = def.IsActive,
                Configuration = def.Configuration,
                CreatedDate = def.CreatedAt,
                LastModifiedDate = def.UpdatedAt ?? def.CreatedAt,
                CreatedByName = def.CreatedBy ?? "Unknown",
                LastModifiedByName = def.UpdatedBy ?? def.CreatedBy ?? "Unknown",
                StepCount = def.Steps.Count,
                ActiveInstancesCount = def.Instances.Count(i =>
                    i.Status == WorkflowInstanceStatus.Created ||
                    i.Status == WorkflowInstanceStatus.InProgress ||
                    i.Status == WorkflowInstanceStatus.Waiting ||
                    i.Status == WorkflowInstanceStatus.Suspended),
                LastUsedDate = null
            });

            // Apply sorting
            adminQuery = filter.SortBy?.ToLower() switch
            {
                "name" => filter.SortDescending ? adminQuery.OrderByDescending(d => d.Name) : adminQuery.OrderBy(d => d.Name),
                "entitytype" => filter.SortDescending ? adminQuery.OrderByDescending(d => d.EntityType) : adminQuery.OrderBy(d => d.EntityType),
                "createddate" => filter.SortDescending ? adminQuery.OrderByDescending(d => d.CreatedDate) : adminQuery.OrderBy(d => d.CreatedDate),
                "isactive" => filter.SortDescending ? adminQuery.OrderByDescending(d => d.IsActive) : adminQuery.OrderBy(d => d.IsActive),
                _ => adminQuery.OrderBy(d => d.Name)
            };

            var totalCount = await adminQuery.CountAsync();

            // Apply pagination
            var paginatedDtos = await adminQuery
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = paginatedDtos,
                metadata = new
                {
                    totalCount,
                    page = filter.Page,
                    pageSize = filter.PageSize
                }
            });
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
            {
                return Unauthorized();
            }

            _logger.LogInformation("User {UserId} is retrieving workflow definition {WorkflowId}", currentUserId, id);

            var definition = await _workflowDefinitionService.GetWorkflowDefinitionAsync(id);
            if (definition == null)
            {
                return NotFound("Workflow definition not found");
            }

            var dto = MapToWorkflowDefinitionDto(definition);
            return Ok(new
            {
                success = true,
                data = dto
            });
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
    [Authorize]
    public async Task<ActionResult<WorkflowDefinitionDto>> CreateWorkflowDefinition(
        CreateWorkflowDefinitionAdminDto createDto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("User {UserId} is creating workflow definition {WorkflowName}", currentUserId, createDto.Name);

            var workflowDto = new CreateWorkflowDefinitionDto
            {
                Name = createDto.Name,
                Description = createDto.Description,
                EntityType = createDto.EntityType,
                Configuration = createDto.Configuration,
                CreatedById = currentUserId.Value,
                Steps = createDto.Steps ?? new List<CreateWorkflowStepDto>(),
                Transitions = createDto.Transitions ?? new List<CreateWorkflowTransitionDto>()
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
                new
                {
                    success = true,
                    data = responseDto
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            // Expected validation / conflict scenarios should not be treated as 500s.
            // The UI can show a friendly message and optionally navigate to the existing definition.
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                Guid? existingId = null;
                try
                {
                    var existing = await _workflowDefinitionService.GetWorkflowDefinitionAsync(createDto.Name, createDto.EntityType);
                    existingId = existing?.Id;
                }
                catch
                {
                    // Best-effort only; don't mask the original conflict.
                }

                _logger.LogWarning("Workflow definition create conflict for user {UserId}: {Message}", GetCurrentUserId(), ex.Message);
                return Conflict(new
                {
                    success = false,
                    error = ex.Message,
                    existingDefinitionId = existingId
                });
            }

            _logger.LogWarning(ex, "Invalid operation while creating workflow definition");
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
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
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<WorkflowDefinitionDto>> UpdateWorkflowDefinition(
        Guid id, UpdateWorkflowDefinitionAdminDto updateDto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("User {UserId} is updating workflow definition {WorkflowId}", currentUserId, id);

            var workflowDto = new UpdateWorkflowDefinitionDto
            {
                Name = updateDto.Name,
                Description = updateDto.Description,
                Configuration = updateDto.Configuration,
                LastModifiedById = currentUserId.Value,
                Steps = updateDto.Steps,
                Transitions = updateDto.Transitions
            };

            var definition = await _workflowDefinitionService.UpdateWorkflowDefinitionAsync(id, workflowDto);

            await _workflowDefinitionService.SetWorkflowDefinitionActiveAsync(id, updateDto.IsActive, currentUserId.Value);

            var responseDto = MapToWorkflowDefinitionDto(definition);
            return Ok(new
            {
                success = true,
                data = responseDto
            });
        }
        catch (InvalidOperationException ex)
        {
            // Validation/activation errors should be surfaced to the UI as a friendly message.
            _logger.LogWarning(ex, "Invalid operation while updating workflow definition {WorkflowId}", id);
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error updating workflow definition {WorkflowId}", id);
            return StatusCode(500, new
            {
                success = false,
                error = "A database error occurred while updating the workflow definition."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow definition {WorkflowId}", id);
            return StatusCode(500, new
            {
                success = false,
                error = "An error occurred while updating the workflow definition."
            });
        }
    }

    /// <summary>
    /// Deletes a workflow definition
    /// </summary>
    [HttpDelete("definitions/{id}")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> DeleteWorkflowDefinition(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("User {UserId} is deleting workflow definition {WorkflowId}", currentUserId, id);

            await _workflowDefinitionService.DeleteWorkflowDefinitionAsync(id);
            return Ok(new
            {
                success = true,
                message = "Workflow definition deleted successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while deleting workflow definition {WorkflowId}", id);
            return BadRequest(new
            {
                success = false,
                error = ex.Message
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error deleting workflow definition {WorkflowId}", id);
            return StatusCode(500, new
            {
                success = false,
                error = "A database error occurred while deleting the workflow definition."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow definition {WorkflowId}", id);
            return StatusCode(500, new
            {
                success = false,
                error = "An error occurred while deleting the workflow definition."
            });
        }
    }

    /// <summary>
    /// Validates a workflow definition structure
    /// </summary>
    [HttpPost("definitions/{id}/validate")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<WorkflowValidationResult>> ValidateWorkflowDefinition(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            _logger.LogInformation("User {UserId} is validating workflow definition {WorkflowId}", currentUserId, id);

            var result = await _workflowDefinitionService.ValidateWorkflowDefinitionAsync(id);
            return Ok(new
            {
                success = true,
                data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating workflow definition {WorkflowId}", id);
            return StatusCode(500, "An error occurred while validating the workflow definition");
        }
    }

    #endregion

    #region Workflow Instance Management

    /// <summary>
    /// Gets workflow instances with optional filtering and pagination
    /// </summary>
    [HttpGet("instances")]
    public async Task<ActionResult<IEnumerable<WorkflowStatusDto>>> GetWorkflowInstances(
        [FromQuery] WorkflowInstanceFilterDto filter)
    {
        try
        {
            if (filter.Page < 1)
            {
                filter.Page = 1;
            }

            if (filter.PageSize <= 0)
            {
                filter.PageSize = 25;
            }

            if (filter.PageSize > 100)
            {
                filter.PageSize = 100;
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var query = _workflowInstanceRepository.GetQueryable();

            if (tenantId != Guid.Empty)
            {
                query = query.Where(i => i.TenantId == tenantId);
            }

            if (filter.WorkflowDefinitionId.HasValue)
            {
                query = query.Where(i => i.WorkflowDefinitionId == filter.WorkflowDefinitionId.Value);
            }

            if (filter.EntityId.HasValue)
            {
                query = query.Where(i => i.EntityId == filter.EntityId.Value);
            }

            if (filter.InitiatedById.HasValue)
            {
                query = query.Where(i => i.InitiatedById == filter.InitiatedById.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.EntityType))
            {
                var entityType = await _workflowEntityTypeRepository.GetByNameAsync(filter.EntityType, tenantId);
                if (entityType != null)
                {
                    query = query.Where(i => i.EntityTypeId == entityType.Id);
                }
                else
                {
                    return Ok(new
                    {
                        success = true,
                        data = Array.Empty<WorkflowStatusDto>(),
                        metadata = new
                        {
                            totalCount = 0,
                            page = filter.Page,
                            pageSize = filter.PageSize
                        }
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.Status) &&
                Enum.TryParse<WorkflowInstanceStatus>(filter.Status, true, out var status))
            {
                query = query.Where(i => i.Status == status);
            }

            if (filter.StartedAfter.HasValue)
            {
                query = query.Where(i => (i.StartedDate ?? i.CreatedDate) >= filter.StartedAfter.Value);
            }

            if (filter.StartedBefore.HasValue)
            {
                query = query.Where(i => (i.StartedDate ?? i.CreatedDate) <= filter.StartedBefore.Value);
            }

            if (filter.CompletedAfter.HasValue)
            {
                query = query.Where(i => i.CompletedDate.HasValue && i.CompletedDate.Value >= filter.CompletedAfter.Value);
            }

            if (filter.CompletedBefore.HasValue)
            {
                query = query.Where(i => i.CompletedDate.HasValue && i.CompletedDate.Value <= filter.CompletedBefore.Value);
            }

            query = ApplyInstanceSorting(query, filter);

            var totalCount = await query.CountAsync();
            var instances = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var statuses = new List<WorkflowStatusDto>();
            foreach (var instance in instances)
            {
                statuses.Add(await _workflowEngine.GetWorkflowStatusAsync(instance.Id));
            }

            return Ok(new
            {
                success = true,
                data = statuses,
                metadata = new
                {
                    totalCount,
                    page = filter.Page,
                    pageSize = filter.PageSize
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow instances");
            return StatusCode(500, "An error occurred while retrieving workflow instances");
        }
    }

    /// <summary>
    /// Gets available condition variables for a given entity type
    /// </summary>
    [HttpGet("variables")]
    [Authorize]
    public async Task<ActionResult> GetAvailableConditionVariables([FromQuery] string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return BadRequest("Entity type is required");
        }

        try
        {
            var variables = await _workflowConditionEvaluator.GetAvailableVariablesAsync(entityType);
            var response = variables.Select(v => new WorkflowVariableInfoResponse
            {
                Name = v.Name,
                DisplayName = v.DisplayName,
                DataType = v.DataType?.Name ?? "object",
                Description = v.Description,
                PossibleValues = v.PossibleValues
            }).ToList();

            return Ok(new
            {
                success = true,
                data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow variables for entity type {EntityType}", entityType);
            return StatusCode(500, "An error occurred while retrieving workflow variables");
        }
    }

    /// <summary>
    /// Gets a lightweight workflow summary for a specific entity record (current step, pending approvers, and whether the current user can approve).
    /// Intended for module UIs to standardize approval UX.
    /// </summary>
    [HttpGet("entity-summary")]
    public async Task<ActionResult> GetEntitySummary([FromQuery] string entityType, [FromQuery] Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return BadRequest("Entity type is required");
        }

        if (entityId == Guid.Empty)
        {
            return BadRequest("Entity id is required");
        }

        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return Unauthorized();
            }

            // Resolve entity type by name/code (case-insensitive).
            var entityTypeRecord = await _workflowEntityTypeRepository.GetByNameAsync(entityType, tenantId);
            if (entityTypeRecord == null)
            {
                var activeTypes = await _workflowEntityTypeRepository.GetActiveEntityTypesAsync(tenantId);
                entityTypeRecord = activeTypes.FirstOrDefault(et =>
                    string.Equals(et.Code, entityType, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(et.Name, entityType, StringComparison.OrdinalIgnoreCase));
            }

            if (entityTypeRecord == null)
            {
                return Ok(new
                {
                    success = true,
                    data = new WorkflowEntitySummaryDto
                    {
                        EntityType = entityType,
                        EntityId = entityId,
                        HasActiveInstance = false,
                        CanCurrentUserApprove = false
                    }
                });
            }

            var instances = await _workflowInstanceRepository.GetByEntityAsync(entityTypeRecord.Id, entityId.ToString());
            var activeInstance = instances
                .Where(i =>
                    i.Status == WorkflowInstanceStatus.Created ||
                    i.Status == WorkflowInstanceStatus.InProgress ||
                    i.Status == WorkflowInstanceStatus.Waiting ||
                    i.Status == WorkflowInstanceStatus.Suspended)
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
                .FirstOrDefault();

            if (activeInstance == null)
            {
                return Ok(new
                {
                    success = true,
                    data = new WorkflowEntitySummaryDto
                    {
                        EntityType = entityTypeRecord.Code ?? entityTypeRecord.Name,
                        EntityId = entityId,
                        HasActiveInstance = false,
                        CanCurrentUserApprove = false
                    }
                });
            }

            var status = await _workflowEngine.GetWorkflowStatusAsync(activeInstance.Id);
            var stepInfo = await _workflowService.GetCurrentWorkflowStepAsync(entityType, entityId);

            var currentStepName = stepInfo?.StepName;
            var currentStepInstanceId = stepInfo?.Id;

            if (string.IsNullOrWhiteSpace(currentStepName))
            {
                // Fallback to last pending/in-progress step instance name from the status view.
                currentStepName = status.Steps
                    .LastOrDefault(s => s.Status == WorkflowStepInstanceStatus.Pending ||
                                        s.Status == WorkflowStepInstanceStatus.InProgress)
                    ?.StepName;
            }

            var pendingApprovalsForCurrentStep = status.PendingApprovals
                .Where(a => a.Status == WorkflowApprovalStatus.Pending &&
                            (string.IsNullOrWhiteSpace(currentStepName) || a.StepName == currentStepName))
                .Select(a => new WorkflowPendingApproverDto
                {
                    ApproverId = a.ApproverId,
                    ApproverName = a.ApproverName,
                    ApproverRole = null
                })
                .ToList();

            var canApprove = await _workflowService.CanUserApproveAsync(entityType, entityId, currentUserId.Value);

            var summary = new WorkflowEntitySummaryDto
            {
                EntityType = status.EntityType,
                EntityId = entityId,
                HasActiveInstance = true,
                WorkflowInstanceId = status.WorkflowInstanceId,
                WorkflowName = status.WorkflowName,
                Status = status.Status,
                CurrentStepName = currentStepName,
                CurrentStepInstanceId = currentStepInstanceId,
                CanCurrentUserApprove = canApprove,
                PendingApprovers = pendingApprovalsForCurrentStep
            };

            return Ok(new
            {
                success = true,
                data = summary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow entity summary for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, "An error occurred while retrieving workflow summary");
        }
    }

    /// <summary>
    /// Batch variant of <see cref="GetEntitySummary"/> for list/grid UIs to avoid per-row calls.
    /// </summary>
    [HttpPost("entity-summary/batch")]
    public async Task<ActionResult> GetEntitySummaryBatch([FromBody] WorkflowEntitySummaryBatchRequestDto request)
    {
        if (request == null || request.Entities == null || request.Entities.Count == 0)
        {
            return BadRequest("At least one entity reference is required");
        }

        // Protect the server from excessively large UI requests.
        if (request.Entities.Count > 200)
        {
            return BadRequest("Too many entities requested (max 200)");
        }

        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return Unauthorized();
            }

            // Resolve entity types once for the tenant to avoid per-row lookups.
            var activeTypes = await _workflowEntityTypeRepository.GetActiveEntityTypesAsync(tenantId);

            WorkflowEntityType? ResolveEntityTypeRecord(string type)
            {
                if (string.IsNullOrWhiteSpace(type)) return null;
                return activeTypes.FirstOrDefault(et =>
                    string.Equals(et.Code, type, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(et.Name, type, StringComparison.OrdinalIgnoreCase));
            }

            async Task<WorkflowEntitySummaryDto> BuildSummaryAsync(string requestedEntityType, Guid requestedEntityId)
            {
                if (string.IsNullOrWhiteSpace(requestedEntityType) || requestedEntityId == Guid.Empty)
                {
                    return new WorkflowEntitySummaryDto
                    {
                        EntityType = requestedEntityType ?? string.Empty,
                        EntityId = requestedEntityId,
                        HasActiveInstance = false,
                        CanCurrentUserApprove = false
                    };
                }

                // Resolve entity type record by code/name (case-insensitive).
                var entityTypeRecord = ResolveEntityTypeRecord(requestedEntityType);
                if (entityTypeRecord == null)
                {
                    // Backward-compatible fallback: some UIs may send the display name while the tenant uses code.
                    entityTypeRecord = await _workflowEntityTypeRepository.GetByNameAsync(requestedEntityType, tenantId);
                }

                if (entityTypeRecord == null)
                {
                    return new WorkflowEntitySummaryDto
                    {
                        EntityType = requestedEntityType,
                        EntityId = requestedEntityId,
                        HasActiveInstance = false,
                        CanCurrentUserApprove = false
                    };
                }

                var instances = await _workflowInstanceRepository.GetByEntityAsync(entityTypeRecord.Id, requestedEntityId.ToString());
                var activeInstance = instances
                    .Where(i =>
                        i.Status == WorkflowInstanceStatus.Created ||
                        i.Status == WorkflowInstanceStatus.InProgress ||
                        i.Status == WorkflowInstanceStatus.Waiting ||
                        i.Status == WorkflowInstanceStatus.Suspended)
                    .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
                    .FirstOrDefault();

                if (activeInstance == null)
                {
                    return new WorkflowEntitySummaryDto
                    {
                        EntityType = entityTypeRecord.Code ?? entityTypeRecord.Name ?? requestedEntityType,
                        EntityId = requestedEntityId,
                        HasActiveInstance = false,
                        CanCurrentUserApprove = false
                    };
                }

                var status = await _workflowEngine.GetWorkflowStatusAsync(activeInstance.Id);

                // Use the canonical entity type string (code preferred) so workflow service lookups are consistent.
                var canonicalEntityType = entityTypeRecord.Code ?? entityTypeRecord.Name ?? requestedEntityType;
                var stepInfo = await _workflowService.GetCurrentWorkflowStepAsync(canonicalEntityType, requestedEntityId);

                var currentStepName = stepInfo?.StepName;
                var currentStepInstanceId = stepInfo?.Id;

                if (string.IsNullOrWhiteSpace(currentStepName))
                {
                    currentStepName = status.Steps
                        .LastOrDefault(s => s.Status == WorkflowStepInstanceStatus.Pending ||
                                            s.Status == WorkflowStepInstanceStatus.InProgress)
                        ?.StepName;
                }

                var normalizedStepName = currentStepName?.Trim();
                var pendingApprovalsForCurrentStep = status.PendingApprovals
                    .Where(a => a.Status == WorkflowApprovalStatus.Pending &&
                                (string.IsNullOrWhiteSpace(normalizedStepName) ||
                                 string.Equals(a.StepName?.Trim(), normalizedStepName, StringComparison.OrdinalIgnoreCase)))
                    .GroupBy(a => a.ApproverId)
                    .Select(g => g.First())
                    .Select(a => new WorkflowPendingApproverDto
                    {
                        ApproverId = a.ApproverId,
                        ApproverName = a.ApproverName,
                        ApproverRole = null
                    })
                    .ToList();

                var canApprove = await _workflowService.CanUserApproveAsync(canonicalEntityType, requestedEntityId, currentUserId.Value);

                return new WorkflowEntitySummaryDto
                {
                    EntityType = status.EntityType,
                    EntityId = requestedEntityId,
                    HasActiveInstance = true,
                    WorkflowInstanceId = status.WorkflowInstanceId,
                    WorkflowName = status.WorkflowName,
                    Status = status.Status,
                    CurrentStepName = currentStepName,
                    CurrentStepInstanceId = currentStepInstanceId,
                    CanCurrentUserApprove = canApprove,
                    PendingApprovers = pendingApprovalsForCurrentStep
                };
            }

            var results = new List<WorkflowEntitySummaryDto>(request.Entities.Count);
            foreach (var e in request.Entities)
            {
                results.Add(await BuildSummaryAsync(e.EntityType, e.EntityId));
            }

            return Ok(new
            {
                success = true,
                data = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow entity summary batch");
            return StatusCode(500, "An error occurred while retrieving workflow summary");
        }
    }

    /// <summary>
    /// Gets detailed workflow audit information for a specific entity record (steps + approvals).
    /// Used by module UIs to show accurate approval history.
    /// </summary>
    [HttpGet("entity-audit")]
    public async Task<ActionResult> GetEntityAudit([FromQuery] string entityType, [FromQuery] Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return BadRequest("Entity type is required");
        }

        if (entityId == Guid.Empty)
        {
            return BadRequest("Entity id is required");
        }

        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return Unauthorized();
            }

            var entityTypeRecord = await _workflowEntityTypeRepository.GetByNameAsync(entityType, tenantId);
            if (entityTypeRecord == null)
            {
                var activeTypes = await _workflowEntityTypeRepository.GetActiveEntityTypesAsync(tenantId);
                entityTypeRecord = activeTypes.FirstOrDefault(et =>
                    string.Equals(et.Code, entityType, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(et.Name, entityType, StringComparison.OrdinalIgnoreCase));
            }

            if (entityTypeRecord == null)
            {
                return NotFound($"Workflow entity type '{entityType}' is not configured");
            }

            var instances = await _workflowInstanceRepository.GetByEntityAsync(entityTypeRecord.Id, entityId.ToString());
            var activeInstance = instances
                .Where(i =>
                    i.Status == WorkflowInstanceStatus.Created ||
                    i.Status == WorkflowInstanceStatus.InProgress ||
                    i.Status == WorkflowInstanceStatus.Waiting ||
                    i.Status == WorkflowInstanceStatus.Suspended)
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
                .FirstOrDefault();

            var instance = activeInstance ?? instances.OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt).FirstOrDefault();
            if (instance == null)
            {
                return Ok(new
                {
                    success = true,
                    data = (WorkflowEntityAuditDto?)null
                });
            }

            var steps = (await _workflowStepInstanceRepository.GetByWorkflowInstanceAsync(instance.Id)).ToList();
            var stepAudits = new List<WorkflowStepAuditDto>();

            foreach (var step in steps.OrderBy(s => s.CreatedAt))
            {
                var approvals = (await _workflowApprovalRepository.GetByStepInstanceAsync(step.Id)).ToList();
                var approvalAudits = approvals
                    .OrderBy(a => a.RequestedDate)
                    .Select(a => new WorkflowApprovalAuditDto
                    {
                        ApprovalId = a.Id,
                        ApproverId = a.ApproverId,
                        ApproverName = a.Approver?.UserName,
                        ApproverRole = a.ApproverRole,
                        Status = a.Status,
                        RequestedDate = a.RequestedDate,
                        ProcessedDate = a.ProcessedDate,
                        Comments = a.Comments,
                        ProcessedById = a.ProcessedById,
                        ProcessedByName = a.ProcessedBy?.UserName
                    })
                    .ToList();

                stepAudits.Add(new WorkflowStepAuditDto
                {
                    StepInstanceId = step.Id,
                    StepName = step.WorkflowStep?.Name ?? "Step",
                    StepType = step.WorkflowStep?.StepType ?? WorkflowStepType.Manual,
                    Status = step.Status,
                    StartedDate = step.StartedDate,
                    CompletedDate = step.CompletedDate,
                    AssignedToId = step.AssignedToId,
                    AssignedToName = step.AssignedTo?.UserName,
                    Comments = step.Comments,
                    Approvals = approvalAudits
                });
            }

            var audit = new WorkflowEntityAuditDto
            {
                EntityType = instance.EntityType?.Code ?? instance.EntityType?.Name ?? entityTypeRecord.Code ?? entityTypeRecord.Name,
                EntityId = entityId,
                WorkflowInstanceId = instance.Id,
                WorkflowName = instance.WorkflowDefinition?.Name ?? "Workflow",
                Status = instance.Status,
                StartedDate = instance.StartedDate ?? instance.CreatedDate,
                CompletedDate = instance.CompletedDate,
                Steps = stepAudits
            };

            return Ok(new
            {
                success = true,
                data = audit
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow entity audit for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, "An error occurred while retrieving workflow audit");
        }
    }

    /// <summary>
    /// Gets active workflow entity types for the current tenant
     /// </summary>
    [HttpGet("entity-types")]
    [Authorize]
    public async Task<ActionResult> GetWorkflowEntityTypes()
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var entityTypes = await _workflowEntityTypeRepository.GetActiveEntityTypesAsync(tenantId);

            var response = entityTypes
                .OrderBy(et => et.DisplayOrder)
                .ThenBy(et => et.Name)
                .Select(et => new WorkflowEntityTypeInfoDto
                {
                    Id = et.Id,
                    Code = et.Code,
                    Name = et.Name,
                    Description = et.Description,
                    DisplayOrder = et.DisplayOrder,
                    Icon = et.Icon,
                    ColorCode = et.ColorCode,
                    IsActive = et.IsActive
                })
                .ToList();

            return Ok(new
            {
                success = true,
                data = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow entity types");
            return StatusCode(500, "An error occurred while retrieving workflow entity types");
        }
    }

    /// <summary>
    /// Seeds common workflow entity types for the current tenant
    /// </summary>
    [HttpPost("entity-types/seed")]
    [Authorize]
    public async Task<ActionResult> SeedWorkflowEntityTypes()
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest("Tenant ID is required to seed workflow entity types");
            }

            var existing = await _workflowEntityTypeRepository
                .GetQueryable(t => t.TenantId == tenantId)
                .ToListAsync();

            var defaults = GetDefaultEntityTypes();

            var created = new List<WorkflowEntityType>();
            var updated = new List<WorkflowEntityType>();

            foreach (var defaultType in defaults)
            {
                var match = existing.FirstOrDefault(et =>
                    string.Equals(et.Name, defaultType.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(et.Code, defaultType.Code, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    created.Add(new WorkflowEntityType
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Name = defaultType.Name,
                        Code = defaultType.Code,
                        Description = defaultType.Description,
                        Icon = defaultType.Icon,
                        ColorCode = defaultType.ColorCode,
                        DisplayOrder = defaultType.DisplayOrder,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = _currentUserService.UserName ?? "System",
                        CreatedById = Guid.TryParse(_currentUserService.UserId, out var createdById) ? createdById : null
                    });
                }
                else
                {
                    var shouldUpdate = false;
                    if (!match.IsActive)
                    {
                        match.IsActive = true;
                        shouldUpdate = true;
                    }

                    if (match.DisplayOrder == 0 && defaultType.DisplayOrder > 0)
                    {
                        match.DisplayOrder = defaultType.DisplayOrder;
                        shouldUpdate = true;
                    }

                    if (shouldUpdate)
                    {
                        match.UpdatedAt = DateTime.UtcNow;
                        match.UpdatedBy = _currentUserService.UserName ?? "System";
                        match.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var modifiedById)
                            ? modifiedById
                            : null;
                        updated.Add(match);
                    }
                }
            }

            if (created.Any())
            {
                await _workflowEntityTypeRepository.AddRangeAsync(created);
            }

            if (updated.Any())
            {
                await _workflowEntityTypeRepository.UpdateRangeAsync(updated);
            }

            if (created.Any() || updated.Any())
            {
                await _workflowEntityTypeRepository.SaveChangesAsync();
            }

            var activeTypes = await _workflowEntityTypeRepository.GetActiveEntityTypesAsync(tenantId);
            var response = activeTypes
                .OrderBy(et => et.DisplayOrder)
                .ThenBy(et => et.Name)
                .Select(et => new WorkflowEntityTypeInfoDto
                {
                    Id = et.Id,
                    Code = et.Code,
                    Name = et.Name,
                    Description = et.Description,
                    DisplayOrder = et.DisplayOrder,
                    Icon = et.Icon,
                    ColorCode = et.ColorCode,
                    IsActive = et.IsActive
                })
                .ToList();

            return Ok(new
            {
                success = true,
                data = response,
                metadata = new
                {
                    created = created.Count,
                    reactivated = updated.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding workflow entity types");
            return StatusCode(500, "An error occurred while seeding workflow entity types");
        }
    }

    private static List<WorkflowEntityTypeSeed> GetDefaultEntityTypes()
    {
        var defaults = new List<WorkflowEntityTypeSeed>
        {
            new("WorkOrder", "Maintenance work orders", "Settings", "#3B82F6", 10),
            new("JobCard", "Maintenance job cards", "FileText", "#8B5CF6", 20),
            new("FleetTrip", "Fleet trip requests and dispatch", "MapPin", "#0EA5E9", 25),
            new("PurchaseOrder", "Procurement purchase orders", "FileText", "#F59E0B", 30),
            new("PurchaseRequisition", "Procurement requisitions", "FileText", "#F97316", 40),
            new("Tender", "Procurement tenders (RFQ/RFP/ITB/EOI)", "FileText", "#06B6D4", 45),
            new("RFQ", "Requests for Quotation (RFQs)", "FileText", "#06B6D4", 46),
            new("SupplierQuote", "Supplier quotes submitted in response to RFQs", "FileText", "#22C55E", 47),
            new("Bid", "Supplier bids submitted in response to tenders", "FileText", "#22C55E", 48),
            new("Evaluation", "Tender evaluations and scoring", "CheckCircle", "#F59E0B", 49),
            new("Asset", "Assets and equipment", "Database", "#10B981", 50),
            new("Inventory", "Inventory records", "Database", "#14B8A6", 60),
            new("InventoryTransfer", "Inventory transfers", "Truck", "#A855F7", 65),
            new("InventoryRequisition", "Inventory requisitions", "ClipboardList", "#0EA5E9", 68),
            new("Employee", "Human resources employees", "Users", "#6366F1", 70),
            new("Project", "Project management items", "CheckCircle", "#22C55E", 80),
            new("ProjectDeliverable", "Project deliverable approvals and external sign-off", "PackageCheck", "#16A34A", 82),
            new("ProjectClosure", "Project closure approval and close-out governance", "Flag", "#15803D", 84),
            new("Customer", "Sales customers", "User", "#0EA5E9", 90),
            new("BusinessPartner", "Business partner onboarding/approvals (suppliers/contractors/customers)", "Building", "#64748B", 95),
            new("Vendor", "Business partners and vendors", "Building", "#64748B", 100),
            new("Quality", "Quality inspections", "CheckCircle", "#EF4444", 110),
            new("ServiceRequest", "Service catalog requests", "ClipboardList", "#10B981", 115)
        };

        return defaults
            .Select((item, index) => item with { DisplayOrder = item.DisplayOrder == 0 ? (index + 1) * 10 : item.DisplayOrder })
            .ToList();
    }

    private record WorkflowEntityTypeSeed(
        string Name,
        string? Description,
        string? Icon,
        string? ColorCode,
        int DisplayOrder)
    {
        public string Code => GenerateEntityTypeCode(Name);
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

    /// <summary>
    /// Gets workflow instance status by ID
    /// </summary>
    [HttpGet("instances/{id:guid}")]
    public async Task<ActionResult<WorkflowStatusDto>> GetWorkflowInstance(Guid id)
    {
        try
        {
            var status = await _workflowEngine.GetWorkflowStatusAsync(id);
            return Ok(new
            {
                success = true,
                data = status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow instance {WorkflowInstanceId}", id);
            return StatusCode(500, "An error occurred while retrieving workflow instance");
        }
    }

    /// <summary>
    /// Starts a new workflow instance
    /// </summary>
    [HttpPost("instances/start")]
    public async Task<ActionResult<WorkflowInstance>> StartWorkflow(StartWorkflowRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var instance = await _workflowEngine.StartWorkflowAsync(
                request.WorkflowName,
                request.EntityId,
                currentUserId.Value,
                request.DataContext);

            return Ok(new
            {
                success = true,
                data = instance
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting workflow {WorkflowName} for entity {EntityId}", request.WorkflowName, request.EntityId);
            return StatusCode(500, "An error occurred while starting the workflow");
        }
    }

    /// <summary>
    /// Executes the next step in a workflow instance
    /// </summary>
    [HttpPost("instances/{id:guid}/execute")]
    public async Task<ActionResult<WorkflowExecutionResult>> ExecuteNextStep(Guid id, ExecuteStepRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var result = await _workflowEngine.ExecuteNextStepAsync(id, currentUserId.Value, request.StepData);
            return Ok(new
            {
                success = true,
                data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing next step for workflow instance {WorkflowInstanceId}", id);
            return StatusCode(500, "An error occurred while executing workflow step");
        }
    }

    /// <summary>
    /// Cancels a running workflow instance
    /// </summary>
    [HttpPost("instances/{id:guid}/cancel")]
    public async Task<ActionResult> CancelWorkflow(Guid id, CancelWorkflowRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            await _workflowEngine.CancelWorkflowAsync(id, currentUserId.Value, request.Reason);
            return Ok(new
            {
                success = true,
                message = "Workflow cancelled successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling workflow instance {WorkflowInstanceId}", id);
            return StatusCode(500, "An error occurred while cancelling workflow");
        }
    }

    #endregion

    #region Workflow Step Management

    /// <summary>
    /// Gets pending workflow tasks for the current user
    /// </summary>
    [HttpGet("tasks/pending")]
    public async Task<ActionResult<IEnumerable<WorkflowStepInstance>>> GetPendingTasks()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var tasks = await _workflowInstanceService.GetPendingTasksForUserAsync(currentUserId.Value);
            return Ok(new
            {
                success = true,
                data = tasks
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending workflow tasks");
            return StatusCode(500, "An error occurred while retrieving pending tasks");
        }
    }

    /// <summary>
    /// Processes a specific workflow step
    /// </summary>
    [HttpPost("steps/{id:guid}/process")]
    public async Task<ActionResult<WorkflowExecutionResult>> ProcessStep(Guid id, ProcessStepRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var result = await _workflowEngine.ProcessStepAsync(id, currentUserId.Value, request.Action, request.ResultData, request.Comments);
            return Ok(new
            {
                success = true,
                data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing workflow step {StepInstanceId}", id);
            return StatusCode(500, "An error occurred while processing workflow step");
        }
    }

    /// <summary>
    /// Assigns a workflow step to a user
    /// </summary>
    [HttpPost("steps/{id:guid}/assign")]
    public async Task<ActionResult> AssignStep(Guid id, AssignStepRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            await _workflowStepService.AssignStepAsync(id, request.AssignedToId, currentUserId.Value);
            return Ok(new
            {
                success = true,
                message = "Step assigned successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning workflow step {StepInstanceId}", id);
            return StatusCode(500, "An error occurred while assigning workflow step");
        }
    }

    #endregion

    #region Workflow Approval Management

    /// <summary>
    /// Gets pending approvals for the current user
    /// </summary>
    [HttpGet("approvals/pending")]
    public async Task<ActionResult<IEnumerable<WorkflowApproval>>> GetPendingApprovals()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var approvals = await _workflowApprovalRepository.GetByStatusAsync(WorkflowApprovalStatus.Pending, tenantId);
            var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            var filtered = approvals.Where(a =>
                (a.ApproverId.HasValue && a.ApproverId.Value == currentUserId.Value) ||
                (!string.IsNullOrWhiteSpace(a.ApproverRole) && roleSet.Contains(a.ApproverRole)));

            return Ok(new
            {
                success = true,
                data = filtered
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending approvals");
            return StatusCode(500, "An error occurred while retrieving pending approvals");
        }
    }

    /// <summary>
    /// Processes an approval decision
    /// </summary>
    [HttpPost("approvals/{id:guid}/process")]
    public async Task<ActionResult<WorkflowApproval>> ProcessApproval(Guid id, ProcessApprovalRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var approval = await _workflowApprovalRepository.GetByIdAsync(id);
            if (approval == null)
            {
                return NotFound();
            }

            // Enforce that the current user can act on this approval (direct assignment or matching role).
            var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var isDirect = approval.ApproverId.HasValue && approval.ApproverId.Value == currentUserId.Value;
            var isRole = !string.IsNullOrWhiteSpace(approval.ApproverRole) && roleSet.Contains(approval.ApproverRole);
            if (!isDirect && !isRole)
            {
                return Forbid();
            }

            var stepAction = request.Action switch
            {
                ErpSystem.Core.Enums.WorkflowApprovalAction.Approve => ErpSystem.Core.Enums.WorkflowStepAction.Complete,
                ErpSystem.Core.Enums.WorkflowApprovalAction.Reject => ErpSystem.Core.Enums.WorkflowStepAction.Reject,
                ErpSystem.Core.Enums.WorkflowApprovalAction.RequestMoreInfo => ErpSystem.Core.Enums.WorkflowStepAction.RequestInformation,
                ErpSystem.Core.Enums.WorkflowApprovalAction.Delegate => ErpSystem.Core.Enums.WorkflowStepAction.Delegate,
                _ => ErpSystem.Core.Enums.WorkflowStepAction.Complete
            };

            // Progress the workflow via the engine (this records the approval + advances the step when satisfied).
            await _workflowEngine.ProcessStepAsync(approval.StepInstanceId, currentUserId.Value, stepAction, comments: request.Comments);

            await TryApplyPostApprovalIntegrationAsync(approval.StepInstanceId, currentUserId.Value, request, HttpContext.RequestAborted);

            // Reload for response (best-effort).
            approval = await _workflowApprovalRepository.GetByIdAsync(id) ?? approval;
            return Ok(new
            {
                success = true,
                data = approval
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing approval {ApprovalId}", id);
            return StatusCode(500, "An error occurred while processing approval");
        }
    }

    private async Task TryApplyPostApprovalIntegrationAsync(Guid stepInstanceId, Guid actorUserId, ProcessApprovalRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var stepInstance = await _db.WorkflowStepInstances
                .AsNoTracking()
                .Include(si => si.WorkflowInstance)
                .ThenInclude(wi => wi!.EntityType)
                .FirstOrDefaultAsync(si => si.Id == stepInstanceId && !si.IsDeleted, cancellationToken);

            var instance = stepInstance?.WorkflowInstance;
            var entityType = instance?.EntityType;
            if (instance == null || entityType == null)
            {
                return;
            }

            var isServiceRequest =
                string.Equals(entityType.Code, "SERVICE_REQUEST", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entityType.Name, "ServiceRequest", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entityType.Name, "Service Request", StringComparison.OrdinalIgnoreCase);
            if (!isServiceRequest)
            {
                return;
            }

            var outcome = instance.Status switch
            {
                WorkflowInstanceStatus.Completed => WorkflowOutcome.Approved,
                WorkflowInstanceStatus.Cancelled => WorkflowOutcome.Rejected,
                WorkflowInstanceStatus.Failed => WorkflowOutcome.Rejected,
                _ => WorkflowOutcome.Pending
            };

            if (outcome == WorkflowOutcome.Pending)
            {
                return;
            }

            var sr = await _db.EhcServiceRequests
                .Include(r => r.RequestType)
                .FirstOrDefaultAsync(r => r.TenantId == instance.TenantId && r.Id == instance.EntityId && !r.IsDeleted, cancellationToken);
            if (sr == null)
            {
                return;
            }

            var rejectionReason = request.Action == WorkflowApprovalAction.Reject
                ? (string.IsNullOrWhiteSpace(request.Comments) ? "Rejected" : request.Comments.Trim())
                : null;

            var adapter = _workflowStatusAdapterRegistry.GetAdapter("ServiceRequest");
            adapter.ApplyApprovalOutcome(sr, outcome, actorUserId, rejectionReason);

            sr.WorkflowInstanceId ??= instance.Id;
            sr.UpdatedAt = DateTime.UtcNow;
            sr.UpdatedBy = _currentUserService.UserName ?? "System";

            var now = DateTime.UtcNow;
            if (outcome == WorkflowOutcome.Approved)
            {
                _db.EhcServiceRequestAuditEvents.Add(new EhcServiceRequestAuditEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = sr.TenantId,
                    ServiceRequestId = sr.Id,
                    EventType = "Approved",
                    Title = "Request approved",
                    Body = string.IsNullOrWhiteSpace(request.Comments) ? null : request.Comments.Trim(),
                    IsInternal = false,
                    ActorUserId = actorUserId,
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName ?? "System",
                    CreatedById = actorUserId
                });
            }
            else if (outcome == WorkflowOutcome.Rejected)
            {
                _db.EhcServiceRequestAuditEvents.Add(new EhcServiceRequestAuditEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = sr.TenantId,
                    ServiceRequestId = sr.Id,
                    EventType = "Rejected",
                    Title = "Request rejected",
                    Body = rejectionReason,
                    IsInternal = false,
                    ActorUserId = actorUserId,
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName ?? "System",
                    CreatedById = actorUserId
                });
            }

            await _db.SaveChangesAsync(cancellationToken);

            var activity = outcome == WorkflowOutcome.Approved ? "Approved" : "Rejected";
            var actionUrl = $"/external-portal/support/requests/{sr.Id}";
            try
            {
                var provider = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.TenantId == sr.TenantId && u.Id == sr.RequesterUserId && u.IsActive)
                    .Select(u => u.AuthenticationProvider)
                    .FirstOrDefaultAsync(cancellationToken);

                if (provider != ErpSystem.Shared.AuthenticationProvider.Local)
                {
                    actionUrl = $"/helpdesk/requests/{sr.Id}";
                }
            }
            catch
            {
                actionUrl = $"/helpdesk/requests/{sr.Id}";
            }

            var data = new Dictionary<string, object>
            {
                ["serviceRequestId"] = sr.Id,
                ["requestNumber"] = sr.RequestNumber,
                ["TargetUserId"] = sr.RequesterUserId,
                ["ActionUrl"] = actionUrl
            };
            if (outcome == WorkflowOutcome.Rejected && !string.IsNullOrWhiteSpace(rejectionReason))
            {
                data["reason"] = rejectionReason;
            }

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = sr.TenantId,
                EntityType = "EhcServiceRequest",
                EntityId = sr.Id,
                Activity = activity,
                Audience = "Customer",
                TriggeredByUserId = actorUserId,
                Data = data
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Post-approval integration hook failed for step {StepInstanceId}", stepInstanceId);
        }
    }

    #endregion

    #region Administration and Statistics

    /// <summary>
    /// Gets workflow administration summary statistics
    /// </summary>
    [HttpGet("administration/summary")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager")]
    public ActionResult<WorkflowSummaryDto> GetWorkflowSummary()
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

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

            return Ok(new
            {
                success = true,
                data = summary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow summary");
            return StatusCode(500, "An error occurred while retrieving the workflow summary");
        }
    }

    /// <summary>
    /// Gets overdue workflow steps
    /// </summary>
    [HttpGet("administration/overdue-steps")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<IEnumerable<WorkflowStepInstance>>> GetOverdueSteps()
    {
        try
        {
            var steps = await _workflowStepService.GetOverdueStepsAsync();
            return Ok(new
            {
                success = true,
                data = steps
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue workflow steps");
            return StatusCode(500, "An error occurred while retrieving overdue workflow steps");
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
        {
            return null;
        }

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

    private static IQueryable<WorkflowInstance> ApplyInstanceSorting(
        IQueryable<WorkflowInstance> query,
        WorkflowInstanceFilterDto filter)
    {
        var sortBy = filter.SortBy?.ToLowerInvariant();
        var desc = filter.SortDescending;

        return sortBy switch
        {
            "createddate" => desc ? query.OrderByDescending(i => i.CreatedDate) : query.OrderBy(i => i.CreatedDate),
            "completeddate" => desc ? query.OrderByDescending(i => i.CompletedDate) : query.OrderBy(i => i.CompletedDate),
            "status" => desc ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            _ => desc
                ? query.OrderByDescending(i => i.StartedDate ?? i.CreatedDate)
                : query.OrderBy(i => i.StartedDate ?? i.CreatedDate)
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

public class WorkflowVariableInfoResponse
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataType { get; set; } = "object";
    public string? Description { get; set; }
    public List<string>? PossibleValues { get; set; }
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

