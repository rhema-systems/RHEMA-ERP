using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.TenantAdmin)]
public class UserEmployeeLinkController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly Services.IEmployeeLinkResolutionService _linkResolution;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<UserEmployeeLinkController> _logger;

    public UserEmployeeLinkController(
        UserManager<ApplicationUser> userManager,
        IEmployeeRepository employeeRepository,
        Services.IEmployeeLinkResolutionService linkResolution,
        ICurrentUserService currentUser,
        IAuditLogService auditLogService,
        ILogger<UserEmployeeLinkController> logger)
    {
        _userManager = userManager;
        _employeeRepository = employeeRepository;
        _linkResolution = linkResolution;
        _currentUser = currentUser;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [HttpPost("link-user-to-employee")]
    public async Task<IActionResult> LinkUserToEmployee([FromBody] LinkUserEmployeeRequest request)
    {
        var tenantId = ActiveTenantId();
        if (!tenantId.HasValue) return MissingTenant();

        var user = await FindTenantUserAsync(request.UserId, tenantId.Value);
        if (user == null) return NotFound("The user was not found in the active tenant.");

        var employee = await FindTenantEmployeeAsync(request.EmployeeId, tenantId.Value);
        if (employee == null) return NotFound("The employee was not found in the active tenant.");

        var existingUser = await TenantUsers(tenantId.Value)
            .FirstOrDefaultAsync(candidate => candidate.EmployeeId == request.EmployeeId && candidate.Id != user.Id);
        if (existingUser != null)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Employee already linked",
                detail: $"{employee.FullName} is already linked to {existingUser.UserName}.");
        }

        var previousEmployeeId = user.EmployeeId;
        user.EmployeeId = request.EmployeeId;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return IdentityFailure(result);

        await AuditAsync("Link", user, previousEmployeeId, request.EmployeeId);
        return Ok(new
        {
            success = true,
            message = $"Linked {user.UserName} to {employee.FullName}.",
            userId = user.Id,
            employeeId = employee.Id,
            employeeName = employee.FullName
        });
    }

    [HttpPost("unlink-user-from-employee")]
    public async Task<IActionResult> UnlinkUserFromEmployee([FromBody] UnlinkUserEmployeeRequest request)
    {
        var tenantId = ActiveTenantId();
        if (!tenantId.HasValue) return MissingTenant();

        var user = await FindTenantUserAsync(request.UserId, tenantId.Value);
        if (user == null) return NotFound("The user was not found in the active tenant.");
        if (!user.EmployeeId.HasValue)
        {
            return Ok(new { success = true, message = $"{user.UserName} is already unlinked.", userId = user.Id });
        }

        var previousEmployeeId = user.EmployeeId;
        user.EmployeeId = null;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return IdentityFailure(result);

        await AuditAsync("Unlink", user, previousEmployeeId, null);
        return Ok(new { success = true, message = $"Unlinked {user.UserName} from the employee record.", userId = user.Id });
    }

    [HttpGet("user-employee-links")]
    public async Task<IActionResult> GetUserEmployeeLinks()
    {
        var tenantId = ActiveTenantId();
        if (!tenantId.HasValue) return MissingTenant();

        var users = await TenantUsers(tenantId.Value)
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .ToListAsync();
        var employeeIds = users.Where(user => user.EmployeeId.HasValue)
            .Select(user => user.EmployeeId!.Value)
            .Distinct()
            .ToArray();
        var employees = employeeIds.Length == 0
            ? new Dictionary<Guid, Employee>()
            : await _employeeRepository.GetQueryable(employee =>
                    employee.TenantId == tenantId.Value && employeeIds.Contains(employee.Id))
                .AsNoTracking()
                .ToDictionaryAsync(employee => employee.Id);

        return Ok(users.Select(user =>
        {
            employees.TryGetValue(user.EmployeeId ?? Guid.Empty, out var employee);
            return new UserEmployeeLink
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                EmployeeId = employee?.Id,
                EmployeeName = employee?.FullName,
                EmployeeNumber = employee?.EmployeeNumber,
                IsLinked = employee != null
            };
        }));
    }

    [HttpGet("unlinked-users")]
    public async Task<IActionResult> GetUnlinkedUsers()
    {
        var tenantId = ActiveTenantId();
        if (!tenantId.HasValue) return MissingTenant();

        var users = await TenantUsers(tenantId.Value)
            .AsNoTracking()
            .Where(user => user.EmployeeId == null)
            .OrderBy(user => user.UserName)
            .ToListAsync();
        var rows = new List<UnlinkedUserRow>();
        foreach (var user in users)
        {
            rows.Add(new UnlinkedUserRow
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                AuthenticationProvider = user.AuthenticationProvider.ToString(),
                IsActive = user.IsActive,
                LastLoginDate = user.LastLoginDate,
                Suggestions = (await _linkResolution.SuggestForUserAsync(user)).ToList()
            });
        }

        return Ok(rows);
    }

    [HttpPost("bulk-link")]
    public async Task<IActionResult> BulkLink([FromBody] BulkLinkRequest request)
    {
        if (request.Links.Count == 0) return BadRequest("No links submitted.");
        var tenantId = ActiveTenantId();
        if (!tenantId.HasValue) return MissingTenant();

        var results = new List<BulkLinkResult>();
        foreach (var pair in request.Links.DistinctBy(link => link.UserId))
        {
            try
            {
                var user = await FindTenantUserAsync(pair.UserId, tenantId.Value);
                if (user == null) { results.Add(BulkLinkResult.Fail(pair, "User not found in the active tenant.")); continue; }
                if (user.EmployeeId.HasValue) { results.Add(BulkLinkResult.Fail(pair, "User is already linked.")); continue; }

                var employee = await FindTenantEmployeeAsync(pair.EmployeeId, tenantId.Value);
                if (employee == null) { results.Add(BulkLinkResult.Fail(pair, "Employee not found in the active tenant.")); continue; }
                if (await TenantUsers(tenantId.Value).AnyAsync(candidate => candidate.EmployeeId == pair.EmployeeId))
                {
                    results.Add(BulkLinkResult.Fail(pair, "Employee is already linked."));
                    continue;
                }

                user.EmployeeId = pair.EmployeeId;
                var update = await _userManager.UpdateAsync(user);
                if (!update.Succeeded)
                {
                    results.Add(BulkLinkResult.Fail(pair, string.Join(", ", update.Errors.Select(error => error.Description))));
                    continue;
                }

                await AuditAsync("BulkLink", user, null, pair.EmployeeId);
                results.Add(new BulkLinkResult
                {
                    UserId = pair.UserId,
                    EmployeeId = pair.EmployeeId,
                    Success = true,
                    Message = $"Linked {user.UserName} to {employee.FullName}."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk link failed for user {UserId} and employee {EmployeeId}", pair.UserId, pair.EmployeeId);
                results.Add(BulkLinkResult.Fail(pair, "Unexpected error."));
            }
        }

        return Ok(new
        {
            linked = results.Count(result => result.Success),
            failed = results.Count(result => !result.Success),
            results
        });
    }

    private Guid? ActiveTenantId() =>
        _currentUser.TenantId is { } tenantId && tenantId != Guid.Empty ? tenantId : null;

    private IQueryable<ApplicationUser> TenantUsers(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        return _userManager.Users.Where(user => user.TenantId == tenantId || user.UserTenants.Any(link =>
            !link.IsDeleted && link.TenantId == tenantId && link.Status == UserTenantStatus.Active &&
            (link.ExpiresAt == null || link.ExpiresAt > now)));
    }

    private Task<ApplicationUser?> FindTenantUserAsync(Guid userId, Guid tenantId) =>
        TenantUsers(tenantId).FirstOrDefaultAsync(user => user.Id == userId);

    private Task<Employee?> FindTenantEmployeeAsync(Guid employeeId, Guid tenantId) =>
        _employeeRepository.GetQueryable(employee => employee.Id == employeeId && employee.TenantId == tenantId)
            .FirstOrDefaultAsync();

    private async Task AuditAsync(string action, ApplicationUser user, Guid? previousEmployeeId, Guid? employeeId)
    {
        try
        {
            await _auditLogService.LogUserActionAsync(
                Guid.TryParse(_currentUser.UserId, out var actorId) ? actorId : Guid.Empty,
                _currentUser.UserName ?? "Unknown",
                action,
                "UserEmployeeLink",
                user.Id.ToString(),
                new { EmployeeId = previousEmployeeId },
                new { EmployeeId = employeeId },
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Request.Headers.UserAgent.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit record for user/employee link {UserId}", user.Id);
        }
    }

    private ObjectResult MissingTenant() => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Active tenant required",
        detail: "Select an active tenant before managing employee links.");

    private ObjectResult IdentityFailure(IdentityResult result) => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Identity update failed",
        detail: string.Join(" ", result.Errors.Select(error => error.Description)));
}
public class UnlinkedUserRow
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AuthenticationProvider { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public List<Services.LinkSuggestion> Suggestions { get; set; } = [];
}

public class BulkLinkRequest
{
    public List<LinkUserEmployeeRequest> Links { get; set; } = [];
}

public class BulkLinkResult
{
    public Guid UserId { get; set; }
    public Guid EmployeeId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static BulkLinkResult Fail(LinkUserEmployeeRequest pair, string message) =>
        new() { UserId = pair.UserId, EmployeeId = pair.EmployeeId, Success = false, Message = message };
}

public class LinkUserEmployeeRequest
{
    public Guid UserId { get; set; }
    public Guid EmployeeId { get; set; }
}

public class UnlinkUserEmployeeRequest
{
    public Guid UserId { get; set; }
}

public class UserEmployeeLink
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }
    public bool IsLinked { get; set; }
}
