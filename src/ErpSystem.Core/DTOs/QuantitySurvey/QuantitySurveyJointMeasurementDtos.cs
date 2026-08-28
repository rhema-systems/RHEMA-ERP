using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyJointMeasurementLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Group { get; init; }
    public string? Description { get; init; }
}

public sealed class QuantitySurveyJointMeasurementLookupsDto
{
    public IReadOnlyList<QuantitySurveyJointMeasurementLookupDto> ApprovedBoqLines { get; init; } = [];
    public IReadOnlyList<QuantitySurveyJointMeasurementLookupDto> ContractorPartners { get; init; } = [];
    public IReadOnlyList<QuantitySurveyJointMeasurementLookupDto> ConsultantPartners { get; init; } = [];
    public IReadOnlyList<QuantitySurveyJointMeasurementLookupDto> RecordedMeasurements { get; init; } = [];
}

public sealed class CreateQuantitySurveyJointMeasurementRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectBoqVersionLineId { get; init; }
    public Guid? ContractorBusinessPartnerId { get; init; }
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 10)] public string Reason { get; init; } = string.Empty;
    [Range(typeof(decimal), "0.0001", "99999999999999.9999")] public decimal? ContractorProposedQuantity { get; init; }
    [StringLength(300)] public string? RequestedSiteLocation { get; init; }
    public DateTime? PreferredStartAt { get; init; }
    public DateTime? PreferredEndAt { get; init; }
}

public sealed class ScheduleQuantitySurveyJointMeasurementRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid ConsultantBusinessPartnerId { get; init; }
    public DateTime ScheduledStartAt { get; init; }
    public DateTime ScheduledEndAt { get; init; }
    [Required, StringLength(300, MinimumLength = 3)] public string SiteLocation { get; init; } = string.Empty;
    [Required] public string RowVersion { get; init; } = string.Empty;
}

public sealed class LinkQuantitySurveyJointMeasurementSheetRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid MeasurementSheetId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
}

public sealed class AttendQuantitySurveyJointMeasurementRequest
{
    public Guid ClientRequestId { get; init; }
    [StringLength(1000)] public string? Notes { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
}

public sealed class EndorseQuantitySurveyJointMeasurementRequest
{
    public Guid ClientRequestId { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
    [Required] public WorkflowSignatureSubmissionDto Signature { get; init; } = new();
    [Required] public string RowVersion { get; init; } = string.Empty;
}

public sealed class QuantitySurveyJointMeasurementLifecycleRequest
{
    public Guid ClientRequestId { get; init; }
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
    [Required] public string RowVersion { get; init; } = string.Empty;
}

public sealed class AddQuantitySurveyJointMeasurementEvidenceRequest
{
    public Guid ClientRequestId { get; init; }
    public QuantitySurveyJointMeasurementEvidenceType EvidenceType { get; init; }
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; init; } = string.Empty;
}

public sealed class QuantitySurveyJointMeasurementListRequest
{
    public Guid? ProjectId { get; init; }
    [StringLength(30)] public string? Status { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 50;
}

public sealed class QuantitySurveyJointMeasurementParticipantDto
{
    public Guid Id { get; init; }
    public string ParticipantType { get; init; } = string.Empty;
    public Guid? BusinessPartnerId { get; init; }
    public string? BusinessPartnerName { get; init; }
    public Guid? RequiredRoleId { get; init; }
    public string? RequiredRoleName { get; init; }
    public bool IsRequired { get; init; }
    public string AttendanceStatus { get; init; } = string.Empty;
    public string? AttendedByName { get; init; }
    public DateTime? AttendedAt { get; init; }
    public string? AttendanceNotes { get; init; }
}

public sealed class QuantitySurveyJointMeasurementEvidenceDto
{
    public Guid Id { get; init; }
    public QuantitySurveyJointMeasurementEvidenceType EvidenceType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string ChecksumSha256 { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string UploadedByName { get; init; } = string.Empty;
    public DateTime UploadedAt { get; init; }
}

public sealed class QuantitySurveyJointMeasurementEndorsementDto
{
    public Guid Id { get; init; }
    public string SignerType { get; init; } = string.Empty;
    public Guid? BusinessPartnerId { get; init; }
    public string? BusinessPartnerName { get; init; }
    public string SignedByName { get; init; } = string.Empty;
    public WorkflowSignatureMethod SignatureMethod { get; init; }
    public string Attestation { get; init; } = string.Empty;
    public string? CertificateThumbprint { get; init; }
    public string? ExternalSignatureReference { get; init; }
    public string? Notes { get; init; }
    public DateTime SignedAt { get; init; }
}

public sealed class QuantitySurveyJointMeasurementDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectTitle { get; init; } = string.Empty;
    public Guid ProjectBoqVersionId { get; init; }
    public int ProjectBoqVersionNumber { get; init; }
    public Guid ProjectBoqVersionLineId { get; init; }
    public Guid BoqLineKey { get; init; }
    public string BoqLineLabel { get; init; } = string.Empty;
    public string? UnitOfMeasure { get; init; }
    public Guid ContractorBusinessPartnerId { get; init; }
    public string ContractorName { get; init; } = string.Empty;
    public Guid? ConsultantBusinessPartnerId { get; init; }
    public string? ConsultantName { get; init; }
    public string RequestNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public decimal PreviousQuantity { get; init; }
    public decimal? ContractorProposedQuantity { get; init; }
    public decimal? RecordedQuantity { get; init; }
    public string? RequestedSiteLocation { get; init; }
    public DateTime? PreferredStartAt { get; init; }
    public DateTime? PreferredEndAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public DateTime? ScheduledStartAt { get; init; }
    public DateTime? ScheduledEndAt { get; init; }
    public string? ScheduledSiteLocation { get; init; }
    public Guid? MeasurementSheetId { get; init; }
    public string? MeasurementSheetReference { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public Guid? RemeasurementVersionId { get; init; }
    public int? RemeasurementVersionNumber { get; init; }
    public bool ContractorSignatureRequired { get; init; }
    public bool ConsultantSignatureRequired { get; init; }
    public bool EvidenceRequired { get; init; }
    public string EndorsementAttestation { get; init; } = string.Empty;
    public string RequestedByName { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? AppliedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyJointMeasurementParticipantDto> Participants { get; init; } = [];
    public IReadOnlyList<QuantitySurveyJointMeasurementEvidenceDto> Evidence { get; init; } = [];
    public IReadOnlyList<QuantitySurveyJointMeasurementEndorsementDto> Endorsements { get; init; } = [];
}

public sealed class QuantitySurveyJointMeasurementPageDto
{
    public IReadOnlyList<QuantitySurveyJointMeasurementDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class QuantitySurveyJointMeasurementRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? BeforeJson { get; init; }
    public string AfterJson { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
