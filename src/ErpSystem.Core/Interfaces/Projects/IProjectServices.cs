using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Projects;

namespace ErpSystem.Core.Interfaces.Projects;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> GetProjectsAsync(int page, int pageSize, string? search = null, string? status = null, Guid? projectTypeId = null, Guid? portfolioId = null, Guid? programId = null);
    Task<ProjectDetailDto?> GetProjectByIdAsync(Guid id);
    Task<IEnumerable<ProjectLookupDto>> LookupProjectsAsync(string? search = null, string? status = null, Guid? projectTypeId = null, Guid? portfolioId = null, Guid? programId = null, int take = 20);
    Task<IEnumerable<ProjectResourceLookupDto>> LookupResourcesAsync(string? search = null, int take = 50);
    Task<ProjectDetailDto> CreateProjectAsync(CreateProjectDto dto);
    Task<ProjectDetailDto> UpdateProjectAsync(Guid id, UpdateProjectDto dto);
    Task SubmitProjectForApprovalAsync(Guid id, Guid userId);
    Task ApproveProjectAsync(Guid id, Guid userId, string? comments = null);
    Task RejectProjectAsync(Guid id, Guid userId, string reason, string? comments = null);

    Task<IEnumerable<ProjectMemberDto>> GetMembersAsync(Guid projectId);
    Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberDto dto);
    Task RemoveMemberAsync(Guid memberId);

    Task<IEnumerable<ProjectWorkItemDto>> GetWorkItemsAsync(Guid projectId);
    Task<ProjectWorkItemDto> AddWorkItemAsync(Guid projectId, CreateProjectWorkItemDto dto);
    Task<ProjectWorkItemDto> UpdateWorkItemAsync(Guid workItemId, CreateProjectWorkItemDto dto);
    Task ReorderWorkItemsAsync(Guid projectId, ReorderProjectWorkItemsDto dto);
    Task DeleteWorkItemAsync(Guid workItemId);

    Task<IEnumerable<ProjectMilestoneDto>> GetMilestonesAsync(Guid projectId);
    Task<ProjectMilestoneDto> AddMilestoneAsync(Guid projectId, CreateProjectMilestoneDto dto);
    Task<ProjectMilestoneDto> UpdateMilestoneAsync(Guid milestoneId, CreateProjectMilestoneDto dto);
    Task DeleteMilestoneAsync(Guid milestoneId);

    Task<IEnumerable<ProjectResourceAllocationDto>> GetResourceAllocationsAsync(Guid projectId);
    Task<ProjectResourceAllocationDto> AddResourceAllocationAsync(Guid projectId, CreateProjectResourceAllocationDto dto);
    Task<ProjectResourceAllocationDto> UpdateResourceAllocationAsync(Guid allocationId, CreateProjectResourceAllocationDto dto);
    Task<ProjectResourceAllocationDto> ApproveResourceAllocationAsync(Guid allocationId);
    Task<ProjectResourceSubstitutionResultDto> SubstituteResourceAllocationAsync(Guid allocationId, SubstituteProjectResourceAllocationDto dto);
    Task DeleteResourceAllocationAsync(Guid allocationId);

    Task<IEnumerable<ProjectRiskDto>> GetRisksAsync(Guid projectId);
    Task<ProjectRiskDto> AddRiskAsync(Guid projectId, CreateProjectRiskDto dto);
    Task<ProjectRiskDto> UpdateRiskAsync(Guid riskId, CreateProjectRiskDto dto);
    Task DeleteRiskAsync(Guid riskId);

    Task<IEnumerable<ProjectIssueDto>> GetIssuesAsync(Guid projectId);
    Task<ProjectIssueDto> AddIssueAsync(Guid projectId, CreateProjectIssueDto dto);
    Task<ProjectIssueDto> UpdateIssueAsync(Guid issueId, CreateProjectIssueDto dto);
    Task DeleteIssueAsync(Guid issueId);

    Task<IEnumerable<ProjectQualityCheckpointDto>> GetQualityCheckpointsAsync(Guid projectId);
    Task<ProjectQualityCheckpointDto> AddQualityCheckpointAsync(Guid projectId, CreateProjectQualityCheckpointDto dto);
    Task<ProjectQualityCheckpointDto> SignOffQualityCheckpointAsync(Guid checkpointId, string? notes = null);
    Task DeleteQualityCheckpointAsync(Guid checkpointId);

    Task<IEnumerable<ProjectNonConformanceDto>> GetNonConformancesAsync(Guid projectId);
    Task<ProjectNonConformanceDto> AddNonConformanceAsync(Guid projectId, CreateProjectNonConformanceDto dto);
    Task<ProjectNonConformanceDto> ResolveNonConformanceAsync(Guid nonConformanceId, string? notes = null);
    Task DeleteNonConformanceAsync(Guid nonConformanceId);

    Task<IEnumerable<ProjectChangeRequestDto>> GetChangeRequestsAsync(Guid projectId);
    Task<ProjectChangeRequestDto> AddChangeRequestAsync(Guid projectId, CreateProjectChangeRequestDto dto);
    Task<ProjectChangeRequestDto> UpdateChangeRequestAsync(Guid changeRequestId, CreateProjectChangeRequestDto dto);
    Task DeleteChangeRequestAsync(Guid changeRequestId);

    Task<IEnumerable<ProjectBillingScheduleDto>> GetBillingSchedulesAsync(Guid projectId);
    Task<ProjectBillingScheduleDto> AddBillingScheduleAsync(Guid projectId, CreateProjectBillingScheduleDto dto);
    Task<ProjectBillingScheduleDto> UpdateBillingScheduleAsync(Guid billingScheduleId, CreateProjectBillingScheduleDto dto);
    Task DeleteBillingScheduleAsync(Guid billingScheduleId);

    Task<IEnumerable<ProjectInvoiceRequestDto>> GetInvoiceRequestsAsync(Guid projectId);
    Task<ProjectInvoiceRequestDto> CreateInvoiceRequestAsync(Guid projectId, CreateProjectInvoiceRequestDto dto);
    Task<ProjectInvoiceRequestDto> GenerateInvoiceRequestFromScheduleAsync(Guid billingScheduleId, string? notes = null);
    Task<ProjectInvoiceRequestDto> SubmitInvoiceRequestAsync(Guid invoiceRequestId, string? comments = null);
    Task<ProjectInvoiceRequestDto> MarkInvoiceRequestSentToFinanceAsync(Guid invoiceRequestId, string? externalReference = null, string? comments = null);
    Task<ProjectInvoiceRequestDto> MarkInvoiceRequestInvoicedAsync(Guid invoiceRequestId, string? externalReference = null, string? comments = null);
    Task<ProjectInvoiceRequestDto> MarkInvoiceRequestPaidAsync(Guid invoiceRequestId, string? comments = null);
    Task<IEnumerable<ProjectContractLookupDto>> GetContractLookupAsync(Guid? businessPartnerId = null, string? search = null);
    Task<IEnumerable<ProjectContractMilestoneLookupDto>> GetContractMilestonesAsync(Guid contractId);

    Task<IEnumerable<ProjectDeliverableDto>> GetDeliverablesAsync(Guid projectId);
    Task<ProjectDeliverableDto> AddDeliverableAsync(Guid projectId, CreateProjectDeliverableDto dto);
    Task<ProjectDeliverableDto> UpdateDeliverableAsync(Guid deliverableId, CreateProjectDeliverableDto dto);
    Task<ProjectDeliverableDto> SubmitDeliverableAsync(Guid deliverableId, SubmitProjectDeliverableDto dto);
    Task<ProjectDeliverableDto> ApproveDeliverableAsync(Guid deliverableId, string? notes = null);
    Task<ProjectDeliverableDto> RejectDeliverableAsync(Guid deliverableId, string? notes = null);

    Task<IEnumerable<ProjectTaskDependencyDto>> GetTaskDependenciesAsync(Guid projectId);
    Task<ProjectTaskDependencyDto> AddTaskDependencyAsync(Guid projectId, CreateProjectTaskDependencyDto dto);
    Task DeleteTaskDependencyAsync(Guid dependencyId);
    Task<IEnumerable<ProjectInterdependencyDto>> GetInterdependenciesAsync(Guid? projectId = null, Guid? portfolioId = null, Guid? programId = null);
    Task<ProjectInterdependencyDto> CreateInterdependencyAsync(CreateProjectInterdependencyDto dto);
    Task<ProjectInterdependencyDto> UpdateInterdependencyAsync(Guid id, CreateProjectInterdependencyDto dto);
    Task DeleteInterdependencyAsync(Guid id);
    Task<IEnumerable<ProjectBaselineDto>> GetBaselinesAsync(Guid projectId);
    Task<ProjectBaselineDto> CreateBaselineAsync(Guid projectId, CreateProjectBaselineDto dto);
    Task<ProjectBaselineComparisonDto> CompareBaselineAsync(Guid baselineId);
    Task<ProjectScheduleAnalysisDto> AnalyzeScheduleAsync(Guid projectId);
    Task<ProjectScheduleAnalysisDto> RecalculateScheduleAsync(Guid projectId);

    Task<IEnumerable<ProjectTimesheetEntryDto>> GetTimesheetEntriesAsync(Guid projectId);
    Task<IEnumerable<ProjectTimesheetEntryDto>> GetMyTimesheetEntriesAsync(string? status = null);
    Task<ProjectApprovalQueueSummaryDto> GetTimesheetApprovalSummaryAsync(Guid? projectId = null);
    Task<IEnumerable<ProjectTimesheetApprovalQueueItemDto>> GetTimesheetApprovalQueueAsync(Guid? projectId = null, string? status = null, Guid? userId = null, int take = 200);
    Task<ProjectTimesheetEntryDto> AddTimesheetEntryAsync(Guid projectId, CreateProjectTimesheetEntryDto dto);
    Task<ProjectTimesheetEntryDto> UpdateTimesheetEntryAsync(Guid entryId, CreateProjectTimesheetEntryDto dto);
    Task<ProjectTimesheetEntryDto> SubmitTimesheetEntryAsync(Guid entryId);
    Task<ProjectTimesheetEntryDto> ApproveTimesheetEntryAsync(Guid entryId, string? comments = null);
    Task<ProjectTimesheetEntryDto> RejectTimesheetEntryAsync(Guid entryId, string? comments = null);
    Task DeleteTimesheetEntryAsync(Guid entryId);
    Task<IEnumerable<ProjectExpenseDto>> GetExpensesAsync(Guid projectId);
    Task<IEnumerable<ProjectExpenseDto>> GetMyExpensesAsync(string? status = null);
    Task<ProjectApprovalQueueSummaryDto> GetExpenseApprovalSummaryAsync(Guid? projectId = null);
    Task<IEnumerable<ProjectExpenseApprovalQueueItemDto>> GetExpenseApprovalQueueAsync(Guid? projectId = null, string? status = null, Guid? userId = null, int take = 200);
    Task<ProjectExpenseDto> AddExpenseAsync(Guid projectId, CreateProjectExpenseDto dto);
    Task<ProjectExpenseDto> UpdateExpenseAsync(Guid expenseId, CreateProjectExpenseDto dto);
    Task<ProjectExpenseDto> SubmitExpenseAsync(Guid expenseId);
    Task<ProjectExpenseDto> ApproveExpenseAsync(Guid expenseId, string? comments = null);
    Task<ProjectExpenseDto> RejectExpenseAsync(Guid expenseId, string? comments = null);
    Task DeleteExpenseAsync(Guid expenseId);
    Task<IEnumerable<ProjectMaterialCostEntryDto>> GetMaterialCostEntriesAsync(Guid projectId);
    Task<IEnumerable<ProjectMaterialCostEntryDto>> GetMaterialCostLedgerReportAsync(Guid? projectId = null, int take = 300, string? sourceDocumentType = null, string? postingState = null, bool? isReversed = null, bool? exceptionsOnly = null);
    Task SyncProjectMaterialCostAsync(Guid projectId);
    Task SyncInventoryRequisitionMaterialCostAsync(Guid requisitionId);
    Task SyncPurchaseReceiptMaterialCostAsync(Guid receiptId);
    Task SyncPurchaseReturnMaterialCostAsync(Guid purchaseReturnId);

    Task<IEnumerable<ProjectRevenueRecognitionDto>> GetRevenueRecognitionsAsync(Guid projectId);
    Task<IEnumerable<ProjectRevenueRecognitionDto>> GenerateRevenueRecognitionAsync(Guid projectId);
    Task<IEnumerable<ProjectBudgetRevisionDto>> GetBudgetRevisionsAsync(Guid projectId);
    Task<ProjectBudgetRevisionDto> CreateBudgetRevisionAsync(Guid projectId, CreateProjectBudgetRevisionDto dto);
    Task<ProjectBudgetRevisionDto> SubmitBudgetRevisionAsync(Guid revisionId, Guid userId);
    Task<ProjectBudgetRevisionDto> ApproveBudgetRevisionAsync(Guid revisionId, Guid userId, string? comments = null);
    Task<ProjectBudgetRevisionDto> RejectBudgetRevisionAsync(Guid revisionId, Guid userId, string reason, string? comments = null);
    Task<IEnumerable<ProjectForecastVersionDto>> GetForecastVersionsAsync(Guid projectId);
    Task<ProjectForecastVersionDto> CreateForecastVersionAsync(Guid projectId, CreateProjectForecastVersionDto dto);
    Task<ProjectForecastVersionDto> SetActiveForecastVersionAsync(Guid forecastVersionId);

    Task<IEnumerable<ProjectAssetLinkDto>> GetAssetLinksAsync(Guid projectId);
    Task<ProjectAssetLinkDto> AddAssetLinkAsync(Guid projectId, CreateProjectAssetLinkDto dto);
    Task<IEnumerable<ProjectExternalAccessPolicyDto>> GetExternalAccessPoliciesAsync(Guid projectId);
    Task<ProjectExternalAccessPolicyDto> UpsertExternalAccessPolicyAsync(Guid projectId, CreateProjectExternalAccessPolicyDto dto);
    Task DeleteExternalAccessPolicyAsync(Guid policyId);
    Task<IEnumerable<ProjectDecisionDto>> GetDecisionsAsync(Guid projectId);
    Task<ProjectDecisionDto> AddDecisionAsync(Guid projectId, CreateProjectDecisionDto dto);
    Task<ProjectDecisionDto> UpdateDecisionAsync(Guid decisionId, CreateProjectDecisionDto dto);
    Task DeleteDecisionAsync(Guid decisionId);
    Task<IEnumerable<ProjectMeetingMinuteDto>> GetMeetingsAsync(Guid projectId);
    Task<ProjectMeetingMinuteDto> AddMeetingAsync(Guid projectId, CreateProjectMeetingMinuteDto dto);
    Task<ProjectMeetingMinuteDto> UpdateMeetingAsync(Guid meetingId, CreateProjectMeetingMinuteDto dto);
    Task DeleteMeetingAsync(Guid meetingId);
    Task<IEnumerable<ProjectActionItemDto>> GetActionItemsAsync(Guid projectId);
    Task<ProjectActionItemDto> AddActionItemAsync(Guid projectId, CreateProjectActionItemDto dto);
    Task<ProjectActionItemDto> UpdateActionItemAsync(Guid actionItemId, CreateProjectActionItemDto dto);
    Task DeleteActionItemAsync(Guid actionItemId);
    Task<IEnumerable<ProjectLessonLearnedDto>> GetLessonsLearnedAsync(Guid projectId);
    Task<ProjectLessonLearnedDto> AddLessonLearnedAsync(Guid projectId, CreateProjectLessonLearnedDto dto);
    Task<ProjectLessonLearnedDto> UpdateLessonLearnedAsync(Guid lessonId, CreateProjectLessonLearnedDto dto);
    Task DeleteLessonLearnedAsync(Guid lessonId);
    Task<ProjectClosureDto?> GetClosureAsync(Guid projectId);
    Task<ProjectClosureDto> UpsertClosureAsync(Guid projectId, UpsertProjectClosureDto dto);
    Task SubmitClosureForApprovalAsync(Guid projectId, Guid userId);
    Task ApproveClosureAsync(Guid closureId, Guid userId, string? comments = null);
    Task RejectClosureAsync(Guid closureId, Guid userId, string reason, string? comments = null);

    Task<IEnumerable<ProjectAiInsightDto>> GetAiInsightsAsync(Guid projectId);
    Task<IEnumerable<ProjectResourceOptimizationSuggestionDto>> GetResourceOptimizationSuggestionsAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<ProjectMobileSummaryDto> GetMobileSummaryAsync(Guid userId);
    Task<ProjectTimesheetEntryDto> SubmitMobileTimesheetAsync(Guid projectId, Guid workItemId, CreateProjectTimesheetEntryDto dto, Guid userId);
    Task<ProjectExpenseDto> SubmitMobileExpenseAsync(Guid projectId, Guid workItemId, CreateProjectExpenseDto dto, Guid userId);
    Task<ProjectWorkItemDto> UpdateMobileWorkItemProgressAsync(Guid projectId, Guid workItemId, UpdateProjectWorkItemProgressDto dto, Guid userId);

    Task<IEnumerable<ProjectDocumentDto>> GetDocumentsAsync(Guid projectId);
    Task<ProjectDocumentDto> AttachDocumentAsync(Guid projectId, AttachProjectDocumentDto dto);
    Task DeleteDocumentAsync(Guid documentId);

    Task<IEnumerable<ProjectCommentDto>> GetCommentsAsync(Guid projectId);
    Task<ProjectCommentDto> AddCommentAsync(Guid projectId, CreateProjectCommentDto dto);
    Task<IEnumerable<ProjectExternalSummaryDto>> GetExternalProjectsAsync(Guid userId);
    Task<ProjectExternalDetailDto?> GetExternalProjectByIdAsync(Guid projectId, Guid userId);
    Task<ProjectCommentDto> AddExternalCommentAsync(Guid projectId, CreateProjectCommentDto dto, Guid userId);
    Task<ProjectDocumentDto> AttachExternalDocumentAsync(Guid projectId, AttachProjectDocumentDto dto, Guid userId);
    Task<ProjectWorkItemDto> UpdateExternalWorkItemProgressAsync(Guid projectId, Guid workItemId, UpdateProjectWorkItemProgressDto dto, Guid userId);
    Task<ProjectDeliverableDto> SubmitExternalDeliverableAsync(Guid projectId, Guid deliverableId, SubmitProjectDeliverableDto dto, Guid userId);
    Task<ProjectDeliverableDto> ApproveExternalDeliverableAsync(Guid projectId, Guid deliverableId, string? notes, Guid userId);
    Task<ProjectDeliverableDto> RejectExternalDeliverableAsync(Guid projectId, Guid deliverableId, string? notes, Guid userId);

    Task<ProjectDashboardDto> GetDashboardAsync();
    Task<ProjectFinancialControlSummaryDto> GetFinancialControlSummaryAsync(Guid projectId);
    Task<ProjectIntegrationSummaryDto> GetIntegrationSummaryAsync(Guid projectId);
    Task<ProjectGovernanceSummaryDto> GetGovernanceSummaryAsync(Guid projectId);
    Task<IEnumerable<ProjectDto>> GetProjectRegisterReportAsync(string? search = null, string? status = null, Guid? projectTypeId = null, int take = 200);
    Task<IEnumerable<ProjectTaskAgingReportItemDto>> GetTaskAgingReportAsync(Guid? projectId = null, int take = 100);
    Task<IEnumerable<ProjectMilestoneTrackerReportItemDto>> GetMilestoneTrackerReportAsync(Guid? projectId = null, int take = 100);
    Task<IEnumerable<ProjectBudgetActualReportItemDto>> GetBudgetActualReportAsync(int take = 200);
    Task<IEnumerable<ProjectRiskIssueSummaryReportItemDto>> GetRiskIssueSummaryReportAsync(int take = 200);
    Task<IEnumerable<ProjectPortfolioSummaryReportItemDto>> GetPortfolioSummaryReportAsync(int take = 100);
    Task<IEnumerable<ProjectProgramSummaryReportItemDto>> GetProgramSummaryReportAsync(Guid? portfolioId = null, int take = 100);
    Task<IEnumerable<ProjectPerformanceAnalyticsReportItemDto>> GetPerformanceAnalyticsReportAsync(int take = 200);
    Task<IEnumerable<ProjectPortfolioPrioritizationReportItemDto>> GetPortfolioPrioritizationReportAsync(Guid? portfolioId = null, int take = 100);
    Task<IEnumerable<ProjectDependencyWatchReportItemDto>> GetDependencyWatchReportAsync(Guid? portfolioId = null, Guid? programId = null, int take = 100);
    Task<IEnumerable<ProjectStrategicInitiativeReportItemDto>> GetStrategicInitiativeReportAsync(Guid? portfolioId = null, int take = 100);
    Task<IEnumerable<ProjectMaterialReconciliationReportItemDto>> GetMaterialReconciliationReportAsync(int take = 200, string? reconciliationStatus = null);
    Task<IEnumerable<ProjectProcurementReconciliationReportItemDto>> GetProcurementReconciliationReportAsync(int take = 200, string? reconciliationStatus = null);
    Task<IEnumerable<ProjectResourceCapacityReportItemDto>> GetResourceCapacityReportAsync(DateTime? startDate = null, DateTime? endDate = null, Guid? userId = null);
    Task<IEnumerable<ProjectResourceCapacityRecommendationDto>> GetResourceCapacityRecommendationsAsync(DateTime? startDate = null, DateTime? endDate = null, Guid? userId = null);
    Task<IEnumerable<ProjectBillingSummaryReportItemDto>> GetBillingSummaryReportAsync(int take = 200);
    Task<IEnumerable<ProjectInvoiceRequestQueueItemDto>> GetInvoiceRequestQueueReportAsync(int take = 200, string? status = null);
    Task<IEnumerable<ProjectWorkflowApprovalQueueItemDto>> GetWorkflowApprovalQueueReportAsync(int take = 200, string? entityType = null);
    Task<IEnumerable<ProjectExternalCollaborationReportItemDto>> GetExternalCollaborationReportAsync(int take = 200, string? collaborationState = null);
}

public interface IProjectSetupService
{
    Task<ProjectMasterDataOverviewDto> GetMasterDataOverviewAsync();
    Task<IEnumerable<ProjectCatalogEntryDto>> GetCatalogEntriesAsync(string catalogType);
    Task<ProjectCatalogEntryDto> CreateCatalogEntryAsync(CreateProjectCatalogEntryDto dto);
    Task<ProjectCatalogEntryDto> UpdateCatalogEntryAsync(Guid id, CreateProjectCatalogEntryDto dto);
    Task DeleteCatalogEntryAsync(Guid id);
    Task SeedCatalogDefaultsAsync(string? catalogType = null);
    Task<IEnumerable<ProjectTypeDto>> GetProjectTypesAsync();
    Task<ProjectTypeDto> CreateProjectTypeAsync(CreateProjectTypeDto dto);
    Task<ProjectTypeDto> UpdateProjectTypeAsync(Guid id, CreateProjectTypeDto dto);
    Task DeleteProjectTypeAsync(Guid id);

    Task<IEnumerable<ProjectPriorityDto>> GetProjectPrioritiesAsync();
    Task<ProjectPriorityDto> CreateProjectPriorityAsync(CreateProjectPriorityDto dto);
    Task<ProjectPriorityDto> UpdateProjectPriorityAsync(Guid id, CreateProjectPriorityDto dto);
    Task DeleteProjectPriorityAsync(Guid id);

    Task<IEnumerable<ProjectTemplateDto>> GetProjectTemplatesAsync(Guid? projectTypeId = null);
    Task<ProjectTemplateDto> CreateProjectTemplateAsync(CreateProjectTemplateDto dto);
    Task<ProjectTemplateDto> UpdateProjectTemplateAsync(Guid id, CreateProjectTemplateDto dto);
    Task DeleteProjectTemplateAsync(Guid id);

    Task<IEnumerable<ProjectPortfolioDto>> GetPortfoliosAsync();
    Task<ProjectPortfolioDto> CreatePortfolioAsync(CreateProjectPortfolioDto dto);
    Task<ProjectPortfolioDto> UpdatePortfolioAsync(Guid id, CreateProjectPortfolioDto dto);
    Task DeletePortfolioAsync(Guid id);

    Task<IEnumerable<ProjectProgramDto>> GetProgramsAsync(Guid? portfolioId = null);
    Task<ProjectProgramDto> CreateProgramAsync(CreateProjectProgramDto dto);
    Task<ProjectProgramDto> UpdateProgramAsync(Guid id, CreateProjectProgramDto dto);
    Task DeleteProgramAsync(Guid id);
}

public interface IProjectManagementSettingsService
{
    Task<ProjectManagementSettingsDto> GetSettingsAsync();
    Task<ProjectManagementSettingsDto> UpdateSettingsAsync(UpdateProjectManagementSettingsDto dto);
}
