using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

#region Job Description Repository

public interface IJobDescriptionRepository : IGenericRepository<JobDescription>
{
    Task<JobDescription?> GetByJobDescriptionNumberAsync(string jobDescriptionNumber);
    Task<IEnumerable<JobDescription>> GetByPositionIdAsync(Guid positionId);
    Task<IEnumerable<JobDescription>> GetByStatusAsync(JobDescriptionStatus status);
    Task<JobDescription?> GetCurrentVersionForPositionAsync(Guid positionId);
    Task<IEnumerable<JobDescription>> GetDueForReviewAsync(int daysAhead = 30);
    Task<IEnumerable<JobDescription>> GetVersionHistoryAsync(Guid positionId);
    Task<int> GetNextVersionNumberAsync(Guid positionId);
}

#endregion Job Description Repository

#region Job Responsibility Repository

public interface IJobResponsibilityRepository : IGenericRepository<JobResponsibility>
{
    Task<IEnumerable<JobResponsibility>> GetByJobDescriptionIdAsync(Guid jobDescriptionId);
    Task<IEnumerable<JobResponsibility>> GetByTypeAsync(Guid jobDescriptionId, ResponsibilityType type);
    /// <summary>Returns responsibilities of type Core (highest importance)</summary>
    Task<IEnumerable<JobResponsibility>> GetCoreResponsibilitiesAsync(Guid jobDescriptionId);
}

#endregion Job Responsibility Repository

#region Job Qualification Repository

public interface IJobQualificationRepository : IGenericRepository<JobQualification>
{
    Task<IEnumerable<JobQualification>> GetByJobDescriptionIdAsync(Guid jobDescriptionId);
    Task<IEnumerable<JobQualification>> GetByResponsibilityIdAsync(Guid responsibilityId);
    Task<IEnumerable<JobQualification>> GetByTypeAsync(Guid jobDescriptionId, QualificationType type);
    Task<IEnumerable<JobQualification>> GetRequiredQualificationsAsync(Guid jobDescriptionId);
}

#endregion Job Qualification Repository

#region Job Competency Repository

public interface IJobCompetencyRepository : IGenericRepository<JobCompetency>
{
    Task<IEnumerable<JobCompetency>> GetByJobDescriptionIdAsync(Guid jobDescriptionId);
    Task<IEnumerable<JobCompetency>> GetByResponsibilityIdAsync(Guid responsibilityId);
    Task<IEnumerable<JobCompetency>> GetByTypeAsync(Guid jobDescriptionId, CompetencyType type);
    Task<IEnumerable<JobCompetency>> GetCriticalCompetenciesAsync(Guid jobDescriptionId);
}

#endregion Job Competency Repository

#region Manpower Budget Repository

public interface IManpowerBudgetRepository : IGenericRepository<ManpowerBudget>
{
    Task<ManpowerBudget?> GetByBudgetNumberAsync(string budgetNumber);
    Task<IEnumerable<ManpowerBudget>> GetByFiscalYearAsync(int fiscalYear);
    Task<IEnumerable<ManpowerBudget>> GetByOrganizationUnitIdAsync(Guid organizationUnitId);
    Task<IEnumerable<ManpowerBudget>> GetByOrganizationLevelIdAsync(Guid organizationLevelId);
    Task<IEnumerable<ManpowerBudget>> GetByStatusAsync(ManpowerBudgetStatus status);
    Task<ManpowerBudget?> GetCurrentBudgetForOrganizationUnitAsync(Guid organizationUnitId);
    Task<IEnumerable<ManpowerBudget>> GetPendingApprovalsAsync();
}

#endregion Manpower Budget Repository

#region Job Physical Demand Repository

public interface IJobPhysicalDemandRepository : IGenericRepository<JobPhysicalDemand>
{
    Task<IEnumerable<JobPhysicalDemand>> GetByJobDescriptionIdAsync(Guid jobDescriptionId);
    Task<IEnumerable<JobPhysicalDemand>> GetByTypeAsync(Guid jobDescriptionId, PhysicalDemandType demandType);
    Task<IEnumerable<JobPhysicalDemand>> GetEssentialDemandsAsync(Guid jobDescriptionId);
}

#endregion Job Physical Demand Repository

#region Job Working Condition Repository

public interface IJobWorkingConditionRepository : IGenericRepository<JobWorkingCondition>
{
    Task<IEnumerable<JobWorkingCondition>> GetByJobDescriptionIdAsync(Guid jobDescriptionId);
    Task<IEnumerable<JobWorkingCondition>> GetByEnvironmentTypeAsync(Guid jobDescriptionId, WorkEnvironmentType environmentType);
    Task<IEnumerable<JobWorkingCondition>> GetRequiringPPEAsync(Guid jobDescriptionId);
}

#endregion Job Working Condition Repository

#region Job Equipment Tool Repository

public interface IJobEquipmentToolRepository : IGenericRepository<JobEquipmentTool>
{
    Task<IEnumerable<JobEquipmentTool>> GetByJobDescriptionIdAsync(Guid jobDescriptionId);
    Task<IEnumerable<JobEquipmentTool>> GetByTypeAsync(Guid jobDescriptionId, EquipmentType type);
    Task<IEnumerable<JobEquipmentTool>> GetEssentialToolsAsync(Guid jobDescriptionId);
}

#endregion Job Equipment Tool Repository

#region Job Duty Item Repository

public interface IJobDutyItemRepository : IGenericRepository<JobDutyItem>
{
    Task<IEnumerable<JobDutyItem>> GetByJobDescriptionIdAsync(Guid jobDescriptionId);
    Task<int> GetNextSequenceNumberAsync(Guid jobDescriptionId);
}

#endregion Job Duty Item Repository

#region Job PPE Requirement Repository

public interface IJobPpeRequirementRepository : IGenericRepository<JobPpeRequirement>
{
    Task<IEnumerable<JobPpeRequirement>> GetByJobDescriptionIdAsync(Guid jobDescriptionId);
}

#endregion Job PPE Requirement Repository

#region Job Equipment Training Repository

public interface IJobEquipmentTrainingRepository : IGenericRepository<JobEquipmentTraining>
{
    Task<IEnumerable<JobEquipmentTraining>> GetByEquipmentToolIdAsync(Guid jobEquipmentToolId);
}

#endregion Job Equipment Training Repository

#region Job Medical Requirement Repository

public interface IJobMedicalRequirementRepository : IGenericRepository<JobMedicalRequirement>
{
    Task<IEnumerable<JobMedicalRequirement>> GetByJobDescriptionIdAsync(Guid jobDescriptionId);
}

#endregion Job Medical Requirement Repository

#region Job Responsibility KPI Repository

public interface IJobResponsibilityKpiRepository : IGenericRepository<JobResponsibilityKpi>
{
    Task<IEnumerable<JobResponsibilityKpi>> GetByResponsibilityIdAsync(Guid responsibilityId);
}

#endregion Job Responsibility KPI Repository

#region Job Reporting Relationship Repository

public interface IJobReportingRelationshipRepository : IGenericRepository<JobReportingRelationship>
{
    Task<IEnumerable<JobReportingRelationship>> GetByJobDescriptionIdAsync(Guid jobDescriptionId);
    Task<IEnumerable<JobReportingRelationship>> GetByRelationshipTypeAsync(Guid jobDescriptionId, ReportingRelationshipType relationshipType);
}

#endregion Job Reporting Relationship Repository

#region Manpower Budget Line Repository

public interface IManpowerBudgetLineRepository : IGenericRepository<ManpowerBudgetLine>
{
    Task<IEnumerable<ManpowerBudgetLine>> GetByBudgetIdAsync(Guid budgetId);
    Task<IEnumerable<ManpowerBudgetLine>> GetByPositionIdAsync(Guid positionId);
    Task<IEnumerable<ManpowerBudgetLine>> GetCriticalPositionsAsync(Guid budgetId);
    Task<IEnumerable<ManpowerBudgetLine>> GetByQuarterAsync(Guid budgetId, int quarter);
    Task<IEnumerable<ManpowerBudgetLine>> GetByPriorityAsync(Guid budgetId, BudgetPriority priority);
}

#endregion Manpower Budget Line Repository
