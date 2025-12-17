using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Models
{
    // Step 1: Get available tenants for user
    public class GetUserTenantsRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public required string Username { get; set; }
    }

    public class UserTenantInfo
    {
        public required Guid TenantId { get; set; }
        public required string TenantCode { get; set; }
        public required string TenantName { get; set; }
        public bool IsDefault { get; set; }
        public string AccessLevel { get; set; } = "Standard";
    }

    public class GetUserTenantsResponse
    {
        public List<UserTenantInfo> Tenants { get; set; } = new();
        public UserTenantInfo? DefaultTenant { get; set; }
    }

    // Step 2: Login with selected tenant
    public class LoginRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public required string Username { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public required string Password { get; set; }

        [StringLength(50)]
        public string? TenantCode { get; set; }

        public bool RememberMe { get; set; } = false;

        /// <summary>
        /// Two-factor authentication code (6 digits)
        /// </summary>
        [StringLength(6)]
        public string? TwoFactorCode { get; set; }
    }

    public class RefreshTokenRequest
    {
        [Required]
        public required string Token { get; set; }

        [Required]
        public required string RefreshToken { get; set; }
    }

    public class LogoutRequest
    {
        /// <summary>
        /// Optional refresh token to revoke. If not provided, all refresh tokens for the user will be revoked.
        /// </summary>
        public string? RefreshToken { get; set; }
    }

    public class LoginResponse
    {
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public UserInfo? User { get; set; }

        /// <summary>
        /// Indicates if two-factor authentication is required to complete login
        /// </summary>
        public bool RequiresTwoFactor { get; set; }

        /// <summary>
        /// Temporary token used for 2FA verification (when RequiresTwoFactor is true)
        /// </summary>
        public string? TwoFactorToken { get; set; }
    }

    public class UserInfo
    {
        public required Guid Id { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }

        // Current session tenant info
        public Guid? CurrentTenantId { get; set; }
        public string? CurrentTenantCode { get; set; }
        public string? CurrentTenantName { get; set; }

        // All accessible tenants
        public List<UserTenantInfo> AccessibleTenants { get; set; } = new();

        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();

        // Authentication provider (Local or LDAP)
        public string AuthenticationProvider { get; set; } = "Local";
    }

    public class RegisterRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public required string Username { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public required string Email { get; set; }

        [Required]
        [Phone]
        [StringLength(20)]
        public required string PhoneNumber { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public required string Password { get; set; }

        [Required]
        [StringLength(50)]
        public required string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public required string LastName { get; set; }

        public string? RecaptchaToken { get; set; }
    }

    public class RegisterResponse
    {
        public bool Success { get; set; }
        public required string Message { get; set; }
        public string? PhoneNumber { get; set; }
        public bool RequiresOtpVerification { get; set; }
    }

    public class VerifyOtpRequest
    {
        [Required]
        [Phone]
        [StringLength(20)]
        public required string PhoneNumber { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public required string OtpCode { get; set; }
    }

    public class VerifyOtpResponse
    {
        public bool Success { get; set; }
        public required string Message { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required]
        public required string CurrentPassword { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public required string NewPassword { get; set; }

        [Required]
        [Compare("NewPassword")]
        public required string ConfirmPassword { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        public required string Token { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public required string Password { get; set; }

        [Required]
        [Compare("Password")]
        public required string ConfirmPassword { get; set; }
    }

    public class SelectTenantRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 1)]
        public required string TenantCode { get; set; }

        public bool SetAsDefault { get; set; } = false;
    }

    public class SelectTenantResponse
    {
        public required string Token { get; set; }
        public DateTime ExpiresAt { get; set; }
        public required UserInfo User { get; set; }
    }

    public class TenantUserMapping
    {
        public required string UserId { get; set; }
        public required string TenantId { get; set; }
        public bool IsActive { get; set; }
        public string? ExpiresAt { get; set; }
        public required string AccessLevel { get; set; }
        public bool IsDefault { get; set; }
        public required string GrantedAt { get; set; }
        public required TenantUserInfo User { get; set; }
    }

    public class TenantUserInfo
    {
        public required string Id { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; }
        public bool IsActive { get; set; }
    }
}
