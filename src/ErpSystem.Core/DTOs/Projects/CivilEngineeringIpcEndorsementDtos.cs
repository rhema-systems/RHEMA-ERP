using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringIpcEndorsementCertificateLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CivilEngineeringIpcEndorsementDocumentLookupDto
{
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string DocumentReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VersionNumber { get; init; } = string.Empty;
}

public sealed class CivilEngineeringIpcEndorsementLookupsDto
{
    public IReadOnlyList<CivilEngineeringIpcEndorsementCertificateLookupDto> PaymentCertificates { get; init; } = [];
    public IReadOnlyList<CivilEngineeringIpcEndorsementDocumentLookupDto> Documents { get; init; } = [];
    public bool RequiresEndorsementEvidence { get; init; }
}

public sealed class CivilEngineeringIpcEndorsementDto
{
    public Guid Id { get; init; }
    public Guid ProjectPaymentCertificateId { get; init; }
    public string PaymentCertificateNumber { get; init; } = string.Empty;
    public string PaymentCertificateTitle { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public Guid ProjectEngineerAssignmentId { get; init; }
    public Guid ProjectEngineerUserId { get; init; }
    public string ProjectEngineerName { get; init; } = string.Empty;
    public Guid SubmittedById { get; init; }
    public string SubmittedByName { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public string SubmissionNotes { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? ReviewedById { get; init; }
    public string? ReviewedByName { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewNotes { get; init; }
    public Guid? EvidenceDocumentRecordId { get; init; }
    public Guid? EvidenceDocumentVersionId { get; init; }
    public bool RequiresEndorsementEvidence { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class SubmitCivilEngineeringIpcEndorsementRequest
{
    public Guid ClientRequestId { get; init; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Notes { get; init; } = string.Empty;
}

public sealed class ReviewCivilEngineeringIpcEndorsementRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    public bool Endorse { get; init; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Notes { get; init; } = string.Empty;
    public Guid? EvidenceDocumentRecordId { get; init; }
    public Guid? EvidenceDocumentVersionId { get; init; }
}
