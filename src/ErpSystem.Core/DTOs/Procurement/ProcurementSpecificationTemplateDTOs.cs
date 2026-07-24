using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementSpecificationTemplateSummaryDto
{
    public int TemplateFamilyCount { get; init; }
    public int DraftCount { get; init; }
    public int PendingApprovalCount { get; init; }
    public int PublishedCount { get; init; }
    public int EffectiveCount { get; init; }
    public Dictionary<string, int> ByKind { get; init; } = new();
}

public sealed class ProcurementSpecificationTemplateSearchRequest
{
    public string? Search { get; set; }
    public ProcurementSpecificationTemplateKind? Kind { get; set; }
    public ProcurementSpecificationTemplateStatus? Status { get; set; }
    public bool EffectiveOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class ProcurementSpecificationTemplatePageDto
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public List<ProcurementSpecificationTemplateListItemDto> Items { get; init; } = new();
}

public class ProcurementSpecificationTemplateListItemDto
{
    public Guid Id { get; init; }
    public Guid TemplateKey { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ProcurementSpecificationTemplateKind Kind { get; init; }
    public int Version { get; init; }
    public ProcurementSpecificationTemplateStatus Status { get; init; }
    public bool IsDefault { get; init; }
    public DateTime EffectiveFromUtc { get; init; }
    public DateTime? EffectiveToUtc { get; init; }
    public bool IsEffective { get; init; }
    public bool IsPublicationReady { get; init; }
    public string? WorkflowDefinitionName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProcurementSpecificationTemplateDto : ProcurementSpecificationTemplateListItemDto
{
    public string? Description { get; init; }
    public string? ChangeSummary { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public string FunctionalAndPerformanceRequirements { get; init; } = string.Empty;
    public string ProcessAndMaterialsRequirements { get; init; } = string.Empty;
    public string DimensionsAndMarkingRequirements { get; init; } = string.Empty;
    public string TestingAndInspectionRequirements { get; init; } = string.Empty;
    public string ApplicableStandards { get; init; } = string.Empty;
    public string Deliverables { get; init; } = string.Empty;
    public string AcceptanceCriteria { get; init; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; init; }
    public int? WorkflowDefinitionVersion { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public Guid? SupersedesTemplateId { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
    public Guid? SubmittedById { get; init; }
    public string? SubmittedByName { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public Guid? PublishedById { get; init; }
    public string? PublishedByName { get; init; }
    public DateTime? RetiredAtUtc { get; init; }
    public Guid? RetiredById { get; init; }
    public string? RetiredByName { get; init; }
    public string? ReviewComment { get; init; }
    public int RevisionNumber { get; init; }
    public List<ProcurementSpecificationTemplateValidationIssueDto> ValidationIssues { get; init; } = new();
    public List<ProcurementSpecificationTemplateTimelineEventDto> Timeline { get; init; } = new();
}

public sealed class SaveProcurementSpecificationTemplateRequest
{
    [Required, StringLength(50)] public string TemplateCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public ProcurementSpecificationTemplateKind Kind { get; set; }
    public bool IsDefault { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string FunctionalAndPerformanceRequirements { get; set; } = string.Empty;
    public string ProcessAndMaterialsRequirements { get; set; } = string.Empty;
    public string DimensionsAndMarkingRequirements { get; set; } = string.Empty;
    public string TestingAndInspectionRequirements { get; set; } = string.Empty;
    public string ApplicableStandards { get; set; } = string.Empty;
    public string Deliverables { get; set; } = string.Empty;
    public string AcceptanceCriteria { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class ProcurementSpecificationTemplateLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Comment { get; set; }
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class CloneProcurementSpecificationTemplateRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    [Required, StringLength(1000)] public string ChangeSummary { get; set; } = string.Empty;
}

public sealed class ProcurementSpecificationTemplateValidationDto
{
    public Guid TemplateId { get; init; }
    public bool IsValid { get; init; }
    public List<ProcurementSpecificationTemplateValidationIssueDto> Issues { get; init; } = new();
}

public sealed class ProcurementSpecificationTemplateValidationIssueDto
{
    public string Code { get; init; } = string.Empty;
    public string Field { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class ProcurementSpecificationWorkflowOptionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Version { get; init; }
    public string EntityTypeName { get; init; } = string.Empty;
}

public sealed class ProcurementSpecificationTemplateTimelineEventDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public ProcurementControlEventResult Result { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
    public List<ProcurementControlEventEvidenceDto> Evidence { get; init; } = new();
}
