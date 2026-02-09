using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities;

/// <summary>
/// Database-backed lease lock used for leader election / distributed background jobs.
/// This is intentionally not tenant-scoped (no TenantId) so a single lock can guard global jobs.
/// </summary>
public class DistributedLock : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string LockName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AcquiredBy { get; set; }

    public DateTime? AcquiredAtUtc { get; set; }

    public DateTime LeaseUntilUtc { get; set; }

    public DateTime? LastHeartbeatUtc { get; set; }
}

