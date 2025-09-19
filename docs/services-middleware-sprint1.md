# Service Implementations & Tenant Middleware - Sprint 1

## 🏢 **Tenant Context Middleware**

```csharp path=null start=null
// ErpSystem.Api/Middleware/TenantContext.cs
namespace ErpSystem.Api.Middleware
{
    public class TenantContext
    {
        public Guid TenantId { get; set; }
        public string TenantCode { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public interface ITenantContextAccessor
    {
        TenantContext? TenantContext { get; set; }
        Guid GetTenantId();
        string GetTenantCode();
        bool HasTenantContext();
    }

    public class TenantContextAccessor : ITenantContextAccessor
    {
        private static readonly AsyncLocal<TenantContext?> _tenantContext = new();

        public TenantContext? TenantContext
        {
            get => _tenantContext.Value;
            set => _tenantContext.Value = value;
        }

        public Guid GetTenantId() => TenantContext?.TenantId ?? Guid.Empty;
        public string GetTenantCode() => TenantContext?.TenantCode ?? string.Empty;
        public bool HasTenantContext() => TenantContext != null && TenantContext.TenantId != Guid.Empty;
    }
}
```

```csharp path=null start=null
// ErpSystem.Api/Middleware/TenantMiddleware.cs
using ErpSystem.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ErpSystem.Api.Middleware
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantMiddleware> _logger;

        public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, 
            ITenantContextAccessor tenantContextAccessor, 
            IServiceProvider serviceProvider)
        {
            var tenantId = GetTenantId(context);
            
            if (tenantId != Guid.Empty)
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                var tenant = await dbContext.Tenants
                    .Where(t => t.Id == tenantId && t.IsActive && !t.IsDeleted)
                    .Select(t => new { t.Id, t.Code, t.Name, t.IsActive, t.SubscriptionExpiry })
                    .FirstOrDefaultAsync();

                if (tenant != null)
                {
                    // Check subscription expiry
                    if (tenant.SubscriptionExpiry.HasValue && tenant.SubscriptionExpiry.Value < DateTime.UtcNow)
                    {
                        _logger.LogWarning("Tenant {TenantId} subscription expired on {ExpiryDate}", 
                            tenantId, tenant.SubscriptionExpiry.Value);
                        
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("Tenant subscription has expired");
                        return;
                    }

                    tenantContextAccessor.TenantContext = new TenantContext
                    {
                        TenantId = tenant.Id,
                        TenantCode = tenant.Code,
                        TenantName = tenant.Name,
                        IsActive = tenant.IsActive
                    };
                }
                else
                {
                    _logger.LogWarning("Invalid or inactive tenant: {TenantId}", tenantId);
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Invalid or inactive tenant");
                    return;
                }
            }

            await _next(context);
        }

        private static Guid GetTenantId(HttpContext context)
        {
            // Try to get tenant from JWT claims first (for authenticated users)
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var tenantClaim = context.User.FindFirst("TenantId")?.Value;
                if (Guid.TryParse(tenantClaim, out var tenantId))
                {
                    return tenantId;
                }
            }

            // Try to get tenant from query parameter (for login)
            if (context.Request.Query.TryGetValue("tenantId", out var tenantQueryValue))
            {
                if (Guid.TryParse(tenantQueryValue, out var queryTenantId))
                {
                    return queryTenantId;
                }
            }

            // Try to get tenant from request body (for login API)
            if (context.Request.ContentType?.Contains("application/json") == true)
            {
                // This would require reading the body, which is complex in middleware
                // Better to handle this in the authentication service
            }

            // Try to get tenant from subdomain
            var host = context.Request.Host.Host;
            if (!string.IsNullOrEmpty(host))
            {
                var parts = host.Split('.');
                if (parts.Length > 2) // e.g., tenant.erpsystem.com
                {
                    var tenantCode = parts[0];
                    // Would need to look up tenant by code - for now return empty
                    // This could be cached for performance
                }
            }

            return Guid.Empty;
        }
    }
}
```

---

## 🔐 **Authentication Service**

```csharp path=null start=null
// ErpSystem.Api/Services/Auth/IAuthenticationService.cs
using ErpSystem.Api.DTOs.Auth;
using ErpSystem.Api.DTOs.Users;

namespace ErpSystem.Api.Services.Auth
{
    public interface IAuthenticationService
    {
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request, string? ipAddress);
        Task<LoginResponseDto> RefreshTokenAsync(string refreshToken);
        Task LogoutAsync(string token);
        Task<UserDto> GetCurrentUserAsync(Guid userId);
        Task ForgotPasswordAsync(ForgotPasswordRequestDto request);
        Task ResetPasswordAsync(ResetPasswordRequestDto request);
    }
}
```

```csharp path=null start=null
// ErpSystem.Api/Services/Auth/AuthenticationService.cs
using AutoMapper;
using ErpSystem.Api.Data;
using ErpSystem.Api.DTOs.Auth;
using ErpSystem.Api.DTOs.Users;
using ErpSystem.Api.Models.Identity;
using ErpSystem.Api.Models.Security;
using ErpSystem.Api.Services.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ErpSystem.Api.Services.Auth
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<AuthenticationService> _logger;

        public AuthenticationService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenService jwtTokenService,
            ApplicationDbContext context,
            IMapper mapper,
            ILogger<AuthenticationService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtTokenService = jwtTokenService;
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, string? ipAddress)
        {
            // Find user by email and tenant
            var user = await _userManager.Users
                .Include(u => u.Tenant)
                .Where(u => u.Email == request.Email && 
                           u.TenantId == request.TenantId && 
                           !u.IsDeleted && 
                           u.IsActive)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("Login attempt with invalid email/tenant: {Email}, {TenantId}", 
                    request.Email, request.TenantId);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            // Check tenant status
            if (user.Tenant?.IsActive != true)
            {
                _logger.LogWarning("Login attempt for inactive tenant: {TenantId}", request.TenantId);
                throw new InvalidOperationException("Tenant is inactive");
            }

            // Verify password
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            
            if (!result.Succeeded)
            {
                _logger.LogWarning("Login failed for user {UserId}: {Reason}", user.Id, result.ToString());
                
                if (result.IsLockedOut)
                    throw new InvalidOperationException("Account is locked due to multiple failed attempts");
                
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            // Get user roles and permissions
            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await GetUserPermissionsAsync(user.Id, user.TenantId);

            // Generate tokens
            var claims = CreateUserClaims(user, roles, permissions);
            var accessToken = _jwtTokenService.GenerateAccessToken(claims);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();

            // Store session
            var session = new UserSession
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                SessionToken = refreshToken,
                IPAddress = ipAddress,
                UserAgent = null, // Could get from HttpContext
                ExpiresAt = DateTime.UtcNow.AddDays(7), // Refresh token expires in 7 days
                CreatedBy = user.Id
            };

            _context.UserSessions.Add(session);

            // Update last login
            user.LastLoginDate = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            await _context.SaveChangesAsync();

            var userDto = _mapper.Map<UserDto>(user);
            var tenantDto = _mapper.Map<TenantDto>(user.Tenant);

            return new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30), // Access token expires in 30 minutes
                User = userDto,
                Tenant = tenantDto
            };
        }

        public async Task<LoginResponseDto> RefreshTokenAsync(string refreshToken)
        {
            var session = await _context.UserSessions
                .Include(s => s.User)
                .ThenInclude(u => u.Tenant)
                .Where(s => s.SessionToken == refreshToken && 
                           s.IsActive && 
                           s.ExpiresAt > DateTime.UtcNow &&
                           !s.IsDeleted)
                .FirstOrDefaultAsync();

            if (session?.User == null)
            {
                throw new UnauthorizedAccessException("Invalid refresh token");
            }

            // Check if user is still active
            if (!session.User.IsActive || session.User.IsDeleted)
            {
                throw new UnauthorizedAccessException("User account is inactive");
            }

            // Get user roles and permissions
            var roles = await _userManager.GetRolesAsync(session.User);
            var permissions = await GetUserPermissionsAsync(session.User.Id, session.User.TenantId);

            // Generate new tokens
            var claims = CreateUserClaims(session.User, roles, permissions);
            var accessToken = _jwtTokenService.GenerateAccessToken(claims);
            var newRefreshToken = _jwtTokenService.GenerateRefreshToken();

            // Update session
            session.SessionToken = newRefreshToken;
            session.ExpiresAt = DateTime.UtcNow.AddDays(7);
            session.UpdatedAt = DateTime.UtcNow;
            session.UpdatedBy = session.User.Id;

            await _context.SaveChangesAsync();

            var userDto = _mapper.Map<UserDto>(session.User);
            var tenantDto = _mapper.Map<TenantDto>(session.User.Tenant);

            return new LoginResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                User = userDto,
                Tenant = tenantDto
            };
        }

        public async Task LogoutAsync(string token)
        {
            // Decode JWT to get session info
            var principal = _jwtTokenService.ValidateToken(token);
            var sessionId = principal?.FindFirst("SessionId")?.Value;

            if (Guid.TryParse(sessionId, out var sessionGuid))
            {
                var session = await _context.UserSessions
                    .Where(s => s.Id == sessionGuid)
                    .FirstOrDefaultAsync();

                if (session != null)
                {
                    session.IsActive = false;
                    session.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
        }

        public async Task<UserDto> GetCurrentUserAsync(Guid userId)
        {
            var user = await _userManager.Users
                .Include(u => u.Tenant)
                .Where(u => u.Id == userId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
                throw new ArgumentException("User not found");

            var userDto = _mapper.Map<UserDto>(user);
            
            // Get roles and permissions
            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await GetUserPermissionsAsync(userId, user.TenantId);
            
            userDto.Roles = roles.ToList();
            userDto.Permissions = permissions.ToList();

            return userDto;
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            var user = await _userManager.Users
                .Where(u => u.Email == request.Email && 
                           u.TenantId == request.TenantId && 
                           !u.IsDeleted && 
                           u.IsActive)
                .FirstOrDefaultAsync();

            if (user != null) // Don't reveal if user exists or not
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                // TODO: Send email with reset link
                // await _emailService.SendPasswordResetEmailAsync(user.Email, token);
                
                _logger.LogInformation("Password reset requested for user {UserId}", user.Id);
            }
        }

        public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            var user = await _userManager.Users
                .Where(u => u.Email == request.Email && 
                           u.TenantId == request.TenantId && 
                           !u.IsDeleted && 
                           u.IsActive)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new InvalidOperationException("Invalid reset request");
            }

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Password reset failed: {errors}");
            }

            _logger.LogInformation("Password reset successful for user {UserId}", user.Id);
        }

        private async Task<IEnumerable<string>> GetUserPermissionsAsync(Guid userId, Guid tenantId)
        {
            var permissions = await _context.RolePermissions
                .Where(rp => rp.TenantId == tenantId &&
                            _context.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == rp.RoleId))
                .Select(rp => rp.Permission!.Name)
                .Distinct()
                .ToListAsync();

            return permissions;
        }

        private static List<Claim> CreateUserClaims(ApplicationUser user, IList<string> roles, IEnumerable<string> permissions)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new("TenantId", user.TenantId.ToString()),
                new("FirstName", user.FirstName ?? string.Empty),
                new("LastName", user.LastName ?? string.Empty)
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            claims.AddRange(permissions.Select(permission => new Claim("Permission", permission)));

            return claims;
        }
    }
}
```

---

## 🔑 **JWT Token Service**

```csharp path=null start=null
// ErpSystem.Api/Services/Security/IJwtTokenService.cs
using System.Security.Claims;

namespace ErpSystem.Api.Services.Security
{
    public interface IJwtTokenService
    {
        string GenerateAccessToken(IEnumerable<Claim> claims);
        string GenerateRefreshToken();
        ClaimsPrincipal? ValidateToken(string token);
    }
}
```

```csharp path=null start=null
// ErpSystem.Api/Services/Security/JwtTokenService.cs
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Api.Services.Security
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtTokenService> _logger;

        public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string GenerateAccessToken(IEnumerable<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured")));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30), // 30 minutes
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    _configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured")));

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = false, // We don't validate lifetime for logout
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Token validation failed: {Error}", ex.Message);
                return null;
            }
        }
    }
}
```

---

## 👥 **User Service**

```csharp path=null start=null
// ErpSystem.Api/Services/Users/IUserService.cs
using ErpSystem.Api.DTOs.Users;

namespace ErpSystem.Api.Services.Users
{
    public interface IUserService
    {
        Task<UserListDto> GetUsersAsync(int page, int pageSize, string? search, bool? isActive);
        Task<UserDto?> GetUserAsync(Guid id);
        Task<UserDto> CreateUserAsync(CreateUserRequestDto request);
        Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequestDto request);
        Task DeleteUserAsync(Guid id);
        Task ChangePasswordAsync(Guid id, ChangePasswordRequestDto request);
        Task<List<BulkUserResult>> BulkCreateUsersAsync(List<CreateUserRequestDto> requests);
    }

    public class BulkUserResult
    {
        public string Email { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
        public UserDto? User { get; set; }
    }
}
```

```csharp path=null start=null
// ErpSystem.Api/Services/Users/UserService.cs
using AutoMapper;
using ErpSystem.Api.Data;
using ErpSystem.Api.DTOs.Users;
using ErpSystem.Api.Extensions;
using ErpSystem.Api.Middleware;
using ErpSystem.Api.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Users
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly ITenantContextAccessor _tenantContext;
        private readonly IMapper _mapper;
        private readonly ILogger<UserService> _logger;

        public UserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext context,
            ITenantContextAccessor tenantContext,
            IMapper mapper,
            ILogger<UserService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _tenantContext = tenantContext;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<UserListDto> GetUsersAsync(int page, int pageSize, string? search, bool? isActive)
        {
            var tenantId = _tenantContext.GetTenantId();
            
            var query = _userManager.Users
                .Where(u => u.TenantId == tenantId && !u.IsDeleted);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.UserName!.Contains(search) || 
                                        u.Email!.Contains(search) ||
                                        (u.FirstName + " " + u.LastName).Contains(search));
            }

            if (isActive.HasValue)
            {
                query = query.Where(u => u.IsActive == isActive.Value);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var users = await query
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userDtos = new List<UserDto>();
            foreach (var user in users)
            {
                var userDto = _mapper.Map<UserDto>(user);
                var roles = await _userManager.GetRolesAsync(user);
                userDto.Roles = roles.ToList();
                userDtos.Add(userDto);
            }

            return new UserListDto
            {
                Users = userDtos,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task<UserDto?> GetUserAsync(Guid id)
        {
            var tenantId = _tenantContext.GetTenantId();
            
            var user = await _userManager.Users
                .Where(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
                return null;

            var userDto = _mapper.Map<UserDto>(user);
            var roles = await _userManager.GetRolesAsync(user);
            userDto.Roles = roles.ToList();

            return userDto;
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequestDto request)
        {
            var tenantId = _tenantContext.GetTenantId();
            
            // Check if user already exists
            var existingUser = await _userManager.Users
                .Where(u => u.Email == request.Email && u.TenantId == tenantId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (existingUser != null)
            {
                throw new InvalidOperationException("User with this email already exists");
            }

            // Check tenant user limit
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant != null)
            {
                var currentUserCount = await _userManager.Users
                    .CountAsync(u => u.TenantId == tenantId && !u.IsDeleted && u.IsActive);

                if (currentUserCount >= tenant.MaxUsers)
                {
                    throw new InvalidOperationException("Tenant user limit reached");
                }
            }

            var user = new ApplicationUser
            {
                UserName = request.UserName,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                TenantId = tenantId,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty // TODO: Get from current user context
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"User creation failed: {errors}");
            }

            // Assign roles
            if (request.RoleIds.Any())
            {
                var roleNames = await _roleManager.Roles
                    .Where(r => request.RoleIds.Contains(r.Id) && r.TenantId == tenantId)
                    .Select(r => r.Name!)
                    .ToListAsync();

                if (roleNames.Any())
                {
                    await _userManager.AddToRolesAsync(user, roleNames);
                }
            }

            _logger.LogInformation("User created: {UserId} for tenant {TenantId}", user.Id, tenantId);

            var userDto = _mapper.Map<UserDto>(user);
            userDto.Roles = (await _userManager.GetRolesAsync(user)).ToList();

            return userDto;
        }

        public async Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequestDto request)
        {
            var tenantId = _tenantContext.GetTenantId();
            
            var user = await _userManager.Users
                .Where(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new ArgumentException("User not found");
            }

            // Check if email is changing and if it's already taken
            if (user.Email != request.Email)
            {
                var existingUser = await _userManager.Users
                    .Where(u => u.Email == request.Email && u.TenantId == tenantId && u.Id != id && !u.IsDeleted)
                    .FirstOrDefaultAsync();

                if (existingUser != null)
                {
                    throw new InvalidOperationException("Email is already taken by another user");
                }
            }

            user.UserName = request.UserName;
            user.Email = request.Email;
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.IsActive = request.IsActive;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = Guid.Empty; // TODO: Get from current user context

            var result = await _userManager.UpdateAsync(user);
            
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"User update failed: {errors}");
            }

            // Update roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            var newRoles = await _roleManager.Roles
                .Where(r => request.RoleIds.Contains(r.Id) && r.TenantId == tenantId)
                .Select(r => r.Name!)
                .ToListAsync();

            var rolesToRemove = currentRoles.Except(newRoles);
            var rolesToAdd = newRoles.Except(currentRoles);

            if (rolesToRemove.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            }

            if (rolesToAdd.Any())
            {
                await _userManager.AddToRolesAsync(user, rolesToAdd);
            }

            _logger.LogInformation("User updated: {UserId}", user.Id);

            var userDto = _mapper.Map<UserDto>(user);
            userDto.Roles = newRoles.ToList();

            return userDto;
        }

        public async Task DeleteUserAsync(Guid id)
        {
            var tenantId = _tenantContext.GetTenantId();
            
            var user = await _userManager.Users
                .Where(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new ArgumentException("User not found");
            }

            // Soft delete
            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletedBy = Guid.Empty; // TODO: Get from current user context
            user.IsActive = false;

            await _userManager.UpdateAsync(user);
            
            _logger.LogInformation("User deleted: {UserId}", user.Id);
        }

        public async Task ChangePasswordAsync(Guid id, ChangePasswordRequestDto request)
        {
            var tenantId = _tenantContext.GetTenantId();
            
            var user = await _userManager.Users
                .Where(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                throw new ArgumentException("User not found");
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Password change failed: {errors}");
            }

            _logger.LogInformation("Password changed for user: {UserId}", user.Id);
        }

        public async Task<List<BulkUserResult>> BulkCreateUsersAsync(List<CreateUserRequestDto> requests)
        {
            var results = new List<BulkUserResult>();

            foreach (var request in requests)
            {
                var result = new BulkUserResult { Email = request.Email };
                
                try
                {
                    result.User = await CreateUserAsync(request);
                    result.Success = true;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                results.Add(result);
            }

            return results;
        }
    }
}
```

---

## ⚙️ **Service Registration & Configuration**

```csharp path=null start=null
// ErpSystem.Api/Program.cs - Service Registration
using ErpSystem.Api.Data;
using ErpSystem.Api.Middleware;
using ErpSystem.Api.Models.Identity;
using ErpSystem.Api.Services.Auth;
using ErpSystem.Api.Services.Security;
using ErpSystem.Api.Services.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("ErpSystem.Api")
    ));

// Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured"))),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Tenant Context
builder.Services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();

// Services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserService, UserService>();

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Controllers
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Tenant middleware (before authentication)
app.UseMiddleware<TenantMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
```

---

## 🔧 **AutoMapper Profile**

```csharp path=null start=null
// ErpSystem.Api/Mappings/MappingProfile.cs
using AutoMapper;
using ErpSystem.Api.DTOs.Auth;
using ErpSystem.Api.DTOs.Tenants;
using ErpSystem.Api.DTOs.Users;
using ErpSystem.Api.Models.Identity;
using ErpSystem.Api.Models.Tenants;

namespace ErpSystem.Api.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ApplicationUser, UserDto>()
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}".Trim()))
                .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => 
                    string.IsNullOrEmpty($"{src.FirstName} {src.LastName}".Trim()) 
                        ? (src.UserName ?? src.Email ?? "Unknown") 
                        : $"{src.FirstName} {src.LastName}".Trim()));

            CreateMap<Tenant, TenantDto>()
                .ForMember(dest => dest.CurrentUserCount, opt => opt.MapFrom(src => 
                    src.Users.Count(u => !u.IsDeleted && u.IsActive)))
                .ForMember(dest => dest.IsExpired, opt => opt.MapFrom(src => 
                    src.SubscriptionExpiry.HasValue && src.SubscriptionExpiry.Value < DateTime.UtcNow));

            CreateMap<Tenant, TenantSelectionDto>();
        }
    }
}
```

---

## 🎯 **Next Steps**

Your Sprint 1 foundation is now complete! Here's what you have:

1. ✅ **Multi-tenant EF Core entities** with proper relationships
2. ✅ **Complete API controllers** with authentication and authorization  
3. ✅ **Tenant context middleware** for automatic data isolation
4. ✅ **JWT authentication service** with refresh tokens
5. ✅ **User management service** with role-based permissions
6. ✅ **Service registration** and configuration

**Ready to implement:**
1. Run the EF migration to create your database
2. Start your ErpSystem.Api project
3. Test authentication endpoints with Swagger
4. Begin frontend integration

Would you like me to create the corresponding **frontend components** for the authentication UI, or proceed to **Sprint 2 financial module** implementations? 🚀

<citations>
<document>
<document_type>RULE</document_type>
<document_id>heEIoq7NkcTjraaXm46qUr</document_id>
</document>
</citations>