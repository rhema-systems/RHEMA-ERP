using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementComplianceDecisionRequest
{
    public Guid? PolicySetId { get; set; }
    [StringLength(50)] public string? PolicyCode { get; set; }
    public ProcurementCategoryClass Category { get; set; }
    [StringLength(150)] public string? ServiceClass { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal Amount { get; set; }
    [Required, StringLength(3), RegularExpression("^[A-Za-z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    public ProcurementMethodType? RequestedMethod { get; set; }
    [Required, StringLength(100)] public string SourceType { get; set; } = string.Empty;
    [Required, StringLength(200)] public string SourceReference { get; set; } = string.Empty;
    public DateTime? AtUtc { get; set; }
    public Guid? ActorUserId { get; set; }
    public List<string> ActorRoles { get; set; } = new();
    public Guid? SourceOwnerUserId { get; set; }
    public List<string> SourceOwnerRoles { get; set; } = new();
    [StringLength(150)] public string EntityType { get; set; } = "ProcurementDocument";
    [StringLength(100)] public string Action { get; set; } = "Evaluate";
    [StringLength(100)] public string? ExceptionType { get; set; }
    public bool JustificationProvided { get; set; }
    [StringLength(500)] public string? ExceptionApprovalReference { get; set; }
    public List<string> EvidenceReferenceKeys { get; set; } = new();
}

public sealed class ProcurementAuthorityRouteDecisionRequest
{
    public Guid? PolicySetId { get; set; }
    [StringLength(50)] public string? PolicyCode { get; set; }
    public ProcurementCategoryClass Category { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal Amount { get; set; }
    [Required, StringLength(3), RegularExpression("^[A-Za-z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    public DateTime? AtUtc { get; set; }
    [Required, StringLength(100)] public string SourceType { get; set; } = string.Empty;
    [Required, StringLength(200)] public string SourceReference { get; set; } = string.Empty;
}

public sealed class ProcurementAuthorityRouteDecisionDto
{
    public Guid EvaluationId { get; init; } = Guid.NewGuid();
    public DateTime EvaluatedAtUtc { get; init; }
    public DateTime PolicyDateUtc { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public bool IsReady { get; init; }
    public string DecisionCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public ProcurementCompliancePolicySelectionDto? Policy { get; init; }
    public ProcurementCategoryClass Category { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public ProcurementAuthorityWorkflowSelectionDto? Workflow { get; init; }
    public IReadOnlyList<ProcurementAuthorityRouteStepDecisionDto> Steps { get; init; } = Array.Empty<ProcurementAuthorityRouteStepDecisionDto>();
    public IReadOnlyList<ProcurementComplianceFindingDto> Findings { get; init; } = Array.Empty<ProcurementComplianceFindingDto>();
    public IReadOnlyList<string> RequiredActions { get; init; } = Array.Empty<string>();
}

public sealed class ProcurementAuthorityWorkflowSelectionDto
{
    public Guid WorkflowDefinitionId { get; init; }
    public Guid DefinitionKey { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Version { get; init; }
    public string EntityTypeCode { get; init; } = string.Empty;
    public string EntityTypeName { get; init; } = string.Empty;
    public DateTime? PublishedAt { get; init; }
}

public sealed class ProcurementAuthorityRouteStepDecisionDto
{
    public int Sequence { get; init; }
    public Guid RuleId { get; init; }
    public Guid RulePolicySetId { get; init; }
    public string RulePolicyCode { get; init; } = string.Empty;
    public int RulePolicyVersion { get; init; }
    public string RuleCode { get; init; } = string.Empty;
    public Guid? SourceRuleId { get; init; }
    public string SourceDecisionKey { get; init; } = string.Empty;
    public string AuthorityName { get; init; } = string.Empty;
    public string AuthorityRole { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal LowerBound { get; init; }
    public decimal? UpperBound { get; init; }
    public bool LowerInclusive { get; init; }
    public bool UpperInclusive { get; init; }
    public int Quorum { get; init; }
    public bool IsObserver { get; init; }
    public string? EscalationAuthority { get; init; }
    public Guid WorkflowDefinitionId { get; init; }
    public Guid WorkflowStepId { get; init; }
    public string WorkflowStepName { get; init; } = string.Empty;
    public int WorkflowStepOrder { get; init; }
}

public class ProcurementCompliancePolicyOptionDto
{
    public Guid PolicySetId { get; init; }
    public string PolicyCode { get; init; } = string.Empty;
    public string PolicyName { get; init; } = string.Empty;
    public int Version { get; init; }
    public ProcurementPolicyScopeType ScopeType { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsDefault { get; init; }
}

public sealed class ProcurementCompliancePolicySelectionDto : ProcurementCompliancePolicyOptionDto
{
    public Guid PolicyKey { get; init; }
    public Guid SourceConfigurationProfileId { get; init; }
    public Guid? BasePolicySetId { get; init; }
    public string SelectionReason { get; init; } = string.Empty;
}

public sealed class ProcurementComplianceRuleReferenceDto
{
    public Guid PolicySetId { get; init; }
    public string PolicyCode { get; init; } = string.Empty;
    public int PolicyVersion { get; init; }
    public Guid RuleId { get; init; }
    public ProcurementPolicyRuleKind RuleKind { get; init; }
    public string RuleCode { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;
    public string SourceDecisionKey { get; init; } = string.Empty;
    public Guid? SourceRuleId { get; init; }
    public ProcurementPolicyOverrideAction OverrideAction { get; init; }
    public string MatchReason { get; init; } = string.Empty;
}

public sealed class ProcurementComplianceFindingDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public ProcurementComplianceFindingSeverity Severity { get; init; }
    public Guid? RuleId { get; init; }
    public string? RuleCode { get; init; }
    public ProcurementPolicyRuleKind? RuleKind { get; init; }
    public string? SourceDecisionKey { get; init; }
}

public sealed class ProcurementComplianceMethodCandidateDto
{
    public ProcurementMethodType Method { get; init; }
    public bool IsAllowed { get; init; }
    public bool RequiresCompetition { get; init; }
    public int MinimumQuotationCount { get; init; }
    public Guid? WorkflowDefinitionId { get; init; }
    public string? ApplicabilityConditions { get; init; }
    public string MethodRuleCode { get; init; } = string.Empty;
    public string? ThresholdRuleCode { get; init; }
    public string? StatutoryReference { get; init; }
    public int Priority { get; init; }
    public bool MatchesAmount { get; init; }
    public string Explanation { get; init; } = string.Empty;
}

public sealed class ProcurementComplianceAuthorityDto
{
    public Guid RuleId { get; init; }
    public string RuleCode { get; init; } = string.Empty;
    public string AuthorityName { get; init; } = string.Empty;
    public string AuthorityRole { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public int Quorum { get; init; }
    public bool IsObserver { get; init; }
    public string? EscalationAuthority { get; init; }
    public Guid? WorkflowDefinitionId { get; init; }
    public string SourceDecisionKey { get; init; } = string.Empty;
}

public sealed class ProcurementComplianceEvidenceDto
{
    public Guid RuleId { get; init; }
    public string RuleCode { get; init; } = string.Empty;
    public string EvidenceName { get; init; } = string.Empty;
    public ProcurementEvidenceStage Stage { get; init; }
    public string? SharedRequirementKey { get; init; }
    public bool IsMandatory { get; init; }
    public bool RequiresVerification { get; init; }
    public int? MaximumAgeDays { get; init; }
    public string SourceDecisionKey { get; init; } = string.Empty;
}

public sealed class ProcurementComplianceRouteStepDto
{
    public ProcurementComplianceRouteStepType StepType { get; init; }
    public int Sequence { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ResponsibleRole { get; init; } = string.Empty;
    public int Quorum { get; init; } = 1;
    public Guid? WorkflowDefinitionId { get; init; }
    public string? EscalationAuthority { get; init; }
    public Guid RuleId { get; init; }
    public string RuleCode { get; init; } = string.Empty;
    public string SourceDecisionKey { get; init; } = string.Empty;
}

public sealed class ProcurementComplianceExceptionDto
{
    public Guid RuleId { get; init; }
    public string RuleCode { get; init; } = string.Empty;
    public string ExceptionName { get; init; } = string.Empty;
    public string ExceptionType { get; init; } = string.Empty;
    public ProcurementExceptionDisposition Disposition { get; init; }
    public bool JustificationRequired { get; init; }
    public bool EvidenceRequired { get; init; }
    public bool PostAwardFilingRequired { get; init; }
    public string ApproverRole { get; init; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; init; }
    public int? MaximumDurationDays { get; init; }
    public string SourceDecisionKey { get; init; } = string.Empty;
}

public sealed class ProcurementComplianceCategoryRequirementDto
{
    public Guid RuleId { get; init; }
    public string RuleCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool RequiresSpecification { get; init; }
    public string? SpecificationTemplateCode { get; init; }
    public string SourceDecisionKey { get; init; } = string.Empty;
}

public sealed class ProcurementComplianceTraceStepDto
{
    public int Sequence { get; init; }
    public string Stage { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public IReadOnlyList<string> RuleCodes { get; init; } = Array.Empty<string>();
}

public sealed class ProcurementComplianceDecisionDto
{
    public Guid EvaluationId { get; init; }
    public DateTime EvaluatedAtUtc { get; init; }
    public DateTime PolicyDateUtc { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public bool EvaluationOnly { get; init; } = true;
    public ProcurementComplianceOutcome Outcome { get; init; }
    public bool IsAllowed => Outcome == ProcurementComplianceOutcome.Allowed;
    public bool CanProceed => Outcome != ProcurementComplianceOutcome.Blocked;
    public ProcurementCompliancePolicySelectionDto Policy { get; init; } = new();
    public ProcurementCategoryClass Category { get; init; }
    public string? ServiceClass { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string SourceReference { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public IReadOnlyList<string> ActorRoles { get; init; } = Array.Empty<string>();
    public ProcurementMethodType? RequestedMethod { get; init; }
    public ProcurementMethodType? SelectedMethod { get; init; }
    public IReadOnlyList<ProcurementComplianceMethodCandidateDto> MethodCandidates { get; init; } = Array.Empty<ProcurementComplianceMethodCandidateDto>();
    public IReadOnlyList<ProcurementComplianceCategoryRequirementDto> CategoryRequirements { get; init; } = Array.Empty<ProcurementComplianceCategoryRequirementDto>();
    public IReadOnlyList<ProcurementComplianceAuthorityDto> RequiredAuthorities { get; init; } = Array.Empty<ProcurementComplianceAuthorityDto>();
    public IReadOnlyList<ProcurementComplianceEvidenceDto> RequiredEvidence { get; init; } = Array.Empty<ProcurementComplianceEvidenceDto>();
    public IReadOnlyList<ProcurementComplianceRouteStepDto> Route { get; init; } = Array.Empty<ProcurementComplianceRouteStepDto>();
    public IReadOnlyList<ProcurementComplianceExceptionDto> ApplicableExceptions { get; init; } = Array.Empty<ProcurementComplianceExceptionDto>();
    public ProcurementComplianceExceptionDto? SelectedException { get; init; }
    public IReadOnlyList<ProcurementComplianceFindingDto> HardStops { get; init; } = Array.Empty<ProcurementComplianceFindingDto>();
    public IReadOnlyList<ProcurementComplianceFindingDto> ReviewRequirements { get; init; } = Array.Empty<ProcurementComplianceFindingDto>();
    public IReadOnlyList<ProcurementComplianceFindingDto> Warnings { get; init; } = Array.Empty<ProcurementComplianceFindingDto>();
    public IReadOnlyList<ProcurementComplianceRuleReferenceDto> MatchedRules { get; init; } = Array.Empty<ProcurementComplianceRuleReferenceDto>();
    public IReadOnlyList<ProcurementComplianceTraceStepDto> Trace { get; init; } = Array.Empty<ProcurementComplianceTraceStepDto>();
}

public sealed class ProcurementComplianceRequestValidationException : Exception
{
    public ProcurementComplianceRequestValidationException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}

public sealed class ProcurementCompliancePolicyNotFoundException : Exception
{
    public ProcurementCompliancePolicyNotFoundException(string message) : base(message) { }
}

public sealed class ProcurementCompliancePolicyConflictException : Exception
{
    public ProcurementCompliancePolicyConflictException(string message) : base(message) { }
}
