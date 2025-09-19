using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Services;
using ErpSystem.Core.Entities;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly ILogger<RoleController> _logger;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;

    public RoleController(
        IRoleService roleService, 
        ILogger<RoleController> logger,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService)
    {
        _roleService = roleService;
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
            var roles = await _roleService.GetAllRolesAsync();
            var roleDtos = roles.Select(r => new RoleDto
            {
                Id = r.Id.ToString(),
                Name = r.Name,
                Description = r.Description,
                Permissions = Array.Empty<string>(), // Permissions managed separately
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
            var role = await _roleService.GetRoleByIdAsync(id);
            if (role == null)
            {
                return NotFound($"Role with ID {id} not found");
            }

            var roleDto = new RoleDto
            {
                Id = role.Id.ToString(),
                Name = role.Name,
                Description = role.Description,
                Permissions = Array.Empty<string>(), // Permissions managed separately
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
            var role = new ApplicationRole(request.Name)
            {
                Description = request.Description,
                IsSystemRole = false
            };
            
            // TODO: Handle permissions - for now we'll store them as a JSON string in description or use a separate entity

            var createdRole = await _roleService.CreateRoleAsync(role);

            // Log audit trail for role creation
            try
            {
                await _auditLogService.LogUserActionAsync(
                    _currentUserService.GetUserId() ?? Guid.Empty,
                    _currentUserService.GetUsername() ?? "Unknown",
                    "Create",
                    "Role",
                    createdRole.Id.ToString(),
                    null,
                    new { 
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

            var roleDto = new RoleDto
            {
                Id = createdRole.Id.ToString(),
                Name = createdRole.Name,
                Description = createdRole.Description,
                Permissions = Array.Empty<string>(), // Permissions managed separately
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

            // Don't allow modification of system roles
            if (role.IsSystemRole)
            {
                return BadRequest("System roles cannot be modified");
            }

            // Capture old values for audit logging
            var oldValues = new 
            {
                Name = role.Name,
                Description = role.Description,
                IsSystemRole = role.IsSystemRole
            };

            role.Name = request.Name;
            role.Description = request.Description;
            // Permissions are managed separately in this architecture

            var updatedRole = await _roleService.UpdateRoleAsync(role);

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
                    _currentUserService.GetUserId() ?? Guid.Empty,
                    _currentUserService.GetUsername() ?? "Unknown",
                    "Update",
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

            var roleDto = new RoleDto
            {
                Id = updatedRole.Id.ToString(),
                Name = updatedRole.Name,
                Description = updatedRole.Description,
                Permissions = Array.Empty<string>(), // Permissions managed separately
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

            // Don't allow deletion of system roles
            if (role.IsSystemRole)
            {
                return BadRequest("System roles cannot be deleted");
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
                    _currentUserService.GetUserId() ?? Guid.Empty,
                    _currentUserService.GetUsername() ?? "Unknown",
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