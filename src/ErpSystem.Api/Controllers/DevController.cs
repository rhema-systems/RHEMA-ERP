using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly ILogger<DevController> _logger;
    private readonly IWebHostEnvironment _environment;

    public DevController(ILogger<DevController> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Development-only endpoint to get mock current user
    /// </summary>
    [HttpGet("me")]
    [AllowAnonymous]
    public IActionResult GetCurrentUser()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var mockUser = new
        {
            Id = Guid.NewGuid(),
            Username = "dev@example.com",
            Email = "dev@example.com",
            FirstName = "Dev",
            LastName = "User",
            PhoneNumber = "+1234567890",
            CurrentTenantId = Guid.NewGuid(),
            CurrentTenantCode = "DEV001",
            CurrentTenantName = "Development Tenant",
            AccessibleTenants = new[]
            {
                new
                {
                    TenantId = Guid.NewGuid(),
                    TenantCode = "DEV001",
                    TenantName = "Development Tenant",
                    IsDefault = true,
                    AccessLevel = "Admin"
                }
            },
            IsActive = true,
            Roles = new[] { "Admin", "User" }
        };

        return Ok(mockUser);
    }

    /// <summary>
    /// Development-only endpoint to get mock security settings
    /// </summary>
    [HttpGet("security-settings")]
    [AllowAnonymous]
    public IActionResult GetSecuritySettings()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var mockSettings = new
        {
            // Password Policy
            PasswordMinLength = 8,
            PasswordRequireUppercase = true,
            PasswordRequireLowercase = true,
            PasswordRequireDigits = true,
            PasswordRequireSpecialChars = false,
            PasswordMaxAge = 90,
            PasswordPreventReuse = 5,

            // Session & Token
            SessionTimeoutMinutes = 30,
            JwtTokenLifetimeMinutes = 60,
            PreventConcurrentLogin = "Disabled",

            // Lockout Settings
            MaxFailedLoginAttempts = 5,
            AccountLockoutMinutes = 30,
            RateLimitLoginMaxAttempts = 10,
            RateLimitLoginWindowMinutes = 15,
            RateLimitLoginBlockDurationMinutes = 30,

            // CAPTCHA Settings
            CaptchaEnabled = false,
            CaptchaProvider = "recaptcha",
            RecaptchaSiteKey = (string?)null,
            RecaptchaSecretKey = (string?)null,
            HCaptchaSiteKey = (string?)null,
            HCaptchaSecretKey = (string?)null,

            // Legal URLs
            TermsOfServiceUrl = (string?)null,
            PrivacyPolicyUrl = (string?)null
        };

        return Ok(mockSettings);
    }

    /// <summary>
    /// Development-only endpoint to get mock tenant info
    /// </summary>
    [HttpGet("tenants")]
    [AllowAnonymous]
    public IActionResult GetTenants()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var mockTenants = new[]
        {
            new
            {
                Id = Guid.NewGuid(),
                Code = "DEV001",
                Name = "Development Tenant",
                Status = "Active",
                IsDefault = true
            }
        };

        return Ok(mockTenants);
    }
}
