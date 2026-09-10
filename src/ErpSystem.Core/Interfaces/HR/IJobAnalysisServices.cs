using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

#region Job Description Service

public interface IJobDescriptionService
{
    Task<JobDescriptionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobDescriptionDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobDescriptionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<JobDescriptionDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobDescriptionSummaryDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobDescriptionSummaryDto>> GetByStatusAsync(JobDescriptionStatus status, CancellationToken cancellationToken = default);
    Task<JobDescriptionDto?> GetCurrentVersionForPositionAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobDescriptionSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<JobAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Positions with no approved job description — the work list behind FR-HR-134, ordered by how
    /// many people are doing a job nobody has described.
    /// </summary>
    Task<IEnumerable<UncoveredPositionDto>> GetUncoveredPositionsAsync(
        CancellationToken cancellationToken = default);

    Task<IEnumerable<JobDescriptionSummaryDto>> GetVersionHistoryAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<JobDescriptionDto> CreateAsync(CreateJobDescriptionDto createDto, Guid preparedById, CancellationToken cancellationToken = default);
    Task<JobDescriptionDto> UpdateAsync(UpdateJobDescriptionDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> SubmitForReviewAsync(SubmitJobDescriptionForReviewDto submitDto, CancellationToken cancellationToken = default);
    Task<bool> ReviewAsync(ReviewJobDescriptionDto reviewDto, Guid reviewedById, CancellationToken cancellationToken = default);
    Task<bool> ApproveAsync(ApproveJobDescriptionDto approveDto, Guid approvedById, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the current workflow step for a job description, and — when that step completes the
    /// chain — applies the consequences of approval (area 17 slice 3, FR-HR-134).
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ApproveAsync"/> because the two answer to different authorities:
    /// the direct route asks whether the caller holds <c>HR.JobArchitecture.Admin</c>, this one asks
    /// the engine whether the caller is the assigned approver for the step in front of them. Once a
    /// tenant publishes a definition the direct route refuses, so the configured chain cannot be
    /// bypassed by a permission.
    /// </remarks>
    Task<bool> ApproveViaWorkflowAsync(Guid jobDescriptionId, Guid approvedById, CancellationToken cancellationToken = default);

    /// <summary>Rejects the current workflow step, returning the job description to its author.</summary>
    Task<bool> RejectViaWorkflowAsync(Guid jobDescriptionId, string? reason, CancellationToken cancellationToken = default);
    Task<JobDescriptionDto> CreateNewVersionAsync(CreateJobDescriptionVersionDto versionDto, Guid preparedById, CancellationToken cancellationToken = default);
    /// <summary>Deep-copies a job description (and all its child sections) into a new Draft.</summary>
    Task<JobDescriptionDto> CloneAsync(Guid id, Guid? preparedById, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Responsibility operations
    Task<JobResponsibilityDto> AddResponsibilityAsync(CreateJobResponsibilityDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobResponsibilityDto>> GetResponsibilitiesAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);
    Task<JobResponsibilityDto> UpdateResponsibilityAsync(UpdateJobResponsibilityDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteResponsibilityAsync(Guid responsibilityId, CancellationToken cancellationToken = default);

    // Qualification operations
    Task<JobQualificationDto> AddQualificationAsync(CreateJobQualificationDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobQualificationDto>> GetQualificationsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);
    Task<JobQualificationDto> UpdateQualificationAsync(UpdateJobQualificationDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default);

    // Competency operations
    Task<JobCompetencyDto> AddCompetencyAsync(CreateJobCompetencyDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobCompetencyDto>> GetCompetenciesAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);
    Task<JobCompetencyDto> UpdateCompetencyAsync(UpdateJobCompetencyDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteCompetencyAsync(Guid competencyId, CancellationToken cancellationToken = default);

    // Physical Demand operations
    Task<JobPhysicalDemandDto> AddPhysicalDemandAsync(CreateJobPhysicalDemandDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobPhysicalDemandDto>> GetPhysicalDemandsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);
    Task<JobPhysicalDemandDto> UpdatePhysicalDemandAsync(UpdateJobPhysicalDemandDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeletePhysicalDemandAsync(Guid demandId, CancellationToken cancellationToken = default);

    // Working Condition operations
    Task<JobWorkingConditionDto> AddWorkingConditionAsync(CreateJobWorkingConditionDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobWorkingConditionDto>> GetWorkingConditionsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);
    Task<JobWorkingConditionDto> UpdateWorkingConditionAsync(UpdateJobWorkingConditionDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteWorkingConditionAsync(Guid conditionId, CancellationToken cancellationToken = default);

    // Equipment Tool operations
    Task<JobEquipmentToolDto> AddEquipmentToolAsync(CreateJobEquipmentToolDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobEquipmentToolDto>> GetEquipmentToolsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);
    Task<JobEquipmentToolDto> UpdateEquipmentToolAsync(UpdateJobEquipmentToolDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteEquipmentToolAsync(Guid toolId, CancellationToken cancellationToken = default);

    // Reporting Relationship operations
    Task<JobReportingRelationshipDto> AddReportingRelationshipAsync(CreateJobReportingRelationshipDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobReportingRelationshipDto>> GetReportingRelationshipsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);
    Task<JobReportingRelationshipDto> UpdateReportingRelationshipAsync(UpdateJobReportingRelationshipDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteReportingRelationshipAsync(Guid relationshipId, CancellationToken cancellationToken = default);

    // Duty Item operations
    Task<JobDutyItemDto> AddDutyItemAsync(CreateJobDutyItemDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobDutyItemDto>> GetDutyItemsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);
    Task<JobDutyItemDto> UpdateDutyItemAsync(UpdateJobDutyItemDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteDutyItemAsync(Guid dutyItemId, CancellationToken cancellationToken = default);

    // PPE Requirement operations
    Task<JobPpeRequirementDto> AddPpeRequirementAsync(CreateJobPpeRequirementDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobPpeRequirementDto>> GetPpeRequirementsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);
    Task<JobPpeRequirementDto> UpdatePpeRequirementAsync(UpdateJobPpeRequirementDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeletePpeRequirementAsync(Guid ppeRequirementId, CancellationToken cancellationToken = default);

    // Equipment Training operations
    Task<JobEquipmentTrainingDto> AddEquipmentTrainingAsync(CreateJobEquipmentTrainingDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobEquipmentTrainingDto>> GetEquipmentTrainingsAsync(Guid jobEquipmentToolId, CancellationToken cancellationToken = default);
    Task<JobEquipmentTrainingDto> UpdateEquipmentTrainingAsync(UpdateJobEquipmentTrainingDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteEquipmentTrainingAsync(Guid equipmentTrainingId, CancellationToken cancellationToken = default);

    // Medical Requirement operations
    Task<JobMedicalRequirementDto> AddMedicalRequirementAsync(CreateJobMedicalRequirementDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobMedicalRequirementDto>> GetMedicalRequirementsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);
    Task<JobMedicalRequirementDto> UpdateMedicalRequirementAsync(UpdateJobMedicalRequirementDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteMedicalRequirementAsync(Guid medicalRequirementId, CancellationToken cancellationToken = default);

    // Job Evaluation / Valuation
    /// <summary>Computes the valuation summary and persists the estimated range + suggested grade.</summary>
    /// <summary>Computes the valuation and returns it. Safe: changes nothing.</summary>
    Task<JobValuationSummaryDto> GetValuationAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);

    /// <summary>Computes the valuation and stores it on the job description. Refuses an approved one.</summary>
    Task<JobValuationSummaryDto> RecalculateValuationAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default);

    // Responsibility KPI operations
    Task<JobResponsibilityKpiDto> AddResponsibilityKpiAsync(CreateJobResponsibilityKpiDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobResponsibilityKpiDto>> GetResponsibilityKpisAsync(Guid responsibilityId, CancellationToken cancellationToken = default);
    Task<JobResponsibilityKpiDto> UpdateResponsibilityKpiAsync(UpdateJobResponsibilityKpiDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteResponsibilityKpiAsync(Guid kpiId, CancellationToken cancellationToken = default);
}

#endregion Job Description Service

#region Manpower Budget Service

public interface IManpowerBudgetService
{
    Task<ManpowerBudgetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ManpowerBudgetDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ManpowerBudgetDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<ManpowerBudgetDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<ManpowerBudgetSummaryDto>> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default);
    Task<IEnumerable<ManpowerBudgetSummaryDto>> GetByOrganizationUnitIdAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ManpowerBudgetSummaryDto>> GetByOrganizationLevelIdAsync(Guid organizationLevelId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ManpowerBudgetSummaryDto>> GetByStatusAsync(ManpowerBudgetStatus status, CancellationToken cancellationToken = default);
    Task<ManpowerBudgetDto?> GetCurrentBudgetForOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ManpowerBudgetSummaryDto>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default);
    Task<ManpowerBudgetDto> CreateAsync(CreateManpowerBudgetDto createDto, CancellationToken cancellationToken = default);
    Task<ManpowerBudgetDto> UpdateAsync(UpdateManpowerBudgetDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> SubmitForApprovalAsync(Guid budgetId, CancellationToken cancellationToken = default);
    Task<bool> ApproveAsync(ApproveManpowerBudgetDto approveDto, Guid approvedById, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the current workflow step for a manpower budget (FR-HR-135: Department Head → HR →
    /// Managing Director). Stamps the approver only when the step completes the chain.
    /// </summary>
    Task<bool> ApproveViaWorkflowAsync(Guid budgetId, Guid approvedById, CancellationToken cancellationToken = default);

    /// <summary>Rejects the current workflow step for a manpower budget, keeping the reason.</summary>
    Task<bool> RejectViaWorkflowAsync(Guid budgetId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a position's approved establishment directly (FR-HR-136), for posts no manpower budget
    /// covers. Stamps <c>EstablishmentApprovedOn</c> and leaves <c>EstablishmentSourceBudgetId</c>
    /// null, so a screen can tell an HR-set number from a budget-derived one.
    /// </summary>
    Task<PositionEstablishmentResultDto> SetPositionEstablishmentAsync(
        Guid positionId, SetPositionEstablishmentDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a position's approved establishment, returning it to unconstrained.
    /// </summary>
    /// <remarks>
    /// ⚠ Needed because an establishment can be set in error — the wrong budget approved, the wrong
    /// number typed — and until this existed there was no way back: the position was permanently
    /// constrained by a figure nobody meant, and every requisition and movement against it was
    /// refused for a reason no one could undo. <c>ExpectedHeadcount</c> is left as it stands; the
    /// authorisation is what is withdrawn, not the planning number.
    /// </remarks>
    Task<PositionEstablishmentResultDto> WithdrawPositionEstablishmentAsync(
        Guid positionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>A position's establishment, how many are actually in post, and where the number came from.</summary>
    Task<PositionEstablishmentResultDto> GetPositionEstablishmentAsync(
        Guid positionId, CancellationToken cancellationToken = default);
    Task<bool> RejectAsync(Guid budgetId, string reason, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the system knows about a unit's subtree before a budget is typed for it: serving
    /// headcount, estimated salary cost, exits due in the period, and each post against its
    /// establishment (round 2b, R2). See <see cref="ManpowerPlanningBaselineDto"/>.
    /// </summary>
    /// <summary>A Draft budget for a unit and year with one line per post, from the establishment (round 2b, R4a).</summary>
    Task<ManpowerBudgetDetailDto> CreateFromEstablishmentAsync(CreateManpowerBudgetFromEstablishmentDto dto, CancellationToken cancellationToken = default);

    /// <summary>Adds lines for posts in the budget's subtree not yet on it; never overwrites (R4a).</summary>
    Task<AddLinesFromEstablishmentResultDto> AddLinesFromEstablishmentAsync(Guid budgetId, bool includeUnestablished, CancellationToken cancellationToken = default);

    /// <summary>Every position's establishment in one read — the admin screen's list (R4a).</summary>
    Task<IEnumerable<PositionEstablishmentResultDto>> GetEstablishmentListAsync(Guid? organizationUnitId, CancellationToken cancellationToken = default);

    Task<ManpowerPlanningBaselineDto> GetPlanningBaselineAsync(
        Guid organizationUnitId, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default);

    // Budget Line operations
    /// <summary>The grade a position carries, if any, so a budget line can start from it (round 2b, R3).</summary>
    Task<PositionSalaryReferenceDto> GetPositionSalaryReferenceAsync(Guid positionId, CancellationToken cancellationToken = default);

    Task<ManpowerBudgetLineDto> AddBudgetLineAsync(CreateManpowerBudgetLineDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<ManpowerBudgetLineDto>> GetBudgetLinesAsync(Guid budgetId, CancellationToken cancellationToken = default);
    Task<ManpowerBudgetLineDto> UpdateBudgetLineAsync(UpdateManpowerBudgetLineDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteBudgetLineAsync(Guid lineId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ManpowerBudgetLineDto>> GetCriticalPositionsAsync(Guid budgetId, CancellationToken cancellationToken = default);
}

#endregion Manpower Budget Service
