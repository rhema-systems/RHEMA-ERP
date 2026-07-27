using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public partial class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISettingsService _settingsService;
    private readonly IProcurementSupplierApplicantAccessService _supplierApplicantAccess;

    public UserController(
        IUserService userService,
        ILogger<UserController> logger,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService,
        ISettingsService settingsService,
        IProcurementSupplierApplicantAccessService supplierApplicantAccess)
    {
        _userService = userService;
        _logger = logger;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
        _settingsService = settingsService;
        _supplierApplicantAccess = supplierApplicantAccess;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin + "," + Constants.Roles.Manager)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        try
        {
            var users = await _userService.GetAllUsersAsync();
            var userDtos = users.Select(u => new UserDto
            {
                Id = u.Id.ToString(),
                Username = u.UserName ?? "",
                Email = u.Email ?? "",
                FirstName = u.FirstName,
                LastName = u.LastName,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                EmployeeId = u.EmployeeId?.ToString(),
                Roles = u.UserRoles?.Select(ur => ur.Role.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray() ?? Array.Empty<string>(),
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginDate,
                TenantId = u.TenantId.ToString(),
                LinkedTenants = u.UserTenants?.Where(ut => !ut.IsDeleted).Select(ut => new UserTenantDto
                {
                    TenantId = ut.TenantId.ToString(),
                    TenantCode = ut.Tenant?.Code ?? "",
                    TenantName = ut.Tenant?.Name ?? "",
                    AccessLevel = ut.AccessLevel.ToString(),
                    Status = ut.Status.ToString(),
                    IsDefault = ut.IsDefault,
                    GrantedAt = ut.GrantedAt,
                    ExpiresAt = ut.ExpiresAt
                }).ToArray() ?? Array.Empty<UserTenantDto>()
            }).ToList();

            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, "An error occurred while retrieving users");
        }
    }

    /// <summary>
    /// Search users by query
    /// </summary>
    [HttpGet("search")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin + "," + Constants.Roles.Manager)]
    public async Task<ActionResult<IEnumerable<UserDto>>> SearchUsers([FromQuery] string? query = null)
    {
        try
        {
            var users = await _userService.GetAllUsersAsync();

            // Filter users based on query
            if (!string.IsNullOrWhiteSpace(query))
            {
                var lowercaseQuery = query.ToLowerInvariant();
                users = users.Where(u =>
                    (u.UserName?.ToLowerInvariant().Contains(lowercaseQuery, StringComparison.InvariantCultureIgnoreCase) == true) ||
                    (u.Email?.ToLowerInvariant().Contains(lowercaseQuery, StringComparison.InvariantCultureIgnoreCase) == true) ||
                    (u.FirstName?.ToLowerInvariant().Contains(lowercaseQuery, StringComparison.InvariantCultureIgnoreCase) == true) ||
                    (u.LastName?.ToLowerInvariant().Contains(lowercaseQuery, StringComparison.InvariantCultureIgnoreCase) == true) ||
                    ($"{u.FirstName} {u.LastName}".Contains(lowercaseQuery, StringComparison.InvariantCultureIgnoreCase))
                );
            }

            var userDtos = users.Select(u => new UserDto
            {
                Id = u.Id.ToString(),
                Username = u.UserName ?? "",
                Email = u.Email ?? "",
                FirstName = u.FirstName,
                LastName = u.LastName,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                EmployeeId = u.EmployeeId?.ToString(),
                Roles = u.UserRoles?.Select(ur => ur.Role.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray() ?? Array.Empty<string>(),
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginDate,
                TenantId = u.TenantId.ToString(),
                LinkedTenants = u.UserTenants?.Where(ut => !ut.IsDeleted).Select(ut => new UserTenantDto
                {
                    TenantId = ut.TenantId.ToString(),
                    TenantCode = ut.Tenant?.Code ?? "",
                    TenantName = ut.Tenant?.Name ?? "",
                    AccessLevel = ut.AccessLevel.ToString(),
                    Status = ut.Status.ToString(),
                    IsDefault = ut.IsDefault,
                    GrantedAt = ut.GrantedAt,
                    ExpiresAt = ut.ExpiresAt
                }).ToArray() ?? Array.Empty<UserTenantDto>()
            }).ToList();

            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users with query: {Query}", query);
            return StatusCode(500, "An error occurred while searching users");
        }
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound($"User with ID {id} not found");
            }

            var userDto = new UserDto
            {
                Id = user.Id.ToString(),
                Username = user.UserName ?? "",
                Email = user.Email ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                EmployeeId = user.EmployeeId?.ToString(),
                Roles = user.UserRoles?.Select(ur => ur.Role.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray() ?? Array.Empty<string>(),
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginDate,
                TenantId = user.TenantId.ToString(),
                LinkedTenants = user.UserTenants?.Where(ut => !ut.IsDeleted).Select(ut => new UserTenantDto
                {
                    TenantId = ut.TenantId.ToString(),
                    TenantCode = ut.Tenant?.Code ?? "",
                    TenantName = ut.Tenant?.Name ?? "",
                    AccessLevel = ut.AccessLevel.ToString(),
                    Status = ut.Status.ToString(),
                    IsDefault = ut.IsDefault,
                    GrantedAt = ut.GrantedAt,
                    ExpiresAt = ut.ExpiresAt
                }).ToArray() ?? Array.Empty<UserTenantDto>()
            };

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(500, "An error occurred while retrieving the user");
        }
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var user = new ApplicationUser
            {
                UserName = request.Username,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                FirstName = request.FirstName ?? string.Empty,
                LastName = request.LastName ?? string.Empty,
                IsActive = request.IsActive,
                TenantId = string.IsNullOrEmpty(request.TenantId) ? Guid.Empty : Guid.Parse(request.TenantId)
            };

            var createdUser = await _userService.CreateUserAsync(user, request.Password);

            // Add roles if specified
            if (request.Roles?.Any() == true)
            {
                await _userService.AddToRolesAsync(createdUser, request.Roles);
            }

            // Log audit trail for user creation
            try
            {
                var currentUserId = Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null;
                var currentUsername = _currentUserService.UserName;
                var currentTenantId = _currentUserService.TenantId;

                _logger.LogInformation("DEBUG: Attempting to log user creation. UserId: {UserId}, Username: {Username}, TenantId: {TenantId}",
                    currentUserId, currentUsername, currentTenantId);

                await _auditLogService.LogUserActionAsync(
                    currentUserId ?? Guid.Empty,
                    currentUsername ?? "Unknown",
                    "Create",
                    "User",
                    createdUser.Id.ToString(),
                    null,
                    new
                    {
                        Username = request.Username,
                        Email = request.Email,
                        FirstName = request.FirstName,
                        LastName = request.LastName,
                        IsActive = request.IsActive,
                        Roles = request.Roles,
                        TenantId = request.TenantId
                    },
                    GetClientIpAddress(),
                    GetUserAgent());

                _logger.LogInformation("DEBUG: User creation audit log completed successfully");
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to log audit trail for user creation. Details: {Details}", logEx.Message);
            }

            var userDto = new UserDto
            {
                Id = createdUser.Id.ToString(),
                Username = createdUser.UserName ?? "",
                Email = createdUser.Email ?? "",
                PhoneNumber = createdUser.PhoneNumber,
                FirstName = createdUser.FirstName,
                LastName = createdUser.LastName,
                IsActive = createdUser.IsActive,
                Roles = request.Roles ?? Array.Empty<string>(),
                CreatedAt = createdUser.CreatedAt,
                TenantId = createdUser.TenantId.ToString()
            };

            return CreatedAtAction(nameof(GetUser), new { id = createdUser.Id }, userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, "An error occurred while creating the user");
        }
    }

    /// <summary>
    /// Update user
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound($"User with ID {id} not found");
            }

            // Capture old values for audit logging
            var oldValues = new
            {
                Username = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                Roles = user.UserRoles?.Select(ur => ur.Role.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray() ?? Array.Empty<string>()
            };

            user.UserName = request.Username;
            user.Email = request.Email;
            user.PhoneNumber = request.PhoneNumber;
            user.FirstName = request.FirstName ?? string.Empty;
            user.LastName = request.LastName ?? string.Empty;
            user.IsActive = request.IsActive;

            var updatedUser = await _userService.UpdateUserAsync(user);

            // Update roles if specified
            if (request.Roles?.Any() == true)
            {
                await _userService.UpdateUserRolesAsync(updatedUser, request.Roles);
            }

            // Log audit trail for user update
            try
            {
                var newValues = new
                {
                    Username = request.Username,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    IsActive = request.IsActive,
                    Roles = request.Roles
                };

                await _auditLogService.LogUserActionAsync(
                    Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null ?? Guid.Empty,
                    _currentUserService.UserName ?? "Unknown",
                    "Update",
                    "User",
                    id.ToString(),
                    oldValues,
                    newValues,
                    GetClientIpAddress(),
                    GetUserAgent());
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log audit trail for user update");
            }

            var userDto = new UserDto
            {
                Id = updatedUser.Id.ToString(),
                Username = updatedUser.UserName ?? "",
                Email = updatedUser.Email ?? "",
                PhoneNumber = updatedUser.PhoneNumber,
                FirstName = updatedUser.FirstName,
                LastName = updatedUser.LastName,
                IsActive = updatedUser.IsActive,
                Roles = request.Roles ?? Array.Empty<string>(),
                CreatedAt = updatedUser.CreatedAt,
                TenantId = updatedUser.TenantId.ToString()
            };

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(500, "An error occurred while updating the user");
        }
    }

    /// <summary>
    /// Delete user
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = Constants.Roles.SuperAdmin)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        try
        {
            // Get user info for audit logging before deletion
            var userToDelete = await _userService.GetUserByIdAsync(id);
            if (userToDelete == null)
            {
                return NotFound($"User with ID {id} not found");
            }

            var success = await _userService.DeleteUserAsync(id);
            if (!success)
            {
                return StatusCode(500, "Failed to delete user");
            }

            // Log audit trail for user deletion
            try
            {
                await _auditLogService.LogUserActionAsync(
                    Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null ?? Guid.Empty,
                    _currentUserService.UserName ?? "Unknown",
                    "Delete",
                    "User",
                    id.ToString(),
                    new
                    {
                        Username = userToDelete.UserName,
                        Email = userToDelete.Email,
                        FirstName = userToDelete.FirstName,
                        LastName = userToDelete.LastName
                    },
                    null,
                    GetClientIpAddress(),
                    GetUserAgent());
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log audit trail for user deletion");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, "An error occurred while deleting the user");
        }
    }

    /// <summary>
    /// Update current user's profile (including tenant selection)
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var currentUserId = Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null;
            if (!currentUserId.HasValue)
            {
                return Unauthorized();
            }

            var user = await _userService.GetUserByIdAsync(currentUserId.Value);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Capture old values for audit logging
            var oldValues = new
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                TenantId = user.TenantId.ToString()
            };

            // Update profile fields
            if (!string.IsNullOrEmpty(request.FirstName))
            {
                user.FirstName = request.FirstName;
            }

            if (!string.IsNullOrEmpty(request.LastName))
            {
                user.LastName = request.LastName;
            }

            if (!string.IsNullOrEmpty(request.Email))
            {
                user.Email = request.Email;
            }

            if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                user.PhoneNumber = request.PhoneNumber;
            }

            // Handle tenant change if provided and user has permission
            if (request.TenantId.HasValue && request.TenantId != user.TenantId)
            {
                // Only allow tenant change for SuperAdmins or if it's a valid tenant assignment
                var currentUserRoles = await _userService.GetUserRolesAsync(user);
                if (currentUserRoles.Contains("SuperAdmin"))
                {
                    user.TenantId = request.TenantId.Value;
                }
                else
                {
                    return BadRequest("You do not have permission to change tenant assignment");
                }
            }

            var updatedUser = await _userService.UpdateUserAsync(user);

            // Log audit trail for profile update
            try
            {
                var newValues = new
                {
                    FirstName = updatedUser.FirstName,
                    LastName = updatedUser.LastName,
                    Email = updatedUser.Email,
                    TenantId = updatedUser.TenantId.ToString()
                };

                await _auditLogService.LogUserActionAsync(
                    currentUserId.Value,
                    _currentUserService.UserName ?? "Unknown",
                    "UpdateProfile",
                    "User",
                    currentUserId.Value.ToString(),
                    oldValues,
                    newValues,
                    GetClientIpAddress(),
                    GetUserAgent());

                _logger.LogInformation("DEBUG: User profile update audit log completed successfully");
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to log audit trail for profile update. Details: {Details}", logEx.Message);
            }

            var userDto = new UserDto
            {
                Id = updatedUser.Id.ToString(),
                Username = updatedUser.UserName ?? "",
                Email = updatedUser.Email ?? "",
                FirstName = updatedUser.FirstName,
                LastName = updatedUser.LastName,
                PhoneNumber = updatedUser.PhoneNumber,
                IsActive = updatedUser.IsActive,
                Roles = updatedUser.UserRoles?.Select(ur => ur.Role.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray() ?? Array.Empty<string>(),
                CreatedAt = updatedUser.CreatedAt,
                LastLoginAt = updatedUser.LastLoginDate,
                TenantId = updatedUser.TenantId.ToString()
            };

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return StatusCode(500, "An error occurred while updating the profile");
        }
    }

    /// <summary>
    /// Change current user's password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var currentUserId = Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null;
            if (!currentUserId.HasValue)
            {
                return Unauthorized();
            }

            var user = await _userService.GetUserByIdAsync(currentUserId.Value);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Verify current password
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash ?? string.Empty, request.CurrentPassword);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return BadRequest("Current password is incorrect");
            }

            // Validate new password against security policy
            var security = await _settingsService.GetSecuritySettingsAsync(user.TenantId);
            if (security != null)
            {
                var errors = new List<string>();

                // Validate minimum length
                if (request.NewPassword.Length < security.PasswordMinLength)
                {
                    errors.Add($"Password must be at least {security.PasswordMinLength} characters long");
                }

                // Validate uppercase requirement
                if (security.PasswordRequireUppercase && !MyRegex().IsMatch(request.NewPassword))
                {
                    errors.Add("Password must contain at least one uppercase letter");
                }

                // Validate lowercase requirement
                if (security.PasswordRequireLowercase && !System.Text.RegularExpressions.Regex.IsMatch(request.NewPassword, @"[a-z]"))
                {
                    errors.Add("Password must contain at least one lowercase letter");
                }

                // Validate digits requirement
                if (security.PasswordRequireDigits && !System.Text.RegularExpressions.Regex.IsMatch(request.NewPassword, @"[0-9]"))
                {
                    errors.Add("Password must contain at least one digit");
                }

                // Validate special characters requirement
                if (security.PasswordRequireSpecialChars && !System.Text.RegularExpressions.Regex.IsMatch(request.NewPassword, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
                {
                    errors.Add("Password must contain at least one special character");
                }

                if (errors.Any())
                {
                    return BadRequest(new { message = "Password does not meet policy requirements", errors = errors });
                }
            }

            // Hash new password
            user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
            user.MustChangePassword = false;
            user.TemporaryPasswordExpiresAtUtc = null;
            user.PasswordChangedAtUtc = DateTime.UtcNow;
            await _userService.UpdateUserAsync(user);
            await _supplierApplicantAccess.CompleteCredentialActivationAsync(
                user.Id,
                $"supplier-credential-activation-{user.Id:N}",
                HttpContext.RequestAborted);

            // Log audit trail for password change
            try
            {
                await _auditLogService.LogUserActionAsync(
                    currentUserId.Value,
                    _currentUserService.UserName ?? "Unknown",
                    "ChangePassword",
                    "User",
                    currentUserId.Value.ToString(),
                    null,
                    new { Message = "Password changed" },
                    GetClientIpAddress(),
                    GetUserAgent());
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log audit trail for password change");
            }

            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, "An error occurred while changing the password");
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

    [System.Text.RegularExpressions.GeneratedRegex(@"[A-Z]")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}

// DTOs
public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public string? EmployeeId { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? TenantId { get; set; }
    public UserTenantDto[] LinkedTenants { get; set; } = Array.Empty<UserTenantDto>();
}

public class UserTenantDto
{
    public string TenantId { get; set; } = string.Empty;
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string? TenantId { get; set; }
}

public class UpdateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
}

public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid? TenantId { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
