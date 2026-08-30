namespace ErpSystem.Core.Enums;

public enum CivilEngineeringConfigurationProfileStatus { Draft = 0, Published = 1, Retired = 2 }
public enum CivilEngineeringConfigurationDecisionStatus { Draft = 0, Proposed = 1, Approved = 2, Rejected = 3 }
public enum CivilEngineeringConfigurationApprovalStatus { Pending = 0, Approved = 1, Rejected = 2 }
public enum CivilEngineeringConfigurationEvidenceStatus { Missing = 0, Attached = 1, Verified = 2 }

public enum CivilEngineeringWorkClassification
{
    NewProjectDesign,
    ConstructionSupervision,
    ScheduledMaintenance,
    BreakdownMaintenance,
    PermittingReview,
    AssetComplaintResolution
}

public enum CivilEngineeringDesignDiscipline
{
    Civil,
    Structural,
    Architecture,
    Geodetic,
    TownPlanning,
    Mechanical,
    Electrical
}

public enum CivilEngineeringDocumentNamingPolicy
{
    ProjectDisciplineSequenceRevision,
    ProjectWorkPackageSequenceRevision
}

public enum CivilEngineeringFileCategory
{
    AutoCad,
    ProtaStructure,
    Revit,
    StaadPro,
    Pdf,
    Office,
    Image,
    Other
}

public enum CivilEngineeringDocumentStatus
{
    Draft,
    ForReview,
    Approved,
    Returned,
    Superseded,
    Archived
}

public enum CivilEngineeringReportDueDay
{
    Friday,
    Monday
}

public enum CivilEngineeringRequestSource
{
    Project,
    Maintenance,
    Estate,
    TenantComplaint,
    InternalInspection,
    ManagementDirective
}

/// <summary>
/// The authoritative record family from which a Civil Engineering design case
/// may be initiated.  This is deliberately separate from maintenance-intake
/// sources: a Works/Engineering Case must retain its original owner lineage.
/// </summary>
public enum CivilEngineeringWorksInitiationSource
{
    ApprovedCapitalProject,
    MaintenanceEscalation,
    PropertyDevelopmentNeed,
    PlanningCondition,
    ManagementDirective,
    DefectMonitoring,
    InfrastructureImprovementRequest
}

/// <summary>
/// Controlled Planning/GIS review lifecycle attached to an existing Civil
/// Engineering design case.  It is deliberately not a second project or
/// permitting lifecycle.
/// </summary>
public enum CivilEngineeringPlanningGisValidationStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected
}

public enum CivilEngineeringLayoutConformity
{
    Conforms,
    ConformsWithConditions,
    NonConforming
}

public enum CivilEngineeringUrgency
{
    Routine,
    Priority,
    Urgent,
    Emergency
}

public enum CivilEngineeringPermittingOutcome
{
    RecommendApproval,
    RecommendApprovalWithConditions,
    ReturnForCorrection,
    RecommendRejection
}

public enum CivilEngineeringQualityTestCategory
{
    Concrete,
    Soil,
    Materials,
    Structural,
    Laboratory,
    Field
}

public enum CivilEngineeringProjectEngineerAuthority
{
    SiteSupervision,
    SiteSupervisionAndInstructions,
    FullProjectEngineer
}

public enum CivilEngineeringMigrationSource
{
    Spreadsheet,
    DocumentRegister,
    SharedDrive,
    PhysicalFileRegister,
    LegacyDatabase
}
