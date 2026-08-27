using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.StaffGrievance;

/// <summary>
/// An employee grievance, and its progress up FR-HR-181's escalation ladder.
/// </summary>
/// <remarks>
/// <para><b>What this is, and what it is not.</b> FR-HR-181 requires that a grievance escalate
/// through Employee → Supervisor → HOD → HR → GM Finance &amp; Administration → Managing Director →
/// Board, retaining the statement and each level's response. That is what this models: the record and
/// the ladder. It is a FIRST CUT — case conferencing, union representation, anonymous intake and
/// grievance analytics belong to an employee-relations module that does not exist yet, and this store
/// becomes its data when it is built. See [[hr-deferred-modules]] #2.</para>
///
/// <para><b>Why it is not on the workflow engine</b>, unlike the disciplinary decision. The engine
/// models approval — a thing is proposed and someone with authority confirms or refuses it. A
/// grievance is not approved; it is ANSWERED, and the person answering may resolve it, or may fail to
/// satisfy the employee, who then escalates. The decision to move up the ladder belongs to the
/// GRIEVER, not to an approver, which is the opposite of the engine's shape. Same class of call as
/// [[goal-approval-stays-bespoke]].</para>
///
/// <para><b>Why the rungs are not resolved to people.</b> TDC's org data cannot support it — see the
/// note on <see cref="GrievanceEscalationLevel"/>. A grievance sits at a rung; whoever answers is
/// recorded from their own token, and HR may name a responder explicitly on a step so that person can
/// see and answer it. Routing can be added later without changing any of this.</para>
/// </remarks>
public class StaffGrievance : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string GrievanceNumber { get; set; } = string.Empty;

    /// <summary>
    /// What kind of employee-relations case this is — area 9c slice 1, decision D-4.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="EmployeeRelationsCaseType.Grievance"/>, which is also enum member 1,
    /// so every row written before this column existed is correct with no back-fill. ⚠ The type is
    /// descriptive, not a permission boundary: read access is still decided entirely by who is on
    /// the case.
    /// </remarks>
    public EmployeeRelationsCaseType CaseType { get; set; } = EmployeeRelationsCaseType.Grievance;

    /// <summary>
    /// The employee raising it — and, since area 9c, the case's PRIMARY PARTY.
    /// </summary>
    /// <remarks>
    /// Always the token's employee on the grievance path; never supplied in a payload, which is why
    /// HR cannot raise a grievance on somebody's behalf. It stays <b>required</b> under decision
    /// D-7: every employee-relations case at TDC concerns at least one identifiable employee, and a
    /// case about a class of staff names the affected employee or the union representative as
    /// primary. Everybody else on the case is a <see cref="StaffGrievanceParty"/>.
    /// </remarks>
    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The grievance statement in the employee's own words. FR-HR-181 requires it be retained, so
    /// nothing overwrites it after filing — later positions are recorded as step responses.
    /// </summary>
    [Required]
    [MaxLength(6000)]
    public string Statement { get; set; } = string.Empty;

    public DateTime FiledDate { get; set; }

    public GrievanceStatus Status { get; set; } = GrievanceStatus.Filed;

    /// <summary>The rung the grievance is currently sitting at, awaiting an answer.</summary>
    public GrievanceEscalationLevel CurrentLevel { get; set; } = GrievanceEscalationLevel.Supervisor;

    public DateTime? ResolvedDate { get; set; }

    [MaxLength(4000)]
    public string? ResolutionSummary { get; set; }

    public DateTime? WithdrawnDate { get; set; }

    [MaxLength(1000)]
    public string? WithdrawalReason { get; set; }

    // ── FR-HR-181 obligation 5: HR interpretation (area 9c slice 2) ───────────

    /// <summary>
    /// HR's formal reading of the case — what the policy, the Conditions of Service or the
    /// Collective Bargaining Agreement say about it.
    /// </summary>
    /// <remarks>
    /// FR-HR-181 names this as an artefact distinct from the HR rung's step response, and it is:
    /// the step response is HR answering the employee, this is HR's position on the merits, which
    /// the GM, the MD and the Board all read on the way up. Before slice 2 there was no field for
    /// it anywhere.
    ///
    /// <para><b>Amendable while the case is open, and re-stamped each time.</b> It is HR's working
    /// reading, not a decision, so it may legitimately change as facts emerge — but it is frozen the
    /// moment the case reaches a terminal state, because an interpretation edited after the fact is
    /// exactly what makes an outcome indefensible. The decision that must never change is the
    /// <see cref="StaffGrievanceResolution"/>, which is frozen on write.</para>
    /// </remarks>
    [MaxLength(4000)]
    public string? HrInterpretation { get; set; }

    public Guid? HrInterpretationById { get; set; }

    [ForeignKey(nameof(HrInterpretationById))]
    public virtual Employee? HrInterpretationBy { get; set; }

    public DateTime? HrInterpretationDate { get; set; }

    // ── Closure without resolution (area 9c slice 2) ──────────────────────────

    /// <summary>
    /// When the case was closed without being resolved — the ladder exhausted at Board level.
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="GrievanceStatus.Closed"/> existed from area 9 slice 7 and <b>had no writer
    /// anywhere</b>: all three references to it in the solution were read filters, and 0 of 69 rows
    /// carried it. A case the Board had answered without resolving therefore had no terminal state —
    /// it sat <c>UnderReview</c> for ever and the reminder sweep counted it as open for ever.
    /// Slice 2 gives it its first writer.
    /// </remarks>
    public DateTime? ClosedDate { get; set; }

    [MaxLength(1000)]
    public string? ClosureReason { get; set; }

    public Guid? ClosedById { get; set; }

    [ForeignKey(nameof(ClosedById))]
    public virtual Employee? ClosedBy { get; set; }

    /// <summary>FR-HR-181 obligation 7 — the investigation report. One per case, or none.</summary>
    public virtual StaffGrievanceInvestigation? Investigation { get; set; }

    /// <summary>FR-HR-181 obligation 8 — the resolution decision. One per case, or none.</summary>
    public virtual StaffGrievanceResolution? Resolution { get; set; }

    /// <summary>FR-HR-181 obligation 9 and the case's paperwork — area 9c slice 3.</summary>
    public virtual ICollection<StaffGrievanceDocument> Documents { get; set; } = new List<StaffGrievanceDocument>();

    /// <summary>Conferences, mediations and union consultations — area 9c slice 4.</summary>
    public virtual ICollection<StaffGrievanceConference> Conferences { get; set; } = new List<StaffGrievanceConference>();

    public virtual ICollection<StaffGrievanceStep> Steps { get; set; } = new List<StaffGrievanceStep>();

    /// <summary>Everybody on the case other than the primary party — area 9c slice 1.</summary>
    public virtual ICollection<StaffGrievanceParty> Parties { get; set; } = new List<StaffGrievanceParty>();
}

/// <summary>
/// FR-HR-181 obligation 7 — the investigation report. Area 9c slice 2. One per case.
/// </summary>
/// <remarks>
/// <para>Modelled on <c>StaffDisciplineInvestigation</c>, which area 9 built for the disciplinary
/// case and which the grievance half never got — so a grievance that needed investigating was
/// investigated on paper and the system held nothing but the responder's eventual answer.</para>
///
/// <para><b>The investigator may be external</b>, for the same reason a party may be
/// (<see cref="StaffGrievanceParty"/>): a grievance about senior management is exactly the one that
/// gets an outside investigator, and requiring an <c>Employee</c> FK would have made that
/// unrecordable. Exactly one of <see cref="InvestigatorId"/> and
/// <see cref="ExternalInvestigatorName"/> is supplied.</para>
///
/// <para><b>Completion is a gate, not a flag.</b> <see cref="CompletedDate"/> is only set through
/// the complete path, which refuses to run without findings — an investigation reported as complete
/// with nothing in it is worse than one still open, because the ladder above it will rely on it.</para>
/// </remarks>
public class StaffGrievanceInvestigation : TenantEntity
{
    [Required]
    public Guid GrievanceId { get; set; }

    [ForeignKey(nameof(GrievanceId))]
    public virtual StaffGrievance Grievance { get; set; } = null!;

    /// <summary>An internal investigator. Mutually exclusive with <see cref="ExternalInvestigatorName"/>.</summary>
    public Guid? InvestigatorId { get; set; }

    [ForeignKey(nameof(InvestigatorId))]
    public virtual Employee? Investigator { get; set; }

    [MaxLength(200)]
    public string? ExternalInvestigatorName { get; set; }

    [MaxLength(200)]
    public string? ExternalInvestigatorOrganisation { get; set; }

    public DateTime StartedDate { get; set; }

    /// <summary>
    /// When the investigation is due. Not a spec requirement for grievances — FR-HR-178's four
    /// weeks is the DISCIPLINARY clock and must not be silently applied here — but a case being
    /// investigated needs a date the reminder sweep can chase, and slice 7 chases this one.
    /// </summary>
    public DateTime? TargetDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    [MaxLength(4000)]
    public string? Findings { get; set; }

    [MaxLength(4000)]
    public string? EvidenceCollected { get; set; }

    /// <summary>What the investigator advises. Advice only — deciding is the rung's act, not theirs.</summary>
    [MaxLength(4000)]
    public string? Recommendation { get; set; }

    /// <summary>Who opened it, from their own token.</summary>
    public Guid? OpenedById { get; set; }

    [ForeignKey(nameof(OpenedById))]
    public virtual Employee? OpenedBy { get; set; }
}

/// <summary>
/// FR-HR-181 obligation 8 — the resolution decision. Area 9c slice 2. One per case.
/// </summary>
/// <remarks>
/// <para><b>What this replaces.</b> Before slice 2 a resolution was
/// <c>StaffGrievance.ResolutionSummary</c>, which <c>RespondAsync</c> filled with a verbatim copy of
/// whatever the last responder typed. The system could not tell an ANSWER from a DECISION: there was
/// no decider distinct from the responder, no decision date of its own, no rung it was taken at, no
/// outcome, and no remedy. Slice 0 asserted that conflation as the current position so that this
/// change would be visible.</para>
///
/// <para><b>Frozen on write.</b> A recorded decision is never amended — that is the whole point of
/// FR-HR-181 retaining it. The single exception is filling in an outcome that was never captured:
/// see <see cref="GrievanceResolutionOutcome.NotRecorded"/>.</para>
///
/// <para><b><see cref="DecidedAtLevel"/> is stamped, not derived.</b> The case's
/// <c>CurrentLevel</c> is where it sits now; this is the rung that actually decided it, and a case
/// resolved at the HOD rung must still read that way if anything later moves it.</para>
/// </remarks>
public class StaffGrievanceResolution : TenantEntity
{
    [Required]
    public Guid GrievanceId { get; set; }

    [ForeignKey(nameof(GrievanceId))]
    public virtual StaffGrievance Grievance { get; set; } = null!;

    public GrievanceResolutionOutcome Outcome { get; set; } = GrievanceResolutionOutcome.NotRecorded;

    /// <summary>The decision, in the decider's words.</summary>
    [Required]
    [MaxLength(4000)]
    public string Decision { get; set; } = string.Empty;

    /// <summary>What either side undertook to do. The part a signed agreement is written from.</summary>
    [MaxLength(4000)]
    public string? RemedyOrUndertakings { get; set; }

    [Required]
    public Guid DecidedById { get; set; }

    [ForeignKey(nameof(DecidedById))]
    public virtual Employee DecidedBy { get; set; } = null!;

    public DateTime DecidedDate { get; set; }

    /// <summary>The rung that decided it, stamped at the time. See remarks.</summary>
    public GrievanceEscalationLevel DecidedAtLevel { get; set; }

    /// <summary>
    /// Set when a <see cref="GrievanceResolutionOutcome.NotRecorded"/> outcome is filled in later,
    /// so that completing the record is visibly a different act from taking the decision.
    /// </summary>
    public DateTime? OutcomeRecordedDate { get; set; }

    public Guid? OutcomeRecordedById { get; set; }

    [ForeignKey(nameof(OutcomeRecordedById))]
    public virtual Employee? OutcomeRecordedBy { get; set; }

    // ── FR-HR-181 obligation 9: the final signed agreement (area 9c slice 3) ──

    /// <summary>
    /// When the agreement was signed off the system, as written on the paper HR then uploaded.
    /// </summary>
    /// <remarks>
    /// Supplied by HR rather than stamped, because the signature happens in a room and the scan
    /// arrives afterwards — stamping <c>UtcNow</c> would record when somebody got round to
    /// uploading it, which is not what FR-HR-181 asks to be retained. The document itself is a
    /// <see cref="StaffGrievanceDocument"/> scoped <c>Agreement</c>.
    /// </remarks>
    public DateTime? AgreementSignedDate { get; set; }

    /// <summary>
    /// When the employee confirmed the agreement in the system — their own act, and the reason this
    /// stayed off the workflow engine.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately NOT an approval (build-plan decision D-10, judged here rather than assumed).
    /// The engine models a proposal somebody with authority confirms or refuses, and routes onward
    /// on refusal. This is a two-party acceptance: if the employee declines, nothing routes anywhere
    /// — the case simply is not settled, and their remedy is the ladder they already have. Recording
    /// acceptance as an approval would also put a decision about the employee's own case into a
    /// queue somebody else can action, which is the one thing this module refuses everywhere else.
    /// </remarks>
    public DateTime? AgreementAcceptedDate { get; set; }

    public Guid? AgreementAcceptedById { get; set; }

    [ForeignKey(nameof(AgreementAcceptedById))]
    public virtual Employee? AgreementAcceptedBy { get; set; }

    /// <summary>
    /// What the employee said when they accepted. Its own column rather than appended to
    /// <see cref="RemedyOrUndertakings"/>, because that field is part of the frozen decision and
    /// editing it to hold a later remark would corrupt exactly the artefact FR-HR-181 retains.
    /// </summary>
    [MaxLength(1000)]
    public string? AgreementAcceptanceComment { get; set; }
}

/// <summary>
/// Who answers an employee-relations case at a given rung, for a given part of the organisation —
/// area 9c slice 5, decision D-3. This is <b>FR-HR-084</b>: "model a grievance hierarchy defining
/// reporting lines."
/// </summary>
/// <remarks>
/// <para><b>Why this is maintained and not derived, which is the whole point.</b> FR-HR-181's ladder
/// names Supervisor and HOD rungs, and the obvious implementation resolves them from
/// <c>Employees.ManagerId</c> and <c>OrganizationUnits.HeadEmployeeId</c>. Measured on the DEFAULT
/// tenant on 2026-08-27: <b>ManagerId is set for 486 of 8,353 employees (5.8%) and unit heads for 2
/// of 48</b> — and both were WORSE than when the same measurement was taken six weeks earlier.
/// Deriving authority from that data resolves to nobody for 94% of staff.</para>
///
/// <para>This is the <b>fourth</b> requirement to hit that wall — FR-HR-080's HOD sanction rule,
/// FR-HR-173's establishment check (already downgraded to advisory for exactly this reason),
/// FR-HR-181's rungs, and now FR-HR-084. Rather than work around it a fourth time, slice 5 builds
/// the alternative already put to TDC in <c>docs/HR-OPEN-QUESTIONS-FOR-TDC.md</c> §4: HR names who
/// answers, directly, on a screen. ⚠ <b>If reporting lines are ever genuinely maintained, this table
/// does not need removing</b> — it is the override layer a derived lookup would need anyway.</para>
///
/// <para><b>A null <see cref="OrganizationUnitId"/> is the tenant-wide default</b>, not a missing
/// value. Resolution is: the row for the employee's unit, else the tenant default, else nobody —
/// and "nobody" is a supported outcome, not a failure. A case in a unit with no row is still
/// filed; it simply arrives unassigned for HR to route by hand, exactly as every case did before
/// this table existed.</para>
///
/// <para><b>The effective window is for acting arrangements</b> — somebody covering while the usual
/// responder is on leave. Overlapping windows for one slot are refused by the service, because two
/// people answering the same rung for the same unit on the same day is not a policy, it is a bug
/// that would resolve arbitrarily.</para>
/// </remarks>
public class EmployeeRelationsResponder : TenantEntity
{
    /// <summary>Null means the tenant-wide default. See the remarks — this is a value, not a gap.</summary>
    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    /// <summary>The rung this person answers.</summary>
    public GrievanceEscalationLevel Level { get; set; }

    [Required]
    public Guid ResponderEmployeeId { get; set; }

    [ForeignKey(nameof(ResponderEmployeeId))]
    public virtual Employee ResponderEmployee { get; set; } = null!;

    /// <summary>Null means "since always".</summary>
    public DateTime? EffectiveFrom { get; set; }

    /// <summary>Null means "until further notice" — at most one such row per slot.</summary>
    public DateTime? EffectiveTo { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// A meeting convened on an employee-relations case — area 9c slice 4, decision D-8. Covers a case
/// conference, a mediation and FR-HR-181 obligation 6's union consultation.
/// </summary>
/// <remarks>
/// <para><b>One entity, three uses.</b> All three are a meeting convened on a date, at a place,
/// chaired by somebody, attended by named people, producing notes and an outcome; what differs is
/// why it was called, which is <see cref="ConferenceType"/>. Three tables would have been the same
/// columns three times over.</para>
///
/// <para><b><see cref="Notes"/> is redacted on read, and that is the point of the field.</b> A
/// mediation's notes record what the OTHER party said in a room they were promised was private, and
/// a union consultation's record what the union said about a member. The case's read rule admits the
/// griever — rightly — so without this the complainant would receive the respondent's position
/// verbatim. HR and the chair see the notes; everyone else who may read the case sees the meeting,
/// its type, date, venue, attendees and <see cref="Outcome"/>, and a flag saying notes exist. Same
/// field-level shape as area 7's author-only mentoring notes: enforced on READ, not by hiding a
/// button.</para>
///
/// <para><b>Held is a gate, not a flag.</b> <see cref="HeldDate"/> is only set through the hold
/// path, which refuses to run without an outcome — a meeting recorded as held with nothing in it
/// tells the rungs above that a step was taken when nothing is known about it.</para>
/// </remarks>
public class StaffGrievanceConference : TenantEntity
{
    [Required]
    public Guid GrievanceId { get; set; }

    [ForeignKey(nameof(GrievanceId))]
    public virtual StaffGrievance Grievance { get; set; } = null!;

    public GrievanceConferenceType ConferenceType { get; set; }

    public GrievanceConferenceStatus Status { get; set; } = GrievanceConferenceStatus.Scheduled;

    public DateTime ScheduledFor { get; set; }

    [MaxLength(300)]
    public string? Venue { get; set; }

    /// <summary>Who chairs it. Internal or external — a mediator is very often neither party's colleague.</summary>
    public Guid? ChairId { get; set; }

    [ForeignKey(nameof(ChairId))]
    public virtual Employee? Chair { get; set; }

    [MaxLength(200)]
    public string? ExternalChairName { get; set; }

    [MaxLength(200)]
    public string? ExternalChairOrganisation { get; set; }

    /// <summary>
    /// The union consulted. Required when <see cref="ConferenceType"/> is
    /// <see cref="GrievanceConferenceType.UnionConsultation"/>, refused otherwise.
    /// </summary>
    public Guid? UnionId { get; set; }

    [ForeignKey(nameof(UnionId))]
    public virtual Union? Union { get; set; }

    /// <summary>Why it was convened. Visible to anyone who may read the case.</summary>
    [MaxLength(1000)]
    public string? Purpose { get; set; }

    /// <summary>⚠ Redacted on read to HR and the chair. See the remarks — this is not a UI concern.</summary>
    [MaxLength(6000)]
    public string? Notes { get; set; }

    /// <summary>What the meeting concluded. Visible to anyone who may read the case.</summary>
    [MaxLength(4000)]
    public string? Outcome { get; set; }

    public DateTime? HeldDate { get; set; }

    public DateTime? CancelledDate { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    public Guid? ConvenedById { get; set; }

    [ForeignKey(nameof(ConvenedById))]
    public virtual Employee? ConvenedBy { get; set; }

    public virtual ICollection<StaffGrievanceConferenceAttendee> Attendees { get; set; }
        = new List<StaffGrievanceConferenceAttendee>();
}

/// <summary>Somebody asked to a conference — area 9c slice 4.</summary>
/// <remarks>
/// Internal or external, for the same reason a party may be: the union official and the mediator are
/// the two people most likely to be at the meeting and least likely to be on the payroll.
///
/// <para><see cref="DidAttend"/> is deliberately <b>nullable</b>. Null means the meeting has not
/// happened yet or attendance was never recorded; <c>false</c> means they were asked and did not
/// come, which is a different fact and one that matters when a grievance turns on whether somebody
/// was given a hearing.</para>
/// </remarks>
public class StaffGrievanceConferenceAttendee : TenantEntity
{
    [Required]
    public Guid ConferenceId { get; set; }

    [ForeignKey(nameof(ConferenceId))]
    public virtual StaffGrievanceConference Conference { get; set; } = null!;

    public Guid? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    [MaxLength(200)]
    public string? ExternalName { get; set; }

    [MaxLength(200)]
    public string? ExternalOrganisation { get; set; }

    /// <summary>What they are there as — "Complainant", "Union representative", "Note taker".</summary>
    [MaxLength(200)]
    public string? Capacity { get; set; }

    /// <summary>Null until recorded. See the remarks — false is a fact, not a default.</summary>
    public bool? DidAttend { get; set; }

    [MaxLength(500)]
    public string? ApologyReason { get; set; }
}

/// <summary>
/// A document on an employee-relations case — area 9c slice 3, and FR-HR-181 obligation 9.
/// </summary>
/// <remarks>
/// <para><b>Before slice 3 a grievance had no document surface at all.</b> Not a broken one: none.
/// The complaint as filed on paper, the evidence an investigation gathered, and the final signed
/// agreement the requirement names explicitly all had nowhere to live.</para>
///
/// <para><b>Everything here goes through the controlled upload gate</b> — scan, central-DMS
/// registration, row write, rollback if that write fails — under its own
/// <c>hr-grievance-documents</c> category. ⚠ <see cref="FilePath"/> is a stored location and
/// <b>never a URL</b>: the files live outside the web root and reading one needs the bearer token,
/// so an <c>&lt;a href&gt;</c> cannot work. The shape this replaces elsewhere in the port — a create
/// endpoint taking <c>fileName</c> and <c>filePath</c> as JSON and storing no file at all — must
/// never appear on this table.</para>
///
/// <para><b>Scope is enforced by the service, not by the shape.</b> A <c>Step</c> document carries
/// a <see cref="StepId"/> and an <c>Investigation</c> one does not, and neither the database nor the
/// DTO can express "exactly the right one for this scope".</para>
/// </remarks>
public class StaffGrievanceDocument : TenantEntity
{
    [Required]
    public Guid GrievanceId { get; set; }

    [ForeignKey(nameof(GrievanceId))]
    public virtual StaffGrievance Grievance { get; set; } = null!;

    public GrievanceDocumentScope Scope { get; set; } = GrievanceDocumentScope.Case;

    /// <summary>Set only when <see cref="Scope"/> is <c>Step</c>.</summary>
    public Guid? StepId { get; set; }

    [ForeignKey(nameof(StepId))]
    public virtual StaffGrievanceStep? Step { get; set; }

    /// <summary>Set only when <see cref="Scope"/> is <c>Conference</c> — area 9c slice 4.</summary>
    public Guid? ConferenceId { get; set; }

    [ForeignKey(nameof(ConferenceId))]
    public virtual StaffGrievanceConference? Conference { get; set; }

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>⚠ A stored location, never a URL. See the remarks.</summary>
    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    /// <summary>The scanned controlled upload backing this document.</summary>
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
/// Somebody involved in an employee-relations case besides the primary party — area 9c slice 1,
/// decision D-7.
/// </summary>
/// <remarks>
/// <para><b>Why the party may be external.</b> Representation at a grievance is very often by a
/// union official or a lawyer who is not on the payroll, and FR-HR-181 requires union consultation
/// be retained. Modelling every party as an <c>Employee</c> FK would have made the commonest real
/// representative unrecordable, so exactly one of <see cref="EmployeeId"/> and
/// <see cref="ExternalName"/> is supplied and the service refuses both or neither.</para>
///
/// <para><b>⚠ Being a party is not by itself a right to read the case.</b> The read rule set in
/// area 9 slice 7 — the primary party, HR, or somebody named on a step — is deliberately narrower
/// than "anyone involved", because a case is usually ABOUT somebody and the respondent must not be
/// handed the complainant's statement by being added to it. Slice 1 does not widen it. What a
/// respondent is owed is a matter of natural justice handled by the disclosure the process makes,
/// not by a row in this table.</para>
/// </remarks>
public class StaffGrievanceParty : TenantEntity
{
    [Required]
    public Guid GrievanceId { get; set; }

    [ForeignKey(nameof(GrievanceId))]
    public virtual StaffGrievance Grievance { get; set; } = null!;

    public GrievancePartyRole Role { get; set; }

    /// <summary>Set when the party is a member of staff. Mutually exclusive with <see cref="ExternalName"/>.</summary>
    public Guid? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    /// <summary>Set when the party is not a member of staff — a union official, a lawyer.</summary>
    [MaxLength(200)]
    public string? ExternalName { get; set; }

    /// <summary>The body an external party comes from, when it is not the union named below.</summary>
    [MaxLength(200)]
    public string? ExternalOrganisation { get; set; }

    /// <summary>
    /// Whom this party acts for. Set for a representative or union representative; null everywhere
    /// else. Points at an employee rather than at another party row, because the person most often
    /// represented is the primary party, who has no party row of their own.
    /// </summary>
    public Guid? RepresentsEmployeeId { get; set; }

    [ForeignKey(nameof(RepresentsEmployeeId))]
    public virtual Employee? RepresentsEmployee { get; set; }

    /// <summary>The recognised union a union representative acts for. FR-HR-181's union thread.</summary>
    public Guid? UnionId { get; set; }

    [ForeignKey(nameof(UnionId))]
    public virtual Union? Union { get; set; }

    public DateTime AddedDate { get; set; }

    /// <summary>Who added them, from their own token — HR, in every path that exists today.</summary>
    public Guid? AddedById { get; set; }

    [ForeignKey(nameof(AddedById))]
    public virtual Employee? AddedBy { get; set; }

    /// <summary>Why they are on the case. Not the substance of the case — that lives on the steps.</summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Cleared when a party stands down — a representative replaced, a witness withdrawn. Kept as a
    /// date rather than a delete so the case file still reads correctly for the period they acted.
    /// </summary>
    public DateTime? RemovedDate { get; set; }

    [MaxLength(500)]
    public string? RemovalReason { get; set; }
}

/// <summary>
/// One rung of the ladder: who was asked, what they said, and what happened next.
/// </summary>
/// <remarks>
/// A step is created when the grievance ARRIVES at a rung, not when it is answered — so an unanswered
/// step is the record of who currently owes a response, and the ladder reads as a history even while
/// it is still running. Steps are append-only: escalating does not amend the level below, because
/// FR-HR-181 requires each level's response be retained.
/// </remarks>
public class StaffGrievanceStep : TenantEntity
{
    [Required]
    public Guid GrievanceId { get; set; }

    [ForeignKey(nameof(GrievanceId))]
    public virtual StaffGrievance Grievance { get; set; } = null!;

    public GrievanceEscalationLevel Level { get; set; }

    /// <summary>Order within the grievance, so the trail reads correctly even if two share a level.</summary>
    public int Sequence { get; set; }

    public DateTime ReachedDate { get; set; }

    /// <summary>
    /// Optional. HR may name who should answer at this rung — which is how a supervisor or head of
    /// department participates without the system having to derive who they are. Naming someone also
    /// lets them see and answer this grievance; nobody else outside HR can.
    /// </summary>
    public Guid? AssignedToId { get; set; }

    [ForeignKey(nameof(AssignedToId))]
    public virtual Employee? AssignedTo { get; set; }

    [MaxLength(4000)]
    public string? Response { get; set; }

    public DateTime? RespondedDate { get; set; }

    /// <summary>Whoever actually answered, from their own token.</summary>
    public Guid? RespondedById { get; set; }

    [ForeignKey(nameof(RespondedById))]
    public virtual Employee? RespondedBy { get; set; }

    public GrievanceStepOutcome Outcome { get; set; } = GrievanceStepOutcome.AwaitingResponse;
}

// =============================================================================
// REMINDER ENGINE (area 9 slice 8)
//
// The area computes a great many queues and tells nobody about any of them: the
// 48-hour written query, the four-week investigation, hearings coming up, the
// five-working-day appeal window, appeals past their ten working days, overdue
// corrective actions, expiring warnings, unpaid fines, and grievances sitting at a
// rung nobody has answered. Every one of those is a deadline visible only to
// someone who happens to open the right screen on the right day — and in this area
// a missed deadline is not an inconvenience, it is the thing that makes a sanction
// or a dismissal indefensible.
//
// Structure mirrors the staff-movement engine (area 8 slice 5), which mirrors SHE's
// (area 10 slice 13): all logic in the service so the daily host and the HR-gated
// run-now endpoint share exactly one code path, and a dispatch log whose unique
// (TenantId, DedupeKey) index is the send-once guarantee.
//
// They live in this file rather than the discipline entities file because the sweep
// covers BOTH halves of the area — disciplinary cases and grievances — and putting
// it with the narrower of the two would misdescribe it.
// =============================================================================

/// <summary>One execution of the discipline reminder sweep, scheduled or run by hand.</summary>
public class DisciplineReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" (background service) or "Manual" (run-now endpoint).</summary>
    [MaxLength(20)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<DisciplineReminderDispatchLog> DispatchLogs { get; set; }
        = new List<DisciplineReminderDispatchLog>();
}

/// <summary>
/// One reminder actually dispatched by a sweep.
/// </summary>
/// <remarks>
/// The unique (TenantId, DedupeKey) index is the send-once guarantee: a key encodes the item, the
/// reminder kind, the due date and the ladder rung or escalation tier reached, so each rung fires
/// exactly once — and moving a due date re-arms the ladder, because it produces fresh keys.
///
/// ⚠ Nothing here carries the allegation, the grievance statement, or the employee's name. A
/// reminder travels further than the record it is about — into notification lists and, one day,
/// email — so it says a case number and a deadline and makes the reader open the record to learn
/// anything else. The same reasoning governs the workflow display resolver for this area.
/// </remarks>
public class DisciplineReminderDispatchLog : TenantEntity
{
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual DisciplineReminderRun Run { get; set; } = null!;

    /// <summary>Machine kind, e.g. "WrittenQueryDue", "AppealDecisionOverdue", "GrievanceUnanswered".</summary>
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Human label for the swept item, e.g. "Disciplinary case", "Grievance".</summary>
    [MaxLength(100)]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Id of the swept record. No FK — the target table varies by kind.</summary>
    public Guid EntityId { get; set; }

    /// <summary>What the notification shows: the case or grievance number, and nothing more.</summary>
    [MaxLength(250)]
    public string Reference { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    /// <summary>Days remaining at dispatch time; negative when overdue.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>0 for a due-soon rung; 1, 2 or 3 for an overdue escalation tier.</summary>
    public int EscalationTier { get; set; }

    [Required]
    [MaxLength(300)]
    public string DedupeKey { get; set; } = string.Empty;
}
