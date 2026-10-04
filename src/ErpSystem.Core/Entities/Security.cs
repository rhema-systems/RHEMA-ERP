using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities;

public class Security : BaseEntity
{
    // Password Policy Settings
    [Range(3, 12)]
    public int PasswordMinLength { get; set; } = 8;

    public bool PasswordRequireUppercase { get; set; } = true;

    public bool PasswordRequireLowercase { get; set; } = true;

    public bool PasswordRequireDigits { get; set; } = true;

    public bool PasswordRequireSpecialChars { get; set; } = true;

    [Range(0, 365)]
    public int? PasswordMaxAge { get; set; } = 90; // Days

    [Range(0, 50)]
    public int? PasswordPreventReuse { get; set; } = 5; // Number of previous passwords to prevent reuse

    // CAPTCHA Settings
    public bool CaptchaEnabled { get; set; } = false;

    [StringLength(20)]
    public string CaptchaProvider { get; set; } = "recaptcha"; // "recaptcha" or "hcaptcha"

    [StringLength(255)]
    public string? RecaptchaSiteKey { get; set; }

    [StringLength(255)]
    public string? RecaptchaSecretKey { get; set; }

    [StringLength(255)]
    public string? HCaptchaSiteKey { get; set; }

    [StringLength(255)]
    public string? HCaptchaSecretKey { get; set; }

    // Rate Limiting Settings - Login
    [Range(1, 20)]
    public int RateLimitLoginMaxAttempts { get; set; } = 5;

    [Range(1, 60)]
    public int RateLimitLoginWindowMinutes { get; set; } = 15;

    [Range(1, 1440)]
    public int RateLimitLoginBlockDurationMinutes { get; set; } = 30;

    // Rate Limiting Settings - Register
    [Range(1, 20)]
    public int RateLimitRegisterMaxAttempts { get; set; } = 3;

    [Range(1, 60)]
    public int RateLimitRegisterWindowMinutes { get; set; } = 60;

    [Range(1, 1440)]
    public int RateLimitRegisterBlockDurationMinutes { get; set; } = 60;

    // Rate Limiting Settings - Forgot Password
    [Range(1, 20)]
    public int RateLimitForgotPasswordMaxAttempts { get; set; } = 3;

    [Range(1, 60)]
    public int RateLimitForgotPasswordWindowMinutes { get; set; } = 60;

    [Range(1, 1440)]
    public int RateLimitForgotPasswordBlockDurationMinutes { get; set; } = 120;

    // Session and Lockout Settings
    [Range(5, 1440)]
    public int SessionTimeoutMinutes { get; set; } = 30;

    // JWT Token Settings
    [Range(5, 480)] // 5 minutes to 8 hours
    public int JwtTokenLifetimeMinutes { get; set; } = 60; // Default 1 hour

    [Range(1, 20)]
    public int MaxFailedLoginAttempts { get; set; } = 5;

    [Range(1, 1440)]
    public int AccountLockoutMinutes { get; set; } = 30;

    // Concurrent Login Prevention
    public PreventConcurrentLogin PreventConcurrentLogin { get; set; } = PreventConcurrentLogin.Disabled;

    // Legal URLs
    [StringLength(2048)]
    public string? TermsOfServiceUrl { get; set; }

    [StringLength(2048)]
    public string? PrivacyPolicyUrl { get; set; }

    // Public login appearance. The default remains usable when a tenant has not configured it yet.
    public LoginPageStyle LoginPageStyle { get; set; } = LoginPageStyle.LightCorporate;

    // Tenant association
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}
