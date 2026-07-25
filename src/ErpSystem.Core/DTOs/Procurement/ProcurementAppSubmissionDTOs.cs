using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementAppSubmissionSearchRequest
{
    public Guid? ProcurementPlanId { get; set; }
    public int? FiscalYear { get; set; }
    public ProcurementAppSubmissionStatus? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class ProcurementAppSubmissionPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementAppSubmissionDto> Items { get; set; } = new();
}

public sealed class ProcurementAppSubmissionSummaryDto
{
    public int PublishedPlanCount { get; set; }
    public int RegisteredPlanCount { get; set; }
    public int TotalAttemptCount { get; set; }
    public int ExportedCount { get; set; }
    public int SubmittedCount { get; set; }
    public int AcknowledgedCount { get; set; }
    public int RejectedCount { get; set; }
    public int ResubmissionCount { get; set; }
    public DateTime? LatestActivityAtUtc { get; set; }
}

public sealed class ProcurementAppSubmissionPlanOptionDto
{
    public Guid Id { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int RevisionNumber { get; set; }
    public DateTime PublishedDate { get; set; }
    public int ItemCount { get; set; }
    public bool HasSubmissionRegister { get; set; }
}

public sealed class ProcurementAppSubmissionDto
{
    public Guid Id { get; set; }
    public Guid ProcurementPlanId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string PlanTitle { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int PlanRevisionNumber { get; set; }
    public DateTime? PlanPublishedDate { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public ProcurementAppSubmissionStatus Status { get; set; }
    public string TimelineCorrelationId { get; set; } = string.Empty;
    public Guid? SupersedesSubmissionId { get; set; }
    public string ExportFileName { get; set; } = string.Empty;
    public string ExportFormat { get; set; } = string.Empty;
    public string ExportTemplateVersion { get; set; } = string.Empty;
    public string ExportChecksumSha256 { get; set; } = string.Empty;
    public DateTime ExportedAtUtc { get; set; }
    public Guid ExportedById { get; set; }
    public string ExportedByName { get; set; } = string.Empty;
    public string? ExternalSubmissionReference { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public string? AcknowledgementReference { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public Guid? AcknowledgedById { get; set; }
    public string? AcknowledgedByName { get; set; }
    public string? RejectionReference { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementAppSubmissionTimelineEventDto> Timeline { get; set; } = new();
}

public sealed class ProcurementAppSubmissionTimelineEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public ProcurementControlEventResult Result { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementAppSubmissionEvidenceDto> Evidence { get; set; } = new();
}

public sealed class ProcurementAppSubmissionEvidenceDto
{
    public ProcurementControlEvidenceReferenceKind ReferenceKind { get; set; }
    public Guid? ReferenceId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? RequirementKey { get; set; }
    public string? FileName { get; set; }
    public string? Sha256 { get; set; }
    public string? VerificationStatus { get; set; }
    public bool ReferenceAvailable { get; set; }
}

public sealed class RecordProcurementAppExportRequest
{
    public Guid ProcurementPlanId { get; set; }
    public string ExportFileName { get; set; } = string.Empty;
    public string ExportFormat { get; set; } = string.Empty;
    public string ExportTemplateVersion { get; set; } = string.Empty;
    public string ExportChecksumSha256 { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class SubmitProcurementAppRequest
{
    public string ExternalSubmissionReference { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public string? Notes { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class AcknowledgeProcurementAppRequest
{
    public string AcknowledgementReference { get; set; } = string.Empty;
    public DateTime AcknowledgedAtUtc { get; set; }
    public string? Notes { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class RejectProcurementAppRequest
{
    public string RejectionReference { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
    public DateTime RejectedAtUtc { get; set; }
    public string? Notes { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class ResubmitProcurementAppRequest
{
    public string ExportFileName { get; set; } = string.Empty;
    public string ExportFormat { get; set; } = string.Empty;
    public string ExportTemplateVersion { get; set; } = string.Empty;
    public string ExportChecksumSha256 { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}
