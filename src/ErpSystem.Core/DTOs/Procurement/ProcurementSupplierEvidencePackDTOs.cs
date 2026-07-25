using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementSupplierEvidencePackSearchRequest
{
    public string? Search { get; set; }
    public ProcurementSupplierRegistrationCategory? Category { get; set; }
    public ProcurementSupplierEvidencePackStatus? Status { get; set; }
    public DateTime? EffectiveAtUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class ProcurementSupplierEvidencePackSummaryDto
{
    public int FamilyCount { get; set; }
    public int DraftCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int PublishedCount { get; set; }
    public int EffectiveCount { get; set; }
    public int RetiredCount { get; set; }
    public Dictionary<ProcurementSupplierRegistrationCategory, int> EffectiveByCategory { get; set; } = new();
}

public sealed class ProcurementSupplierEvidencePackPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementSupplierEvidencePackListItemDto> Items { get; set; } = new();
}

public class ProcurementSupplierEvidencePackListItemDto
{
    public Guid Id { get; set; }
    public Guid PackKey { get; set; }
    public string PackCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ProcurementSupplierRegistrationCategory Category { get; set; }
    public int Version { get; set; }
    public ProcurementSupplierEvidencePackStatus Status { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsEffective { get; set; }
    public int RequirementCount { get; set; }
    public int MandatoryRequirementCount { get; set; }
    public string SourceConfigurationProfileCode { get; set; } = string.Empty;
    public int SourceConfigurationProfileVersion { get; set; }
    public List<string> AllowedActions { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierEvidencePackDto : ProcurementSupplierEvidencePackListItemDto
{
    public string? Description { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SupersedesVersionId { get; set; }
    public string? ChangeSummary { get; set; }
    public string? ReviewComment { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? RetiredById { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementSupplierEvidenceRequirementDto> Requirements { get; set; } = new();
    public List<string> BlockedReasons { get; set; } = new();
}

public sealed class ProcurementSupplierEvidenceRequirementDto
{
    public Guid Id { get; set; }
    public string RequirementCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProcurementSupplierEvidenceRequirementKind Kind { get; set; }
    public string? DocumentType { get; set; }
    public bool IsMandatory { get; set; }
    public string? ClassificationScheme { get; set; }
    public List<string> AllowedClassifications { get; set; } = new();
    public ProcurementSupplierEvidenceValidityMode ValidityMode { get; set; }
    public int? MinimumRemainingDays { get; set; }
    public int ApprovalStepOrder { get; set; }
    public string ApprovalStepName { get; set; } = string.Empty;
    public long MaxFileSizeBytes { get; set; }
    public List<string> AllowedMimeTypes { get; set; } = new();
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class SaveProcurementSupplierEvidencePackRequest
{
    [Required, StringLength(50)]
    public string PackCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public ProcurementSupplierRegistrationCategory Category { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }

    [StringLength(1000)]
    public string? ChangeSummary { get; set; }

    public List<SaveProcurementSupplierEvidenceRequirementRequest> Requirements { get; set; } = new();
    public string? RowVersion { get; set; }
}

public sealed class SaveProcurementSupplierEvidenceRequirementRequest
{
    [Required, StringLength(50)]
    public string RequirementCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public ProcurementSupplierEvidenceRequirementKind Kind { get; set; }

    [StringLength(100)]
    public string? DocumentType { get; set; }

    public bool IsMandatory { get; set; } = true;

    [StringLength(100)]
    public string? ClassificationScheme { get; set; }

    public List<string> AllowedClassifications { get; set; } = new();
    public ProcurementSupplierEvidenceValidityMode ValidityMode { get; set; }
    public int? MinimumRemainingDays { get; set; }
    public int ApprovalStepOrder { get; set; }

    [Required, StringLength(100)]
    public string ApprovalStepName { get; set; } = string.Empty;

    [Range(1, 100 * 1024 * 1024)]
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    public List<string> AllowedMimeTypes { get; set; } = new();
}

public sealed class ProcurementSupplierEvidencePackLifecycleRequest
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Comment { get; set; }

    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class CloneProcurementSupplierEvidencePackRequest
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string ChangeSummary { get; set; } = string.Empty;

    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
}

public sealed class ProcurementSupplierEvidencePackOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public List<ProcurementSupplierEvidencePackWorkflowStepOptionDto> Steps { get; set; } = new();
}

public sealed class ProcurementSupplierEvidencePackWorkflowStepOptionDto
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierEvidenceReadinessDto
{
    public Guid RegistrationId { get; set; }
    public ProcurementSupplierRegistrationCategory? Category { get; set; }
    public Guid? PackVersionId { get; set; }
    public string? PackCode { get; set; }
    public int? PackVersion { get; set; }
    public bool IsBound { get; set; }
    public bool IsReady { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }
    public List<ProcurementSupplierEvidenceRequirementReadinessDto> Requirements { get; set; } = new();
    public List<string> BlockingReasons { get; set; } = new();
}

public sealed class ProcurementSupplierEvidenceRequirementReadinessDto
{
    public string RequirementCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public bool IsSatisfied { get; set; }
    public ProcurementSupplierEvidenceRequirementKind Kind { get; set; }
    public string? DocumentType { get; set; }
    public string? ClassificationScheme { get; set; }
    public List<string> AllowedClassifications { get; set; } = new();
    public ProcurementSupplierEvidenceValidityMode ValidityMode { get; set; }
    public int? MinimumRemainingDays { get; set; }
    public long MaxFileSizeBytes { get; set; }
    public List<string> AllowedMimeTypes { get; set; } = new();
    public string ApprovalStepName { get; set; } = string.Empty;
    public int ApprovalStepOrder { get; set; }
    public string? MatchedDocumentName { get; set; }
    public string? ClassificationCode { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public List<string> Issues { get; set; } = new();
}
