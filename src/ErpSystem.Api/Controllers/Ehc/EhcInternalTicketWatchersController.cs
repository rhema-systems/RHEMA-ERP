using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/tickets/{ticketId:guid}/watchers")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin + "," +
    Constants.Roles.Employee)]
public sealed class EhcInternalTicketWatchersController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;

    public EhcInternalTicketWatchersController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _currentUserService = currentUserService;
        _userManager = userManager;
    }

    public sealed class UpsertWatcherRequest
    {
        public Guid UserId { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult> List(Guid ticketId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = Array.Empty<object>() });

        var exists = await _db.EhcTickets.AsNoTracking().AnyAsync(t => t.Id == ticketId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);
        if (!exists)
            return NotFound(new { success = false, message = "Ticket not found" });

        var watchers = await _db.EhcTicketWatchers
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && !w.IsDeleted && w.TicketId == ticketId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new { w.UserId })
            .ToListAsync(cancellationToken);

        var userIds = watchers.Select(w => w.UserId).Where(id => id != Guid.Empty).Distinct().ToList();
        if (userIds.Count == 0)
            return Ok(new { success = true, data = Array.Empty<object>() });

        var users = await _userManager.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.TenantId == tenantId && u.IsActive)
            .Select(u => new
            {
                id = u.Id,
                name = $"{u.FirstName} {u.LastName}".Trim(),
                email = u.Email
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = users });
    }

    [HttpGet("me")]
    public async Task<ActionResult> GetMe(Guid ticketId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return Ok(new { success = true, data = new { isWatching = false } });

        if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
            return Ok(new { success = true, data = new { isWatching = false } });

        var exists = await _db.EhcTicketWatchers
            .AsNoTracking()
            .AnyAsync(w => w.TenantId == tenantId && !w.IsDeleted && w.TicketId == ticketId && w.UserId == userId, cancellationToken);

        return Ok(new { success = true, data = new { isWatching = exists } });
    }

    [HttpPost("me")]
    public async Task<ActionResult> WatchMe(Guid ticketId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
            return BadRequest(new { success = false, message = "User context is required." });

        var ticketExists = await _db.EhcTickets.AsNoTracking().AnyAsync(t => t.Id == ticketId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);
        if (!ticketExists)
            return NotFound(new { success = false, message = "Ticket not found" });

        var existing = await _db.EhcTicketWatchers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.TicketId == ticketId && w.UserId == userId, cancellationToken);

        if (existing != null)
        {
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedBy = null;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = _currentUserService.UserName;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return Ok(new { success = true, data = new { isWatching = true } });
        }

        _db.EhcTicketWatchers.Add(new EhcTicketWatcher
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketId = ticketId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName,
            CreatedById = userId
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = new { isWatching = true } });
    }

    [HttpPost]
    public async Task<ActionResult> AddWatcher(Guid ticketId, [FromBody] UpsertWatcherRequest request, CancellationToken cancellationToken)
    {
        request ??= new UpsertWatcherRequest();

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
            return BadRequest(new { success = false, message = "User context is required." });

        if (request.UserId == Guid.Empty)
            return BadRequest(new { success = false, message = "UserId is required." });

        var ticketExists = await _db.EhcTickets.AsNoTracking().AnyAsync(t => t.Id == ticketId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);
        if (!ticketExists)
            return NotFound(new { success = false, message = "Ticket not found" });

        var userExists = await _userManager.Users.AsNoTracking().AnyAsync(u => u.Id == request.UserId && u.TenantId == tenantId && u.IsActive, cancellationToken);
        if (!userExists)
            return BadRequest(new { success = false, message = "User not found" });

        var existing = await _db.EhcTicketWatchers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.TicketId == ticketId && w.UserId == request.UserId, cancellationToken);

        var now = DateTime.UtcNow;
        if (existing != null)
        {
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedBy = null;
            }

            existing.UpdatedAt = now;
            existing.UpdatedBy = _currentUserService.UserName;
            existing.LastModifiedById = actorUserId;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }

        _db.EhcTicketWatchers.Add(new EhcTicketWatcher
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketId = ticketId,
            UserId = request.UserId,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = actorUserId
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpDelete("me")]
    public async Task<ActionResult> UnwatchMe(Guid ticketId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
            return BadRequest(new { success = false, message = "User context is required." });

        var existing = await _db.EhcTicketWatchers
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && !w.IsDeleted && w.TicketId == ticketId && w.UserId == userId, cancellationToken);

        if (existing == null)
            return Ok(new { success = true, data = new { isWatching = false } });

        existing.IsDeleted = true;
        existing.DeletedAt = DateTime.UtcNow;
        existing.DeletedBy = _currentUserService.UserName;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = _currentUserService.UserName;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = new { isWatching = false } });
    }

    [HttpDelete("{userId:guid}")]
    public async Task<ActionResult> RemoveWatcher(Guid ticketId, Guid userId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            return BadRequest(new { success = false, message = "Tenant context is required." });

        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
            return BadRequest(new { success = false, message = "User context is required." });

        var existing = await _db.EhcTicketWatchers
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && !w.IsDeleted && w.TicketId == ticketId && w.UserId == userId, cancellationToken);

        if (existing == null)
            return Ok(new { success = true });

        existing.IsDeleted = true;
        existing.DeletedAt = DateTime.UtcNow;
        existing.DeletedBy = _currentUserService.UserName;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = _currentUserService.UserName;
        existing.LastModifiedById = actorUserId;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }
}
