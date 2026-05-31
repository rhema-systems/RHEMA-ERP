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
    Task<BudgetScenarioDto> LockScenarioAsync(Guid id);

    // Returns (Worksheets/Proposals)
    Task<BudgetReturnDto> CreateReturnAsync(CreateBudgetReturnDto dto);
    Task<BudgetReturnDto> GetReturnAsync(Guid id);
    Task<IEnumerable<BudgetReturnDto>> GetReturnsForScenarioAsync(Guid scenarioId);
    Task<BudgetReturnDto> SubmitReturnAsync(Guid id);
    Task<BudgetReturnDto> ApproveReturnAsync(Guid id, Guid approverId);
    Task<BudgetReturnDto> RejectReturnAsync(Guid id, string reason, Guid rejectorId);

    // Entries
    Task<IEnumerable<BudgetEntryDto>> GetEntriesAsync(Guid returnId);
    Task BulkSaveEntriesAsync(BulkSaveBudgetEntriesDto dto);
    Task<IEnumerable<BudgetEntryDto>> GetConsolidatedBudgetAsync(Guid scenarioId, Guid? accountId = null);
}
