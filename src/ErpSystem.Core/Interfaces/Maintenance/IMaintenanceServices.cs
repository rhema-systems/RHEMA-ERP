using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Maintenance;

#region Asset Management Services

public interface IMaintenanceAssetService
{
    // CRUD operations
    Task<MaintenanceAssetDto> CreateAssetAsync(CreateMaintenanceAssetDto createDto);
    Task<MaintenanceAssetDto> UpdateAssetAsync(Guid id, UpdateMaintenanceAssetDto updateDto);
    Task DeleteAssetAsync(Guid id);
    Task<MaintenanceAssetDto?> GetAssetByIdAsync(Guid id);
    Task<IEnumerable<MaintenanceAssetDto>> GetAssetsByIdsAsync(List<Guid> ids);
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
    Task<IEnumerable<MaintenanceAssetDto>> GetAvailableVehiclesAsync();
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

#region Task Template Management Service

public interface ITaskTemplateService
{
    // Get task templates for work order generation
    Task<IEnumerable<WorkOrderTaskDto>> GetTaskTemplatesForWorkOrderAsync(Guid assetId, Guid maintenanceTypeId);

    // Manage asset-specific task templates
    Task<IEnumerable<AssetTaskTemplateDto>> GetAssetTaskTemplatesAsync(Guid assetId);
    Task<IEnumerable<AssetTaskTemplateDto>> GetAllAssetTaskTemplatesAsync();
    Task<AssetTaskTemplateDto> CreateAssetTaskTemplateAsync(CreateAssetTaskTemplateDto createDto);
    Task<AssetTaskTemplateDto> UpdateAssetTaskTemplateAsync(Guid id, UpdateAssetTaskTemplateDto updateDto);
    Task DeleteAssetTaskTemplateAsync(Guid id);

    // Manage asset type task templates
    Task<IEnumerable<AssetTypeTaskTemplateDto>> GetAssetTypeTaskTemplatesAsync(Guid assetTypeId);
    Task<IEnumerable<AssetTypeTaskTemplateDto>> GetAllAssetTypeTaskTemplatesAsync();
    Task<AssetTypeTaskTemplateDto> CreateAssetTypeTaskTemplateAsync(CreateAssetTypeTaskTemplateDto createDto);
    Task<AssetTypeTaskTemplateDto> UpdateAssetTypeTaskTemplateAsync(Guid id, UpdateAssetTypeTaskTemplateDto updateDto);
    Task DeleteAssetTypeTaskTemplateAsync(Guid id);

    // Manage maintenance type task templates (system defaults)
    Task<IEnumerable<MaintenanceTaskTemplateDto>> GetMaintenanceTaskTemplatesAsync(Guid maintenanceTypeId);
    Task<IEnumerable<MaintenanceTaskTemplateDto>> GetAllMaintenanceTaskTemplatesAsync();
    Task<MaintenanceTaskTemplateDto> CreateMaintenanceTaskTemplateAsync(CreateMaintenanceTaskTemplateDto createDto);
    Task<MaintenanceTaskTemplateDto> UpdateMaintenanceTaskTemplateAsync(Guid id, UpdateMaintenanceTaskTemplateDto updateDto);
    Task DeleteMaintenanceTaskTemplateAsync(Guid id);
}

// DTOs for task templates
public class AssetTaskTemplateDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public Guid MaintenanceTypeId { get; set; }
    public string MaintenanceTypeName { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public bool IsRequired { get; set; }
    public string? Instructions { get; set; }
    public string? SafetyRequirements { get; set; }
    public string? RequiredTools { get; set; }
    public string? RequiredParts { get; set; }
    public bool IsActive { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianName { get; set; }
}

public class CreateAssetTaskTemplateDto
{
    public Guid AssetId { get; set; }
    public Guid MaintenanceTypeId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? Instructions { get; set; }
    public string? SafetyRequirements { get; set; }
    public string? RequiredTools { get; set; }
    public string? RequiredParts { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? AssignedTechnicianId { get; set; }
}

public class UpdateAssetTaskTemplateDto
{
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public bool IsRequired { get; set; }
    public string? Instructions { get; set; }
    public string? SafetyRequirements { get; set; }
    public string? RequiredTools { get; set; }
    public string? RequiredParts { get; set; }
    public bool IsActive { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
}

public class AssetTypeTaskTemplateDto
{
    public Guid Id { get; set; }
    public Guid AssetTypeId { get; set; }
    public string AssetTypeName { get; set; } = string.Empty;
    public Guid MaintenanceTypeId { get; set; }
    public string MaintenanceTypeName { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public bool IsRequired { get; set; }
    public string? Instructions { get; set; }
    public string? SafetyRequirements { get; set; }
    public string? RequiredTools { get; set; }
    public string? RequiredParts { get; set; }
    public bool IsActive { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianName { get; set; }
}

public class CreateAssetTypeTaskTemplateDto
{
    public Guid AssetTypeId { get; set; }
    public Guid MaintenanceTypeId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? Instructions { get; set; }
    public string? SafetyRequirements { get; set; }
    public string? RequiredTools { get; set; }
    public string? RequiredParts { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? AssignedTechnicianId { get; set; }
}

public class UpdateAssetTypeTaskTemplateDto
{
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public bool IsRequired { get; set; }
    public string? Instructions { get; set; }
    public string? SafetyRequirements { get; set; }
    public string? RequiredTools { get; set; }
    public string? RequiredParts { get; set; }
    public bool IsActive { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
}

public class MaintenanceTaskTemplateDto
{
    public Guid Id { get; set; }
    public Guid MaintenanceTypeId { get; set; }
    public string MaintenanceTypeName { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public bool IsRequired { get; set; }
    public string? Instructions { get; set; }
    public string? SafetyRequirements { get; set; }
    public bool IsActive { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianName { get; set; }
}

public class CreateMaintenanceTaskTemplateDto
{
    public Guid MaintenanceTypeId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? Instructions { get; set; }
    public string? SafetyRequirements { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? AssignedTechnicianId { get; set; }
}

public class UpdateMaintenanceTaskTemplateDto
{
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public bool IsRequired { get; set; }
    public string? Instructions { get; set; }
    public string? SafetyRequirements { get; set; }
    public bool IsActive { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
}

// DTO to represent tasks for work order generation
public class WorkOrderTaskDto
{
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public bool IsRequired { get; set; }
    public string? Instructions { get; set; }
    public string? SafetyRequirements { get; set; }
    public string? RequiredTools { get; set; }
    public string? RequiredParts { get; set; }
    public string Source { get; set; } = string.Empty; // "Asset", "AssetType", or "MaintenanceType"
    public Guid? AssignedTechnicianId { get; set; } // Technician assigned to this task from template
}

#endregion

#region Priority Level Services

public interface IPriorityLevelService
{
    Task<PriorityLevelDto> CreatePriorityLevelAsync(CreatePriorityLevelDto createDto);
    Task<PriorityLevelDto> UpdatePriorityLevelAsync(Guid id, UpdatePriorityLevelDto updateDto);
    Task DeletePriorityLevelAsync(Guid id);
    Task<PriorityLevelDto?> GetPriorityLevelByIdAsync(Guid id);
    Task<IEnumerable<PriorityLevelDto>> GetAllPriorityLevelsAsync();
    Task<IEnumerable<PriorityLevelDto>> GetActivePriorityLevelsAsync();
    Task<PagedResult<PriorityLevelDto>> GetPriorityLevelsPagedAsync(PriorityLevelFilterDto filter);
    Task<PriorityLevelDto> TogglePriorityLevelStatusAsync(Guid id);
    Task<bool> IsPriorityLevelCodeUniqueAsync(string code, Guid? excludeId = null);
}

#endregion

#region Work Order Type Services

public interface IWorkOrderTypeService
{
    Task<WorkOrderTypeDto> CreateWorkOrderTypeAsync(CreateWorkOrderTypeDto createDto);
    Task<WorkOrderTypeDto> UpdateWorkOrderTypeAsync(Guid id, UpdateWorkOrderTypeDto updateDto);
    Task DeleteWorkOrderTypeAsync(Guid id);
    Task<WorkOrderTypeDto?> GetWorkOrderTypeByIdAsync(Guid id);
    Task<IEnumerable<WorkOrderTypeDto>> GetAllWorkOrderTypesAsync();
    Task<IEnumerable<WorkOrderTypeDto>> GetActiveWorkOrderTypesAsync();
    Task<IEnumerable<WorkOrderTypeDto>> GetWorkOrderTypesByCategoryAsync(string category);
    Task<PagedResult<WorkOrderTypeDto>> GetWorkOrderTypesPagedAsync(WorkOrderTypeFilterDto filter);
    Task<WorkOrderTypeDto> ToggleWorkOrderTypeStatusAsync(Guid id);
    Task<bool> IsWorkOrderTypeCodeUniqueAsync(string code, Guid? excludeId = null);
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
    Task<InvoiceDto> PostWorkOrderBillingToArInvoiceAsync(Guid id);

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

    // Task management
    Task<ErpSystem.Core.DTOs.Maintenance.WorkOrderTaskDto?> UpdateTaskStatusAsync(Guid taskId, string status, double? actualHours = null, string? completionNotes = null, Guid? technicianId = null);
    Task<ErpSystem.Core.DTOs.Maintenance.WorkOrderTaskDto?> UpdateTaskPhotoAsync(Guid taskId, string? photoPath);
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
    // Single operations
    Task<WorkOrderPartDto> AddPartAsync(CreateWorkOrderPartDto createDto);
    Task<WorkOrderPartDto> UpdatePartAsync(Guid id, UpdateWorkOrderPartDto updateDto);
    Task DeletePartAsync(Guid id);
    Task<IEnumerable<WorkOrderPartDto>> GetPartsByWorkOrderAsync(Guid workOrderId);
    Task<WorkOrderPartDto> UpdatePartStatusAsync(Guid id, string status, int? quantityUsed = null);
    Task<WorkOrderPartDto> ReturnUnusedPartsAsync(Guid partId);
    Task<decimal> GetTotalPartsCostAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderPartDto>> GetPartsRequiringOrderAsync();

    // Bulk operations
    Task<IEnumerable<WorkOrderPartDto>> AddPartsBulkAsync(IEnumerable<CreateWorkOrderPartDto> createDtos);
    Task DeletePartsBulkAsync(IEnumerable<Guid> ids);
}

public interface IWorkOrderToolService
{
    // Tool allocation
    Task<WorkOrderToolDto> AllocateToolAsync(AllocateWorkOrderToolDto allocateDto);
    Task<IEnumerable<WorkOrderToolDto>> GetToolsByWorkOrderAsync(Guid workOrderId);
    Task<WorkOrderToolSummaryDto> GetWorkOrderToolSummaryAsync(Guid workOrderId);
    Task RemoveToolAllocationAsync(Guid workOrderId, Guid toolId);
    Task<WorkOrderToolDto> ExcludeToolFromBillingAsync(Guid workOrderId, Guid toolId, string reason);

    // Bulk operations
    Task<IEnumerable<WorkOrderToolDto>> AllocateToolsBulkAsync(IEnumerable<AllocateWorkOrderToolDto> allocateDtos);
    Task RemoveToolAllocationsBulkAsync(IEnumerable<Guid> toolIds, Guid workOrderId);

    // Tool checkout/return for work orders
    Task<WorkOrderToolDto> CheckoutToolAsync(CheckoutWorkOrderToolDto checkoutDto);
    Task<WorkOrderToolDto> ReturnToolAsync(Guid workOrderId, Guid toolId, ReturnWorkOrderToolDto returnDto);

    // Query operations
    Task<IEnumerable<WorkOrderToolDto>> GetCheckedOutToolsForWorkOrderAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderToolDto>> GetOverdueToolsForWorkOrderAsync(Guid workOrderId);
    Task<decimal> GetTotalToolCostAsync(Guid workOrderId);
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

    // Additional methods needed by controllers
    Task<PagedResult<MaintenanceScheduleDto>> GetSchedulesPagedAsync(MaintenanceScheduleFilterDto filter);
    Task<IEnumerable<MaintenanceScheduleDto>> GetOverdueSchedulesAsync();
    Task<MaintenanceScheduleDto> ToggleScheduleStatusAsync(Guid id);

    // Notification and reminder methods
    Task SendScheduleReminderAsync(Guid scheduleId, bool force = false);
    Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesDueForRemindersAsync();
    Task<int> SendAdvanceRemindersAsync();

    // History methods
    Task<IEnumerable<MaintenanceScheduleHistoryDto>> GetScheduleHistoryAsync(Guid scheduleId);

    // Usage-based trigger evaluation
    Task<bool> EvaluateUsageTriggersAsync(Guid scheduleId);
    Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesDueByUsageAsync();
    Task UpdateAssetUsageAsync(Guid assetId, double? mileage, double? operatingHours);

    // Condition-based trigger evaluation
    Task<bool> EvaluateConditionTriggersAsync(Guid scheduleId);
    Task<IEnumerable<MaintenanceScheduleDto>> GetSchedulesDueByConditionAsync();

    // Multi-criteria evaluation
    Task<bool> ShouldGenerateWorkOrderAsync(Guid scheduleId);
    Task ProcessUsageBasedSchedulesAsync();
    Task ProcessConditionBasedSchedulesAsync();
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

#region Asset Admission & Discharge Services

public interface ILegacyAssetAdmissionService
{
    Task<PagedResult<AssetAdmissionDto>> GetAdmissionsAsync(AdmissionQueryParameters query);
    Task<AssetAdmissionDto?> GetAdmissionByIdAsync(Guid id);
    Task<AssetAdmissionDto> CreateAdmissionAsync(CreateAssetAdmissionDto dto);
    Task<AssetAdmissionDto> UpdateAdmissionAsync(Guid id, UpdateAssetAdmissionDto dto);
    Task CancelAdmissionAsync(Guid id, string? reason = null);
    Task<IEnumerable<AssetAdmissionDto>> GetActiveAdmissionsAsync();
    Task<IEnumerable<AssetAdmissionDto>> GetAdmissionsByAssetAsync(Guid assetId);
    Task<IEnumerable<AssetAdmissionDto>> GetAdmissionsByWorkOrderAsync(Guid workOrderId);
    Task<AdmissionStatsDto> GetAdmissionStatsAsync(AdmissionStatsQuery query);
    Task<DowntimeSummaryDto> GetAdmissionDowntimeReportAsync(DowntimeReportQuery query);
}

public interface ILegacyAssetDischargeService
{
    Task<PagedResult<AssetDischargeDto>> GetDischargesAsync(DischargeQueryParameters query);
    Task<AssetDischargeDto?> GetDischargeByIdAsync(Guid id);
    Task<IEnumerable<AssetDischargeDto>> GetDischargesByAdmissionAsync(Guid admissionId);
    Task<AssetDischargeDto> CreateDischargeAsync(CreateAssetDischargeDto dto);
    Task<AssetDischargeDto> UpdateDischargeAsync(Guid id, UpdateAssetDischargeDto dto);
    Task<MaintenanceCertificateDto> GenerateCompletionCertificateAsync(Guid dischargeId);
}

#endregion


#region Technician Services

public interface ITechnicianService
{
    Task<TechnicianDto> CreateTechnicianAsync(CreateTechnicianDto createDto);
    Task<TechnicianDto> UpdateTechnicianAsync(Guid id, UpdateTechnicianDto updateDto);
    Task DeleteTechnicianAsync(Guid id);
    Task<TechnicianDto?> GetTechnicianByIdAsync(Guid id);
    Task<IEnumerable<TechnicianDto>> GetAllTechniciansAsync();
    Task<IEnumerable<TechnicianDto>> GetActiveTechniciansAsync();
    Task<PagedResult<TechnicianDto>> GetTechniciansPagedAsync(TechnicianFilterDto filter);
    Task<IEnumerable<TechnicianDto>> GetTechniciansBySkillAsync(Guid skillId);
    Task<IEnumerable<TechnicianDto>> GetAvailableTechniciansAsync(DateTime startTime, DateTime endTime);
    Task<TechnicianDto?> GetTechnicianWithSkillsAsync(Guid technicianId);
    Task<bool> IsTechnicianAvailableAsync(Guid technicianId, DateTime startTime, DateTime endTime);
    Task<IEnumerable<TechnicianDto>> GetTechniciansByTeamAsync(Guid teamId);
    Task<TechnicianWorkloadDto> GetTechnicianWorkloadAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<TechnicianAvailabilityDto> GetTechnicianAvailabilityAsync(Guid technicianId, DateTime date);
    Task<IEnumerable<TechnicianDto>> GetTechniciansByLocationAsync(Guid locationId);
    Task<TechnicianAnalyticsDto> GetTechnicianAnalyticsAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<SkillUtilizationDto> GetSkillUtilizationAsync(Guid skillId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<TechnicianDto>> FindTechniciansForWorkOrderAsync(Guid workOrderId);
}

public interface ITechnicalSkillService
{
    Task<TechnicalSkillDto> CreateSkillAsync(CreateTechnicalSkillDto createDto);
    Task<TechnicalSkillDto> UpdateSkillAsync(Guid id, UpdateTechnicalSkillDto updateDto);
    Task DeleteSkillAsync(Guid id);
    Task<TechnicalSkillDto?> GetSkillByIdAsync(Guid id);
    Task<IEnumerable<TechnicalSkillDto>> GetAllSkillsAsync();
    Task<IEnumerable<TechnicalSkillDto>> GetActiveSkillsAsync();
    Task<PagedResult<TechnicalSkillDto>> GetSkillsPagedAsync(TechnicalSkillFilterDto filter);
    Task<IEnumerable<TechnicalSkillDto>> GetSkillsByCategoryAsync(string category);
    Task<IEnumerable<TechnicalSkillDto>> GetSkillsByComplexityAsync(string complexity);
    Task<IEnumerable<TechnicalSkillDto>> GetSkillsByRiskLevelAsync(string riskLevel);
    Task<bool> IsSkillNameUniqueAsync(string name, Guid? excludeId = null);
    Task<SkillGapAnalysisDto> GetSkillGapAnalysisAsync();
    Task<IEnumerable<TechnicalSkillDto>> GetRecommendedSkillsAsync(Guid technicianId);

    // Additional methods needed by controllers
    Task<IEnumerable<TechnicalSkillDto>> SyncSkillsFromHRAsync();
    Task<IEnumerable<TechnicianDto>> GetTechniciansWithSkillAsync(Guid skillId);
    Task<TechnicianSkillAssignmentDto> AssignSkillToTechnicianAsync(CreateTechnicianSkillAssignmentDto createDto);
    Task<IEnumerable<TechnicianSkillAssignmentDto>> GetTechnicianSkillsAsync(Guid technicianId);
}

public interface ISafetyProtocolService
{
    Task<SafetyProtocolDto> CreateProtocolAsync(CreateSafetyProtocolDto createDto);
    Task<SafetyProtocolDto> UpdateProtocolAsync(Guid id, UpdateSafetyProtocolDto updateDto);
    Task DeleteProtocolAsync(Guid id);
    Task<SafetyProtocolDto?> GetProtocolByIdAsync(Guid id);
    Task<IEnumerable<SafetyProtocolDto>> GetAllProtocolsAsync();
    Task<IEnumerable<SafetyProtocolDto>> GetActiveProtocolsAsync();
    Task<PagedResult<SafetyProtocolDto>> GetProtocolsPagedAsync(SafetyProtocolFilterDto filter);
    Task<IEnumerable<SafetyProtocolDto>> GetProtocolsByCategoryAsync(string category);
    Task<IEnumerable<SafetyProtocolDto>> GetMandatoryProtocolsAsync();
    Task<IEnumerable<SafetyProtocolDto>> GetProtocolsBySeverityAsync(string severity);
    Task<IEnumerable<SafetyProtocolDto>> GetProtocolsByRegulatoryStandardAsync(string standard);
    Task<IEnumerable<SafetyProtocolDto>> GetProtocolsDueForReviewAsync();
    Task<IEnumerable<SafetyProtocolDto>> GetOverdueProtocolsAsync();

    // Approval workflow
    Task SubmitForApprovalAsync(Guid protocolId);
    Task ApproveProtocolAsync(Guid protocolId, ApprovalRequestDto approvalRequest);
    Task RejectProtocolAsync(Guid protocolId, ApprovalRequestDto approvalRequest);
    Task RequestChangesAsync(Guid protocolId, ApprovalRequestDto approvalRequest);

    // Compliance tracking
    Task RecordAdherenceAsync(CreateProtocolAdherenceDto createDto);
    Task RecordViolationAsync(CreateProtocolViolationDto createDto);
    Task<IEnumerable<ProtocolAdherenceDto>> GetAdherenceHistoryAsync(Guid protocolId);
    Task<IEnumerable<ProtocolViolationDto>> GetViolationHistoryAsync(Guid protocolId);

    // Training tracking
    Task RecordTrainingAsync(CreateProtocolTrainingDto createDto);
    Task<IEnumerable<ProtocolTrainingDto>> GetProtocolTrainingHistoryAsync(Guid protocolId);
    Task<IEnumerable<ProtocolTrainingDto>> GetTechnicianTrainingHistoryAsync(Guid technicianId);

    // Reporting
    Task<ComplianceReportDto> GetComplianceReportAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<SafetyProtocolComplianceDto>> GetProtocolComplianceAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<CategoryComplianceDto>> GetCategoryComplianceAsync();
    Task<SafetyAnalyticsDto> GetSafetyAnalyticsAsync(DateTime startDate, DateTime endDate);
}

#endregion

#region Usage Tracking Service

public interface IAssetUsageTrackingService
{
    // CRUD operations
    Task<AssetUsageTrackingDto> CreateUsageRecordAsync(CreateAssetUsageTrackingDto createDto);
    Task<IEnumerable<AssetUsageTrackingDto>> BulkCreateUsageRecordsAsync(BulkUsageImportDto bulkDto);
    Task<AssetUsageTrackingDto?> GetUsageRecordByIdAsync(Guid id);
    Task<IEnumerable<AssetUsageTrackingDto>> GetUsageRecordsAsync(Guid assetId, DateTime? startDate = null, DateTime? endDate = null);
    Task<PagedResult<AssetUsageTrackingDto>> GetUsageRecordsPagedAsync(Guid assetId, int page, int pageSize, DateTime? startDate = null, DateTime? endDate = null);
    Task DeleteUsageRecordAsync(Guid id);

    // Usage analytics
    Task<AssetUsageSummaryDto> GetAssetUsageSummaryAsync(Guid assetId);
    Task<AssetUsageSummaryDto> GetAssetUsageSummaryAsync(Guid assetId, Guid? tenantId);
    Task<IEnumerable<AssetUsageSummaryDto>> GetAllAssetUsageSummariesAsync();
    Task<decimal?> GetCurrentMileageAsync(Guid assetId);
    Task<decimal?> GetCurrentOperatingHoursAsync(Guid assetId);
    Task<int?> GetCurrentCyclesAsync(Guid assetId);
    Task<decimal?> GetAverageDailyMileageAsync(Guid assetId, int days = 30);
    Task<decimal?> GetAverageDailyHoursAsync(Guid assetId, int days = 30);

    // Integration helpers
    Task<bool> ImportUsageDataFromSourceAsync(string dataSource, string externalData);
    Task<IEnumerable<AssetUsageTrackingDto>> GetUnvalidatedRecordsAsync();
    Task ValidateUsageRecordAsync(Guid id);
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

#region Maintenance Type Services

public interface IMaintenanceTypeService
{
    Task<MaintenanceTypeDto> CreateMaintenanceTypeAsync(CreateMaintenanceTypeDto createDto);
    Task<MaintenanceTypeDto> UpdateMaintenanceTypeAsync(Guid id, UpdateMaintenanceTypeDto updateDto);
    Task DeleteMaintenanceTypeAsync(Guid id);
    Task<MaintenanceTypeDto?> GetMaintenanceTypeByIdAsync(Guid id);
    Task<IEnumerable<MaintenanceTypeDto>> GetAllMaintenanceTypesAsync();
    Task<IEnumerable<MaintenanceTypeDto>> GetActiveMaintenanceTypesAsync();
    Task<PagedResult<MaintenanceTypeDto>> GetMaintenanceTypesPagedAsync(MaintenanceTypeFilterDto filter);
    Task<IEnumerable<MaintenanceTypeDto>> GetMaintenanceTypesByCategoryAsync(string category);
    Task<bool> IsMaintenanceTypeCodeUniqueAsync(string code, Guid? excludeId = null);
    Task<bool> IsMaintenanceTypeNameUniqueAsync(string name, Guid? excludeId = null);
    Task<MaintenanceTypeDto> ToggleMaintenanceTypeStatusAsync(Guid id);
    Task<IEnumerable<MaintenanceTypeDto>> GetMaintenanceTypesForAssetCategoryAsync(Guid assetCategoryId);
    Task<IEnumerable<string>> GetMaintenanceTypeCategoriesAsync();
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

#region Maintenance Schedule Services

// IMaintenanceScheduleService is defined earlier in the file

#endregion

#region Technical Skill Services

// ITechnicalSkillService - duplicate removed, using definition from above

#endregion

#region Safety Protocol Services

// ISafetyProtocolService - duplicate removed, using definition from above

#endregion

#region Technician Data Services (HR Integration)

public interface ITechnicianDataService
{
    // Read-only technician data from HR module
    Task<TechnicianDto?> GetTechnicianByIdAsync(Guid id);
    Task<IEnumerable<TechnicianDto>> GetAllTechniciansAsync();
    Task<PagedResult<TechnicianListDto>> GetTechniciansPagedAsync(TechnicianFilterDto filter);

    // HR Module synchronization
    Task<IEnumerable<TechnicianDto>> SyncTechniciansFromHRAsync();
    Task<TechnicianDto?> GetTechnicianFromHRAsync(Guid hrEmployeeId);
    Task UpdateTechnicianFromHRAsync(TechnicianDto hrTechnician);

    // Availability and workload
    Task<IEnumerable<TechnicianDto>> GetAvailableTechniciansAsync();
    Task<IEnumerable<TechnicianDto>> GetTechniciansByLocationAsync(string location);
    Task<IEnumerable<TechnicianDto>> GetTechniciansByDepartmentAsync(string department);
    Task<IEnumerable<TechnicianDto>> GetTechniciansBySkillAsync(Guid skillId);
    Task<IEnumerable<TechnicianDto>> GetTechniciansByExperienceLevelAsync(string experienceLevel);

    // Workload and performance
    Task<TechnicianWorkloadDto> GetTechnicianWorkloadAsync(Guid technicianId);
    Task<IEnumerable<TechnicianWorkloadDto>> GetTeamWorkloadAsync();
    Task<TechnicianPerformanceDto> GetTechnicianPerformanceAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<TechnicianAvailabilityDto> GetTechnicianAvailabilityAsync(Guid technicianId);

    // Statistics and analytics
    Task<TechnicianStatsDto> GetTechnicianStatsAsync();
    Task<IEnumerable<TechnicianDto>> GetTechniciansWithExpiringCertificationsAsync(int days = 30);
    Task<IEnumerable<TechnicianDto>> GetTechniciansWithExpiredCertificationsAsync();
}

#endregion
