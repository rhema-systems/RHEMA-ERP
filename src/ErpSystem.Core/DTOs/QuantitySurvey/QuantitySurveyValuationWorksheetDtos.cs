using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyValuationBoqLookupDto
{
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public string Label { get; init; } = string.Empty;
    public int LineCount { get; init; }
}

public sealed class QuantitySurveyValuationLookupsDto
{
    public IReadOnlyList<QuantitySurveyValuationBoqLookupDto> ApprovedBoqVersions { get; init; } = [];
    public IReadOnlyList<QuantitySurveyValuationPartnerLookupDto> Contractors { get; init; } = [];
    public IReadOnlyList<QuantitySurveyValuationPartnerLookupDto> Consultants { get; init; } = [];
}

public sealed class QuantitySurveyValuationPartnerLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class QuantitySurveyValuationWorksheetLineInputDto
{
    public Guid ProjectBoqVersionLineId { get; init; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal CurrentClaimedQuantity { get; init; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal CurrentCertifiedQuantity { get; init; }
    [StringLength(1000)] public string? ReviewNote { get; init; }
}

public sealed class SaveQuantitySurveyValuationWorksheetRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid ProjectBoqVersionId { get; init; }
    public Guid? ContractorBusinessPartnerId { get; init; }
    public Guid? ConsultantBusinessPartnerId { get; init; }
    public string? RowVersion { get; init; }
    [Range(typeof(decimal), "0", "100")] public decimal RetentionPercentage { get; init; }
    [MinLength(1), MaxLength(2000)] public List<QuantitySurveyValuationWorksheetLineInputDto> Lines { get; init; } = [];
}

public sealed class QuantitySurveyValuationLifecycleRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyValuationEndorsementRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [StringLength(2000)] public string? Notes { get; init; }
    [Required] public WorkflowSignatureSubmissionDto Signature { get; init; } = new();
}

public sealed class AddQuantitySurveyValuationEvidenceRequest
{
    public Guid ClientRequestId { get; init; }
    public QuantitySurveyValuationEvidenceType EvidenceType { get; init; }
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; init; } = string.Empty;
}

public sealed class QuantitySurveyValuationEvidenceDto
{
    public Guid Id { get; init; }
    public QuantitySurveyValuationEvidenceType EvidenceType { get; init; }
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

public sealed class QuantitySurveyValuationWorksheetLineDto
{
    public Guid Id { get; init; }
    public Guid ProjectBoqVersionLineId { get; init; }
    public Guid BoqLineKey { get; init; }
    public int Sequence { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? UnitOfMeasure { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal BoqQuantity { get; init; }
    public decimal UnitRate { get; init; }
    public decimal MeasuredToDateQuantity { get; init; }
    public decimal PreviouslyCertifiedQuantity { get; init; }
    public decimal CurrentClaimedQuantity { get; init; }
    public decimal CurrentCertifiedQuantity { get; init; }
    public decimal DisputedQuantity { get; init; }
    public decimal MeasuredToDateValue { get; init; }
    public decimal PreviouslyCertifiedValue { get; init; }
    public decimal CurrentClaimedValue { get; init; }
    public decimal CurrentCertifiedValue { get; init; }
    public decimal CurrentPeriodCertifiedValue { get; init; }
    public decimal DisputedValue { get; init; }
    public decimal PreviousRetentionValue { get; init; }
    public decimal RetentionToDateValue { get; init; }
    public decimal CurrentRetentionValue { get; init; }
    public decimal NetCurrentValue { get; init; }
    public string? ReviewNote { get; init; }
}

public sealed class QuantitySurveyValuationWorksheetDto
{
    public Guid? Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectInterimValuationId { get; init; }
    public string InterimValuationLabel { get; init; } = string.Empty;
    public Guid ProjectBoqVersionId { get; init; }
    public int BoqVersionNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid? ContractorBusinessPartnerId { get; init; }
    public string? ContractorName { get; init; }
    public Guid? ConsultantBusinessPartnerId { get; init; }
    public string? ConsultantName { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public bool ContractorSubmissionRequired { get; init; }
    public bool ConsultantEndorsementRequired { get; init; }
    public bool SupportingEvidenceRequired { get; init; }
    public bool PortalIdentityRequired { get; init; }
    public bool ExternalSignatureRequired { get; init; }
    public string ContractorAttestationText { get; init; } = string.Empty;
    public string ConsultantAttestationText { get; init; } = string.Empty;
    public DateTime? ContractorSubmittedAt { get; init; }
    public DateTime? QsVettedAt { get; init; }
    public string? QsReviewNote { get; init; }
    public DateTime? ConsultantEndorsedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? RejectionReason { get; init; }
    public bool CertificateReady { get; init; }
    public DateTime? CertificateReadyAt { get; init; }
    public decimal RetentionPercentage { get; init; }
    public decimal MeasuredToDateValue { get; init; }
    public decimal PreviouslyCertifiedValue { get; init; }
    public decimal CurrentClaimedValue { get; init; }
    public decimal CurrentCertifiedValue { get; init; }
    public decimal CurrentPeriodCertifiedValue { get; init; }
    public decimal DisputedValue { get; init; }
    public decimal RetentionToDateValue { get; init; }
    public decimal CurrentRetentionValue { get; init; }
    public decimal NetCurrentValue { get; init; }
    public string? RowVersion { get; init; }
    public IReadOnlyList<QuantitySurveyValuationWorksheetLineDto> Lines { get; init; } = [];
    public IReadOnlyList<QuantitySurveyValuationEvidenceDto> Evidence { get; init; } = [];
}

public sealed class QuantitySurveyValuationWorksheetRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? BeforeJson { get; init; }
    public string AfterJson { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
