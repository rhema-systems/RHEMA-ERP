using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/priorities")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminPrioritiesController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public EhcAdminPrioritiesController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;
    private Guid? CurrentUserId => Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : null;

    private static readonly (EhcTicketPriority priority, string name, int sort)[] DefaultLevels =
    [
        (EhcTicketPriority.Low, "Low", 1),
        (EhcTicketPriority.Medium, "Medium", 2),
        (EhcTicketPriority.High, "High", 3),
        (EhcTicketPriority.Critical, "Critical", 4),
    ];

    [HttpGet]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = Array.Empty<EhcTicketPriorityLevelDto>() });

        await EnsureDefaultsAsync(tenantId, cancellationToken);

        var items = await _db.EhcTicketPriorityLevels
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Priority)
            .Select(p => new EhcTicketPriorityLevelDto
            {
                Priority = p.Priority,
                DisplayName = p.DisplayName,
                Description = p.Description,
                IsActive = p.IsActive,
                SortOrder = p.SortOrder
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpPut("{priority}")]
    public async Task<ActionResult> Update(string priority, [FromBody] UpdateEhcTicketPriorityLevelRequestDto request, CancellationToken cancellationToken)
    {
        request ??= new UpdateEhcTicketPriorityLevelRequestDto();

        var tenantId = TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        if (!Enum.TryParse<EhcTicketPriority>(priority, ignoreCase: true, out var parsed))
            return BadRequest(new { success = false, message = "Invalid priority. Use Low/Medium/High/Critical." });

        await EnsureDefaultsAsync(tenantId, cancellationToken);

        var entity = await _db.EhcTicketPriorityLevels
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.Priority == parsed, cancellationToken);

        if (entity == null)
            return NotFound(new { success = false, message = "Not found" });

        var displayName = (request.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return BadRequest(new { success = false, message = "DisplayName is required." });

        entity.DisplayName = displayName;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUserService.UserName;
        entity.LastModifiedById = CurrentUserId;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    private async Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await _db.EhcTicketPriorityLevels
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted)
            .Select(p => p.Priority)
            .ToListAsync(cancellationToken);

        var missing = DefaultLevels.Where(d => !existing.Contains(d.priority)).ToList();
        if (missing.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var d in missing)
        {
            _db.EhcTicketPriorityLevels.Add(new EhcTicketPriorityLevel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Priority = d.priority,
                DisplayName = d.name,
                Description = null,
                IsActive = true,
                SortOrder = d.sort,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName,
                CreatedById = CurrentUserId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
