using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyEscalationComponentType
{
    Material = 0,
    Labour = 1,
    Plant = 2,
    Other = 3
}

[Table("QuantitySurveyPriceIndexFamilies")]
public sealed class QuantitySurveyPriceIndexFamily : TenantEntity
{
    [Required, StringLength(40)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(160)] public string Name { get; set; } = string.Empty;
    public QuantitySurveyIndexSource Source { get; set; }
    [Required, StringLength(160)] public string Publisher { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

[Table("QuantitySurveyEscalationFormulas")]
public sealed class QuantitySurveyEscalationFormulaDefinition : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid FormulaKey { get; set; } = Guid.NewGuid();
    [Required, StringLength(40)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public Guid ProjectId { get; set; }
    public Guid ContractId { get; set; }
    [Required, StringLength(120)] public string ContractClauseReference { get; set; } = string.Empty;
    public QuantitySurveyEscalationFormula FormulaType { get; set; }
    public DateTime BaseDate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public Guid AuthorityRoleId { get; set; }
    [Required, StringLength(160)] public string AuthorityRoleNameSnapshot { get; set; } = string.Empty;
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid? SupersedesFormulaId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string SnapshotHash { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = "Draft";
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    public Guid PreparedById { get; set; }
    public DateTime PreparedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(1000)] public string? RejectionReason { get; set; }
    public Guid? RetiredById { get; set; }
    public DateTime? RetiredAt { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(1000)] public string? ChangeReason { get; set; }
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public ApplicationRole AuthorityRole { get; set; } = null!;
    public QuantitySurveyConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public WorkflowDefinition ApprovalWorkflowDefinition { get; set; } = null!;
    public CentralDocumentRecord CentralDocumentRecord { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public QuantitySurveyEscalationFormulaDefinition? SupersedesFormula { get; set; }
    public ICollection<QuantitySurveyEscalationFormulaComponent> Components { get; set; } = new List<QuantitySurveyEscalationFormulaComponent>();
}

[Table("QuantitySurveyEscalationFormulaComponents")]
public sealed class QuantitySurveyEscalationFormulaComponent : TenantEntity
{
    public Guid FormulaId { get; set; }
    public int Sequence { get; set; }
    public QuantitySurveyEscalationComponentType Component { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal Coefficient { get; set; }
    public Guid IndexFamilyId { get; set; }
    public QuantitySurveyIndexSource IndexSourceSnapshot { get; set; }
    [Required, StringLength(40)] public string IndexFamilyCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(160)] public string IndexFamilyNameSnapshot { get; set; } = string.Empty;

    public QuantitySurveyEscalationFormulaDefinition Formula { get; set; } = null!;
    public QuantitySurveyPriceIndexFamily IndexFamily { get; set; } = null!;
}

[Table("QuantitySurveyEscalationFormulaRevisions")]
public sealed class QuantitySurveyEscalationFormulaRevision : TenantEntity
{
    public Guid FormulaId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? AfterJson { get; set; }

    public QuantitySurveyEscalationFormulaDefinition Formula { get; set; } = null!;
}
