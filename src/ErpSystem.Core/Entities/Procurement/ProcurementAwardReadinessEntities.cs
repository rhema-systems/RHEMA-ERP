using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementAwardReadinessDecisions")]
public sealed class ProcurementAwardReadinessDecision : TenantEntity
{
    public ProcurementAwardReadinessSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    [Required, StringLength(100)] public string SourceReference { get; set; } = string.Empty;
    public ProcurementMethodType Method { get; set; }
    [Range(1, int.MaxValue)] public int DecisionSequence { get; set; }
    public ProcurementAwardReadinessDecisionStatus Status { get; set; }

    public Guid? MethodRuleId { get; set; }
    [StringLength(100)] public string? MethodRuleCode { get; set; }
    public Guid? AuthorityRouteId { get; set; }
    [StringLength(100)] public string? AuthorityRouteReference { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }

    [Required, StringLength(100)] public string RecommendationSubjectType { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string RecommendedSubjectIdsJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string RecommendedBusinessPartnerIdsJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string RecommendationSnapshotJson { get; set; } = "{}";
    [Column(TypeName = "nvarchar(max)")] public string EvaluationLineageJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string SupplierLineageJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string PrequalificationLineageJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string VerificationLineageJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string AuthorityLineageJson { get; set; } = "{}";
    [Column(TypeName = "nvarchar(max)")] public string EvidenceLineageJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string PrerequisiteSnapshotJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string TimelineSnapshotJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string BlockedReasonsJson { get; set; } = "[]";

    [Required, StringLength(64)] public string SourceIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; }
    public Guid EvaluatedByUserId { get; set; }
    [Required, StringLength(300)] public string EvaluatedByName { get; set; } = string.Empty;
}
