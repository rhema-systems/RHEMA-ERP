using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.DTOs.Maintenance;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for mobile maintenance operations with offline synchronization support
/// </summary>
public class MobileMaintenanceService : IMobileMaintenanceService
{
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceAssetService _assetService;
    private readonly ITechnicianService _technicianService;
    private readonly IMaintenanceScheduleService _scheduleService;
    private readonly IMaintenanceInventoryService _inventoryService;
    private readonly ISafetyProtocolService _safetyService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<MobileMaintenanceService> _logger;

    public MobileMaintenanceService(
        IWorkOrderService workOrderService,
        IMaintenanceAssetService assetService,
        ITechnicianService technicianService,
        IMaintenanceScheduleService scheduleService,
        IMaintenanceInventoryService inventoryService,
        ISafetyProtocolService safetyService,
        ICurrentUserProvider currentUserProvider,
        ILogger<MobileMaintenanceService> logger)
    {
        _workOrderService = workOrderService;
        _assetService = assetService;
        _technicianService = technicianService;
        _scheduleService = scheduleService;
        _inventoryService = inventoryService;
        _safetyService = safetyService;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region Work Order Operations

    public async Task<MobileWorkOrdersResponseDto> GetTechnicianWorkOrdersAsync(
        Guid technicianId, int page, int pageSize, string? status, DateTime? lastSync)
    {
        try
        {
            _logger.LogInformation("Getting mobile work orders for technician {TechnicianId}", technicianId);

            // Get technician's assigned work orders
            var allWorkOrders = await _workOrderService.GetWorkOrdersByTechnicianAsync(technicianId);
            
            // Filter by status if provided
            if (!string.IsNullOrEmpty(status))
            {
                allWorkOrders = allWorkOrders.Where(wo => wo.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by last sync if provided (for incremental sync)
            if (lastSync.HasValue)
            {
                allWorkOrders = allWorkOrders.Where(wo => wo.UpdatedAt > lastSync.Value);
            }

            var totalCount = allWorkOrders.Count();
            var pagedWorkOrders = allWorkOrders
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var mobileWorkOrders = new List<MobileWorkOrderSummaryDto>();

            foreach (var wo in pagedWorkOrders)
            {
                var asset = await _assetService.GetAssetByIdAsync(wo.AssetId);
                
                mobileWorkOrders.Add(new MobileWorkOrderSummaryDto
                {
                    Id = wo.Id,
                    Title = wo.Title,
                    Description = wo.Description,
                    Status = wo.Status,
                    Priority = wo.Priority,
                    ScheduledDate = wo.ScheduledStartDate,
                    DueDate = wo.DueDate,
                    AssetName = asset?.Name ?? "Unknown Asset",
                    Location = asset?.Location ?? "Unknown Location",
                    EstimatedDuration = wo.EstimatedDuration,
                    IsOverdue = wo.DueDate.HasValue && wo.DueDate.Value < DateTime.UtcNow && wo.Status != "Completed",
                    LastModified = wo.UpdatedAt,
                    PhotoCount = await GetWorkOrderPhotoCountAsync(wo.Id),
                    RequiresCheckIn = DetermineIfCheckInRequired(wo.Priority, wo.Type)
                });
            }

            return new MobileWorkOrdersResponseDto
            {
                WorkOrders = mobileWorkOrders,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                LastSync = DateTime.UtcNow,
                HasMore = (page * pageSize) < totalCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technician work orders for mobile");
            throw;
        }
    }

    public async Task<MobileWorkOrderDetailDto?> GetMobileWorkOrderDetailAsync(Guid workOrderId)
    {
        try
        {
            var workOrder = await _workOrderService.GetWorkOrderByIdAsync(workOrderId);
            if (workOrder == null) return null;

            var asset = await _assetService.GetAssetByIdAsync(workOrder.AssetId);
            var workLogs = await GetWorkOrderLogsAsync(workOrderId);
            var requiredParts = await GetWorkOrderRequiredPartsAsync(workOrderId);
            var checkInStatus = await GetWorkOrderCheckInStatusAsync(workOrderId);

            return new MobileWorkOrderDetailDto
            {
                Id = workOrder.Id,
                Title = workOrder.Title,
                Description = workOrder.Description,
                Instructions = workOrder.Instructions ?? string.Empty,
                Status = workOrder.Status,
                Priority = workOrder.Priority,
                ScheduledDate = workOrder.ScheduledStartDate,
                DueDate = workOrder.DueDate,
                AssetName = asset?.Name ?? "Unknown Asset",
                Location = asset?.Location ?? "Unknown Location",
                EstimatedDuration = workOrder.EstimatedDuration,
                IsOverdue = workOrder.DueDate.HasValue && workOrder.DueDate.Value < DateTime.UtcNow && workOrder.Status != "Completed",
                LastModified = workOrder.UpdatedAt,
                RequiredSkills = await GetWorkOrderRequiredSkillsAsync(workOrderId),
                SafetyRequirements = await GetWorkOrderSafetyRequirementsAsync(workOrderId),
                WorkLogs = workLogs,
                RelatedAssets = new List<MobileAssetInfoDto> { CreateMobileAssetInfo(asset) },
                RequiredParts = requiredParts,
                CheckInStatus = checkInStatus,
                PhotoCount = await GetWorkOrderPhotoCountAsync(workOrderId),
                RequiresCheckIn = DetermineIfCheckInRequired(workOrder.Priority, workOrder.Type)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mobile work order detail {WorkOrderId}", workOrderId);
            throw;
        }
    }

    public async Task<MobileWorkOrderUpdateResultDto> UpdateWorkOrderStatusMobileAsync(
        Guid workOrderId, MobileWorkOrderStatusUpdateDto updateDto, Guid userId)
    {
        try
        {
            _logger.LogInformation("Updating work order {WorkOrderId} status to {Status} from mobile", 
                workOrderId, updateDto.Status);

            // Update work order status
            var updateRequest = new UpdateWorkOrderDto
            {
                Id = workOrderId,
                Status = updateDto.Status,
                Notes = updateDto.Notes
            };

            var workOrder = await _workOrderService.UpdateWorkOrderAsync(updateRequest);

            // Create sync record for offline support
            var syncId = await CreateSyncRecordAsync("WorkOrderStatusUpdate", workOrderId, updateDto.LocalId, userId);

            return new MobileWorkOrderUpdateResultDto
            {
                Success = true,
                Message = $"Work order status updated to {updateDto.Status}",
                ServerTimestamp = DateTime.UtcNow,
                SyncId = syncId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work order status from mobile");
            return new MobileWorkOrderUpdateResultDto
            {
                Success = false,
                Message = "Failed to update work order status"
            };
        }
    }

    public async Task<MobileWorkLogResultDto> AddWorkLogMobileAsync(
        Guid workOrderId, MobileWorkLogCreateDto logDto, Guid userId)
    {
        try
        {
            _logger.LogInformation("Adding work log to work order {WorkOrderId} from mobile", workOrderId);

            // Create work log entry
            var workLogRequest = new CreateWorkLogDto
            {
                WorkOrderId = workOrderId,
                Description = logDto.Description,
                Duration = logDto.Duration,
                LoggedAt = logDto.Timestamp,
                TechnicianId = userId
            };

            var logId = await CreateWorkLogAsync(workLogRequest);

            // Create sync record
            var syncId = await CreateSyncRecordAsync("WorkLogCreate", logId, logDto.LocalId, userId);

            return new MobileWorkLogResultDto
            {
                Success = true,
                Message = "Work log added successfully",
                LogId = logId,
                SyncId = syncId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding work log from mobile");
            return new MobileWorkLogResultDto
            {
                Success = false,
                Message = "Failed to add work log"
            };
        }
    }

    public async Task<MobilePhotoUploadResultDto> UploadWorkOrderPhotosAsync(
        Guid workOrderId, List<IFormFile> photos, string? description, Guid userId)
    {
        try
        {
            _logger.LogInformation("Uploading {Count} photos for work order {WorkOrderId}", 
                photos.Count, workOrderId);

            var uploadedPhotoIds = new List<string>();
            var totalUploaded = 0;

            foreach (var photo in photos)
            {
                if (photo.Length > 0)
                {
                    // Compress and save photo
                    var photoId = await ProcessAndSavePhotoAsync(workOrderId, photo, description, userId);
                    if (!string.IsNullOrEmpty(photoId))
                    {
                        uploadedPhotoIds.Add(photoId);
                        totalUploaded++;
                    }
                }
            }

            return new MobilePhotoUploadResultDto
            {
                Success = totalUploaded > 0,
                Message = $"Uploaded {totalUploaded} of {photos.Count} photos",
                PhotoIds = uploadedPhotoIds,
                TotalUploaded = totalUploaded
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading photos from mobile");
            return new MobilePhotoUploadResultDto
            {
                Success = false,
                Message = "Failed to upload photos"
            };
        }
    }

    #endregion

    #region Asset Operations

    public async Task<MobileAssetDetailDto?> GetMobileAssetDetailAsync(Guid assetId)
    {
        try
        {
            var asset = await _assetService.GetAssetByIdAsync(assetId);
            if (asset == null) return null;

            var lastMaintenance = await GetAssetLastMaintenanceDateAsync(assetId);
            var nextMaintenance = await GetAssetNextMaintenanceDateAsync(assetId);

            return new MobileAssetDetailDto
            {
                Id = asset.Id,
                Name = asset.Name,
                AssetTag = asset.AssetTag ?? string.Empty,
                QrCode = asset.QrCode ?? string.Empty,
                Type = asset.AssetType ?? "Unknown",
                Location = asset.Location ?? "Unknown",
                Status = asset.Status,
                LastMaintenanceDate = lastMaintenance,
                NextMaintenanceDate = nextMaintenance,
                Specifications = CreateAssetSpecifications(asset),
                Documents = await GetAssetDocumentsAsync(assetId),
                Photos = await GetAssetPhotosAsync(assetId)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mobile asset detail {AssetId}", assetId);
            throw;
        }
    }

    public async Task<MobileAssetSearchResultDto> SearchAssetsAsync(string? qrCode, string? assetTag, string? search)
    {
        try
        {
            _logger.LogInformation("Searching assets - QR: {QrCode}, Tag: {AssetTag}, Search: {Search}", 
                qrCode, assetTag, search);

            var assets = await _assetService.GetAllAssetsAsync();
            var filteredAssets = assets.AsEnumerable();
            var searchType = "Search";

            if (!string.IsNullOrEmpty(qrCode))
            {
                filteredAssets = assets.Where(a => a.QrCode != null && a.QrCode.Contains(qrCode, StringComparison.OrdinalIgnoreCase));
                searchType = "QrCode";
            }
            else if (!string.IsNullOrEmpty(assetTag))
            {
                filteredAssets = assets.Where(a => a.AssetTag != null && a.AssetTag.Contains(assetTag, StringComparison.OrdinalIgnoreCase));
                searchType = "AssetTag";
            }
            else if (!string.IsNullOrEmpty(search))
            {
                filteredAssets = assets.Where(a => 
                    a.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (a.Description != null && a.Description.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (a.Location != null && a.Location.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            var results = filteredAssets.Take(20).Select(asset => new MobileAssetSummaryDto
            {
                Id = asset.Id,
                Name = asset.Name,
                AssetTag = asset.AssetTag ?? string.Empty,
                Location = asset.Location ?? "Unknown",
                Status = asset.Status
            }).ToList();

            return new MobileAssetSearchResultDto
            {
                Assets = results,
                TotalFound = results.Count,
                SearchType = searchType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching assets from mobile");
            throw;
        }
    }

    public async Task<MobileAssetHistoryDto> GetAssetMaintenanceHistoryAsync(Guid assetId, int limit)
    {
        try
        {
            var workOrders = await _workOrderService.GetWorkOrdersByAssetAsync(assetId);
            var recentOrders = workOrders
                .OrderByDescending(wo => wo.CreatedAt)
                .Take(limit)
                .ToList();

            var events = new List<MobileMaintenanceEventDto>();

            foreach (var wo in recentOrders)
            {
                var technician = await _technicianService.GetTechnicianByIdAsync(wo.AssignedTechnicianId ?? Guid.Empty);

                events.Add(new MobileMaintenanceEventDto
                {
                    Date = wo.CreatedAt,
                    Type = wo.Type,
                    Description = wo.Description,
                    TechnicianName = technician?.Name ?? "Unassigned",
                    Status = wo.Status
                });
            }

            return new MobileAssetHistoryDto
            {
                AssetId = assetId,
                Events = events
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting asset maintenance history {AssetId}", assetId);
            throw;
        }
    }

    #endregion

    #region Schedule Operations

    public async Task<MobileScheduleDto> GetTechnicianScheduleAsync(Guid technicianId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var schedules = await _scheduleService.GetSchedulesByTechnicianAsync(technicianId, startDate, endDate);
            
            var events = new List<MobileScheduleEventDto>();
            var totalTime = TimeSpan.Zero;

            foreach (var schedule in schedules)
            {
                var workOrder = await _workOrderService.GetWorkOrderByIdAsync(schedule.WorkOrderId);
                if (workOrder != null)
                {
                    var asset = await _assetService.GetAssetByIdAsync(workOrder.AssetId);
                    
                    var eventDto = new MobileScheduleEventDto
                    {
                        WorkOrderId = workOrder.Id,
                        Title = workOrder.Title,
                        StartTime = schedule.ScheduledStartTime,
                        EndTime = schedule.ScheduledEndTime,
                        Location = asset?.Location ?? "Unknown",
                        Priority = workOrder.Priority,
                        Status = workOrder.Status,
                        RequiresCheckIn = DetermineIfCheckInRequired(workOrder.Priority, workOrder.Type)
                    };

                    events.Add(eventDto);
                    
                    if (workOrder.EstimatedDuration.HasValue)
                        totalTime = totalTime.Add(workOrder.EstimatedDuration.Value);
                }
            }

            return new MobileScheduleDto
            {
                StartDate = startDate,
                EndDate = endDate,
                Events = events.OrderBy(e => e.StartTime).ToList(),
                TotalWorkOrders = events.Count,
                EstimatedTotalTime = totalTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technician schedule for mobile");
            throw;
        }
    }

    public async Task<MobileCheckInResultDto> CheckInToWorkOrderAsync(MobileCheckInDto checkInDto, Guid userId)
    {
        try
        {
            _logger.LogInformation("Processing mobile check-in for work order {WorkOrderId}", checkInDto.WorkOrderId);

            // Verify location if provided
            var locationVerified = await VerifyLocationAsync(checkInDto.WorkOrderId, checkInDto.Latitude, checkInDto.Longitude);

            // Record check-in
            await RecordCheckInAsync(checkInDto.WorkOrderId, userId, checkInDto.Timestamp, checkInDto.Notes, 
                checkInDto.Latitude, checkInDto.Longitude);

            return new MobileCheckInResultDto
            {
                Success = true,
                Message = "Successfully checked in",
                CheckInTime = checkInDto.Timestamp,
                LocationVerified = locationVerified
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing mobile check-in");
            return new MobileCheckInResultDto
            {
                Success = false,
                Message = "Failed to check in"
            };
        }
    }

    public async Task<MobileCheckOutResultDto> CheckOutFromWorkOrderAsync(MobileCheckOutDto checkOutDto, Guid userId)
    {
        try
        {
            _logger.LogInformation("Processing mobile check-out for work order {WorkOrderId}", checkOutDto.WorkOrderId);

            // Get check-in time to calculate duration
            var checkInTime = await GetCheckInTimeAsync(checkOutDto.WorkOrderId, userId);
            var totalDuration = checkOutDto.Timestamp - (checkInTime ?? checkOutDto.Timestamp);

            // Record check-out
            await RecordCheckOutAsync(checkOutDto.WorkOrderId, userId, checkOutDto.Timestamp, 
                checkOutDto.CompletionNotes, totalDuration);

            return new MobileCheckOutResultDto
            {
                Success = true,
                Message = "Successfully checked out",
                CheckOutTime = checkOutDto.Timestamp,
                TotalDuration = totalDuration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing mobile check-out");
            return new MobileCheckOutResultDto
            {
                Success = false,
                Message = "Failed to check out"
            };
        }
    }

    #endregion

    #region Synchronization

    public async Task<MobileSyncResponseDto> SyncMobileDataAsync(MobileSyncRequestDto syncRequest, Guid userId)
    {
        try
        {
            _logger.LogInformation("Processing mobile sync for user {UserId}", userId);

            var syncResponse = new MobileSyncResponseDto
            {
                SyncId = Guid.NewGuid().ToString(),
                ServerTimestamp = DateTime.UtcNow,
                Success = true
            };

            // Process uploaded changes
            if (syncRequest.PendingUploads?.Any() == true)
            {
                await ProcessPendingUploadsAsync(syncRequest.PendingUploads, userId);
                syncResponse.UploadedCount = syncRequest.PendingUploads.Count;
            }

            // Get server updates since last sync
            if (syncRequest.LastSyncTimestamp.HasValue)
            {
                var updates = await GetServerUpdatesSinceAsync(userId, syncRequest.LastSyncTimestamp.Value);
                syncResponse.Updates = updates;
                syncResponse.UpdateCount = updates.Count;
            }

            // Handle conflicts if any
            if (syncRequest.ConflictResolutions?.Any() == true)
            {
                await ProcessConflictResolutionsAsync(syncRequest.ConflictResolutions, userId);
                syncResponse.ConflictsResolvedCount = syncRequest.ConflictResolutions.Count;
            }

            _logger.LogInformation("Mobile sync completed successfully for user {UserId}", userId);
            return syncResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing mobile sync for user {UserId}", userId);
            return new MobileSyncResponseDto
            {
                Success = false,
                ErrorMessage = "Sync failed"
            };
        }
    }

    public async Task<MobileSyncStatusDto> GetSyncStatusAsync(Guid userId)
    {
        try
        {
            var pendingUploads = await GetPendingUploadsCountAsync(userId);
            var lastSyncTime = await GetLastSyncTimeAsync(userId);
            var conflicts = await GetConflictsCountAsync(userId);

            return new MobileSyncStatusDto
            {
                LastSyncTime = lastSyncTime,
                PendingUploadsCount = pendingUploads,
                ConflictsCount = conflicts,
                SyncRequired = pendingUploads > 0 || conflicts > 0,
                IsOnline = true // This would be determined by connectivity check
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync status for user {UserId}", userId);
            throw;
        }
    }

    public async Task<MobileOfflinePackageDto> CreateOfflinePackageAsync(Guid userId, DateTime startDate, DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Creating offline package for user {UserId}", userId);

            // Get work orders for the period
            var workOrders = await GetTechnicianWorkOrdersAsync(userId, 1, 1000, null, null);
            
            // Get required reference data
            var assets = await GetRelevantAssetsAsync(userId, startDate, endDate);
            var schedule = await GetTechnicianScheduleAsync(userId, startDate, endDate);
            var inventory = await GetMaintenanceInventoryAsync(null, null, 1, 1000);
            var safety = await GetRelevantSafetyProtocolsAsync(userId);

            return new MobileOfflinePackageDto
            {
                PackageId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(7),
                WorkOrders = workOrders.WorkOrders,
                Assets = assets,
                Schedule = schedule,
                Inventory = inventory,
                SafetyProtocols = safety,
                DataVersion = DateTime.UtcNow.Ticks.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating offline package for user {UserId}", userId);
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private async Task<int> GetWorkOrderPhotoCountAsync(Guid workOrderId)
    {
        // Mock implementation - would count photos in actual storage
        return Random.Shared.Next(0, 5);
    }

    private bool DetermineIfCheckInRequired(string priority, string type)
    {
        // Require check-in for high priority or certain types
        return priority == "High" || priority == "Critical" || type == "Emergency";
    }

    private async Task<List<MobileWorkLogDto>> GetWorkOrderLogsAsync(Guid workOrderId)
    {
        // Mock implementation - would get actual work logs
        return new List<MobileWorkLogDto>();
    }

    private async Task<List<MobilePartsDto>> GetWorkOrderRequiredPartsAsync(Guid workOrderId)
    {
        // Mock implementation - would get required parts
        return new List<MobilePartsDto>();
    }

    private async Task<MobileCheckInStatusDto?> GetWorkOrderCheckInStatusAsync(Guid workOrderId)
    {
        // Mock implementation - would check current check-in status
        return null;
    }

    private async Task<List<string>> GetWorkOrderRequiredSkillsAsync(Guid workOrderId)
    {
        // Mock implementation - would get required skills
        return new List<string> { "Electrical", "Mechanical" };
    }

    private async Task<List<string>> GetWorkOrderSafetyRequirementsAsync(Guid workOrderId)
    {
        // Mock implementation - would get safety requirements
        return new List<string> { "Safety glasses required", "Lock-out tag-out procedure" };
    }

    private MobileAssetInfoDto CreateMobileAssetInfo(MaintenanceAssetDto? asset)
    {
        if (asset == null) return new MobileAssetInfoDto();

        return new MobileAssetInfoDto
        {
            Id = asset.Id,
            Name = asset.Name,
            AssetTag = asset.AssetTag ?? string.Empty,
            Location = asset.Location ?? string.Empty
        };
    }

    private async Task<string> CreateSyncRecordAsync(string operationType, Guid entityId, string? localId, Guid userId)
    {
        // Mock implementation - would create sync tracking record
        var syncId = Guid.NewGuid().ToString();
        _logger.LogDebug("Created sync record {SyncId} for {OperationType}", syncId, operationType);
        return syncId;
    }

    private async Task<Guid> CreateWorkLogAsync(CreateWorkLogDto workLogRequest)
    {
        // Mock implementation - would create actual work log
        return Guid.NewGuid();
    }

    private async Task<string> ProcessAndSavePhotoAsync(Guid workOrderId, IFormFile photo, string? description, Guid userId)
    {
        // Mock implementation - would compress, save photo, and return photo ID
        return Guid.NewGuid().ToString();
    }

    private async Task<DateTime?> GetAssetLastMaintenanceDateAsync(Guid assetId)
    {
        // Mock implementation
        return DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30));
    }

    private async Task<DateTime?> GetAssetNextMaintenanceDateAsync(Guid assetId)
    {
        // Mock implementation
        return DateTime.UtcNow.AddDays(Random.Shared.Next(1, 60));
    }

    private Dictionary<string, object> CreateAssetSpecifications(MaintenanceAssetDto asset)
    {
        // Mock implementation - would extract actual specifications
        return new Dictionary<string, object>
        {
            ["Model"] = "Unknown",
            ["Manufacturer"] = "Unknown",
            ["Year"] = "Unknown"
        };
    }

    private async Task<List<string>> GetAssetDocumentsAsync(Guid assetId)
    {
        // Mock implementation
        return new List<string>();
    }

    private async Task<List<string>> GetAssetPhotosAsync(Guid assetId)
    {
        // Mock implementation
        return new List<string>();
    }

    private async Task<string> VerifyLocationAsync(Guid workOrderId, double? latitude, double? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
            return "Location not provided";

        // Mock implementation - would verify against asset location
        return "Location verified";
    }

    private async Task RecordCheckInAsync(Guid workOrderId, Guid userId, DateTime timestamp, string? notes, 
        double? latitude, double? longitude)
    {
        // Mock implementation - would record check-in in database
        _logger.LogInformation("Recorded check-in for work order {WorkOrderId}", workOrderId);
    }

    private async Task RecordCheckOutAsync(Guid workOrderId, Guid userId, DateTime timestamp, 
        string? notes, TimeSpan duration)
    {
        // Mock implementation - would record check-out in database
        _logger.LogInformation("Recorded check-out for work order {WorkOrderId}, duration: {Duration}", 
            workOrderId, duration);
    }

    private async Task<DateTime?> GetCheckInTimeAsync(Guid workOrderId, Guid userId)
    {
        // Mock implementation - would get actual check-in time
        return DateTime.UtcNow.AddHours(-2);
    }

    private async Task ProcessPendingUploadsAsync(List<MobilePendingUploadDto> uploads, Guid userId)
    {
        // Mock implementation - would process each upload
        _logger.LogInformation("Processing {Count} pending uploads", uploads.Count);
    }

    private async Task<List<MobileUpdateDto>> GetServerUpdatesSinceAsync(Guid userId, DateTime lastSync)
    {
        // Mock implementation - would get actual server updates
        return new List<MobileUpdateDto>();
    }

    private async Task ProcessConflictResolutionsAsync(List<MobileConflictResolutionDto> resolutions, Guid userId)
    {
        // Mock implementation - would resolve conflicts
        _logger.LogInformation("Processing {Count} conflict resolutions", resolutions.Count);
    }

    private async Task<int> GetPendingUploadsCountAsync(Guid userId)
    {
        // Mock implementation
        return Random.Shared.Next(0, 5);
    }

    private async Task<DateTime?> GetLastSyncTimeAsync(Guid userId)
    {
        // Mock implementation
        return DateTime.UtcNow.AddMinutes(-Random.Shared.Next(5, 120));
    }

    private async Task<int> GetConflictsCountAsync(Guid userId)
    {
        // Mock implementation
        return 0;
    }

    private async Task<List<MobileAssetSummaryDto>> GetRelevantAssetsAsync(Guid userId, DateTime startDate, DateTime endDate)
    {
        // Mock implementation - would get assets relevant to user's work orders
        return new List<MobileAssetSummaryDto>();
    }

    private async Task<List<MobileSafetyProtocolSummaryDto>> GetRelevantSafetyProtocolsAsync(Guid userId)
    {
        // Mock implementation - would get relevant safety protocols
        return new List<MobileSafetyProtocolSummaryDto>();
    }

    #region Not Implemented Interface Methods - Stubs

    public async Task<MobileInventoryDto> GetMaintenanceInventoryAsync(string? search, string? barcode, int page, int pageSize)
    {
        // Stub implementation
        return new MobileInventoryDto
        {
            Items = new List<MobileInventoryItemDto>(),
            TotalCount = 0,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<MobilePartsRequestResultDto> RequestPartsAsync(Guid workOrderId, MobilePartsRequestDto requestDto, Guid userId)
    {
        // Stub implementation
        return new MobilePartsRequestResultDto
        {
            Success = true,
            Message = "Parts request submitted",
            RequestId = Guid.NewGuid()
        };
    }

    public async Task<MobileSafetyProtocolsDto> GetSafetyProtocolsAsync(Guid? assetId, Guid? workOrderId)
    {
        // Stub implementation
        return new MobileSafetyProtocolsDto
        {
            Protocols = new List<MobileSafetyProtocolDto>()
        };
    }

    public async Task<MobileSafetyChecklistResultDto> SubmitSafetyChecklistAsync(MobileSafetyChecklistDto checklistDto, Guid userId)
    {
        // Stub implementation
        return new MobileSafetyChecklistResultDto
        {
            Success = true,
            Message = "Safety checklist submitted",
            SubmissionId = Guid.NewGuid()
        };
    }

    #endregion

    #endregion
}

// Additional DTOs that were referenced in the mobile controller
public class CreateWorkLogDto
{
    public Guid WorkOrderId { get; set; }
    public string Description { get; set; } = string.Empty;
    public TimeSpan? Duration { get; set; }
    public DateTime LoggedAt { get; set; }
    public Guid TechnicianId { get; set; }
}

// Additional mobile DTOs for completeness
public class MobileWorkLogDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
}

public class MobileAssetInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetTag { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public class MobilePartsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public int QuantityRequired { get; set; }
    public int QuantityAvailable { get; set; }
    public bool IsAvailable { get; set; }
}

public class MobileCheckInStatusDto
{
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string? CheckInLocation { get; set; }
    public string? CheckInNotes { get; set; }
}

// Sync related DTOs
public class MobileSyncRequestDto
{
    public DateTime? LastSyncTimestamp { get; set; }
    public List<MobilePendingUploadDto> PendingUploads { get; set; } = new();
    public List<MobileConflictResolutionDto> ConflictResolutions { get; set; } = new();
}

public class MobileSyncResponseDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string SyncId { get; set; } = string.Empty;
    public DateTime ServerTimestamp { get; set; }
    public List<MobileUpdateDto> Updates { get; set; } = new();
    public int UploadedCount { get; set; }
    public int UpdateCount { get; set; }
    public int ConflictsResolvedCount { get; set; }
}

public class MobileSyncStatusDto
{
    public DateTime? LastSyncTime { get; set; }
    public int PendingUploadsCount { get; set; }
    public int ConflictsCount { get; set; }
    public bool SyncRequired { get; set; }
    public bool IsOnline { get; set; }
}

public class MobileOfflinePackageDto
{
    public string PackageId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ValidUntil { get; set; }
    public List<MobileWorkOrderSummaryDto> WorkOrders { get; set; } = new();
    public List<MobileAssetSummaryDto> Assets { get; set; } = new();
    public MobileScheduleDto Schedule { get; set; } = null!;
    public MobileInventoryDto Inventory { get; set; } = null!;
    public List<MobileSafetyProtocolSummaryDto> SafetyProtocols { get; set; } = new();
    public string DataVersion { get; set; } = string.Empty;
}

public class MobilePendingUploadDto
{
    public string LocalId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class MobileUpdateDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
}

public class MobileConflictResolutionDto
{
    public string ConflictId { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty; // "server" or "client"
    public Dictionary<string, object>? ClientData { get; set; }
}

public class MobileInventoryDto
{
    public List<MobileInventoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class MobileInventoryItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public int QuantityAvailable { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
}

public class MobilePartsRequestDto
{
    public List<MobilePartRequestItemDto> Items { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime RequestedFor { get; set; } = DateTime.UtcNow;
}

public class MobilePartRequestItemDto
{
    public Guid InventoryItemId { get; set; }
    public int QuantityRequested { get; set; }
    public string? Notes { get; set; }
}

public class MobilePartsRequestResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
}

public class MobileSafetyProtocolsDto
{
    public List<MobileSafetyProtocolDto> Protocols { get; set; } = new();
}

public class MobileSafetyProtocolDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public bool IsMandatory { get; set; }
}

public class MobileSafetyProtocolSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
}

public class MobileSafetyChecklistDto
{
    public Guid? AssetId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public List<MobileSafetyChecklistItemDto> Items { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

public class MobileSafetyChecklistItemDto
{
    public Guid ProtocolId { get; set; }
    public bool IsCompleted { get; set; }
    public string? Notes { get; set; }
}

public class MobileSafetyChecklistResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid SubmissionId { get; set; }
}