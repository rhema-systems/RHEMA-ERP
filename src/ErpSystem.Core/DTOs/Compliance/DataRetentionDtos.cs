namespace ErpSystem.Core.DTOs.Compliance;

public sealed class DataRetentionPolicyDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public bool Enabled { get; set; }
    public int AuditLogRetentionDays { get; set; }
    public int SecurityLogRetentionDays { get; set; }
    public int NotificationRetentionDays { get; set; }
    public int EhcAuditEventRetentionDays { get; set; }
}

public sealed class UpdateDataRetentionPolicyRequestDto
{
    public bool Enabled { get; set; } = true;
    public int AuditLogRetentionDays { get; set; } = 365;
    public int SecurityLogRetentionDays { get; set; } = 365;
    public int NotificationRetentionDays { get; set; } = 180;
    public int EhcAuditEventRetentionDays { get; set; } = 365;
}

public sealed class DataRetentionJobRunDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string JobName { get; set; } = "data-retention";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool Success { get; set; }
    public string? CountsJson { get; set; }
    public string? Error { get; set; }
}

