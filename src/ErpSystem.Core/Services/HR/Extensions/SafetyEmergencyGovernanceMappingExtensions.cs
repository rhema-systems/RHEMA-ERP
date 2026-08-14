using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Application.HR.Extensions;

// ============================================================================
// SHE mapping — Emergency (M), Regulatory (N), Signage (O), KPI (P),
// Committee & Meetings (Q) and Return-to-Work (R).
// ============================================================================

public static class SafetyEmergencyGovernanceMappingExtensions
{
    // ========================================================================
    // M. EMERGENCY PREPAREDNESS & RESPONSE
    // ========================================================================

    #region EmergencyPlan

    public static EmergencyPlanDto ToDto(this EmergencyPlan e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        PlanNumber = e.PlanNumber,
        PlanName = e.PlanName,
        Type = e.Type,
        Description = e.Description,
        Procedures = e.Procedures,
        EvacuationRouteDocumentPath = e.EvacuationRouteDocumentPath,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        LastReviewed = e.LastReviewed,
        NextReviewDate = e.NextReviewDate,
        PlanOwnerId = e.PlanOwnerId,
        PlanOwnerName = e.PlanOwner?.FullName ?? string.Empty,
        DocumentPath = e.DocumentPath,
        IsActive = e.IsActive,
        AssemblyPoints = e.AssemblyPoints.Select(a => a.ToDto()).ToList(),
        EmergencyContacts = e.EmergencyContacts.Select(c => c.ToDto()).ToList(),
        Drills = e.Drills.Select(d => d.ToDto()).ToList(),
        TeamMembers = e.TeamMembers.Select(t => t.ToDto()).ToList(),
    };

    public static EmergencyPlanSummaryDto ToSummaryDto(this EmergencyPlan e) => new()
    {
        Id = e.Id,
        PlanNumber = e.PlanNumber,
        PlanName = e.PlanName,
        Type = e.Type,
        LocationName = e.Location?.Name,
        PlanOwnerName = e.PlanOwner?.FullName ?? string.Empty,
        NextReviewDate = e.NextReviewDate,
        IsActive = e.IsActive,
    };

    public static EmergencyPlan ToEntity(this CreateEmergencyPlanDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        PlanNumber = dto.PlanNumber,
        PlanName = dto.PlanName,
        Type = dto.Type,
        Description = dto.Description,
        Procedures = dto.Procedures,
        EvacuationRouteDocumentPath = dto.EvacuationRouteDocumentPath,
        LocationId = dto.LocationId,
        LastReviewed = dto.LastReviewed,
        NextReviewDate = dto.NextReviewDate,
        PlanOwnerId = dto.PlanOwnerId,
        DocumentPath = dto.DocumentPath,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this EmergencyPlan e, UpdateEmergencyPlanDto dto, Guid userId)
    {
        e.PlanName = dto.PlanName;
        e.Type = dto.Type;
        e.Description = dto.Description;
        e.Procedures = dto.Procedures;
        e.EvacuationRouteDocumentPath = dto.EvacuationRouteDocumentPath;
        e.LocationId = dto.LocationId;
        e.LastReviewed = dto.LastReviewed;
        e.NextReviewDate = dto.NextReviewDate;
        e.PlanOwnerId = dto.PlanOwnerId;
        e.DocumentPath = dto.DocumentPath;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmergencyPlanSummaryDto> ToSummaryDtoList(this IEnumerable<EmergencyPlan> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SheAssemblyPointDto ToDto(this SheAssemblyPoint e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        EmergencyPlanId = e.EmergencyPlanId,
        Name = e.Name,
        Description = e.Description,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        SpecificArea = e.SpecificArea,
        GpsLatitude = e.GpsLatitude,
        GpsLongitude = e.GpsLongitude,
        Capacity = e.Capacity,
        IsActive = e.IsActive,
    };

    public static SheAssemblyPoint ToEntity(this CreateSheAssemblyPointDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        EmergencyPlanId = dto.EmergencyPlanId,
        Name = dto.Name,
        Description = dto.Description,
        LocationId = dto.LocationId,
        SpecificArea = dto.SpecificArea,
        GpsLatitude = dto.GpsLatitude,
        GpsLongitude = dto.GpsLongitude,
        Capacity = dto.Capacity,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheAssemblyPoint e, UpdateSheAssemblyPointDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.Description = dto.Description;
        e.LocationId = dto.LocationId;
        e.SpecificArea = dto.SpecificArea;
        e.GpsLatitude = dto.GpsLatitude;
        e.GpsLongitude = dto.GpsLongitude;
        e.Capacity = dto.Capacity;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static EmergencyContactDto ToDto(this EmergencyContact e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        EmergencyPlanId = e.EmergencyPlanId,
        Name = e.Name,
        Role = e.Role,
        PrimaryPhone = e.PrimaryPhone,
        AlternatePhone = e.AlternatePhone,
        Email = e.Email,
        IsExternal = e.IsExternal,
        DisplayOrder = e.DisplayOrder,
        IsActive = e.IsActive,
    };

    public static EmergencyContact ToEntity(this CreateEmergencyContactDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        EmergencyPlanId = dto.EmergencyPlanId,
        Name = dto.Name,
        Role = dto.Role,
        PrimaryPhone = dto.PrimaryPhone,
        AlternatePhone = dto.AlternatePhone,
        Email = dto.Email,
        IsExternal = dto.IsExternal,
        DisplayOrder = dto.DisplayOrder,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this EmergencyContact e, UpdateEmergencyContactDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.Role = dto.Role;
        e.PrimaryPhone = dto.PrimaryPhone;
        e.AlternatePhone = dto.AlternatePhone;
        e.Email = dto.Email;
        e.IsExternal = dto.IsExternal;
        e.DisplayOrder = dto.DisplayOrder;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static EmergencyDrillDto ToDto(this EmergencyDrill e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        EmergencyPlanId = e.EmergencyPlanId,
        DrillNumber = e.DrillNumber,
        DrillName = e.DrillName,
        DrillDate = e.DrillDate,
        DrillTime = e.DrillTime,
        Scenario = e.Scenario,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        DepartmentId = e.DepartmentId,
        DepartmentName = e.Department?.Name,
        WasAnnounced = e.WasAnnounced,
        ParticipantsCount = e.ParticipantsCount,
        EvacuationTime = e.EvacuationTime,
        Observations = e.Observations,
        StrengthsIdentified = e.StrengthsIdentified,
        AreasForImprovement = e.AreasForImprovement,
        CorrectiveActions = e.CorrectiveActions,
        ObjectivesMet = e.ObjectivesMet,
        CoordinatorId = e.CoordinatorId,
        CoordinatorName = e.Coordinator?.FullName ?? string.Empty,
        NextDrillScheduledDate = e.NextDrillScheduledDate,
        DrillReportDocumentPath = e.DrillReportDocumentPath,
    };

    public static EmergencyDrill ToEntity(this CreateEmergencyDrillDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        EmergencyPlanId = dto.EmergencyPlanId,
        DrillNumber = dto.DrillNumber,
        DrillName = dto.DrillName,
        DrillDate = dto.DrillDate,
        DrillTime = dto.DrillTime,
        Scenario = dto.Scenario,
        LocationId = dto.LocationId,
        DepartmentId = dto.DepartmentId,
        WasAnnounced = dto.WasAnnounced,
        CoordinatorId = dto.CoordinatorId,
        NextDrillScheduledDate = dto.NextDrillScheduledDate,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this EmergencyDrill e, UpdateEmergencyDrillDto dto, Guid userId)
    {
        e.DrillName = dto.DrillName;
        e.DrillDate = dto.DrillDate;
        e.DrillTime = dto.DrillTime;
        e.Scenario = dto.Scenario;
        e.LocationId = dto.LocationId;
        e.DepartmentId = dto.DepartmentId;
        e.WasAnnounced = dto.WasAnnounced;
        e.ParticipantsCount = dto.ParticipantsCount;
        e.EvacuationTime = dto.EvacuationTime;
        e.Observations = dto.Observations;
        e.StrengthsIdentified = dto.StrengthsIdentified;
        e.AreasForImprovement = dto.AreasForImprovement;
        e.CorrectiveActions = dto.CorrectiveActions;
        e.ObjectivesMet = dto.ObjectivesMet;
        e.CoordinatorId = dto.CoordinatorId;
        e.NextDrillScheduledDate = dto.NextDrillScheduledDate;
        e.DrillReportDocumentPath = dto.DrillReportDocumentPath;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static EmergencyResponseTeamDto ToDto(this EmergencyResponseTeam e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        EmergencyPlanId = e.EmergencyPlanId,
        PlanName = e.EmergencyPlan?.PlanName,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        Role = e.Role,
        Responsibilities = e.Responsibilities,
        CertificateExpiryDate = e.CertificateExpiryDate,
        CertificateDocumentPath = e.CertificateDocumentPath,
        IsActive = e.IsActive,
    };

    public static EmergencyResponseTeam ToEntity(this CreateEmergencyResponseTeamDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        EmergencyPlanId = dto.EmergencyPlanId,
        EmployeeId = dto.EmployeeId,
        Role = dto.Role,
        Responsibilities = dto.Responsibilities,
        CertificateExpiryDate = dto.CertificateExpiryDate,
        CertificateDocumentPath = dto.CertificateDocumentPath,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this EmergencyResponseTeam e, UpdateEmergencyResponseTeamDto dto, Guid userId)
    {
        e.Role = dto.Role;
        e.Responsibilities = dto.Responsibilities;
        e.CertificateExpiryDate = dto.CertificateExpiryDate;
        e.CertificateDocumentPath = dto.CertificateDocumentPath;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // N. REGULATORY COMPLIANCE REGISTER
    // ========================================================================

    #region RegulatoryObligation

    public static SheRegulatoryObligationDto ToDto(this SheRegulatoryObligation e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ObligationCode = e.ObligationCode,
        Title = e.Title,
        Description = e.Description,
        Domain = e.Domain,
        LegislationName = e.LegislationName,
        SectionOrClause = e.SectionOrClause,
        RegulatoryBodyId = e.RegulatoryBodyId,
        RegulatoryBodyName = e.RegulatoryBody?.Name,
        ComplianceStatus = e.ComplianceStatus,
        ComplianceNotes = e.ComplianceNotes,
        ObligationOwnerId = e.ObligationOwnerId,
        ObligationOwnerName = e.ObligationOwner?.FullName,
        LastReviewedDate = e.LastReviewedDate,
        NextReviewDate = e.NextReviewDate,
        LastAmendmentNotes = e.LastAmendmentNotes,
        LastAmendmentDate = e.LastAmendmentDate,
        IsActive = e.IsActive,
        EvidenceRecords = e.EvidenceRecords.Select(r => r.ToDto()).ToList(),
    };

    public static SheRegulatoryObligationSummaryDto ToSummaryDto(this SheRegulatoryObligation e) => new()
    {
        Id = e.Id,
        ObligationCode = e.ObligationCode,
        Title = e.Title,
        Domain = e.Domain,
        RegulatoryBodyName = e.RegulatoryBody?.Name,
        ComplianceStatus = e.ComplianceStatus,
        ObligationOwnerName = e.ObligationOwner?.FullName,
        NextReviewDate = e.NextReviewDate,
        IsActive = e.IsActive,
    };

    public static SheRegulatoryObligation ToEntity(this CreateSheRegulatoryObligationDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ObligationCode = dto.ObligationCode,
        Title = dto.Title,
        Description = dto.Description,
        Domain = dto.Domain,
        LegislationName = dto.LegislationName,
        SectionOrClause = dto.SectionOrClause,
        RegulatoryBodyId = dto.RegulatoryBodyId,
        ComplianceStatus = dto.ComplianceStatus,
        ComplianceNotes = dto.ComplianceNotes,
        ObligationOwnerId = dto.ObligationOwnerId,
        NextReviewDate = dto.NextReviewDate,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheRegulatoryObligation e, UpdateSheRegulatoryObligationDto dto, Guid userId)
    {
        e.Title = dto.Title;
        e.Description = dto.Description;
        e.Domain = dto.Domain;
        e.LegislationName = dto.LegislationName;
        e.SectionOrClause = dto.SectionOrClause;
        e.RegulatoryBodyId = dto.RegulatoryBodyId;
        e.ComplianceStatus = dto.ComplianceStatus;
        e.ComplianceNotes = dto.ComplianceNotes;
        e.ObligationOwnerId = dto.ObligationOwnerId;
        e.LastReviewedDate = dto.LastReviewedDate;
        e.NextReviewDate = dto.NextReviewDate;
        e.LastAmendmentNotes = dto.LastAmendmentNotes;
        e.LastAmendmentDate = dto.LastAmendmentDate;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SheRegulatoryObligationSummaryDto> ToSummaryDtoList(this IEnumerable<SheRegulatoryObligation> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SheRegulatoryComplianceEvidenceDto ToDto(this SheRegulatoryComplianceEvidence e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ObligationId = e.ObligationId,
        EvidenceTitle = e.EvidenceTitle,
        Description = e.Description,
        EvidenceDate = e.EvidenceDate,
        ExpiryDate = e.ExpiryDate,
        DocumentPath = e.DocumentPath,
        RecordedById = e.RecordedById,
        RecordedByName = e.RecordedBy?.FullName ?? string.Empty,
    };

    public static SheRegulatoryComplianceEvidence ToEntity(this CreateSheRegulatoryComplianceEvidenceDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ObligationId = dto.ObligationId,
        EvidenceTitle = dto.EvidenceTitle,
        Description = dto.Description,
        EvidenceDate = dto.EvidenceDate,
        ExpiryDate = dto.ExpiryDate,
        DocumentPath = dto.DocumentPath,
        RecordedById = dto.RecordedById,
        CreatedBy = userId.ToString(),
    };

    #endregion

    // ========================================================================
    // O. SAFETY SIGNAGE REGISTER
    // ========================================================================

    #region SafetySign

    public static SafetySignDto ToDto(this SafetySign e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        SignCode = e.SignCode,
        Description = e.Description,
        SignType = e.SignType,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name ?? string.Empty,
        SpecificPosition = e.SpecificPosition,
        InstallationDate = e.InstallationDate,
        Manufacturer = e.Manufacturer,
        Material = e.Material,
        IsPhotoluminescent = e.IsPhotoluminescent,
        Status = e.Status,
        LastInspectionDate = e.LastInspectionDate,
        NextInspectionDate = e.NextInspectionDate,
        InspectionNotes = e.InspectionNotes,
        IsActive = e.IsActive,
    };

    public static SafetySign ToEntity(this CreateSafetySignDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        SignCode = dto.SignCode,
        Description = dto.Description,
        SignType = dto.SignType,
        LocationId = dto.LocationId,
        SpecificPosition = dto.SpecificPosition,
        InstallationDate = dto.InstallationDate,
        Manufacturer = dto.Manufacturer,
        Material = dto.Material,
        IsPhotoluminescent = dto.IsPhotoluminescent,
        Status = dto.Status,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetySign e, UpdateSafetySignDto dto, Guid userId)
    {
        e.Description = dto.Description;
        e.SignType = dto.SignType;
        e.LocationId = dto.LocationId;
        e.SpecificPosition = dto.SpecificPosition;
        e.InstallationDate = dto.InstallationDate;
        e.Manufacturer = dto.Manufacturer;
        e.Material = dto.Material;
        e.IsPhotoluminescent = dto.IsPhotoluminescent;
        e.Status = dto.Status;
        e.LastInspectionDate = dto.LastInspectionDate;
        e.NextInspectionDate = dto.NextInspectionDate;
        e.InspectionNotes = dto.InspectionNotes;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // P. SHE PERFORMANCE METRICS / KPIs
    // ========================================================================

    #region PerformanceSnapshot

    public static ShePerformanceSnapshotDto ToDto(this ShePerformanceSnapshot e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        SnapshotNumber = e.SnapshotNumber,
        PeriodType = e.PeriodType,
        Year = e.Year,
        PeriodNumber = e.PeriodNumber,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        TotalAccidents = e.TotalAccidents,
        TotalIncidents = e.TotalIncidents,
        TotalNearMisses = e.TotalNearMisses,
        TotalDangerousOccurrences = e.TotalDangerousOccurrences,
        TotalFatalities = e.TotalFatalities,
        TotalLostTimeInjuries = e.TotalLostTimeInjuries,
        LostTimeInjuryFrequencyRate = e.LostTimeInjuryFrequencyRate,
        TotalManHoursWorked = e.TotalManHoursWorked,
        TotalLostDays = e.TotalLostDays,
        InspectionsPlanned = e.InspectionsPlanned,
        InspectionsConducted = e.InspectionsConducted,
        InspectionsOverdue = e.InspectionsOverdue,
        CorrectiveActionsIssued = e.CorrectiveActionsIssued,
        CorrectiveActionsCompleted = e.CorrectiveActionsCompleted,
        CorrectiveActionsOverdue = e.CorrectiveActionsOverdue,
        CorrectiveActionClosureRate = e.CorrectiveActionClosureRate,
        TrainingProgramsPlanned = e.TrainingProgramsPlanned,
        TrainingProgramsConducted = e.TrainingProgramsConducted,
        TotalTrainingHours = e.TotalTrainingHours,
        ContractorsOnSite = e.ContractorsOnSite,
        ContractorInspectionsConducted = e.ContractorInspectionsConducted,
        ContractorNonComplianceNoticesIssued = e.ContractorNonComplianceNoticesIssued,
        ContractorComplianceRate = e.ContractorComplianceRate,
        EnvironmentalIncidents = e.EnvironmentalIncidents,
        EnvironmentalIncidentsReportedToEpa = e.EnvironmentalIncidentsReportedToEpa,
        EmergencyDrillsPlanned = e.EmergencyDrillsPlanned,
        EmergencyDrillsConducted = e.EmergencyDrillsConducted,
        PpeComplianceRate = e.PpeComplianceRate,
        HousekeepingComplianceRating = e.HousekeepingComplianceRating,
        RegulatoryObligationsTotal = e.RegulatoryObligationsTotal,
        RegulatoryObligationsCompliant = e.RegulatoryObligationsCompliant,
        RegulatoryObligationsNonCompliant = e.RegulatoryObligationsNonCompliant,
        RegulatoryObligationsExpiringSoon = e.RegulatoryObligationsExpiringSoon,
        PreparedById = e.PreparedById,
        PreparedByName = e.PreparedBy?.FullName ?? string.Empty,
        PreparedDate = e.PreparedDate,
        ReviewedById = e.ReviewedById,
        ReviewedByName = e.ReviewedBy?.FullName,
        ReviewedDate = e.ReviewedDate,
        ManagementComments = e.ManagementComments,
        ReportDocumentPath = e.ReportDocumentPath,
        TotalRecordableIncidentRate = e.TotalRecordableIncidentRate,
        NearMissFrequencyRate = e.NearMissFrequencyRate,
        TrainingCompletionRate = e.TrainingCompletionRate,
        FireDrillObjectivesMetRate = e.FireDrillObjectivesMetRate,
        WasteRecyclingRate = e.WasteRecyclingRate,
        AverageInspectionComplianceScore = e.AverageInspectionComplianceScore,
        KpisComputedAt = e.KpisComputedAt,
        KpisComputedById = e.KpisComputedById,
        KpisComputedByName = e.KpisComputedBy?.FullName,
    };

    public static ShePerformanceSnapshotSummaryDto ToSummaryDto(this ShePerformanceSnapshot e) => new()
    {
        Id = e.Id,
        SnapshotNumber = e.SnapshotNumber,
        PeriodType = e.PeriodType,
        Year = e.Year,
        PeriodNumber = e.PeriodNumber,
        LocationName = e.Location?.Name,
        TotalIncidents = e.TotalIncidents,
        TotalLostTimeInjuries = e.TotalLostTimeInjuries,
        LostTimeInjuryFrequencyRate = e.LostTimeInjuryFrequencyRate,
        PreparedDate = e.PreparedDate,
    };

    public static ShePerformanceSnapshot ToEntity(this CreateShePerformanceSnapshotDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        SnapshotNumber = dto.SnapshotNumber,
        PeriodType = dto.PeriodType,
        Year = dto.Year,
        PeriodNumber = dto.PeriodNumber,
        LocationId = dto.LocationId,
        TotalAccidents = dto.TotalAccidents,
        TotalIncidents = dto.TotalIncidents,
        TotalNearMisses = dto.TotalNearMisses,
        TotalDangerousOccurrences = dto.TotalDangerousOccurrences,
        TotalFatalities = dto.TotalFatalities,
        TotalLostTimeInjuries = dto.TotalLostTimeInjuries,
        LostTimeInjuryFrequencyRate = dto.LostTimeInjuryFrequencyRate,
        TotalManHoursWorked = dto.TotalManHoursWorked,
        TotalLostDays = dto.TotalLostDays,
        InspectionsPlanned = dto.InspectionsPlanned,
        InspectionsConducted = dto.InspectionsConducted,
        InspectionsOverdue = dto.InspectionsOverdue,
        CorrectiveActionsIssued = dto.CorrectiveActionsIssued,
        CorrectiveActionsCompleted = dto.CorrectiveActionsCompleted,
        CorrectiveActionsOverdue = dto.CorrectiveActionsOverdue,
        CorrectiveActionClosureRate = dto.CorrectiveActionClosureRate,
        TrainingProgramsPlanned = dto.TrainingProgramsPlanned,
        TrainingProgramsConducted = dto.TrainingProgramsConducted,
        TotalTrainingHours = dto.TotalTrainingHours,
        ContractorsOnSite = dto.ContractorsOnSite,
        ContractorInspectionsConducted = dto.ContractorInspectionsConducted,
        ContractorNonComplianceNoticesIssued = dto.ContractorNonComplianceNoticesIssued,
        ContractorComplianceRate = dto.ContractorComplianceRate,
        EnvironmentalIncidents = dto.EnvironmentalIncidents,
        EnvironmentalIncidentsReportedToEpa = dto.EnvironmentalIncidentsReportedToEpa,
        EmergencyDrillsPlanned = dto.EmergencyDrillsPlanned,
        EmergencyDrillsConducted = dto.EmergencyDrillsConducted,
        PpeComplianceRate = dto.PpeComplianceRate,
        HousekeepingComplianceRating = dto.HousekeepingComplianceRating,
        RegulatoryObligationsTotal = dto.RegulatoryObligationsTotal,
        RegulatoryObligationsCompliant = dto.RegulatoryObligationsCompliant,
        RegulatoryObligationsNonCompliant = dto.RegulatoryObligationsNonCompliant,
        RegulatoryObligationsExpiringSoon = dto.RegulatoryObligationsExpiringSoon,
        PreparedById = dto.PreparedById,
        PreparedDate = dto.PreparedDate,
        ManagementComments = dto.ManagementComments,
        ReportDocumentPath = dto.ReportDocumentPath,
        CreatedBy = userId.ToString(),
    };

    /// <summary>Rewrites the reported figures. Identity fields (number, period, location,
    /// preparer) are deliberately not touched — see <see cref="UpdateShePerformanceSnapshotDto"/>.</summary>
    public static void UpdateEntity(this ShePerformanceSnapshot e, UpdateShePerformanceSnapshotDto dto, Guid userId)
    {
        e.TotalAccidents = dto.TotalAccidents;
        e.TotalIncidents = dto.TotalIncidents;
        e.TotalNearMisses = dto.TotalNearMisses;
        e.TotalDangerousOccurrences = dto.TotalDangerousOccurrences;
        e.TotalFatalities = dto.TotalFatalities;
        e.TotalLostTimeInjuries = dto.TotalLostTimeInjuries;
        e.LostTimeInjuryFrequencyRate = dto.LostTimeInjuryFrequencyRate;
        e.TotalManHoursWorked = dto.TotalManHoursWorked;
        e.TotalLostDays = dto.TotalLostDays;
        e.InspectionsPlanned = dto.InspectionsPlanned;
        e.InspectionsConducted = dto.InspectionsConducted;
        e.InspectionsOverdue = dto.InspectionsOverdue;
        e.CorrectiveActionsIssued = dto.CorrectiveActionsIssued;
        e.CorrectiveActionsCompleted = dto.CorrectiveActionsCompleted;
        e.CorrectiveActionsOverdue = dto.CorrectiveActionsOverdue;
        e.CorrectiveActionClosureRate = dto.CorrectiveActionClosureRate;
        e.TrainingProgramsPlanned = dto.TrainingProgramsPlanned;
        e.TrainingProgramsConducted = dto.TrainingProgramsConducted;
        e.TotalTrainingHours = dto.TotalTrainingHours;
        e.ContractorsOnSite = dto.ContractorsOnSite;
        e.ContractorInspectionsConducted = dto.ContractorInspectionsConducted;
        e.ContractorNonComplianceNoticesIssued = dto.ContractorNonComplianceNoticesIssued;
        e.ContractorComplianceRate = dto.ContractorComplianceRate;
        e.EnvironmentalIncidents = dto.EnvironmentalIncidents;
        e.EnvironmentalIncidentsReportedToEpa = dto.EnvironmentalIncidentsReportedToEpa;
        e.EmergencyDrillsPlanned = dto.EmergencyDrillsPlanned;
        e.EmergencyDrillsConducted = dto.EmergencyDrillsConducted;
        e.PpeComplianceRate = dto.PpeComplianceRate;
        e.HousekeepingComplianceRating = dto.HousekeepingComplianceRating;
        e.RegulatoryObligationsTotal = dto.RegulatoryObligationsTotal;
        e.RegulatoryObligationsCompliant = dto.RegulatoryObligationsCompliant;
        e.RegulatoryObligationsNonCompliant = dto.RegulatoryObligationsNonCompliant;
        e.RegulatoryObligationsExpiringSoon = dto.RegulatoryObligationsExpiringSoon;
        e.ManagementComments = dto.ManagementComments;
        e.ReportDocumentPath = dto.ReportDocumentPath;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<ShePerformanceSnapshotSummaryDto> ToSummaryDtoList(this IEnumerable<ShePerformanceSnapshot> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    // ========================================================================
    // Q. SAFETY COMMITTEE & MEETINGS
    // ========================================================================

    #region SafetyCommittee

    public static SafetyCommitteeDto ToDto(this SafetyCommittee e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        CommitteeName = e.CommitteeName,
        Description = e.Description,
        EstablishedDate = e.EstablishedDate,
        ChairPersonId = e.ChairPersonId,
        ChairPersonName = e.ChairPerson?.FullName,
        MeetingFrequencyDays = e.MeetingFrequencyDays,
        MeetingSchedule = e.MeetingSchedule,
        IsActive = e.IsActive,
        Members = e.Members.Select(m => m.ToDto()).ToList(),
    };

    public static SafetyCommittee ToEntity(this CreateSafetyCommitteeDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        CommitteeName = dto.CommitteeName,
        Description = dto.Description,
        EstablishedDate = dto.EstablishedDate,
        ChairPersonId = dto.ChairPersonId,
        MeetingFrequencyDays = dto.MeetingFrequencyDays,
        MeetingSchedule = dto.MeetingSchedule,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyCommittee e, UpdateSafetyCommitteeDto dto, Guid userId)
    {
        e.CommitteeName = dto.CommitteeName;
        e.Description = dto.Description;
        e.EstablishedDate = dto.EstablishedDate;
        e.ChairPersonId = dto.ChairPersonId;
        e.MeetingFrequencyDays = dto.MeetingFrequencyDays;
        e.MeetingSchedule = dto.MeetingSchedule;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SafetyCommitteeMemberDto ToDto(this SafetyCommitteeMember e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        CommitteeId = e.CommitteeId,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        Role = e.Role,
        JoinDate = e.JoinDate,
        EndDate = e.EndDate,
        IsActive = e.IsActive,
    };

    public static SafetyCommitteeMember ToEntity(this CreateSafetyCommitteeMemberDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        CommitteeId = dto.CommitteeId,
        EmployeeId = dto.EmployeeId,
        Role = dto.Role,
        JoinDate = dto.JoinDate,
        EndDate = dto.EndDate,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyCommitteeMember e, UpdateSafetyCommitteeMemberDto dto, Guid userId)
    {
        e.Role = dto.Role;
        e.EndDate = dto.EndDate;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region SafetyMeeting

    public static SafetyMeetingDto ToDto(this SafetyMeeting e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        CommitteeId = e.CommitteeId,
        CommitteeName = e.Committee?.CommitteeName,
        MeetingNumber = e.MeetingNumber,
        MeetingDate = e.MeetingDate,
        StartTime = e.StartTime,
        EndTime = e.EndTime,
        Location = e.Location,
        Type = e.Type,
        Agenda = e.Agenda,
        Minutes = e.Minutes,
        TopicsDiscussed = e.TopicsDiscussed,
        DecisionsMade = e.DecisionsMade,
        FacilitatorId = e.FacilitatorId,
        FacilitatorName = e.Facilitator?.FullName,
        AttendeesCount = e.AttendeesCount,
        Attendees = e.Attendees.Select(a => a.ToDto()).ToList(),
        ActionItems = e.ActionItems.Select(a => a.ToDto()).ToList(),
        Documents = e.Documents.Select(d => d.ToDto()).ToList(),
    };

    public static SafetyMeetingSummaryDto ToSummaryDto(this SafetyMeeting e) => new()
    {
        Id = e.Id,
        MeetingNumber = e.MeetingNumber,
        CommitteeName = e.Committee?.CommitteeName,
        MeetingDate = e.MeetingDate,
        Location = e.Location,
        Type = e.Type,
        AttendeesCount = e.AttendeesCount,
        OpenActionItemCount = e.ActionItems.Count(a =>
            a.Status != SheActionItemStatus.Completed &&
            a.Status != SheActionItemStatus.Cancelled),
    };

    public static SafetyMeeting ToEntity(this CreateSafetyMeetingDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        CommitteeId = dto.CommitteeId,
        MeetingNumber = dto.MeetingNumber,
        MeetingDate = dto.MeetingDate,
        StartTime = dto.StartTime,
        EndTime = dto.EndTime,
        Location = dto.Location,
        Type = dto.Type,
        Agenda = dto.Agenda,
        FacilitatorId = dto.FacilitatorId,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyMeeting e, UpdateSafetyMeetingDto dto, Guid userId)
    {
        e.CommitteeId = dto.CommitteeId;
        e.MeetingDate = dto.MeetingDate;
        e.StartTime = dto.StartTime;
        e.EndTime = dto.EndTime;
        e.Location = dto.Location;
        e.Type = dto.Type;
        e.Agenda = dto.Agenda;
        e.Minutes = dto.Minutes;
        e.TopicsDiscussed = dto.TopicsDiscussed;
        e.DecisionsMade = dto.DecisionsMade;
        e.FacilitatorId = dto.FacilitatorId;
        e.AttendeesCount = dto.AttendeesCount;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SafetyMeetingSummaryDto> ToSummaryDtoList(this IEnumerable<SafetyMeeting> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SafetyMeetingAttendeeDto ToDto(this SafetyMeetingAttendee e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        MeetingId = e.MeetingId,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        Attended = e.Attended,
        SignedDate = e.SignedDate,
    };

    public static SafetyMeetingAttendee ToEntity(this CreateSafetyMeetingAttendeeDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        MeetingId = dto.MeetingId,
        EmployeeId = dto.EmployeeId,
        Attended = dto.Attended,
        SignedDate = dto.SignedDate,
        CreatedBy = userId.ToString(),
    };

    public static SafetyMeetingActionItemDto ToDto(this SafetyMeetingActionItem e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        MeetingId = e.MeetingId,
        MeetingNumber = e.Meeting?.MeetingNumber,
        ActionDescription = e.ActionDescription,
        Priority = e.Priority,
        Status = e.Status,
        AssignedToId = e.AssignedToId,
        AssignedToName = e.AssignedTo?.FullName,
        DueDate = e.DueDate,
        CompletionDate = e.CompletionDate,
        CompletionNotes = e.CompletionNotes,
    };

    public static SafetyMeetingActionItem ToEntity(this CreateSafetyMeetingActionItemDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        MeetingId = dto.MeetingId,
        ActionDescription = dto.ActionDescription,
        Priority = dto.Priority,
        Status = SheActionItemStatus.Open,
        AssignedToId = dto.AssignedToId,
        DueDate = dto.DueDate,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyMeetingActionItem e, UpdateSafetyMeetingActionItemDto dto, Guid userId)
    {
        e.ActionDescription = dto.ActionDescription;
        e.Priority = dto.Priority;
        e.Status = dto.Status;
        e.AssignedToId = dto.AssignedToId;
        e.DueDate = dto.DueDate;
        e.CompletionDate = dto.CompletionDate;
        e.CompletionNotes = dto.CompletionNotes;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SafetyMeetingDocumentDto ToDto(this SafetyMeetingDocument e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        MeetingId = e.MeetingId,
        FileName = e.FileName,
        FilePath = e.FilePath,
        Description = e.Description,
        UploadDate = e.UploadDate,
    };

    public static SafetyMeetingDocument ToEntity(this CreateSafetyMeetingDocumentDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        MeetingId = dto.MeetingId,
        FileName = dto.FileName,
        FilePath = dto.FilePath,
        Description = dto.Description,
        UploadDate = DateTime.UtcNow,
        CreatedBy = userId.ToString(),
    };

    #endregion

    // ========================================================================
    // R. RETURN-TO-WORK PLANS
    // ========================================================================

    #region ReturnToWorkPlan

    public static SheReturnToWorkPlanDto ToDto(this SheReturnToWorkPlan e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        EmployeeId = e.EmployeeId,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        EmployeeNumber = e.Employee?.EmployeeNumber,
        SafetyIncidentId = e.SafetyIncidentId,
        SafetyIncidentNumber = e.SafetyIncident?.IncidentNumber,
        PlanNumber = e.PlanNumber,
        PlanDate = e.PlanDate,
        PlannedReturnDate = e.PlannedReturnDate,
        ActualReturnDate = e.ActualReturnDate,
        MedicalRestrictions = e.MedicalRestrictions,
        MedicalClearanceDate = e.MedicalClearanceDate,
        MedicalClearanceNotes = e.MedicalClearanceNotes,
        RequiresWorkplaceModifications = e.RequiresWorkplaceModifications,
        WorkplaceModificationsDescription = e.WorkplaceModificationsDescription,
        Status = e.Status,
        CoordinatorId = e.CoordinatorId,
        CoordinatorName = e.Coordinator?.FullName,
        SupervisorId = e.SupervisorId,
        SupervisorName = e.Supervisor?.FullName,
        CompletionDate = e.CompletionDate,
        SuccessfullyCompleted = e.SuccessfullyCompleted,
        CompletionNotes = e.CompletionNotes,
        Phases = e.Phases.Select(p => p.ToDto()).ToList(),
        Reviews = e.Reviews.Select(r => r.ToDto()).ToList(),
    };

    public static SheReturnToWorkPlanSummaryDto ToSummaryDto(this SheReturnToWorkPlan e) => new()
    {
        Id = e.Id,
        PlanNumber = e.PlanNumber,
        EmployeeName = e.Employee?.FullName ?? string.Empty,
        SafetyIncidentNumber = e.SafetyIncident?.IncidentNumber,
        PlanDate = e.PlanDate,
        PlannedReturnDate = e.PlannedReturnDate,
        ActualReturnDate = e.ActualReturnDate,
        Status = e.Status,
        SuccessfullyCompleted = e.SuccessfullyCompleted,
    };

    public static SheReturnToWorkPlan ToEntity(this CreateSheReturnToWorkPlanDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        EmployeeId = dto.EmployeeId,
        SafetyIncidentId = dto.SafetyIncidentId,
        PlanNumber = dto.PlanNumber,
        PlanDate = dto.PlanDate,
        PlannedReturnDate = dto.PlannedReturnDate,
        MedicalRestrictions = dto.MedicalRestrictions,
        MedicalClearanceDate = dto.MedicalClearanceDate,
        MedicalClearanceNotes = dto.MedicalClearanceNotes,
        RequiresWorkplaceModifications = dto.RequiresWorkplaceModifications,
        WorkplaceModificationsDescription = dto.WorkplaceModificationsDescription,
        Status = SheReturnToWorkStatus.PendingMedicalClearance,
        CoordinatorId = dto.CoordinatorId,
        SupervisorId = dto.SupervisorId,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheReturnToWorkPlan e, UpdateSheReturnToWorkPlanDto dto, Guid userId)
    {
        e.SafetyIncidentId = dto.SafetyIncidentId;
        e.PlanDate = dto.PlanDate;
        e.PlannedReturnDate = dto.PlannedReturnDate;
        e.ActualReturnDate = dto.ActualReturnDate;
        e.MedicalRestrictions = dto.MedicalRestrictions;
        e.MedicalClearanceDate = dto.MedicalClearanceDate;
        e.MedicalClearanceNotes = dto.MedicalClearanceNotes;
        e.RequiresWorkplaceModifications = dto.RequiresWorkplaceModifications;
        e.WorkplaceModificationsDescription = dto.WorkplaceModificationsDescription;
        e.Status = dto.Status;
        e.CoordinatorId = dto.CoordinatorId;
        e.SupervisorId = dto.SupervisorId;
        e.CompletionDate = dto.CompletionDate;
        e.SuccessfullyCompleted = dto.SuccessfullyCompleted;
        e.CompletionNotes = dto.CompletionNotes;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SheReturnToWorkPlanSummaryDto> ToSummaryDtoList(this IEnumerable<SheReturnToWorkPlan> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SheReturnToWorkPhaseDto ToDto(this SheReturnToWorkPhase e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ReturnToWorkPlanId = e.ReturnToWorkPlanId,
        PhaseName = e.PhaseName,
        PhaseNumber = e.PhaseNumber,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        RequiresReducedHours = e.RequiresReducedHours,
        HoursPerDay = e.HoursPerDay,
        DaysPerWeek = e.DaysPerWeek,
        Duties = e.Duties,
        Restrictions = e.Restrictions,
        AssessmentDate = e.AssessmentDate,
        AssessedById = e.AssessedById,
        AssessedByName = e.AssessedBy?.FullName ?? string.Empty,
        EmployeeProgress = e.EmployeeProgress,
        ChallengesFaced = e.ChallengesFaced,
        AccommodationsEffectiveness = e.AccommodationsEffectiveness,
        RecommendedAdjustments = e.RecommendedAdjustments,
        EmployeeFeedback = e.EmployeeFeedback,
        PhaseCompleted = e.PhaseCompleted,
        ActualEndDate = e.ActualEndDate,
        CanContinuePlan = e.CanContinuePlan,
        CompletionNotes = e.CompletionNotes,
    };

    public static SheReturnToWorkPhase ToEntity(this CreateSheReturnToWorkPhaseDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ReturnToWorkPlanId = dto.ReturnToWorkPlanId,
        PhaseName = dto.PhaseName,
        StartDate = dto.StartDate,
        EndDate = dto.EndDate,
        RequiresReducedHours = dto.RequiresReducedHours,
        HoursPerDay = dto.HoursPerDay,
        DaysPerWeek = dto.DaysPerWeek,
        Duties = dto.Duties,
        Restrictions = dto.Restrictions,
        AssessmentDate = dto.AssessmentDate,
        AssessedById = dto.AssessedById,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheReturnToWorkPhase e, UpdateSheReturnToWorkPhaseDto dto, Guid userId)
    {
        e.PhaseName = dto.PhaseName;
        e.StartDate = dto.StartDate;
        e.EndDate = dto.EndDate;
        e.RequiresReducedHours = dto.RequiresReducedHours;
        e.HoursPerDay = dto.HoursPerDay;
        e.DaysPerWeek = dto.DaysPerWeek;
        e.Duties = dto.Duties;
        e.Restrictions = dto.Restrictions;
        e.AssessmentDate = dto.AssessmentDate;
        e.EmployeeProgress = dto.EmployeeProgress;
        e.ChallengesFaced = dto.ChallengesFaced;
        e.AccommodationsEffectiveness = dto.AccommodationsEffectiveness;
        e.RecommendedAdjustments = dto.RecommendedAdjustments;
        e.EmployeeFeedback = dto.EmployeeFeedback;
        e.PhaseCompleted = dto.PhaseCompleted;
        e.ActualEndDate = dto.ActualEndDate;
        e.CanContinuePlan = dto.CanContinuePlan;
        e.CompletionNotes = dto.CompletionNotes;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SheReturnToWorkReviewDto ToDto(this SheReturnToWorkReview e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ReturnToWorkPlanId = e.ReturnToWorkPlanId,
        ReviewDate = e.ReviewDate,
        ReviewNumber = e.ReviewNumber,
        EmployeeCondition = e.EmployeeCondition,
        WorkProgress = e.WorkProgress,
        IssuesIdentified = e.IssuesIdentified,
        RecommendedActions = e.RecommendedActions,
        ReviewedById = e.ReviewedById,
        ReviewedByName = e.ReviewedBy?.FullName ?? string.Empty,
        NextReviewDate = e.NextReviewDate,
    };

    public static SheReturnToWorkReview ToEntity(this CreateSheReturnToWorkReviewDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ReturnToWorkPlanId = dto.ReturnToWorkPlanId,
        ReviewDate = dto.ReviewDate,
        EmployeeCondition = dto.EmployeeCondition,
        WorkProgress = dto.WorkProgress,
        IssuesIdentified = dto.IssuesIdentified,
        RecommendedActions = dto.RecommendedActions,
        ReviewedById = dto.ReviewedById,
        NextReviewDate = dto.NextReviewDate,
        CreatedBy = userId.ToString(),
    };

    #endregion
}
