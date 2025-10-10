using ErpSystem.Core.Entities;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

#region Asset Management Services

public interface IMaintenanceAssetService
{
    // CRUD operations
    Task<MaintenanceAssetDto> CreateAssetAsync(CreateMaintenanceAssetDto createDto);
    Task<MaintenanceAssetDto> UpdateAssetAsync(Guid id, UpdateMaintenanceAssetDto updateDto);
    Task DeleteAssetAsync(Guid id);
    Task<MaintenanceAssetDto?> GetAssetByIdAsync(Guid id);
    Task<IEnumerable<MaintenanceAssetDto>> GetAllAssetsAsync();
    Task<PagedResult<MaintenanceAssetListDto>> GetAssetsPagedAsync(int page, int pageSize, string? searchTerm = null, Guid? categoryId = null);

    // Business logic methods
    Task<bool> IsAssetNumberUniqueAsync(string assetNumber, Guid? excludeId = null);
    Task<IEnumerable<MaintenanceAssetDto>> GetAssetsByStatusAsync(string status);
    Task<IEnumerable<MaintenanceAssetDto>> GetAssetsByCategoryAsync(Guid categoryId);
    Task<IEnumerable<MaintenanceAssetDto>> GetAssetsWithActiveWorkOrdersAsync();
    Task<IEnumerable<MaintenanceAssetDto>> GetAssetsRequiringMaintenanceAsync();
    Task<IEnumerable<MaintenanceAssetDto>> GetAssetHierarchyAsync(Guid rootAssetId);
    Task<AssetMetricsDto> GetAssetMetricsAsync();
    Task UpdateAssetOperatingHoursAsync(Guid assetId, double operatingHours);
    Task UpdateAssetMileageAsync(Guid assetId, double mileage);
    Task<string> GenerateAssetNumberAsync(Guid categoryId);
}

public interface IMaintenanceAssetCategoryService
{
    Task<MaintenanceAssetCategoryDto> CreateCategoryAsync(CreateMaintenanceAssetCategoryDto createDto);
    Task<MaintenanceAssetCategoryDto> UpdateCategoryAsync(Guid id, UpdateMaintenanceAssetCategoryDto updateDto);
    Task DeleteCategoryAsync(Guid id);
    Task<MaintenanceAssetCategoryDto?> GetCategoryByIdAsync(Guid id);
    Task<IEnumerable<MaintenanceAssetCategoryDto>> GetAllCategoriesAsync();
    Task<IEnumerable<MaintenanceAssetCategoryDto>> GetActiveCategoriesAsync();
    Task<bool> IsCategoryCodeUniqueAsync(string code, Guid? excludeId = null);
    Task<CategoryStatisticsDto> GetCategoryStatisticsAsync(Guid categoryId);
    Task<IEnumerable<MaintenanceAssetCategoryDto>> GetCategoryHierarchyAsync(Guid? parentId = null);
    Task<IEnumerable<MaintenanceAssetCategoryDto>> GetChildCategoriesAsync(Guid parentId);
}

public interface IAssetTypeService
{
    // CRUD operations
    Task<AssetTypeDto> CreateAssetTypeAsync(CreateAssetTypeDto createDto, Guid userId, Guid tenantId);
    Task<AssetTypeDto> UpdateAssetTypeAsync(Guid id, UpdateAssetTypeDto updateDto, Guid userId, Guid tenantId);
    Task<bool> DeleteAssetTypeAsync(Guid id, Guid tenantId);
    Task<AssetTypeDto?> GetAssetTypeByIdAsync(Guid id, Guid tenantId);
    Task<IEnumerable<AssetTypeDto>> GetAssetTypesAsync(Guid tenantId, bool includeInactive = false);
    Task<IEnumerable<AssetTypeDto>> GetAssetTypesByCategoryAsync(string category, Guid tenantId, bool includeInactive = false);
    
    // Business logic
    Task<bool> IsAssetTypeNameUniqueAsync(string name, Guid tenantId, Guid? excludeId = null);
    Task<bool> IsAssetTypeCodeUniqueAsync(string code, Guid tenantId, Guid? excludeId = null);
    Task<bool> AssetTypeExistsAsync(Guid assetTypeId, Guid tenantId);
    Task<IEnumerable<string>> GetAssetTypeCategoriesAsync(Guid tenantId);
    Task<int> GetAssetCountByTypeAsync(Guid assetTypeId, Guid tenantId);
}

#endregion

#region Work Order Services

public interface IWorkOrderService
{
    // CRUD operations
    Task<WorkOrderDto> CreateWorkOrderAsync(CreateWorkOrderDto createDto);
    Task<WorkOrderDto> UpdateWorkOrderAsync(Guid id, UpdateWorkOrderDto updateDto);
    Task DeleteWorkOrderAsync(Guid id);
    Task<WorkOrderDto?> GetWorkOrderByIdAsync(Guid id);
    Task<WorkOrderDto?> GetWorkOrderByNumberAsync(string workOrderNumber);
    Task<PagedResult<WorkOrderListDto>> GetWorkOrdersPagedAsync(WorkOrderFilterDto filter);

    // Status management
    Task<WorkOrderDto> UpdateWorkOrderStatusAsync(Guid id, string status, string? notes = null);
    Task<WorkOrderDto> AssignWorkOrderAsync(Guid id, Guid? technicianId, Guid? teamId);
    Task<WorkOrderDto> ApproveWorkOrderAsync(Guid id, string? approvalNotes = null);
    Task<WorkOrderDto> StartWorkOrderAsync(Guid id);
    Task<WorkOrderDto> CompleteWorkOrderAsync(Guid id, CompleteWorkOrderDto completeDto);

    // Business logic
    Task<IEnumerable<WorkOrderListDto>> GetOverdueWorkOrdersAsync();
    Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersDueInDaysAsync(int days);
    Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersDueSoonAsync(int days = 7);
    Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersByTechnicianAsync(Guid technicianId);
    Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersByTeamAsync(Guid teamId);
    Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersByAssetAsync(Guid assetId);
    Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersByStatusAsync(string status);
    Task<IEnumerable<WorkOrderListDto>> GetWorkOrdersScheduledTodayAsync();
    Task<WorkOrderMetricsDto> GetWorkOrderMetricsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<string> GenerateWorkOrderNumberAsync();
    
    // Additional operations
    Task<WorkOrderDto> AssignTechnicianAsync(Guid workOrderId, Guid technicianId);
    Task<WorkOrderDto> CancelWorkOrderAsync(Guid workOrderId, string reason);
    Task<WorkOrderDto> UpdatePriorityAsync(Guid workOrderId, string priority);
    Task<WorkOrderDto> RescheduleWorkOrderAsync(Guid workOrderId, DateTime newScheduledDate);
    Task<IEnumerable<WorkOrderDto>> CreateWorkOrdersFromScheduleAsync(Guid scheduleId, int count = 1);

    // Child work orders
    Task<WorkOrderDto> CreateChildWorkOrderAsync(Guid parentId, CreateWorkOrderDto createDto);
    Task<IEnumerable<WorkOrderListDto>> GetChildWorkOrdersAsync(Guid parentId);
}

public interface IWorkOrderTaskService
{
    Task<WorkOrderTaskDto> CreateTaskAsync(CreateWorkOrderTaskDto createDto);
    Task<WorkOrderTaskDto> UpdateTaskAsync(Guid id, UpdateWorkOrderTaskDto updateDto);
    Task DeleteTaskAsync(Guid id);
    Task<IEnumerable<WorkOrderTaskDto>> GetTasksByWorkOrderAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderTaskDto>> GetTasksByTechnicianAsync(Guid technicianId);
    Task<WorkOrderTaskDto> UpdateTaskStatusAsync(Guid id, string status, string? notes = null);
    Task<WorkOrderTaskDto> StartTaskAsync(Guid id);
    Task<WorkOrderTaskDto> CompleteTaskAsync(Guid id, double actualHours, string? notes = null);
    Task<double> GetWorkOrderCompletionPercentageAsync(Guid workOrderId);
}

public interface IWorkOrderPartService
{
    Task<WorkOrderPartDto> AddPartAsync(CreateWorkOrderPartDto createDto);
    Task<WorkOrderPartDto> UpdatePartAsync(Guid id, UpdateWorkOrderPartDto updateDto);
    Task DeletePartAsync(Guid id);
    Task<IEnumerable<WorkOrderPartDto>> GetPartsByWorkOrderAsync(Guid workOrderId);
    Task<WorkOrderPartDto> UpdatePartStatusAsync(Guid id, string status, int? quantityUsed = null);
    Task<decimal> GetTotalPartsCostAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderPartDto>> GetPartsRequiringOrderAsync();
}

public interface IWorkOrderLaborService
{
    Task<WorkOrderLaborDto> StartLaborAsync(CreateWorkOrderLaborDto createDto);
    Task<WorkOrderLaborDto> EndLaborAsync(Guid id, DateTime endTime, string? notes = null);
    Task<WorkOrderLaborDto> UpdateLaborAsync(Guid id, UpdateWorkOrderLaborDto updateDto);
    Task DeleteLaborAsync(Guid id);
    Task<IEnumerable<WorkOrderLaborDto>> GetLaborByWorkOrderAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderLaborDto>> GetLaborByTechnicianAsync(Guid technicianId, DateTime? startDate = null, DateTime? endDate = null);
    Task<decimal> GetTotalLaborCostAsync(Guid workOrderId);
    Task<LaborReportDto> GetLaborReportAsync(Guid technicianId, DateTime startDate, DateTime endDate);
}

#endregion

#region Maintenance Scheduling Services

public interface IMaintenanceScheduleService
{
    Task<MaintenanceScheduleDto> CreateScheduleAsync(CreateMaintenanceScheduleDto createDto);
    Task<MaintenanceScheduleDto> UpdateScheduleAsync(Guid id, UpdateMaintenanceScheduleDto updateDto);
    Task DeleteScheduleAsync(Guid id);
    Task<MaintenanceScheduleDto?> GetScheduleByIdAsync(Guid id);
    Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesByAssetAsync(Guid assetId);
    Task<IEnumerable<MaintenanceScheduleDto>> GetActiveSchedulesAsync();
    Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesDueInDaysAsync(int days);

    // Schedule processing
    Task ProcessSchedulesAsync(); // Background service method
    Task<WorkOrderDto> GenerateWorkOrderFromScheduleAsync(Guid scheduleId);
    Task UpdateNextDueDateAsync(Guid scheduleId);
    Task<DateTime> CalculateNextDueDateAsync(MaintenanceScheduleDto schedule);
    Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesDueForCreationAsync();
    Task<IEnumerable<WorkOrderDto>> CreateWorkOrdersFromScheduleAsync(Guid scheduleId, int count = 1);

    // Analytics
    Task<ScheduleComplianceReportDto> GetScheduleComplianceReportAsync(DateTime startDate, DateTime endDate);
}

#endregion

#region Inspection Services

public interface IInspectionTemplateService
{
    Task<InspectionTemplateDto> CreateTemplateAsync(CreateInspectionTemplateDto createDto);
    Task<InspectionTemplateDto> UpdateTemplateAsync(Guid id, UpdateInspectionTemplateDto updateDto);
    Task DeleteTemplateAsync(Guid id);
    Task<InspectionTemplateDto?> GetTemplateByIdAsync(Guid id);
    Task<IEnumerable<InspectionTemplateDto>> GetAllTemplatesAsync();
    Task<IEnumerable<InspectionTemplateDto>> GetTemplatesByCategoryAsync(string category);
    Task<IEnumerable<InspectionTemplateDto>> GetActiveTemplatesAsync();
}

public interface IAssetInspectionService
{
    Task<AssetInspectionDto> CreateInspectionAsync(CreateAssetInspectionDto createDto);
    Task<AssetInspectionDto> UpdateInspectionAsync(Guid id, UpdateAssetInspectionDto updateDto);
    Task DeleteInspectionAsync(Guid id);
    Task<AssetInspectionDto?> GetInspectionByIdAsync(Guid id);
    Task<IEnumerable<AssetInspectionDto>> GetInspectionsByAssetAsync(Guid assetId);
    Task<IEnumerable<AssetInspectionDto>> GetInspectionsByInspectorAsync(Guid inspectorId);
    Task<IEnumerable<AssetInspectionDto>> GetInspectionsDueAsync();
    Task<IEnumerable<AssetInspectionDto>> GetRegulatoryInspectionsAsync();

    // Inspection workflow
    Task<AssetInspectionDto> StartInspectionAsync(Guid id);
    Task<AssetInspectionDto> CompleteInspectionAsync(Guid id, CompleteInspectionDto completeDto);
    Task<AssetInspectionDto> FailInspectionAsync(Guid id, string failureReasons, string recommendedActions);

    // Analytics
    Task<InspectionComplianceReportDto> GetInspectionComplianceReportAsync(DateTime startDate, DateTime endDate);
    Task<AssetInspectionDto?> GetLatestInspectionByAssetAsync(Guid assetId);
}

#endregion

#region Resource Management Services

public interface ITechnicianTeamService
{
    Task<TechnicianTeamDto> CreateTeamAsync(CreateTechnicianTeamDto createDto);
    Task<TechnicianTeamDto> UpdateTeamAsync(Guid id, UpdateTechnicianTeamDto updateDto);
    Task DeleteTeamAsync(Guid id);
    Task<TechnicianTeamDto?> GetTeamByIdAsync(Guid id);
    Task<IEnumerable<TechnicianTeamDto>> GetAllTeamsAsync();
    Task<IEnumerable<TechnicianTeamDto>> GetActiveTeamsAsync();

    // Team member management
    Task<TechnicianTeamMemberDto> AddMemberAsync(CreateTechnicianTeamMemberDto createDto);
    Task<TechnicianTeamMemberDto> UpdateMemberAsync(Guid id, UpdateTechnicianTeamMemberDto updateDto);
    Task RemoveMemberAsync(Guid id);
    Task<IEnumerable<TechnicianTeamMemberDto>> GetTeamMembersAsync(Guid teamId);
    Task<IEnumerable<TechnicianTeamMemberDto>> GetActiveTeamMembersAsync(Guid teamId);

    // Team analytics
    Task<TeamPerformanceReportDto> GetTeamPerformanceReportAsync(Guid teamId, DateTime startDate, DateTime endDate);
    Task<TeamWorkloadReportDto> GetTeamWorkloadReportAsync(Guid teamId);
}

public interface ITechnicianSkillService
{
    Task<TechnicianSkillDto> CreateSkillAsync(CreateTechnicianSkillDto createDto);
    Task<TechnicianSkillDto> UpdateSkillAsync(Guid id, UpdateTechnicianSkillDto updateDto);
    Task DeleteSkillAsync(Guid id);
    Task<TechnicianSkillDto?> GetSkillByIdAsync(Guid id);
    Task<IEnumerable<TechnicianSkillDto>> GetAllSkillsAsync();
    Task<IEnumerable<TechnicianSkillDto>> GetActiveSkillsAsync();
    Task<IEnumerable<TechnicianSkillDto>> GetSkillsByCategoryAsync(string category);

    // User skill management
    Task<UserTechnicianSkillDto> AssignSkillToUserAsync(CreateUserTechnicianSkillDto createDto);
    Task<UserTechnicianSkillDto> UpdateUserSkillAsync(Guid id, UpdateUserTechnicianSkillDto updateDto);
    Task RemoveUserSkillAsync(Guid id);
    Task<IEnumerable<UserTechnicianSkillDto>> GetUserSkillsAsync(Guid userId);
    Task<IEnumerable<UserTechnicianSkillDto>> GetExpiredCertificationsAsync();
    Task<IEnumerable<UserTechnicianSkillDto>> GetCertificationsExpiringInDaysAsync(int days);

    // Skill matching
    Task<IEnumerable<string>> GetTechniciansWithSkillAsync(Guid skillId, int? minProficiencyLevel = null);
    Task<SkillGapAnalysisDto> GetSkillGapAnalysisAsync();
}

#endregion

#region Asset Downtime Services

public interface IAssetDowntimeService
{
    Task<AssetDowntimeDto> StartDowntimeAsync(CreateAssetDowntimeDto createDto);
    Task<AssetDowntimeDto> EndDowntimeAsync(Guid id, DateTime endTime, string? resolutionNotes = null);
    Task<AssetDowntimeDto> UpdateDowntimeAsync(Guid id, UpdateAssetDowntimeDto updateDto);
    Task DeleteDowntimeAsync(Guid id);
    Task<AssetDowntimeDto?> GetDowntimeByIdAsync(Guid id);
    Task<IEnumerable<AssetDowntimeDto>> GetDowntimeByAssetAsync(Guid assetId);
    Task<IEnumerable<AssetDowntimeDto>> GetActiveDowntimeAsync();

    // Analytics
    Task<DowntimeAnalyticsDto> GetDowntimeAnalyticsAsync(DateTime startDate, DateTime endDate, Guid? assetId = null);
    Task<AssetDowntimeDto?> GetActiveDowntimeByAssetAsync(Guid assetId);
    Task<double> GetAssetAvailabilityPercentageAsync(Guid assetId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<AssetDowntimeReportDto>> GetDowntimeReportAsync(DateTime startDate, DateTime endDate);
}

#endregion

#region Analytics and Reporting Services

public interface IMaintenanceAnalyticsService
{
    Task<MaintenanceDashboardDto> GetDashboardDataAsync();
    Task<MaintenanceKPIsDto> GetMaintenanceKPIsAsync(DateTime startDate, DateTime endDate);
    Task<AssetPerformanceReportDto> GetAssetPerformanceReportAsync(Guid assetId, DateTime startDate, DateTime endDate);
    Task<MaintenanceCostAnalysisDto> GetCostAnalysisAsync(DateTime startDate, DateTime endDate);
    Task<PreventiveMaintenanceReportDto> GetPreventiveMaintenanceReportAsync(DateTime startDate, DateTime endDate);
    Task<WorkOrderTrendsDto> GetWorkOrderTrendsAsync(DateTime startDate, DateTime endDate);
    Task<TechnicianUtilizationReportDto> GetTechnicianUtilizationReportAsync(DateTime startDate, DateTime endDate);
    Task<AssetReliabilityReportDto> GetAssetReliabilityReportAsync(DateTime startDate, DateTime endDate);
}

public interface IMaintenanceReportService
{
    Task<byte[]> GenerateWorkOrderReportAsync(WorkOrderReportFilterDto filter);
    Task<byte[]> GenerateAssetReportAsync(AssetReportFilterDto filter);
    Task<byte[]> GenerateMaintenanceScheduleReportAsync(ScheduleReportFilterDto filter);
    Task<byte[]> GenerateDowntimeReportAsync(DowntimeReportFilterDto filter);
    Task<byte[]> GenerateInspectionReportAsync(InspectionReportFilterDto filter);
    Task<byte[]> GenerateMaintenanceCostReportAsync(CostReportFilterDto filter);
}

#endregion

#region Attachment Services

public interface IMaintenanceAttachmentService
{
    // Basic CRUD operations
    Task<MaintenanceAttachmentDto> CreateAttachmentAsync(CreateMaintenanceAttachmentDto createDto);
    Task<MaintenanceAttachmentDto> UpdateAttachmentAsync(Guid id, UpdateMaintenanceAttachmentDto updateDto);
    Task DeleteAttachmentAsync(Guid id);
    Task<MaintenanceAttachmentDto?> GetAttachmentByIdAsync(Guid id);
    
    // Query operations
    Task<IEnumerable<MaintenanceAttachmentDto>> GetAttachmentsByEntityAsync(string entityType, Guid entityId, string? category = null, string? attachmentType = null);
    Task<IEnumerable<MaintenanceAttachmentDto>> GetAttachmentsByTypeAsync(string attachmentType);
    Task<IEnumerable<MaintenanceAttachmentDto>> GetRecentAttachmentsAsync(int count = 10);
    
    // File operations
    Task<byte[]> GetAttachmentFileAsync(Guid id);
    Task<string> GetAttachmentUrlAsync(Guid id);
    Task<string> GetThumbnailUrlAsync(Guid id);
    Task<bool> SetMainImageAsync(Guid attachmentId, Guid entityId);
    
    // Storage statistics
    Task<long> GetTotalStorageUsedAsync();
    
    // Tag management
    Task AddAttachmentTagsAsync(Guid attachmentId, List<AttachmentTagDto> tags);
    Task RemoveTagAsync(Guid attachmentId, string tagName);
    Task<IEnumerable<MaintenanceAttachmentDto>> GetAttachmentsByTagAsync(string tagName, string? tagValue = null);
    
    // Access tracking
    Task LogAttachmentAccessAsync(Guid attachmentId, Guid userId, string accessType);
    Task<IEnumerable<AttachmentAccessLogDto>> GetAttachmentAccessLogsAsync(Guid attachmentId);
}

#endregion

#region Background Services

public interface IMaintenanceBackgroundService
{
    Task ProcessScheduledMaintenanceAsync();
    Task SendMaintenanceNotificationsAsync();
    Task UpdateAssetMetricsAsync();
    Task ProcessExpiredCertificationsAsync();
    Task CalculateAssetDowntimeAsync();
    Task GenerateMaintenanceAlertsAsync();
}

#endregion

