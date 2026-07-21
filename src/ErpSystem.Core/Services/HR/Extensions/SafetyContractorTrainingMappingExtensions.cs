using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Application.HR.Extensions;

// ============================================================================
// SHE mapping — Contractor SHE Management (H) and Training & Awareness (I).
// ============================================================================

public static class SafetyContractorTrainingMappingExtensions
{
    // ========================================================================
    // H. CONTRACTOR SHE MANAGEMENT
    // ========================================================================

    #region Contractor

    public static SheContractorDto ToDto(this SheContractor e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ContractorCode = e.ContractorCode,
        CompanyName = e.CompanyName,
        TradingName = e.TradingName,
        Address = e.Address,
        Phone = e.Phone,
        Email = e.Email,
        RegistrationNumber = e.RegistrationNumber,
        PrimaryContactName = e.PrimaryContactName,
        PrimaryContactPhone = e.PrimaryContactPhone,
        PrimaryContactEmail = e.PrimaryContactEmail,
        SheStatus = e.SheStatus,
        PreQualificationScore = e.PreQualificationScore,
        PreQualificationDate = e.PreQualificationDate,
        PreQualificationExpiryDate = e.PreQualificationExpiryDate,
        PreQualifiedById = e.PreQualifiedById,
        PreQualifiedByName = e.PreQualifiedBy?.FullName,
        SheConditions = e.SheConditions,
        IsActive = e.IsActive,
        Inductions = e.Inductions.Select(i => i.ToDto()).ToList(),
        SheInspections = e.SheInspections.Select(i => i.ToDto()).ToList(),
        NonCompliances = e.NonCompliances.Select(n => n.ToDto()).ToList(),
        Documents = e.Documents.Select(d => d.ToDto()).ToList(),
    };

    public static SheContractorSummaryDto ToSummaryDto(this SheContractor e) => new()
    {
        Id = e.Id,
        ContractorCode = e.ContractorCode,
        CompanyName = e.CompanyName,
        PrimaryContactName = e.PrimaryContactName,
        SheStatus = e.SheStatus,
        PreQualificationScore = e.PreQualificationScore,
        PreQualificationExpiryDate = e.PreQualificationExpiryDate,
        IsActive = e.IsActive,
        OpenNonComplianceCount = e.NonCompliances.Count(n =>
            n.Status != SheNonComplianceStatus.Closed),
    };

    public static SheContractor ToEntity(this CreateSheContractorDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ContractorCode = dto.ContractorCode,
        CompanyName = dto.CompanyName,
        TradingName = dto.TradingName,
        Address = dto.Address,
        Phone = dto.Phone,
        Email = dto.Email,
        RegistrationNumber = dto.RegistrationNumber,
        PrimaryContactName = dto.PrimaryContactName,
        PrimaryContactPhone = dto.PrimaryContactPhone,
        PrimaryContactEmail = dto.PrimaryContactEmail,
        SheStatus = SheContractorStatus.PendingAssessment,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheContractor e, UpdateSheContractorDto dto, Guid userId)
    {
        e.CompanyName = dto.CompanyName;
        e.TradingName = dto.TradingName;
        e.Address = dto.Address;
        e.Phone = dto.Phone;
        e.Email = dto.Email;
        e.RegistrationNumber = dto.RegistrationNumber;
        e.PrimaryContactName = dto.PrimaryContactName;
        e.PrimaryContactPhone = dto.PrimaryContactPhone;
        e.PrimaryContactEmail = dto.PrimaryContactEmail;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SheContractorSummaryDto> ToSummaryDtoList(this IEnumerable<SheContractor> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SheContractorInductionDto ToDto(this SheContractorInduction e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ContractorId = e.ContractorId,
        WorkerName = e.WorkerName,
        WorkerIdOrPassport = e.WorkerIdOrPassport,
        Trade = e.Trade,
        InductionDate = e.InductionDate,
        ConductedById = e.ConductedById,
        ConductedByName = e.ConductedBy?.FullName ?? string.Empty,
        InductionPassed = e.InductionPassed,
        InductionExpiryDate = e.InductionExpiryDate,
        SignaturePath = e.SignaturePath,
        Notes = e.Notes,
    };

    public static SheContractorInduction ToEntity(this CreateSheContractorInductionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ContractorId = dto.ContractorId,
        WorkerName = dto.WorkerName,
        WorkerIdOrPassport = dto.WorkerIdOrPassport,
        Trade = dto.Trade,
        InductionDate = dto.InductionDate,
        ConductedById = dto.ConductedById,
        InductionPassed = dto.InductionPassed,
        InductionExpiryDate = dto.InductionExpiryDate,
        SignaturePath = dto.SignaturePath,
        Notes = dto.Notes,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheContractorInduction e, UpdateSheContractorInductionDto dto, Guid userId)
    {
        e.WorkerName = dto.WorkerName;
        e.WorkerIdOrPassport = dto.WorkerIdOrPassport;
        e.Trade = dto.Trade;
        e.InductionDate = dto.InductionDate;
        e.InductionPassed = dto.InductionPassed;
        e.InductionExpiryDate = dto.InductionExpiryDate;
        e.SignaturePath = dto.SignaturePath;
        e.Notes = dto.Notes;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SheContractorInspectionDto ToDto(this SheContractorInspection e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ContractorId = e.ContractorId,
        InspectionNumber = e.InspectionNumber,
        InspectionDate = e.InspectionDate,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        SpecificArea = e.SpecificArea,
        InspectorId = e.InspectorId,
        InspectorName = e.Inspector?.FullName ?? string.Empty,
        ComplianceScore = e.ComplianceScore,
        Result = e.Result,
        Findings = e.Findings,
        RecommendedActions = e.RecommendedActions,
        NextInspectionDate = e.NextInspectionDate,
        Status = e.Status,
    };

    public static SheContractorInspection ToEntity(this CreateSheContractorInspectionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ContractorId = dto.ContractorId,
        InspectionNumber = dto.InspectionNumber,
        InspectionDate = dto.InspectionDate,
        LocationId = dto.LocationId,
        SpecificArea = dto.SpecificArea,
        InspectorId = dto.InspectorId,
        ComplianceScore = dto.ComplianceScore,
        Result = dto.Result,
        Findings = dto.Findings,
        RecommendedActions = dto.RecommendedActions,
        NextInspectionDate = dto.NextInspectionDate,
        Status = dto.Status,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheContractorInspection e, UpdateSheContractorInspectionDto dto, Guid userId)
    {
        e.InspectionDate = dto.InspectionDate;
        e.LocationId = dto.LocationId;
        e.SpecificArea = dto.SpecificArea;
        e.ComplianceScore = dto.ComplianceScore;
        e.Result = dto.Result;
        e.Findings = dto.Findings;
        e.RecommendedActions = dto.RecommendedActions;
        e.NextInspectionDate = dto.NextInspectionDate;
        e.Status = dto.Status;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SheContractorNonComplianceDto ToDto(this SheContractorNonCompliance e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ContractorId = e.ContractorId,
        NoticeNumber = e.NoticeNumber,
        IssuedDate = e.IssuedDate,
        IssuedById = e.IssuedById,
        IssuedByName = e.IssuedBy?.FullName ?? string.Empty,
        ViolationDescription = e.ViolationDescription,
        Severity = e.Severity,
        RectificationDeadline = e.RectificationDeadline,
        IsRepeatViolation = e.IsRepeatViolation,
        RepeatCount = e.RepeatCount,
        Status = e.Status,
        RectificationDate = e.RectificationDate,
        ContractorResponse = e.ContractorResponse,
        ClosureNotes = e.ClosureNotes,
        ClosedDate = e.ClosedDate,
        ClosedById = e.ClosedById,
        ClosedByName = e.ClosedBy?.FullName,
        SanctionApplied = e.SanctionApplied,
    };

    public static SheContractorNonCompliance ToEntity(this CreateSheContractorNonComplianceDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ContractorId = dto.ContractorId,
        NoticeNumber = dto.NoticeNumber,
        IssuedDate = dto.IssuedDate,
        IssuedById = dto.IssuedById,
        ViolationDescription = dto.ViolationDescription,
        Severity = dto.Severity,
        RectificationDeadline = dto.RectificationDeadline,
        IsRepeatViolation = dto.IsRepeatViolation,
        RepeatCount = dto.RepeatCount,
        Status = SheNonComplianceStatus.Open,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheContractorNonCompliance e, UpdateSheContractorNonComplianceDto dto, Guid userId)
    {
        e.ViolationDescription = dto.ViolationDescription;
        e.Severity = dto.Severity;
        e.RectificationDeadline = dto.RectificationDeadline;
        e.Status = dto.Status;
        e.RectificationDate = dto.RectificationDate;
        e.ContractorResponse = dto.ContractorResponse;
        e.SanctionApplied = dto.SanctionApplied;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SheContractorDocumentDto ToDto(this SheContractorDocument e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ContractorId = e.ContractorId,
        DocumentType = e.DocumentType,
        FileName = e.FileName,
        FilePath = e.FilePath,
        Title = e.Title,
        DocumentDate = e.DocumentDate,
        ExpiryDate = e.ExpiryDate,
        IsVerified = e.IsVerified,
        VerifiedById = e.VerifiedById,
        VerifiedByName = e.VerifiedBy?.FullName,
        VerifiedDate = e.VerifiedDate,
        VerificationNotes = e.VerificationNotes,
        UploadedDate = e.UploadedDate,
        UploadedById = e.UploadedById,
        UploadedByName = e.UploadedBy?.FullName ?? string.Empty,
    };

    public static SheContractorDocument ToEntity(this CreateSheContractorDocumentDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ContractorId = dto.ContractorId,
        DocumentType = dto.DocumentType,
        FileName = dto.FileName,
        FilePath = dto.FilePath,
        Title = dto.Title,
        DocumentDate = dto.DocumentDate,
        ExpiryDate = dto.ExpiryDate,
        UploadedDate = DateTime.UtcNow,
        UploadedById = dto.UploadedById,
        CreatedBy = userId.ToString(),
    };

    #endregion

    // ========================================================================
    // I. SHE TRAINING & AWARENESS
    // ========================================================================

    #region TrainingPlan

    public static SheTrainingPlanDto ToDto(this SheTrainingPlan e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        PlanNumber = e.PlanNumber,
        Title = e.Title,
        Year = e.Year,
        Quarter = e.Quarter,
        OrganizationUnitId = e.OrganizationUnitId,
        OrganizationUnitName = e.OrganizationUnit?.Name,
        Status = e.Status,
        PreparedById = e.PreparedById,
        PreparedByName = e.PreparedBy?.FullName ?? string.Empty,
        PreparedDate = e.PreparedDate,
        ApprovedById = e.ApprovedById,
        ApprovedByName = e.ApprovedBy?.FullName,
        ApprovedDate = e.ApprovedDate,
        Notes = e.Notes,
        Programs = e.Programs.Select(p => p.ToSummaryDto()).ToList(),
    };

    public static SheTrainingPlan ToEntity(this CreateSheTrainingPlanDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        PlanNumber = dto.PlanNumber,
        Title = dto.Title,
        Year = dto.Year,
        Quarter = dto.Quarter,
        OrganizationUnitId = dto.OrganizationUnitId,
        Status = SheTrainingPlanStatus.Draft,
        PreparedById = dto.PreparedById,
        PreparedDate = dto.PreparedDate,
        Notes = dto.Notes,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheTrainingPlan e, UpdateSheTrainingPlanDto dto, Guid userId)
    {
        e.Title = dto.Title;
        e.Year = dto.Year;
        e.Quarter = dto.Quarter;
        e.OrganizationUnitId = dto.OrganizationUnitId;
        e.Status = dto.Status;
        e.ApprovedById = dto.ApprovedById;
        e.ApprovedDate = dto.ApprovedDate;
        e.Notes = dto.Notes;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region TrainingProgram

    public static SheTrainingProgramDto ToDto(this SheTrainingProgram e) => new()
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
        Category = e.Category,
        PlanId = e.PlanId,
        PlanNumber = e.Plan?.PlanNumber,
        DeliveryMethod = e.DeliveryMethod,
        DurationMinutes = e.DurationMinutes,
        ScheduledDate = e.ScheduledDate,
        ActualDate = e.ActualDate,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        TrainerId = e.TrainerId,
        TrainerName = e.Trainer?.FullName,
        ExternalTrainerName = e.ExternalTrainerName,
        ExternalTrainerOrganization = e.ExternalTrainerOrganization,
        Status = e.Status,
        MaxParticipants = e.MaxParticipants,
        ActualAttendees = e.ActualAttendees,
        MaterialPath = e.MaterialPath,
        WasEvaluated = e.WasEvaluated,
        EvaluationSummary = e.EvaluationSummary,
        EvaluatedById = e.EvaluatedById,
        EvaluatedByName = e.EvaluatedBy?.FullName,
        EvaluationDate = e.EvaluationDate,
        Attendances = e.Attendances.Select(a => a.ToDto()).ToList(),
    };

    public static SheTrainingProgramSummaryDto ToSummaryDto(this SheTrainingProgram e) => new()
    {
        Id = e.Id,
        ProgramCode = e.ProgramCode,
        Title = e.Title,
        Category = e.Category,
        DeliveryMethod = e.DeliveryMethod,
        ScheduledDate = e.ScheduledDate,
        ActualDate = e.ActualDate,
        Status = e.Status,
        ActualAttendees = e.ActualAttendees,
    };

    public static SheTrainingProgram ToEntity(this CreateSheTrainingProgramDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ProgramCode = dto.ProgramCode,
        Title = dto.Title,
        Description = dto.Description,
        Category = dto.Category,
        PlanId = dto.PlanId,
        DeliveryMethod = dto.DeliveryMethod,
        DurationMinutes = dto.DurationMinutes,
        ScheduledDate = dto.ScheduledDate,
        LocationId = dto.LocationId,
        TrainerId = dto.TrainerId,
        ExternalTrainerName = dto.ExternalTrainerName,
        ExternalTrainerOrganization = dto.ExternalTrainerOrganization,
        Status = SheTrainingStatus.Planned,
        MaxParticipants = dto.MaxParticipants,
        MaterialPath = dto.MaterialPath,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheTrainingProgram e, UpdateSheTrainingProgramDto dto, Guid userId)
    {
        e.Title = dto.Title;
        e.Description = dto.Description;
        e.Category = dto.Category;
        e.PlanId = dto.PlanId;
        e.DeliveryMethod = dto.DeliveryMethod;
        e.DurationMinutes = dto.DurationMinutes;
        e.ScheduledDate = dto.ScheduledDate;
        e.ActualDate = dto.ActualDate;
        e.LocationId = dto.LocationId;
        e.TrainerId = dto.TrainerId;
        e.ExternalTrainerName = dto.ExternalTrainerName;
        e.ExternalTrainerOrganization = dto.ExternalTrainerOrganization;
        e.Status = dto.Status;
        e.MaxParticipants = dto.MaxParticipants;
        e.ActualAttendees = dto.ActualAttendees;
        e.MaterialPath = dto.MaterialPath;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SheTrainingProgramSummaryDto> ToSummaryDtoList(this IEnumerable<SheTrainingProgram> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SheTrainingAttendanceDto ToDto(this SheTrainingAttendance e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        ProgramId = e.ProgramId,
        IsEmployee = e.IsEmployee,
        EmployeeId = e.EmployeeId,
        AttendanceName = e.AttendanceName,
        CompanyName = e.CompanyName,
        Attended = e.Attended,
        SignedDate = e.SignedDate,
        SignaturePath = e.SignaturePath,
        AssessmentPassed = e.AssessmentPassed,
        AssessmentScore = e.AssessmentScore,
        CertificateExpiryDate = e.CertificateExpiryDate,
        CertificateDocumentPath = e.CertificateDocumentPath,
        ProgramCode = e.Program?.ProgramCode,
        ProgramTitle = e.Program?.Title,
        ProgramCategory = e.Program?.Category,
        ProgramDate = e.Program == null ? null : (e.Program.ActualDate ?? e.Program.ScheduledDate),
        ProgramStatus = e.Program?.Status,
    };

    public static SheTrainingAttendance ToEntity(this CreateSheTrainingAttendanceDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        ProgramId = dto.ProgramId,
        IsEmployee = dto.IsEmployee,
        EmployeeId = dto.EmployeeId,
        AttendanceName = dto.AttendanceName,
        CompanyName = dto.CompanyName,
        Attended = dto.Attended,
        SignedDate = dto.SignedDate,
        SignaturePath = dto.SignaturePath,
        AssessmentPassed = dto.AssessmentPassed,
        AssessmentScore = dto.AssessmentScore,
        CertificateExpiryDate = dto.CertificateExpiryDate,
        CertificateDocumentPath = dto.CertificateDocumentPath,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SheTrainingAttendance e, UpdateSheTrainingAttendanceDto dto, Guid userId)
    {
        e.AttendanceName = dto.AttendanceName;
        e.CompanyName = dto.CompanyName;
        e.Attended = dto.Attended;
        e.SignedDate = dto.SignedDate;
        e.SignaturePath = dto.SignaturePath;
        e.AssessmentPassed = dto.AssessmentPassed;
        e.AssessmentScore = dto.AssessmentScore;
        e.CertificateExpiryDate = dto.CertificateExpiryDate;
        e.CertificateDocumentPath = dto.CertificateDocumentPath;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion
}
