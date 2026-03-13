namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcRetentionCategoryExceptionDto
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public bool IsActive { get; set; }
    public int AuditEventRetentionDays { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpsertEhcRetentionCategoryExceptionRequestDto
{
    public Guid CategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public int AuditEventRetentionDays { get; set; } = 365;
    public string? Notes { get; set; }
}

public sealed class EhcLegalHoldDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
}

public sealed class CreateEhcLegalHoldRequestDto
{
    public Guid TicketId { get; set; }
    public string? Reason { get; set; }
    public string? ReferenceNumber { get; set; }
}

public sealed class ReleaseEhcLegalHoldRequestDto
{
    public string? Notes { get; set; }
}

public sealed class EhcComplianceAuditExportDto
{
    public Guid Id { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? CategoryId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CreateEhcComplianceAuditExportRequestDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? CategoryId { get; set; }
}

public sealed class EhcComplianceSummaryDto
{
    public int ActiveLegalHolds { get; set; }
    public int ActiveRetentionCategoryExceptions { get; set; }
    public int AuditExports { get; set; }
}

public sealed class DataRetentionJobRunDto
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool Success { get; set; }
    public string? CountsJson { get; set; }
    public string? Error { get; set; }
}

public sealed class EhcComplianceReportDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public EhcComplianceSummaryDto Summary { get; set; } = new();
    public List<EhcRetentionCategoryExceptionDto> RetentionCategoryExceptions { get; set; } = new();
    public List<EhcLegalHoldDto> LegalHolds { get; set; } = new();
    public List<EhcComplianceAuditExportDto> AuditExports { get; set; } = new();
    public List<DataRetentionJobRunDto> RetentionRuns { get; set; } = new();
}
