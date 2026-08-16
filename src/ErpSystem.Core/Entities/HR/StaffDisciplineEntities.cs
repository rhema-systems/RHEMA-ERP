using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.StaffDiscipline;

/// <summary>
/// Master list of offense types that can be committed by staff.
/// </summary>
public class StaffOffense : TenantEntity
{
	[MaxLength(50)]
    public string OffenseCode { get; set; } = string.Empty;
	
	[Required]
    [MaxLength(200)]
    public string OffenseName { get; set; } = string.Empty;
    
	[MaxLength(1000)]
	public string OffenseDescription { get; set; } = string.Empty;
	
	public bool IsActive { get; set; } = true;
 
    public virtual ICollection<StaffOffenseProcedure> OffenseProcedures { get; set; } = new List<StaffOffenseProcedure>();
}

/// <summary>
/// Ordered procedural steps to follow for a specific offense during disciplinary action.
/// Ensures consistent and fair treatment across all employees.
/// </summary>
public class StaffOffenseProcedure : TenantEntity
{
	[Required]
    public Guid OffenseId { get; set; }

    [ForeignKey(nameof(OffenseId))]
    public virtual StaffOffense Offense { get; set; } = null!;
	
	[Required]
    [MaxLength(200)]
    public string StepName { get; set; } = string.Empty;
    
	[MaxLength(1000)]
	public string StepDescription { get; set; } = string.Empty;
    
	[Required]
	public int Sequence { get; set; }
	
	public int? ExpectedCompletionDays { get; set; }
}

/// <summary>
/// Lookup table of disciplinary action types (e.g. Verbal Warning, Written Warning, Dismissal).
/// </summary>
public class StaffDisciplinaryActionType : TenantEntity
{
	[MaxLength(50)]
    public string Code { get; set; } = string.Empty;
	
	[Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
	
	[MaxLength(500)]
    public string Description { get; set; } = string.Empty;
	
	public bool IsActive { get; set; } = true;

	public int? DefaultSuspensionDays { get; set; }

	public decimal? DefaultFineAmount { get; set; }

    /// <summary>
    /// Who may issue this action, per FR-HR-080 (HODs limited to verbal warnings) and FR-HR-092
    /// (the MD signs terminations). Defaults to <see cref="DisciplinaryActionAuthority.Hr"/> so an
    /// unmaintained catalog row is restrictive rather than permissive.
    /// </summary>
    public DisciplinaryActionAuthority MinimumAuthority { get; set; } = DisciplinaryActionAuthority.Hr;
}

/// <summary>
/// Disciplinary case header. Holds case identity, incident details, decision, and closure.
/// All penalty, investigation, and hearing detail is carried by dedicated one-to-one sub-entities.
/// Whether a penalty applies is determined by the presence (non-null) of the corresponding sub-entity:
///   warning → WarningPenalty, suspension → SuspensionPenalty, fine → FinePenalty,
///   termination → TerminationDetails, demotion → query StaffDemotion.DisciplinaryActionId.
/// </summary>
public class StaffDisciplinaryAction : TenantEntity
{
    [MaxLength(50)]
    public string CaseNumber { get; set; } = string.Empty;

    public DisciplinaryStatus Status { get; set; } = DisciplinaryStatus.Draft;

    // Employee
    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required]
    public Guid StaffOffenseId { get; set; }

    [ForeignKey(nameof(StaffOffenseId))]
    public virtual StaffOffense StaffOffense { get; set; } = null!;

    [Required]
    public StaffOffenseSeverity Severity { get; set; } = StaffOffenseSeverity.Minor;

    // Incident Details
    [Required]
    public DateTime IncidentDate { get; set; }

    [Required]
    [MaxLength(4000)]
    public string IncidentDescription { get; set; } = string.Empty;

    // Reported By
    [Required]
    public Guid ReportedById { get; set; }

    [ForeignKey(nameof(ReportedById))]
    public virtual Employee ReportedBy { get; set; } = null!;

    [Required]
    public DateTime ReportedDate { get; set; }

    public Guid? ReportedToId { get; set; }

    [ForeignKey(nameof(ReportedToId))]
    public virtual Employee? ReportedTo { get; set; }

    // Investigation flag — detail lives in Investigation sub-entity
    public bool RequiresInvestigation { get; set; }

    // Hearing flag — detail lives in Hearing sub-entity
    public bool HearingRequired { get; set; }

    // Decision
    public Guid? ActionTypeId { get; set; }

    [ForeignKey(nameof(ActionTypeId))]
    public virtual StaffDisciplinaryActionType? ActionType { get; set; }

    [MaxLength(1000)]
    public string? ActionDetails { get; set; }

    public DateTime? DecisionDate { get; set; }

    public Guid? DecisionById { get; set; }

    [ForeignKey(nameof(DecisionById))]
    public virtual Employee? DecisionBy { get; set; }

    [MaxLength(4000)]
    public string? DecisionRationale { get; set; }

    // Closure
    public DateTime? ClosedDate { get; set; }

    [MaxLength(4000)]
    public string? ClosureNotes { get; set; }

    public Guid? ClosedById { get; set; }

    [ForeignKey(nameof(ClosedById))]
    public virtual Employee? ClosedBy { get; set; }

    // -------------------------------------------------------------------------
    // Sub-entity navigations
    // -------------------------------------------------------------------------

    /// <summary>Populated when an investigation is opened for this case.</summary>
    public virtual StaffDisciplineInvestigation? Investigation { get; set; }

    /// <summary>Populated when a formal hearing is scheduled for this case.</summary>
    public virtual StaffDisciplineHearing? Hearing { get; set; }

    /// <summary>Populated when the outcome includes a warning.</summary>
    public virtual StaffDisciplineWarning? Warning { get; set; }

    /// <summary>Populated when the outcome includes a suspension.</summary>
    public virtual StaffDisciplineSuspension? Suspension { get; set; }

    /// <summary>Populated when the outcome includes a fine.</summary>
    public virtual StaffDisciplineFine? Fine { get; set; }

    /// <summary>Populated when the outcome results in termination.</summary>
    public virtual StaffDisciplineTermination? Termination { get; set; }

    /// <summary>Populated when a separation/offboarding process is initiated.</summary>
    public virtual StaffDisciplineSeparation? Separation { get; set; }

    /// <summary>One case has at most one appeal.</summary>
    public virtual StaffDisciplineAppeal? Appeal { get; set; }

    /// <summary>One case can produce at most one corrective action plan.</summary>
    public virtual StaffDisciplineCorrectiveAction? CorrectiveAction { get; set; }

    // Collections
    public virtual ICollection<StaffDisciplineActionStep> ActionSteps { get; set; } = new List<StaffDisciplineActionStep>();
    public virtual ICollection<StaffDisciplineWitness> Witnesses { get; set; } = new List<StaffDisciplineWitness>();
    public virtual ICollection<StaffDisciplineDocument> Documents { get; set; } = new List<StaffDisciplineDocument>();
    public virtual ICollection<StaffDisciplineNote> Notes { get; set; } = new List<StaffDisciplineNote>();
    public virtual ICollection<StaffDisciplineNotification> Notifications { get; set; } = new List<StaffDisciplineNotification>();
    public virtual ICollection<StaffDisciplineLegalReview> LegalReviews { get; set; } = new List<StaffDisciplineLegalReview>();
}

/// <summary>
/// Investigation details for a disciplinary case.
/// Created when RequiresInvestigation is set; one-to-one with StaffDisciplinaryAction.
/// </summary>
public class StaffDisciplineInvestigation : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    public Guid? InvestigatorId { get; set; }

    [ForeignKey(nameof(InvestigatorId))]
    public virtual Employee? Investigator { get; set; }

    public DateTime? InvestigationStartDate { get; set; }
    public DateTime? InvestigationEndDate { get; set; }

    [MaxLength(4000)]
    public string? InvestigationFindings { get; set; }

    [MaxLength(4000)]
    public string? EvidenceCollected { get; set; }
}

/// <summary>
/// Hearing details for a disciplinary case, including employee representation.
/// Created when HearingRequired is set; one-to-one with StaffDisciplinaryAction.
/// </summary>
public class StaffDisciplineHearing : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    public DateTime? HearingDate { get; set; }

    [MaxLength(500)]
    public string? HearingVenue { get; set; }

    [MaxLength(4000)]
    public string? HearingNotes { get; set; }

    public Guid? HearingOfficerId { get; set; }

    [ForeignKey(nameof(HearingOfficerId))]
    public virtual Employee? HearingOfficer { get; set; }

    public bool EmployeeAttendedHearing { get; set; }

    [MaxLength(4000)]
    public string? EmployeeStatement { get; set; }

    public bool EmployeeHadRepresentation { get; set; }

    public DisciplinaryRepresentativeType? RepresentativeType { get; set; }

    public Guid? RepresentativeEmployeeId { get; set; }

    [ForeignKey(nameof(RepresentativeEmployeeId))]
    public virtual Employee? RepresentativeEmployee { get; set; }

    [MaxLength(100)]
    public string? RepresentativeName { get; set; }

    [MaxLength(200)]
    public string? RepresentativePosition { get; set; }

    [MaxLength(100)]
    public string? RepresentativeContactInfo { get; set; }
}

/// <summary>
/// Warning penalty detail for a disciplinary case.
/// Created when the decision includes a verbal, written, or final warning; one-to-one with StaffDisciplinaryAction.
/// </summary>
public class StaffDisciplineWarning : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    [Required]
    public DisciplinaryWarningType WarningType { get; set; }

    public DateTime? WarningExpiryDate { get; set; }

    [MaxLength(200)]
    public string? WarningLetterReference { get; set; }
}

/// <summary>
/// Suspension penalty detail for a disciplinary case.
/// Created when the decision includes a suspension; one-to-one with StaffDisciplinaryAction.
/// </summary>
public class StaffDisciplineSuspension : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    public DateTime? SuspensionStartDate { get; set; }
    public DateTime? SuspensionEndDate { get; set; }

    public bool SuspensionWithPay { get; set; }
}

/// <summary>
/// Fine penalty detail for a disciplinary case.
/// Created when the decision includes a monetary fine; one-to-one with StaffDisciplinaryAction.
/// </summary>
public class StaffDisciplineFine : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    public decimal? FineAmount { get; set; }
    public DisciplinaryFinePaymentStatus? FinePaymentStatus { get; set; }
    public DateTime? FineDueDate { get; set; }
    public decimal? FinePaidAmount { get; set; }
    public DateTime? FinePaymentDate { get; set; }
}

/// <summary>
/// Tracks the completion of each procedural step (from StaffOffenseProcedure)
/// against a live disciplinary case.
/// </summary>
public class StaffDisciplineActionStep : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    [Required]
    public Guid OffenseProcedureId { get; set; }

    [ForeignKey(nameof(OffenseProcedureId))]
    public virtual StaffOffenseProcedure OffenseProcedure { get; set; } = null!;

    public DateTime? DueDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    [Required]
    public DisciplinaryActionStepStatus Status { get; set; } = DisciplinaryActionStepStatus.Pending;

    public Guid? ActionedById { get; set; }

    [ForeignKey(nameof(ActionedById))]
    public virtual Employee? ActionedBy { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public virtual ICollection<StaffDisciplineDocument> Documents { get; set; } = new List<StaffDisciplineDocument>();
}

/// <summary>
/// Witness information for a disciplinary case.
/// </summary>
public class StaffDisciplineWitness : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsEmployee { get; set; }

    public Guid? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    [MaxLength(200)]
    public string? ContactInfo { get; set; }

    [MaxLength(4000)]
    public string? Statement { get; set; }

    public DateTime? StatementDate { get; set; }
}

/// <summary>
/// A document attached to a disciplinary case, step, or appeal (evidence, reports, letters, etc.).
/// DisciplinaryActionId is always set. Scope determines which context the document belongs to:
/// Case — main case only; ActionStep — a specific procedural step; Appeal — the appeal record.
/// ActionStepId must be set when Scope is ActionStep; AppealId must be set when Scope is Appeal.
/// </summary>
public class StaffDisciplineDocument : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    /// <summary>Determines which context this document belongs to. Enforced by the service layer.</summary>
    public DisciplinaryDocumentScope Scope { get; set; } = DisciplinaryDocumentScope.Case;

    /// <summary>Set only when Scope is ActionStep.</summary>
    public Guid? ActionStepId { get; set; }

    [ForeignKey(nameof(ActionStepId))]
    public virtual StaffDisciplineActionStep? ActionStep { get; set; }

    /// <summary>Set only when Scope is Appeal.</summary>
    public Guid? AppealId { get; set; }

    [ForeignKey(nameof(AppealId))]
    public virtual StaffDisciplineAppeal? Appeal { get; set; }

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    public DisciplinaryDocumentCategory Category { get; set; } = DisciplinaryDocumentCategory.Evidence;

    /// <summary>Scanned controlled upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; }

    [Required]
    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

/// <summary>
/// Free-text case notes added by HR staff or investigators.
/// Confidential notes are only visible to authorised roles.
/// </summary>
public class StaffDisciplineNote : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    [Required]
    public Guid CreatedByEmployeeId { get; set; }

    [ForeignKey(nameof(CreatedByEmployeeId))]
    public virtual Employee CreatedByEmployee { get; set; } = null!;

    [Required]
    [MaxLength(4000)]
    public string Note { get; set; } = string.Empty;

    public bool IsConfidential { get; set; }

    [Required]
    public DateTime NoteDate { get; set; }
}

/// <summary>
/// Formal letters and notices issued to the employee throughout the disciplinary process.
/// Provides a legally defensible communication audit trail.
/// </summary>
public class StaffDisciplineNotification : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    public DisciplinaryNotificationType NotificationType { get; set; }

    public DateTime SentDate { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    [Required]
    public Guid SentById { get; set; }

    [ForeignKey(nameof(SentById))]
    public virtual Employee SentBy { get; set; } = null!;

    public DateTime? AcknowledgedDate { get; set; }

    public bool IsFollowupSent { get; set; }
    public DateTime? FollowupDate { get; set; }
}

/// <summary>
/// An appeal lodged by the employee against a disciplinary decision.
/// </summary>
public class StaffDisciplineAppeal : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public DateTime FiledDate { get; set; }

    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    public DisciplineAppealStatus AppealStatus { get; set; } = DisciplineAppealStatus.Filed;

    public Guid? AppealOfficerId { get; set; }

    [ForeignKey(nameof(AppealOfficerId))]
    public virtual Employee? AppealOfficer { get; set; }

    public DateTime? HearingDate { get; set; }

    [MaxLength(1000)]
    public string? HearingVenue { get; set; }

    [MaxLength(3000)]
    public string? HearingNotes { get; set; }

    public DisciplineAppealOutcomeType? AppealOutcome { get; set; }

    [MaxLength(3000)]
    public string? AppealOutcomeNotes { get; set; }

    public DateTime? AppealOutcomeDate { get; set; }

    public Guid? AppealOutcomeById { get; set; }

    [ForeignKey(nameof(AppealOutcomeById))]
    public virtual Employee? AppealOutcomeBy { get; set; }

    public virtual ICollection<StaffDisciplineDocument> Documents { get; set; } = new List<StaffDisciplineDocument>();
}

/// <summary>
/// A rehabilitation/corrective plan assigned to an employee following a disciplinary outcome.
/// Contains one or more measurable action items with target dates.
/// </summary>
public class StaffDisciplineCorrectiveAction : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required]
    public Guid SupervisorId { get; set; }

    [ForeignKey(nameof(SupervisorId))]
    public virtual Employee Supervisor { get; set; } = null!;

    [MaxLength(1000)]
    public string Objective { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime ReviewDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public DisciplineCorrectiveActionStatus Status { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public virtual ICollection<StaffDisciplineCorrectiveActionItem> Items { get; set; } = new List<StaffDisciplineCorrectiveActionItem>();
}

/// <summary>
/// An individual task within a corrective action plan, with its own target date and completion status.
/// </summary>
public class StaffDisciplineCorrectiveActionItem : TenantEntity
{
    [Required]
    public Guid CorrectiveActionId { get; set; }

    [ForeignKey(nameof(CorrectiveActionId))]
    public virtual StaffDisciplineCorrectiveAction CorrectiveAction { get; set; } = null!;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public DateTime TargetDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public DisciplineCorrectiveActionStatus Status { get; set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }
}

/// <summary>
/// Tracks legal review activity for a disciplinary case.
/// Whether legal review is required is derivable from the presence of entries in this collection.
/// </summary>
public class StaffDisciplineLegalReview : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    [Required]
    public DateTime ReferredToLegalDate { get; set; }

    public Guid? ReferredById { get; set; }

    [ForeignKey(nameof(ReferredById))]
    public virtual Employee? ReferredBy { get; set; }

    public DateTime? LegalReviewCompleteDate { get; set; }

    [Required]
    public DisciplineLegalRiskLevel LegalRiskLevel { get; set; }

    [MaxLength(4000)]
    public string? LegalAdvice { get; set; }

    public bool RequiresExternalCounsel { get; set; }

    public Guid? ExternalCounselId { get; set; }

    [ForeignKey(nameof(ExternalCounselId))]
    public virtual ExternalAssociate? ExternalCounsel { get; set; }

    /// <remarks>
    /// Point-in-time snapshot of the counsel's name at the time of engagement.
    /// Preserved independently of ExternalCounsel so that later changes to the
    /// ExternalAssociate record do not alter the legal review history.
    /// </remarks>
    [MaxLength(200)]
    public string? ExternalCounselName { get; set; }

    /// <remarks>
    /// Point-in-time snapshot of the counsel's firm name at the time of engagement.
    /// Preserved independently of ExternalCounsel so that later changes to the
    /// ExternalAssociate record do not alter the legal review history.
    /// </remarks>
    [MaxLength(700)]
    public string? ExternalCounselFirm { get; set; }

    public DateTime? ExternalCounselReviewDate { get; set; }

    [MaxLength(4000)]
    public string? ExternalCounselOpinion { get; set; }

    public decimal? LegalCostsIncurred { get; set; }

    public bool IsConfidential { get; set; } = true;
}

/// <summary>
/// Termination details for a disciplinary case that results in employment termination.
/// One-to-one with StaffDisciplinaryAction; presence of this record indicates termination outcome.
/// </summary>
public class StaffDisciplineTermination : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    [Required]
    public EmployeeTerminationType Type { get; set; }

    public bool IsEligibleForRehire { get; set; }
    public DateTime? EligibleForRehireDate { get; set; }

    [MaxLength(1000)]
    public string? RehireRestrictions { get; set; }

    public bool FinalPaycheckProcessed { get; set; }
    public DateTime? FinalPaycheckDate { get; set; }
    public decimal? FinalPaycheckAmount { get; set; }

    [MaxLength(4000)]
    public string? SeparationNotes { get; set; }
}

/// <summary>
/// Separation/offboarding process checklist for a disciplinary termination case.
/// One-to-one with StaffDisciplinaryAction; complements StaffDisciplineTerminationDetails
/// with the operational tasks that must be completed before the employee leaves.
/// </summary>
public class StaffDisciplineSeparation : TenantEntity
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction DisciplinaryAction { get; set; } = null!;

    public bool ExitInterviewCompleted { get; set; }
    public DateTime? ExitInterviewDate { get; set; }

    [MaxLength(4000)]
    public string? ExitInterviewNotes { get; set; }

    public Guid? ExitInterviewerId { get; set; }

    [ForeignKey(nameof(ExitInterviewerId))]
    public virtual Employee? ExitInterviewer { get; set; }

    public bool EquipmentReturned { get; set; }
    public DateTime? EquipmentReturnedDate { get; set; }

    [MaxLength(1000)]
    public string? MissingEquipment { get; set; }

    public bool AccessRevoked { get; set; }
    public DateTime? AccessRevokedDate { get; set; }

    public Guid? AccessRevokedById { get; set; }

    [ForeignKey(nameof(AccessRevokedById))]
    public virtual Employee? AccessRevokedBy { get; set; }

    public bool FinalPayrollProcessed { get; set; }
    public DateTime? FinalPayrollDate { get; set; }

    public bool BenefitsTerminated { get; set; }
    public DateTime? BenefitsTerminationDate { get; set; }

    public bool ExitChecklistCompleted { get; set; }
    public DateTime? ExitChecklistCompletedDate { get; set; }

    [MaxLength(4000)]
    public string? AdditionalNotes { get; set; }
}
