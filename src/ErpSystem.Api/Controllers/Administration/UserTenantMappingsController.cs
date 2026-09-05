using ErpSystem.Api.Models;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Administration;

[ApiController]
[Route("api/administration/user-tenant-mappings")]
[Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
public sealed class UserTenantMappingsController : ControllerBase
{
    private readonly IUserTenantService _userTenantService;
    private readonly ITenantService _tenantService;
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUser;

    public UserTenantMappingsController(
        IUserTenantService userTenantService,
        ITenantService tenantService,
        IUserService userService,
        ICurrentUserService currentUser)
    {
        _userTenantService = userTenantService;
        _tenantService = tenantService;
        _userService = userService;
        _currentUser = currentUser;
    }

    [HttpGet("{tenantId:guid}/users")]
    public async Task<IActionResult> GetUsers(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!CanManageTenant(tenantId)) return Forbid();

        var tenant = await _tenantService.GetTenantByIdAsync(tenantId);
        if (tenant is null || tenant.Status != TenantStatus.Active)
            return NotFound(Problem("USER_TENANT_TENANT_NOT_FOUND",
                "The target tenant was not found or is inactive."));

        var users = await _userTenantService.GetActiveTenantUsersAsync(tenantId);
        var mappings = new List<TenantUserMapping>();
        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relationship = await _userTenantService
                .GetUserTenantRelationshipAsync(user.Id, tenantId);
            if (relationship is not null)
                mappings.Add(Map(relationship, user));
        }

        return Ok(mappings);
    }

    [HttpPost]
    public async Task<IActionResult> Grant(
        [FromBody] SaveTenantUserMappingRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManageTenant(request.TenantId)) return Forbid();
        if (!TryGetActor(out _, out var actor)) return Forbid();
        if (request.UserId == Guid.Empty || request.TenantId == Guid.Empty)
            return Invalid("USER_TENANT_MAPPING_REQUIRED", "UserId and TenantId are required.");
        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTime.UtcNow)
            return Invalid("USER_TENANT_EXPIRY_INVALID", "ExpiresAt must be in the future.");

        var tenant = await _tenantService.GetTenantByIdAsync(request.TenantId);
        if (tenant is null || tenant.Status != TenantStatus.Active)
            return NotFound(Problem("USER_TENANT_TENANT_NOT_FOUND",
                "The target tenant was not found or is inactive."));
        var user = await _userService.GetUserByIdAsync(request.UserId);
        if (user is null || !user.IsActive)
            return NotFound(Problem("USER_TENANT_USER_NOT_FOUND",
                "The target user was not found or is inactive."));

        var relationship = await _userTenantService.GrantUserAccessToTenantAsync(
            request.UserId,
            request.TenantId,
            UserTenantAccessLevel.Standard,
            actor,
            request.ExpiresAt,
            string.IsNullOrWhiteSpace(request.Reason)
                ? "Granted through tenant administration."
                : request.Reason.Trim());

        return Ok(Map(relationship, user));
    }

    [HttpDelete("{tenantId:guid}/users/{userId:guid}")]
    public async Task<IActionResult> Revoke(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!CanManageTenant(tenantId)) return Forbid();
        if (!TryGetActor(out var actorId, out var actor)) return Forbid();
        if (userId == Guid.Empty || tenantId == Guid.Empty)
            return Invalid("USER_TENANT_MAPPING_REQUIRED", "UserId and TenantId are required.");
        if (userId == actorId)
            return ConflictResult(
                "USER_TENANT_SELF_REVOKE_PROHIBITED",
                "Administrators cannot revoke their own tenant access.");

        await _userTenantService.RevokeUserAccessFromTenantAsync(
            userId,
            tenantId,
            actor,
            "Revoked through tenant administration.");
        return NoContent();
    }

    private bool CanManageTenant(Guid tenantId) =>
        _currentUser.IsAuthenticated &&
        _currentUser.TenantId.HasValue &&
        _currentUser.TenantId.Value == tenantId;

    private bool TryGetActor(out Guid actorId, out string actor)
    {
        actor = string.IsNullOrWhiteSpace(_currentUser.UserName)
            ? _currentUser.FullName
            : _currentUser.UserName;
        return Guid.TryParse(_currentUser.UserId, out actorId) &&
               !string.IsNullOrWhiteSpace(actor);
    }

    private ObjectResult Invalid(string code, string detail)
    {
        var problem = Problem(code, detail);
        problem.Status = StatusCodes.Status422UnprocessableEntity;
        return UnprocessableEntity(problem);
    }

    private ObjectResult ConflictResult(string code, string detail)
    {
        var problem = Problem(code, detail);
        problem.Status = StatusCodes.Status409Conflict;
        return Conflict(problem);
    }

    private ProblemDetails Problem(string code, string detail) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "User tenant mapping failed",
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions = { ["code"] = code, ["correlationId"] = HttpContext.TraceIdentifier }
    };

    private static TenantUserMapping Map(UserTenant relationship, ApplicationUser user) => new()
    {
        UserId = user.Id.ToString(),
        TenantId = relationship.TenantId.ToString(),
        IsActive = relationship.Status == UserTenantStatus.Active,
        ExpiresAt = relationship.ExpiresAt?.ToString("O"),
        AccessLevel = relationship.AccessLevel.ToString(),
        IsDefault = relationship.IsDefault,
        GrantedAt = relationship.GrantedAt.ToString("O"),
        User = new TenantUserInfo
        {
            Id = user.Id.ToString(),
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            IsActive = user.IsActive
        }
    };
}
