using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

// =============================================================================
// AREA 9b — SEPARATION, CLEARANCE & EXIT
//
// FRD §A1.10 (FR-HR-090/091/092/093) and §3.A.2 "Separation & Final Settlement"
// (FR-HR-182/183/184/185).
//
// This is the ONE exit record, for every route out of the organisation. Before it, the only
// way an employee could leave was through a disciplinary case: StaffDisciplineTermination and
// StaffDisciplineSeparation both hang off a DisciplinaryActionId, so resignation, retirement,
// contract expiry and death had enum members and no route. Measured on the live tenant
// 2026-08-20: 3,693 employees, none ever terminated, and 29 disciplinary terminations whose
// employees were all still StaffStatus = Active because nothing propagated the outcome.
//
// The disciplinary route does not get its own parallel store — it creates one of these too, via
// DisciplinaryActionId. Two exit stores that can disagree is exactly how the 29 orphans happened.
// =============================================================================

/// <summary>
/// One employee leaving the organisation, by any route: the register FR-HR-090 asks for and the
/// spine the clearance run (FR-HR-183), approval (FR-HR-092) and final settlement (FR-HR-184)
/// hang off.
/// </summary>
/// <remarks>
/// <para><b>Separation type versus reason.</b> <see cref="SeparationType"/> is the route out and
/// drives behaviour — what notice applies, who signs, whether a settlement is due.
/// <see cref="ReasonCategory"/> and <see cref="ReasonNotes"/> record why, and are reporting, not
/// logic. Keeping them apart is what lets "resignation" and "resignation to avoid dismissal" be
/// the same process with different analytics.</para>
///
/// <para><b>The record is opened before it is decided.</b> A separation exists from the moment it
/// is raised — a resignation letter received, a retirement date reached, a disciplinary outcome
/// recorded — and moves through clearance, approval and settlement. That is why
/// <see cref="EffectiveDate"/> is nullable at creation: the date someone actually stops being an
/// employee is an outcome of the process, not an input to it.</para>
/// </remarks>
public class EmployeeSeparation : TenantEntity
{
    /// <summary>
    /// Human-readable reference, e.g. <c>SEP-2026-00001</c>. Assigned on create.
    /// </summary>
    /// <remarks>
    /// ⚠ Sequential and therefore enumerable — a by-number lookup walks the range and harvests who
    /// is leaving, which is precisely the information an exit register must not leak. There is
    /// deliberately <b>no</b> by-number endpoint; lookups are by id, and the number is a display
    /// and search field only.
    /// </remarks>
    [Required]
    [MaxLength(30)]
    public string SeparationNumber { get; set; } = string.Empty;

    [Required]
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    /// <summary>The route out — see FR-HR-182. Drives notice, approval and settlement.</summary>
    [Required]
    public EmployeeTerminationType SeparationType { get; set; }

    [Required]
    public SeparationStatus Status { get; set; } = SeparationStatus.Draft;

    /// <summary>Why the employee is leaving. Reporting only; <see cref="SeparationType"/> drives behaviour.</summary>
    public TerminationReason? ReasonCategory { get; set; }

    [MaxLength(2000)]
    public string? ReasonNotes { get; set; }

    // ── Dates ────────────────────────────────────────────────────────────────

    /// <summary>When the separation was raised in the system.</summary>
    [Required]
    public DateOnly InitiatedOn { get; set; }

    /// <summary>
    /// When notice was given — the employee's resignation letter, or the organisation's notice of
    /// termination. Null for routes that carry no notice: death, summary dismissal, contract expiry.
    /// </summary>
    public DateOnly? NoticeGivenOn { get; set; }

    /// <summary>
    /// Notice days that apply, defaulted from <c>CompanyHrPolicySettings</c>
    /// (<c>DefaultResignationNoticeDays</c> / <c>DefaultTerminationNoticeDays</c>, both 30) and
    /// overridable per separation. Feeds notice pay in the FR-HR-184 settlement when notice is
    /// not served.
    /// </summary>
    public int? NoticeDays { get; set; }

    /// <summary>Last day the employee actually attends work. May precede <see cref="EffectiveDate"/> where leave is taken in lieu.</summary>
    public DateOnly? LastWorkingDay { get; set; }

    /// <summary>
    /// The day employment ends. **Null until the separation is approved** — it is an outcome of the
    /// process, not an input. For compulsory retirement this is the birthday itself (FR-HR-093).
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }

    // ── Who raised it ────────────────────────────────────────────────────────

    /// <summary>
    /// The employee who raised the separation. Null when the system raised it — a retirement date
    /// reached or a contract expiring is nobody's act, and recording a person there would be a lie
    /// the audit trail cannot distinguish from a real one.
    /// </summary>
    public Guid? InitiatedById { get; set; }

    [ForeignKey(nameof(InitiatedById))]
    public virtual Employee? InitiatedBy { get; set; }

    /// <summary>True when the system raised this — the retirement or contract-expiry sweep.</summary>
    public bool IsSystemInitiated { get; set; }

    /// <summary>
    /// When the separation left Draft and entered the approval queue. Null while it is still being
    /// prepared, and the point from which its notice facts stop being editable.
    /// </summary>
    public DateTime? SubmittedOn { get; set; }

    public Guid? SubmittedById { get; set; }

    [ForeignKey(nameof(SubmittedById))]
    public virtual Employee? SubmittedBy { get; set; }

    // ── FR-HR-092: who signs ─────────────────────────────────────────────────

    /// <summary>
    /// True when this separation is <i>procedural</i> and HR may approve it without the MD's
    /// signature (FR-HR-092). Settled with the user 2026-08-20: the FRD's example — absence beyond
    /// ten days — is taken as the whole list, so everything else, resignation and retirement
    /// included, goes to the MD. Set by the service from policy, never by the client, so widening
    /// the list later is a settings change rather than a new trust boundary.
    /// </summary>
    public bool IsProcedural { get; set; }

    /// <summary>
    /// Days of unauthorised absence behind an absence-based termination. Compared against
    /// <c>CompanyHrPolicySettings.ProceduralAbsenceDays</c> to decide whether
    /// <see cref="IsProcedural"/> is true — the one exception FR-HR-092 allows to the MD's
    /// signature.
    /// </summary>
    public int? AbsenceDays { get; set; }

    public Guid? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    public DateTime? ApprovedOn { get; set; }

    [MaxLength(2000)]
    public string? ApprovalNotes { get; set; }

    public Guid? RejectedById { get; set; }

    [ForeignKey(nameof(RejectedById))]
    public virtual Employee? RejectedBy { get; set; }

    public DateTime? RejectedOn { get; set; }

    /// <summary>
    /// Why the separation was refused. Its own field rather than a reuse of
    /// <see cref="ApprovalNotes"/>: an approval and a refusal are different acts, and a record that
    /// cannot tell them apart cannot answer "was this person refused, and on what grounds".
    /// </summary>
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // ── Notice, as settled at approval ───────────────────────────────────────

    /// <summary>
    /// The organisation released the employee without requiring the balance of their notice, and
    /// is not recovering it. Decided by whoever approves the separation, never by the person
    /// leaving.
    /// </summary>
    public bool IsNoticeWaived { get; set; }

    [MaxLength(1000)]
    public string? NoticeWaiverReason { get; set; }

    /// <summary>
    /// The notice not served will be paid rather than worked — pay in lieu, which the FR-HR-184
    /// settlement adds. Mutually exclusive with <see cref="IsNoticeWaived"/>: waived notice costs
    /// nobody anything, paid-in-lieu notice is money.
    /// </summary>
    public bool IsNoticePaidInLieu { get; set; }

    /// <summary>
    /// When the notice decision was taken — and the only thing that can tell "we decided neither
    /// applies" from "nobody has decided yet".
    /// </summary>
    /// <remarks>
    /// ⚠ Two bools cannot express this. <c>IsNoticeWaived == false &amp;&amp; IsNoticePaidInLieu
    /// == false</c> is both the ordinary outcome (the notice was served, so there is nothing to
    /// settle) and the dangerous one (nobody has looked). FR-HR-184 needs the difference: a
    /// settlement prepared with an unserved notice and no decision recorded silently omits notice
    /// pay the leaver may be owed, and omission is invisible in a way a wrong figure is not.
    ///
    /// <para>Null until somebody with the authority to sign this separation records the decision.
    /// Preparing the settlement is refused while it is null AND notice was left unserved.</para>
    /// </remarks>
    public DateTime? NoticeDecisionOn { get; set; }

    /// <summary>Who took the notice decision — the same authority that may sign the separation.</summary>
    public Guid? NoticeDecidedById { get; set; }

    [ForeignKey(nameof(NoticeDecidedById))]
    public virtual Employee? NoticeDecidedBy { get; set; }

    /// <summary>Set when the approval runs through the workflow engine (slice 4).</summary>
    public Guid? WorkflowInstanceId { get; set; }

    // ── Rehire ───────────────────────────────────────────────────────────────

    public bool IsEligibleForRehire { get; set; } = true;

    public DateOnly? EligibleForRehireDate { get; set; }

    [MaxLength(1000)]
    public string? RehireRestrictions { get; set; }

    // ── The disciplinary route (D1: one pipeline, not two) ───────────────────

    /// <summary>
    /// Set when this separation came out of a disciplinary case, linking to the
    /// <c>StaffDisciplinaryAction</c> that decided it. Area 9 keeps owning the decision and the
    /// hearing record; the exit itself lives here, so the register, the clearance run and the
    /// settlement are the same for a dismissal as for a resignation.
    /// </summary>
    public Guid? DisciplinaryActionId { get; set; }

    // ── The employee master record ───────────────────────────────────────────

    /// <summary>
    /// When this separation was actually applied to the employee's master record — status,
    /// termination date, contracts and position history.
    /// </summary>
    /// <remarks>
    /// ⚠ This column exists because of a measured defect, not for tidiness. Before area 9b, 29
    /// disciplinary terminations had been recorded while all 29 employees remained
    /// <c>StaffStatus = Active</c>: the outcome was written somewhere nobody read. A null here on a
    /// completed separation is that bug, visible in one query instead of a join across three
    /// tables.
    /// </remarks>
    public DateTime? EmployeeRecordUpdatedOn { get; set; }

    // ── Cancellation ─────────────────────────────────────────────────────────

    public DateTime? CancelledOn { get; set; }

    public Guid? CancelledById { get; set; }

    [ForeignKey(nameof(CancelledById))]
    public virtual Employee? CancelledBy { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    public virtual ICollection<EmployeeSeparationDocument> Documents { get; set; }
        = new List<EmployeeSeparationDocument>();

    public virtual ICollection<SeparationClearanceItem> ClearanceItems { get; set; }
        = new List<SeparationClearanceItem>();
}

/// <summary>
/// A file attached to a separation — the resignation letter, the signed clearance form, the
/// settlement statement, a medical report, a death certificate.
/// </summary>
/// <remarks>
/// <para>One table for the whole lifecycle rather than a column per stage. The alternative —
/// adding <c>ResignationLetterPath</c> in one slice and <c>ClearanceFormPath</c> in the next — ends
/// with a wide row of nullable paths that cannot hold two versions of anything and cannot say who
/// attached what.</para>
///
/// <para>Every file arrives through the controlled-upload gate
/// (<c>ControlledFileUploadCategories.HrSeparationDocuments</c>), which scans it and optionally
/// registers it in the central DMS. The three id columns below are that registration; a row here
/// without a <see cref="FileUploadRecordId"/> would be a path nobody scanned.</para>
/// </remarks>
public class EmployeeSeparationDocument : TenantEntity
{
    [Required]
    public Guid SeparationId { get; set; }

    [ForeignKey(nameof(SeparationId))]
    public virtual EmployeeSeparation Separation { get; set; } = null!;

    [Required]
    public SeparationDocumentCategory Category { get; set; } = SeparationDocumentCategory.Other;

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>The scanned controlled upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime UploadedOn { get; set; }

    public Guid? UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee? UploadedBy { get; set; }
}

// =============================================================================
// CLEARANCE — FR-HR-091 (a completed clearance form before separation, and only
// then are entitlements computed) and FR-HR-183 (what the clearance runs across).
// =============================================================================

/// <summary>
/// One line of the tenant's clearance form — the catalogue every separation's checklist is built
/// from.
/// </summary>
/// <remarks>
/// <para>A catalogue rather than a fixed list in code, because what an organisation clears varies
/// and FR-HR-183 names categories, not items: "company property" is one line at one employer and
/// six at another.</para>
///
/// <para>⚠ <b><see cref="OwningOrganizationUnitId"/> is advisory, not an authority.</b> Measured on
/// the live tenant 2026-08-20: <b>41 organisation units, 0 of them with a head</b>
/// (<c>HeadEmployeeId</c> is null throughout). Deriving "who signs this item" from the unit head
/// would resolve to nobody for every line of every form. So the unit here routes and labels; the
/// signing is done by HR walking the form round, recording each unit's answer and who gave it.
/// That is also what actually happens on paper.</para>
/// </remarks>
public class SeparationClearanceTemplate : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ClearanceItemKind Kind { get; set; } = ClearanceItemKind.Other;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Which organisation unit answers this line. Advisory — see the remarks on this class.
    /// </summary>
    /// <remarks>
    /// <c>OrganizationUnit</c>, not <c>Department</c>: the organisation unit is the HR module's
    /// placement entity, and it is also the one the data supports — 3,810 of 3,834 employees carry
    /// an <c>OrganizationUnitId</c> against 3,320 with a <c>DepartmentId</c>, and the seven
    /// departments on this tenant belong to the estate and procurement modules rather than to HR.
    /// </remarks>
    public Guid? OwningOrganizationUnitId { get; set; }

    [ForeignKey(nameof(OwningOrganizationUnitId))]
    public virtual OrganizationUnit? OwningOrganizationUnit { get; set; }

    /// <summary>
    /// A mandatory item must be Cleared, Waived or Not Applicable before the FR-HR-091 gate opens.
    /// A non-mandatory one is recorded but never blocks.
    /// </summary>
    public bool IsMandatory { get; set; } = true;

    public bool IsActive { get; set; } = true;

    /// <summary>Order the line appears on the form.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// One line of one employee's clearance form.
/// </summary>
/// <remarks>
/// The name, kind, organisation unit and mandatory flag are <b>snapshotted</b> from the template
/// rather than read through it. A clearance form is evidence about a particular exit; editing the
/// catalogue afterwards must not rewrite what somebody signed.
/// </remarks>
public class SeparationClearanceItem : TenantEntity
{
    [Required]
    public Guid SeparationId { get; set; }

    [ForeignKey(nameof(SeparationId))]
    public virtual EmployeeSeparation Separation { get; set; } = null!;

    /// <summary>The catalogue line this came from, where it came from one at all.</summary>
    public Guid? TemplateId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ClearanceItemKind Kind { get; set; } = ClearanceItemKind.Other;

    public Guid? OwningOrganizationUnitId { get; set; }

    [ForeignKey(nameof(OwningOrganizationUnitId))]
    public virtual OrganizationUnit? OwningOrganizationUnit { get; set; }

    public bool IsMandatory { get; set; } = true;

    public int SortOrder { get; set; }

    [Required]
    public ClearanceItemStatus Status { get; set; } = ClearanceItemStatus.Pending;

    /// <summary>
    /// What is still owed or unreturned, in money, where the item is the kind that carries an
    /// amount — a loan, an advance, a payroll recovery.
    /// </summary>
    /// <remarks>
    /// ⚠ This is an <b>input to the FR-HR-184 settlement</b>, not decoration: an amount recorded
    /// here against a Blocked or Waived item is what the settlement deducts. It stays null for
    /// items that cannot carry money — nobody owes a quantity of duty-post keys.
    /// </remarks>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? OutstandingAmount { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Who in the owning unit actually gave the answer, as recorded on the form. Free text on
    /// purpose: the person signing a clearance line is often not an ERP user at all, and a nullable
    /// FK to <c>Employee</c> would quietly lose them.
    /// </summary>
    [MaxLength(200)]
    public string? SignedOffBy { get; set; }

    /// <summary>The HR user who recorded the answer. Not the same person as the signatory.</summary>
    public Guid? RecordedById { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public virtual Employee? RecordedBy { get; set; }

    public DateTime? RecordedOn { get; set; }
}

// =============================================================================
// FINAL SETTLEMENT — FR-HR-184: "unpaid salary, notice pay, leave encashment,
// benefits, deductions, recoveries, loans and pension-related payments".
// Reviewed by Internal Audit before payment is released (FR-HR-185, slice 6).
// =============================================================================

/// <summary>
/// What an employee is owed, and what is owed back, when they leave. One per separation.
/// </summary>
/// <remarks>
/// <para><b>The statement is assembled, not calculated in one shot.</b> Lines arrive from three
/// places — computed by the system, carried from the clearance form and the employee's travel
/// advances, or entered by hand — and each says which it was. That is the whole design, and it
/// comes from a measurement: 202 of 3,883 employees have a salary on file and there are no leave
/// balances at all, so a settlement that only computed would be almost entirely zeros.</para>
///
/// <para><b>Totals are derived from the lines, never stored.</b> Once the statement is finalised
/// its lines are immutable, so a derived total is a frozen total — without the risk that a stored
/// copy quietly disagrees with the lines under it.</para>
///
/// <para>⚠ <b>Nothing here posts to the general ledger.</b> Per the standing HR↔Finance split every
/// money event is registered in <c>docs/HR-FINANCE-INTEGRATION-BACKLOG.md</c> and posted in one
/// sweep after the whole HR module. This records what is payable; Finance pays it.</para>
/// </remarks>
public class SeparationSettlement : TenantEntity
{
    [Required]
    public Guid SeparationId { get; set; }

    [ForeignKey(nameof(SeparationId))]
    public virtual EmployeeSeparation Separation { get; set; } = null!;

    /// <summary>
    /// The currency the settlement is stated in.
    /// </summary>
    /// <remarks>
    /// <para><b>Finance owns this, not HR.</b> Set from Finance's <i>base currency</i> when the
    /// statement is prepared, and any override is validated against Finance's currency master —
    /// the same read-only relationship <c>StaffTravelCurrencyBridge</c> established for travel,
    /// and for the same reason: travel had shipped its own rate table beside Finance's, so a trip
    /// could be worth one thing on a travel screen and another on a financial report. A settlement
    /// must not be able to be stated in a currency Finance does not hold.</para>
    ///
    /// <para>Deliberately <b>no hardcoded default</b>. It was <c>"GHS"</c> here until the question
    /// was asked, which is correct on this tenant and a lie on any other. A code rather than a
    /// foreign key, because Finance's uniqueness is <c>(TenantId, Code)</c> and a composite FK
    /// would make re-coding a currency a schema problem.</para>
    /// </remarks>
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// The daily rate every computed line was worked out from, snapshotted at preparation.
    /// Null when no salary could be found — which is what makes those lines
    /// <c>CannotCompute</c> rather than zero.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? DailyRate { get; set; }

    /// <summary>
    /// Where the rate came from and how it was derived, in words, so the figure can be argued with
    /// rather than merely believed.
    /// </summary>
    [MaxLength(500)]
    public string? DailyRateBasis { get; set; }

    public Guid? PreparedById { get; set; }

    [ForeignKey(nameof(PreparedById))]
    public virtual Employee? PreparedBy { get; set; }

    public DateTime? PreparedOn { get; set; }

    /// <summary>
    /// When the statement was closed for review. After this its lines cannot change — it is the
    /// document Internal Audit signs off (FR-HR-185).
    /// </summary>
    public DateTime? FinalisedOn { get; set; }

    public Guid? FinalisedById { get; set; }

    [ForeignKey(nameof(FinalisedById))]
    public virtual Employee? FinalisedBy { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // ── FR-HR-185: Internal Audit's review, before payment is released ───────

    [Required]
    public SettlementReviewOutcome ReviewOutcome { get; set; } = SettlementReviewOutcome.NotReviewed;

    public Guid? ReviewedById { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? ReviewedBy { get; set; }

    public DateTime? ReviewedOn { get; set; }

    /// <summary>
    /// Internal Audit's findings. Required when a statement is returned — a control that can refuse
    /// without saying why leaves HR guessing at what to correct, and the statement would come back
    /// unchanged.
    /// </summary>
    [MaxLength(2000)]
    public string? ReviewNotes { get; set; }

    /// <summary>
    /// How many times this statement has been returned by Internal Audit. Kept because
    /// <see cref="FinalisedOn"/> is overwritten on each re-finalisation, and "this was queried
    /// three times before it was paid" is exactly what an auditor asks later.
    /// </summary>
    public int ReturnCount { get; set; }

    public virtual ICollection<SeparationSettlementLine> Lines { get; set; }
        = new List<SeparationSettlementLine>();
}

/// <summary>One line of a final settlement — an amount owed to the employee, or back to the employer.</summary>
public class SeparationSettlementLine : TenantEntity
{
    [Required]
    public Guid SettlementId { get; set; }

    [ForeignKey(nameof(SettlementId))]
    public virtual SeparationSettlement Settlement { get; set; } = null!;

    [Required]
    public SettlementLineCategory Category { get; set; }

    /// <summary>
    /// True when the amount comes off what is payable. Held explicitly rather than inferred from
    /// the category: a benefit can be a payment or a clawback, and a statement that guesses which
    /// gets the sign wrong on somebody's money.
    /// </summary>
    public bool IsDeduction { get; set; }

    [Required]
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The amount. <b>Null where <see cref="Computation"/> is <c>CannotCompute</c></b> — null is
    /// the honest value for "unknown", and zero is not.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Amount { get; set; }

    [Required]
    public SettlementLineComputation Computation { get; set; } = SettlementLineComputation.ManuallyEntered;

    /// <summary>How the amount was arrived at, or why it could not be.</summary>
    [MaxLength(500)]
    public string? Basis { get; set; }

    /// <summary>For a hand-entered figure: where it came from. Required for manual amounts.</summary>
    [MaxLength(300)]
    public string? SourceReference { get; set; }

    /// <summary>
    /// The clearance line this deduction was carried from, where it was. Provenance, not ownership
    /// — no foreign key, so tidying clearance later cannot orphan a signed settlement.
    /// </summary>
    public Guid? SourceClearanceItemId { get; set; }

    /// <summary>The travel advance this recovery came from, where it did. Provenance, as above.</summary>
    public Guid? SourceTravelAdvanceId { get; set; }

    /// <summary>
    /// True when the system put this line here. Distinguishes a line HR must justify from one the
    /// system will regenerate — and stops a regeneration silently deleting somebody's manual entry.
    /// </summary>
    public bool IsSystemGenerated { get; set; }

    public int SortOrder { get; set; }
}

// =============================================================================
// THE EXIT INTERVIEW (slice 12)
//
// ⚠ Not an FRD requirement — "exit interview" appears nowhere in the specification. Added at the
// user's explicit request, and it is a real part of an exit process regardless: without it the
// only record of why somebody left is the reason the ORGANISATION wrote down.
// =============================================================================

/// <summary>
/// What the leaver said on the way out. One per separation.
/// </summary>
/// <remarks>
/// <para><b>Declining is a first-class outcome.</b> People leave angry, or in a hurry, or under
/// notice they did not want — plenty never sit down for this. A record that can only express a
/// completed interview forces whoever is keeping it either to invent one or to leave the file
/// blank, and both are worse than "they were asked and said no".</para>
///
/// <para><b>The interviewer may not be an ERP user.</b> A departmental head or an external HR
/// consultant often conducts these, so the name is free text alongside the optional employee link —
/// the same shape the clearance form uses for its signatories, and for the same reason.</para>
///
/// <para><b>Ratings are 1–5 and nullable.</b> Null means not asked; zero would mean "the worst
/// possible answer", and a half-finished form must not read as a damning one.</para>
/// </remarks>
public class SeparationExitInterview : TenantEntity
{
    [Required]
    public Guid SeparationId { get; set; }

    [ForeignKey(nameof(SeparationId))]
    public virtual EmployeeSeparation Separation { get; set; } = null!;

    /// <summary>
    /// True where the employee was offered an interview and declined, or could not be reached. The
    /// rest of the record is then empty by design rather than by omission.
    /// </summary>
    public bool WasDeclined { get; set; }

    [MaxLength(500)]
    public string? DeclinedReason { get; set; }

    public DateOnly? ConductedOn { get; set; }

    /// <summary>The interviewer, where they are an employee of this organisation.</summary>
    public Guid? ConductedById { get; set; }

    [ForeignKey(nameof(ConductedById))]
    public virtual Employee? ConductedBy { get; set; }

    /// <summary>Who conducted it, as written on the form. Free text — see the remarks.</summary>
    [MaxLength(200)]
    public string? ConductedByName { get; set; }

    /// <summary>
    /// What the LEAVER says was behind it — which is not the same as the termination reason the
    /// organisation recorded. That difference is the point of asking.
    /// </summary>
    public ExitInterviewReason? PrimaryReason { get; set; }

    [MaxLength(2000)]
    public string? PrimaryReasonDetail { get; set; }

    // ── Ratings, 1–5. Null means not asked, never "nought out of five". ──────

    /// <summary>Their experience of working here overall.</summary>
    [Range(1, 5)]
    public int? OverallExperienceRating { get; set; }

    /// <summary>How they were managed and supervised.</summary>
    [Range(1, 5)]
    public int? ManagementRating { get; set; }

    /// <summary>Pay and benefits against what the work asked of them.</summary>
    [Range(1, 5)]
    public int? PayAndBenefitsRating { get; set; }

    /// <summary>Whether the job gave them somewhere to go.</summary>
    [Range(1, 5)]
    public int? CareerDevelopmentRating { get; set; }

    /// <summary>Would they recommend this employer to somebody else. Null where not asked.</summary>
    public bool? WouldRecommendEmployer { get; set; }

    /// <summary>
    /// Would they consider coming back. Deliberately distinct from
    /// <c>EmployeeSeparation.IsEligibleForRehire</c>: that is whether the ORGANISATION would have
    /// them, this is whether THEY would return, and a record that conflates the two answers neither.
    /// </summary>
    public bool? WouldConsiderReturning { get; set; }

    [MaxLength(2000)]
    public string? WhatWorkedWell { get; set; }

    [MaxLength(2000)]
    public string? WhatShouldChange { get; set; }

    [MaxLength(2000)]
    public string? AdditionalComments { get; set; }

    public Guid? RecordedById { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public virtual Employee? RecordedBy { get; set; }

    public DateTime RecordedOn { get; set; }
}
