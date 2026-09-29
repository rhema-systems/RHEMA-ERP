using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IBudgetService
{
    // Scenarios (Versions)
    Task<BudgetScenarioDto> CreateScenarioAsync(CreateBudgetScenarioDto dto);
    Task<BudgetScenarioDto> UpdateScenarioAsync(UpdateBudgetScenarioDto dto);
    Task<BudgetScenarioDto> GetScenarioAsync(Guid id);
    Task<IEnumerable<BudgetScenarioDto>> GetScenariosForYearAsync(Guid fiscalYearId);
    Task<bool> DeleteScenarioAsync(Guid id);
    Task<BudgetScenarioDto> OpenScenarioAsync(Guid id, string rowVersion);
    Task<BudgetScenarioDto> SubmitScenarioAsync(Guid id, string rowVersion);
    Task<BudgetScenarioDto> AdoptScenarioAsync(Guid id, AdoptBudgetScenarioDto dto);
    Task<BudgetScenarioDto> ArchiveScenarioAsync(Guid id, string rowVersion);

    // Returns (Worksheets/Proposals)
    Task<BudgetReturnDto> CreateReturnAsync(CreateBudgetReturnDto dto);
    Task<BudgetReturnDto> UpdateReturnAsync(Guid id, UpdateBudgetReturnDto dto);
    Task<BudgetReturnDto> GetReturnAsync(Guid id);
    Task<IEnumerable<BudgetReturnDto>> GetMyReturnsAsync();
    Task<IEnumerable<BudgetReturnDto>> GetReturnsForScenarioAsync(Guid scenarioId);
    Task<BudgetReturnDto> SubmitReturnAsync(Guid id, string rowVersion);
    Task<BudgetReturnDto> RecallReturnAsync(Guid id, string rowVersion, string? reason);

    // Entries
    Task<IEnumerable<BudgetEntryDto>> GetEntriesAsync(Guid returnId);
    Task<BudgetReturnDto> BulkSaveEntriesAsync(BulkSaveBudgetEntriesDto dto);
    Task<IEnumerable<BudgetEntryDto>> GetConsolidatedBudgetAsync(Guid scenarioId, Guid? accountId = null);
    Task<ConsolidatedBudgetViewDto> GetConsolidatedViewAsync(Guid scenarioId, bool approvedOnly);
    Task<ConsolidatedBudgetViewDto> GetActiveBudgetVsActualAsync(Guid fiscalYearId);
    Task<BudgetScenarioComparisonDto> CompareScenariosAsync(Guid baseScenarioId, Guid comparisonScenarioId);
    Task<IReadOnlyList<BudgetAuditEventDto>> GetAuditHistoryAsync(string entityType, Guid entityId);

    // Controlled changes to an adopted budget. Approval does not mutate the
    // official scenario; Apply creates and adopts an immutable successor version.
    Task<IReadOnlyList<BudgetRevisionDto>> GetRevisionsAsync(Guid? fiscalYearId = null);
    Task<BudgetRevisionDto> GetRevisionAsync(Guid id);
    Task<BudgetRevisionDto> CreateRevisionAsync(CreateBudgetRevisionDto dto);
    Task<BudgetRevisionDto> UpdateRevisionAsync(Guid id, UpdateBudgetRevisionDto dto);
    Task<BudgetRevisionDto> SubmitRevisionAsync(Guid id, string rowVersion);
    Task<BudgetRevisionDto> ApplyRevisionAsync(Guid id, string rowVersion);

    Task<FinanceBudgetReconciliationReportDto> GetBudgetReconciliationAsync(
        CancellationToken cancellationToken = default);

    // Analytics
    Task<BudgetSummaryDto> GetScenarioSummaryAsync(Guid scenarioId);
}
