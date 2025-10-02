using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Services;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ISettingsService settingsService, IAuditLogService auditLogService, ICurrentUserService currentUserService, ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet("security")]
        [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
        public async Task<ActionResult<SecuritySettingsDto>> GetSecuritySettings()
        {
            try
            {
                var settings = await _settingsService.GetSecuritySettingsAsync();
                if (settings == null)
                {
                    // Return default security settings
                    return Ok(new SecuritySettingsDto
                    {
                        // Password Policy
                        PasswordMinLength = 8,
                        PasswordRequireUppercase = true,
                        PasswordRequireLowercase = true,
                        PasswordRequireDigits = true,
                        PasswordRequireSpecialChars = true,
                        PasswordMaxAge = 90,
                        PasswordPreventReuse = 5,

                        // Session & Token
                        SessionTimeoutMinutes = 30,
                        JwtTokenLifetimeMinutes = 60,
                        PreventConcurrentLogin = "Disabled",

                        // Lockout Settings
                        MaxFailedLoginAttempts = 5,
                        AccountLockoutMinutes = 30,
                        RateLimitLoginMaxAttempts = 5,
                        RateLimitLoginWindowMinutes = 15,
                        RateLimitLoginBlockDurationMinutes = 30,

                        // CAPTCHA Settings
                        CaptchaEnabled = false,
                        CaptchaProvider = "recaptcha",
                        RecaptchaSiteKey = null,
                        RecaptchaSecretKey = null,
                        HCaptchaSiteKey = null,
                        HCaptchaSecretKey = null,

                        // Legal URLs
                        TermsOfServiceUrl = null,
                        PrivacyPolicyUrl = null
                    });
                }

                return Ok(new SecuritySettingsDto
                {
                    // Password Policy
                    PasswordMinLength = settings.PasswordMinLength,
                    PasswordRequireUppercase = settings.PasswordRequireUppercase,
                    PasswordRequireLowercase = settings.PasswordRequireLowercase,
                    PasswordRequireDigits = settings.PasswordRequireDigits,
                    PasswordRequireSpecialChars = settings.PasswordRequireSpecialChars,
                    PasswordMaxAge = settings.PasswordMaxAge,
                    PasswordPreventReuse = settings.PasswordPreventReuse,

                    // Session & Token
                    SessionTimeoutMinutes = settings.SessionTimeoutMinutes,
                    JwtTokenLifetimeMinutes = settings.JwtTokenLifetimeMinutes,
                    PreventConcurrentLogin = settings.PreventConcurrentLogin.ToString(),

                    // Lockout Settings
                    MaxFailedLoginAttempts = settings.MaxFailedLoginAttempts,
                    AccountLockoutMinutes = settings.AccountLockoutMinutes,
                    RateLimitLoginMaxAttempts = settings.RateLimitLoginMaxAttempts,
                    RateLimitLoginWindowMinutes = settings.RateLimitLoginWindowMinutes,
                    RateLimitLoginBlockDurationMinutes = settings.RateLimitLoginBlockDurationMinutes,

                    // CAPTCHA Settings
                    CaptchaEnabled = settings.CaptchaEnabled,
                    CaptchaProvider = settings.CaptchaProvider,
                    RecaptchaSiteKey = settings.RecaptchaSiteKey,
                    RecaptchaSecretKey = settings.RecaptchaSecretKey,
                    HCaptchaSiteKey = settings.HCaptchaSiteKey,
                    HCaptchaSecretKey = settings.HCaptchaSecretKey,

                    // Legal URLs
                    TermsOfServiceUrl = settings.TermsOfServiceUrl,
                    PrivacyPolicyUrl = settings.PrivacyPolicyUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving security settings");
                return StatusCode(500, "An error occurred while retrieving security settings");
            }
        }

        [HttpPut("security")]
        [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
        public async Task<ActionResult<SecuritySettingsDto>> UpdateSecuritySettings([FromBody] SecuritySettingsDto request)
        {
            try
            {
                // Get existing settings for audit logging
                var existingSettings = await _settingsService.GetSecuritySettingsAsync();
                
                // Convert DTO to Security entity
                var securitySettings = new Core.Entities.Security
                {
                    // Password Policy
                    PasswordMinLength = request.PasswordMinLength,
                    PasswordRequireUppercase = request.PasswordRequireUppercase,
                    PasswordRequireLowercase = request.PasswordRequireLowercase,
                    PasswordRequireDigits = request.PasswordRequireDigits,
                    PasswordRequireSpecialChars = request.PasswordRequireSpecialChars,
                    PasswordMaxAge = request.PasswordMaxAge,
                    PasswordPreventReuse = request.PasswordPreventReuse,
                    
                    // Session & Token Settings
                    SessionTimeoutMinutes = request.SessionTimeoutMinutes,
                    JwtTokenLifetimeMinutes = request.JwtTokenLifetimeMinutes,
                    PreventConcurrentLogin = Enum.Parse<Core.Enums.PreventConcurrentLogin>(request.PreventConcurrentLogin),
                    
                    // Lockout Settings
                    MaxFailedLoginAttempts = request.MaxFailedLoginAttempts,
                    AccountLockoutMinutes = request.AccountLockoutMinutes,
                    RateLimitLoginMaxAttempts = request.RateLimitLoginMaxAttempts,
                    RateLimitLoginWindowMinutes = request.RateLimitLoginWindowMinutes,
                    RateLimitLoginBlockDurationMinutes = request.RateLimitLoginBlockDurationMinutes,
                    
                    // CAPTCHA Settings
                    CaptchaEnabled = request.CaptchaEnabled,
                    CaptchaProvider = request.CaptchaProvider,
                    RecaptchaSiteKey = request.RecaptchaSiteKey,
                    RecaptchaSecretKey = request.RecaptchaSecretKey,
                    HCaptchaSiteKey = request.HCaptchaSiteKey,
                    HCaptchaSecretKey = request.HCaptchaSecretKey,

                    // Legal URLs
                    TermsOfServiceUrl = request.TermsOfServiceUrl,
                    PrivacyPolicyUrl = request.PrivacyPolicyUrl
                };

                var updatedSettings = await _settingsService.UpdateSecuritySettingsAsync(securitySettings);

                // Log the audit event
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var usernameClaim = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
                var tenantId = _currentUserService.GetTenantId();
                
                if (Guid.TryParse(userIdClaim, out var userId) && !string.IsNullOrEmpty(usernameClaim) && tenantId.HasValue)
                {
                    var auditLog = new Core.Entities.AuditLog
                    {
                        UserId = userId,
                        Username = usernameClaim,
                        Action = existingSettings == null ? "CREATE" : "UPDATE",
                        Resource = "SecuritySettings",
                        ResourceId = updatedSettings.Id.ToString(),
                        OldValues = existingSettings != null ? System.Text.Json.JsonSerializer.Serialize(existingSettings) : null,
                        NewValues = System.Text.Json.JsonSerializer.Serialize(request),
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                        TenantId = tenantId.Value
                    };
                    
                    await _auditLogService.CreateAuditLogAsync(auditLog);
                }

                // Convert back to DTO for response
                var responseDto = new SecuritySettingsDto
                {
                    PasswordMinLength = updatedSettings.PasswordMinLength,
                    PasswordRequireUppercase = updatedSettings.PasswordRequireUppercase,
                    PasswordRequireLowercase = updatedSettings.PasswordRequireLowercase,
                    PasswordRequireDigits = updatedSettings.PasswordRequireDigits,
                    PasswordRequireSpecialChars = updatedSettings.PasswordRequireSpecialChars,
                    PasswordMaxAge = updatedSettings.PasswordMaxAge,
                    PasswordPreventReuse = updatedSettings.PasswordPreventReuse,
                    SessionTimeoutMinutes = updatedSettings.SessionTimeoutMinutes,
                    JwtTokenLifetimeMinutes = updatedSettings.JwtTokenLifetimeMinutes,
                    PreventConcurrentLogin = updatedSettings.PreventConcurrentLogin.ToString(),
                    MaxFailedLoginAttempts = updatedSettings.MaxFailedLoginAttempts,
                    AccountLockoutMinutes = updatedSettings.AccountLockoutMinutes,
                    RateLimitLoginMaxAttempts = updatedSettings.RateLimitLoginMaxAttempts,
                    RateLimitLoginWindowMinutes = updatedSettings.RateLimitLoginWindowMinutes,
                    RateLimitLoginBlockDurationMinutes = updatedSettings.RateLimitLoginBlockDurationMinutes,
                    CaptchaEnabled = updatedSettings.CaptchaEnabled,
                    CaptchaProvider = updatedSettings.CaptchaProvider,
                    RecaptchaSiteKey = updatedSettings.RecaptchaSiteKey,
                    RecaptchaSecretKey = updatedSettings.RecaptchaSecretKey,
                    HCaptchaSiteKey = updatedSettings.HCaptchaSiteKey,
                    HCaptchaSecretKey = updatedSettings.HCaptchaSecretKey,
                    TermsOfServiceUrl = updatedSettings.TermsOfServiceUrl,
                    PrivacyPolicyUrl = updatedSettings.PrivacyPolicyUrl
                };
                
                return Ok(responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating security settings");
                return StatusCode(500, "An error occurred while updating security settings");
            }
        }

    /// <summary>
    /// Get email settings
    /// </summary>
    [HttpGet("email")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<EmailSettingsDto>> GetEmailSettings()
    {
        try
        {
            var settings = await _settingsService.GetEmailSettingsAsync();
            if (settings == null)
            {
                // Return default settings if none exist
                return Ok(new EmailSettingsDto
                {
                    SmtpHost = "",
                    SmtpPort = 587,
                    SmtpUsername = "",
                    SmtpPassword = "",
                    UseTLS = true,
                    FromAddress = "",
                    FromName = ""
                });
            }

            return Ok(new EmailSettingsDto
            {
                SmtpHost = settings.SmtpHost,
                SmtpPort = settings.SmtpPort,
                SmtpUsername = settings.SmtpUsername,
                SmtpPassword = settings.SmtpPassword, // In production, don't return the password
                UseTLS = settings.UseTLS,
                FromAddress = settings.FromAddress,
                FromName = settings.FromName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email settings");
            return StatusCode(500, "An error occurred while retrieving email settings");
        }
    }

    /// <summary>
    /// Create email settings
    /// </summary>
    [HttpPost("email")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<EmailSettingsDto>> CreateEmailSettings([FromBody] EmailSettingsDto request)
    {
        try
        {
            // Check if settings already exist
            var existingSettings = await _settingsService.GetEmailSettingsAsync();
            if (existingSettings != null)
            {
                return Conflict("Email settings already exist. Use PUT to update them.");
            }

            var createdSettings = await _settingsService.UpdateEmailSettingsAsync(new Core.Entities.EmailSettings
            {
                SmtpHost = request.SmtpHost,
                SmtpPort = request.SmtpPort,
                SmtpUsername = request.SmtpUsername,
                SmtpPassword = request.SmtpPassword,
                UseTLS = request.UseTLS,
                FromAddress = request.FromAddress,
                FromName = request.FromName
            });

            // Log the audit event
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var usernameClaim = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
            var tenantId = _currentUserService.GetTenantId();
            
            if (Guid.TryParse(userIdClaim, out var userId) && !string.IsNullOrEmpty(usernameClaim) && tenantId.HasValue)
            {
                var auditLog = new Core.Entities.AuditLog
                {
                    UserId = userId,
                    Username = usernameClaim,
                    Action = "CREATE",
                    Resource = "EmailSettings",
                    ResourceId = createdSettings.Id.ToString(),
                    OldValues = null,
                    NewValues = System.Text.Json.JsonSerializer.Serialize(request),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                    TenantId = tenantId.Value
                };
                
                await _auditLogService.CreateAuditLogAsync(auditLog);
            }

            return CreatedAtAction(nameof(GetEmailSettings), null, new EmailSettingsDto
            {
                SmtpHost = createdSettings.SmtpHost,
                SmtpPort = createdSettings.SmtpPort,
                SmtpUsername = createdSettings.SmtpUsername,
                SmtpPassword = createdSettings.SmtpPassword,
                UseTLS = createdSettings.UseTLS,
                FromAddress = createdSettings.FromAddress,
                FromName = createdSettings.FromName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating email settings");
            return StatusCode(500, "An error occurred while creating email settings");
        }
    }

    /// <summary>
    /// Update email settings
    /// </summary>
    [HttpPut("email")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<EmailSettingsDto>> UpdateEmailSettings([FromBody] EmailSettingsDto request)
    {
        try
        {
            // Check if settings exist
            var existingSettings = await _settingsService.GetEmailSettingsAsync();
            if (existingSettings == null)
            {
                return NotFound("Email settings not found. Use POST to create them first.");
            }

            var updatedSettings = await _settingsService.UpdateEmailSettingsAsync(new Core.Entities.EmailSettings
            {
                SmtpHost = request.SmtpHost,
                SmtpPort = request.SmtpPort,
                SmtpUsername = request.SmtpUsername,
                SmtpPassword = request.SmtpPassword,
                UseTLS = request.UseTLS,
                FromAddress = request.FromAddress,
                FromName = request.FromName
            });

            // Log the audit event
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var usernameClaim = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
            var tenantId = _currentUserService.GetTenantId();
            
            if (Guid.TryParse(userIdClaim, out var userId) && !string.IsNullOrEmpty(usernameClaim) && tenantId.HasValue)
            {
                var auditLog = new Core.Entities.AuditLog
                {
                    UserId = userId,
                    Username = usernameClaim,
                    Action = "UPDATE",
                    Resource = "EmailSettings",
                    ResourceId = updatedSettings.Id.ToString(),
                    OldValues = System.Text.Json.JsonSerializer.Serialize(existingSettings),
                    NewValues = System.Text.Json.JsonSerializer.Serialize(request),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                    TenantId = tenantId.Value
                };
                
                await _auditLogService.CreateAuditLogAsync(auditLog);
            }

            return Ok(new EmailSettingsDto
            {
                SmtpHost = updatedSettings.SmtpHost,
                SmtpPort = updatedSettings.SmtpPort,
                SmtpUsername = updatedSettings.SmtpUsername,
                SmtpPassword = updatedSettings.SmtpPassword,
                UseTLS = updatedSettings.UseTLS,
                FromAddress = updatedSettings.FromAddress,
                FromName = updatedSettings.FromName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating email settings");
            return StatusCode(500, "An error occurred while updating email settings");
        }
    }

    /// <summary>
    /// Test email settings
    /// </summary>
    [HttpPost("email/test")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<TestEmailResultDto>> TestEmailSettings([FromBody] TestEmailRequest request)
    {
        try
        {
            var emailSettings = new Core.Entities.EmailSettings
            {
                SmtpHost = request.Settings.SmtpHost,
                SmtpPort = request.Settings.SmtpPort,
                SmtpUsername = request.Settings.SmtpUsername,
                SmtpPassword = request.Settings.SmtpPassword,
                UseTLS = request.Settings.UseTLS,
                FromAddress = request.Settings.FromAddress,
                FromName = request.Settings.FromName
            };

            var success = await _settingsService.TestEmailSettingsAsync(emailSettings, request.TestEmail);

            return Ok(new TestEmailResultDto
            {
                Success = success,
                Message = success ? "Test email sent successfully!" : "Failed to send test email. Please check your settings."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing email settings");
            return Ok(new TestEmailResultDto
            {
                Success = false,
                Message = $"Error testing email settings: {ex.Message}"
            });
        }
    }

}

// DTOs
public class EmailSettingsDto
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool UseTLS { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}

public class TestEmailRequest
{
    public EmailSettingsDto Settings { get; set; } = new();
    public string TestEmail { get; set; } = string.Empty;
}

    public class TestEmailResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SecuritySettingsDto
    {
        // Password Policy
        public int PasswordMinLength { get; set; }
        public bool PasswordRequireUppercase { get; set; }
        public bool PasswordRequireLowercase { get; set; }
        public bool PasswordRequireDigits { get; set; }
        public bool PasswordRequireSpecialChars { get; set; }
        public int? PasswordMaxAge { get; set; }
        public int? PasswordPreventReuse { get; set; }

        // Session & Token
        public int SessionTimeoutMinutes { get; set; }
        public int JwtTokenLifetimeMinutes { get; set; }
        public string PreventConcurrentLogin { get; set; } = "Disabled";

        // Lockout & Rate Limiting
        public int MaxFailedLoginAttempts { get; set; }
        public int AccountLockoutMinutes { get; set; }
        public int RateLimitLoginMaxAttempts { get; set; }
        public int RateLimitLoginWindowMinutes { get; set; }
        public int RateLimitLoginBlockDurationMinutes { get; set; }

        // CAPTCHA
        public bool CaptchaEnabled { get; set; }
        public string CaptchaProvider { get; set; } = "recaptcha";
        public string? RecaptchaSiteKey { get; set; }
        public string? RecaptchaSecretKey { get; set; }
        public string? HCaptchaSiteKey { get; set; }
        public string? HCaptchaSecretKey { get; set; }

        // Legal URLs
        public string? TermsOfServiceUrl { get; set; }
        public string? PrivacyPolicyUrl { get; set; }
    }

