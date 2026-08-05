using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Application.HR.Extensions;

// ============================================================================
// SHE mapping — Permit-to-Work (E), PPE Management (F) and Safety Equipment (G).
// ============================================================================

public static class SafetyPermitPpeEquipmentMappingExtensions
{
    // ========================================================================
    // E. PERMIT-TO-WORK
    // ========================================================================

    #region PermitToWork

    public static ShePermitToWorkDto ToDto(this ShePermitToWork e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        PermitNumber = e.PermitNumber,
        PermitType = e.PermitType,
        WorkDescription = e.WorkDescription,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name,
        SpecificArea = e.SpecificArea,
        RequestedById = e.RequestedById,
        RequestedByName = e.RequestedBy?.FullName ?? string.Empty,
        ContractorId = e.ContractorId,
        ContractorName = e.Contractor?.CompanyName,
        RequestedDate = e.RequestedDate,
        PlannedStartDate = e.PlannedStartDate,
        PlannedStartTime = e.PlannedStartTime,
        PlannedEndDate = e.PlannedEndDate,
        PlannedEndTime = e.PlannedEndTime,
        ActualStartDate = e.ActualStartDate,
        ActualEndDate = e.ActualEndDate,
        Status = e.Status,
        IssuedById = e.IssuedById,
        IssuedByName = e.IssuedBy?.FullName,
        IssuedDate = e.IssuedDate,
        ApprovedById = e.ApprovedById,
        ApprovedByName = e.ApprovedBy?.FullName,
        ApprovedDate = e.ApprovedDate,
        HazardsIdentified = e.HazardsIdentified,
        ControlMeasures = e.ControlMeasures,
        PpeRequired = e.PpeRequired,
        GasTestResults = e.GasTestResults,
        IsolationDetails = e.IsolationDetails,
        RiskAssessmentId = e.RiskAssessmentId,
        RiskAssessmentNumber = e.RiskAssessment?.AssessmentNumber,
        IsSuspended = e.IsSuspended,
        SuspendedDate = e.SuspendedDate,
        SuspensionReason = e.SuspensionReason,
        SuspendedById = e.SuspendedById,
        SuspendedByName = e.SuspendedBy?.FullName,
        ClosedDate = e.ClosedDate,
        ClosedById = e.ClosedById,
        ClosedByName = e.ClosedBy?.FullName,
        ClosureNotes = e.ClosureNotes,
        WorkCompletedSatisfactorily = e.WorkCompletedSatisfactorily,
        AreaLeftSafe = e.AreaLeftSafe,
        ReinstatementNotes = e.ReinstatementNotes,
        AuthorisedWorkers = e.AuthorisedWorkers.Select(w => w.ToDto()).ToList(),
        Extensions = e.Extensions.Select(x => x.ToDto()).ToList(),
        Documents = e.Documents.Select(d => d.ToDto()).ToList(),
    };

    public static ShePermitToWorkSummaryDto ToSummaryDto(this ShePermitToWork e) => new()
    {
        Id = e.Id,
        PermitNumber = e.PermitNumber,
        PermitType = e.PermitType,
        WorkDescription = e.WorkDescription,
        LocationName = e.Location?.Name,
        RequestedByName = e.RequestedBy?.FullName ?? string.Empty,
        ContractorName = e.Contractor?.CompanyName,
        PlannedStartDate = e.PlannedStartDate,
        PlannedEndDate = e.PlannedEndDate,
        Status = e.Status,
        IsSuspended = e.IsSuspended,
    };

    public static ShePermitToWork ToEntity(this CreateShePermitToWorkDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        PermitType = dto.PermitType,
        WorkDescription = dto.WorkDescription,
        LocationId = dto.LocationId,
        SpecificArea = dto.SpecificArea,
        RequestedById = dto.RequestedById,
        ContractorId = dto.ContractorId,
        RequestedDate = dto.RequestedDate,
        PlannedStartDate = dto.PlannedStartDate,
        PlannedStartTime = dto.PlannedStartTime,
        PlannedEndDate = dto.PlannedEndDate,
        PlannedEndTime = dto.PlannedEndTime,
        Status = ShePermitStatus.Draft,
        HazardsIdentified = dto.HazardsIdentified,
        ControlMeasures = dto.ControlMeasures,
        PpeRequired = dto.PpeRequired,
        GasTestResults = dto.GasTestResults,
        IsolationDetails = dto.IsolationDetails,
        RiskAssessmentId = dto.RiskAssessmentId,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this ShePermitToWork e, UpdateShePermitToWorkDto dto, Guid userId)
    {
        e.PermitType = dto.PermitType;
        e.WorkDescription = dto.WorkDescription;
        e.LocationId = dto.LocationId;
        e.SpecificArea = dto.SpecificArea;
        e.ContractorId = dto.ContractorId;
        e.PlannedStartDate = dto.PlannedStartDate;
        e.PlannedStartTime = dto.PlannedStartTime;
        e.PlannedEndDate = dto.PlannedEndDate;
        e.PlannedEndTime = dto.PlannedEndTime;
        e.ActualStartDate = dto.ActualStartDate;
        e.ActualEndDate = dto.ActualEndDate;
        e.HazardsIdentified = dto.HazardsIdentified;
        e.ControlMeasures = dto.ControlMeasures;
        e.PpeRequired = dto.PpeRequired;
        e.GasTestResults = dto.GasTestResults;
        e.IsolationDetails = dto.IsolationDetails;
        e.RiskAssessmentId = dto.RiskAssessmentId;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<ShePermitToWorkSummaryDto> ToSummaryDtoList(this IEnumerable<ShePermitToWork> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static ShePermitToWorkWorkerDto ToDto(this ShePermitToWorkWorker e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        PermitToWorkId = e.PermitToWorkId,
        EmployeeId = e.EmployeeId,
        WorkerName = e.WorkerName,
        CompanyName = e.CompanyName,
        TradeOrRole = e.TradeOrRole,
        Briefed = e.Briefed,
        BriefedDate = e.BriefedDate,
        SignedOff = e.SignedOff,
        SignedDate = e.SignedDate,
    };

    public static ShePermitToWorkWorker ToEntity(this CreateShePermitToWorkWorkerDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        PermitToWorkId = dto.PermitToWorkId,
        EmployeeId = dto.EmployeeId,
        WorkerName = dto.WorkerName,
        CompanyName = dto.CompanyName,
        TradeOrRole = dto.TradeOrRole,
        Briefed = dto.Briefed,
        BriefedDate = dto.BriefedDate,
        SignedOff = dto.SignedOff,
        SignedDate = dto.SignedDate,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this ShePermitToWorkWorker e, UpdateShePermitToWorkWorkerDto dto, Guid userId)
    {
        e.WorkerName = dto.WorkerName;
        e.CompanyName = dto.CompanyName;
        e.TradeOrRole = dto.TradeOrRole;
        e.Briefed = dto.Briefed;
        e.BriefedDate = dto.BriefedDate;
        e.SignedOff = dto.SignedOff;
        e.SignedDate = dto.SignedDate;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static ShePermitToWorkExtensionDto ToDto(this ShePermitToWorkExtension e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        PermitToWorkId = e.PermitToWorkId,
        ExtensionNumber = e.ExtensionNumber,
        NewEndDate = e.NewEndDate,
        NewEndTime = e.NewEndTime,
        Reason = e.Reason,
        ApprovedById = e.ApprovedById,
        ApprovedByName = e.ApprovedBy?.FullName ?? string.Empty,
        ApprovedDate = e.ApprovedDate,
    };

    public static ShePermitToWorkExtension ToEntity(this CreateShePermitToWorkExtensionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        PermitToWorkId = dto.PermitToWorkId,
        ExtensionNumber = dto.ExtensionNumber,
        NewEndDate = dto.NewEndDate,
        NewEndTime = dto.NewEndTime,
        Reason = dto.Reason,
        ApprovedById = dto.ApprovedById,
        ApprovedDate = dto.ApprovedDate,
        CreatedBy = userId.ToString(),
    };

    public static ShePermitToWorkDocumentDto ToDto(this ShePermitToWorkDocument e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        PermitToWorkId = e.PermitToWorkId,
        FileName = e.FileName,
        FilePath = e.FilePath,
        Description = e.Description,
        UploadDate = e.UploadDate,
        UploadedById = e.UploadedById,
        UploadedByName = e.UploadedBy?.FullName ?? string.Empty,
    };

    public static ShePermitToWorkDocument ToEntity(this CreateShePermitToWorkDocumentDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        PermitToWorkId = dto.PermitToWorkId,
        FileName = dto.FileName,
        FilePath = dto.FilePath,
        Description = dto.Description,
        UploadDate = DateTime.UtcNow,
        UploadedById = dto.UploadedById,
        CreatedBy = userId.ToString(),
    };

    #endregion

    // ========================================================================
    // F. PPE MANAGEMENT
    // ========================================================================

    #region PpeType

    public static PpeTypeDto ToDto(this PpeType e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        Code = e.Code,
        Name = e.Name,
        Description = e.Description,
        Category = e.Category,
        Standard = e.Standard,
        LifespanMonths = e.LifespanMonths,
        RequiresSerialNumber = e.RequiresSerialNumber,
        HasExpiryDate = e.HasExpiryDate,
        IsActive = e.IsActive,
    };

    public static PpeType ToEntity(this CreatePpeTypeDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        Code = dto.Code,
        Name = dto.Name,
        Description = dto.Description,
        Category = dto.Category,
        Standard = dto.Standard,
        LifespanMonths = dto.LifespanMonths,
        RequiresSerialNumber = dto.RequiresSerialNumber,
        HasExpiryDate = dto.HasExpiryDate,
        IsActive = dto.IsActive,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this PpeType e, UpdatePpeTypeDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.Description = dto.Description;
        e.Category = dto.Category;
        e.Standard = dto.Standard;
        e.LifespanMonths = dto.LifespanMonths;
        e.RequiresSerialNumber = dto.RequiresSerialNumber;
        e.HasExpiryDate = dto.HasExpiryDate;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region PpeInventory

    public static PpeInventoryDto ToDto(this PpeInventory e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        PpeTypeId = e.PpeTypeId,
        PpeTypeName = e.PpeType?.Name ?? string.Empty,
        ItemCode = e.ItemCode,
        Brand = e.Brand,
        Model = e.Model,
        Size = e.Size,
        QuantityInStock = e.QuantityInStock,
        MinimumStockLevel = e.MinimumStockLevel,
        ReorderLevel = e.ReorderLevel,
        StorageLocation = e.StorageLocation,
        UnitCost = e.UnitCost,
        Supplier = e.Supplier,
        LastRestockDate = e.LastRestockDate,
        LastRestockedById = e.LastRestockedById,
        LastRestockedByName = e.LastRestockedBy?.FullName,
    };

    public static PpeInventory ToEntity(this CreatePpeInventoryDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        PpeTypeId = dto.PpeTypeId,
        ItemCode = dto.ItemCode,
        Brand = dto.Brand,
        Model = dto.Model,
        Size = dto.Size,
        QuantityInStock = dto.QuantityInStock,
        MinimumStockLevel = dto.MinimumStockLevel,
        ReorderLevel = dto.ReorderLevel,
        StorageLocation = dto.StorageLocation,
        UnitCost = dto.UnitCost,
        Supplier = dto.Supplier,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this PpeInventory e, UpdatePpeInventoryDto dto, Guid userId)
    {
        e.Brand = dto.Brand;
        e.Model = dto.Model;
        e.Size = dto.Size;
        e.QuantityInStock = dto.QuantityInStock;
        e.MinimumStockLevel = dto.MinimumStockLevel;
        e.ReorderLevel = dto.ReorderLevel;
        e.StorageLocation = dto.StorageLocation;
        e.UnitCost = dto.UnitCost;
        e.Supplier = dto.Supplier;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    #region PpeIssuance

    public static PpeIssuanceDto ToDto(this PpeIssuance e) => new()
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
        PpeTypeId = e.PpeTypeId,
        PpeTypeName = e.PpeType?.Name ?? string.Empty,
        IssueDate = e.IssueDate,
        Quantity = e.Quantity,
        Size = e.Size,
        SerialNumber = e.SerialNumber,
        ExpiryDate = e.ExpiryDate,
        ExpectedReturnDate = e.ExpectedReturnDate,
        ActualReturnDate = e.ActualReturnDate,
        ConditionWhenIssued = e.ConditionWhenIssued,
        ConditionWhenReturned = e.ConditionWhenReturned,
        IssuedById = e.IssuedById,
        IssuedByName = e.IssuedBy?.FullName ?? string.Empty,
        IsReturned = e.IsReturned,
        ReturnedToId = e.ReturnedToId,
        ReturnedToName = e.ReturnedTo?.FullName,
        Notes = e.Notes,
    };

    public static PpeIssuance ToEntity(this CreatePpeIssuanceDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        EmployeeId = dto.EmployeeId,
        PpeTypeId = dto.PpeTypeId,
        IssueDate = dto.IssueDate,
        Quantity = dto.Quantity,
        Size = dto.Size,
        SerialNumber = dto.SerialNumber,
        ExpiryDate = dto.ExpiryDate,
        ExpectedReturnDate = dto.ExpectedReturnDate,
        ConditionWhenIssued = dto.ConditionWhenIssued,
        IssuedById = dto.IssuedById,
        IsReturned = false,
        Notes = dto.Notes,
        CreatedBy = userId.ToString(),
    };

    #endregion

    #region JobRolePpeRequirement

    public static JobRolePpeRequirementDto ToDto(this JobRolePpeRequirement e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        JobRoleCode = e.JobRoleCode,
        JobRoleName = e.JobRoleName,
        PpeTypeId = e.PpeTypeId,
        PpeTypeName = e.PpeType?.Name ?? string.Empty,
        Quantity = e.Quantity,
        ReplacementFrequencyMonths = e.ReplacementFrequencyMonths,
        IsMandatory = e.IsMandatory,
    };

    public static JobRolePpeRequirement ToEntity(this CreateJobRolePpeRequirementDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        JobRoleCode = dto.JobRoleCode,
        JobRoleName = dto.JobRoleName,
        PpeTypeId = dto.PpeTypeId,
        Quantity = dto.Quantity,
        ReplacementFrequencyMonths = dto.ReplacementFrequencyMonths,
        IsMandatory = dto.IsMandatory,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this JobRolePpeRequirement e, UpdateJobRolePpeRequirementDto dto, Guid userId)
    {
        e.JobRoleName = dto.JobRoleName;
        e.Quantity = dto.Quantity;
        e.ReplacementFrequencyMonths = dto.ReplacementFrequencyMonths;
        e.IsMandatory = dto.IsMandatory;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // G. SAFETY EQUIPMENT
    // ========================================================================

    #region SafetyEquipment

    public static SafetyEquipmentDto ToDto(this SafetyEquipment e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        EquipmentNumber = e.EquipmentNumber,
        Name = e.Name,
        Description = e.Description,
        Type = e.Type,
        LocationId = e.LocationId,
        LocationName = e.Location?.Name ?? string.Empty,
        SpecificArea = e.SpecificArea,
        OrganizationUnitId = e.OrganizationUnitId,
        OrganizationUnitName = e.OrganizationUnit?.Name,
        Manufacturer = e.Manufacturer,
        Model = e.Model,
        SerialNumber = e.SerialNumber,
        PurchaseDate = e.PurchaseDate,
        InstallationDate = e.InstallationDate,
        RequiresRegularInspection = e.RequiresRegularInspection,
        InspectionFrequencyDays = e.InspectionFrequencyDays,
        LastInspectionDate = e.LastInspectionDate,
        NextInspectionDueDate = e.NextInspectionDueDate,
        RequiresCertification = e.RequiresCertification,
        CertificationExpiryDate = e.CertificationExpiryDate,
        CertificationDocumentPath = e.CertificationDocumentPath,
        Status = e.Status,
        OutOfServiceDate = e.OutOfServiceDate,
        OutOfServiceReason = e.OutOfServiceReason,
        ExpiryDate = e.ExpiryDate,
        LastMaintenanceDate = e.LastMaintenanceDate,
        NextMaintenanceDueDate = e.NextMaintenanceDueDate,
        ResponsiblePersonId = e.ResponsiblePersonId,
        ResponsiblePersonName = e.ResponsiblePerson?.FullName,
        Notes = e.Notes,
        Inspections = e.Inspections.Select(i => i.ToDto()).ToList(),
        MaintenanceRecords = e.MaintenanceRecords.Select(m => m.ToDto()).ToList(),
    };

    public static SafetyEquipmentSummaryDto ToSummaryDto(this SafetyEquipment e) => new()
    {
        Id = e.Id,
        EquipmentNumber = e.EquipmentNumber,
        Name = e.Name,
        Type = e.Type,
        LocationName = e.Location?.Name ?? string.Empty,
        Status = e.Status,
        NextInspectionDueDate = e.NextInspectionDueDate,
        CertificationExpiryDate = e.CertificationExpiryDate,
    };

    public static SafetyEquipment ToEntity(this CreateSafetyEquipmentDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        EquipmentNumber = dto.EquipmentNumber,
        Name = dto.Name,
        Description = dto.Description,
        Type = dto.Type,
        LocationId = dto.LocationId,
        SpecificArea = dto.SpecificArea,
        OrganizationUnitId = dto.OrganizationUnitId,
        Manufacturer = dto.Manufacturer,
        Model = dto.Model,
        SerialNumber = dto.SerialNumber,
        PurchaseDate = dto.PurchaseDate,
        InstallationDate = dto.InstallationDate,
        RequiresRegularInspection = dto.RequiresRegularInspection,
        InspectionFrequencyDays = dto.InspectionFrequencyDays,
        RequiresCertification = dto.RequiresCertification,
        CertificationExpiryDate = dto.CertificationExpiryDate,
        ExpiryDate = dto.ExpiryDate,
        Status = SheSafetyEquipmentStatus.Operational,
        ResponsiblePersonId = dto.ResponsiblePersonId,
        Notes = dto.Notes,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyEquipment e, UpdateSafetyEquipmentDto dto, Guid userId)
    {
        e.Name = dto.Name;
        e.Description = dto.Description;
        e.Type = dto.Type;
        e.LocationId = dto.LocationId;
        e.SpecificArea = dto.SpecificArea;
        e.OrganizationUnitId = dto.OrganizationUnitId;
        e.Manufacturer = dto.Manufacturer;
        e.Model = dto.Model;
        e.SerialNumber = dto.SerialNumber;
        e.PurchaseDate = dto.PurchaseDate;
        e.InstallationDate = dto.InstallationDate;
        e.RequiresRegularInspection = dto.RequiresRegularInspection;
        e.InspectionFrequencyDays = dto.InspectionFrequencyDays;
        e.LastInspectionDate = dto.LastInspectionDate;
        e.NextInspectionDueDate = dto.NextInspectionDueDate;
        e.RequiresCertification = dto.RequiresCertification;
        e.CertificationExpiryDate = dto.CertificationExpiryDate;
        e.CertificationDocumentPath = dto.CertificationDocumentPath;
        e.Status = dto.Status;
        e.OutOfServiceDate = dto.OutOfServiceDate;
        e.OutOfServiceReason = dto.OutOfServiceReason;
        e.ExpiryDate = dto.ExpiryDate;
        e.LastMaintenanceDate = dto.LastMaintenanceDate;
        e.NextMaintenanceDueDate = dto.NextMaintenanceDueDate;
        e.ResponsiblePersonId = dto.ResponsiblePersonId;
        e.Notes = dto.Notes;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<SafetyEquipmentSummaryDto> ToSummaryDtoList(this IEnumerable<SafetyEquipment> entities)
        => entities.Select(e => e.ToSummaryDto());

    public static SafetyEquipmentInspectionDto ToDto(this SafetyEquipmentInspection e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        EquipmentId = e.EquipmentId,
        InspectionDate = e.InspectionDate,
        InspectionType = e.InspectionType,
        InspectedById = e.InspectedById,
        InspectedByName = e.InspectedBy?.FullName ?? string.Empty,
        Result = e.Result,
        Findings = e.Findings,
        DeficienciesNoted = e.DeficienciesNoted,
        NextInspectionDate = e.NextInspectionDate,
        InspectionActions = e.InspectionActions.Select(a => a.ToDto()).ToList(),
    };

    public static SafetyEquipmentInspection ToEntity(this CreateSafetyEquipmentInspectionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        EquipmentId = dto.EquipmentId,
        InspectionDate = dto.InspectionDate,
        InspectionType = dto.InspectionType,
        InspectedById = dto.InspectedById,
        Result = dto.Result,
        Findings = dto.Findings,
        DeficienciesNoted = dto.DeficienciesNoted,
        NextInspectionDate = dto.NextInspectionDate,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyEquipmentInspection e, UpdateSafetyEquipmentInspectionDto dto, Guid userId)
    {
        e.InspectionDate = dto.InspectionDate;
        e.InspectionType = dto.InspectionType;
        e.Result = dto.Result;
        e.Findings = dto.Findings;
        e.DeficienciesNoted = dto.DeficienciesNoted;
        e.NextInspectionDate = dto.NextInspectionDate;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SafetyEquipmentInspectionActionDto ToDto(this SafetyEquipmentInspectionAction e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        SafetyEquipmentInspectionId = e.SafetyEquipmentInspectionId,
        CorrectiveActionTemplateId = e.CorrectiveActionTemplateId,
        CorrectiveActionTemplateTitle = e.CorrectiveActionTemplate?.Title ?? string.Empty,
        Status = e.Status,
        DueDate = e.DueDate,
        CompletionDate = e.CompletionDate,
        CompletionNotes = e.CompletionNotes,
        AssignedToId = e.AssignedToId,
        AssignedToName = e.AssignedTo?.FullName,
    };

    public static SafetyEquipmentInspectionAction ToEntity(this CreateSafetyEquipmentInspectionActionDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        SafetyEquipmentInspectionId = dto.SafetyEquipmentInspectionId,
        CorrectiveActionTemplateId = dto.CorrectiveActionTemplateId,
        Status = dto.Status,
        DueDate = dto.DueDate,
        AssignedToId = dto.AssignedToId,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyEquipmentInspectionAction e, UpdateSafetyEquipmentInspectionActionDto dto, Guid userId)
    {
        e.Status = dto.Status;
        e.DueDate = dto.DueDate;
        e.CompletionDate = dto.CompletionDate;
        e.CompletionNotes = dto.CompletionNotes;
        e.AssignedToId = dto.AssignedToId;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    public static SafetyEquipmentMaintenanceDto ToDto(this SafetyEquipmentMaintenance e) => new()
    {
        Id = e.Id,
        CreatedAt = e.CreatedAt,
        CreatedBy = e.CreatedBy ?? string.Empty,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
        EquipmentId = e.EquipmentId,
        MaintenanceRecordId = e.MaintenanceRecordId,
        MaintenanceDate = e.MaintenanceDate,
        MaintenanceType = e.MaintenanceType,
        Description = e.Description,
        EquipmentTakenOutOfService = e.EquipmentTakenOutOfService,
        OutOfServiceStart = e.OutOfServiceStart,
        OutOfServiceEnd = e.OutOfServiceEnd,
        Cost = e.Cost,
        PerformedBy = e.PerformedBy,
        Notes = e.Notes,
    };

    public static SafetyEquipmentMaintenance ToEntity(this CreateSafetyEquipmentMaintenanceDto dto, Guid tenantId, Guid userId) => new()
    {
        TenantId = tenantId,
        EquipmentId = dto.EquipmentId,
        MaintenanceRecordId = dto.MaintenanceRecordId,
        MaintenanceDate = dto.MaintenanceDate,
        MaintenanceType = dto.MaintenanceType,
        Description = dto.Description,
        EquipmentTakenOutOfService = dto.EquipmentTakenOutOfService,
        OutOfServiceStart = dto.OutOfServiceStart,
        OutOfServiceEnd = dto.OutOfServiceEnd,
        Cost = dto.Cost,
        PerformedBy = dto.PerformedBy,
        Notes = dto.Notes,
        CreatedBy = userId.ToString(),
    };

    public static void UpdateEntity(this SafetyEquipmentMaintenance e, UpdateSafetyEquipmentMaintenanceDto dto, Guid userId)
    {
        e.MaintenanceDate = dto.MaintenanceDate;
        e.MaintenanceType = dto.MaintenanceType;
        e.Description = dto.Description;
        e.EquipmentTakenOutOfService = dto.EquipmentTakenOutOfService;
        e.OutOfServiceStart = dto.OutOfServiceStart;
        e.OutOfServiceEnd = dto.OutOfServiceEnd;
        e.Cost = dto.Cost;
        e.PerformedBy = dto.PerformedBy;
        e.Notes = dto.Notes;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = userId.ToString();
    }

    #endregion
}
