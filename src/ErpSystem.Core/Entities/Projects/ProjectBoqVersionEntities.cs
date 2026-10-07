using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.Projects;

public static class ProjectBoqVersionStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Retired = "Retired";
}

public sealed class ProjectBoqVersion : TenantEntity, IQuantitySurveyWorkflowRecord
{
    public Guid ProjectId { get; set; }
    public Guid? SourceVersionId { get; set; }
    public int VersionNumber { get; set; }
    public QuantitySurveyBoqVersionType VersionType { get; set; }

    [Required, StringLength(30)]
    public string Status { get; set; } = ProjectBoqVersionStatuses.Draft;

    [Required, StringLength(30)]
    public string ApprovalStatus { get; set; } = ProjectBoqVersionStatuses.Draft;

    public Guid? WorkflowInstanceId { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? PublishedAt { get; set; }

    [StringLength(2000)]
    public string? RejectionReason { get; set; }

    [Required, StringLength(2000)]
    public string ChangeSummary { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string AuditAction { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string SnapshotHash { get; set; } = string.Empty;

    public int LineCount { get; set; }
    public DateTime SnapshotAt { get; set; }

    [Required, StringLength(200)]
    public string ActorRoles { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    [ForeignKey(nameof(SourceVersionId))]
    public ProjectBoqVersion? SourceVersion { get; set; }

    public ICollection<ProjectBoqVersionLine> Lines { get; set; } = new List<ProjectBoqVersionLine>();
}

public sealed class ProjectBoqVersionLine : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid ProjectBoqVersionId { get; set; }
    public Guid LineKey { get; set; }
    public Guid? SourceBoqItemId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public Guid? ProjectWorkItemId { get; set; }

    [StringLength(50)] public string? PackageCode { get; set; }
    [StringLength(200)] public string? PackageName { get; set; }
    [StringLength(30)] public string? ActivityNodeType { get; set; }
    [StringLength(200)] public string? ActivityTitle { get; set; }
    [StringLength(50)] public string? SectionCode { get; set; }
    [StringLength(150)] public string? SectionName { get; set; }
    [StringLength(50)] public string? TradeCode { get; set; }
    [StringLength(150)] public string? TradeName { get; set; }
    [StringLength(50)] public string? CostCode { get; set; }
    [StringLength(150)] public string? CostCodeName { get; set; }
    [StringLength(30)] public string? MeasurementStandard { get; set; }
    [StringLength(50)] public string? MeasurementCode { get; set; }
    [StringLength(2000)] public string? MeasurementRule { get; set; }
    [StringLength(50)] public string? LineNumber { get; set; }
    [StringLength(50)] public string? ItemCode { get; set; }

    [Required, StringLength(50)]
    public string ItemType { get; set; } = ProjectBoqItemTypes.Item;

    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    [StringLength(20)] public string? UnitOfMeasure { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal? UnitRate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? LineAmount { get; set; }
    [StringLength(10)] public string Currency { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    [ForeignKey(nameof(ProjectBoqVersionId))]
    public ProjectBoqVersion Version { get; set; } = null!;
}
