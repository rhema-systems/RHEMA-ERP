
namespace ErpSystem.Core.DTOs.Maintenance;

/// <summary>
/// Mobile work orders response DTO
/// </summary>
public class MobileWorkOrdersResponseDto
{
    public List<MobileWorkOrderSummaryDto> WorkOrders { get; set; } = new();
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedTodayCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public DateTime LastSync { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>
/// Mobile work order summary DTO
/// </summary>
public class MobileWorkOrderSummaryDto
{
    public Guid Id { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetLocation { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? AssignedToId { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public double EstimatedHours { get; set; }
    public double EstimatedDuration { get; set; }
    public bool IsOverdue { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public int PhotoCount { get; set; }
    public bool RequiresCheckIn { get; set; }
}

/// <summary>
/// Mobile work order detail DTO
/// </summary>
public class MobileWorkOrderDetailDto
{
    public Guid Id { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string WorkOrderType { get; set; } = string.Empty;
    
    // Asset Information
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public string AssetLocation { get; set; } = string.Empty;
    public string AssetDescription { get; set; } = string.Empty;
    
    // Scheduling
    public DateTime? ScheduledDate { get; set; }
    public DateTime? DueDate { get; set; }
    public double EstimatedHours { get; set; }
    public double EstimatedDuration { get; set; }
    
    // Assignment
    public Guid? AssignedToId { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public Guid? AssignedTeamId { get; set; }
    public string AssignedTeamName { get; set; } = string.Empty;
    
    // Parts and Materials
    public List<MobileWorkOrderPartDto> RequiredParts { get; set; } = new();
    
    // Tasks/Checklist
    public List<MobileWorkOrderTaskDto> Tasks { get; set; } = new();
    
    // Attachments
    public List<MobileAttachmentDto> Attachments { get; set; } = new();
    
    // Work Logs
    public List<MobileWorkLogDto> WorkLogs { get; set; } = new();
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastModified { get; set; }
    
    // Additional Properties
    public string Location { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> SafetyRequirements { get; set; } = new();
    public List<MobileAssetInfoDto> RelatedAssets { get; set; } = new();
    public string CheckInStatus { get; set; } = string.Empty;
    public int PhotoCount { get; set; }
    public bool RequiresCheckIn { get; set; }
}

/// <summary>
/// Mobile work order status update DTO
/// </summary>
public class MobileWorkOrderStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double? HoursSpent { get; set; }
    public Guid UpdatedBy { get; set; }
    public string LocalId { get; set; } = string.Empty;
}

/// <summary>
/// Mobile work order update result DTO
/// </summary>
public class MobileWorkOrderUpdateResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid WorkOrderId { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public DateTime ServerTimestamp { get; set; }
    public string SyncId { get; set; } = string.Empty;
}

/// <summary>
/// Mobile work log create DTO
/// </summary>
public class MobileWorkLogCreateDto
{
    public Guid WorkOrderId { get; set; }
    public string Description { get; set; } = string.Empty;
    public double HoursSpent { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string WorkType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; }
    public string LocalId { get; set; } = string.Empty;
}

/// <summary>
/// Mobile work log result DTO
/// </summary>
public class MobileWorkLogResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid WorkLogId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid LogId { get; set; }
    public string SyncId { get; set; } = string.Empty;
}

/// <summary>
/// Mobile photo upload result DTO
/// </summary>
public class MobilePhotoUploadResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public List<Guid> PhotoIds { get; set; } = new();
    public int TotalUploaded { get; set; }
}

/// <summary>
/// Mobile asset detail DTO
/// </summary>
public class MobileAssetDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime? InstallationDate { get; set; }
    public DateTime? WarrantyExpiry { get; set; }
    public decimal PurchasePrice { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public List<MobileAssetSpecificationDto> Specifications { get; set; } = new();
    public List<MobileAttachmentDto> Photos { get; set; } = new();
    public List<MobileAttachmentDto> Documents { get; set; } = new();
    public MobileAssetMaintenanceInfoDto MaintenanceInfo { get; set; } = new();
}

/// <summary>
/// Mobile asset search result DTO
/// </summary>
public class MobileAssetSearchResultDto
{
    public List<MobileAssetSummaryDto> Assets { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalFound { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage { get; set; }
    public string SearchType { get; set; } = string.Empty;
}

/// <summary>
/// Mobile asset summary DTO
/// </summary>
public class MobileAssetSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public int ActiveWorkOrdersCount { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
}

/// <summary>
/// Mobile asset history DTO
/// </summary>
public class MobileAssetHistoryDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public List<MobileAssetHistoryItemDto> HistoryItems { get; set; } = new();
    public List<MobileMaintenanceEventDto> Events { get; set; } = new();
    public int TotalItems { get; set; }
}

/// <summary>
/// Mobile asset history item DTO
/// </summary>
public class MobileAssetHistoryItemDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public decimal? Cost { get; set; }
    public double? HoursSpent { get; set; }
    public string Status { get; set; } = string.Empty;
    public string WorkOrderNumber { get; set; } = string.Empty;
}

/// <summary>
/// Mobile schedule DTO
/// </summary>
public class MobileScheduleDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime ScheduleDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<MobileScheduleItemDto> ScheduleItems { get; set; } = new();
    public List<MobileScheduleEventDto> Events { get; set; } = new();
    public int TotalWorkOrders { get; set; }
    public double EstimatedTotalTime { get; set; }
}

/// <summary>
/// Mobile schedule item DTO
/// </summary>
public class MobileScheduleItemDto
{
    public Guid Id { get; set; }
    public string ItemType { get; set; } = string.Empty; // WorkOrder, Inspection, etc.
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public double EstimatedHours { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetLocation { get; set; } = string.Empty;
}

/// <summary>
/// Mobile maintenance event DTO
/// </summary>
public class MobileMaintenanceEventDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? Cost { get; set; }
    public double? Duration { get; set; }
}

/// <summary>
/// Mobile schedule event DTO
/// </summary>
public class MobileScheduleEventDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool RequiresCheckIn { get; set; }
    public double? EstimatedDuration { get; set; }
}

/// <summary>
/// Mobile check-in DTO
/// </summary>
public class MobileCheckInDto
{
    public Guid WorkOrderId { get; set; }
    public DateTime CheckInTime { get; set; }
    public DateTime Timestamp { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Mobile check-in result DTO
/// </summary>
public class MobileCheckInResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid CheckInId { get; set; }
    public DateTime CheckInTime { get; set; }
    public bool LocationVerified { get; set; }
}

/// <summary>
/// Mobile check-out DTO
/// </summary>
public class MobileCheckOutDto
{
    public Guid WorkOrderId { get; set; }
    public DateTime CheckOutTime { get; set; }
    public DateTime Timestamp { get; set; }
    public double HoursSpent { get; set; }
    public string WorkCompleted { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string CompletionNotes { get; set; } = string.Empty;
    public bool RequiresFollowUp { get; set; }
    public string FollowUpNotes { get; set; } = string.Empty;
}

/// <summary>
/// Mobile check-out result DTO
/// </summary>
public class MobileCheckOutResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid CheckOutId { get; set; }
    public DateTime CheckOutTime { get; set; }
    public double TotalHoursSpent { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

// Supporting DTOs

/// <summary>
/// Mobile work order part DTO
/// </summary>
public class MobileWorkOrderPartDto
{
    public Guid Id { get; set; }
    public Guid PartId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int QuantityRequired { get; set; }
    public int QuantityUsed { get; set; }
    public decimal UnitCost { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Mobile work order task DTO
/// </summary>
public class MobileWorkOrderTaskDto
{
    public Guid Id { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CompletedByName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>
/// Mobile attachment DTO
/// </summary>
public class MobileAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

/// <summary>
/// Mobile work log DTO
/// </summary>
public class MobileWorkLogDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public double HoursSpent { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string WorkType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
}

/// <summary>
/// Mobile asset specification DTO
/// </summary>
public class MobileAssetSpecificationDto
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Mobile asset maintenance info DTO
/// </summary>
public class MobileAssetMaintenanceInfoDto
{
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public int MaintenanceIntervalDays { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public int ActiveWorkOrdersCount { get; set; }
    public int CompletedWorkOrdersCount { get; set; }
    public decimal TotalMaintenanceCost { get; set; }
    public double AverageDowntime { get; set; }
    public double ReliabilityScore { get; set; }
}

/// <summary>
/// File upload DTO for mobile operations
/// </summary>
public class FileUploadDto
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
}

/// <summary>
/// Mobile sync request DTO
/// </summary>
public class MobileSyncRequestDto
{
    public Guid UserId { get; set; }
    public DateTime LastSyncTime { get; set; }
    public DateTime? LastSyncTimestamp { get; set; }
    public List<MobilePendingUploadDto> PendingUploads { get; set; } = new();
    public List<MobileConflictResolutionDto> ConflictResolutions { get; set; } = new();
    public string DeviceId { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
}

/// <summary>
/// Mobile sync response DTO
/// </summary>
public class MobileSyncResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ServerTime { get; set; }
    public DateTime ServerTimestamp { get; set; }
    public string SyncId { get; set; } = string.Empty;
    public List<MobileUpdateDto> Updates { get; set; } = new();
    public List<MobileConflictDto> Conflicts { get; set; } = new();
    public int ProcessedUploads { get; set; }
    public int FailedUploads { get; set; }
    public int UploadedCount { get; set; }
    public int UpdateCount { get; set; }
    public int ConflictsResolvedCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Mobile sync status DTO
/// </summary>
public class MobileSyncStatusDto
{
    public Guid UserId { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public int PendingUploads { get; set; }
    public int PendingUploadsCount { get; set; }
    public int PendingDownloads { get; set; }
    public int ConflictsCount { get; set; }
    public bool IsSyncing { get; set; }
    public bool SyncRequired { get; set; }
    public bool IsOnline { get; set; }
    public string SyncStatus { get; set; } = string.Empty;
    public List<string> SyncErrors { get; set; } = new();
}

/// <summary>
/// Mobile offline package DTO
/// </summary>
public class MobileOfflinePackageDto
{
    public string PackageId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ValidUntil { get; set; }
    public List<MobileWorkOrderSummaryDto> WorkOrders { get; set; } = new();
    public List<MobileAssetSummaryDto> Assets { get; set; } = new();
    public MobileScheduleDto Schedule { get; set; } = new();
    public MobileInventoryDto Inventory { get; set; } = new();
    public List<MobileSafetyProtocolDto> SafetyProtocols { get; set; } = new();
    public string DataVersion { get; set; } = string.Empty;
}

/// <summary>
/// Mobile inventory DTO
/// </summary>
public class MobileInventoryDto
{
    public List<MobileInventoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Mobile inventory item DTO
/// </summary>
public class MobileInventoryItemDto
{
    public Guid Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int ReorderPoint { get; set; }
    public decimal UnitCost { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
}

/// <summary>
/// Mobile parts request DTO
/// </summary>
public class MobilePartsRequestDto
{
    public Guid WorkOrderId { get; set; }
    public List<MobilePartRequestItemDto> RequestedParts { get; set; } = new();
    public string RequestNotes { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public DateTime RequestedDate { get; set; }
}

/// <summary>
/// Mobile part request item DTO
/// </summary>
public class MobilePartRequestItemDto
{
    public Guid PartId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public int QuantityRequested { get; set; }
    public string Justification { get; set; } = string.Empty;
}

/// <summary>
/// Mobile parts request result DTO
/// </summary>
public class MobilePartsRequestResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Mobile safety protocols DTO
/// </summary>
public class MobileSafetyProtocolsDto
{
    public Guid? AssetId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public List<MobileSafetyProtocolDto> Protocols { get; set; } = new();
    public List<string> RequiredPPE { get; set; } = new();
    public List<string> HazardWarnings { get; set; } = new();
}

/// <summary>
/// Mobile safety protocol DTO
/// </summary>
public class MobileSafetyProtocolDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public bool IsRequired { get; set; }
}

/// <summary>
/// Mobile safety checklist DTO
/// </summary>
public class MobileSafetyChecklistDto
{
    public Guid WorkOrderId { get; set; }
    public List<MobileSafetyCheckItemDto> CheckItems { get; set; } = new();
    public string TechnicianSignature { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public string AdditionalNotes { get; set; } = string.Empty;
}

/// <summary>
/// Mobile safety check item DTO
/// </summary>
public class MobileSafetyCheckItemDto
{
    public Guid ProtocolId { get; set; }
    public bool IsCompleted { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Mobile safety checklist result DTO
/// </summary>
public class MobileSafetyChecklistResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid ChecklistId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public bool ComplianceAchieved { get; set; }
}

// Supporting sync DTOs

/// <summary>
/// Mobile pending upload DTO
/// </summary>
public class MobilePendingUploadDto
{
    public string LocalId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public int RetryCount { get; set; }
}

/// <summary>
/// Mobile conflict resolution DTO
/// </summary>
public class MobileConflictResolutionDto
{
    public string ConflictId { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public Dictionary<string, object> ResolvedData { get; set; } = new();
}

/// <summary>
/// Mobile update DTO
/// </summary>
public class MobileUpdateDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Mobile conflict DTO
/// </summary>
public class MobileConflictDto
{
    public string ConflictId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Dictionary<string, object> LocalData { get; set; } = new();
    public Dictionary<string, object> ServerData { get; set; } = new();
    public string ConflictType { get; set; } = string.Empty;
}

// Additional supporting DTOs that were referenced in MobileMaintenanceService

/// <summary>
/// Mobile asset info DTO
/// </summary>
public class MobileAssetInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Mobile parts DTO
/// </summary>
public class MobilePartsDto
{
    public Guid Id { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int QuantityRequired { get; set; }
    public int QuantityUsed { get; set; }
    public decimal UnitCost { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Mobile check-in status DTO
/// </summary>
public class MobileCheckInStatusDto
{
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? CheckedInBy { get; set; }
    public string CheckedInByName { get; set; } = string.Empty;
}


/// <summary>
/// Mobile safety protocol summary DTO
/// </summary>
public class MobileSafetyProtocolSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}

/// <summary>
/// Create work log DTO for MobileMaintenanceService
/// </summary>
public class CreateWorkLogDto
{
    public Guid WorkOrderId { get; set; }
    public string Description { get; set; } = string.Empty;
    public TimeSpan? Duration { get; set; }
    public DateTime LoggedAt { get; set; }
    public Guid TechnicianId { get; set; }
}

/// <summary>
/// Maintenance notification recipient DTO
/// </summary>
public class MaintenanceNotificationRecipientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
