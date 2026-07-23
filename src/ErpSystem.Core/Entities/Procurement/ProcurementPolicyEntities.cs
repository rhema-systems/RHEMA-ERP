using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementPolicySets")]
public class ProcurementPolicySet : TenantEntity
{
    public Guid PolicyKey { get; set; } = Guid.NewGuid();
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public int Version { get; set; } = 1;
    public ProcurementPolicyLifecycleStatus LifecycleStatus { get; set; } = ProcurementPolicyLifecycleStatus.Draft;
    public ProcurementPolicyScopeType ScopeType { get; set; } = ProcurementPolicyScopeType.TenantBaseline;
    public Guid SourceConfigurationProfileId { get; set; }
    public Guid? BasePolicySetId { get; set; }
    public Guid? SupersedesPolicySetId { get; set; }
    [Required, StringLength(3)] public string DefaultCurrencyCode { get; set; } = "GHS";
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    public bool IsDefault { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? RetiredAt { get; set; }
    public Guid? RetiredById { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ProcurementConfigurationProfile SourceConfigurationProfile { get; set; } = null!;
    public virtual ICollection<ProcurementPolicyCategoryRule> CategoryRules { get; set; } = new List<ProcurementPolicyCategoryRule>();
    public virtual ICollection<ProcurementPolicyMethodRule> MethodRules { get; set; } = new List<ProcurementPolicyMethodRule>();
    public virtual ICollection<ProcurementPolicyThresholdRule> ThresholdRules { get; set; } = new List<ProcurementPolicyThresholdRule>();
    public virtual ICollection<ProcurementPolicyAuthorityRule> AuthorityRules { get; set; } = new List<ProcurementPolicyAuthorityRule>();
    public virtual ICollection<ProcurementPolicyEvidenceRule> EvidenceRules { get; set; } = new List<ProcurementPolicyEvidenceRule>();
    public virtual ICollection<ProcurementPolicyExceptionRule> ExceptionRules { get; set; } = new List<ProcurementPolicyExceptionRule>();
    public virtual ICollection<ProcurementPolicySodRule> SodRules { get; set; } = new List<ProcurementPolicySodRule>();
}

[Table("ProcurementPolicyCategoryRules")]
public class ProcurementPolicyCategoryRule : TenantEntity
{
    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string RuleCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public ProcurementCategoryClass Category { get; set; }
    [StringLength(150)] public string? ServiceClass { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    public bool RequiresSpecification { get; set; } = true;
    [StringLength(200)] public string? SpecificationTemplateCode { get; set; }
    public ProcurementPolicyOverrideAction OverrideAction { get; set; }
    public Guid? SourceRuleId { get; set; }
    [StringLength(7)] public string SourceDecisionKey { get; set; } = "DEC-001";
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual ProcurementPolicySet PolicySet { get; set; } = null!;
}

[Table("ProcurementPolicyMethodRules")]
public class ProcurementPolicyMethodRule : TenantEntity
{
    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string RuleCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public ProcurementCategoryClass Category { get; set; }
    [StringLength(150)] public string? ServiceClass { get; set; }
    public ProcurementMethodType Method { get; set; }
    public bool IsAllowed { get; set; } = true;
    public bool RequiresCompetition { get; set; } = true;
    [Range(0, 100)] public int MinimumQuotationCount { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    [StringLength(1000)] public string? ApplicabilityConditions { get; set; }
    public ProcurementPolicyOverrideAction OverrideAction { get; set; }
    public Guid? SourceRuleId { get; set; }
    [StringLength(7)] public string SourceDecisionKey { get; set; } = "DEC-001";
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual ProcurementPolicySet PolicySet { get; set; } = null!;
}

[Table("ProcurementPolicyThresholdRules")]
public class ProcurementPolicyThresholdRule : TenantEntity
{
    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string RuleCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public ProcurementCategoryClass Category { get; set; }
    [StringLength(150)] public string? ServiceClass { get; set; }
    public ProcurementMethodType Method { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = "GHS";
    [Column(TypeName = "decimal(18,2)")] public decimal LowerBound { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? UpperBound { get; set; }
    public bool LowerInclusive { get; set; } = true;
    public bool UpperInclusive { get; set; } = true;
    [Required, StringLength(500)] public string StatutoryReference { get; set; } = string.Empty;
    public ProcurementPolicyOverrideAction OverrideAction { get; set; }
    public Guid? SourceRuleId { get; set; }
    [StringLength(7)] public string SourceDecisionKey { get; set; } = "DEC-001";
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual ProcurementPolicySet PolicySet { get; set; } = null!;
}

[Table("ProcurementPolicyAuthorityRules")]
public class ProcurementPolicyAuthorityRule : TenantEntity
{
    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string RuleCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string AuthorityName { get; set; } = string.Empty;
    [Required, StringLength(150)] public string AuthorityRole { get; set; } = string.Empty;
    public ProcurementCategoryClass? Category { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = "GHS";
    [Column(TypeName = "decimal(18,2)")] public decimal LowerBound { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? UpperBound { get; set; }
    public bool LowerInclusive { get; set; } = true;
    public bool UpperInclusive { get; set; } = true;
    [Range(1, 100)] public int Sequence { get; set; } = 1;
    [Range(1, 100)] public int Quorum { get; set; } = 1;
    public bool IsObserver { get; set; }
    [StringLength(200)] public string? EscalationAuthority { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public ProcurementPolicyOverrideAction OverrideAction { get; set; }
    public Guid? SourceRuleId { get; set; }
    [StringLength(7)] public string SourceDecisionKey { get; set; } = "DEC-002";
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual ProcurementPolicySet PolicySet { get; set; } = null!;
}

[Table("ProcurementPolicyEvidenceRules")]
public class ProcurementPolicyEvidenceRule : TenantEntity
{
    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string RuleCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string EvidenceName { get; set; } = string.Empty;
    public ProcurementEvidenceStage Stage { get; set; }
    public ProcurementCategoryClass? Category { get; set; }
    public ProcurementMethodType? Method { get; set; }
    [StringLength(200)] public string? SharedRequirementKey { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool RequiresVerification { get; set; } = true;
    [Range(0, 36500)] public int? MaximumAgeDays { get; set; }
    public ProcurementPolicyOverrideAction OverrideAction { get; set; }
    public Guid? SourceRuleId { get; set; }
    [StringLength(7)] public string SourceDecisionKey { get; set; } = "DEC-006";
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual ProcurementPolicySet PolicySet { get; set; } = null!;
}

[Table("ProcurementPolicyExceptionRules")]
public class ProcurementPolicyExceptionRule : TenantEntity
{
    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string RuleCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string ExceptionName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ExceptionType { get; set; } = string.Empty;
    public ProcurementCategoryClass? Category { get; set; }
    public ProcurementMethodType? Method { get; set; }
    public ProcurementExceptionDisposition Disposition { get; set; } = ProcurementExceptionDisposition.ApprovalRequired;
    public bool JustificationRequired { get; set; } = true;
    public bool EvidenceRequired { get; set; } = true;
    public bool PostAwardFilingRequired { get; set; }
    [Required, StringLength(200)] public string ApproverRole { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    [Range(1, 3650)] public int? MaximumDurationDays { get; set; }
    public ProcurementPolicyOverrideAction OverrideAction { get; set; }
    public Guid? SourceRuleId { get; set; }
    [StringLength(7)] public string SourceDecisionKey { get; set; } = "DEC-006";
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual ProcurementPolicySet PolicySet { get; set; } = null!;
}

[Table("ProcurementPolicySodRules")]
public class ProcurementPolicySodRule : TenantEntity
{
    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string RuleCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(150)] public string InitiatorRole { get; set; } = string.Empty;
    [Required, StringLength(150)] public string ConflictingRole { get; set; } = string.Empty;
    [Required, StringLength(150)] public string EntityType { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public ProcurementSodEnforcement Enforcement { get; set; } = ProcurementSodEnforcement.HardStop;
    [StringLength(1000)] public string? Explanation { get; set; }
    public ProcurementPolicyOverrideAction OverrideAction { get; set; }
    public Guid? SourceRuleId { get; set; }
    [StringLength(7)] public string SourceDecisionKey { get; set; } = "DEC-004";
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual ProcurementPolicySet PolicySet { get; set; } = null!;
}

[Table("ProcurementPolicyRevisions")]
public class ProcurementPolicyRevision : TenantEntity
{
    public Guid PolicySetId { get; set; }
    public Guid? RuleId { get; set; }
    public ProcurementPolicyRuleKind? RuleKind { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Result { get; set; } = "Succeeded";
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? AfterJson { get; set; }
}
