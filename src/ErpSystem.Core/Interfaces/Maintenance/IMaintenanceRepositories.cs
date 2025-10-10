using ErpSystem.Core.Entities;
using System.Linq.Expressions;
using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

#region Core Asset Management Repositories

public interface IMaintenanceAssetRepository : IGenericRepository<MaintenanceAsset>
{
    Task<IEnumerable<MaintenanceAsset>> GetByAssetCategoryIdAsync(Guid categoryId);
    Task<IEnumerable<MaintenanceAsset>> GetByParentAssetIdAsync(Guid parentAssetId);
    Task<IEnumerable<MaintenanceAsset>> GetByStatusAsync(string status);
    Task<IEnumerable<MaintenanceAsset>> GetByCriticalityAsync(string criticality);
    Task<IEnumerable<MaintenanceAsset>> GetAssetsWithActiveWorkOrdersAsync();
    Task<IEnumerable<MaintenanceAsset>> GetAssetsRequiringMaintenanceAsync();
    Task<MaintenanceAsset?> GetByAssetNumberAsync(string assetNumber);
    Task<bool> IsAssetNumberUniqueAsync(string assetNumber, Guid? excludeId = null);
    Task<IEnumerable<MaintenanceAsset>> SearchAssetsAsync(string searchTerm);
    Task<IEnumerable<MaintenanceAsset>> GetAssetHierarchyAsync(Guid rootAssetId);
    Task<double> GetTotalAssetValueAsync();
    Task<double> GetAssetValueByCategoryAsync(Guid categoryId);
}

public interface IMaintenanceAssetCategoryRepository : IGenericRepository<MaintenanceAssetCategory>
{
    Task<MaintenanceAssetCategory?> GetByCodeAsync(string code);
    Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null);
    Task<IEnumerable<MaintenanceAssetCategory>> GetActiveAsync();
    Task<int> GetAssetCountByCategoryAsync(Guid categoryId);
}

#endregion

#region Work Order Management Repositories

public interface IWorkOrderRepository : IGenericRepository<WorkOrder>
{
    Task<WorkOrder?> GetByWorkOrderNumberAsync(string workOrderNumber);
    Task<bool> IsWorkOrderNumberUniqueAsync(string workOrderNumber, Guid? excludeId = null);
    Task<IEnumerable<WorkOrder>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<WorkOrder>> GetByStatusAsync(string status);
    Task<IEnumerable<WorkOrder>> GetByPriorityLevelAsync(Guid priorityLevelId);
    Task<IEnumerable<WorkOrder>> GetByAssignedTechnicianAsync(Guid technicianId);
    Task<IEnumerable<WorkOrder>> GetByAssignedTeamAsync(Guid teamId);
    Task<IEnumerable<WorkOrder>> GetOverdueWorkOrdersAsync();
    Task<IEnumerable<WorkOrder>> GetWorkOrdersDueInDaysAsync(int days);
    Task<IEnumerable<WorkOrder>> GetByMaintenanceScheduleIdAsync(Guid scheduleId);
    Task<IEnumerable<WorkOrder>> GetChildWorkOrdersAsync(Guid parentWorkOrderId);
    Task<IEnumerable<WorkOrder>> GetWorkOrdersByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<string> GenerateWorkOrderNumberAsync();
    Task<decimal> GetTotalCostByAssetAsync(Guid assetId);
    Task<decimal> GetTotalCostByPeriodAsync(DateTime startDate, DateTime endDate);
    Task<double> GetTotalLaborHoursByTechnicianAsync(Guid technicianId, DateTime startDate, DateTime endDate);
}

public interface IWorkOrderTypeRepository : IGenericRepository<WorkOrderType>
{
    Task<WorkOrderType?> GetByCodeAsync(string code);
    Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null);
    Task<IEnumerable<WorkOrderType>> GetActiveAsync();
    Task<int> GetWorkOrderCountByTypeAsync(Guid workOrderTypeId);
}

public interface IMaintenanceTypeRepository : IGenericRepository<MaintenanceType>
{
    Task<MaintenanceType?> GetByCodeAsync(string code);
    Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null);
    Task<IEnumerable<MaintenanceType>> GetActiveAsync();
    Task<IEnumerable<MaintenanceType>> GetByCategoryAsync(string category);
    Task<int> GetWorkOrderCountByMaintenanceTypeAsync(Guid maintenanceTypeId);
}

public interface IPriorityLevelRepository : IGenericRepository<PriorityLevel>
{
    Task<IEnumerable<PriorityLevel>> GetActiveAsync();
    Task<IEnumerable<PriorityLevel>> GetOrderedByLevelAsync();
    Task<PriorityLevel?> GetByLevelAsync(int level);
    Task<bool> IsLevelUniqueAsync(int level, Guid? excludeId = null);
    Task<int> GetWorkOrderCountByPriorityAsync(Guid priorityLevelId);
}

#endregion

#region Work Order Detail Repositories

public interface IWorkOrderTaskRepository : IGenericRepository<WorkOrderTask>
{
    Task<IEnumerable<WorkOrderTask>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderTask>> GetByAssignedTechnicianAsync(Guid technicianId);
    Task<IEnumerable<WorkOrderTask>> GetByStatusAsync(string status);
    Task<IEnumerable<WorkOrderTask>> GetOverdueTasksAsync();
    Task<double> GetTotalHoursByWorkOrderAsync(Guid workOrderId);
    Task<double> GetCompletionPercentageAsync(Guid workOrderId);
}

public interface IWorkOrderPartRepository : IGenericRepository<WorkOrderPart>
{
    Task<IEnumerable<WorkOrderPart>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderPart>> GetByPartNumberAsync(string partNumber);
    Task<IEnumerable<WorkOrderPart>> GetByStatusAsync(string status);
    Task<decimal> GetTotalCostByWorkOrderAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderPart>> GetPartsRequiringOrderAsync();
}

public interface IWorkOrderLaborRepository : IGenericRepository<WorkOrderLabor>
{
    Task<IEnumerable<WorkOrderLabor>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderLabor>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<WorkOrderLabor>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<decimal> GetTotalCostByWorkOrderAsync(Guid workOrderId);
    Task<double> GetTotalHoursByTechnicianAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<decimal> GetTotalLaborCostByTechnicianAsync(Guid technicianId, DateTime startDate, DateTime endDate);
}

public interface IWorkOrderDocumentRepository : IGenericRepository<WorkOrderDocument>
{
    Task<IEnumerable<WorkOrderDocument>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderDocument>> GetByDocumentTypeAsync(string documentType);
    Task<IEnumerable<WorkOrderDocument>> GetByUploadedByAsync(Guid userId);
    Task<long> GetTotalFileSizeByWorkOrderAsync(Guid workOrderId);
}

public interface IWorkOrderCommentRepository : IGenericRepository<WorkOrderComment>
{
    Task<IEnumerable<WorkOrderComment>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderComment>> GetByCommentTypeAsync(string commentType);
    Task<IEnumerable<WorkOrderComment>> GetInternalCommentsAsync(Guid workOrderId);
    Task<IEnumerable<WorkOrderComment>> GetExternalCommentsAsync(Guid workOrderId);
}

#endregion

#region Maintenance Scheduling Repositories

public interface IMaintenanceScheduleRepository : IGenericRepository<MaintenanceSchedule>
{
    Task<IEnumerable<MaintenanceSchedule>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<MaintenanceSchedule>> GetActiveSchedulesAsync();
    Task<IEnumerable<MaintenanceSchedule>> GetSchedulesDueForGenerationAsync();
    Task<IEnumerable<MaintenanceSchedule>> GetByScheduleTypeAsync(string scheduleType);
    Task<IEnumerable<MaintenanceSchedule>> GetByFrequencyAsync(string frequency);
    Task<IEnumerable<MaintenanceSchedule>> GetByMaintenanceTypeAsync(Guid maintenanceTypeId);
    Task<IEnumerable<MaintenanceSchedule>> GetSchedulesDueInDaysAsync(int days);
    Task UpdateNextDueDateAsync(Guid scheduleId, DateTime nextDueDate);
    Task UpdateLastGeneratedDateAsync(Guid scheduleId, DateTime lastGeneratedDate);
}

#endregion

#region Inspection Management Repositories

public interface IInspectionTemplateRepository : IGenericRepository<InspectionTemplate>
{
    Task<IEnumerable<InspectionTemplate>> GetActiveAsync();
    Task<IEnumerable<InspectionTemplate>> GetByCategoryAsync(string category);
    Task<IEnumerable<InspectionTemplate>> GetByInspectionTypeAsync(string inspectionType);
    Task<int> GetInspectionCountByTemplateAsync(Guid templateId);
}

public interface IAssetInspectionRepository : IGenericRepository<AssetInspection>
{
    Task<IEnumerable<AssetInspection>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetInspection>> GetByInspectorIdAsync(Guid inspectorId);
    Task<IEnumerable<AssetInspection>> GetByStatusAsync(string status);
    Task<IEnumerable<AssetInspection>> GetByOverallResultAsync(string result);
    Task<IEnumerable<AssetInspection>> GetInspectionsDueAsync();
    Task<IEnumerable<AssetInspection>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<AssetInspection>> GetRegulatoryInspectionsAsync();
    Task<AssetInspection?> GetLatestInspectionByAssetAsync(Guid assetId);
    Task<IEnumerable<AssetInspection>> GetInspectionHistoryByAssetAsync(Guid assetId);
}

public interface IInspectionDocumentRepository : IGenericRepository<InspectionDocument>
{
    Task<IEnumerable<InspectionDocument>> GetByInspectionIdAsync(Guid inspectionId);
    Task<IEnumerable<InspectionDocument>> GetByDocumentTypeAsync(string documentType);
    Task<long> GetTotalFileSizeByInspectionAsync(Guid inspectionId);
}

#endregion

#region Resource Management Repositories

public interface ITechnicianTeamRepository : IGenericRepository<TechnicianTeam>
{
    Task<IEnumerable<TechnicianTeam>> GetActiveAsync();
    Task<IEnumerable<TechnicianTeam>> GetByStatusAsync(string status);
    Task<IEnumerable<TechnicianTeam>> GetTeamsByLeaderAsync(Guid leaderId);
    Task<int> GetMemberCountAsync(Guid teamId);
    Task<int> GetActiveMemberCountAsync(Guid teamId);
}

public interface ITechnicianTeamMemberRepository : IGenericRepository<TechnicianTeamMember>
{
    Task<IEnumerable<TechnicianTeamMember>> GetByTeamIdAsync(Guid teamId);
    Task<IEnumerable<TechnicianTeamMember>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<TechnicianTeamMember>> GetActiveByTeamIdAsync(Guid teamId);
    Task<IEnumerable<TechnicianTeamMember>> GetByRoleAsync(string role);
    Task<bool> IsTechnicianInTeamAsync(Guid technicianId, Guid teamId);
    Task<TechnicianTeamMember?> GetActiveMembershipAsync(Guid technicianId, Guid teamId);
}

public interface ITechnicianSkillRepository : IGenericRepository<TechnicianSkill>
{
    Task<IEnumerable<TechnicianSkill>> GetActiveAsync();
    Task<IEnumerable<TechnicianSkill>> GetByCategoryAsync(string category);
    Task<int> GetUserCountBySkillAsync(Guid skillId);
}

public interface IUserTechnicianSkillRepository : IGenericRepository<UserTechnicianSkill>
{
    Task<IEnumerable<UserTechnicianSkill>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<UserTechnicianSkill>> GetBySkillIdAsync(Guid skillId);
    Task<IEnumerable<UserTechnicianSkill>> GetByProficiencyLevelAsync(int proficiencyLevel);
    Task<IEnumerable<UserTechnicianSkill>> GetExpiredCertificationsAsync();
    Task<IEnumerable<UserTechnicianSkill>> GetCertificationsExpiringInDaysAsync(int days);
    Task<UserTechnicianSkill?> GetUserSkillAsync(Guid userId, Guid skillId);
    Task<bool> HasUserSkillAsync(Guid userId, Guid skillId);
    Task<IEnumerable<ApplicationUser>> GetUsersBySkillAsync(Guid skillId, int? minProficiencyLevel = null);
}

#endregion

#region Asset Downtime Repositories

public interface IAssetDowntimeRepository : IGenericRepository<AssetDowntime>
{
    Task<IEnumerable<AssetDowntime>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetDowntime>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<AssetDowntime>> GetByStatusAsync(string status);
    Task<IEnumerable<AssetDowntime>> GetActiveDowntimeAsync();
    Task<IEnumerable<AssetDowntime>> GetByReasonAsync(string reason);
    Task<IEnumerable<AssetDowntime>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<double> GetTotalDowntimeHoursByAssetAsync(Guid assetId, DateTime startDate, DateTime endDate);
    Task<decimal> GetTotalCostImpactByAssetAsync(Guid assetId, DateTime startDate, DateTime endDate);
    Task<AssetDowntime?> GetActiveDowntimeByAssetAsync(Guid assetId);
    Task<double> GetDowntimePercentageByAssetAsync(Guid assetId, DateTime startDate, DateTime endDate);
}

#endregion
