using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/canned-responses")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminCannedResponsesController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcAdminCannedResponsesController> _logger;

    public EhcAdminCannedResponsesController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcAdminCannedResponsesController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = Array.Empty<EhcCannedResponseDto>() });

        var rows = await _db.EhcCannedResponses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Code)
            .Select(x => new EhcCannedResponseDto
            {
                Id = x.Id,
                Code = x.Code,
                Title = x.Title,
                Body = x.Body,
                IsActive = x.IsActive,
                AppliesToType = x.AppliesToType,
                CategoryId = x.CategoryId
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = rows });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = (EhcCannedResponseDto?)null });

        var row = await _db.EhcCannedResponses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == id)
            .Select(x => new EhcCannedResponseDto
            {
                Id = x.Id,
                Code = x.Code,
                Title = x.Title,
                Body = x.Body,
                IsActive = x.IsActive,
                AppliesToType = x.AppliesToType,
                CategoryId = x.CategoryId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new { success = true, data = row });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateEhcCannedResponseRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            request ??= new CreateEhcCannedResponseRequestDto();

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
                return BadRequest(new { success = false, message = "Tenant context is required." });

            var code = (request.Code ?? string.Empty).Trim();
            var title = (request.Title ?? string.Empty).Trim();
            var body = (request.Body ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
                return BadRequest(new { success = false, message = "Code, Title and Body are required." });

            var exists = await _db.EhcCannedResponses
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Code == code, cancellationToken);

            if (exists)
                return BadRequest(new { success = false, message = "A canned response with this code already exists." });

            var now = DateTime.UtcNow;
            var entity = new EhcCannedResponse
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Title = title,
                Body = body,
                IsActive = request.IsActive,
                AppliesToType = request.AppliesToType,
                CategoryId = request.CategoryId,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName
            };

            _db.EhcCannedResponses.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new EhcCannedResponseDto
                {
                    Id = entity.Id,
                    Code = entity.Code,
                    Title = entity.Title,
                    Body = entity.Body,
                    IsActive = entity.IsActive,
                    AppliesToType = entity.AppliesToType,
                    CategoryId = entity.CategoryId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create EHC canned response");
            return StatusCode(500, new { success = false, message = "Failed to create canned response" });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateEhcCannedResponseRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            request ??= new UpdateEhcCannedResponseRequestDto();

            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
                return BadRequest(new { success = false, message = "Tenant context is required." });

            var entity = await _db.EhcCannedResponses
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

            if (entity == null)
                return NotFound(new { success = false, message = "Not found" });

            var code = (request.Code ?? string.Empty).Trim();
            var title = (request.Title ?? string.Empty).Trim();
            var body = (request.Body ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
                return BadRequest(new { success = false, message = "Code, Title and Body are required." });

            if (!string.Equals(entity.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                var exists = await _db.EhcCannedResponses
                    .IgnoreQueryFilters()
                    .AnyAsync(x => x.TenantId == tenantId && x.Code == code && x.Id != id, cancellationToken);

                if (exists)
                    return BadRequest(new { success = false, message = "A canned response with this code already exists." });
            }

            entity.Code = code;
            entity.Title = title;
            entity.Body = body;
            entity.IsActive = request.IsActive;
            entity.AppliesToType = request.AppliesToType;
            entity.CategoryId = request.CategoryId;
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.DeletedBy = null;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _currentUserService.UserName;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update EHC canned response {Id}", id);
            return StatusCode(500, new { success = false, message = "Failed to update canned response" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
                return BadRequest(new { success = false, message = "Tenant context is required." });

            var entity = await _db.EhcCannedResponses
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.Id == id, cancellationToken);

            if (entity == null)
                return NotFound(new { success = false, message = "Not found" });

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedBy = _currentUserService.UserName;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _currentUserService.UserName;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete EHC canned response {Id}", id);
            return StatusCode(500, new { success = false, message = "Failed to delete canned response" });
        }
    }
}

