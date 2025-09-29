using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities;

public class BlacklistedToken : BaseEntity
{
    /// <summary>
    /// JWT ID (jti claim) - unique identifier for the JWT token
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Jti { get; set; } = string.Empty;

    /// <summary>
    /// User ID from the token
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Original expiration date of the JWT token
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When the token was blacklisted
    /// </summary>
    [Required]
    public DateTime BlacklistedAt { get; set; }

    /// <summary>
    /// Reason for blacklisting the token
    /// </summary>
    [StringLength(200)]
    public string? Reason { get; set; }

    /// <summary>
    /// Navigation property to user
    /// </summary>
    public virtual ApplicationUser User { get; set; } = null!;
}