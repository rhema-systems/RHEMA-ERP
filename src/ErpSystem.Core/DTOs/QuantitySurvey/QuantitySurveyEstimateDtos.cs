using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed record QuantitySurveyEstimateAssumptionRequest(
    [property: Required, StringLength(50)] string Code,
    [property: Required, StringLength(250)] string Description,
    [property: Required, StringLength(500)] string Value,
    [property: StringLength(50)] string? Unit);

public sealed record QuantitySurveyEstimateMarkupRequest(
    QuantitySurveyRateComponent Component,
    decimal Percentage);

public sealed class CreateQuantitySurveyEstimateRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid ProjectBoqVersionId { get; init; }
    public Guid? SourceEstimateVersionId { get; init; }
    public QuantitySurveyEstimateType EstimateType { get; init; }
    [Required, StringLength(160)] public string Name { get; init; } = string.Empty;
    public DateTime? EstimateDate { get; init; }
    [Required, StringLength(1000), MinLength(5)] public string ChangeReason { get; init; } = string.Empty;
    public Guid? CentralDocumentVersionId { get; init; }
    public IReadOnlyList<QuantitySurveyEstimateAssumptionRequest> Assumptions { get; init; } = [];
    public IReadOnlyList<QuantitySurveyEstimateMarkupRequest> Markups { get; init; } = [];
}

public sealed record QuantitySurveyEstimateWorkflowRequest(string? Comments);
public sealed record RejectQuantitySurveyEstimateRequest([property: Required, MinLength(5), StringLength(2000)] string Reason);

public sealed class QuantitySurveyEstimateLineDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public Guid ProjectBoqVersionLineId { get; init; }
    public Guid? SourceRateId { get; init; }
    public string LineNumber { get; init; } = string.Empty;
    public string? ItemCode { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? UnitOfMeasure { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitRate { get; init; }
    public decimal LineAmount { get; init; }
    public string? SourceRateItemCode { get; init; }
    public int? SourceRateVersion { get; init; }
    public string RateSource { get; init; } = string.Empty;
}

public sealed record QuantitySurveyEstimateAssumptionDto(Guid Id, int Sequence, string Code, string Description, string Value, string? Unit);
public sealed record QuantitySurveyEstimateMarkupDto(Guid Id, int Sequence, QuantitySurveyRateComponent Component, decimal Percentage, decimal BasisAmount, decimal Amount);
public sealed record QuantitySurveyEstimateHistoryDto(Guid Id, string Action, Guid ActorUserId, string ActorName, string? ActorRoles, string CorrelationId, string? Reason, DateTime CreatedAt);

public sealed class QuantitySurveyEstimateVersionDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProjectBoqVersionId { get; init; }
    public Guid? SourceEstimateVersionId { get; init; }
    public int VersionNumber { get; init; }
    public QuantitySurveyEstimateType EstimateType { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime EstimateDate { get; init; }
    public Guid CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal DirectCost { get; init; }
    public decimal MarkupTotal { get; init; }
    public decimal TotalAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid? WorkflowInstanceId { get; init; }
    public Guid? WorkflowDefinitionId { get; init; }
    public Guid? SubmittedById { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string ChangeReason { get; init; } = string.Empty;
    public string SnapshotHash { get; init; } = string.Empty;
    public Guid ConfigurationProfileId { get; init; }
    public int ConfigurationProfileVersion { get; init; }
    public Guid? CentralDocumentVersionId { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public byte[] RowVersion { get; init; } = [];
    public IReadOnlyList<QuantitySurveyEstimateLineDto> Lines { get; init; } = [];
    public IReadOnlyList<QuantitySurveyEstimateAssumptionDto> Assumptions { get; init; } = [];
    public IReadOnlyList<QuantitySurveyEstimateMarkupDto> Markups { get; init; } = [];
    public IReadOnlyList<QuantitySurveyEstimateHistoryDto> ApprovalHistory { get; init; } = [];
}

public sealed class QuantitySurveyEstimateWorkspaceDto
{
    public IReadOnlyList<QuantitySurveyEstimateVersionDto> Versions { get; init; } = [];
    public IReadOnlyList<Guid> ApprovedBoqVersionIds { get; init; } = [];
    public IReadOnlyList<QuantitySurveyEstimateType> AllowedTypes { get; init; } = Enum.GetValues<QuantitySurveyEstimateType>();
}
