using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Interface for mobile maintenance operations with offline synchronization support
/// </summary>
public interface IMobileMaintenanceService
{
    #region Work Order Operations

    /// <summary>
    /// Get work orders assigned to a technician with mobile optimization
    /// </summary>
    Task<MobileWorkOrdersResponseDto> GetTechnicianWorkOrdersAsync(
        Guid technicianId, int page, int pageSize, string? status, DateTime? lastSync);

    /// <summary>
    /// Get work order details optimized for mobile
    /// </summary>
    Task<MobileWorkOrderDetailDto?> GetMobileWorkOrderDetailAsync(Guid workOrderId);

    /// <summary>
    /// Update work order status with offline sync support
    /// </summary>
    Task<MobileWorkOrderUpdateResultDto> UpdateWorkOrderStatusMobileAsync(
        Guid workOrderId, MobileWorkOrderStatusUpdateDto updateDto, Guid userId);

    /// <summary>
    /// Add work log entry with offline sync support
    /// </summary>
    Task<MobileWorkLogResultDto> AddWorkLogMobileAsync(
        Guid workOrderId, MobileWorkLogCreateDto logDto, Guid userId);

    // /// <summary>
    // /// Upload work order photos with compression and offline queuing
    // /// </summary>
    // Task<MobilePhotoUploadResultDto> UploadWorkOrderPhotosAsync(
    //     Guid workOrderId, List<IFormFile> photos, string? description, Guid userId);

    #endregion

    #region Asset Operations

    /// <summary>
    /// Get asset information optimized for mobile with QR code scanning support
    /// </summary>
    Task<MobileAssetDetailDto?> GetMobileAssetDetailAsync(Guid assetId);

    /// <summary>
    /// Search assets by QR code, asset tag, or general search
    /// </summary>
    Task<MobileAssetSearchResultDto> SearchAssetsAsync(string? qrCode, string? assetTag, string? search);

    /// <summary>
    /// Get asset maintenance history for mobile view
    /// </summary>
    Task<MobileAssetHistoryDto> GetAssetMaintenanceHistoryAsync(Guid assetId, int limit);

    #endregion

    #region Schedule Operations

    /// <summary>
    /// Get technician schedule for mobile calendar view
    /// </summary>
    Task<MobileScheduleDto> GetTechnicianScheduleAsync(Guid technicianId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Check in to work order with GPS location
    /// </summary>
    Task<MobileCheckInResultDto> CheckInToWorkOrderAsync(MobileCheckInDto checkInDto, Guid userId);

    /// <summary>
    /// Check out from work order with time tracking
    /// </summary>
    Task<MobileCheckOutResultDto> CheckOutFromWorkOrderAsync(MobileCheckOutDto checkOutDto, Guid userId);

    #endregion

    #region Synchronization

    /// <summary>
    /// Sync mobile data with server - upload pending changes and download updates
    /// </summary>
    Task<MobileSyncResponseDto> SyncMobileDataAsync(MobileSyncRequestDto syncRequest, Guid userId);

    /// <summary>
    /// Get sync status and pending items count
    /// </summary>
    Task<MobileSyncStatusDto> GetSyncStatusAsync(Guid userId);

    /// <summary>
    /// Create offline data package for specified date range
    /// </summary>
    Task<MobileOfflinePackageDto> CreateOfflinePackageAsync(Guid userId, DateTime startDate, DateTime endDate);

    #endregion

    #region Inventory Integration

    /// <summary>
    /// Get maintenance inventory items with barcode scanning support
    /// </summary>
    Task<MobileInventoryDto> GetMaintenanceInventoryAsync(string? search, string? barcode, int page, int pageSize);

    /// <summary>
    /// Request parts for work order with mobile form
    /// </summary>
    Task<MobilePartsRequestResultDto> RequestPartsAsync(Guid workOrderId, MobilePartsRequestDto requestDto, Guid userId);

    #endregion

    #region Safety & Compliance

    /// <summary>
    /// Get safety protocols for asset or work order
    /// </summary>
    Task<MobileSafetyProtocolsDto> GetSafetyProtocolsAsync(Guid? assetId, Guid? workOrderId);

    /// <summary>
    /// Submit safety checklist with mobile form
    /// </summary>
    Task<MobileSafetyChecklistResultDto> SubmitSafetyChecklistAsync(MobileSafetyChecklistDto checklistDto, Guid userId);

    #endregion
}
