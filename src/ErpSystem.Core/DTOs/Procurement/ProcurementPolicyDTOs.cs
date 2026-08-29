using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementPolicySetListRequest
{
    public string? Search { get; set; }
    public ProcurementPolicyLifecycleStatus? Status { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public class ProcurementPolicySetSummaryDto
{
    public Guid Id { get; init; }
    public Guid PolicyKey { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Version { get; init; }
    public ProcurementPolicyLifecycleStatus LifecycleStatus { get; init; }
    public ProcurementPolicyScopeType ScopeType { get; init; }
    public Guid SourceConfigurationProfileId { get; init; }
    public string SourceConfigurationProfileCode { get; init; } = string.Empty;
    public int SourceConfigurationProfileVersion { get; init; }
    public string DefaultCurrencyCode { get; init; } = string.Empty;
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsDefault { get; init; }
    public int RuleCount { get; init; }
    public int RuleFamilyCount { get; init; }
    public bool IsComplete { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProcurementPolicySetDto : ProcurementPolicySetSummaryDto
{
    public string? Description { get; init; }
    public string? ChangeSummary { get; init; }
    public Guid? BasePolicySetId { get; init; }
    public Guid? SupersedesPolicySetId { get; init; }
    public DateTime? PublishedAt { get; init; }
    public Guid? PublishedById { get; init; }
    public DateTime? RetiredAt { get; init; }
    public Guid? RetiredById { get; init; }
    public IReadOnlyList<ProcurementPolicyRuleDto> Rules { get; init; } = Array.Empty<ProcurementPolicyRuleDto>();
    public ProcurementPolicyValidationResultDto Validation { get; init; } = new();
    public IReadOnlyList<ProcurementPolicyRevisionDto> RecentHistory { get; init; } = Array.Empty<ProcurementPolicyRevisionDto>();
}

public sealed class ProcurementPolicyRuleDto
{
    public Guid Id { get; init; }
    public ProcurementPolicyRuleKind Kind { get; init; }
    public string RuleCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Priority { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public ProcurementPolicyOverrideAction OverrideAction { get; init; }
    public Guid? SourceRuleId { get; init; }
    public string SourceDecisionKey { get; init; } = string.Empty;
    public JsonElement Value { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProcurementPolicyRevisionDto
{
    public Guid Id { get; init; }
    public Guid PolicySetId { get; init; }
    public Guid? RuleId { get; init; }
    public ProcurementPolicyRuleKind? RuleKind { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string? Reason { get; init; }
    public JsonElement? Before { get; init; }
    public JsonElement? After { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class ProcurementPolicyValidationIssueDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public ProcurementPolicyRuleKind? RuleKind { get; init; }
    public Guid? RuleId { get; init; }
    public string? RuleCode { get; init; }
    public string Severity { get; init; } = "Error";
}

public sealed class ProcurementPolicyValidationResultDto
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<ProcurementPolicyValidationIssueDto> Errors { get; init; } = Array.Empty<ProcurementPolicyValidationIssueDto>();
    public IReadOnlyList<ProcurementPolicyValidationIssueDto> Warnings { get; init; } = Array.Empty<ProcurementPolicyValidationIssueDto>();
}

public sealed class CreateProcurementPolicySetRequest
{
    public Guid SourceConfigurationProfileId { get; set; }
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public ProcurementPolicyScopeType ScopeType { get; set; }
    public Guid? BasePolicySetId { get; set; }
    [Required, StringLength(3), RegularExpression("^[A-Z]{3}$")] public string DefaultCurrencyCode { get; set; } = "GHS";
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class UpdateProcurementPolicySetRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Required, StringLength(3), RegularExpression("^[A-Z]{3}$")] public string DefaultCurrencyCode { get; set; } = "GHS";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    public bool IsDefault { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class ProcurementPolicyLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class CloneProcurementPolicySetRequest
{
    [StringLength(1000)] public string? ChangeSummary { get; set; }
}

public abstract class SaveProcurementPolicyRuleValueBase
{
    [Required, StringLength(50)] public string RuleCode { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public ProcurementPolicyOverrideAction OverrideAction { get; set; }
    public Guid? SourceRuleId { get; set; }
    [Required, StringLength(7), RegularExpression("^DEC-[0-9]{3}$")] public string SourceDecisionKey { get; set; } = string.Empty;
}

public sealed class SaveProcurementPolicyCategoryRuleValue : SaveProcurementPolicyRuleValueBase
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public ProcurementCategoryClass Category { get; set; }
    [StringLength(150)] public string? ServiceClass { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    public bool RequiresSpecification { get; set; } = true;
    [StringLength(200)] public string? SpecificationTemplateCode { get; set; }
}

public sealed class SaveProcurementPolicyMethodRuleValue : SaveProcurementPolicyRuleValueBase
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public ProcurementCategoryClass Category { get; set; }
    [StringLength(150)] public string? ServiceClass { get; set; }
    public ProcurementMethodType Method { get; set; }
    public bool IsAllowed { get; set; } = true;
    public bool RequiresCompetition { get; set; } = true;
    public bool JustificationRequired { get; set; }
    [Range(0, 100)] public int MinimumQuotationCount { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    [StringLength(1000)] public string? ApplicabilityConditions { get; set; }
}

public sealed class SaveProcurementPolicyThresholdRuleValue : SaveProcurementPolicyRuleValueBase
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public ProcurementCategoryClass Category { get; set; }
    [StringLength(150)] public string? ServiceClass { get; set; }
    public ProcurementMethodType Method { get; set; }
    [Required, StringLength(3), RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public bool LowerInclusive { get; set; } = true;
    public bool UpperInclusive { get; set; } = true;
    [Required, StringLength(500)] public string StatutoryReference { get; set; } = string.Empty;
}

public sealed class SaveProcurementPolicyAuthorityRuleValue : SaveProcurementPolicyRuleValueBase
{
    [Required, StringLength(200)] public string AuthorityName { get; set; } = string.Empty;
    public Guid? AuthorityRoleId { get; set; }
    [StringLength(150)] public string AuthorityRole { get; set; } = string.Empty;
    public ProcurementCategoryClass? Category { get; set; }
    [Required, StringLength(3), RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public bool LowerInclusive { get; set; } = true;
    public bool UpperInclusive { get; set; } = true;
    [Range(1, 100)] public int Sequence { get; set; } = 1;
    [Range(1, 100)] public int Quorum { get; set; } = 1;
    public bool IsObserver { get; set; }
    public Guid? EscalationAuthorityRoleId { get; set; }
    [StringLength(200)] public string? EscalationAuthority { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
}

public sealed class SaveProcurementPolicyEvidenceRuleValue : SaveProcurementPolicyRuleValueBase
{
    [Required, StringLength(200)] public string EvidenceName { get; set; } = string.Empty;
    public ProcurementEvidenceStage Stage { get; set; }
    public ProcurementCategoryClass? Category { get; set; }
    public ProcurementMethodType? Method { get; set; }
    [StringLength(200)] public string? SharedRequirementKey { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool RequiresVerification { get; set; } = true;
    [Range(0, 36500)] public int? MaximumAgeDays { get; set; }
}

public sealed class SaveProcurementPolicyExceptionRuleValue : SaveProcurementPolicyRuleValueBase
{
    [Required, StringLength(200)] public string ExceptionName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ExceptionType { get; set; } = string.Empty;
    public ProcurementCategoryClass? Category { get; set; }
    public ProcurementMethodType? Method { get; set; }
    public ProcurementExceptionDisposition Disposition { get; set; } = ProcurementExceptionDisposition.ApprovalRequired;
    public bool JustificationRequired { get; set; } = true;
    public bool EvidenceRequired { get; set; } = true;
    public bool PostAwardFilingRequired { get; set; }
    public Guid? ApproverRoleId { get; set; }
    [StringLength(200)] public string ApproverRole { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    [Range(1, 3650)] public int? MaximumDurationDays { get; set; }
}

public sealed class SaveProcurementPolicySodRuleValue : SaveProcurementPolicyRuleValueBase
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    public Guid? InitiatorRoleId { get; set; }
    [StringLength(150)] public string InitiatorRole { get; set; } = string.Empty;
    public Guid? ConflictingRoleId { get; set; }
    [StringLength(150)] public string ConflictingRole { get; set; } = string.Empty;
    [Required, StringLength(150)] public string EntityType { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public ProcurementSodEnforcement Enforcement { get; set; } = ProcurementSodEnforcement.HardStop;
    [StringLength(1000)] public string? Explanation { get; set; }
}

public sealed class ProcurementPolicyRoleOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsAssignedToSelectedWorkflow { get; set; }
}

public sealed class SaveProcurementPolicyRuleRequest
{
    public ProcurementPolicyRuleKind Kind { get; set; }
    public SaveProcurementPolicyCategoryRuleValue? Category { get; set; }
    public SaveProcurementPolicyMethodRuleValue? Method { get; set; }
    public SaveProcurementPolicyThresholdRuleValue? Threshold { get; set; }
    public SaveProcurementPolicyAuthorityRuleValue? Authority { get; set; }
    public SaveProcurementPolicyEvidenceRuleValue? Evidence { get; set; }
    public SaveProcurementPolicyExceptionRuleValue? Exception { get; set; }
    public SaveProcurementPolicySodRuleValue? SegregationOfDuties { get; set; }
    public string? RowVersion { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class DeleteProcurementPolicyRuleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}
