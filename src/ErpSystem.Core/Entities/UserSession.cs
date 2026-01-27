using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities
{
    public class UserSession
    {
        [Key]
        public string SessionId { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid TenantId { get; set; }

        [Required]
        public DateTime LoginTime { get; set; } = DateTime.UtcNow;

        public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;

        public DateTime? LogoutTime { get; set; }

        [Required]
        [MaxLength(500)]
        public string IpAddress { get; set; }

        [MaxLength(1000)]
        public string UserAgent { get; set; }

        [MaxLength(100)]
        public string DeviceFingerprint { get; set; }

        [MaxLength(50)]
        public string DeviceType { get; set; } // Desktop, Mobile, Tablet

        [MaxLength(100)]
        public string Browser { get; set; }

        [MaxLength(100)]
        public string OperatingSystem { get; set; }

        [MaxLength(200)]
        public string Location { get; set; } // City, Country

        public bool IsActive { get; set; } = true;

        public bool WasTerminatedByConcurrentLogin { get; set; } = false;

        [MaxLength(500)]
        public string TerminationReason { get; set; }

        [MaxLength(100)]
        public string? JwtTokenId { get; set; } // Store JTI (JWT ID) to enable token blacklisting

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [ForeignKey("TenantId")]
        public virtual Tenant Tenant { get; set; }

        // Helper methods
        public void MarkAsLoggedOut(string? reason = null)
        {
            IsActive = false;
            LogoutTime = DateTime.UtcNow;
            TerminationReason = reason;
        }

        public void UpdateActivity()
        {
            LastActivityTime = DateTime.UtcNow;
        }

        public bool IsExpired(int sessionTimeoutMinutes)
        {
            return DateTime.UtcNow.Subtract(LastActivityTime).TotalMinutes > sessionTimeoutMinutes;
        }

        public TimeSpan GetSessionDuration()
        {
            var endTime = LogoutTime ?? DateTime.UtcNow;
            return endTime.Subtract(LoginTime);
        }
    }
}
