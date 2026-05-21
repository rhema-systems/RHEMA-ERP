using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/quality-control")]
// [Authorize] // Temporarily disabled for testing
public class QualityControlController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<QualityControlController> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Services.QualityCertificateService _certificateService;
    private readonly Core.Services.Maintenance.IQualityControlService _qualityControlService;

    public QualityControlController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<QualityControlController> logger,
        IHttpContextAccessor httpContextAccessor,
        Services.QualityCertificateService certificateService,
        Core.Services.Maintenance.IQualityControlService qualityControlService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _certificateService = certificateService;
        _qualityControlService = qualityControlService;
    }

    /// <summary>
    /// Validates if a work order can be completed based on quality control requirements.
    /// Delegates to the core QualityControlService so the same rules are used by APIs and services.
    /// </summary>
    [HttpGet("validate-work-order/{workOrderId}")]
    public async Task<ActionResult<QualityValidationResult>> ValidateWorkOrderCompletion(string workOrderId)
    {
        try
        {
            _logger.LogInformation("Validating work order {WorkOrderId} for completion", workOrderId);

            if (!Guid.TryParse(workOrderId, out var woId))
            {
                return BadRequest("Invalid work order ID format");
            }

            // Use the core quality control service for validation logic
            var coreResult = await _qualityControlService.ValidateWorkOrderCompletionAsync(woId);

            var apiResult = new QualityValidationResult
            {
                WorkOrderId = coreResult.WorkOrderId.ToString(),
                CanComplete = coreResult.CanComplete,
                ValidationDate = coreResult.ValidationDate.ToString("O"),
                RequiresInspectionOfficerApproval = coreResult.RequiresInspectionOfficerApproval,
                ValidationMessages = coreResult.ValidationMessages.ToList(),
                ValidationFailures = coreResult.ValidationFailures.ToList(),
                RequiredInspections = coreResult.RequiredInspections.Select(ri => new RequiredInspection
                {
                    InspectionTemplateId = ri.InspectionTemplateId.ToString(),
                    InspectionType = ri.InspectionType,
                    InspectionName = ri.InspectionName,
                    IsRegulatory = ri.IsRegulatory,
                    Description = ri.Description ?? string.Empty,
                    IsCompleted = ri.IsCompleted,
                    CompletedDate = ri.CompletedDate?.ToString("O"),
                    Result = ri.Result
                }).ToList()
            };

            return Ok(apiResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating work order {WorkOrderId} for completion", workOrderId);
            return StatusCode(500, "An error occurred while validating the work order");
        }
    }

    /// <summary>
    /// Gets required inspections for an asset and work order type
    /// </summary>
    [HttpGet("required-inspections")]
    public async Task<ActionResult<List<RequiredInspection>>> GetRequiredInspections(
        [FromQuery] string assetId,
        [FromQuery] string workOrderType)
    {
        try
        {
            _logger.LogInformation("Getting required inspections for asset {AssetId} and type {WorkOrderType}",
                assetId, workOrderType);

            // Get checklists that match this work order type
            var checklists = await _context.QualityControlChecklists
                .Where(c => c.WorkOrderType == workOrderType && c.IsActive && c.IsMandatory)
                .ToListAsync();

            var requiredInspections = checklists.Select(c => new RequiredInspection
            {
                InspectionTemplateId = c.Id.ToString(),
                InspectionType = c.WorkOrderType ?? "General",
                InspectionName = c.Name,
                IsRegulatory = c.IsMandatory,
                Description = c.Description ?? "",
                IsCompleted = false
            }).ToList();

            return Ok(requiredInspections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting required inspections");
            return StatusCode(500, "An error occurred while retrieving required inspections");
        }
    }

    /// <summary>
    /// Submits a work order for quality control inspection
    /// </summary>
    [HttpPost("submit-for-inspection/{workOrderId}")]
    public async Task<ActionResult<WorkOrderQualityCheckDto>> SubmitForInspection(string workOrderId)
    {
        try
        {
            _logger.LogInformation("Submitting work order {WorkOrderId} for QC inspection", workOrderId);

            if (!Guid.TryParse(workOrderId, out var woId))
            {
                return BadRequest("Invalid work order ID format");
            }

            // Get work order with related entities
            var workOrder = await _context.WorkOrders
                .Include(wo => wo.Asset)
                    .ThenInclude(a => a.AssetCategory)
                .Include(wo => wo.WorkOrderType)
                .Include(wo => wo.MaintenanceType)
                .FirstOrDefaultAsync(wo => wo.Id == woId);

            if (workOrder == null)
            {
                return NotFound($"Work order with ID {workOrderId} not found");
            }

            // Check if already submitted for inspection (Pending or InProgress)
            var existingInspection = await _context.WorkOrderQualityChecks
                .Where(qc => qc.TenantId == workOrder.TenantId &&
                    qc.WorkOrderId == woId &&
                    (qc.OverallResult == "Pending" || qc.OverallResult == "InProgress"))
                .OrderBy(qc => qc.OverallResult == "InProgress" ? 0 : 1)
                .ThenByDescending(qc => qc.InspectionDate)
                .FirstOrDefaultAsync();

            if (existingInspection != null)
            {
                return BadRequest("Work order already submitted for inspection");
            }

            // Find the most appropriate quality checklist template
            var assetCategoryId = workOrder.Asset?.AssetCategoryId;
            var assetCategory = workOrder.Asset?.AssetCategory?.Name;
            var workOrderTypeId = workOrder.WorkOrderTypeId;
            var workOrderTypeName = workOrder.WorkOrderType?.Name;
            var maintenanceTypeId = workOrder.MaintenanceTypeId;
            var maintenanceTypeName = workOrder.MaintenanceType?.Name;

            // Log the values we're searching for
            _logger.LogInformation("Looking for checklist with AssetCategoryId={AssetCategoryId}, AssetCategory={AssetCategory}, WorkOrderTypeId={WorkOrderTypeId}, WorkOrderType={WorkOrderType}, MaintenanceTypeId={MaintenanceTypeId}, MaintenanceType={MaintenanceType}",
                assetCategoryId, assetCategory, workOrderTypeId, workOrderTypeName, maintenanceTypeId, maintenanceTypeName);

            var checklist = await FindBestChecklistTemplateAsync(
                workOrder.TenantId,
                maintenanceTypeId,
                maintenanceTypeName,
                workOrderTypeId,
                workOrderTypeName,
                assetCategoryId,
                assetCategory);

            if (checklist != null)
            {
                _logger.LogInformation("Found checklist {ChecklistId} ({ChecklistName}) using Maintenance Type first matching", checklist.Id, checklist.Name);
            }

            if (checklist == null)
            {
                // Log all available checklists for debugging
                var allChecklists = await _context.QualityControlChecklists
                    .Where(c => c.IsActive)
                    .Select(c => new { c.Id, c.Name, c.AssetCategory, c.WorkOrderType, c.MaintenanceType })
                    .ToListAsync();
                _logger.LogWarning("No checklist found. Available active checklists: {Checklists}",
                    System.Text.Json.JsonSerializer.Serialize(allChecklists));

                return BadRequest($"No quality checklist found for asset category '{assetCategory}', work order type '{workOrderTypeName}', and maintenance type '{maintenanceTypeName}'");
            }

            var checklistItemCount = CountChecklistItems(checklist.ChecklistItems);
            if (checklistItemCount == 0)
            {
                _logger.LogWarning("Checklist {ChecklistId} ({ChecklistName}) has no items and cannot be used for work order {WorkOrderId}",
                    checklist.Id, checklist.Name, workOrderId);

                return BadRequest($"Quality checklist '{checklist.Name}' has no checklist items. Add checklist items before submitting this work order for inspection.");
            }

            // Create quality check record
            var qualityCheck = new ErpSystem.Core.Entities.Maintenance.WorkOrderQualityCheck
            {
                WorkOrderId = woId,
                ChecklistId = checklist.Id,
                InspectorId = _currentUserService?.EmployeeId ?? Guid.Empty, // Will be updated when inspector starts inspection
                InspectionDate = DateTime.UtcNow,
                OverallResult = "Pending", // Will be set to Pass/Fail when inspection is completed
                Score = 0,
                CheckResults = "[]", // Empty results initially
                TenantId = _currentUserService?.TenantId ?? Guid.Empty
            };

            _context.WorkOrderQualityChecks.Add(qualityCheck);

            // Update work order status to indicate QC pending
            workOrder.Status = "PendingQC";
            workOrder.UpdatedAt = DateTime.UtcNow;
            workOrder.UpdatedBy = _currentUserService?.UserId ?? "System";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Created quality check {QualityCheckId} for work order {WorkOrderId} using checklist {ChecklistId}",
                qualityCheck.Id, workOrderId, checklist.Id);

            // Return the created quality check
            var dto = new WorkOrderQualityCheckDto
            {
                Id = qualityCheck.Id.ToString(),
                WorkOrderId = qualityCheck.WorkOrderId.ToString(),
                WorkOrderNumber = workOrder.WorkOrderNumber,
                AssetName = workOrder.Asset?.Name ?? "Unknown",
                ChecklistName = checklist.Name,
                Status = qualityCheck.OverallResult, // Using OverallResult as status
                InspectionDate = qualityCheck.InspectionDate,
                OverallResult = qualityCheck.OverallResult,
                Score = qualityCheck.Score
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting work order {WorkOrderId} for inspection", workOrderId);
            return StatusCode(500, "An error occurred while submitting for inspection");
        }
    }

    /// <summary>
    /// Gets pending quality inspections
    /// </summary>
    [HttpGet("pending-inspections")]
    public async Task<ActionResult<List<WorkOrderQualityCheckDto>>> GetPendingInspections()
    {
        try
        {
            var tenantId = _currentUserService.TenantId.GetValueOrDefault(Guid.Empty);

            // Note: WorkOrderQualityCheck entity does not have navigation property for WorkOrder
            // We need to join manually or load work orders separately
            var pendingInspections = await _context.WorkOrderQualityChecks
                .Include(qc => qc.Checklist)
                .Where(qc => qc.TenantId == tenantId && qc.OverallResult == "Pending")
                .OrderBy(qc => qc.InspectionDate)
                .ToListAsync();

            // Load work orders separately
            var workOrderIds = pendingInspections.Select(qc => qc.WorkOrderId).Distinct().ToList();
            var workOrders = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Where(wo => workOrderIds.Contains(wo.Id))
                .ToDictionaryAsync(wo => wo.Id);

            var dtos = pendingInspections.Select(qc =>
            {
                workOrders.TryGetValue(qc.WorkOrderId, out var workOrder);
                return new WorkOrderQualityCheckDto
                {
                    Id = qc.Id.ToString(),
                    WorkOrderId = qc.WorkOrderId.ToString(),
                    WorkOrderNumber = workOrder?.WorkOrderNumber ?? "Unknown",
                    AssetName = workOrder?.Asset?.Name ?? "Unknown",
                    ChecklistName = qc.Checklist?.Name ?? "Unknown",
                    Status = qc.OverallResult,
                    InspectionDate = qc.InspectionDate,
                    OverallResult = qc.OverallResult,
                    Score = qc.Score,
                    InspectorName = null // Would need to join with Employee table
                };
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending inspections");
            return StatusCode(500, "An error occurred while retrieving pending inspections");
        }
    }

    /// <summary>
    /// Gets inspection details by work order ID including work order and checklist info
    /// </summary>
    [HttpGet("inspection-by-workorder/{workOrderId}")]
    public async Task<ActionResult<object>> GetInspectionByWorkOrderId(string workOrderId)
    {
        try
        {
            _logger.LogInformation("Getting inspection for work order {WorkOrderId}", workOrderId);

            if (!Guid.TryParse(workOrderId, out var woId))
            {
                return BadRequest("Invalid work order ID format");
            }

            var tenantId = _currentUserService.TenantId;

            // Prefer the active inspection. If only historical inspections exist, return the newest one.
            var qualityCheck = await _context.WorkOrderQualityChecks
                .Include(qc => qc.Checklist)
                .Where(qc => qc.TenantId == tenantId && qc.WorkOrderId == woId)
                .OrderBy(qc => qc.OverallResult == "InProgress" ? 0 :
                    qc.OverallResult == "Pending" ? 1 : 2)
                .ThenByDescending(qc => qc.InspectionDate)
                .FirstOrDefaultAsync();

            if (qualityCheck == null)
            {
                return NotFound($"No inspection found for work order {workOrderId}");
            }

            // Load work order with asset
            var workOrder = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.WorkOrderType)
                .Include(wo => wo.MaintenanceType)
                .Include(wo => wo.PriorityLevel)
                .FirstOrDefaultAsync(wo => wo.Id == woId);

            if (workOrder == null)
            {
                return NotFound($"Work order {workOrderId} not found");
            }

            var checklistItems = ParseJsonArray(qualityCheck.Checklist?.ChecklistItems);

            var checkResults = ParseJsonArray(qualityCheck.CheckResults);

            var response = new
            {
                inspection = new
                {
                    id = qualityCheck.Id.ToString(),
                    workOrderId = qualityCheck.WorkOrderId.ToString(),
                    checklistId = qualityCheck.ChecklistId.ToString(),
                    status = qualityCheck.OverallResult,
                    inspectionDate = qualityCheck.InspectionDate,
                    overallScore = qualityCheck.Score,
                    checkResults = checkResults,
                    notes = qualityCheck.Notes
                },
                workOrder = new
                {
                    id = workOrder.Id.ToString(),
                    workOrderNumber = workOrder.WorkOrderNumber,
                    title = workOrder.Title,
                    description = workOrder.Description,
                    assetId = workOrder.AssetId.ToString(),
                    assetName = workOrder.Asset?.Name ?? "Unknown",
                    assetLocation = workOrder.Asset?.Location,
                    workOrderType = workOrder.WorkOrderType?.Name,
                    maintenanceType = workOrder.MaintenanceType?.Name,
                    status = workOrder.Status,
                    priority = workOrder.PriorityLevel?.Name ?? "Medium",
                    completedDate = workOrder.ActualCompletionDate
                },
                checklist = new
                {
                    id = qualityCheck.Checklist?.Id.ToString(),
                    name = qualityCheck.Checklist?.Name ?? "Unknown",
                    workOrderType = qualityCheck.Checklist?.WorkOrderType,
                    assetCategory = qualityCheck.Checklist?.AssetCategory,
                    maintenanceType = qualityCheck.Checklist?.MaintenanceType,
                    isMandatory = qualityCheck.Checklist?.IsMandatory ?? false,
                    minimumPassingScore = qualityCheck.Checklist?.MinimumPassingScore ?? 0,
                    version = qualityCheck.Checklist?.Version ?? 1,
                    items = checklistItems
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspection for work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while retrieving the inspection");
        }
    }

    /// <summary>
    /// Updates a checklist item result during inspection
    /// </summary>
    [HttpPost("update-item-result")]
    public async Task<ActionResult> UpdateItemResult([FromBody] UpdateItemResultRequest request)
    {
        try
        {
            var tenantId = _currentUserService.TenantId.GetValueOrDefault(Guid.Empty);

            if (!Guid.TryParse(request.QualityCheckId, out var qcId))
            {
                return BadRequest("Invalid quality check ID format");
            }

            var qualityCheck = await _context.WorkOrderQualityChecks
                .FirstOrDefaultAsync(qc => qc.Id == qcId && qc.TenantId == tenantId);

            if (qualityCheck == null)
            {
                return NotFound($"Quality check {request.QualityCheckId} not found");
            }

            // Parse existing results
            var checkResults = new List<CheckItemResult>();
            if (!string.IsNullOrEmpty(qualityCheck.CheckResults) && qualityCheck.CheckResults != "[]")
            {
                checkResults = System.Text.Json.JsonSerializer.Deserialize<List<CheckItemResult>>(qualityCheck.CheckResults) ?? new List<CheckItemResult>();
            }

            // Update or add item result
            var existingResult = checkResults.FirstOrDefault(r => r.ItemId == request.ItemId);
            if (existingResult != null)
            {
                existingResult.Result = request.Result;
                existingResult.Notes = request.Notes;
                existingResult.InspectedAt = DateTime.UtcNow;
            }
            else
            {
                checkResults.Add(new CheckItemResult
                {
                    ItemId = request.ItemId,
                    Result = request.Result,
                    Notes = request.Notes,
                    InspectedAt = DateTime.UtcNow
                });
            }

            // Save updated results
            qualityCheck.CheckResults = System.Text.Json.JsonSerializer.Serialize(checkResults);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Item result updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item result");
            return StatusCode(500, "An error occurred while updating item result");
        }
    }

    /// <summary>
    /// Completes an inspection
    /// </summary>
    [HttpPost("complete-inspection")]
    public async Task<ActionResult> CompleteInspection([FromBody] CompleteInspectionRequest request)
    {
        try
        {
            var tenantId = _currentUserService.TenantId.GetValueOrDefault(Guid.Empty);

            if (!Guid.TryParse(request.QualityCheckId, out var qcId))
            {
                return BadRequest("Invalid quality check ID format");
            }

            var qualityCheck = await _context.WorkOrderQualityChecks
                .Include(qc => qc.Checklist)
                .FirstOrDefaultAsync(qc => qc.Id == qcId && qc.TenantId == tenantId);

            if (qualityCheck == null)
            {
                return NotFound($"Quality check {request.QualityCheckId} not found");
            }

            // Calculate score
            var checkResults = System.Text.Json.JsonSerializer.Deserialize<List<CheckItemResult>>(qualityCheck.CheckResults) ?? new List<CheckItemResult>();
            var passedCount = checkResults.Count(r => r.Result == "Pass");
            var totalCount = checkResults.Count;
            var score = totalCount > 0 ? (passedCount * 100 / totalCount) : 0;

            // Determine overall result
            var minimumScore = qualityCheck.Checklist?.MinimumPassingScore ?? 80;
            var overallResult = score >= minimumScore ? "Pass" : "Fail";

            // Update quality check
            qualityCheck.OverallResult = overallResult;
            qualityCheck.Score = score;
            qualityCheck.Notes = request.Notes;
            qualityCheck.InspectionDate = DateTime.UtcNow;

            // NOTE:
            // We no longer complete the work order directly here.
            // The centralized completion flow in WorkOrdersController + WorkOrderService
            // (which calls IQualityControlService.ValidateWorkOrderCompletionAsync)
            // is responsible for setting the final work order status and completion dates.

            await _context.SaveChangesAsync();

            return Ok(new
            {
                overallResult,
                score,
                message = "Inspection completed successfully. Use the standard work order completion flow to complete the work order."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing inspection");
            return StatusCode(500, "An error occurred while completing inspection");
        }
    }

    /// <summary>
    /// Rejects work order and creates rework
    /// </summary>
    [HttpPost("reject-for-rework")]
    public async Task<ActionResult> RejectForRework([FromBody] RejectForReworkRequest request)
    {
        try
        {
            var tenantId = _currentUserService.TenantId.GetValueOrDefault(Guid.Empty);
            var inspectorId = _currentUserService.EmployeeId.GetValueOrDefault(Guid.Empty);

            if (!Guid.TryParse(request.QualityCheckId, out var qcId))
            {
                return BadRequest("Invalid quality check ID format");
            }

            var qualityCheck = await _context.WorkOrderQualityChecks
                .FirstOrDefaultAsync(qc => qc.Id == qcId && qc.TenantId == tenantId);

            if (qualityCheck == null)
            {
                return NotFound($"Quality check {request.QualityCheckId} not found");
            }

            // Update quality check result to Fail
            qualityCheck.OverallResult = "Fail";
            qualityCheck.Notes = request.RejectionReason;
            qualityCheck.CorrectiveActions = request.CorrectiveActions;
            qualityCheck.RequiresFollowUp = true;

            // Create rework record
            var rework = new ErpSystem.Core.Entities.Maintenance.WorkOrderRework
            {
                WorkOrderId = qualityCheck.WorkOrderId,
                InspectorId = inspectorId,
                ReworkReason = "QualityIssue",
                Description = request.RejectionReason,
                Severity = request.Severity,
                Status = "Pending",
                IdentifiedDate = DateTime.UtcNow,
                Notes = request.CorrectiveActions,
                TenantId = tenantId
            };

            _context.Add(rework);

            // Update work order status
            var workOrder = await _context.WorkOrders.FindAsync(qualityCheck.WorkOrderId);
            if (workOrder != null)
            {
                workOrder.Status = "Rework";
                workOrder.UpdatedAt = DateTime.UtcNow;
                workOrder.UpdatedBy = _currentUserService?.UserId ?? "System";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                reworkId = rework.Id,
                message = "Work order rejected for rework"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting for rework");
            return StatusCode(500, "An error occurred while rejecting for rework");
        }
    }

    /// <summary>
    /// Generates PDF certificate for completed inspection
    /// </summary>
    [HttpGet("certificate/{qualityCheckId}")]
    public async Task<ActionResult> GenerateCertificate(string qualityCheckId)
    {
        try
        {
            if (!Guid.TryParse(qualityCheckId, out var qcId))
            {
                return BadRequest("Invalid quality check ID format");
            }

            var pdfBytes = await _certificateService.GenerateCertificateAsync(qcId);

            return File(pdfBytes, "application/pdf", $"QC-Certificate-{qualityCheckId}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating certificate for quality check {QualityCheckId}", qualityCheckId);
            return StatusCode(500, "An error occurred while generating the certificate");
        }
    }

    /// <summary>
    /// Gets completed inspections with Pass/Fail results
    /// </summary>
    [HttpGet("completed-inspections")]
    public async Task<ActionResult<List<CompletedInspectionDto>>> GetCompletedInspections()
    {
        try
        {
            var tenantId = _currentUserService.TenantId.GetValueOrDefault(Guid.Empty);

            var qualityChecks = await _context.WorkOrderQualityChecks
                .Include(qc => qc.Checklist)
                .Where(qc => qc.TenantId == tenantId &&
                    (qc.OverallResult == "Pass" || qc.OverallResult == "Fail"))
                .OrderByDescending(qc => qc.InspectionDate)
                .Take(50) // Limit to last 50 inspections
                .ToListAsync();

            var completedInspections = new List<CompletedInspectionDto>();
            foreach (var qc in qualityChecks)
            {
                var workOrder = await _context.WorkOrders
                    .Include(wo => wo.Asset)
                    .Include(wo => wo.JobCard)
                    .FirstOrDefaultAsync(wo => wo.Id == qc.WorkOrderId);

                // Get inspector name
                var inspector = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Id == qc.InspectorId);

                completedInspections.Add(new CompletedInspectionDto
                {
                    Id = qc.Id.ToString(),
                    WorkOrderId = qc.WorkOrderId.ToString(),
                    WorkOrderNumber = workOrder?.WorkOrderNumber ?? "Unknown",
                    JobCardNumber = workOrder?.JobCard?.JobCardNumber ?? "N/A",
                    AssetName = workOrder?.Asset?.Name ?? "Unknown",
                    ChecklistName = qc.Checklist?.Name ?? "Unknown",
                    InspectionDate = qc.InspectionDate,
                    OverallResult = qc.OverallResult,
                    Score = qc.Score,
                    Notes = qc.Notes,
                    InspectorName = inspector != null ? $"{inspector.FirstName} {inspector.LastName}" : "Unknown"
                });
            }

            return Ok(completedInspections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completed inspections");
            return StatusCode(500, "An error occurred while getting completed inspections");
        }
    }

    /// <summary>
    /// Starts an inspection by changing status from Pending to InProgress
    /// </summary>
    [HttpPost("start-inspection/{workOrderId}")]
    public async Task<ActionResult<WorkOrderQualityCheckDto>> StartInspection(string workOrderId)
    {
        try
        {
            _logger.LogInformation("Starting inspection for work order {WorkOrderId}", workOrderId);

            if (!Guid.TryParse(workOrderId, out var woId))
            {
                return BadRequest("Invalid work order ID format");
            }

            var tenantId = _currentUserService.TenantId;

            // Find the newest pending QC inspection for this work order.
            var qualityCheck = await _context.WorkOrderQualityChecks
                .Include(qc => qc.Checklist)
                .Where(qc => qc.TenantId == tenantId && qc.WorkOrderId == woId && qc.OverallResult == "Pending")
                .OrderByDescending(qc => qc.InspectionDate)
                .FirstOrDefaultAsync();

            if (qualityCheck == null)
            {
                return NotFound($"No pending QC inspection found for work order {workOrderId}");
            }

            var checklistItemCount = CountChecklistItems(qualityCheck.Checklist?.ChecklistItems);
            if (checklistItemCount == 0)
            {
                return BadRequest($"Quality checklist '{qualityCheck.Checklist?.Name ?? "Unknown"}' has no checklist items. Add checklist items before starting this inspection.");
            }

            // Change status to InProgress and assign inspector
            qualityCheck.OverallResult = "InProgress";
            qualityCheck.InspectionDate = DateTime.UtcNow; // Update to actual start time

            // Assign current user as inspector if they have an EmployeeId
            var currentEmployeeId = _currentUserService?.EmployeeId;
            if (currentEmployeeId.HasValue && currentEmployeeId.Value != Guid.Empty)
            {
                qualityCheck.InspectorId = currentEmployeeId.Value;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Started inspection {QualityCheckId} for work order {WorkOrderId}", qualityCheck.Id, workOrderId);

            // Load work order for response
            var workOrder = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .FirstOrDefaultAsync(wo => wo.Id == woId);

            var dto = new WorkOrderQualityCheckDto
            {
                Id = qualityCheck.Id.ToString(),
                WorkOrderId = qualityCheck.WorkOrderId.ToString(),
                WorkOrderNumber = workOrder?.WorkOrderNumber ?? "Unknown",
                AssetName = workOrder?.Asset?.Name ?? "Unknown",
                ChecklistName = qualityCheck.Checklist?.Name ?? "Unknown",
                Status = qualityCheck.OverallResult,
                InspectionDate = qualityCheck.InspectionDate,
                OverallResult = qualityCheck.OverallResult,
                Score = qualityCheck.Score
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting inspection for work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while starting the inspection");
        }
    }

    private static int CountChecklistItems(string? checklistItemsJson)
    {
        return ParseJsonArray(checklistItemsJson).Count;
    }

    private async Task<ErpSystem.Core.Entities.Maintenance.QualityControlChecklist?> FindBestChecklistTemplateAsync(
        Guid tenantId,
        Guid maintenanceTypeId,
        string? maintenanceTypeName,
        Guid workOrderTypeId,
        string? workOrderTypeName,
        Guid? assetCategoryId,
        string? assetCategoryName)
    {
        var candidates = await _context.QualityControlChecklists
            .Where(c => c.IsActive && c.TenantId == tenantId)
            .ToListAsync();

        return candidates
            .Select(checklist => new
            {
                Checklist = checklist,
                Score = CalculateChecklistMatchScore(
                    checklist,
                    maintenanceTypeId,
                    maintenanceTypeName,
                    workOrderTypeId,
                    workOrderTypeName,
                    assetCategoryId,
                    assetCategoryName)
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Checklist.IsMandatory)
            .ThenByDescending(match => CountChecklistItems(match.Checklist.ChecklistItems))
            .ThenByDescending(match => match.Checklist.LastModifiedDate ?? match.Checklist.CreatedDate)
            .Select(match => match.Checklist)
            .FirstOrDefault();
    }

    private static int CalculateChecklistMatchScore(
        ErpSystem.Core.Entities.Maintenance.QualityControlChecklist checklist,
        Guid maintenanceTypeId,
        string? maintenanceTypeName,
        Guid workOrderTypeId,
        string? workOrderTypeName,
        Guid? assetCategoryId,
        string? assetCategoryName)
    {
        var hasMaintenanceScope = HasScope(checklist.MaintenanceTypeId, checklist.MaintenanceType);
        var hasWorkOrderScope = HasScope(checklist.WorkOrderTypeId, checklist.WorkOrderType);
        var hasAssetCategoryScope = HasScope(checklist.AssetCategoryId, checklist.AssetCategory);

        var maintenanceMatches = ScopeMatches(checklist.MaintenanceTypeId, checklist.MaintenanceType, maintenanceTypeId, maintenanceTypeName);
        var workOrderMatches = ScopeMatches(checklist.WorkOrderTypeId, checklist.WorkOrderType, workOrderTypeId, workOrderTypeName);
        var assetCategoryMatches = ScopeMatches(checklist.AssetCategoryId, checklist.AssetCategory, assetCategoryId, assetCategoryName);

        // Priority is exclusive by configured scope:
        // 1. Maintenance Type
        // 2. Work Order Type, only when Maintenance Type is not configured on the template
        // 3. Asset Category, only when Maintenance Type and Work Order Type are not configured on the template
        if (hasMaintenanceScope)
        {
            if (!maintenanceMatches)
            {
                return 0;
            }

            return 3000 + (workOrderMatches ? 40 : 0) + (assetCategoryMatches ? 30 : 0);
        }

        if (hasWorkOrderScope)
        {
            if (!workOrderMatches)
            {
                return 0;
            }

            return 2000 + (assetCategoryMatches ? 30 : 0);
        }

        if (hasAssetCategoryScope)
        {
            return assetCategoryMatches ? 1000 : 0;
        }

        return 0;
    }

    private static bool HasScope(Guid? templateId, string? templateName)
    {
        return (templateId.HasValue && templateId.Value != Guid.Empty)
            || !string.IsNullOrWhiteSpace(NormalizeScopeText(templateName));
    }

    private static bool ScopeMatches(
        Guid? templateId,
        string? templateName,
        Guid? actualId,
        string? actualName)
    {
        var templateScopeId = templateId.GetValueOrDefault();
        var templateHasId = templateScopeId != Guid.Empty;
        var normalizedTemplateName = NormalizeScopeText(templateName);

        if (!templateHasId && string.IsNullOrWhiteSpace(normalizedTemplateName))
        {
            return false;
        }

        var idMatches = templateHasId
            && actualId is Guid actualScopeId
            && actualScopeId != Guid.Empty
            && templateScopeId == actualScopeId;
        var nameMatches = !string.IsNullOrWhiteSpace(normalizedTemplateName) &&
            string.Equals(normalizedTemplateName, NormalizeScopeText(actualName), StringComparison.OrdinalIgnoreCase);

        return idMatches || nameMatches;
    }

    private static string? NormalizeScopeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ||
            string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase)
                ? null
                : trimmed;
    }

    private static List<object> ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return new List<object>();
        }

        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(json);
            return items?.Cast<object>().ToList() ?? new List<object>();
        }
        catch
        {
            return new List<object>();
        }
    }
}

public class WorkOrderQualityCheckDto
{
    public string Id { get; set; } = string.Empty;
    public string WorkOrderId { get; set; } = string.Empty;
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string ChecklistName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public string? OverallResult { get; set; }
    public decimal Score { get; set; }
    public string? InspectorName { get; set; }
}

public class CompletedInspectionDto
{
    public string Id { get; set; } = string.Empty;
    public string WorkOrderId { get; set; } = string.Empty;
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string JobCardNumber { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string ChecklistName { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public string OverallResult { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string? Notes { get; set; }
    public string? InspectorName { get; set; }
}

public class QualityValidationResult
{
    public string WorkOrderId { get; set; } = string.Empty;
    public bool CanComplete { get; set; }
    public string ValidationDate { get; set; } = string.Empty;
    public List<RequiredInspection> RequiredInspections { get; set; } = new();
    public List<string> ValidationMessages { get; set; } = new();
    public List<string> ValidationFailures { get; set; } = new();
    public bool RequiresInspectionOfficerApproval { get; set; }
    public double? QualityScore { get; set; }
    public List<string>? RecommendedActions { get; set; }
}

public class RequiredInspection
{
    public string InspectionTemplateId { get; set; } = string.Empty;
    public string InspectionType { get; set; } = string.Empty;
    public string InspectionName { get; set; } = string.Empty;
    public bool IsRegulatory { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool? IsCompleted { get; set; }
    public string? CompletedDate { get; set; }
    public string? Result { get; set; }
}

public class UpdateItemResultRequest
{
    public string QualityCheckId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty; // Pass, Fail, N/A
    public string? Notes { get; set; }
}

public class CompleteInspectionRequest
{
    public string QualityCheckId { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class RejectForReworkRequest
{
    public string QualityCheckId { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
    public string CorrectiveActions { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
}

public class CheckItemResult
{
    public string ItemId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty; // Pass, Fail, N/A
    public string? Notes { get; set; }
    public DateTime InspectedAt { get; set; }
}
