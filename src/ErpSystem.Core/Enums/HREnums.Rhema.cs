// RHEMA-only HR enums — kept OUT of HREnums.cs on purpose.
//
// ⚠ THE OTHER HALF OF THIS ARRANGEMENT: members RHEMA has added to enums that HRApi DOES own.
// Those cannot live here — a C# enum cannot be declared in two files — so they sit in HREnums.cs
// and are listed below. **After any HRApi sync, check each one is still present**; a lost member
// compiles as an error at its usage site, which is the good case, but a re-used numeric value would
// not. Each is marked with a doc comment naming the area that added it.
//
//   AppraisalStatus.Open = 0                 the retained RHEMA appraisal model
//   ProbationStatus.ConfirmationApproved = 5 area 15b — the gap between the authority approving
//                                            confirmation and HR issuing the letter
//   AssignmentStatus.Transferred = 6         area 16 slice 4 — custody passed to another employee
//                                            by an approved transfer, rather than handed back
//
// HREnums.cs is owned by HRApi and is overwritten byte-for-byte on every HRApi sync, so any
// RHEMA-specific enum defined there would be lost. This file is the sync-safe home for the two
// kinds of RHEMA enum that HRApi's HREnums.cs does not define:
//   1. HR-port overrides created for this port (HR-prefixed to avoid cross-module name collisions):
//      HRAssetCondition, HRAssetTransferType, HRAssetTransferStatus, HRAssetRequisitionPriority,
//      HRSchedulePeriodType  — referenced by the asset/schedule deltas (see
//      scripts/hr-port-reapply-deltas.py).
//   2. Pre-existing RHEMA platform enums that used to live in RHEMA's old HREnums.cs and are still
//      referenced across the solution (some by non-HR modules, e.g. DocumentType).
//
// History: 36 enums that became dead code after the Performance model was replaced wholesale were
// pruned (2026-07-20). Re-run the type-usage audit before adding or removing anything here.
using System.ComponentModel;

namespace ErpSystem.Core.Enums;

public enum AppealStatus
{
    [Description("Submitted")]
    Submitted = 1,

    [Description("Under Review")]
    UnderReview = 2,

    [Description("Hearing Scheduled")]
    HearingScheduled = 3,

    [Description("Upheld")]
    Upheld = 4,

    [Description("Overturned")]
    Overturned = 5,

    [Description("Modified")]
    Modified = 6,

    [Description("Dismissed")]
    Dismissed = 7
}

public enum AttendanceStatus
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

public enum DeviceType
{
    [Description("Fingerprint Scanner")]
    FingerprintScanner = 1,

    [Description("Face Recognition")]
    FaceRecognition = 2,

    [Description("RFID Card Reader")]
    RFIDCard = 3,

    [Description("Mobile App")]
    MobileApp = 4,

    [Description("Web Portal")]
    WebPortal = 5
}

public enum DocumentType
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

public enum HRAssetCondition
{
    [Description("Excellent")]
    Excellent = 1,

    [Description("Good")]
    Good = 2,

    [Description("Fair")]
    Fair = 3,

    [Description("Poor")]
    Poor = 4,

    [Description("Non-Functional")]
    NonFunctional = 5
}

/// <summary>
/// Where an entry in the HR asset register came from — AST-11.
/// </summary>
/// <remarks>
/// <para>The change document asks for "some kind of flag or detail to indicate that it came from
/// that module, aside any assets HR might possibly create at their end". This is that flag, and it
/// is not cosmetic: it decides who owns which fields. On a <see cref="FixedAssetsModule"/> asset the
/// purchase figures and the disposal belong to Finance and HR refuses to edit them; on an
/// <see cref="HrCreated"/> one HR owns everything.</para>
///
/// <para>Values start at 1 so that a row written before this column existed cannot read as a
/// meaningful source by accident. The database default is <see cref="HrCreated"/>, which is the
/// truthful description of every asset that existed before the link did.</para>
/// </remarks>
public enum AssetSource
{
    /// <summary>Registered by HR directly — uniforms, phones, tools, anything below the capitalisation threshold.</summary>
    [Description("Created in HR")]
    HrCreated = 1,

    /// <summary>Picked from the Finance fixed-asset register and linked to it. Finance owns its money.</summary>
    [Description("From Fixed Assets")]
    FixedAssetsModule = 2
}

public enum HRAssetRequisitionPriority
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

public enum HRAssetTransferStatus
{
    /// <summary>
    /// Raised but not yet sent for approval — area 16, slice 3b.
    /// </summary>
    /// <remarks>
    /// The enum had no such member because nothing could create a transfer at all (D-l), so every
    /// transfer was born <c>Pending</c> and "pending" meant both "nobody has looked at this" and
    /// "this is out for approval". Putting the approval on the workflow engine needs those to be
    /// two different states: a draft is the initiator's to edit or withdraw, a pending one is the
    /// engine's and must be recalled first. Zero is safe as the default for the unset case, which
    /// is the whole point of choosing it.
    /// </remarks>
    [Description("Draft")]
    Draft = 0,

    [Description("Pending")]
    Pending = 1,

    [Description("Approved")]
    Approved = 2,

    [Description("In Transit")]
    InTransit = 3,

    [Description("Completed")]
    Completed = 4,

    [Description("Rejected")]
    Rejected = 5,

    [Description("Cancelled")]
    Cancelled = 6
}

public enum HRAssetTransferType
{
    [Description("Employee to Employee")]
    EmployeeToEmployee = 1,

    [Description("Location to Location")]
    LocationToLocation = 2,

    [Description("Department to Department")]
    DepartmentToDepartment = 3,

    [Description("Unit to Unit")]
    UnitToUnit = 4
}

public enum HRSchedulePeriodType
{
    [Description("Quarter")]
    Quarter = 1,

    [Description("Month")]
    Month = 2,

    [Description("Semi-Annual")]
    SemiAnnual = 3
}

public enum InspectionType
{
    [Description("Routine Inspection")]
    Routine = 1,

    [Description("Compliance Inspection")]
    Compliance = 2,

    [Description("Follow-up Inspection")]
    FollowUp = 3,

    [Description("Special Inspection")]
    Special = 4
}

public enum InterviewStatus
{
    [Description("Scheduled")]
    Scheduled = 1,

    [Description("Confirmed")]
    Confirmed = 2,

    [Description("Rescheduled")]
    Rescheduled = 3,

    [Description("In Progress")]
    InProgress = 4,

    [Description("Completed")]
    Completed = 5,

    [Description("No Show")]
    NoShow = 6,

    [Description("Cancelled")]
    Cancelled = 7
}

public enum LogType
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

public enum MovementType
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

public enum VacancyStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Pending Approval")]
    PendingApproval = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Published")]
    Published = 4,

    [Description("Closed for Applications")]
    ClosedForApplications = 5,

    [Description("Shortlisting")]
    Shortlisting = 6,

    [Description("Interviewing")]
    Interviewing = 7,

    [Description("Offer Stage")]
    OfferStage = 8,

    [Description("Filled")]
    Filled = 9,

    [Description("Cancelled")]
    Cancelled = 10,

    [Description("On Hold")]
    OnHold = 11
}

public enum WarningType
{
    [Description("Verbal Warning")]
    Verbal = 1,

    [Description("First Written Warning")]
    FirstWritten = 2,

    [Description("Second Written Warning")]
    SecondWritten = 3,

    [Description("Final Written Warning")]
    FinalWritten = 4
}

// ─────────────────────────────────────────────────────────────────────────────
// AREA 16 SLICE 7 — damage, loss, and the surcharge (AST-3, defect D-d, decision D9)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// How a custody ended badly — what a "report an incident" call is declaring.
/// </summary>
/// <remarks>
/// Separate from <see cref="AssignmentStatus"/> because the caller states an OUTCOME and the
/// service decides what that means for the assignment and for the asset. `AssignmentStatus.Lost`
/// and `.Damaged` existed from the port with <b>no writer anywhere</b>: nothing in the codebase
/// could ever set them, so an asset that never came back had no way of being recorded as such and
/// the clearest surcharge case of all was unreachable.
/// </remarks>
public enum AssetIncidentOutcome
{
    [Description("Lost or Stolen")]
    Lost = 1,

    [Description("Damaged Beyond Return")]
    Damaged = 2
}

/// <summary>Why an employee is being charged for a company asset — AST-3.</summary>
public enum AssetSurchargeReason
{
    [Description("Damage")]
    Damage = 1,

    [Description("Loss or Theft")]
    Loss = 2,

    [Description("Not Returned")]
    NotReturned = 3,

    [Description("Other")]
    Other = 4
}

/// <summary>
/// Where a surcharge has got to.
/// </summary>
/// <remarks>
/// <para>The employee's answer is deliberately <b>not</b> a status. Accepting and disputing lead to
/// the same next step — the charge goes for approval either way — and they differ only in what the
/// approver is looking at. Modelling them as states would imply a dispute stops the employer, which
/// is not true and would be a promise the code could not keep; it obliges them to consider it. The
/// answer therefore lives in <see cref="AssetSurchargeEmployeeResponse"/> on the record.</para>
///
/// <para><c>Recovering</c> is distinct from <c>Approved</c> on purpose: "decided, nothing collected
/// yet" and "part-collected" are different facts to a finance desk and to an exit settlement.</para>
/// </remarks>
public enum AssetSurchargeStatus
{
    [Description("Draft")]
    Draft = 0,

    /// <summary>
    /// The charge has been put to the employee. They may or may not have answered yet — that is
    /// <see cref="AssetSurchargeEmployeeResponse"/>'s question, not this one.
    /// </summary>
    /// <remarks>
    /// Named for where the record IS rather than for what is being waited on. "AwaitingResponse"
    /// would read as a lie the moment the employee answered, on precisely the record where the
    /// employee's answer is the thing that matters.
    /// </remarks>
    [Description("With Employee")]
    WithEmployee = 1,

    [Description("Submitted")]
    Submitted = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Rejected")]
    Rejected = 4,

    [Description("Recovering")]
    Recovering = 5,

    [Description("Recovered")]
    Recovered = 6,

    [Description("Waived")]
    Waived = 7,

    [Description("Cancelled")]
    Cancelled = 8
}

/// <summary>What the employee said when the charge was put to them — decision D9.</summary>
public enum AssetSurchargeEmployeeResponse
{
    [Description("Not Yet Given")]
    NotYetGiven = 0,

    [Description("Accepted")]
    Accepted = 1,

    [Description("Disputed")]
    Disputed = 2
}

/// <summary>
/// How the money is to come back.
/// </summary>
/// <remarks>
/// A <b>declaration</b>, not a mechanism. HR states what should be recovered and how; payroll runs
/// the deduction and the exit settlement applies the balance. Nothing here computes a payslip —
/// see the payroll ownership boundary and decision D2, which draws the same line for rental.
/// </remarks>
public enum AssetSurchargeRecoveryMethod
{
    [Description("Payroll Deduction")]
    PayrollDeduction = 1,

    [Description("Direct Payment")]
    DirectPayment = 2,

    [Description("Deduct at Exit")]
    ExitSettlement = 3
}
