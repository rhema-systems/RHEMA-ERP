using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/mobile/maintenance")]
[Authorize]
public class MaintenanceMobileController : ControllerBase
{
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceAssetService _assetService;
    private readonly ITechnicianService _technicianService;
    private readonly IMaintenanceScheduleService _scheduleService;
    private readonly IMobileMaintenanceService _mobileService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MaintenanceMobileController> _logger;

    public MaintenanceMobileController(
        IWorkOrderService workOrderService,
        IMaintenanceAssetService assetService,
        ITechnicianService technicianService,
        IMaintenanceScheduleService scheduleService,
        IMobileMaintenanceService mobileService,
        ICurrentUserService currentUserService,
        ILogger<MaintenanceMobileController> logger)
    {
        _workOrderService = workOrderService;
        _assetService = assetService;
        _technicianService = technicianService;
        _scheduleService = scheduleService;
        _mobileService = mobileService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private Guid CurrentUserId => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : Guid.Empty;

    #region Work Orders - Mobile Optimized

    /// <summary>
    /// Get work orders assigned to current technician with mobile optimization
    /// </summary>
    [HttpGet("work-orders/my-assignments")]
    public async Task<ActionResult<MobileWorkOrdersResponseDto>> GetMyWorkOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? lastSync = null)
    {
        try
        {
            var result = await _mobileService.GetTechnicianWorkOrdersAsync(
                CurrentUserId, page, pageSize, status, lastSync);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mobile work orders for user {UserId}", CurrentUserId);
            return StatusCode(500, "Error retrieving work orders");
        }
    }

    /// <summary>
    /// Get work order details with mobile-optimized data
    /// </summary>
    [HttpGet("work-orders/{id}")]
    public async Task<ActionResult<MobileWorkOrderDetailDto>> GetWorkOrderDetail(Guid id)
    {
        try
        {
            var workOrder = await _mobileService.GetMobileWorkOrderDetailAsync(id);
            if (workOrder == null)
                return NotFound();

            return Ok(workOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work order detail {WorkOrderId}", id);
            return StatusCode(500, "Error retrieving work order details");
        }
    }

    /// <summary>
    /// Update work order status with offline sync support
    /// </summary>
    [HttpPut("work-orders/{id}/status")]
    public async Task<ActionResult<MobileWorkOrderUpdateResultDto>> UpdateWorkOrderStatus(
        Guid id, 
        [FromBody] MobileWorkOrderStatusUpdateDto updateDto)
    {
        try
        {
            var coreDto = new ErpSystem.Core.DTOs.Maintenance.MobileWorkOrderStatusUpdateDto
            {
                Status = updateDto.Status,
                Notes = updateDto.Notes,
                Timestamp = updateDto.Timestamp,
                LocalId = updateDto.LocalId
            };
            var result = await _mobileService.UpdateWorkOrderStatusMobileAsync(id, coreDto, CurrentUserId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work order status {WorkOrderId}", id);
            return StatusCode(500, "Error updating work order status");
        }
    }

    /// <summary>
    /// Add work log entry with offline sync support
    /// </summary>
    [HttpPost("work-orders/{id}/logs")]
    public async Task<ActionResult<MobileWorkLogResultDto>> AddWorkLog(
        Guid id, 
        [FromBody] MobileWorkLogCreateDto logDto)
    {
        try
        {
            var coreDto = new ErpSystem.Core.DTOs.Maintenance.MobileWorkLogCreateDto
            {
                Description = logDto.Description,
                Timestamp = logDto.Timestamp,
                Duration = logDto.Duration ?? TimeSpan.Zero,
                LocalId = logDto.LocalId
            };
            var result = await _mobileService.AddWorkLogMobileAsync(id, coreDto, CurrentUserId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding work log for work order {WorkOrderId}", id);
            return StatusCode(500, "Error adding work log");
        }
    }

    /// <summary>
    /// Upload work order photos with compression and offline queuing
    /// </summary>
    [HttpPost("work-orders/{id}/photos")]
    public async Task<ActionResult<MobilePhotoUploadResultDto>> UploadWorkOrderPhotos(
        Guid id,
        [FromForm] List<IFormFile> photos,
        [FromForm] string? description = null)
    {
        try
        {
            if (photos == null || !photos.Any())
                return BadRequest("No photos provided");

            // Photo upload functionality not yet implemented
            return BadRequest("Photo upload functionality is currently under development");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading photos for work order {WorkOrderId}", id);
            return StatusCode(500, "Error uploading photos");
        }
    }

    #endregion

    #region Asset Information - Mobile Optimized

    /// <summary>
    /// Get asset information optimized for mobile with QR code scanning support
    /// </summary>
    [HttpGet("assets/{id}")]
    public async Task<ActionResult<MobileAssetDetailDto>> GetAssetDetail(Guid id)
    {
        try
        {
            var asset = await _mobileService.GetMobileAssetDetailAsync(id);
            if (asset == null)
                return NotFound();

            return Ok(asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset detail {AssetId}", id);
            return StatusCode(500, "Error retrieving asset details");
        }
    }

    /// <summary>
    /// Search assets by QR code or asset tag
    /// </summary>
    [HttpGet("assets/search")]
    public async Task<ActionResult<MobileAssetSearchResultDto>> SearchAssets(
        [FromQuery] string? qrCode = null,
        [FromQuery] string? assetTag = null,
        [FromQuery] string? search = null)
    {
        try
        {
            var result = await _mobileService.SearchAssetsAsync(qrCode, assetTag, search);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching assets");
            return StatusCode(500, "Error searching assets");
        }
    }

    /// <summary>
    /// Get asset maintenance history for mobile view
    /// </summary>
    [HttpGet("assets/{id}/history")]
    public async Task<ActionResult<MobileAssetHistoryDto>> GetAssetHistory(Guid id, [FromQuery] int limit = 10)
    {
        try
        {
            var history = await _mobileService.GetAssetMaintenanceHistoryAsync(id, limit);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset history {AssetId}", id);
            return StatusCode(500, "Error retrieving asset history");
        }
    }

    #endregion

    #region Schedule & Calendar - Mobile Optimized

    /// <summary>
    /// Get technician schedule for mobile calendar view
    /// </summary>
    [HttpGet("schedule/my-schedule")]
    public async Task<ActionResult<MobileScheduleDto>> GetMySchedule(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today;
            var end = endDate ?? DateTime.Today.AddDays(7);

            var schedule = await _mobileService.GetTechnicianScheduleAsync(CurrentUserId, start, end);
            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technician schedule for user {UserId}", CurrentUserId);
            return StatusCode(500, "Error retrieving schedule");
        }
    }

    /// <summary>
    /// Check in/out of work orders with GPS location
    /// </summary>
    [HttpPost("schedule/checkin")]
    public async Task<ActionResult<MobileCheckInResultDto>> CheckIn([FromBody] MobileCheckInDto checkInDto)
    {
        try
        {
            var coreDto = new ErpSystem.Core.DTOs.Maintenance.MobileCheckInDto
            {
                WorkOrderId = checkInDto.WorkOrderId,
                Latitude = checkInDto.Latitude,
                Longitude = checkInDto.Longitude,
                Notes = checkInDto.Notes,
                Timestamp = checkInDto.Timestamp
            };
            var result = await _mobileService.CheckInToWorkOrderAsync(coreDto, CurrentUserId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking in to work order");
            return StatusCode(500, "Error processing check-in");
        }
    }

    /// <summary>
    /// Check out of work orders with time tracking
    /// </summary>
    [HttpPost("schedule/checkout")]
    public async Task<ActionResult<MobileCheckOutResultDto>> CheckOut([FromBody] MobileCheckOutDto checkOutDto)
    {
        try
        {
            var coreDto = new ErpSystem.Core.DTOs.Maintenance.MobileCheckOutDto
            {
                WorkOrderId = checkOutDto.WorkOrderId,
                CompletionNotes = checkOutDto.CompletionNotes,
                Timestamp = checkOutDto.Timestamp,
                // ActualDuration property doesn't exist in Core DTO
                // TODO: Add ActualDuration to the Core DTO or calculate it differently
            };
            var result = await _mobileService.CheckOutFromWorkOrderAsync(coreDto, CurrentUserId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking out from work order");
            return StatusCode(500, "Error processing check-out");
        }
    }

    #endregion

    #region Offline Synchronization

    /// <summary>
    /// Sync mobile data with server - upload pending changes and download updates
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<MobileSyncResponseDto>> SyncData([FromBody] MobileSyncRequestDto syncRequest)
    {
        try
        {
            var result = await _mobileService.SyncMobileDataAsync(syncRequest, CurrentUserId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing mobile data for user {UserId}", CurrentUserId);
            return StatusCode(500, "Error syncing data");
        }
    }

    /// <summary>
    /// Get sync status and pending items count
    /// </summary>
    [HttpGet("sync/status")]
    public async Task<ActionResult<MobileSyncStatusDto>> GetSyncStatus()
    {
        try
        {
            var status = await _mobileService.GetSyncStatusAsync(CurrentUserId);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync status for user {UserId}", CurrentUserId);
            return StatusCode(500, "Error retrieving sync status");
        }
    }

    /// <summary>
    /// Download offline data package for specified date range
    /// </summary>
    [HttpGet("offline/package")]
    public async Task<ActionResult<MobileOfflinePackageDto>> GetOfflinePackage(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.Today;
            var end = endDate ?? DateTime.Today.AddDays(7);

            var package = await _mobileService.CreateOfflinePackageAsync(CurrentUserId, start, end);
            return Ok(package);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating offline package for user {UserId}", CurrentUserId);
            return StatusCode(500, "Error creating offline package");
        }
    }

    #endregion

    #region Inventory Integration - Mobile

    /// <summary>
    /// Get maintenance inventory items with barcode scanning support
    /// </summary>
    [HttpGet("inventory")]
    public async Task<ActionResult<MobileInventoryDto>> GetMaintenanceInventory(
        [FromQuery] string? search = null,
        [FromQuery] string? barcode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var inventory = await _mobileService.GetMaintenanceInventoryAsync(search, barcode, page, pageSize);
            return Ok(inventory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting maintenance inventory");
            return StatusCode(500, "Error retrieving inventory");
        }
    }

    /// <summary>
    /// Request parts for work order with mobile form
    /// </summary>
    [HttpPost("work-orders/{workOrderId}/parts-request")]
    public async Task<ActionResult<MobilePartsRequestResultDto>> RequestParts(
        Guid workOrderId,
        [FromBody] MobilePartsRequestDto requestDto)
    {
        try
        {
            var result = await _mobileService.RequestPartsAsync(workOrderId, requestDto, CurrentUserId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting parts for work order {WorkOrderId}", workOrderId);
            return StatusCode(500, "Error requesting parts");
        }
    }

    #endregion

    #region Safety & Compliance - Mobile

    /// <summary>
    /// Get safety protocols for asset or work order
    /// </summary>
    [HttpGet("safety/protocols")]
    public async Task<ActionResult<MobileSafetyProtocolsDto>> GetSafetyProtocols(
        [FromQuery] Guid? assetId = null,
        [FromQuery] Guid? workOrderId = null)
    {
        try
        {
            var protocols = await _mobileService.GetSafetyProtocolsAsync(assetId, workOrderId);
            return Ok(protocols);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting safety protocols");
            return StatusCode(500, "Error retrieving safety protocols");
        }
    }

    /// <summary>
    /// Submit safety checklist with mobile form
    /// </summary>
    [HttpPost("safety/checklist")]
    public async Task<ActionResult<MobileSafetyChecklistResultDto>> SubmitSafetyChecklist(
        [FromBody] MobileSafetyChecklistDto checklistDto)
    {
        try
        {
            var result = await _mobileService.SubmitSafetyChecklistAsync(checklistDto, CurrentUserId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting safety checklist");
            return StatusCode(500, "Error submitting safety checklist");
        }
    }

    #endregion
}

#region Mobile DTOs

public class MobileWorkOrdersResponseDto
{
    public List<MobileWorkOrderSummaryDto> WorkOrders { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public DateTime LastSync { get; set; }
    public bool HasMore { get; set; }
}

public class MobileWorkOrderSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? ScheduledDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public TimeSpan? EstimatedDuration { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime LastModified { get; set; }
    public int PhotoCount { get; set; }
    public bool RequiresCheckIn { get; set; }
}

public class MobileWorkOrderDetailDto : MobileWorkOrderSummaryDto
{
    public string Instructions { get; set; } = string.Empty;
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> SafetyRequirements { get; set; } = new();
    public List<MobileWorkLogDto> WorkLogs { get; set; } = new();
    public List<MobileAssetInfoDto> RelatedAssets { get; set; } = new();
    public List<MobilePartsDto> RequiredParts { get; set; } = new();
    public MobileCheckInStatusDto? CheckInStatus { get; set; }
}

public class MobileWorkOrderStatusUpdateDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? LocalId { get; set; } // For offline sync
}

public class MobileWorkOrderUpdateResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ServerTimestamp { get; set; }
    public string? SyncId { get; set; }
}

public class MobileWorkLogCreateDto
{
    [Required]
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan? Duration { get; set; }
    public string? LocalId { get; set; } // For offline sync
}

public class MobileWorkLogResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? LogId { get; set; }
    public string? SyncId { get; set; }
}

public class MobilePhotoUploadResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> PhotoIds { get; set; } = new();
    public int TotalUploaded { get; set; }
}

public class MobileAssetDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public Dictionary<string, object> Specifications { get; set; } = new();
    public List<string> Documents { get; set; } = new();
    public List<string> Photos { get; set; } = new();
}

public class MobileAssetSearchResultDto
{
    public List<MobileAssetSummaryDto> Assets { get; set; } = new();
    public int TotalFound { get; set; }
    public string SearchType { get; set; } = string.Empty; // QrCode, AssetTag, Search
}

public class MobileAssetSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class MobileAssetHistoryDto
{
    public Guid AssetId { get; set; }
    public List<MobileMaintenanceEventDto> Events { get; set; } = new();
}

public class MobileMaintenanceEventDto
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class MobileScheduleDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<MobileScheduleEventDto> Events { get; set; } = new();
    public int TotalWorkOrders { get; set; }
    public TimeSpan EstimatedTotalTime { get; set; }
}

public class MobileScheduleEventDto
{
    public Guid WorkOrderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool RequiresCheckIn { get; set; }
}

public class MobileCheckInDto
{
    [Required]
    public Guid WorkOrderId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class MobileCheckInResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CheckInTime { get; set; }
    public string? LocationVerified { get; set; }
}

public class MobileCheckOutDto
{
    [Required]
    public Guid WorkOrderId { get; set; }
    public string? CompletionNotes { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan? ActualDuration { get; set; }
}

public class MobileCheckOutResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CheckOutTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

// Additional DTOs for sync, inventory, safety, etc. would continue here...

#endregion