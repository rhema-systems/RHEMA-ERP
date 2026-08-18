using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed record QuantitySurveySubcontractContractLookupDto(
    Guid Id, string Number, string Title, decimal ContractValue, string Currency,
    decimal RetentionPercentage, Guid PaymentTermId);
public sealed record QuantitySurveySubcontractPartnerLookupDto(Guid Id, string Code, string Name);
public sealed record QuantitySurveySubcontractPaymentTermLookupDto(Guid Id, string Code, string Name);

public sealed class QuantitySurveySubcontractWorkspaceDto
{
    public IReadOnlyList<QuantitySurveySubcontractContractLookupDto> Contracts { get; init; } = [];
    public IReadOnlyList<QuantitySurveySubcontractPartnerLookupDto> Subcontractors { get; init; } = [];
    public IReadOnlyList<QuantitySurveySubcontractPaymentTermLookupDto> PaymentTerms { get; init; } = [];
    public IReadOnlyList<QuantitySurveySubcontractDto> Subcontracts { get; init; } = [];
    public Guid? ExternalBusinessPartnerId { get; init; }
}

public sealed class SaveQuantitySurveySubcontractRequest
{
    public Guid? Id { get; init; }
    public Guid ClientRequestId { get; init; }
    public Guid ContractId { get; init; }
    public Guid SubcontractorBusinessPartnerId { get; init; }
    public Guid PaymentTermId { get; init; }
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Scope { get; init; } = string.Empty;
    [Range(typeof(decimal), "0.01", "999999999999")] public decimal SubcontractValue { get; init; }
    [Range(typeof(decimal), "0", "100")] public decimal RetentionPercentage { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? RowVersion { get; init; }
}

public class QuantitySurveySubcontractActionRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
}

public sealed class SaveQuantitySurveySubcontractValuationRequest
{
    public Guid? Id { get; init; }
    public Guid ClientRequestId { get; init; }
    public DateTime ValuationDate { get; init; }
    public DateTime PeriodEndDate { get; init; }
    [Range(typeof(decimal), "0.01", "999999999999")] public decimal ClaimedToDateAmount { get; init; }
    public bool IsFinal { get; init; }
    [StringLength(2000)] public string? SubmissionNote { get; init; }
    public string? RowVersion { get; init; }
}

public sealed class AssessQuantitySurveySubcontractValuationRequest : QuantitySurveySubcontractActionRequest
{
    [Range(typeof(decimal), "0", "999999999999")] public decimal AssessedToDateAmount { get; init; }
    [Range(typeof(decimal), "0", "999999999999")] public decimal RetentionReleasedAmount { get; init; }
    public IReadOnlyList<Guid> ChargeNoticeIds { get; init; } = [];
}

public sealed class QuantitySurveySubcontractEvidenceDto
{
    public Guid Id { get; init; }
    public Guid? ValuationId { get; init; }
    public string EvidenceType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string ChecksumSha256 { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
}

public sealed class QuantitySurveySubcontractValuationDto
{
    public Guid Id { get; init; }
    public Guid SubcontractId { get; init; }
    public string ValuationNumber { get; init; } = string.Empty;
    public DateTime ValuationDate { get; init; }
    public DateTime PeriodEndDate { get; init; }
    public decimal ClaimedToDateAmount { get; init; }
    public decimal? AssessedToDateAmount { get; init; }
    public decimal PreviouslyCertifiedAmount { get; init; }
    public decimal CurrentCertifiedAmount { get; init; }
    public decimal RetentionHeldAmount { get; init; }
    public decimal RetentionReleasedAmount { get; init; }
    public decimal ApprovedBackChargeAmount { get; init; }
    public decimal ApprovedContraChargeAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal NetCertifiedAmount { get; init; }
    public bool IsFinal { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string? SubmissionNote { get; init; }
    public string? AssessmentNote { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public Guid? PaymentCertificateId { get; init; }
    public string? CertificateNumber { get; init; }
    public Guid? VendorInvoiceId { get; init; }
    public string PaymentStatus { get; init; } = string.Empty;
    public string ApHandoffStatus { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveySubcontractEvidenceDto> Evidence { get; init; } = [];
}

public sealed class QuantitySurveySubcontractDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ContractId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public Guid SubcontractorBusinessPartnerId { get; init; }
    public string SubcontractorName { get; init; } = string.Empty;
    public Guid PaymentTermId { get; init; }
    public string PaymentTermName { get; init; } = string.Empty;
    public string SubcontractNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public decimal SubcontractValue { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal RetentionPercentage { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid? WorkflowInstanceId { get; init; }
    public DateTime? ClosedAt { get; init; }
    public string? ClosureNote { get; init; }
    public decimal CertifiedToDateAmount { get; init; }
    public decimal PaidToDateAmount { get; init; }
    public decimal RetentionBalance { get; init; }
    public decimal OutstandingBalance { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveySubcontractEvidenceDto> Evidence { get; init; } = [];
    public IReadOnlyList<QuantitySurveySubcontractValuationDto> Valuations { get; init; } = [];
}

public sealed class QuantitySurveySubcontractRevisionDto
{
    public Guid Id { get; init; }
    public Guid? ValuationId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? BeforeJson { get; init; }
    public string AfterJson { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
