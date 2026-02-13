using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/root-causes")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminRootCauseCodesController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcAdminRootCauseCodesController> _logger;

    public EhcAdminRootCauseCodesController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcAdminRootCauseCodesController> logger)
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
            return Ok(new { success = true, data = Array.Empty<EhcRootCauseCodeDto>() });
        }

        var q = _db.EhcRootCauseCodes.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (!includeDeleted)
        {
            q = q.Where(c => !c.IsDeleted);
        }

        var items = await q.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var data = items.Select(c => new EhcRootCauseCodeDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            Description = c.Description,
            IsActive = c.IsActive
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateEhcRootCauseCodeRequestDto request, CancellationToken cancellationToken)
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
            var exists = await _db.EhcRootCauseCodes.AnyAsync(
                c => c.TenantId == tenantId && !c.IsDeleted && c.Code == code,
                cancellationToken);
            if (exists)
            {
                return BadRequest(new { success = false, message = "Root cause code already exists." });
            }

            var now = DateTime.UtcNow;
            var entity = new EhcRootCauseCode
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                IsActive = request.IsActive,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System"
            };

            _db.EhcRootCauseCodes.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new EhcRootCauseCodeDto
                {
                    Id = entity.Id,
                    Code = entity.Code,
                    Name = entity.Name,
                    Description = entity.Description,
                    IsActive = entity.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EHC root cause code");
            return StatusCode(500, new { success = false, message = "Failed to create root cause" });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateEhcRootCauseCodeRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var entity = await _db.EhcRootCauseCodes.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, cancellationToken);
            if (entity == null)
            {
                return NotFound(new { success = false, message = "Root cause not found" });
            }

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { success = false, message = "Code and Name are required." });
            }

            var code = request.Code.Trim();
            var codeExists = await _db.EhcRootCauseCodes.AnyAsync(
                c => c.TenantId == tenantId && !c.IsDeleted && c.Code == code && c.Id != id,
                cancellationToken);
            if (codeExists)
            {
                return BadRequest(new { success = false, message = "Root cause code already exists." });
            }

            entity.Code = code;
            entity.Name = request.Name.Trim();
            entity.Description = request.Description?.Trim();
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new EhcRootCauseCodeDto
                {
                    Id = entity.Id,
                    Code = entity.Code,
                    Name = entity.Name,
                    Description = entity.Description,
                    IsActive = entity.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating EHC root cause code {RootCauseId}", id);
            return StatusCode(500, new { success = false, message = "Failed to update root cause" });
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

            var entity = await _db.EhcRootCauseCodes.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId, cancellationToken);
            if (entity == null)
            {
                return NotFound(new { success = false, message = "Root cause not found" });
            }

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting EHC root cause code {RootCauseId}", id);
            return StatusCode(500, new { success = false, message = "Failed to delete root cause" });
        }
    }
}

