using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/kb")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin + "," +
    Constants.Roles.Employee)]
public sealed class EhcKnowledgeBaseInternalController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public EhcKnowledgeBaseInternalController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;

    [HttpGet("categories")]
    public async Task<ActionResult> ListCategories(CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = Array.Empty<EhcKnowledgeBaseCategoryDto>() });

        var items = await _db.EhcKnowledgeBaseCategories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new EhcKnowledgeBaseCategoryDto
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

    [HttpGet("articles/search")]
    public async Task<ActionResult> Search([FromQuery] string? q = null, [FromQuery] Guid? categoryId = null, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = Array.Empty<EhcKnowledgeBaseArticleListItemDto>() });

        q = (q ?? string.Empty).Trim();
        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        var query = _db.EhcKnowledgeBaseArticles
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.IsPublished && a.IsInternalOnly);

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            query = query.Where(a => a.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.ToUpperInvariant();
            query = query.Where(a =>
                (a.Title != null && a.Title.ToUpper().Contains(term)) ||
                (a.Summary != null && a.Summary.ToUpper().Contains(term)) ||
                (a.TagsCsv != null && a.TagsCsv.ToUpper().Contains(term)) ||
                (a.Code != null && a.Code.ToUpper().Contains(term)));
        }

        var items = await query
            .OrderByDescending(a => a.ViewCount)
            .ThenBy(a => a.Title)
            .Take(limit)
            .Select(a => new EhcKnowledgeBaseArticleListItemDto
            {
                Id = a.Id,
                Code = a.Code,
                Title = a.Title,
                Summary = a.Summary,
                CategoryId = a.CategoryId,
                CategoryName = a.Category != null ? a.Category.Name : null,
                TagsCsv = a.TagsCsv,
                IsPublished = a.IsPublished,
                IsInternalOnly = a.IsInternalOnly,
                ViewCount = a.ViewCount,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpGet("articles/{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = (EhcKnowledgeBaseArticleDetailDto?)null });

        var a = await _db.EhcKnowledgeBaseArticles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsPublished && x.IsInternalOnly && x.Id == id)
            .Select(x => new EhcKnowledgeBaseArticleDetailDto
            {
                Id = x.Id,
                Code = x.Code,
                Title = x.Title,
                Summary = x.Summary,
                Body = x.Body,
                CategoryId = x.CategoryId,
                CategoryName = x.Category != null ? x.Category.Name : null,
                TagsCsv = x.TagsCsv,
                IsPublished = x.IsPublished,
                IsInternalOnly = x.IsInternalOnly,
                ViewCount = x.ViewCount,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new { success = true, data = a });
    }

    [HttpPost("articles/{id:guid}/view")]
    public async Task<ActionResult> TrackView(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true });

        var entity = await _db.EhcKnowledgeBaseArticles.FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == id, cancellationToken);
        if (entity == null)
            return Ok(new { success = true });

        entity.ViewCount += 1;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName;
        entity.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }
}

