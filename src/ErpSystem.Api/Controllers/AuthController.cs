using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Core.Interfaces;
using ErpSystem.Api.Services;
using ErpSystem.Api.Models;
using ErpSystem.Shared;

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
        private readonly ILdapAuthenticationService _ldapAuthService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenService tokenService,
            ISecurityLogService securityLogService,
            ICurrentUserService currentUserService,
            ITenantService tenantService,
            ILdapAuthenticationService ldapAuthService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _securityLogService = securityLogService;
            _currentUserService = currentUserService;
            _tenantService = tenantService;
            _ldapAuthService = ldapAuthService;
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

                // First validate the tenant exists and is active
                var tenant = await _tenantService.GetTenantByCodeAsync(request.TenantCode);
                if (tenant == null || tenant.Status != TenantStatus.Active)
                {
                    _logger.LogWarning("Login failed: Invalid or inactive tenant {TenantCode}", request.TenantCode);
                    return Unauthorized(new { message = "Invalid tenant" });
                }

                ApplicationUser? user = null;
                bool isLdapAuthenticated = false;
                LdapUser? ldapUser = null;
                
                // First try to find existing user in database
                user = await _userManager.FindByNameAsync(request.Username) ?? 
                       await _userManager.FindByEmailAsync(request.Username);
                
                // If LDAP is enabled for this tenant and user is not found or password check fails,
                // try LDAP authentication
                if (tenant.LdapEnabled)
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
                
                // Validate that the user belongs to the selected tenant
                if (user.TenantId != tenant.Id)
                {
                    _logger.LogWarning("Login failed: User {Username} does not belong to tenant {TenantCode}", request.Username, request.TenantCode);
                    
                    // Log security event for tenant mismatch
                    var tenantMismatchSecurityLog = new SecurityLog
                    {
                        Action = SecurityAction.LoginFailure.ToString(),
                        Success = false,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        Username = request.Username,
                        UserId = user.Id,
                        Details = $"User belongs to different tenant. Attempted: {request.TenantCode}, User's tenant: {user.TenantId}",
                        FailureReason = "Tenant mismatch",
                        UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                        TenantId = tenant.Id
                    };
                    await _securityLogService.CreateSecurityLogAsync(tenantMismatchSecurityLog);
                    
                    return Unauthorized(new { message = "Invalid credentials" });
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
                        TenantId = user.TenantId,
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
                        TenantId = user.TenantId,
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
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();
                
                // TODO: Blacklist the current JWT token
                // TODO: Remove refresh token from database
                
                // Log logout success using current user's tenant context
                var currentUserId = _currentUserService.GetUserId();
                var currentTenantId = _currentUserService.GetTenantId();
                var username = _currentUserService.GetUsername();
                
                if (currentUserId.HasValue && currentTenantId.HasValue && !string.IsNullOrEmpty(username))
                {
                    var logoutSecurityLog = new SecurityLog
                    {
                        Action = SecurityAction.LogoutSuccess.ToString(),
                        Success = true,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        Username = username,
                        UserId = currentUserId.Value,
                        Details = "User logged out",
                        FailureReason = null,
                        UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                        TenantId = currentTenantId.Value
                    };
                    await _securityLogService.CreateSecurityLogAsync(logoutSecurityLog);
                }
                
                _logger.LogInformation("User logged out successfully");
                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { message = "An error occurred during logout" });
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

                var userInfo = new UserInfo
                {
                    Id = user.Id,
                    Username = user.UserName!,
                    Email = user.Email!,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    TenantId = user.TenantId,
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
    }
}