using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/categories")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminCategoriesController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcAdminCategoriesController> _logger;

    public EhcAdminCategoriesController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcAdminCategoriesController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcTicketCategoryDto>() });
        }

        var q = _db.EhcTicketCategories.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (!includeDeleted)
        {
            q = q.Where(c => !c.IsDeleted);
        }

        var items = await q.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var data = items.Select(c => new EhcTicketCategoryDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            Description = c.Description,
            AppliesToType = c.AppliesToType,
            ParentCategoryId = c.ParentCategoryId
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateEhcTicketCategoryRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { success = false, message = "Code and Name are required." });
            }

            var code = request.Code.Trim();
            var exists = await _db.EhcTicketCategories.AnyAsync(
                c => c.TenantId == tenantId && !c.IsDeleted && c.Code == code,
                cancellationToken);
            if (exists)
            {
                return BadRequest(new { success = false, message = "Category code already exists." });
            }

            var now = DateTime.UtcNow;
            var parentId = request.ParentCategoryId.HasValue && request.ParentCategoryId.Value == Guid.Empty
                ? (Guid?)null
                : request.ParentCategoryId;

            var category = new EhcTicketCategory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                AppliesToType = request.AppliesToType,
                ParentCategoryId = parentId,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System"
            };

            _db.EhcTicketCategories.Add(category);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new EhcTicketCategoryDto
                {
                    Id = category.Id,
                    Code = category.Code,
                    Name = category.Name,
                    Description = category.Description,
                    AppliesToType = category.AppliesToType,
                    ParentCategoryId = category.ParentCategoryId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EHC category");
            return StatusCode(500, new { success = false, message = "Failed to create category" });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateEhcTicketCategoryRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var category = await _db.EhcTicketCategories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, cancellationToken);
            if (category == null)
            {
                return NotFound(new { success = false, message = "Category not found" });
            }

            var code = request.Code.Trim();
            var codeExists = await _db.EhcTicketCategories.AnyAsync(
                c => c.TenantId == tenantId && !c.IsDeleted && c.Code == code && c.Id != id,
                cancellationToken);
            if (codeExists)
            {
                return BadRequest(new { success = false, message = "Category code already exists." });
            }

            category.Code = code;
            category.Name = request.Name.Trim();
            category.Description = request.Description?.Trim();
            category.AppliesToType = request.AppliesToType;
            category.ParentCategoryId = request.ParentCategoryId.HasValue && request.ParentCategoryId.Value == Guid.Empty
                ? (Guid?)null
                : request.ParentCategoryId;
            category.UpdatedAt = DateTime.UtcNow;
            category.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new EhcTicketCategoryDto
                {
                    Id = category.Id,
                    Code = category.Code,
                    Name = category.Name,
                    Description = category.Description,
                    AppliesToType = category.AppliesToType,
                    ParentCategoryId = category.ParentCategoryId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating EHC category {CategoryId}", id);
            return StatusCode(500, new { success = false, message = "Failed to update category" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var category = await _db.EhcTicketCategories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, cancellationToken);
            if (category == null)
            {
                return NotFound(new { success = false, message = "Category not found" });
            }

            category.IsDeleted = true;
            category.DeletedAt = DateTime.UtcNow;
            category.DeletedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting EHC category {CategoryId}", id);
            return StatusCode(500, new { success = false, message = "Failed to delete category" });
        }
    }
}
