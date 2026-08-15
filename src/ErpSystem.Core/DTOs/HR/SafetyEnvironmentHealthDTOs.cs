using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SHE — WASTE, ENVIRONMENTAL & OCCUPATIONAL HEALTH DTOs
// Domains: J. Waste Management   K. Environmental Management
//          L. Occupational Health Management
// ============================================================================

// ============================================================================
// J. WASTE MANAGEMENT
// ============================================================================

#region Waste Type

public class SheWasteTypeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SheWasteClassification Classification { get; set; }
    public string ClassificationName => Classification.ToString();
    public string? DisposalRequirements { get; set; }
    public string? RegulatoryReference { get; set; }
    public bool RequiresManifest { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSheWasteTypeDto : CreateDtoBase
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public SheWasteClassification Classification { get; set; }

    [MaxLength(500)]
    public string? DisposalRequirements { get; set; }

    [MaxLength(200)]
    public string? RegulatoryReference { get; set; }

    public bool RequiresManifest { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSheWasteTypeDto : UpdateDtoBase
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public SheWasteClassification Classification { get; set; }

    [MaxLength(500)]
    public string? DisposalRequirements { get; set; }

    [MaxLength(200)]
    public string? RegulatoryReference { get; set; }

    public bool RequiresManifest { get; set; }
    public bool IsActive { get; set; } = true;
}

#endregion

#region Waste Disposal Record

public class SheWasteDisposalRecordDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RecordNumber { get; set; } = string.Empty;
    public Guid WasteTypeId { get; set; }
    public string WasteTypeName { get; set; } = string.Empty;
    public SheWasteClassification WasteClassification { get; set; }
    public string WasteClassificationName => WasteClassification.ToString();
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? GenerationArea { get; set; }
    public string? StorageLocation { get; set; }
    public DateTime DisposalDate { get; set; }
    public decimal Quantity { get; set; }
    public SheWasteMeasurementUnit Unit { get; set; }
    public string UnitName => Unit.ToString();
    public SheWasteDisposalMethod DisposalMethod { get; set; }
    public string DisposalMethodName => DisposalMethod.ToString();
    public Guid? WasteContractorId { get; set; }
    public string? WasteContractorName { get; set; }
    public string? ManifestNumber { get; set; }
    public string? DisposalSite { get; set; }
    public string? Notes { get; set; }
    public Guid RecordedById { get; set; }
    public string RecordedByName { get; set; } = string.Empty;
    public string? DocumentPath { get; set; }
}

public class SheWasteDisposalRecordSummaryDto
{
    public Guid Id { get; set; }
    public string RecordNumber { get; set; } = string.Empty;
    public string WasteTypeName { get; set; } = string.Empty;
    public DateTime DisposalDate { get; set; }
    public decimal Quantity { get; set; }
    public SheWasteMeasurementUnit Unit { get; set; }
    public string UnitName => Unit.ToString();
    public SheWasteDisposalMethod DisposalMethod { get; set; }
    public string DisposalMethodName => DisposalMethod.ToString();
    public string? WasteContractorName { get; set; }
}

public class CreateSheWasteDisposalRecordDto : CreateDtoBase
{
    /// <summary>Optional — left blank, the server assigns the next number in sequence.</summary>
    [MaxLength(30)]
    public string? RecordNumber { get; set; }

    [Required]
    public Guid WasteTypeId { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? GenerationArea { get; set; }

    /// <summary>On-site interim storage before disposal (FR-ENV-020).</summary>
    [MaxLength(200)]
    public string? StorageLocation { get; set; }

    [Required]
    public DateTime DisposalDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Required]
    public SheWasteMeasurementUnit Unit { get; set; }

    [Required]
    public SheWasteDisposalMethod DisposalMethod { get; set; }

    public Guid? WasteContractorId { get; set; }

    [MaxLength(100)]
    public string? ManifestNumber { get; set; }

    [MaxLength(500)]
    public string? DisposalSite { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    public Guid RecordedById { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}

public class UpdateSheWasteDisposalRecordDto : UpdateDtoBase
{
    [Required]
    public Guid WasteTypeId { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? GenerationArea { get; set; }

    /// <summary>On-site interim storage before disposal (FR-ENV-020).</summary>
    [MaxLength(200)]
    public string? StorageLocation { get; set; }

    [Required]
    public DateTime DisposalDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Required]
    public SheWasteMeasurementUnit Unit { get; set; }

    [Required]
    public SheWasteDisposalMethod DisposalMethod { get; set; }

    public Guid? WasteContractorId { get; set; }

    [MaxLength(100)]
    public string? ManifestNumber { get; set; }

    [MaxLength(500)]
    public string? DisposalSite { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}

#endregion

// ============================================================================
// K. ENVIRONMENTAL MANAGEMENT
// ============================================================================

#region Environmental Incident

public class SheEnvironmentalIncidentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public Guid? SafetyIncidentId { get; set; }
    public string? SafetyIncidentNumber { get; set; }
    public SheEnvironmentalIncidentType Type { get; set; }
    public string TypeName => Type.ToString();
    public SheEnvironmentalMedia AffectedMedia { get; set; }
    public string AffectedMediaName => AffectedMedia.ToString();
    public SheIncidentSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public DateTime IncidentDate { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? SpecificArea { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? SpillVolume { get; set; }
    public string? SubstanceInvolved { get; set; }
    public string? ImmediateResponseAction { get; set; }
    public bool ReportedToEpa { get; set; }
    public DateTime? EpaNotificationDate { get; set; }
    public string? EpaReferenceNumber { get; set; }
    public SheEnvironmentalIncidentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid ReportedById { get; set; }
    public string ReportedByName { get; set; } = string.Empty;
    public DateTime ReportedDate { get; set; }
    public string? InvestigationFindings { get; set; }
    public string? CorrectiveActions { get; set; }
    public string? PreventiveActions { get; set; }
    public string? LessonsLearned { get; set; }
    public DateTime? ClosedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
}

public class SheEnvironmentalIncidentSummaryDto
{
    public Guid Id { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public SheEnvironmentalIncidentType Type { get; set; }
    public string TypeName => Type.ToString();
    public SheEnvironmentalMedia AffectedMedia { get; set; }
    public string AffectedMediaName => AffectedMedia.ToString();
    public SheIncidentSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public DateTime IncidentDate { get; set; }
    public string? LocationName { get; set; }
    public bool ReportedToEpa { get; set; }
    public SheEnvironmentalIncidentStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class CreateSheEnvironmentalIncidentDto : CreateDtoBase
{
    /// <summary>Optional — left blank, the server assigns the next number in sequence.</summary>
    [MaxLength(30)]
    public string? IncidentNumber { get; set; }

    public Guid? SafetyIncidentId { get; set; }

    [Required]
    public SheEnvironmentalIncidentType Type { get; set; }

    [Required]
    public SheEnvironmentalMedia AffectedMedia { get; set; }

    [Required]
    public SheIncidentSeverity Severity { get; set; }

    [Required]
    public DateTime IncidentDate { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Required, MaxLength(3000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? SpillVolume { get; set; }

    [MaxLength(500)]
    public string? SubstanceInvolved { get; set; }

    [MaxLength(1000)]
    public string? ImmediateResponseAction { get; set; }

    [Required]
    public Guid ReportedById { get; set; }

    public DateTime ReportedDate { get; set; } = DateTime.UtcNow;
}

public class UpdateSheEnvironmentalIncidentDto : UpdateDtoBase
{
    public Guid? SafetyIncidentId { get; set; }

    [Required]
    public SheEnvironmentalIncidentType Type { get; set; }

    [Required]
    public SheEnvironmentalMedia AffectedMedia { get; set; }

    [Required]
    public SheIncidentSeverity Severity { get; set; }

    [Required]
    public DateTime IncidentDate { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Required, MaxLength(3000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? SpillVolume { get; set; }

    [MaxLength(500)]
    public string? SubstanceInvolved { get; set; }

    [MaxLength(1000)]
    public string? ImmediateResponseAction { get; set; }

    public bool ReportedToEpa { get; set; }
    public DateTime? EpaNotificationDate { get; set; }

    [MaxLength(100)]
    public string? EpaReferenceNumber { get; set; }

    [Required]
    public SheEnvironmentalIncidentStatus Status { get; set; }

    [MaxLength(2000)]
    public string? InvestigationFindings { get; set; }

    [MaxLength(2000)]
    public string? CorrectiveActions { get; set; }

    [MaxLength(2000)]
    public string? PreventiveActions { get; set; }

    [MaxLength(2000)]
    public string? LessonsLearned { get; set; }
}

/// <summary>Closes an environmental incident.</summary>
public class CloseSheEnvironmentalIncidentDto
{
    [Required]
    public Guid IncidentId { get; set; }

    [Required]
    public Guid ClosedById { get; set; }

    public DateTime ClosedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string? CorrectiveActions { get; set; }

    [MaxLength(2000)]
    public string? PreventiveActions { get; set; }

    /// <summary>FR-ENV-027 — captured at close-out.</summary>
    [MaxLength(2000)]
    public string? LessonsLearned { get; set; }
}

#endregion

#region Environmental Monitoring Record

public class SheEnvironmentalMonitoringRecordDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RecordNumber { get; set; } = string.Empty;
    public SheEnvironmentalMonitoringType MonitoringType { get; set; }
    public string MonitoringTypeName => MonitoringType.ToString();
    /// <summary>Set when the record completed a scheduled monitoring activity (FR-ENV-023).</summary>
    public Guid? ScheduleId { get; set; }
    public string? ScheduleNumber { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? MonitoringPoint { get; set; }
    public DateTime MeasurementDate { get; set; }
    public decimal MeasuredValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? RegulatoryLimit { get; set; }
    public decimal? ActionLevel { get; set; }
    public bool ExceedsLimit { get; set; }
    public bool ExceedsActionLevel { get; set; }
    public string? InstrumentUsed { get; set; }
    public string? WeatherConditions { get; set; }
    public Guid MeasuredById { get; set; }
    public string MeasuredByName { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public string? DocumentPath { get; set; }
}

public class CreateSheEnvironmentalMonitoringRecordDto : CreateDtoBase
{
    /// <summary>Optional — left blank, the server assigns the next number in sequence.</summary>
    [MaxLength(30)]
    public string? RecordNumber { get; set; }

    [Required]
    public SheEnvironmentalMonitoringType MonitoringType { get; set; }

    /// <summary>Optional — links the record to the schedule cycle it satisfies (FR-ENV-023/024).</summary>
    public Guid? ScheduleId { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? MonitoringPoint { get; set; }

    [Required]
    public DateTime MeasurementDate { get; set; }

    public decimal MeasuredValue { get; set; }

    [Required, MaxLength(30)]
    public string Unit { get; set; } = string.Empty;

    public decimal? RegulatoryLimit { get; set; }
    public decimal? ActionLevel { get; set; }

    [MaxLength(500)]
    public string? InstrumentUsed { get; set; }

    [MaxLength(200)]
    public string? WeatherConditions { get; set; }

    [Required]
    public Guid MeasuredById { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}

public class UpdateSheEnvironmentalMonitoringRecordDto : UpdateDtoBase
{
    [Required]
    public SheEnvironmentalMonitoringType MonitoringType { get; set; }

    /// <summary>Optional — links the record to the schedule cycle it satisfies (FR-ENV-023/024).</summary>
    public Guid? ScheduleId { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(200)]
    public string? MonitoringPoint { get; set; }

    [Required]
    public DateTime MeasurementDate { get; set; }

    public decimal MeasuredValue { get; set; }

    [Required, MaxLength(30)]
    public string Unit { get; set; } = string.Empty;

    public decimal? RegulatoryLimit { get; set; }
    public decimal? ActionLevel { get; set; }

    [MaxLength(500)]
    public string? InstrumentUsed { get; set; }

    [MaxLength(200)]
    public string? WeatherConditions { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}

#endregion

// ============================================================================
// L. OCCUPATIONAL HEALTH MANAGEMENT
// ============================================================================

#region Occupational Health Surveillance

public class SheOccupationalHealthSurveillanceDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string SurveillanceNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public SheHealthSurveillanceType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? ExposureHazard { get; set; }
    public DateTime ExaminationDate { get; set; }
    public DateTime? NextExaminationDate { get; set; }
    public Guid? HealthcareFacilityId { get; set; }
    public string? HealthcareFacilityName { get; set; }
    public string? ExaminingPhysician { get; set; }
    public SheHealthSurveillanceResult Result { get; set; }
    public string ResultName => Result.ToString();
    public string? Findings { get; set; }
    public string? Recommendations { get; set; }
    public bool WorkRestrictionIssued { get; set; }
    public string? WorkRestrictionDetails { get; set; }
    public string? DocumentPath { get; set; }
    public Guid RecordedById { get; set; }
    public string RecordedByName { get; set; } = string.Empty;
}

public class SheOccupationalHealthSurveillanceSummaryDto
{
    public Guid Id { get; set; }
    public string SurveillanceNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public SheHealthSurveillanceType Type { get; set; }
    public string TypeName => Type.ToString();
    public DateTime ExaminationDate { get; set; }
    public DateTime? NextExaminationDate { get; set; }
    public SheHealthSurveillanceResult Result { get; set; }
    public string ResultName => Result.ToString();
    public bool WorkRestrictionIssued { get; set; }
}

public class CreateSheOccupationalHealthSurveillanceDto : CreateDtoBase
{
    [Required, MaxLength(30)]
    public string SurveillanceNumber { get; set; } = string.Empty;

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public SheHealthSurveillanceType Type { get; set; }

    [MaxLength(300)]
    public string? ExposureHazard { get; set; }

    [Required]
    public DateTime ExaminationDate { get; set; }

    public DateTime? NextExaminationDate { get; set; }
    public Guid? HealthcareFacilityId { get; set; }

    [MaxLength(200)]
    public string? ExaminingPhysician { get; set; }

    [Required]
    public SheHealthSurveillanceResult Result { get; set; }

    [MaxLength(2000)]
    public string? Findings { get; set; }

    [MaxLength(1000)]
    public string? Recommendations { get; set; }

    public bool WorkRestrictionIssued { get; set; }

    [MaxLength(500)]
    public string? WorkRestrictionDetails { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    [Required]
    public Guid RecordedById { get; set; }
}

public class UpdateSheOccupationalHealthSurveillanceDto : UpdateDtoBase
{
    [Required]
    public SheHealthSurveillanceType Type { get; set; }

    [MaxLength(300)]
    public string? ExposureHazard { get; set; }

    [Required]
    public DateTime ExaminationDate { get; set; }

    public DateTime? NextExaminationDate { get; set; }
    public Guid? HealthcareFacilityId { get; set; }

    [MaxLength(200)]
    public string? ExaminingPhysician { get; set; }

    [Required]
    public SheHealthSurveillanceResult Result { get; set; }

    [MaxLength(2000)]
    public string? Findings { get; set; }

    [MaxLength(1000)]
    public string? Recommendations { get; set; }

    public bool WorkRestrictionIssued { get; set; }

    [MaxLength(500)]
    public string? WorkRestrictionDetails { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}

#endregion

#region First Aid Station

public class SheFirstAidStationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string? SpecificArea { get; set; }
    public SheFirstAidStationType Type { get; set; }
    public string TypeName => Type.ToString();
    public Guid? ResponsibleAiderId { get; set; }
    public string? ResponsibleAiderName { get; set; }
    public DateTime? LastInspectionDate { get; set; }
    public DateTime? NextInspectionDate { get; set; }
    public bool IsFullyStocked { get; set; }
    public string? StockingDeficiencies { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class CreateSheFirstAidStationDto : CreateDtoBase
{
    [Required, MaxLength(30)]
    public string StationCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Required]
    public SheFirstAidStationType Type { get; set; }

    public Guid? ResponsibleAiderId { get; set; }
    public bool IsFullyStocked { get; set; } = true;
    public bool IsActive { get; set; } = true;

    [MaxLength(300)]
    public string? Notes { get; set; }
}

public class UpdateSheFirstAidStationDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid LocationId { get; set; }

    [MaxLength(200)]
    public string? SpecificArea { get; set; }

    [Required]
    public SheFirstAidStationType Type { get; set; }

    public Guid? ResponsibleAiderId { get; set; }
    public DateTime? LastInspectionDate { get; set; }
    public DateTime? NextInspectionDate { get; set; }
    public bool IsFullyStocked { get; set; }

    [MaxLength(500)]
    public string? StockingDeficiencies { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(300)]
    public string? Notes { get; set; }
}

#endregion

#region Wellness Program

public class SheWellnessProgramDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SheWellnessProgramType Type { get; set; }
    public string TypeName => Type.ToString();
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? CoordinatorId { get; set; }
    public string? CoordinatorName { get; set; }
    public SheWellnessProgramStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int? ParticipantsCount { get; set; }
    public string? Outcomes { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSheWellnessProgramDto : CreateDtoBase
{
    [Required, MaxLength(30)]
    public string ProgramCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public SheWellnessProgramType Type { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }
    public Guid? CoordinatorId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSheWellnessProgramDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public SheWellnessProgramType Type { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }
    public Guid? CoordinatorId { get; set; }

    [Required]
    public SheWellnessProgramStatus Status { get; set; }

    public int? ParticipantsCount { get; set; }

    [MaxLength(1000)]
    public string? Outcomes { get; set; }

    public bool IsActive { get; set; } = true;
}

#endregion
