using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

public static class AssetsMappingExtensions
{
    #region AssetType Mappings

    public static AssetTypeDto ToDto(this AssetType entity)
    {
        return new AssetTypeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Description = entity.Description,
            HasExtraAttributes = entity.HasExtraAttributes,
            AttributeCount = entity.AssetTypeAttributes?.Count ?? 0,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AssetTypeSummaryDto ToSummaryDto(this AssetType entity, int assetCount = 0)
    {
        return new AssetTypeSummaryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            HasExtraAttributes = entity.HasExtraAttributes,
            AssetCount = assetCount,
            AttributeCount = entity.AssetTypeAttributes?.Count ?? 0
        };
    }

    public static AssetTypeDetailDto ToDetailDto(this AssetType entity)
    {
        return new AssetTypeDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Description = entity.Description,
            HasExtraAttributes = entity.HasExtraAttributes,
            AttributeCount = entity.AssetTypeAttributes?.Count ?? 0,
            Attributes = entity.AssetTypeAttributes?.Select(a => a.ToDto()).ToList() ?? new List<AssetTypeAttributeDto>(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AssetType ToEntity(this CreateAssetTypeDto dto, Guid tenantId, Guid userId)
    {
        return new AssetType
        {
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            HasExtraAttributes = dto.HasExtraAttributes,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AssetType entity, UpdateAssetTypeDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.HasExtraAttributes = dto.HasExtraAttributes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AssetTypeDto> ToDtoList(this IEnumerable<AssetType> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AssetTypeAttribute Mappings

    public static AssetTypeAttributeDto ToDto(this AssetTypeAttribute entity)
    {
        return new AssetTypeAttributeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AssetTypeId = entity.AssetTypeId,
            AssetTypeName = entity.AssetType?.Name ?? string.Empty,
            AttributeName = entity.AttributeName,
            DataType = entity.DataType,
            IsRequired = entity.IsRequired,
            IsExpiryDate = entity.IsExpiryDate,
            AttributeOptions = entity.AttributeOptions,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AssetTypeAttribute ToEntity(this CreateAssetTypeAttributeDto dto, Guid tenantId, Guid userId)
    {
        return new AssetTypeAttribute
        {
            TenantId = tenantId,
            AssetTypeId = dto.AssetTypeId,
            AttributeName = dto.AttributeName,
            DataType = dto.DataType,
            IsRequired = dto.IsRequired,
            IsExpiryDate = dto.IsExpiryDate,
            AttributeOptions = dto.AttributeOptions,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AssetTypeAttribute entity, UpdateAssetTypeAttributeDto dto, Guid userId)
    {
        entity.AssetTypeId = dto.AssetTypeId;
        entity.AttributeName = dto.AttributeName;
        entity.DataType = dto.DataType;
        entity.IsRequired = dto.IsRequired;
        entity.IsExpiryDate = dto.IsExpiryDate;
        entity.AttributeOptions = dto.AttributeOptions;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AssetTypeAttributeDto> ToDtoList(this IEnumerable<AssetTypeAttribute> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region CompanyAsset Mappings

    public static CompanyAssetDto ToDto(this CompanyAsset entity)
    {
        return new CompanyAssetDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AssetNumber = entity.AssetNumber,
            AssetTag = entity.AssetTag,
            AssetName = entity.AssetName,
            Description = entity.Description,
            AssetTypeId = entity.AssetTypeId,
            AssetTypeName = entity.AssetType?.Name ?? string.Empty,
            Manufacturer = entity.Manufacturer,
            ModelNumber = entity.ModelNumber,
            SerialNumber = entity.SerialNumber,
            PurchaseDate = entity.PurchaseDate,
            PurchaseCost = entity.PurchaseCost,
            Supplier = entity.Supplier,
            InvoiceNumber = entity.InvoiceNumber,
            HasWarranty = entity.HasWarranty,
            WarrantyStartDate = entity.WarrantyStartDate,
            WarrantyEndDate = entity.WarrantyEndDate,
            WarrantyProvider = entity.WarrantyProvider,
            Color = entity.Color,
            Size = entity.Size,
            Specifications = entity.Specifications,
            Status = entity.Status,
            Condition = entity.Condition,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Name,
            LocationDetails = entity.LocationDetails,
            IsAssignable = entity.IsAssignable,
            IsCurrentlyAssigned = entity.IsCurrentlyAssigned,
            CurrentAssignedToId = entity.CurrentAssignedToId,
            CurrentAssignedToName = entity.CurrentAssignedTo != null 
                ? $"{entity.CurrentAssignedTo.FirstName} {entity.CurrentAssignedTo.LastName}" 
                : null,
            RequiresRegularMaintenance = entity.RequiresRegularMaintenance,
            MaintenanceIntervalDays = entity.MaintenanceIntervalDays,
            LastMaintenanceDate = entity.LastMaintenanceDate,
            NextMaintenanceDate = entity.NextMaintenanceDate,
            IsInsured = entity.IsInsured,
            InsurancePolicyNumber = entity.InsurancePolicyNumber,
            InsuredValue = entity.InsuredValue,
            DisposalDate = entity.DisposalDate,
            DisposalMethod = entity.DisposalMethod,
            DisposalNotes = entity.DisposalNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static CompanyAssetSummaryDto ToSummaryDto(this CompanyAsset entity)
    {
        return new CompanyAssetSummaryDto
        {
            Id = entity.Id,
            AssetNumber = entity.AssetNumber,
            AssetTag = entity.AssetTag,
            AssetName = entity.AssetName,
            AssetTypeName = entity.AssetType?.Name ?? string.Empty,
            Status = entity.Status,
            Condition = entity.Condition,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Name,
            UnitId = entity.UnitId,
            UnitName = entity.Unit?.Name,
            IsCurrentlyAssigned = entity.IsCurrentlyAssigned,
            CurrentAssignedToName = entity.CurrentAssignedTo != null 
                ? $"{entity.CurrentAssignedTo.FirstName} {entity.CurrentAssignedTo.LastName}" 
                : null,
            CurrentValue = entity.PurchaseCost
        };
    }

    public static CompanyAssetDetailDto ToDetailDto(this CompanyAsset entity)
    {
        var dto = new CompanyAssetDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AssetNumber = entity.AssetNumber,
            AssetTag = entity.AssetTag,
            AssetName = entity.AssetName,
            Description = entity.Description,
            AssetTypeId = entity.AssetTypeId,
            AssetTypeName = entity.AssetType?.Name ?? string.Empty,
            Manufacturer = entity.Manufacturer,
            ModelNumber = entity.ModelNumber,
            SerialNumber = entity.SerialNumber,
            PurchaseDate = entity.PurchaseDate,
            PurchaseCost = entity.PurchaseCost,
            Supplier = entity.Supplier,
            InvoiceNumber = entity.InvoiceNumber,
            HasWarranty = entity.HasWarranty,
            WarrantyStartDate = entity.WarrantyStartDate,
            WarrantyEndDate = entity.WarrantyEndDate,
            WarrantyProvider = entity.WarrantyProvider,
            Color = entity.Color,
            Size = entity.Size,
            Specifications = entity.Specifications,
            Status = entity.Status,
            Condition = entity.Condition,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Name,
            LocationDetails = entity.LocationDetails,
            IsAssignable = entity.IsAssignable,
            IsCurrentlyAssigned = entity.IsCurrentlyAssigned,
            CurrentAssignedToId = entity.CurrentAssignedToId,
            CurrentAssignedToName = entity.CurrentAssignedTo != null 
                ? $"{entity.CurrentAssignedTo.FirstName} {entity.CurrentAssignedTo.LastName}" 
                : null,
            RequiresRegularMaintenance = entity.RequiresRegularMaintenance,
            MaintenanceIntervalDays = entity.MaintenanceIntervalDays,
            LastMaintenanceDate = entity.LastMaintenanceDate,
            NextMaintenanceDate = entity.NextMaintenanceDate,
            IsInsured = entity.IsInsured,
            InsurancePolicyNumber = entity.InsurancePolicyNumber,
            InsuredValue = entity.InsuredValue,
            DisposalDate = entity.DisposalDate,
            DisposalMethod = entity.DisposalMethod,
            DisposalNotes = entity.DisposalNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            AttributeValues = entity.AssetAttributeValues?.Select(v => v.ToDto()).ToList() ?? new List<AssetAttributeValueDto>(),
            RecentAssignments = entity.AssignmentHistory?.Select(a => a.ToSummaryDto()).ToList() ?? new List<AssetAssignmentSummaryDto>(),
            RecentMaintenance = entity.MaintenanceRecords?.Select(m => m.ToSummaryDto()).ToList() ?? new List<AssetMaintenanceSummaryDto>(),
            Attachments = entity.Attachments?.Select(a => a.ToDto()).ToList() ?? new List<AssetAttachmentDto>()
        };
        return dto;
    }

    public static CompanyAsset ToEntity(this CreateCompanyAssetDto dto, Guid tenantId, Guid userId)
    {
        return new CompanyAsset
        {
            TenantId = tenantId,
            AssetNumber = dto.AssetNumber,
            AssetTag = dto.AssetTag,
            AssetName = dto.AssetName,
            Description = dto.Description,
            AssetTypeId = dto.AssetTypeId,
            Manufacturer = dto.Manufacturer,
            ModelNumber = dto.ModelNumber,
            SerialNumber = dto.SerialNumber,
            PurchaseDate = dto.PurchaseDate,
            PurchaseCost = dto.PurchaseCost,
            Supplier = dto.Supplier,
            InvoiceNumber = dto.InvoiceNumber,
            HasWarranty = dto.HasWarranty,
            WarrantyStartDate = dto.WarrantyStartDate,
            WarrantyEndDate = dto.WarrantyEndDate,
            WarrantyProvider = dto.WarrantyProvider,
            Color = dto.Color,
            Size = dto.Size,
            Specifications = dto.Specifications,
            Status = dto.Status,
            Condition = dto.Condition,
            LocationId = dto.LocationId,
            LocationDetails = dto.LocationDetails,
            IsAssignable = dto.IsAssignable,
            RequiresRegularMaintenance = dto.RequiresRegularMaintenance,
            MaintenanceIntervalDays = dto.MaintenanceIntervalDays,
            IsInsured = dto.IsInsured,
            InsurancePolicyNumber = dto.InsurancePolicyNumber,
            InsuredValue = dto.InsuredValue,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this CompanyAsset entity, UpdateCompanyAssetDto dto, Guid userId)
    {
        entity.AssetNumber = dto.AssetNumber;
        entity.AssetTag = dto.AssetTag;
        entity.AssetName = dto.AssetName;
        entity.Description = dto.Description;
        entity.AssetTypeId = dto.AssetTypeId;
        entity.Manufacturer = dto.Manufacturer;
        entity.ModelNumber = dto.ModelNumber;
        entity.SerialNumber = dto.SerialNumber;
        entity.PurchaseDate = dto.PurchaseDate;
        entity.PurchaseCost = dto.PurchaseCost;
        entity.Supplier = dto.Supplier;
        entity.InvoiceNumber = dto.InvoiceNumber;
        entity.HasWarranty = dto.HasWarranty;
        entity.WarrantyStartDate = dto.WarrantyStartDate;
        entity.WarrantyEndDate = dto.WarrantyEndDate;
        entity.WarrantyProvider = dto.WarrantyProvider;
        entity.Color = dto.Color;
        entity.Size = dto.Size;
        entity.Specifications = dto.Specifications;
        entity.Status = dto.Status;
        entity.Condition = dto.Condition;
        entity.LocationId = dto.LocationId;
        entity.LocationDetails = dto.LocationDetails;
        entity.IsAssignable = dto.IsAssignable;
        entity.RequiresRegularMaintenance = dto.RequiresRegularMaintenance;
        entity.MaintenanceIntervalDays = dto.MaintenanceIntervalDays;
        entity.LastMaintenanceDate = dto.LastMaintenanceDate;
        entity.NextMaintenanceDate = dto.NextMaintenanceDate;
        entity.IsInsured = dto.IsInsured;
        entity.InsurancePolicyNumber = dto.InsurancePolicyNumber;
        entity.InsuredValue = dto.InsuredValue;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<CompanyAssetSummaryDto> ToSummaryDtoList(this IEnumerable<CompanyAsset> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region AssetAttributeValue Mappings

    public static AssetAttributeValueDto ToDto(this AssetAttributeValue entity)
    {
        return new AssetAttributeValueDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AssetId = entity.AssetId,
            AssetTypeAttributeId = entity.AssetTypeAttributeId,
            AttributeName = entity.AssetTypeAttribute?.AttributeName ?? string.Empty,
            DataType = entity.AssetTypeAttribute?.DataType ?? default,
            Value = entity.Value,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AssetAttributeValue ToEntity(this CreateAssetAttributeValueDto dto, Guid tenantId, Guid assetId, Guid userId)
    {
        return new AssetAttributeValue
        {
            TenantId = tenantId,
            AssetId = assetId,
            AssetTypeAttributeId = dto.AssetTypeAttributeId,
            Value = dto.Value,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AssetAttributeValue entity, UpdateAssetAttributeValueDto dto, Guid userId)
    {
        entity.Value = dto.Value;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AssetAttributeValueDto> ToDtoList(this IEnumerable<AssetAttributeValue> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AssetAssignment Mappings

    public static AssetAssignmentDto ToDto(this AssetAssignment entity)
    {
        return new AssetAssignmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AssignmentNumber = entity.AssignmentNumber,
            AssetId = entity.AssetId,
            AssetName = entity.Asset?.AssetName ?? string.Empty,
            AssetNumber = entity.Asset?.AssetNumber ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee != null 
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}" 
                : string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            AssignmentDate = entity.AssignmentDate,
            ExpectedReturnDate = entity.ExpectedReturnDate,
            Type = entity.Type,
            Purpose = entity.Purpose,
            AssignmentNotes = entity.AssignmentNotes,
            IsPrimaryUser = entity.IsPrimaryUser,
            ConditionAtAssignment = entity.ConditionAtAssignment,
            ConditionNotes = entity.ConditionNotes,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy != null 
                ? $"{entity.ApprovedBy.FirstName} {entity.ApprovedBy.LastName}" 
                : null,
            ApprovalDate = entity.ApprovalDate,
            EmployeeAcknowledged = entity.EmployeeAcknowledged,
            AcknowledgementDate = entity.AcknowledgementDate,
            ResponsibleForLoss = entity.ResponsibleForLoss,
            ResponsibleForDamage = entity.ResponsibleForDamage,
            TermsAndConditions = entity.TermsAndConditions,
            Status = entity.Status,
            ReturnDate = entity.ReturnDate,
            ConditionAtReturn = entity.ConditionAtReturn,
            ReturnNotes = entity.ReturnNotes,
            ReturnedInGoodCondition = entity.ReturnedInGoodCondition,
            ReturnedToId = entity.ReturnedToId,
            ReturnedToName = entity.ReturnedTo != null 
                ? $"{entity.ReturnedTo.FirstName} {entity.ReturnedTo.LastName}" 
                : null,
            DamageReported = entity.DamageReported,
            DamageDescription = entity.DamageDescription,
            EmployeeLiable = entity.EmployeeLiable,
            RepairCost = entity.RepairCost,
            ReplacementCost = entity.ReplacementCost,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AssetAssignmentSummaryDto ToSummaryDto(this AssetAssignment entity)
    {
        return new AssetAssignmentSummaryDto
        {
            Id = entity.Id,
            AssignmentNumber = entity.AssignmentNumber,
            AssetName = entity.Asset?.AssetName ?? string.Empty,
            EmployeeName = entity.Employee != null 
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}" 
                : string.Empty,
            AssignmentDate = entity.AssignmentDate,
            ExpectedReturnDate = entity.ExpectedReturnDate,
            Type = entity.Type,
            Status = entity.Status,
            ReturnDate = entity.ReturnDate
        };
    }

    public static AssetAssignment ToEntity(this CreateAssetAssignmentDto dto, Guid tenantId, Guid userId, string assignmentNumber)
    {
        return new AssetAssignment
        {
            TenantId = tenantId,
            AssignmentNumber = assignmentNumber,
            AssetId = dto.AssetId,
            EmployeeId = dto.EmployeeId,
            AssignmentDate = dto.AssignmentDate,
            ExpectedReturnDate = dto.ExpectedReturnDate,
            Type = dto.Type,
            Purpose = dto.Purpose,
            AssignmentNotes = dto.AssignmentNotes,
            IsPrimaryUser = dto.IsPrimaryUser,
            ConditionAtAssignment = dto.ConditionAtAssignment,
            ConditionNotes = dto.ConditionNotes,
            ApprovedById = dto.ApprovedById,
            ApprovalDate = dto.ApprovedById != null ? DateTime.UtcNow : null,
            ResponsibleForLoss = dto.ResponsibleForLoss,
            ResponsibleForDamage = dto.ResponsibleForDamage,
            TermsAndConditions = dto.TermsAndConditions,
            Status = AssignmentStatus.Active,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AssetAssignment entity, UpdateAssetAssignmentDto dto, Guid userId)
    {
        entity.ExpectedReturnDate = dto.ExpectedReturnDate;
        entity.AssignmentNotes = dto.AssignmentNotes;
        entity.IsPrimaryUser = dto.IsPrimaryUser;
        entity.ResponsibleForLoss = dto.ResponsibleForLoss;
        entity.ResponsibleForDamage = dto.ResponsibleForDamage;
        entity.TermsAndConditions = dto.TermsAndConditions;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AssetAssignmentSummaryDto> ToSummaryDtoList(this IEnumerable<AssetAssignment> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region AssetMaintenance Mappings

    public static AssetMaintenanceDto ToDto(this AssetMaintenance entity)
    {
        return new AssetMaintenanceDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            MaintenanceNumber = entity.MaintenanceNumber,
            AssetId = entity.AssetId,
            AssetName = entity.Asset?.AssetName ?? string.Empty,
            AssetNumber = entity.Asset?.AssetNumber ?? string.Empty,
            MaintenanceDate = entity.MaintenanceDate,
            Type = entity.Type,
            Description = entity.Description,
            WorkPerformed = entity.WorkPerformed,
            PartsReplaced = entity.PartsReplaced,
            IsInternalMaintenance = entity.IsInternalMaintenance,
            PerformedById = entity.PerformedById,
            PerformedByName = entity.PerformedBy != null 
                ? $"{entity.PerformedBy.FirstName} {entity.PerformedBy.LastName}" 
                : null,
            ExternalServiceProvider = entity.ExternalServiceProvider,
            ServiceTicketNumber = entity.ServiceTicketNumber,
            Cost = entity.Cost,
            NextMaintenanceDate = entity.NextMaintenanceDate,
            Status = entity.Status,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AssetMaintenanceSummaryDto ToSummaryDto(this AssetMaintenance entity)
    {
        return new AssetMaintenanceSummaryDto
        {
            Id = entity.Id,
            MaintenanceNumber = entity.MaintenanceNumber,
            AssetName = entity.Asset?.AssetName ?? string.Empty,
            MaintenanceDate = entity.MaintenanceDate,
            Type = entity.Type,
            Status = entity.Status,
            Cost = entity.Cost,
            NextMaintenanceDate = entity.NextMaintenanceDate
        };
    }

    public static AssetMaintenance ToEntity(this CreateAssetMaintenanceDto dto, Guid tenantId, Guid userId, string maintenanceNumber)
    {
        return new AssetMaintenance
        {
            TenantId = tenantId,
            MaintenanceNumber = maintenanceNumber,
            AssetId = dto.AssetId,
            MaintenanceDate = dto.MaintenanceDate,
            Type = dto.Type,
            Description = dto.Description,
            WorkPerformed = dto.WorkPerformed,
            PartsReplaced = dto.PartsReplaced,
            IsInternalMaintenance = dto.IsInternalMaintenance,
            PerformedById = dto.PerformedById,
            ExternalServiceProvider = dto.ExternalServiceProvider,
            ServiceTicketNumber = dto.ServiceTicketNumber,
            Cost = dto.Cost,
            NextMaintenanceDate = dto.NextMaintenanceDate,
            Status = dto.Status,
            Notes = dto.Notes,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AssetMaintenance entity, UpdateAssetMaintenanceDto dto, Guid userId)
    {
        entity.MaintenanceDate = dto.MaintenanceDate;
        entity.Type = dto.Type;
        entity.Description = dto.Description;
        entity.WorkPerformed = dto.WorkPerformed;
        entity.PartsReplaced = dto.PartsReplaced;
        entity.IsInternalMaintenance = dto.IsInternalMaintenance;
        entity.PerformedById = dto.PerformedById;
        entity.ExternalServiceProvider = dto.ExternalServiceProvider;
        entity.ServiceTicketNumber = dto.ServiceTicketNumber;
        entity.Cost = dto.Cost;
        entity.NextMaintenanceDate = dto.NextMaintenanceDate;
        entity.Status = dto.Status;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AssetMaintenanceSummaryDto> ToSummaryDtoList(this IEnumerable<AssetMaintenance> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region AssetImage Mappings

    public static AssetImageDto ToDto(this AssetImage entity)
    {
        return new AssetImageDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AssetId = entity.AssetId,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            UploadDate = entity.UploadDate,
            UploadedBy = entity.CreatedBy
        };
    }

    public static AssetImage ToEntity(this CreateAssetImageDto dto, Guid tenantId, Guid assetId, Guid userId)
    {
        return new AssetImage
        {
            TenantId = tenantId,
            AssetId = assetId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            UploadDate = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        };
    }

    public static List<AssetImageDto> ToDtoList(this IEnumerable<AssetImage> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AssetAttachment Mappings

    public static AssetAttachmentDto ToDto(this AssetAttachment entity)
    {
        return new AssetAttachmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AssetId = entity.AssetId,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AssetAttachment ToEntity(this CreateAssetAttachmentDto dto, Guid tenantId, Guid assetId, Guid userId)
    {
        return new AssetAttachment
        {
            TenantId = tenantId,
            AssetId = assetId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        };
    }

    public static List<AssetAttachmentDto> ToDtoList(this IEnumerable<AssetAttachment> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AssetRequisition Mappings

    public static AssetRequisitionDto ToDto(this AssetRequisition entity)
    {
        return new AssetRequisitionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            RequisitionNumber = entity.RequisitionNumber,
            RequestedById = entity.RequestedById,
            RequestedByName = entity.RequestedBy != null 
                ? $"{entity.RequestedBy.FirstName} {entity.RequestedBy.LastName}" 
                : string.Empty,
            RequestDate = entity.RequestDate,
            AssetTypeId = entity.AssetTypeId,
            AssetTypeName = entity.AssetType?.Name ?? string.Empty,
            Description = entity.Description,
            Quantity = entity.Quantity,
            Priority = entity.Priority,
            Justification = entity.Justification,
            RequiredByDate = entity.RequiredByDate,
            Status = entity.Status,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy != null 
                ? $"{entity.ApprovedBy.FirstName} {entity.ApprovedBy.LastName}" 
                : null,
            ApprovalDate = entity.ApprovalDate,
            ApprovalComments = entity.ApprovalComments,
            RejectedDate = entity.RejectedDate,
            RejectionReason = entity.RejectionReason,
            IsFulfilled = entity.IsFulfilled,
            FulfilledDate = entity.FulfilledDate,
            FulfilledById = entity.FulfilledById,
            FulfilledByName = entity.FulfilledBy != null 
                ? $"{entity.FulfilledBy.FirstName} {entity.FulfilledBy.LastName}" 
                : null,
            AssignedAssetId = entity.AssignedAssetId,
            AssignedAssetName = entity.AssignedAsset?.AssetName,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AssetRequisitionSummaryDto ToSummaryDto(this AssetRequisition entity)
    {
        return new AssetRequisitionSummaryDto
        {
            Id = entity.Id,
            RequisitionNumber = entity.RequisitionNumber,
            RequestedByName = entity.RequestedBy != null 
                ? $"{entity.RequestedBy.FirstName} {entity.RequestedBy.LastName}" 
                : string.Empty,
            RequestDate = entity.RequestDate,
            AssetTypeName = entity.AssetType?.Name ?? string.Empty,
            Quantity = entity.Quantity,
            Priority = entity.Priority,
            Status = entity.Status,
            RequiredByDate = entity.RequiredByDate
        };
    }

    public static AssetRequisition ToEntity(this CreateAssetRequisitionDto dto, Guid tenantId, Guid requestedById, Guid userId, string requisitionNumber)
    {
        return new AssetRequisition
        {
            TenantId = tenantId,
            RequisitionNumber = requisitionNumber,
            RequestedById = requestedById,
            RequestDate = DateTime.UtcNow,
            AssetTypeId = dto.AssetTypeId,
            Description = dto.Description,
            Quantity = dto.Quantity,
            Priority = dto.Priority,
            Justification = dto.Justification,
            RequiredByDate = dto.RequiredByDate,
            Status = dto.Status,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AssetRequisition entity, UpdateAssetRequisitionDto dto, Guid userId)
    {
        entity.AssetTypeId = dto.AssetTypeId;
        entity.Description = dto.Description;
        entity.Quantity = dto.Quantity;
        entity.Priority = dto.Priority;
        entity.Justification = dto.Justification;
        entity.RequiredByDate = dto.RequiredByDate;
        
        // Update status only if provided
        if (dto.Status.HasValue)
        {
            entity.Status = dto.Status.Value;
        }
        
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AssetRequisitionSummaryDto> ToSummaryDtoList(this IEnumerable<AssetRequisition> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region AssetTransfer Mappings

    public static AssetTransferDto ToDto(this AssetTransfer entity)
    {
        return new AssetTransferDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TransferNumber = entity.TransferNumber,
            AssetId = entity.AssetId,
            AssetName = entity.Asset?.AssetName ?? string.Empty,
            AssetNumber = entity.Asset?.AssetNumber ?? string.Empty,
            TransferDate = entity.TransferDate,
            Type = entity.Type,
            FromEmployeeId = entity.FromEmployeeId,
            FromEmployeeName = entity.FromEmployee != null 
                ? $"{entity.FromEmployee.FirstName} {entity.FromEmployee.LastName}" 
                : null,
            FromLocationId = entity.FromLocationId,
            FromLocationName = entity.FromLocation?.Name,
            ToEmployeeId = entity.ToEmployeeId,
            ToEmployeeName = entity.ToEmployee != null 
                ? $"{entity.ToEmployee.FirstName} {entity.ToEmployee.LastName}" 
                : null,
            ToLocationId = entity.ToLocationId,
            ToLocationName = entity.ToLocation?.Name,
            FromUnitId = entity.FromUnitId,
            FromUnitName = entity.FromUnit?.Name,
            ToUnitId = entity.ToUnitId,
            ToUnitName = entity.ToUnit?.Name,
            TransferReason = entity.TransferReason,
            InitiatedById = entity.InitiatedById,
            InitiatedByName = entity.InitiatedBy != null 
                ? $"{entity.InitiatedBy.FirstName} {entity.InitiatedBy.LastName}" 
                : string.Empty,
            Status = entity.Status,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy != null 
                ? $"{entity.ApprovedBy.FirstName} {entity.ApprovedBy.LastName}" 
                : null,
            ApprovalDate = entity.ApprovalDate,
            CompletionDate = entity.CompletionDate,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AssetTransferSummaryDto ToSummaryDto(this AssetTransfer entity)
    {
        string? fromName = null;
        string? toName = null;

        if (entity.Type == HRAssetTransferType.EmployeeToEmployee)
        {
            fromName = entity.FromEmployee != null 
                ? $"{entity.FromEmployee.FirstName} {entity.FromEmployee.LastName}" 
                : null;
            toName = entity.ToEmployee != null 
                ? $"{entity.ToEmployee.FirstName} {entity.ToEmployee.LastName}" 
                : null;
        }
        else if (entity.Type == HRAssetTransferType.LocationToLocation)
        {
            fromName = entity.FromLocation?.Name;
            toName = entity.ToLocation?.Name;
        }
        else if (entity.Type == HRAssetTransferType.UnitToUnit)
        {
            fromName = entity.FromUnit?.Name;
            toName = entity.ToUnit?.Name;
        }

        return new AssetTransferSummaryDto
        {
            Id = entity.Id,
            TransferNumber = entity.TransferNumber,
            AssetName = entity.Asset?.AssetName ?? string.Empty,
            TransferDate = entity.TransferDate,
            Type = entity.Type,
            FromName = fromName,
            ToName = toName,
            Status = entity.Status
        };
    }

    public static AssetTransfer ToEntity(this CreateAssetTransferDto dto, Guid tenantId, Guid initiatedById, Guid userId, string transferNumber)
    {
        return new AssetTransfer
        {
            TenantId = tenantId,
            TransferNumber = transferNumber,
            AssetId = dto.AssetId,
            TransferDate = dto.TransferDate,
            Type = dto.Type,
            FromEmployeeId = dto.FromEmployeeId,
            FromLocationId = dto.FromLocationId,
            FromUnitId = dto.FromUnitId,
            ToEmployeeId = dto.ToEmployeeId,
            ToLocationId = dto.ToLocationId,
            ToUnitId = dto.ToUnitId,
            TransferReason = dto.TransferReason,
            InitiatedById = initiatedById,
            Status = HRAssetTransferStatus.Pending,
            Notes = dto.Notes,
            CreatedBy = userId.ToString()
        };
    }

    public static void UpdateEntity(this AssetTransfer entity, UpdateAssetTransferDto dto, Guid userId)
    {
        entity.TransferDate = dto.TransferDate;
        entity.TransferReason = dto.TransferReason;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static List<AssetTransferSummaryDto> ToSummaryDtoList(this IEnumerable<AssetTransfer> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion
}
