using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/faq")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin + "," +
    Constants.Roles.Employee)]
public sealed class EhcFaqInternalController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public EhcFaqInternalController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;

    private Guid? CurrentUserId => Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null;

    [HttpGet("categories")]
    public async Task<ActionResult> ListCategories(CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = Array.Empty<EhcFaqCategoryDto>() });

        var items = await _db.EhcFaqCategories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new EhcFaqCategoryDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpGet("items/search")]
    public async Task<ActionResult> Search([FromQuery] string? q = null, [FromQuery] Guid? categoryId = null, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = Array.Empty<EhcFaqItemDetailDto>() });

        q = (q ?? string.Empty).Trim();
        if (limit < 1) limit = 1;
        if (limit > 200) limit = 200;

        var query = _db.EhcFaqItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.IsPublished);

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            query = query.Where(i => i.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.ToUpperInvariant();
            query = query.Where(i =>
                (i.Question != null && i.Question.ToUpper().Contains(term)) ||
                (i.Answer != null && i.Answer.ToUpper().Contains(term)) ||
                (i.Category != null && i.Category.Name.ToUpper().Contains(term)) ||
                (i.Category != null && i.Category.Code.ToUpper().Contains(term)));
        }

        var items = await query
            .OrderBy(i => i.SortOrder)
            .ThenByDescending(i => i.ViewCount)
            .ThenBy(i => i.Question)
            .Take(limit)
            .Select(i => new EhcFaqItemDetailDto
            {
                Id = i.Id,
                Question = i.Question,
                Answer = i.Answer,
                CategoryId = i.CategoryId,
                CategoryName = i.Category != null ? i.Category.Name : null,
                IsPublished = i.IsPublished,
                IsInternalOnly = i.IsInternalOnly,
                SortOrder = i.SortOrder,
                ViewCount = i.ViewCount,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpGet("items/{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = (EhcFaqItemDetailDto?)null });

        var item = await _db.EhcFaqItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.IsPublished && i.Id == id)
            .Select(i => new EhcFaqItemDetailDto
            {
                Id = i.Id,
                Question = i.Question,
                Answer = i.Answer,
                CategoryId = i.CategoryId,
                CategoryName = i.Category != null ? i.Category.Name : null,
                IsPublished = i.IsPublished,
                IsInternalOnly = i.IsInternalOnly,
                SortOrder = i.SortOrder,
                ViewCount = i.ViewCount,
                CreatedAt = i.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new { success = true, data = item });
    }

    [HttpPost("items/{id:guid}/view")]
    public async Task<ActionResult> TrackView(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true });

        var entity = await _db.EhcFaqItems.FirstOrDefaultAsync(i => i.TenantId == tenantId && !i.IsDeleted && i.Id == id, cancellationToken);
        if (entity == null)
            return Ok(new { success = true });

        entity.ViewCount += 1;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName;
        entity.LastModifiedById = CurrentUserId;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }
}

