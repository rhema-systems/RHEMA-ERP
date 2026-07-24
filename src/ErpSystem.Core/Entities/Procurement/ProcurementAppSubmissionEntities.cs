using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementAppSubmissions")]
public sealed class ProcurementAppSubmission : TenantEntity
{
    public Guid ProcurementPlanId { get; set; }

    [Required, StringLength(50)]
    public string SubmissionNumber { get; set; } = string.Empty;

    public int AttemptNumber { get; set; } = 1;
    public ProcurementAppSubmissionStatus Status { get; set; } = ProcurementAppSubmissionStatus.Exported;

    [Required, StringLength(100)]
    public string TimelineCorrelationId { get; set; } = string.Empty;

    public Guid? SupersedesSubmissionId { get; set; }

    [Required, StringLength(260)]
    public string ExportFileName { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string ExportFormat { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ExportTemplateVersion { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string ExportChecksumSha256 { get; set; } = string.Empty;

    public DateTime ExportedAtUtc { get; set; }
    public Guid ExportedById { get; set; }

    [Required, StringLength(300)]
    public string ExportedByName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? ExternalSubmissionReference { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? SubmittedById { get; set; }

    [StringLength(300)]
    public string? SubmittedByName { get; set; }

    [StringLength(200)]
    public string? AcknowledgementReference { get; set; }

    public DateTime? AcknowledgedAtUtc { get; set; }
    public Guid? AcknowledgedById { get; set; }

    [StringLength(300)]
    public string? AcknowledgedByName { get; set; }

    [StringLength(200)]
    public string? RejectionReference { get; set; }

    [StringLength(1000)]
    public string? RejectionReason { get; set; }

    public DateTime? RejectedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }

    [StringLength(300)]
    public string? RejectedByName { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementPlan ProcurementPlan { get; set; } = null!;
    public ProcurementAppSubmission? SupersedesSubmission { get; set; }
}
