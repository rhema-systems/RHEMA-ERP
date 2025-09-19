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

    /// <summary>
    /// Get password policy
    /// </summary>
    [HttpGet("password-policy")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<PasswordPolicyDto>> GetPasswordPolicy()
    {
        try
        {
            var policy = await _settingsService.GetPasswordPolicyAsync();
            if (policy == null)
            {
                // Return default policy if none exists
                return Ok(new PasswordPolicyDto
                {
                    MinLength = 8,
                    RequireUppercase = true,
                    RequireLowercase = true,
                    RequireDigits = true,
                    RequireSpecialChars = true,
                    MaxAge = 90,
                    PreventReuse = 5
                });
            }

            return Ok(new PasswordPolicyDto
            {
                MinLength = policy.MinLength,
                RequireUppercase = policy.RequireUppercase,
                RequireLowercase = policy.RequireLowercase,
                RequireDigits = policy.RequireDigits,
                RequireSpecialChars = policy.RequireSpecialChars,
                MaxAge = policy.MaxAge,
                PreventReuse = policy.PreventReuse
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving password policy");
            return StatusCode(500, "An error occurred while retrieving password policy");
        }
    }

    /// <summary>
    /// Create password policy
    /// </summary>
    [HttpPost("password-policy")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<PasswordPolicyDto>> CreatePasswordPolicy([FromBody] PasswordPolicyDto request)
    {
        try
        {
            // Check if policy already exists
            var existingPolicy = await _settingsService.GetPasswordPolicyAsync();
            if (existingPolicy != null)
            {
                return Conflict("Password policy already exists. Use PUT to update it.");
            }

            var createdPolicy = await _settingsService.UpdatePasswordPolicyAsync(new Core.Entities.PasswordPolicy
            {
                MinLength = request.MinLength,
                RequireUppercase = request.RequireUppercase,
                RequireLowercase = request.RequireLowercase,
                RequireDigits = request.RequireDigits,
                RequireSpecialChars = request.RequireSpecialChars,
                MaxAge = request.MaxAge,
                PreventReuse = request.PreventReuse
            });

            return CreatedAtAction(nameof(GetPasswordPolicy), null, new PasswordPolicyDto
            {
                MinLength = createdPolicy.MinLength,
                RequireUppercase = createdPolicy.RequireUppercase,
                RequireLowercase = createdPolicy.RequireLowercase,
                RequireDigits = createdPolicy.RequireDigits,
                RequireSpecialChars = createdPolicy.RequireSpecialChars,
                MaxAge = createdPolicy.MaxAge,
                PreventReuse = createdPolicy.PreventReuse
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating password policy");
            return StatusCode(500, "An error occurred while creating password policy");
        }
    }

    /// <summary>
    /// Update password policy
    /// </summary>
    [HttpPut("password-policy")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<PasswordPolicyDto>> UpdatePasswordPolicy([FromBody] PasswordPolicyDto request)
    {
        try
        {
            // Check if policy exists
            var existingPolicy = await _settingsService.GetPasswordPolicyAsync();
            if (existingPolicy == null)
            {
                return NotFound("Password policy not found. Use POST to create it first.");
            }

            var updatedPolicy = await _settingsService.UpdatePasswordPolicyAsync(new Core.Entities.PasswordPolicy
            {
                MinLength = request.MinLength,
                RequireUppercase = request.RequireUppercase,
                RequireLowercase = request.RequireLowercase,
                RequireDigits = request.RequireDigits,
                RequireSpecialChars = request.RequireSpecialChars,
                MaxAge = request.MaxAge,
                PreventReuse = request.PreventReuse
            });

            return Ok(new PasswordPolicyDto
            {
                MinLength = updatedPolicy.MinLength,
                RequireUppercase = updatedPolicy.RequireUppercase,
                RequireLowercase = updatedPolicy.RequireLowercase,
                RequireDigits = updatedPolicy.RequireDigits,
                RequireSpecialChars = updatedPolicy.RequireSpecialChars,
                MaxAge = updatedPolicy.MaxAge,
                PreventReuse = updatedPolicy.PreventReuse
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating password policy");
            return StatusCode(500, "An error occurred while updating password policy");
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

public class PasswordPolicyDto
{
    public int MinLength { get; set; }
    public bool RequireUppercase { get; set; }
    public bool RequireLowercase { get; set; }
    public bool RequireDigits { get; set; }
    public bool RequireSpecialChars { get; set; }
    public int? MaxAge { get; set; }
    public int? PreventReuse { get; set; }
}