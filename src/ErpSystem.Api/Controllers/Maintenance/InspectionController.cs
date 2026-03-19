using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/inspections")]
[Authorize]
public class InspectionController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<InspectionController> _logger;

    public InspectionController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<InspectionController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Gets active inspections for quality control dashboard
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<object[]>> GetActiveInspections([FromQuery] string? inspectorId = null)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Getting active inspections for tenant {TenantId}", tenantId);

            // Query WorkOrderQualityChecks with status "InProgress"
            IQueryable<ErpSystem.Core.Entities.Maintenance.WorkOrderQualityCheck> query = _context.WorkOrderQualityChecks
                .Where(qc => qc.TenantId == tenantId && qc.OverallResult == "InProgress")
                .Include(qc => qc.Checklist);

            // Filter by inspector if specified
            if (!string.IsNullOrEmpty(inspectorId) && Guid.TryParse(inspectorId, out var inspectorGuid))
            {
                query = query.Where(qc => qc.InspectorId == inspectorGuid);
            }

            var qualityChecks = await query.ToListAsync();

            // Load work orders separately
            var workOrderIds = qualityChecks.Select(qc => qc.WorkOrderId).Distinct().ToList();
            var workOrders = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Where(wo => workOrderIds.Contains(wo.Id))
                .ToDictionaryAsync(wo => wo.Id);

            // Map to response format
            var activeInspections = qualityChecks.Select(qc =>
            {
                workOrders.TryGetValue(qc.WorkOrderId, out var workOrder);

                // Parse CheckResults JSON to calculate progress
                List<System.Text.Json.JsonElement>? checkResults = null;
                try
                {
                    checkResults = string.IsNullOrEmpty(qc.CheckResults) || qc.CheckResults == "[]"
                        ? new List<System.Text.Json.JsonElement>()
                        : System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(qc.CheckResults);
                }
                catch
                {
                    checkResults = new List<System.Text.Json.JsonElement>();
                }

                List<System.Text.Json.JsonElement>? checklistItems = null;
                try
                {
                    checklistItems = string.IsNullOrEmpty(qc.Checklist?.ChecklistItems) || qc.Checklist.ChecklistItems == "[]"
                        ? new List<System.Text.Json.JsonElement>()
                        : System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(qc.Checklist.ChecklistItems ?? "[]");
                }
                catch
                {
                    checklistItems = new List<System.Text.Json.JsonElement>();
                }

                var completedItems = checkResults?.Count ?? 0;
                var totalItems = checklistItems?.Count ?? 0;
                var progress = totalItems > 0 ? (completedItems * 100 / totalItems) : 0;

                return new
                {
                    id = qc.Id.ToString(),
                    workOrderId = qc.WorkOrderId.ToString(),
                    workOrderNumber = workOrder?.WorkOrderNumber ?? "Unknown",
                    workOrderTitle = workOrder?.Title ?? "Unknown Work Order",
                    assetId = workOrder?.AssetId.ToString() ?? "",
                    assetName = workOrder?.Asset?.Name ?? "Unknown",
                    assetLocation = workOrder?.Asset?.Location ?? "",
                    checklistId = qc.ChecklistId.ToString(),
                    checklist = new
                    {
                        id = qc.ChecklistId.ToString(),
                        name = qc.Checklist?.Name ?? "Unknown",
                        items = checklistItems
                    },
                    inspectorId = qc.InspectorId.ToString(),
                    inspectorName = "Inspector", // TODO: Load from Employee table
                    status = "In Progress",
                    scheduledDate = qc.InspectionDate,
                    startedDate = qc.InspectionDate,
                    progress = progress,
                    overallScore = qc.Score,
                    itemResponses = checkResults,
                    generalNotes = qc.Notes,
                    createdAt = qc.InspectionDate,
                    updatedAt = qc.InspectionDate
                };
            }).ToArray();

            _logger.LogInformation("Found {Count} active inspections for tenant {TenantId}", activeInspections.Length, tenantId);
            return Ok(activeInspections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active inspections");
            return StatusCode(500, "An error occurred while retrieving active inspections");
        }
    }

    /// <summary>
    /// Gets a specific inspection by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<object> GetInspectionById(string id)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Getting inspection {InspectionId} for tenant {TenantId}", id, tenantId);

            // Return mock inspection data
            var inspection = new
            {
                id = id,
                title = "Monthly Safety Inspection - HVAC Unit 1",
                description = "Comprehensive safety and performance inspection of HVAC Unit 1",
                assetId = "asset-1",
                assetName = "HVAC Unit 1",
                assetLocation = "Building A - Floor 3",
                inspectorId = "inspector-1",
                inspectorName = "John Smith",
                checklistId = "checklist-1",
                checklistName = "HVAC Safety Inspection",
                workOrderId = "wo-001",
                status = "In Progress",
                priority = "High",
                scheduledDate = DateTime.Today.AddDays(-1),
                startedDate = DateTime.Today.AddDays(-1).AddHours(8),
                dueDate = DateTime.Today.AddDays(2),
                estimatedDuration = 120,
                actualDuration = 78,
                progress = 65,
                overallResult = (string?)null,
                overallScore = (int?)null,
                minimumPassingScore = 80,
                itemResponses = new object[]
                {
                    new
                    {
                        itemId = "item-1",
                        itemText = "Air filter condition check",
                        result = "Pass",
                        score = (int?)null,
                        notes = "Filter is clean and properly installed",
                        timestamp = DateTime.Now.AddHours(-3)
                    },
                    new
                    {
                        itemId = "item-2",
                        itemText = "Thermostat calibration",
                        result = (string?)null,
                        score = 8,
                        notes = "Temperature reading within acceptable range",
                        timestamp = DateTime.Now.AddHours(-2)
                    }
                },
                generalNotes = "Inspection proceeding as scheduled. No major issues identified so far.",
                createdAt = DateTime.Today.AddDays(-1),
                updatedAt = DateTime.Now.AddMinutes(-30),
                workflowStatus = "In Progress",
                signatures = Array.Empty<object>()
            };

            return Ok(inspection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspection {InspectionId}", id);
            return StatusCode(500, "An error occurred while retrieving the inspection");
        }
    }

    /// <summary>
    /// Gets work orders awaiting inspection for quality control
    /// </summary>
    [HttpGet("work-orders-for-inspection")]
    public ActionResult<object[]> GetWorkOrdersForInspection()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Getting work orders for inspection for tenant {TenantId}", tenantId);

            // Return mock work orders awaiting inspection
            var workOrders = new[]
            {
                new
                {
                    id = "wo-004",
                    workOrderNumber = "WO-2024-004",
                    title = "Preventive Maintenance - Generator 2",
                    description = "Monthly preventive maintenance on backup generator",
                    assetId = "asset-4",
                    assetName = "Backup Generator 2",
                    assetLocation = "Building D - Roof",
                    status = "Completed",
                    completedDate = DateTime.Today.AddDays(-1),
                    technicianId = "tech-1",
                    technicianName = "Mike Wilson",
                    workType = "Preventive",
                    priority = "Medium",
                    estimatedHours = 4,
                    actualHours = 3.5,
                    requiresInspection = true,
                    inspectionDueDate = DateTime.Today.AddDays(2),
                    createdAt = DateTime.Today.AddDays(-5),
                    completedAt = DateTime.Today.AddDays(-1).AddHours(16)
                },
                new
                {
                    id = "wo-005",
                    workOrderNumber = "WO-2024-005",
                    title = "Repair - Fire Suppression System",
                    description = "Replace faulty pressure sensor in fire suppression system",
                    assetId = "asset-5",
                    assetName = "Fire Suppression System",
                    assetLocation = "Building A - Mechanical Room",
                    status = "Completed",
                    completedDate = DateTime.Today.AddHours(-4),
                    technicianId = "tech-2",
                    technicianName = "Lisa Chen",
                    workType = "Corrective",
                    priority = "High",
                    estimatedHours = 2,
                    actualHours = 2.5,
                    requiresInspection = true,
                    inspectionDueDate = DateTime.Today.AddDays(1),
                    createdAt = DateTime.Today.AddDays(-3),
                    completedAt = DateTime.Today.AddHours(-4)
                },
                new
                {
                    id = "wo-006",
                    workOrderNumber = "WO-2024-006",
                    title = "Calibration - Temperature Sensors",
                    description = "Annual calibration of temperature monitoring sensors",
                    assetId = "asset-6",
                    assetName = "Temperature Monitoring System",
                    assetLocation = "Building B - Server Room",
                    status = "Completed",
                    completedDate = DateTime.Today.AddDays(-2),
                    technicianId = "tech-3",
                    technicianName = "David Rodriguez",
                    workType = "Calibration",
                    priority = "Medium",
                    estimatedHours = 3,
                    actualHours = 3.2,
                    requiresInspection = true,
                    inspectionDueDate = DateTime.Today.AddDays(3),
                    createdAt = DateTime.Today.AddDays(-7),
                    completedAt = DateTime.Today.AddDays(-2).AddHours(14)
                }
            };

            return Ok(workOrders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work orders for inspection");
            return StatusCode(500, "An error occurred while retrieving work orders for inspection");
        }
    }

    /// <summary>
    /// Updates inspection progress
    /// </summary>
    [HttpPut("{id}/progress")]
    public ActionResult<object> UpdateInspectionProgress(string id, [FromBody] object itemResponses)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Updating progress for inspection {InspectionId} for tenant {TenantId}", id, tenantId);

            // Return updated inspection mock data
            var updatedInspection = new
            {
                id = id,
                title = "Monthly Safety Inspection - HVAC Unit 1",
                status = "In Progress",
                progress = 75, // Updated progress
                updatedAt = DateTime.Now,
                message = "Inspection progress updated successfully"
            };

            return Ok(updatedInspection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inspection progress {InspectionId}", id);
            return StatusCode(500, "An error occurred while updating inspection progress");
        }
    }

    /// <summary>
    /// Completes an inspection
    /// </summary>
    [HttpPost("complete")]
    public ActionResult<object> CompleteInspection([FromBody] object request)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Completing inspection for tenant {TenantId}", tenantId);

            // Return completed inspection mock data
            var completedInspection = new
            {
                id = "inspection-1",
                title = "Monthly Safety Inspection - HVAC Unit 1",
                status = "Completed",
                completedDate = DateTime.Now,
                overallResult = "Pass",
                overallScore = 85,
                workflowStatus = "Approved",
                progress = 100,
                updatedAt = DateTime.Now,
                message = "Inspection completed successfully"
            };

            return Ok(completedInspection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing inspection");
            return StatusCode(500, "An error occurred while completing the inspection");
        }
    }

    /// <summary>
    /// Gets inspections by work order ID
    /// </summary>
    [HttpGet("work-order/{workOrderId}")]
    public ActionResult<object[]> GetInspectionsByWorkOrder(string workOrderId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Getting inspections for work order {WorkOrderId} for tenant {TenantId}", workOrderId, tenantId);

            // Return mock inspections for the work order
            var inspections = new[]
            {
                new
                {
                    id = "inspection-4",
                    title = "Post-Maintenance Quality Inspection",
                    workOrderId = workOrderId,
                    status = "Completed",
                    overallResult = "Pass",
                    overallScore = 92,
                    completedDate = DateTime.Today.AddHours(-2),
                    inspectorName = "John Smith"
                }
            };

            return Ok(inspections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspections for work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "An error occurred while retrieving inspections for the work order");
        }
    }

    /// <summary>
    /// Gets inspection history for an asset
    /// </summary>
    [HttpGet("history/{assetId}")]
    public ActionResult<object[]> GetInspectionHistory(string assetId, [FromQuery] int limit = 10)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Getting inspection history for asset {AssetId} for tenant {TenantId}", assetId, tenantId);

            // Return mock inspection history
            var history = new[]
            {
                new
                {
                    id = "inspection-hist-1",
                    title = "Monthly Safety Inspection",
                    assetId = assetId,
                    status = "Completed",
                    overallResult = "Pass",
                    overallScore = 88,
                    completedDate = DateTime.Today.AddDays(-30),
                    inspectorName = "John Smith"
                },
                new
                {
                    id = "inspection-hist-2",
                    title = "Quarterly Performance Review",
                    assetId = assetId,
                    status = "Completed",
                    overallResult = "Pass",
                    overallScore = 91,
                    completedDate = DateTime.Today.AddDays(-90),
                    inspectorName = "Sarah Johnson"
                }
            }.Take(limit).ToArray();

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspection history for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while retrieving inspection history");
        }
    }

    /// <summary>
    /// Gets regulatory compliance status for an asset
    /// </summary>
    [HttpGet("regulatory-compliance/{assetId}")]
    public ActionResult<object[]> GetRegulatoryComplianceStatus(string assetId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Getting regulatory compliance status for asset {AssetId} for tenant {TenantId}", assetId, tenantId);

            // Return mock regulatory compliance data
            var compliance = new object[]
            {
                new
                {
                    id = "compliance-1",
                    assetId = assetId,
                    requirementId = "req-1",
                    requirementName = "Annual Safety Inspection",
                    status = "Compliant",
                    lastComplianceDate = DateTime.Today.AddDays(-45),
                    nextDueDate = DateTime.Today.AddDays(320),
                    certificateNumber = "CERT-2024-001",
                    certificateValidUntil = DateTime.Today.AddDays(320),
                    riskLevel = "Low"
                },
                new
                {
                    id = "compliance-2",
                    assetId = assetId,
                    requirementId = "req-2",
                    requirementName = "Monthly Performance Check",
                    status = "At_Risk",
                    lastComplianceDate = DateTime.Today.AddDays(-35),
                    nextDueDate = DateTime.Today.AddDays(-5),
                    certificateNumber = (string?)null,
                    certificateValidUntil = (DateTime?)null,
                    riskLevel = "Medium"
                }
            };

            return Ok(compliance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving regulatory compliance status for asset {AssetId}", assetId);
            return StatusCode(500, "An error occurred while retrieving regulatory compliance status");
        }
    }

    /// <summary>
    /// Generates an inspection report
    /// </summary>
    [HttpGet("{id}/report")]
    public ActionResult<object> GenerateInspectionReport(string id)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Generating report for inspection {InspectionId} for tenant {TenantId}", id, tenantId);

            return BadRequest("Report generation not available in mock mode");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report for inspection {InspectionId}", id);
            return StatusCode(500, "An error occurred while generating the inspection report");
        }
    }
}
