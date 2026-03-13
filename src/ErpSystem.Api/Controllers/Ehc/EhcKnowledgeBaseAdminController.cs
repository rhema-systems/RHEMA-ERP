using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/kb")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcKnowledgeBaseAdminController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public EhcKnowledgeBaseAdminController(
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
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
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

    [HttpPost("categories")]
    public async Task<ActionResult> CreateCategory([FromBody] CreateEhcKnowledgeBaseCategoryRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new CreateEhcKnowledgeBaseCategoryRequestDto();

        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest(new { success = false, message = "Code and Name are required." });

        var exists = await _db.EhcKnowledgeBaseCategories.AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Code == code, cancellationToken);
        if (exists)
            return BadRequest(new { success = false, message = "Category code already exists." });

        _db.EhcKnowledgeBaseCategories.Add(new EhcKnowledgeBaseCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName,
            CreatedById = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPut("categories/{id:guid}")]
    public async Task<ActionResult> UpdateCategory(Guid id, [FromBody] UpdateEhcKnowledgeBaseCategoryRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new UpdateEhcKnowledgeBaseCategoryRequestDto();

        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var entity = await _db.EhcKnowledgeBaseCategories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        if (entity == null)
            return NotFound(new { success = false, message = "Not found" });

        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest(new { success = false, message = "Code and Name are required." });

        var exists = await _db.EhcKnowledgeBaseCategories.AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Id != id && c.Code == code, cancellationToken);
        if (exists)
            return BadRequest(new { success = false, message = "Category code already exists." });

        entity.Code = code;
        entity.Name = name;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName;
        entity.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpDelete("categories/{id:guid}")]
    public async Task<ActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var entity = await _db.EhcKnowledgeBaseCategories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        if (entity == null)
            return Ok(new { success = true });

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = _currentUserService.UserName;
        entity.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpGet("articles")]
    public async Task<ActionResult> ListArticles(CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = Array.Empty<EhcKnowledgeBaseArticleListItemDto>() });

        var items = await _db.EhcKnowledgeBaseArticles
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
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
    public async Task<ActionResult> GetArticle(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = (EhcKnowledgeBaseArticleDetailDto?)null });

        var a = await _db.EhcKnowledgeBaseArticles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == id)
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

    [HttpPost("articles")]
    public async Task<ActionResult> CreateArticle([FromBody] CreateEhcKnowledgeBaseArticleRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new CreateEhcKnowledgeBaseArticleRequestDto();

        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { success = false, message = "Code, Title and Body are required." });

        var exists = await _db.EhcKnowledgeBaseArticles.AnyAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.Code == code, cancellationToken);
        if (exists)
            return BadRequest(new { success = false, message = "Article code already exists." });

        _db.EhcKnowledgeBaseArticles.Add(new EhcKnowledgeBaseArticle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Title = title,
            Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim(),
            Body = request.Body.Trim(),
            CategoryId = request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty ? request.CategoryId : null,
            TagsCsv = string.IsNullOrWhiteSpace(request.TagsCsv) ? null : request.TagsCsv.Trim(),
            IsPublished = request.IsPublished,
            IsInternalOnly = request.IsInternalOnly,
            ViewCount = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName,
            CreatedById = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPut("articles/{id:guid}")]
    public async Task<ActionResult> UpdateArticle(Guid id, [FromBody] UpdateEhcKnowledgeBaseArticleRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new UpdateEhcKnowledgeBaseArticleRequestDto();

        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var entity = await _db.EhcKnowledgeBaseArticles.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (entity == null)
            return NotFound(new { success = false, message = "Not found" });

        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { success = false, message = "Code, Title and Body are required." });

        var exists = await _db.EhcKnowledgeBaseArticles.AnyAsync(a => a.TenantId == tenantId && !a.IsDeleted && a.Id != id && a.Code == code, cancellationToken);
        if (exists)
            return BadRequest(new { success = false, message = "Article code already exists." });

        entity.Code = code;
        entity.Title = title;
        entity.Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim();
        entity.Body = request.Body.Trim();
        entity.CategoryId = request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty ? request.CategoryId : null;
        entity.TagsCsv = string.IsNullOrWhiteSpace(request.TagsCsv) ? null : request.TagsCsv.Trim();
        entity.IsPublished = request.IsPublished;
        entity.IsInternalOnly = request.IsInternalOnly;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName;
        entity.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpDelete("articles/{id:guid}")]
    public async Task<ActionResult> DeleteArticle(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var entity = await _db.EhcKnowledgeBaseArticles.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted, cancellationToken);
        if (entity == null)
            return Ok(new { success = true });

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = _currentUserService.UserName;
        entity.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }
}

