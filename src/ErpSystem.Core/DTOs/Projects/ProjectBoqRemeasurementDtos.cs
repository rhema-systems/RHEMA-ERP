using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class ProjectBoqRemeasurementMeasurementDto
{
    public Guid MeasurementSheetId { get; init; }
    public string SheetReference { get; init; } = string.Empty;
    public Guid ProjectBoqVersionLineId { get; init; }
    public Guid BoqLineKey { get; init; }
    public string BoqLineLabel { get; init; } = string.Empty;
    public string? UnitOfMeasure { get; init; }
    public decimal PreviousQuantity { get; init; }
    public decimal MeasuredQuantity { get; init; }
    public DateTime MeasurementDate { get; init; }
    public DateTime RecordedAt { get; init; }
    public string? RecordedByName { get; init; }
}

public sealed class ProjectBoqRemeasurementWorkspaceDto
{
    public Guid ProjectId { get; init; }
    public Guid SourceApprovedBoqVersionId { get; init; }
    public int SourceApprovedBoqVersionNumber { get; init; }
    public Guid? OpenCandidateVersionId { get; init; }
    public IReadOnlyList<ProjectBoqRemeasurementMeasurementDto> EligibleMeasurements { get; init; } = [];
}

public sealed class CreateProjectBoqRemeasurementDto
{
    public Guid ClientRequestId { get; init; }

    [Required, MinLength(1)]
    public IReadOnlyList<Guid> MeasurementSheetIds { get; init; } = [];

    [Required, StringLength(2000), MinLength(5)]
    public string ChangeSummary { get; init; } = string.Empty;
}
