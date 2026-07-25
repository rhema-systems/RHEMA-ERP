using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public class ProcurementPrequalificationSummaryDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ProcurementPrequalificationStatus Status { get; set; }
    public DateTime OpensAtUtc { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public int ApplicationCount { get; set; }
    public int QualifiedCount { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class ProcurementPrequalificationExerciseDto : ProcurementPrequalificationSummaryDto
{
    public string Description { get; set; } = string.Empty;
    public int ValidityMonths { get; set; }
    public decimal PassingScore { get; set; }
    public Guid PolicySetId { get; set; }
    public string PolicySetCode { get; set; } = string.Empty;
    public int PolicySetVersion { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? AdvertisementReference { get; set; }
    public string? AdvertisementEvidenceReference { get; set; }
    public DateTime? AdvertisedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime? SubmittedForApprovalAtUtc { get; set; }
    public string? DecisionReference { get; set; }
    public string? DecisionEvidenceReference { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementPrequalificationCategoryDto> Categories { get; set; } = new();
    public List<ProcurementPrequalificationCriterionDto> Criteria { get; set; } = new();
    public List<ProcurementPrequalificationApplicationDto> Applications { get; set; } = new();
    public List<ProcurementQualifiedListEntryDto> QualifiedEntries { get; set; } = new();
    public List<ProcurementPrequalificationMilestoneDto> Milestones { get; set; } = new();
}

public sealed class ProcurementPrequalificationCategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProcurementPrequalificationCriterionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Weight { get; set; }
    public decimal MinimumScore { get; set; }
    public bool IsMandatory { get; set; }
    public bool RequiresEvidence { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ProcurementPrequalificationApplicationDto
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public ProcurementPrequalificationApplicationStatus Status { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? EvaluatedAtUtc { get; set; }
    public decimal? TotalScore { get; set; }
    public bool? Passed { get; set; }
    public string? EvaluationRemarks { get; set; }
    public string? RecommendationEvidenceReference { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<Guid> CategoryIds { get; set; } = new();
    public List<ProcurementPrequalificationEvidenceDto> Evidence { get; set; } = new();
    public List<ProcurementPrequalificationScoreDto> Scores { get; set; } = new();
}

public sealed class ProcurementPrequalificationEvidenceDto
{
    public string CriterionCode { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string VerificationReference { get; set; } = string.Empty;
}

public sealed class ProcurementPrequalificationScoreDto
{
    public Guid CriterionId { get; set; }
    public string CriterionCode { get; set; } = string.Empty;
    public string CriterionName { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public bool MeetsRequirement { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceReference { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }
    public Guid EvaluatedById { get; set; }
}

public sealed class ProcurementQualifiedListEntryDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime ValidFromUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public ProcurementQualifiedListEntryStatus Status { get; set; }
    public string ApprovalReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementPrequalificationMilestoneDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime? CompletedAtUtc { get; set; }
    public string? Reference { get; set; }
}

public sealed class ProcurementPrequalificationReadinessDto
{
    public List<ProcurementPrequalificationCategoryDto> Categories { get; set; } = new();
    public List<ProcurementPrequalificationPolicyDto> Policies { get; set; } = new();
    public List<ProcurementPrequalificationWorkflowDto> Workflows { get; set; } = new();
    public List<ProcurementPrequalificationSupplierDto> Suppliers { get; set; } = new();
}

public sealed class ProcurementPrequalificationPolicyDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public Guid SourceConfigurationProfileId { get; set; }
}

public sealed class ProcurementPrequalificationWorkflowDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
}

public sealed class ProcurementPrequalificationSupplierDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierEligibilityDto
{
    public Guid BusinessPartnerId { get; set; }
    public Guid CategoryId { get; set; }
    public DateTime EvaluatedAtUtc { get; set; }
    public bool Eligible { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ProcurementQualifiedListEntryDto? Entry { get; set; }
}

public sealed class CreateProcurementPrequalificationExerciseRequest
{
    [Required, StringLength(100)] public string Reference { get; set; } = string.Empty;
    [Required, StringLength(300)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000), MinLength(20)] public string Description { get; set; } = string.Empty;
    [MinLength(1)] public List<Guid> CategoryIds { get; set; } = new();
    public DateTime OpensAtUtc { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    [Range(1, 60)] public int ValidityMonths { get; set; } = 12;
    [Range(1, 100)] public decimal PassingScore { get; set; } = 70;
    public Guid PolicySetId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    [MinLength(1)] public List<CreateProcurementPrequalificationCriterionRequest> Criteria { get; set; } = new();
}

public sealed class CreateProcurementPrequalificationCriterionRequest
{
    [Required, StringLength(100)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(300)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Range(typeof(decimal), "0.01", "100")] public decimal Weight { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal MinimumScore { get; set; }
    public bool IsMandatory { get; set; }
    public bool RequiresEvidence { get; set; }
    public int SortOrder { get; set; }
}

public sealed class AdvertiseProcurementPrequalificationRequest
{
    [Required, StringLength(300)] public string AdvertisementReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string AdvertisementEvidenceReference { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class SubmitProcurementPrequalificationApplicationRequest
{
    public Guid BusinessPartnerId { get; set; }
    [MinLength(1)] public List<Guid> CategoryIds { get; set; } = new();
    [MinLength(1)] public List<ProcurementPrequalificationEvidenceRequest> Evidence { get; set; } = new();
}

public sealed class ProcurementPrequalificationEvidenceRequest
{
    [Required, StringLength(100)] public string CriterionCode { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string VerificationReference { get; set; } = string.Empty;
}

public sealed class CloseProcurementPrequalificationRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class EvaluateProcurementPrequalificationApplicationRequest
{
    [Required, StringLength(2000)] public string Remarks { get; set; } = string.Empty;
    [Required, StringLength(500)] public string RecommendationEvidenceReference { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementPrequalificationCriterionScoreRequest> Scores { get; set; } = new();
}

public sealed class ProcurementPrequalificationCriterionScoreRequest
{
    public Guid CriterionId { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal Score { get; set; }
    public bool MeetsRequirement { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [StringLength(500)] public string? EvidenceReference { get; set; }
}

public sealed class SubmitProcurementPrequalificationDecisionRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class DecideProcurementPrequalificationRequest
{
    [Required] public string Action { get; set; } = string.Empty;
    [Required, StringLength(300)] public string DecisionReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string DecisionEvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ExpireProcurementPrequalificationRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}
