using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyEscalationCalculationLookupDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Group { get; init; }
    public decimal? Amount { get; init; }
    public string? CurrencyCode { get; init; }
    public string? Status { get; init; }
}

public sealed class QuantitySurveyEscalationCalculationLookupsDto
{
    public IReadOnlyList<QuantitySurveyEscalationCalculationLookupDto> Formulas { get; init; } = [];
}

public sealed class QuantitySurveyEscalationImpactTargetsDto
{
    public IReadOnlyList<QuantitySurveyEscalationCalculationLookupDto> PaymentCertificates { get; init; } = [];
    public IReadOnlyList<QuantitySurveyEscalationCalculationLookupDto> FinalAccounts { get; init; } = [];
}

public sealed class CreateQuantitySurveyEscalationCalculationRequest
{
    public Guid ClientRequestId { get; init; }
    public Guid FormulaId { get; init; }
    public QuantitySurveyEscalationImpactTargetType ImpactTargetType { get; init; }
    public Guid ImpactTargetId { get; init; }
    public DateTime CurrentIndexPeriod { get; init; }
    [Required, StringLength(1000)] public string Reason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyEscalationCalculationListRequest
{
    public Guid? ProjectId { get; init; }
    public Guid? FormulaId { get; init; }
    public string? Status { get; init; }
    [Range(1, 1_000_000)] public int Page { get; init; } = 1;
    [Range(1, 200)] public int PageSize { get; init; } = 50;
}

public sealed class QuantitySurveyEscalationCalculationLifecycleRequest
{
    [Required] public string RowVersion { get; init; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; init; } = string.Empty;
}

public sealed class ReviewQuantitySurveyEscalationCalculationRequest
{
    [Required] public string RowVersion { get; init; } = string.Empty;
    public decimal AdjustmentAmount { get; init; }
    [Required, StringLength(1000), MinLength(10)] public string Reason { get; init; } = string.Empty;
}

public sealed class QuantitySurveyEscalationCalculationLineDto
{
    public int Sequence { get; init; }
    public QuantitySurveyEscalationComponentType Component { get; init; }
    public decimal Coefficient { get; init; }
    public Guid IndexFamilyId { get; init; }
    public string IndexFamilyCode { get; init; } = string.Empty;
    public Guid BaseIndexValueId { get; init; }
    public Guid CurrentIndexValueId { get; init; }
    public decimal BaseIndexValue { get; init; }
    public decimal CurrentIndexValue { get; init; }
    public decimal IndexRatio { get; init; }
    public decimal WeightedContribution { get; init; }
}

public sealed class QuantitySurveyEscalationCalculationDto
{
    public Guid Id { get; init; }
    public string RunReference { get; init; } = string.Empty;
    public Guid FormulaId { get; init; }
    public string FormulaCode { get; init; } = string.Empty;
    public int FormulaVersion { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public Guid ContractId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public DateTime BaseIndexPeriod { get; init; }
    public DateTime CurrentIndexPeriod { get; init; }
    public DateTime CalculationDate { get; init; }
    public QuantitySurveyEscalationImpactTargetType ImpactTargetType { get; init; }
    public Guid ImpactTargetId { get; init; }
    public string ImpactTargetReference { get; init; } = string.Empty;
    public string ImpactTargetStatus { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BaseRate { get; init; }
    public decimal RevisedRate { get; init; }
    public decimal AdjustmentFactor { get; init; }
    public decimal CalculatedFluctuationAmount { get; init; }
    public decimal ReviewerAdjustmentAmount { get; init; }
    public string? ReviewerAdjustmentReason { get; init; }
    public Guid? ReviewedById { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public decimal ApprovedImpactAmount { get; init; }
    public string ImpactApplicationStatus { get; init; } = string.Empty;
    public Guid AuthorityRoleId { get; init; }
    public string AuthorityRoleName { get; init; } = string.Empty;
    public Guid? WorkflowInstanceId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ApprovalStatus { get; init; } = string.Empty;
    public Guid PreparedById { get; init; }
    public DateTime PreparedAt { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? RejectionReason { get; init; }
    public string SnapshotHash { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<QuantitySurveyEscalationCalculationLineDto> Lines { get; init; } = [];
}

public sealed class QuantitySurveyEscalationCalculationPageDto
{
    public IReadOnlyList<QuantitySurveyEscalationCalculationDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class QuantitySurveyEscalationCalculationRevisionDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? BeforeJson { get; init; }
    public string? AfterJson { get; init; }
    public DateTime CreatedAt { get; init; }
}
