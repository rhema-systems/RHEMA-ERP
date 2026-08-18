using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed record QuantitySurveyVariationContractLookupDto(Guid Id, string Number, string Title, Guid ContractorId, string Contractor, decimal ContractSum, string Currency);
public sealed record QuantitySurveyVariationSourceLookupDto(Guid Id, string Reference, string Title, string Status, decimal? CostImpact, int? ScheduleImpactDays);
public sealed record QuantitySurveyVariationBoqLineLookupDto(Guid Id, Guid VersionId, Guid LineKey, string Reference, string Description, string? Unit, decimal Quantity, decimal UnitRate, string Currency);

public sealed class QuantitySurveyVariationWorkspaceDto
{
    public IReadOnlyList<QuantitySurveyVariationContractLookupDto> Contracts { get; init; } = [];
    public IReadOnlyList<QuantitySurveyVariationSourceLookupDto> SiteInstructions { get; init; } = [];
    public IReadOnlyList<QuantitySurveyVariationSourceLookupDto> ChangeRequests { get; init; } = [];
    public IReadOnlyList<QuantitySurveyVariationBoqLineLookupDto> BoqLines { get; init; } = [];
    public IReadOnlyList<QuantitySurveyVariationDto> Variations { get; init; } = [];
}

public sealed class SaveQuantitySurveyVariationRequest
{
    public Guid? Id { get; init; }
    public Guid ClientRequestId { get; init; }
    public Guid ContractId { get; init; }
    public Guid ApprovedBoqVersionId { get; init; }
    public QuantitySurveyVariationSourceType SourceType { get; init; }
    public Guid? SiteInstructionId { get; init; }
    public Guid? ChangeRequestId { get; init; }
    [Required, StringLength(200, MinimumLength = 3)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Reason { get; init; } = string.Empty;
    [Required, StringLength(50)] public string VariationType { get; init; } = string.Empty;
    [Range(-3650, 3650)] public int ScheduleImpactDays { get; init; }
    public string? RowVersion { get; init; }
    [MinLength(1)] public IReadOnlyList<SaveQuantitySurveyVariationLineRequest> Lines { get; init; } = [];
}

public sealed class SaveQuantitySurveyVariationLineRequest
{
    public Guid ProjectBoqVersionLineId { get; init; }
    [Range(typeof(decimal), "-999999999999", "999999999999")] public decimal QuantityChange { get; init; }
    [Range(typeof(decimal), "0", "999999999999")] public decimal? UnitRate { get; init; }
    [Required, StringLength(1000, MinimumLength = 5)] public string ValuationReason { get; init; } = string.Empty;
}

public class QuantitySurveyVariationActionRequest
{
    public Guid ClientRequestId { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 5)] public string Reason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyVariationLineDto
{
    public Guid Id { get; init; }
    public Guid ProjectBoqVersionLineId { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Unit { get; init; }
    public decimal QuantityChange { get; init; }
    public decimal UnitRate { get; init; }
    public decimal Amount { get; init; }
    public string ValuationReason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyVariationEvidenceDto
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

public sealed class QuantitySurveyVariationDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ContractId { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string VariationType { get; init; } = string.Empty;
    public QuantitySurveyVariationSourceType SourceType { get; init; }
    public Guid? SiteInstructionId { get; init; }
    public Guid? ChangeRequestId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractorName { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal ValuedAmount { get; init; }
    public decimal? ApprovedAmount { get; init; }
    public decimal OriginalContractSum { get; init; }
    public decimal? RevisedContractSum { get; init; }
    public string DownstreamApplicationStatus { get; init; } = string.Empty;
    public Guid? ContractAmendmentId { get; init; }
    public Guid? RevisedBoqVersionId { get; init; }
    public int? RevisedBoqVersionNumber { get; init; }
    public string? RevisedBoqStatus { get; init; }
    public Guid? BudgetRevisionId { get; init; }
    public Guid? ForecastVersionId { get; init; }
    public DateTime? AppliedAt { get; init; }
    public bool CertificateEligible { get; init; }
    public int ScheduleImpactDays { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public string? RejectionReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyVariationLineDto> Lines { get; init; } = [];
    public IReadOnlyList<QuantitySurveyVariationEvidenceDto> Evidence { get; init; } = [];
}

public sealed class QuantitySurveyVariationRevisionDto
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
