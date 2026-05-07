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

public enum WorkSchedule
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

/// <summary>
/// Types of employee emergency contact
/// </summary>
public enum EmergencyContactType
{
    EmergencyContact = 1,
    NextOfKin = 2,
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
    Annually = 5
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

public enum PositionChangeReason
{
    InitialAssignment = 0,

    Promotion = 1,
    
    Demotion = 2,
    
    Transfer = 3,
    
    Restructure = 4,

    Termination = 5,
    
    Other = 6
}

public enum EmployeeContactType
{
    Home = 1,
    Postal = 2,
    Temporary = 3,
    Other = 4
}

public enum EmployeeBankAccountType
{
    Current = 1,
    
    Savings = 2,
    
    MobileMoney = 3
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

public enum BenefitRelation
{
    All = 1,

    Spouse = 2,

    Child = 3,

    Son = 4,

    Daughter = 5,

    Mother = 6,

    Father = 7,

    Brother = 8,

    Sister = 9
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

public enum BenefitLimitPeriod
{
    Monthly = 1,

    Annual = 2,
    
    Lifetime = 3
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
    Closed = 5
}

#endregion Staff Leave

#region Performance Appraisal

public enum AppraisalType
{
    Quarterly = 1,
    MidYear = 2,
    Annual = 3,
    OneOff = 4
}

public enum AppraisalStatus
{
    Open = 1,
    InProgress = 2,
    Submitted = 3,
    Reviewed = 4,
    Closed = 5
}

public enum EvaluatorRole
{
    Self = 1,
    Manager = 2,
    Peer = 3,
    HR = 4
}

public enum CriteriaType
{
    Competency = 1,
    KPI = 2
}

public enum MeasurementType
{
    NumericAbsolute = 1,
    PercentageTarget = 2,
    Boolean = 3,
    Range = 4
}

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
    Cancelled = 5
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

public enum VacancyType
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

public enum VacancyClosureReason
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
    Other = 6
}

public enum ApplicationStatus
{
    [Description("Submitted")]
    Submitted = 1,

    [Description("Under Review")]
    UnderReview = 2,

    [Description("Shortlisted")]
    Shortlisted = 3,

    [Description("Interview Scheduled")]
    InterviewScheduled = 4,

    [Description("Interview Completed")]
    InterviewCompleted = 5,

    [Description("Assessment Pending")]
    AssessmentPending = 6,

    [Description("Reference Check")]
    ReferenceCheck = 7,

    [Description("Offer Extended")]
    OfferExtended = 8,

    [Description("Offer Accepted")]
    OfferAccepted = 9,

    [Description("Offer Declined")]
    OfferDeclined = 10,

    [Description("Rejected")]
    Rejected = 11,

    [Description("Withdrawn")]
    Withdrawn = 12,

    [Description("Hired")]
    Hired = 13
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
    Other = 10
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

public enum InterviewType
{
    [Description("Phone Screening")]
    PhoneScreening = 1,

    [Description("Video Interview")]
    VideoInterview = 2,

    [Description("In-Person Individual")]
    InPersonIndividual = 3,

    [Description("Panel Interview")]
    PanelInterview = 4,

    [Description("Technical Assessment")]
    TechnicalAssessment = 5,

    [Description("Group Interview")]
    GroupInterview = 6,

    [Description("Final Interview")]
    FinalInterview = 7
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

public enum InterviewOutcome
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
    Rejected = 6
}

public enum PanelistRole
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

public enum InterviewRecommendation
{
    [Description("Strong Hire")]
    StrongHire = 1,

    [Description("Hire")]
    Hire = 2,

    [Description("Maybe")]
    Maybe = 3,

    [Description("No Hire")]
    NoHire = 4,

    [Description("Strong No Hire")]
    StrongNoHire = 5
}

public enum ShortlistingCriteriaType
{
    [Description("Qualification")]
    Qualification = 1,

    [Description("Experience")]
    Experience = 2,

    [Description("Skills")]
    Skills = 3,

    [Description("Certification")]
    Certification = 4,

    [Description("Language")]
    Language = 5
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

#endregion Recruitment

#region Disciplinary Actions

public enum OffenseCategory
{
    [Description("Attendance/Punctuality")]
    Attendance = 1,

    [Description("Performance")]
    Performance = 2,

    [Description("Misconduct")]
    Misconduct = 3,

    [Description("Insubordination")]
    Insubordination = 4,

    [Description("Policy Violation")]
    PolicyViolation = 5,

    [Description("Harassment")]
    Harassment = 6,

    [Description("Theft/Fraud")]
    TheftFraud = 7,

    [Description("Safety Violation")]
    SafetyViolation = 8,

    [Description("Substance Abuse")]
    SubstanceAbuse = 9,

    [Description("Confidentiality Breach")]
    ConfidentialityBreach = 10,

    [Description("Other")]
    Other = 11
}

public enum OffenseSeverity
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
    [Description("Reported")]
    Reported = 1,

    [Description("Under Investigation")]
    UnderInvestigation = 2,

    [Description("Investigation Complete")]
    InvestigationComplete = 3,

    [Description("Hearing Scheduled")]
    HearingScheduled = 4,

    [Description("Hearing Completed")]
    HearingCompleted = 5,

    [Description("Decision Pending")]
    DecisionPending = 6,

    [Description("Action Taken")]
    ActionTaken = 7,

    [Description("Under Appeal")]
    UnderAppeal = 8,

    [Description("Appeal Completed")]
    AppealCompleted = 9,

    [Description("Closed")]
    Closed = 10,

    [Description("Dismissed")]
    Dismissed = 11
}

public enum DisciplinaryActionType
{
    [Description("Verbal Warning")]
    VerbalWarning = 1,

    [Description("Written Warning")]
    WrittenWarning = 2,

    [Description("Final Written Warning")]
    FinalWrittenWarning = 3,

    [Description("Suspension")]
    Suspension = 4,

    [Description("Demotion")]
    Demotion = 5,

    [Description("Fine/Penalty")]
    Fine = 6,

    [Description("Termination")]
    Termination = 7,

    [Description("Training/Counseling")]
    TrainingCounseling = 8,

    [Description("No Action")]
    NoAction = 9
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

public enum DocumentCategory
{
    [Description("Evidence")]
    Evidence = 1,

    [Description("Witness Statement")]
    WitnessStatement = 2,

    [Description("Investigation Report")]
    InvestigationReport = 3,

    [Description("Hearing Minutes")]
    HearingMinutes = 4,

    [Description("Decision Letter")]
    DecisionLetter = 5,

    [Description("Appeal Document")]
    AppealDocument = 6
}

#endregion Disciplinary Actions

#region Training Management

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

public enum TrainingType
{
    [Description("Internal Training")]
    Internal = 1,

    [Description("External Training")]
    External = 2,

    [Description("Online/E-Learning")]
    Online = 3,

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

public enum TrainingLevel
{
    [Description("Beginner")]
    Beginner = 1,

    [Description("Intermediate")]
    Intermediate = 2,

    [Description("Advanced")]
    Advanced = 3,

    [Description("Expert")]
    Expert = 4
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
    SpecialRecognition = 8
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

    [Description("Deferred")]
    Deferred = 6
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
    [Description("Submitted")]
    Submitted = 1,

    [Description("Under Review")]
    UnderReview = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Rejected")]
    Rejected = 4,

    [Description("Award Granted")]
    AwardGranted = 5
}

#endregion Staff Awards

#region Staff Accidents and Safety

public enum AccidentType
{
    [Description("Injury")]
    Injury = 1,

    [Description("Near Miss")]
    NearMiss = 2,

    [Description("Property Damage")]
    PropertyDamage = 3,

    [Description("Vehicle Accident")]
    VehicleAccident = 4,

    [Description("Fire")]
    Fire = 5,

    [Description("Chemical Spill")]
    ChemicalSpill = 6,

    [Description("Equipment Failure")]
    EquipmentFailure = 7
}

public enum AccidentSeverity
{
    [Description("Minor - First Aid Only")]
    Minor = 1,

    [Description("Moderate - Medical Treatment")]
    Moderate = 2,

    [Description("Serious - Hospitalization")]
    Serious = 3,

    [Description("Critical - Life Threatening")]
    Critical = 4,

    [Description("Fatality")]
    Fatality = 5
}

public enum InjuryType
{
    [Description("Cut/Laceration")]
    Cut = 1,

    [Description("Bruise/Contusion")]
    Bruise = 2,

    [Description("Sprain/Strain")]
    Sprain = 3,

    [Description("Fracture/Break")]
    Fracture = 4,

    [Description("Burn")]
    Burn = 5,

    [Description("Head Injury")]
    HeadInjury = 6,

    [Description("Back Injury")]
    BackInjury = 7,

    [Description("Eye Injury")]
    EyeInjury = 8,

    [Description("Chemical Exposure")]
    ChemicalExposure = 9,

    [Description("Other")]
    Other = 10
}

public enum AccidentStatus
{
    [Description("Reported")]
    Reported = 1,

    [Description("Under Investigation")]
    UnderInvestigation = 2,

    [Description("Investigation Complete")]
    InvestigationComplete = 3,

    [Description("Corrective Actions In Progress")]
    CorrectiveActionsInProgress = 4,

    [Description("Closed")]
    Closed = 5
}

public enum AccidentDocumentType
{
    [Description("Medical Report")]
    MedicalReport = 1,

    [Description("Photos")]
    Photos = 2,

    [Description("Police Report")]
    PoliceReport = 3,

    [Description("Witness Statement")]
    WitnessStatement = 4,

    [Description("Investigation Report")]
    InvestigationReport = 5
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

public enum InspectionStatus
{
    [Description("Scheduled")]
    Scheduled = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Corrective Actions Required")]
    CorrectiveActionsRequired = 4,

    [Description("Closed")]
    Closed = 5
}

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

#region Medical Expenses

public enum MedicalExpenseType
{
    [Description("Consultation")]
    Consultation = 1,

    [Description("Medication")]
    Medication = 2,

    [Description("Laboratory Tests")]
    LabTests = 3,

    [Description("X-Ray/Imaging")]
    Imaging = 4,

    [Description("Surgery")]
    Surgery = 5,

    [Description("Hospitalization")]
    Hospitalization = 6,

    [Description("Dental")]
    Dental = 7,

    [Description("Optical")]
    Optical = 8,

    [Description("Physiotherapy")]
    Physiotherapy = 9,

    [Description("Emergency Treatment")]
    Emergency = 10,

    [Description("Other")]
    Other = 11
}

public enum ClaimStatus
{
    [Description("Submitted")]
    Submitted = 1,

    [Description("Supervisor Review")]
    SupervisorReview = 2,

    [Description("HR Review")]
    HrReview = 3,

    [Description("Finance Review")]
    FinanceReview = 4,

    [Description("Approved")]
    Approved = 5,

    [Description("Rejected")]
    Rejected = 6,

    [Description("Payment Processing")]
    PaymentProcessing = 7,

    [Description("Paid")]
    Paid = 8,

    [Description("Partially Approved")]
    PartiallyApproved = 9,

    [Description("Additional Info Required")]
    AdditionalInfoRequired = 10
}

public enum MedicalItemType
{
    [Description("Consultation Fee")]
    ConsultationFee = 1,

    [Description("Laboratory Test")]
    LabTest = 2,

    [Description("Medication")]
    Medication = 3,

    [Description("Procedure")]
    Procedure = 4,

    [Description("Accommodation")]
    Accommodation = 5,

    [Description("Other")]
    Other = 6
}

public enum MedicalDocumentType
{
    [Description("Receipt")]
    Receipt = 1,

    [Description("Invoice")]
    Invoice = 2,

    [Description("Prescription")]
    Prescription = 3,

    [Description("Medical Report")]
    MedicalReport = 4,

    [Description("Lab Results")]
    LabResults = 5,

    [Description("Referral Letter")]
    ReferralLetter = 6
}

public enum PaymentMethod
{
    [Description("Bank Transfer")]
    BankTransfer = 1,

    [Description("Cash")]
    Cash = 2,

    [Description("Cheque")]
    Cheque = 3,

    [Description("Direct Reimbursement")]
    DirectReimbursement = 4,

    [Description("Salary Deduction Reversal")]
    SalaryDeductionReversal = 5
}

#endregion Medical Expenses

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
    Language = 6
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

public enum RelationshipType
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

#endregion Job Analysis

#region Succession Planning

public enum SuccessionPlanStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Under Review")]
    UnderReview = 2,

    [Description("Approved")]
    Approved = 3,

    [Description("Active")]
    Active = 4,

    [Description("Under Revision")]
    UnderRevision = 5,

    [Description("Completed")]
    Completed = 6
}

public enum PositionCriticality
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

    [Description("Other")]
    Other = 6
}

public enum CandidateType
{
    [Description("Internal - Ready Now")]
    InternalReady = 1,

    [Description("Internal - 1-2 Years")]
    Internal1To2Years = 2,

    [Description("Internal - 3+ Years")]
    Internal3PlusYears = 3,

    [Description("External")]
    External = 4,

    [Description("Emergency/Acting")]
    Emergency = 5
}

public enum ReadinessLevel
{
    [Description("Ready Now")]
    ReadyNow = 1,

    [Description("Ready in 1 Year")]
    ReadyIn1Year = 2,

    [Description("Ready in 2-3 Years")]
    ReadyIn2To3Years = 3,

    [Description("Not Ready")]
    NotReady = 4
}

public enum PotentialRating
{
    [Description("High Potential")]
    HighPotential = 1,

    [Description("Moderate Potential")]
    ModeratePotential = 2,

    [Description("Limited Potential")]
    LimitedPotential = 3
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
    ExternalExperience = 7
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
    Assessment = 4
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

    [Description("Future Leaders")]
    FutureLeaders = 4,

    [Description("Key Talent")]
    KeyTalent = 5
}

public enum NineBoxCategory
{
    [Description("Stars - High Performance, High Potential")]
    Stars = 1,

    [Description("High Performers - High Performance, Moderate Potential")]
    HighPerformers = 2,

    [Description("Core Players - High Performance, Low Potential")]
    CorePlayers = 3,

    [Description("High Potentials - Moderate Performance, High Potential")]
    HighPotentials = 4,

    [Description("Solid Professionals - Moderate Performance, Moderate Potential")]
    SolidProfessionals = 5,

    [Description("Solid Contributors - Moderate Performance, Low Potential")]
    SolidContributors = 6,

    [Description("Inconsistent Players - Low Performance, High Potential")]
    InconsistentPlayers = 7,

    [Description("Dilemmas - Low Performance, Moderate Potential")]
    Dilemmas = 8,

    [Description("Low Performers - Low Performance, Low Potential")]
    LowPerformers = 9
}

#endregion Succession Planning

#region Staff Requisition

public enum StaffRequisitionType
{
    [Description("New Position")]
    NewPosition = 1,

    [Description("Replacement")]
    Replacement = 2,

    [Description("Temporary/Contract")]
    Temporary = 3,

    [Description("Internship")]
    Internship = 4,

    [Description("Backfill")]
    Backfill = 5
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

    [Description("Supervisor Review")]
    SupervisorReview = 3,

    [Description("HOD Review")]
    HodReview = 4,

    [Description("HR Review")]
    HrReview = 5,

    [Description("Finance Review")]
    FinanceReview = 6,

    [Description("CEO Approval")]
    CeoApproval = 7,

    [Description("Approved")]
    Approved = 8,

    [Description("Rejected")]
    Rejected = 9,

    [Description("On Hold")]
    OnHold = 10,

    [Description("Cancelled")]
    Cancelled = 11,

    [Description("Fulfilled")]
    Fulfilled = 12
}

public enum ReplacementReason
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

public enum AssetCondition
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

    [Description("Overdue")]
    Overdue = 3,

    [Description("Lost")]
    Lost = 4,

    [Description("Damaged")]
    Damaged = 5
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

public enum AssetRequisitionStatus
{
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

public enum AssetTransferType
{
    [Description("Employee to Employee")]
    EmployeeToEmployee = 1,

    [Description("Location to Location")]
    LocationToLocation = 2,

    [Description("Department to Department")]
    DepartmentToDepartment = 3
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

public enum AssetAttributeDataType
{
    Checkbox = 1,

    Date = 2,

    Decimal = 3,

    Dropdown = 4,

    Integer = 5,

    Text = 6
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

#endregion Staff Assets

#region Career Movement

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

public enum MovementCategory
{
    [Description("Voluntary")]
    Voluntary = 1,

    [Description("Involuntary")]
    Involuntary = 2,

    [Description("Organizational Restructure")]
    OrganizationalRestructure = 3,

    [Description("Career Development")]
    CareerDevelopment = 4
}

public enum MovementStatus
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

public enum MovementAttachmentType
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

public enum ChecklistCategory
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

public enum PromotionType
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

public enum TransferReason
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

public enum DemotionReason
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

public enum SecondmentType
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

public enum ActingReason
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

public enum ActingStatus
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

public enum AllowanceCalculationMethod
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

public enum ScheduleType
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

    [Description("Night Shift")]
    Night = 3,

    [Description("Rotating Shift")]
    Rotating = 4,

    [Description("Split Shift")]
    Split = 5
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

public enum RegularizationStatus
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

public enum TaskCategory
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

public enum TaskStatus
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

public enum PeriodType
{
    [Description("Quarter")]
    Quarter = 1,

    [Description("Month")]
    Month = 2,

    [Description("Semi-Annual")]
    SemiAnnual = 3
}

#endregion Company Schedule
