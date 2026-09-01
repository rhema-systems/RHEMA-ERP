using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Services.Workflow;
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
    private static readonly JsonSerializerOptions WorkflowJsonOptions = CreateWorkflowJsonOptions();

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
    private readonly IWorkflowEntityTypeCatalogService _workflowEntityTypeCatalog;
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IAppEventBus _appEventBus;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IProcurementRequisitionBudgetControlService _requisitionBudgetControlService;
    private readonly IProcedureCaseService _procedureCaseService;
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
        IWorkflowEntityTypeCatalogService workflowEntityTypeCatalog,
        ErpSystem.Data.ApplicationDbContext db,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IAppEventBus appEventBus,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        IProcurementRequisitionBudgetControlService requisitionBudgetControlService,
        IProcedureCaseService procedureCaseService,
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
        _workflowEntityTypeCatalog = workflowEntityTypeCatalog;
        _db = db;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _appEventBus = appEventBus;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _requisitionBudgetControlService = requisitionBudgetControlService;
        _procedureCaseService = procedureCaseService;
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
                DefinitionKey = def.DefinitionKey,
                Name = def.Name,
                Description = def.Description,
                EntityType = def.EntityType.Name,
                Version = def.Version,
                IsActive = def.IsActive,
                LifecycleStatus = def.LifecycleStatus,
                ChangeSummary = def.ChangeSummary,
                SupersedesDefinitionId = def.SupersedesDefinitionId,
                PublishedAt = def.PublishedAt,
                RetiredAt = def.RetiredAt,
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
            else
            {
                definition = await _workflowDefinitionService.PublishWorkflowDefinitionAsync(definition.Id, currentUserId.Value);
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
                EntityType = updateDto.EntityType,
                Configuration = updateDto.Configuration,
                LastModifiedById = currentUserId.Value,
                Steps = updateDto.Steps,
                Transitions = updateDto.Transitions
            };

            var definition = await _workflowDefinitionService.UpdateWorkflowDefinitionAsync(id, workflowDto);

            await _workflowDefinitionService.SetWorkflowDefinitionActiveAsync(definition.Id, updateDto.IsActive, currentUserId.Value);
            definition = await _workflowDefinitionService.GetWorkflowDefinitionAsync(definition.Id) ?? definition;

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
            var response = new
            {
                success = false,
                error = ex.Message
            };

            if (ex.Message.Contains("live instances", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("active instances", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(response);
            }

            return BadRequest(response);
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
                entityTypeRecord = activeTypes.FirstOrDefault(et => EntityTypeMatches(et, entityType));
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
                        ApprovalRequired = false,
                        CanCurrentUserApprove = false
                    }
                });
            }

            var approvalRequired = await _workflowService.HasActiveApprovalWorkflowAsync(
                entityTypeRecord.Code ?? entityTypeRecord.Name ?? entityType);

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
                        ApprovalRequired = approvalRequired,
                        CanCurrentUserApprove = false
                    }
                });
            }

            var status = await _workflowEngine.GetWorkflowStatusAsync(activeInstance.Id);
            var stepInfo = await _workflowService.GetCurrentWorkflowStepAsync(entityType, entityId);

            var currentStepName = stepInfo?.StepName;
            var currentStepInstanceId = stepInfo?.Id;

            currentStepName = string.IsNullOrWhiteSpace(currentStepName)
                ? status.CurrentStepName
                : currentStepName;
            currentStepInstanceId ??= status.CurrentStepInstanceId;

            if (string.IsNullOrWhiteSpace(currentStepName))
            {
                // Fallback to last pending/in-progress step instance name from the status view.
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
                .GroupBy(GetPendingApproverKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(a => new WorkflowPendingApproverDto
                {
                    ApproverId = a.ApproverId == Guid.Empty ? null : a.ApproverId,
                    ApproverName = a.ApproverName,
                    ApproverRole = a.ApproverRole
                })
                .ToList();

            var canApprove = await _workflowService.CanUserApproveAsync(entityType, entityId, currentUserId.Value);
            var currentUserApprovalId = await ResolveCurrentUserApprovalIdAsync(currentStepInstanceId, currentUserId.Value);
            var currentUserCorrection = await ResolveCurrentUserCorrectionAsync(status.WorkflowInstanceId, currentUserId.Value);
            var currentStepChecklist = await GetStepChecklistAsync(currentStepInstanceId, HttpContext.RequestAborted);
            var currentStepInstance = await GetStepInstanceForSummaryAsync(currentStepInstanceId, HttpContext.RequestAborted);
            var currentStepType = currentStepInstance?.WorkflowStep?.StepType;
            var currentStepTaskConfig = GetTaskConfigFromStepConfiguration(currentStepInstance?.WorkflowStep?.Configuration);
            var currentStepSignaturePolicy = DeserializeStepConfiguration(currentStepInstance?.WorkflowStep?.Configuration)?.ApprovalConfig?.SignaturePolicy;
            var currentStepTaskAttachments = GetWorkflowTaskAttachments(currentStepInstance?.ResultData);
            var canComplete = currentStepInstance != null && CanCurrentUserCompleteWorkflowTask(currentStepInstance, currentUserId.Value);

            var summary = new WorkflowEntitySummaryDto
            {
                EntityType = status.EntityType,
                EntityId = entityId,
                HasActiveInstance = true,
                ApprovalRequired = true,
                WorkflowInstanceId = status.WorkflowInstanceId,
                WorkflowName = status.WorkflowName,
                Status = status.Status,
                CurrentStepName = currentStepName,
                CurrentStepInstanceId = currentStepInstanceId,
                CurrentStepType = currentStepType,
                CanCurrentUserApprove = canApprove,
                CurrentUserApprovalId = currentUserApprovalId,
                CurrentUserCorrectionId = currentUserCorrection?.Id,
                CanCurrentUserResubmit = currentUserCorrection != null,
                CorrectionInstructions = currentUserCorrection?.Instructions,
                CanCurrentUserRecall = CanCurrentUserRecall(activeInstance, currentUserId.Value),
                CanCurrentUserComplete = canComplete,
                PendingApprovers = pendingApprovalsForCurrentStep,
                CurrentStepChecklist = currentStepChecklist,
                CurrentStepTaskConfig = currentStepTaskConfig,
                CurrentStepSignaturePolicy = currentStepSignaturePolicy,
                CurrentStepTaskAttachments = currentStepTaskAttachments
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
                return activeTypes.FirstOrDefault(et => EntityTypeMatches(et, type));
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
                        ApprovalRequired = false,
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
                        ApprovalRequired = false,
                        CanCurrentUserApprove = false
                    };
                }

                var canonicalEntityType = entityTypeRecord.Code ?? entityTypeRecord.Name ?? requestedEntityType;
                var approvalRequired = await _workflowService.HasActiveApprovalWorkflowAsync(canonicalEntityType);

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
                        ApprovalRequired = approvalRequired,
                        CanCurrentUserApprove = false
                    };
                }

                var status = await _workflowEngine.GetWorkflowStatusAsync(activeInstance.Id);

                // Use the canonical entity type string (code preferred) so workflow service lookups are consistent.
                var stepInfo = await _workflowService.GetCurrentWorkflowStepAsync(canonicalEntityType, requestedEntityId);

                var currentStepName = stepInfo?.StepName;
                var currentStepInstanceId = stepInfo?.Id;

                currentStepName = string.IsNullOrWhiteSpace(currentStepName)
                    ? status.CurrentStepName
                    : currentStepName;
                currentStepInstanceId ??= status.CurrentStepInstanceId;

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
                    .GroupBy(GetPendingApproverKey, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .Select(a => new WorkflowPendingApproverDto
                    {
                        ApproverId = a.ApproverId == Guid.Empty ? null : a.ApproverId,
                        ApproverName = a.ApproverName,
                        ApproverRole = a.ApproverRole
                    })
                    .ToList();

                var canApprove = await _workflowService.CanUserApproveAsync(canonicalEntityType, requestedEntityId, currentUserId.Value);
                var currentUserApprovalId = await ResolveCurrentUserApprovalIdAsync(currentStepInstanceId, currentUserId.Value);
                var currentUserCorrection = await ResolveCurrentUserCorrectionAsync(status.WorkflowInstanceId, currentUserId.Value);
                var currentStepChecklist = await GetStepChecklistAsync(currentStepInstanceId, HttpContext.RequestAborted);
                var currentStepInstance = await GetStepInstanceForSummaryAsync(currentStepInstanceId, HttpContext.RequestAborted);
                var currentStepType = currentStepInstance?.WorkflowStep?.StepType;
                var currentStepTaskConfig = GetTaskConfigFromStepConfiguration(currentStepInstance?.WorkflowStep?.Configuration);
                var currentStepSignaturePolicy = DeserializeStepConfiguration(currentStepInstance?.WorkflowStep?.Configuration)?.ApprovalConfig?.SignaturePolicy;
                var currentStepTaskAttachments = GetWorkflowTaskAttachments(currentStepInstance?.ResultData);
                var canComplete = currentStepInstance != null && CanCurrentUserCompleteWorkflowTask(currentStepInstance, currentUserId.Value);

                return new WorkflowEntitySummaryDto
                {
                    EntityType = status.EntityType,
                    EntityId = requestedEntityId,
                    HasActiveInstance = true,
                    ApprovalRequired = true,
                    WorkflowInstanceId = status.WorkflowInstanceId,
                    WorkflowName = status.WorkflowName,
                    Status = status.Status,
                    CurrentStepName = currentStepName,
                    CurrentStepInstanceId = currentStepInstanceId,
                    CurrentStepType = currentStepType,
                    CanCurrentUserApprove = canApprove,
                    CurrentUserApprovalId = currentUserApprovalId,
                    CurrentUserCorrectionId = currentUserCorrection?.Id,
                    CanCurrentUserResubmit = currentUserCorrection != null,
                    CorrectionInstructions = currentUserCorrection?.Instructions,
                    CanCurrentUserRecall = CanCurrentUserRecall(activeInstance, currentUserId.Value),
                    CanCurrentUserComplete = canComplete,
                    PendingApprovers = pendingApprovalsForCurrentStep,
                    CurrentStepChecklist = currentStepChecklist,
                    CurrentStepTaskConfig = currentStepTaskConfig,
                    CurrentStepSignaturePolicy = currentStepSignaturePolicy,
                    CurrentStepTaskAttachments = currentStepTaskAttachments
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
                entityTypeRecord = activeTypes.FirstOrDefault(et => EntityTypeMatches(et, entityType));
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
                    .OrderBy(a => a.ApprovalGroup)
                    .ThenBy(a => a.RequestedDate)
                    .Select(a => new WorkflowApprovalAuditDto
                    {
                        ApprovalId = a.Id,
                        ApprovalGroup = Math.Max(a.ApprovalGroup, 1),
                        IsAdHoc = a.IsAdHoc,
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
                    AssignedToName = step.AssignedTo?.UserName ?? FormatApprovalOwnerList(approvalAudits),
                    Comments = step.Comments,
                    Checklist = GetChecklistFromStepConfiguration(step.WorkflowStep?.Configuration),
                    ChecklistResponses = GetApprovalChecklistResponses(step.ResultData),
                    TaskConfig = GetTaskConfigFromStepConfiguration(step.WorkflowStep?.Configuration),
                    TaskAttachments = GetWorkflowTaskAttachments(step.ResultData),
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

    [HttpPost("definitions/{id}/clone-draft")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<WorkflowDefinitionDto>> CloneWorkflowDefinitionDraft(
        Guid id,
        [FromBody] CloneWorkflowDefinitionDraftDto? request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized();
            }

            var draft = await _workflowDefinitionService.CloneWorkflowDefinitionDraftAsync(
                id,
                request?.ChangeSummary,
                currentUserId.Value);
            return Ok(new { success = true, data = MapToWorkflowDefinitionDto(draft) });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("definitions/{id}/publish")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<WorkflowDefinitionDto>> PublishWorkflowDefinition(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized();
            }

            var definition = await _workflowDefinitionService.PublishWorkflowDefinitionAsync(id, currentUserId.Value);
            return Ok(new { success = true, data = MapToWorkflowDefinitionDto(definition) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("definitions/{id}/retire")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<WorkflowDefinitionDto>> RetireWorkflowDefinition(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return Unauthorized();
            }

            var definition = await _workflowDefinitionService.RetireWorkflowDefinitionAsync(id, currentUserId.Value);
            return Ok(new { success = true, data = MapToWorkflowDefinitionDto(definition) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("definitions/{id}/versions")]
    public async Task<ActionResult<IReadOnlyList<WorkflowDefinitionVersionDto>>> GetWorkflowDefinitionVersions(Guid id)
    {
        try
        {
            var versions = await _workflowDefinitionService.GetWorkflowDefinitionVersionsAsync(id);
            var data = versions.Select(definition => new WorkflowDefinitionVersionDto
            {
                Id = definition.Id,
                DefinitionKey = definition.DefinitionKey,
                Name = definition.Name,
                Version = definition.Version,
                LifecycleStatus = definition.LifecycleStatus,
                IsActive = definition.IsActive,
                ChangeSummary = definition.ChangeSummary,
                SupersedesDefinitionId = definition.SupersedesDefinitionId,
                CreatedDate = definition.CreatedAt,
                PublishedAt = definition.PublishedAt,
                RetiredAt = definition.RetiredAt,
                ActiveInstancesCount = definition.Instances.Count(instance =>
                    instance.Status == WorkflowInstanceStatus.Created ||
                    instance.Status == WorkflowInstanceStatus.InProgress ||
                    instance.Status == WorkflowInstanceStatus.Waiting ||
                    instance.Status == WorkflowInstanceStatus.Suspended)
            }).ToList();
            return Ok(new { success = true, data });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("definitions/compare")]
    public async Task<ActionResult<WorkflowDefinitionComparisonDto>> CompareWorkflowDefinitions(
        [FromQuery] Guid fromDefinitionId,
        [FromQuery] Guid toDefinitionId)
    {
        try
        {
            var comparison = await _workflowDefinitionService.CompareWorkflowDefinitionsAsync(
                fromDefinitionId,
                toDefinitionId);
            return Ok(new { success = true, data = comparison });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Recalls the active workflow for an entity record. Only the requester can recall.
    /// </summary>
    [HttpPost("entity/{entityType}/{entityId:guid}/recall")]
    public async Task<ActionResult> RecallEntityWorkflow(string entityType, Guid entityId, [FromBody] RecallWorkflowRequest? request)
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

            var entityTypeRecord = await ResolveWorkflowEntityTypeRecordAsync(entityType, tenantId);
            if (entityTypeRecord == null)
            {
                return NotFound($"Workflow entity type '{entityType}' is not configured");
            }

            var activeInstance = await ResolveActiveWorkflowInstanceAsync(entityTypeRecord, entityId);
            if (activeInstance == null)
            {
                return BadRequest("No active workflow found for this record");
            }

            if (!CanCurrentUserRecall(activeInstance, currentUserId.Value))
            {
                return Forbid();
            }

            var reason = string.IsNullOrWhiteSpace(request?.Reason)
                ? "Recalled by requester"
                : request.Reason.Trim();

            var canonicalEntityType = entityTypeRecord.Code ?? entityTypeRecord.Name ?? entityType;
            var isPurchaseRequisition = NormalizeEntityTypeKey(canonicalEntityType) is
                "PURCHASEREQUISITION" or "PR";
            WorkflowExecutionResult? result = null;
            var entityStatusUpdated = false;
            if (isPurchaseRequisition && _db.Database.IsRelational())
            {
                var executionStrategy = _db.Database.CreateExecutionStrategy();
                await executionStrategy.ExecuteAsync(async () =>
                {
                    await using var recallTransaction = await _db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
                    result = await _workflowService.RecallWorkflowAsync(
                        canonicalEntityType,
                        entityId,
                        currentUserId.Value,
                        reason);
                    if (!result.Success)
                    {
                        await recallTransaction.RollbackAsync(HttpContext.RequestAborted);
                        return;
                    }

                    entityStatusUpdated = await TryApplyRecallStatusAsync(
                        canonicalEntityType,
                        entityId,
                        currentUserId.Value,
                        reason,
                        HttpContext.RequestAborted);
                    await recallTransaction.CommitAsync(HttpContext.RequestAborted);
                });
            }
            else
            {
                result = await _workflowService.RecallWorkflowAsync(
                    canonicalEntityType,
                    entityId,
                    currentUserId.Value,
                    reason);
                if (result.Success)
                {
                    entityStatusUpdated = await TryApplyRecallStatusAsync(
                        canonicalEntityType,
                        entityId,
                        currentUserId.Value,
                        reason,
                        HttpContext.RequestAborted);
                }
            }

            if (result is null)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Workflow recall did not return an execution result"
                });
            }

            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message
                });
            }

            return Ok(new
            {
                success = true,
                message = "Workflow recalled successfully",
                data = new
                {
                    workflowInstanceId = result.WorkflowInstanceId,
                    entityStatusUpdated
                }
            });
        }
        catch (ProcurementRequisitionBudgetAuthorizationException ex)
        {
            return StatusCode(403, new { success = false, code = "PR_BUDGET_RELEASE_FORBIDDEN", message = ex.Message });
        }
        catch (ProcurementRequisitionBudgetNotFoundException ex)
        {
            return NotFound(new { success = false, code = ex.Code, message = ex.Message });
        }
        catch (ProcurementRequisitionBudgetConflictException ex)
        {
            return Conflict(new { success = false, code = ex.Code, message = ex.Message });
        }
        catch (ProcurementRequisitionBudgetValidationException ex)
        {
            return UnprocessableEntity(new { success = false, code = ex.Code, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalling workflow for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, "An error occurred while recalling workflow");
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

            var defaults = _workflowEntityTypeCatalog.GetDefaultEntityTypes();

            var created = new List<WorkflowEntityType>();
            var updated = new List<WorkflowEntityType>();
            var deactivated = new List<WorkflowEntityType>();
            var blockedUnsupported = new List<string>();

            foreach (var defaultType in defaults)
            {
                var match = existing.FirstOrDefault(et =>
                    string.Equals(et.Name, defaultType.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(et.Code, defaultType.Code, StringComparison.OrdinalIgnoreCase));
                var isSupported = _workflowStatusAdapterRegistry.TryGetAdapter(defaultType.Name, out _) ||
                    _workflowStatusAdapterRegistry.TryGetAdapter(defaultType.Code, out _);

                if (!isSupported)
                {
                    if (match?.IsActive == true)
                    {
                        var hasDefinitions = await _db.WorkflowDefinitions
                            .AnyAsync(definition => definition.EntityTypeId == match.Id && !definition.IsDeleted);
                        if (hasDefinitions)
                        {
                            blockedUnsupported.Add($"{match.Name} ({match.Code})");
                        }
                        else
                        {
                            match.IsActive = false;
                            match.UpdatedAt = DateTime.UtcNow;
                            match.UpdatedBy = _currentUserService.UserName ?? "System";
                            match.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var deactivatedById)
                                ? deactivatedById
                                : null;
                            deactivated.Add(match);
                        }
                    }

                    continue;
                }

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

            if (deactivated.Any())
            {
                await _workflowEntityTypeRepository.UpdateRangeAsync(deactivated);
            }

            if (created.Any() || updated.Any() || deactivated.Any())
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
                    reactivated = updated.Count,
                    deactivated = deactivated.Count,
                    blockedUnsupported
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding workflow entity types");
            return StatusCode(500, "An error occurred while seeding workflow entity types");
        }
    }

    [HttpGet("conformance")]
    [Authorize(Roles = "SystemAdmin,WorkflowAdmin,SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<WorkflowModuleConformanceReport>> GetWorkflowModuleConformance()
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Unauthorized();
        }

        var activeEntityTypes = await _workflowEntityTypeRepository.GetActiveEntityTypesAsync(tenantId);
        var report = WorkflowModuleConformanceValidator.Evaluate(
            activeEntityTypes,
            _workflowStatusAdapterRegistry);

        return Ok(new { success = true, data = report });
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

    private async Task<WorkflowEntityType?> ResolveWorkflowEntityTypeRecordAsync(string entityType, Guid tenantId)
    {
        var entityTypeRecord = await _workflowEntityTypeRepository.GetByNameAsync(entityType, tenantId);
        if (entityTypeRecord != null)
        {
            return entityTypeRecord;
        }

        var activeTypes = await _workflowEntityTypeRepository.GetActiveEntityTypesAsync(tenantId);
        return activeTypes.FirstOrDefault(et => EntityTypeMatches(et, entityType));
    }

    private async Task<WorkflowInstance?> ResolveActiveWorkflowInstanceAsync(WorkflowEntityType entityTypeRecord, Guid entityId)
    {
        var instances = await _workflowInstanceRepository.GetByEntityAsync(entityTypeRecord.Id, entityId.ToString());
        return instances
            .Where(i =>
                i.Status == WorkflowInstanceStatus.Created ||
                i.Status == WorkflowInstanceStatus.InProgress ||
                i.Status == WorkflowInstanceStatus.Waiting ||
                i.Status == WorkflowInstanceStatus.Suspended)
            .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
            .FirstOrDefault();
    }

    private static bool CanCurrentUserRecall(WorkflowInstance instance, Guid currentUserId)
        => instance.InitiatedById == currentUserId || instance.StartedById == currentUserId;

    private async Task<bool> TryApplyRecallStatusAsync(
        string entityType,
        Guid entityId,
        Guid userId,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!_workflowStatusAdapterRegistry.TryGetAdapter(entityType, out var adapter))
        {
            _logger.LogWarning("Workflow recall status was not applied because no adapter exists for {EntityType}", entityType);
            return false;
        }

        var entity = await ResolveWorkflowEntityForAdapterAsync(adapter, entityType, entityId, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning(
                "Workflow recall status was not applied because entity {EntityType} {EntityId} was not found",
                entityType,
                entityId);
            return false;
        }

        if (entity is PurchaseRequisition requisition)
        {
            await using var transaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null
                ? await _db.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                adapter.ApplyRecallOutcome(entity, userId, reason);
                await _requisitionBudgetControlService.ReleaseAsync(
                    requisition,
                    string.IsNullOrWhiteSpace(reason) ? "Purchase requisition workflow recalled." : reason,
                    "procurement.requisition.create",
                    HttpContext.TraceIdentifier,
                    cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            adapter.ApplyRecallOutcome(entity, userId, reason);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    private async Task<object?> ResolveWorkflowEntityForAdapterAsync(
        IWorkflowStatusAdapter adapter,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var aliases = adapter.EntityTypes
            .Append(entityType)
            .Select(NormalizeEntityTypeKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var modelEntityType = _db.Model
            .GetEntityTypes()
            .Where(et => et.FindPrimaryKey()?.Properties.Count == 1)
            .FirstOrDefault(et =>
                aliases.Contains(NormalizeEntityTypeKey(et.ClrType.Name)) ||
                aliases.Contains(NormalizeEntityTypeKey(et.GetTableName())) ||
                aliases.Contains(NormalizeEntityTypeKey(GetWorkflowEntityAliasForClr(et.ClrType))));

        if (modelEntityType == null)
        {
            return null;
        }

        return await _db.FindAsync(modelEntityType.ClrType, new object?[] { entityId }, cancellationToken);
    }

    private static string? GetWorkflowEntityAliasForClr(Type clrType)
        => clrType.Name switch
        {
            "EhcServiceRequest" => "ServiceRequest",
            _ => null
        };

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
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "User is not authorized to process workflow step {StepInstanceId}", id);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid workflow step processing request for {StepInstanceId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing workflow step {StepInstanceId}", id);
            return StatusCode(500, "An error occurred while processing workflow step");
        }
    }

    [HttpGet("steps/{id:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<WorkflowTaskAttachmentDto>>> GetStepAttachments(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var stepInstance = await _db.WorkflowStepInstances
                .AsNoTracking()
                .Include(si => si.WorkflowStep)
                .FirstOrDefaultAsync(si => si.Id == id && si.TenantId == tenantId && !si.IsDeleted);

            if (stepInstance == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                success = true,
                data = GetWorkflowTaskAttachments(stepInstance.ResultData)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow task attachments for step {StepInstanceId}", id);
            return StatusCode(500, "An error occurred while retrieving workflow task attachments");
        }
    }

    [HttpGet("steps/{id:guid}/attachments/{attachmentId}/download")]
    public async Task<IActionResult> DownloadStepAttachment(Guid id, string attachmentId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var stepInstance = await _db.WorkflowStepInstances
                .AsNoTracking()
                .Include(si => si.WorkflowStep)
                .FirstOrDefaultAsync(si => si.Id == id && si.TenantId == tenantId && !si.IsDeleted);

            if (stepInstance == null)
            {
                return NotFound();
            }

            var attachment = GetWorkflowTaskAttachments(stepInstance.ResultData)
                .FirstOrDefault(a => string.Equals(a.Id, attachmentId, StringComparison.OrdinalIgnoreCase));

            if (attachment == null || string.IsNullOrWhiteSpace(attachment.FilePath))
            {
                return NotFound(new { message = "Workflow task attachment was not found" });
            }

            var fileId = Guid.TryParse(attachment.Id, out var parsedFileId)
                ? parsedFileId
                : Guid.Empty;
            var stream = await _fileStorageService.DownloadFileAsync(attachment.FilePath, fileId);
            var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType;
            var fileName = string.IsNullOrWhiteSpace(attachment.FileName)
                ? "workflow-document"
                : attachment.FileName;

            return File(stream, contentType, fileName);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Workflow task attachment file was not found for step {StepInstanceId}, attachment {AttachmentId}", id, attachmentId);
            return NotFound(new { message = "Workflow task attachment file was not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading workflow task attachment {AttachmentId} for step {StepInstanceId}", attachmentId, id);
            return StatusCode(500, "An error occurred while downloading workflow task attachment");
        }
    }

    [HttpPost("steps/{id:guid}/attachments")]
    [RequestSizeLimit(26214400)] // 25MB
    public async Task<ActionResult<WorkflowTaskAttachmentDto>> UploadStepAttachment(
        Guid id,
        [FromForm] IFormFile file,
        [FromForm] string? requirementKey = null,
        [FromForm] string? documentName = null,
        [FromForm] string? documentType = null,
        [FromForm] Guid? documentOwnerId = null,
        [FromForm] DateTime? issueDate = null,
        [FromForm] DateTime? expiryDate = null,
        [FromForm] string? replacesAttachmentId = null)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var stepInstance = await _db.WorkflowStepInstances
                .Include(si => si.WorkflowStep)
                .Include(si => si.WorkflowInstance)
                .FirstOrDefaultAsync(si => si.Id == id && si.TenantId == tenantId && !si.IsDeleted);

            if (stepInstance == null)
            {
                return NotFound();
            }

            if (!await CanCurrentUserAttachToStepAsync(stepInstance, currentUserId.Value, HttpContext.RequestAborted))
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file provided" });
            }

            var evidencePolicy = await _db.WorkflowEvidencePolicies.AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive)
                .OrderByDescending(item => item.CreatedAt).FirstOrDefaultAsync(HttpContext.RequestAborted);
            var allowedExtensions = evidencePolicy == null
                ? new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".txt" }
                : JsonSerializer.Deserialize<string[]>(evidencePolicy.AllowedExtensionsJson) ?? [];
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = $"File type '{extension}' is not allowed" });
            }

            var maxFileSizeBytes = evidencePolicy?.MaximumFileSizeBytes ?? 25 * 1024 * 1024;
            if (file.Length > maxFileSizeBytes)
            {
                return BadRequest(new { message = $"File size exceeds maximum allowed size of {maxFileSizeBytes / 1024 / 1024}MB" });
            }

            var safeRequirementKey = string.IsNullOrWhiteSpace(requirementKey) ? null : requirementKey.Trim();
            string? checklistItemId = null;
            var checklist = GetChecklistFromStepConfiguration(stepInstance.WorkflowStep?.Configuration);
            WorkflowQualityCheckDto? checklistItem = null;
            WorkflowDocumentRequirementDto? stepDocumentRequirement = null;

            if (!string.IsNullOrWhiteSpace(safeRequirementKey) && checklist.Count > 0)
            {
                // Resolve named upload requirements so attachments stay tied to the workflow step that requested them.
                checklistItem = checklist.FirstOrDefault(item =>
                    string.Equals(
                        WorkflowChecklistEvidenceValidator.NormalizeKey(item.Id, item.Name),
                        safeRequirementKey,
                        StringComparison.OrdinalIgnoreCase));

                if (checklistItem != null)
                {
                    if (!checklistItem.RequiresDocument)
                    {
                        return BadRequest(new { message = "The selected checklist item does not require document evidence" });
                    }

                    checklistItemId = WorkflowChecklistEvidenceValidator.NormalizeKey(checklistItem.Id, checklistItem.Name);
                    safeRequirementKey = checklistItemId;
                    documentName = checklistItem.DocumentName;
                    documentType = checklistItem.DocumentType;
                }
            }

            if (stepInstance.WorkflowStep?.StepType == WorkflowStepType.Approval && checklistItem == null)
            {
                // Finance payment evidence is policy-driven rather than an approval check-box. The
                // workflow step advertises the allowed named keys through TaskConfig, while the AP
                // approval gate evaluates the effective policy snapshot and verification status.
                var taskConfig = GetTaskConfigFromStepConfiguration(stepInstance.WorkflowStep.Configuration);
                stepDocumentRequirement = taskConfig?.DocumentRequirements.FirstOrDefault(requirement =>
                    string.Equals(requirement.RequirementKey, safeRequirementKey, StringComparison.OrdinalIgnoreCase));
                if (stepDocumentRequirement == null)
                {
                    return BadRequest(new { message = "The selected workflow step does not define this document requirement" });
                }

                safeRequirementKey = stepDocumentRequirement.RequirementKey;
                documentName = stepDocumentRequirement.DocumentName;
                documentType = stepDocumentRequirement.DocumentType;
            }

            if (stepInstance.WorkflowStep?.StepType != WorkflowStepType.Approval && checklistItem == null)
            {
                safeRequirementKey ??= "task-document";
            }

            if (expiryDate.HasValue && issueDate.HasValue && expiryDate <= issueDate)
                return BadRequest(new { message = "Document expiry date must be after its issue date" });

            await using var content = new MemoryStream();
            await file.CopyToAsync(content, HttpContext.RequestAborted);
            var bytes = content.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var scanText = Encoding.ASCII.GetString(bytes);
            var infected = scanText.Contains("EICAR-STANDARD-ANTIVIRUS-TEST-FILE", StringComparison.Ordinal);
            if (evidencePolicy?.RequireMalwareScan != false && infected)
                return BadRequest(new { message = "The uploaded document failed malware scanning" });

            WorkflowEvidenceDocument? replacedEvidence = null;
            if (!string.IsNullOrWhiteSpace(replacesAttachmentId))
            {
                replacedEvidence = await _db.WorkflowEvidenceDocuments.FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.StepInstanceId == id && item.AttachmentId == replacesAttachmentId &&
                    item.IsCurrent && !item.IsDeleted, HttpContext.RequestAborted);
                if (replacedEvidence == null) return BadRequest(new { message = "The evidence being replaced was not found" });
                replacedEvidence.IsCurrent = false;
                replacedEvidence.UpdatedAt = DateTime.UtcNow;
            }

            var filePath = $"workflow/{tenantId:N}/{id:N}";
            content.Position = 0;
            var uploadedPath = await _fileStorageService.UploadFileAsync(content, file.FileName, filePath);

            if (string.IsNullOrWhiteSpace(uploadedPath))
            {
                return StatusCode(500, "Failed to upload workflow task attachment");
            }

            var attachment = new WorkflowTaskAttachmentDto
            {
                Id = Guid.NewGuid().ToString("N"),
                RequirementKey = safeRequirementKey,
                ChecklistItemId = checklistItemId,
                DocumentType = string.IsNullOrWhiteSpace(documentType) ? null : documentType.Trim(),
                DocumentName = string.IsNullOrWhiteSpace(documentName) ? null : documentName.Trim(),
                FileName = file.FileName,
                FilePath = uploadedPath,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow,
                UploadedById = currentUserId.Value,
                UploadedByName = _currentUserService.UserName,
                DocumentOwnerId = documentOwnerId ?? currentUserId.Value,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                Version = (replacedEvidence?.Version ?? 0) + 1,
                ReplacesAttachmentId = replacedEvidence?.AttachmentId,
                VerificationStatus = WorkflowEvidenceVerificationStatus.Pending,
                MalwareScanStatus = WorkflowMalwareScanStatus.Clean,
                Sha256 = hash
            };

            _db.WorkflowEvidenceDocuments.Add(new WorkflowEvidenceDocument
            {
                TenantId = tenantId, StepInstanceId = id, AttachmentId = attachment.Id,
                RequirementKey = attachment.RequirementKey, ChecklistItemId = attachment.ChecklistItemId,
                DocumentType = attachment.DocumentType, DocumentName = attachment.DocumentName,
                FileName = attachment.FileName, FilePath = attachment.FilePath, ContentType = attachment.ContentType,
                FileSizeBytes = attachment.FileSizeBytes, Sha256 = hash, UploadedAt = attachment.UploadedAt,
                UploadedById = currentUserId.Value, DocumentOwnerId = attachment.DocumentOwnerId.Value,
                IssueDate = issueDate, ExpiryDate = expiryDate, Version = attachment.Version,
                ReplacesEvidenceId = replacedEvidence?.Id, VerificationStatus = attachment.VerificationStatus,
                MalwareScanStatus = attachment.MalwareScanStatus, MalwareScanResult = "Content scan completed",
                RetainUntil = DateTime.UtcNow.AddDays(evidencePolicy?.RetentionDays ?? 2555),
                CreatedById = currentUserId.Value, CreatedBy = _currentUserService.UserName
            });

            stepInstance.ResultData = BuildStepResultDataWithTaskAttachment(stepInstance.ResultData, attachment);
            stepInstance.UpdatedAt = DateTime.UtcNow;
            stepInstance.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                data = attachment
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading workflow task attachment for step {StepInstanceId}", id);
            return StatusCode(500, "An error occurred while uploading workflow task attachment");
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
    /// Saves the checklist responses for the current actor before a workflow step is completed.
    /// </summary>
    [HttpPost("steps/{id:guid}/checklist-responses")]
    public async Task<ActionResult> SaveStepChecklistResponses(Guid id, [FromBody] SaveWorkflowChecklistResponsesRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            var stepInstance = await _db.WorkflowStepInstances
                .Include(si => si.WorkflowStep)
                .FirstOrDefaultAsync(si => si.Id == id && si.TenantId == tenantId && !si.IsDeleted);

            if (stepInstance == null)
            {
                return NotFound();
            }

            var canAct = CanCurrentUserCompleteWorkflowTask(stepInstance, currentUserId.Value);
            if (!canAct)
            {
                var approvals = await _db.WorkflowApprovals
                    .Where(a => a.StepInstanceId == id && a.Status == WorkflowApprovalStatus.Pending && !a.IsDeleted)
                    .ToListAsync();

                var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                canAct = approvals.Any(a =>
                    (a.ApproverId.HasValue && a.ApproverId.Value == currentUserId.Value) ||
                    (!string.IsNullOrWhiteSpace(a.ApproverRole) && roleSet.Contains(a.ApproverRole)));
            }

            if (!canAct)
            {
                return Forbid();
            }

            var checklist = GetChecklistFromStepConfiguration(stepInstance.WorkflowStep?.Configuration);
            var attachments = GetWorkflowTaskAttachments(stepInstance.ResultData);
            var responses = PrepareChecklistResponses(
                checklist,
                request?.Responses ?? new List<WorkflowApprovalChecklistResponseDto>(),
                attachments,
                currentUserId.Value,
                _currentUserService.UserName);
            var validationErrors = WorkflowChecklistEvidenceValidator.Validate(checklist, responses, attachments);
            if (validationErrors.Any())
            {
                return BadRequest(string.Join(" ", validationErrors));
            }

            stepInstance.ResultData = BuildStepResultDataWithChecklistResponses(stepInstance.ResultData, responses);
            stepInstance.UpdatedAt = DateTime.UtcNow;
            stepInstance.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving checklist responses for workflow step {StepInstanceId}", id);
            return StatusCode(500, "An error occurred while saving checklist responses");
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

            var supplierDebitNoteWorkflow = await _db.WorkflowStepInstances
                .AsNoTracking()
                .Where(step => step.Id == approval.StepInstanceId && !step.IsDeleted)
                .Select(step => new
                {
                    step.TenantId,
                    step.WorkflowInstance.EntityId,
                    EntityTypeCode = step.WorkflowInstance.EntityType.Code
                })
                .FirstOrDefaultAsync(HttpContext.RequestAborted);
            if (supplierDebitNoteWorkflow != null &&
                supplierDebitNoteWorkflow.TenantId == approval.TenantId &&
                string.Equals(supplierDebitNoteWorkflow.EntityTypeCode, "SupplierDebitNote", StringComparison.OrdinalIgnoreCase))
            {
                // The shared workflow endpoint may advance steps but cannot update the AP
                // document, enforce its purpose-specific permission, or post its audit outcome.
                // Review this Finance-owned document through the canonical domain boundary.
                return Conflict(new
                {
                    code = "FINANCE_DOMAIN_APPROVAL_REQUIRED",
                    route = $"/api/ap/supplier-debit-notes/{supplierDebitNoteWorkflow.EntityId}/approval"
                });
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
            var stepInstance = await _db.WorkflowStepInstances
                .AsNoTracking()
                .Include(si => si.WorkflowStep)
                .FirstOrDefaultAsync(si => si.Id == approval.StepInstanceId && !si.IsDeleted);
            var checklist = GetChecklistFromStepConfiguration(stepInstance?.WorkflowStep?.Configuration);
            var attachments = GetWorkflowTaskAttachments(stepInstance?.ResultData);
            var checklistResponses = request.ChecklistResponses?.Any() == true
                ? PrepareChecklistResponses(
                    checklist,
                    request.ChecklistResponses,
                    attachments,
                    currentUserId.Value,
                    _currentUserService.UserName)
                : null;
            object? resultData;
            if (request.Action == ErpSystem.Core.Enums.WorkflowApprovalAction.Delegate)
            {
                resultData = new { delegateToId = request.DelegateToId };
            }
            else
            {
                var payload = new Dictionary<string, object?>();
                if (checklistResponses?.Any() == true) payload["approvalChecklistResponses"] = checklistResponses;
                if (request.Signature != null) payload["signature"] = request.Signature;
                resultData = payload.Count == 0 ? null : payload;
            }

            var executionResult = await _workflowEngine.ProcessStepAsync(
                approval.StepInstanceId, currentUserId.Value, stepAction, resultData, request.Comments);
            if (!executionResult.Success)
            {
                return BadRequest(new { success = false, error = executionResult.Message, errors = executionResult.Errors });
            }

            if (request.Action == ErpSystem.Core.Enums.WorkflowApprovalAction.Approve && request.Signature != null)
            {
                var certificate = WorkflowSignatureValidator.InspectCertificate(request.Signature.CertificateBase64, DateTime.UtcNow);
                var signedPayload = $"{approval.Id:N}|{currentUserId.Value:N}|{request.Signature.SignedAt:O}|{request.Signature.Attestation}";
                _db.WorkflowSignatureEvidence.Add(new WorkflowSignatureEvidence
                {
                    TenantId = approval.TenantId,
                    ApprovalId = approval.Id,
                    SignerUserId = currentUserId.Value,
                    Method = request.Signature.Method,
                    Attestation = request.Signature.Attestation.Trim(),
                    CertificateThumbprint = certificate?.Thumbprint,
                    CertificateSubject = certificate?.Subject,
                    CertificateNotBefore = certificate?.NotBefore,
                    CertificateNotAfter = certificate?.NotAfter,
                    CertificateChainValid = certificate?.ChainValid ?? false,
                    SubmissionJson = JsonSerializer.Serialize(request.Signature),
                    SignedPayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signedPayload))),
                    SignedAt = request.Signature.SignedAt,
                    IsCommitted = true,
                    CommittedAt = DateTime.UtcNow,
                    IpAddress = _currentUserService.IpAddress,
                    UserAgent = _currentUserService.UserAgent,
                    CreatedById = currentUserId.Value,
                    CreatedBy = _currentUserService.UserName
                });
                await _db.SaveChangesAsync(HttpContext.RequestAborted);
            }

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

            // PR review: generic workflow approvals must resync module-owned procedure cases after approver actions.
            var procedureCaseId = await _db.ProcedureCases
                .AsNoTracking()
                .Where(item => item.TenantId == instance.TenantId
                    && item.Id == instance.EntityId
                    && item.WorkflowInstanceId == instance.Id
                    && !item.IsDeleted)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (procedureCaseId.HasValue)
            {
                await _procedureCaseService.SyncFromWorkflowRuntimeAsync(
                    procedureCaseId.Value,
                    instance.Id,
                    actorUserId,
                    request.Comments);
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
            DefinitionKey = definition.DefinitionKey,
            Name = definition.Name,
            Description = definition.Description,
            EntityType = definition.EntityType?.Name ?? "Unknown",
            Configuration = definition.Configuration,
            IsActive = definition.IsActive,
            Version = definition.Version,
            LifecycleStatus = definition.LifecycleStatus,
            ChangeSummary = definition.ChangeSummary,
            SupersedesDefinitionId = definition.SupersedesDefinitionId,
            PublishedAt = definition.PublishedAt,
            RetiredAt = definition.RetiredAt,
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
            // Use the guarded deserializer because workflow JSON may come from older saved definitions.
            Configuration = DeserializeWorkflowJson<WorkflowStepConfigurationDto>(step.Configuration)
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
            // Keep transition deserialization non-fatal for legacy definitions edited by other modules.
            Condition = DeserializeWorkflowJson<WorkflowConditionDto>(transition.Condition)
        };
    }

    private static T? DeserializeWorkflowJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, WorkflowJsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
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

    private static string GetPendingApproverKey(WorkflowApprovalStatusDto approval)
    {
        var approverId = approval.ApproverId == Guid.Empty ? string.Empty : approval.ApproverId.ToString("N");
        var role = approval.ApproverRole?.Trim() ?? string.Empty;
        var name = approval.ApproverName?.Trim() ?? string.Empty;
        return $"{approverId}|{role}|{name}";
    }

    private static string? FormatApprovalOwnerList(IEnumerable<WorkflowApprovalAuditDto> approvals)
    {
        var owners = approvals
            .Select(a => !string.IsNullOrWhiteSpace(a.ApproverName)
                ? a.ApproverName
                : !string.IsNullOrWhiteSpace(a.ApproverRole)
                    ? a.ApproverRole
                    : a.ApproverId?.ToString())
            .Where(owner => !string.IsNullOrWhiteSpace(owner))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return owners.Count switch
        {
            0 => null,
            1 => owners[0],
            2 => string.Join(", ", owners),
            _ => $"{string.Join(", ", owners.Take(2))} +{owners.Count - 2}"
        };
    }

    private async Task<WorkflowStepInstance?> GetStepInstanceForSummaryAsync(Guid? stepInstanceId, CancellationToken cancellationToken)
    {
        if (!stepInstanceId.HasValue || stepInstanceId.Value == Guid.Empty)
        {
            return null;
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        return await _db.WorkflowStepInstances
            .AsNoTracking()
            .Include(si => si.WorkflowStep)
            .FirstOrDefaultAsync(si => si.Id == stepInstanceId.Value && si.TenantId == tenantId && !si.IsDeleted, cancellationToken);
    }

    private async Task<List<WorkflowQualityCheckDto>> GetStepChecklistAsync(Guid? stepInstanceId, CancellationToken cancellationToken)
    {
        if (!stepInstanceId.HasValue || stepInstanceId.Value == Guid.Empty)
        {
            return new List<WorkflowQualityCheckDto>();
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var stepInstance = await _db.WorkflowStepInstances
            .AsNoTracking()
            .Include(si => si.WorkflowStep)
            .FirstOrDefaultAsync(si => si.Id == stepInstanceId.Value && si.TenantId == tenantId && !si.IsDeleted, cancellationToken);

        return GetChecklistFromStepConfiguration(stepInstance?.WorkflowStep?.Configuration);
    }

    private bool CanCurrentUserCompleteWorkflowTask(WorkflowStepInstance stepInstance, Guid currentUserId)
    {
        if (stepInstance.WorkflowStep?.StepType != WorkflowStepType.Manual)
        {
            return false;
        }

        if (stepInstance.Status != WorkflowStepInstanceStatus.Pending &&
            stepInstance.Status != WorkflowStepInstanceStatus.InProgress)
        {
            return false;
        }

        if (stepInstance.AssignedToId.HasValue && stepInstance.AssignedToId.Value == currentUserId)
        {
            return true;
        }

        var config = DeserializeStepConfiguration(stepInstance.WorkflowStep?.Configuration);
        var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(stepInstance.WorkflowStep?.RequiredRole) &&
            roleSet.Contains(stepInstance.WorkflowStep.RequiredRole))
        {
            return true;
        }

        var assignedRoles = config?.AssignmentRules?
            .Where(rule => rule.AssignmentType == WorkflowAssignmentType.Role && !string.IsNullOrWhiteSpace(rule.Role))
            .Select(rule => rule.Role!)
            .ToList() ?? new List<string>();

        return assignedRoles.Any(roleSet.Contains);
    }

    private async Task<bool> CanCurrentUserAttachToStepAsync(
        WorkflowStepInstance stepInstance,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (stepInstance.Status != WorkflowStepInstanceStatus.Pending &&
            stepInstance.Status != WorkflowStepInstanceStatus.InProgress)
        {
            return false;
        }

        if (stepInstance.WorkflowStep?.StepType == WorkflowStepType.Manual)
        {
            return CanCurrentUserCompleteWorkflowTask(stepInstance, currentUserId);
        }

        if (stepInstance.WorkflowStep?.StepType != WorkflowStepType.Approval)
        {
            return false;
        }

        // The workflow initiator remains responsible for assembling requested support while the
        // approval is pending. They may upload, but cannot verify or approve their own payment;
        // those controls are enforced independently by evidence verification and SOD guards.
        if (stepInstance.WorkflowInstance?.InitiatedById == currentUserId ||
            stepInstance.WorkflowInstance?.StartedById == currentUserId)
        {
            return true;
        }

        var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        return await _db.WorkflowApprovals
            .AsNoTracking()
            .AnyAsync(approval =>
                approval.StepInstanceId == stepInstance.Id &&
                approval.Status == WorkflowApprovalStatus.Pending &&
                !approval.IsDeleted &&
                ((approval.ApproverId.HasValue && approval.ApproverId.Value == currentUserId) ||
                 (!string.IsNullOrWhiteSpace(approval.ApproverRole) && roleSet.Contains(approval.ApproverRole))),
                cancellationToken);
    }

    private static WorkflowStepConfigurationDto? DeserializeStepConfiguration(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(configurationJson, WorkflowJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static WorkflowTaskConfigDto? GetTaskConfigFromStepConfiguration(string? configurationJson)
    {
        var taskConfig = DeserializeStepConfiguration(configurationJson)?.TaskConfig;
        if (taskConfig == null)
        {
            return null;
        }

        var taskActionType = string.IsNullOrWhiteSpace(taskConfig.TaskActionType)
            ? "general"
            : taskConfig.TaskActionType.Trim();

        return new WorkflowTaskConfigDto
        {
            TaskActionType = taskActionType,
            DocumentName = string.IsNullOrWhiteSpace(taskConfig.DocumentName) ? null : taskConfig.DocumentName.Trim(),
            RequiresDocument = taskConfig.RequiresDocument ||
                taskConfig.DocumentRequirements.Any(requirement => requirement.IsRequired) ||
                taskActionType.Equals("document", StringComparison.OrdinalIgnoreCase),
            DocumentRequirementKey = string.IsNullOrWhiteSpace(taskConfig.DocumentRequirementKey) ? null : taskConfig.DocumentRequirementKey.Trim(),
            DocumentRequirements = NormalizeDocumentRequirements(taskConfig),
            Instructions = string.IsNullOrWhiteSpace(taskConfig.Instructions) ? null : taskConfig.Instructions.Trim()
        };
    }

    private static List<WorkflowDocumentRequirementDto> NormalizeDocumentRequirements(WorkflowTaskConfigDto taskConfig)
    {
        // Surface a normalized requirement list to the UI while still honoring older single-document configs.
        var configured = taskConfig.DocumentRequirements
            .Where(requirement =>
                !string.IsNullOrWhiteSpace(requirement.DocumentName) ||
                !string.IsNullOrWhiteSpace(requirement.RequirementKey))
            .Select((requirement, index) => new WorkflowDocumentRequirementDto
            {
                Id = string.IsNullOrWhiteSpace(requirement.Id) ? $"document-{index + 1}" : requirement.Id,
                RequirementKey = string.IsNullOrWhiteSpace(requirement.RequirementKey)
                    ? BuildRequirementKey(requirement.DocumentName, index)
                    : requirement.RequirementKey.Trim(),
                DocumentName = string.IsNullOrWhiteSpace(requirement.DocumentName)
                    ? $"Document {index + 1}"
                    : requirement.DocumentName.Trim(),
                DocumentType = string.IsNullOrWhiteSpace(requirement.DocumentType) ? null : requirement.DocumentType.Trim(),
                IsRequired = requirement.IsRequired,
            })
            .ToList();

        if (configured.Count > 0)
        {
            return configured;
        }

        if (taskConfig.RequiresDocument ||
            taskConfig.TaskActionType.Equals("document", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(taskConfig.DocumentName))
        {
            configured.Add(new WorkflowDocumentRequirementDto
            {
                Id = "document-1",
                RequirementKey = string.IsNullOrWhiteSpace(taskConfig.DocumentRequirementKey)
                    ? BuildRequirementKey(taskConfig.DocumentName, 0)
                    : taskConfig.DocumentRequirementKey.Trim(),
                DocumentName = string.IsNullOrWhiteSpace(taskConfig.DocumentName)
                    ? "Required document"
                    : taskConfig.DocumentName.Trim(),
                IsRequired = true,
            });
        }

        return configured;
    }

    private static string BuildRequirementKey(string? value, int index)
    {
        var source = string.IsNullOrWhiteSpace(value) ? $"document-{index + 1}" : value.Trim().ToLowerInvariant();
        var key = new string(source.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        while (key.Contains("--", StringComparison.Ordinal))
        {
            key = key.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(key) ? $"document-{index + 1}" : key;
    }

    private static List<WorkflowQualityCheckDto> GetChecklistFromStepConfiguration(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new List<WorkflowQualityCheckDto>();
        }

        try
        {
            var config = JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(configurationJson, WorkflowJsonOptions);
            return config?.QualityConfig?.QualityChecks?
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .Select((item, index) => new WorkflowQualityCheckDto
                {
                    Id = string.IsNullOrWhiteSpace(item.Id) ? $"check-{index + 1}" : item.Id,
                    Name = item.Name.Trim(),
                    Description = item.Description?.Trim() ?? string.Empty,
                    IsRequired = item.IsRequired,
                    RequiresDocument = item.RequiresDocument,
                    DocumentType = string.IsNullOrWhiteSpace(item.DocumentType) ? null : item.DocumentType.Trim(),
                    DocumentName = string.IsNullOrWhiteSpace(item.DocumentName) ? null : item.DocumentName.Trim(),
                    ApplicabilityCondition = item.ApplicabilityCondition,
                    ExpectedValue = item.ExpectedValue,
                    ValidationExpression = item.ValidationExpression
                })
                .ToList() ?? new List<WorkflowQualityCheckDto>();
        }
        catch
        {
            return new List<WorkflowQualityCheckDto>();
        }
    }

    private static List<WorkflowApprovalChecklistResponseDto> PrepareChecklistResponses(
        IReadOnlyCollection<WorkflowQualityCheckDto> checklist,
        IReadOnlyCollection<WorkflowApprovalChecklistResponseDto> responses,
        IReadOnlyCollection<WorkflowTaskAttachmentDto> attachments,
        Guid currentUserId,
        string? currentUserName)
    {
        var responseLookup = responses
            .Where(response => !string.IsNullOrWhiteSpace(response.Id) || !string.IsNullOrWhiteSpace(response.Name))
            .GroupBy(response => WorkflowChecklistEvidenceValidator.NormalizeKey(response.Id, response.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        return checklist
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item =>
            {
                var key = WorkflowChecklistEvidenceValidator.NormalizeKey(item.Id, item.Name);
                responseLookup.TryGetValue(key, out var response);
                var evidence = WorkflowChecklistEvidenceValidator.GetAttachmentsForItem(item, attachments);

                return new WorkflowApprovalChecklistResponseDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    IsSatisfied = response?.IsSatisfied == true,
                    Notes = string.IsNullOrWhiteSpace(response?.Notes) ? null : response.Notes.Trim(),
                    AttachmentIds = evidence.Select(attachment => attachment.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    CompletedById = response == null ? null : currentUserId,
                    CompletedByName = response == null ? null : currentUserName,
                    CompletedAt = response == null ? null : DateTime.UtcNow
                };
            })
            .ToList();
    }

    private static List<WorkflowApprovalChecklistResponseDto> GetApprovalChecklistResponses(string? resultData)
    {
        if (string.IsNullOrWhiteSpace(resultData))
        {
            return new List<WorkflowApprovalChecklistResponseDto>();
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(resultData, WorkflowJsonOptions);
            if (payload == null ||
                !payload.TryGetValue("approvalChecklistResponses", out var responsesElement) ||
                responsesElement.ValueKind != JsonValueKind.Array)
            {
                return new List<WorkflowApprovalChecklistResponseDto>();
            }

            return JsonSerializer.Deserialize<List<WorkflowApprovalChecklistResponseDto>>(
                responsesElement.GetRawText(),
                WorkflowJsonOptions) ?? new List<WorkflowApprovalChecklistResponseDto>();
        }
        catch
        {
            return new List<WorkflowApprovalChecklistResponseDto>();
        }
    }

    private static string BuildStepResultDataWithChecklistResponses(
        string? existingResultData,
        List<WorkflowApprovalChecklistResponseDto> responses)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(existingResultData))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existingResultData, WorkflowJsonOptions);
                if (existing != null)
                {
                    foreach (var item in existing)
                    {
                        payload[item.Key] = item.Value.Clone();
                    }
                }
            }
            catch
            {
                // Keep malformed legacy result data from blocking checklist capture.
            }
        }

        payload["approvalChecklistResponses"] = responses;
        return JsonSerializer.Serialize(payload, WorkflowJsonOptions);
    }

    private static List<WorkflowTaskAttachmentDto> GetWorkflowTaskAttachments(string? resultData)
    {
        if (string.IsNullOrWhiteSpace(resultData))
        {
            return new List<WorkflowTaskAttachmentDto>();
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(resultData, WorkflowJsonOptions);
            if (payload == null ||
                !payload.TryGetValue("workflowTaskAttachments", out var attachmentsElement) ||
                attachmentsElement.ValueKind != JsonValueKind.Array)
            {
                return new List<WorkflowTaskAttachmentDto>();
            }

            return JsonSerializer.Deserialize<List<WorkflowTaskAttachmentDto>>(
                attachmentsElement.GetRawText(),
                WorkflowJsonOptions) ?? new List<WorkflowTaskAttachmentDto>();
        }
        catch
        {
            return new List<WorkflowTaskAttachmentDto>();
        }
    }

    private static string BuildStepResultDataWithTaskAttachment(
        string? existingResultData,
        WorkflowTaskAttachmentDto attachment)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(existingResultData))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existingResultData, WorkflowJsonOptions);
                if (existing != null)
                {
                    foreach (var item in existing)
                    {
                        payload[item.Key] = item.Value.Clone();
                    }
                }
            }
            catch
            {
                // Keep malformed legacy result data from blocking document capture.
            }
        }

        var attachments = GetWorkflowTaskAttachments(existingResultData);
        attachments.Add(attachment);
        payload["workflowTaskAttachments"] = attachments;
        return JsonSerializer.Serialize(payload, WorkflowJsonOptions);
    }

    private async Task<Guid?> ResolveCurrentUserApprovalIdAsync(Guid? stepInstanceId, Guid userId)
    {
        if (!stepInstanceId.HasValue) return null;
        var roles = new HashSet<string>(_currentUserService.Roles ?? [], StringComparer.OrdinalIgnoreCase);
        return await _db.WorkflowApprovals.AsNoTracking()
            .Where(approval => approval.StepInstanceId == stepInstanceId.Value && !approval.IsDeleted &&
                approval.Status == WorkflowApprovalStatus.Pending &&
                (approval.ApproverId == userId || approval.ApproverRole != null && roles.Contains(approval.ApproverRole)))
            .OrderBy(approval => approval.RequestedDate)
            .Select(approval => (Guid?)approval.Id)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
    }

    private Task<WorkflowCorrectionRequest?> ResolveCurrentUserCorrectionAsync(Guid instanceId, Guid userId) =>
        _db.WorkflowCorrectionRequests.AsNoTracking()
            .Where(item => item.WorkflowInstanceId == instanceId && item.CorrectionOwnerId == userId &&
                item.Status == WorkflowCorrectionStatus.Open && !item.IsDeleted)
            .OrderByDescending(item => item.RequestedAt)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

    private static JsonSerializerOptions CreateWorkflowJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
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
/// Request DTO for recalling an active workflow back to draft.
/// </summary>
public class RecallWorkflowRequest
{
    public string? Reason { get; set; }
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
    public List<WorkflowApprovalChecklistResponseDto>? ChecklistResponses { get; set; }
    public Guid? DelegateToId { get; set; }
    public WorkflowSignatureSubmissionDto? Signature { get; set; }
}

public class SaveWorkflowChecklistResponsesRequest
{
    public List<WorkflowApprovalChecklistResponseDto> Responses { get; set; } = new();
}

#endregion

