using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/faq")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcFaqAdminController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public EhcFaqAdminController(
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
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
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

    [HttpPost("categories")]
    public async Task<ActionResult> CreateCategory([FromBody] CreateEhcFaqCategoryRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new CreateEhcFaqCategoryRequestDto();

        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest(new { success = false, message = "Code and Name are required." });

        var exists = await _db.EhcFaqCategories
            .AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Code == code, cancellationToken);
        if (exists)
            return BadRequest(new { success = false, message = "Category code already exists." });

        _db.EhcFaqCategories.Add(new EhcFaqCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName,
            CreatedById = CurrentUserId
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPut("categories/{id:guid}")]
    public async Task<ActionResult> UpdateCategory(Guid id, [FromBody] UpdateEhcFaqCategoryRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new UpdateEhcFaqCategoryRequestDto();

        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var entity = await _db.EhcFaqCategories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        if (entity == null)
            return NotFound(new { success = false, message = "Not found" });

        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest(new { success = false, message = "Code and Name are required." });

        var exists = await _db.EhcFaqCategories
            .AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Id != id && c.Code == code, cancellationToken);
        if (exists)
            return BadRequest(new { success = false, message = "Category code already exists." });

        entity.Code = code;
        entity.Name = name;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName;
        entity.LastModifiedById = CurrentUserId;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpDelete("categories/{id:guid}")]
    public async Task<ActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var entity = await _db.EhcFaqCategories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        if (entity == null)
            return Ok(new { success = true });

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = _currentUserService.UserName;
        entity.LastModifiedById = CurrentUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpGet("items")]
    public async Task<ActionResult> ListItems(CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = Array.Empty<EhcFaqItemListItemDto>() });

        var items = await _db.EhcFaqItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Question)
            .Select(i => new EhcFaqItemListItemDto
            {
                Id = i.Id,
                Question = i.Question,
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
    public async Task<ActionResult> GetItem(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = (EhcFaqItemDetailDto?)null });

        var item = await _db.EhcFaqItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.Id == id)
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

    [HttpPost("items")]
    public async Task<ActionResult> CreateItem([FromBody] CreateEhcFaqItemRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new CreateEhcFaqItemRequestDto();

        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var question = (request.Question ?? string.Empty).Trim();
        var answer = (request.Answer ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
            return BadRequest(new { success = false, message = "Question and Answer are required." });

        Guid? categoryId = null;
        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
        {
            categoryId = request.CategoryId.Value;
            var categoryExists = await _db.EhcFaqCategories
                .AsNoTracking()
                .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Id == categoryId.Value, cancellationToken);
            if (!categoryExists)
                return BadRequest(new { success = false, message = "Invalid CategoryId." });
        }

        _db.EhcFaqItems.Add(new EhcFaqItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Question = question,
            Answer = answer,
            CategoryId = categoryId,
            IsPublished = request.IsPublished,
            IsInternalOnly = request.IsInternalOnly,
            SortOrder = request.SortOrder,
            ViewCount = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName,
            CreatedById = CurrentUserId
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPut("items/{id:guid}")]
    public async Task<ActionResult> UpdateItem(Guid id, [FromBody] UpdateEhcFaqItemRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new UpdateEhcFaqItemRequestDto();

        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var entity = await _db.EhcFaqItems.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId && !i.IsDeleted, cancellationToken);
        if (entity == null)
            return NotFound(new { success = false, message = "Not found" });

        var question = (request.Question ?? string.Empty).Trim();
        var answer = (request.Answer ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
            return BadRequest(new { success = false, message = "Question and Answer are required." });

        Guid? categoryId = null;
        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
        {
            categoryId = request.CategoryId.Value;
            var categoryExists = await _db.EhcFaqCategories
                .AsNoTracking()
                .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Id == categoryId.Value, cancellationToken);
            if (!categoryExists)
                return BadRequest(new { success = false, message = "Invalid CategoryId." });
        }

        entity.Question = question;
        entity.Answer = answer;
        entity.CategoryId = categoryId;
        entity.IsPublished = request.IsPublished;
        entity.IsInternalOnly = request.IsInternalOnly;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName;
        entity.LastModifiedById = CurrentUserId;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpDelete("items/{id:guid}")]
    public async Task<ActionResult> DeleteItem(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        var entity = await _db.EhcFaqItems.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId && !i.IsDeleted, cancellationToken);
        if (entity == null)
            return Ok(new { success = true });

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = _currentUserService.UserName;
        entity.LastModifiedById = CurrentUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }
}

