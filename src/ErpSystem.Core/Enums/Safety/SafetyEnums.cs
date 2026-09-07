namespace ErpSystem.Core.Enums.Safety;

// ============================================================
//  SHE (Safety, Health & Environment) module enums.
//  All names are She-prefixed and isolated in this namespace to
//  avoid clashing with the shared ComplianceStatus enum in HREnums.cs
//  (used by the Training module).
//  Values are explicit and 1-based (0 = unset/invalid sentinel),
//  matching the rest of the codebase.
// ============================================================

// ── Incident ──────────────────────────────────────────────
public enum SheIncidentCategory
{
    Accident = 1,
    NearMiss = 2,
    DangerousOccurrence = 3,
    OccupationalIllness = 4,
    EnvironmentalIncident = 5,
    PropertyDamage = 6,
    SecurityIncident = 7,
    FireIncident = 8
}

public enum SheIncidentSeverity
{
    Negligible = 1,
    Minor = 2,
    Moderate = 3,
    Major = 4,
    Catastrophic = 5
}

public enum SheIncidentStatus
{
    Reported = 1,
    UnderReview = 2,
    InvestigationInProgress = 3,
    PendingCorrective = 4,
    PendingClosure = 5,
    Closed = 6,
    Reopened = 7
}

public enum SheInjuryClassification
{
    FirstAidCase = 1,
    MedicalTreatmentCase = 2,
    RestrictedWorkCase = 3,
    LostTimeInjury = 4,
    PermanentDisability = 5,
    Fatality = 6
}

public enum SheInvolvedPersonRole
{
    PrimaryVictim = 1,
    SecondaryVictim = 2,
    Perpetrator = 3,
    Bystander = 4,
    Responder = 5
}

public enum SheBodySide
{
    Left = 1,
    Right = 2,
    Central = 3,
    Both = 4
}

public enum SheRootCauseMethod
{
    FiveWhy = 1,
    Fishbone = 2,
    FaultTree = 3,
    SCAT = 4,
    BowTie = 5,
    ICAM = 6,
    Other = 7
}

public enum SheIncidentDocumentType
{
    Photo = 1,
    WitnessStatement = 2,
    MedicalReport = 3,
    InvestigationReport = 4,
    RegulatoryNotification = 5,
    InsuranceClaim = 6,
    CCTV = 7,
    Other = 8
}

// ── Corrective Actions ────────────────────────────────────
public enum SheCorrectiveActionCategory
{
    Engineering = 1,
    Administrative = 2,
    Behavioural = 3,
    PPE = 4,
    Maintenance = 5,
    Training = 6,
    PolicyProcedure = 7,
    Environmental = 8,
    Other = 9
}

public enum SheCorrectiveActionPriority
{
    Critical = 1,
    High = 2,
    Medium = 3,
    Low = 4
}

public enum SheCorrectiveActionStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Verified = 4,
    Overdue = 5,
    Cancelled = 6
}

// ── Hazard & Risk ─────────────────────────────────────────
public enum SheHazardCategory
{
    Physical = 1,
    Chemical = 2,
    Biological = 3,
    Ergonomic = 4,
    Psychosocial = 5,
    Electrical = 6,
    Mechanical = 7,
    FireExplosion = 8,
    SlipTripFall = 9,
    WorkingAtHeight = 10,
    ConfinedSpace = 11,
    Radiation = 12,
    Environmental = 13,
    Traffic = 14,
    Other = 15
}

public enum SheHazardStatus
{
    Identified = 1,
    UnderAssessment = 2,
    ControlsInPlace = 3,
    Monitoring = 4,
    Resolved = 5,
    Closed = 6
}

public enum SheHazardRiskLevel
{
    VeryLow = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    VeryHigh = 5,
    Critical = 6
}

public enum SheHierarchyOfControl
{
    Elimination = 1,
    Substitution = 2,
    Engineering = 3,
    Administrative = 4,
    PPE = 5
}

public enum SheControlStatus
{
    Planned = 1,
    Implemented = 2,
    Verified = 3,
    Ineffective = 4,
    Superseded = 5
}

public enum SheRiskLevel
{
    Negligible = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    Critical = 5
}

public enum SheRiskAssessmentType
{
    HIRA = 1,
    JHA = 2,
    PreTask = 3,
    COSHH = 4,
    FireRisk = 5,
    EnvironmentalImpact = 6,
    ErgoAssessment = 7,
    Other = 8
}

public enum SheRiskAssessmentStatus
{
    Draft = 1,
    PendingReview = 2,
    PendingApproval = 3,
    Approved = 4,
    Active = 5,
    Expired = 6,
    Superseded = 7,
    Withdrawn = 8
}

// ── Inspections ───────────────────────────────────────────
public enum SheInspectionType
{
    Routine = 1,
    Planned = 2,
    Unplanned = 3,
    FollowUp = 4,
    PreTask = 5,
    PostIncident = 6,
    Regulatory = 7,
    Management = 8
}

public enum SheInspectionCategory
{
    General = 1,
    FireSafety = 2,
    Construction = 3,
    Equipment = 4,
    Chemical = 5,
    Electrical = 6,
    WorkingAtHeight = 7,
    ConfinedSpace = 8,
    ManualHandling = 9,
    Environmental = 10,
    Housekeeping = 11
}

public enum SheInspectionStatus
{
    Scheduled = 1,
    InProgress = 2,
    PendingCorrectiveActions = 3,
    Completed = 4,
    Closed = 5,
    Overdue = 6
}

public enum SheComplianceStatus
{
    Compliant = 1,
    NonCompliant = 2,
    PartiallyCompliant = 3,
    NotApplicable = 4,
    NotAssessed = 5
}

public enum SheInspectionResult
{
    Pass = 1,
    ConditionalPass = 2,
    Fail = 3,
    NeedsFollowUp = 4
}

// ── Inspection checklist builder (docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md) ──

/// <summary>Template lifecycle. Structure is editable only in Draft; Published rows are what inspections pin.</summary>
public enum SheChecklistStatus
{
    Draft = 1,
    Published = 2,
    Retired = 3
}

/// <summary>How a run against the template is summarised.</summary>
public enum SheChecklistScoringMode
{
    /// <summary>No score and no outcome block — items and findings only.</summary>
    None = 1,
    /// <summary>Compliant ÷ applicable as a percentage, mapped onto the template's outcome bands.</summary>
    CompliancePercentage = 2,
    /// <summary>The inspector picks one of the template's outcomes (e.g. Excellent … Unsatisfactory); no percentage is computed.</summary>
    QualitativeRating = 3
}

/// <summary>Standard sections score C / NC / NA. Critical sections are Yes / No, and any Yes disqualifies.</summary>
public enum SheChecklistSectionKind
{
    Standard = 1,
    Critical = 2
}

/// <summary>The closed set of header-field types a template may declare ("Department" on paper is OrganizationUnit here).</summary>
public enum SheChecklistFieldType
{
    Text = 1,
    LongText = 2,
    Number = 3,
    Date = 4,
    Time = 5,
    YesNo = 6,
    Choice = 7,
    Employee = 8,
    Location = 9,
    OrganizationUnit = 10
}

/// <summary>System users sign in-app as the logged-in employee; external parties (vendor, operator) are a typed name and date.</summary>
public enum SheChecklistSignatoryKind
{
    SystemUser = 1,
    External = 2
}

// ── Permit-to-Work ────────────────────────────────────────
public enum ShePermitType
{
    HotWork = 1,
    ConfinedSpaceEntry = 2,
    WorkingAtHeight = 3,
    Excavation = 4,
    ElectricalIsolation = 5,
    ChemicalHandling = 6,
    CriticalLift = 7,
    Demolition = 8,
    General = 9,
    RoadClosure = 10 // FRD §6.2 — the one spec permit type the port lacked
}

public enum ShePermitStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Active = 4,
    Suspended = 5,
    Completed = 6,
    Cancelled = 7,
    Expired = 8
}

// ── PPE ───────────────────────────────────────────────────
public enum ShePpeCategory
{
    HeadProtection = 1,
    EyeFaceProtection = 2,
    HearingProtection = 3,
    RespiratoryProtection = 4,
    HandProtection = 5,
    FootProtection = 6,
    BodyProtection = 7,
    FallProtection = 8,
    HighVisibility = 9,
    Combination = 10
}

public enum ShePpeCondition
{
    New = 1,
    Good = 2,
    Fair = 3,
    Worn = 4,
    Damaged = 5,
    Condemned = 6
}

// ── Safety Equipment ──────────────────────────────────────
public enum SheSafetyEquipmentType
{
    FireExtinguisher = 1,
    FirstAidKit = 2,
    AED = 3,
    EmergencyShower = 4,
    EyeWashStation = 5,
    FireHydrant = 6,
    FireAlarmPanel = 7,
    SmokeDetector = 8,
    HeatDetector = 9,
    SpillKit = 10,
    SafetyShower = 11,
    GasDetector = 12,
    EmergencyLighting = 13,
    FireSuppressionSystem = 14,
    Other = 15
}

public enum SheSafetyEquipmentStatus
{
    Operational = 1,
    RequiresMaintenance = 2,
    UnderMaintenance = 3,
    OutOfService = 4,
    Expired = 5,
    Decommissioned = 6
}

// ── Contractor ────────────────────────────────────────────
public enum SheContractorStatus
{
    PendingAssessment = 1,
    Approved = 2,
    ConditionalApproval = 3,
    Suspended = 4,
    Revoked = 5,
    Blacklisted = 6
}

public enum SheContractorDocumentType
{
    ShePolicy = 1,
    MethodStatement = 2,
    RiskAssessment = 3,
    InsuranceCertificate = 4,
    CompetencyCertificate = 5,
    InductionRecord = 6,
    SafetyPlan = 7,
    AccidentRecord = 8,
    Other = 9
}

public enum SheNonComplianceSeverity
{
    Advisory = 1,
    Minor = 2,
    Major = 3,
    Critical = 4,
    Imminent = 5
}

public enum SheNonComplianceStatus
{
    Open = 1,
    InProgress = 2,
    PendingVerification = 3,
    Closed = 4,
    Escalated = 5
}

public enum SheContractorSanction
{
    VerbalWarning = 1,
    WrittenWarning = 2,
    WorkSuspension = 3,
    PartialSuspension = 4,
    ContractTermination = 5
}

// ── SHE Training ──────────────────────────────────────────
public enum SheTrainingCategory
{
    GeneralInduction = 1,
    FireSafetyAndEvacuation = 2,
    FirstAid = 3,
    PPEUsage = 4,
    HazardCommunication = 5,
    ManualHandling = 6,
    WorkingAtHeight = 7,
    ConfinedSpaceEntry = 8,
    HotWork = 9,
    EnvironmentalAwareness = 10,
    IncidentReporting = 11,
    EmergencyResponse = 12,
    ToolboxTalk = 13,
    ConstructionSafety = 14,
    OccupationalHealth = 15,
    BehaviouralSafety = 16
}

public enum SheTrainingDeliveryMethod
{
    Classroom = 1,
    OnTheJob = 2,
    PracticalDrill = 3,
    Online = 4,
    ToolboxTalk = 5,
    SiteWalkthrough = 6,
    Workshop = 7,
    Simulation = 8
}

public enum SheTrainingStatus
{
    Planned = 1,
    Scheduled = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5,
    Postponed = 6
}

public enum SheTrainingPlanStatus
{
    Draft = 1,
    Approved = 2,
    Active = 3,
    Completed = 4,
    Archived = 5
}

// ── Waste Management ──────────────────────────────────────
public enum SheWasteClassification
{
    NonHazardous = 1,
    Hazardous = 2,
    Recyclable = 3,
    Compostable = 4,
    InertWaste = 5,
    EWaste = 6,
    Medical = 7,
    Radioactive = 8
}

public enum SheWasteMeasurementUnit
{
    Kilograms = 1,
    Litres = 2,
    CubicMetres = 3,
    Units = 4,
    Tonnes = 5
}

public enum SheWasteDisposalMethod
{
    Landfill = 1,
    Incineration = 2,
    Recycling = 3,
    ChemicalTreatment = 4,
    BiologicalTreatment = 5,
    SecureStorage = 6,
    ReturnToSupplier = 7,
    Composting = 8
}

// ── Environmental ─────────────────────────────────────────
public enum SheEnvironmentalIncidentType
{
    ChemicalSpill = 1,
    OilSpill = 2,
    AirEmissionExceedance = 3,
    WaterContamination = 4,
    SoilContamination = 5,
    UncontrolledDumping = 6,
    NoiseExceedance = 7,
    DustExceedance = 8,
    Other = 9
}

public enum SheEnvironmentalMedia
{
    Air = 1,
    Water = 2,
    Soil = 3,
    Groundwater = 4,
    Marine = 5,
    Multiple = 6
}

public enum SheEnvironmentalIncidentStatus
{
    Reported = 1,
    ResponseInProgress = 2,
    Contained = 3,
    Remediated = 4,
    PendingRegulatoryClosure = 5,
    Closed = 6
}

public enum SheEnvironmentalMonitoringType
{
    Noise = 1,
    AmbientDust = 2,
    AirQuality = 3,
    WaterQuality = 4,
    Vibration = 5,
    Emissions = 6,
    SoilQuality = 7,
    Lighting = 8,
    // Slice 17 — the two FR-ENV-023 schedule subjects the original list lacked.
    WasteStorageInspection = 9,
    AnnualPerformanceReview = 10
}

// ── Occupational Health ───────────────────────────────────
public enum SheHealthSurveillanceType
{
    Audiometry = 1,
    LungFunctionSpirometry = 2,
    VisionTest = 3,
    BloodTest = 4,
    SkinCheck = 5,
    MusculoskeletalAssessment = 6,
    PreEmploymentMedical = 7,
    PeriodicMedical = 8,
    FitnessForWork = 9,
    HazardousSubstanceExposure = 10
}

public enum SheHealthSurveillanceResult
{
    Normal = 1,
    ActionRequired = 2,
    Referral = 3,
    WorkRestrictionsIssued = 4,
    TemporarilyUnfit = 5,
    PermanentlyUnfit = 6
}

public enum SheFirstAidStationType
{
    BasicKit = 1,
    FullKit = 2,
    MedicalRoom = 3,
    AED = 4,
    TraumaBag = 5
}

public enum SheWellnessProgramType
{
    HealthScreening = 1,
    MentalHealthSupport = 2,
    ErgonomicsAssessment = 3,
    Nutrition = 4,
    FitnessAndExercise = 5,
    StressManagement = 6,
    SubstanceAbuseAwareness = 7,
    HivAidsAwareness = 8
}

public enum SheWellnessProgramStatus
{
    Planned = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4
}

// ── Emergency ─────────────────────────────────────────────
public enum SheEmergencyType
{
    Fire = 1,
    MedicalEmergency = 2,
    ChemicalSpill = 3,
    Explosion = 4,
    NaturalDisaster = 5,
    StructuralCollapse = 6,
    PowerFailure = 7,
    SecurityThreat = 8,
    FloodOrWaterIntrusion = 9,
    General = 10
}

// ── Regulatory ────────────────────────────────────────────
public enum SheRegulatoryDomain
{
    OccupationalHealth = 1,
    OccupationalSafety = 2,
    EnvironmentalProtection = 3,
    FireSafety = 4,
    ChemicalControl = 5,
    Construction = 6,
    Labour = 7,
    PublicHealth = 8
}

// ── Safety Signage ────────────────────────────────────────
public enum SheSafetySignType
{
    Prohibition = 1,
    Warning = 2,
    Mandatory = 3,
    EmergencyEscape = 4,
    FireEquipment = 5,
    DangerousGoods = 6,
    Traffic = 7,
    Informational = 8
}

public enum SheSafetySignStatus
{
    Good = 1,
    Faded = 2,
    Damaged = 3,
    Missing = 4,
    Replaced = 5,
    Decommissioned = 6
}

// ── KPI / Performance ─────────────────────────────────────
public enum SheSnapshotPeriodType
{
    Monthly = 1,
    Quarterly = 2,
    Annual = 3
}

// ── Meetings ──────────────────────────────────────────────
public enum SheSafetyMeetingType
{
    CommitteeMeeting = 1,
    ToolboxTalk = 2,
    SafetyBriefing = 3,
    EmergencyDrillDebriefing = 4,
    ManagementReview = 5,
    TrainingSession = 6
}

public enum SheActionItemPriority
{
    Critical = 1,
    High = 2,
    Medium = 3,
    Low = 4
}

public enum SheActionItemStatus
{
    Open = 1,
    InProgress = 2,
    Completed = 3,
    Overdue = 4,
    Cancelled = 5
}

// ── Return-to-Work ────────────────────────────────────────
public enum SheReturnToWorkStatus
{
    PendingMedicalClearance = 1,
    Active = 2,
    OnHold = 3,
    Completed = 4,
    Discontinued = 5
}

// ── SHE Audits (slice 15, FRD §12 / FR-SHE-229) ──────────
public enum SheAuditType
{
    Internal = 1,
    External = 2,
    Regulatory = 3,
    Certification = 4
}

public enum SheAuditStatus
{
    Planned = 1,
    InProgress = 2,
    ReportIssued = 3,
    Closed = 4,
    Cancelled = 5
}

public enum SheAuditFindingClassification
{
    MajorNonConformity = 1,
    MinorNonConformity = 2,
    Observation = 3,
    OpportunityForImprovement = 4
}

public enum SheAuditFindingStatus
{
    Open = 1,
    Resolved = 2,
    Verified = 3,
    Closed = 4
}

// ── Stop-Work Authority (slice 15, FR-SHE-200) ───────────
public enum SheStopWorkStatus
{
    Raised = 1,
    UnderReview = 2,
    Resolved = 3,
    Cleared = 4,
    Cancelled = 5
}

// ── Statutory incident submissions (slice 15, FR-SHE-103) ─
public enum SheStatutorySubmissionType
{
    InitialNotification = 1,
    FollowUpReport = 2,
    FinalReport = 3,
    AdditionalInformation = 4
}

public enum SheStatutorySubmissionMethod
{
    OnlinePortal = 1,
    Email = 2,
    Letter = 3,
    InPerson = 4,
    Phone = 5
}

// ── SHE controlled document register (slice 16, FR-SHE-246/170) ─
/// <summary>
/// The SRS §14 document-library families plus the SoW Module-14 additions
/// (SWP, JSA, HIRA). Structured records (incident registers, training rows,
/// contractor files) stay in their own tables; these categories classify the
/// controlled DOCUMENTS about them.
/// </summary>
public enum SheControlledDocumentCategory
{
    Policy = 1,
    Procedure = 2,
    SafeWorkProcedure = 3,
    JobSafetyAnalysis = 4,
    RiskAssessment = 5,
    EmergencyPlan = 6,
    TrainingRecord = 7,
    Permit = 8,
    InspectionReport = 9,
    AuditReport = 10,
    IncidentReport = 11,
    InvestigationReport = 12,
    ContractorDocument = 13,
    Form = 14,
    Other = 15
}

public enum SheControlledDocumentStatus
{
    Draft = 1,
    Active = 2,
    UnderReview = 3,
    Archived = 4
}

// ── Part D environmental core (slice 17) ──────────────────

/// <summary>FR-ENV-017 — what kind of authorisation the register row is.</summary>
public enum SheEnvironmentalPermitType
{
    EnvironmentalPermit = 1,
    EpaRegistration = 2,
    OperatingLicence = 3,
    Certificate = 4,
    Consent = 5,
    Other = 6
}

public enum SheEnvironmentalPermitStatus
{
    Active = 1,
    RenewalInProgress = 2,
    /// <summary>Assigned by the reminder engine when the expiry date passes (FR-ENV-019 red status).</summary>
    Expired = 3,
    Suspended = 4,
    Archived = 5
}

// FR-ENV-030–032 / FR-SHE-182 — regulatory updates register.
public enum SheRegulatoryUpdateRiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum SheRegulatoryUpdateStatus
{
    Recorded = 1,
    ActionsInProgress = 2,
    Closed = 3
}

// FR-ENV-028 — the spec's eight sustainability initiative families.
public enum SheSustainabilityCategory
{
    EnergySavings = 1,
    WaterSavings = 2,
    PaperReduction = 3,
    TreePlanting = 4,
    Recycling = 5,
    WasteRecycled = 6,
    CarbonReduction = 7,
    CostSavings = 8,
    Other = 9
}

public enum SheSustainabilityStatus
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>FR-ENV-012's mandatory work-classification taxonomy at project creation.</summary>
public enum SheEnvironmentalWorkClassification
{
    PlannedProject = 1,
    Upgrade = 2,
    Maintenance = 3,
    Construction = 4,
    Demolition = 5,
    InfrastructureModification = 6,
    Other = 7
}

/// <summary>
/// FR-ENV-001–016 review lifecycle. Clearance (FR-ENV-016) is a step past
/// approval: an approved review still needs its clearance issued (and, where
/// flagged, management approval first) before FR-ENV-010's gate would open.
/// </summary>
public enum SheEnvironmentalReviewStatus
{
    Submitted = 1,
    CorrectionsRequested = 2,
    Approved = 3,
    Rejected = 4,
    ClearanceIssued = 5
}
