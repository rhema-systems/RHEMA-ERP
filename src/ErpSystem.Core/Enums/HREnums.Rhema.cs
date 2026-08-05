// RHEMA-only HR enums — kept OUT of HREnums.cs on purpose.
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



