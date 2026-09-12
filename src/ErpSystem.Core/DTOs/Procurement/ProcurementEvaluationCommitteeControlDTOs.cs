using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementEvaluationCommitteeReadinessDto
{
    public ProcurementEvaluationSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public bool SourceExists { get; set; }
    public bool HasControl { get; set; }
    public Guid? CommitteeControlId { get; set; }
    public ProcurementEvaluationCommitteeControlStatus? Status { get; set; }
    public bool CompositionReady { get; set; }
    public bool AppointmentsReady { get; set; }
    public bool DeclarationsReady { get; set; }
    public bool QuorumMet { get; set; }
    public int RequiredQuorum { get; set; }
    public int EligibleVotingMemberCount { get; set; }
    public int SignedVotingAttendanceCount { get; set; }
    public List<string> BlockedReasons { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
}

public sealed class ProcurementEvaluationCommitteeOptionsDto
{
    public List<ProcurementEvaluationCommitteeTemplateOptionDto> Committees { get; set; } = new();
    public List<ProcurementEvaluationWorkflowOptionDto> Workflows { get; set; } = new();
    public List<ProcurementEvaluationUserOptionDto> Users { get; set; } = new();
}

public sealed class ProcurementEvaluationCommitteeTemplateOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int RequiredQuorum { get; set; }
    public bool CompositionReady { get; set; }
    public int ActiveMemberCount { get; set; }
    public List<string> Issues { get; set; } = new();
}

public sealed class ProcurementEvaluationWorkflowOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
}

public sealed class ProcurementEvaluationUserOptionDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ProcurementEvaluationCommitteeDto
{
    public Guid Id { get; set; }
    public ProcurementEvaluationSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public int Version { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public ProcurementEvaluationCommitteeControlStatus Status { get; set; }
    public Guid CommitteeTemplateId { get; set; }
    public string CommitteeCode { get; set; } = string.Empty;
    public string CommitteeName { get; set; } = string.Empty;
    public int RequiredQuorum { get; set; }
    public bool CompositionReady { get; set; }
    public bool QuorumMet { get; set; }
    public Guid PolicySetId { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public int PolicyVersion { get; set; }
    public Guid? ConfigurationProfileId { get; set; }
    public string? ConfigurationProfileCode { get; set; }
    public int? ConfigurationProfileVersion { get; set; }
    public Guid MethodRuleId { get; set; }
    public string MethodRuleCode { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public Guid? ActivatedByUserId { get; set; }
    public string? ActivationEvidenceReference { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public Guid? RetiredByUserId { get; set; }
    public string? RetirementReason { get; set; }
    public string? RetirementEvidenceReference { get; set; }
    public string CompositionIntegrityHash { get; set; } = string.Empty;
    public List<ProcurementEvaluationRoleRequirementDto> RequiredRoles { get; set; } = new();
    public List<ProcurementEvaluationAppointmentDto> Members { get; set; } = new();
    public List<ProcurementEvaluationMeetingDto> Meetings { get; set; } = new();
    public List<ProcurementEvaluationScoreSheetDto> ScoreSheets { get; set; } = new();
    public List<ProcurementEvaluationScoreRecallDto> Recalls { get; set; } = new();
    public List<ProcurementEvaluationTimelineEntryDto> Timeline { get; set; } = new();
    public List<string> AllowedActions { get; set; } = new();
    public List<string> BlockedReasons { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementEvaluationRoleRequirementDto
{
    public Guid Id { get; set; }
    public ProcurementCommitteeMemberKind MemberKind { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int MinimumCount { get; set; }
    public bool IsVoting { get; set; }
    public bool IsRequiredForQuorum { get; set; }
    public int MatchedCount { get; set; }
    public bool IsMet { get; set; }
}

public sealed class ProcurementEvaluationAppointmentDto
{
    public Guid Id { get; set; }
    public Guid CommitteeMemberId { get; set; }
    public Guid ResponsibilityAssignmentId { get; set; }
    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public ProcurementCommitteeMemberKind MemberKind { get; set; }
    public bool IsVoting { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public ProcurementEvaluationAppointmentStatus Status { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public string? AcceptanceSignatureReference { get; set; }
    public string? AcceptanceEvidenceReference { get; set; }
    public ProcurementEvaluationConflictDeclarationDto? CurrentDeclaration { get; set; }
    public bool EligibleToScore { get; set; }
    public List<string> BlockedReasons { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementEvaluationConflictDeclarationDto
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public ProcurementEvaluationConflictOutcome Outcome { get; set; }
    public string Declaration { get; set; } = string.Empty;
    public string? ConflictDetails { get; set; }
    public string SignatureReference { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public DateTime ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }
    public DateTime DeclaredAtUtc { get; set; }
    public Guid DeclaredByUserId { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementEvaluationMeetingDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public ProcurementEvaluationPhase Phase { get; set; }
    public ProcurementEvaluationMeetingStatus Status { get; set; }
    public string MeetingMode { get; set; } = string.Empty;
    public string MeetingChannel { get; set; } = string.Empty;
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public int EligibleVotingMemberCount { get; set; }
    public int SignedVotingAttendanceCount { get; set; }
    public bool ChairPresent { get; set; }
    public bool SecretaryPresent { get; set; }
    public bool QuorumMet { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public string? RemoteMeetingEvidenceReference { get; set; }
    public string QuorumIntegrityHash { get; set; } = string.Empty;
    public List<ProcurementEvaluationAttendanceDto> Attendance { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementEvaluationAttendanceDto
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public ProcurementCommitteeMemberKind MemberKind { get; set; }
    public bool IsVoting { get; set; }
    public bool IsPresent { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public string? SignatureReference { get; set; }
    public string? EvidenceReference { get; set; }
    public bool WasEligibleAtSignature { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementEvaluationScoreSheetDto
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public ProcurementEvaluationPhase Phase { get; set; }
    public string ScoreSubjectType { get; set; } = string.Empty;
    public Guid ScoreSubjectId { get; set; }
    public int Attempt { get; set; }
    public ProcurementEvaluationScoreSheetStatus Status { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public string ScoreSnapshotJson { get; set; } = string.Empty;
    public string SignatureReference { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementEvaluationScoreRecallDto
{
    public Guid Id { get; set; }
    public Guid ScoreSheetId { get; set; }
    public ProcurementEvaluationScoreRecallStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionReference { get; set; }
    public string? DecisionEvidenceReference { get; set; }
    public int? AuthorizedNewAttempt { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementEvaluationTimelineEntryDto
{
    public DateTime OccurredAtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? Reference { get; set; }
}

public sealed class BindProcurementEvaluationCommitteeRequest
{
    public ProcurementEvaluationSourceType SourceType { get; set; }
    [Required] public Guid SourceId { get; set; }
    [Required] public Guid CommitteeTemplateId { get; set; }
    [Required, StringLength(1000)] public string Purpose { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public List<SaveProcurementEvaluationRoleRequirementRequest> RequiredRoles { get; set; } = new();
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class SaveProcurementEvaluationRoleRequirementRequest
{
    public ProcurementCommitteeMemberKind MemberKind { get; set; }
    [Required, StringLength(100)] public string RoleName { get; set; } = string.Empty;
    [Range(1, 50)] public int MinimumCount { get; set; } = 1;
    public bool IsVoting { get; set; }
    public bool IsRequiredForQuorum { get; set; }
}

public sealed class ActivateProcurementEvaluationCommitteeRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(500)] public string? EvidenceReference { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class RetireProcurementEvaluationCommitteeDraftRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;
    [StringLength(500)] public string? EvidenceReference { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class RespondProcurementEvaluationAppointmentRequest
{
    public bool Accept { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(500)] public string? SignatureReference { get; set; }
    [StringLength(500)] public string? EvidenceReference { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class SubmitProcurementEvaluationConflictDeclarationRequest
{
    public ProcurementEvaluationConflictOutcome Outcome { get; set; }
    [Required, StringLength(2000)] public string Declaration { get; set; } = string.Empty;
    [StringLength(2000)] public string? ConflictDetails { get; set; }
    [StringLength(500)] public string? SignatureReference { get; set; }
    [StringLength(500)] public string? EvidenceReference { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }
    [Required, StringLength(100)] public string AppointmentRowVersion { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class CreateProcurementEvaluationMeetingRequest
{
    public ProcurementEvaluationPhase Phase { get; set; }
    [Required, StringLength(200)] public string MeetingMode { get; set; } = string.Empty;
    [Required, StringLength(500)] public string MeetingChannel { get; set; } = string.Empty;
    public DateTime ScheduledAtUtc { get; set; }
    [StringLength(500)] public string? EvidenceReference { get; set; }
    [StringLength(500)] public string? RemoteMeetingEvidenceReference { get; set; }
    [Required, StringLength(100)] public string CommitteeRowVersion { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class SignProcurementEvaluationAttendanceRequest
{
    public bool IsPresent { get; set; } = true;
    [StringLength(500)] public string? SignatureReference { get; set; }
    [StringLength(500)] public string? EvidenceReference { get; set; }
    [Required, StringLength(100)] public string MeetingRowVersion { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AppointmentRowVersion { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class ConfirmProcurementEvaluationQuorumRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(500)] public string? EvidenceReference { get; set; }
    [StringLength(500)] public string? RemoteMeetingEvidenceReference { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class ProcurementEvaluationScorerEligibilityDto
{
    public bool Allowed { get; set; }
    public ProcurementEvaluationSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public ProcurementEvaluationPhase Phase { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid? CommitteeControlId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? MeetingId { get; set; }
    public int AuthorizedAttempt { get; set; } = 1;
    public List<string> BlockedReasons { get; set; } = new();
}

public sealed class LockProcurementEvaluationScoreSheetRequest
{
    public ProcurementEvaluationSourceType SourceType { get; set; }
    [Required] public Guid SourceId { get; set; }
    public ProcurementEvaluationPhase Phase { get; set; }
    [Required, StringLength(100)] public string ScoreSubjectType { get; set; } = string.Empty;
    [Required] public Guid ScoreSubjectId { get; set; }
    [Required] public Guid MeetingId { get; set; }
    [Required] public Guid AppointmentId { get; set; }
    [Required, StringLength(100)] public string CommitteeRowVersion { get; set; } = string.Empty;
    [Required, StringLength(100)] public string MeetingRowVersion { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AppointmentRowVersion { get; set; } = string.Empty;
    [Required] public string ScoreSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(500)] public string SignatureReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class RequestProcurementEvaluationScoreRecallRequest
{
    [Required] public string ScoreSheetRowVersion { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required] public Guid WorkflowDefinitionId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class DecideProcurementEvaluationScoreRecallRequest
{
    public bool Approve { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(500)] public string DecisionReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
}
