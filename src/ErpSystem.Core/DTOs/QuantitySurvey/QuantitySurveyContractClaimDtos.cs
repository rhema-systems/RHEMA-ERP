using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed record QuantitySurveyClaimContractLookupDto(Guid Id, string Number, string Title, Guid ContractorId, string Contractor, decimal ContractSum, string Currency);
public sealed record QuantitySurveyClaimVariationLookupDto(Guid Id, string Reference, string Title, string Type, decimal Amount);
public sealed record QuantitySurveyClaimExtensionLookupDto(Guid Id, string Reference, string Title, int? DaysRequested, int? DaysApproved, string Status);
public sealed record QuantitySurveyClaimBoqLookupDto(Guid Id, string VersionNumber, string Status);

public sealed class QuantitySurveyContractClaimWorkspaceDto
{
    public IReadOnlyList<QuantitySurveyClaimContractLookupDto> Contracts { get; init; } = [];
    public IReadOnlyList<QuantitySurveyClaimVariationLookupDto> Variations { get; init; } = [];
    public IReadOnlyList<QuantitySurveyClaimExtensionLookupDto> ExtensionsOfTime { get; init; } = [];
    public IReadOnlyList<QuantitySurveyClaimBoqLookupDto> ApprovedBoqVersions { get; init; } = [];
    public IReadOnlyList<QuantitySurveyContractClaimDto> Claims { get; init; } = [];
}

public sealed class SaveQuantitySurveyContractClaimRequest
{
    public Guid? Id { get; init; }
    public Guid ClientRequestId { get; init; }
    public Guid ContractId { get; init; }
    public Guid? ApprovedBoqVersionId { get; init; }
    public Guid? VariationOrderId { get; init; }
    public Guid? ExtensionOfTimeId { get; init; }
    public QuantitySurveyContractClaimType ClaimType { get; init; }
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Basis { get; init; } = string.Empty;
    [Range(typeof(decimal), "0.01", "999999999999")] public decimal ClaimedAmount { get; init; }
    public string? RowVersion { get; init; }
}

public class QuantitySurveyContractClaimActionRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
}

public sealed class VetQuantitySurveyContractClaimRequest : QuantitySurveyContractClaimActionRequest
{
    [Range(typeof(decimal), "0", "999999999999")] public decimal AssessedAmount { get; init; }
}

public sealed class SettleQuantitySurveyContractClaimRequest : QuantitySurveyContractClaimActionRequest
{
    [Range(typeof(decimal), "0.01", "999999999999")] public decimal Amount { get; init; }
    [Required, StringLength(100, MinimumLength = 2)] public string SettlementReference { get; init; } = string.Empty;
    public DateTime SettlementDate { get; init; }
}

public sealed class QuantitySurveyContractClaimEvidenceDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string ChecksumSha256 { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
}

public sealed class QuantitySurveyContractClaimDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ContractId { get; init; }
    public Guid ContractorBusinessPartnerId { get; init; }
    public Guid? ApprovedBoqVersionId { get; init; }
    public Guid? VariationOrderId { get; init; }
    public Guid? ExtensionOfTimeId { get; init; }
    public string ClaimNumber { get; init; } = string.Empty;
    public QuantitySurveyContractClaimType ClaimType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Basis { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractorName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal ClaimedAmount { get; init; }
    public decimal? QsAssessedAmount { get; init; }
    public decimal? ApprovedAmount { get; init; }
    public decimal? RejectedAmount { get; init; }
    public string? QsReviewNote { get; init; }
    public QuantitySurveyClaimDisputeStatus DisputeStatus { get; init; }
    public string? DisputeReason { get; init; }
    public string? DisputeResolution { get; init; }
    public QuantitySurveyClaimSettlementStatus SettlementStatus { get; init; }
    public decimal SettledAmount { get; init; }
    public string? SettlementReference { get; init; }
    public DateTime? SettlementDate { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public string? RejectionReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyContractClaimEvidenceDto> Evidence { get; init; } = [];
}

public sealed class QuantitySurveyContractClaimRevisionDto
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
