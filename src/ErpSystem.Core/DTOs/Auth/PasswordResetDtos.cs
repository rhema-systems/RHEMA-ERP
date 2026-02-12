using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Auth
{
    /// <summary>
    /// Request for forgot password functionality
    /// </summary>
    public class ForgotPasswordRequest
    {
        /// <summary>
        /// Email address of the user account
        /// </summary>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Valid email is required")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Optional tenant code for multi-tenant systems
        /// </summary>
        [StringLength(50)]
        public string? TenantCode { get; set; }

        /// <summary>
        /// CAPTCHA token (required when tenant security settings enable CAPTCHA)
        /// </summary>
        public string? CaptchaToken { get; set; }
    }

    /// <summary>
    /// Response for successful forgot password request
    /// </summary>
    public class ForgotPasswordResponse
    {
        /// <summary>
        /// Indicates success of the operation
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Message describing the operation result
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request for resetting password with token
    /// </summary>
    public class ResetPasswordRequest
    {
        /// <summary>
        /// The password reset token sent to user's email
        /// </summary>
        [Required(ErrorMessage = "Reset token is required")]
        public string ResetToken { get; set; } = string.Empty;

        /// <summary>
        /// Email address of the user account
        /// </summary>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Valid email is required")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The new password to set
        /// </summary>
        [Required(ErrorMessage = "New password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>
        /// Confirmation of the new password
        /// </summary>
        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>
        /// CAPTCHA token (required when tenant security settings enable CAPTCHA)
        /// </summary>
        public string? CaptchaToken { get; set; }
    }

    /// <summary>
    /// Response for successful password reset
    /// </summary>
    public class ResetPasswordResponse
    {
        /// <summary>
        /// Indicates success of the password reset
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Message describing the operation result
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to validate password reset token
    /// </summary>
    public class ValidateResetTokenRequest
    {
        /// <summary>
        /// The password reset token to validate
        /// </summary>
        [Required]
        public string ResetToken { get; set; } = string.Empty;

        /// <summary>
        /// Email address associated with the token
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// CAPTCHA token (required when tenant security settings enable CAPTCHA)
        /// </summary>
        public string? CaptchaToken { get; set; }
    }

    /// <summary>
    /// Response for token validation
    /// </summary>
    public class ValidateResetTokenResponse
    {
        /// <summary>
        /// Indicates whether the token is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Message about token validity status
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Token expiry time (if valid)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }
}
