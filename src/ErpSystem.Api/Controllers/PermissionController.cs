using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Data.Services;
using ErpSystem.Core.Services;
using ErpSystem.Core.Entities;
using ErpSystem.Shared;
using IPermissionService = ErpSystem.Data.Services.IPermissionService;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionController : ControllerBase
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionController> _logger;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;

    public PermissionController(
        IPermissionService permissionService,
        ILogger<PermissionController> logger,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService)
    {
        _permissionService = permissionService;
        _logger = logger;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all permissions
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<PermissionDto>>> GetPermissions()
    {
        try
        {
            var permissions = await _permissionService.GetAllPermissionsAsync();
            var permissionDtos = permissions.Select(p => new PermissionDto
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Category = p.Category,
                IsSystemPermission = p.IsSystemPermission,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();

            return Ok(permissionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions");
            return StatusCode(500, "An error occurred while retrieving permissions");
        }
    }

    /// <summary>
    /// Get permissions by category
    /// </summary>
    [HttpGet("category/{category}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<PermissionDto>>> GetPermissionsByCategory(string category)
    {
        try
        {
            var permissions = await _permissionService.GetPermissionsByCategoryAsync(category);
            var permissionDtos = permissions.Select(p => new PermissionDto
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Category = p.Category,
                IsSystemPermission = p.IsSystemPermission,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();

            return Ok(permissionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for category {Category}", category);
            return StatusCode(500, "An error occurred while retrieving permissions");
        }
    }

    /// <summary>
    /// Get permission categories
    /// </summary>
    [HttpGet("categories")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<string>>> GetPermissionCategories()
    {
        try
        {
            var categories = await _permissionService.GetPermissionCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permission categories");
            return StatusCode(500, "An error occurred while retrieving permission categories");
        }
    }

    /// <summary>
    /// Get permissions for a specific role
    /// </summary>
    [HttpGet("role/{roleId}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<PermissionDto>>> GetPermissionsForRole(Guid roleId)
    {
        try
        {
            var permissions = await _permissionService.GetPermissionsForRoleAsync(roleId);
            var permissionDtos = permissions.Select(p => new PermissionDto
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Category = p.Category,
                IsSystemPermission = p.IsSystemPermission,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();

            return Ok(permissionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permissions for role {RoleId}", roleId);
            return StatusCode(500, "An error occurred while retrieving permissions");
        }
    }

    /// <summary>
    /// Create a new permission
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<PermissionDto>> CreatePermission([FromBody] CreatePermissionRequest request)
    {
        try
        {
            var permission = new Permission
            {
                Name = request.Name,
                DisplayName = request.DisplayName,
                Description = request.Description,
                Category = request.Category,
                IsSystemPermission = false
            };

            var createdPermission = await _permissionService.CreatePermissionAsync(permission);

            // Log audit trail for permission creation
            try
            {
                await _auditLogService.LogUserActionAsync(
                    _currentUserService.GetUserId() ?? Guid.Empty,
                    _currentUserService.GetUsername() ?? "Unknown",
                    "Create",
                    "Permission",
                    createdPermission.Id.ToString(),
                    null,
                    new { 
                        Name = request.Name, 
                        DisplayName = request.DisplayName,
                        Category = request.Category
                    },
                    GetClientIpAddress(),
                    GetUserAgent());
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log audit trail for permission creation");
            }

            var permissionDto = new PermissionDto
            {
                Id = createdPermission.Id.ToString(),
                Name = createdPermission.Name,
                DisplayName = createdPermission.DisplayName,
                Description = createdPermission.Description,
                Category = createdPermission.Category,
                IsSystemPermission = createdPermission.IsSystemPermission,
                CreatedAt = createdPermission.CreatedAt,
                UpdatedAt = createdPermission.UpdatedAt
            };

            return CreatedAtAction(nameof(GetPermissions), new { id = createdPermission.Id }, permissionDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating permission");
            return StatusCode(500, "An error occurred while creating the permission");
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
public class PermissionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsSystemPermission { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreatePermissionRequest
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
}