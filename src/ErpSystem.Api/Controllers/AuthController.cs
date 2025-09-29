using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Core.Interfaces;
using ErpSystem.Api.Services;
using ErpSystem.Api.Models;
using ErpSystem.Shared;
using System.IdentityModel.Tokens.Jwt;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenService _tokenService;
        private readonly ISecurityLogService _securityLogService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantService _tenantService;
        private readonly IUserTenantService _userTenantService;
        private readonly ILdapAuthenticationService _ldapAuthService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IJwtBlacklistService _jwtBlacklistService;
        private readonly ISettingsService _settingsService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenService tokenService,
            ISecurityLogService securityLogService,
            ICurrentUserService currentUserService,
            ITenantService tenantService,
            IUserTenantService userTenantService,
            ILdapAuthenticationService ldapAuthService,
            IRefreshTokenService refreshTokenService,
            IJwtBlacklistService jwtBlacklistService,
            ISettingsService settingsService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _securityLogService = securityLogService;
            _currentUserService = currentUserService;
            _tenantService = tenantService;
            _userTenantService = userTenantService;
            _ldapAuthService = ldapAuthService;
            _refreshTokenService = refreshTokenService;
            _jwtBlacklistService = jwtBlacklistService;
            _settingsService = settingsService;
            _logger = logger;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Login attempt for user: {Username} with tenant: {TenantCode}", request.Username, request.TenantCode);

                Tenant? tenant = null;
                if (!string.IsNullOrEmpty(request.TenantCode))
                {
                    // Validate tenant if code provided
                    tenant = await _tenantService.GetTenantByCodeAsync(request.TenantCode);
                    if (tenant == null || tenant.Status != TenantStatus.Active)
                    {
                        _logger.LogWarning("Login failed: Invalid or inactive tenant {TenantCode}", request.TenantCode);
                        return Unauthorized(new { message = "Invalid tenant" });
                    }
                }

                ApplicationUser? user = null;
                bool isLdapAuthenticated = false;
                LdapUser? ldapUser = null;
                
                // First try to find existing user in database
                user = await _userManager.FindByNameAsync(request.Username) ?? 
                       await _userManager.FindByEmailAsync(request.Username);
                
                // If LDAP is enabled for this tenant and user is not found or password check fails,
                // try LDAP authentication
                if (tenant?.LdapEnabled == true)
                {
                    _logger.LogInformation("Attempting LDAP authentication for user: {Username}", request.Username);
                    
                    var ldapResult = await _ldapAuthService.AuthenticateAsync(request.Username, request.Password, tenant);
                    if (ldapResult.Success && ldapResult.User != null)
                    {
                        isLdapAuthenticated = true;
                        ldapUser = ldapResult.User;
                        _logger.LogInformation("LDAP authentication successful for user: {Username}", request.Username);
                        
                        // If user doesn't exist locally, create them from LDAP data
                        if (user == null)
                        {
                            _logger.LogInformation("Creating local user from LDAP data for: {Username}", request.Username);
                            
                            user = new ApplicationUser
                            {
                                UserName = ldapUser.Username,
                                Email = ldapUser.Email,
                                FirstName = ldapUser.FirstName,
                                LastName = ldapUser.LastName,
                                TenantId = tenant.Id,
                                IsActive = true,
                                EmailConfirmed = true, // Trust LDAP email
                                AuthenticationProvider = AuthenticationProvider.LDAP
                            };
                            
                            var createResult = await _userManager.CreateAsync(user);
                            if (!createResult.Succeeded)
                            {
                                _logger.LogError("Failed to create local user from LDAP: {Errors}", 
                                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                                return StatusCode(500, new { message = "Failed to create user account" });
                            }
                            
                            // Assign default role (Employee) for new LDAP users
                            await _userManager.AddToRoleAsync(user, "Employee");
                        }
                        else
                        {
                            // Update existing user with latest LDAP data
                            user.FirstName = ldapUser.FirstName;
                            user.LastName = ldapUser.LastName;
                            user.Email = ldapUser.Email;
                            user.AuthenticationProvider = AuthenticationProvider.LDAP;
                            await _userManager.UpdateAsync(user);
                        }
                    }
                    else if (user == null)
                    {
                        // Both LDAP and local user lookup failed
                        _logger.LogWarning("Login failed: User not found in LDAP or local database for {Username}. LDAP Error: {LdapError}", 
                            request.Username, ldapResult.ErrorMessage);
                        
                        var userNotFoundSecurityLog = new SecurityLog
                        {
                            Action = SecurityAction.LoginFailure.ToString(),
                            Success = false,
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                            Username = request.Username,
                            UserId = null,
                            Details = $"User not found in LDAP or local database. LDAP: {ldapResult.ErrorMessage}",
                            FailureReason = "Invalid credentials - user not found",
                            UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                            TenantId = tenant.Id
                        };
                        await _securityLogService.CreateSecurityLogAsync(userNotFoundSecurityLog);
                        
                        return Unauthorized(new { message = "Invalid credentials" });
                    }
                }
                
                // If user still not found and LDAP is not enabled or failed
                if (user == null)
                {
                    _logger.LogWarning("Login failed: User not found for {Username}", request.Username);
                    
                    var userNotFoundSecurityLog = new SecurityLog
                    {
                        Action = SecurityAction.LoginFailure.ToString(),
                        Success = false,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        Username = request.Username,
                        UserId = null,
                        Details = "User not found",
                        FailureReason = "Invalid credentials - user not found",
                        UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                        TenantId = tenant.Id
                    };
                    await _securityLogService.CreateSecurityLogAsync(userNotFoundSecurityLog);
                    
                    return Unauthorized(new { message = "Invalid credentials" });
                }
                
                // If tenant code provided, validate that user belongs to the tenant
                if (tenant != null)
                {
                    var userTenant = await _userTenantService.GetUserTenantRelationshipAsync(user.Id, tenant.Id);
                    if (userTenant == null || !await _userTenantService.HasActiveAccessAsync(user.Id, tenant.Id))
                    {
                        var statusMessage = userTenant == null ? "not assigned" : "no active access";
                        _logger.LogWarning("Login failed: User {Username} {Status} for tenant {TenantCode}", 
                            request.Username, statusMessage, request.TenantCode);
                        
                        // Log security event for tenant mismatch
                        var tenantMismatchSecurityLog = new SecurityLog
                        {
                            Action = SecurityAction.LoginFailure.ToString(),
                            Success = false,
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                            Username = request.Username,
                            UserId = user.Id,
                            Details = $"User {statusMessage} to tenant: {request.TenantCode}",
                            FailureReason = "Tenant access denied",
                            UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                            TenantId = tenant.Id
                        };
                        await _securityLogService.CreateSecurityLogAsync(tenantMismatchSecurityLog);
                        
                        return Unauthorized(new { message = "Invalid credentials" });
                    }
                }

                Microsoft.AspNetCore.Identity.SignInResult result;
                
                // If LDAP authentication succeeded, skip local password check
                if (isLdapAuthenticated)
                {
                    result = Microsoft.AspNetCore.Identity.SignInResult.Success;
                }
                else
                {
                    // Use local password authentication
                    result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
                }

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Login failed for user {Username}: {Reason}", request.Username, result.ToString());
                    
                    if (result.IsLockedOut)
                    {
                        // Log account lockout event using user's tenant context
                        var lockoutSecurityLog = new SecurityLog
                        {
                            Action = SecurityAction.AccountLocked.ToString(),
                            Success = false,
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                            Username = request.Username,
                            UserId = user.Id,
                            Details = "Account locked due to too many failed attempts",
                            FailureReason = "Account is locked out",
                            UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                            TenantId = user.TenantId
                        };
                        await _securityLogService.CreateSecurityLogAsync(lockoutSecurityLog);
                            
                        return Unauthorized(new { message = "Account is locked out" });
                    }
                    
                    // Log failed login attempt using user's tenant context
                    var loginFailureSecurityLog = new SecurityLog
                    {
                        Action = SecurityAction.LoginFailure.ToString(),
                        Success = false,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        Username = request.Username,
                        UserId = user.Id,
                        Details = "Invalid password",
                        FailureReason = "Invalid credentials",
                        UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                        TenantId = user.TenantId
                    };
                    await _securityLogService.CreateSecurityLogAsync(loginFailureSecurityLog);
                    
                    return Unauthorized(new { message = "Invalid credentials" });
                }

                // Update last login timestamp
                user.LastLoginDate = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                var token = await _tokenService.GenerateTokenAsync(user);
                var refreshToken = _tokenService.GenerateRefreshToken();

                // TODO: Store refresh token in database for security
                
                _logger.LogInformation("Login successful for user: {Username}", request.Username);

                // Log login success using user's tenant context
                var loginSuccessSecurityLog = new SecurityLog
                {
                    Action = SecurityAction.LoginSuccess.ToString(),
                    Success = true,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Username = request.Username,
                    UserId = user.Id,
                    Details = "Login successful",
                    FailureReason = null,
                    UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                    TenantId = user.TenantId
                };
                await _securityLogService.CreateSecurityLogAsync(loginSuccessSecurityLog);

                var response = new LoginResponse
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(24), // Match JWT expiry
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.UserName!,
                        Email = user.Email!,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        CurrentTenantId = tenant?.Id,
                        CurrentTenantCode = tenant?.Code,
                        CurrentTenantName = tenant?.Name,
                        IsActive = user.IsActive,
                        Roles = (await _userManager.GetRolesAsync(user)).ToList()
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Username}", request.Username);
                return StatusCode(500, new { message = "An error occurred during login" });
            }
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var principal = _tokenService.GetPrincipalFromExpiredToken(request.Token);
                var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                // TODO: Validate refresh token from database

                var newToken = await _tokenService.GenerateTokenAsync(user);
                var newRefreshToken = _tokenService.GenerateRefreshToken();

                var response = new LoginResponse
                {
                    Token = newToken,
                    RefreshToken = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.UserName!,
                        Email = user.Email!,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        CurrentTenantId = user.TenantId,
                        IsActive = user.IsActive,
                        Roles = (await _userManager.GetRolesAsync(user)).ToList()
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return Unauthorized(new { message = "Invalid token" });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest? request = null)
        {
            try
            {
                // Get current user context
                var currentUserId = _currentUserService.GetUserId();
                var currentTenantId = _currentUserService.GetTenantId();
                var username = _currentUserService.GetUsername();
                
                if (!currentUserId.HasValue)
                {
                    _logger.LogWarning("Logout attempted but no current user found");
                    return Unauthorized();
                }

                // Extract JWT token information for blacklisting
                var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader?.StartsWith("Bearer ") == true)
                {
                    var jwt = authHeader["Bearer ".Length..];
                    try
                    {
                        var tokenHandler = new JwtSecurityTokenHandler();
                        var jsonToken = tokenHandler.ReadJwtToken(jwt);
                        var jti = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;
                        var exp = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp)?.Value;
                        
                        if (!string.IsNullOrEmpty(jti) && !string.IsNullOrEmpty(exp))
                        {
                            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp)).DateTime;
                            await _jwtBlacklistService.BlacklistTokenAsync(jti, currentUserId.Value, expiresAt, "User logout");
                            _logger.LogInformation("Blacklisted JWT token with JTI {Jti} for user {UserId}", jti, currentUserId.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to blacklist JWT token during logout");
                        // Continue with logout even if blacklisting fails
                    }
                }

                // Revoke refresh tokens
                int revokedTokens = 0;
                if (request?.RefreshToken != null)
                {
                    // Revoke specific refresh token if provided
                    var success = await _refreshTokenService.RevokeRefreshTokenAsync(
                        request.RefreshToken, 
                        currentUserId.Value, 
                        "User logout");
                    if (success) revokedTokens = 1;
                }
                else
                {
                    // Revoke all refresh tokens for the user
                    revokedTokens = await _refreshTokenService.RevokeAllUserRefreshTokensAsync(
                        currentUserId.Value, 
                        currentUserId.Value, 
                        "User logout - all sessions");
                }

                _logger.LogInformation("Revoked {RevokedTokens} refresh tokens for user {UserId} during logout", 
                    revokedTokens, currentUserId.Value);

                // Traditional sign out (for any server-side sessions)
                await _signInManager.SignOutAsync();
                
                // Log logout success
                if (currentUserId.HasValue && currentTenantId.HasValue && !string.IsNullOrEmpty(username))
                {
                    var logoutSecurityLog = new SecurityLog
                    {
                        Action = SecurityAction.LogoutSuccess.ToString(),
                        Success = true,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        Username = username,
                        UserId = currentUserId.Value,
                        Details = $"User logged out, revoked {revokedTokens} refresh tokens",
                        FailureReason = null,
                        UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                        TenantId = currentTenantId.Value
                    };
                    await _securityLogService.CreateSecurityLogAsync(logoutSecurityLog);
                }
                
                _logger.LogInformation("User {UserId} logged out successfully", currentUserId.Value);
                return Ok(new { message = "Logged out successfully", revokedSessions = revokedTokens });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { message = "An error occurred during logout" });
            }
        }

        [HttpPost("user-tenants")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUserTenants([FromBody] GetUserTenantsRequest request)
        {
            try
            {
                _logger.LogInformation("Getting tenants for user: {Username}", request.Username);

                var user = await _userManager.FindByNameAsync(request.Username) ?? 
                           await _userManager.FindByEmailAsync(request.Username);
                
                if (user == null)
                {
                    _logger.LogWarning("User not found: {Username}", request.Username);
                    return NotFound(new { message = "User not found" });
                }

                // Get all active tenant relationships for this user
                var userTenants = await _userTenantService.GetActiveUserTenantsAsync(user.Id);
                var tenantInfoList = new List<UserTenantInfo>();
                UserTenantInfo? defaultTenant = null;

                foreach (var ut in userTenants)
                {
                    var tenant = await _tenantService.GetTenantByIdAsync(ut.TenantId);
                    if (tenant?.Status == TenantStatus.Active)
                    {
                        var tenantInfo = new UserTenantInfo
                        {
                            TenantId = tenant.Id,
                            TenantCode = tenant.Code,
                            TenantName = tenant.Name,
                            IsDefault = ut.IsDefault,
                            AccessLevel = ut.AccessLevel.ToString()
                        };
                        
                        tenantInfoList.Add(tenantInfo);
                        
                        if (ut.IsDefault)
                        {
                            defaultTenant = tenantInfo;
                        }
                    }
                }

                var response = new GetUserTenantsResponse
                {
                    Tenants = tenantInfoList,
                    DefaultTenant = defaultTenant
                };

                _logger.LogInformation("Found {Count} accessible tenants for user {Username}", tenantInfoList.Count, request.Username);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenants for user {Username}", request.Username);
                return StatusCode(500, new { message = "An error occurred while retrieving user tenants" });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return Unauthorized();
                }

                // Get user's accessible tenants
                var userTenants = await _userTenantService.GetActiveUserTenantsAsync(user.Id);
                var accessibleTenants = new List<UserTenantInfo>();
                
                foreach (var ut in userTenants)
                {
                    var tenant = await _tenantService.GetTenantByIdAsync(ut.TenantId);
                    if (tenant?.Status == TenantStatus.Active)
                    {
                        accessibleTenants.Add(new UserTenantInfo
                        {
                            TenantId = tenant.Id,
                            TenantCode = tenant.Code,
                            TenantName = tenant.Name,
                            IsDefault = ut.IsDefault,
                            AccessLevel = ut.AccessLevel.ToString()
                        });
                    }
                }

                // Get current tenant info
                var currentTenant = await _tenantService.GetTenantByIdAsync(user.TenantId);

                var userInfo = new UserInfo
                {
                    Id = user.Id,
                    Username = user.UserName!,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    CurrentTenantId = user.TenantId,
                    CurrentTenantCode = currentTenant?.Code,
                    CurrentTenantName = currentTenant?.Name,
                    AccessibleTenants = accessibleTenants,
                    IsActive = user.IsActive,
                    Roles = (await _userManager.GetRolesAsync(user)).ToList()
                };

                return Ok(userInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, new { message = "An error occurred" });
            }
        }

        [HttpPost("select-tenant")]
        [Authorize]
        public async Task<IActionResult> SelectTenant([FromBody] SelectTenantRequest request)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return Unauthorized();
                }

                // Validate tenant exists and is active
                var tenant = await _tenantService.GetTenantByCodeAsync(request.TenantCode);
                if (tenant == null || tenant.Status != TenantStatus.Active)
                {
                    return BadRequest(new { message = "Invalid or inactive tenant" });
                }

                // Validate user has access to this tenant
                if (!await _userTenantService.HasActiveAccessAsync(Guid.Parse(userId), tenant.Id))
                {
                    return Forbid("User does not have access to this tenant");
                }

                // Update user's current tenant
                user.TenantId = tenant.Id;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = user.UserName;
                await _userManager.UpdateAsync(user);

                // Optionally set this tenant as default for the user
                if (request.SetAsDefault)
                {
                    await _userTenantService.SetDefaultTenantAsync(Guid.Parse(userId), tenant.Id);
                }

                _logger.LogInformation("User {Username} selected tenant {TenantCode}", user.UserName, request.TenantCode);

                // Generate new JWT token with updated tenant context
                var newToken = await _tokenService.GenerateTokenAsync(user);

                // Get updated user info with new tenant context
                var userTenants = await _userTenantService.GetActiveUserTenantsAsync(user.Id);
                var accessibleTenants = new List<UserTenantInfo>();
                
                foreach (var ut in userTenants)
                {
                    var t = await _tenantService.GetTenantByIdAsync(ut.TenantId);
                    if (t?.Status == TenantStatus.Active)
                    {
                        accessibleTenants.Add(new UserTenantInfo
                        {
                            TenantId = t.Id,
                            TenantCode = t.Code,
                            TenantName = t.Name,
                            IsDefault = ut.IsDefault,
                            AccessLevel = ut.AccessLevel.ToString()
                        });
                    }
                }

                var response = new SelectTenantResponse
                {
                    Token = newToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Username = user.UserName!,
                        Email = user.Email!,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        CurrentTenantId = tenant.Id,
                        CurrentTenantCode = tenant.Code,
                        CurrentTenantName = tenant.Name,
                        AccessibleTenants = accessibleTenants,
                        IsActive = user.IsActive,
                        Roles = (await _userManager.GetRolesAsync(user)).ToList()
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting tenant {TenantCode}", request.TenantCode);
                return StatusCode(500, new { message = "An error occurred while selecting tenant" });
            }
        }

        [HttpGet("tenant/{tenantId}/users")]
        [Authorize]
        public async Task<IActionResult> GetTenantUsers(Guid tenantId)
        {
            try
            {
                // Validate tenant exists
                var tenant = await _tenantService.GetTenantByIdAsync(tenantId);
                if (tenant == null || tenant.Status != TenantStatus.Active)
                {
                    return NotFound(new { message = "Tenant not found or inactive" });
                }

                // Get all users mapped to this tenant
                var tenantUsers = await _userTenantService.GetActiveTenantUsersAsync(tenantId);
                
                var userMappings = new List<TenantUserMapping>();
                foreach (var user in tenantUsers)
                {
                    // Get the user-tenant relationship details
                    var relationship = await _userTenantService.GetUserTenantRelationshipAsync(user.Id, tenantId);
                    if (relationship != null)
                    {
                        userMappings.Add(new TenantUserMapping
                        {
                            UserId = user.Id.ToString(),
                            TenantId = tenantId.ToString(),
                            IsActive = relationship.Status == UserTenantStatus.Active,
                            ExpiresAt = relationship.ExpiresAt?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                            AccessLevel = relationship.AccessLevel.ToString(),
                            IsDefault = relationship.IsDefault,
                            GrantedAt = relationship.GrantedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                            User = new TenantUserInfo
                            {
                                Id = user.Id.ToString(),
                                Username = user.UserName ?? "",
                                Email = user.Email ?? "",
                                FirstName = user.FirstName,
                                LastName = user.LastName,
                                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                                IsActive = user.IsActive
                            }
                        });
                    }
                }

                return Ok(userMappings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users for tenant {TenantId}", tenantId);
                return StatusCode(500, new { message = "An error occurred while retrieving tenant users" });
            }
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Registration attempt for user: {Username}, email: {Email}", request.Username, request.Email);

                // Check if a user with the same username or email already exists
                var existingUserByUsername = await _userManager.FindByNameAsync(request.Username);
                if (existingUserByUsername != null)
                {
                    return BadRequest(new { message = "A user with this username already exists." });
                }

                var existingUserByEmail = await _userManager.FindByEmailAsync(request.Email);
                if (existingUserByEmail != null)
                {
                    return BadRequest(new { message = "A user with this email address already exists." });
                }

                // For now, we'll assign users to the first active tenant that allows self-registration
                // Later this can be enhanced to support tenant selection during registration
                var availableTenants = await _tenantService.GetAllTenantsAsync();
                var registrationTenant = availableTenants.FirstOrDefault(t => t.Status == TenantStatus.Active && t.AllowSelfRegistration);
                
                if (registrationTenant == null)
                {
                    return BadRequest(new { message = "Self-registration is not currently available." });
                }

                // Create the user
                var user = new ApplicationUser
                {
                    UserName = request.Username,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    TenantId = registrationTenant.Id,
                    IsActive = false, // Will be activated after OTP verification
                    EmailConfirmed = false,
                    PhoneNumberConfirmed = false,
                    AuthenticationProvider = AuthenticationProvider.Local,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Self-Registration"
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    _logger.LogWarning("User registration failed for {Username}: {Errors}", request.Username, string.Join(", ", errors));
                    return BadRequest(new { message = "Registration failed.", errors });
                }

                // Assign default role (Employee) to the new user
                await _userManager.AddToRoleAsync(user, "Employee");

                // Create user-tenant relationship
                await _userTenantService.GrantUserAccessToTenantAsync(
                    user.Id,
                    registrationTenant.Id,
                    UserTenantAccessLevel.Standard,
                    "Self-Registration"
                );

                // Log successful registration
                var registrationSecurityLog = new SecurityLog
                {
                    Action = "UserRegistration",
                    Success = true,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Username = request.Username,
                    UserId = user.Id,
                    Details = "User self-registered successfully",
                    FailureReason = null,
                    UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                    TenantId = registrationTenant.Id
                };
                await _securityLogService.CreateSecurityLogAsync(registrationSecurityLog);

                _logger.LogInformation("User registration successful for: {Username}", request.Username);

                // TODO: Send OTP via SMS for phone verification
                // For now, just return success
                return Ok(new RegisterResponse
                {
                    Success = true,
                    Message = "Registration successful. Please verify your phone number.",
                    PhoneNumber = request.PhoneNumber,
                    RequiresOtpVerification = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for user: {Username}", request.Username);
                return StatusCode(500, new { message = "An error occurred during registration" });
            }
        }

        [HttpPost("verify-otp")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("OTP verification attempt for phone: {PhoneNumber}", request.PhoneNumber);

                // Find user by phone number
                var users = _userManager.Users.Where(u => u.PhoneNumber == request.PhoneNumber && !u.IsActive).ToList();
                var user = users.FirstOrDefault();
                
                if (user == null)
                {
                    return BadRequest(new { message = "Invalid phone number or user already verified." });
                }

                // TODO: Implement actual OTP verification logic
                // For now, accept any 6-digit code
                if (request.OtpCode.Length != 6 || !request.OtpCode.All(char.IsDigit))
                {
                    return BadRequest(new { message = "Invalid OTP code. Please enter a 6-digit code." });
                }

                // Activate the user account
                user.IsActive = true;
                user.PhoneNumberConfirmed = true;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = "OTP-Verification";
                
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogError("Failed to activate user {Username} after OTP verification", user.UserName);
                    return StatusCode(500, new { message = "Failed to activate account" });
                }

                // Log successful OTP verification
                var otpVerificationSecurityLog = new SecurityLog
                {
                    Action = "OtpVerification",
                    Success = true,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Username = user.UserName,
                    UserId = user.Id,
                    Details = "Phone number verified successfully via OTP",
                    FailureReason = null,
                    UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                    TenantId = user.TenantId
                };
                await _securityLogService.CreateSecurityLogAsync(otpVerificationSecurityLog);

                _logger.LogInformation("OTP verification successful for user: {Username}", user.UserName);

                return Ok(new VerifyOtpResponse
                {
                    Success = true,
                    Message = "Phone number verified successfully. Your account is now active."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OTP verification for phone: {PhoneNumber}", request.PhoneNumber);
                return StatusCode(500, new { message = "An error occurred during OTP verification" });
            }
        }

        [HttpGet("security-settings")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicSecuritySettings()
        {
            try
            {
                // This endpoint provides public security settings needed for registration and login
                // (password policy, CAPTCHA settings, etc.) without requiring authentication
                
                // Try to get actual security settings from the database
                Core.Entities.Security? settings = null;
                try
                {
                    settings = await _settingsService.GetPublicSecuritySettingsAsync();
                    _logger.LogInformation("Retrieved public security settings from database: CAPTCHA enabled = {CaptchaEnabled}, Site key = {SiteKeyPrefix}...", 
                        settings?.CaptchaEnabled, settings?.RecaptchaSiteKey?.Length > 10 ? settings.RecaptchaSiteKey[..10] : settings?.RecaptchaSiteKey);
                }
                catch (Exception settingsEx)
                {
                    _logger.LogWarning(settingsEx, "Failed to retrieve public security settings from database, using defaults");
                }
                
                // Return actual security settings or defaults
                var publicSettings = new
                {
                    passwordMinLength = settings?.PasswordMinLength ?? 8,
                    passwordRequireUppercase = settings?.PasswordRequireUppercase ?? true,
                    passwordRequireLowercase = settings?.PasswordRequireLowercase ?? true,
                    passwordRequireDigits = settings?.PasswordRequireDigits ?? true,
                    passwordRequireSpecialChars = settings?.PasswordRequireSpecialChars ?? true,
                    captchaEnabled = settings?.CaptchaEnabled ?? false,
                    captchaProvider = settings?.CaptchaProvider ?? "recaptcha",
                    recaptchaSiteKey = settings?.RecaptchaSiteKey,
                    hCaptchaSiteKey = settings?.HCaptchaSiteKey,
                    termsOfServiceUrl = settings?.TermsOfServiceUrl,
                    privacyPolicyUrl = settings?.PrivacyPolicyUrl
                };
                
                _logger.LogInformation("Public security settings retrieved: CAPTCHA enabled = {CaptchaEnabled}, Provider = {CaptchaProvider}, Site Key = {SiteKeyPrefix}...", 
                    publicSettings.captchaEnabled, 
                    publicSettings.captchaProvider, 
                    publicSettings.recaptchaSiteKey?.Length > 10 ? publicSettings.recaptchaSiteKey[..10] : publicSettings.recaptchaSiteKey);
                return Ok(publicSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public security settings");
                
                // Return defaults even on error to ensure registration page works
                var fallbackSettings = new
                {
                    passwordMinLength = 8,
                    passwordRequireUppercase = true,
                    passwordRequireLowercase = true,
                    passwordRequireDigits = true,
                    passwordRequireSpecialChars = true,
                    captchaEnabled = false,
                    captchaProvider = "recaptcha",
                    recaptchaSiteKey = (string?)null,
                    hCaptchaSiteKey = (string?)null,
                    termsOfServiceUrl = (string?)null,
                    privacyPolicyUrl = (string?)null
                };
                
                return Ok(fallbackSettings);
            }
        }
    }
}
