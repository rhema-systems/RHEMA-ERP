using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Data.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IPermissionService = ErpSystem.Data.Services.IPermissionService;
using IRolePermissionService = ErpSystem.Data.Services.IRolePermissionService;
namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly IPermissionService _permissionService;
    private readonly IRolePermissionService _rolePermissionService;
    private readonly ILogger<RoleController> _logger;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;

    public RoleController(
        IRoleService roleService,
        IPermissionService permissionService,
        IRolePermissionService rolePermissionService,
        ILogger<RoleController> logger,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService)
    {
        _roleService = roleService;
        _permissionService = permissionService;
        _rolePermissionService = rolePermissionService;
        _logger = logger;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all roles
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetRoles()
    {
        try
        {
            var roles = await _rolePermissionService.GetAllRolesWithPermissionsAsync();
            var roleDtos = roles.Select(r => new RoleDto
            {
                Id = r.Id.ToString(),
                Name = r.Name ?? string.Empty,
                Description = r.Description ?? string.Empty,
                Permissions = r.RolePermissions.Select(rp => rp.Permission.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray(),
                IsSystemRole = r.IsSystemRole,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Ok(roleDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles");
            return StatusCode(500, "An error occurred while retrieving roles");
        }
    }

    /// <summary>
    /// Get role by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<RoleDto>> GetRole(Guid id)
    {
        try
        {
            var role = await _rolePermissionService.GetRoleWithPermissionsByIdAsync(id);
            if (role == null)
            {
                return NotFound($"Role with ID {id} not found");
            }

            var roleDto = new RoleDto
            {
                Id = role.Id.ToString(),
                Name = role.Name ?? string.Empty,
                Description = role.Description ?? string.Empty,
                Permissions = role.RolePermissions.Select(rp => rp.Permission.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray(),
                IsSystemRole = role.IsSystemRole,
                CreatedAt = role.CreatedAt,
                UpdatedAt = role.UpdatedAt
            };

            return Ok(roleDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleId}", id);
            return StatusCode(500, "An error occurred while retrieving the role");
        }
    }

    /// <summary>
    /// Create a new role
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest request)
    {
        try
        {
            if (Constants.Roles.IsProtectedSystemRole(request.Name))
            {
                return BadRequest($"'{request.Name}' is reserved for a protected system role and cannot be created from role management.");
            }

            var role = new ApplicationRole(request.Name)
            {
                Description = request.Description,
                IsSystemRole = false
            };

            var createdRole = await _roleService.CreateRoleAsync(role);

            // Handle permissions
            if (request.Permissions != null && request.Permissions.Length > 0)
            {
                var permissions = await _permissionService.GetAllPermissionsAsync();
                var permissionIds = permissions
                    .Where(p => request.Permissions.Contains(p.Name))
                    .Select(p => p.Id)
                    .ToList();

                if (permissionIds.Any())
                {
                    await _permissionService.UpdateRolePermissionsAsync(
                        createdRole.Id,
                        permissionIds,
                        _currentUserService.UserName);
                }
            }

            // Log audit trail for role creation
            try
            {
                await _auditLogService.LogUserActionAsync(
                    Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null ?? Guid.Empty,
                    _currentUserService.UserName ?? "Unknown",
                    "Create",
                    "Role",
                    createdRole.Id.ToString(),
                    null,
                    new
                    {
                        Name = request.Name,
                        Description = request.Description,
                        Permissions = request.Permissions
                    },
                    GetClientIpAddress(),
                    GetUserAgent());
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log audit trail for role creation");
            }

            // Get the created role with permissions
            var roleWithPermissions = await _rolePermissionService.GetRoleWithPermissionsByIdAsync(createdRole.Id);

            var roleDto = new RoleDto
            {
                Id = createdRole.Id.ToString(),
                Name = createdRole.Name ?? string.Empty,
                Description = createdRole.Description ?? string.Empty,
                Permissions = roleWithPermissions?.RolePermissions.Select(rp => rp.Permission.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray() ?? Array.Empty<string>(),
                IsSystemRole = createdRole.IsSystemRole,
                CreatedAt = createdRole.CreatedAt,
                UpdatedAt = createdRole.UpdatedAt
            };

            return CreatedAtAction(nameof(GetRole), new { id = createdRole.Id }, roleDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role");
            return StatusCode(500, "An error occurred while creating the role");
        }
    }

    /// <summary>
    /// Update role
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<RoleDto>> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        try
        {
            var role = await _roleService.GetRoleByIdAsync(id);
            if (role == null)
            {
                return NotFound($"Role with ID {id} not found");
            }

            var isProtectedSystemRole =
                role.IsSystemRole || Constants.Roles.IsProtectedSystemRole(role.Name);
            if (isProtectedSystemRole &&
                (!string.Equals(
                        (request.Name ?? string.Empty).Trim(),
                        role.Name,
                        StringComparison.Ordinal) ||
                 !string.Equals(
                        (request.Description ?? string.Empty).Trim(),
                        (role.Description ?? string.Empty).Trim(),
                        StringComparison.Ordinal)))
            {
                return BadRequest(
                    $"'{role.Name}' is a protected system role. Its name and description are locked, but its permission assignments can be updated.");
            }

            if (!isProtectedSystemRole &&
                Constants.Roles.IsProtectedSystemRole(request.Name))
            {
                return BadRequest($"'{request.Name}' is reserved for a protected system role and cannot be assigned to a custom role.");
            }

            if (!isProtectedSystemRole && string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Role name is required.");
            }

            var currentRoleWithPermissions =
                await _rolePermissionService.GetRoleWithPermissionsByIdAsync(id);
            var oldPermissions = currentRoleWithPermissions?.RolePermissions
                .Select(item => item.Permission.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];

            // Capture old values for audit logging
            var oldValues = new
            {
                Name = role.Name,
                Description = role.Description,
                IsSystemRole = role.IsSystemRole,
                Permissions = oldPermissions
            };

            var requestedPermissionNames = (request.Permissions ?? [])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var permissions = (await _permissionService.GetAllPermissionsAsync())
                .GroupBy(permission => permission.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);
            var unknownPermissions = requestedPermissionNames
                .Where(name => !permissions.ContainsKey(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (unknownPermissions.Length > 0)
            {
                return BadRequest(new
                {
                    Message = "One or more selected permissions do not exist.",
                    UnknownPermissions = unknownPermissions
                });
            }

            var updatedRole = role;
            if (!isProtectedSystemRole)
            {
                role.Name = request.Name.Trim();
                role.Description = request.Description;
                updatedRole = await _roleService.UpdateRoleAsync(role);
            }

            await _permissionService.UpdateRolePermissionsAsync(
                updatedRole.Id,
                requestedPermissionNames.Select(name => permissions[name].Id),
                _currentUserService.UserName);

            // Log audit trail for role update
            try
            {
                var newValues = new
                {
                    Name = request.Name,
                    Description = request.Description,
                    Permissions = request.Permissions
                };

                await _auditLogService.LogUserActionAsync(
                    Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null ?? Guid.Empty,
                    _currentUserService.UserName ?? "Unknown",
                    isProtectedSystemRole ? "UpdatePermissions" : "Update",
                    "Role",
                    id.ToString(),
                    oldValues,
                    newValues,
                    GetClientIpAddress(),
                    GetUserAgent());
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log audit trail for role update");
            }

            // Get the updated role with permissions
            var roleWithPermissions = await _rolePermissionService.GetRoleWithPermissionsByIdAsync(updatedRole.Id);

            var roleDto = new RoleDto
            {
                Id = updatedRole.Id.ToString(),
                Name = updatedRole.Name ?? string.Empty,
                Description = updatedRole.Description ?? string.Empty,
                Permissions = roleWithPermissions?.RolePermissions.Select(rp => rp.Permission.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray() ?? Array.Empty<string>(),
                IsSystemRole = updatedRole.IsSystemRole,
                CreatedAt = updatedRole.CreatedAt,
                UpdatedAt = updatedRole.UpdatedAt
            };

            return Ok(roleDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", id);
            return StatusCode(500, "An error occurred while updating the role");
        }
    }

    /// <summary>
    /// Delete role
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Constants.Roles.SuperAdmin)]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        try
        {
            var role = await _roleService.GetRoleByIdAsync(id);
            if (role == null)
            {
                return NotFound($"Role with ID {id} not found");
            }

            if (role.IsSystemRole || Constants.Roles.IsProtectedSystemRole(role.Name))
            {
                return BadRequest($"'{role.Name}' is a protected system role and cannot be deleted.");
            }

            var success = await _roleService.DeleteRoleAsync(id);
            if (!success)
            {
                return StatusCode(500, "Failed to delete role");
            }

            // Log audit trail for role deletion
            try
            {
                await _auditLogService.LogUserActionAsync(
                    Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null ?? Guid.Empty,
                    _currentUserService.UserName ?? "Unknown",
                    "Delete",
                    "Role",
                    id.ToString(),
                    new { Name = role.Name, Description = role.Description },
                    null,
                    GetClientIpAddress(),
                    GetUserAgent());
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log audit trail for role deletion");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", id);
            return StatusCode(500, "An error occurred while deleting the role");
        }
    }

    /// <summary>
    /// Helper method to get client IP address
    /// </summary>
    private string GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Helper method to get user agent
    /// </summary>
    private string GetUserAgent()
    {
        return HttpContext.Request.Headers["User-Agent"].ToString();
    }
}

// DTOs
public class RoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public bool IsSystemRole { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Permissions { get; set; } = Array.Empty<string>();
}

public class UpdateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Permissions { get; set; } = Array.Empty<string>();
}
