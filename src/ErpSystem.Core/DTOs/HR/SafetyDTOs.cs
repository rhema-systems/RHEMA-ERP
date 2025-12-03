using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class AccidentListDto
{
    public Guid Id { get; set; }
    public string AccidentNumber { get; set; }
    public Guid PrimaryEmployeeId { get; set; }
    public string PrimaryEmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Department { get; set; }
    public DateTime IncidentDate { get; set; }
    public AccidentType AccidentType { get; set; }
    public string AccidentTypeName { get; set; }
    public AccidentSeverity Severity { get; set; }
    public string SeverityName { get; set; }
    public string Location { get; set; }
    public AccidentStatus Status { get; set; }
    public string StatusName { get; set; }
    public bool RequiredHospitalization { get; set; }
    public int DaysLost { get; set; }
}

// Detail DTO
public class AccidentDetailDto
{
    public Guid Id { get; set; }
    public string AccidentNumber { get; set; }

    // Primary Employee Info
    public Guid PrimaryEmployeeId { get; set; }
    public string PrimaryEmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Position { get; set; }
    public string Department { get; set; }

    // Incident Details
    public DateTime IncidentDate { get; set; }
    public TimeSpan? IncidentTime { get; set; }
    public string Location { get; set; }
    public string SpecificArea { get; set; }
    public AccidentType AccidentType { get; set; }
    public string AccidentTypeName { get; set; }
    public AccidentSeverity Severity { get; set; }
    public string SeverityName { get; set; }
    public InjuryType? InjuryType { get; set; }
    public string InjuryTypeName { get; set; }
    public string BodyPartAffected { get; set; }
    public string Description { get; set; }

    // Causes
    public string ImmediateCause { get; set; }
    public string RootCause { get; set; }
    public string ContributingFactors { get; set; }
    public string ActivityBeingPerformed { get; set; }
    public string PpeUsed { get; set; }

    // Reporting
    public Guid ReportedById { get; set; }
    public string ReportedByName { get; set; }
    public DateTime ReportedDate { get; set; }

    // Immediate Response
    public string ImmediateActionTaken { get; set; }
    public bool FirstAidProvided { get; set; }
    public Guid? FirstAidProviderId { get; set; }
    public string FirstAidProviderName { get; set; }
    public string FirstAidDetails { get; set; }

    // Medical Treatment
    public bool RequiredHospitalization { get; set; }
    public string TreatmentFacility { get; set; }
    public string TreatmentDetails { get; set; }
    public string DiagnosisGiven { get; set; }
    public int DaysLost { get; set; }

    // Work Restrictions
    public bool RequiresLightDuty { get; set; }
    public DateTime? LightDutyStartDate { get; set; }
    public DateTime? LightDutyEndDate { get; set; }
    public string LightDutyRestrictions { get; set; }
    public DateTime? ExpectedReturnToWorkDate { get; set; }
    public DateTime? ActualReturnToWorkDate { get; set; }

    // Investigation
    public Guid? InvestigatorId { get; set; }
    public string InvestigatorName { get; set; }
    public DateTime? InvestigationStartDate { get; set; }
    public DateTime? InvestigationCompletionDate { get; set; }
    public string InvestigationFindings { get; set; }
    public string CorrectiveActionsTaken { get; set; }
    public string PreventiveMeasures { get; set; }

    // Regulatory Reporting
    public bool ReportedToAuthority { get; set; }
    public string AuthorityReportedTo { get; set; }
    public DateTime? AuthorityReportDate { get; set; }
    public string AuthorityReferenceNumber { get; set; }

    // Insurance Claim
    public bool InsuranceClaimFiled { get; set; }
    public DateTime? ClaimFiledDate { get; set; }
    public string ClaimReferenceNumber { get; set; }
    public string InsuranceCompany { get; set; }
    public decimal? ClaimAmount { get; set; }
    public decimal? AmountPaid { get; set; }

    // Status
    public AccidentStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime? ClosedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public string ClosedByName { get; set; }
    public string ClosureNotes { get; set; }

    // Collections
    public List<AccidentWitnessDto> Witnesses { get; set; }
    public List<AccidentInvolvedPersonDto> InvolvedPersons { get; set; }
    public List<AccidentDocumentDto> Documents { get; set; }
    public List<AccidentFollowUpDto> FollowUps { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateAccidentDto
{
    public Guid PrimaryEmployeeId { get; set; }
    public DateTime IncidentDate { get; set; }
    public TimeSpan? IncidentTime { get; set; }
    public string Location { get; set; }
    public string SpecificArea { get; set; }
    public AccidentType AccidentType { get; set; }
    public AccidentSeverity Severity { get; set; }
    public InjuryType? InjuryType { get; set; }
    public string BodyPartAffected { get; set; }
    public string Description { get; set; }
    public string ImmediateCause { get; set; }
    public string ActivityBeingPerformed { get; set; }
    public string PpeUsed { get; set; }
    public string ImmediateActionTaken { get; set; }
    public bool FirstAidProvided { get; set; }
    public Guid? FirstAidProviderId { get; set; }
    public string FirstAidDetails { get; set; }
    public bool RequiredHospitalization { get; set; }
    public string TreatmentFacility { get; set; }
    public List<CreateAccidentWitnessDto> Witnesses { get; set; }
    public List<CreateAccidentInvolvedPersonDto> InvolvedPersons { get; set; }
}

// Update DTO
public class UpdateAccidentDto
{
    public Guid Id { get; set; }
    public string Description { get; set; }
    public string ImmediateCause { get; set; }
    public string RootCause { get; set; }
    public string ContributingFactors { get; set; }
    public string InvestigationFindings { get; set; }
    public string CorrectiveActionsTaken { get; set; }
    public string PreventiveMeasures { get; set; }
    public int DaysLost { get; set; }
    public DateTime? ExpectedReturnToWorkDate { get; set; }
}

// Supporting DTOs
public class AccidentWitnessDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Guid? EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string ContactInfo { get; set; }
    public string Statement { get; set; }
    public DateTime? StatementDate { get; set; }
}

public class CreateAccidentWitnessDto
{
    public string Name { get; set; }
    public Guid? EmployeeId { get; set; }
    public string ContactInfo { get; set; }
    public string Statement { get; set; }
    public DateTime? StatementDate { get; set; }
}

public class AccidentInvolvedPersonDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string RoleInIncident { get; set; }
    public bool WasInjured { get; set; }
    public string InjuryDescription { get; set; }
}

public class CreateAccidentInvolvedPersonDto
{
    public Guid EmployeeId { get; set; }
    public string RoleInIncident { get; set; }
    public bool WasInjured { get; set; }
    public string InjuryDescription { get; set; }
}

public class AccidentDocumentDto
{
    public Guid Id { get; set; }
    public AccidentDocumentType DocumentType { get; set; }
    public string DocumentTypeName { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class AccidentFollowUpDto
{
    public Guid Id { get; set; }
    public DateTime FollowUpDate { get; set; }
    public Guid ConductedById { get; set; }
    public string ConductedByName { get; set; }
    public string Notes { get; set; }
    public string EmployeeCondition { get; set; }
    public string ActionsTaken { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
}

// Safety Inspection DTOs
public class SafetyInspectionListDto
{
    public Guid Id { get; set; }
    public string InspectionNumber { get; set; }
    public InspectionType InspectionType { get; set; }
    public string InspectionTypeName { get; set; }
    public DateTime InspectionDate { get; set; }
    public string Location { get; set; }
    public string InspectorName { get; set; }
    public InspectionStatus Status { get; set; }
    public string StatusName { get; set; }
    public int TotalItems { get; set; }
    public int NonCompliantItems { get; set; }
}

public class SafetyInspectionDetailDto
{
    public Guid Id { get; set; }
    public string InspectionNumber { get; set; }
    public InspectionType InspectionType { get; set; }
    public string InspectionTypeName { get; set; }
    public DateTime InspectionDate { get; set; }
    public string Location { get; set; }

    // Inspector
    public Guid InspectorId { get; set; }
    public string InspectorName { get; set; }

    // Details
    public string AreasInspected { get; set; }
    public string FindingsAndObservations { get; set; }
    public string HazardsIdentified { get; set; }
    public string RecommendedActions { get; set; }

    // Status
    public InspectionStatus Status { get; set; }
    public string StatusName { get; set; }

    // Collections
    public List<SafetyInspectionItemDto> Items { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SafetyInspectionItemDto
{
    public Guid Id { get; set; }
    public string ItemDescription { get; set; }
    public ComplianceStatus ComplianceStatus { get; set; }
    public string ComplianceStatusName { get; set; }
    public string DeficiencyNoted { get; set; }
    public string ActionRequired { get; set; }
    public Guid? ResponsiblePersonId { get; set; }
    public string ResponsiblePersonName { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
}

// Dashboard DTO
public class SafetyDashboardDto
{
    public int TotalAccidentsThisYear { get; set; }
    public int AccidentsThisMonth { get; set; }
    public int DaysWithoutAccident { get; set; }
    public int TotalDaysLost { get; set; }
    public decimal AccidentFrequencyRate { get; set; }
    public Dictionary<AccidentType, int> AccidentsByType { get; set; }
    public Dictionary<AccidentSeverity, int> AccidentsBySeverity { get; set; }
    public List<AccidentListDto> RecentAccidents { get; set; }
    public List<SafetyInspectionListDto> UpcomingInspections { get; set; }
}