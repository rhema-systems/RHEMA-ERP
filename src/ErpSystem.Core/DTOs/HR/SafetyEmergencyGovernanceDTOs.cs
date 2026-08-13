using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE — EMERGENCY, REGULATORY, SIGNAGE, KPI, COMMITTEE & RETURN-TO-WORK DTOs
// Domains: M. Emergency Preparedness & Response   N. Regulatory Compliance Register
//          O. Safety Signage Register   P. SHE Performance Metrics / KPIs
//          Q. Safety Committee & Meetings   R. Return-to-Work Plans
// ============================================================================

// ============================================================================
// M. EMERGENCY PREPAREDNESS & RESPONSE
// ============================================================================

#region Emergency Plan

public class EmergencyPlanDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public SheEmergencyType Type { get; set; }
    public string TypeName => Type.ToString();
    public string Description { get; set; } = string.Empty;
    public string Procedures { get; set; } = string.Empty;
    public string? EvacuationRouteDocumentPath { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public DateTime LastReviewed { get; set; }
    public DateTime NextReviewDate { get; set; }
    public Guid PlanOwnerId { get; set; }
    public string PlanOwnerName { get; set; } = string.Empty;
    public string? DocumentPath { get; set; }
    public bool IsActive { get; set; }

    public List<SheAssemblyPointDto> AssemblyPoints { get; set; } = new();
    public List<EmergencyContactDto> EmergencyContacts { get; set; } = new();
    public List<EmergencyDrillDto> Drills { get; set; } = new();
    public List<EmergencyResponseTeamDto> TeamMembers { get; set; } = new();
}

public class EmergencyPlanSummaryDto
{
    public Guid Id { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public SheEmergencyType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? LocationName { get; set; }
    public string PlanOwnerName { get; set; } = string.Empty;
    public DateTime NextReviewDate { get; set; }
    public bool IsActive { get; set; }
}

public class CreateEmergencyPlanDto : CreateDtoBase
{
    [Required, MaxLength(100)]
    public string PlanNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string PlanName { get; set; } = string.Empty;

    [Required]
    public SheEmergencyType Type { get; set; }

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Procedures { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? EvacuationRouteDocumentPath { get; set; }

    public Guid? LocationId { get; set; }

    [Required]
    public DateTime LastReviewed { get; set; }

    [Required]
    public DateTime NextReviewDate { get; set; }

    [Required]
    public Guid PlanOwnerId { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateEmergencyPlanDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string PlanName { get; set; } = string.Empty;

    [Required]
    public SheEmergencyType Type { get; set; }

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Procedures { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? EvacuationRouteDocumentPath { get; set; }

    public Guid? LocationId { get; set; }

    [Required]
    public DateTime LastReviewed { get; set; }

    [Required]
    public DateTime NextReviewDate { get; set; }

    [Required]
    public Guid PlanOwnerId { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public bool IsActive { get; set; } = true;
}

public class SheAssemblyPointDto : BaseDto
{
    public Guid EmergencyPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? SpecificArea { get; set; }
    public decimal? GpsLatitude { get; set; }
    public decimal? GpsLongitude { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSheAssemblyPointDto : CreateDtoBase
{
    [Required]
    public Guid EmergencyPlanId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public decimal? GpsLatitude { get; set; }
    public decimal? GpsLongitude { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSheAssemblyPointDto : UpdateDtoBase
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    public decimal? GpsLatitude { get; set; }
    public decimal? GpsLongitude { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EmergencyContactDto : BaseDto
{
    public Guid EmergencyPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PrimaryPhone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public string? Email { get; set; }
    public bool IsExternal { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CreateEmergencyContactDto : CreateDtoBase
{
    [Required]
    public Guid EmergencyPlanId { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string PrimaryPhone { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? AlternatePhone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    public bool IsExternal { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateEmergencyContactDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string PrimaryPhone { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? AlternatePhone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    public bool IsExternal { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EmergencyDrillDto : BaseDto
{
    public Guid EmergencyPlanId { get; set; }
    public string DrillNumber { get; set; } = string.Empty;
    public string DrillName { get; set; } = string.Empty;
    public DateTime DrillDate { get; set; }
    public TimeSpan? DrillTime { get; set; }
    public string? Scenario { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public bool WasAnnounced { get; set; }
    public int? ParticipantsCount { get; set; }
    public TimeSpan? EvacuationTime { get; set; }
    public string? Observations { get; set; }
    public string? StrengthsIdentified { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? CorrectiveActions { get; set; }
    public bool ObjectivesMet { get; set; }
    public Guid CoordinatorId { get; set; }
    public string CoordinatorName { get; set; } = string.Empty;
    public DateTime? NextDrillScheduledDate { get; set; }
    public string? DrillReportDocumentPath { get; set; }
}

public class CreateEmergencyDrillDto : CreateDtoBase
{
    [Required]
    public Guid EmergencyPlanId { get; set; }

    [Required, MaxLength(30)]
    public string DrillNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string DrillName { get; set; } = string.Empty;

    [Required]
    public DateTime DrillDate { get; set; }
    public TimeSpan? DrillTime { get; set; }

    [MaxLength(1000)]
    public string? Scenario { get; set; }

    public Guid? LocationId { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool WasAnnounced { get; set; }

    [Required]
    public Guid CoordinatorId { get; set; }

    public DateTime? NextDrillScheduledDate { get; set; }
}

public class UpdateEmergencyDrillDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string DrillName { get; set; } = string.Empty;

    [Required]
    public DateTime DrillDate { get; set; }
    public TimeSpan? DrillTime { get; set; }

    [MaxLength(1000)]
    public string? Scenario { get; set; }

    public Guid? LocationId { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool WasAnnounced { get; set; }
    public int? ParticipantsCount { get; set; }
    public TimeSpan? EvacuationTime { get; set; }

    [MaxLength(2000)]
    public string? Observations { get; set; }

    [MaxLength(1000)]
    public string? StrengthsIdentified { get; set; }

    [MaxLength(1000)]
    public string? AreasForImprovement { get; set; }

    [MaxLength(1000)]
    public string? CorrectiveActions { get; set; }

    public bool ObjectivesMet { get; set; }

    [Required]
    public Guid CoordinatorId { get; set; }

    public DateTime? NextDrillScheduledDate { get; set; }

    [MaxLength(500)]
    public string? DrillReportDocumentPath { get; set; }
}

public class EmergencyResponseTeamDto : BaseDto
{
    public Guid EmergencyPlanId { get; set; }
    public string? PlanName { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Responsibilities { get; set; }
    public DateTime? CertificateExpiryDate { get; set; }
    public string? CertificateDocumentPath { get; set; }
    public bool IsActive { get; set; }
}

public class CreateEmergencyResponseTeamDto : CreateDtoBase
{
    [Required]
    public Guid EmergencyPlanId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Responsibilities { get; set; }

    public DateTime? CertificateExpiryDate { get; set; }

    [MaxLength(200)]
    public string? CertificateDocumentPath { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateEmergencyResponseTeamDto : UpdateDtoBase
{
    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Responsibilities { get; set; }

    public DateTime? CertificateExpiryDate { get; set; }

    [MaxLength(200)]
    public string? CertificateDocumentPath { get; set; }

    public bool IsActive { get; set; } = true;
}

#endregion

// ============================================================================
// N. REGULATORY COMPLIANCE REGISTER
// ============================================================================

#region Regulatory Obligation

public class SheRegulatoryObligationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ObligationCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SheRegulatoryDomain Domain { get; set; }
    public string DomainName => Domain.ToString();
    public string? LegislationName { get; set; }
    public string? SectionOrClause { get; set; }
    public Guid? RegulatoryBodyId { get; set; }
    public string? RegulatoryBodyName { get; set; }
    public SheComplianceStatus ComplianceStatus { get; set; }
    public string ComplianceStatusName => ComplianceStatus.ToString();
    public string? ComplianceNotes { get; set; }
    public Guid? ObligationOwnerId { get; set; }
    public string? ObligationOwnerName { get; set; }
    public DateTime? LastReviewedDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public string? LastAmendmentNotes { get; set; }
    public DateTime? LastAmendmentDate { get; set; }
    public bool IsActive { get; set; }
    public List<SheRegulatoryComplianceEvidenceDto> EvidenceRecords { get; set; } = new();
}

public class SheRegulatoryObligationSummaryDto
{
    public Guid Id { get; set; }
    public string ObligationCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public SheRegulatoryDomain Domain { get; set; }
    public string DomainName => Domain.ToString();
    public string? RegulatoryBodyName { get; set; }
    public SheComplianceStatus ComplianceStatus { get; set; }
    public string ComplianceStatusName => ComplianceStatus.ToString();
    public string? ObligationOwnerName { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSheRegulatoryObligationDto : CreateDtoBase
{
    [Required, MaxLength(30)]
    public string ObligationCode { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public SheRegulatoryDomain Domain { get; set; }

    [MaxLength(200)]
    public string? LegislationName { get; set; }

    [MaxLength(100)]
    public string? SectionOrClause { get; set; }

    public Guid? RegulatoryBodyId { get; set; }

    [Required]
    public SheComplianceStatus ComplianceStatus { get; set; }

    [MaxLength(1000)]
    public string? ComplianceNotes { get; set; }

    public Guid? ObligationOwnerId { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSheRegulatoryObligationDto : UpdateDtoBase
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public SheRegulatoryDomain Domain { get; set; }

    [MaxLength(200)]
    public string? LegislationName { get; set; }

    [MaxLength(100)]
    public string? SectionOrClause { get; set; }

    public Guid? RegulatoryBodyId { get; set; }

    [Required]
    public SheComplianceStatus ComplianceStatus { get; set; }

    [MaxLength(1000)]
    public string? ComplianceNotes { get; set; }

    public Guid? ObligationOwnerId { get; set; }
    public DateTime? LastReviewedDate { get; set; }
    public DateTime? NextReviewDate { get; set; }

    [MaxLength(500)]
    public string? LastAmendmentNotes { get; set; }

    public DateTime? LastAmendmentDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SheRegulatoryComplianceEvidenceDto : BaseDto
{
    public Guid ObligationId { get; set; }
    public string EvidenceTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EvidenceDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? DocumentPath { get; set; }
    public Guid RecordedById { get; set; }
    public string RecordedByName { get; set; } = string.Empty;
}

public class CreateSheRegulatoryComplianceEvidenceDto : CreateDtoBase
{
    [Required]
    public Guid ObligationId { get; set; }

    [Required, MaxLength(200)]
    public string EvidenceTitle { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime EvidenceDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    [Required]
    public Guid RecordedById { get; set; }
}

#endregion

// ============================================================================
// O. SAFETY SIGNAGE REGISTER
// ============================================================================

#region Safety Sign

public class SafetySignDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string SignCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SheSafetySignType SignType { get; set; }
    public string SignTypeName => SignType.ToString();
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string? SpecificPosition { get; set; }
    public DateTime InstallationDate { get; set; }
    public string? Manufacturer { get; set; }
    public string? Material { get; set; }
    public bool IsPhotoluminescent { get; set; }
    public SheSafetySignStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? LastInspectionDate { get; set; }
    public DateTime? NextInspectionDate { get; set; }
    public string? InspectionNotes { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSafetySignDto : CreateDtoBase
{
    [Required, MaxLength(30)]
    public string SignCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public SheSafetySignType SignType { get; set; }

    [Required]
    public Guid LocationId { get; set; }

    [MaxLength(300)]
    public string? SpecificPosition { get; set; }

    [Required]
    public DateTime InstallationDate { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Material { get; set; }

    public bool IsPhotoluminescent { get; set; }

    [Required]
    public SheSafetySignStatus Status { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSafetySignDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public SheSafetySignType SignType { get; set; }

    [Required]
    public Guid LocationId { get; set; }

    [MaxLength(300)]
    public string? SpecificPosition { get; set; }

    [Required]
    public DateTime InstallationDate { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Material { get; set; }

    public bool IsPhotoluminescent { get; set; }

    [Required]
    public SheSafetySignStatus Status { get; set; }

    public DateTime? LastInspectionDate { get; set; }
    public DateTime? NextInspectionDate { get; set; }

    [MaxLength(500)]
    public string? InspectionNotes { get; set; }

    public bool IsActive { get; set; } = true;
}

#endregion

// ============================================================================
// P. SHE PERFORMANCE METRICS / KPIs
// ============================================================================

#region Performance Snapshot

public class ShePerformanceSnapshotDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string SnapshotNumber { get; set; } = string.Empty;
    public SheSnapshotPeriodType PeriodType { get; set; }
    public string PeriodTypeName => PeriodType.ToString();
    public int Year { get; set; }
    public int? PeriodNumber { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }

    public int TotalAccidents { get; set; }
    public int TotalIncidents { get; set; }
    public int TotalNearMisses { get; set; }
    public int TotalDangerousOccurrences { get; set; }
    public int TotalFatalities { get; set; }
    public int TotalLostTimeInjuries { get; set; }

    public decimal? LostTimeInjuryFrequencyRate { get; set; }
    public long TotalManHoursWorked { get; set; }
    public int TotalLostDays { get; set; }

    public int InspectionsPlanned { get; set; }
    public int InspectionsConducted { get; set; }
    public int InspectionsOverdue { get; set; }

    public int CorrectiveActionsIssued { get; set; }
    public int CorrectiveActionsCompleted { get; set; }
    public int CorrectiveActionsOverdue { get; set; }
    public decimal? CorrectiveActionClosureRate { get; set; }

    public int TrainingProgramsPlanned { get; set; }
    public int TrainingProgramsConducted { get; set; }
    public int TotalTrainingHours { get; set; }

    public int ContractorsOnSite { get; set; }
    public int ContractorInspectionsConducted { get; set; }
    public int ContractorNonComplianceNoticesIssued { get; set; }
    public decimal? ContractorComplianceRate { get; set; }

    public int EnvironmentalIncidents { get; set; }
    public int EnvironmentalIncidentsReportedToEpa { get; set; }

    public int EmergencyDrillsPlanned { get; set; }
    public int EmergencyDrillsConducted { get; set; }

    public decimal? PpeComplianceRate { get; set; }
    public decimal? HousekeepingComplianceRating { get; set; }

    public int RegulatoryObligationsTotal { get; set; }
    public int RegulatoryObligationsCompliant { get; set; }
    public int RegulatoryObligationsNonCompliant { get; set; }
    public int RegulatoryObligationsExpiringSoon { get; set; }

    public Guid PreparedById { get; set; }
    public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedDate { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ManagementComments { get; set; }
    public string? ReportDocumentPath { get; set; }
}

public class ShePerformanceSnapshotSummaryDto
{
    public Guid Id { get; set; }
    public string SnapshotNumber { get; set; } = string.Empty;
    public SheSnapshotPeriodType PeriodType { get; set; }
    public string PeriodTypeName => PeriodType.ToString();
    public int Year { get; set; }
    public int? PeriodNumber { get; set; }
    public string? LocationName { get; set; }
    public int TotalIncidents { get; set; }
    public int TotalLostTimeInjuries { get; set; }
    public decimal? LostTimeInjuryFrequencyRate { get; set; }
    public DateTime PreparedDate { get; set; }
}

public class CreateShePerformanceSnapshotDto : CreateDtoBase
{
    [Required, MaxLength(30)]
    public string SnapshotNumber { get; set; } = string.Empty;

    [Required]
    public SheSnapshotPeriodType PeriodType { get; set; }

    [Required, Range(2000, 2100)]
    public int Year { get; set; }

    public int? PeriodNumber { get; set; }
    public Guid? LocationId { get; set; }

    // The reported figures. Every one is hand-entered by the SHE officer — nothing computes
    // them yet (KPI computation is a later slice); the screens label them "reported".
    [Range(0, int.MaxValue)] public int TotalAccidents { get; set; }
    [Range(0, int.MaxValue)] public int TotalIncidents { get; set; }
    [Range(0, int.MaxValue)] public int TotalNearMisses { get; set; }
    [Range(0, int.MaxValue)] public int TotalDangerousOccurrences { get; set; }
    [Range(0, int.MaxValue)] public int TotalFatalities { get; set; }
    [Range(0, int.MaxValue)] public int TotalLostTimeInjuries { get; set; }

    [Range(0, double.MaxValue)] public decimal? LostTimeInjuryFrequencyRate { get; set; }
    [Range(0, long.MaxValue)] public long TotalManHoursWorked { get; set; }
    [Range(0, int.MaxValue)] public int TotalLostDays { get; set; }

    [Range(0, int.MaxValue)] public int InspectionsPlanned { get; set; }
    [Range(0, int.MaxValue)] public int InspectionsConducted { get; set; }
    [Range(0, int.MaxValue)] public int InspectionsOverdue { get; set; }

    [Range(0, int.MaxValue)] public int CorrectiveActionsIssued { get; set; }
    [Range(0, int.MaxValue)] public int CorrectiveActionsCompleted { get; set; }
    [Range(0, int.MaxValue)] public int CorrectiveActionsOverdue { get; set; }
    [Range(0, 100)] public decimal? CorrectiveActionClosureRate { get; set; }

    [Range(0, int.MaxValue)] public int TrainingProgramsPlanned { get; set; }
    [Range(0, int.MaxValue)] public int TrainingProgramsConducted { get; set; }
    [Range(0, int.MaxValue)] public int TotalTrainingHours { get; set; }

    [Range(0, int.MaxValue)] public int ContractorsOnSite { get; set; }
    [Range(0, int.MaxValue)] public int ContractorInspectionsConducted { get; set; }
    [Range(0, int.MaxValue)] public int ContractorNonComplianceNoticesIssued { get; set; }
    [Range(0, 100)] public decimal? ContractorComplianceRate { get; set; }

    [Range(0, int.MaxValue)] public int EnvironmentalIncidents { get; set; }
    [Range(0, int.MaxValue)] public int EnvironmentalIncidentsReportedToEpa { get; set; }

    [Range(0, int.MaxValue)] public int EmergencyDrillsPlanned { get; set; }
    [Range(0, int.MaxValue)] public int EmergencyDrillsConducted { get; set; }

    [Range(0, 100)] public decimal? PpeComplianceRate { get; set; }
    [Range(0, 100)] public decimal? HousekeepingComplianceRating { get; set; }

    [Range(0, int.MaxValue)] public int RegulatoryObligationsTotal { get; set; }
    [Range(0, int.MaxValue)] public int RegulatoryObligationsCompliant { get; set; }
    [Range(0, int.MaxValue)] public int RegulatoryObligationsNonCompliant { get; set; }
    [Range(0, int.MaxValue)] public int RegulatoryObligationsExpiringSoon { get; set; }

    [Required]
    public Guid PreparedById { get; set; }

    public DateTime PreparedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? ManagementComments { get; set; }

    [MaxLength(500)]
    public string? ReportDocumentPath { get; set; }
}

/// <summary>
/// Corrects the reported figures on a snapshot that has not yet been through management review.
/// The identity fields (number, period, year, location, preparer) are fixed at creation — a
/// snapshot for the wrong period is deleted and re-entered, not renamed.
/// </summary>
public class UpdateShePerformanceSnapshotDto : UpdateDtoBase
{
    [Range(0, int.MaxValue)] public int TotalAccidents { get; set; }
    [Range(0, int.MaxValue)] public int TotalIncidents { get; set; }
    [Range(0, int.MaxValue)] public int TotalNearMisses { get; set; }
    [Range(0, int.MaxValue)] public int TotalDangerousOccurrences { get; set; }
    [Range(0, int.MaxValue)] public int TotalFatalities { get; set; }
    [Range(0, int.MaxValue)] public int TotalLostTimeInjuries { get; set; }

    [Range(0, double.MaxValue)] public decimal? LostTimeInjuryFrequencyRate { get; set; }
    [Range(0, long.MaxValue)] public long TotalManHoursWorked { get; set; }
    [Range(0, int.MaxValue)] public int TotalLostDays { get; set; }

    [Range(0, int.MaxValue)] public int InspectionsPlanned { get; set; }
    [Range(0, int.MaxValue)] public int InspectionsConducted { get; set; }
    [Range(0, int.MaxValue)] public int InspectionsOverdue { get; set; }

    [Range(0, int.MaxValue)] public int CorrectiveActionsIssued { get; set; }
    [Range(0, int.MaxValue)] public int CorrectiveActionsCompleted { get; set; }
    [Range(0, int.MaxValue)] public int CorrectiveActionsOverdue { get; set; }
    [Range(0, 100)] public decimal? CorrectiveActionClosureRate { get; set; }

    [Range(0, int.MaxValue)] public int TrainingProgramsPlanned { get; set; }
    [Range(0, int.MaxValue)] public int TrainingProgramsConducted { get; set; }
    [Range(0, int.MaxValue)] public int TotalTrainingHours { get; set; }

    [Range(0, int.MaxValue)] public int ContractorsOnSite { get; set; }
    [Range(0, int.MaxValue)] public int ContractorInspectionsConducted { get; set; }
    [Range(0, int.MaxValue)] public int ContractorNonComplianceNoticesIssued { get; set; }
    [Range(0, 100)] public decimal? ContractorComplianceRate { get; set; }

    [Range(0, int.MaxValue)] public int EnvironmentalIncidents { get; set; }
    [Range(0, int.MaxValue)] public int EnvironmentalIncidentsReportedToEpa { get; set; }

    [Range(0, int.MaxValue)] public int EmergencyDrillsPlanned { get; set; }
    [Range(0, int.MaxValue)] public int EmergencyDrillsConducted { get; set; }

    [Range(0, 100)] public decimal? PpeComplianceRate { get; set; }
    [Range(0, 100)] public decimal? HousekeepingComplianceRating { get; set; }

    [Range(0, int.MaxValue)] public int RegulatoryObligationsTotal { get; set; }
    [Range(0, int.MaxValue)] public int RegulatoryObligationsCompliant { get; set; }
    [Range(0, int.MaxValue)] public int RegulatoryObligationsNonCompliant { get; set; }
    [Range(0, int.MaxValue)] public int RegulatoryObligationsExpiringSoon { get; set; }

    [MaxLength(1000)]
    public string? ManagementComments { get; set; }

    [MaxLength(500)]
    public string? ReportDocumentPath { get; set; }
}

/// <summary>Records the management review of a KPI snapshot.</summary>
public class ReviewShePerformanceSnapshotDto
{
    [Required]
    public Guid SnapshotId { get; set; }

    [Required]
    public Guid ReviewedById { get; set; }

    public DateTime ReviewedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? ManagementComments { get; set; }
}

#endregion

// ============================================================================
// Q. SAFETY COMMITTEE & MEETINGS
// ============================================================================

#region Safety Committee

public class SafetyCommitteeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string CommitteeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EstablishedDate { get; set; }
    public Guid? ChairPersonId { get; set; }
    public string? ChairPersonName { get; set; }
    public int? MeetingFrequencyDays { get; set; }
    public string? MeetingSchedule { get; set; }
    public bool IsActive { get; set; }
    public List<SafetyCommitteeMemberDto> Members { get; set; } = new();
}

public class CreateSafetyCommitteeDto : CreateDtoBase
{
    [Required, MaxLength(200)]
    public string CommitteeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime EstablishedDate { get; set; }

    public Guid? ChairPersonId { get; set; }
    public int? MeetingFrequencyDays { get; set; }

    [MaxLength(200)]
    public string? MeetingSchedule { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSafetyCommitteeDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string CommitteeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime EstablishedDate { get; set; }

    public Guid? ChairPersonId { get; set; }
    public int? MeetingFrequencyDays { get; set; }

    [MaxLength(200)]
    public string? MeetingSchedule { get; set; }

    public bool IsActive { get; set; } = true;
}

public class SafetyCommitteeMemberDto : BaseDto
{
    public Guid CommitteeId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSafetyCommitteeMemberDto : CreateDtoBase
{
    [Required]
    public Guid CommitteeId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    [Required]
    public DateTime JoinDate { get; set; }

    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSafetyCommitteeMemberDto : UpdateDtoBase
{
    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

#endregion

#region Safety Meeting

public class SafetyMeetingDto : BaseDto
{
    public Guid? CommitteeId { get; set; }
    public string? CommitteeName { get; set; }
    public string MeetingNumber { get; set; } = string.Empty;
    public DateTime MeetingDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public SheSafetyMeetingType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Agenda { get; set; }
    public string? Minutes { get; set; }
    public string? TopicsDiscussed { get; set; }
    public string? DecisionsMade { get; set; }
    public Guid? FacilitatorId { get; set; }
    public string? FacilitatorName { get; set; }
    public int? AttendeesCount { get; set; }

    public List<SafetyMeetingAttendeeDto> Attendees { get; set; } = new();
    public List<SafetyMeetingActionItemDto> ActionItems { get; set; } = new();
    public List<SafetyMeetingDocumentDto> Documents { get; set; } = new();
}

public class SafetyMeetingSummaryDto
{
    public Guid Id { get; set; }
    public string MeetingNumber { get; set; } = string.Empty;
    public string? CommitteeName { get; set; }
    public DateTime MeetingDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public SheSafetyMeetingType Type { get; set; }
    public string TypeName => Type.ToString();
    public int? AttendeesCount { get; set; }
    public int OpenActionItemCount { get; set; }
}

public class CreateSafetyMeetingDto : CreateDtoBase
{
    public Guid? CommitteeId { get; set; }

    [Required, MaxLength(30)]
    public string MeetingNumber { get; set; } = string.Empty;

    [Required]
    public DateTime MeetingDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    [Required, MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [Required]
    public SheSafetyMeetingType Type { get; set; }

    [MaxLength(3000)]
    public string? Agenda { get; set; }

    public Guid? FacilitatorId { get; set; }
}

public class UpdateSafetyMeetingDto : UpdateDtoBase
{
    public Guid? CommitteeId { get; set; }

    [Required]
    public DateTime MeetingDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    [Required, MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [Required]
    public SheSafetyMeetingType Type { get; set; }

    [MaxLength(3000)]
    public string? Agenda { get; set; }

    [MaxLength(5000)]
    public string? Minutes { get; set; }

    [MaxLength(2000)]
    public string? TopicsDiscussed { get; set; }

    [MaxLength(1000)]
    public string? DecisionsMade { get; set; }

    public Guid? FacilitatorId { get; set; }
    public int? AttendeesCount { get; set; }
}

public class SafetyMeetingAttendeeDto : BaseDto
{
    public Guid MeetingId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public bool Attended { get; set; }
    public DateTime? SignedDate { get; set; }
}

public class CreateSafetyMeetingAttendeeDto : CreateDtoBase
{
    [Required]
    public Guid MeetingId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public bool Attended { get; set; }
    public DateTime? SignedDate { get; set; }
}

public class SafetyMeetingActionItemDto : BaseDto
{
    public Guid MeetingId { get; set; }
    public string? MeetingNumber { get; set; }
    public string ActionDescription { get; set; } = string.Empty;
    public SheActionItemPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public SheActionItemStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }
}

public class CreateSafetyMeetingActionItemDto : CreateDtoBase
{
    [Required]
    public Guid MeetingId { get; set; }

    [Required, MaxLength(1000)]
    public string ActionDescription { get; set; } = string.Empty;

    [Required]
    public SheActionItemPriority Priority { get; set; }

    public Guid? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
}

public class UpdateSafetyMeetingActionItemDto : UpdateDtoBase
{
    [Required, MaxLength(1000)]
    public string ActionDescription { get; set; } = string.Empty;

    [Required]
    public SheActionItemPriority Priority { get; set; }

    [Required]
    public SheActionItemStatus Status { get; set; }

    public Guid? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }
}

public class SafetyMeetingDocumentDto : BaseDto
{
    public Guid MeetingId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

public class CreateSafetyMeetingDocumentDto : CreateDtoBase
{
    [Required]
    public Guid MeetingId { get; set; }

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }
}

#endregion

// ============================================================================
// R. RETURN-TO-WORK PLANS
// ============================================================================

#region Return-to-Work Plan

public class SheReturnToWorkPlanDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public Guid? SafetyIncidentId { get; set; }
    public string? SafetyIncidentNumber { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public DateTime PlanDate { get; set; }
    public DateTime? PlannedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public string? MedicalRestrictions { get; set; }
    public DateTime? MedicalClearanceDate { get; set; }
    public string? MedicalClearanceNotes { get; set; }
    public bool RequiresWorkplaceModifications { get; set; }
    public string? WorkplaceModificationsDescription { get; set; }
    public SheReturnToWorkStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? CoordinatorId { get; set; }
    public string? CoordinatorName { get; set; }
    public Guid? SupervisorId { get; set; }
    public string? SupervisorName { get; set; }
    public DateTime? CompletionDate { get; set; }
    public bool SuccessfullyCompleted { get; set; }
    public string? CompletionNotes { get; set; }

    public List<SheReturnToWorkPhaseDto> Phases { get; set; } = new();
    public List<SheReturnToWorkReviewDto> Reviews { get; set; } = new();
}

public class SheReturnToWorkPlanSummaryDto
{
    public Guid Id { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? SafetyIncidentNumber { get; set; }
    public DateTime PlanDate { get; set; }
    public DateTime? PlannedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public SheReturnToWorkStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool SuccessfullyCompleted { get; set; }
}

public class CreateSheReturnToWorkPlanDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? SafetyIncidentId { get; set; }

    [Required, MaxLength(30)]
    public string PlanNumber { get; set; } = string.Empty;

    [Required]
    public DateTime PlanDate { get; set; }

    public DateTime? PlannedReturnDate { get; set; }

    [MaxLength(1000)]
    public string? MedicalRestrictions { get; set; }

    public DateTime? MedicalClearanceDate { get; set; }

    [MaxLength(500)]
    public string? MedicalClearanceNotes { get; set; }

    public bool RequiresWorkplaceModifications { get; set; }

    [MaxLength(1000)]
    public string? WorkplaceModificationsDescription { get; set; }

    public Guid? CoordinatorId { get; set; }
    public Guid? SupervisorId { get; set; }
}

public class UpdateSheReturnToWorkPlanDto : UpdateDtoBase
{
    public Guid? SafetyIncidentId { get; set; }

    [Required]
    public DateTime PlanDate { get; set; }

    public DateTime? PlannedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }

    [MaxLength(1000)]
    public string? MedicalRestrictions { get; set; }

    public DateTime? MedicalClearanceDate { get; set; }

    [MaxLength(500)]
    public string? MedicalClearanceNotes { get; set; }

    public bool RequiresWorkplaceModifications { get; set; }

    [MaxLength(1000)]
    public string? WorkplaceModificationsDescription { get; set; }

    [Required]
    public SheReturnToWorkStatus Status { get; set; }

    public Guid? CoordinatorId { get; set; }
    public Guid? SupervisorId { get; set; }
    public DateTime? CompletionDate { get; set; }
    public bool SuccessfullyCompleted { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }
}

public class SheReturnToWorkPhaseDto : BaseDto
{
    public Guid ReturnToWorkPlanId { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public int PhaseNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool RequiresReducedHours { get; set; }
    public int? HoursPerDay { get; set; }
    public int? DaysPerWeek { get; set; }
    public string Duties { get; set; } = string.Empty;
    public string Restrictions { get; set; } = string.Empty;
    public DateTime AssessmentDate { get; set; }
    public Guid AssessedById { get; set; }
    public string AssessedByName { get; set; } = string.Empty;
    public string? EmployeeProgress { get; set; }
    public string? ChallengesFaced { get; set; }
    public string? AccommodationsEffectiveness { get; set; }
    public string? RecommendedAdjustments { get; set; }
    public string? EmployeeFeedback { get; set; }
    public bool PhaseCompleted { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public bool CanContinuePlan { get; set; }
    public string? CompletionNotes { get; set; }
}

public class CreateSheReturnToWorkPhaseDto : CreateDtoBase
{
    [Required]
    public Guid ReturnToWorkPlanId { get; set; }

    [Required, MaxLength(100)]
    public string PhaseName { get; set; } = string.Empty;

    public int PhaseNumber { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }
    public bool RequiresReducedHours { get; set; }
    public int? HoursPerDay { get; set; }
    public int? DaysPerWeek { get; set; }

    [Required, MaxLength(1000)]
    public string Duties { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Restrictions { get; set; } = string.Empty;

    [Required]
    public DateTime AssessmentDate { get; set; }

    [Required]
    public Guid AssessedById { get; set; }
}

public class UpdateSheReturnToWorkPhaseDto : UpdateDtoBase
{
    [Required, MaxLength(100)]
    public string PhaseName { get; set; } = string.Empty;

    public int PhaseNumber { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }
    public bool RequiresReducedHours { get; set; }
    public int? HoursPerDay { get; set; }
    public int? DaysPerWeek { get; set; }

    [Required, MaxLength(1000)]
    public string Duties { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Restrictions { get; set; } = string.Empty;

    [Required]
    public DateTime AssessmentDate { get; set; }

    [MaxLength(1000)]
    public string? EmployeeProgress { get; set; }

    [MaxLength(500)]
    public string? ChallengesFaced { get; set; }

    [MaxLength(500)]
    public string? AccommodationsEffectiveness { get; set; }

    [MaxLength(500)]
    public string? RecommendedAdjustments { get; set; }

    [MaxLength(500)]
    public string? EmployeeFeedback { get; set; }

    public bool PhaseCompleted { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public bool CanContinuePlan { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }
}

public class SheReturnToWorkReviewDto : BaseDto
{
    public Guid ReturnToWorkPlanId { get; set; }
    public DateTime ReviewDate { get; set; }
    public int ReviewNumber { get; set; }
    public string? EmployeeCondition { get; set; }
    public string? WorkProgress { get; set; }
    public string? IssuesIdentified { get; set; }
    public string? RecommendedActions { get; set; }
    public Guid ReviewedById { get; set; }
    public string ReviewedByName { get; set; } = string.Empty;
    public DateTime? NextReviewDate { get; set; }
}

public class CreateSheReturnToWorkReviewDto : CreateDtoBase
{
    [Required]
    public Guid ReturnToWorkPlanId { get; set; }

    [Required]
    public DateTime ReviewDate { get; set; }

    public int ReviewNumber { get; set; }

    [MaxLength(500)]
    public string? EmployeeCondition { get; set; }

    [MaxLength(500)]
    public string? WorkProgress { get; set; }

    [MaxLength(500)]
    public string? IssuesIdentified { get; set; }

    [MaxLength(500)]
    public string? RecommendedActions { get; set; }

    [Required]
    public Guid ReviewedById { get; set; }

    public DateTime? NextReviewDate { get; set; }
}

#endregion
