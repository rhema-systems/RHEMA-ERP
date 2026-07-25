using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementRequisitionAuthorityRoutes")]
public sealed class ProcurementRequisitionAuthorityRoute : TenantEntity
{
    public Guid PurchaseRequisitionId { get; set; }
    [Range(1, int.MaxValue)] public int AttemptNumber { get; set; }
    [Required, StringLength(100)] public string RouteReference { get; set; } = string.Empty;
    public Guid EvaluationId { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;

    public Guid PolicySetId { get; set; }
    public Guid PolicyKey { get; set; }
    [Required, StringLength(50)] public string PolicyCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string PolicyName { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int PolicyVersion { get; set; }
    public ProcurementPolicyScopeType PolicyScopeType { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
    public Guid? BasePolicySetId { get; set; }

    public ProcurementCategoryClass Category { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    public DateTime PolicyDateUtc { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }

    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowDefinitionKey { get; set; }
    [Required, StringLength(100)] public string WorkflowName { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int WorkflowVersion { get; set; }
    [Required, StringLength(50)] public string WorkflowEntityTypeCode { get; set; } = string.Empty;

    public DateTime CapturedAtUtc { get; set; }
    public Guid CapturedById { get; set; }
    [Required, StringLength(300)] public string CapturedByName { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public PurchaseRequisition PurchaseRequisition { get; set; } = null!;
    public ProcurementPolicySet PolicySet { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public ICollection<ProcurementRequisitionAuthorityRouteStep> Steps { get; set; } = new List<ProcurementRequisitionAuthorityRouteStep>();
}

[Table("ProcurementRequisitionAuthorityRouteSteps")]
public sealed class ProcurementRequisitionAuthorityRouteStep : TenantEntity
{
    public Guid AuthorityRouteId { get; set; }
    [Range(1, 100)] public int Sequence { get; set; }
    public Guid AuthorityRuleId { get; set; }
    public Guid RulePolicySetId { get; set; }
    [Required, StringLength(50)] public string RulePolicyCode { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int RulePolicyVersion { get; set; }
    [Required, StringLength(50)] public string RuleCode { get; set; } = string.Empty;
    [Required, StringLength(7)] public string SourceDecisionKey { get; set; } = "DEC-002";
    public Guid? SourceRuleId { get; set; }
    [Required, StringLength(200)] public string AuthorityName { get; set; } = string.Empty;
    [Required, StringLength(150)] public string AuthorityRole { get; set; } = string.Empty;
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal LowerBound { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? UpperBound { get; set; }
    public bool LowerInclusive { get; set; }
    public bool UpperInclusive { get; set; }
    [Range(1, 100)] public int Quorum { get; set; }
    public bool IsObserver { get; set; }
    [StringLength(200)] public string? EscalationAuthority { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowStepId { get; set; }
    [Required, StringLength(100)] public string WorkflowStepName { get; set; } = string.Empty;
    public int WorkflowStepOrder { get; set; }

    public ProcurementRequisitionAuthorityRoute AuthorityRoute { get; set; } = null!;
    public ProcurementPolicyAuthorityRule AuthorityRule { get; set; } = null!;
    public ProcurementPolicySet RulePolicySet { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowStep WorkflowStep { get; set; } = null!;
}
