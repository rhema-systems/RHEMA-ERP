using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyDesignImpactRoute
{
    Measurement = 0,
    Variation = 1
}

public enum QuantitySurveyDesignImpactType
{
    RemeasurementRequired = 0,
    QuantityIncrease = 1,
    QuantityDecrease = 2,
    ScopeAddition = 3,
    Omission = 4,
    RateReviewOnly = 5
}

public static class QuantitySurveyDesignImpactStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

[Table("QuantitySurveyDesignRevisionImpacts")]
public sealed class QuantitySurveyDesignRevisionImpact : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid PreviousDrawingId { get; set; }
    public Guid RevisedDrawingId { get; set; }
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid? LastMutationClientRequestId { get; set; }
    [StringLength(64)] public string? LastMutationRequestHash { get; set; }
    [Required, StringLength(50)] public string ImpactNumber { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string ChangeSummary { get; set; } = string.Empty;
    public QuantitySurveyDesignImpactRoute Route { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = QuantitySurveyDesignImpactStatuses.Draft;
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    [Required, StringLength(40)] public string WorkflowEntityTypeCode { get; set; } = string.Empty;
    [Required, StringLength(64)] public string PolicyHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string PreviousDrawingNumberSnapshot { get; set; } = string.Empty;
    [StringLength(30)] public string? PreviousRevisionSnapshot { get; set; }
    [Required, StringLength(100)] public string RevisedDrawingNumberSnapshot { get; set; } = string.Empty;
    [Required, StringLength(30)] public string RevisedRevisionSnapshot { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(2000)] public string? RejectionReason { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Project Project { get; set; } = null!;
    public ProjectDrawing PreviousDrawing { get; set; } = null!;
    public ProjectDrawing RevisedDrawing { get; set; } = null!;
    public QuantitySurveyConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public ICollection<QuantitySurveyDesignRevisionImpactLine> Lines { get; set; } = [];
    public ICollection<QuantitySurveyDesignRevisionImpactRevision> Revisions { get; set; } = [];
}

[Table("QuantitySurveyDesignRevisionImpactLines")]
public sealed class QuantitySurveyDesignRevisionImpactLine : TenantEntity
{
    public Guid ImpactId { get; set; }
    public Guid ProjectBoqVersionId { get; set; }
    public Guid ProjectBoqVersionLineId { get; set; }
    public Guid BoqLineKey { get; set; }
    public QuantitySurveyDesignImpactType ImpactType { get; set; }
    [Required, StringLength(50)] public string LineReferenceSnapshot { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string DescriptionSnapshot { get; set; } = string.Empty;
    [StringLength(50)] public string? UnitSnapshot { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal PreviousQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? IndicativeQuantity { get; set; }
    [Required, StringLength(1000)] public string ImpactReason { get; set; } = string.Empty;

    public QuantitySurveyDesignRevisionImpact Impact { get; set; } = null!;
    public ProjectBoqVersion ProjectBoqVersion { get; set; } = null!;
    public ProjectBoqVersionLine ProjectBoqVersionLine { get; set; } = null!;
}

[Table("QuantitySurveyDesignRevisionImpactRevisions")]
public sealed class QuantitySurveyDesignRevisionImpactRevision : TenantEntity
{
    public Guid ImpactId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(2000)] public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    [Required] public string AfterJson { get; set; } = string.Empty;
    public QuantitySurveyDesignRevisionImpact Impact { get; set; } = null!;
}
