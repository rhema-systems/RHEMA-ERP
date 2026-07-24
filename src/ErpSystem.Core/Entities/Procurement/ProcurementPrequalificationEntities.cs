using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementPrequalificationExercises")]
public sealed class ProcurementPrequalificationExercise : TenantEntity
{
    [Required, StringLength(100)] public string Reference { get; set; } = string.Empty;
    [Required, StringLength(300)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Description { get; set; } = string.Empty;
    public ProcurementPrequalificationStatus Status { get; set; } = ProcurementPrequalificationStatus.Draft;
    [Column(TypeName = "nvarchar(max)")] public string CategoryIdsJson { get; set; } = "[]";
    public DateTime OpensAtUtc { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public int ValidityMonths { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal PassingScore { get; set; }
    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string PolicySetCode { get; set; } = string.Empty;
    public int PolicySetVersion { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }

    [StringLength(300)] public string? AdvertisementReference { get; set; }
    [StringLength(500)] public string? AdvertisementEvidenceReference { get; set; }
    public DateTime? AdvertisedAtUtc { get; set; }
    public Guid? AdvertisedById { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedById { get; set; }
    public DateTime? SubmittedForApprovalAtUtc { get; set; }
    public Guid? SubmittedForApprovalById { get; set; }
    [StringLength(300)] public string? DecisionReference { get; set; }
    [StringLength(500)] public string? DecisionEvidenceReference { get; set; }
    [StringLength(1000)] public string? DecisionReason { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public Guid? DecidedById { get; set; }

    [Column(TypeName = "nvarchar(max)")] public string LifecycleSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementPolicySet PolicySet { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ICollection<ProcurementPrequalificationCriterion> Criteria { get; set; } = new List<ProcurementPrequalificationCriterion>();
    public ICollection<ProcurementPrequalificationApplication> Applications { get; set; } = new List<ProcurementPrequalificationApplication>();
    public ICollection<ProcurementQualifiedListEntry> QualifiedEntries { get; set; } = new List<ProcurementQualifiedListEntry>();
}

[Table("ProcurementPrequalificationCriteria")]
public sealed class ProcurementPrequalificationCriterion : TenantEntity
{
    public Guid ExerciseId { get; set; }
    [Required, StringLength(100)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(300)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal Weight { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal MinimumScore { get; set; }
    public bool IsMandatory { get; set; }
    public bool RequiresEvidence { get; set; }
    public int SortOrder { get; set; }
    public ProcurementPrequalificationExercise Exercise { get; set; } = null!;
    public ICollection<ProcurementPrequalificationScore> Scores { get; set; } = new List<ProcurementPrequalificationScore>();
}

[Table("ProcurementPrequalificationApplications")]
public sealed class ProcurementPrequalificationApplication : TenantEntity
{
    public Guid ExerciseId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    [Required, StringLength(120)] public string ApplicationNumber { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string CategoryIdsJson { get; set; } = "[]";
    [Column(TypeName = "nvarchar(max)")] public string EvidenceJson { get; set; } = "[]";
    public ProcurementPrequalificationApplicationStatus Status { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public Guid SubmittedById { get; set; }
    public DateTime? EvaluatedAtUtc { get; set; }
    public Guid? EvaluatedById { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? TotalScore { get; set; }
    public bool? Passed { get; set; }
    [StringLength(2000)] public string? EvaluationRemarks { get; set; }
    [StringLength(500)] public string? RecommendationEvidenceReference { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string EvaluationSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementPrequalificationExercise Exercise { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
    public ICollection<ProcurementPrequalificationScore> Scores { get; set; } = new List<ProcurementPrequalificationScore>();
    public ICollection<ProcurementQualifiedListEntry> QualifiedEntries { get; set; } = new List<ProcurementQualifiedListEntry>();
}

[Table("ProcurementPrequalificationScores")]
public sealed class ProcurementPrequalificationScore : TenantEntity
{
    public Guid ApplicationId { get; set; }
    public Guid CriterionId { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal Score { get; set; }
    public bool MeetsRequirement { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [StringLength(500)] public string? EvidenceReference { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }
    public Guid EvaluatedById { get; set; }
    public ProcurementPrequalificationApplication Application { get; set; } = null!;
    public ProcurementPrequalificationCriterion Criterion { get; set; } = null!;
}

[Table("ProcurementQualifiedListEntries")]
public sealed class ProcurementQualifiedListEntry : TenantEntity
{
    public Guid ExerciseId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public Guid CategoryId { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public ProcurementQualifiedListEntryStatus Status { get; set; }
    [Required, StringLength(300)] public string ApprovalReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string ApprovalEvidenceReference { get; set; } = string.Empty;
    public DateTime? ExpiredAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    [StringLength(1000)] public string? RevocationReason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string LifecycleSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementPrequalificationExercise Exercise { get; set; } = null!;
    public ProcurementPrequalificationApplication Application { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
    public PartnerCategory Category { get; set; } = null!;
}
