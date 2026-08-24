using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

#region Asset Type Repositories

public interface IAssetTypeRepository : IGenericRepository<AssetType>
{
    Task<IEnumerable<AssetType>> GetByTenantAsync(Guid tenantId);
    Task<AssetType?> GetWithAttributesAsync(Guid id);
    Task<AssetType?> GetByNameAsync(Guid tenantId, string name);
    Task<int> GetAssetCountByTypeAsync(Guid assetTypeId);
}

public interface IAssetTypeAttributeRepository : IGenericRepository<AssetTypeAttribute>
{
    /// <summary>
    /// One attribute with its asset type loaded — D-o(b).
    /// </summary>
    /// <remarks>
    /// The by-id read went through the generic <c>GetByIdAsync</c>, which loads no navigations, so
    /// <c>GET attributes/{id}</c> answered with a blank <c>assetTypeName</c> while the list beside
    /// it filled the same field in. Two fillings of one DTO, and nothing in the payload to tell a
    /// screen which it was holding.
    /// </remarks>
    Task<AssetTypeAttribute?> GetWithTypeAsync(Guid id);

    Task<IEnumerable<AssetTypeAttribute>> GetByAssetTypeIdAsync(Guid assetTypeId);
    Task<IEnumerable<AssetTypeAttribute>> GetRequiredAttributesAsync(Guid assetTypeId);
}

#endregion

#region Company Asset Repositories

public interface ICompanyAssetRepository : IGenericRepository<CompanyAsset>
{
    Task<IEnumerable<CompanyAsset>> GetByTenantAsync(Guid tenantId);
    Task<CompanyAsset?> GetWithDetailsAsync(Guid id);
    Task<CompanyAsset?> GetByAssetNumberAsync(Guid tenantId, string assetNumber);
    Task<CompanyAsset?> GetByAssetTagAsync(Guid tenantId, string assetTag);
    Task<CompanyAsset?> GetBySerialNumberAsync(Guid tenantId, string serialNumber);
    Task<IEnumerable<CompanyAsset>> GetByAssetTypeAsync(Guid assetTypeId);
    Task<IEnumerable<CompanyAsset>> GetByStatusAsync(Guid tenantId, CompanyAssetStatus status);
    Task<IEnumerable<CompanyAsset>> GetByLocationAsync(Guid locationId);
    Task<IEnumerable<CompanyAsset>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<CompanyAsset>> GetAvailableForAssignmentAsync(Guid tenantId);
    /// <summary>Assets whose next maintenance falls on or before the supplied date. Area 16 slice 9.</summary>
    Task<IEnumerable<CompanyAsset>> GetDueForMaintenanceAsync(Guid tenantId, DateOnly onOrBefore);

    /// <summary>Assets that require regular maintenance and have no next date at all. Area 16 slice 9.</summary>
    Task<IEnumerable<CompanyAsset>> GetUnscheduledMaintenanceAsync(Guid tenantId);
    Task<IEnumerable<CompanyAsset>> GetWarrantyExpiringAsync(Guid tenantId, int daysAhead = 30);
}

public interface IAssetAttributeValueRepository : IGenericRepository<AssetAttributeValue>
{
    /// <summary>
    /// One attribute value with its defining attribute loaded — D-o(b).
    /// </summary>
    /// <remarks>
    /// Same shape as <see cref="IAssetTypeAttributeRepository.GetWithTypeAsync"/>, and worse in one
    /// respect: without the navigation the DTO's <c>dataType</c> is read off a null attribute and
    /// falls back to the enum's default, so the by-id read did not merely lose a label — it reported
    /// the wrong type for the value it was returning.
    /// </remarks>
    Task<AssetAttributeValue?> GetWithAttributeAsync(Guid id);

    Task<IEnumerable<AssetAttributeValue>> GetByAssetIdAsync(Guid assetId);
    Task<AssetAttributeValue?> GetByAssetAndAttributeAsync(Guid assetId, Guid attributeId);
    Task DeleteByAssetIdAsync(Guid assetId);
}

public interface IAssetImageRepository : IGenericRepository<AssetImage>
{
    Task<IEnumerable<AssetImage>> GetByAssetIdAsync(Guid assetId);
}

#endregion

#region Asset Assignment Repositories

public interface IAssetAssignmentRepository : IGenericRepository<AssetAssignment>
{
    Task<IEnumerable<AssetAssignment>> GetByTenantAsync(Guid tenantId);
    Task<AssetAssignment?> GetWithDetailsAsync(Guid id);

    /// <summary>Everything a requisition produced, with the asset and holder loaded — D-e.</summary>
    Task<IEnumerable<AssetAssignment>> GetByRequisitionIdAsync(Guid requisitionId);
    Task<AssetAssignment?> GetByAssignmentNumberAsync(Guid tenantId, string assignmentNumber);
    Task<IEnumerable<AssetAssignment>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetAssignment>> GetByEmployeeIdAsync(Guid employeeId);
    Task<AssetAssignment?> GetActiveAssignmentForAssetAsync(Guid assetId);
    Task<IEnumerable<AssetAssignment>> GetActiveAssignmentsForEmployeeAsync(Guid employeeId);
    Task<IEnumerable<AssetAssignment>> GetOverdueAssignmentsAsync(Guid tenantId);
    Task<IEnumerable<AssetAssignment>> GetByStatusAsync(Guid tenantId, AssignmentStatus status);

    /// <summary>
    /// Rental arrangements whose effective window overlaps a period — AST-10, slice 8.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately NOT filtered to active assignments. A tenancy that ran for half the month and
    /// ended when the employee handed the keys back is owed for that half; filtering on the custody
    /// would silently drop the last period of every arrangement the module has ever carried.
    /// </remarks>
    Task<IEnumerable<AssetAssignment>> GetRentalArrangementsAsync(
        Guid tenantId, DateOnly periodStart, DateOnly periodEnd);
}

#endregion

#region Asset Maintenance Repositories

public interface IAssetMaintenanceRepository : IGenericRepository<AssetMaintenance>
{
    Task<IEnumerable<AssetMaintenance>> GetByTenantAsync(Guid tenantId);
    Task<AssetMaintenance?> GetWithDetailsAsync(Guid id);
    Task<AssetMaintenance?> GetByMaintenanceNumberAsync(Guid tenantId, string maintenanceNumber);
    Task<IEnumerable<AssetMaintenance>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetMaintenance>> GetByStatusAsync(Guid tenantId, MaintenanceStatus status);
    Task<IEnumerable<AssetMaintenance>> GetScheduledMaintenanceAsync(Guid tenantId, DateTime from, DateTime to);
    Task<AssetMaintenance?> GetLatestMaintenanceForAssetAsync(Guid assetId);
}

#endregion

#region Asset Attachment Repositories

public interface IAssetAttachmentRepository : IGenericRepository<AssetAttachment>
{
    Task<IEnumerable<AssetAttachment>> GetByAssetIdAsync(Guid assetId);
    Task DeleteByAssetIdAsync(Guid assetId);
}

#endregion

#region Asset Requisition Repositories

public interface IAssetRequisitionRepository : IGenericRepository<AssetRequisition>
{
    Task<IEnumerable<AssetRequisition>> GetByTenantAsync(Guid tenantId);
    Task<AssetRequisition?> GetWithDetailsAsync(Guid id);
    Task<AssetRequisition?> GetByRequisitionNumberAsync(Guid tenantId, string requisitionNumber);
    Task<IEnumerable<AssetRequisition>> GetByRequestedByIdAsync(Guid employeeId);

    /// <summary>Raised BY the employee or FOR them — the portal's list, AST-6b.</summary>
    Task<IEnumerable<AssetRequisition>> GetForEmployeeAsync(Guid employeeId);
    Task<IEnumerable<AssetRequisition>> GetByStatusAsync(Guid tenantId, AssetRequisitionStatus status);
    Task<IEnumerable<AssetRequisition>> GetPendingApprovalsAsync(Guid tenantId);
}

#endregion

#region Asset Transfer Repositories

public interface IAssetTransferRepository : IGenericRepository<AssetTransfer>
{
    Task<IEnumerable<AssetTransfer>> GetByTenantAsync(Guid tenantId);
    Task<AssetTransfer?> GetWithDetailsAsync(Guid id);
    Task<AssetTransfer?> GetByTransferNumberAsync(Guid tenantId, string transferNumber);
    Task<IEnumerable<AssetTransfer>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetTransfer>> GetByStatusAsync(Guid tenantId, HRAssetTransferStatus status);
    Task<IEnumerable<AssetTransfer>> GetPendingTransfersAsync(Guid tenantId);
}

#endregion




