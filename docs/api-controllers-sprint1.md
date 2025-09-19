# API Controllers & DTOs - Sprint 1 Authentication

## 📋 **DTOs for ErpSystem.Api**

### **Authentication DTOs**

```csharp path=null start=null
// ErpSystem.Api/DTOs/Auth/AuthenticationDto.cs
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.DTOs.Auth
{
    public class LoginRequestDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;
        
        [Required]
        public Guid TenantId { get; set; }
        
        public bool RememberMe { get; set; } = false;
    }

    public class LoginResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = null!;
        public TenantDto Tenant { get; set; } = null!;
    }

    public class RefreshTokenRequestDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequestDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public Guid TenantId { get; set; }
    }

    public class ResetPasswordRequestDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string Token { get; set; } = string.Empty;
        
        [Required, MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
        
        [Required]
        public Guid TenantId { get; set; }
    }
}
```

### **User Management DTOs**

```csharp path=null start=null
// ErpSystem.Api/DTOs/Users/UserDto.cs
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.DTOs.Users
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
    }

    public class CreateUserRequestDto
    {
        [Required, MaxLength(256)]
        public string UserName { get; set; } = string.Empty;
        
        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? FirstName { get; set; }
        
        [MaxLength(100)]
        public string? LastName { get; set; }
        
        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        
        public List<Guid> RoleIds { get; set; } = new();
        
        public bool SendWelcomeEmail { get; set; } = true;
    }

    public class UpdateUserRequestDto
    {
        [Required, MaxLength(256)]
        public string UserName { get; set; } = string.Empty;
        
        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? FirstName { get; set; }
        
        [MaxLength(100)]
        public string? LastName { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public List<Guid> RoleIds { get; set; } = new();
    }

    public class ChangePasswordRequestDto
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;
        
        [Required, MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UserListDto
    {
        public List<UserDto> Users { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
```

### **Role & Permission DTOs**

```csharp path=null start=null
// ErpSystem.Api/DTOs/Authorization/RoleDto.cs
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.DTOs.Authorization
{
    public class RoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystemRole { get; set; }
        public List<PermissionDto> Permissions { get; set; } = new();
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateRoleRequestDto
    {
        [Required, MaxLength(256)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        public List<Guid> PermissionIds { get; set; } = new();
    }

    public class UpdateRoleRequestDto
    {
        [Required, MaxLength(256)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        public List<Guid> PermissionIds { get; set; } = new();
    }

    public class PermissionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Module { get; set; } = string.Empty;
        public string? Category { get; set; }
        public bool IsSystemPermission { get; set; }
    }

    public class RolePermissionsDto
    {
        public List<PermissionGroupDto> PermissionGroups { get; set; } = new();
    }

    public class PermissionGroupDto
    {
        public string Module { get; set; } = string.Empty;
        public string? Category { get; set; }
        public List<PermissionDto> Permissions { get; set; } = new();
    }
}
```

### **Tenant DTOs**

```csharp path=null start=null
// ErpSystem.Api/DTOs/Tenants/TenantDto.cs
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.DTOs.Tenants
{
    public class TenantDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public string? SubscriptionPlan { get; set; }
        public DateTime? SubscriptionExpiry { get; set; }
        public int MaxUsers { get; set; }
        public int CurrentUserCount { get; set; }
        public bool IsExpired { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateTenantRequestDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [Required, MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;
        
        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? Domain { get; set; }
        
        [EmailAddress, MaxLength(255)]
        public string? ContactEmail { get; set; }
        
        [MaxLength(50)]
        public string? ContactPhone { get; set; }
        
        [MaxLength(500)]
        public string? Address { get; set; }
        
        [MaxLength(50)]
        public string? SubscriptionPlan { get; set; }
        
        public DateTime? SubscriptionExpiry { get; set; }
        
        public int MaxUsers { get; set; } = 100;
        
        // Admin user creation
        [Required, MaxLength(256)]
        public string AdminUserName { get; set; } = string.Empty;
        
        [Required, EmailAddress, MaxLength(256)]
        public string AdminEmail { get; set; } = string.Empty;
        
        [Required, MinLength(8)]
        public string AdminPassword { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? AdminFirstName { get; set; }
        
        [MaxLength(100)]
        public string? AdminLastName { get; set; }
    }

    public class UpdateTenantRequestDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [Required, MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? Domain { get; set; }
        
        [EmailAddress, MaxLength(255)]
        public string? ContactEmail { get; set; }
        
        [MaxLength(50)]
        public string? ContactPhone { get; set; }
        
        [MaxLength(500)]
        public string? Address { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        [MaxLength(50)]
        public string? SubscriptionPlan { get; set; }
        
        public DateTime? SubscriptionExpiry { get; set; }
        
        public int MaxUsers { get; set; } = 100;
    }

    public class TenantSelectionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Domain { get; set; }
        public bool IsActive { get; set; }
    }
}
```

---

## 🎮 **API Controllers**

### **Authentication Controller**

```csharp path=null start=null
// ErpSystem.Api/Controllers/AuthController.cs
using ErpSystem.Api.DTOs.Auth;
using ErpSystem.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthenticationService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _authService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString());
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Login failed for {Email}: {Message}", request.Email, ex.Message);
                return Unauthorized(new { message = "Invalid credentials" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Login failed for {Email}: {Message}", request.Email, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<LoginResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request.RefreshToken);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Invalid refresh token" });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (!string.IsNullOrEmpty(token))
            {
                await _authService.LogoutAsync(token);
            }
            return Ok(new { message = "Logged out successfully" });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            var userId = User.GetUserId();
            var user = await _authService.GetCurrentUserAsync(userId);
            return Ok(user);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            await _authService.ForgotPasswordAsync(request);
            return Ok(new { message = "Password reset instructions sent to your email" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            try
            {
                await _authService.ResetPasswordAsync(request);
                return Ok(new { message = "Password reset successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
```

### **Users Controller**

```csharp path=null start=null
// ErpSystem.Api/Controllers/UsersController.cs
using ErpSystem.Api.DTOs.Users;
using ErpSystem.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        [RequirePermission("users.view")]
        public async Task<ActionResult<UserListDto>> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null)
        {
            var result = await _userService.GetUsersAsync(page, pageSize, search, isActive);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [RequirePermission("users.view")]
        public async Task<ActionResult<UserDto>> GetUser(Guid id)
        {
            var user = await _userService.GetUserAsync(id);
            if (user == null)
                return NotFound();
                
            return Ok(user);
        }

        [HttpPost]
        [RequirePermission("users.create")]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequestDto request)
        {
            try
            {
                var user = await _userService.CreateUserAsync(request);
                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [RequirePermission("users.edit")]
        public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequestDto request)
        {
            try
            {
                var user = await _userService.UpdateUserAsync(id, request);
                return Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id}")]
        [RequirePermission("users.delete")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            try
            {
                await _userService.DeleteUserAsync(id);
                return NoContent();
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/change-password")]
        public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequestDto request)
        {
            // Users can only change their own password unless they have admin permissions
            var currentUserId = User.GetUserId();
            if (id != currentUserId && !User.HasPermission("users.edit"))
            {
                return Forbid();
            }

            try
            {
                await _userService.ChangePasswordAsync(id, request);
                return Ok(new { message = "Password changed successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("bulk")]
        [RequirePermission("users.create")]
        public async Task<IActionResult> BulkCreateUsers([FromBody] List<CreateUserRequestDto> requests)
        {
            var results = await _userService.BulkCreateUsersAsync(requests);
            return Ok(new { 
                successful = results.Count(r => r.Success),
                failed = results.Count(r => !r.Success),
                results = results 
            });
        }
    }
}
```

### **Roles Controller**

```csharp path=null start=null
// ErpSystem.Api/Controllers/RolesController.cs
using ErpSystem.Api.DTOs.Authorization;
using ErpSystem.Api.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;
        private readonly ILogger<RolesController> _logger;

        public RolesController(IRoleService roleService, ILogger<RolesController> logger)
        {
            _roleService = roleService;
            _logger = logger;
        }

        [HttpGet]
        [RequirePermission("roles.view")]
        public async Task<ActionResult<List<RoleDto>>> GetRoles()
        {
            var roles = await _roleService.GetRolesAsync();
            return Ok(roles);
        }

        [HttpGet("{id}")]
        [RequirePermission("roles.view")]
        public async Task<ActionResult<RoleDto>> GetRole(Guid id)
        {
            var role = await _roleService.GetRoleAsync(id);
            if (role == null)
                return NotFound();
                
            return Ok(role);
        }

        [HttpPost]
        [RequirePermission("roles.create")]
        public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequestDto request)
        {
            try
            {
                var role = await _roleService.CreateRoleAsync(request);
                return CreatedAtAction(nameof(GetRole), new { id = role.Id }, role);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [RequirePermission("roles.edit")]
        public async Task<ActionResult<RoleDto>> UpdateRole(Guid id, [FromBody] UpdateRoleRequestDto request)
        {
            try
            {
                var role = await _roleService.UpdateRoleAsync(id, request);
                return Ok(role);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id}")]
        [RequirePermission("roles.delete")]
        public async Task<IActionResult> DeleteRole(Guid id)
        {
            try
            {
                await _roleService.DeleteRoleAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
        }

        [HttpGet("permissions")]
        [RequirePermission("roles.view")]
        public async Task<ActionResult<RolePermissionsDto>> GetAvailablePermissions()
        {
            var permissions = await _roleService.GetAvailablePermissionsAsync();
            return Ok(permissions);
        }
    }
}
```

### **Tenants Controller**

```csharp path=null start=null
// ErpSystem.Api/Controllers/TenantsController.cs
using ErpSystem.Api.DTOs.Tenants;
using ErpSystem.Api.Services.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly ILogger<TenantsController> _logger;

        public TenantsController(ITenantService tenantService, ILogger<TenantsController> logger)
        {
            _tenantService = tenantService;
            _logger = logger;
        }

        [HttpGet("selection")]
        public async Task<ActionResult<List<TenantSelectionDto>>> GetTenantsForSelection()
        {
            var tenants = await _tenantService.GetActiveTenantsAsync();
            return Ok(tenants);
        }

        [HttpGet]
        [Authorize]
        [RequirePermission("tenants.view")]
        public async Task<ActionResult<List<TenantDto>>> GetTenants()
        {
            var tenants = await _tenantService.GetTenantsAsync();
            return Ok(tenants);
        }

        [HttpGet("{id}")]
        [Authorize]
        [RequirePermission("tenants.view")]
        public async Task<ActionResult<TenantDto>> GetTenant(Guid id)
        {
            var tenant = await _tenantService.GetTenantAsync(id);
            if (tenant == null)
                return NotFound();
                
            return Ok(tenant);
        }

        [HttpPost]
        [Authorize]
        [RequirePermission("tenants.create")]
        public async Task<ActionResult<TenantDto>> CreateTenant([FromBody] CreateTenantRequestDto request)
        {
            try
            {
                var tenant = await _tenantService.CreateTenantAsync(request);
                return CreatedAtAction(nameof(GetTenant), new { id = tenant.Id }, tenant);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        [RequirePermission("tenants.edit")]
        public async Task<ActionResult<TenantDto>> UpdateTenant(Guid id, [FromBody] UpdateTenantRequestDto request)
        {
            try
            {
                var tenant = await _tenantService.UpdateTenantAsync(id, request);
                return Ok(tenant);
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        [RequirePermission("tenants.delete")]
        public async Task<IActionResult> DeleteTenant(Guid id)
        {
            try
            {
                await _tenantService.DeleteTenantAsync(id);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
        }
    }
}
```

---

## 🛡️ **Authorization Attributes**

```csharp path=null start=null
// ErpSystem.Api/Attributes/RequirePermissionAttribute.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpSystem.Api.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _permission;

        public RequirePermissionAttribute(string permission)
        {
            _permission = permission;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (!context.HttpContext.User.HasPermission(_permission))
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
```

---

## 🔧 **Extension Methods**

```csharp path=null start=null
// ErpSystem.Api/Extensions/ClaimsPrincipalExtensions.cs
using System.Security.Claims;

namespace ErpSystem.Api.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        public static Guid GetTenantId(this ClaimsPrincipal user)
        {
            var tenantIdClaim = user.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : Guid.Empty;
        }

        public static string? GetUserName(this ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.Name)?.Value;
        }

        public static string? GetEmail(this ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.Email)?.Value;
        }

        public static IEnumerable<string> GetRoles(this ClaimsPrincipal user)
        {
            return user.FindAll(ClaimTypes.Role).Select(c => c.Value);
        }

        public static IEnumerable<string> GetPermissions(this ClaimsPrincipal user)
        {
            return user.FindAll("Permission").Select(c => c.Value);
        }

        public static bool HasPermission(this ClaimsPrincipal user, string permission)
        {
            return user.GetPermissions().Contains(permission);
        }

        public static bool HasRole(this ClaimsPrincipal user, string role)
        {
            return user.GetRoles().Contains(role);
        }
    }
}
```

---

## 📋 **Next Steps**

1. **Implement the service interfaces** referenced in the controllers
2. **Create the tenant context middleware** for automatic data isolation
3. **Set up JWT authentication configuration**
4. **Add AutoMapper profiles** for DTO mapping
5. **Implement input validation** and error handling middleware

Would you like me to create the service implementations and tenant middleware next? 🚀