using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities
{
    /// <summary>
    /// Represents a password reset token for user account recovery
    /// </summary>
    [Table("PasswordResetTokens")]
    public class PasswordResetToken : TenantEntity
    {

        /// <summary>
        /// The user ID for whom the reset token is issued
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// The unique reset token (should be cryptographically secure)
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Token expiry time in UTC
        /// </summary>
        public DateTime ExpiryTime { get; set; }

        /// <summary>
        /// Indicates whether the token has been used to reset password
        /// </summary>
        public bool IsUsed { get; set; } = false;

        /// <summary>
        /// Timestamp when the token was used to reset password (if applicable)
        /// </summary>
        public DateTime? UsedAt { get; set; }

        /// <summary>
        /// IP address from which the token was requested
        /// </summary>
        public string? RequestedFromIpAddress { get; set; }

        /// <summary>
        /// User agent of the request that created the token
        /// </summary>
        public string? RequestedFromUserAgent { get; set; }

        /// <summary>
        /// Navigation property for the associated user
        /// </summary>
        public virtual ApplicationUser? User { get; set; }
    }
}
