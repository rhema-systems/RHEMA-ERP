using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/external/metadata")]
[Authorize(Policy = "ExternalOnly")]
public sealed class EhcExternalMetadataController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcExternalMetadataController> _logger;

    public EhcExternalMetadataController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcExternalMetadataController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet("priorities")]
    public async Task<ActionResult> GetPriorities(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return Ok(new { success = true, data = Array.Empty<object>() });
            }

            var anyConfigured = await _db.EhcTicketPriorityLevels
                .AsNoTracking()
                .AnyAsync(p => p.TenantId == tenantId && !p.IsDeleted, cancellationToken);

            var items = await _db.EhcTicketPriorityLevels
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.IsActive)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Priority)
                .Select(p => new
                {
                    priority = p.Priority,
                    displayName = p.DisplayName,
                    description = p.Description,
                    isActive = p.IsActive,
                    sortOrder = p.SortOrder
                })
                .ToListAsync(cancellationToken);

            if (anyConfigured)
            {
                return Ok(new { success = true, data = items });
            }

            // Fallback when admin has not configured priorities yet.
            var defaults = new[]
            {
                new { priority = EhcTicketPriority.Low, displayName = "Low", description = (string?)null, isActive = true, sortOrder = 1 },
                new { priority = EhcTicketPriority.Medium, displayName = "Medium", description = (string?)null, isActive = true, sortOrder = 2 },
                new { priority = EhcTicketPriority.High, displayName = "High", description = (string?)null, isActive = true, sortOrder = 3 },
                new { priority = EhcTicketPriority.Critical, displayName = "Critical", description = (string?)null, isActive = true, sortOrder = 4 },
            };

            return Ok(new { success = true, data = defaults });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading EHC priorities metadata");
            return StatusCode(500, new { success = false, message = "Failed to load metadata" });
        }
    }

    [HttpGet("categories")]
    public async Task<ActionResult> GetCategories(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return Ok(new { success = true, data = Array.Empty<object>() });
            }

            // Load all categories for the tenant and build a full tree in-memory.
            // This avoids relying on EF self-referencing Includes (which can be brittle for deeper nesting).
            var all = await _db.EhcTicketCategories
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            // Build hierarchy in-memory defensively:
            // - Avoid ToDictionary() on nullable keys (boxing Guid? w/ no value becomes null and throws).
            // - Treat orphaned categories (parent missing) as roots so they still appear in the portal.
            var byId = all.ToDictionary(c => c.Id, c => c);

            var rootNodes = new List<EhcTicketCategory>();
            var childrenByParent = new Dictionary<Guid, List<EhcTicketCategory>>();

            foreach (var c in all)
            {
                var parentId = c.ParentCategoryId;
                var isRoot = !parentId.HasValue || parentId.Value == Guid.Empty || !byId.ContainsKey(parentId.Value);
                if (isRoot)
                {
                    rootNodes.Add(c);
                    continue;
                }

                var pid = parentId!.Value;
                if (!childrenByParent.TryGetValue(pid, out var list))
                {
                    list = new List<EhcTicketCategory>();
                    childrenByParent[pid] = list;
                }
                list.Add(c);
            }

            rootNodes = rootNodes.OrderBy(c => c.Name).ToList();
            foreach (var kvp in childrenByParent)
            {
                kvp.Value.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }

            List<EhcTicketCategoryTreeDto> Build(Guid? parentId)
            {
                List<EhcTicketCategory> children;
                if (parentId == null || parentId == Guid.Empty)
                {
                    children = rootNodes;
                }
                else if (!childrenByParent.TryGetValue(parentId.Value, out children!))
                {
                    return new List<EhcTicketCategoryTreeDto>();
                }

                return children.Select(c => new EhcTicketCategoryTreeDto
                {
                    Id = c.Id,
                    Code = c.Code,
                    Name = c.Name,
                    Description = c.Description,
                    AppliesToType = c.AppliesToType,
                    Subcategories = Build(c.Id)
                }).ToList();
            }

            var result = Build(null);

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading EHC categories metadata");
            return StatusCode(500, new { success = false, message = "Failed to load metadata" });
        }
    }
}
