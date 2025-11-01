using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/quality-control")]
// [Authorize] // Temporarily disabled for testing
public class QualityControlController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<QualityControlController> _logger;
    private readonly ApplicationDbContext _context;

    public QualityControlController(
        ICurrentUserService currentUserService,
        ILogger<QualityControlController> logger,
        ApplicationDbContext context)
    {
        _currentUserService = currentUserService;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Validates if a work order can be completed based on quality control requirements
    /// </summary>
    [HttpGet("validate-work-order/{workOrderId}")]
    public async Task<ActionResult<QualityValidationResult>> ValidateWorkOrderCompletion(string workOrderId)
    {
        try
        {
            var tenantId = _currentUserService?.TenantId;
            _logger.LogInformation("Validating work order {WorkOrderId} for completion", workOrderId);

            // Parse the ID to Guid
            if (!Guid.TryParse(workOrderId, out var woId))
            {
                return BadRequest("Invalid work order ID format");
            }

            // Get work order from database
            var workOrder = await _context.WorkOrders
                .Include(wo => wo.Tasks)
                .Include(wo => wo.JobCard)
                .Include(wo => wo.Asset)
                    .ThenInclude(a => a.AssetCategory)
                .Include(wo => wo.WorkOrderType)
                .Include(wo => wo.MaintenanceType)
                .FirstOrDefaultAsync(wo => wo.Id == woId);

            if (workOrder == null)
            {
                return NotFound($"Work order with ID {workOrderId} not found");
            }

            var validationResult = new QualityValidationResult
            {
                WorkOrderId = workOrderId,
                CanComplete = true,
                ValidationDate = DateTime.UtcNow.ToString("O"),
                RequiredInspections = new List<RequiredInspection>(),
                ValidationMessages = new List<string>(),
                ValidationFailures = new List<string>(),
                RequiresInspectionOfficerApproval = false
            };

            // Check if all required tasks are completed
            var incompleteTasks = workOrder.Tasks?.Where(t => t.IsRequired && t.Status != "Completed").ToList();
            if (incompleteTasks?.Any() == true)
            {
                validationResult.CanComplete = false;
                validationResult.ValidationFailures.Add($"{incompleteTasks.Count} required task(s) not completed");
            }

            // Find applicable quality checklists based on asset category, work order type, and maintenance type
            var assetCategory = workOrder.Asset?.AssetCategory?.Name;
            var workOrderTypeName = workOrder.WorkOrderType?.Name;
            var maintenanceTypeName = workOrder.MaintenanceType?.Name;

            var applicableChecklists = await _context.QualityControlChecklists
                .Where(c => c.IsActive && 
                    (c.WorkOrderType == workOrderTypeName || string.IsNullOrEmpty(c.WorkOrderType)) &&
                    (c.AssetCategory == assetCategory || string.IsNullOrEmpty(c.AssetCategory)) &&
                    (c.MaintenanceType == maintenanceTypeName || string.IsNullOrEmpty(c.MaintenanceType)))
                .ToListAsync();

            var mandatoryChecklists = applicableChecklists.Where(c => c.IsMandatory).ToList();

            // Add required inspections to the result
            validationResult.RequiredInspections = mandatoryChecklists.Select(c => new RequiredInspection
            {
                InspectionTemplateId = c.Id.ToString(),
                InspectionType = c.WorkOrderType ?? "General",
                InspectionName = c.Name,
                IsRegulatory = c.IsMandatory,
                Description = c.Description ?? "",
                IsCompleted = false
            }).ToList();

            // Check if quality check is passed (if job card exists)
            if (workOrder.JobCard != null)
            {
                // Only block if quality check date is set but not passed
                if (workOrder.JobCard.QualityCheckDate.HasValue && !workOrder.JobCard.QualityCheckPassed)
                {
                    validationResult.CanComplete = false;
                    validationResult.ValidationFailures.Add("Quality check performed but not passed");
                    validationResult.RequiresInspectionOfficerApproval = true;
                }

                if (!workOrder.JobCard.CustomerAcceptance)
                {
                    validationResult.ValidationMessages.Add("Customer acceptance pending");
                }
            }

            // Get any quality checklists already performed for this work order
            var performedQualityChecks = await _context.WorkOrderQualityChecks
                .Include(qc => qc.Checklist)
                .Where(qc => qc.WorkOrderId == woId)
                .ToListAsync();

            // Check performed quality checks for failures
            foreach (var check in performedQualityChecks)
            {
                // Mark this checklist as completed in required inspections
                var requiredInspection = validationResult.RequiredInspections
                    .FirstOrDefault(ri => ri.InspectionTemplateId == check.ChecklistId.ToString());
                if (requiredInspection != null)
                {
                    requiredInspection.IsCompleted = true;
                    requiredInspection.CompletedDate = check.InspectionDate.ToString("O");
                    requiredInspection.Result = check.OverallResult;
                }

                if (check.OverallResult == "Fail")
                {
                    validationResult.CanComplete = false;
                    validationResult.ValidationFailures.Add($"Quality check '{check.Checklist?.Name ?? "Unknown"}' failed");
                    validationResult.RequiresInspectionOfficerApproval = true;
                }
                else if (check.Score < (check.Checklist?.MinimumPassingScore ?? 80))
                {
                    validationResult.CanComplete = false;
                    validationResult.ValidationFailures.Add($"Quality check '{check.Checklist?.Name ?? "Unknown"}' score below minimum");
                    validationResult.RequiresInspectionOfficerApproval = true;
                }
            }

            // Check if any mandatory checklists have not been performed
            var missingMandatoryChecklists = validationResult.RequiredInspections
                .Where(ri => ri.IsRegulatory && ri.IsCompleted != true)
                .ToList();

            if (missingMandatoryChecklists.Any())
            {
                validationResult.CanComplete = false;
                foreach (var missing in missingMandatoryChecklists)
                {
                    validationResult.ValidationFailures.Add($"Mandatory quality checklist '{missing.InspectionName}' not completed");
                }
                validationResult.RequiresInspectionOfficerApproval = true;
            }

            if (validationResult.CanComplete)
            {
                validationResult.ValidationMessages.Add("All quality control requirements met");
            }

            return Ok(validationResult);
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
