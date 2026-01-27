using System.Linq.Expressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
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
    Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null);

    // Notification and reminder tracking
    Task<IEnumerable<MaintenanceSchedule>> GetSchedulesDueForRemindersAsync(int advanceDays);
    Task UpdateLastReminderSentDateAsync(Guid scheduleId, DateTime date);

    // Usage-based trigger methods
    Task<IEnumerable<MaintenanceSchedule>> GetSchedulesByUsageTriggersAsync();
    Task UpdateLastUsageCheckDateAsync(Guid scheduleId, DateTime date);

    // Condition-based trigger methods
    Task<IEnumerable<MaintenanceSchedule>> GetSchedulesByConditionTriggersAsync();
    Task UpdateLastConditionCheckDateAsync(Guid scheduleId, DateTime date);
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

#region Task Template Management Repositories

public interface IAssetTaskTemplateRepository : IGenericRepository<AssetTaskTemplate>
{
    Task<IEnumerable<AssetTaskTemplate>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetTaskTemplate>> GetByAssetIdAndMaintenanceTypeAsync(Guid assetId, Guid maintenanceTypeId);
    Task<IEnumerable<AssetTaskTemplate>> GetByMaintenanceTypeIdAsync(Guid maintenanceTypeId);
    Task<IEnumerable<AssetTaskTemplate>> GetActiveByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetTaskTemplate>> GetOrderedBySequenceAsync(Guid assetId, Guid maintenanceTypeId);
}

public interface IAssetTypeTaskTemplateRepository : IGenericRepository<AssetTypeTaskTemplate>
{
    Task<IEnumerable<AssetTypeTaskTemplate>> GetByAssetTypeIdAsync(Guid assetTypeId);
    Task<IEnumerable<AssetTypeTaskTemplate>> GetByAssetTypeIdAndMaintenanceTypeAsync(Guid assetTypeId, Guid maintenanceTypeId);
    Task<IEnumerable<AssetTypeTaskTemplate>> GetByMaintenanceTypeIdAsync(Guid maintenanceTypeId);
    Task<IEnumerable<AssetTypeTaskTemplate>> GetActiveByAssetTypeIdAsync(Guid assetTypeId);
    Task<IEnumerable<AssetTypeTaskTemplate>> GetOrderedBySequenceAsync(Guid assetTypeId, Guid maintenanceTypeId);
}

public interface IMaintenanceTaskTemplateRepository : IGenericRepository<MaintenanceTaskTemplate>
{
    Task<IEnumerable<MaintenanceTaskTemplate>> GetByMaintenanceTypeIdAsync(Guid maintenanceTypeId);
    Task<IEnumerable<MaintenanceTaskTemplate>> GetActiveByMaintenanceTypeIdAsync(Guid maintenanceTypeId);
    Task<IEnumerable<MaintenanceTaskTemplate>> GetOrderedBySequenceAsync(Guid maintenanceTypeId);
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

public interface ITechnicalSkillRepository : IGenericRepository<Skill>
{
    Task<IEnumerable<Skill>> GetActiveAsync();
    Task<IEnumerable<Skill>> GetByCategoryAsync(string category);
    Task<int> GetTechnicianCountBySkillAsync(Guid skillId);
    Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null);
    Task<Skill?> GetByNameAsync(string name);
}

public interface ITechnicianSkillAssignmentRepository : IGenericRepository<TechnicianSkillAssignment>
{
    Task<IEnumerable<TechnicianSkillAssignment>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<TechnicianSkillAssignment>> GetByTechnicalSkillIdAsync(Guid skillId);
    Task<IEnumerable<TechnicianSkillAssignment>> GetByProficiencyLevelAsync(int proficiencyLevel);
    Task<IEnumerable<TechnicianSkillAssignment>> GetExpiredCertificationsAsync();
    Task<IEnumerable<TechnicianSkillAssignment>> GetCertificationsExpiringInDaysAsync(int days);
    Task<TechnicianSkillAssignment?> GetTechnicianSkillAsync(Guid technicianId, Guid skillId);
    Task<bool> HasTechnicianSkillAsync(Guid technicianId, Guid skillId);
    Task<IEnumerable<Employee>> GetTechniciansBySkillAsync(Guid skillId, int? minProficiencyLevel = null);
    Task<IEnumerable<TechnicianSkillAssignment>> GetBySkillIdAsync(Guid skillId);
}

public interface ITechnicianCertificationRepository : IGenericRepository<TechnicianCertification>
{
    Task<IEnumerable<TechnicianCertification>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<TechnicianCertification>> GetExpiredCertificationsAsync();
    Task<IEnumerable<TechnicianCertification>> GetExpiringSoonAsync(int daysAhead = 30);
    Task<IEnumerable<TechnicianCertification>> GetByCategoryAsync(string category);
    Task<IEnumerable<TechnicianCertification>> GetByStatusAsync(string status);
    Task<IEnumerable<TechnicianCertification>> GetMandatoryCertificationsAsync();
    Task<TechnicianCertification?> GetByCertificationNumberAsync(string certificationNumber);
    Task<bool> IsCertificationNumberUniqueAsync(string certificationNumber, Guid? excludeId = null);
    Task<IEnumerable<TechnicianCertification>> GetByIssuingOrganizationAsync(string organization);
    Task<IEnumerable<TechnicianCertification>> GetUnverifiedCertificationsAsync();
    Task<IEnumerable<TechnicianCertification>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<int> GetCertificationCountByTechnicianAsync(Guid technicianId);
    Task<int> GetExpiredCountByTechnicianAsync(Guid technicianId);
}

public interface ISafetyProtocolRepository : IGenericRepository<SafetyProtocol>
{
    Task<IEnumerable<SafetyProtocol>> GetActiveAsync();
    Task<IEnumerable<SafetyProtocol>> GetByCategoryAsync(string category);
    Task<IEnumerable<SafetyProtocol>> GetBySeverityAsync(string severity);
    Task<IEnumerable<SafetyProtocol>> GetByRegulatoryStandardAsync(string standard);
    Task<IEnumerable<SafetyProtocol>> GetMandatoryProtocolsAsync();
    Task<IEnumerable<SafetyProtocol>> GetProtocolsDueForReviewAsync();
    Task<IEnumerable<SafetyProtocol>> GetOverdueProtocolsAsync();
    Task<bool> IsProtocolNameUniqueAsync(string name, Guid? excludeId = null);
    Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null);
    Task<IEnumerable<SafetyProtocol>> GetByRiskLevelAsync(string riskLevel);
    Task<IEnumerable<SafetyProtocol>> GetMandatoryAsync();
    Task<IEnumerable<SafetyProtocol>> GetOverdueAsync();
    Task<IEnumerable<SafetyProtocol>> GetDueForReviewAsync();
    Task<IEnumerable<SafetyProtocol>> GetExpiredAsync();
}

public interface ISafetyComplianceRepository : IGenericRepository<SafetyComplianceRecord>
{
    Task<IEnumerable<SafetyComplianceRecord>> GetByProtocolIdAsync(Guid protocolId);
    Task<IEnumerable<SafetyComplianceRecord>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<SafetyComplianceRecord>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<SafetyComplianceRecord>> GetByComplianceStatusAsync(string status);
    Task<IEnumerable<SafetyComplianceRecord>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<SafetyComplianceRecord?> GetLatestComplianceAsync(Guid protocolId, Guid technicianId);
    Task<decimal> GetComplianceRateAsync(Guid protocolId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<SafetyComplianceRecord>> GetViolationsAsync(DateTime? startDate = null, DateTime? endDate = null);
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

#region Technician Repositories

public interface ITechnicianRepository : IGenericRepository<Employee>
{
    Task<IEnumerable<Employee>> GetTechniciansAsync();
    Task<IEnumerable<Employee>> GetActiveTechniciansAsync();
    Task<IEnumerable<Employee>> GetTechniciansBySkillAsync(Guid skillId);
    Task<IEnumerable<Employee>> GetAvailableTechniciansAsync(DateTime startTime, DateTime endTime);
    Task<Employee?> GetTechnicianWithSkillsAsync(Guid technicianId);
    Task<bool> IsTechnicianAvailableAsync(Guid technicianId, DateTime startTime, DateTime endTime);
    Task<IEnumerable<Employee>> GetTechniciansByTeamAsync(Guid teamId);
    Task<double> GetTechnicianWorkloadAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<Employee>> GetTechniciansByLocationAsync(Guid locationId);
    Task<IEnumerable<Employee>> GetActiveAsync();
    Task<IEnumerable<Employee>> GetByDepartmentAsync(string department);
    Task<IEnumerable<Employee>> GetBySpecializationAsync(string specialization);
    Task<IEnumerable<Employee>> GetAvailableAsync(DateTime startTime, DateTime endTime);
    Task<IEnumerable<Employee>> SyncFromHRAsync();
    Task<IEnumerable<Employee>> GetFromHRModuleAsync();
    Task<Employee?> GetByEmployeeIdAsync(Guid employeeId);
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

#region Technical Skill Repositories

// Duplicate repositories removed - using definitions from above

public interface ISkillAssessmentRepository : IGenericRepository<SkillAssessment>
{
    Task<IEnumerable<SkillAssessment>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<SkillAssessment>> GetBySkillIdAsync(Guid skillId);
    Task<IEnumerable<SkillAssessment>> GetByAssessorIdAsync(Guid assessorId);
    Task<IEnumerable<SkillAssessment>> GetByAssessmentTypeAsync(string assessmentType);
    Task<IEnumerable<SkillAssessment>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<SkillAssessment?> GetLatestAssessmentAsync(Guid technicianId, Guid skillId);
    Task<IEnumerable<SkillAssessment>> GetPassedAssessmentsAsync();
    Task<IEnumerable<SkillAssessment>> GetFailedAssessmentsAsync();
    Task<decimal> GetAverageScoreAsync(Guid skillId);
    Task<decimal> GetTechnicianAverageScoreAsync(Guid technicianId);
}

public interface ISkillGapAnalysisRepository : IGenericRepository<SkillGapAnalysis>
{
    Task<IEnumerable<SkillGapAnalysis>> GetBySkillIdAsync(Guid skillId);
    Task<IEnumerable<SkillGapAnalysis>> GetByImpactLevelAsync(string impactLevel);
    Task<IEnumerable<SkillGapAnalysis>> GetByStatusAsync(string status);
    Task<IEnumerable<SkillGapAnalysis>> GetByAnalysisPeriodAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<SkillGapAnalysis>> GetSkillDeficitsAsync();
    Task<IEnumerable<SkillGapAnalysis>> GetSkillSurplusAsync();
    Task<IEnumerable<SkillGapAnalysis>> GetCriticalGapsAsync();
    Task<SkillGapAnalysis?> GetLatestAnalysisAsync(Guid skillId);
}

#endregion

#region Safety Protocol Repositories

// Using ISafetyProtocolRepository definition from above

public interface IProtocolAdherenceRepository : IGenericRepository<ProtocolAdherence>
{
    Task<IEnumerable<ProtocolAdherence>> GetByProtocolIdAsync(Guid protocolId);
    Task<IEnumerable<ProtocolAdherence>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<ProtocolAdherence>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<ProtocolAdherence>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<ProtocolAdherence>> GetByComplianceScoreRangeAsync(int minScore, int maxScore);
    Task<IEnumerable<ProtocolAdherence>> GetVerifiedAdherenceAsync();
    Task<IEnumerable<ProtocolAdherence>> GetUnverifiedAdherenceAsync();
    Task<decimal> GetComplianceRateAsync(Guid protocolId, DateTime startDate, DateTime endDate);
    Task<decimal> GetTechnicianComplianceRateAsync(Guid technicianId, DateTime startDate, DateTime endDate);
    Task<int> GetAdherenceCountAsync(Guid protocolId, DateTime startDate, DateTime endDate);
}

public interface IProtocolViolationRepository : IGenericRepository<ProtocolViolation>
{
    Task<IEnumerable<ProtocolViolation>> GetByProtocolIdAsync(Guid protocolId);
    Task<IEnumerable<ProtocolViolation>> GetByWorkOrderIdAsync(Guid workOrderId);
    Task<IEnumerable<ProtocolViolation>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<ProtocolViolation>> GetBySeverityAsync(string severity);
    Task<IEnumerable<ProtocolViolation>> GetByInvestigationStatusAsync(string status);
    Task<IEnumerable<ProtocolViolation>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<ProtocolViolation>> GetWithInjuriesAsync();
    Task<IEnumerable<ProtocolViolation>> GetWithPropertyDamageAsync();
    Task<IEnumerable<ProtocolViolation>> GetOpenInvestigationsAsync();
    Task<IEnumerable<ProtocolViolation>> GetOverdueInvestigationsAsync();
    Task<decimal> GetTotalCostImpactAsync(DateTime startDate, DateTime endDate);
    Task<int> GetViolationCountByTechnicianAsync(Guid technicianId, DateTime startDate, DateTime endDate);
}

public interface IProtocolTrainingRepository : IGenericRepository<ProtocolTraining>
{
    Task<IEnumerable<ProtocolTraining>> GetByProtocolIdAsync(Guid protocolId);
    Task<IEnumerable<ProtocolTraining>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<ProtocolTraining>> GetByTrainingMethodAsync(string trainingMethod);
    Task<IEnumerable<ProtocolTraining>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<ProtocolTraining>> GetCompletedTrainingsAsync();
    Task<IEnumerable<ProtocolTraining>> GetIncompleteTrainingsAsync();
    Task<IEnumerable<ProtocolTraining>> GetExpiredTrainingsAsync();
    Task<IEnumerable<ProtocolTraining>> GetExpiringInDaysAsync(int days);
    Task<ProtocolTraining?> GetLatestTrainingAsync(Guid protocolId, Guid technicianId);
    Task<bool> HasValidTrainingAsync(Guid protocolId, Guid technicianId);
    Task<decimal> GetAverageTrainingHoursAsync(Guid protocolId);
    Task<decimal> GetCompletionRateAsync(Guid protocolId);
}

public interface ISafetyAuditRepository : IGenericRepository<SafetyAudit>
{
    Task<IEnumerable<SafetyAudit>> GetByAuditorAsync(string auditor);
    Task<IEnumerable<SafetyAudit>> GetByStatusAsync(string status);
    Task<IEnumerable<SafetyAudit>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<SafetyAudit>> GetByScoreRangeAsync(int minScore, int maxScore);
    Task<IEnumerable<SafetyAudit>> GetCompletedAuditsAsync();
    Task<IEnumerable<SafetyAudit>> GetAuditsRequiringFollowUpAsync();
    Task<SafetyAudit?> GetLatestAuditAsync();
    Task<decimal> GetAverageAuditScoreAsync(DateTime startDate, DateTime endDate);
}

public interface IProtocolAuditDetailRepository : IGenericRepository<ProtocolAuditDetail>
{
    Task<IEnumerable<ProtocolAuditDetail>> GetByAuditIdAsync(Guid auditId);
    Task<IEnumerable<ProtocolAuditDetail>> GetByProtocolIdAsync(Guid protocolId);
    Task<IEnumerable<ProtocolAuditDetail>> GetFailedAuditsAsync();
    Task<IEnumerable<ProtocolAuditDetail>> GetByPriorityAsync(string priority);
    Task<IEnumerable<ProtocolAuditDetail>> GetByComplianceScoreRangeAsync(int minScore, int maxScore);
    Task<decimal> GetAverageComplianceScoreAsync(Guid protocolId);
    Task<int> GetFailureCountAsync(Guid protocolId);
}

#endregion
