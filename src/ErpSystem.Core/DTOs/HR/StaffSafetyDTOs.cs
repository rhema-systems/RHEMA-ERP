using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE (Safety, Health & Environment) — REFERENCE CATALOG & INCIDENT DTOs
// Domains: A. Reference / Lookup Catalog   B. Incident Management & Investigation
// Hazard/Risk, Inspections, Permit, PPE, Equipment, Contractor, Training,
// Waste, Environmental, Occupational Health, Emergency, Regulatory, Signage,
// KPI, Committee and Return-to-Work DTOs live in the sibling Safety*DTOs.cs files.
// ============================================================================

// ============================================================================
// A. REFERENCE / LOOKUP CATALOG
// ============================================================================

#region Incident Type

public class SheIncidentTypeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SheIncidentCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public bool IsReportable { get; set; }
    public Guid? RegulatoryBodyId { get; set; }
    public string? RegulatoryBodyName { get; set; }
    public int? ReportingWindowHours { get; set; }
    public bool IsActive { get; set; }
    public List<SheIncidentTypeCorrectiveActionDto> DefaultCorrectiveActions { get; set; } = new();
}

public class CreateSheIncidentTypeDto : CreateDtoBase
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public SheIncidentCategory Category { get; set; }

    public bool IsReportable { get; set; }
    public Guid? RegulatoryBodyId { get; set; }
    public int? ReportingWindowHours { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSheIncidentTypeDto : UpdateDtoBase
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public SheIncidentCategory Category { get; set; }

    public bool IsReportable { get; set; }
    public Guid? RegulatoryBodyId { get; set; }
    public int? ReportingWindowHours { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SheIncidentTypeCorrectiveActionDto : BaseDto
{
    public Guid IncidentTypeId { get; set; }
    public Guid CorrectiveActionTemplateId { get; set; }
    public string CorrectiveActionTemplateCode { get; set; } = string.Empty;
    public string CorrectiveActionTemplateTitle { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public int? DeadlineDays { get; set; }
    public bool IsMandatory { get; set; }
}

public class CreateSheIncidentTypeCorrectiveActionDto : CreateDtoBase
{
    [Required]
    public Guid IncidentTypeId { get; set; }

    [Required]
    public Guid CorrectiveActionTemplateId { get; set; }

    public int DisplayOrder { get; set; }
    public int? DeadlineDays { get; set; }
    public bool IsMandatory { get; set; } = true;
}

public class UpdateSheIncidentTypeCorrectiveActionDto : UpdateDtoBase
{
    public int DisplayOrder { get; set; }
    public int? DeadlineDays { get; set; }
    public bool IsMandatory { get; set; } = true;
}

#endregion

#region Injury Type

public class SheInjuryTypeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSheInjuryTypeDto : CreateDtoBase
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSheInjuryTypeDto : UpdateDtoBase
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

#endregion

#region Body Part

public class SheBodyPartDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Region { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSheBodyPartDto : CreateDtoBase
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Region { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSheBodyPartDto : UpdateDtoBase
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Region { get; set; }

    public bool IsActive { get; set; } = true;
}

#endregion

#region Corrective Action Template

public class SheCorrectiveActionTemplateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SheCorrectiveActionCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public int? DefaultDeadlineDays { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSheCorrectiveActionTemplateDto : CreateDtoBase
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public SheCorrectiveActionCategory Category { get; set; }

    public int? DefaultDeadlineDays { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSheCorrectiveActionTemplateDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public SheCorrectiveActionCategory Category { get; set; }

    public int? DefaultDeadlineDays { get; set; }
    public bool IsActive { get; set; } = true;
}

#endregion

#region Regulatory Body

public class SheRegulatoryBodyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactAddress { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public SheRegulatoryDomain Domain { get; set; }
    public string DomainName => Domain.ToString();
    public bool IsActive { get; set; }
}

public class CreateSheRegulatoryBodyDto : CreateDtoBase
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ShortName { get; set; }

    [MaxLength(300)]
    public string? ContactAddress { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [Required]
    public SheRegulatoryDomain Domain { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSheRegulatoryBodyDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ShortName { get; set; }

    [MaxLength(300)]
    public string? ContactAddress { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [Required]
    public SheRegulatoryDomain Domain { get; set; }

    public bool IsActive { get; set; } = true;
}

#endregion

// ============================================================================
// B. INCIDENT MANAGEMENT & INVESTIGATION
// ============================================================================

#region Safety Incident

public class SafetyIncidentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;

    // Classification
    public SheIncidentCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public SheIncidentSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public SheIncidentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? IncidentTypeId { get; set; }
    public string? IncidentTypeName { get; set; }

    // When / Where
    public DateTime IncidentDate { get; set; }
    public TimeSpan? IncidentTime { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? SpecificArea { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? SupervisorId { get; set; }
    public string? SupervisorName { get; set; }

    // Description
    public string Description { get; set; } = string.Empty;
    public string? ImmediateCause { get; set; }
    public string? UnderlyingCause { get; set; }
    public string? ContributingFactors { get; set; }
    public bool CouldHaveCausedInjury { get; set; }
    public string? PotentialConsequence { get; set; }

    // Reporting
    public Guid ReportedById { get; set; }
    public string ReportedByName { get; set; } = string.Empty;
    public DateTime ReportedDate { get; set; }
    public string? ImmediateActionTaken { get; set; }

    // Risk rating
    public int? LikelihoodBefore { get; set; }
    public int? SeverityBefore { get; set; }
    public int? RiskScoreBefore { get; set; }
    public int? LikelihoodAfter { get; set; }
    public int? SeverityAfter { get; set; }
    public int? RiskScoreAfter { get; set; }

    // Investigation
    public bool RequiresInvestigation { get; set; }
    public Guid? LeadInvestigatorId { get; set; }
    public string? LeadInvestigatorName { get; set; }
    public DateTime? InvestigationStartDate { get; set; }
    public DateTime? InvestigationTargetDate { get; set; }
    public DateTime? InvestigationCompleteDate { get; set; }
    public string? RootCauseAnalysis { get; set; }
    public string? InvestigationFindings { get; set; }
    public SheRootCauseMethod? RootCauseMethod { get; set; }
    public string? RootCauseMethodName => RootCauseMethod?.ToString();

    // Regulatory notification
    public bool ReportableToAuthority { get; set; }
    public Guid? ReportedToBodyId { get; set; }
    public string? ReportedToBodyName { get; set; }
    public DateTime? AuthorityNotificationDate { get; set; }
    public string? AuthorityReferenceNumber { get; set; }
    public Guid? AuthorityNotifiedById { get; set; }
    public string? AuthorityNotifiedByName { get; set; }

    // Insurance
    public bool InsuranceClaimFiled { get; set; }
    public DateTime? ClaimFiledDate { get; set; }
    public string? ClaimReferenceNumber { get; set; }
    public Guid? InsuranceProviderId { get; set; }
    public string? InsuranceProviderName { get; set; }
    public decimal? ClaimAmount { get; set; }
    public bool ClaimApproved { get; set; }
    public decimal? AmountPaid { get; set; }

    // Lost time
    public int TotalLostDays { get; set; }
    public bool IsLostTimeInjury { get; set; }

    // Governance review
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewComments { get; set; }

    // Closure
    public DateTime? ClosedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
    public string? ClosureNotes { get; set; }

    // Children
    public List<SafetyIncidentInvolvedPersonDto> InvolvedPersons { get; set; } = new();
    public List<SafetyIncidentWitnessDto> Witnesses { get; set; } = new();
    public List<SafetyIncidentInvestigationTeamMemberDto> InvestigationTeam { get; set; } = new();
    public List<SafetyIncidentCorrectiveActionDto> CorrectiveActions { get; set; } = new();
    public List<SafetyIncidentFollowUpDto> FollowUps { get; set; } = new();
    public List<SafetyIncidentDocumentDto> Documents { get; set; } = new();
}

public class SafetyIncidentSummaryDto
{
    public Guid Id { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public SheIncidentCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public SheIncidentSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public SheIncidentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? IncidentTypeName { get; set; }
    public DateTime IncidentDate { get; set; }
    public string? LocationName { get; set; }
    public string ReportedByName { get; set; } = string.Empty;
    public bool RequiresInvestigation { get; set; }
    public bool IsLostTimeInjury { get; set; }
    public int TotalLostDays { get; set; }
    public int InvolvedPersonCount { get; set; }
    public int OpenCorrectiveActionCount { get; set; }
}

public class CreateSafetyIncidentDto : CreateDtoBase
{
    [Required]
    public SheIncidentCategory Category { get; set; }

    [Required]
    public SheIncidentSeverity Severity { get; set; }

    public Guid? IncidentTypeId { get; set; }

    [Required]
    public DateTime IncidentDate { get; set; }
    public TimeSpan? IncidentTime { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public Guid? SupervisorId { get; set; }

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ImmediateCause { get; set; }

    [MaxLength(2000)]
    public string? ContributingFactors { get; set; }

    public bool CouldHaveCausedInjury { get; set; }

    [MaxLength(500)]
    public string? PotentialConsequence { get; set; }

    [Required]
    public Guid ReportedById { get; set; }

    public DateTime ReportedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? ImmediateActionTaken { get; set; }

    [Range(1, 5)]
    public int? LikelihoodBefore { get; set; }

    [Range(1, 5)]
    public int? SeverityBefore { get; set; }

    public bool RequiresInvestigation { get; set; }
    public bool ReportableToAuthority { get; set; }
}

public class UpdateSafetyIncidentDto : UpdateDtoBase
{
    [Required]
    public SheIncidentCategory Category { get; set; }

    [Required]
    public SheIncidentSeverity Severity { get; set; }

    public Guid? IncidentTypeId { get; set; }

    [Required]
    public DateTime IncidentDate { get; set; }
    public TimeSpan? IncidentTime { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public Guid? SupervisorId { get; set; }

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ImmediateCause { get; set; }

    [MaxLength(2000)]
    public string? UnderlyingCause { get; set; }

    [MaxLength(2000)]
    public string? ContributingFactors { get; set; }

    public bool CouldHaveCausedInjury { get; set; }

    [MaxLength(500)]
    public string? PotentialConsequence { get; set; }

    [MaxLength(500)]
    public string? ImmediateActionTaken { get; set; }

    [Range(1, 5)]
    public int? LikelihoodBefore { get; set; }

    [Range(1, 5)]
    public int? SeverityBefore { get; set; }

    [Range(1, 5)]
    public int? LikelihoodAfter { get; set; }

    [Range(1, 5)]
    public int? SeverityAfter { get; set; }

    public bool RequiresInvestigation { get; set; }
    public bool ReportableToAuthority { get; set; }
}

/// <summary>Assigns/updates the investigation for an incident.</summary>
public class AssignSafetyIncidentInvestigationDto
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required]
    public Guid LeadInvestigatorId { get; set; }

    public DateTime? InvestigationStartDate { get; set; }
    public DateTime? InvestigationTargetDate { get; set; }
    public SheRootCauseMethod? RootCauseMethod { get; set; }
}

/// <summary>Records investigation findings / root-cause analysis.</summary>
public class RecordSafetyIncidentInvestigationDto
{
    [Required]
    public Guid IncidentId { get; set; }

    [MaxLength(4000)]
    public string? RootCauseAnalysis { get; set; }

    [MaxLength(4000)]
    public string? InvestigationFindings { get; set; }

    public SheRootCauseMethod? RootCauseMethod { get; set; }
    public DateTime? InvestigationCompleteDate { get; set; }

    [Range(1, 5)]
    public int? LikelihoodAfter { get; set; }

    [Range(1, 5)]
    public int? SeverityAfter { get; set; }
}

/// <summary>Records statutory notification to a regulatory body.</summary>
public class NotifySafetyIncidentAuthorityDto
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required]
    public Guid ReportedToBodyId { get; set; }

    public DateTime AuthorityNotificationDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? AuthorityReferenceNumber { get; set; }

    [Required]
    public Guid AuthorityNotifiedById { get; set; }
}

/// <summary>Files / updates an insurance claim against the incident.</summary>
public class FileSafetyIncidentClaimDto
{
    [Required]
    public Guid IncidentId { get; set; }

    public DateTime ClaimFiledDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ClaimReferenceNumber { get; set; }

    public Guid? InsuranceProviderId { get; set; }
    public decimal? ClaimAmount { get; set; }
    public bool ClaimApproved { get; set; }
    public decimal? AmountPaid { get; set; }
}

/// <summary>Governance review of the incident.</summary>
public class ReviewSafetyIncidentDto
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required]
    public Guid ReviewedById { get; set; }

    public DateTime ReviewedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? ReviewComments { get; set; }

    public SheIncidentStatus? NewStatus { get; set; }
}

/// <summary>Closes the incident.</summary>
public class CloseSafetyIncidentDto
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required]
    public Guid ClosedById { get; set; }

    public DateTime ClosedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? ClosureNotes { get; set; }
}

#endregion

#region Safety Incident Involved Person

public class SafetyIncidentInvolvedPersonDto : BaseDto
{
    public Guid IncidentId { get; set; }
    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeNumber { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? OrganizationOrCompany { get; set; }
    public SheInvolvedPersonRole RoleInIncident { get; set; }
    public string RoleInIncidentName => RoleInIncident.ToString();
    public bool WasOnDuty { get; set; }
    public string? ActivityBeingPerformed { get; set; }

    // PPE
    public bool WasUsingPpe { get; set; }
    public string? PpeUsed { get; set; }
    public bool PpeWasAdequate { get; set; }

    // Injury
    public bool WasInjured { get; set; }
    public string? InjuryDescription { get; set; }
    public SheInjuryClassification? InjuryClassification { get; set; }
    public string? InjuryClassificationName => InjuryClassification?.ToString();
    public Guid? InjuryTypeId { get; set; }
    public string? InjuryTypeName { get; set; }
    public bool IsFatal { get; set; }
    public List<SafetyIncidentInjuredBodyPartDto> InjuredBodyParts { get; set; } = new();

    // First aid
    public bool FirstAidGiven { get; set; }
    public string? FirstAidDetails { get; set; }
    public Guid? FirstAidProviderId { get; set; }
    public string? FirstAidProviderName { get; set; }

    // Medical treatment
    public bool MedicalTreatmentRequired { get; set; }
    public Guid? HealthcareFacilityId { get; set; }
    public string? HealthcareFacilityName { get; set; }
    public DateTime? TreatmentDate { get; set; }
    public string? DiagnosisGiven { get; set; }
    public Guid? MedicalExpenseClaimId { get; set; }

    // Lost time
    public bool ResultedInTimeOff { get; set; }
    public DateTime? TimeOffStartDate { get; set; }
    public DateTime? TimeOffEndDate { get; set; }
    public int? LostDays { get; set; }
    public bool OnLightDuty { get; set; }
    public string? LightDutyRestrictions { get; set; }
    public Guid? ReturnToWorkPlanId { get; set; }
}

public class CreateSafetyIncidentInvolvedPersonDto : CreateDtoBase
{
    [Required]
    public Guid IncidentId { get; set; }

    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? OrganizationOrCompany { get; set; }

    [Required]
    public SheInvolvedPersonRole RoleInIncident { get; set; }

    public bool WasOnDuty { get; set; }

    [MaxLength(500)]
    public string? ActivityBeingPerformed { get; set; }

    public bool WasUsingPpe { get; set; }

    [MaxLength(500)]
    public string? PpeUsed { get; set; }

    public bool PpeWasAdequate { get; set; }

    public bool WasInjured { get; set; }

    [MaxLength(1000)]
    public string? InjuryDescription { get; set; }

    public SheInjuryClassification? InjuryClassification { get; set; }
    public Guid? InjuryTypeId { get; set; }
    public bool IsFatal { get; set; }

    public bool FirstAidGiven { get; set; }

    [MaxLength(500)]
    public string? FirstAidDetails { get; set; }

    public Guid? FirstAidProviderId { get; set; }

    public bool MedicalTreatmentRequired { get; set; }
    public Guid? HealthcareFacilityId { get; set; }
    public DateTime? TreatmentDate { get; set; }

    [MaxLength(500)]
    public string? DiagnosisGiven { get; set; }

    public bool ResultedInTimeOff { get; set; }
    public DateTime? TimeOffStartDate { get; set; }
    public DateTime? TimeOffEndDate { get; set; }
    public int? LostDays { get; set; }
    public bool OnLightDuty { get; set; }

    [MaxLength(500)]
    public string? LightDutyRestrictions { get; set; }
}

public class UpdateSafetyIncidentInvolvedPersonDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? OrganizationOrCompany { get; set; }

    [Required]
    public SheInvolvedPersonRole RoleInIncident { get; set; }

    public bool WasOnDuty { get; set; }

    [MaxLength(500)]
    public string? ActivityBeingPerformed { get; set; }

    public bool WasUsingPpe { get; set; }

    [MaxLength(500)]
    public string? PpeUsed { get; set; }

    public bool PpeWasAdequate { get; set; }

    public bool WasInjured { get; set; }

    [MaxLength(1000)]
    public string? InjuryDescription { get; set; }

    public SheInjuryClassification? InjuryClassification { get; set; }
    public Guid? InjuryTypeId { get; set; }
    public bool IsFatal { get; set; }

    public bool FirstAidGiven { get; set; }

    [MaxLength(500)]
    public string? FirstAidDetails { get; set; }

    public Guid? FirstAidProviderId { get; set; }

    public bool MedicalTreatmentRequired { get; set; }
    public Guid? HealthcareFacilityId { get; set; }
    public DateTime? TreatmentDate { get; set; }

    [MaxLength(500)]
    public string? DiagnosisGiven { get; set; }

    public Guid? MedicalExpenseClaimId { get; set; }

    public bool ResultedInTimeOff { get; set; }
    public DateTime? TimeOffStartDate { get; set; }
    public DateTime? TimeOffEndDate { get; set; }
    public int? LostDays { get; set; }
    public bool OnLightDuty { get; set; }

    [MaxLength(500)]
    public string? LightDutyRestrictions { get; set; }

    public Guid? ReturnToWorkPlanId { get; set; }
}

public class SafetyIncidentInjuredBodyPartDto : BaseDto
{
    public Guid InvolvedPersonId { get; set; }
    public Guid BodyPartId { get; set; }
    public string BodyPartName { get; set; } = string.Empty;
    public SheBodySide? Side { get; set; }
    public string? SideName => Side?.ToString();
    public string? Notes { get; set; }
}

public class CreateSafetyIncidentInjuredBodyPartDto : CreateDtoBase
{
    [Required]
    public Guid InvolvedPersonId { get; set; }

    [Required]
    public Guid BodyPartId { get; set; }

    public SheBodySide? Side { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Safety Incident Witness

public class SafetyIncidentWitnessDto : BaseDto
{
    public Guid IncidentId { get; set; }
    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? EmailAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Statement { get; set; }
    public DateTime? StatementDate { get; set; }
    public bool StatementSigned { get; set; }
    public string? StatementDocumentPath { get; set; }
    public Guid? InterviewedById { get; set; }
    public string? InterviewedByName { get; set; }
    public DateTime? InterviewDate { get; set; }
}

public class CreateSafetyIncidentWitnessDto : CreateDtoBase
{
    [Required]
    public Guid IncidentId { get; set; }

    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? EmailAddress { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [MaxLength(3000)]
    public string? Statement { get; set; }

    public DateTime? StatementDate { get; set; }
    public bool StatementSigned { get; set; }

    [MaxLength(500)]
    public string? StatementDocumentPath { get; set; }

    public Guid? InterviewedById { get; set; }
    public DateTime? InterviewDate { get; set; }
}

public class UpdateSafetyIncidentWitnessDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? EmailAddress { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [MaxLength(3000)]
    public string? Statement { get; set; }

    public DateTime? StatementDate { get; set; }
    public bool StatementSigned { get; set; }

    [MaxLength(500)]
    public string? StatementDocumentPath { get; set; }

    public Guid? InterviewedById { get; set; }
    public DateTime? InterviewDate { get; set; }
}

#endregion

#region Safety Incident Investigation Team

public class SafetyIncidentInvestigationTeamMemberDto : BaseDto
{
    public Guid IncidentId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
}

public class CreateSafetyIncidentInvestigationTeamMemberDto : CreateDtoBase
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
}

#endregion

#region Safety Incident Corrective Action

public class SafetyIncidentCorrectiveActionDto : BaseDto
{
    public Guid IncidentId { get; set; }
    public string? IncidentNumber { get; set; }
    public Guid? IncidentTypeCorrectiveActionId { get; set; }
    public string ActionDescription { get; set; } = string.Empty;
    public SheCorrectiveActionPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public SheCorrectiveActionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid ResponsiblePersonId { get; set; }
    public string ResponsiblePersonName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }
    public bool EffectivenessVerified { get; set; }
    public DateTime? VerificationDate { get; set; }
    public string? EffectivenessReviewNotes { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public bool IsOverdue => Status != SheCorrectiveActionStatus.Completed
                             && Status != SheCorrectiveActionStatus.Verified
                             && Status != SheCorrectiveActionStatus.Cancelled
                             && DueDate < DateTime.UtcNow;
}

public class CreateSafetyIncidentCorrectiveActionDto : CreateDtoBase
{
    [Required]
    public Guid IncidentId { get; set; }

    public Guid? IncidentTypeCorrectiveActionId { get; set; }

    [Required, MaxLength(1000)]
    public string ActionDescription { get; set; } = string.Empty;

    [Required]
    public SheCorrectiveActionPriority Priority { get; set; }

    [Required]
    public Guid ResponsiblePersonId { get; set; }

    [Required]
    public DateTime DueDate { get; set; }
}

public class UpdateSafetyIncidentCorrectiveActionDto : UpdateDtoBase
{
    [Required, MaxLength(1000)]
    public string ActionDescription { get; set; } = string.Empty;

    [Required]
    public SheCorrectiveActionPriority Priority { get; set; }

    [Required]
    public SheCorrectiveActionStatus Status { get; set; }

    [Required]
    public Guid ResponsiblePersonId { get; set; }

    [Required]
    public DateTime DueDate { get; set; }

    public DateTime? CompletionDate { get; set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }
}

/// <summary>Verifies the effectiveness of a completed corrective action.</summary>
public class VerifySafetyIncidentCorrectiveActionDto
{
    [Required]
    public Guid CorrectiveActionId { get; set; }

    [Required]
    public Guid VerifiedById { get; set; }

    public DateTime VerificationDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? EffectivenessReviewNotes { get; set; }
}

#endregion

#region Safety Incident Follow-Up

public class SafetyIncidentFollowUpDto : BaseDto
{
    public Guid IncidentId { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string? ActionsTaken { get; set; }
    public string? PersonCondition { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool FurtherFollowUpRequired { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid ConductedById { get; set; }
    public string ConductedByName { get; set; } = string.Empty;
}

public class CreateSafetyIncidentFollowUpDto : CreateDtoBase
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required]
    public DateTime FollowUpDate { get; set; }

    [MaxLength(1000)]
    public string? ActionsTaken { get; set; }

    [MaxLength(500)]
    public string? PersonCondition { get; set; }

    [Required, MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;

    public bool FurtherFollowUpRequired { get; set; }
    public DateTime? NextFollowUpDate { get; set; }

    [Required]
    public Guid ConductedById { get; set; }
}

#endregion

#region Safety Incident Document

public class SafetyIncidentDocumentDto : BaseDto
{
    public Guid IncidentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public SheIncidentDocumentType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class CreateSafetyIncidentDocumentDto : CreateDtoBase
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public SheIncidentDocumentType Type { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public Guid UploadedById { get; set; }
}

#endregion
