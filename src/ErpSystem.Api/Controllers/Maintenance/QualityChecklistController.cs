using System.Text.Json;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/quality-checklists")]
// [Authorize] // Temporarily disabled for testing
public class QualityChecklistController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<QualityChecklistController> _logger;
    private readonly ApplicationDbContext _context;

    public QualityChecklistController(
        ICurrentUserService currentUserService,
        ILogger<QualityChecklistController> logger,
        ApplicationDbContext context)
    {
        _currentUserService = currentUserService;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Gets all quality checklists
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<object[]>> GetAllChecklists(
        [FromQuery] string? workOrderType = null,
        [FromQuery] string? assetCategory = null,
        [FromQuery] string? maintenanceType = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var tenantId = _currentUserService?.TenantId;
            _logger.LogInformation("Getting all quality checklists for tenant {TenantId}", tenantId?.ToString() ?? "No Tenant (Auth Disabled)");

            // Get checklists from database
            var query = _context.QualityControlChecklists.AsQueryable();

            // Apply filters
            workOrderType = NormalizeScopeValue(workOrderType);
            assetCategory = NormalizeScopeValue(assetCategory);
            maintenanceType = NormalizeScopeValue(maintenanceType);

            if (!string.IsNullOrEmpty(workOrderType))
            {
                query = query.Where(c => c.WorkOrderType == workOrderType);
            }

            if (!string.IsNullOrEmpty(assetCategory))
            {
                query = query.Where(c => c.AssetCategory == assetCategory);
            }

            if (!string.IsNullOrEmpty(maintenanceType))
            {
                query = query.Where(c => c.MaintenanceType == maintenanceType);
            }

            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive.Value);
            }

            var checklistsData = await query.ToListAsync();

            var checklists = checklistsData.Select(c => new
            {
                id = c.Id.ToString(),
                name = c.Name,
                description = c.Description,
                workOrderType = c.WorkOrderType,
                workOrderTypeId = c.WorkOrderTypeId?.ToString(),
                assetCategory = c.AssetCategory,
                assetCategoryId = c.AssetCategoryId?.ToString(),
                maintenanceType = c.MaintenanceType,
                maintenanceTypeId = c.MaintenanceTypeId?.ToString(),
                isMandatory = c.IsMandatory,
                isActive = c.IsActive,
                minimumPassingScore = c.MinimumPassingScore,
                version = c.Version,
                createdDate = c.CreatedDate,
                createdBy = "System", // TODO: Get actual user name from CreatedById
                lastModifiedDate = c.LastModifiedDate,
                lastModifiedBy = "System", // TODO: Get actual user name from LastModifiedById
                items = ParseChecklistItems(c.ChecklistItems) // Parse JSON to items array
            }).ToArray();

            return Ok(checklists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quality checklists");
            return StatusCode(500, "An error occurred while retrieving quality checklists");
        }
    }

    /// <summary>
    /// Gets a specific quality checklist by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetChecklistById(string id)
    {
        try
        {
            var tenantId = _currentUserService?.TenantId;
            _logger.LogInformation("Getting quality checklist {ChecklistId} for tenant {TenantId}", id, tenantId?.ToString() ?? "No Tenant (Auth Disabled)");

            // Parse the ID to Guid
            if (!Guid.TryParse(id, out var checklistId))
            {
                return BadRequest("Invalid checklist ID format");
            }

            // Get checklist from database
            var checklistData = await _context.QualityControlChecklists
                .FirstOrDefaultAsync(c => c.Id == checklistId);

            if (checklistData == null)
            {
                return NotFound($"Quality checklist with ID {id} not found");
            }

            var checklist = new
            {
                id = checklistData.Id.ToString(),
                name = checklistData.Name,
                description = checklistData.Description,
                workOrderType = checklistData.WorkOrderType,
                workOrderTypeId = checklistData.WorkOrderTypeId?.ToString(),
                assetCategory = checklistData.AssetCategory,
                assetCategoryId = checklistData.AssetCategoryId?.ToString(),
                maintenanceType = checklistData.MaintenanceType,
                maintenanceTypeId = checklistData.MaintenanceTypeId?.ToString(),
                isMandatory = checklistData.IsMandatory,
                isActive = checklistData.IsActive,
                minimumPassingScore = checklistData.MinimumPassingScore,
                version = checklistData.Version,
                estimatedDuration = 60, // Default value for now
                items = ParseChecklistItems(checklistData.ChecklistItems),
                createdDate = checklistData.CreatedDate,
                createdBy = "System", // TODO: Get actual user name from CreatedById
                lastModifiedDate = checklistData.LastModifiedDate,
                lastModifiedBy = "System", // TODO: Get actual user name from LastModifiedById
                usageStatistics = new // Mock statistics for now
                {
                    totalUsages = 0,
                    averageScore = 0.0,
                    passRate = 0.0,
                    averageDuration = 0,
                    lastUsed = (DateTime?)null
                }
            };

            return Ok(checklist);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quality checklist {ChecklistId}", id);
            return StatusCode(500, "An error occurred while retrieving the quality checklist");
        }
    }

    /// <summary>
    /// Creates a new quality checklist
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> CreateChecklist([FromBody] CreateChecklistRequest request)
    {
        try
        {
            var tenantId = _currentUserService?.TenantId ?? Guid.Empty;
            _logger.LogInformation("Creating new quality checklist for tenant {TenantId}", tenantId.ToString());

            var checklistId = Guid.NewGuid();
            var currentUserId = Guid.Empty; // TODO: Get from current user service when auth is enabled
            var now = DateTime.UtcNow;

            // Resolve IDs from names if not provided
            Guid? workOrderTypeId = request.WorkOrderTypeId;
            Guid? assetCategoryId = request.AssetCategoryId;
            Guid? maintenanceTypeId = request.MaintenanceTypeId;
            var workOrderTypeName = NormalizeScopeValue(request.WorkOrderType);
            var assetCategoryName = NormalizeScopeValue(request.AssetCategory);
            var maintenanceTypeName = NormalizeScopeValue(request.MaintenanceType);

            if (!HasAnyChecklistScope(workOrderTypeName, workOrderTypeId, assetCategoryName, assetCategoryId, maintenanceTypeName, maintenanceTypeId))
            {
                return BadRequest("Select at least one checklist scope: maintenance type, work order type, or asset category.");
            }

            // Look up WorkOrderType by name if ID not provided
            if (!workOrderTypeId.HasValue && !string.IsNullOrEmpty(workOrderTypeName))
            {
                var workOrderType = await _context.WorkOrderTypes
                    .FirstOrDefaultAsync(w => w.Name == workOrderTypeName && w.TenantId == tenantId);
                workOrderTypeId = workOrderType?.Id;
            }

            // Look up AssetCategory by name if ID not provided
            if (!assetCategoryId.HasValue && !string.IsNullOrEmpty(assetCategoryName))
            {
                var assetCategory = await _context.MaintenanceAssetCategories
                    .FirstOrDefaultAsync(a => a.Name == assetCategoryName && a.TenantId == tenantId);
                assetCategoryId = assetCategory?.Id;
            }

            // Look up MaintenanceType by name if ID not provided
            if (!maintenanceTypeId.HasValue && !string.IsNullOrEmpty(maintenanceTypeName))
            {
                var maintenanceType = await _context.MaintenanceTypes
                    .FirstOrDefaultAsync(m => m.Name == maintenanceTypeName && m.TenantId == tenantId);
                maintenanceTypeId = maintenanceType?.Id;
            }

            var scopeConflictMessage = await GetChecklistScopeConflictMessageAsync(
                tenantId,
                null,
                maintenanceTypeId,
                maintenanceTypeName,
                workOrderTypeId,
                workOrderTypeName,
                assetCategoryId,
                assetCategoryName);

            if (!string.IsNullOrWhiteSpace(scopeConflictMessage))
            {
                return BadRequest(new { message = scopeConflictMessage });
            }

            // Convert items to JSON
            var itemsJson = JsonSerializer.Serialize(request.Items.Select((item, index) => new
            {
                item = item.Text,
                description = item.Description,
                weight = item.Critical ? 25 : 10 // Higher weight for critical items
            }));

            var checklist = new ErpSystem.Core.Entities.Maintenance.QualityControlChecklist
            {
                Id = checklistId,
                Name = request.Name,
                Description = request.Description,
                WorkOrderType = workOrderTypeName,
                WorkOrderTypeId = workOrderTypeId,
                AssetCategory = assetCategoryName,
                AssetCategoryId = assetCategoryId,
                MaintenanceType = maintenanceTypeName,
                MaintenanceTypeId = maintenanceTypeId,
                IsMandatory = request.IsMandatory,
                IsActive = true,
                ChecklistItems = itemsJson,
                MinimumPassingScore = request.MinimumPassingScore,
                Version = 1,
                CreatedDate = now,
                LastModifiedDate = now,
                CreatedById = currentUserId,
                LastModifiedById = currentUserId,
                TenantId = tenantId
            };

            _context.QualityControlChecklists.Add(checklist);
            await _context.SaveChangesAsync();

            var response = new
            {
                id = checklist.Id.ToString(),
                name = checklist.Name,
                description = checklist.Description,
                workOrderType = checklist.WorkOrderType,
                workOrderTypeId = checklist.WorkOrderTypeId?.ToString(),
                assetCategory = checklist.AssetCategory,
                assetCategoryId = checklist.AssetCategoryId?.ToString(),
                maintenanceType = checklist.MaintenanceType,
                maintenanceTypeId = checklist.MaintenanceTypeId?.ToString(),
                isMandatory = checklist.IsMandatory,
                isActive = checklist.IsActive,
                minimumPassingScore = checklist.MinimumPassingScore,
                version = checklist.Version,
                createdDate = checklist.CreatedDate,
                createdBy = "System",
                lastModifiedDate = checklist.LastModifiedDate,
                lastModifiedBy = "System",
                items = ParseChecklistItems(checklist.ChecklistItems)
            };

            return CreatedAtAction(nameof(GetChecklistById), new { id = checklist.Id.ToString() }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quality checklist");
            return StatusCode(500, "An error occurred while creating the quality checklist");
        }
    }

    /// <summary>
    /// Updates an existing quality checklist
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<object>> UpdateChecklist(string id, [FromBody] UpdateChecklistRequest request)
    {
        try
        {
            var tenantId = _currentUserService?.TenantId;
            _logger.LogInformation("Updating quality checklist {ChecklistId} for tenant {TenantId}", id, tenantId?.ToString() ?? "No Tenant (Auth Disabled)");

            // Parse the ID to Guid
            if (!Guid.TryParse(id, out var checklistId))
            {
                return BadRequest("Invalid checklist ID format");
            }

            // Find existing checklist
            var existingChecklist = await _context.QualityControlChecklists
                .FirstOrDefaultAsync(c => c.Id == checklistId);

            if (existingChecklist == null)
            {
                return NotFound($"Quality checklist with ID {id} not found");
            }

            var currentUserId = Guid.Empty; // TODO: Get from current user service when auth is enabled
            var now = DateTime.UtcNow;

            // Resolve IDs from names if not provided
            Guid? workOrderTypeId = request.WorkOrderTypeId;
            Guid? assetCategoryId = request.AssetCategoryId;
            Guid? maintenanceTypeId = request.MaintenanceTypeId;
            var workOrderTypeName = NormalizeScopeValue(request.WorkOrderType);
            var assetCategoryName = NormalizeScopeValue(request.AssetCategory);
            var maintenanceTypeName = NormalizeScopeValue(request.MaintenanceType);

            if (!HasAnyChecklistScope(workOrderTypeName, workOrderTypeId, assetCategoryName, assetCategoryId, maintenanceTypeName, maintenanceTypeId))
            {
                return BadRequest("Select at least one checklist scope: maintenance type, work order type, or asset category.");
            }

            // Look up WorkOrderType by name if ID not provided
            if (!workOrderTypeId.HasValue && !string.IsNullOrEmpty(workOrderTypeName))
            {
                var workOrderType = await _context.WorkOrderTypes
                    .FirstOrDefaultAsync(w => w.Name == workOrderTypeName && w.TenantId == existingChecklist.TenantId);
                workOrderTypeId = workOrderType?.Id;
            }

            // Look up AssetCategory by name if ID not provided
            if (!assetCategoryId.HasValue && !string.IsNullOrEmpty(assetCategoryName))
            {
                var assetCategory = await _context.MaintenanceAssetCategories
                    .FirstOrDefaultAsync(a => a.Name == assetCategoryName && a.TenantId == existingChecklist.TenantId);
                assetCategoryId = assetCategory?.Id;
            }

            // Look up MaintenanceType by name if ID not provided
            if (!maintenanceTypeId.HasValue && !string.IsNullOrEmpty(maintenanceTypeName))
            {
                var maintenanceType = await _context.MaintenanceTypes
                    .FirstOrDefaultAsync(m => m.Name == maintenanceTypeName && m.TenantId == existingChecklist.TenantId);
                maintenanceTypeId = maintenanceType?.Id;
            }

            if (request.IsActive)
            {
                var scopeConflictMessage = await GetChecklistScopeConflictMessageAsync(
                    existingChecklist.TenantId,
                    existingChecklist.Id,
                    maintenanceTypeId,
                    maintenanceTypeName,
                    workOrderTypeId,
                    workOrderTypeName,
                    assetCategoryId,
                    assetCategoryName);

                if (!string.IsNullOrWhiteSpace(scopeConflictMessage))
                {
                    return BadRequest(new { message = scopeConflictMessage });
                }
            }

            // Convert items to JSON
            var itemsJson = JsonSerializer.Serialize(request.Items.Select((item, index) => new
            {
                item = item.Text,
                description = item.Description,
                weight = item.Critical ? 25 : 10 // Higher weight for critical items
            }));

            // Update properties
            existingChecklist.Name = request.Name;
            existingChecklist.Description = request.Description;
            existingChecklist.WorkOrderType = workOrderTypeName;
            existingChecklist.WorkOrderTypeId = workOrderTypeId;
            existingChecklist.AssetCategory = assetCategoryName;
            existingChecklist.AssetCategoryId = assetCategoryId;
            existingChecklist.MaintenanceType = maintenanceTypeName;
            existingChecklist.MaintenanceTypeId = maintenanceTypeId;
            existingChecklist.IsMandatory = request.IsMandatory;
            existingChecklist.IsActive = request.IsActive;
            existingChecklist.ChecklistItems = itemsJson;
            existingChecklist.MinimumPassingScore = request.MinimumPassingScore;
            existingChecklist.LastModifiedDate = now;
            existingChecklist.LastModifiedById = currentUserId;
            existingChecklist.Version += 1;

            await _context.SaveChangesAsync();

            var response = new
            {
                id = existingChecklist.Id.ToString(),
                name = existingChecklist.Name,
                description = existingChecklist.Description,
                workOrderType = existingChecklist.WorkOrderType,
                workOrderTypeId = existingChecklist.WorkOrderTypeId?.ToString(),
                assetCategory = existingChecklist.AssetCategory,
                assetCategoryId = existingChecklist.AssetCategoryId?.ToString(),
                maintenanceType = existingChecklist.MaintenanceType,
                maintenanceTypeId = existingChecklist.MaintenanceTypeId?.ToString(),
                isMandatory = existingChecklist.IsMandatory,
                isActive = existingChecklist.IsActive,
                minimumPassingScore = existingChecklist.MinimumPassingScore,
                version = existingChecklist.Version,
                createdDate = existingChecklist.CreatedDate,
                createdBy = "System",
                lastModifiedDate = existingChecklist.LastModifiedDate,
                lastModifiedBy = "System",
                items = ParseChecklistItems(existingChecklist.ChecklistItems)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quality checklist {ChecklistId}", id);
            return StatusCode(500, "An error occurred while updating the quality checklist");
        }
    }

    /// <summary>
    /// Deletes a quality checklist
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteChecklist(string id)
    {
        try
        {
            var tenantId = _currentUserService?.TenantId;
            _logger.LogInformation("Deleting quality checklist {ChecklistId} for tenant {TenantId}", id, tenantId?.ToString() ?? "No Tenant (Auth Disabled)");

            // Parse the ID to Guid
            if (!Guid.TryParse(id, out var checklistId))
            {
                return BadRequest("Invalid checklist ID format");
            }

            // Find existing checklist
            var existingChecklist = await _context.QualityControlChecklists
                .FirstOrDefaultAsync(c => c.Id == checklistId);

            if (existingChecklist == null)
            {
                return NotFound($"Quality checklist with ID {id} not found");
            }

            // Check if there are any associated quality checks before deleting
            var hasQualityChecks = await _context.WorkOrderQualityChecks
                .AnyAsync(qc => qc.ChecklistId == checklistId);

            if (hasQualityChecks)
            {
                // Instead of hard delete, soft delete by marking as inactive
                existingChecklist.IsActive = false;
                existingChecklist.LastModifiedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Quality checklist has been deactivated due to existing usage in quality checks" });
            }
            else
            {
                // Hard delete if no quality checks are associated
                _context.QualityControlChecklists.Remove(existingChecklist);
                await _context.SaveChangesAsync();

                return NoContent();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting quality checklist {ChecklistId}", id);
            return StatusCode(500, "An error occurred while deleting the quality checklist");
        }
    }

    /// <summary>
    /// Gets checklists by work order type
    /// </summary>
    [HttpGet("by-work-order-type/{workOrderType}")]
    public ActionResult<object[]> GetChecklistsByWorkOrderType(string workOrderType)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Getting checklists for work order type {WorkOrderType} for tenant {TenantId}", workOrderType, tenantId);

            // Filter mock checklists by work order type
            var checklists = new[]
            {
                new
                {
                    id = "checklist-2",
                    name = "HVAC Safety Inspection Checklist",
                    workOrderType = "Safety",
                    assetCategory = "HVAC",
                    isMandatory = true,
                    minimumPassingScore = 90,
                    itemCount = 5
                },
                new
                {
                    id = "checklist-3",
                    name = "Fire Safety System Inspection",
                    workOrderType = "Safety",
                    assetCategory = "Fire Safety",
                    isMandatory = true,
                    minimumPassingScore = 95,
                    itemCount = 8
                }
            }.Where(c => c.workOrderType.Equals(workOrderType, StringComparison.OrdinalIgnoreCase)).ToArray();

            return Ok(checklists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checklists for work order type {WorkOrderType}", workOrderType);
            return StatusCode(500, "An error occurred while retrieving checklists for the work order type");
        }
    }

    /// <summary>
    /// Gets checklists by asset category
    /// </summary>
    [HttpGet("by-asset-category/{assetCategory}")]
    public ActionResult<object[]> GetChecklistsByAssetCategory(string assetCategory)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Getting checklists for asset category {AssetCategory} for tenant {TenantId}", assetCategory, tenantId);

            // Filter mock checklists by asset category
            var checklists = new[]
            {
                new
                {
                    id = "checklist-2",
                    name = "HVAC Safety Inspection Checklist",
                    workOrderType = "Safety",
                    assetCategory = "HVAC",
                    isMandatory = true,
                    minimumPassingScore = 90,
                    itemCount = 5
                }
            }.Where(c => c.assetCategory.Equals(assetCategory, StringComparison.OrdinalIgnoreCase)).ToArray();

            return Ok(checklists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checklists for asset category {AssetCategory}", assetCategory);
            return StatusCode(500, "An error occurred while retrieving checklists for the asset category");
        }
    }

    /// <summary>
    /// Gets checklist usage statistics
    /// </summary>
    [HttpGet("{id}/statistics")]
    public ActionResult<object> GetChecklistStatistics(string id)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            _logger.LogInformation("Getting statistics for checklist {ChecklistId} for tenant {TenantId}", id, tenantId);

            // Return mock statistics
            var statistics = new
            {
                checklistId = id,
                totalUsages = 18,
                averageScore = 91.2,
                passRate = 94.4,
                averageDuration = 58,
                lastUsed = DateTime.Today.AddDays(-2),
                usagesByMonth = new[]
                {
                    new { month = "Jan", usages = 6, averageScore = 89.5 },
                    new { month = "Feb", usages = 5, averageScore = 92.1 },
                    new { month = "Mar", usages = 4, averageScore = 93.2 },
                    new { month = "Apr", usages = 3, averageScore = 91.8 }
                },
                commonFailurePoints = new[]
                {
                    new { itemId = "item-2", itemText = "Refrigerant levels within range", failureRate = 12.5 },
                    new { itemId = "item-4", itemText = "Thermostat calibration", failureRate = 8.3 }
                },
                performanceByInspector = new[]
                {
                    new { inspectorId = "inspector-1", inspectorName = "John Smith", usages = 10, averageScore = 90.5 },
                    new { inspectorId = "inspector-2", inspectorName = "Sarah Johnson", usages = 8, averageScore = 92.1 }
                }
            };

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics for checklist {ChecklistId}", id);
            return StatusCode(500, "An error occurred while retrieving checklist statistics");
        }
    }

    private object[] ParseChecklistItems(string checklistItemsJson)
    {
        try
        {
            if (string.IsNullOrEmpty(checklistItemsJson))
            {
                return Array.Empty<object>();
            }

            var items = System.Text.Json.JsonSerializer.Deserialize<ChecklistItemDto[]>(checklistItemsJson);
            return items?.Select((item, index) => new
            {
                id = $"item-{index + 1}",
                text = item.item ?? $"Quality Check Item {index + 1}",
                description = item.description ?? "Quality check requirement",
                required = true,
                critical = item.weight >= 20,
                category = "Quality",
                responseType = "Pass/Fail",
                order = index + 1
            }).ToArray() ?? Array.Empty<object>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse checklist items JSON: {Json}", checklistItemsJson);
            return Array.Empty<object>();
        }
    }

    private class ChecklistItemDto
    {
        public string? item { get; set; }
        public string? description { get; set; }
        public int weight { get; set; }
    }

    private static string? NormalizeScopeValue(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ||
            string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase)
                ? null
                : trimmed;
    }

    private static bool HasAnyChecklistScope(
        string? workOrderType,
        Guid? workOrderTypeId,
        string? assetCategory,
        Guid? assetCategoryId,
        string? maintenanceType,
        Guid? maintenanceTypeId)
    {
        return HasScope(maintenanceTypeId, maintenanceType) ||
            HasScope(workOrderTypeId, workOrderType) ||
            HasScope(assetCategoryId, assetCategory);
    }

    private async Task<string?> GetChecklistScopeConflictMessageAsync(
        Guid tenantId,
        Guid? excludeChecklistId,
        Guid? maintenanceTypeId,
        string? maintenanceTypeName,
        Guid? workOrderTypeId,
        string? workOrderTypeName,
        Guid? assetCategoryId,
        string? assetCategoryName)
    {
        var matchingScope = await _context.QualityControlChecklists
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .Where(c => !excludeChecklistId.HasValue || c.Id != excludeChecklistId.Value)
            .ToListAsync();

        var duplicate = matchingScope.FirstOrDefault(checklist =>
            SameScopeLevel(
                checklist.MaintenanceTypeId,
                checklist.MaintenanceType,
                maintenanceTypeId,
                maintenanceTypeName) &&
            SameScopeLevel(
                checklist.WorkOrderTypeId,
                checklist.WorkOrderType,
                workOrderTypeId,
                workOrderTypeName) &&
            SameScopeLevel(
                checklist.AssetCategoryId,
                checklist.AssetCategory,
                assetCategoryId,
                assetCategoryName));

        if (duplicate == null)
        {
            return null;
        }

        return $"A quality checklist template already exists for this scope ({DescribeChecklistScope(maintenanceTypeName, workOrderTypeName, assetCategoryName)}): {duplicate.Name}. Add the next priority value to make the template more specific, or update the existing template.";
    }

    private static bool SameScopeLevel(Guid? existingId, string? existingName, Guid? requestedId, string? requestedName)
    {
        var existingHasScope = HasScope(existingId, existingName);
        var requestedHasScope = HasScope(requestedId, requestedName);

        if (existingHasScope != requestedHasScope)
        {
            return false;
        }

        if (!existingHasScope)
        {
            return true;
        }

        return ScopeValuesMatch(existingId, existingName, requestedId, requestedName);
    }

    private static bool HasScope(Guid? id, string? name)
    {
        return (id.HasValue && id.Value != Guid.Empty) || !string.IsNullOrWhiteSpace(NormalizeScopeValue(name));
    }

    private static bool ScopeValuesMatch(Guid? existingId, string? existingName, Guid? requestedId, string? requestedName)
    {
        var existingScopeId = existingId.GetValueOrDefault();
        var requestedScopeId = requestedId.GetValueOrDefault();

        if (existingScopeId != Guid.Empty && requestedScopeId != Guid.Empty)
        {
            return existingScopeId == requestedScopeId;
        }

        var existingScopeName = NormalizeScopeValue(existingName);
        var requestedScopeName = NormalizeScopeValue(requestedName);

        return !string.IsNullOrWhiteSpace(existingScopeName) &&
            !string.IsNullOrWhiteSpace(requestedScopeName) &&
            string.Equals(existingScopeName, requestedScopeName, StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeChecklistScope(string? maintenanceTypeName, string? workOrderTypeName, string? assetCategoryName)
    {
        var parts = new List<string>();
        var maintenanceType = NormalizeScopeValue(maintenanceTypeName);
        var workOrderType = NormalizeScopeValue(workOrderTypeName);
        var assetCategory = NormalizeScopeValue(assetCategoryName);

        if (!string.IsNullOrWhiteSpace(maintenanceType))
        {
            parts.Add($"Maintenance Type: {maintenanceType}");
        }

        if (!string.IsNullOrWhiteSpace(workOrderType))
        {
            parts.Add($"Work Order Type: {workOrderType}");
        }

        if (!string.IsNullOrWhiteSpace(assetCategory))
        {
            parts.Add($"Asset Category: {assetCategory}");
        }

        return parts.Count == 0 ? "No scope" : string.Join(", ", parts);
    }

    public class CreateChecklistRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? WorkOrderType { get; set; }
        public Guid? WorkOrderTypeId { get; set; }
        public string? AssetCategory { get; set; }
        public Guid? AssetCategoryId { get; set; }
        public string? MaintenanceType { get; set; }
        public Guid? MaintenanceTypeId { get; set; }
        public bool IsMandatory { get; set; } = true;
        public int MinimumPassingScore { get; set; } = 80;
        public List<CreateChecklistItemRequest> Items { get; set; } = new();
    }

    public class UpdateChecklistRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? WorkOrderType { get; set; }
        public Guid? WorkOrderTypeId { get; set; }
        public string? AssetCategory { get; set; }
        public Guid? AssetCategoryId { get; set; }
        public string? MaintenanceType { get; set; }
        public Guid? MaintenanceTypeId { get; set; }
        public bool IsMandatory { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public int MinimumPassingScore { get; set; } = 80;
        public List<CreateChecklistItemRequest> Items { get; set; } = new();
    }

    public class CreateChecklistItemRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Required { get; set; } = true;
        public bool Critical { get; set; } = false;
        public string Category { get; set; } = "Quality";
        public string ResponseType { get; set; } = "Pass/Fail";
        public int Order { get; set; }
    }
}
