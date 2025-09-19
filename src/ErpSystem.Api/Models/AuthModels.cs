using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Models
{
    public class LoginRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public required string Username { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public required string Password { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        public required string TenantCode { get; set; }

        public bool RememberMe { get; set; } = false;
    }

    public class RefreshTokenRequest
    {
        [Required]
        public required string Token { get; set; }

        [Required]
        public required string RefreshToken { get; set; }
    }

    public class LoginResponse
    {
        public required string Token { get; set; }
        public required string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public required UserInfo User { get; set; }
    }

    public class UserInfo
    {
        public required Guid Id { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public Guid? TenantId { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
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
        [StringLength(100, MinimumLength = 8)]
        public required string Password { get; set; }

        [Required]
        [Compare("Password")]
        public required string ConfirmPassword { get; set; }

        [StringLength(50)]
        public string? FirstName { get; set; }

        [StringLength(50)]
        public string? LastName { get; set; }
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
}