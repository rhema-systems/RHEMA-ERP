using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
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
            AdditionalRemarks = entity.AdditionalRemarks,
            Source = entity.Source,
            FixedAssetId = entity.FixedAssetId,
            MaintenanceAssetId = entity.MaintenanceAssetId,
            UnitId = entity.UnitId,
            UnitName = entity.Unit?.Name,
            InsuranceExpiryDate = entity.InsuranceExpiryDate,
            IsRentable = entity.IsRentable,
            StandardRentalAmount = entity.StandardRentalAmount,
            RentalCurrencyCode = entity.RentalCurrencyCode,
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
            // D-jj. It was called CurrentValue and mapped from PurchaseCost, so every register
            // list answered the acquisition cost under a name promising a depreciated one.
            PurchaseCost = entity.PurchaseCost,
            IsRentable = entity.IsRentable,
            Source = entity.Source
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
            AdditionalRemarks = entity.AdditionalRemarks,
            Source = entity.Source,
            FixedAssetId = entity.FixedAssetId,
            MaintenanceAssetId = entity.MaintenanceAssetId,
            UnitId = entity.UnitId,
            UnitName = entity.Unit?.Name,
            InsuranceExpiryDate = entity.InsuranceExpiryDate,
            IsRentable = entity.IsRentable,
            StandardRentalAmount = entity.StandardRentalAmount,
            RentalCurrencyCode = entity.RentalCurrencyCode,
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
            MaintenanceAssetId = dto.MaintenanceAssetId,
            RequiresRegularMaintenance = dto.RequiresRegularMaintenance,
            MaintenanceIntervalDays = dto.MaintenanceIntervalDays,
            IsInsured = dto.IsInsured,
            InsurancePolicyNumber = dto.InsurancePolicyNumber,
            InsuredValue = dto.InsuredValue,
            InsuranceExpiryDate = dto.InsuranceExpiryDate,
            IsRentable = dto.IsRentable,
            StandardRentalAmount = dto.StandardRentalAmount,
            RentalCurrencyCode = dto.RentalCurrencyCode,
            AdditionalRemarks = dto.AdditionalRemarks,
            UnitId = dto.UnitId,
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
        entity.MaintenanceAssetId = dto.MaintenanceAssetId;
        entity.RequiresRegularMaintenance = dto.RequiresRegularMaintenance;
        entity.MaintenanceIntervalDays = dto.MaintenanceIntervalDays;
        entity.LastMaintenanceDate = dto.LastMaintenanceDate;
        entity.NextMaintenanceDate = dto.NextMaintenanceDate;
        entity.IsInsured = dto.IsInsured;
        entity.InsurancePolicyNumber = dto.InsurancePolicyNumber;
        entity.InsuredValue = dto.InsuredValue;
        entity.InsuranceExpiryDate = dto.InsuranceExpiryDate;
        entity.IsRentable = dto.IsRentable;
        entity.StandardRentalAmount = dto.StandardRentalAmount;
        entity.RentalCurrencyCode = dto.RentalCurrencyCode;
        entity.AdditionalRemarks = dto.AdditionalRemarks;
        entity.UnitId = dto.UnitId;
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
            // D-ll. Both halves land together: the `.Include` for `Asset.AssetType` was already on
            // `GetWithDetailsAsync`, so this line is the whole fix and the field was blank without it.
            AssetTypeName = entity.Asset?.AssetType?.Name ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            RequisitionId = entity.RequisitionId,
            RequisitionNumber = entity.Requisition?.RequisitionNumber,
            TransferId = entity.TransferId,
            TransferNumber = entity.Transfer?.TransferNumber,
            TermsDocumentSentAt = entity.TermsDocumentSentAt,
            TermsDocumentSentTo = entity.TermsDocumentSentTo,
            TermsDocumentSentByName = entity.TermsDocumentSentBy != null
                ? $"{entity.TermsDocumentSentBy.FirstName} {entity.TermsDocumentSentBy.LastName}"
                : null,
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
            RentalAmount = entity.RentalAmount,
            RentalCurrencyCode = entity.RentalCurrencyCode,
            RentalFrequency = entity.RentalFrequency,
            RentalEffectiveFrom = entity.RentalEffectiveFrom,
            RentalEffectiveTo = entity.RentalEffectiveTo,
            IsBenefitInKind = entity.IsBenefitInKind,
            BenefitInKindValue = entity.BenefitInKindValue,
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
            AssetId = entity.AssetId,
            AssetName = entity.Asset?.AssetName ?? string.Empty,
            AssetNumber = entity.Asset?.AssetNumber ?? string.Empty,
            AssetTypeName = entity.Asset?.AssetType?.Name ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee != null 
                ? $"{entity.Employee.FirstName} {entity.Employee.LastName}" 
                : string.Empty,
            AssignmentDate = entity.AssignmentDate,
            ExpectedReturnDate = entity.ExpectedReturnDate,
            Type = entity.Type,
            Status = entity.Status,
            ReturnDate = entity.ReturnDate,
            EmployeeAcknowledged = entity.EmployeeAcknowledged,
            AcknowledgementDate = entity.AcknowledgementDate,
            RentalAmount = entity.RentalAmount,
            RentalCurrencyCode = entity.RentalCurrencyCode,
            RentalFrequencyName = entity.RentalFrequency?.ToString(),
            IsBenefitInKind = entity.IsBenefitInKind
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
            MaintenanceAdmissionId = entity.MaintenanceAdmissionId,
            MaintenanceAdmissionNumber = entity.MaintenanceAdmissionNumber,
            MaintenanceDischargeId = entity.MaintenanceDischargeId,
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
            // D-ee. Kept next to the name so a future edit cannot take one and leave the other —
            // the by-id read carries all three and the two reads are asserted to agree.
            AssetId = entity.AssetId,
            AssetNumber = entity.Asset?.AssetNumber ?? string.Empty,
            AssetName = entity.Asset?.AssetName ?? string.Empty,
            MaintenanceDate = entity.MaintenanceDate,
            Type = entity.Type,
            Status = entity.Status,
            Cost = entity.Cost,
            NextMaintenanceDate = entity.NextMaintenanceDate,
            MaintenanceAdmissionNumber = entity.MaintenanceAdmissionNumber,
            // Computed the same way the detail DTO computes it, from the same two columns, so the
            // list and the by-id read cannot disagree about whether an asset is off site.
            IsAtWorkshop = entity.MaintenanceAdmissionId.HasValue && !entity.MaintenanceDischargeId.HasValue
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

    /// <summary>
    /// Projects an asset onto the maintenance watchlist row — area 16 slice 9, AST-1, D-ff.
    /// </summary>
    /// <remarks>
    /// One projection for all three reads on purpose. Due, overdue and unscheduled differ only in
    /// which assets they select; if each computed its own <c>daysRemaining</c> the three lists could
    /// disagree about the same asset on the same day, and a schedule that contradicts itself is
    /// worse than no schedule.
    /// </remarks>
    public static AssetMaintenanceDueDto ToMaintenanceDueDto(this CompanyAsset entity, DateOnly asOf)
    {
        var next = entity.NextMaintenanceDate;
        var days = next.HasValue ? next.Value.DayNumber - asOf.DayNumber : 0;

        return new AssetMaintenanceDueDto
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
            CurrentAssignedToId = entity.CurrentAssignedToId,
            CurrentAssignedToName = entity.CurrentAssignedTo != null
                ? $"{entity.CurrentAssignedTo.FirstName} {entity.CurrentAssignedTo.LastName}"
                : null,
            RequiresRegularMaintenance = entity.RequiresRegularMaintenance,
            MaintenanceIntervalDays = entity.MaintenanceIntervalDays,
            LastMaintenanceDate = entity.LastMaintenanceDate,
            NextMaintenanceDate = next,
            IsScheduled = next.HasValue,
            DaysRemaining = days,
            IsOverdue = next.HasValue && days < 0,
            AsOf = asOf,
        };
    }

    public static List<AssetMaintenanceDueDto> ToMaintenanceDueDtoList(
        this IEnumerable<CompanyAsset> entities, DateOnly asOf)
    {
        return entities.Select(e => e.ToMaintenanceDueDto(asOf)).ToList();
    }

    /// <summary>
    /// An asset as an insurance watchlist row — area 16 slice 11.
    /// </summary>
    /// <remarks>
    /// Deliberately the same shape as <see cref="ToMaintenanceDueDto"/>: the same identity, the
    /// same placement, the same holder, and a signed day count with a flag beside it. Two
    /// watchlists over one register that disagreed about how to describe a row would make every
    /// screen that shows both write the difference out twice.
    /// </remarks>
    public static AssetInsuranceWatchItemDto ToInsuranceWatchItemDto(this CompanyAsset entity, DateOnly asOf)
    {
        var expiry = entity.InsuranceExpiryDate;
        var days = expiry.HasValue ? expiry.Value.DayNumber - asOf.DayNumber : 0;

        return new AssetInsuranceWatchItemDto
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
            CurrentAssignedToId = entity.CurrentAssignedToId,
            CurrentAssignedToName = entity.CurrentAssignedTo != null
                ? $"{entity.CurrentAssignedTo.FirstName} {entity.CurrentAssignedTo.LastName}"
                : null,
            IsInsured = entity.IsInsured,
            InsurancePolicyNumber = entity.InsurancePolicyNumber,
            InsuredValue = entity.InsuredValue,
            InsuranceExpiryDate = expiry,
            IsDated = expiry.HasValue,
            DaysRemaining = days,
            IsExpired = expiry.HasValue && days < 0,
            AsOf = asOf,
        };
    }

    public static List<AssetInsuranceWatchItemDto> ToInsuranceWatchItemDtoList(
        this IEnumerable<CompanyAsset> entities, DateOnly asOf)
    {
        return entities.Select(e => e.ToInsuranceWatchItemDto(asOf)).ToList();
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
            Caption = entity.Caption,
            UploadDate = entity.UploadDate,
            UploadedBy = entity.CreatedBy,
            UploadedById = entity.UploadedById,
            FileSizeBytes = entity.FileSizeBytes,
            FileUploadRecordId = entity.FileUploadRecordId,
            DocumentRecordId = entity.DocumentRecordId,
            DocumentVersionId = entity.DocumentVersionId
        };
    }

    /// <summary>
    /// Builds an image row from a file that has ALREADY been through the controlled upload gate.
    /// </summary>
    /// <remarks>
    /// ⚠ The file name and the stored location come from <paramref name="storedFileName"/> and
    /// <paramref name="filePath"/> — the gate's, not the caller's. That is the whole difference
    /// between this and what it replaced.
    /// </remarks>
    public static AssetImage ToUploadedEntity(
        this CreateAssetImageDto dto,
        Guid tenantId,
        Guid assetId,
        Guid userId,
        Guid uploadedById,
        string storedFileName,
        string filePath,
        long? fileSizeBytes,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId)
    {
        return new AssetImage
        {
            TenantId = tenantId,
            AssetId = assetId,
            FileName = storedFileName,
            FilePath = filePath,
            Caption = dto.Caption,
            UploadDate = DateTime.UtcNow,
            UploadedById = uploadedById,
            FileSizeBytes = fileSizeBytes,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
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
            UploadedById = entity.UploadedById,
            FileSizeBytes = entity.FileSizeBytes,
            FileUploadRecordId = entity.FileUploadRecordId,
            DocumentRecordId = entity.DocumentRecordId,
            DocumentVersionId = entity.DocumentVersionId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    /// <summary>Builds an attachment row from a file already through the controlled upload gate.</summary>
    public static AssetAttachment ToUploadedEntity(
        this CreateAssetAttachmentDto dto,
        Guid tenantId,
        Guid assetId,
        Guid userId,
        Guid uploadedById,
        string storedFileName,
        string filePath,
        long? fileSizeBytes,
        Guid? fileUploadRecordId,
        Guid? documentRecordId,
        Guid? documentVersionId)
    {
        return new AssetAttachment
        {
            TenantId = tenantId,
            AssetId = assetId,
            FileName = storedFileName,
            FilePath = filePath,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow,
            UploadedById = uploadedById,
            FileSizeBytes = fileSizeBytes,
            FileUploadRecordId = fileUploadRecordId,
            DocumentRecordId = documentRecordId,
            DocumentVersionId = documentVersionId,
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
            BeneficiaryEmployeeId = entity.BeneficiaryEmployeeId,
            BeneficiaryEmployeeName = entity.BeneficiaryEmployee != null
                ? $"{entity.BeneficiaryEmployee.FirstName} {entity.BeneficiaryEmployee.LastName}"
                : null,
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
            RequestedById = entity.RequestedById,
            RequestedByName = entity.RequestedBy != null 
                ? $"{entity.RequestedBy.FirstName} {entity.RequestedBy.LastName}" 
                : string.Empty,
            // AST-6b. The field, the doc comment and the .Include on every query that feeds this
            // mapping all existed; the assignment did not, so every requisition LIST answered with
            // a blank beneficiary while the by-id read resolved it in full. The rule that catches
            // this shape - and this is now its sixth appearance in the area - is that a by-id read
            // and its list read must be asserted to agree.
            BeneficiaryEmployeeId = entity.BeneficiaryEmployeeId,
            BeneficiaryEmployeeName = entity.BeneficiaryEmployee != null
                ? $"{entity.BeneficiaryEmployee.FirstName} {entity.BeneficiaryEmployee.LastName}"
                : null,
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
            // Status is not mapped from the payload — the service sets Draft. See the note on
            // CreateAssetRequisitionDto for what a client-settable status let through.
            Status = AssetRequisitionStatus.Draft,
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

        // The status is NOT taken from the payload. It used to be — see UpdateAssetRequisitionDto.
        // BeneficiaryEmployeeId is not set here either: it needs the on-behalf authorization check,
        // so the service assigns it after calling this.
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
            // Draft, not Pending. A transfer is out for approval only once somebody submits it —
            // slice 3b split the two states apart so a recall has somewhere to land.
            Status = HRAssetTransferStatus.Draft,
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


    #region Asset Surcharges — area 16 slice 7

    private static string PersonName(Employee? e)
        => e is null ? string.Empty : $"{e.FirstName} {e.LastName}";

    public static AssetSurchargeDto ToDto(this AssetSurcharge entity)
    {
        return new AssetSurchargeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            SurchargeNumber = entity.SurchargeNumber,

            AssignmentId = entity.AssignmentId,
            AssignmentNumber = entity.Assignment?.AssignmentNumber ?? string.Empty,
            AssetId = entity.Assignment?.AssetId ?? Guid.Empty,
            AssetName = entity.Assignment?.Asset?.AssetName ?? string.Empty,
            AssetNumber = entity.Assignment?.Asset?.AssetNumber ?? string.Empty,

            EmployeeId = entity.EmployeeId,
            EmployeeName = PersonName(entity.Employee),

            Reason = entity.Reason,
            Description = entity.Description,

            AssessedAmount = entity.AssessedAmount,
            CurrencyCode = entity.CurrencyCode,
            BasisRepairCost = entity.BasisRepairCost,
            BasisReplacementCost = entity.BasisReplacementCost,
            AmountRecovered = entity.AmountRecovered,

            Status = entity.Status,
            RaisedById = entity.RaisedById,
            RaisedByName = entity.RaisedBy is null ? null : PersonName(entity.RaisedBy),
            RaisedAt = entity.RaisedAt,

            NotifiedAt = entity.NotifiedAt,
            EmployeeResponse = entity.EmployeeResponse,
            EmployeeRespondedAt = entity.EmployeeRespondedAt,
            EmployeeResponseComments = entity.EmployeeResponseComments,
            ProceededWithoutResponseReason = entity.ProceededWithoutResponseReason,

            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy is null ? null : PersonName(entity.ApprovedBy),
            ApprovalDate = entity.ApprovalDate,
            ApprovalComments = entity.ApprovalComments,
            RejectedDate = entity.RejectedDate,
            RejectionReason = entity.RejectionReason,

            RecoveryMethod = entity.RecoveryMethod,
            InstalmentCount = entity.InstalmentCount,
            RecoveryStartDate = entity.RecoveryStartDate,
            Recoveries = entity.Recoveries?
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.RecoveredOn)
                .Select(r => r.ToDto())
                .ToList() ?? [],

            WaivedById = entity.WaivedById,
            WaivedByName = entity.WaivedBy is null ? null : PersonName(entity.WaivedBy),
            WaivedAt = entity.WaivedAt,
            WaiverReason = entity.WaiverReason,
            CancelledAt = entity.CancelledAt,
            CancellationReason = entity.CancellationReason,

            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    /// <summary>
    /// The list row. ⚠ Every field it carries is one the by-id read carries identically — the rule
    /// this area has been bitten by six times is that the two must be asserted to agree.
    /// </summary>
    public static AssetSurchargeSummaryDto ToSummaryDto(this AssetSurcharge entity)
    {
        return new AssetSurchargeSummaryDto
        {
            Id = entity.Id,
            SurchargeNumber = entity.SurchargeNumber,
            AssignmentId = entity.AssignmentId,
            AssignmentNumber = entity.Assignment?.AssignmentNumber ?? string.Empty,
            AssetId = entity.Assignment?.AssetId ?? Guid.Empty,
            AssetName = entity.Assignment?.Asset?.AssetName ?? string.Empty,
            AssetNumber = entity.Assignment?.Asset?.AssetNumber ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = PersonName(entity.Employee),
            Reason = entity.Reason,
            AssessedAmount = entity.AssessedAmount,
            CurrencyCode = entity.CurrencyCode,
            AmountRecovered = entity.AmountRecovered,
            Status = entity.Status,
            EmployeeResponse = entity.EmployeeResponse,
            RaisedAt = entity.RaisedAt,
            RecoveryStartDate = entity.RecoveryStartDate
        };
    }

    public static List<AssetSurchargeSummaryDto> ToSummaryDtoList(this IEnumerable<AssetSurcharge> entities)
        => entities.Select(e => e.ToSummaryDto()).ToList();

    public static AssetSurchargeRecoveryDto ToDto(this AssetSurchargeRecovery entity)
    {
        return new AssetSurchargeRecoveryDto
        {
            Id = entity.Id,
            SurchargeId = entity.SurchargeId,
            Amount = entity.Amount,
            RecoveredOn = entity.RecoveredOn,
            Method = entity.Method,
            Reference = entity.Reference,
            Notes = entity.Notes,
            RecordedById = entity.RecordedById,
            RecordedByName = entity.RecordedBy is null ? null : PersonName(entity.RecordedBy),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    #endregion
}
