using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class ProjectBoqVersionWorkspaceDto
{
    public Guid ProjectId { get; set; }
    public string WorkingSetHash { get; set; } = string.Empty;
    public int WorkingLineCount { get; set; }
    public bool WorkflowRequired { get; set; }
    public bool ApprovedVersionsImmutable { get; set; }
    public Guid? CurrentPublishedVersionId { get; set; }
    public IReadOnlyList<QuantitySurveyBoqVersionType> AllowedVersionTypes { get; set; } = [];
    public IReadOnlyList<ProjectBoqVersionSummaryDto> Versions { get; set; } = [];
}

public class ProjectBoqVersionSummaryDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SourceVersionId { get; set; }
    public int VersionNumber { get; set; }
    public QuantitySurveyBoqVersionType VersionType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsPublished { get; set; }
    public bool CanCurrentUserApprove { get; set; }
    public string ChangeSummary { get; set; } = string.Empty;
    public string SnapshotHash { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public DateTime SnapshotAt { get; set; }
    public string? CreatedBy { get; set; }
    public Guid? CreatedById { get; set; }
}

public sealed class CreateProjectBoqVersionDto
{
    public QuantitySurveyBoqVersionType VersionType { get; set; }
    public Guid? SourceVersionId { get; set; }

    [Required, StringLength(64)]
    public string ExpectedWorkingSetHash { get; set; } = string.Empty;

    [Required, StringLength(2000), MinLength(5)]
    public string ChangeSummary { get; set; } = string.Empty;
}

public sealed class ProjectBoqWorkflowActionDto
{
    [StringLength(2000)]
    public string? Comments { get; set; }
}

public sealed class RejectProjectBoqVersionDto
{
    [Required, StringLength(2000), MinLength(5)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class RecallProjectBoqVersionDto
{
    [Required, StringLength(2000), MinLength(5)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ProjectBoqVersionDetailDto : ProjectBoqVersionSummaryDto
{
    public IReadOnlyList<ProjectBoqVersionLineDto> Lines { get; set; } = [];
}

public sealed class ProjectBoqVersionLineDto
{
    public Guid Id { get; set; }
    public Guid LineKey { get; set; }
    public Guid? SourceBoqItemId { get; set; }
    public Guid? ProjectPackageId { get; set; }
    public string? PackageCode { get; set; }
    public string? PackageName { get; set; }
    public string? SectionCode { get; set; }
    public string? SectionName { get; set; }
    public string? TradeCode { get; set; }
    public string? TradeName { get; set; }
    public string? CostCode { get; set; }
    public string? CostCodeName { get; set; }
    public string? MeasurementStandard { get; set; }
    public string? MeasurementCode { get; set; }
    public string? MeasurementRule { get; set; }
    public string? LineNumber { get; set; }
    public string? ItemCode { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal? UnitRate { get; set; }
    public decimal? LineAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class ProjectBoqVersionComparisonDto
{
    public ProjectBoqVersionSummaryDto Baseline { get; set; } = new();
    public ProjectBoqVersionSummaryDto Comparison { get; set; } = new();
    public int AddedLineCount { get; set; }
    public int RemovedLineCount { get; set; }
    public int ChangedLineCount { get; set; }
    public int UnchangedLineCount { get; set; }
    public IReadOnlyList<ProjectBoqVersionCurrencyDeltaDto> CurrencyTotals { get; set; } = [];
    public IReadOnlyList<ProjectBoqVersionLineComparisonDto> Lines { get; set; } = [];
}

public sealed class ProjectBoqVersionCurrencyDeltaDto
{
    public string Currency { get; set; } = string.Empty;
    public decimal BaselineAmount { get; set; }
    public decimal ComparisonAmount { get; set; }
    public decimal DeltaAmount { get; set; }
}

public sealed class ProjectBoqVersionLineComparisonDto
{
    public Guid LineKey { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public ProjectBoqVersionLineDto? BaselineLine { get; set; }
    public ProjectBoqVersionLineDto? ComparisonLine { get; set; }
    public decimal QuantityDelta { get; set; }
    public decimal? UnitRateDelta { get; set; }
    public decimal? AmountDelta { get; set; }
    public IReadOnlyList<string> ChangedFields { get; set; } = [];
}
