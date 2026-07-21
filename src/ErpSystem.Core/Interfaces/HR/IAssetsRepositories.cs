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
    Task<IEnumerable<CompanyAsset>> GetDueForMaintenanceAsync(Guid tenantId, int daysAhead = 30);
    Task<IEnumerable<CompanyAsset>> GetWarrantyExpiringAsync(Guid tenantId, int daysAhead = 30);
}

public interface IAssetAttributeValueRepository : IGenericRepository<AssetAttributeValue>
{
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
    Task<AssetAssignment?> GetByAssignmentNumberAsync(Guid tenantId, string assignmentNumber);
    Task<IEnumerable<AssetAssignment>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetAssignment>> GetByEmployeeIdAsync(Guid employeeId);
    Task<AssetAssignment?> GetActiveAssignmentForAssetAsync(Guid assetId);
    Task<IEnumerable<AssetAssignment>> GetActiveAssignmentsForEmployeeAsync(Guid employeeId);
    Task<IEnumerable<AssetAssignment>> GetOverdueAssignmentsAsync(Guid tenantId);
    Task<IEnumerable<AssetAssignment>> GetByStatusAsync(Guid tenantId, AssignmentStatus status);
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




