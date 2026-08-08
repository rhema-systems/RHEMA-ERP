using System.IdentityModel.Tokens.Jwt;
using ErpSystem.Api.Models;
using ErpSystem.Api.Services;
using ErpSystem.Api.Services.Sms;
using ErpSystem.Api.Services.Otp;
using ErpSystem.Core.DTOs.Auth;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.Identity;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

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
        private readonly IUserSessionService _userSessionService;
        private readonly ITwoFactorAuthService _twoFactorService;
        private readonly IPasswordResetService _passwordResetService;
        private readonly IEmailService _emailService;
        private readonly INotificationService _notificationService;
        private readonly ITenantSmsSender _tenantSmsSender;
        private readonly ICaptchaVerificationService _captchaVerificationService;
        private readonly IOtpService _otpService;
        private readonly IHrIdentityAccessService _hrIdentityAccessService;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
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
            IUserSessionService userSessionService,
            ITwoFactorAuthService twoFactorService,
            IPasswordResetService passwordResetService,
            IEmailService emailService,
            INotificationService notificationService,
            ITenantSmsSender tenantSmsSender,
            ICaptchaVerificationService captchaVerificationService,
            IOtpService otpService,
            IHrIdentityAccessService hrIdentityAccessService,
            ApplicationDbContext context,
            IConfiguration configuration,
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
            _userSessionService = userSessionService;
            _twoFactorService = twoFactorService;
            _passwordResetService = passwordResetService;
            _emailService = emailService;
            _notificationService = notificationService;
            _tenantSmsSender = tenantSmsSender;
            _captchaVerificationService = captchaVerificationService;
            _otpService = otpService;
            _hrIdentityAccessService = hrIdentityAccessService;
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        private string? GetEffectiveHost()
        {
            var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            var raw = !string.IsNullOrWhiteSpace(forwardedHost) ? forwardedHost : Request.Host.Host;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            // X-Forwarded-Host can contain a comma-separated list and may include a port.
            var host = raw.Split(',')[0].Trim();
            var portIdx = host.IndexOf(':');
            if (portIdx > 0)
            {
                host = host[..portIdx];
            }

            return string.IsNullOrWhiteSpace(host) ? null : host;
        }

        private bool IsSupportHostname(string? host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;

            var h = host.Trim().ToLowerInvariant();
            var configured = (_configuration["SUPPORT_PORTAL_HOSTNAME"] ?? string.Empty).Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(configured) && h == configured) return true;
            return h.StartsWith("support.");
        }

        private async Task<List<string>> GetUserPermissionsAsync(ApplicationUser user)
        {
            return await _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Name))
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync();
        }

        private async Task<Guid> ResolveTenantIdForCaptchaAsync(string? tenantCode)
        {
            if (!string.IsNullOrWhiteSpace(tenantCode))
            {
                var tenant = await _tenantService.GetTenantByCodeAsync(tenantCode);
                if (tenant != null && tenant.Status == TenantStatus.Active)
                {
                    return tenant.Id;
                }
            }

            var host = GetEffectiveHost();
            if (!string.IsNullOrWhiteSpace(host))
            {
                var tenantByDomain = await _tenantService.GetTenantByDomainAsync(host);
                if (tenantByDomain != null && tenantByDomain.Status == TenantStatus.Active)
                {
                    return tenantByDomain.Id;
                }
            }

            return Constants.Tenants.DefaultTenantId;
        }

        private static bool TryParseOtpChannel(string? channel, out OtpChannel otpChannel)
        {
            otpChannel = OtpChannel.Email;
            if (string.IsNullOrWhiteSpace(channel))
            {
                return false;
            }

            var c = channel.Trim().ToLowerInvariant();
            if (c is "email")
            {
                otpChannel = OtpChannel.Email;
                return true;
            }

            if (c is "sms" or "text")
            {
                otpChannel = OtpChannel.Sms;
                return true;
            }

            return false;
        }

        private static string NormalizePhone(string phoneNumber)
        {
            var t = (phoneNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(t)) return string.Empty;

            var sb = new System.Text.StringBuilder();
            foreach (var c in t)
            {
                if (c == '+' && sb.Length == 0)
                {
                    sb.Append(c);
                    continue;
                }

                if (char.IsDigit(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private async Task<IActionResult> CompleteSuccessfulLoginAsync(ApplicationUser user, Tenant? tenant, string usernameForLogs, string details)
        {
            var accessDecision = await _hrIdentityAccessService.EvaluateAsync(user.Id, HttpContext.RequestAborted);
            if (!accessDecision.IsAllowed)
            {
                _logger.LogWarning(
                    "Login denied for HR-ineligible identity {UserId}. Code={Code}",
                    user.Id,
                    accessDecision.Code);
                return Unauthorized(new { message = accessDecision.Message, code = accessDecision.Code });
            }

            if (user.MustChangePassword &&
                (!user.TemporaryPasswordExpiresAtUtc.HasValue ||
                 user.TemporaryPasswordExpiresAtUtc.Value <= DateTime.UtcNow))
            {
                _logger.LogWarning(
                    "Expired one-time credential denied for user {UserId}",
                    user.Id);
                return Unauthorized(new
                {
                    message =
                        "The one-time temporary password has expired. Ask a supplier administrator to resend it.",
                    code = "TEMPORARY_PASSWORD_EXPIRED"
                });
            }

            // Determine the effective tenant ID for this login session
            var effectiveTenantId = tenant?.Id ?? user.TenantId;

            // If effectiveTenantId is still empty, try to get from UserTenants table
            if (effectiveTenantId == Guid.Empty)
            {
                // Get active user tenants ordered by IsDefault
                var userTenants = await _context.UserTenants
                    .Where(ut => ut.UserId == user.Id && !ut.IsDeleted && ut.Status == UserTenantStatus.Active)
                    .OrderByDescending(ut => ut.IsDefault)
                    .ToListAsync();

                if (userTenants.Any())
                {
                    effectiveTenantId = userTenants.First().TenantId;
                    var isDefault = userTenants.First().IsDefault;
                    _logger.LogInformation("Using {TenantType} tenant {TenantId} from UserTenants for user {Username}",
                        isDefault ? "default" : "first active", effectiveTenantId, user.UserName);
                }
                else
                {
                    _logger.LogWarning("User {Username} has no tenant assigned in User.TenantId or UserTenants table", user.UserName);
                    return Unauthorized(new { message = "User has no tenant assigned. Please contact administrator." });
                }

                // Update user's TenantId field for future logins
                user.TenantId = effectiveTenantId;
                await _userManager.UpdateAsync(user);
                _logger.LogInformation("Updated user {Username} TenantId to {TenantId}", user.UserName, effectiveTenantId);
            }

            // Update user's current tenant if tenant was specified in login
            if (tenant != null && user.TenantId != tenant.Id)
            {
                user.TenantId = tenant.Id;
                await _userManager.UpdateAsync(user);
                _logger.LogInformation("Updated user {Username} tenant to {TenantId}", user.UserName, tenant.Id);
            }

            // Get security settings for concurrent login prevention from user's tenant
            var securitySettings = await _settingsService.GetSecuritySettingsAsync(effectiveTenantId);
            var preventConcurrentLogin = securitySettings?.PreventConcurrentLogin.ToString() ?? "Disabled";

            // Check if user can login based on concurrent login prevention settings
            var canLogin = await _userSessionService.CanUserLoginAsync(user.Id, preventConcurrentLogin);
            if (!canLogin)
            {
                _logger.LogWarning("Login prevented for user {Username}: Active session exists and prevention mode is {Mode}",
                    usernameForLogs, preventConcurrentLogin);

                var preventedLoginSecurityLog = new SecurityLog
                {
                    Action = SecurityAction.LoginFailure.ToString(),
                    Success = false,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Username = usernameForLogs,
                    UserId = user.Id,
                    Details = $"Login prevented due to concurrent session policy: {preventConcurrentLogin}",
                    FailureReason = "Active session exists - concurrent login prevented",
                    UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                    TenantId = user.TenantId
                };
                await _securityLogService.CreateSecurityLogAsync(preventedLoginSecurityLog);

                return Unauthorized(new
                {
                    message = "You already have an active session. Please logout from other devices first.",
                    code = "CONCURRENT_SESSION_PREVENTED"
                });
            }

            // Get device information for session tracking
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
            var deviceFingerprint = GenerateDeviceFingerprint(ipAddress, userAgent);

            // Create user session (this handles concurrent login prevention logic)
            var userSession = await _userSessionService.CreateSessionAsync(
                user.Id,
                effectiveTenantId,
                ipAddress,
                userAgent,
                deviceFingerprint,
                preventConcurrentLogin
            );

            // Generate token with session ID included
            var token = await _tokenService.GenerateTokenAsync(user, userSession.SessionId);

            // Extract JTI from the generated token and update the session
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                if (tokenHandler.CanReadToken(token))
                {
                    var jsonToken = tokenHandler.ReadJwtToken(token);
                    var jti = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;

                    if (!string.IsNullOrEmpty(jti))
                    {
                        userSession.JwtTokenId = jti;
                        await _context.SaveChangesAsync(); // Save the JTI to the session
                        _logger.LogDebug("Linked JWT token {Jti} to session {SessionId}", jti, userSession.SessionId);
                    }
                }
                else
                {
                    _logger.LogWarning("Generated JWT token cannot be read: {TokenPreview}",
                        token.Length > 50 ? string.Concat(token.AsSpan(0, 50), "...") : token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to link JWT token to session {SessionId}", userSession.SessionId);
                // Continue with login even if linking fails
            }

            // Update last login timestamp
            user.LastLoginDate = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            // TODO: Store refresh token in database for security

            _logger.LogInformation("{Details} for user: {Username}", details, usernameForLogs);

            // Log login success using user's tenant context
            var loginSuccessSecurityLog = new SecurityLog
            {
                Action = SecurityAction.LoginSuccess.ToString(),
                Success = true,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Username = usernameForLogs,
                UserId = user.Id,
                Details = details,
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
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    CurrentTenantId = effectiveTenantId,
                    CurrentTenantCode = tenant?.Code,
                    CurrentTenantName = tenant?.Name,
                    IsActive = user.IsActive,
                    MustChangePassword = user.MustChangePassword,
                    TemporaryPasswordExpiresAtUtc = user.TemporaryPasswordExpiresAtUtc,
                    Roles = (await _userManager.GetRolesAsync(user)).ToList(),
                    Permissions = await GetUserPermissionsAsync(user),
                    AuthenticationProvider = user.AuthenticationProvider.ToString()
                }
            };

            return Ok(response);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // CAPTCHA enforcement (when enabled for tenant)
                try
                {
                    var tenantIdForCaptcha = await ResolveTenantIdForCaptchaAsync(request.TenantCode);
                    var host = GetEffectiveHost();
                    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _captchaVerificationService.EnsureCaptchaValidAsync(tenantIdForCaptcha, request.RecaptchaToken, host, remoteIp, HttpContext.RequestAborted);
                }
                catch (CaptchaVerificationException ex)
                {
                    return BadRequest(new { message = ex.Message });
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

                // Resolve the default tenant (used for LDAP auto-provisioning / internal ERP context)
                Tenant? defaultTenant = null;
                try
                {
                    defaultTenant = await _tenantService.GetTenantByIdAsync(Constants.Tenants.DefaultTenantId);
                    if (defaultTenant == null || defaultTenant.Status != TenantStatus.Active)
                    {
                        defaultTenant = null;
                    }
                }
                catch
                {
                    defaultTenant = null;
                }

                ApplicationUser? user = null;
                bool isLdapAuthenticated = false;
                LdapUser? ldapUser = null;
                string? ldapFailureReason = null;

                // First try to find existing user in database
                user = await _userManager.FindByNameAsync(request.Username) ??
                       await _userManager.FindByEmailAsync(request.Username);

                // Prefer LDAP authentication when available. If a tenant code isn't provided, use the default tenant's LDAP config.
                var tenantForLdap = tenant ?? defaultTenant;

                if (tenantForLdap?.LdapEnabled == true)
                {
                    _logger.LogInformation(
                        "Attempting LDAP authentication for user {Username} using tenant {TenantId}. Server={LdapServer}, Port={LdapPort}, BaseDn={LdapBaseDn}",
                        request.Username,
                        tenantForLdap.Id,
                        tenantForLdap.LdapServer,
                        tenantForLdap.LdapPort ?? 389,
                        tenantForLdap.LdapBaseDn);

                    var ldapResult = await _ldapAuthService.AuthenticateAsync(request.Username, request.Password, tenantForLdap);
                    ldapFailureReason = ldapResult.ErrorMessage;

                    _logger.LogInformation(
                        "LDAP authentication call completed for user {Username}. Success={Success}, Error={Error}",
                        request.Username,
                        ldapResult.Success,
                        ldapResult.ErrorMessage);

                    if (ldapResult.Success && ldapResult.User != null)
                    {
                        isLdapAuthenticated = true;
                        ldapUser = ldapResult.User;
                        _logger.LogInformation("LDAP authentication successful for user: {Username}", request.Username);

                        // If user doesn't exist locally, auto-provision them after successful LDAP authentication.
                        // Requirement: create with AuthenticationProvider=LDAP, no role assignment, and default tenant context.
                        if (user == null)
                        {
                            _logger.LogInformation("Creating local user from LDAP data for: {Username}", request.Username);

                            var provisionTenantId = defaultTenant?.Id ?? tenantForLdap.Id;
                            var email = !string.IsNullOrWhiteSpace(ldapUser.Email) ? ldapUser.Email : $"{ldapUser.Username}@ldap.local";

                            user = new ApplicationUser
                            {
                                Id = Guid.NewGuid(),
                                UserName = ldapUser.Username,
                                Email = email,
                                FirstName = ldapUser.FirstName,
                                LastName = ldapUser.LastName,
                                TenantId = provisionTenantId,
                                IsActive = true,
                                EmailConfirmed = true, // Trust LDAP email
                                AuthenticationProvider = AuthenticationProvider.LDAP,
                                LdapDn = string.IsNullOrWhiteSpace(ldapUser.DistinguishedName) ? null : ldapUser.DistinguishedName
                            };

                            // Create without a local password (LDAP remains the source of truth).
                            var createResult = await _userManager.CreateAsync(user);
                            if (!createResult.Succeeded)
                            {
                                _logger.LogError("Failed to create local user from LDAP: {Errors}",
                                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                                return StatusCode(500, new { message = "Failed to create user account" });
                            }
                        }
                        else
                        {
                            // Update existing user with latest LDAP data
                            user.FirstName = ldapUser.FirstName;
                            user.LastName = ldapUser.LastName;
                            var fallbackEmail = user.Email;
                            if (string.IsNullOrWhiteSpace(fallbackEmail))
                            {
                                fallbackEmail = $"{ldapUser.Username}@ldap.local";
                            }
                            user.Email = !string.IsNullOrWhiteSpace(ldapUser.Email) ? ldapUser.Email : fallbackEmail;
                            user.AuthenticationProvider = AuthenticationProvider.LDAP;
                            // Directory authentication can refresh profile data, but it must not silently
                            // reactivate an HR-linked identity that HR or reconciliation has suspended.
                            if (!user.EmployeeId.HasValue)
                            {
                                user.IsActive = true;
                            }
                            user.LdapDn = string.IsNullOrWhiteSpace(ldapUser.DistinguishedName) ? user.LdapDn : ldapUser.DistinguishedName;
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
                            TenantId = tenant?.Id ?? Constants.Tenants.DefaultTenantId
                        };
                        await _securityLogService.CreateSecurityLogAsync(userNotFoundSecurityLog);

                        return Unauthorized(new { message = "Invalid credentials" });
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "LDAP authentication skipped for user {Username}. Tenant is null or LDAP is disabled (TenantCode={TenantCode}).",
                        request.Username,
                        request.TenantCode);
                }

                // If LDAP is enabled and this user is an LDAP user, do not fall back to local password authentication.
                if (!isLdapAuthenticated &&
                    tenantForLdap?.LdapEnabled == true &&
                    user != null &&
                    user.AuthenticationProvider == AuthenticationProvider.LDAP)
                {
                    _logger.LogWarning(
                        "Login failed for LDAP user {Username}: LDAP authentication failed. Reason={Reason}",
                        request.Username,
                        ldapFailureReason ?? "Unknown");

                    var loginFailureSecurityLog = new SecurityLog
                    {
                        Action = SecurityAction.LoginFailure.ToString(),
                        Success = false,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        Username = request.Username,
                        UserId = user.Id,
                        Details = $"LDAP authentication failed: {ldapFailureReason ?? "Invalid credentials"}",
                        FailureReason = "Invalid credentials",
                        UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                        TenantId = user.TenantId
                    };
                    await _securityLogService.CreateSecurityLogAsync(loginFailureSecurityLog);

                    var responseMessage = "Invalid credentials";
                    if (string.Equals(ldapFailureReason, "Authentication service error", StringComparison.OrdinalIgnoreCase))
                    {
                        responseMessage = "Directory service error. Please contact your system administrator.";
                    }
                    else if (string.Equals(ldapFailureReason, "LDAP is not configured for this tenant", StringComparison.OrdinalIgnoreCase))
                    {
                        responseMessage = "Directory service is not configured. Please contact your system administrator.";
                    }

                    return Unauthorized(new { message = responseMessage });
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
                        TenantId = tenant?.Id ?? Constants.Tenants.DefaultTenantId
                    };
                    await _securityLogService.CreateSecurityLogAsync(userNotFoundSecurityLog);

                    return Unauthorized(new { message = "Invalid credentials" });
                }

                // If tenant code provided, validate that user belongs to the tenant
                // NOTE: For LDAP-authenticated users we allow login without requiring pre-created UserTenant mappings.
                if (tenant != null && !isLdapAuthenticated)
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

                var accessDecision = await _hrIdentityAccessService.EvaluateAsync(user.Id, HttpContext.RequestAborted);
                if (!accessDecision.IsAllowed)
                {
                    _logger.LogWarning(
                        "Credential validation succeeded but HR access was denied for user {UserId}. Code={Code}",
                        user.Id,
                        accessDecision.Code);
                    return Unauthorized(new { message = accessDecision.Message, code = accessDecision.Code });
                }

                // Check if user has Two-Factor Authentication enabled
                var hasTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user) && !string.IsNullOrEmpty(user.AuthenticatorKey);

                if (hasTwoFactorEnabled)
                {
                    // If 2FA code is not provided, return response indicating 2FA is required
                    if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                    {
                        _logger.LogInformation("User {Username} requires 2FA verification", request.Username);

                        // Generate a temporary token for 2FA verification
                        var tempToken = Guid.NewGuid().ToString("N");

                        // Store the temporary login state (you might want to use a cache like Redis in production)
                        // For now, we'll use a simple approach with a temporary JWT
                        var tempLoginInfo = new
                        {
                            UserId = user.Id,
                            Username = request.Username,
                            TenantId = tenant?.Id ?? user.TenantId,
                            RememberMe = request.RememberMe,
                            Timestamp = DateTime.UtcNow
                        };

                        return Ok(new LoginResponse
                        {
                            RequiresTwoFactor = true,
                            TwoFactorToken = tempToken,
                            Token = null,
                            RefreshToken = null,
                            ExpiresAt = null,
                            User = null
                        });
                    }

                    // Validate the provided 2FA code format first
                    if (request.TwoFactorCode.Length != 6 || !request.TwoFactorCode.All(char.IsDigit))
                    {
                        _logger.LogWarning("Invalid 2FA code format for user {Username}: length={Length}, code='{Code}'", request.Username, request.TwoFactorCode.Length, request.TwoFactorCode);
                        return BadRequest(new { message = "Two-factor authentication code must be exactly 6 digits" });
                    }

                    // Validate the provided 2FA code
                    var isValid2FA = await _twoFactorService.ValidateTotpAsync(user, request.TwoFactorCode);
                    if (!isValid2FA)
                    {
                        _logger.LogWarning("Invalid 2FA code provided for user {Username}", request.Username);

                        var invalid2FASecurityLog = new SecurityLog
                        {
                            Action = SecurityAction.LoginFailure.ToString(),
                            Success = false,
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                            Username = request.Username,
                            UserId = user.Id,
                            Details = "Invalid 2FA code",
                            FailureReason = "Two-factor authentication failed",
                            UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                            TenantId = user.TenantId
                        };
                        await _securityLogService.CreateSecurityLogAsync(invalid2FASecurityLog);

                        return Unauthorized(new { message = "Invalid two-factor authentication code" });
                    }

                    _logger.LogInformation("2FA verification successful for user {Username}", request.Username);
                }

                // For LDAP logins without an explicit tenant selection, use the default tenant context in the session/response.
                var sessionTenant = isLdapAuthenticated ? (defaultTenant ?? tenantForLdap) : tenant;
                return await CompleteSuccessfulLoginAsync(user, sessionTenant, request.Username, "Login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Username}", request.Username);
                return StatusCode(500, new { message = "An error occurred during login" });
            }
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
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
                if (user == null)
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var accessDecision = await _hrIdentityAccessService.EvaluateAsync(user.Id, HttpContext.RequestAborted);
                if (!accessDecision.IsAllowed)
                {
                    return Unauthorized(new { message = accessDecision.Message, code = accessDecision.Code });
                }

                if (user.MustChangePassword &&
                    (!user.TemporaryPasswordExpiresAtUtc.HasValue ||
                     user.TemporaryPasswordExpiresAtUtc.Value <= DateTime.UtcNow))
                {
                    return Unauthorized(new
                    {
                        code = "TEMPORARY_PASSWORD_EXPIRED",
                        message = "The temporary password has expired. Ask the supplier administrator to resend credentials."
                    });
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
                        Email = user.Email ?? string.Empty,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        CurrentTenantId = user.TenantId,
                        IsActive = user.IsActive,
                        MustChangePassword = user.MustChangePassword,
                        TemporaryPasswordExpiresAtUtc = user.TemporaryPasswordExpiresAtUtc,
                        Roles = (await _userManager.GetRolesAsync(user)).ToList(),
                        Permissions = await GetUserPermissionsAsync(user),
                        AuthenticationProvider = user.AuthenticationProvider.ToString()
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
                var currentUserId = Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null;
                var currentTenantId = _currentUserService.TenantId;
                var username = _currentUserService.UserName;

                if (!currentUserId.HasValue)
                {
                    _logger.LogWarning("Logout attempted but no current user found");
                    return Unauthorized();
                }

                // Extract JWT token information for blacklisting and session termination
                string? sessionId = null;
                string? currentTokenJti = null;
                DateTime? currentTokenExpiresAt = null;
                var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader?.StartsWith("Bearer ") == true)
                {
                    var jwt = authHeader["Bearer ".Length..].Trim();
                    try
                    {
                        var tokenHandler = new JwtSecurityTokenHandler();
                        if (!tokenHandler.CanReadToken(jwt))
                        {
                            _logger.LogWarning("Cannot read JWT token during logout. Token preview: {TokenPreview}",
                                jwt.Length > 50 ? string.Concat(jwt.AsSpan(0, 50), "...") : jwt);
                        }
                        else
                        {
                            var jsonToken = tokenHandler.ReadJwtToken(jwt);
                            currentTokenJti = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;
                            var exp = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp)?.Value;
                            sessionId = jsonToken.Claims.FirstOrDefault(x => x.Type == "sid")?.Value; // Session ID claim

                            if (!string.IsNullOrEmpty(exp) && long.TryParse(exp, out var expiresAtUnixSeconds))
                            {
                                currentTokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnixSeconds).UtcDateTime;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to blacklist JWT token during logout. Token preview: {TokenPreview}",
                            jwt.Length > 50 ? string.Concat(jwt.AsSpan(0, 50), "...") : jwt);
                        // Continue with logout even if blacklisting fails
                    }
                }

                // Terminate user session if session ID found
                if (!string.IsNullOrEmpty(sessionId))
                {
                    try
                    {
                        _logger.LogInformation("Attempting to terminate session {SessionId} for user {UserId}", sessionId, currentUserId.Value);
                        await _userSessionService.TerminateSessionAsync(sessionId, "User logout");
                        _logger.LogInformation("Successfully terminated session {SessionId} for user {UserId}", sessionId, currentUserId.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to terminate user session {SessionId} during logout", sessionId);

                        if (!string.IsNullOrEmpty(currentTokenJti) && currentTokenExpiresAt.HasValue)
                        {
                            try
                            {
                                await _jwtBlacklistService.BlacklistTokenAsync(
                                    currentTokenJti,
                                    currentUserId.Value,
                                    currentTokenExpiresAt.Value,
                                    "User logout (session termination fallback)");
                                _logger.LogInformation(
                                    "Blacklisted current JWT token with JTI {Jti} after session termination fallback for user {UserId}",
                                    currentTokenJti,
                                    currentUserId.Value);
                            }
                            catch (Exception blacklistEx)
                            {
                                _logger.LogWarning(
                                    blacklistEx,
                                    "Failed to blacklist JWT token with JTI {Jti} during logout fallback",
                                    currentTokenJti);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No session ID found in JWT token for user {UserId} logout. Attempting to terminate all active sessions.", currentUserId.Value);

                    if (!string.IsNullOrEmpty(currentTokenJti) && currentTokenExpiresAt.HasValue)
                    {
                        try
                        {
                            await _jwtBlacklistService.BlacklistTokenAsync(
                                currentTokenJti,
                                currentUserId.Value,
                                currentTokenExpiresAt.Value,
                                "User logout");
                            _logger.LogInformation("Blacklisted JWT token with JTI {Jti} for user {UserId}", currentTokenJti, currentUserId.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "Failed to blacklist JWT token during logout without a session ID. JTI: {Jti}",
                                currentTokenJti);
                        }
                    }

                    // Fallback: terminate all active sessions for this user
                    try
                    {
                        await _userSessionService.TerminateAllUserSessionsAsync(currentUserId.Value, null, "User logout (session ID not found)");
                        _logger.LogInformation("Terminated all active sessions for user {UserId} as fallback during logout", currentUserId.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to terminate sessions for user {UserId} during logout fallback", currentUserId.Value);
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
                    if (success)
                    {
                        revokedTokens = 1;
                    }
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
        [EnableRateLimiting("AuthPolicy")]
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

                // If no explicit UserTenant relationships exist, include the user's primary tenant
                if (!tenantInfoList.Any() && user.TenantId != Guid.Empty)
                {
                    var primaryTenant = await _tenantService.GetTenantByIdAsync(user.TenantId);
                    if (primaryTenant?.Status == TenantStatus.Active)
                    {
                        var tenantInfo = new UserTenantInfo
                        {
                            TenantId = primaryTenant.Id,
                            TenantCode = primaryTenant.Code,
                            TenantName = primaryTenant.Name,
                            IsDefault = true, // User's primary tenant is always default
                            AccessLevel = "Standard" // Default access level for primary tenant
                        };

                        tenantInfoList.Add(tenantInfo);
                        defaultTenant = tenantInfo;

                        _logger.LogInformation("Added user's primary tenant {TenantCode} as accessible tenant for user {Username} (no explicit UserTenant relationships found)",
                            primaryTenant.Code, request.Username);
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

                // If no explicit UserTenant relationships exist, include the user's primary tenant
                if (!accessibleTenants.Any() && user.TenantId != Guid.Empty)
                {
                    var primaryTenant = await _tenantService.GetTenantByIdAsync(user.TenantId);
                    if (primaryTenant?.Status == TenantStatus.Active)
                    {
                        accessibleTenants.Add(new UserTenantInfo
                        {
                            TenantId = primaryTenant.Id,
                            TenantCode = primaryTenant.Code,
                            TenantName = primaryTenant.Name,
                            IsDefault = true, // User's primary tenant is always default
                            AccessLevel = "Standard" // Default access level for primary tenant
                        });

                        _logger.LogInformation("Added user's primary tenant {TenantCode} as accessible tenant for user {UserId} (no explicit UserTenant relationships found)",
                            primaryTenant.Code, user.Id);
                    }
                }

                // Get current tenant info
                var currentTenant = await _tenantService.GetTenantByIdAsync(user.TenantId);

                var userInfo = new UserInfo
                {
                    Id = user.Id,
                    Username = user.UserName!,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    CurrentTenantId = user.TenantId,
                    CurrentTenantCode = currentTenant?.Code,
                    CurrentTenantName = currentTenant?.Name,
                    AccessibleTenants = accessibleTenants,
                    IsActive = user.IsActive,
                    Roles = (await _userManager.GetRolesAsync(user)).ToList(),
                    Permissions = await GetUserPermissionsAsync(user),
                    AuthenticationProvider = user.AuthenticationProvider.ToString()
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

                // Support portal hardening: if host resolves to a tenant, don't allow external (Local) users to
                // select a different tenant. This prevents cross-tenant access on a tenant-branded subdomain.
                var host = GetEffectiveHost();
                if (user.AuthenticationProvider == AuthenticationProvider.Local && IsSupportHostname(host))
                {
                    var tenantByDomain = !string.IsNullOrWhiteSpace(host)
                        ? await _tenantService.GetTenantByDomainAsync(host)
                        : null;

                    if (tenantByDomain != null && tenantByDomain.Status == TenantStatus.Active && tenantByDomain.Id != tenant.Id)
                    {
                        return BadRequest(new { message = $"This portal is restricted to tenant {tenantByDomain.Code}." });
                    }
                }

                // Validate user has access to this tenant
                // Check both explicit UserTenant relationships and user's primary tenant
                var hasExplicitAccess = await _userTenantService.HasActiveAccessAsync(Guid.Parse(userId), tenant.Id);
                var isPrimaryTenant = user.TenantId == tenant.Id;

                if (!hasExplicitAccess && !isPrimaryTenant)
                {
                    _logger.LogWarning("User {UserId} attempted to access tenant {TenantCode} without permission", userId, request.TenantCode);
                    return BadRequest(new { message = "User does not have access to this tenant" });
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

                // Extract JTI from the new token and update the current session
                try
                {
                    var sessionId = User.FindFirst("sid")?.Value;
                    if (!string.IsNullOrEmpty(sessionId) && Guid.TryParse(sessionId, out var sessionGuid))
                    {
                        var currentSession = await _context.UserSessions
                            .FirstOrDefaultAsync(s => s.SessionId == sessionGuid.ToString() && s.IsActive);

                        if (currentSession != null)
                        {
                            var tokenHandler = new JwtSecurityTokenHandler();
                            if (!tokenHandler.CanReadToken(newToken))
                            {
                                _logger.LogWarning("Cannot read new JWT token after tenant selection. Token preview: {TokenPreview}",
                                    newToken.Length > 50 ? string.Concat(newToken.AsSpan(0, 50), "...") : newToken);
                            }
                            else
                            {
                                var jsonToken = tokenHandler.ReadJwtToken(newToken);
                                var newJti = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;

                                if (!string.IsNullOrEmpty(newJti))
                                {
                                    currentSession.JwtTokenId = newJti;
                                    await _context.SaveChangesAsync();
                                    _logger.LogDebug("Updated session {SessionId} with new JTI {Jti} after tenant selection",
                                        sessionGuid, newJti);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update session with new JWT token after tenant selection");
                    // Continue with the response even if linking fails
                }

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

                // If no explicit UserTenant relationships exist, include the user's primary tenant
                if (!accessibleTenants.Any() && user.TenantId != Guid.Empty)
                {
                    var primaryTenant = await _tenantService.GetTenantByIdAsync(user.TenantId);
                    if (primaryTenant?.Status == TenantStatus.Active)
                    {
                        accessibleTenants.Add(new UserTenantInfo
                        {
                            TenantId = primaryTenant.Id,
                            TenantCode = primaryTenant.Code,
                            TenantName = primaryTenant.Name,
                            IsDefault = true, // User's primary tenant is always default
                            AccessLevel = "Standard" // Default access level for primary tenant
                        });

                        _logger.LogInformation("Added user's primary tenant {TenantCode} as accessible tenant for user {UserId} during tenant selection (no explicit UserTenant relationships found)",
                            primaryTenant.Code, user.Id);
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
                        Email = user.Email ?? string.Empty,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        CurrentTenantId = tenant.Id,
                        CurrentTenantCode = tenant.Code,
                        CurrentTenantName = tenant.Name,
                        AccessibleTenants = accessibleTenants,
                        IsActive = user.IsActive,
                        Roles = (await _userManager.GetRolesAsync(user)).ToList(),
                        Permissions = await GetUserPermissionsAsync(user),
                        AuthenticationProvider = user.AuthenticationProvider.ToString()
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
        [EnableRateLimiting("SensitivePolicy")]
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

                // Prefer tenant selection by request host/domain for multi-tenant public portals (e.g. support.company.com)
                Tenant? registrationTenant = null;
                try
                {
                    var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
                    var host = !string.IsNullOrWhiteSpace(forwardedHost) ? forwardedHost : Request.Host.Host;

                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        registrationTenant = await _tenantService.GetTenantByDomainAsync(host);
                        if (registrationTenant != null && (registrationTenant.Status != TenantStatus.Active || !registrationTenant.AllowSelfRegistration))
                        {
                            registrationTenant = null;
                        }
                    }
                }
                catch
                {
                    // Best-effort: fall back to default selection below
                }

                // Fallback: assign users to the first active tenant that allows self-registration
                var availableTenants = await _tenantService.GetAllTenantsAsync();
                registrationTenant ??= availableTenants.FirstOrDefault(t => t.Status == TenantStatus.Active && t.AllowSelfRegistration);

                if (registrationTenant == null)
                {
                    return BadRequest(new { message = "Self-registration is not currently available." });
                }

                // CAPTCHA enforcement (when enabled for tenant)
                try
                {
                    var host = GetEffectiveHost();
                    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _captchaVerificationService.EnsureCaptchaValidAsync(registrationTenant.Id, request.RecaptchaToken, host, remoteIp, HttpContext.RequestAborted);
                }
                catch (CaptchaVerificationException ex)
                {
                    return BadRequest(new { message = ex.Message });
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

                // Assign default role (ExternalUser) to the new self-registered user
                // NOTE: Do not grant internal ERP roles to external portal users.
                await _userManager.AddToRoleAsync(user, Constants.Roles.ExternalUser);

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

                // Send OTP via SMS for phone verification (best-effort; do not fail registration if SMS fails)
                try
                {
                    var phone = NormalizePhone(request.PhoneNumber);
                    var otp = await _otpService.CreateOtpAsync(
                        registrationTenant.Id,
                        OtpPurpose.PhoneVerification,
                        OtpChannel.Sms,
                        phone,
                        TimeSpan.FromMinutes(10),
                        maxAttempts: 5,
                        HttpContext.RequestAborted);

                    await _tenantSmsSender.SendAsync(
                        registrationTenant.Id,
                        phone,
                        $"Your {registrationTenant.Name} verification code is {otp}. It expires in 10 minutes.",
                        HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send phone verification OTP for user {Username}", request.Username);
                }

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
        [EnableRateLimiting("SensitivePolicy")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // CAPTCHA enforcement (when enabled for tenant)
                try
                {
                    var tenantIdForCaptcha = await ResolveTenantIdForCaptchaAsync(null);
                    var host = GetEffectiveHost();
                    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _captchaVerificationService.EnsureCaptchaValidAsync(tenantIdForCaptcha, request.RecaptchaToken, host, remoteIp, HttpContext.RequestAborted);
                }
                catch (CaptchaVerificationException ex)
                {
                    return BadRequest(new { message = ex.Message });
                }

                _logger.LogInformation("OTP verification attempt for phone: {PhoneNumber}", request.PhoneNumber);

                // Find user by phone number
                var normalizedPhone = NormalizePhone(request.PhoneNumber);
                var users = _userManager.Users.Where(u => u.PhoneNumber == normalizedPhone && !u.IsActive).ToList();
                var user = users.FirstOrDefault();

                if (user == null)
                {
                    return BadRequest(new { message = "Invalid phone number or user already verified." });
                }

                var verify = await _otpService.VerifyOtpAsync(
                    user.TenantId,
                    OtpPurpose.PhoneVerification,
                    OtpChannel.Sms,
                    normalizedPhone,
                    request.OtpCode,
                    consumeOnSuccess: true,
                    HttpContext.RequestAborted);

                if (!verify.Success)
                {
                    return BadRequest(new { message = verify.FailureReason ?? "Invalid OTP code." });
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

        [HttpPost("otp/request")]
        [AllowAnonymous]
        [EnableRateLimiting("SensitivePolicy")]
        public async Task<IActionResult> RequestLoginOtp([FromBody] RequestLoginOtpRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (!TryParseOtpChannel(request.Channel, out var channel))
                {
                    return BadRequest(new { message = "Invalid channel. Use 'Email' or 'Sms'." });
                }

                // CAPTCHA enforcement (when enabled for tenant)
                Guid tenantIdForCaptcha;
                try
                {
                    tenantIdForCaptcha = await ResolveTenantIdForCaptchaAsync(request.TenantCode);
                    var host = GetEffectiveHost();
                    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _captchaVerificationService.EnsureCaptchaValidAsync(tenantIdForCaptcha, request.RecaptchaToken, host, remoteIp, HttpContext.RequestAborted);
                }
                catch (CaptchaVerificationException ex)
                {
                    return BadRequest(new { message = ex.Message });
                }

                // Resolve tenant for lookup (prefer tenant code if provided, else host)
                Tenant? tenant = null;
                if (!string.IsNullOrWhiteSpace(request.TenantCode))
                {
                    tenant = await _tenantService.GetTenantByCodeAsync(request.TenantCode);
                }
                else
                {
                    var host = GetEffectiveHost();
                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        tenant = await _tenantService.GetTenantByDomainAsync(host);
                    }
                }

                var tenantId = tenant?.Id ?? tenantIdForCaptcha;
                var identifier = request.Identifier.Trim();
                if (channel == OtpChannel.Sms)
                {
                    identifier = NormalizePhone(identifier);
                }

                // Find user by identifier scoped to tenant; do not leak user existence in responses
                ApplicationUser? user = null;
                if (channel == OtpChannel.Email)
                {
                    var email = identifier.ToLowerInvariant();
                    user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == email && u.TenantId == tenantId);
                }
                else
                {
                    user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == identifier && u.TenantId == tenantId);
                }

                if (user != null &&
                    user.IsActive &&
                    user.AuthenticationProvider == AuthenticationProvider.Local)
                {
                    var otp = await _otpService.CreateOtpAsync(
                        tenantId,
                        OtpPurpose.Login,
                        channel,
                        identifier,
                        TimeSpan.FromMinutes(10),
                        maxAttempts: 5,
                        HttpContext.RequestAborted);

                     try
                     {
                         if (channel == OtpChannel.Email)
                         {
                             var delivered = await _emailService.SendEmailAsync(new EmailDto
                             {
                                 To = user.Email ?? identifier,
                                 Subject = "Your login code",
                                 Body = $"Your one-time login code is <strong>{otp}</strong>. It expires in 10 minutes.",
                                 IsHtml = true
                             });

                             if (!delivered)
                             {
                                 _logger.LogWarning(
                                     "Login OTP email was not delivered (tenant={TenantId} userId={UserId})",
                                     tenantId,
                                     user.Id);
                             }
                         }
                         else
                         {
                             await _tenantSmsSender.SendAsync(
                                 tenantId,
                                user.PhoneNumber ?? identifier,
                                $"Your one-time login code is {otp}. It expires in 10 minutes.",
                                HttpContext.RequestAborted);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deliver login OTP (tenant={TenantId} channel={Channel})", tenantId, channel);
                    }
                }

                return Ok(new RequestLoginOtpResponse
                {
                    Success = true,
                    Message = "If an account exists, a login code has been sent."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting login OTP");
                return StatusCode(500, new { message = "An error occurred while requesting OTP." });
            }
        }

        [HttpPost("otp/verify")]
        [AllowAnonymous]
        [EnableRateLimiting("SensitivePolicy")]
        public async Task<IActionResult> VerifyLoginOtp([FromBody] VerifyLoginOtpRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (!TryParseOtpChannel(request.Channel, out var channel))
                {
                    return BadRequest(new { message = "Invalid channel. Use 'Email' or 'Sms'." });
                }

                // CAPTCHA enforcement (when enabled for tenant)
                Guid tenantIdForCaptcha;
                try
                {
                    tenantIdForCaptcha = await ResolveTenantIdForCaptchaAsync(request.TenantCode);
                    var host = GetEffectiveHost();
                    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _captchaVerificationService.EnsureCaptchaValidAsync(tenantIdForCaptcha, request.RecaptchaToken, host, remoteIp, HttpContext.RequestAborted);
                }
                catch (CaptchaVerificationException ex)
                {
                    return BadRequest(new { message = ex.Message });
                }

                // Resolve tenant for lookup (prefer tenant code if provided, else host)
                Tenant? tenant = null;
                if (!string.IsNullOrWhiteSpace(request.TenantCode))
                {
                    tenant = await _tenantService.GetTenantByCodeAsync(request.TenantCode);
                }
                else
                {
                    var host = GetEffectiveHost();
                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        tenant = await _tenantService.GetTenantByDomainAsync(host);
                    }
                }

                var tenantId = tenant?.Id ?? tenantIdForCaptcha;
                var identifier = request.Identifier.Trim();
                if (channel == OtpChannel.Sms)
                {
                    identifier = NormalizePhone(identifier);
                }

                ApplicationUser? user = null;
                if (channel == OtpChannel.Email)
                {
                    var email = identifier.ToLowerInvariant();
                    user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == email && u.TenantId == tenantId);
                }
                else
                {
                    user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == identifier && u.TenantId == tenantId);
                }

                if (user == null || !user.IsActive || user.AuthenticationProvider != AuthenticationProvider.Local)
                {
                    return Unauthorized(new { message = "Invalid OTP credentials" });
                }

                // Optional TOTP 2FA if enabled (when enabled and 2FA code is missing, do not consume OTP yet)
                var hasTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user) && !string.IsNullOrEmpty(user.AuthenticatorKey);

                var otpVerify = await _otpService.VerifyOtpAsync(
                    tenantId,
                    OtpPurpose.Login,
                    channel,
                    identifier,
                    request.OtpCode,
                    consumeOnSuccess: !hasTwoFactorEnabled || !string.IsNullOrWhiteSpace(request.TwoFactorCode),
                    HttpContext.RequestAborted);

                if (!otpVerify.Success)
                {
                    return Unauthorized(new { message = otpVerify.FailureReason ?? "Invalid OTP code" });
                }

                if (hasTwoFactorEnabled)
                {
                    if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                    {
                        return Ok(new LoginResponse
                        {
                            RequiresTwoFactor = true,
                            TwoFactorToken = Guid.NewGuid().ToString("N"),
                            Token = null,
                            RefreshToken = null,
                            ExpiresAt = null,
                            User = null
                        });
                    }

                    if (request.TwoFactorCode.Length != 6 || !request.TwoFactorCode.All(char.IsDigit))
                    {
                        return BadRequest(new { message = "Two-factor authentication code must be exactly 6 digits" });
                    }

                    var isValid2FA = await _twoFactorService.ValidateTotpAsync(user, request.TwoFactorCode);
                    if (!isValid2FA)
                    {
                        return Unauthorized(new { message = "Invalid two-factor authentication code" });
                    }
                }

                return await CompleteSuccessfulLoginAsync(user, tenant, user.UserName ?? identifier, "Login successful (OTP)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying login OTP");
                return StatusCode(500, new { message = "An error occurred while verifying OTP." });
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
                Tenant? resolvedTenant = null;
                try
                {
                    // Prefer host-based tenant selection for public portals (e.g. support.company.com)
                    var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
                    var host = !string.IsNullOrWhiteSpace(forwardedHost) ? forwardedHost : Request.Host.Host;

                    if (!string.IsNullOrWhiteSpace(host))
                    {
                        var tenant = await _tenantService.GetTenantByDomainAsync(host);
                        if (tenant != null && tenant.Status == TenantStatus.Active)
                        {
                            resolvedTenant = tenant;
                            settings = await _settingsService.GetSecuritySettingsAsync(tenant.Id);
                        }
                    }

                    // Fallback to the default tenant settings if host-based lookup didn't resolve
                    settings ??= await _settingsService.GetPublicSecuritySettingsAsync();
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
                    tenantCode = resolvedTenant?.Code,
                    tenantName = resolvedTenant?.Name,
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

        [HttpGet("session-settings")]
        [Authorize]
        public async Task<IActionResult> GetSessionSettings()
        {
            try
            {
                var settings = await _settingsService.GetSecuritySettingsAsync();

                return Ok(new
                {
                    sessionTimeoutMinutes = settings?.SessionTimeoutMinutes ?? 30,
                    jwtTokenLifetimeMinutes = settings?.JwtTokenLifetimeMinutes ?? 60
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving authenticated session settings");
                return StatusCode(500, new { message = "An error occurred while retrieving session settings" });
            }
        }

        /// <summary>
        /// Get password policy for authenticated users
        /// </summary>
        [HttpGet("password-policy")]
        [Authorize]
        public async Task<IActionResult> GetPasswordPolicy()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var security = await _settingsService.GetSecuritySettingsAsync();

                if (security == null)
                {
                    // Return default policy if none exists
                    return Ok(new
                    {
                        minLength = 8,
                        requireUppercase = true,
                        requireLowercase = true,
                        requireDigits = true,
                        requireSpecialChars = true,
                        maxAge = 90,
                        preventReuse = 5
                    });
                }

                return Ok(new
                {
                    minLength = security.PasswordMinLength,
                    requireUppercase = security.PasswordRequireUppercase,
                    requireLowercase = security.PasswordRequireLowercase,
                    requireDigits = security.PasswordRequireDigits,
                    requireSpecialChars = security.PasswordRequireSpecialChars,
                    maxAge = security.PasswordMaxAge,
                    preventReuse = security.PasswordPreventReuse
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving password policy for user");
                return StatusCode(500, new { message = "An error occurred while retrieving password policy" });
            }
        }

        /// <summary>
        /// Request password reset email
        /// </summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [EnableRateLimiting("SensitivePolicy")]
        public async Task<IActionResult> ForgotPassword([FromBody] ErpSystem.Core.DTOs.Auth.ForgotPasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // CAPTCHA enforcement (when enabled for tenant)
                try
                {
                    var tenantIdForCaptcha = await ResolveTenantIdForCaptchaAsync(request.TenantCode);
                    var host = GetEffectiveHost();
                    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _captchaVerificationService.EnsureCaptchaValidAsync(tenantIdForCaptcha, request.CaptchaToken, host, remoteIp, HttpContext.RequestAborted);
                }
                catch (CaptchaVerificationException ex)
                {
                    return BadRequest(new { message = ex.Message });
                }

                _logger.LogInformation("Password reset requested for email: {Email}", request.Email);

                // Find user by email - use normalized email for case-insensitive search
                var normalizedEmail = request.Email.ToUpper();
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
                if (user == null)
                {
                    // Don't reveal if email exists (security best practice)
                    _logger.LogWarning("Password reset requested for non-existent email: {Email}", request.Email);
                    return Ok(new ErpSystem.Core.DTOs.Auth.ForgotPasswordResponse
                    {
                        Success = true,
                        Message = "If an account with this email exists, you will receive password reset instructions"
                    });
                }

                // Generate reset token
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var userAgent = Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";

                string resetToken;
                try
                {
                    resetToken = await _passwordResetService.GeneratePasswordResetTokenAsync(
                        user.Id, ipAddress, userAgent);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("LDAP"))
                {
                    _logger.LogWarning("LDAP user attempted password reset: {Email}", request.Email);
                    // Return generic message to not reveal LDAP authentication method
                    return Ok(new ErpSystem.Core.DTOs.Auth.ForgotPasswordResponse
                    {
                        Success = false,
                        Message = "Your account is managed through your organization's directory service. Please contact your system administrator to reset your password."
                    });
                }

                // Send email with reset link
                try
                {
                    // Read frontend URL from configuration
                    var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:3000";
                    var resetUrl = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(user.Email ?? string.Empty)}";

                    _logger.LogInformation("Generated password reset URL: {ResetUrl}", resetUrl);

                    var tenantName = await ResolvePasswordResetTenantNameAsync(user, request.TenantCode);

                    var emailDto = new ErpSystem.Core.Interfaces.Common.EmailDto
                    {
                        To = user.Email ?? string.Empty,
                        Subject = "Password Reset Request",
                        Body = GeneratePasswordResetEmailBody(user.FirstName, resetUrl, tenantName),
                        IsHtml = true
                    };

                    var emailSent = await _emailService.SendEmailAsync(emailDto);
                    if (emailSent)
                    {
                        _logger.LogInformation("Password reset email sent to {Email}", user.Email);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send password reset email to {Email} - email service returned false", user.Email);
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Exception occurred while sending password reset email to {Email}", user.Email);
                    // Don't fail the request - token was generated, email might be resent
                }

                return Ok(new ErpSystem.Core.DTOs.Auth.ForgotPasswordResponse
                {
                    Success = true,
                    Message = "If an account with this email exists, you will receive password reset instructions"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password request");
                return StatusCode(500, new { message = "An error occurred during password reset request" });
            }
        }

        /// <summary>
        /// Reset password with token
        /// </summary>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting("SensitivePolicy")]
        public async Task<IActionResult> ResetPassword([FromBody] ErpSystem.Core.DTOs.Auth.ResetPasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // CAPTCHA enforcement (when enabled for tenant)
                try
                {
                    var tenantIdForCaptcha = await ResolveTenantIdForCaptchaAsync(null);
                    var host = GetEffectiveHost();
                    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _captchaVerificationService.EnsureCaptchaValidAsync(tenantIdForCaptcha, request.CaptchaToken, host, remoteIp, HttpContext.RequestAborted);
                }
                catch (CaptchaVerificationException ex)
                {
                    return BadRequest(new { message = ex.Message });
                }

                _logger.LogInformation("Password reset attempt for email: {Email}", request.Email);

                // Find user by email
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return BadRequest(new { message = "Invalid email or reset token" });
                }

                // Reset password
                var success = await _passwordResetService.ResetPasswordAsync(
                    user.Id, request.ResetToken, request.NewPassword);

                if (!success)
                {
                    _logger.LogWarning("Password reset failed for user {UserId}", user.Id);
                    return BadRequest(new { message = "Invalid or expired reset token" });
                }

                _logger.LogInformation("Password successfully reset for user {UserId}", user.Id);

                return Ok(new ErpSystem.Core.DTOs.Auth.ResetPasswordResponse
                {
                    Success = true,
                    Message = "Password has been reset successfully. You can now login with your new password."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset");
                return StatusCode(500, new { message = "An error occurred during password reset" });
            }
        }

        /// <summary>
        /// Validate password reset token
        /// </summary>
        [HttpPost("validate-reset-token")]
        [AllowAnonymous]
        [EnableRateLimiting("SensitivePolicy")]
        public async Task<IActionResult> ValidateResetToken([FromBody] ErpSystem.Core.DTOs.Auth.ValidateResetTokenRequest request)
        {
            try
            {
                // CAPTCHA enforcement (when enabled for tenant)
                try
                {
                    var tenantIdForCaptcha = await ResolveTenantIdForCaptchaAsync(null);
                    var host = GetEffectiveHost();
                    var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _captchaVerificationService.EnsureCaptchaValidAsync(tenantIdForCaptcha, request.CaptchaToken, host, remoteIp, HttpContext.RequestAborted);
                }
                catch (CaptchaVerificationException ex)
                {
                    return BadRequest(new { message = ex.Message });
                }

                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return Ok(new ErpSystem.Core.DTOs.Auth.ValidateResetTokenResponse
                    {
                        IsValid = false,
                        Message = "Invalid email or token"
                    });
                }

                var isValid = await _passwordResetService.ValidateResetTokenAsync(user.Id, request.ResetToken);

                if (isValid)
                {
                    return Ok(new ErpSystem.Core.DTOs.Auth.ValidateResetTokenResponse
                    {
                        IsValid = true,
                        Message = "Token is valid"
                    });
                }
                else
                {
                    return Ok(new ErpSystem.Core.DTOs.Auth.ValidateResetTokenResponse
                    {
                        IsValid = false,
                        Message = "Token is invalid or has expired"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating reset token");
                return StatusCode(500, new { message = "An error occurred during token validation" });
            }
        }

        private static string GenerateDeviceFingerprint(string ipAddress, string userAgent)
        {
            // Create a simple device fingerprint using IP and User Agent
            var combined = $"{ipAddress}|{userAgent}";
            var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined));
            return Convert.ToBase64String(hashBytes)[..16]; // Take first 16 characters
        }

        private async Task<string> ResolvePasswordResetTenantNameAsync(ApplicationUser user, string? tenantCode)
        {
            Tenant? tenant = null;

            if (!string.IsNullOrWhiteSpace(tenantCode))
            {
                tenant = await _context.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t =>
                        !t.IsDeleted &&
                        t.Code == tenantCode &&
                        (t.Id == user.TenantId || t.UserTenants.Any(ut => ut.UserId == user.Id && !ut.IsDeleted)));
            }

            tenant ??= await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => !t.IsDeleted && t.Id == user.TenantId);

            tenant ??= await _context.UserTenants
                .AsNoTracking()
                .Where(ut => !ut.IsDeleted && ut.UserId == user.Id && ut.Status == UserTenantStatus.Active)
                .Where(ut => ut.Tenant != null && !ut.Tenant.IsDeleted)
                .Select(ut => ut.Tenant)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(tenant?.Name) ? "ERP System" : tenant.Name;
        }

        private static string GeneratePasswordResetEmailBody(string firstName, string resetUrl, string tenantName)
        {
            var displayFirstName = System.Net.WebUtility.HtmlEncode(firstName);
            var displayTenantName = System.Net.WebUtility.HtmlEncode(tenantName);
            var displayResetUrl = System.Net.WebUtility.HtmlEncode(resetUrl);

            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #f8f9fa; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; }}
                        .button {{ background-color: #007bff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; display: inline-block; }}
                        .footer {{ background-color: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Password Reset Request</h2>
                        </div>
                        <div class='content'>
                            <p>Hello {displayFirstName},</p>
                            <p>We received a request to reset your password. Click the button below to set a new password:</p>
                            <p style='text-align: center; margin: 30px 0;'>
                                <a href='{displayResetUrl}' class='button'>Reset Password</a>
                            </p>
                            <p>Or copy and paste this link in your browser:</p>
                            <p><code>{displayResetUrl}</code></p>
                            <p><strong>This link will expire in 15 minutes.</strong></p>
                            <p>If you did not request a password reset, you can ignore this email.</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; {DateTime.Now.Year} {displayTenantName}. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>
            ";
        }
    }
}
