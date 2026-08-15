using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Application.HR.Extensions;

// ============================================================================
// SHE mapping — Waste (J), Environmental (K) and Occupational Health (L).
// ============================================================================

public static class SafetyEnvironmentHealthMappingExtensions
{
    // ========================================================================
    // J. WASTE MANAGEMENT
    // ========================================================================

    #region WasteType

    public static SheWasteTypeDto ToDto(this SheWasteType e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        Code = e.Code,
        Name = e.Name,
        Classification = e.Classification,
        DisposalRequirements = e.DisposalRequirements,
        RegulatoryReference = e.RegulatoryReference,
        RequiresManifest = e.RequiresManifest,
        IsActive = e.IsActive,
    };

    public static SheWasteType ToEntity(this CreateSheWasteTypeDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        Code = dto.Code,
        Name = dto.Name,
        Classification = dto.Classification,
        DisposalRequirements = dto.DisposalRequirements,
        RegulatoryReference = dto.RegulatoryReference,
        RequiresManifest = dto.RequiresManifest,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheWasteType e, UpdateSheWasteTypeDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.Classification = dto.Classification;
        e.DisposalRequirements = dto.DisposalRequirements;
        e.RegulatoryReference = dto.RegulatoryReference;
        e.RequiresManifest = dto.RequiresManifest;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region WasteDisposalRecord

    public static SheWasteDisposalRecordDto ToDto(this SheWasteDisposalRecord e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        RecordNumber = e.RecordNumber,
        WasteTypeId = e.WasteTypeId,
        WasteTypeName = e.WasteType?.Name ?? string.Empty,
        WasteClassification = e.WasteType?.Classification ?? default,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        GenerationArea = e.GenerationArea,
        StorageLocation = e.StorageLocation,
        DisposalDate = e.DisposalDate,
        Quantity = e.Quantity,
        Unit = e.Unit,
        DisposalMethod = e.DisposalMethod,
        WasteContractorId = e.WasteContractorId,
        WasteContractorName = e.WasteContractor?.CompanyName,
        ManifestNumber = e.ManifestNumber,
        DisposalSite = e.DisposalSite,
        Notes = e.Notes,
        RecordedById = e.RecordedById,
        RecordedByName = e.RecordedBy?.FullName ?? string.Empty,
        DocumentPath = e.DocumentPath,
    };

    public static SheWasteDisposalRecordSummaryDto ToSummaryDto(this SheWasteDisposalRecord e) => new()
    {
        Id = e.Id,
        RecordNumber = e.RecordNumber,
        WasteTypeName = e.WasteType?.Name ?? string.Empty,
        DisposalDate = e.DisposalDate,
        Quantity = e.Quantity,
        Unit = e.Unit,
        DisposalMethod = e.DisposalMethod,
        WasteContractorName = e.WasteContractor?.CompanyName,
    };

    public static SheWasteDisposalRecord ToEntity(this CreateSheWasteDisposalRecordDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        RecordNumber = dto.RecordNumber ?? string.Empty,
        WasteTypeId = dto.WasteTypeId,
        LocationId = dto.LocationId,
        GenerationArea = dto.GenerationArea,
        StorageLocation = dto.StorageLocation,
        DisposalDate = dto.DisposalDate,
        Quantity = dto.Quantity,
        Unit = dto.Unit,
        DisposalMethod = dto.DisposalMethod,
        WasteContractorId = dto.WasteContractorId,
        ManifestNumber = dto.ManifestNumber,
        DisposalSite = dto.DisposalSite,
        Notes = dto.Notes,
        RecordedById = dto.RecordedById,
        DocumentPath = dto.DocumentPath,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheWasteDisposalRecord e, UpdateSheWasteDisposalRecordDto dto, Guid userId)
    {
        e.WasteTypeId = dto.WasteTypeId;
        e.LocationId = dto.LocationId;
        e.GenerationArea = dto.GenerationArea;
        e.StorageLocation = dto.StorageLocation;
        e.DisposalDate = dto.DisposalDate;
        e.Quantity = dto.Quantity;
        e.Unit = dto.Unit;
        e.DisposalMethod = dto.DisposalMethod;
        e.WasteContractorId = dto.WasteContractorId;
        e.ManifestNumber = dto.ManifestNumber;
        e.DisposalSite = dto.DisposalSite;
        e.Notes = dto.Notes;
        e.DocumentPath = dto.DocumentPath;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SheWasteDisposalRecordSummaryDto> ToSummaryDtoList(this IEnumerable<SheWasteDisposalRecord> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // K. ENVIRONMENTAL MANAGEMENT
    // ========================================================================

    #region EnvironmentalIncident

    public static SheEnvironmentalIncidentDto ToDto(this SheEnvironmentalIncident e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        IncidentNumber = e.IncidentNumber,
        SafetyIncidentId = e.SafetyIncidentId,
        SafetyIncidentNumber = e.SafetyIncident?.IncidentNumber,
        Type = e.Type,
        AffectedMedia = e.AffectedMedia,
        Severity = e.Severity,
        IncidentDate = e.IncidentDate,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        SpecificArea = e.SpecificArea,
        Description = e.Description,
        SpillVolume = e.SpillVolume,
        SubstanceInvolved = e.SubstanceInvolved,
        ImmediateResponseAction = e.ImmediateResponseAction,
        ReportedToEpa = e.ReportedToEpa,
        EpaNotificationDate = e.EpaNotificationDate,
        EpaReferenceNumber = e.EpaReferenceNumber,
        Status = e.Status,
        ReportedById = e.ReportedById,
        ReportedByName = e.ReportedBy?.FullName ?? string.Empty,
        ReportedDate = e.ReportedDate,
        InvestigationFindings = e.InvestigationFindings,
        CorrectiveActions = e.CorrectiveActions,
        PreventiveActions = e.PreventiveActions,
        LessonsLearned = e.LessonsLearned,
        ClosedDate = e.ClosedDate,
        ClosedById = e.ClosedById,
        ClosedByName = e.ClosedBy?.FullName,
    };

    public static SheEnvironmentalIncidentSummaryDto ToSummaryDto(this SheEnvironmentalIncident e) => new()
    {
        Id = e.Id,
        IncidentNumber = e.IncidentNumber,
        Type = e.Type,
        AffectedMedia = e.AffectedMedia,
        Severity = e.Severity,
        IncidentDate = e.IncidentDate,
        LocationName = e.Location?.Name,
        ReportedToEpa = e.ReportedToEpa,
        Status = e.Status,
    };

    public static SheEnvironmentalIncident ToEntity(this CreateSheEnvironmentalIncidentDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        IncidentNumber = dto.IncidentNumber ?? string.Empty,
        SafetyIncidentId = dto.SafetyIncidentId,
        Type = dto.Type,
        AffectedMedia = dto.AffectedMedia,
        Severity = dto.Severity,
        IncidentDate = dto.IncidentDate,
        LocationId = dto.LocationId,
        SpecificArea = dto.SpecificArea,
        Description = dto.Description,
        SpillVolume = dto.SpillVolume,
        SubstanceInvolved = dto.SubstanceInvolved,
        ImmediateResponseAction = dto.ImmediateResponseAction,
        Status = SheEnvironmentalIncidentStatus.Reported,
        ReportedById = dto.ReportedById,
        ReportedDate = dto.ReportedDate,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheEnvironmentalIncident e, UpdateSheEnvironmentalIncidentDto dto, Guid userId)
    {
        e.SafetyIncidentId = dto.SafetyIncidentId;
        e.Type = dto.Type;
        e.AffectedMedia = dto.AffectedMedia;
        e.Severity = dto.Severity;
        e.IncidentDate = dto.IncidentDate;
        e.LocationId = dto.LocationId;
        e.SpecificArea = dto.SpecificArea;
        e.Description = dto.Description;
        e.SpillVolume = dto.SpillVolume;
        e.SubstanceInvolved = dto.SubstanceInvolved;
        e.ImmediateResponseAction = dto.ImmediateResponseAction;
        e.ReportedToEpa = dto.ReportedToEpa;
        e.EpaNotificationDate = dto.EpaNotificationDate;
        e.EpaReferenceNumber = dto.EpaReferenceNumber;
        e.Status = dto.Status;
        e.InvestigationFindings = dto.InvestigationFindings;
        e.CorrectiveActions = dto.CorrectiveActions;
        e.PreventiveActions = dto.PreventiveActions;
        e.LessonsLearned = dto.LessonsLearned;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SheEnvironmentalIncidentSummaryDto> ToSummaryDtoList(this IEnumerable<SheEnvironmentalIncident> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region EnvironmentalMonitoringRecord

    public static SheEnvironmentalMonitoringRecordDto ToDto(this SheEnvironmentalMonitoringRecord e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        RecordNumber = e.RecordNumber,
        MonitoringType = e.MonitoringType,
        ScheduleId = e.ScheduleId,
        ScheduleNumber = e.Schedule?.ScheduleNumber,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        MonitoringPoint = e.MonitoringPoint,
        MeasurementDate = e.MeasurementDate,
        MeasuredValue = e.MeasuredValue,
        Unit = e.Unit,
        RegulatoryLimit = e.RegulatoryLimit,
        ActionLevel = e.ActionLevel,
        ExceedsLimit = e.ExceedsLimit,
        ExceedsActionLevel = e.ExceedsActionLevel,
        InstrumentUsed = e.InstrumentUsed,
        WeatherConditions = e.WeatherConditions,
        MeasuredById = e.MeasuredById,
        MeasuredByName = e.MeasuredBy?.FullName ?? string.Empty,
        Comments = e.Comments,
        DocumentPath = e.DocumentPath,
    };

    public static SheEnvironmentalMonitoringRecord ToEntity(this CreateSheEnvironmentalMonitoringRecordDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        RecordNumber = dto.RecordNumber ?? string.Empty,
        MonitoringType = dto.MonitoringType,
        ScheduleId = dto.ScheduleId,
        LocationId = dto.LocationId,
        MonitoringPoint = dto.MonitoringPoint,
        MeasurementDate = dto.MeasurementDate,
        MeasuredValue = dto.MeasuredValue,
        Unit = dto.Unit,
        RegulatoryLimit = dto.RegulatoryLimit,
        ActionLevel = dto.ActionLevel,
        InstrumentUsed = dto.InstrumentUsed,
        WeatherConditions = dto.WeatherConditions,
        MeasuredById = dto.MeasuredById,
        Comments = dto.Comments,
        DocumentPath = dto.DocumentPath,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheEnvironmentalMonitoringRecord e, UpdateSheEnvironmentalMonitoringRecordDto dto, Guid userId)
    {
        e.MonitoringType = dto.MonitoringType;
        e.ScheduleId = dto.ScheduleId;
        e.LocationId = dto.LocationId;
        e.MonitoringPoint = dto.MonitoringPoint;
        e.MeasurementDate = dto.MeasurementDate;
        e.MeasuredValue = dto.MeasuredValue;
        e.Unit = dto.Unit;
        e.RegulatoryLimit = dto.RegulatoryLimit;
        e.ActionLevel = dto.ActionLevel;
        e.InstrumentUsed = dto.InstrumentUsed;
        e.WeatherConditions = dto.WeatherConditions;
        e.Comments = dto.Comments;
        e.DocumentPath = dto.DocumentPath;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // L. OCCUPATIONAL HEALTH MANAGEMENT
    // ========================================================================

    #region OccupationalHealthSurveillance

    public static SheOccupationalHealthSurveillanceDto ToDto(this SheOccupationalHealthSurveillance e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        SurveillanceNumber = e.SurveillanceNumber,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        EmployeeNumber = e.Employee?.EmployeeNumber,
        Type = e.Type,
        ExposureHazard = e.ExposureHazard,
        ExaminationDate = e.ExaminationDate,
        NextExaminationDate = e.NextExaminationDate,
        HealthcareFacilityId = e.HealthcareFacilityId,
        HealthcareFacilityName = e.HealthcareFacility?.FacilityName,
        ExaminingPhysician = e.ExaminingPhysician,
        Result = e.Result,
        Findings = e.Findings,
        Recommendations = e.Recommendations,
        WorkRestrictionIssued = e.WorkRestrictionIssued,
        WorkRestrictionDetails = e.WorkRestrictionDetails,
        DocumentPath = e.DocumentPath,
        RecordedById = e.RecordedById,
        RecordedByName = e.RecordedBy?.FullName ?? string.Empty,
    };

    public static SheOccupationalHealthSurveillanceSummaryDto ToSummaryDto(this SheOccupationalHealthSurveillance e) => new()
    {
        Id = e.Id,
        SurveillanceNumber = e.SurveillanceNumber,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        Type = e.Type,
        ExaminationDate = e.ExaminationDate,
        NextExaminationDate = e.NextExaminationDate,
        Result = e.Result,
        WorkRestrictionIssued = e.WorkRestrictionIssued,
    };

    public static SheOccupationalHealthSurveillance ToEntity(this CreateSheOccupationalHealthSurveillanceDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        SurveillanceNumber = dto.SurveillanceNumber,
        EmployeeId = dto.EmployeeId,
        Type = dto.Type,
        ExposureHazard = dto.ExposureHazard,
        ExaminationDate = dto.ExaminationDate,
        NextExaminationDate = dto.NextExaminationDate,
        HealthcareFacilityId = dto.HealthcareFacilityId,
        ExaminingPhysician = dto.ExaminingPhysician,
        Result = dto.Result,
        Findings = dto.Findings,
        Recommendations = dto.Recommendations,
        WorkRestrictionIssued = dto.WorkRestrictionIssued,
        WorkRestrictionDetails = dto.WorkRestrictionDetails,
        DocumentPath = dto.DocumentPath,
        RecordedById = dto.RecordedById,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheOccupationalHealthSurveillance e, UpdateSheOccupationalHealthSurveillanceDto dto, Guid userId)
    {
        e.Type = dto.Type;
        e.ExposureHazard = dto.ExposureHazard;
        e.ExaminationDate = dto.ExaminationDate;
        e.NextExaminationDate = dto.NextExaminationDate;
        e.HealthcareFacilityId = dto.HealthcareFacilityId;
        e.ExaminingPhysician = dto.ExaminingPhysician;
        e.Result = dto.Result;
        e.Findings = dto.Findings;
        e.Recommendations = dto.Recommendations;
        e.WorkRestrictionIssued = dto.WorkRestrictionIssued;
        e.WorkRestrictionDetails = dto.WorkRestrictionDetails;
        e.DocumentPath = dto.DocumentPath;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SheOccupationalHealthSurveillanceSummaryDto> ToSummaryDtoList(this IEnumerable<SheOccupationalHealthSurveillance> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region FirstAidStation

    public static SheFirstAidStationDto ToDto(this SheFirstAidStation e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        StationCode = e.StationCode,
        Name = e.Name,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name ?? string.Empty,
        SpecificArea = e.SpecificArea,
        Type = e.Type,
        ResponsibleAiderId = e.ResponsibleAiderId,
        ResponsibleAiderName = e.ResponsibleAider?.FullName,
        LastInspectionDate = e.LastInspectionDate,
        NextInspectionDate = e.NextInspectionDate,
        IsFullyStocked = e.IsFullyStocked,
        StockingDeficiencies = e.StockingDeficiencies,
        IsActive = e.IsActive,
        Notes = e.Notes,
    };

    public static SheFirstAidStation ToEntity(this CreateSheFirstAidStationDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        StationCode = dto.StationCode,
        Name = dto.Name,
        LocationId = dto.LocationId,
        SpecificArea = dto.SpecificArea,
        Type = dto.Type,
        ResponsibleAiderId = dto.ResponsibleAiderId,
        IsFullyStocked = dto.IsFullyStocked,
        IsActive = dto.IsActive,
        Notes = dto.Notes,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheFirstAidStation e, UpdateSheFirstAidStationDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.LocationId = dto.LocationId;
        e.SpecificArea = dto.SpecificArea;
        e.Type = dto.Type;
        e.ResponsibleAiderId = dto.ResponsibleAiderId;
        e.LastInspectionDate = dto.LastInspectionDate;
        e.NextInspectionDate = dto.NextInspectionDate;
        e.IsFullyStocked = dto.IsFullyStocked;
        e.StockingDeficiencies = dto.StockingDeficiencies;
        e.IsActive = dto.IsActive;
        e.Notes = dto.Notes;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region WellnessProgram

    public static SheWellnessProgramDto ToDto(this SheWellnessProgram e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ProgramCode = e.ProgramCode,
        Title = e.Title,
        Description = e.Description,
        Type = e.Type,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        CoordinatorId = e.CoordinatorId,
        CoordinatorName = e.Coordinator?.FullName,
        Status = e.Status,
        ParticipantsCount = e.ParticipantsCount,
        Outcomes = e.Outcomes,
        IsActive = e.IsActive,
    };

    public static SheWellnessProgram ToEntity(this CreateSheWellnessProgramDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ProgramCode = dto.ProgramCode,
        Title = dto.Title,
        Description = dto.Description,
        Type = dto.Type,
        StartDate = dto.StartDate,
        EndDate = dto.EndDate,
        CoordinatorId = dto.CoordinatorId,
        Status = SheWellnessProgramStatus.Planned,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheWellnessProgram e, UpdateSheWellnessProgramDto dto, Guid userId)
    {
        e.Title = dto.Title;
        e.Description = dto.Description;
        e.Type = dto.Type;
        e.StartDate = dto.StartDate;
        e.EndDate = dto.EndDate;
        e.CoordinatorId = dto.CoordinatorId;
        e.Status = dto.Status;
        e.ParticipantsCount = dto.ParticipantsCount;
        e.Outcomes = dto.Outcomes;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion
}
