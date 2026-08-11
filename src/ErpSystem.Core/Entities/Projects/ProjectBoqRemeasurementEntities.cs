using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.Entities.Projects;

/// <summary>
/// Immutable measurement-to-BoQ lineage for a Remeasurement candidate. The
/// ProjectBoqVersion remains the workflow and publication owner.
/// </summary>
public sealed class ProjectBoqRemeasurementRevision : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid ProjectBoqVersionId { get; set; }
    public Guid SourceApprovedBoqVersionId { get; set; }
    public Guid ClientRequestId { get; set; }

    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    [Required, StringLength(64)] public string MeasurementSetHash { get; set; } = string.Empty;
    public int SelectedMeasurementCount { get; set; }
    public int ChangedLineCount { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal TotalAbsoluteQuantityDelta { get; set; }
    public bool IsFinalized { get; set; }

    [ForeignKey(nameof(ProjectBoqVersionId))] public ProjectBoqVersion Version { get; set; } = null!;
    [ForeignKey(nameof(SourceApprovedBoqVersionId))] public ProjectBoqVersion SourceApprovedVersion { get; set; } = null!;
    public ICollection<ProjectBoqRemeasurementLine> Lines { get; set; } = new List<ProjectBoqRemeasurementLine>();
}

public sealed class ProjectBoqRemeasurementLine : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid RemeasurementRevisionId { get; set; }
    public Guid ProjectBoqVersionId { get; set; }
    public Guid ProjectBoqVersionLineId { get; set; }
    public Guid SourceApprovedBoqVersionLineId { get; set; }
    public Guid BoqLineKey { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal PreviousQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal RevisedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal QuantityDelta { get; set; }

    [ForeignKey(nameof(RemeasurementRevisionId))] public ProjectBoqRemeasurementRevision Revision { get; set; } = null!;
    [ForeignKey(nameof(ProjectBoqVersionLineId))] public ProjectBoqVersionLine VersionLine { get; set; } = null!;
    [ForeignKey(nameof(SourceApprovedBoqVersionLineId))] public ProjectBoqVersionLine SourceApprovedLine { get; set; } = null!;
    public ICollection<ProjectBoqRemeasurementSource> Sources { get; set; } = new List<ProjectBoqRemeasurementSource>();
}

public sealed class ProjectBoqRemeasurementSource : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Guid RemeasurementRevisionId { get; set; }
    public Guid RemeasurementLineId { get; set; }
    public Guid MeasurementSheetId { get; set; }
    [Required, StringLength(50)] public string MeasurementReferenceSnapshot { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal MeasuredQuantitySnapshot { get; set; }
    public DateTime RecordedAtSnapshot { get; set; }
    [Required, StringLength(64)] public string MeasurementRequestHashSnapshot { get; set; } = string.Empty;

    [ForeignKey(nameof(RemeasurementRevisionId))] public ProjectBoqRemeasurementRevision Revision { get; set; } = null!;
    [ForeignKey(nameof(RemeasurementLineId))] public ProjectBoqRemeasurementLine Line { get; set; } = null!;
    [ForeignKey(nameof(MeasurementSheetId))] public QuantitySurveyMeasurementSheet MeasurementSheet { get; set; } = null!;
}
