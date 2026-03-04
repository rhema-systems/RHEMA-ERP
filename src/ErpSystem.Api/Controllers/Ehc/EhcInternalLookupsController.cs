using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/lookups")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin + "," +
    Constants.Roles.Employee)]
public sealed class EhcInternalLookupsController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUserService;

    public EhcInternalLookupsController(
        ErpSystem.Data.ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _userManager = userManager;
        _currentUserService = currentUserService;
    }

    [HttpGet("priorities")]
    public async Task<ActionResult> GetPriorities(CancellationToken cancellationToken)
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

    [HttpGet("users")]
    public async Task<ActionResult> SearchUsers([FromQuery] string? q = null, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        q = (q ?? string.Empty).Trim();
        if (q.Length < 2)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        var query = _userManager.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive);

        var term = q.ToUpperInvariant();
        query = query.Where(u =>
            (u.NormalizedEmail != null && u.NormalizedEmail.Contains(term)) ||
            (u.FirstName != null && u.FirstName.ToUpper().Contains(term)) ||
            (u.LastName != null && u.LastName.ToUpper().Contains(term)));

        var items = await query
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Take(limit)
            .Select(u => new
            {
                id = u.Id,
                name = (u.FirstName + " " + u.LastName).Trim(),
                email = u.Email
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpGet("problems")]
    public async Task<ActionResult> SearchProblems(
        [FromServices] ErpSystem.Core.Interfaces.Ehc.IEhcProblemService problemService,
        [FromQuery] string? q = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        q = (q ?? string.Empty).Trim();
        if (q.Length < 2)
            return Ok(new { success = true, data = Array.Empty<object>() });

        try
        {
            var result = await problemService.SearchProblemsAsync(q, limit, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }
    }

    [HttpGet("tickets")]
    public async Task<ActionResult> SearchTickets([FromQuery] string? q = null, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        q = (q ?? string.Empty).Trim();
        if (q.Length < 2)
        {
            return Ok(new { success = true, data = Array.Empty<object>() });
        }

        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        var term = q.ToUpperInvariant();

        var query = _db.Set<EhcTicket>()
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        query = query.Where(t =>
            t.TicketNumber.ToUpper().Contains(term) ||
            (t.Subject != null && t.Subject.ToUpper().Contains(term)));

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new
            {
                id = t.Id,
                ticketNumber = t.TicketNumber,
                subject = t.Subject,
                status = t.Status,
                priority = t.Priority,
                createdAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpGet("departments")]
    public async Task<ActionResult> GetDepartments(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcLookupItemDto>() });
        }

        var items = await _db.Set<Department>()
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new EhcLookupItemDto { Id = d.Id, Name = d.Name })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpGet("agents")]
    public async Task<ActionResult> GetAgents(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcLookupItemDto>() });
        }

        var agents = await _userManager.GetUsersInRoleAsync(Constants.Roles.HelpdeskAgent);
        var items = agents
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new EhcLookupItemDto
            {
                Id = u.Id,
                Name = $"{u.FirstName} {u.LastName}".Trim()
            })
            .ToList();

        return Ok(new { success = true, data = items });
    }

    [HttpGet("my-department")]
    public async Task<ActionResult> GetMyDepartment(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = (EhcLookupItemDto?)null });
        }

        var employeeId = _currentUserService.EmployeeId;
        if (!employeeId.HasValue || employeeId.Value == Guid.Empty)
        {
            return Ok(new { success = true, data = (EhcLookupItemDto?)null });
        }

        var employee = await _db.Set<Employee>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId.Value && e.TenantId == tenantId && !e.IsDeleted && e.IsActive, cancellationToken);

        if (employee == null || employee.DepartmentId == Guid.Empty)
        {
            return Ok(new { success = true, data = (EhcLookupItemDto?)null });
        }

        var department = await _db.Set<Department>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == employee.DepartmentId && d.TenantId == tenantId && !d.IsDeleted && d.IsActive, cancellationToken);

        if (department == null)
        {
            return Ok(new { success = true, data = (EhcLookupItemDto?)null });
        }

        return Ok(new { success = true, data = new EhcLookupItemDto { Id = department.Id, Name = department.Name } });
    }

    [HttpGet("root-causes")]
    public async Task<ActionResult> GetRootCauses(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcRootCauseCodeDto>() });
        }

        var items = await _db.Set<EhcRootCauseCode>()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new EhcRootCauseCodeDto
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name,
                Description = r.Description,
                IsActive = r.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }

    [HttpGet("canned-responses")]
    public async Task<ActionResult> GetCannedResponses([FromQuery] EhcTicketType? ticketType = null, [FromQuery] Guid? categoryId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcCannedResponseDto>() });
        }

        var q = _db.Set<EhcCannedResponse>()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted && r.IsActive);

        if (ticketType.HasValue)
        {
            q = q.Where(r => r.AppliesToType == null || r.AppliesToType == ticketType.Value);
        }

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            q = q.Where(r => r.CategoryId == null || r.CategoryId == categoryId.Value);
        }

        var items = await q
            .OrderBy(r => r.Code)
            .Select(r => new EhcCannedResponseDto
            {
                Id = r.Id,
                Code = r.Code,
                Title = r.Title,
                Body = r.Body,
                IsActive = r.IsActive,
                AppliesToType = r.AppliesToType,
                CategoryId = r.CategoryId
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = items });
    }
}
