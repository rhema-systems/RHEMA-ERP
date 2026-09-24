using System.ComponentModel;

namespace ErpSystem.Core.Enums;

#region Employee Details

/// <summary>
/// Gender classification for employees
/// </summary>
public enum Gender
{
    /// <summary>
    /// Male
    /// </summary>
    Male = 1,

    /// <summary>
    /// Female
    /// </summary>
    Female = 2,

    /// <summary>
    /// Other/Non-binary
    /// </summary>
    Other = 3,

    /// <summary>
    /// Prefer not to say
    /// </summary>
    PreferNotToSay = 4
}

/// <summary>
/// Marital status classification
/// </summary>
public enum MaritalStatus
{
    /// <summary>
    /// Single
    /// </summary>
    Single = 1,

    /// <summary>
    /// Married
    /// </summary>
    Married = 2,

    /// <summary>
    /// Divorced
    /// </summary>
    Divorced = 3,

    /// <summary>
    /// Widowed
    /// </summary>
    Widowed = 4,

    /// <summary>
    /// Separated
    /// </summary>
    Separated = 5,

    /// <summary>
    /// Other
    /// </summary>
    Other = 6
}

/// <summary>
/// Employee employment status
/// </summary>
public enum StaffStatus
{
    /// <summary>
    /// Active employee
    /// </summary>
    Active = 1,

    /// <summary>
    /// Inactive employee
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// Employee on probation
    /// </summary>
    Probation = 3,

    /// <summary>
    /// Suspended employee
    /// </summary>
    Suspended = 4,

    /// <summary>
    /// Terminated employee
    /// </summary>
    Terminated = 5,

    /// <summary>
    /// Retired employee
    /// </summary>
    Retired = 6,

    /// <summary>
    /// Employee on leave
    /// </summary>
    OnLeave = 7
}

/// <summary>
/// Work arrangement / schedule types
/// </summary>
public enum WorkArrangementType
{
    FullTime = 1,
    PartTime = 2,
    Shift = 3,
    Flexi = 4,
    Remote = 5,
    Hybrid = 6
}

/// <summary>
/// Blood type classification
/// </summary>
public enum BloodType
{
    /// <summary>
    /// A positive
    /// </summary>
    APositive = 1,

    /// <summary>
    /// A negative
    /// </summary>
    ANegative = 2,

    /// <summary>
    /// B positive
    /// </summary>
    BPositive = 3,

    /// <summary>
    /// B negative
    /// </summary>
    BNegative = 4,

    /// <summary>
    /// AB positive
    /// </summary>
    ABPositive = 5,

    /// <summary>
    /// AB negative
    /// </summary>
    ABNegative = 6,

    /// <summary>
    /// O positive
    /// </summary>
    OPositive = 7,

    /// <summary>
    /// O negative
    /// </summary>
    ONegative = 8,

    /// <summary>
    /// Unknown blood type
    /// </summary>
    Unknown = 9
}

/// <summary>
/// Employee skill levels
/// </summary>
public enum SkillLevel
{
    /// <summary>
    /// Beginner level
    /// </summary>
    Beginner = 1,

    /// <summary>
    /// Intermediate level
    /// </summary>
    Intermediate = 2,

    /// <summary>
    /// Advanced level
    /// </summary>
    Advanced = 3,

    /// <summary>
    /// Expert level
    /// </summary>
    Expert = 4,

    /// <summary>
    /// Master level
    /// </summary>
    Master = 5
}

public enum SkillCategory
{
    Technical = 1,
    SoftSkill = 2,
    Leadership = 3,
    Domain = 4,
    Language = 5,
    Certification = 6,
    Tool = 7
}

/// <summary>
/// Types of employee emergency contact
/// </summary>
public enum EmergencyContactType
{
    EmergencyContact = 1,

    NextOfKin = 2,

    Both = 3
}

/// <summary>
/// Types of employee contract status
/// </summary>
public enum ContractStatus
{
    Active = 1,

    Expired = 2,

    Terminated = 3
}

/// <summary>
/// Where an employee's probation term came from, recorded so a screen can say it rather than
/// present a number with no provenance.
/// </summary>
/// <remarks>
/// <para>⚠ Null on every row that predates lane D1 (2026-09-09), and that is the honest answer:
/// those terms were typed into a free field and nobody knows whether they agreed with the post.
/// A default of <see cref="Position"/> would have claimed a provenance the data does not have.</para>
///
/// <para>The term itself is resolved the way <c>ProbationService.BuildPolicy</c> already resolved
/// it — the position first, the company policy default second — so this records a decision the
/// system was already making silently.</para>
/// </remarks>
public enum ProbationSource
{
    /// <summary>Taken from <c>EmployeePosition.ProbationPeriodMonths</c> — the normal case.</summary>
    Position = 1,

    /// <summary>The position is silent, so <c>CompanyHrPolicySettings.DefaultProbationMonths</c> applied.</summary>
    PolicyDefault = 2,

    /// <summary>Neither: a length was supplied for this person, against a silent position.</summary>
    Override = 3
}

/// <summary>
/// Which kind of tie a <c>RelationshipType</c> describes, so a screen can offer only the values
/// that make sense on it.
/// </summary>
/// <remarks>
/// <para>The demo feedback asked for this in as many words (register row E-11b): "differentiate
/// familial from professional relationships, to know which values to populate". A next-of-kin
/// dropdown offering "Former manager" and a referee dropdown offering "Nephew" are the same
/// defect — a list that is technically complete and practically useless.</para>
///
/// <para><b>Other</b> is not a dumping ground. It is the tie that is neither blood nor work —
/// family friend, landlord, pastor — and both the next-of-kin and the referee screens accept it,
/// which is the whole reason it is a third value rather than a null.</para>
///
/// <para>Stored as an int; members are APPENDED and never renumbered.</para>
/// </remarks>
public enum RelationshipCategory
{
    Familial = 1,

    Professional = 2,

    Other = 3
}

public enum DependentRelationship
{
    Spouse = 1,

    Son = 2,

    Daughter = 3,

    Mother = 4,

    Father = 5,

    Brother = 6,

    Sister = 7,

    Uncle = 8,

    Aunt = 9,

    Nephew = 10,

    Niece = 11,

    Grandfather = 12,

    Grandmother = 13,

    Other = 14
}

public enum PayFrequency
{
    Weekly = 1,
    BiWeekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Annually = 5,
    /// <summary>A single, non-recurring payment/value (e.g. one-off bonus or relocation benefit).</summary>
    OneTime = 6
}

public enum TaxTreatmentType
{
    None = 0,

    /// <summary>
    /// Pay As You Earn (standard employee payroll tax)
    /// </summary>
    PAYE = 1,

    /// <summary>
    /// Withholding tax (typically for contractors / consultants)
    /// </summary>
    WithholdingTax = 2
}

public enum RefereeType
{
    Professional,
    Academic,
    Personal
}

/// <summary>
/// Why an employee's position changed, as recorded on their position-history timeline.
///
/// Stored as an int with no lookup table or check constraint, so members are APPENDED and never
/// renumbered — an existing row's meaning must not shift under it.
/// </summary>
public enum PositionChangeReason
{
    InitialAssignment = 0,

    Promotion = 1,

    Demotion = 2,

    Transfer = 3,

    Restructure = 4,

    Termination = 5,

    Other = 6,

    // Added with area 8. Three of the seven staff-movement types had no reason of their own and
    // landed on Other, which loses the fact in the one place people look for it: a secondment and a
    // permanent transfer are not the same event, and a timeline that calls both "Other" cannot say
    // whether someone ever actually left their post.
    Secondment = 7,

    ActingAppointment = 8,

    Redesignation = 9
}

public enum EmployeeBankAccountType
{
    Current = 1,

    Savings = 2,

    MobileMoney = 3
}

public enum EmployeeContactType
{
    Home = 1,
    Postal = 2,
    Temporary = 3,
    Other = 4
}

#endregion Employee Details

#region Organization Structure

/// <summary>
/// Department types for organizational structure
/// </summary>
public enum DepartmentType
{
    /// <summary>
    /// Operations department
    /// </summary>
    Operations = 1,

    /// <summary>
    /// Administration department
    /// </summary>
    Administration = 2,

    /// <summary>
    /// Human Resources department
    /// </summary>
    HumanResources = 3,

    /// <summary>
    /// Finance department
    /// </summary>
    Finance = 4,

    /// <summary>
    /// Information Technology department
    /// </summary>
    IT = 5,

    /// <summary>
    /// Maintenance department
    /// </summary>
    Maintenance = 6,

    /// <summary>
    /// Safety department
    /// </summary>
    Safety = 7,

    /// <summary>
    /// Quality Assurance department
    /// </summary>
    QualityAssurance = 8,

    /// <summary>
    /// Research and Development department
    /// </summary>
    RnD = 9,

    /// <summary>
    /// Marketing department
    /// </summary>
    Marketing = 10,

    /// <summary>
    /// Sales department
    /// </summary>
    Sales = 11
}

/// <summary>
/// Types of company stations/locations
/// </summary>
public enum StationType
{
    HeadOffice = 1,
    Branch = 2,
    Warehouse = 3,
    Manufacturing = 4,
    SalesOffice = 5,
    ServiceCenter = 6,
    Other = 99
}

public enum LocationContactType
{
    Primary = 1,

    Secondary = 2,

    Emergency = 3,

    Other = 4
}

/// <summary>
/// Classification of an HR team.
/// </summary>
public enum TeamType
{
    Permanent = 1,
    Project = 2,
    TaskForce = 3,
    CrossFunctional = 4,
    Shift = 5,
    Committee = 6,
    Other = 99
}

/// <summary>
/// Lifecycle status of a team.
/// </summary>
public enum TeamStatus
{
    Draft = 1,
    Active = 2,
    Inactive = 3,
    Dissolved = 4
}

/// <summary>
/// Role of an employee within a team.
/// </summary>
public enum TeamMemberRole
{
    Member = 1,
    DeputyLead = 2,
    TeamLead = 3,
    Coordinator = 4,
    Secretary = 5
}

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  Teams and committees — the activity sub-module (round 2, lane F; plan § 1.4, § 6.6).
//
//  ⚠ These sit on the EXISTING Team record, whose TeamType already distinguishes a Committee from
//  a project team. SafetyCommittee and AwardCommittee stay separate on purpose: they are bounded
//  contexts with their own rules (meeting quorum, scoring), and merging them would break two
//  closed areas.
//
//  Every member is APPENDED and never renumbered — an existing row's meaning must not shift.
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Where a team's terms of reference are in their life.
/// </summary>
/// <remarks>
/// ⚠ <see cref="Approved"/> is IMMUTABLE. Changing approved terms means a new version — the row is
/// cloned to <c>Draft</c> at version + 1 and the old one becomes <see cref="Superseded"/> when the
/// new one is approved. Editing in place would rewrite what a committee was actually chartered to
/// do, which is the one thing terms of reference exist to record.
/// </remarks>
public enum TeamTorStatus
{
    Draft = 1,

    PendingApproval = 2,

    Approved = 3,

    Superseded = 4
}

/// <summary>What a team has undertaken to achieve, and where it has got to.</summary>
public enum TeamObjectiveStatus
{
    Draft = 1,

    PendingApproval = 2,

    Active = 3,

    OnHold = 4,

    Completed = 5,

    Cancelled = 6
}

/// <summary>
/// Whether an objective's progress is counted from its tasks or typed by hand.
/// </summary>
/// <remarks>
/// ⚠ <see cref="FromTasks"/> makes <c>ProgressPercent</c> a DERIVED column the service recomputes
/// on every task change — a caller cannot set it. <see cref="Manual"/> is for an objective whose
/// progress is not a count of anything ("stakeholder confidence restored"), and there the typed
/// figure is the only truth there is.
/// </remarks>
public enum TeamObjectiveProgressMode
{
    FromTasks = 1,

    Manual = 2
}

public enum TeamTaskPriority
{
    Low = 1,

    Normal = 2,

    High = 3,

    Urgent = 4
}

/// <summary>
/// Where a team task stands.
/// </summary>
/// <remarks>
/// ⚠ <see cref="Blocked"/> requires a reason. A board full of blocked cards that cannot say what
/// is blocking them tells a lead nothing, and the whole point of the monitoring half of this
/// sub-module is that the lead can see where the work has stopped.
/// </remarks>
public enum TeamTaskStatus
{
    NotStarted = 1,

    InProgress = 2,

    Blocked = 3,

    Completed = 4,

    Cancelled = 5
}

/// <summary>What kind of gathering a team record is describing.</summary>
/// <remarks>
/// ⚠ A committee's work does not all happen in a meeting. A site visit and a workshop produce
/// decisions and action items exactly as a meeting does, and filing them as "Meeting" would make
/// the minute book lie about what actually happened.
/// </remarks>
public enum TeamMeetingKind
{
    Meeting = 1,

    Workshop = 2,

    SiteVisit = 3,

    Other = 99
}

public enum TeamMeetingStatus
{
    Scheduled = 1,

    Held = 2,

    Cancelled = 3
}

/// <summary>
/// Where a periodic review of a team stands.
/// </summary>
/// <remarks>
/// ⚠ A SUBMITTED review is immutable, and the lead acknowledges it rather than approving it. A
/// review is a record of what somebody found, not a request for permission — which is why this is
/// not on the workflow engine (plan § 6.6.2). Acknowledgement says it was read, and nothing about
/// whether the lead agreed.
/// </remarks>
public enum TeamReviewStatus
{
    Draft = 1,

    Submitted = 2,

    Acknowledged = 3
}

public enum WorkMode
{
    OnSite = 1,

    Remote = 2,

    Hybrid = 3
}

#endregion Organization Structure

#region Staff Benefits

public enum BenefitRecipient
{
    Staff = 1,

    Dependent = 2,

    Both = 3
}

public enum BenefitRelationType
{
    Any = 1,

    Spouse = 2,

    Child = 3,

    Son = 4,

    Daughter = 5,

    Mother = 6,

    Father = 7,

    Parent = 8,

    Sibling = 9,

    Brother = 10,

    Sister = 11,

    Uncle = 12,

    Aunt = 13,

    Nephew = 14,

    Niece = 15,

    Grandparent = 16,

    Grandfather = 17,

    Grandmother = 18,
}

public enum BenefitPolicyType
{
    Medical = 1,

    Legal = 2,

    Educational = 3,

    Financial = 4,

    FamilyAndLifestyle = 5,

    Insurance = 6,

    Transportation = 7,

    Housing = 8,

    Technology = 9,

    LeaveAndPTO = 10,

    Other = 11
}

public enum BenefitLimitPeriod
{
    Monthly = 1,

    Annual = 2,

    Lifetime = 3
}

/// <summary>
/// How a benefit is delivered to the employee. Drives whether it produces a cash payroll line,
/// a notional (taxable-only) Benefit-in-Kind value, or neither.
/// </summary>
public enum BenefitDeliveryType
{
    /// <summary>Paid as money (becomes a cash earning on payroll).</summary>
    Cash = 1,

    /// <summary>Non-cash benefit (car, accommodation, etc.) — valued for tax but not paid out.</summary>
    InKind = 2,

    /// <summary>Employee incurs cost and is reimbursed against a limit.</summary>
    Reimbursement = 3,

    /// <summary>Employer-provided service (e.g. gym, transport) with no cash flow to the employee.</summary>
    Service = 4,

    /// <summary>Voucher / scrip with a face value.</summary>
    Voucher = 5
}

/// <summary>
/// Tax treatment of a benefit's assessed value for income tax (PAYE) purposes.
/// </summary>
public enum BenefitTaxTreatment
{
    /// <summary>The whole assessed value is added to taxable income.</summary>
    FullyTaxable = 1,

    /// <summary>Only a portion (see TaxablePercentage / exempt threshold) is taxable.</summary>
    PartiallyTaxable = 2,

    /// <summary>Not subject to income tax.</summary>
    TaxExempt = 3,

    /// <summary>Taxed under a special/concessionary statutory rule.</summary>
    ConcessionaryRate = 4
}

/// <summary>
/// How the monetary (or Benefit-in-Kind) value of a benefit is determined.
/// </summary>
public enum BenefitValuationMethod
{
    /// <summary>Actual invoiced/employer cost.</summary>
    ActualCost = 1,

    /// <summary>Open-market value of the benefit.</summary>
    MarketValue = 2,

    /// <summary>A statutory formula (e.g. GRA Benefit-in-Kind percentages) using ValuationRate/ValuationCap.</summary>
    StatutoryFormula = 3,

    /// <summary>A percentage of the employee's basic salary (ValuationRate).</summary>
    PercentageOfBasicSalary = 4,

    /// <summary>A percentage of the employee's total cash emoluments (ValuationRate).</summary>
    PercentageOfCashEmoluments = 5,

    /// <summary>A flat configured amount.</summary>
    FlatRate = 6,

    /// <summary>Resolved from the per-grade value table (<c>BenefitGradeValue</c>).</summary>
    GradeBased = 7
}

/// <summary>
/// How a benefit's contribution amounts are calculated.
/// </summary>
public enum BenefitCalculationBasis
{
    FixedAmount = 1,

    PercentageOfBasic = 2,

    PercentageOfGross = 3,

    /// <summary>Driven by the per-grade value table (<c>BenefitGradeValue</c>).</summary>
    GradeBandTable = 4
}

/// <summary>
/// Who bears the cost of a benefit.
/// </summary>
public enum BenefitContributionResponsibility
{
    EmployerPaysAll = 1,

    EmployeePaysAll = 2,

    Shared = 3,

    EmployeePaysWithSubsidy = 4
}

/// <summary>
/// Lifecycle status of an employee's enrollment in a benefit.
/// </summary>
public enum EmployeeBenefitEnrollmentStatus
{
    Draft = 1,

    PendingApproval = 2,

    Active = 3,

    Suspended = 4,

    Terminated = 5,

    Expired = 6,

    Rejected = 7
}

/// <summary>
/// Where an employee benefit enrollment originated. Position/Grade/Mandatory enrollments are
/// materialized from entitlement templates and kept in sync; Manual ones are created directly.
/// </summary>
public enum BenefitEnrollmentSource
{
    /// <summary>Materialized from a position's <c>EmployeePositionBenefit</c> entitlement.</summary>
    Position = 1,

    /// <summary>Derived from a grade/staff-level entitlement.</summary>
    Grade = 2,

    /// <summary>Created directly for the employee.</summary>
    Manual = 3,

    /// <summary>Auto-enrolled because the policy is mandatory.</summary>
    Mandatory = 4
}

/// <summary>
/// Nature of a <c>BenefitUtilization</c> drawdown against an enrollment's coverage limit.
/// </summary>
public enum BenefitUtilizationType
{
    /// <summary>An incurred expense consuming the benefit (the common case).</summary>
    Expense = 1,

    /// <summary>A reimbursement claim against the benefit limit.</summary>
    Reimbursement = 2,

    /// <summary>A manual adjustment to the used amount (e.g. correction).</summary>
    Adjustment = 3,

    /// <summary>A reversal that returns amount to the available balance.</summary>
    Reversal = 4
}

/// <summary>
/// Lifecycle status of a benefit utilization/claim. Only <see cref="Approved"/> and <see cref="Paid"/>
/// draw down the available balance.
/// </summary>
public enum BenefitClaimStatus
{
    Pending = 1,

    Approved = 2,

    Rejected = 3,

    Paid = 4,

    Cancelled = 5
}

#endregion Staff Benefits

#region Staff Leave

/// <summary>
/// Leave request status types
/// </summary>
public enum LeaveStatus
{
    Draft = 0,

    Pending = 1,

    Approved = 2,

    Rejected = 3,

    Cancelled = 4,

    InProgress = 5,

    Completed = 6,

    /// <summary>
    /// The approver reviewed the request and proposed different dates; it is back with the employee,
    /// who accepts them or counters with their own. The mirror of
    /// <see cref="LeavePlanStatus.ChangesSuggested"/>, which leave PLANS have had since the port —
    /// requests did not, so TDC's *"sending back for correction with suggested dates"* had nowhere
    /// to happen on the record that actually books the days (closure plan R-3 / decision D-1).
    /// </summary>
    ChangesSuggested = 7
}

public enum LeaveEligibilityType
{
    Gender = 1,

    OrganizationLevel = 2,

    OrganizationUnit = 3,

    Position = 4
}

public enum LeavePlanStatus
{
    Draft = 0,

    Submitted = 1,

    Approved = 2,

    Rejected = 3,

    Cancelled = 4,

    /// <summary>
    /// Manager reviewed the submitted plan and proposed alternative dates; awaiting the
    /// employee's confirmation (the collaborative leg of the self-service proposal flow).
    /// The employee submitting their preferred dates is simply <see cref="Submitted"/>.
    /// </summary>
    ChangesSuggested = 5
}

/// <summary>
/// Categories for the reusable, system-wide reason-code lookup. Lets the same code list be
/// scoped to where it applies (leave adjustments, encashments, cancellations, etc.).
/// </summary>
public enum ReasonCodeCategory
{
    General = 0,

    LeaveAdjustment = 1,

    LeaveEncashment = 2,

    LeaveCancellation = 3,

    LeaveRejection = 4
}

public enum LeaveEncashmentStatus
{
    Draft = 0,

    Submitted = 1,

    PendingApproval = 2,

    Approved = 3,

    Rejected = 4,

    Processed = 5,   // Payment made

    Cancelled = 6
}

public enum AccrualFrequency
{
    None = 0,          // Leave does not accrue automatically (allocated yearly)

    Monthly = 1,       // Most common (e.g., 2 days per month)

    Annual = 2,        // Once per year

    PerPayPeriod = 3,  // Based on payroll cycles

    Quarterly = 4,     // Every 3 months

    SemiAnnual = 5     // Every 6 months
}

/// <summary>
/// What the year-end runs count as a person's unused days (entitlement plan B2, decision D-2).
/// </summary>
/// <remarks>
/// ⚠ Both readings are ordinary employer policy, which is why this is a setting and not a fix. The
/// product answered the question silently — as <see cref="Granted"/> — until 2026-09-18.
/// </remarks>
public enum LeaveYearEndBasis
{
    /// <summary>
    /// What the YEAR OWED them: entitlement + carry-over + adjustments, less taken, pending and
    /// encashed. ⚠ The default, because it is the behaviour that predates the setting and the
    /// year-end runs have no undo.
    /// </summary>
    Granted = 0,

    /// <summary>
    /// What they actually EARNED: the same sum with accrued-to-date in place of entitlement. A
    /// mid-year joiner carries what they built up, not what the full year would have given them.
    /// </summary>
    Earned = 1
}

/// <summary>
/// How an accrual policy releases the annual entitlement over the leave year.
/// </summary>
public enum AccrualMode
{
    /// <summary>
    /// Earn the entitlement incrementally each accrual period (e.g. 1.5 days/month), reaching
    /// the full entitlement only after a complete cycle. The common enterprise default.
    /// </summary>
    AccrueIncrementally = 0,

    /// <summary>
    /// Grant the full entitlement the moment the access/service gate is met (e.g. the 1-year
    /// anniversary); thereafter it remains at the full entitlement for that year. Matches the
    /// "entitled to the full days on your anniversary" model.
    /// </summary>
    FullGrantOnEligibility = 1
}

#endregion Staff Leave

#region Performance Appraisal

public enum GoalPriority
{
    Low = 1,

    Medium = 2,

    High = 3,

    Critical = 4
}

public enum GoalParentType
{
    Company = 1,

    Unit = 2,
}

public enum GoalStatus
{
    Draft = 1,

    PendingApproval = 2,

    Approved = 3,

    Rejected = 4,

    Locked = 5,

    InProgress = 6,

    AtRisk = 7,

    OnTrack = 8,

    Completed = 9,
}

/// <summary>
/// Governance completeness status for a team member's goal set within an appraisal cycle.
/// Derived entirely from goal counts and weight totals — never from UI state.
/// </summary>
public enum TeamGovernanceStatus
{
    /// <summary>Employee has no goals in this cycle.</summary>
    NotStarted = 0,

    /// <summary>One or more goals are still in Draft or Rejected state — structural work is incomplete.</summary>
    InProgress = 1,

    /// <summary>At least one goal is awaiting manager approval.</summary>
    AwaitingApproval = 2,

    /// <summary>All workflow states are resolved but total weight ≠ 100.</summary>
    InvalidWeight = 3,

    /// <summary>
    /// No drafts, no pending approvals, total weight == 100.
    /// Execution states (InProgress, OnTrack, AtRisk, Completed) do NOT block this status.
    /// </summary>
    StructurallyComplete = 4,
}

public enum GoalProgressStatus
{
    NotStarted = 1,

    InProgress = 2,

    OnTrack = 3,

    AtRisk = 4,

    Completed = 5,

    Cancelled = 6
}

public enum CheckInType
{
    OneOnOne = 1,

    AdHocFeedback = 2,

    GoalProgressUpdate = 3,

    CoachingSession = 4,

    MidYearCheckIn = 5
}

/// <summary>
/// Controls how many interim review events are generated within an annual cycle.
/// </summary>
public enum ReviewFrequency
{
    /// <summary>No interim reviews — only the year-end formal review.</summary>
    None = 0,

    /// <summary>A single mid-year review event is generated.</summary>
    MidYearOnly = 1,

    /// <summary>Three interim events: Q1, Mid-Year (Q2), Q3.</summary>
    Quarterly = 2,

    /// <summary>HR manually creates review events as needed (no auto-generation).</summary>
    Custom = 3
}

/// <summary>
/// Controls when peer evaluations open relative to the self-evaluation step.
/// </summary>
public enum PeerEvaluationOpenMode
{
    /// <summary>Peers can begin evaluating as soon as the self-evaluation window opens (parallel).</summary>
    WithSelfEval = 0,

    /// <summary>Peer evaluation only opens after the employee submits their self-assessment (sequential).</summary>
    AfterSelfEval = 1
}

/// <summary>
/// Controls where in the pipeline HR performs their review.
/// </summary>
public enum HRReviewTiming
{
    /// <summary>HR reviews appraisals before calibration meetings.</summary>
    BeforeCalibration = 0,

    /// <summary>HR reviews appraisals after calibration (final sign-off before employee release).</summary>
    AfterCalibration = 1
}

public enum AppraisalType
{
    Quarterly = 1,
    MidYear = 2,
    Annual = 3,
    OneOff = 4,
    /// <summary>New-hire probation / confirmation appraisal (short cycle, Theme 12).</summary>
    Probation = 5
}

/// <summary>
/// Controls how heavy the interim (quarterly / mid-year) review events are.
/// LightTouch = progress summary + conversation; FullAppraisal = a full self/manager
/// evaluation against the period's goals/KPIs producing a period score.
/// </summary>
public enum InterimReviewDepth
{
    LightTouch = 0,
    FullAppraisal = 1
}

/// <summary>
/// The period a goal/KPI target applies to within the cycle. Lets an organization
/// run full quarterly/half-year appraisals against period-scoped targets.
/// </summary>
public enum GoalPeriod
{
    FullCycle = 0,
    Q1 = 1,
    Q2 = 2,
    H1 = 3,
    Q3 = 4,
    Q4 = 5,
    H2 = 6
}

/// <summary>
/// The kind of follow-up action an appraisal recommends. Each approved recommendation
/// is dispatched to the owning module to create a real downstream record (Theme 8 backbone).
/// </summary>
public enum RecommendationType
{
    MeritIncrease = 1,
    Bonus = 2,
    Promotion = 3,
    TrainingNomination = 4,
    SuccessionNomination = 5,
    PerformanceImprovementPlan = 6,
    ConfirmProbation = 7,
    ExtendProbation = 8,
    ContractRenewal = 9,
    Demotion = 10,
    Termination = 11,
    Recognition = 12
}

/// <summary>
/// Lifecycle of an appraisal outcome recommendation. Proposed → Approved → Actioned
/// (or Rejected / Dismissed). "Actioned" means the downstream record was created.
/// </summary>
public enum RecommendationStatus
{
    Proposed = 1,
    Approved = 2,
    Actioned = 3,
    Rejected = 4,
    Dismissed = 5
}

/// <summary>Kind of compensation change proposed by an appraisal (Theme 11).</summary>
public enum SalaryReviewProposalType
{
    MeritIncrease = 1,
    Bonus = 2
}

/// <summary>
/// Lifecycle of a salary review proposal handed off to payroll/comp (Theme 11).
///
/// <para>Proposed → PendingApproval (out on the workflow engine) → Approved | Rejected, and an
/// approved proposal is marked Applied once payroll has made the change.</para>
/// </summary>
public enum SalaryReviewProposalStatus
{
    Proposed = 1,
    Approved = 2,
    Rejected = 3,
    Applied = 4,

    /// <summary>Submitted and out for approval on the workflow engine.</summary>
    [Description("Pending Approval")]
    PendingApproval = 5
}

/// <summary>
/// Type of employment action proposed from an appraisal outcome recommendation. These route to a
/// lightweight <c>EmploymentActionProposal</c> intake record (no heavyweight target container needed at
/// approval time) for HR to action in the owning module.
/// </summary>
public enum EmploymentActionType
{
    Promotion = 1,
    Demotion = 2,
    ContractRenewal = 3,
    Termination = 4,
    Recognition = 5
}

/// <summary>
/// Lifecycle of an employment-action proposal raised from an appraisal recommendation.
///
/// <para>Proposed → PendingApproval (out on the workflow engine) → Approved | Rejected, and an
/// approved proposal is marked Actioned once the owning module has created the real record.</para>
/// </summary>
public enum EmploymentActionProposalStatus
{
    Proposed = 1,
    Approved = 2,
    Rejected = 3,
    Actioned = 4,

    /// <summary>Submitted and out for approval on the workflow engine.</summary>
    [Description("Pending Approval")]
    PendingApproval = 5
}

public enum AppraisalCycleStatus
{
    Draft = 1,                    // Being configured
    Open = 2,                   // Active, appraisals can be created
    InProgress = 3,               // Evaluations are happening
    Closed = 4,                   // Finalized, no changes
}

public enum AppraisalTargetType
{
    OrganizationLevel = 1,
    OrganizationUnit = 2,
    Position = 3,
    Employee = 4,
}

/// <summary>
/// Coverage status for an employee in the Coverage Preview simulation.
/// </summary>
public enum EmployeeCoverageStatus
{
    /// <summary>A unique highest-priority template was resolved.</summary>
    Covered = 1,
    /// <summary>No active template matched this employee's scope.</summary>
    NoTemplate = 2,
    /// <summary>Multiple templates tied at the same priority for the same scope level.</summary>
    Conflict = 3,
    /// <summary>Employee is explicitly excluded by a cycle target exclusion rule.</summary>
    Excluded = 4,
}

/// <summary>
/// High-level lifecycle state persisted on <see cref="PerformanceAppraisal"/>.
/// Use <see cref="AppraisalPhase"/> (computed, not persisted) for fine-grained phase detection.
/// </summary>
public enum AppraisalStatus
{
    /// <summary>Legacy/transitional 'open' status used by the retained RHEMA appraisal model.</summary>
    Open = 0,

    /// <summary>
    /// Appraisal created but not yet opened for employee action (goals may still be pending).
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Appraisal is open and actively progressing through employee/peer/manager evaluation phases.
    /// Fine-grained phase is computed via <see cref="AppraisalPhase"/>.
    /// </summary>
    Active = 2,

    /// <summary>
    /// Manager evaluation is complete; appraisal is under HR/Calibration governance review.
    /// </summary>
    Governance = 3,

    /// <summary>
    /// Employee has filed an appeal that is under review.
    /// </summary>
    Appealed = 4,

    /// <summary>
    /// HR finalisation complete and employee has acknowledged (or acknowledgment not required).
    /// Appeal window may still be open.
    /// </summary>
    Completed = 5,

    /// <summary>
    /// Appraisal fully closed — terminal state, no further transitions permitted.
    /// </summary>
    Closed = 6,
}

/// <summary>
/// Fine-grained sub-status of an appraisal within its lifecycle, derived from entity state and
/// settings flags by <see cref="AppraisalSubStatusResolver"/>.
/// Used as the "current stuck step" identifier when HR manually advances a stalled pipeline.
/// NOT persisted — always derived on demand.
/// </summary>
public enum AppraisalSubStatus
{
    // ── Pre-pipeline ───────────────────────────────
    GoalSetting = 0,

    // ── Pipeline active phases ─────────────────────
    PeerNomination = 1,
    SelfEvaluation = 2,
    PeerEvaluation = 3,
    ManagerEvaluation = 4,

    // ── Post-manager governance ────────────────────
    PendingCalibration = 5,
    CalibrationInProgress = 6,
    PendingHRReview = 7,
    HRReviewInProgress = 8,

    // ── Employee end ───────────────────────────────
    PendingConversation = 9,
    PendingAcknowledgment = 10,

    // ── Appeals ────────────────────────────────────
    AppealSubmitted = 11,
    AppealUnderReview = 12,
    AppealResolved = 13,

    // ── Terminal ───────────────────────────────────
    Completed = 14,
    Closed = 15,
}

/// <summary>
/// Fine-grained workflow phase computed dynamically from appraisal data.
/// NOT persisted to the database — derive on demand via <c>IAppraisalWorkflowService.GetCurrentPhase</c>.
/// </summary>
public enum AppraisalPhase
{
    /// <summary>Goals are required but not yet approved by the manager.</summary>
    GoalSetting = 1,

    /// <summary>Awaiting employee self-evaluation submission.</summary>
    SelfEvaluation = 2,

    /// <summary>Awaiting minimum peer evaluator submissions.</summary>
    PeerEvaluation = 3,

    /// <summary>Awaiting manager evaluation submission.</summary>
    ManagerEvaluation = 4,

    /// <summary>Awaiting HR calibration session completion.</summary>
    Calibration = 5,

    /// <summary>HR is reviewing and finalising scores.</summary>
    HRReview = 6,

    /// <summary>Employee must review and acknowledge results.</summary>
    EmployeeReview = 7,

    /// <summary>All steps complete — appraisal is in a terminal phase.</summary>
    Closed = 8,
}

public enum DevelopmentPlanStatus
{
    Draft = 0,

    Active = 1,

    OnHold = 2,

    Completed = 3,

    Cancelled = 4,
}

public enum DevelopmentObjectiveStatus
{
    NotStarted = 1,

    InProgress = 2,

    Completed = 3,

    Cancelled = 4
}

/// <summary>
/// Classifies the type of manager feedback recorded against a development plan.
/// </summary>
public enum FeedbackType
{
    /// <summary>General observation or comment.</summary>
    GeneralComment = 1,

    /// <summary>Acknowledgment of progress toward objectives.</summary>
    ProgressUpdate = 2,

    /// <summary>Concern or warning that the plan may be at risk.</summary>
    RiskFlag = 3,

    /// <summary>Formal mid-cycle review note.</summary>
    MidCycleReview = 4,

    /// <summary>End-of-cycle summary feedback.</summary>
    CycleClosing = 5,
}

public enum ReviewEventType
{
    GoalSetting = 1,

    QuarterlyQ1 = 2,

    QuarterlyQ2 = 3,

    MidYearReview = 4,

    QuarterlyQ3 = 5,

    QuarterlyQ4 = 6,

    YearEndReview = 7,
}

public enum AppraisalReviewStatus
{
    Pending = 1,

    InProgress = 2,

    /// <summary>Employee has formally submitted their side; awaiting manager review and close.</summary>
    EmployeeSubmitted = 3,

    Completed = 4,

    Cancelled = 5
}

public enum ConversationType
{
    KickOff = 1,

    QuarterlyQ1 = 2,

    MidYear = 3,

    QuarterlyQ3 = 4,

    QuarterlyQ4 = 5,

    FinalReview = 6,

    PIPDiscussion = 7,

    AdHocMeeting = 8
}

public enum AppraisalAttachmentEntityType
{
    PerformanceAppraisal = 1,

    Goal = 2,

    CheckIn = 3,

    PipPlan = 4,

    Appeal = 5,

    KpiEvaluation = 6,

    CalibrationSession = 7,

    /// <summary>Evidence attached to an interim (quarterly/mid-year) review event.</summary>
    ReviewEvent = 8
}

public enum EvaluatorRole
{
    Self = 1,

    Manager = 2,

    Peer = 3,

    HR = 4
}

/// <summary>Indicates the source that provided the KPI target in a criterion config snapshot.</summary>
public enum KpiTargetSource
{
    /// <summary>Target came from the employee's locked goal.</summary>
    Goal = 1,

    /// <summary>Target came from the appraisal template item default.</summary>
    Template = 3
}

/// <summary>
/// Approval lifecycle for an appraisal template. Units draft templates and submit them to HR;
/// only Approved templates can be assigned to a cycle.
/// </summary>
public enum TemplateApprovalStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4
}

public enum PeerNominationMode
{
    Employee = 1,

    Manager = 2,
}

public enum PeerNominationStatus
{
    Pending = 1,

    Approved = 2,

    Rejected = 3
}

public enum MeasurementType
{
    NumericAbsolute = 1,

    PercentageTarget = 2,

    Boolean = 3,

    Range = 4
}

/// <summary>
/// Represents the lifecycle and final outcome of an appraisal appeal.
/// 
/// IMPORTANT SEMANTICS:
/// - "Remanded" is a PROCESS state (manager action required).
/// - "Upheld" and "Rejected" are FINAL VERDICT states.
/// - Once a FINAL state is reached, no further score changes are allowed.
/// </summary>
public enum AppraisalAppealStatus
{
    /// <summary>
    /// Employee has submitted an appeal.
    /// No HR action has started yet.
    /// </summary>
    Submitted = 1,

    /// <summary>
    /// HR is actively reviewing the appeal.
    /// Scores are read-only at this stage.
    /// </summary>
    UnderReview = 2,

    /// <summary>
    /// HR has determined that the appeal has merit
    /// but requires manager re-evaluation or clarification
    /// before a final decision can be made.
    /// 
    /// Manager action is required.
    /// A remand deadline applies.
    /// </summary>
    Remanded = 3,

    /// <summary>
    /// FINAL STATE.
    /// HR has concluded that the employee's appeal is valid.
    /// 
    /// This may occur:
    /// - Directly after HR review (no remand needed), OR
    /// - After reviewing post-remand manager re-evaluation.
    /// 
    /// Final scores are confirmed and locked.
    /// </summary>
    Upheld = 4,

    /// <summary>
    /// FINAL STATE.
    /// HR has concluded that the employee's appeal is not valid.
    /// 
    /// This may occur:
    /// - Directly after HR review, OR
    /// - After reviewing post-remand manager re-evaluation.
    /// 
    /// Original or post-remand scores are confirmed and locked.
    /// </summary>
    Rejected = 5
}

public enum AppraisalResponseStatus
{
    Draft = 1,

    Submitted = 2,

    Recalled = 3
}

public enum CalibrationStatus
{
    Pending = 1,

    InProgress = 2,

    Completed = 3,

    Cancelled = 4
}

/// <summary>
/// Lifecycle of a performance improvement plan.
///
/// <para><c>Draft</c> and <c>PendingApproval</c> were added when the PIP was put on the generic
/// workflow engine. A PIP is an employment record served on a named employee, so it is written in
/// draft, approved through a published <c>PerformanceImprovementPlan</c> workflow definition, and
/// only then becomes <c>Active</c> — the engine owns those three states and nothing else may set
/// them. Everything from <c>Active</c> onwards is the plan actually running, and stays a direct
/// action on the record.</para>
/// </summary>
public enum PipStatus
{
    [Description("Active")]
    Active = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Successfully Completed")]
    Completed = 3,

    [Description("Unsuccessful")]
    Unsuccessful = 4,

    [Description("Cancelled")]
    Cancelled = 5,

    /// <summary>Being written. Not yet visible to the employee and not yet in force.</summary>
    [Description("Draft")]
    Draft = 6,

    /// <summary>Out for approval on the workflow engine.</summary>
    [Description("Pending Approval")]
    PendingApproval = 7
}

public enum PerformanceRating
{
    [Description("Outstanding")]
    Outstanding = 5,

    [Description("Exceeds Expectations")]
    ExceedsExpectations = 4,

    [Description("Meets Expectations")]
    MeetsExpectations = 3,

    [Description("Below Expectations")]
    BelowExpectations = 2,

    [Description("Unsatisfactory")]
    Unsatisfactory = 1
}

public enum PipOutcome
{
    [Description("Performance Improved")]
    PerformanceImproved = 1,

    [Description("Extended")]
    Extended = 2,

    [Description("Demotion")]
    Demotion = 3,

    [Description("Termination")]
    Termination = 4,

    [Description("Transferred")]
    Transferred = 5
}

#endregion Performance Appraisal

#region Recruitment

public enum JobVacancyType
{
    [Description("New Position")]
    NewPosition = 1,

    [Description("Replacement")]
    Replacement = 2,

    [Description("Temporary/Contract")]
    Temporary = 3,

    [Description("Internship")]
    Internship = 4
}

public enum JobVacancyStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Pending Approval")]
    PendingApproval = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Rejected")]
    Rejected = 4,

    [Description("Published")]
    Published = 5,

    [Description("Closed for Applications")]
    ClosedForApplications = 6,

    [Description("Shortlisting")]
    Shortlisting = 7,

    [Description("Interviewing")]
    Interviewing = 8,

    [Description("Offer Stage")]
    OfferStage = 9,

    [Description("Filled")]
    Filled = 10,

    [Description("Cancelled")]
    Cancelled = 11,

    [Description("On Hold")]
    OnHold = 12
}

public enum JobVacancyClosureReason
{
    [Description("Position Filled")]
    PositionFilled = 1,

    [Description("Budget Constraints")]
    BudgetConstraints = 2,

    [Description("Position Eliminated")]
    PositionEliminated = 3,

    [Description("Hiring Freeze")]
    HiringFreeze = 4,

    [Description("No Suitable Candidates")]
    NoSuitableCandidates = 5,

    [Description("Other")]
    Other = 6,

    [Description("Application Deadline Passed")]
    ApplicationDeadlinePassed = 7,

    [Description("Sufficient Applications Received")]
    SufficientApplicationsReceived = 8,

    [Description("Proceeding to Shortlisting")]
    ProceedingToShortlisting = 9
}

public enum JobPostingChannel
{
    InternalPortal = 1,
    CompanyWebsite = 2,
    LinkedIn = 3,
    JobBoard = 4,
    Agency = 5,
    Indeed = 6,
    Glassdoor = 7,
    Newspaper = 8,
    Other = 9
}

public enum JobPostingStatus
{
    Draft = 1,
    Published = 2,
    Expired = 3,
    Closed = 4,
    Removed = 5
}

/// <summary>
/// Lifecycle status of a <see cref="VacancyPipelineStageAssignment"/> — tracks where a
/// vacancy-specific pipeline stage activity is in its execution.
/// </summary>
public enum VacancyStageAssignmentStatus
{
    [Description("Not Started")]
    NotStarted = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    /// <summary>Past the due date and not yet completed.</summary>
    [Description("Overdue")]
    Overdue = 4,

    /// <summary>Escalation has been triggered because the responsible person did not complete in time.</summary>
    [Description("Escalated")]
    Escalated = 5,

    /// <summary>Intentionally skipped (only valid when the stage's <c>CanSkip</c> flag is true).</summary>
    [Description("Skipped")]
    Skipped = 6
}

public enum RecruitmentPipelineStageType
{
    ApplicationReview = 1,

    Screening = 2,

    /// <summary>
    /// Optional gate where the hiring manager reviews shortlisted CVs before interviews are
    /// scheduled. Configure only if the client's process requires this explicit approval step.
    /// </summary>
    HiringManagerReview = 3,

    /// <summary>
    /// Any formal test or evaluation — written, practical, psychometric, or cognitive.
    /// The specific test type is captured on JobApplicantTestResult.TestType.
    /// </summary>
    Assessment = 4,

    Interview = 5,

    /// <summary>
    /// Pre-employment verification stage — background checks, reference checks, and medical
    /// examinations. These typically run in parallel; each item is tracked individually
    /// via PreEmploymentCheckItem.
    /// </summary>
    PreEmploymentCheck = 6,

    Offer = 7,

    /// <summary>
    /// Terminal stage — the candidate has been confirmed hired.
    /// Triggers creation of the JobHireRecord and handoff to the Onboarding subsystem.
    /// </summary>
    Hired = 8,

    Other = 9
}

public enum ApplicationStatus
{
    [Description("Draft")]
    Draft = 0,

    [Description("New")]
    New = 1,

    [Description("Submitted")]
    Submitted = 2,

    [Description("Under Review")]
    UnderReview = 3,

    [Description("Shortlisted")]
    Shortlisted = 4,

    [Description("Interview Scheduled")]
    InterviewScheduled = 5,

    [Description("Interview Completed")]
    InterviewCompleted = 6,

    [Description("Assessment Pending")]
    AssessmentPending = 7,

    [Description("Pre-Employment Check")]
    PreEmploymentCheck = 8,

    [Description("Offer Extended")]
    OfferExtended = 9,

    [Description("Offer Accepted")]
    OfferAccepted = 10,

    [Description("Offer Declined")]
    OfferDeclined = 11,

    [Description("Rejected")]
    Rejected = 12,

    [Description("Withdrawn")]
    Withdrawn = 13,

    [Description("Hired")]
    Hired = 14,

    [Description("Waitlisted")]
    Waitlisted = 15
}

public enum ApplicationSource
{
    [Description("Company Website")]
    CompanyWebsite = 1,

    [Description("Job Board")]
    JobBoard = 2,

    [Description("LinkedIn")]
    LinkedIn = 3,

    [Description("Employee Referral")]
    EmployeeReferral = 4,

    [Description("Walk-in")]
    WalkIn = 5,

    [Description("Recruitment Agency")]
    RecruitmentAgency = 6,

    [Description("Career Fair")]
    CareerFair = 7,

    [Description("Social Media")]
    SocialMedia = 8,

    [Description("Newspaper Advertisement")]
    NewspaperAd = 9,

    [Description("Other")]
    Other = 10,

    [Description("Internal Portal")]
    InternalPortal = 11,

    /// <summary>
    /// Sourced from the candidate talent pool: HR screened pooled candidates against a vacancy's
    /// criteria and invited them to apply (round 4, lane B).
    /// </summary>
    /// <remarks>
    /// Distinct from every other member in that the application was not initiated by the candidate.
    /// It is worth its own member rather than being folded into Other, because "how many of our
    /// hires came out of the pool we keep warm?" is exactly the question the pool exists to answer,
    /// and Other cannot answer it.
    /// </remarks>
    [Description("Talent Pool")]
    TalentPool = 12
}

public enum JobCandidateDocumentType
{
    [Description("Resume/CV")]
    Resume = 1,

    [Description("Cover Letter")]
    CoverLetter = 2,

    [Description("Academic Transcript")]
    Transcript = 3,

    [Description("Certificate")]
    Certificate = 4,

    [Description("Professional License")]
    License = 5,

    [Description("Portfolio")]
    Portfolio = 6,

    [Description("Reference Letter")]
    ReferenceLetter = 7,

    [Description("ID Document")]
    IdDocument = 8,

    [Description("Other")]
    Other = 9
}

public enum JobApplicationStageExitReason
{
    Progressed = 1,
    Rejected = 2,
    Withdrawn = 3,
    OnHold = 4,
    Merged = 5
}

public enum JobApplicantTestType
{
    Written = 1,
    Practical = 2
}

/// <summary>
/// What a recruitment test question asks for (round 4, lane E).
/// </summary>
/// <remarks>
/// ⚠ Mirrors <c>OrientationQuestionType</c> deliberately (decision D-2), because the marking follows
/// the same rules — including the one it had to learn: the denominator is every GRADABLE question on
/// the paper, and <see cref="FreeText"/> is the only member that is not one.
/// </remarks>
public enum RecruitmentQuestionType
{
    [Description("Single choice")]
    SingleChoice = 1,

    /// <summary>⚠ Marked on EXACT SET EQUALITY — every correct option and no incorrect one.</summary>
    [Description("Multiple choice")]
    MultiSelect = 2,

    [Description("True or false")]
    TrueFalse = 3,

    /// <summary>The one type the machine cannot mark. Stored, and awaits a human.</summary>
    [Description("Free text")]
    FreeText = 4,

    [Description("Numeric")]
    Numeric = 5,
}

/// <summary>Where one candidate's attempt at a paper has got to (round 4, lane E).</summary>
public enum RecruitmentSittingStatus
{
    [Description("Not started")]
    NotStarted = 1,

    [Description("In progress")]
    InProgress = 2,

    /// <summary>Submitted, and the closed questions are marked. A paper with free text is not done.</summary>
    [Description("Awaiting marking")]
    AwaitingMarking = 3,

    [Description("Marked")]
    Marked = 4,

    /// <summary>
    /// The window closed with the paper unsubmitted.
    /// </summary>
    /// <remarks>
    /// ⚠ Distinct from a zero. A candidate who ran out of time has a mark; one who never started has
    /// nothing, and recording that as 0% would blend a non-event into their shortlisting score.
    /// </remarks>
    [Description("Expired")]
    Expired = 5,

    [Description("Cancelled")]
    Cancelled = 6,
}

/// <summary>
/// How a recruitment test was sat (round 4, lane E6).
/// </summary>
/// <remarks>
/// ⚠ Recorded rather than inferred. A script the candidate submitted themselves and one HR typed in
/// from paper are different kinds of evidence, and a recruitment decision can be challenged: the
/// record must say which without anybody having to reason it out of which columns happen to be null.
/// </remarks>
public enum RecruitmentSittingMode
{
    /// <summary>Sat in the careers portal, submitted by the candidate, marked by the server.</summary>
    [Description("Online")]
    Online = 1,

    /// <summary>
    /// Sat on the printed paper; what the candidate ticked, and the marks for their written answers,
    /// entered by HR. The closed questions are still marked by the server against the key.
    /// </summary>
    [Description("On paper")]
    Paper = 2,
}

public enum JobInterviewType
{
    /// <summary>Initial short conversation to verify basics. Format is set via InterviewMode.</summary>
    [Description("Screening")]
    Screening = 1,

    [Description("One-on-One")]
    OneOnOne = 2,

    [Description("Panel")]
    Panel = 3,

    [Description("Technical")]
    Technical = 4,

    /// <summary>Structured behavioural interview using the STAR method. Common in enterprise and public-sector hiring.</summary>
    [Description("Competency-Based")]
    CompetencyBased = 5,

    /// <summary>Business case or problem-solving scenario. Common in consulting, finance, and strategy roles.</summary>
    [Description("Case Study")]
    CaseStudy = 6,

    /// <summary>Candidate presents on a given topic or their own work. Common for senior and specialist roles.</summary>
    [Description("Presentation")]
    Presentation = 7,

    /// <summary>Multiple candidates assessed simultaneously. Common in graduate recruitment and assessment centres.</summary>
    [Description("Group Assessment")]
    GroupAssessment = 8,

    [Description("Final")]
    Final = 9,

    [Description("Other")]
    Other = 10
}

/// <summary>
/// How an interview session is delivered. Stored separately from JobInterviewType so that
/// any interview format (Panel, Technical, Final, etc.) can be conducted via any modality.
/// </summary>
public enum InterviewMode
{
    [Description("In Person")]
    InPerson = 1,

    [Description("Phone")]
    Phone = 2,

    [Description("Video")]
    Video = 3,

    /// <summary>Some panelists in-person, others joining remotely.</summary>
    [Description("Hybrid")]
    Hybrid = 4
}

public enum JobInterviewStatus
{
    [Description("Scheduled")]
    Scheduled = 1,

    [Description("Rescheduled")]
    Rescheduled = 2,

    [Description("In Progress")]
    InProgress = 3,

    [Description("Completed")]
    Completed = 4,

    [Description("No Show")]
    NoShow = 5,

    [Description("Cancelled")]
    Cancelled = 6
}

public enum JobInterviewOutcome
{
    [Description("Highly Recommended")]
    HighlyRecommended = 1,

    [Description("Recommended")]
    Recommended = 2,

    [Description("Acceptable")]
    Acceptable = 3,

    [Description("Not Recommended")]
    NotRecommended = 4,

    [Description("Proceed to Next Round")]
    ProceedToNextRound = 5,

    [Description("Rejected")]
    Rejected = 6,

    [Description("On Hold")]
    OnHold = 7
}

public enum JobInterviewPanelistRole
{
    [Description("Panel Chair")]
    Chair = 1,

    [Description("Panel Member")]
    Member = 2,

    [Description("Technical Assessor")]
    TechnicalAssessor = 3,

    [Description("Observer")]
    Observer = 4
}

public enum JobInterviewRecommendation
{
    [Description("Strong Hire")]
    StrongHire = 1,

    [Description("Hire")]
    Hire = 2,

    [Description("Neutral")]
    Neutral = 3,

    [Description("No Hire")]
    NoHire = 4,

    [Description("Strong No Hire")]
    StrongNoHire = 5
}

/// <summary>
/// How a scorecard reached the system (round 4, lane F4).
/// </summary>
/// <remarks>
/// A panel that scored on paper hands HR a signed sheet, and HR types it in. The record that comes
/// out is indistinguishable from one the panelist typed themselves unless the difference is stored,
/// which is why this exists: the scorecard remains the panelist's verdict either way, but the
/// keystrokes were somebody else's and an audit trail should not quietly claim otherwise.
/// </remarks>
public enum InterviewScoreSource
{
    /// <summary>The panelist filed it themselves, in the product.</summary>
    [Description("Filed online")]
    Online = 1,

    /// <summary>
    /// Transcribed by HR from a signed paper sheet. <c>FiledByHrOnBehalfOfUserId</c> names who
    /// typed it.
    /// </summary>
    [Description("From a paper sheet")]
    PaperSheet = 2
}

public enum JobShortlistingCriteriaType
{
    [Description("Qualification")]
    Qualification = 1,

    [Description("Years Of Experience")]
    YearsOfExperience = 2,

    [Description("Skill")]
    Skill = 3,

    [Description("Certification")]
    Certification = 4,

    [Description("Language")]
    Language = 5,

    [Description("Gender")]
    Gender = 6,

    [Description("Age")]
    Age = 7,

    [Description("Location")]
    Location = 8,

    [Description("Education Level")]
    EducationLevel = 9,

    [Description("Other")]
    Other = 10
}

public enum ShortlistingComparisonOperator
{
    Equals = 1,
    NotEquals = 2,
    Contains = 3,
    GreaterThan = 4,
    GreaterThanOrEqual = 5,
    LessThan = 6,
    LessThanOrEqual = 7,
    Between = 8,
    In = 9
}

/// <summary>
/// Controls how a multi-valued shortlisting criterion (Skill, Qualification,
/// Certification, Language) decides whether a candidate has "passed".
///
/// <list type="bullet">
///   <item>
///     <term>AnyMatched</term>
///     <description>
///       The candidate passes if they satisfy at least one of the listed values.
///       Use this for broad criteria where any relevant skill or qualification is acceptable.
///       This is the default so existing rows are unaffected.
///     </description>
///   </item>
///   <item>
///     <term>AllRequired</term>
///     <description>
///       The candidate must match every listed value to pass.
///       Use this for hard requirements where the full set is non-negotiable.
///     </description>
///   </item>
/// </list>
/// </summary>
public enum MandatoryMatchMode
{
    [Description("Any matched")]
    AnyMatched = 0,   // default — preserves legacy behaviour

    [Description("All required")]
    AllRequired = 1,
}

/// <summary>
/// Controls how individual values in a multi-valued criterion's <c>RequiredValue</c> list
/// are compared against candidate profile strings.
/// <list type="bullet">
///   <item><term>Exact</term><description>Case-insensitive exact equality (default, current behaviour).</description></item>
///   <item><term>Contains</term><description>Passes if either string contains the other (handles abbreviations and prefixes).</description></item>
///   <item><term>Fuzzy</term><description>Tokenised word-overlap ≥ 50 %, with Levenshtein edit-distance ≤ 2 fallback for short strings.</description></item>
/// </list>
/// </summary>
public enum ValueMatchStrategy
{
    [Description("Exact")]
    Exact = 0,      // default — current behaviour

    [Description("Contains")]
    Contains = 1,   // candidateValue ⊇ requiredTerm  OR  requiredTerm ⊇ candidateValue

    [Description("Fuzzy")]
    Fuzzy = 2,      // tokenised word-overlap, with Levenshtein fallback
}

/// <summary>
/// What one accepted value on a shortlisting criterion refers to (round 3, lane K; register row
/// R-8). A catalogue kind carries the row id and its name; Gender carries the enum member's name;
/// Text is a typed label (a city for Location, anything for Other).
/// </summary>
public enum ShortlistingValueKind
{
    [Description("Text")]
    Text = 0,

    [Description("Skill")]
    Skill = 1,

    [Description("Qualification")]
    Qualification = 2,

    [Description("Certification")]
    Certification = 3,

    [Description("Language")]
    Language = 4,

    [Description("Gender")]
    Gender = 5,

    /// <summary>
    /// An administrative area from the shared geography tree — round 4, lane A.
    /// </summary>
    /// <remarks>
    /// ⚠ The value's <c>ReferenceId</c> is a <c>GeoArea.Id</c> and its <c>Label</c> is the area's
    /// name mirrored at save. Matching is by <b>tree containment</b>, not by the label: an accepted
    /// area matches a candidate in it and anywhere beneath it, so "Greater Accra" matches Tema. The
    /// label exists for display and for the legacy text fallback, never as the comparison.
    /// </remarks>
    [Description("Geographic Area")]
    GeoArea = 6,

    /// <summary>
    /// A rung of the tenant's qualification ladder — round 4, lane Q.
    /// </summary>
    /// <remarks>
    /// ⚠ The value's <c>ReferenceId</c> is a <c>QualificationLevel.Id</c>: the MINIMUM an
    /// "Education level" criterion accepts. Matching is by <b>rank</b>, never by the label. A
    /// candidate passes when any of their qualifications sits on that rung or higher, so ties pass:
    /// with HND and Bachelor's both at 50, an HND meets "at least Bachelor's". Appended, never
    /// renumbered: the kind is stored as an integer.
    /// </remarks>
    [Description("Qualification Level")]
    QualificationLevel = 7,
}

/// <summary>
/// Approval state of the completed shortlist before candidates are contacted.
/// </summary>
public enum ShortlistApprovalStatus
{
    [Description("Not Submitted")]
    NotSubmitted = 1,

    [Description("Submitted for Approval")]
    PendingApproval = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Rejected — Revise Shortlist")]
    Rejected = 4
}

/// <summary>
/// Whether an auto-shortlist action was triggered by the system or manually overridden.
/// </summary>
public enum ShortlistDecisionSource
{
    [Description("Manual")]
    Manual = 1,

    [Description("Auto (Score Threshold)")]
    AutoScoreThreshold = 2,

    [Description("Manual Override of Auto")]
    ManualOverride = 3
}

/// <summary>
/// The type of shortlisting decision recorded in the immutable audit log.
/// </summary>
public enum ShortlistDecisionType
{
    [Description("Shortlisted")]
    Shortlisted = 1,

    [Description("Rejected")]
    Rejected = 2,

    [Description("Waitlisted")]
    Waitlisted = 3,

    [Description("Un-shortlisted")]
    Unshortlisted = 4,

    [Description("Auto-Shortlisted")]
    AutoShortlisted = 5
}

public enum JobOfferStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Sent = 4,
    Negotiating = 5,
    Accepted = 6,
    Declined = 7,
    Withdrawn = 8,
    Expired = 9,
    OnHold = 10,
    /// <summary>Candidate has verbally/formally accepted; pre-employment checks are underway.</summary>
    ConditionallyAccepted = 11,
    /// <summary>All blocking pre-employment checks have passed; hire record may now be created.</summary>
    ChecksCleared = 12,
    /// <summary>Offer was rejected by an approver during the approval workflow. The preparer must revise and resubmit.</summary>
    Rejected = 13,

    /// <summary>
    /// This version of the offer has been replaced by a revision. Terminal: the terms it carries
    /// are no longer on the table, and the live offer is the one at <c>IsLatestVersion = true</c>.
    /// </summary>
    /// <remarks>
    /// <para>Added for G-2.4/G-10.2 (2026-09-15). <c>ReviseOfferAsync</c> set
    /// <c>original.IsLatestVersion = false</c> and <b>did not change the status</b>, so v1 stayed
    /// <c>Sent</c> or <c>Negotiating</c> for ever — and since nothing filtered on
    /// <c>IsLatestVersion</c>, superseded versions were listed by <c>GET /offers/status/Sent</c>,
    /// counted as chasable by <c>GET /offers/expiring</c>, inflated the landing page's "offers
    /// expiring soon" tile permanently, and inflated the dashboard's "offers pending response".
    /// Every revision added one more row to a chase list that could never fall.</para>
    ///
    /// <para><b>Why not reuse <see cref="Withdrawn"/>.</b> Withdrawing is a decision somebody makes
    /// about a live offer — the organisation taking the terms back. Being superseded is what
    /// happens to a version when better terms replace it; nobody revoked anything. Collapsing the
    /// two would answer "how many offers did we withdraw this year?" wrongly, which is the same
    /// class of reporting defect this programme is closing.</para>
    ///
    /// <para>⚠ Appended as 14, after <see cref="Rejected"/>. This column is a plain int, so
    /// appending is schema-safe and needs no migration — but members must be APPENDED, never
    /// renumbered, or existing rows silently change meaning. Same call as
    /// <c>EmployeeTerminationType.SummaryDismissal</c>.</para>
    /// </remarks>
    Superseded = 14
}

public enum EmploymentType
{
    Permanent = 1,
    Contract = 2,
    FixedTerm = 3,
    Internship = 4,
    Casual = 5,
    PartTime = 6,
    Temporary = 7,
    Consultant = 8,
    Freelance = 9
}

/// <summary>
/// How an employee's basic pay is arrived at: read off the salary scale, or agreed for the person.
/// </summary>
/// <remarks>
/// <para>HR's fact, because the scale is HR's concept — <c>EmployeeSalaryAssignment</c> places a
/// person on a <c>SalaryNotch</c> whose amount is the pay — while payroll is already amount-based
/// and never reads the placement. Round-2 lane E1 (docs/HR/programme/HR-DEMO-FEEDBACK-ROUND-2-PLAN.md § 6.5.2).</para>
///
/// <para>⚠ Independent of <c>EmploymentType</c> and of <c>IsOnPayroll</c>. A permanent employee can
/// be negotiated (a retained specialist) and a contractor can be on the scale; the feedback's
/// "distinguish contract from permanent" is answered by two axes, not one.</para>
/// </remarks>
public enum PayBasis
{
    /// <summary>Basic pay is the amount of the notch the person is placed on. Placement expected; its absence is reported, not blocked.</summary>
    SalaryScale = 1,

    /// <summary>Basic pay is an amount agreed for this person. Placement on the scale is REFUSED while this stands.</summary>
    Negotiated = 2,
}

/// <summary>
/// Why an employee is NOT paid through the payroll run. Recorded alongside
/// <c>Employee.IsOnPayroll = false</c>, because a bare "off" cannot answer the question the payroll
/// owner will ask of every active person missing from a run — and because the answer decides how
/// that person IS paid (an invoice, an allowance, another employer).
/// </summary>
public enum OffPayrollReason
{
    /// <summary>Paid against invoices — consultants, freelancers, contractors on a fee.</summary>
    PaidByInvoice = 1,
    /// <summary>Paid a stipend or allowance outside the run — interns, national service personnel.</summary>
    Allowance = 2,
    /// <summary>Paid by a parent organisation — secondees, attached staff.</summary>
    PaidByParentOrganisation = 3,
    /// <summary>Unpaid — volunteers, honorary appointments.</summary>
    Unpaid = 4,
    /// <summary>Board and committee members remunerated by sitting allowance, not payroll.</summary>
    BoardOrCommittee = 5,
    /// <summary>Anything else; the note says what.</summary>
    Other = 99
}

public enum JobApplicantCommunicationType
{
    Email = 1,
    TextMessage = 2,
    Letter = 3,
    PortalNotification = 4,
    PhoneCall = 5
}

public enum JobApplicantCommunicationDirection
{
    Outbound = 1,
    Inbound = 2,
    System = 3
}

public enum JobHireStatus
{
    PendingOnboarding = 1,
    OnboardingInProgress = 2,
    OnboardingCompleted = 3,
    Active = 4,
    Cancelled = 5
}

public enum PreEmploymentCheckStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    CompletedWithCaution = 4,
    Failed = 5,
    Waived = 6
}

public enum PreEmploymentCheckType
{
    MedicalExamination = 1,
    PoliceClearance = 2,
    BackgroundCheck = 3,
    AcademicVerification = 4,
    ProfessionalLicenceVerification = 5,
    ReferenceCheck = 6,
    CreditCheck = 7,
    DrugTest = 8,
    Other = 9
}

public enum CheckItemStatus
{
    Pending = 1,
    Requested = 2,
    Received = 3,
    Verified = 4,
    Failed = 5,
    Waived = 6,
    NotApplicable = 7
}

public enum ReferenceResponseMethod
{
    Email = 1,
    Phone = 2,
    InPerson = 3,
    Form = 4,
    Letter = 5
}

public enum ReferenceRating
{
    Excellent = 1,
    Good = 2,
    Satisfactory = 3,
    Poor = 4,
    Unsatisfactory = 5
}

public enum OnboardingTaskCategory
{
    Documentation = 1,
    SystemAccess = 2,
    Orientation = 3,
    Training = 4,
    EquipmentSetup = 5,
    PayrollSetup = 6,
    PolicyAcknowledgement = 7,
    MeetAndGreet = 8,
    HealthAndSafety = 9,
    Compliance = 10,
    Other = 11
}

public enum OnboardingStatus
{
    NotStarted = 1,
    InProgress = 2,
    Completed = 3,
    Overdue = 4,
    Cancelled = 5
}

public enum OnboardingTaskStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Overdue = 4,
    Waived = 5,
    Blocked = 6,

    /// <summary>
    /// Done by the assignee, awaiting the second-party sign-off that <c>RequiresVerification</c> asks
    /// for. Deliberately distinct from <see cref="Completed"/>: a task nobody has verified is not
    /// finished, and the plan's "completed tasks" roll-up must not count it.
    /// </summary>
    PendingVerification = 7
}

public enum OnboardingAssetType
{
    Laptop = 1,
    Desktop = 2,
    MobilePhone = 3,
    AccessCard = 4,
    ParkingPass = 5,
    Uniform = 6,
    SystemAccount = 7,
    EmailAccount = 8,
    SoftwareLicence = 9,
    Keys = 10,
    Other = 11
}

public enum OnboardingAssetProvisionStatus
{
    Pending = 1,
    Ordered = 2,
    Ready = 3,
    Issued = 4,
    Acknowledged = 5,
    NotRequired = 6
}

/// <summary>How an oath of secrecy reached the record (FR-HR-030).</summary>
/// <remarks>
/// The two are kept apart deliberately. An oath affirmed in the system carries a server-stamped
/// time, the affirmer's IP and a tamper hash; one sworn on paper carries a witness and, usually, a
/// scan. Collapsing them into a single "recorded" state would let an HR-entered row be mistaken
/// later for the employee's own act — which is the distinction the whole record exists to preserve.
/// </remarks>
public enum OathAdministrationMethod
{
    /// <summary>The employee affirmed it themselves in the system.</summary>
    Affirmed = 1,

    /// <summary>Sworn on paper before a witness, and recorded afterwards by HR.</summary>
    Administered = 2
}

public enum ProbationStatus
{
    /// <summary>Running. Reviews are held, and the outcome has not been asked for yet.</summary>
    Active = 1,

    /// <summary>Confirmed: the employee's appointment is permanent.</summary>
    Completed = 2,

    /// <summary>Ended without confirmation. Area 15b records the decision and hands off.</summary>
    Terminated = 3,

    /// <summary>
    /// Submitted to the confirming authority and awaiting their decision (area 15b slice 8b).
    /// </summary>
    /// <remarks>
    /// Added because nothing could otherwise tell "running" from "out for confirmation" — on a
    /// screen or in a query — and the reminder engine would have kept chasing a form that had
    /// already gone out. Statuses are stored as int and the DB is built from the EF model, so
    /// adding members is schema-safe.
    /// </remarks>
    PendingConfirmation = 4,

    /// <summary>
    /// The authority has approved confirmation; HR has yet to record it and issue the letter.
    /// </summary>
    /// <remarks>
    /// ⚠ This exists because the FRD chain has two human steps — <i>head confirms → HR issues the
    /// confirmation letter</i> — and because a workflow status adapter is synchronous and sees only
    /// the entity, so it cannot write the employee record. Approval lands here; HR's confirm call
    /// applies <c>StaffStatus</c> and <c>ConfirmationDate</c> and moves it to <c>Completed</c>.
    /// Same shape as the proposals: leave the terminal step off the engine, because it records that
    /// the work was DONE rather than that anyone approved it.
    /// </remarks>
    ConfirmationApproved = 5
}

public enum ProbationReviewStatus
{
    Scheduled = 1,
    Completed = 2,
    Missed = 3,
    Rescheduled = 4
}

public enum ProbationReviewRecommendation
{
    Confirm = 1,
    Extend = 2,
    Terminate = 3,
    ContinueMonitoring = 4
}

public enum ProbationPerformanceRating
{
    Outstanding = 1,
    ExceedsExpectations = 2,
    MeetsExpectations = 3,
    BelowExpectations = 4,
    Unsatisfactory = 5
}

public enum TerminationReason
{
    Resignation = 1,
    Redundancy = 2,
    Dismissal = 3,
    ContractExpiry = 4,
    Retirement = 5,
    Death = 6,
    MutualAgreement = 7,
    EndOfInternship = 8,
    Other = 9
}

#endregion Recruitment

#region Disciplinary Actions

public enum StaffOffenseSeverity
{
    [Description("Minor")]
    Minor = 1,

    [Description("Moderate")]
    Moderate = 2,

    [Description("Serious")]
    Serious = 3,

    [Description("Gross Misconduct")]
    GrossMisconduct = 4
}

public enum DisciplinaryStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Reported")]
    Reported = 2,

    [Description("Under Review")]
    UnderReview = 3,

    [Description("Under Investigation")]
    UnderInvestigation = 4,

    [Description("Investigation Complete")]
    InvestigationComplete = 5,

    [Description("Hearing Scheduled")]
    HearingScheduled = 6,

    [Description("Hearing Conducted")]
    HearingConducted = 7,

    [Description("Awaiting Decision")]
    AwaitingDecision = 8,

    [Description("Decision Made")]
    DecisionMade = 9,

    [Description("Under Appeal")]
    UnderAppeal = 10,

    [Description("Closed")]
    Closed = 11,

    [Description("Dismissed")]
    Dismissed = 12,

    [Description("On Hold")]
    OnHold = 13
}

public enum DisciplinaryRepresentativeType
{
    [Description("Legal Counsel")]
    LegalCounsel = 1,

    [Description("Family Member")]
    FamilyMember = 2,

    [Description("Workplace Colleague")]
    WorkplaceColleague = 3,

    [Description("Union Representative")]
    UnionRepresentative = 4,

    [Description("Other")]
    Other = 99
}

public enum DisciplinaryWarningType
{
    [Description("Verbal Warning")]
    Verbal = 1,

    [Description("Written Warning")]
    Written = 2,

    [Description("Final Warning")]
    Final = 3,
}

public enum DisciplinaryFinePaymentStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Partially Paid")]
    PartiallyPaid = 2,

    [Description("Fully Paid")]
    FullyPaid = 3,

    [Description("Waived")]
    Waived = 4,
}

public enum DisciplinaryActionStepStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Cancelled")]
    Cancelled = 4,

    [Description("Skipped")]
    Skipped = 5,
}

public enum DisciplineAppealStatus
{
    [Description("Filed")]
    Filed = 1,

    [Description("Under Review")]
    UnderReview = 2,

    [Description("Hearing Scheduled")]
    HearingScheduled = 3,

    [Description("Hearing Conducted")]
    HearingConducted = 4,

    [Description("Awaiting Decision")]
    AwaitingDecision = 5,

    [Description("Decision Made")]
    DecisionMade = 6,

    [Description("Dismissed")]
    Dismissed = 7
}

public enum DisciplineAppealOutcomeType
{
    [Description("Upheld")]
    Upheld = 1,

    [Description("Overturned")]
    Overturned = 2,

    [Description("Reduced")]
    Reduced = 3
}

public enum DisciplinaryDocumentCategory
{
    [Description("Evidence")]
    Evidence = 1,

    [Description("Notification Letter")]
    NotificationLetter = 2,

    [Description("Statement")]
    Statement = 3,

    [Description("Report")]
    Report = 4,

    [Description("Hearing Minutes")]
    HearingMinutes = 5,

    [Description("Decision Letter")]
    DecisionLetter = 6,

    [Description("Appeal Document")]
    AppealDocument = 7,

    [Description("Legal Document")]
    LegalDocument = 8,

    [Description("Other")]
    Other = 99
}

/// <summary>
/// Identifies which context a StaffDisciplineDocument belongs to.
/// Enforced by the service layer: ActionStepId must be set when ActionStep;
/// AppealId must be set when Appeal; both must be null when Case.
/// </summary>
public enum DisciplinaryDocumentScope
{
    [Description("Case")]
    Case = 1,

    [Description("Action Step")]
    ActionStep = 2,

    [Description("Appeal")]
    Appeal = 3,
}

public enum DisciplinaryNotificationType
{
    [Description("Show Cause")]
    ShowCause = 1, // the notice to the employee to explain the allegations and the opportunity to respond

    [Description("Hearing Notice")]
    HearingNotice = 2, // the notice to the employee to attend a hearing

    [Description("Investigation Notice")]
    InvestigationNotice = 3, // the notice to the employee to participate in an investigation

    [Description("Decision Letter")]
    DecisionLetter = 4, // the notice to the employee of the decision

    [Description("Warning Letter")]
    WarningLetter = 5, // the notice to the employee of a warning

    [Description("Suspension Notice")]
    SuspensionNotice = 6, // the notice to the employee of a suspension

    [Description("Termination Letter")]
    TerminationLetter = 7, // the notice to the employee of a termination

    [Description("Appeal Outcome Notice")]
    AppealOutcomeNotice = 8, // the notice to the employee of the outcome of an appeal

    [Description("Other")]
    Other = 99 // other types of notices
}

public enum DisciplineCorrectiveActionStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Overdue")]
    Overdue = 4,

    [Description("Cancelled")]
    Cancelled = 5
}

public enum DisciplineLegalRiskLevel
{
    [Description("None")]
    None = 0,

    [Description("Low")]
    Low = 1,

    [Description("Medium")]
    Medium = 2,

    [Description("High")]
    High = 3,

    [Description("Critical")]
    Critical = 4
}

/// <summary>
/// Who may issue a disciplinary action of a given type, per FR-HR-080 and FR-HR-092.
/// </summary>
/// <remarks>
/// This lives on <see cref="ErpSystem.Core.Entities.HR.StaffDiscipline.StaffDisciplinaryActionType"/>
/// rather than being hard-coded against sanction names, because what counts as a head-of-department
/// sanction is a policy each tenant sets in its own catalog — TDC's rule is that HODs are limited to
/// verbal warnings, but the mechanism should not assume the rule.
///
/// The values are ordered by increasing authority so a comparison reads naturally
/// (<c>type.MinimumAuthority > DisciplinaryActionAuthority.HeadOfDepartment</c>). Append only:
/// this is a plain int column with no lookup table, so renumbering would silently change what
/// existing rows mean.
/// </remarks>
public enum DisciplinaryActionAuthority
{
    /// <summary>A head of department may issue this directly. FR-HR-080 puts verbal warnings here.</summary>
    [Description("Head of Department")]
    HeadOfDepartment = 1,

    /// <summary>HR issues it. The default, and the safe one for an unmaintained catalog row.</summary>
    [Description("HR")]
    Hr = 2,

    /// <summary>
    /// Requires management sign-off. FR-HR-092 puts terminations here — the MD signs all of them
    /// except the procedural cases HR approves automatically per policy.
    /// </summary>
    [Description("Management")]
    Management = 3
}

/// <summary>
/// FR-HR-181's escalation ladder: Employee → Supervisor → HOD → HR → GM Finance &amp; Administration →
/// Managing Director → Board.
/// </summary>
/// <remarks>
/// The employee is the origin, not a rung — these are the levels a grievance is answered AT, in the
/// order the requirement names them. Ordered so <c>next = current + 1</c> is the escalation, and
/// Board is the ceiling.
///
/// ⚠ These rungs are NOT resolved to people by the system. TDC's org data cannot support it: on
/// 2026-08-16, 0 of 41 organisation units had a head recorded and 175 of 1,486 active employees had a
/// manager, so deriving "this employee's HOD" would resolve to nobody for almost everyone. A
/// grievance therefore sits AT a rung and whoever answers it is recorded from their own token, with
/// HR able to name a responder explicitly per step. When an org-authority model exists it can add
/// routing on top; the ladder and the record work without it. See [[hr-deferred-modules]] #3.
/// </remarks>
public enum GrievanceEscalationLevel
{
    [Description("Supervisor")]
    Supervisor = 1,

    [Description("Head of Department")]
    HeadOfDepartment = 2,

    [Description("Human Resources")]
    HumanResources = 3,

    [Description("GM Finance & Administration")]
    GeneralManagerFinanceAdmin = 4,

    [Description("Managing Director")]
    ManagingDirector = 5,

    [Description("Board")]
    Board = 6
}

public enum GrievanceStatus
{
    [Description("Filed")]
    Filed = 1,

    [Description("Under Review")]
    UnderReview = 2,

    [Description("Escalated")]
    Escalated = 3,

    [Description("Resolved")]
    Resolved = 4,

    [Description("Withdrawn")]
    Withdrawn = 5,

    /// <summary>Closed without resolution — the ladder was exhausted at Board level.</summary>
    [Description("Closed")]
    Closed = 6
}

/// <summary>What the responder at a rung decided to do with the grievance.</summary>
public enum GrievanceStepOutcome
{
    [Description("Awaiting Response")]
    AwaitingResponse = 1,

    [Description("Resolved At This Level")]
    Resolved = 2,

    [Description("Escalated")]
    Escalated = 3
}

/// <summary>
/// What an anonymously-reported concern is about — area 9c slice 6, decision D-2.
/// </summary>
/// <remarks>
/// Kept deliberately coarse. A reporter choosing a category is telling HR how to triage, not
/// classifying themselves out of anonymity — a fine-grained list would narrow the field of possible
/// reporters, which is the opposite of the point.
/// </remarks>
public enum ConcernCategory
{
    [Description("Harassment")]
    Harassment = 1,

    [Description("Discrimination")]
    Discrimination = 2,

    [Description("Bullying")]
    Bullying = 3,

    [Description("Safety Risk")]
    SafetyRisk = 4,

    [Description("Fraud or Malpractice")]
    FraudOrMalpractice = 5,

    [Description("Misconduct")]
    Misconduct = 6,

    [Description("Other")]
    Other = 7
}

/// <summary>Where an anonymously-reported concern has got to — area 9c slice 6.</summary>
public enum ConcernStatus
{
    /// <summary>Reported, nobody has looked at it yet.</summary>
    [Description("New")]
    New = 1,

    /// <summary>HR has read it and is deciding what to do.</summary>
    [Description("Under Triage")]
    UnderTriage = 2,

    /// <summary>Being looked into, without a case having been opened.</summary>
    [Description("Under Review")]
    UnderReview = 3,

    [Description("Closed")]
    Closed = 4,

    /// <summary>Became a named employee-relations case. Terminal for the concern itself.</summary>
    [Description("Converted To Case")]
    ConvertedToCase = 5
}

/// <summary>
/// Which part of an employee-relations case a document belongs to — area 9c slice 3.
/// </summary>
/// <remarks>
/// Mirrors <see cref="DisciplinaryDocumentScope"/>, which area 9 built for the disciplinary case
/// and the grievance half never got: before slice 3 a grievance had no document surface at all, so
/// FR-HR-181's "final signed agreement" could not be held anywhere.
///
/// <para>⚠ There is deliberately no <c>Conference</c> member yet. Slice 4 builds the conference
/// entity, and a scope pointing at a table that does not exist would be an FK with nowhere to go.
/// It is appended there — this enum is append-only, like every other in this file.</para>
/// </remarks>
public enum GrievanceDocumentScope
{
    /// <summary>Belongs to the case as a whole — the complaint as filed on paper, correspondence.</summary>
    [Description("Case")]
    Case = 1,

    /// <summary>Attached to one rung's answer.</summary>
    [Description("Ladder Step")]
    Step = 2,

    /// <summary>Evidence gathered during the investigation, or the report itself.</summary>
    [Description("Investigation")]
    Investigation = 3,

    /// <summary>FR-HR-181 obligation 9 — the final signed agreement. At most one per case.</summary>
    [Description("Signed Agreement")]
    Agreement = 4,

    /// <summary>Papers tabled at, or minutes of, a conference — appended by slice 4 as promised.</summary>
    [Description("Conference")]
    Conference = 5
}

/// <summary>
/// What kind of meeting was convened on an employee-relations case — area 9c slice 4, decision D-8.
/// </summary>
/// <remarks>
/// One entity for three uses rather than three tables: a case conference, a mediation and a union
/// consultation are the same shape — a meeting convened on a date, at a place, chaired by somebody,
/// attended by named people, producing notes and an outcome. What differs is why it was called, and
/// that is this enum.
///
/// <para><see cref="UnionConsultation"/> is FR-HR-181's sixth obligation: the requirement names
/// "union consultation notes" among the things a grievance must retain, and before slice 4 there was
/// nowhere in the schema to put them.</para>
/// </remarks>
public enum GrievanceConferenceType
{
    /// <summary>The parties and the desk sit down to review the case.</summary>
    [Description("Case Conference")]
    CaseConference = 1,

    /// <summary>A neutral third party works to settle it between the parties.</summary>
    [Description("Mediation")]
    Mediation = 2,

    /// <summary>FR-HR-181 obligation 6 — consultation with a recognised union.</summary>
    [Description("Union Consultation")]
    UnionConsultation = 3
}

/// <summary>Where a convened meeting got to — area 9c slice 4.</summary>
public enum GrievanceConferenceStatus
{
    [Description("Scheduled")]
    Scheduled = 1,

    /// <summary>It happened, and its notes and outcome are on the record.</summary>
    [Description("Held")]
    Held = 2,

    [Description("Cancelled")]
    Cancelled = 3
}

/// <summary>
/// What a resolved employee-relations case actually decided — FR-HR-181's "resolution decision",
/// area 9c slice 2.
/// </summary>
/// <remarks>
/// <para><b>Why <see cref="NotRecorded"/> exists, and why it is member 1.</b> Before slice 2 the
/// only way to resolve a case was <c>respond(resolvesGrievance: true)</c>, which copied the
/// responder's answer into <c>ResolutionSummary</c> and captured no outcome at all — an answer and a
/// decision were the same field. That path still works, so that the portal screen calling it does
/// not break, but it now produces a resolution artefact marked <c>NotRecorded</c>.</para>
///
/// <para>That is deliberately an honest gap rather than an invented value: it says "this case was
/// resolved and nobody captured what was decided", which HR can be asked to fill in — and
/// <c>POST resolve</c> permits exactly that one transition, filling a <c>NotRecorded</c> outcome
/// while refusing to amend a real one. A real decision, once recorded, is frozen.</para>
/// </remarks>
public enum GrievanceResolutionOutcome
{
    /// <summary>Resolved, but what was decided was never captured. ⚠ Not selectable — see remarks.</summary>
    [Description("Not Recorded")]
    NotRecorded = 1,

    [Description("Upheld In Full")]
    UpheldInFull = 2,

    [Description("Upheld In Part")]
    UpheldInPart = 3,

    [Description("Not Upheld")]
    NotUpheld = 4,

    /// <summary>The parties reached an agreement rather than the case being adjudicated.</summary>
    [Description("Settled By Agreement")]
    SettledByAgreement = 5,

    [Description("No Further Action")]
    NoFurtherAction = 6
}

/// <summary>
/// What kind of employee-relations case this is — area 9c slice 1, decision D-4.
/// </summary>
/// <remarks>
/// <para>Area 9 slice 7 built one store for one thing: the FR-HR-181 grievance. An employee-relations
/// function handles more than grievances — a conflict two people want mediated, a welfare or
/// counselling matter, a consultation with the union — and before this enum existed those had
/// nowhere to live at all, so they were kept off the system entirely.</para>
///
/// <para><b>Grievance is deliberately member 1</b>, so that the column defaults to it and every row
/// written before this existed is correct without a data fix. Nothing else may be renumbered for the
/// same reason.</para>
///
/// <para>⚠ The case type is <i>not</i> a permission boundary and must never become one. What may be
/// read is decided by who is on the case (the primary party, HR, or somebody named on a step or as a
/// party), exactly as it was for the grievance alone.</para>
/// </remarks>
public enum EmployeeRelationsCaseType
{
    /// <summary>FR-HR-181's formal grievance, escalating up the six-rung ladder.</summary>
    [Description("Grievance")]
    Grievance = 1,

    /// <summary>A dispute between colleagues that is being mediated rather than adjudicated.</summary>
    [Description("Conflict / Mediation")]
    ConflictMediation = 2,

    /// <summary>A welfare or counselling matter — hardship, bereavement, wellbeing support.</summary>
    [Description("Welfare / Counselling")]
    WelfareCounselling = 3,

    /// <summary>A consultation with a recognised union, which FR-HR-181 requires be retained.</summary>
    [Description("Union Consultation")]
    UnionConsultation = 4,

    /// <summary>Employee-relations work that fits none of the above. Kept last.</summary>
    [Description("Other")]
    Other = 5
}

/// <summary>
/// The kinds of record an employee-relations case can be cross-referenced to — area 9c slice 9.
/// </summary>
/// <remarks>
/// <para>Deliberately a CLOSED enum with one real foreign key behind each member, not a
/// polymorphic (type-name, id) pair. A polymorphic link cannot be enforced by the database, so it
/// rots the first time a source row is deleted and nothing complains; these three have real FKs,
/// real referential integrity, and no way to point at a table that does not exist.</para>
///
/// <para><b>The link is a POINTER, never a window.</b> Following it takes the reader into the
/// source module, where that module's own permission decides what they see. Nothing about the
/// source's substance — an offence, a set of findings, an injury — is copied onto the
/// employee-relations case file.</para>
/// </remarks>
public enum EmployeeRelationsLinkSource
{
    /// <summary>A SHE incident the case arose from or concerns.</summary>
    [Description("Safety Incident")]
    SafetyIncident = 1,

    /// <summary>A performance improvement plan the case arose from or concerns.</summary>
    [Description("Performance Improvement Plan")]
    PerformanceImprovementPlan = 2,

    /// <summary>A disciplinary case the case arose from or concerns.</summary>
    [Description("Disciplinary Case")]
    DisciplinaryCase = 3
}

/// <summary>
/// The part somebody plays in an employee-relations case, beyond the primary party — area 9c
/// slice 1, decision D-7.
/// </summary>
/// <remarks>
/// The case's own <c>EmployeeId</c> is the primary party and is never repeated here; adding them as
/// a party is refused. Everybody else on a case — the person complained of, a representative, a
/// union official, a witness, a mediator — is one of these.
/// </remarks>
public enum GrievancePartyRole
{
    /// <summary>A further employee the case concerns, alongside the primary party.</summary>
    [Description("Affected Employee")]
    AffectedEmployee = 1,

    /// <summary>The person the case is about. ⚠ Being a respondent does NOT confer a right to read.</summary>
    [Description("Respondent")]
    Respondent = 2,

    /// <summary>Acts for another party — a colleague, a friend, a lawyer.</summary>
    [Description("Representative")]
    Representative = 3,

    /// <summary>Acts for another party on behalf of a recognised union.</summary>
    [Description("Union Representative")]
    UnionRepresentative = 4,

    [Description("Witness")]
    Witness = 5,

    /// <summary>Convenes and chairs a mediation between the parties.</summary>
    [Description("Mediator")]
    Mediator = 6
}

public enum EmployeeTerminationType
{
    [Description("Involuntary For Cause")]
    InvoluntaryForCause = 1,

    [Description("Involuntary Performance")]
    InvoluntaryPerformance = 2,

    [Description("Involuntary Redundancy")]
    InvoluntaryRedundancy = 3,

    [Description("Voluntary Resignation")]
    VoluntaryResignation = 4,

    [Description("Voluntary Retirement")]
    VoluntaryRetirement = 5,

    [Description("Mutual Agreement")]
    MutualAgreement = 6,

    [Description("Contract Expiry")]
    ContractExpiry = 7,

    [Description("Death")]
    Death = 8,

    /// <summary>
    /// FR-HR-179's sanction ladder names summary dismissal separately from termination, and it is a
    /// distinct thing: dismissal without notice or pay in lieu, for conduct grave enough to end the
    /// contract immediately.
    /// </summary>
    /// <remarks>
    /// ⚠ "Summary" means without NOTICE, not without PROCESS. A summary dismissal still requires the
    /// employee to have been queried and heard — the natural-justice gate in
    /// <c>StaffDisciplinaryCaseService</c> has no carve-out for it, deliberately.
    ///
    /// Appended as 9, immediately after Death=8 and before Other=99. This column is a plain int with
    /// no lookup table or check constraint, so appending is schema-safe and needs no migration — but
    /// members must be APPENDED, never renumbered, or existing rows silently change meaning. The same
    /// call was made for PositionChangeReason in area 8 slice 4.
    /// </remarks>
    [Description("Summary Dismissal")]
    SummaryDismissal = 9,

    /// <summary>
    /// Retirement on reaching the compulsory age — 60 at TDC, effective on the birthday
    /// (FR-HR-093). Distinct from <see cref="VoluntaryRetirement"/>, which the employee elects
    /// from the voluntary age (55 by default): one is the organisation applying a rule, the other
    /// is a person exercising a choice, and they differ in notice, approval and entitlement.
    /// </summary>
    /// <remarks>
    /// Appended as 10 for area 9b. FR-HR-182 requires compulsory and medical retirement as
    /// separate separation types and neither existed — the enum offered only VoluntaryRetirement,
    /// so a compulsory retirement had to be recorded as something it was not. Appended, never
    /// renumbered, per the note on <see cref="SummaryDismissal"/>.
    /// </remarks>
    [Description("Compulsory Retirement")]
    CompulsoryRetirement = 10,

    /// <summary>
    /// Retirement on medical grounds — the employee is permanently unfit to continue, evidenced by
    /// a medical report, before either retirement age is reached (FR-HR-182).
    /// </summary>
    /// <remarks>
    /// Appended as 11 for area 9b; see <see cref="CompulsoryRetirement"/>. This is the separation
    /// type that reads across to the SHE/medical surveillance and return-to-work records rather
    /// than replacing them — the boundary is unchanged.
    /// </remarks>
    [Description("Medical Retirement")]
    MedicalRetirement = 11,

    [Description("Other")]
    Other = 99
}

/// <summary>
/// Where a separation has reached (area 9b). The order is the FRD's own sequence, and it matters:
/// FR-HR-091 requires a completed clearance form <b>before</b> the separation, and <i>then</i>
/// entitlements are computed — so clearance precedes settlement, never the reverse. FR-HR-185 then
/// puts Internal Audit between a prepared settlement and a paid one.
/// </summary>
/// <remarks>
/// <c>Approved</c> means the FR-HR-092 signature is in — the MD's, or HR's own where the
/// separation is procedural. Nothing that computes money may run before <c>ClearanceCompleted</c>,
/// and nothing may pay before <c>SettlementApproved</c>.
/// </remarks>
public enum SeparationStatus
{
    /// <summary>Raised and still editable; no approval sought.</summary>
    [Description("Draft")]
    Draft = 1,

    /// <summary>Submitted; awaiting the MD's signature, or HR's where procedural (FR-HR-092).</summary>
    [Description("Pending Approval")]
    PendingApproval = 2,

    [Description("Approved")]
    Approved = 3,

    /// <summary>The FR-HR-183 clearance run is open; items are being signed off.</summary>
    [Description("Clearance In Progress")]
    ClearanceInProgress = 4,

    /// <summary>Every required clearance item is signed off — the FR-HR-091 gate is now open.</summary>
    [Description("Clearance Completed")]
    ClearanceCompleted = 5,

    /// <summary>The FR-HR-184 settlement statement is being prepared.</summary>
    [Description("Settlement Pending")]
    SettlementPending = 6,

    /// <summary>Statement prepared; with Internal Audit for review (FR-HR-185).</summary>
    [Description("Settlement Under Review")]
    SettlementUnderReview = 7,

    /// <summary>Internal Audit has reviewed it; payment may be released.</summary>
    [Description("Settlement Approved")]
    SettlementApproved = 8,

    /// <summary>Paid, and applied to the employee's master record.</summary>
    [Description("Completed")]
    Completed = 9,

    /// <summary>Withdrawn before completion — a resignation retracted, a retirement deferred.</summary>
    [Description("Cancelled")]
    Cancelled = 10,

    /// <summary>Refused at approval.</summary>
    [Description("Rejected")]
    Rejected = 11
}

/// <summary>
/// Internal Audit's verdict on a final settlement (FR-HR-185: <i>"require Internal Audit review of
/// the final settlement before payment is released"</i>).
/// </summary>
/// <remarks>
/// ⚠ <b>Nobody holds <c>TDC_INTERNAL_AUDIT</c> on the live tenant</b> (measured 2026-08-20, zero
/// members). The control is built as specified and will therefore hold every settlement at
/// <see cref="NotReviewed"/> until somebody is granted the role — correct behaviour, and it will
/// look like a stuck queue to whoever meets it first. Raised with TDC as an operational
/// prerequisite, not a development gap.
/// </remarks>
public enum SettlementReviewOutcome
{
    /// <summary>Not yet seen by Internal Audit.</summary>
    [Description("Not Reviewed")]
    NotReviewed = 1,

    /// <summary>Reviewed and passed. Payment may be released.</summary>
    [Description("Approved")]
    Approved = 2,

    /// <summary>
    /// Sent back to HR with findings. The statement becomes editable again and must be corrected
    /// and re-finalised — a return is not a refusal of the separation, only of the figures.
    /// </summary>
    [Description("Returned")]
    Returned = 3
}

/// <summary>
/// The main reason a leaver gives at their exit interview.
/// </summary>
/// <remarks>
/// <para>Separate from <c>TerminationReason</c> on purpose, and the distinction is the whole value
/// of an exit interview: <c>TerminationReason</c> is what the <b>organisation</b> records — a
/// resignation — while this is what the <b>employee</b> says was behind it. "Resignation, because
/// the pay was uncompetitive" and "resignation, because of their manager" are the same termination
/// reason and completely different facts, and only the second one tells anybody what to fix.</para>
///
/// <para>Kept to a short list on purpose. A long taxonomy nobody can hold in their head gets
/// answered with "Other", and then the analytics say nothing.</para>
/// </remarks>
public enum ExitInterviewReason
{
    [Description("Pay And Benefits")]
    PayAndBenefits = 1,

    [Description("Career Progression")]
    CareerProgression = 2,

    [Description("Management Or Supervision")]
    ManagementOrSupervision = 3,

    [Description("Workload Or Stress")]
    WorkloadOrStress = 4,

    [Description("Work-Life Balance")]
    WorkLifeBalance = 5,

    [Description("Working Conditions")]
    WorkingConditions = 6,

    [Description("Relationship With Colleagues")]
    RelationshipWithColleagues = 7,

    [Description("Job Security")]
    JobSecurity = 8,

    [Description("Relocation")]
    Relocation = 9,

    [Description("Health Or Personal")]
    HealthOrPersonal = 10,

    /// <summary>Retirement, contract expiry, death — an exit nobody chose for a reason.</summary>
    [Description("Not Applicable — End Of Service")]
    EndOfService = 11,

    [Description("Other")]
    Other = 99
}

/// <summary>
/// What a line of the final settlement is (FR-HR-184: <i>"unpaid salary, notice pay, leave
/// encashment, benefits, deductions, recoveries, loans and pension-related payments"</i>).
/// </summary>
public enum SettlementLineCategory
{
    [Description("Unpaid Salary")]
    UnpaidSalary = 1,

    /// <summary>Notice not served and not waived — paid in lieu.</summary>
    [Description("Notice Pay")]
    NoticePay = 2,

    /// <summary>Accrued leave paid out. Exit only (FR-HR-046), capped at 56 days (FR-HR-152).</summary>
    [Description("Leave Encashment")]
    LeaveEncashment = 3,

    [Description("Gratuity Or End Of Service")]
    GratuityOrEndOfService = 4,

    [Description("Benefit Payment")]
    BenefitPayment = 5,

    [Description("Pension-Related Payment")]
    PensionRelated = 6,

    [Description("Other Earning")]
    OtherEarning = 49,

    [Description("Loan Repayment")]
    LoanRepayment = 50,

    [Description("Salary Advance Recovery")]
    SalaryAdvanceRecovery = 51,

    /// <summary>An outstanding travel advance, from the employee's own travel records.</summary>
    [Description("Travel Advance Recovery")]
    TravelAdvanceRecovery = 52,

    /// <summary>Unreturned property or equipment, carried from a clearance line.</summary>
    [Description("Property Recovery")]
    PropertyRecovery = 53,

    [Description("Tax Deduction")]
    TaxDeduction = 54,

    [Description("Other Deduction")]
    OtherDeduction = 99
}

/// <summary>
/// Where a settlement line's amount came from — and whether it is trustworthy.
/// </summary>
/// <remarks>
/// ⚠ This enum exists because of a measurement, not for tidiness. On the live tenant
/// 2026-08-20, <b>202 of 3,883 employees</b> have a salary on file and <c>LeaveBalances</c> holds
/// <b>zero</b> rows — so a settlement that simply computed from salary would print 0.00 for almost
/// everybody. Zero and "we could not work it out" are not the same statement, and somebody would
/// sign the first one. <see cref="CannotCompute"/> keeps them apart, and blocks finalisation until
/// a human supplies the figure.
/// </remarks>
public enum SettlementLineComputation
{
    /// <summary>Worked out by the system from data it holds. <c>Basis</c> says how.</summary>
    [Description("Computed")]
    Computed = 1,

    /// <summary>Entered by a person. <c>SourceReference</c> says where they got it.</summary>
    [Description("Manually Entered")]
    ManuallyEntered = 2,

    /// <summary>
    /// The system knows this line is owed but cannot value it — no salary on record, no leave
    /// balance, no payroll figure. Carries no amount, and holds the statement open.
    /// </summary>
    [Description("Cannot Compute")]
    CannotCompute = 3
}

/// <summary>
/// What a clearance item checks — FR-HR-183's list, verbatim: <i>"exit clearance across outstanding
/// loans, salary advances, company property, office equipment, duty-post keys, documents and
/// payroll recoveries"</i>.
/// </summary>
/// <remarks>
/// The kind is what makes an item mean something rather than being a line of free text. Two things
/// hang off it: whether the item can carry an outstanding <b>amount</b> (a loan can, a set of keys
/// cannot), and which system will eventually be able to answer it automatically — loans, advances
/// and recoveries live in payroll, property and equipment in HR Assets (area 16, unbuilt).
/// </remarks>
public enum ClearanceItemKind
{
    [Description("Outstanding Loan")]
    OutstandingLoan = 1,

    [Description("Salary Advance")]
    SalaryAdvance = 2,

    [Description("Company Property")]
    CompanyProperty = 3,

    [Description("Office Equipment")]
    OfficeEquipment = 4,

    [Description("Duty-Post Keys")]
    DutyPostKeys = 5,

    [Description("Documents And Records")]
    DocumentsAndRecords = 6,

    [Description("Payroll Recovery")]
    PayrollRecovery = 7,

    [Description("Other")]
    Other = 99
}

/// <summary>Where a single clearance item has got to.</summary>
public enum ClearanceItemStatus
{
    /// <summary>Nobody has answered it yet.</summary>
    [Description("Pending")]
    Pending = 1,

    /// <summary>Answered and settled — nothing outstanding, or what was outstanding has been returned.</summary>
    [Description("Cleared")]
    Cleared = 2,

    /// <summary>
    /// Answered and NOT settled: something is still owed or unreturned. A blocking answer, and the
    /// reason the FR-HR-091 gate exists.
    /// </summary>
    [Description("Blocked")]
    Blocked = 3,

    /// <summary>
    /// Deliberately set aside — the item does not apply to this person, or what is outstanding is
    /// being carried into the final settlement instead of recovered first. Requires a reason.
    /// </summary>
    [Description("Waived")]
    Waived = 4,

    /// <summary>Does not apply to this separation at all.</summary>
    [Description("Not Applicable")]
    NotApplicable = 5
}

/// <summary>
/// What a document attached to a separation is (area 9b). The category is what makes the file
/// findable years later, when the question is "show me the clearance form" rather than "show me
/// the attachments".
/// </summary>
public enum SeparationDocumentCategory
{
    /// <summary>The employee's own letter. The origin of a resignation, and its date of record.</summary>
    [Description("Resignation Letter")]
    ResignationLetter = 1,

    /// <summary>The organisation's reply accepting the resignation, or its notice of termination.</summary>
    [Description("Acceptance Or Notice Letter")]
    AcceptanceOrNoticeLetter = 2,

    /// <summary>The signed clearance form FR-HR-091 requires before entitlements are computed.</summary>
    [Description("Clearance Form")]
    ClearanceForm = 3,

    /// <summary>The FR-HR-184 final settlement statement, and the FR-HR-185 review of it.</summary>
    [Description("Settlement Statement")]
    SettlementStatement = 4,

    [Description("Exit Interview Record")]
    ExitInterviewRecord = 5,

    /// <summary>Supports a medical retirement — the evidence that the employee is permanently unfit.</summary>
    [Description("Medical Report")]
    MedicalReport = 6,

    /// <summary>Supports a separation by death, and the entitlement paid to the estate.</summary>
    [Description("Death Certificate")]
    DeathCertificate = 7,

    /// <summary>A handover note, a certificate of service, correspondence.</summary>
    [Description("Other")]
    Other = 99
}

#endregion Disciplinary Actions

#region Training Management

public enum TrainingVendorType
{
    [Description("Individual Consultant")]
    IndividualConsultant = 1,

    [Description("Training Firm")]
    TrainingFirm = 2,

    [Description("Accredited Institution")]
    AccreditedInstitution = 3,

    [Description("University / Tertiary")]
    University = 4,

    [Description("Government Agency")]
    GovernmentAgency = 5,

    [Description("NGO / Non-Profit")]
    NGO = 6,

    [Description("Other")]
    Other = 7
}

public enum VendorAccreditationStatus
{
    [Description("Active")]
    Active = 1,

    [Description("Pending")]
    Pending = 2,

    [Description("Expired")]
    Expired = 3,

    [Description("Suspended")]
    Suspended = 4,

    [Description("Revoked")]
    Revoked = 5
}

public enum TrainingCategory
{
    [Description("Technical Skills")]
    Technical = 1,

    [Description("Soft Skills")]
    SoftSkills = 2,

    [Description("Leadership Development")]
    Leadership = 3,

    [Description("Compliance/Regulatory")]
    Compliance = 4,

    [Description("Health & Safety")]
    HealthSafety = 5,

    [Description("Customer Service")]
    CustomerService = 6,

    [Description("Product Knowledge")]
    ProductKnowledge = 7,

    [Description("Management")]
    Management = 8,

    [Description("Professional Certification")]
    ProfessionalCertification = 9,

    [Description("Other")]
    Other = 10
}

// NOTE: Internal/External/Online were moved out of TrainingType into the new TrainingSource
// enum (they classify the *provenance* of a training, not its *format*). Values 4+ keep their
// original integers so existing rows stay valid; a data migration remaps old 1/2/3 rows.
public enum TrainingType
{
    [Description("Workshop")]
    Workshop = 4,

    [Description("Seminar")]
    Seminar = 5,

    [Description("Conference")]
    Conference = 6,

    [Description("On-the-Job Training")]
    OnTheJob = 7,

    [Description("Mentoring/Coaching")]
    Mentoring = 8,

    [Description("Certification Program")]
    Certification = 9
}

/// <summary>
/// What a trainer is engaged on during an availability/blocked window — helps decide whether a
/// higher-priority training can pull them off it.
/// </summary>
public enum TrainerEngagementType
{
    [Description("Internal Engagement")]
    Internal = 1,

    [Description("External Engagement")]
    External = 2,

    [Description("Other")]
    Other = 3
}

/// <summary>
/// Where/how a training is sourced. Applied in combination with <see cref="TrainingType"/>
/// (e.g. an External Workshop, or an Internal On-the-Job session).
/// </summary>
public enum TrainingSource
{
    [Description("Internal")]
    Internal = 1,

    [Description("External")]
    External = 2,

    [Description("Online / E-Learning")]
    OnlineELearning = 3
}

public enum TrainingLevel
{
    [Description("Beginner")]
    Beginner = 1,

    [Description("Intermediate")]
    Intermediate = 2,

    [Description("Advanced")]
    Advanced = 3,

    [Description("Expert")]
    Expert = 4,

    [Description("Any Level")]
    AnyLevel = 5
}

public enum TrainingCompletionStatus
{
    NotStarted = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    Incomplete = 5,
    Exempted = 6
}

public enum TrainingAssessmentType
{
    PreTraining = 1,
    PostTraining = 2,
    FollowUp30Day = 3,
    FollowUp60Day = 4,
    FollowUp90Day = 5
}

public enum CertificateStatus
{
    Active = 1,
    Expired = 2,
    Revoked = 3,
    Pending = 4
}

public enum ComplianceFrequency
{
    OneTime = 1,
    Annual = 2,
    BiAnnual = 3,
    Quarterly = 4,
    Monthly = 5,
    Custom = 6
}

public enum TrainingBudgetStatus
{
    Draft = 1,
    Approved = 2,
    Denied = 3,
    Active = 4,
    Closed = 5
}

public enum TrainingWaitlistStatus
{
    Active = 1,
    Offered = 2,
    Accepted = 3,
    Declined = 4,
    Expired = 5,
    Removed = 6
}

public enum TrainingRequestStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5
}

public enum LearningPathStatus
{
    Draft = 0,

    Active = 1,

    Inactive = 2,
}

/// <summary>
/// The status scale used for <c>EmployeeLearningPathStep</c> rows in <c>TrainingStatusHistory</c>.
///
/// The step entity itself only stores a boolean, so this is what preserves the distinction that
/// matters at audit time: whether a completion rested on evidence or on someone's authority. A
/// learning path can award a certificate that outside parties verify, so "who said so, and on what
/// basis" has to survive on the record rather than in the boolean.
/// </summary>
public enum LearningPathStepStatus
{
    NotCompleted = 0,

    /// <summary>Backed by attendance or a completion record.</summary>
    Completed = 1,

    /// <summary>Recorded by HR without evidence, against a mandatory reason.</summary>
    CompletedByOverride = 2,
}

public enum MentoringStatus
{
    Active = 1,
    Paused = 2,
    Completed = 3,
    Cancelled = 4
}

public enum MentoringSessionFormat
{
    InPerson = 1,

    Virtual = 2,

    Hybrid = 3
}

public enum ScheduleStatus
{
    [Description("Planned")]
    Planned = 1,

    [Description("Registration Open")]
    RegistrationOpen = 2,

    [Description("Registration Closed")]
    RegistrationClosed = 3,

    [Description("In Progress")]
    InProgress = 4,

    [Description("Completed")]
    Completed = 5,

    [Description("Cancelled")]
    Cancelled = 6,

    [Description("Postponed")]
    Postponed = 7
}

public enum NominationType
{
    [Description("Self-Nomination")]
    Self = 1,

    [Description("Supervisor Nomination")]
    Supervisor = 2,

    [Description("HR Nomination")]
    HR = 3,

    [Description("Management Nomination")]
    Management = 4,

    [Description("Mandatory Training")]
    Mandatory = 5
}

public enum NominationStatus
{
    [Description("Draft")]
    Draft = 0,

    [Description("Submitted")]
    Submitted = 1,

    [Description("Supervisor Review")]
    SupervisorReview = 2,

    [Description("HR Review")]
    HrReview = 3,

    [Description("Approved")]
    Approved = 4,

    [Description("Rejected")]
    Rejected = 5,

    [Description("Waitlisted")]
    Waitlisted = 6,

    [Description("Confirmed")]
    Confirmed = 7,

    [Description("Withdrawn")]
    Withdrawn = 8
}

public enum MaterialType
{
    [Description("Handbook")]
    Handbook = 1,

    [Description("Presentation Slides")]
    Slides = 2,

    [Description("Video")]
    Video = 3,

    [Description("Document")]
    Document = 4,

    [Description("Exercise/Activity")]
    Exercise = 5,

    [Description("Assessment")]
    Assessment = 6,

    [Description("Reference Material")]
    Reference = 7
}

public enum TrainingPlanStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Pending Approval")]
    PendingApproval = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("In Progress")]
    InProgress = 4,

    [Description("Completed")]
    Completed = 5,

    [Description("Revised")]
    Revised = 6
}

public enum AssessmentSource
{
    [Description("Performance Review")]
    PerformanceReview = 1,

    [Description("Self Assessment")]
    SelfAssessment = 2,

    [Description("Manager Request")]
    ManagerRequest = 3,

    [Description("Skills Gap Analysis")]
    SkillsGapAnalysis = 4,

    [Description("Job Role Change")]
    JobRoleChange = 5
}

public enum TrainingPriority
{
    [Description("Critical")]
    Critical = 1,

    [Description("High")]
    High = 2,

    [Description("Medium")]
    Medium = 3,

    [Description("Low")]
    Low = 4
}

/// <summary>
/// Lifecycle of a training service bond (binding service-obligation agreement tied to a nomination).
/// </summary>
public enum TrainingBondStatus
{
    /// <summary>Bond created, awaiting the employee's (or HR-on-behalf) acceptance of the terms.</summary>
    [Description("Pending Acceptance")]
    PendingAcceptance = 1,

    /// <summary>Accepted; the service obligation period is running.</summary>
    [Description("Active")]
    Active = 2,

    /// <summary>The full service obligation was served — no repayment owed.</summary>
    [Description("Fulfilled")]
    Fulfilled = 3,

    /// <summary>Employee exited before the obligation ended — a pro-rated repayment is owed.</summary>
    [Description("Breached")]
    Breached = 4,

    /// <summary>The owed repayment has been settled / resolved.</summary>
    [Description("Settled")]
    Settled = 5,

    /// <summary>Obligation waived by HR — no repayment pursued.</summary>
    [Description("Waived")]
    Waived = 6,

    /// <summary>Cancelled before acceptance (e.g. the nomination was withdrawn or rejected).</summary>
    [Description("Cancelled")]
    Cancelled = 7
}

#endregion Training Management

#region Staff Awards

public enum AwardCategory
{
    [Description("Performance Excellence")]
    Performance = 1,

    [Description("Long Service")]
    LongService = 2,

    [Description("Innovation")]
    Innovation = 3,

    [Description("Customer Service")]
    CustomerService = 4,

    [Description("Safety")]
    Safety = 5,

    [Description("Team Player")]
    TeamPlayer = 6,

    [Description("Leadership")]
    Leadership = 7,

    [Description("Special Recognition")]
    SpecialRecognition = 8,

    [Description("Other")]
    Other = 9
}

public enum AwardFrequency
{
    [Description("Monthly")]
    Monthly = 1,

    [Description("Quarterly")]
    Quarterly = 2,

    [Description("Annual")]
    Annual = 3,

    [Description("Ad-hoc")]
    AdHoc = 4
}

public enum AwardScope
{
    Employee = 1,

    Position = 2,

    StaffLevel = 3,

    OrganizationUnit = 4
}

public enum AwardTargetType
{
    OrganizationUnit = 1,

    Position = 2,

    StaffLevel = 3,

    Employee = 4,
}

/// <summary>
/// Where the candidates for an award come from.
/// </summary>
/// <remarks>
/// <para>TDC's <i>Staff Awards Changes</i> note describes three distinct origins, and they are not
/// interchangeable: <i>"employees or management will do the nomination"</i>, <i>"some of the
/// nomination will be due to performance or target reached"</i>, and <i>"some too will have to be a
/// direct selection by management"</i>.</para>
///
/// <para>This replaces the decorative <c>AutoGenerateNominees</c> flag, which was mapped through
/// every DTO and read by nothing. Keeping both would have left two fields meaning the same thing
/// and free to disagree.</para>
/// </remarks>
public enum AwardNominationSource
{
    /// <summary>Employees or management nominate. The document's main flow.</summary>
    [Description("Open nomination")]
    OpenNomination = 1,

    /// <summary>The system derives the candidates from performance results or targets reached.</summary>
    [Description("Performance or target triggered")]
    PerformanceTriggered = 2,

    /// <summary>There is no nomination stage at all — management names the recipient.</summary>
    [Description("Direct management selection")]
    ManagementDirect = 3
}

/// <summary>
/// What a target on an award type is scoping: who may win it, or who may vote in it.
/// </summary>
/// <remarks>
/// <para>TDC's note asks for both, and they are different sets: <i>"management will set the criteria
/// and then it will qualify some employees"</i> is who may win, while <i>"a section of the employees
/// or all of them can vote on the nominees"</i> is who may vote. A department might nominate from
/// its own staff but let the whole company vote, or the reverse.</para>
///
/// <para>They share one table because they are the same shape — an organisation unit, a position, a
/// staff level or a named person, included or excluded, optionally effective-dated — and because
/// sharing it means the name resolver and the matching logic are written once. <c>Eligibility</c> is
/// the default, so every target written before voting existed keeps meaning what it meant.</para>
/// </remarks>
public enum AwardTargetPurpose
{
    /// <summary>Scopes who may receive the award.</summary>
    [Description("Eligible to win")]
    Eligibility = 1,

    /// <summary>Scopes who may vote in it. An award with none of these is voted on by everyone.</summary>
    [Description("Eligible to vote")]
    Electorate = 2
}

/// <summary>
/// Where an award cycle is in its life, which is not the same question as whether its windows are open.
/// </summary>
/// <remarks>
/// Deliberately excludes "NominationsOpen" and "VotingOpen". Those are answered by comparing the
/// clock to <c>AwardCycle.NominationOpensOn</c> / <c>VotingOpensOn</c>, so they cannot drift out of
/// step with the dates they describe. A status that says a window is open while the dates say it
/// closed yesterday is the two-sources-of-one-fact defect this area keeps producing.
/// </remarks>
public enum AwardCycleStatus
{
    /// <summary>Being set up. Not visible to employees, and nothing may be nominated to it.</summary>
    [Description("Draft")]
    Draft = 1,

    /// <summary>Live. Its windows govern what may happen and when.</summary>
    [Description("Published")]
    Published = 2,

    /// <summary>The winner has been decided and the cycle is finished.</summary>
    [Description("Completed")]
    Completed = 3,

    /// <summary>Abandoned before completion. Kept so its nominations still have a home.</summary>
    [Description("Cancelled")]
    Cancelled = 4
}

/// <summary>
/// How the winner is chosen once there are candidates.
/// </summary>
/// <remarks>
/// <para>Deliberately separate from <see cref="AwardNominationSource"/>, because the source and the
/// decision vary independently in TDC's note. <i>"HR will setup the eligibility criteria, then
/// employees or management will do the nomination, and then staff can vote"</i> pairs open
/// nomination with a vote; <i>"some might not have to go through the employee vote since management
/// will decide and award"</i> pairs the same open nomination with a management decision; and the
/// committee section pairs it with scoring. Collapsing the two axes into one field would force a
/// fixed menu of combinations and lose real ones.</para>
///
/// <para>One combination is invalid and is refused: a <see cref="AwardNominationSource.ManagementDirect"/>
/// award cannot be decided by <see cref="StaffVote"/> or <see cref="CommitteeScore"/>, because there
/// is no candidate list for anyone to vote on or score.</para>
/// </remarks>
public enum AwardWinnerDecision
{
    /// <summary>Eligible employees vote on the shortlist; most votes wins.</summary>
    [Description("Staff vote")]
    StaffVote = 1,

    /// <summary>The award committee scores each nominee; the highest average score wins.</summary>
    [Description("Committee score")]
    CommitteeScore = 2,

    /// <summary>Management decides outright, with no vote and no scoring.</summary>
    [Description("Management decision")]
    ManagementDecision = 3
}

public enum AwardStatus
{
    [Description("Nominated")]
    Nominated = 1,

    [Description("Under Review")]
    UnderReview = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Rejected")]
    Rejected = 4,

    [Description("Presented")]
    Presented = 5,

    [Description("Cancelled")]
    Cancelled = 6
}

public enum AwardAttachmentType
{
    [Description("Photo")]
    Photo = 1,

    [Description("Certificate")]
    Certificate = 2,

    [Description("Citation")]
    Citation = 3,

    [Description("Supporting Document")]
    SupportingDocument = 4
}

public enum AwardNominationStatus
{
    [Description("Draft")]
    Draft = 0,

    [Description("Submitted")]
    Submitted = 1,

    [Description("Under Review")]
    UnderReview = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Rejected")]
    Rejected = 4,

    [Description("Withdrawn")]
    Withdrawn = 5,
}

#endregion Staff Awards

#region Staff Accidents and Safety

// NOTE: The former Staff Accident / Safety / Inspection enums (StaffAccidentType,
// AccidentSeverity, InjuryType, AccidentStatus, AccidentDocumentType, InspectionType,
// InspectionStatus) were removed when the SHE module migrated to She-prefixed enums in
// ErpSystem.Core.Enums.Safety (see SafetyEnums.cs). ComplianceStatus is retained below
// because the Training module (EmployeeComplianceRecord) still depends on it.

public enum ComplianceStatus
{
    [Description("Compliant")]
    Compliant = 1,

    [Description("Non-Compliant")]
    NonCompliant = 2,

    [Description("Partially Compliant")]
    PartiallyCompliant = 3,

    [Description("Not Applicable")]
    NotApplicable = 4
}

#endregion Staff Accidents and Safety

#region Health and Medical Expenses

public enum DisabilityStatus
{
    [Description("None")]
    None = 1,

    [Description("Mild")]
    Mild = 2,

    [Description("Moderate")]
    Moderate = 3,

    [Description("Severe")]
    Severe = 4
}

public enum BloodGroup
{
    [Description("A Positive")]
    APositive = 1,

    [Description("A Negative")]
    ANegative = 2,

    [Description("B Positive")]
    BPositive = 3,

    [Description("B Negative")]
    BNegative = 4,

    [Description("AB Positive")]
    ABPositive = 5,

    [Description("AB Negative")]
    ABNegative = 6,

    [Description("O Positive")]
    OPositive = 7,

    [Description("O Negative")]
    ONegative = 8,

    [Description("Unknown")]
    Unknown = 9
}

public enum HealthConditionSeverity
{
    [Description("Mild")]
    Mild = 1,

    [Description("Moderate")]
    Moderate = 2,

    [Description("Severe")]
    Severe = 3,

    [Description("Critical")]
    Critical = 4
}

public enum HealthConditionStatus
{
    [Description("Active")]
    Active = 1,

    [Description("Managed")]
    Managed = 2,

    [Description("Resolved")]
    Resolved = 3,

    [Description("In Remission")]
    InRemission = 4
}

public enum AllergyType
{
    [Description("Drug")]
    Drug = 1,

    [Description("Food")]
    Food = 2,

    [Description("Environmental")]
    Environmental = 3,

    [Description("Latex")]
    Latex = 4,

    [Description("Insect")]
    Insect = 5,

    [Description("Other")]
    Other = 6
}

public enum AllergySeverity
{
    [Description("Mild")]
    Mild = 1,

    [Description("Moderate")]
    Moderate = 2,

    [Description("Severe")]
    Severe = 3,

    [Description("Anaphylactic")]
    Anaphylactic = 4
}

public enum MedicalExamResult
{
    [Description("Fit")]
    Fit = 1,

    [Description("Fit With Restrictions")]

    FitWithRestrictions = 2,

    [Description("Temporarily Unfit")]
    TemporarilyUnfit = 3,

    [Description("Unfit")]
    Unfit = 4,

    [Description("Requires Further Investigation")]
    RequiresFurtherInvestigation = 5
}

public enum MedicalExpenseType
{
    [Description("Consultation")]
    Consultation = 1,

    [Description("Medication")]
    Medication = 2,

    [Description("Laboratory Tests")]
    LaboratoryTests = 3,

    [Description("X-Ray/Imaging")]
    Imaging = 4,

    [Description("Surgery")]
    Surgery = 5,

    [Description("Hospitalization")]
    Hospitalization = 6,

    [Description("Dental Care")]
    DentalCare = 7,

    [Description("Optical Care")]
    OpticalCare = 8,

    [Description("Physiotherapy")]
    Physiotherapy = 9,

    [Description("Emergency Care")]
    EmergencyCare = 10,

    [Description("Maternity Care")]
    MaternityCare = 11,

    [Description("Mental Health")]
    MentalHealth = 12,

    [Description("Vaccination")]
    Vaccination = 13,

    [Description("Health Screening")]
    HealthScreening = 14,

    [Description("Medical Equipment")]
    MedicalEquipment = 15,

    [Description("Ambulance Service")]
    AmbulanceService = 16,

    [Description("Other")]
    Other = 99
}

public enum ClaimStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Submitted")]
    Submitted = 2,

    [Description("Supervisor Review")]
    SupervisorReview = 3,

    [Description("HR Review")]
    HrReview = 4,

    [Description("Finance Review")]
    FinanceReview = 5,

    [Description("Approved")]
    Approved = 6,

    [Description("Rejected")]
    Rejected = 7,

    [Description("Paid")]
    Paid = 8,

    [Description("Partially Approved")]
    PartiallyApproved = 9,

    [Description("Additional Info Required")]
    AdditionalInfoRequired = 10,

    [Description("Cancelled")]
    Cancelled = 11
}

public enum ClaimPreAuthorizationStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Requested")]
    Requested = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Pending Approval")]
    PendingApproval = 4,

    [Description("Rejected")]
    Rejected = 5,

    [Description("Expired")]
    Expired = 6,

    [Description("Cancelled")]
    Cancelled = 7
}

public enum MedicalReferralPriority
{
    Routine,
    Urgent,
    Emergency
}

public enum MedicalReferralStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Issued")]
    Issued = 2,

    [Description("Accepted")]
    Accepted = 3,

    [Description("Completed")]
    Completed = 4,

    [Description("Cancelled")]
    Cancelled = 5,

    [Description("Expired")]
    Expired = 6
}

/// <summary>
/// Where a medical board has got to (residue plan G4 / R-15b).
/// </summary>
/// <remarks>
/// ⚠ A board is <b>convened for one employee about one question</b>, so its lifecycle is a case's,
/// not a committee's. A standing panel that sits repeatedly on different people would need a
/// different model, and TDC has not described one.
/// </remarks>
public enum MedicalBoardStatus
{
    /// <summary>Somebody has asked for a board. Nobody has been appointed to it yet.</summary>
    [Description("Requested")]
    Requested = 1,

    /// <summary>Members appointed; it may now sit. Its recommendation is not yet given.</summary>
    [Description("Convened")]
    Convened = 2,

    /// <summary>It has reported. ⚠ The only status leave and separation will act on.</summary>
    [Description("Concluded")]
    Concluded = 3,

    [Description("Cancelled")]
    Cancelled = 4
}

/// <summary>What a person is doing on a medical board.</summary>
/// <remarks>
/// ⚠ A member may be a registered <c>Physician</c> or somebody named only here — a board commonly
/// includes a doctor from outside the organisation who is in nobody's register. Both are recorded,
/// and the entity requires one or the other rather than pretending every member is on file.
/// </remarks>
public enum MedicalBoardMemberRole
{
    [Description("Chair")]
    Chair = 1,

    [Description("Member")]
    Member = 2,

    [Description("Secretary")]
    Secretary = 3,

    [Description("Observer")]
    Observer = 4
}

public enum MedicalAppointmentStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Scheduled")]
    Scheduled = 2,

    [Description("Confirmed")]
    Confirmed = 3,

    [Description("CheckedIn")]
    CheckedIn = 4,

    [Description("InProgress")]
    InProgress = 5,

    [Description("Completed")]
    Completed = 6,

    [Description("NoShow")]
    NoShow = 7,

    [Description("Cancelled")]
    Cancelled = 8,

    [Description("Rescheduled")]
    Rescheduled = 9
}

public enum NHISClaimStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Submitted")]
    Submitted = 2,

    [Description("Under Review")]
    UnderReview = 3,

    [Description("Approved")]
    Approved = 4,

    [Description("Partially Approved")]
    PartiallyApproved = 5,

    [Description("Rejected")]
    Rejected = 6,

    [Description("Paid")]
    Paid = 7,

    [Description("Appealed")]
    Appealed = 8
}

public enum MedicalItemType
{
    [Description("Consultation Fee")]
    ConsultationFee = 1,

    [Description("Laboratory Test")]
    LaboratoryTest = 2,

    [Description("Imaging/Scan")]
    Imaging = 3,

    [Description("Medication/Drug")]
    Medication = 4,

    [Description("Medical Procedure")]
    Procedure = 5,

    [Description("Surgical Procedure")]
    Surgery = 6,

    [Description("Hospital Bed")]
    HospitalBed = 7,

    [Description("Medical Supply")]
    MedicalSupply = 8,

    [Description("Medical Equipment")]
    MedicalEquipment = 9,

    [Description("Therapy Session")]
    TherapySession = 10,

    [Description("Professional Fee")]
    ProfessionalFee = 11,

    [Description("Facility Fee")]
    FacilityFee = 12,

    [Description("Other")]
    Other = 99
}

public enum MedicalDocumentType
{
    [Description("Medical Receipt")]
    Receipt = 1,

    [Description("Invoice")]
    Invoice = 2,

    [Description("Prescription")]
    Prescription = 3,

    [Description("Medical Report")]
    MedicalReport = 4,

    [Description("Lab Results")]
    LabResults = 5,

    [Description("Imaging Results")]
    ImagingResults = 6,

    [Description("Discharge Summary")]
    DischargeSummary = 7,

    [Description("Referral Letter")]
    ReferralLetter = 8,

    [Description("Insurance Claim Form")]
    InsuranceClaimForm = 9,

    [Description("Other")]
    Other = 99
}

public enum PaymentMethod
{
    [Description("Bank Transfer")]
    BankTransfer = 1,

    [Description("Cash")]
    Cash = 2,

    [Description("Cheque")]
    Cheque = 3,

    [Description("Mobile Money")]
    MobileMoney = 4,

    [Description("Direct Deposit")]
    DirectDeposit = 5,

    [Description("Salary Deduction")]
    SalaryDeduction = 6
}

public enum HealthFacilityType
{
    [Description("General Hospital")]
    GeneralHospital = 1,

    [Description("Specialized Hospital")]
    SpecializedHospital = 2,

    [Description("Teaching Hospital")]
    TeachingHospital = 3,

    [Description("Clinic")]
    Clinic = 4,

    [Description("Polyclinic")]
    Polyclinic = 5,

    [Description("Medical Center")]
    MedicalCenter = 6,

    [Description("Pharmacy")]
    Pharmacy = 7,

    [Description("Diagnostic Center")]
    DiagnosticCenter = 8,

    [Description("Laboratory")]
    Laboratory = 9,

    [Description("Imaging Center")]
    ImagingCenter = 10,

    [Description("Urgent Care")]
    UrgentCare = 11,

    [Description("Day Surgery Center")]
    DaySurgeryCenter = 12,

    [Description("Rehabilitation Center")]
    RehabilitationCenter = 13,

    [Description("Maternity Home")]
    MaternityHome = 14,

    [Description("Dental Clinic")]
    DentalClinic = 15,

    [Description("Optical Center")]
    OpticalCenter = 16,

    [Description("Mental Health Facility")]
    MentalHealthFacility = 17,

    [Description("Other")]
    Other = 99
}

public enum MedicalSpecialty
{
    [Description("General Practice")]
    GeneralPractice = 1,

    [Description("Internal Medicine")]
    InternalMedicine = 2,

    [Description("Pediatrics")]
    Pediatrics = 3,

    [Description("Obstetrics & Gynecology")]
    ObstetricsGynecology = 4,

    [Description("Surgery")]
    Surgery = 5,

    [Description("Orthopedics")]
    Orthopedics = 6,

    [Description("Cardiology")]
    Cardiology = 7,

    [Description("Neurology")]
    Neurology = 8,

    [Description("Psychiatry")]
    Psychiatry = 9,

    [Description("Dermatology")]
    Dermatology = 10,

    [Description("Ophthalmology")]
    Ophthalmology = 11,

    [Description("ENT (Ear, Nose, Throat)")]
    ENT = 12,

    [Description("Radiology")]
    Radiology = 13,

    [Description("Anesthesiology")]
    Anesthesiology = 14,

    [Description("Emergency Medicine")]
    EmergencyMedicine = 15,

    [Description("Pathology")]
    Pathology = 16,

    [Description("Oncology")]
    Oncology = 17,

    [Description("Nephrology")]
    Nephrology = 18,

    [Description("Gastroenterology")]
    Gastroenterology = 19,

    [Description("Pulmonology")]
    Pulmonology = 20,

    [Description("Endocrinology")]
    Endocrinology = 21,

    [Description("Rheumatology")]
    Rheumatology = 22,

    [Description("Urology")]
    Urology = 23,

    [Description("Plastic Surgery")]
    PlasticSurgery = 24,

    [Description("Dentistry")]
    Dentistry = 25,

    [Description("Physiotherapy")]
    Physiotherapy = 26,

    [Description("Nutrition & Dietetics")]
    NutritionDietetics = 27,

    [Description("Clinical Psychology")]
    ClinicalPsychology = 28,

    [Description("Other")]
    Other = 99
}

public enum MedicalServiceType
{
    [Description("Consultation")]
    Consultation = 1,

    [Description("Emergency Care")]
    EmergencyCare = 2,

    [Description("Inpatient Care")]
    InpatientCare = 3,

    [Description("Outpatient Care")]
    OutpatientCare = 4,

    [Description("Surgery")]
    Surgery = 5,

    [Description("Laboratory Services")]
    LaboratoryServices = 6,

    [Description("Imaging/Radiology")]
    ImagingRadiology = 7,

    [Description("Pharmacy")]
    Pharmacy = 8,

    [Description("Physiotherapy")]
    Physiotherapy = 9,

    [Description("Dental Care")]
    DentalCare = 10,

    [Description("Optical Care")]
    OpticalCare = 11,

    [Description("Maternity Care")]
    MaternityCare = 12,

    [Description("Vaccination")]
    Vaccination = 13,

    [Description("Health Screening")]
    HealthScreening = 14,

    [Description("Mental Health")]
    MentalHealth = 15,

    [Description("Ambulance Service")]
    AmbulanceService = 16,

    [Description("Home Care")]
    HomeCare = 17,

    [Description("Telemedicine")]
    Telemedicine = 18,

    [Description("Rehabilitation")]
    Rehabilitation = 19,

    [Description("Dialysis")]
    Dialysis = 20,

    [Description("Psychiatric Services")]
    PsychiatricServices = 21,

    [Description("Pediatric Services")]
    PediatricServices = 22,

    [Description("Obstetrics & Gynecology Services")]
    ObstetricsGynecologyServices = 23,

    [Description("Cardiology Services")]
    CardiologyServices = 24,

    [Description("Neurology Services")]
    NeurologyServices = 25,

    [Description("Dermatology Services")]
    DermatologyServices = 26,

    [Description("Ophthalmology Services")]
    OphthalmologyServices = 27,

    [Description("ENT (Ear, Nose, Throat) Services")]
    ENT = 28,

    [Description("Pathology Services")]
    PathologyServices = 29,

    [Description("Oncology Services")]
    OncologyServices = 30,

    [Description("Nephrology Services")]
    NephrologyServices = 31,

    [Description("Gastroenterology Services")]
    GastroenterologyServices = 32,

    [Description("Pulmonology Services")]
    PulmonologyServices = 33,

    [Description("Other")]
    Other = 99
}

public enum FacilityDocumentType
{
    [Description("Operating License")]
    OperatingLicense = 1,

    [Description("Business Registration")]
    BusinessRegistration = 2,

    [Description("Accreditation Certificate")]
    AccreditationCertificate = 3,

    [Description("Insurance Certificate")]
    InsuranceCertificate = 4,

    [Description("Tax Clearance")]
    TaxClearance = 5,

    [Description("Fire Safety Certificate")]
    FireSafetyCertificate = 6,

    [Description("Health Department Approval")]
    HealthDepartmentApproval = 7,

    [Description("Memorandum of Understanding")]
    MOU = 8,

    [Description("Contract Agreement")]
    ContractAgreement = 9,

    [Description("Other")]
    Other = 99
}

public enum PhysicianDocumentType
{
    [Description("Medical License")]
    MedicalLicense = 1,

    [Description("Medical Degree")]
    MedicalDegree = 2,

    [Description("Postgraduate Certificate")]
    PostgraduateCertificate = 3,

    [Description("Board Certification")]
    BoardCertification = 4,

    [Description("Continuing Education Certificate")]
    ContinuingEducation = 5,

    [Description("Professional Membership")]
    ProfessionalMembership = 6,

    [Description("Malpractice Insurance")]
    MalpracticeInsurance = 7,

    [Description("Background Check")]
    BackgroundCheck = 8,

    [Description("CV/Resume")]
    CVResume = 9,

    [Description("ID/Passport")]
    IDPassport = 10,

    [Description("Contract Agreement")]
    ContractAgreement = 11,

    [Description("Reference Letter")]
    ReferenceLetter = 12,

    [Description("Other")]
    Other = 99
}

public enum MedicalInsuranceProviderType
{
    [Description("Health Insurance")]
    HealthInsurance = 1,

    [Description("Life Insurance")]
    LifeInsurance = 2,

    [Description("Health Maintenance Organization (HMO)")]
    HMO = 3,

    [Description("Preferred Provider Organization (PPO)")]
    PPO = 4,

    [Description("National Health Insurance")]
    NationalHealthInsurance = 5,

    [Description("Private Health Insurance")]
    PrivateHealthInsurance = 6,

    [Description("Group Insurance")]
    GroupInsurance = 7,

    [Description("Travel Insurance")]
    TravelInsurance = 8,

    [Description("Dental Insurance")]
    DentalInsurance = 9,

    [Description("Vision Insurance")]
    VisionInsurance = 10,

    [Description("Other")]
    Other = 99
}

public enum MedicalInsurancePlanType
{
    [Description("Basic Plan")]
    BasicPlan = 1,

    [Description("Standard Plan")]
    StandardPlan = 2,

    [Description("Premium Plan")]
    PremiumPlan = 3,

    [Description("Executive Plan")]
    ExecutivePlan = 4,

    [Description("Family Plan")]
    FamilyPlan = 5,

    [Description("Individual Plan")]
    IndividualPlan = 6,

    [Description("Corporate Plan")]
    CorporatePlan = 7,

    [Description("Student Plan")]
    StudentPlan = 8,

    [Description("Senior Plan")]
    SeniorPlan = 9,

    [Description("Maternity Plan")]
    MaternityPlan = 10,

    [Description("Catastrophic Plan")]
    CatastrophicPlan = 11,

    [Description("Custom Plan")]
    CustomPlan = 12,
}

public enum BenefitCategory
{
    [Description("Outpatient Care")]
    OutpatientCare = 1,

    [Description("Inpatient Care")]
    InpatientCare = 2,

    [Description("Emergency Care")]
    EmergencyCare = 3,

    [Description("Surgery")]
    Surgery = 4,

    [Description("Maternity Care")]
    MaternityCare = 5,

    [Description("Dental Care")]
    DentalCare = 6,

    [Description("Optical Care")]
    OpticalCare = 7,

    [Description("Prescription Drugs")]
    PrescriptionDrugs = 8,

    [Description("Laboratory Tests")]
    LaboratoryTests = 9,

    [Description("Imaging/Radiology")]
    Imaging = 10,

    [Description("Physiotherapy")]
    Physiotherapy = 11,

    [Description("Mental Health")]
    MentalHealth = 12,

    [Description("Preventive Care")]
    PreventiveCare = 13,

    [Description("Ambulance Service")]
    AmbulanceService = 14,

    [Description("Home Care")]
    HomeCare = 15,

    [Description("Chronic Disease Management")]
    ChronicDiseaseManagement = 16,

    [Description("Wellness Programs")]
    WellnessPrograms = 17
}

public enum PremiumPaymentResponsibility
{
    [Description("Employer Pays All")]
    EmployerPaysAll = 1,

    [Description("Employee Pays All")]
    EmployeePaysAll = 2,

    [Description("Shared - Employer/Employee")]
    Shared = 3,

    [Description("Employee Pays with Subsidy")]
    EmployeePaysWithSubsidy = 4
}

public enum MedicalInsurancePolicyStatus
{
    [Description("Active")]
    Active = 1,

    [Description("Pending Renewal")]
    PendingRenewal = 2,

    [Description("Suspended")]
    Suspended = 3,

    [Description("Cancelled")]
    Cancelled = 4,

    [Description("Expired")]
    Expired = 5,

    [Description("Lapsed")]
    Lapsed = 6,

    [Description("Under Review")]
    UnderReview = 7
}

public enum MedicalInsuranceClaimStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Submitted to Provider")]
    Submitted = 2,

    [Description("Under Review")]
    UnderReview = 3,

    [Description("Additional Information Required")]
    AdditionalInfoRequired = 4,

    [Description("Approved")]
    Approved = 5,

    [Description("Partially Approved")]
    PartiallyApproved = 6,

    [Description("Rejected")]
    Rejected = 7,

    [Description("Pending Payment")]
    PendingPayment = 8,

    [Description("Paid")]
    Paid = 9,

    [Description("Partially Paid")]
    PartiallyPaid = 10,

    [Description("Appealed")]
    Appealed = 11,

    [Description("Cancelled")]
    Cancelled = 12
}

public enum InsuranceClaimDocumentType
{
    [Description("Claim Form")]
    ClaimForm = 1,

    [Description("Medical Receipt")]
    MedicalReceipt = 2,

    [Description("Invoice")]
    Invoice = 3,

    [Description("Prescription")]
    Prescription = 4,

    [Description("Medical Report")]
    MedicalReport = 5,

    [Description("Lab Results")]
    LabResults = 6,

    [Description("Imaging Results")]
    ImagingResults = 7,

    [Description("Discharge Summary")]
    DischargeSummary = 8,

    [Description("Pre-Authorization Form")]
    PreAuthorizationForm = 9,

    [Description("Referral Letter")]
    ReferralLetter = 10,

    [Description("Proof of Payment")]
    ProofOfPayment = 11,

    [Description("Other")]
    Other = 99
}

public enum MedicalInsuranceProviderDocumentType
{
    [Description("Operating License")]
    OperatingLicense = 1,

    [Description("Business Registration")]
    BusinessRegistration = 2,

    [Description("Insurance Authority Certificate")]
    InsuranceAuthorityCertificate = 3,

    [Description("Tax Clearance")]
    TaxClearance = 4,

    [Description("Financial Statements")]
    FinancialStatements = 5,

    [Description("Solvency Certificate")]
    SolvencyCertificate = 6,

    [Description("Contract Agreement")]
    ContractAgreement = 7,

    [Description("Service Level Agreement")]
    ServiceLevelAgreement = 8,

    [Description("Rate Card")]
    RateCard = 9,

    [Description("Network List")]
    NetworkList = 10,

    [Description("Policy Document")]
    PolicyDocument = 11,

    [Description("Accreditation")]
    Accreditation = 12,

    [Description("Regulatory Filing")]
    RegulatoryFiling = 13,

    [Description("Claim Form")]
    ClaimForm = 14,

    [Description("Other")]
    Other = 99
}

public enum MedicalInsurancePremiumPaymentStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Paid")]
    Paid = 2,

    [Description("Overdue")]
    Overdue = 3,

    [Description("Waived")]
    Waived = 4,

    [Description("Refunded")]
    Refunded = 5
}

public enum MedicalExpenseClaimNoteType
{
    [Description("General")]
    General = 1,

    [Description("Internal HR")]
    InternalHR = 2,

    [Description("Finance Note")]
    FinanceNote = 3,

    [Description("Insurance Correspondence")]
    InsuranceCorrespondence = 4,

    [Description("Employee Comment")]
    EmployeeComment = 5
}

public enum MedicalExpenseApprovalStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Approved")]
    Approved = 2,

    [Description("Rejected")]
    Rejected = 3,

    [Description("Escalated")]
    Escalated = 4
}

#endregion Health and Medical Expenses

#region Job Analysis

public enum JobLevel
{
    [Description("Entry Level")]
    EntryLevel = 1,

    [Description("Junior")]
    Junior = 2,

    [Description("Intermediate")]
    Intermediate = 3,

    [Description("Senior")]
    Senior = 4,

    [Description("Lead")]
    Lead = 5,

    [Description("Principal")]
    Principal = 6,

    [Description("Manager")]
    Manager = 7,

    [Description("Senior Manager")]
    SeniorManager = 8,

    [Description("Director")]
    Director = 9,

    [Description("Executive")]
    Executive = 10
}

public enum JobGrade
{
    [Description("Grade 1")]
    Grade1 = 1,

    [Description("Grade 2")]
    Grade2 = 2,

    [Description("Grade 3")]
    Grade3 = 3,

    [Description("Grade 4")]
    Grade4 = 4,

    [Description("Grade 5")]
    Grade5 = 5,

    [Description("Grade 6")]
    Grade6 = 6,

    [Description("Grade 7")]
    Grade7 = 7,

    [Description("Grade 8")]
    Grade8 = 8,

    [Description("Grade 9")]
    Grade9 = 9,

    [Description("Grade 10")]
    Grade10 = 10
}

public enum FLSAClassification
{
    [Description("Exempt")]
    Exempt = 1,

    [Description("Non-Exempt")]
    NonExempt = 2
}

public enum JobDescriptionStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Pending Review")]
    PendingReview = 2,

    [Description("Under Revision")]
    UnderRevision = 3,

    [Description("Approved")]
    Approved = 4,

    [Description("Active")]
    Active = 5,

    [Description("Superseded")]
    Superseded = 6,

    [Description("Archived")]
    Archived = 7
}

public enum ResponsibilityType
{
    [Description("Core Responsibility")]
    Core = 1,

    [Description("Secondary Responsibility")]
    Secondary = 2,

    [Description("Occasional Responsibility")]
    Occasional = 3
}

public enum QualificationType
{
    [Description("Education")]
    Education = 1,

    [Description("Work Experience")]
    Experience = 2,

    [Description("Certification")]
    Certification = 3,

    [Description("License")]
    License = 4,

    [Description("Technical Skills")]
    TechnicalSkills = 5,

    [Description("Language")]
    Language = 6,

    [Description("Membership")]
    Membership = 7,

    [Description("Other")]
    Other = 8,
}

public enum CompetencyType
{
    [Description("Technical")]
    Technical = 1,

    [Description("Behavioral")]
    Behavioral = 2,

    [Description("Leadership")]
    Leadership = 3,

    [Description("Core Competency")]
    Core = 4
}

public enum ProficiencyLevel
{
    [Description("Basic/Awareness")]
    Basic = 1,

    [Description("Working Knowledge")]
    WorkingKnowledge = 2,

    [Description("Proficient")]
    Proficient = 3,

    [Description("Advanced")]
    Advanced = 4,

    [Description("Expert")]
    Expert = 5
}

/// <summary>
/// The reach of a working relationship on a job description — who the post deals with, and how far
/// outside the organisation that reaches.
/// </summary>
/// <remarks>
/// <para>⚠ <b>Renamed from <c>RelationshipType</c> in round 2 lane D2.</b> It collided with the new
/// <c>Entities.HR.RelationshipType</c> table (how one PERSON is tied to another — a next of kin, a
/// referee, a guarantor), and the two are unrelated ideas that would have forced a <c>using</c>
/// alias into every file touching either. Renaming was free: this enum had <b>no typed consumer
/// anywhere</b> — no property, no parameter, no column, no frontend reference — so nothing was
/// serialized against the old name and no data carries it. Verified 2026-09-10.</para>
///
/// <para>The name it has now is what its members actually describe. Job descriptions record their
/// working relationships through <see cref="ReportingRelationshipType"/> instead, which is why
/// this one was never wired up.</para>
/// </remarks>
public enum WorkingRelationshipScope
{
    [Description("Internal - Same Department")]
    InternalSameDepartment = 1,

    [Description("Internal - Other Departments")]
    InternalOtherDepartments = 2,

    [Description("External - Clients")]
    ExternalClients = 3,

    [Description("External - Suppliers")]
    ExternalSuppliers = 4,

    [Description("External - Regulators")]
    ExternalRegulators = 5,

    [Description("Stakeholders")]
    Stakeholders = 6
}

public enum InteractionFrequency
{
    [Description("Daily")]
    Daily = 1,

    [Description("Weekly")]
    Weekly = 2,

    [Description("Monthly")]
    Monthly = 3,

    [Description("Quarterly")]
    Quarterly = 4,

    [Description("Annually")]
    Annually = 5,

    [Description("As Needed")]
    AsNeeded = 6
}

public enum JobEvaluationMethod
{
    [Description("Point Factor Method")]
    PointFactor = 1,

    [Description("Ranking Method")]
    Ranking = 2,

    [Description("Classification Method")]
    Classification = 3,

    [Description("Factor Comparison Method")]
    FactorComparison = 4,

    [Description("Hay Method")]
    HayMethod = 5
}

public enum JobEvaluationStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Approved")]
    Approved = 4,

    [Description("Rejected")]
    Rejected = 5
}

public enum ManpowerBudgetStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Submitted")]
    Submitted = 2,

    [Description("Under Review")]
    UnderReview = 3,

    [Description("Approved")]
    Approved = 4,

    [Description("Rejected")]
    Rejected = 5,

    [Description("Active")]
    Active = 6,

    [Description("Closed")]
    Closed = 7
}

/// <summary>
/// Where a manpower budget line's planned average salary came from (round 2b, R3, decision D-1).
/// </summary>
/// <remarks>
/// ⚠ <c>Manual</c> is deliberately the highest member and the entity default, and the migration's
/// column default is 4, not the scaffolded 0 — every line that exists when R3 lands was typed by
/// hand. A zero here would be a value that is not a member (the lane E1/G trap, fourth time).
/// </remarks>
public enum PlannedSalarySource
{
    [Description("Notch on the scale")]
    Notch = 1,

    [Description("Level mid-point")]
    LevelMidpoint = 2,

    [Description("Grade minimum")]
    GradeMinimum = 3,

    [Description("Entered by hand")]
    Manual = 4
}

public enum BudgetPriority
{
    [Description("Critical")]
    Critical = 1,

    [Description("High")]
    High = 2,

    [Description("Medium")]
    Medium = 3,

    [Description("Low")]
    Low = 4
}

/// <summary>Legal form / incorporation type of the employing company (on <c>CompanyProfile</c>).</summary>
public enum CompanyLegalForm
{
    [Description("Limited Liability Company")]
    LimitedCompany = 1,

    [Description("Public Limited Company")]
    PublicLimitedCompany = 2,

    [Description("Partnership")]
    Partnership = 3,

    [Description("Sole Proprietorship")]
    SoleProprietorship = 4,

    [Description("Non-Governmental Organisation")]
    Ngo = 5,

    [Description("Statutory / State Entity")]
    StatutoryBody = 6,

    [Description("Other")]
    Other = 99
}

/// <summary>
/// How strictly a staff requisition is checked against the position's approved manpower
/// budget line (<c>ManpowerBudgetLine</c>) for the fiscal year. Configured once per tenant on
/// <c>CompanyHrPolicySettings</c>. Enforcement only ever applies when a budget line actually
/// exists for the requisition's position — an unbudgeted position is never blocked.
/// </summary>
/// <summary>
/// How many tiers the tenant's salary scale has. Decides what screens show and what a placement
/// needs; it does not change the schema.
/// </summary>
/// <remarks>
/// <para>HR's tables are three-tier (grade → level → notch) with a notch required to hang off a
/// level. A two-tier scale is represented losslessly as a grade with exactly ONE level, and that is
/// what the payroll projection has synthesised since lane 3a — so two-tier is the schema's
/// degenerate case, not a second schema. This setting says which case the tenant is in, so the
/// level step can be hidden where it is a phantom and required where it is real.</para>
/// <para>Lane G (salary structure tiers and source), 2026-09-09.</para>
/// </remarks>
public enum SalaryStructureTiers
{
    /// <summary>Grade → notch. Each grade has one implicit level, never shown, resolved server-side.</summary>
    [Description("Two-tier: grade and notch")]
    GradeAndNotch = 2,

    /// <summary>Grade → level → notch. The level is a real band within the grade and is chosen.</summary>
    [Description("Three-tier: grade, level and notch")]
    GradeLevelAndNotch = 3,
}

/// <summary>
/// Who maintains the tenant's salary scale — the payroll module, or HR itself.
/// </summary>
/// <remarks>
/// <para><b>Payroll</b> is the standing decision of 2026-08-02: payroll's grades and notches are the
/// source of truth and HR mirrors them by projection; HR's own structure writes answer 409. Payroll
/// is two-tier and has no level concept, so a tenant on this source cannot be three-tier — the
/// switch is refused with a sentence, and a level tier in payroll is recorded as an ask to the
/// payroll owner (round-2 plan § 7.1).</para>
/// <para><b>Hr</b> is the option decided 2026-09-09 for tenants that do not run this payroll
/// module: the projection stops, HR's dormant grade/level/notch CRUD opens, and the scale may be
/// three-tier. Nothing payroll holds is touched by the switch; nothing HR authors is pushed to it.</para>
/// </remarks>
public enum SalaryStructureSource
{
    /// <summary>Defined in Payroll (Administration → HR → Payroll → Grades Setup); HR is a read-only mirror.</summary>
    [Description("Payroll")]
    Payroll = 1,

    /// <summary>Defined in HR (Administration → HR → Pay &amp; Benefits → Salary Structure); the projection is off.</summary>
    [Description("HR")]
    Hr = 2,
}

public enum BudgetEnforcementMode
{
    /// <summary>No budget checking — requisitions proceed regardless of the manpower budget.</summary>
    [Description("Off")]
    Off = 1,

    /// <summary>Advisory: an over-budget requisition surfaces a warning but is still allowed through.</summary>
    [Description("Warn")]
    Warn = 2,

    /// <summary>Hard stop: submitting/approving an over-budget requisition is rejected.</summary>
    [Description("Block")]
    Block = 3
}

public enum PhysicalDemandType
{
    Sitting = 1,                  // Remaining in a seated position

    Standing = 2,                 // Remaining on one's feet in an upright position without moving

    Walking = 3,                  // Moving about on foot

    Running = 4,                  // Rapid locomotion on foot

    Climbing = 5,                 // Ascending or descending ladders, stairs, ramps, poles, etc.

    Balancing = 6,                // Maintaining body equilibrium to prevent falling

    Stooping = 7,                 // Bending body downward and forward (waist/spine)

    Kneeling = 8,                 // Bending legs at knee to come to rest on knee(s)

    Crouching = 9,                // Bending body downward and forward (legs and spine)

    Crawling = 10,                 // Moving about on hands and knees or hands and feet

    Reaching = 11,                 // Extending hand(s) and arm(s) in any direction

    Handling = 12,                 // Seizing, holding, grasping, turning, or working with hand(s)

    Fingering = 13,                // Picking, pinching, or otherwise working primarily with fingers

    Feeling = 14,                  // Perceiving attributes of objects by touch (texture, temperature, etc.)

    Talking = 15,                  // Expressing or exchanging ideas by means of spoken word

    Hearing = 16,                  // Perceiving the nature of sounds with or without correction

    SeeingNear = 17,               // Close visual acuity (e.g., computer work, reading)

    SeeingFar = 18,                // Visual acuity at distance

    SeeingPeripheral = 19,         // Side vision

    SeeingColor = 20,              // Distinguishing colors

    SeeingDepth = 21,              // Judging distances and spatial relationships

    TastingSmelling = 22,          // Using taste or smell senses

    LiftingCarrying = 23,          // Raising/lowering or moving objects (often with weight specified separately)

    PushingPulling = 24,           // Exerting force to move objects away/toward

    KeyboardingTyping = 25,        // Repetitive motions of hands, wrists, fingers for data entry

    RepetitiveMotion = 26,         // Substantial movements of wrists, hands, fingers (e.g., assembly line)

    Other = 27                     // Catch-all for rare/unlisted demands
}

public enum PhysicalDemandFrequency
{
    Never = 1,          // 0% of the time

    Rarely = 2,         // Up to 5% (or 1-5% of workday)

    Occasionally = 3,   // 6–33% (up to 1/3 of workday) – most common standard threshold

    Frequently = 4,     // 34–66% (1/3 to 2/3 of workday)

    Continuously = 5,   // 67–100% (2/3 or more of workday) – sometimes called "Constantly"
}

public enum WorkEnvironmentType
{
    Office = 1,

    Outdoor = 2,

    Industrial = 3,

    Laboratory = 4,

    Remote = 5,

    Hybrid = 6,

    FieldBased = 7,

    Other = 8
}

public enum ExposureLevel
{
    None = 1,        // No exposure
    Rare = 2,        // Very infrequent (e.g., <1% of time)
    Occasional = 3,  // 6–33% (up to 1/3 of workday)
    Frequent = 4,    // 34–66% (1/3 to 2/3 of workday)
    Constant = 5     // 67–100% (2/3 or more of workday)
}

public enum EquipmentType
{
    SoftwareApplication = 1,     // e.g., Microsoft Office, ERP system, specialized software
    ComputerHardware = 2,        // Desktop, laptop, tablet, server
    MobileDevice = 3,            // Smartphone, handheld scanner
    OfficeEquipment = 4,         // Printer, scanner, phone system
    HandTool = 5,                // Hammer, screwdriver, scalpel
    PowerTool = 6,               // Drill, saw, grinder
    HeavyMachinery = 7,          // Forklift, crane, excavator
    Vehicle = 8,                 // Car, truck, van, motorcycle
    SpecializedInstrument = 9,   // Lab equipment, medical device, surveying tool
    SafetyEquipment = 10,         // PPE (harness, respirator) – if used as tool
    Other = 11
}

/// <summary>
/// Working relationships a job interacts with. Supervisory reporting lines
/// (the position a holder reports to / supervises) are defined in the position
/// details, NOT here — so this intentionally omits "ReportsTo"/"Supervises".
/// </summary>
public enum ReportingRelationshipType
{
    CollaboratesWith = 1,
    InternalCustomers = 2,
    ExternalCustomers = 3,
    Vendors = 4,
    RegulatoryBodies = 5,
    MatrixReport = 6
}

/// <summary>
/// Category for a job's medical / mental / health / special requirement
/// (e.g. "not suitable if asthmatic" for a dusty environment).
/// </summary>
public enum MedicalRequirementCategory
{
    Medical = 1,      // e.g. no respiratory conditions, colour-vision normal
    Mental = 2,       // e.g. ability to cope with high-pressure situations
    Health = 3,       // e.g. general fitness, immunisation status
    Sensory = 4,      // e.g. hearing/eyesight acuity
    Other = 5
}

/// <summary>
/// Intrinsic business value/criticality of a role to the organization,
/// independent of the qualifications/competencies of the holder.
/// </summary>
public enum RoleCriticalityLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    MissionCritical = 4
}

/// <summary>
/// Level of decision-making authority/autonomy a role carries.
/// </summary>
public enum DecisionAuthorityLevel
{
    Operational = 1,   // day-to-day execution within set procedures
    Tactical = 2,      // shapes how objectives are met within a function
    Strategic = 3      // sets direction / makes decisions with org-wide impact
}

#endregion Job Analysis

#region Succession Planning

public enum CompetencyCategory
{
    Leadership = 1,

    Technical = 2,

    Behavioral = 3,

    Functional = 4,

    Core = 5,

    Other = 6
}

public enum GapStatus
{
    Gap = 1,

    Met = 2,

    Exceeded = 3
}

/// <summary>
/// A reviewer's disposition when giving feedback on a succession candidate during selection.
/// </summary>
public enum FeedbackDisposition
{
    Support = 1,
    Neutral = 2,
    Oppose  = 3
}

public enum SuccessionPlanStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Under Review")]
    UnderReview = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Rejected")]
    Rejected = 4,

    [Description("Completed")]
    Completed = 5,

    [Description("Archived")]
    Archived = 6
}

public enum PositionCriticality
{
    [Description("Low")]
    Low = 1,

    [Description("Medium")]
    Medium = 2,

    [Description("High")]
    High = 3,

    [Description("Critical")]
    Critical = 4
}

public enum SuccessionRisk
{
    [Description("High Risk - No Successor Ready")]
    HighRisk = 1,

    [Description("Medium Risk - Successor Needs Development")]
    MediumRisk = 2,

    [Description("Low Risk - Ready Successor Available")]
    LowRisk = 3,

    [Description("No Risk - Multiple Ready Successors")]
    NoRisk = 4
}

public enum VacancyReason
{
    [Description("Retirement")]
    Retirement = 1,

    [Description("Resignation")]
    Resignation = 2,

    [Description("Promotion")]
    Promotion = 3,

    [Description("Transfer")]
    Transfer = 4,

    [Description("Termination")]
    Termination = 5,

    [Description("Restructure")]
    Restructure = 6,

    [Description("Death")]
    Death = 7,

    [Description("Other")]
    Other = 8
}

/// <summary>
/// Lifecycle of a <c>PositionVacancy</c> — an empty seat in the org that HR tracks from the moment it
/// opens until it is filled or closed. Distinct from <c>JobVacancyStatus</c>, which tracks an approved
/// recruitment opening being advertised.
/// </summary>
public enum PositionVacancyStatus
{
    /// <summary>Known future vacancy (e.g. an upcoming retirement or contract expiry) opened ahead of the departure.</summary>
    [Description("Anticipated")]
    Anticipated = 1,

    /// <summary>The seat is empty now and awaiting HR review / decision.</summary>
    [Description("Open")]
    Open = 2,

    /// <summary>HR is discussing the vacancy with the department head / stakeholders.</summary>
    [Description("Under Review")]
    UnderReview = 3,

    /// <summary>A staff requisition has been raised to fill the vacancy.</summary>
    [Description("Requisition Raised")]
    RequisitionRaised = 4,

    /// <summary>The seat has been filled (a hire started, or an employee moved into the position).</summary>
    [Description("Filled")]
    Filled = 5,

    /// <summary>Closed without filling (e.g. the position was eliminated, or the vacancy was a false positive).</summary>
    [Description("Closed")]
    Closed = 6,

    /// <summary>Deliberately left unfilled for now (hiring freeze / on hold).</summary>
    [Description("Frozen")]
    Frozen = 7
}

/// <summary>
/// Whether a logged <c>PositionVacancy</c> reflects a genuine establishment shortfall. A departure is
/// ALWAYS logged (never silently swallowed); this classifies whether it is fillable.
/// </summary>
public enum VacancyClassification
{
    /// <summary>Active headcount is below the position's ExpectedHeadcount — a genuine, fillable shortfall.</summary>
    [Description("Within Establishment")]
    WithinEstablishment = 1,

    /// <summary>The position is still at its ExpectedHeadcount after the departure — flagged for HR review, not auto-fillable.</summary>
    [Description("No Shortfall")]
    NoShortfall = 2,

    /// <summary>The position is over its ExpectedHeadcount — an over-establishment situation for HR to review.</summary>
    [Description("Over Establishment")]
    OverEstablishment = 3
}

public enum CandidateType
{
    [Description("Internal")]
    Internal = 1,

    [Description("External")]
    External = 2,

    [Description("Emergency/Acting")]
    Emergency = 3
}

public enum ReadinessLevel
{
    [Description("Ready Now")]
    ReadyNow = 1,

    [Description("Ready in 12 Months")]
    ReadyIn12Months = 2,

    [Description("Ready in 24 Months")]
    ReadyIn24Months = 3,

    [Description("Ready in 36+ Months")]
    ReadyIn36PlusMonths = 4,

    [Description("Not Ready")]
    NotReady = 5
}

public enum PotentialRating
{
    [Description("Low Potential")]
    LowPotential = 1,

    [Description("Medium Potential")]
    MediumPotential = 2,

    [Description("High Potential")]
    HighPotential = 3
}

public enum RetentionRisk
{
    [Description("High Flight Risk")]
    HighRisk = 1,

    [Description("Medium Risk")]
    MediumRisk = 2,

    [Description("Low Risk")]
    LowRisk = 3,

    [Description("Secure")]
    Secure = 4
}

public enum DevelopmentActivityType
{
    [Description("Training/Course")]
    Training = 1,

    [Description("Mentoring/Coaching")]
    Mentoring = 2,

    [Description("Job Rotation")]
    JobRotation = 3,

    [Description("Special Project Assignment")]
    ProjectAssignment = 4,

    [Description("Shadowing")]
    Shadowing = 5,

    [Description("Acting Role")]
    ActingRole = 6,

    [Description("External Experience")]
    ExternalExperience = 7,

    [Description("Certification")]
    Certification = 8,

    [Description("Other")]
    Other = 9
}

public enum DevelopmentActivityStatus
{
    [Description("Planned")]
    Planned = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Deferred")]
    Deferred = 4,

    [Description("Cancelled")]
    Cancelled = 5
}

public enum ActionType
{
    [Description("Development Action")]
    Development = 1,

    [Description("Recruitment Action")]
    Recruitment = 2,

    [Description("Retention Action")]
    Retention = 3,

    [Description("Assessment Action")]
    Assessment = 4,

    [Description("Other Action")]
    Other = 5
}

public enum ActionPriority
{
    [Description("Critical")]
    Critical = 1,

    [Description("High")]
    High = 2,

    [Description("Medium")]
    Medium = 3,

    [Description("Low")]
    Low = 4
}

public enum ActionStatus
{
    [Description("Not Started")]
    NotStarted = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Overdue")]
    Overdue = 4,

    [Description("Cancelled")]
    Cancelled = 5
}

public enum TalentPoolType
{
    [Description("High Potential Employees")]
    HighPotential = 1,

    [Description("Leadership Pipeline")]
    LeadershipPipeline = 2,

    [Description("Technical Experts")]
    TechnicalExperts = 3,

    [Description("Specialized Skills")]
    Specialist = 4,

    [Description("Emergency Pool")]
    EmergencyPool = 5,

    [Description("Other")]
    Other = 6
}

#endregion Succession Planning

#region Staff Requisition

public enum StaffRequisitionType
{
    [Description("New Position")]
    NewPosition = 1,

    [Description("Replacement")]
    Replacement = 2,

    [Description("Contract")]
    Contract = 3,

    [Description("Internship")]
    Internship = 4,

    [Description("Backfill")]
    Backfill = 5,

    [Description("Other")]
    Other = 6
}

public enum StaffRequisitionPriority
{
    [Description("Urgent")]
    Urgent = 1,

    [Description("High")]
    High = 2,

    [Description("Medium")]
    Medium = 3,

    [Description("Low")]
    Low = 4
}

public enum StaffRequisitionStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Submitted")]
    Submitted = 2,

    [Description("Under Review")]
    UnderReview = 3,

    [Description("Approved")]
    Approved = 4,

    [Description("Rejected")]
    Rejected = 5,

    [Description("On Hold")]
    OnHold = 6,

    [Description("Cancelled")]
    Cancelled = 7,

    [Description("Partially Fulfilled")]
    PartiallyFulfilled = 8,

    [Description("Fulfilled")]
    Fulfilled = 9,
}

public enum StaffReplacementReason
{
    [Description("Resignation")]
    Resignation = 1,

    [Description("Retirement")]
    Retirement = 2,

    [Description("Termination")]
    Termination = 3,

    [Description("Promotion")]
    Promotion = 4,

    [Description("Transfer")]
    Transfer = 5,

    [Description("Long-term Leave")]
    LongTermLeave = 6,

    [Description("Death")]
    Death = 7,

    [Description("Other")]
    Other = 8
}

/// <summary>
/// HR's own approval of a recruitment cost (round 2b, R7). ⚠ This is a fact about HR's approval,
/// NOT a payment status: nothing here says paid or unpaid. Whether Finance has paid it is Finance's
/// to say (the R8 AP hand-off, when agreed) and is never recorded HR-side.
/// </summary>
public enum StaffRequisitionCostStatus
{
    [Description("Recorded")]
    Recorded = 1,

    [Description("Approved")]
    Approved = 2,

    [Description("Rejected")]
    Rejected = 3
}

public enum StaffRequisitionCostCategory
{
    [Description("Recruitment Agency Fee")]
    RecruitmentAgencyFee = 1,

    [Description("Job Advertising")]
    JobAdvertising = 2,

    [Description("Background Check")]
    BackgroundCheck = 3,

    [Description("Assessment")]
    Assessment = 4,

    [Description("Relocation")]
    Relocation = 5,

    [Description("Onboarding Materials")]
    OnboardingMaterials = 6,

    [Description("Training and Induction")]
    TrainingAndInduction = 7,

    [Description("Medical Examination")]
    MedicalExamination = 8,

    [Description("Travel and Interview")]
    TravelAndInterview = 9,

    [Description("Other")]
    Other = 10
}

#endregion Staff Requisition

#region Staff Assets

public enum AssetCategory
{
    [Description("IT Equipment")]
    ITEquipment = 1,

    [Description("Office Furniture")]
    OfficeFurniture = 2,

    [Description("Vehicle")]
    Vehicle = 3,

    [Description("Tools & Equipment")]
    ToolsEquipment = 4,

    [Description("Mobile Devices")]
    MobileDevices = 5,

    [Description("Safety Equipment")]
    SafetyEquipment = 6,

    [Description("Office Supplies")]
    OfficeSupplies = 7,

    [Description("Other")]
    Other = 8
}

public enum CompanyAssetType
{
    [Description("Laptop")]
    Laptop = 1,

    [Description("Desktop Computer")]
    Desktop = 2,

    [Description("Monitor")]
    Monitor = 3,

    [Description("Mobile Phone")]
    MobilePhone = 4,

    [Description("Tablet")]
    Tablet = 5,

    [Description("Printer")]
    Printer = 6,

    [Description("Desk")]
    Desk = 7,

    [Description("Chair")]
    Chair = 8,

    [Description("Vehicle")]
    Vehicle = 9,

    [Description("Projector")]
    Projector = 10,

    [Description("Other")]
    Other = 11
}

public enum CompanyAssetStatus
{
    [Description("Available")]
    Available = 1,

    [Description("Assigned")]
    Assigned = 2,

    [Description("In Maintenance")]
    InMaintenance = 3,

    [Description("Damaged")]
    Damaged = 4,

    [Description("Lost/Stolen")]
    LostStolen = 5,

    [Description("Disposed")]
    Disposed = 6,

    [Description("Reserved")]
    Reserved = 7
}

public enum DisposalMethod
{
    [Description("Sold")]
    Sold = 1,

    [Description("Donated")]
    Donated = 2,

    [Description("Recycled")]
    Recycled = 3,

    [Description("Discarded")]
    Discarded = 4,

    [Description("Transferred")]
    Transferred = 5
}

public enum AssignmentType
{
    [Description("Permanent Assignment")]
    Permanent = 1,

    [Description("Temporary Assignment")]
    Temporary = 2,

    [Description("Project-Based")]
    ProjectBased = 3,

    [Description("Short-Term Loan")]
    ShortTermLoan = 4
}

public enum AssignmentPurpose
{
    [Description("Regular Work")]
    RegularWork = 1,

    [Description("Special Project")]
    SpecialProject = 2,

    [Description("Travel")]
    Travel = 3,

    [Description("Training")]
    Training = 4,

    [Description("Replacement")]
    Replacement = 5
}

public enum AssignmentStatus
{
    [Description("Active")]
    Active = 1,

    [Description("Returned")]
    Returned = 2,

    /// <summary>
    /// ⚠ <b>Nothing writes this, and nothing should.</b> Whether a custody is late is derived from
    /// <c>ExpectedReturnDate</c> against today, and a derived fact stored in a status column is
    /// stale the moment the day turns.
    /// </summary>
    /// <remarks>
    /// <para>Measured in area 16 slice 11: the only references anywhere are reads that TOLERATE it.
    /// It came from the port with no writer — the same shape as <see cref="Lost"/> and
    /// <see cref="Damaged"/>, which slice 7 did give one because those record something that
    /// happened rather than something a calendar implies.</para>
    ///
    /// <para><b>Writing it would break the feature that reports it.</b> Before slice 11,
    /// <c>GetOverdueAssignmentsAsync</c> asked <c>Status == Active</c>, so a sweep that helpfully
    /// flipped late custodies to <c>Overdue</c> would have emptied the overdue list itself — and
    /// <c>ReturnAsync</c> and <c>ReportIncidentAsync</c> both refuse anything that is not
    /// <c>Active</c>, so a late asset could then never be handed back at all. The return
    /// watchlists now accept both statuses so that trap is defused, but the rule stands: this
    /// member is tolerated on read and never set.</para>
    /// </remarks>
    [Description("Overdue")]
    Overdue = 3,

    [Description("Lost")]
    Lost = 4,

    [Description("Damaged")]
    Damaged = 5,

    /// <summary>
    /// The holder passed the asset to another employee through an approved asset transfer, rather
    /// than handing it back. RHEMA addition, area 16 slice 4 — see the note at the top of
    /// <c>HREnums.Rhema.cs</c>.
    /// </summary>
    /// <remarks>
    /// Closing an employee-to-employee transfer as <c>Returned</c> would have been a lie in the one
    /// place somebody goes to find out what happened to an asset: nobody took it back, and the next
    /// custody starts the same day. Every query in the module asks whether an assignment is
    /// <c>Active</c>, so this behaves exactly as <c>Returned</c> does for "what does this employee
    /// still hold" — it only changes what the record says about why the custody ended.
    /// </remarks>
    [Description("Transferred")]
    Transferred = 6
}

public enum AssetMaintenanceType
{
    [Description("Preventive Maintenance")]
    Preventive = 1,

    [Description("Corrective Maintenance")]
    Corrective = 2,

    [Description("Inspection")]
    Inspection = 3,

    [Description("Repair")]
    Repair = 4,

    [Description("Upgrade")]
    Upgrade = 5
}

public enum MaintenanceStatus
{
    [Description("Scheduled")]
    Scheduled = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Cancelled")]
    Cancelled = 4
}

public enum AssetAttachmentType
{
    [Description("Photo")]
    Photo = 1,

    [Description("Purchase Invoice")]
    Invoice = 2,

    [Description("User Manual")]
    Manual = 3,

    [Description("Warranty Certificate")]
    WarrantyCertificate = 4,

    [Description("Maintenance Record")]
    MaintenanceRecord = 5
}

public enum AssetRequisitionPriority
{
    [Description("Urgent")]
    Urgent = 1,

    [Description("High")]
    High = 2,

    [Description("Medium")]
    Medium = 3,

    [Description("Low")]
    Low = 4
}

public enum AssetRequisitionStatus
{
    [Description("Draft")]
    Draft = 0,

    [Description("Submitted")]
    Submitted = 1,

    [Description("Under Review")]
    UnderReview = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Rejected")]
    Rejected = 4,

    [Description("Fulfilled")]
    Fulfilled = 5,

    [Description("Cancelled")]
    Cancelled = 6
}


public enum AssetAttributeDataType
{
    Checkbox = 1,

    Date = 2,

    Decimal = 3,

    Dropdown = 4,

    Integer = 5,

    Text = 6
}

#endregion Staff Assets

#region Career Movement

public enum StaffMovementType
{
    [Description("Promotion")]
    Promotion = 1,

    [Description("Transfer")]
    Transfer = 2,

    [Description("Demotion")]
    Demotion = 3,

    [Description("Lateral Move")]
    LateralMove = 4,

    [Description("Secondment")]
    Secondment = 5,

    [Description("Acting Appointment")]
    ActingAppointment = 6,

    [Description("Redesignation")]
    Redesignation = 7
}

public enum StaffMovementCategory
{
    [Description("Voluntary")]
    Voluntary = 1,

    [Description("Involuntary")]
    Involuntary = 2,

    [Description("Organizational Restructure")]
    OrganizationalRestructure = 3,

    [Description("Career Development")]
    CareerDevelopment = 4,

    [Description("Administrative")]
    Administrative = 5
}

public enum StaffMovementStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Submitted")]
    Submitted = 2,

    [Description("Current Supervisor Approval")]
    CurrentSupervisorApproval = 3,

    [Description("New Supervisor Approval")]
    NewSupervisorApproval = 4,

    [Description("Current HOD Approval")]
    CurrentHodApproval = 5,

    [Description("New HOD Approval")]
    NewHodApproval = 6,

    [Description("HR Review")]
    HrReview = 7,

    [Description("Management Approval")]
    ManagementApproval = 8,

    [Description("Employee Acceptance Pending")]
    EmployeeAcceptancePending = 9,

    [Description("Approved")]
    Approved = 10,

    [Description("Rejected")]
    Rejected = 11,

    [Description("Implemented")]
    Implemented = 12,

    [Description("Cancelled")]
    Cancelled = 13
}

public enum StaffMovementAttachmentType
{
    [Description("Approval Letter")]
    ApprovalLetter = 1,

    [Description("Performance Review")]
    PerformanceReview = 2,

    [Description("Justification Document")]
    Justification = 3,

    [Description("Job Description")]
    JobDescription = 4,

    [Description("Other")]
    Other = 5
}

public enum StaffMovementChecklistCategory
{
    [Description("HR Tasks")]
    HRTasks = 1,

    [Description("IT Tasks")]
    ITTasks = 2,

    [Description("Finance Tasks")]
    FinanceTasks = 3,

    [Description("Department Handover")]
    DepartmentHandover = 4,

    [Description("Access & Security")]
    AccessSecurity = 5
}

public enum StaffPromotionType
{
    [Description("Merit-Based")]
    MeritBased = 1,

    [Description("Seniority-Based")]
    SeniorityBased = 2,

    [Description("Competitive")]
    Competitive = 3,

    [Description("Acting Promotion")]
    Acting = 4,

    [Description("Automatic")]
    Automatic = 5
}

public enum StaffTransferType
{
    [Description("Interdepartmental Transfer")]
    Interdepartmental = 1,

    [Description("Inter-Station Transfer")]
    InterStation = 2,

    [Description("Cross-Functional Transfer")]
    CrossFunctional = 3,

    [Description("Temporary Transfer")]
    Temporary = 4
}

public enum StaffTransferReasonCategory
{
    [Description("Career Development")]
    CareerDevelopment = 1,

    [Description("Operational Needs")]
    OperationalNeeds = 2,

    [Description("Employee Request")]
    EmployeeRequest = 3,

    [Description("Skills Gap Filling")]
    SkillsGapFilling = 4,

    [Description("Organizational Restructure")]
    OrganizationalRestructure = 5,

    [Description("Personal Reasons")]
    PersonalReasons = 6
}

public enum StaffDemotionReason
{
    [Description("Performance Issues")]
    PerformanceIssues = 1,

    [Description("Disciplinary Action")]
    DisciplinaryAction = 2,

    [Description("Position Elimination")]
    PositionElimination = 3,

    [Description("Voluntary Demotion")]
    VoluntaryDemotion = 4,

    [Description("Failed Probation")]
    FailedProbation = 5,

    [Description("Organizational Restructure")]
    OrganizationalRestructure = 6
}

public enum StaffSecondmentType
{
    [Description("Internal Secondment")]
    Internal = 1,

    [Description("External Secondment")]
    External = 2,

    [Description("Government Secondment")]
    Government = 3,

    [Description("International Secondment")]
    International = 4
}

public enum StaffActingReason
{
    [Description("Incumbent on Leave")]
    IncumbentOnLeave = 1,

    [Description("Vacancy")]
    Vacancy = 2,

    [Description("Trial Period")]
    TrialPeriod = 3,

    [Description("Special Project")]
    SpecialProject = 4,

    [Description("Development Opportunity")]
    DevelopmentOpportunity = 5
}

public enum StaffActingStatus
{
    [Description("Active")]
    Active = 1,

    [Description("Completed")]
    Completed = 2,

    [Description("Extended")]
    Extended = 3,

    [Description("Terminated Early")]
    TerminatedEarly = 4,

    [Description("Converted to Permanent")]
    ConvertedToPermanent = 5
}

public enum HRAllowanceCalculationMethod
{
    [Description("Fixed Amount")]
    FixedAmount = 1,

    [Description("Percentage of New Salary")]
    PercentageOfNewSalary = 2,

    [Description("Difference Between Salaries")]
    DifferenceBetweenSalaries = 3,

    [Description("Percentage of Current Salary")]
    PercentageOfCurrentSalary = 4
}

#endregion Career Movement

#region Attendance

public enum StaffAttendanceStatus
{
    [Description("Present")]
    Present = 1,

    [Description("Absent")]
    Absent = 2,

    [Description("Late")]
    Late = 3,

    [Description("Half Day")]
    HalfDay = 4,

    [Description("On Leave")]
    OnLeave = 5,

    [Description("Public Holiday")]
    PublicHoliday = 6,

    [Description("Weekend")]
    Weekend = 7,

    [Description("Off Day")]
    OffDay = 8,

    [Description("Remote Work")]
    RemoteWork = 9,

    [Description("On Duty")]
    OnDuty = 10
}

public enum WorkScheduleType
{
    [Description("Fixed Schedule")]
    Fixed = 1,

    [Description("Flexible Schedule")]
    Flexible = 2,

    [Description("Shift Work")]
    Shift = 3,

    [Description("Compressed Workweek")]
    Compressed = 4,

    [Description("Part-Time")]
    PartTime = 5
}

public enum ShiftType
{
    [Description("Morning Shift")]
    Morning = 1,

    [Description("Afternoon Shift")]
    Afternoon = 2,

    [Description("Evening Shift")]
    Evening = 3,

    [Description("Night Shift")]
    Night = 4,

    [Description("Rotating Shift")]
    Rotating = 5,

    [Description("Split Shift")]
    Split = 6
}

public enum ShiftRotationCycle
{
    [Description("Weekly")]
    Weekly = 1,

    [Description("Fortnightly")]
    Fortnightly = 2,

    [Description("Monthly")]
    Monthly = 3,

    [Description("Quarterly")]
    Quarterly = 4
}

public enum RecurrencePattern
{
    [Description("Daily")]
    Daily = 1,

    [Description("Weekly")]
    Weekly = 2,

    [Description("Bi-Weekly")]
    BiWeekly = 3,

    [Description("Monthly")]
    Monthly = 4,

    [Description("Quarterly")]
    Quarterly = 5,

    [Description("Annually")]
    Annually = 6
}

public enum RegularizationType
{
    [Description("Missing Check-In")]
    MissingCheckIn = 1,

    [Description("Missing Check-Out")]
    MissingCheckOut = 2,

    [Description("Wrong Time Entry")]
    WrongTimeEntry = 3,

    [Description("Forgot to Mark")]
    ForgotToMark = 4,

    [Description("System Error")]
    SystemError = 5
}

public enum AttendanceRegularizationStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Approved")]
    Approved = 2,

    [Description("Rejected")]
    Rejected = 3,

    [Description("Applied")]
    Applied = 4
}

public enum OvertimeAllowanceType
{
    [Description("Overtime")]
    Overtime = 1,

    [Description("Night Allowance")]
    NightAllowance = 2,

    [Description("Shift Differential")]
    ShiftDifferential = 3,

    [Description("Weekend Allowance")]
    WeekendAllowance = 4,

    [Description("Holiday Allowance")]
    HolidayAllowance = 5,

    [Description("Transport Allowance")]
    TransportAllowance = 6,

    [Description("Other")]
    Other = 7
}

public enum OvertimeType
{
    [Description("Weekday Overtime")]
    Weekday = 1,

    [Description("Weekend Overtime")]
    Weekend = 2,

    [Description("Holiday Overtime")]
    Holiday = 3,

    [Description("Emergency Overtime")]
    Emergency = 4
}

public enum OvertimeRequestStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Approved")]
    Approved = 2,

    [Description("Rejected")]
    Rejected = 3,

    [Description("Completed")]
    Completed = 4,

    [Description("Cancelled")]
    Cancelled = 5
}

public enum BiometricType
{
    [Description("Fingerprint")]
    Fingerprint = 1,

    [Description("Face")]
    Face = 2,

    [Description("Iris")]
    Iris = 3,

    [Description("Palm")]
    Palm = 4,

    [Description("Vein")]
    Vein = 5,

    [Description("Retina")]

    Retina = 6
}

public enum FingerPosition
{
    [Description("Right Thumb")]
    RightThumb = 1,

    [Description("Right Index")]
    RightIndex = 2,

    [Description("Right Middle")]
    RightMiddle = 3,

    [Description("Right Ring")]
    RightRing = 4,

    [Description("Right Little")]
    RightLittle = 5,

    [Description("Left Thumb")]
    LeftThumb = 6,

    [Description("Left Index")]
    LeftIndex = 7,

    [Description("Left Middle")]
    LeftMiddle = 8,

    [Description("Left Ring")]
    LeftRing = 9,

    [Description("Left Little")]
    LeftLittle = 10
}

public enum AttendanceDeviceType
{
    [Description("Fingerprint Scanner")]
    Fingerprint = 1,

    [Description("Face Recognition")]
    FaceRecognition = 2,

    [Description("RFID Card Reader")]
    RFIDCard = 3,

    [Description("Iris Recognition")]
    Iris = 4,

    [Description("Palm")]
    Palm = 5,

    [Description("QR Code Scanner")]
    QRCode = 6,

    [Description("PIN Pad")]
    PINPad = 7,

    [Description("Mobile App")]
    MobileApp = 8,

    [Description("Web Portal")]
    WebPortal = 9
}

public enum AttendanceLogType
{
    [Description("Check-In")]
    CheckIn = 1,

    [Description("Check-Out")]
    CheckOut = 2,

    [Description("Break Start")]
    BreakStart = 3,

    [Description("Break End")]
    BreakEnd = 4
}

public enum AttendanceImportSourceType
{
    [Description("CSV")]
    CSV = 1,

    [Description("Excel")]
    Excel = 2,

    [Description("API")]
    API = 3,

    [Description("Biometric Device")]
    BiometricDevice = 4,

    [Description("Manual Entry")]
    ManualEntry = 5
}

public enum AttendanceImportStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Processing")]
    Processing = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Failed")]
    Failed = 4,

    [Description("Partial Success")]
    PartialSuccess = 5
}

public enum GeofenceShape
{
    [Description("Circle")]
    Circle = 1,

    [Description("Polygon")]
    Polygon = 2
}

public enum LocationVerificationStatus
{
    [Description("Within Zone")]
    WithinZone = 1,

    [Description("Outside Zone")]
    OutsideZone = 2,

    [Description("Unverified")]
    Unverified = 3,

    [Description("GPS Unavailable")]
    GPSUnavailable = 4
}

public enum RemoteWorkRequestStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Approved")]
    Approved = 2,

    [Description("Rejected")]
    Rejected = 3,

    [Description("Cancelled")]
    Cancelled = 4,
}

public enum HolidayObservanceType
{
    [Description("Mandatory")]
    Mandatory = 1,

    [Description("Optional")]
    Optional = 2,

    [Description("Substitute Day")]
    SubstituteDay = 3
}

public enum PayPeriodType
{
    [Description("Weekly")]
    Weekly = 1,

    [Description("Biweekly")]
    Biweekly = 2,

    [Description("SemiMonthly")]
    SemiMonthly = 3,

    [Description("Monthly")]
    Monthly = 4
}

public enum PayPeriodStatus
{
    [Description("Open")]
    Open = 1,

    [Description("Pending Close")]
    PendingClose = 2,

    [Description("Closed")]
    Closed = 3,

    [Description("Exported to Payroll")]
    ExportedToPayroll = 4
}

public enum PayrollExportStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Failed")]
    Failed = 4,

    [Description("Partial Success")]
    PartialSuccess = 5
}

public enum AttendanceAlertTriggerType
{
    [Description("Consecutive Absences")]
    ConsecutiveAbsences = 1,

    [Description("Chronic Lateness")]
    ChronicLateness = 2,

    [Description("Missing Punch")]
    MissingPunch = 3,

    [Description("Overtime Threshold Reached")]
    OvertimeThresholdReached = 4,

    [Description("Excessive Early Departure")]
    ExcessiveEarlyDeparture = 5,

    [Description("Unauthorised Absence")]
    UnauthorisedAbsence = 6,

    [Description("Low Attendance Percentage")]
    LowAttendancePercentage = 7
}

public enum AttendanceAlertSeverity
{
    [Description("Info")]
    Info = 1,

    [Description("Warning")]
    Warning = 2,

    [Description("Critical")]
    Critical = 3
}

public enum AttendanceAlertStatus
{
    [Description("Active")]
    Active = 1,

    [Description("Acknowledged")]
    Acknowledged = 2,

    [Description("Resolved")]
    Resolved = 3,

    [Description("Dismissed")]
    Dismissed = 4
}

public enum TimesheetStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Submitted")]
    Submitted = 2,

    [Description("Sent to Client")]
    SentToClient = 3,

    [Description("Client Confirmed")]
    ClientConfirmed = 4,

    [Description("Client Rejected")]
    ClientRejected = 5,

    [Description("Billed")]
    Billed = 6,

    [Description("Void")]
    Void = 7,

    [Description("Approved")]
    Approved = 8,

    [Description("Rejected")]
    Rejected = 9
}

public enum TimesheetConfirmationStatus
{
    [Description("Sent")]
    Sent = 1,

    [Description("Viewed")]
    Viewed = 2,

    [Description("Confirmed")]
    Confirmed = 3,

    [Description("Rejected")]
    Rejected = 4,

    [Description("Expired")]
    Expired = 5,

    [Description("Resent")]
    Resent = 6
}

public enum TimesheetInvoiceStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Sent")]
    Sent = 2,

    [Description("Partially Paid")]
    PartiallyPaid = 3,

    [Description("Paid")]
    Paid = 4,

    [Description("Voided")]
    Voided = 5,

    [Description("Overdue")]
    Overdue = 6
}

public enum BillingCycle
{
    [Description("Weekly")]
    Weekly = 1,

    [Description("Fortnightly")]
    Fortnightly = 2,

    [Description("Monthly")]
    Monthly = 3,

    [Description("Milestone-Based")]
    MilestoneBased = 4,

    [Description("On Completion")]
    OnCompletion = 5
}

public enum ClientEngagementStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Active")]
    Active = 2,

    [Description("Suspended")]
    Suspended = 3,

    [Description("Completed")]
    Completed = 4,

    [Description("Terminated")]
    Terminated = 5
}

#endregion Attendance

#region Company Schedule

public enum EventCategory
{
    [Description("Meeting")]
    Meeting = 1,

    [Description("Training")]
    Training = 2,

    [Description("Company Event")]
    CompanyEvent = 3,

    [Description("Deadline")]
    Deadline = 4,

    [Description("Holiday")]
    Holiday = 5,

    [Description("Conference")]
    Conference = 6,

    [Description("Social Event")]
    SocialEvent = 7,

    [Description("Milestone")]
    Milestone = 8
}

public enum EventType
{
    [Description("Internal")]
    Internal = 1,

    [Description("External")]
    External = 2,

    [Description("Client Meeting")]
    ClientMeeting = 3,

    [Description("Statutory")]
    Statutory = 4,

    [Description("Board Meeting")]
    BoardMeeting = 5
}

public enum EventPriority
{
    [Description("Critical")]
    Critical = 1,

    [Description("High")]
    High = 2,

    [Description("Medium")]
    Medium = 3,

    [Description("Low")]
    Low = 4
}

public enum EventLocation
{
    [Description("On-Site")]
    OnSite = 1,

    [Description("Off-Site")]
    OffSite = 2,

    [Description("Virtual/Online")]
    Virtual = 3,

    [Description("Hybrid")]
    Hybrid = 4
}

public enum ParticipantScope
{
    [Description("All Staff")]
    AllStaff = 1,

    [Description("Department")]
    Department = 2,

    [Description("Selected Individuals")]
    Selected = 3,

    [Description("Management Only")]
    ManagementOnly = 4,

    [Description("External Only")]
    ExternalOnly = 5
}

public enum EventVisibility
{
    [Description("Public")]
    Public = 1,

    [Description("Private")]
    Private = 2,

    [Description("Department")]
    Department = 3,

    [Description("Management")]
    Management = 4,

    [Description("Confidential")]
    Confidential = 5
}

/// <summary>
/// What kind of thing is standing between a panelist and a proposed interview window
/// (round 4, lane D1).
/// </summary>
public enum CommitmentKind
{
    [Description("Interview")]
    Interview = 1,

    [Description("Leave")]
    Leave = 2,

    [Description("Travel")]
    Travel = 3,

    [Description("Meeting or event")]
    Event = 4,

    [Description("Room booking")]
    RoomBooking = 5,

    [Description("Training")]
    Training = 6,

    [Description("Business closure")]
    Closure = 7,

    [Description("Public holiday")]
    Holiday = 8,
}

/// <summary>
/// Whether a clash refuses the schedule or merely warns about it (round 4, decision D-5).
/// </summary>
/// <remarks>
/// <para><b>Hard</b> is a commitment that is both confirmed and time-precise: another interview, a
/// Confirmed room booking, a Confirmed event this person accepted. Scheduling over it would
/// double-book a real person at a real hour, so it is refused unless the recruiter supplies an
/// override reason, which is recorded on the interview.</para>
///
/// <para><b>Soft</b> is everything the system knows but should not overrule: leave and travel, which
/// are recorded by the DAY and cannot say whether the 09:00 hour is free; a Tentative booking; a
/// training nomination, which is a plan rather than an attendance; a closure or a public holiday,
/// which say the office is shut, not that the person is unavailable. A recruiter who knows the
/// panelist swapped a meeting should not be stopped by the system's second-hand information.</para>
/// </remarks>
public enum CommitmentHardness
{
    [Description("Warns")]
    Soft = 1,

    [Description("Refuses")]
    Hard = 2,
}

public enum EventStatus
{
    [Description("Scheduled")]
    Scheduled = 1,

    [Description("Confirmed")]
    Confirmed = 2,

    [Description("In Progress")]
    InProgress = 3,

    [Description("Completed")]
    Completed = 4,

    [Description("Cancelled")]
    Cancelled = 5,

    [Description("Postponed")]
    Postponed = 6,

    [Description("Rescheduled")]
    Rescheduled = 7
}

public enum ParticipantRole
{
    [Description("Organizer")]
    Organizer = 1,

    [Description("Presenter")]
    Presenter = 2,

    [Description("Attendee")]
    Attendee = 3,

    [Description("Optional Attendee")]
    Optional = 4,

    [Description("Facilitator")]
    Facilitator = 5
}

public enum InvitationStatus
{
    [Description("Not Sent")]
    NotSent = 1,

    [Description("Sent")]
    Sent = 2,

    [Description("Accepted")]
    Accepted = 3,

    [Description("Declined")]
    Declined = 4,

    [Description("Tentative")]
    Tentative = 5,

    [Description("No Response")]
    NoResponse = 6
}

public enum EventAttachmentType
{
    [Description("Agenda")]
    Agenda = 1,

    [Description("Minutes")]
    Minutes = 2,

    [Description("Presentation")]
    Presentation = 3,

    [Description("Handout")]
    Handout = 4,

    [Description("Resource Material")]
    Resource = 5
}

public enum EventTaskCategory
{
    [Description("Pre-Event Preparation")]
    Preparation = 1,

    [Description("During Event")]
    DuringEvent = 2,

    [Description("Post-Event Follow-up")]
    FollowUp = 3
}

public enum TaskPriority
{
    [Description("Critical")]
    Critical = 1,

    [Description("High")]
    High = 2,

    [Description("Medium")]
    Medium = 3,

    [Description("Low")]
    Low = 4
}

public enum EventTaskStatus
{
    [Description("Not Started")]
    NotStarted = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Overdue")]
    Overdue = 4,

    [Description("Cancelled")]
    Cancelled = 5
}

public enum RoomType
{
    [Description("Conference Room")]
    Conference = 1,

    [Description("Boardroom")]
    Boardroom = 2,

    [Description("Training Room")]
    Training = 3,

    [Description("Huddle Room")]
    Huddle = 4,

    [Description("Auditorium")]
    Auditorium = 5
}

public enum BookingStatus
{
    [Description("Tentative")]
    Tentative = 1,

    [Description("Confirmed")]
    Confirmed = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Cancelled")]
    Cancelled = 4,

    [Description("No Show")]
    NoShow = 5
}

public enum MilestoneCategory
{
    [Description("Company Anniversary")]
    CompanyAnniversary = 1,

    [Description("Achievement")]
    Achievement = 2,

    [Description("Product Launch")]
    ProductLaunch = 3,

    [Description("Target/Goal")]
    Target = 4,

    [Description("Certification")]
    Certification = 5
}

public enum ClosureType
{
    [Description("Full Closure")]
    FullClosure = 1,

    [Description("Partial Closure")]
    PartialClosure = 2,

    [Description("Department Closure")]
    DepartmentClosure = 3,

    [Description("Station Closure")]
    StationClosure = 4
}

public enum FiscalYearStatus
{
    [Description("Active")]
    Active = 1,

    [Description("Closed")]
    Closed = 2,

    [Description("Archived")]
    Archived = 3
}

#endregion Company Schedule

#region Miscellaneous

public enum RequestApprovalStage
{
    [Description("Supervisor Approval")]
    SupervisorApproval = 1,

    [Description("HOD Approval")]
    HodApproval = 2,

    [Description("HR Review")]
    HrReview = 3,

    [Description("Finance Review")]
    FinanceReview = 4
}

public enum ApprovalStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Approved")]
    Approved = 2,

    [Description("Rejected")]
    Rejected = 3
}

public enum ApprovalRole
{
    [Description("Supervisor")]
    Supervisor = 1,

    [Description("Head of Department")]
    HeadOfDepartment = 2,

    [Description("HR Manager")]
    HRManager = 3,

    [Description("Finance Manager")]
    FinanceManager = 4,

    [Description("CEO")]
    CEO = 5
}

#endregion

#region HR Cycle Dashboard

/// <summary>Describes where a pipeline-step deadline sits relative to today.</summary>
public enum DeadlineState
{
    None = 0,
    Safe = 1, // > 7 days
    Approaching = 2, // 4–7 days
    Imminent = 3, // 1–3 days
    Today = 4, // due today
    Overdue = 5, // past due
    Passed = 6, // completed / no longer relevant
}

/// <summary>Why an appraisal appears in the HR attention queue.</summary>
public enum AttentionReason
{
    OverdueAtStep = 0,
    PIPrecommendation = 1,
    TerminationRecommendation = 2,
    AppealFiled = 3,
    AppealOverdue = 4,
    CalibrationAdjustmentLarge = 5,
    ManagerEvalIncomplete = 6,
    LowScore = 7,
}

/// <summary>Visual severity level for an attention item.</summary>
public enum AttentionSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2,
}

/// <summary>Event type used for in-app notifications and the cycle activity feed.</summary>
public enum AppraisalNotificationType
{
    SelfEvalWindowOpen = 0,
    SelfEvalSubmitted = 1,
    PeerNominationSubmitted = 2,
    PeerEvaluationAssigned = 3,
    PeerEvaluationCompleted = 4,
    AllPeerEvalsComplete = 5,
    ManagerEvalSubmitted = 6,
    CalibrationComplete = 7,
    HRReviewApproved = 8,
    ConversationCompleted = 9,
    EmployeeAcknowledged = 10,
    AppealSubmitted = 11,
    AppealResolved = 12,
    DeadlineApproaching = 13,
    DeadlineImminent = 14,
    DeadlinePassed = 15,
    AutoLocked = 16,
    PeerEvaluationReminder = 17,
    ActionRequired = 18,

    // ── Conversations, development plans and improvement plans ────────────────
    // Added with the development/PIP/conversation slice. Stored as int, so new members are
    // schema-safe; keep the existing numbering untouched.

    /// <summary>A manager has put an appraisal conversation in the diary.</summary>
    ConversationScheduled = 19,

    /// <summary>A development plan has been activated and is now the employee's to work on.</summary>
    DevelopmentPlanActivated = 20,

    /// <summary>A manager has written feedback on a development plan.</summary>
    DevelopmentFeedbackAdded = 21,

    /// <summary>An improvement plan has been approved and is now in force.</summary>
    PipOpened = 22,

    /// <summary>A PIP review meeting has been scheduled.</summary>
    PipMeetingScheduled = 23,

    /// <summary>An improvement plan has been closed with an outcome.</summary>
    PipOutcomeRecorded = 24,
}

/// <summary>Urgency level for in-app notifications.</summary>
public enum NotificationUrgency
{
    Normal = 0,
    Warning = 1,
    Urgent = 2,
}

#endregion

#region Candidate Portal

/// <summary>Candidate's preferred work arrangement.</summary>
public enum PreferredWorkArrangement
{
    [Description("Any")]
    Any = 0,

    [Description("On-Site")]
    OnSite = 1,

    [Description("Hybrid")]
    Hybrid = 2,

    [Description("Remote")]
    Remote = 3,
}

/// <summary>Work authorisation / right-to-work status of the candidate.</summary>
public enum WorkAuthorizationStatus
{
    [Description("Not Specified")]
    NotSpecified = 0,

    [Description("Citizen")]
    Citizen = 1,

    [Description("Permanent Resident")]
    PermanentResident = 2,

    [Description("Work Visa (No Sponsorship Needed)")]
    WorkVisa = 3,

    [Description("Requires Sponsorship")]
    RequiresSponsorship = 4,
}

/// <summary>Self-assessed proficiency level for a spoken/written language.</summary>
public enum LanguageProficiency
{
    [Description("Basic")]
    Basic = 1,

    [Description("Conversational")]
    Conversational = 2,

    [Description("Professional Working")]
    ProfessionalWorking = 3,

    [Description("Full Professional / Fluent")]
    Fluent = 4,

    [Description("Native / Bilingual")]
    Native = 5,
}

// ── Talent Pool (Candidate-facing) ────────────────────────────────────────────

/// <summary>How a job candidate was added to the talent pool.</summary>
public enum TalentPoolEntrySource
{
    [Description("Not Specified")]
    NotSpecified = 0,

    [Description("Applied & Retained")]
    AppliedAndRetained = 1,

    [Description("Recruiter Added")]
    RecruiterAdded = 2,

    [Description("Referral")]
    Referral = 3,

    [Description("Career Fair / Event")]
    CareerFair = 4,

    [Description("LinkedIn / Social")]
    LinkedIn = 5,

    [Description("Unsolicited CV")]
    UnsolicitedCv = 6,

    [Description("Internal Transfer")]
    InternalTransfer = 7,

    [Description("Candidate Portal")]
    CandidatePortal = 8,
}

/// <summary>The current engagement / activity status of a candidate in the talent pool.</summary>
public enum TalentPoolCandidateStatus
{
    [Description("Active")]
    Active = 1,

    [Description("Passive")]
    Passive = 2,

    [Description("Dormant")]
    Dormant = 3,

    [Description("Expired")]
    Expired = 4,

    [Description("Converted")]
    Converted = 5,

    [Description("On Hold")]
    OnHold = 6,
}

/// <summary>Type of engagement / contact event logged against a pool candidate.</summary>
public enum CandidateEngagementEventType
{
    [Description("Email")]
    Email = 1,

    [Description("Phone Call")]
    PhoneCall = 2,

    [Description("In-Person Meeting")]
    InPersonMeeting = 3,

    [Description("Invited to Apply")]
    InvitedToApply = 4,

    [Description("Profile Review")]
    ProfileReview = 5,

    [Description("Status Update")]
    StatusUpdate = 6,

    [Description("LinkedIn Message")]
    LinkedInMessage = 7,

    [Description("SMS")]
    Sms = 8,

    [Description("Interview / Assessment")]
    InterviewAssessment = 9,

    [Description("Internal Note")]
    InternalNote = 10,
}

/// <summary>Type of bulk operation to perform on talent pool candidates.</summary>
public enum BulkTalentPoolOperation
{
    AssignSegment = 1,
    RemoveSegment = 2,
    SetStatus = 3,
    RemoveFromPool = 4,
}

#endregion

#region Staff Travels

#region Travel classification

public enum StaffTravelType
{
    [Description("Domestic")]
    Domestic = 1,

    [Description("International")]
    International = 2,

    [Description("Cross-Border")]
    CrossBorder = 3,

    [Description("Regional")]
    Regional = 4,

    [Description("Overseas Assignment")]
    OverseasAssignment = 5,

    [Description("Field Visit")]
    FieldVisit = 6,

    [Description("Training")]
    Training = 7,

    [Description("Conference")]
    Conference = 8,

    [Description("Client Visit")]
    ClientVisit = 9,

    [Description("Government Duty")]
    GovernmentDuty = 10,

    [Description("Emergency")]
    Emergency = 11
}

public enum StaffTravelPurpose
{
    [Description("Business Development")]
    BusinessDevelopment = 1,

    [Description("Client Meeting")]
    ClientMeeting = 2,

    [Description("Conference")]
    Conference = 3,

    [Description("Training")]
    Training = 4,

    [Description("Audit")]
    Audit = 5,

    [Description("Inspection")]
    Inspection = 6,

    [Description("Project Work")]
    ProjectWork = 7,

    [Description("Site Visit")]
    SiteVisit = 8,

    [Description("Government Engagement")]
    GovernmentEngagement = 9,

    [Description("Personal Combined")]
    PersonalCombined = 10,

    [Description("Emergency")]
    Emergency = 11,

    [Description("Other")]
    Other = 12
}

public enum StaffTravelPriority
{
    [Description("Routine")]
    Routine = 1,

    [Description("Urgent")]
    Urgent = 2,

    [Description("Emergency")]
    Emergency = 3
}

public enum StaffTravelRequestStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Submitted")]
    Submitted = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Rejected")]
    Rejected = 4,

    [Description("Cancelled")]
    Cancelled = 5,

    [Description("Returned For Revision")]
    ReturnedForRevision = 6,

    [Description("In Progress")]
    InProgress = 7,

    [Description("Completed")]
    Completed = 8,

    [Description("Closed")]
    Closed = 9
}

public enum TravelItineraryStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Pending Review")]
    PendingReview = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Active")]
    Active = 4,

    [Description("Completed")]
    Completed = 5,

    [Description("Cancelled")]
    Cancelled = 6,

    [Description("Superseded")]
    Superseded = 7
}

public enum TravelItineraryLegType
{
    [Description("Departure")]
    Departure = 1,

    [Description("Transit")]
    Transit = 2,

    [Description("Arrival")]
    Arrival = 3,

    [Description("Stay")]
    Stay = 4,

    [Description("Day Trip")]
    DayTrip = 5,

    [Description("Return")]
    Return = 6
}

public enum StaffTravelTransportMode
{
    [Description("Flight")]
    Flight = 1,

    [Description("Train")]
    Train = 2,

    [Description("Bus")]
    Bus = 3,

    [Description("Car")]
    Car = 4,

    [Description("Ferry")]
    Ferry = 5,

    [Description("Helicopter")]
    Helicopter = 6,

    [Description("Motorcycle")]
    Motorcycle = 7,

    [Description("Walk")]
    Walk = 8
}

public enum StaffTravelActivityType
{
    [Description("Meeting")]
    Meeting = 1,

    [Description("Conference")]
    Conference = 2,

    [Description("Training")]
    Training = 3,

    [Description("Site Visit")]
    SiteVisit = 4,

    [Description("Client Dinner")]
    ClientDinner = 5,

    [Description("Free Time")]
    FreeTime = 6,

    [Description("Transit Layover")]
    TransitLayover = 7,

    [Description("Other")]
    Other = 8
}

#endregion

#region Workflow & approvals

public enum TravelInitiatorRole
{
    [Description("Employee")]
    Employee = 1,

    [Description("Manager")]
    Manager = 2,

    [Description("HR Admin")]
    HrAdmin = 3,

    [Description("Travel Desk")]
    TravelDesk = 4,

    [Description("System")]
    System = 5
}

public enum TravelApproverType
{
    [Description("Line Manager")]
    LineManager = 1,

    [Description("Department Head")]
    DepartmentHead = 2,

    [Description("HR Manager")]
    HrManager = 3,

    [Description("Finance Manager")]
    FinanceManager = 4,

    [Description("Travel Desk")]
    TravelDesk = 5,

    [Description("Executive")]
    Executive = 6,

    [Description("Specific Person")]
    SpecificPerson = 7
}

public enum TravelApprovalDecision
{
    [Description("Approved")]
    Approved = 1,

    [Description("Rejected")]
    Rejected = 2,

    [Description("Returned For Revision")]
    ReturnedForRevision = 3,

    [Description("Escalated")]
    Escalated = 4,

    [Description("Delegated")]
    Delegated = 5,

    [Description("Abstained")]
    Abstained = 6
}

public enum TravelApprovalInstanceStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Rejected")]
    Rejected = 4,

    [Description("Withdrawn")]
    Withdrawn = 5,

    [Description("Escalated")]
    Escalated = 6,

    [Description("Expired")]
    Expired = 7
}

#endregion

#region Bookings

public enum FlightCabinClass
{
    [Description("Economy")]
    Economy = 1,

    [Description("Premium Economy")]
    PremiumEconomy = 2,

    [Description("Business")]
    Business = 3,

    [Description("First")]
    First = 4
}

public enum TravelBookingChannel
{
    [Description("Self Service")]
    SelfService = 1,

    [Description("Travel Desk")]
    TravelDesk = 2,

    [Description("Travel Agency")]
    TravelAgency = 3,

    [Description("Direct Airline")]
    DirectAirline = 4,

    [Description("Direct Hotel")]
    DirectHotel = 5,

    [Description("Online Portal")]
    OnlinePortal = 6
}

public enum TravelBookingStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Confirmed")]
    Confirmed = 2,

    [Description("Ticketed")]
    Ticketed = 3,

    [Description("Cancelled")]
    Cancelled = 4,

    [Description("Refunded")]
    Refunded = 5,

    [Description("No Show")]
    NoShow = 6,

    [Description("Completed")]
    Completed = 7,

    [Description("On Hold")]
    OnHold = 8
}

public enum GroundTransportType
{
    [Description("Taxi")]
    Taxi = 1,

    [Description("Rideshare")]
    Rideshare = 2,

    [Description("Bus")]
    Bus = 3,

    [Description("Train")]
    Train = 4,

    [Description("Metro")]
    Metro = 5,

    [Description("Company Vehicle")]
    CompanyVehicle = 6,

    [Description("Private Car Hire")]
    PrivateCarHire = 7,

    [Description("Shuttle")]
    Shuttle = 8,

    [Description("Motorcycle")]
    Motorcycle = 9,

    [Description("Ferry")]
    Ferry = 10
}

public enum VehicleCategory
{
    [Description("Economy")]
    Economy = 1,

    [Description("Compact")]
    Compact = 2,

    [Description("Intermediate")]
    Intermediate = 3,

    [Description("Full Size")]
    FullSize = 4,

    [Description("SUV")]
    Suv = 5,

    [Description("Luxury")]
    Luxury = 6,

    [Description("Minivan")]
    Minivan = 7,

    [Description("Truck")]
    Truck = 8
}

#endregion

#region Finance & expenses

public enum TravelExpenseCategory
{
    [Description("Airfare")]
    Airfare = 1,

    [Description("Accommodation")]
    Accommodation = 2,

    [Description("Meals")]
    Meals = 3,

    [Description("Local Transport")]
    LocalTransport = 4,

    [Description("Taxi / Rideshare")]
    TaxiRideshare = 5,

    [Description("Car Rental")]
    CarRental = 6,

    [Description("Fuel")]
    Fuel = 7,

    [Description("Visa Fees")]
    VisaFees = 8,

    [Description("Insurance")]
    Insurance = 9,

    [Description("Communication")]
    Communication = 10,

    [Description("Conference Fees")]
    ConferenceFees = 11,

    [Description("Gifts & Entertainment")]
    GiftsEntertainment = 12,

    [Description("Tips & Gratuity")]
    TipsGratuity = 13,

    [Description("Laundry")]
    Laundry = 14,

    [Description("Medical")]
    Medical = 15,

    [Description("Baggage Fees")]
    BaggageFees = 16,

    [Description("Miscellaneous")]
    Miscellaneous = 17
}

public enum TravelClaimStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Submitted")]
    Submitted = 2,

    [Description("Under Review")]
    UnderReview = 3,

    [Description("Approved")]
    Approved = 4,

    [Description("Partially Approved")]
    PartiallyApproved = 5,

    [Description("Rejected")]
    Rejected = 6,

    [Description("Paid")]
    Paid = 7,

    [Description("Returned")]
    Returned = 8
}

public enum TravelExpenseLineStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Approved")]
    Approved = 2,

    [Description("Rejected")]
    Rejected = 3,

    [Description("Queried")]
    Queried = 4,

    [Description("Resolved")]
    Resolved = 5
}

public enum TravelClaimType
{
    [Description("Post Travel")]
    PostTravel = 1,

    [Description("Advance Settlement")]
    AdvanceSettlement = 2,

    [Description("Partial Claim")]
    PartialClaim = 3,

    [Description("Amendment")]
    Amendment = 4
}

public enum TravelAdvanceType
{
    [Description("Cash")]
    Cash = 1,

    [Description("Corporate Card Load")]
    CorporateCardLoad = 2,

    [Description("Petty Cash")]
    PettyCash = 3,

    [Description("Wire Transfer")]
    WireTransfer = 4
}

public enum TravelAdvanceStatus
{
    [Description("Requested")]
    Requested = 1,

    [Description("Approved")]
    Approved = 2,

    [Description("Disbursed")]
    Disbursed = 3,

    [Description("Partially Settled")]
    PartiallySettled = 4,

    [Description("Fully Settled")]
    FullySettled = 5,

    [Description("Overdue")]
    Overdue = 6,

    [Description("Written Off")]
    WrittenOff = 7
}

public enum TravelPaymentMethod
{
    [Description("Bank Transfer")]
    BankTransfer = 1,

    [Description("Payroll Offset")]
    PayrollOffset = 2,

    [Description("Cash")]
    Cash = 3,

    [Description("Cheque")]
    Cheque = 4,

    [Description("Corporate Card")]
    CorporateCard = 5
}

public enum TravelAllowanceType
{
    [Description("Daily Subsistence")]
    DailySubsistence = 1,

    [Description("Accommodation")]
    Accommodation = 2,

    [Description("Transport")]
    Transport = 3,

    [Description("Incidental")]
    Incidental = 4,

    [Description("Meals Only")]
    MealsOnly = 5,

    [Description("Hardship")]
    Hardship = 6
}

#endregion

#region Policy

public enum TravelPolicyRuleType
{
    [Description("Hard Limit")]
    HardLimit = 1,

    [Description("Soft Limit")]
    SoftLimit = 2,

    [Description("Warning")]
    Warning = 3,

    [Description("Mandatory")]
    Mandatory = 4,

    [Description("Preferred")]
    Preferred = 5,

    [Description("Prohibited")]
    Prohibited = 6
}

public enum TravelPolicyViolationAction
{
    [Description("Block")]
    Block = 1,

    [Description("Warn")]
    Warn = 2,

    [Description("Flag For Review")]
    FlagForReview = 3,

    [Description("Require Justification")]
    RequireJustification = 4,

    [Description("Escalate To Approver")]
    EscalateToApprover = 5
}

public enum TravelPolicyExceptionStatus
{
    [Description("Pending")]
    Pending = 1,

    [Description("Approved")]
    Approved = 2,

    [Description("Rejected")]
    Rejected = 3,

    [Description("Expired")]
    Expired = 4
}

#endregion

#region Vendors

public enum TravelVendorType
{
    [Description("Airline")]
    Airline = 1,

    [Description("Hotel Chain")]
    HotelChain = 2,

    [Description("Car Rental")]
    CarRental = 3,

    [Description("Travel Agency")]
    TravelAgency = 4,

    [Description("GDS")]
    Gds = 5,

    [Description("Rideshare")]
    Rideshare = 6,

    [Description("Insurance")]
    Insurance = 7,

    [Description("Visa Service")]
    VisaService = 8,

    [Description("Ground Transport")]
    GroundTransport = 9,

    [Description("Forex")]
    Forex = 10,

    [Description("Other")]
    Other = 11
}

#endregion

#region Compliance & safety

public enum TravelDocumentType
{
    [Description("Passport")]
    Passport = 1,

    [Description("National ID")]
    NationalId = 2,

    [Description("Visa")]
    Visa = 3,

    [Description("Resident Permit")]
    ResidentPermit = 4,

    [Description("Work Permit")]
    WorkPermit = 5,

    [Description("Frequent Flyer Card")]
    FrequentFlyerCard = 6,

    [Description("Hotel Loyalty Card")]
    HotelLoyaltyCard = 7,

    [Description("Driving Licence")]
    DrivingLicence = 8,

    [Description("Vaccine Certificate")]
    VaccineCertificate = 9
}

public enum VisaRequirementType
{
    [Description("Visa Free")]
    VisaFree = 1,

    [Description("Visa On Arrival")]
    VisaOnArrival = 2,

    [Description("E-Visa")]
    EVisa = 3,

    [Description("Embassy Visa")]
    EmbassyVisa = 4,

    [Description("Prohibited")]
    Prohibited = 5,

    [Description("Conditional")]
    Conditional = 6
}

public enum VisaApplicationStatus
{
    [Description("Not Started")]
    NotStarted = 1,

    [Description("In Preparation")]
    InPreparation = 2,

    [Description("Submitted")]
    Submitted = 3,

    [Description("Approved")]
    Approved = 4,

    [Description("Rejected")]
    Rejected = 5,

    [Description("Expired")]
    Expired = 6,

    [Description("Not Required")]
    NotRequired = 7
}

public enum TravelRiskLevel
{
    [Description("Low")]
    Low = 1,

    [Description("Medium")]
    Medium = 2,

    [Description("High")]
    High = 3,

    [Description("Critical")]
    Critical = 4,

    [Description("Prohibited")]
    Prohibited = 5
}

public enum TravelRiskCategory
{
    [Description("Security")]
    Security = 1,

    [Description("Health")]
    Health = 2,

    [Description("Natural Disaster")]
    NaturalDisaster = 3,

    [Description("Political Instability")]
    PoliticalInstability = 4,

    [Description("Infrastructure")]
    Infrastructure = 5,

    [Description("Crime")]
    Crime = 6,

    [Description("Other")]
    Other = 7
}

public enum TravelAlertType
{
    [Description("Security")]
    Security = 1,

    [Description("Health Outbreak")]
    HealthOutbreak = 2,

    [Description("Weather")]
    Weather = 3,

    [Description("Political Unrest")]
    PoliticalUnrest = 4,

    [Description("Transport Disruption")]
    TransportDisruption = 5,

    [Description("Natural Disaster")]
    NaturalDisaster = 6
}

public enum TravelAlertSeverity
{
    [Description("Info")]
    Info = 1,

    [Description("Warning")]
    Warning = 2,

    [Description("Critical")]
    Critical = 3,

    [Description("Emergency")]
    Emergency = 4
}

public enum TravelInsuranceType
{
    [Description("Corporate Group")]
    CorporateGroup = 1,

    [Description("Individual")]
    Individual = 2,

    [Description("Top Up")]
    TopUp = 3,

    [Description("Statutory")]
    Statutory = 4
}

public enum TravelInsuranceCoverageType
{
    [Description("Medical")]
    Medical = 1,

    [Description("Trip Cancellation")]
    TripCancellation = 2,

    [Description("Baggage")]
    Baggage = 3,

    [Description("Personal Liability")]
    PersonalLiability = 4,

    [Description("Emergency Evacuation")]
    EmergencyEvacuation = 5,

    [Description("Comprehensive")]
    Comprehensive = 6
}

public enum TravelHealthRequirementType
{
    [Description("Vaccination")]
    Vaccination = 1,

    [Description("PCR Test")]
    PcrTest = 2,

    [Description("Rapid Antigen Test")]
    RapidAntigenTest = 3,

    [Description("Medical Clearance")]
    MedicalClearance = 4,

    [Description("Health Declaration")]
    HealthDeclaration = 5,

    [Description("Quarantine")]
    Quarantine = 6
}

#endregion

#region Miscellaneous

public enum TravelRequestCommentType
{
    [Description("Comment")]
    Comment = 1,

    [Description("Internal Note")]
    InternalNote = 2,

    [Description("Rejection Reason")]
    RejectionReason = 3,

    [Description("Query")]
    Query = 4,

    [Description("Response")]
    Response = 5,

    [Description("System Note")]
    SystemNote = 6
}

public enum TravelAttachmentType
{
    [Description("Invitation Letter")]
    InvitationLetter = 1,

    [Description("Conference Brochure")]
    ConferenceBrochure = 2,

    [Description("Receipt")]
    Receipt = 3,

    [Description("Visa Document")]
    VisaDocument = 4,

    [Description("Insurance Certificate")]
    InsuranceCertificate = 5,

    [Description("Medical Certificate")]
    MedicalCertificate = 6,

    [Description("Other")]
    Other = 7
}

public enum GroupTravelStatus
{
    [Description("Planning")]
    Planning = 1,

    [Description("Open")]
    Open = 2,

    [Description("Closed")]
    Closed = 3,

    [Description("In Progress")]
    InProgress = 4,

    [Description("Completed")]
    Completed = 5,

    [Description("Cancelled")]
    Cancelled = 6
}

#endregion

#region Orientation Management

/// <summary>
/// Broad category of what an orientation program addresses. Orientation covers
/// onboarding as well as policy/product/compliance awareness rollouts.
/// </summary>
public enum OrientationProgramType
{
    /// <summary>
    /// New-hire or role-transition onboarding.
    /// </summary>
    Onboarding = 1,

    /// <summary>
    /// Awareness of a new or updated company policy.
    /// </summary>
    PolicyAwareness = 2,

    /// <summary>
    /// Introduction or update to a product or service.
    /// </summary>
    ProductLaunch = 3,

    /// <summary>
    /// Mandatory regulatory, legal, or industry compliance.
    /// </summary>
    Compliance = 4,

    /// <summary>
    /// Health, safety, and environmental orientation.
    /// </summary>
    HealthAndSafety = 5,

    /// <summary>
    /// Technology tool or system adoption.
    /// </summary>
    SystemsAndTools = 6,

    /// <summary>
    /// Organizational culture, values, or mission awareness.
    /// </summary>
    CultureAndValues = 7,

    /// <summary>
    /// Catch-all for any other orientation type.
    /// </summary>
    General = 99
}

/// <summary>
/// Lifecycle state of an orientation program definition.
/// </summary>
public enum OrientationProgramStatus
{
    /// <summary>
    /// Being authored; not yet available.
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Submitted for approval before activation.
    /// </summary>
    PendingApproval = 2,

    /// <summary>
    /// Live and available for enrollment.
    /// </summary>
    Active = 3,

    /// <summary>
    /// Temporarily paused.
    /// </summary>
    Suspended = 4,

    /// <summary>
    /// Withdrawn from active use but retained.
    /// </summary>
    Retired = 5,

    /// <summary>
    /// Archived for historical reference only.
    /// </summary>
    Archived = 6
}

/// <summary>
/// How an orientation is delivered to participants.
/// </summary>
public enum OrientationDeliveryMode
{
    /// <summary>
    /// Physical, in-person classroom or meeting room.
    /// </summary>
    InPerson = 1,

    /// <summary>
    /// Live virtual session via video conferencing.
    /// </summary>
    VirtualInstructor = 2,

    /// <summary>
    /// Self-paced online content (e-learning, videos).
    /// </summary>
    SelfPacedOnline = 3,

    /// <summary>
    /// Mix of in-person/virtual and self-paced elements.
    /// </summary>
    Blended = 4,

    /// <summary>
    /// Pre-recorded video with no live component.
    /// </summary>
    VideoOnDemand = 5,

    /// <summary>
    /// Printed material or document-only delivery.
    /// </summary>
    PrintedMaterial = 6
}

/// <summary>
/// Relative importance of completing an orientation program.
/// </summary>
public enum OrientationPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
    Mandatory = 5
}

/// <summary>
/// Which employee population an orientation program targets.
/// </summary>
/// <remarks>
/// ⚠ Since round 4 lane I this is a DESCRIPTIVE label on the programme only. Audience rules — the
/// part that actually enrols people — target through <see cref="HrAudienceTargetType"/> plus
/// <see cref="OrientationAudiencePopulation"/>, because a single value from this list could not say
/// "new hires in one unit", and <see cref="JobGrade"/> / <see cref="Custom"/> named axes no resolver
/// could ever evaluate.
/// </remarks>
public enum OrientationAudienceScope
{
    AllEmployees = 1,
    NewHires = 2,
    OrganizationUnit = 3,
    JobGrade = 4,
    Location = 5,
    Role = 6,
    Management = 7,
    Contractors = 8,
    Custom = 99
}

/// <summary>
/// Which kind of person an orientation audience rule reaches, layered ON TOP of its target
/// (round 4, lane I1).
/// </summary>
/// <remarks>
/// <para><b>Why this is a second axis and not more members of the target enum.</b> An audience
/// rule used to be ONE <see cref="OrientationAudienceScope"/> value, so "new hires" and "the
/// Operations unit" were alternatives — "new hires in Operations" could not be said at all. The
/// target now comes from the shared <see cref="HrAudienceTargetType"/> (where a person sits) and
/// this says who, of the people there, the rule means. The two are intersected.</para>
///
/// <para>Each is DERIVED from data the system already holds, never typed in:</para>
/// <list type="bullet">
///   <item><see cref="NewHires"/> — employed within the last 90 days
///   (<c>OrientationTriggerWindows.NewHireWindowDays</c>).</item>
///   <item><see cref="Management"/> — heads an organisation unit, or has at least one active
///   direct report.</item>
///   <item><see cref="Contractors"/> — employed on a Contract, Fixed-term, Consultant or
///   Freelance basis.</item>
/// </list>
///
/// <para>⚠ <see cref="Anyone"/> is <c>0</c> on purpose: it is the value every rule that predates
/// this column means, so a <c>DEFAULT 0</c> is the true value for existing rows rather than the
/// non-member this module has tripped over before.</para>
/// </remarks>
public enum OrientationAudiencePopulation
{
    /// <summary>Everyone the target reaches.</summary>
    Anyone = 0,

    /// <summary>Only people employed within the new-hire window.</summary>
    NewHires = 1,

    /// <summary>Only people who head a unit or manage someone.</summary>
    Management = 2,

    /// <summary>Only contract, fixed-term, consultant and freelance staff.</summary>
    Contractors = 3
}

/// <summary>
/// The type of content a module represents.
/// </summary>
public enum OrientationModuleType
{
    InformationContent = 1,
    VideoLesson = 2,
    Interactive = 3,
    Assessment = 4,
    Acknowledgement = 5,
    Survey = 6,
    LiveSession = 7
}

/// <summary>
/// Format/type of a single orientation content item.
/// </summary>
public enum OrientationContentType
{
    Video = 1,
    Audio = 2,
    PDF = 3,
    Document = 4,
    Presentation = 5,
    ExternalLink = 6,
    EmbeddedWebPage = 7,
    Image = 8,
    Text = 9,
    Quiz = 10
}

/// <summary>
/// Lifecycle state of a scheduled orientation session instance.
/// </summary>
public enum OrientationSessionStatus
{
    Draft = 1,
    Published = 2,
    EnrollmentOpen = 3,
    EnrollmentClosed = 4,
    InProgress = 5,
    Completed = 6,
    Cancelled = 7,
    Postponed = 8
}

/// <summary>
/// Lifecycle state of a single orientation enrollment record.
/// </summary>
public enum OrientationEnrollmentStatus
{
    PendingConfirmation = 1,
    Confirmed = 2,
    Waitlisted = 3,
    Active = 4,
    Completed = 5,
    Cancelled = 6,
    NoShow = 7,
    Withdrawn = 8
}

/// <summary>
/// How an orientation enrollment was initiated.
/// </summary>
public enum OrientationEnrollmentSource
{
    AutoRule = 1,
    SelfEnrollment = 2,
    HrAssigned = 3,
    ManagerAssigned = 4,

    /// <summary>
    /// The next cycle of a recurring programme, opened by the nightly sweep one period after the
    /// last completion (round 4, lane I-b). No rule creates it, so it carries no rule id; its
    /// <c>TriggerDate</c> is the completion it renews.
    /// </summary>
    Recurrence = 5
}

/// <summary>
/// Overall completion state for an orientation enrollment.
/// </summary>
public enum OrientationCompletionStatus
{
    NotStarted = 1,
    InProgress = 2,
    PendingAssessment = 3,
    PendingAcknowledgement = 4,
    Completed = 5,
    Failed = 6,
    Overdue = 7,
    Exempted = 8
}

/// <summary>
/// Consumption state of a single orientation content item.
/// </summary>
public enum OrientationContentProgressStatus
{
    NotStarted = 1,
    InProgress = 2,
    Completed = 3,
    Skipped = 4
}

/// <summary>
/// Attendance status for an instructor-led orientation session day.
/// </summary>
public enum OrientationAttendanceStatus
{
    NotRecorded = 0,
    Present = 1,
    Absent = 2,
    Excused = 3,
    Partial = 4,
    Late = 5
}

/// <summary>
/// Role of a person facilitating an orientation session.
/// </summary>
public enum OrientationFacilitatorRole
{
    Lead = 1,
    CoFacilitator = 2,
    SubjectMatterExpert = 3,
    Observer = 4
}

/// <summary>
/// What triggers automatic enrollment for an audience rule.
/// </summary>
public enum OrientationEnrollmentTrigger
{
    OnHire = 1,
    OnTransfer = 2,
    OnPromotion = 3,
    OnProgramPublish = 4,
    Scheduled = 5,
    Manual = 6
}

/// <summary>
/// Format of an orientation assessment question.
/// </summary>
public enum OrientationQuestionType
{
    SingleChoice = 1,
    MultiSelect = 2,
    TrueFalse = 3,
    FreeText = 4
}

/// <summary>
/// State of a participant's acknowledgement declaration.
/// </summary>
public enum OrientationAcknowledgementStatus
{
    Pending = 1,
    Presented = 2,
    Signed = 3,
    Declined = 4,
    Expired = 5
}

/// <summary>
/// Status of an issued orientation certificate.
/// </summary>
public enum OrientationCertificateStatus
{
    Active = 1,
    Expired = 2,
    Revoked = 3,
    Reissued = 4
}

/// <summary>
/// Type of orientation lifecycle notification.
/// </summary>
public enum OrientationNotificationType
{
    Enrollment = 1,
    Reminder = 2,
    DeadlineApproaching = 3,
    Overdue = 4,
    Completion = 5,
    CertificateIssued = 6,
    Cancellation = 7,

    /// <summary>A participant placed on a session, or a facilitator told of theirs (round 4, lane K-b).</summary>
    SessionScheduled = 8,

    /// <summary>A live session's date, time, place or link changed.</summary>
    SessionRescheduled = 9,

    /// <summary>A live session put off, with no new date yet.</summary>
    SessionPostponed = 10,

    /// <summary>An onboarding plan made: to the new hire, their coordinator and their buddy.</summary>
    OnboardingPlanAssigned = 11,

    /// <summary>An onboarding task given to a person.</summary>
    OnboardingTaskAssigned = 12
}

/// <summary>
/// How often a recurring orientation program repeats.
/// </summary>
public enum OrientationRecurrenceFrequency
{
    Monthly = 1,
    Quarterly = 2,
    SemiAnnually = 3,
    Annually = 4,
    Biennially = 5
}

#endregion

#endregion

#region Emoluments / Pay Components

/// <summary>
/// Classifies a pay component as an earning added to basic pay (Allowance) or an amount
/// subtracted from gross (Deduction). The consolidated emoluments roll-up is
/// basic + allowances − deductions.
/// </summary>
public enum PayComponentType
{
    Allowance = 0,
    Deduction = 1,
    /// <summary>
    /// A non-cash, taxable Benefit-in-Kind line: it raises taxable income (and may be pensionable)
    /// but is not paid out as cash. Sourced from the Benefits module's BIK valuations.
    /// </summary>
    BenefitInKindNotional = 2
}

/// <summary>
/// How a pay component's amount is calculated: a fixed monetary amount, or a percentage of
/// the employee's monthly basic pay.
/// </summary>
public enum PayComponentCalculationBasis
{
    FixedAmount = 0,
    PercentageOfBasic = 1
}

/// <summary>
/// How a leave type's per-day encashment rate is derived: computed from the employee's
/// emoluments (basic + linked allowances) or entered manually per leave type.
/// </summary>
public enum EncashmentRateBasis
{
    DerivedFromEmoluments = 0,
    Manual = 1
}

/// <summary>
/// What a document attached to a leave request actually IS (residue plan R-15a).
/// </summary>
/// <remarks>
/// <para><b>Why typing them matters.</b> Leave attachments were untyped — a file name and a path —
/// so nothing could ask *"is the required evidence present?"*. A gate can check that a document of
/// the right kind is attached; it cannot check that against a list of filenames, because
/// <c>scan.pdf</c> is indistinguishable from a holiday photograph.</para>
///
/// <para>⚠ <b><see cref="ExcuseDuty"/> and "medical certificate" are the same document.</b> Excuse
/// duty is the term TDC's stakeholders used and the one Ghanaian practice uses; medical certificate
/// is the generic name, and it is what <c>LeaveType.RequiresMedicalCertificate</c> calls it. Both
/// names appear deliberately: the setting reads as a policy, the attachment reads as the thing an
/// employee is holding.</para>
///
/// <para>⚠ Values are persisted. <see cref="Other"/> is 0 so every attachment that existed before
/// this enum keeps meaning exactly what it meant — an untyped supporting document — rather than
/// silently becoming a medical certificate nobody uploaded.</para>
/// </remarks>
public enum LeaveEvidenceKind
{
    /// <summary>A supporting document of no particular kind. The default, and what every pre-existing row is.</summary>
    [Description("Supporting document")]
    Other = 0,

    /// <summary>
    /// A medical certificate excusing the employee from duty — "excuse duty". What a leave type
    /// with <c>RequiresMedicalCertificate</c> demands once the self-certification period is passed.
    /// </summary>
    [Description("Excuse duty (medical certificate)")]
    ExcuseDuty = 1,

    /// <summary>
    /// A medical board's recommendation, required once cumulative sick leave passes the leave
    /// type's board threshold. ⚠ The board itself is a Medical-module record; this is the document
    /// the leave request carries to show one has sat.
    /// </summary>
    [Description("Medical board recommendation")]
    MedicalBoardRecommendation = 2
}

#endregion

#region Employee Profile Change Requests (area 25 slice 12 — decision D6)

/// <summary>
/// Where a personal-data change request stands.
/// </summary>
/// <remarks>
/// There is no separate "Applied" state: approving a request APPLIES it in the same
/// transaction, because a request approved but not applied is a promise the employee cannot
/// see the result of. <see cref="EmployeeProfileChangeRequest.AppliedAt"/> records when that
/// happened, and the items keep what actually landed.
/// </remarks>
public enum ProfileChangeRequestStatus
{
    /// <summary>Filed by the employee and waiting on HR.</summary>
    Pending = 1,

    /// <summary>HR agreed; the values were written onto the employee record.</summary>
    Approved = 2,

    /// <summary>HR refused, with a reason the employee reads back.</summary>
    Rejected = 3,

    /// <summary>The employee withdrew it before HR answered.</summary>
    Cancelled = 4
}

/// <summary>
/// The fields an employee may ask to have changed. Deliberately an ENUM, not a free string:
/// the applier switches on it, so a field that is not named here cannot be written by this
/// path at all.
/// </summary>
/// <remarks>
/// <para>What is absent matters as much as what is present. Employment placement
/// (<c>PositionId</c>, org unit, <c>ManagerId</c>), money (<c>Salary</c>, the payroll
/// switches), identity assigned by the employer (<c>EmployeeNumber</c>, <c>BadgeNumber</c>)
/// and lifecycle dates are **not** requestable — they are HR/payroll decisions, not personal
/// data corrections, and an employee asking to change their own salary is not a workflow
/// anybody wants.</para>
/// <para>The low-risk contact fields (mobile, telephone, business number, extension) are
/// absent too, for the opposite reason: the employee edits those directly, so routing them
/// through an approval queue would only teach people that HR approval is noise.</para>
/// </remarks>
public enum EmployeeProfileField
{
    // ── Identity ──────────────────────────────────────────────────────────
    FirstName = 1,
    MiddleName = 2,
    LastName = 3,
    Title = 4,
    DateOfBirth = 5,
    Gender = 6,
    MaritalStatus = 7,

    /// <summary>Login-adjacent and tenant-unique — never a direct edit.</summary>
    EmailAddress = 8,

    // ── Address ───────────────────────────────────────────────────────────
    Address = 20,
    City = 21,
    State = 22,
    PostalCode = 23,
    DigitalAddress = 24,
    CountryId = 25,

    // ── Statutory ─────────────────────────────────────────────────────────
    SocialSecurityNumber = 40,
    TINNumber = 41,
    TaxNumber = 42,

    // ── Bank account (targets the request's BankDetailId) ─────────────────
    BankName = 60,
    BankBranchName = 61,
    BankAccountNumber = 62,
    BankAccountName = 63,
    BankAccountType = 64,
    MobileMoneyNumber = 65
}

#endregion

#region HR Letter Requests (area 25 slice 12b — decision D7/D10)

/// <summary>
/// The letters an employee may ask HR for. Each has a built-in template in
/// <c>HrLettersEmailCatalog</c>, so a tenant that has never opened the template editor can
/// still issue one.
/// </summary>
public enum HrLetterType
{
    /// <summary>"X has been employed by us since Y as Z" — the everyday request.</summary>
    EmploymentConfirmation = 1,

    /// <summary>An introduction addressed to a named third party.</summary>
    IntroductionLetter = 2,

    /// <summary>A certificate of service, usually wanted on or after leaving.</summary>
    ServiceCertificate = 3,

    /// <summary>
    /// Employment confirmation that also states the salary — for a bank or a landlord.
    /// ⚠ Only HR can issue it, and the figure comes from the employee record; the employee's
    /// own profile deliberately never shows salary (slice 12a), so this letter is the one
    /// sanctioned path by which they receive it, on purpose and for a stated reason.
    /// </summary>
    SalaryConfirmation = 4
}

/// <summary>Where a letter request stands.</summary>
public enum HrLetterRequestStatus
{
    /// <summary>Asked for, waiting on HR.</summary>
    Pending = 1,

    /// <summary>HR issued it — either generated from the template or uploaded as a signed scan.</summary>
    Issued = 2,

    /// <summary>HR refused, with a reason the employee reads back.</summary>
    Rejected = 3,

    /// <summary>The employee withdrew it before HR answered.</summary>
    Cancelled = 4
}

#endregion

#region HR Audience Targeting (area 25 slice 12c)

/// <summary>
/// How a broadcast picks its recipients. Shared by announcements (slice 12c) and, when it is
/// built, the policy library (slice 12d).
/// </summary>
/// <remarks>
/// <para><b>Where an employee sits is <c>OrganizationUnit</c> + <c>OrganizationLevel</c>, and
/// nothing else.</b> The org structure here is generic: a tenant names its own tiers, so what
/// one client calls a department another calls a section, a division or a directorate — the
/// LEVEL names the tier and the UNIT is the actual box on the chart. There is deliberately no
/// <c>Department</c> target: <c>Employee.DepartmentId</c> is a parallel legacy column, and
/// measured on live data it is both coarser and less complete than the unit tree (7,440 of
/// 7,954 employees carry it against 7,930 for the unit; 7 departments against 48 units; and
/// only 24 people have a department without a unit). Offering both would let a sender pick the
/// axis that quietly reaches a different population than the one they meant.</para>
///
/// <para><c>OrganizationUnit</c> includes CHILD units: announcing something to "Operations" and
/// having it miss every team inside Operations is never what the sender meant.</para>
///
/// <para><c>OrientationAudienceRule</c> models the same idea and, until round 4 lane I, had no
/// resolver anywhere — its rules were stored and never expanded. They now use this enum for their
/// target and come through <c>IHrAudienceResolver</c>, as this note used to ask; the orientation-
/// only notions (new hires, management, contractors) became <c>OrientationAudiencePopulation</c>,
/// layered on top rather than added here.</para>
/// </remarks>
public enum HrAudienceTargetType
{
    /// <summary>Everyone active in the tenant. Needs no target id.</summary>
    AllEmployees = 1,

    /// <summary>A unit and everything beneath it — the placement axis.</summary>
    OrganizationUnit = 2,

    /// <summary>A tier of the org chart, whatever this tenant calls it.</summary>
    OrganizationLevel = 3,

    /// <summary>A job, wherever it sits.</summary>
    Position = 4,

    /// <summary>A physical site — orthogonal to the org chart, which is why it is its own axis.</summary>
    Location = 5,

    /// <summary>One named person — mostly useful as an exclusion.</summary>
    Employee = 6
}

#endregion

#region HR Announcements (area 25 slice 12c — decision D7)

public enum HrAnnouncementCategory
{
    General = 1,
    Policy = 2,
    Benefits = 3,
    Safety = 4,
    Event = 5,

    /// <summary>Shown first and styled to interrupt. Use sparingly, or it stops working.</summary>
    Urgent = 6
}

public enum HrAnnouncementStatus
{
    /// <summary>Being written. Visible to no employee.</summary>
    Draft = 1,

    /// <summary>Live for its audience, subject to its dates.</summary>
    Published = 2,

    /// <summary>Taken down. Kept, because what was announced and when is a record.</summary>
    Archived = 3
}

#endregion

#region HR Policy Library + Acknowledgements (area 25 slice 12d — decision D7)

public enum HrPolicyCategory
{
    General = 1,
    CodeOfConduct = 2,
    HumanResources = 3,
    HealthAndSafety = 4,
    Finance = 5,
    InformationTechnology = 6
}

/// <summary>
/// Where a policy stands. A superseded version is <see cref="Archived"/> rather than deleted —
/// the acknowledgements against it are evidence of what somebody agreed to, and they are
/// meaningless without the version they agreed to.
/// </summary>
public enum HrPolicyStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3
}

/// <summary>
/// What an employee did when asked to acknowledge a policy.
/// </summary>
/// <remarks>
/// There is deliberately no <c>Pending</c> member. Acknowledgement rows are created only when
/// somebody actually acts, so "pending" is the ABSENCE of a row, not a state in it — see
/// <c>HrPolicyAcknowledgement</c>'s remarks for why the roster is computed rather than
/// pre-seeded.
/// </remarks>
public enum HrPolicyAcknowledgementOutcome
{
    /// <summary>They read it and agreed.</summary>
    Signed = 1,

    /// <summary>They read it and refused, with a reason. A real outcome, not a failure.</summary>
    Declined = 2
}

/// <summary>
/// Which instrument of authority a stored company image is.
/// </summary>
/// <remarks>
/// ⚠ One table, two kinds, because they are governed identically: both are what makes a generated
/// document look authentic, both must be versioned rather than overwritten, and both are replaced
/// by the same restricted act. Two tables would duplicate that governance and let it drift.
/// </remarks>
public enum CompanySealAssetKind
{
    /// <summary>The company seal or stamp.</summary>
    Seal = 1,

    /// <summary>The authorised signatory's signature image.</summary>
    Signature = 2
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════════
//  Demo feedback round 2, lane C2 — the certification model (plan § 6.3)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>What kind of credential a catalogue row is.</summary>
public enum CertificationKind
{
    Certification = 0,
    Licence = 1,
    Permit = 2,
    Registration = 3
}

/// <summary>
/// The computed state of a credential a person holds. Only <c>Revoked</c> is stored; the rest fall
/// out of the expiry date and the lead days at read time.
/// </summary>
public enum EmployeeCertificationStatus
{
    Valid = 0,
    ExpiringSoon = 1,
    Expired = 2,
    Revoked = 3
}

/// <summary>What a salary change request changes (round 3, lane S).</summary>
public enum SalaryChangeKind
{
    /// <summary>A new grade / level / notch for somebody already paid on the scale.</summary>
    Placement = 1,
    /// <summary>A new agreed figure for somebody paid off the scale.</summary>
    NegotiatedAmount = 2,
    /// <summary>Scale ↔ negotiated, carrying whichever figure the new basis needs.</summary>
    PayBasisSwitch = 3
}

/// <summary>Lifecycle of a salary change request. Recall returns to Draft.</summary>
public enum SalaryChangeRequestStatus
{
    Draft = 1,
    PendingApproval = 2,
    /// <summary>Decided, HR's half not yet through (see the request's ApplyFailure).</summary>
    Approved = 3,
    Rejected = 4,
    Applied = 5,
    /// <summary>HR's half written; payroll's monthly basic could not be — a retry re-runs only that.</summary>
    AwaitingPayrollEntry = 6
}

/// <summary>
/// Who is asking a pay door to write (round 3, lane S). <c>Direct</c> is a caller at the door —
/// refused when the tenant requires approval; <c>Approved</c> is a record the engine has already
/// approved (a salary change request, a staff movement), which the gate lets through.
/// </summary>
public enum SalaryChangeAuthority
{
    Direct = 0,
    Approved = 1
}

/// <summary>
/// What a file on a union's record is (round 3, lane U; register row U-2; decision D-17's cousin).
/// A <see cref="CollectiveAgreement"/> row carries the agreement it is the signed copy of.
/// </summary>
public enum UnionDocumentKind
{
    [Description("Collective agreement")]
    CollectiveAgreement = 1,

    [Description("Constitution")]
    Constitution = 2,

    [Description("Correspondence")]
    Correspondence = 3,

    [Description("Membership list")]
    MembershipList = 4,

    [Description("Other")]
    Other = 9
}

/// <summary>
/// The axis a disability type is grouped on (round 3, lane P2; register row E-5). Grouping, not a
/// medical classification: it is what a dropdown is sectioned by and what a headcount report counts.
/// </summary>
public enum DisabilityCategory
{
    [Description("Physical / mobility")]
    Physical = 1,

    [Description("Visual")]
    Visual = 2,

    [Description("Hearing")]
    Hearing = 3,

    [Description("Speech")]
    Speech = 4,

    [Description("Intellectual / learning")]
    Intellectual = 5,

    [Description("Psychosocial / mental health")]
    Psychosocial = 6,

    [Description("Neurological")]
    Neurological = 7,

    [Description("Chronic health condition")]
    ChronicHealth = 8,

    [Description("Multiple")]
    Multiple = 9,

    [Description("Other")]
    Other = 99
}

/// <summary>
/// Whether an employee is available to Maintenance as a technician because of their position, or
/// because HR said so for this person (round 4, lane O).
/// </summary>
/// <remarks>
/// A view over two stored columns, <c>Employee.MaintenanceAssignmentSetByHand</c> and
/// <c>Employee.CanBeAssignedToMaintenance</c>. The second is what every reader asks, HR's technician
/// door and Maintenance's work-order gates alike.
/// </remarks>
public enum MaintenanceAssignmentMode
{
    /// <summary>The position decides: available exactly when it is a technician role.</summary>
    [Description("Follow the position")]
    FollowPosition = 0,

    /// <summary>Available whatever the position says, e.g. someone seconded in from another post.</summary>
    [Description("Include, set by hand")]
    Include = 1,

    /// <summary>Not available although the position is a technician role, e.g. long-term light duties.</summary>
    [Description("Exclude, set by hand")]
    Exclude = 2
}
