using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class FinanceDimensionRouteCertificationDto
{
    public FinanceDimensionRouteId RouteId { get; set; }
    public string ProducerModule { get; set; } = string.Empty;
    public string SourceRoute { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = string.Empty;
    public string Grain { get; set; } = string.Empty;
    public bool SupportsDocumentDefaults { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public FinanceDimensionCertificationState State { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string? RowVersion { get; set; }
    public Guid? LatestAssessmentId { get; set; }
    public int? LatestBlockerCount { get; set; }
    public DateTime? LatestAssessmentExpiresAt { get; set; }
}

public sealed class FinanceDimensionReadinessBlockerDto
{
    [Required, MaxLength(100)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string Message { get; set; } = string.Empty;
    [MaxLength(100)] public string? LifecycleState { get; set; }
    public Guid? DocumentId { get; set; }
    [MaxLength(100)] public string? DocumentReference { get; set; }
    [MaxLength(150)] public string? DocumentLink { get; set; }
    [MaxLength(100)] public string? RemediationStatus { get; set; }
    [MaxLength(100)] public string? DimensionIssue { get; set; }
    public bool FixedRuleDrift { get; set; }
    public bool StaleBudgetEvidence { get; set; }
    [MaxLength(100)] public string? ActiveReservationState { get; set; }
    public string? Details { get; set; }
}

public sealed class FinanceDimensionReadinessAssessmentDto
{
    public Guid Id { get; set; }
    public FinanceDimensionRouteId RouteId { get; set; }
    public string ProducerModule { get; set; } = string.Empty;
    public string SourceRoute { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = string.Empty;
    public FinanceDimensionCertificationState CurrentState { get; set; }
    public FinanceDimensionCertificationState TargetState { get; set; }
    public int BlockerCount { get; set; }
    public IReadOnlyList<FinanceDimensionReadinessBlockerDto> Blockers { get; set; } =
        Array.Empty<FinanceDimensionReadinessBlockerDto>();
    public IReadOnlyDictionary<string, int> BlockerTotalsByLifecycle { get; set; } =
        new Dictionary<string, int>();
    public string DataVersionWatermark { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
}

public sealed class CreateFinanceDimensionReadinessAssessmentDto
{
    public FinanceDimensionCertificationState TargetState { get; set; } =
        FinanceDimensionCertificationState.Enforced;
}

public sealed class PromoteFinanceDimensionRouteDto
{
    [Required] public Guid ReadinessAssessmentId { get; set; }
    public FinanceDimensionCertificationState TargetState { get; set; } =
        FinanceDimensionCertificationState.Enforced;
    [Required, MinLength(10), MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    [MaxLength(500)] public string? RowVersion { get; set; }
}

/// <summary>
/// Additive, module-neutral source-line contract.  Producer adapters submit canonical assignments;
/// they never write Finance sets/snapshots or claim a producer identity in this payload.
/// </summary>
public sealed class FinanceSourceLineDimensionInputDto
{
    public Guid? SourceLineId { get; set; }
    [Required] public Guid AccountId { get; set; }
    public IReadOnlyList<FinancePostingDimensionValueDto> Dimensions { get; set; } =
        Array.Empty<FinancePostingDimensionValueDto>();
}

public sealed class FinanceSourceDocumentDimensionInputDto
{
    public IReadOnlyList<FinancePostingDimensionValueDto> DefaultDimensions { get; set; } =
        Array.Empty<FinancePostingDimensionValueDto>();
    public IReadOnlyList<FinanceSourceLineDimensionInputDto> Lines { get; set; } =
        Array.Empty<FinanceSourceLineDimensionInputDto>();
    public bool ApplyDefaultToEligibleLines { get; set; }
}

/// <summary>
/// Persisted, server-provenanced source assignment. A null SourceLineId is the clearable draft
/// header default; source lines remain authoritative for validation, approval and posting.
/// </summary>
public sealed class FinanceSourceDimensionAssignmentDto
{
    public Guid Id { get; set; }
    public FinanceDimensionRouteId RouteId { get; set; }
    public string ProducerModule { get; set; } = string.Empty;
    public string SourceRoute { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public Guid? SourceLineId { get; set; }
    public Guid? ResolvedAccountId { get; set; }
    public DateTime? SourceDocumentDate { get; set; }
    public int? ExpectedSourceLineCount { get; set; }
    public string? SourceLineManifestHash { get; set; }
    public Guid? FinanceDimensionSetId { get; set; }
    public Guid? FinanceDimensionSnapshotId { get; set; }
    public DateTime? EvidenceFrozenAt { get; set; }
    public bool IsFrozen => EvidenceFrozenAt.HasValue;
    public string BudgetEvidenceStatus { get; set; } = "NotApplicable";
    public string? BudgetEvaluationHash { get; set; }
    public DateTime? BudgetEvidenceUpdatedAt { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class FinanceSourceDimensionValueDto
{
    public string DimensionCode { get; set; } = string.Empty;
    public string DimensionName { get; set; } = string.Empty;
    public string ValueCode { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public string? RuleType { get; set; }
    public bool IsReadOnly { get; set; }
}

public sealed class FinanceSourceLineDimensionDto
{
    public Guid SourceLineId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? FinanceDimensionSetId { get; set; }
    public string? CombinationHash { get; set; }
    public string? DisplayValue { get; set; }
    public bool IsFrozen { get; set; }
    public IReadOnlyList<FinanceSourceDimensionValueDto> Values { get; set; } =
        Array.Empty<FinanceSourceDimensionValueDto>();
    public IReadOnlyList<string> ReadinessWarnings { get; set; } = Array.Empty<string>();
}

public sealed class FinanceSourceDocumentDimensionDto
{
    public FinanceDimensionRouteId RouteId { get; set; }
    public FinanceDimensionCertificationState CertificationState { get; set; }
    public Guid SourceDocumentId { get; set; }
    public Guid? DefaultFinanceDimensionSetId { get; set; }
    public IReadOnlyList<FinanceSourceDimensionValueDto> DefaultValues { get; set; } =
        Array.Empty<FinanceSourceDimensionValueDto>();
    public IReadOnlyList<FinanceSourceLineDimensionDto> Lines { get; set; } =
        Array.Empty<FinanceSourceLineDimensionDto>();
    public IReadOnlyList<string> ReadinessWarnings { get; set; } = Array.Empty<string>();
    public string BudgetEvidenceStatus { get; set; } = "NotApplicable";
    public string? BudgetEvaluationHash { get; set; }
    public DateTime? BudgetEvidenceUpdatedAt { get; set; }
}

public sealed class FinanceDimensionSnapshotDto
{
    public Guid Id { get; set; }
    public Guid FinanceDimensionSetId { get; set; }
    public string CombinationHash { get; set; } = string.Empty;
    public string DisplayValue { get; set; } = string.Empty;
    public string SnapshotSource { get; set; } = string.Empty;
    public DateTime SnapshotCapturedAt { get; set; }
    public string SnapshotQuality { get; set; } = string.Empty;
    public bool HistoricalNameReconstructed { get; set; }
    public IReadOnlyList<FinanceDimensionSnapshotItemDto> Items { get; set; } =
        Array.Empty<FinanceDimensionSnapshotItemDto>();
}

public sealed class FinanceDimensionSnapshotItemDto
{
    public Guid FinanceDimensionDefinitionId { get; set; }
    public Guid FinanceDimensionValueId { get; set; }
    public string DimensionCode { get; set; } = string.Empty;
    public string DimensionName { get; set; } = string.Empty;
    public string ValueCode { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public Guid? FinanceDimensionAccountRuleId { get; set; }
    public Guid? RuleFamilyId { get; set; }
    public int? RuleVersion { get; set; }
    public string? RuleType { get; set; }
}
