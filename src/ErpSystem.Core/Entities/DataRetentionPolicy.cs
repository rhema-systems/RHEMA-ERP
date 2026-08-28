using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities;

[Table("DataRetentionPolicies")]
public class DataRetentionPolicy : BaseEntity
{
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    public bool Enabled { get; set; } = true;

    [Range(2555, 36500)]
    public int AuditLogRetentionDays { get; set; } = 2555;

    [Range(1, 3650)]
    public int SecurityLogRetentionDays { get; set; } = 365;

    [Range(1, 3650)]
    public int NotificationRetentionDays { get; set; } = 180;

    [Range(1, 3650)]
    public int EhcAuditEventRetentionDays { get; set; } = 365;

    [Range(2555, 36500)]
    public int WorkflowAuditRetentionDays { get; set; } = 2555;
}

[Table("DataRetentionJobRuns")]
public class DataRetentionJobRun : BaseEntity
{
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string JobName { get; set; } = "data-retention";

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public bool Success { get; set; } = true;

    [Column(TypeName = "nvarchar(max)")]
    public string? CountsJson { get; set; }

    [StringLength(1000)]
    public string? Error { get; set; }
}
