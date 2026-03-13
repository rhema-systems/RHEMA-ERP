using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.StaffSafety;

/// <summary>
/// Workplace accident/incident report
/// </summary>
public class Accident : TenantEntity
{
    public string AccidentNumber { get; set; } = string.Empty;

    // Employee(s) Involved
    public Guid PrimaryEmployeeId { get; set; }
    public Employee PrimaryEmployee { get; set; } = null!;

    // Incident Details
    public DateTime IncidentDate { get; set; }
    public TimeSpan IncidentTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? SpecificArea { get; set; }

    // Classification
    public AccidentType Type { get; set; } // Injury, Near Miss, Property Damage, Vehicle
    public AccidentSeverity Severity { get; set; }
    public InjuryType? InjuryType { get; set; }
    public string? BodyPartAffected { get; set; }

    // Description
    public string Description { get; set; } = string.Empty;
    public string? ImmediateCause { get; set; }
    public string? RootCause { get; set; }
    public string? ContributingFactors { get; set; }

    // Reporting
    public Guid ReportedById { get; set; }
    public Employee ReportedBy { get; set; } = null!;
    public DateTime ReportedDate { get; set; }
    public TimeSpan ReportedTime { get; set; }

    // Work Status at Time of Incident
    public bool WasOnDuty { get; set; }
    public string? ActivityBeingPerformed { get; set; }
    public bool WasUsingPpe { get; set; }
    public string? PpeUsed { get; set; }

    // Witnesses
    public bool WitnessesPresent { get; set; }
    public int? NumberOfWitnesses { get; set; }

    // Immediate Action Taken
    public string? ImmediateActionTaken { get; set; }
    public bool FirstAidGiven { get; set; }
    public string? FirstAidDetails { get; set; }
    public Guid? FirstAidProviderId { get; set; }
    public Employee? FirstAidProvider { get; set; }

    // Medical Treatment
    public bool MedicalTreatmentRequired { get; set; }
    public string? TreatmentFacility { get; set; }
    public DateTime? TreatmentDate { get; set; }
    public string? TreatmentDetails { get; set; }
    public string? DiagnosisGiven { get; set; }

    // Time Lost
    public bool ResultedInTimeOff { get; set; }
    public DateTime? TimeOffStartDate { get; set; }
    public DateTime? TimeOffEndDate { get; set; }
    public int? DaysLost { get; set; }
    public bool OnLightDuty { get; set; }
    public string? LightDutyRestrictions { get; set; }

    // Investigation
    public AccidentStatus Status { get; set; }
    public bool RequiresInvestigation { get; set; }
    public Guid? InvestigatorId { get; set; }
    public Employee? Investigator { get; set; }

    public DateTime? InvestigationStartDate { get; set; }
    public DateTime? InvestigationCompleteDate { get; set; }
    public string? InvestigationFindings { get; set; }

    // Corrective Actions
    public string? CorrectiveActionsTaken { get; set; }
    public string? PreventiveMeasures { get; set; }
    public DateTime? CorrectiveActionDueDate { get; set; }
    public bool CorrectiveActionsCompleted { get; set; }
    public DateTime? CorrectiveActionCompletionDate { get; set; }

    // Regulatory
    public bool ReportableToAuthority { get; set; }
    public string? AuthorityReportedTo { get; set; }
    public DateTime? AuthorityReportDate { get; set; }
    public string? AuthorityReferenceNumber { get; set; }

    // Insurance
    public bool InsuranceClaimFiled { get; set; }
    public DateTime? ClaimFiledDate { get; set; }
    public string? ClaimReferenceNumber { get; set; }
    public string? InsuranceCompany { get; set; }
    public decimal? ClaimAmount { get; set; }
    public bool ClaimApproved { get; set; }
    public decimal? AmountPaid { get; set; }

    // Closure
    public DateTime? ClosedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public Employee? ClosedBy { get; set; }
    public string? ClosureNotes { get; set; }

    // Relations
    public ICollection<AccidentWitness> Witnesses { get; set; } = new List<AccidentWitness>();
    public ICollection<AccidentInvolvedPerson> InvolvedPersons { get; set; } = new List<AccidentInvolvedPerson>();
    public ICollection<AccidentDocument> Documents { get; set; } = new List<AccidentDocument>();
    public ICollection<AccidentFollowUp> FollowUps { get; set; } = new List<AccidentFollowUp>();
}

public class AccidentWitness : TenantEntity
{
    public Guid AccidentId { get; set; }
    public Accident Accident { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string? ContactInfo { get; set; }
    public string? Statement { get; set; }
    public DateTime? StatementDate { get; set; }
    public bool StatementSigned { get; set; }
}

public class AccidentInvolvedPerson : TenantEntity
{
    public Guid AccidentId { get; set; }
    public Accident Accident { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public string RoleInIncident { get; set; } = string.Empty;
    public bool WasInjured { get; set; }
    public string? InjuryDescription { get; set; }
}

public class AccidentDocument : TenantEntity
{
    public Guid AccidentId { get; set; }
    public Accident Accident { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public AccidentDocumentType Type { get; set; } // Medical Report, Photos, Police Report
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

public class AccidentFollowUp : TenantEntity
{
    public Guid AccidentId { get; set; }
    public Accident Accident { get; set; } = null!;

    public DateTime FollowUpDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string? EmployeeCondition { get; set; }
    public string? ActionsTaken { get; set; }

    public Guid ConductedById { get; set; }
    public Employee ConductedBy { get; set; } = null!;
}

/// <summary>
/// Safety inspection record
/// </summary>
public class SafetyInspection : TenantEntity
{
    public string InspectionNumber { get; set; } = string.Empty;

    public DateTime InspectionDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public InspectionType Type { get; set; } // Routine, Compliance, Follow-up

    public Guid InspectorId { get; set; }
    public Employee Inspector { get; set; } = null!;

    public string? AreasInspected { get; set; }
    public string? FindingsAndObservations { get; set; }
    public string? HazardsIdentified { get; set; }
    public string? RecommendedActions { get; set; }

    public InspectionStatus Status { get; set; }
    public DateTime? ComplianceDeadline { get; set; }

    public ICollection<SafetyInspectionItem> Items { get; set; } = new List<SafetyInspectionItem>();
}

public class SafetyInspectionItem : TenantEntity
{
    public Guid InspectionId { get; set; }
    public SafetyInspection Inspection { get; set; } = null!;

    public string ItemDescription { get; set; } = string.Empty;
    public ComplianceStatus Status { get; set; }
    public string? DeficiencyNoted { get; set; }
    public string? ActionRequired { get; set; }
    public DateTime? TargetDate { get; set; }

    public Guid? ResponsiblePersonId { get; set; }
    public Employee? ResponsiblePerson { get; set; }

    public bool IsResolved { get; set; }
    public DateTime? ResolvedDate { get; set; }
}