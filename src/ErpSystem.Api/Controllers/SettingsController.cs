using System.Security.Claims;
using ErpSystem.Api.Services.Sms;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantSmsSender _tenantSmsSender;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ISettingsService settingsService,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService,
        ITenantSmsSender tenantSmsSender,
        ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
        _tenantSmsSender = tenantSmsSender;
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
            var tenantId = _currentUserService.TenantId;

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
            var tenantId = _currentUserService.TenantId;

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
            var tenantId = _currentUserService.TenantId;

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

    private async Task TryAuditAsync(string action, string resource, string resourceId, object? oldValues, object? newValues)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var usernameClaim = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
            var tenantId = _currentUserService.TenantId;

            if (!Guid.TryParse(userIdClaim, out var userId) || string.IsNullOrEmpty(usernameClaim) || !tenantId.HasValue)
            {
                return;
            }

            var auditLog = new Core.Entities.AuditLog
            {
                UserId = userId,
                Username = usernameClaim,
                Action = action,
                Resource = resource,
                ResourceId = resourceId,
                OldValues = oldValues == null ? null : System.Text.Json.JsonSerializer.Serialize(oldValues),
                NewValues = newValues == null ? null : System.Text.Json.JsonSerializer.Serialize(newValues),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                TenantId = tenantId.Value
            };

            await _auditLogService.CreateAuditLogAsync(auditLog);
        }
        catch
        {
            // Best-effort audit logging. Do not fail the request.
        }
    }

    /// <summary>
    /// Get SMS settings (per tenant)
    /// </summary>
    [HttpGet("sms")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<SmsSettingsDto>> GetSmsSettings()
    {
        try
        {
            var settings = await _settingsService.GetSmsSettingsAsync();
            if (settings == null)
            {
                return Ok(new SmsSettingsDto
                {
                    IsConfigured = false,
                    DefaultProvider = "GhanaGateway",
                    FallbackProvidersCsv = "",
                    TwilioEnabled = false,
                    TwilioAccountSid = "",
                    TwilioAuthToken = "",
                    TwilioFromNumber = "",
                    GhanaGatewayEnabled = false,
                    GhanaGatewayUrlTemplate = ErpSystem.Api.Services.Sms.MNotifySmsGateway.DefaultEndpoint,
                    GhanaGatewayApiKey = "",
                    GhanaGatewaySenderId = "",
                    GhanaGatewayTimeoutSeconds = 10
                });
            }

            return Ok(ToSmsSettingsDto(settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SMS settings");
            return StatusCode(500, "An error occurred while retrieving SMS settings");
        }
    }

    /// <summary>
    /// Create SMS settings (per tenant)
    /// </summary>
    [HttpPost("sms")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<SmsSettingsDto>> CreateSmsSettings([FromBody] SmsSettingsDto request)
    {
        try
        {
            var existingSettings = await _settingsService.GetSmsSettingsAsync();
            if (existingSettings != null)
            {
                return Conflict("SMS settings already exist. Use PUT to update them.");
            }

            var validation = ValidateSmsSettings(request, existingSettings);
            if (validation is not null)
                return BadRequest(validation);

            var created = await _settingsService.UpdateSmsSettingsAsync(new Core.Entities.SmsSettings
            {
                DefaultProvider = request.DefaultProvider,
                FallbackProvidersJson = SmsSettingsDto.FallbackCsvToJson(request.FallbackProvidersCsv),
                TwilioEnabled = request.TwilioEnabled,
                TwilioAccountSid = request.TwilioAccountSid,
                TwilioAuthToken = request.TwilioAuthToken,
                TwilioFromNumber = request.TwilioFromNumber,
                GhanaGatewayEnabled = request.GhanaGatewayEnabled,
                GhanaGatewayUrlTemplate = request.GhanaGatewayUrlTemplate,
                GhanaGatewayApiKey = request.GhanaGatewayApiKey,
                GhanaGatewaySenderId = request.GhanaGatewaySenderId,
                GhanaGatewayTimeoutSeconds = request.GhanaGatewayTimeoutSeconds
            });

            await TryAuditAsync("CREATE", "SmsSettings", created.Id.ToString(), oldValues: null, newValues: ToSmsAuditValues(request));

            return CreatedAtAction(nameof(GetSmsSettings), null, ToSmsSettingsDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SMS settings");
            return StatusCode(500, "An error occurred while creating SMS settings");
        }
    }

    /// <summary>
    /// Update SMS settings (per tenant)
    /// </summary>
    [HttpPut("sms")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<SmsSettingsDto>> UpdateSmsSettings([FromBody] SmsSettingsDto request)
    {
        try
        {
            var existing = await _settingsService.GetSmsSettingsAsync();
            if (existing == null)
            {
                return NotFound("SMS settings not found. Use POST to create them first.");
            }

            var validation = ValidateSmsSettings(request, existing);
            if (validation is not null)
                return BadRequest(validation);

            var oldAuditValues = ToSmsAuditValues(existing);
            var updated = await _settingsService.UpdateSmsSettingsAsync(new Core.Entities.SmsSettings
            {
                DefaultProvider = request.DefaultProvider,
                FallbackProvidersJson = SmsSettingsDto.FallbackCsvToJson(request.FallbackProvidersCsv),
                TwilioEnabled = request.TwilioEnabled,
                TwilioAccountSid = request.TwilioAccountSid,
                TwilioAuthToken = request.TwilioAuthToken,
                TwilioFromNumber = request.TwilioFromNumber,
                GhanaGatewayEnabled = request.GhanaGatewayEnabled,
                GhanaGatewayUrlTemplate = request.GhanaGatewayUrlTemplate,
                GhanaGatewayApiKey = request.GhanaGatewayApiKey,
                GhanaGatewaySenderId = request.GhanaGatewaySenderId,
                GhanaGatewayTimeoutSeconds = request.GhanaGatewayTimeoutSeconds
            });

            await TryAuditAsync("UPDATE", "SmsSettings", updated.Id.ToString(), oldValues: oldAuditValues, newValues: ToSmsAuditValues(request));

            return Ok(ToSmsSettingsDto(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SMS settings");
            return StatusCode(500, "An error occurred while updating SMS settings");
        }
    }

    [HttpGet("sms/balance")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<MNotifyBalanceDto>> GetSmsBalance()
    {
        var tenantId = _currentUserService.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return BadRequest(SmsProblem("SMS tenant is missing", "Select a tenant before checking the SMS balance.", "SMS_TENANT_REQUIRED"));

        try
        {
            var balance = await _tenantSmsSender.GetMNotifyBalanceAsync(tenantId.Value, HttpContext.RequestAborted);
            return Ok(new MNotifyBalanceDto { Balance = balance.Balance, Bonus = balance.Bonus });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "mNotify balance check failed for tenant {TenantId}", tenantId.Value);
            return BadRequest(SmsProblem("SMS balance could not be checked", ex.Message, "MNOTIFY_BALANCE_FAILED"));
        }
    }

    [HttpPost("sms/test")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<TestSmsResultDto>> SendTestSms([FromBody] SendTestSmsRequestDto request)
    {
        var tenantId = _currentUserService.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return BadRequest(SmsProblem("SMS tenant is missing", "Select a tenant before sending a test SMS.", "SMS_TENANT_REQUIRED"));

        try
        {
            _ = MNotifySmsGateway.NormalizeRecipient(request.PhoneNumber);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(SmsProblem("Test SMS recipient is invalid", ex.Message, "SMS_RECIPIENT_INVALID"));
        }

        try
        {
            await _tenantSmsSender.SendAsync(
                tenantId.Value,
                request.PhoneNumber.Trim(),
                "Rhema ERP test SMS. Your tenant SMS configuration is working.",
                HttpContext.RequestAborted);
            await TryAuditAsync(
                "TEST",
                "SmsSettings",
                tenantId.Value.ToString(),
                oldValues: null,
                newValues: new { Recipient = MaskSmsRecipient(request.PhoneNumber), MessageType = "Standard" });

            return Ok(new TestSmsResultDto { Success = true, Message = "Test SMS sent successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test SMS failed for tenant {TenantId}", tenantId.Value);
            return BadRequest(SmsProblem("Test SMS could not be sent", ex.Message, "SMS_TEST_FAILED"));
        }
    }

    private static SmsSettingsDto ToSmsSettingsDto(Core.Entities.SmsSettings settings) => new()
    {
        IsConfigured = true,
        DefaultProvider = settings.DefaultProvider ?? "GhanaGateway",
        FallbackProvidersCsv = SmsSettingsDto.FallbackJsonToCsv(settings.FallbackProvidersJson),
        TwilioEnabled = settings.TwilioEnabled,
        TwilioAccountSid = settings.TwilioAccountSid ?? string.Empty,
        TwilioAuthToken = string.Empty,
        TwilioAuthTokenConfigured = !string.IsNullOrWhiteSpace(settings.TwilioAuthToken),
        TwilioFromNumber = settings.TwilioFromNumber ?? string.Empty,
        GhanaGatewayEnabled = settings.GhanaGatewayEnabled,
        GhanaGatewayUrlTemplate = string.IsNullOrWhiteSpace(settings.GhanaGatewayUrlTemplate)
            ? ErpSystem.Api.Services.Sms.MNotifySmsGateway.DefaultEndpoint
            : settings.GhanaGatewayUrlTemplate,
        GhanaGatewayApiKey = string.Empty,
        GhanaGatewayApiKeyConfigured = !string.IsNullOrWhiteSpace(settings.GhanaGatewayApiKey),
        GhanaGatewaySenderId = settings.GhanaGatewaySenderId ?? string.Empty,
        GhanaGatewayTimeoutSeconds = settings.GhanaGatewayTimeoutSeconds <= 0 ? 10 : settings.GhanaGatewayTimeoutSeconds
    };

    private static object ToSmsAuditValues(SmsSettingsDto request) => new
    {
        request.DefaultProvider,
        request.FallbackProvidersCsv,
        request.TwilioEnabled,
        request.TwilioAccountSid,
        TwilioAuthTokenSupplied = !string.IsNullOrWhiteSpace(request.TwilioAuthToken),
        request.TwilioFromNumber,
        MNotifyEnabled = request.GhanaGatewayEnabled,
        MNotifyEndpoint = request.GhanaGatewayUrlTemplate,
        MNotifyApiKeySupplied = !string.IsNullOrWhiteSpace(request.GhanaGatewayApiKey),
        MNotifySenderId = request.GhanaGatewaySenderId,
        MNotifyTimeoutSeconds = request.GhanaGatewayTimeoutSeconds
    };

    private static object ToSmsAuditValues(Core.Entities.SmsSettings settings) => new
    {
        settings.DefaultProvider,
        FallbackProvidersCsv = SmsSettingsDto.FallbackJsonToCsv(settings.FallbackProvidersJson),
        settings.TwilioEnabled,
        settings.TwilioAccountSid,
        TwilioAuthTokenConfigured = !string.IsNullOrWhiteSpace(settings.TwilioAuthToken),
        settings.TwilioFromNumber,
        MNotifyEnabled = settings.GhanaGatewayEnabled,
        MNotifyEndpoint = settings.GhanaGatewayUrlTemplate,
        MNotifyApiKeyConfigured = !string.IsNullOrWhiteSpace(settings.GhanaGatewayApiKey),
        MNotifySenderId = settings.GhanaGatewaySenderId,
        MNotifyTimeoutSeconds = settings.GhanaGatewayTimeoutSeconds
    };

    private static string? ValidateSmsSettings(SmsSettingsDto request, Core.Entities.SmsSettings? existing)
    {
        var provider = request.DefaultProvider?.Trim();
        if (provider is not null &&
            !provider.Equals("Twilio", StringComparison.OrdinalIgnoreCase) &&
            !provider.Equals("mNotify", StringComparison.OrdinalIgnoreCase) &&
            !provider.Equals("GhanaGateway", StringComparison.OrdinalIgnoreCase) &&
            !provider.Equals("Ghana", StringComparison.OrdinalIgnoreCase))
        {
            return "Default provider must be Twilio or mNotify.";
        }

        if (request.GhanaGatewayTimeoutSeconds is < 1 or > 60)
            return "mNotify timeout must be between 1 and 60 seconds.";

        if (request.GhanaGatewayEnabled)
        {
            var endpoint = string.IsNullOrWhiteSpace(request.GhanaGatewayUrlTemplate)
                ? ErpSystem.Api.Services.Sms.MNotifySmsGateway.DefaultEndpoint
                : request.GhanaGatewayUrlTemplate.Trim();
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
                !string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(endpointUri.Host, "api.mnotify.com", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(endpointUri.AbsolutePath.TrimEnd('/'), "/api/sms/quick", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(endpointUri.Query))
            {
                return $"mNotify endpoint must be {ErpSystem.Api.Services.Sms.MNotifySmsGateway.DefaultEndpoint} without query parameters.";
            }
            if (string.IsNullOrWhiteSpace(request.GhanaGatewaySenderId))
                return "mNotify sender ID is required when mNotify is enabled.";
            if (string.IsNullOrWhiteSpace(request.GhanaGatewayApiKey) && string.IsNullOrWhiteSpace(existing?.GhanaGatewayApiKey))
                return "mNotify API key is required when mNotify is enabled.";
        }

        return null;
    }

    private static ProblemDetails SmsProblem(string title, string detail, string code)
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status400BadRequest
        };
        problem.Extensions["code"] = code;
        return problem;
    }

    private static string MaskSmsRecipient(string phoneNumber)
    {
        var digits = new string((phoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length <= 4 ? "***" : $"***{digits[^4..]}";
    }

    #region Field Labels

    /// <summary>
    /// Get all field labels for a specific module (e.g., "InventoryItem")
    /// </summary>
    [HttpGet("field-labels/{module}")]
    public async Task<ActionResult<FieldLabelsDto>> GetFieldLabels(string module)
    {
        try
        {
            var labels = await _settingsService.GetFieldLabelsAsync(module);
            return Ok(new FieldLabelsDto
            {
                Module = module,
                Labels = labels
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving field labels for module {Module}", module);
            return StatusCode(500, "An error occurred while retrieving field labels");
        }
    }

    /// <summary>
    /// Update field labels for a specific module
    /// </summary>
    [HttpPut("field-labels/{module}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<FieldLabelsDto>> UpdateFieldLabels(string module, [FromBody] UpdateFieldLabelsRequest request)
    {
        try
        {
            // Get existing labels for audit logging
            var existingLabels = await _settingsService.GetFieldLabelsAsync(module);

            var updatedLabels = await _settingsService.SetFieldLabelsAsync(module, request.Labels);

            // Log the audit event
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var usernameClaim = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
            var tenantId = _currentUserService.TenantId;

            if (Guid.TryParse(userIdClaim, out var userId) && !string.IsNullOrEmpty(usernameClaim) && tenantId.HasValue)
            {
                var auditLog = new Core.Entities.AuditLog
                {
                    UserId = userId,
                    Username = usernameClaim,
                    Action = "UPDATE",
                    Resource = $"FieldLabels:{module}",
                    ResourceId = module,
                    OldValues = System.Text.Json.JsonSerializer.Serialize(existingLabels),
                    NewValues = System.Text.Json.JsonSerializer.Serialize(request.Labels),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                    TenantId = tenantId.Value
                };

                await _auditLogService.CreateAuditLogAsync(auditLog);
            }

            _logger.LogInformation("Updated field labels for module {Module}", module);

            return Ok(new FieldLabelsDto
            {
                Module = module,
                Labels = updatedLabels
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating field labels for module {Module}", module);
            return StatusCode(500, "An error occurred while updating field labels");
        }
    }

    /// <summary>
    /// Get a single field label
    /// </summary>
    [HttpGet("field-labels/{module}/{fieldName}")]
    public async Task<ActionResult<FieldLabelDto>> GetFieldLabel(string module, string fieldName)
    {
        try
        {
            var label = await _settingsService.GetFieldLabelAsync(module, fieldName);
            return Ok(new FieldLabelDto
            {
                Module = module,
                FieldName = fieldName,
                Label = label
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving field label {Module}:{FieldName}", module, fieldName);
            return StatusCode(500, "An error occurred while retrieving field label");
        }
    }

    /// <summary>
    /// Update a single field label
    /// </summary>
    [HttpPut("field-labels/{module}/{fieldName}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<FieldLabelDto>> UpdateFieldLabel(string module, string fieldName, [FromBody] UpdateFieldLabelRequest request)
    {
        try
        {
            await _settingsService.SetFieldLabelAsync(module, fieldName, request.Label);

            _logger.LogInformation("Updated field label {Module}:{FieldName} = {Label}", module, fieldName, request.Label);

            return Ok(new FieldLabelDto
            {
                Module = module,
                FieldName = fieldName,
                Label = request.Label
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating field label {Module}:{FieldName}", module, fieldName);
            return StatusCode(500, "An error occurred while updating field label");
        }
    }

    #endregion

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

public class SmsSettingsDto
{
    public bool IsConfigured { get; set; }
    public string DefaultProvider { get; set; } = "GhanaGateway";

    /// <summary>
    /// Comma-separated list of fallback providers (e.g. "GhanaGateway").
    /// </summary>
    public string FallbackProvidersCsv { get; set; } = string.Empty;

    public bool TwilioEnabled { get; set; } = false;
    public string TwilioAccountSid { get; set; } = string.Empty;
    public string TwilioAuthToken { get; set; } = string.Empty;
    public bool TwilioAuthTokenConfigured { get; set; }
    public string TwilioFromNumber { get; set; } = string.Empty;

    public bool GhanaGatewayEnabled { get; set; } = false;
    public string GhanaGatewayUrlTemplate { get; set; } = string.Empty;
    public string GhanaGatewayApiKey { get; set; } = string.Empty;
    public bool GhanaGatewayApiKeyConfigured { get; set; }
    public string GhanaGatewaySenderId { get; set; } = string.Empty;
    public int GhanaGatewayTimeoutSeconds { get; set; } = 10;

    public static string? FallbackCsvToJson(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        var items = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return items.Length == 0 ? null : System.Text.Json.JsonSerializer.Serialize(items);
    }

    public static string FallbackJsonToCsv(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
            return string.Join(", ", items.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        }
        catch
        {
            return string.Empty;
        }
    }
}

public sealed class SendTestSmsRequestDto
{
    public string PhoneNumber { get; set; } = string.Empty;
}

public sealed class TestSmsResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class MNotifyBalanceDto
{
    public decimal Balance { get; set; }
    public decimal Bonus { get; set; }
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

// Field Label DTOs
public class FieldLabelsDto
{
    public string Module { get; set; } = string.Empty;
    public Dictionary<string, string> Labels { get; set; } = new();
}

public class FieldLabelDto
{
    public string Module { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class UpdateFieldLabelsRequest
{
    public Dictionary<string, string> Labels { get; set; } = new();
}

public class UpdateFieldLabelRequest
{
    public string Label { get; set; } = string.Empty;
}

