using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementEvaluationCommitteeControls")]
public sealed class ProcurementEvaluationCommitteeControl : TenantEntity
{
    public ProcurementEvaluationSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    [Range(1, int.MaxValue)] public int Version { get; set; } = 1;
    [Required, StringLength(100)] public string SourceReference { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Purpose { get; set; } = string.Empty;
    public ProcurementEvaluationCommitteeControlStatus Status { get; set; } =
        ProcurementEvaluationCommitteeControlStatus.Draft;

    public Guid CommitteeTemplateId { get; set; }
    [Required, StringLength(50)] public string CommitteeCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string CommitteeName { get; set; } = string.Empty;
    [Range(1, 50)] public int RequiredQuorum { get; set; }

    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string PolicyCode { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int PolicyVersion { get; set; }
    public Guid? ConfigurationProfileId { get; set; }
    [StringLength(50)] public string? ConfigurationProfileCode { get; set; }
    public int? ConfigurationProfileVersion { get; set; }
    public Guid MethodRuleId { get; set; }
    [Required, StringLength(100)] public string MethodRuleCode { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }

    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public Guid? ActivatedByUserId { get; set; }
    [StringLength(500)] public string? ActivationEvidenceReference { get; set; }
    [StringLength(100)] public string? ActivationIdempotencyKey { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public Guid? RetiredByUserId { get; set; }
    [StringLength(1000)] public string? RetirementReason { get; set; }
    [StringLength(500)] public string? RetirementEvidenceReference { get; set; }
    [StringLength(100)] public string? RetirementIdempotencyKey { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string CompositionSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string CompositionIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CreationIdempotencyKey { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementCommittee CommitteeTemplate { get; set; } = null!;
    public ProcurementPolicySet PolicySet { get; set; } = null!;
    public ProcurementConfigurationProfile? ConfigurationProfile { get; set; }
    public ProcurementPolicyMethodRule MethodRule { get; set; } = null!;
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ICollection<ProcurementEvaluationCommitteeRoleRequirement> RequiredRoles { get; set; } =
        new List<ProcurementEvaluationCommitteeRoleRequirement>();
    public ICollection<ProcurementEvaluationCommitteeAppointment> Appointments { get; set; } =
        new List<ProcurementEvaluationCommitteeAppointment>();
    public ICollection<ProcurementEvaluationMeeting> Meetings { get; set; } =
        new List<ProcurementEvaluationMeeting>();
    public ICollection<ProcurementEvaluationScoreSheet> ScoreSheets { get; set; } =
        new List<ProcurementEvaluationScoreSheet>();
}

[Table("ProcurementEvaluationCommitteeRoleRequirements")]
public sealed class ProcurementEvaluationCommitteeRoleRequirement : TenantEntity
{
    public Guid CommitteeControlId { get; set; }
    public ProcurementCommitteeMemberKind MemberKind { get; set; }
    [Required, StringLength(100)] public string RoleName { get; set; } = string.Empty;
    [Range(1, 50)] public int MinimumCount { get; set; } = 1;
    public bool IsVoting { get; set; }
    public bool IsRequiredForQuorum { get; set; }

    public ProcurementEvaluationCommitteeControl CommitteeControl { get; set; } = null!;
}

[Table("ProcurementEvaluationCommitteeAppointments")]
public sealed class ProcurementEvaluationCommitteeAppointment : TenantEntity
{
    public Guid CommitteeControlId { get; set; }
    public Guid CommitteeMemberId { get; set; }
    public Guid ResponsibilityAssignmentId { get; set; }
    public Guid UserId { get; set; }
    [Required, StringLength(300)] public string UserDisplayName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string RoleName { get; set; } = string.Empty;
    public ProcurementCommitteeMemberKind MemberKind { get; set; }
    public bool IsVoting { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public ProcurementEvaluationAppointmentStatus Status { get; set; } =
        ProcurementEvaluationAppointmentStatus.Pending;
    public DateTime? AcceptedAtUtc { get; set; }
    [StringLength(500)] public string? AcceptanceSignatureReference { get; set; }
    [StringLength(500)] public string? AcceptanceEvidenceReference { get; set; }
    [StringLength(100)] public string? AcceptanceIdempotencyKey { get; set; }
    [StringLength(1000)] public string? StatusReason { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementEvaluationCommitteeControl CommitteeControl { get; set; } = null!;
    public ProcurementCommitteeMember CommitteeMember { get; set; } = null!;
    public ProcurementResponsibilityAssignment ResponsibilityAssignment { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<ProcurementEvaluationConflictDeclaration> ConflictDeclarations { get; set; } =
        new List<ProcurementEvaluationConflictDeclaration>();
    public ICollection<ProcurementEvaluationAttendanceRecord> AttendanceRecords { get; set; } =
        new List<ProcurementEvaluationAttendanceRecord>();
    public ICollection<ProcurementEvaluationScoreSheet> ScoreSheets { get; set; } =
        new List<ProcurementEvaluationScoreSheet>();
}

[Table("ProcurementEvaluationConflictDeclarations")]
public sealed class ProcurementEvaluationConflictDeclaration : TenantEntity
{
    public Guid AppointmentId { get; set; }
    [Range(1, int.MaxValue)] public int Version { get; set; }
    public ProcurementEvaluationConflictOutcome Outcome { get; set; }
    [Required, StringLength(2000)] public string Declaration { get; set; } = string.Empty;
    [StringLength(2000)] public string? ConflictDetails { get; set; }
    [Required, StringLength(500)] public string SignatureReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }
    public DateTime DeclaredAtUtc { get; set; }
    public Guid DeclaredByUserId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementEvaluationCommitteeAppointment Appointment { get; set; } = null!;
    public WorkflowEvidenceDocument? WorkflowEvidenceDocument { get; set; }
    public FileUploadRecord? FileUploadRecord { get; set; }
}

[Table("ProcurementEvaluationMeetings")]
public sealed class ProcurementEvaluationMeeting : TenantEntity
{
    public Guid CommitteeControlId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public ProcurementEvaluationPhase Phase { get; set; }
    public ProcurementEvaluationMeetingStatus Status { get; set; } = ProcurementEvaluationMeetingStatus.Draft;
    [Required, StringLength(200)] public string MeetingMode { get; set; } = string.Empty;
    [Required, StringLength(500)] public string MeetingChannel { get; set; } = string.Empty;
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public int EligibleVotingMemberCount { get; set; }
    public int SignedVotingAttendanceCount { get; set; }
    public bool ChairPresent { get; set; }
    public bool SecretaryPresent { get; set; }
    public bool QuorumMet { get; set; }
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [StringLength(500)] public string? RemoteMeetingEvidenceReference { get; set; }
    [StringLength(100)] public string? QuorumIdempotencyKey { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string QuorumSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string QuorumIntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementEvaluationCommitteeControl CommitteeControl { get; set; } = null!;
    public ICollection<ProcurementEvaluationAttendanceRecord> AttendanceRecords { get; set; } =
        new List<ProcurementEvaluationAttendanceRecord>();
    public ICollection<ProcurementEvaluationScoreSheet> ScoreSheets { get; set; } =
        new List<ProcurementEvaluationScoreSheet>();
}

[Table("ProcurementEvaluationAttendanceRecords")]
public sealed class ProcurementEvaluationAttendanceRecord : TenantEntity
{
    public Guid MeetingId { get; set; }
    public Guid AppointmentId { get; set; }
    public bool IsPresent { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    [StringLength(500)] public string? SignatureReference { get; set; }
    [StringLength(500)] public string? EvidenceReference { get; set; }
    public bool WasEligibleAtSignature { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementEvaluationMeeting Meeting { get; set; } = null!;
    public ProcurementEvaluationCommitteeAppointment Appointment { get; set; } = null!;
}

[Table("ProcurementEvaluationScoreSheets")]
public sealed class ProcurementEvaluationScoreSheet : TenantEntity
{
    public Guid CommitteeControlId { get; set; }
    public Guid MeetingId { get; set; }
    public Guid AppointmentId { get; set; }
    public ProcurementEvaluationPhase Phase { get; set; }
    [Required, StringLength(100)] public string ScoreSubjectType { get; set; } = string.Empty;
    public Guid ScoreSubjectId { get; set; }
    [Range(1, int.MaxValue)] public int Attempt { get; set; } = 1;
    public ProcurementEvaluationScoreSheetStatus Status { get; set; } =
        ProcurementEvaluationScoreSheetStatus.Locked;
    public DateTime SubmittedAtUtc { get; set; }
    public Guid SubmittedByUserId { get; set; }
    [Required, StringLength(300)] public string SubmittedByName { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string ScoreSnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(500)] public string SignatureReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementEvaluationCommitteeControl CommitteeControl { get; set; } = null!;
    public ProcurementEvaluationMeeting Meeting { get; set; } = null!;
    public ProcurementEvaluationCommitteeAppointment Appointment { get; set; } = null!;
    public ICollection<ProcurementEvaluationScoreRecall> Recalls { get; set; } =
        new List<ProcurementEvaluationScoreRecall>();
}

[Table("ProcurementEvaluationScoreRecalls")]
public sealed class ProcurementEvaluationScoreRecall : TenantEntity
{
    public Guid ScoreSheetId { get; set; }
    public ProcurementEvaluationScoreRecallStatus Status { get; set; } =
        ProcurementEvaluationScoreRecallStatus.PendingApproval;
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid RequestedByUserId { get; set; }
    [Required, StringLength(300)] public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    [StringLength(300)] public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [StringLength(500)] public string? DecisionReference { get; set; }
    [StringLength(500)] public string? DecisionEvidenceReference { get; set; }
    [StringLength(100)] public string? DecisionIdempotencyKey { get; set; }
    public int? AuthorizedNewAttempt { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementEvaluationScoreSheet ScoreSheet { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public WorkflowEvidenceDocument? WorkflowEvidenceDocument { get; set; }
    public FileUploadRecord? FileUploadRecord { get; set; }
}
