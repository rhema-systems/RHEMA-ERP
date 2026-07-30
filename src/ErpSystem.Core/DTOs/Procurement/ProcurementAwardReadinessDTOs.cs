using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class EvaluateProcurementAwardReadinessRequest
{
    [Required, StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public List<Guid> ExpectedRecommendedSubjectIds { get; set; } = new();
    public List<Guid> ExpectedBusinessPartnerIds { get; set; } = new();

    [StringLength(64)]
    public string? ExpectedSourceIntegrityHash { get; set; }
}

public sealed class ProcurementAwardReadinessDto
{
    public Guid Id { get; set; }
    public int DecisionSequence { get; set; }
    public ProcurementAwardReadinessSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public ProcurementMethodType Method { get; set; }
    public ProcurementAwardReadinessDecisionStatus Status { get; set; }
    public bool IsReady => Status == ProcurementAwardReadinessDecisionStatus.Ready;
    public bool IsCurrent { get; set; }
    public string SourceIntegrityHash { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; }
    public Guid EvaluatedByUserId { get; set; }
    public string EvaluatedByName { get; set; } = string.Empty;
    public ProcurementAwardReadinessRecommendationDto Recommendation { get; set; } = new();
    public List<ProcurementAwardReadinessEvaluationDto> Evaluations { get; set; } = new();
    public List<ProcurementAwardReadinessSupplierDto> Suppliers { get; set; } = new();
    public List<ProcurementAwardReadinessVerificationDto> Verifications { get; set; } = new();
    public ProcurementAwardReadinessAuthorityDto Authority { get; set; } = new();
    public List<ProcurementAwardReadinessEvidenceDto> Evidence { get; set; } = new();
    public List<ProcurementAwardReadinessPrerequisiteGroupDto> PrerequisiteGroups { get; set; } = new();
    public List<ProcurementAwardReadinessTimelineEntryDto> Timeline { get; set; } = new();
    public List<string> BlockedReasons { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
}

public sealed class ProcurementEvaluatorAwardApproverSodStatusDto
{
    public ProcurementAwardReadinessSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public bool Allowed { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CurrentActorUserId { get; set; }
    public string CurrentActorName { get; set; } = string.Empty;
    public List<string> CurrentActorRoles { get; set; } = new();
    public List<Guid> EvaluatorUserIds { get; set; } = new();
    public List<Guid> IndependentApprovalActorUserIds { get; set; } = new();
    public List<ProcurementAwardEvaluatorLineageDto> EvaluatorLineage { get; set; } = new();
    public Guid? ReadinessDecisionId { get; set; }
    public int? ReadinessDecisionSequence { get; set; }
    public string? ReadinessSourceIntegrityHash { get; set; }
    public string? ReadinessIntegrityHash { get; set; }
    public bool? ReadinessDecisionIsCurrent { get; set; }
    public Guid SodDecisionId { get; set; }
    public string SodControlCode { get; set; } = string.Empty;
    public Guid? SodPolicySetId { get; set; }
    public string? SodPolicyCode { get; set; }
    public int? SodPolicyVersion { get; set; }
    public Guid? SodRuleId { get; set; }
    public string? SodRuleCode { get; set; }
    public string? SodSourceDecisionKey { get; set; }
    public Guid? SourceMethodRuleId { get; set; }
    public string? SourceMethodRuleCode { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; }
}

public sealed class ProcurementAwardEvaluatorLineageDto
{
    public string Family { get; set; } = string.Empty;
    public Guid? EvaluationId { get; set; }
    public Guid? CommitteeControlId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? ScoreSheetId { get; set; }
    public string? ScoreSubjectType { get; set; }
    public Guid? ScoreSubjectId { get; set; }
    public ProcurementEvaluationPhase? Phase { get; set; }
    public int? Attempt { get; set; }
    public ProcurementEvaluationScoreSheetStatus? ScoreStatus { get; set; }
    public bool IsRetainedAttempt { get; set; }
    public bool IsRecalledAttempt { get; set; }
    public Guid EvaluatorUserId { get; set; }
    public DateTime? EvaluatedAtUtc { get; set; }
    public string? IntegrityHash { get; set; }
}

public sealed class ProcurementAwardReadinessRecommendationDto
{
    public string SubjectType { get; set; } = string.Empty;
    public List<Guid> SubjectIds { get; set; } = new();
    public List<Guid> BusinessPartnerIds { get; set; } = new();
    public string? Reason { get; set; }
    public string? EvidenceReference { get; set; }
    public DateTime? RecommendedAtUtc { get; set; }
    public Guid? RecommendedByUserId { get; set; }
}

public sealed class ProcurementAwardReadinessEvaluationDto
{
    public string EvaluationType { get; set; } = string.Empty;
    public Guid EvaluationId { get; set; }
    public ProcurementEvaluationPhase Phase { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAtUtc { get; set; }
    public string? EvidenceReference { get; set; }
    public string? IntegrityHash { get; set; }
    public List<ProcurementAwardReadinessScoreAttemptDto> ScoreAttempts { get; set; } = new();
}

public sealed class ProcurementAwardReadinessScoreAttemptDto
{
    public Guid ScoreSheetId { get; set; }
    public Guid CommitteeControlId { get; set; }
    public Guid MeetingId { get; set; }
    public Guid AppointmentId { get; set; }
    public ProcurementEvaluationPhase Phase { get; set; }
    public string ScoreSubjectType { get; set; } = string.Empty;
    public Guid ScoreSubjectId { get; set; }
    public int Attempt { get; set; }
    public ProcurementEvaluationScoreSheetStatus Status { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public List<Guid> RecallIds { get; set; } = new();
}

public sealed class ProcurementAwardReadinessSupplierDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public bool IsEligible { get; set; }
    public string ValidationCode { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<ProcurementAwardReadinessPrequalificationDto> Prequalification { get; set; } = new();
}

public sealed class ProcurementAwardReadinessPrequalificationDto
{
    public Guid EntryId { get; set; }
    public Guid ExerciseId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid CategoryId { get; set; }
    public ProcurementQualifiedListEntryStatus Status { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string ApprovalReference { get; set; } = string.Empty;
    public string ApprovalEvidenceReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementAwardReadinessVerificationDto
{
    public Guid VerificationId { get; set; }
    public Guid VerificationBidderId { get; set; }
    public Guid TenderBidId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public Guid? TemplateId { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public string BidderStatus { get; set; } = string.Empty;
    public DateTime? CompletedAtUtc { get; set; }
    public List<Guid> ItemResultIds { get; set; } = new();
    public List<Guid> DocumentIds { get; set; } = new();
    public string SnapshotIntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementAwardReadinessAuthorityDto
{
    public Guid? MethodRuleId { get; set; }
    public string? MethodRuleCode { get; set; }
    public Guid? AuthorityRouteId { get; set; }
    public string? AuthorityRouteReference { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? WorkflowStatus { get; set; }
    public string? ApprovalReference { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public List<Guid> ApprovalActorUserIds { get; set; } = new();
}

public sealed class ProcurementAwardReadinessEvidenceDto
{
    public string RequirementKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public string? Reference { get; set; }
    public bool IsAvailable { get; set; }
}

public sealed class ProcurementAwardReadinessPrerequisiteGroupDto
{
    public ProcurementAwardReadinessPrerequisiteGroup Group { get; set; }
    public ProcurementAwardReadinessPrerequisiteStatus Status { get; set; }
    public List<ProcurementAwardReadinessPrerequisiteItemDto> Items { get; set; } = new();
}

public sealed class ProcurementAwardReadinessPrerequisiteItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public ProcurementAwardReadinessPrerequisiteStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Remediation { get; set; }
    public string? LineageType { get; set; }
    public Guid? LineageId { get; set; }
    public string? LineageHash { get; set; }
}

public sealed class ProcurementAwardReadinessTimelineEntryDto
{
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? Reference { get; set; }
    public string? IntegrityHash { get; set; }
}
