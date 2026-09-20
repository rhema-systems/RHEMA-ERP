using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface ICapitalProjectService
{
    Task<IEnumerable<CapitalProjectListDto>> GetAllAsync();
    Task<CapitalProjectDetailDto?> GetByIdAsync(Guid id);
    Task<CapitalProjectDetailDto> CreateAsync(CreateCapitalProjectDto dto);
    Task<CapitalProjectDetailDto> UpdateAsync(Guid id, UpdateCapitalProjectDto dto);
    Task DeleteAsync(Guid id);

    // Status management
    Task<CapitalProjectDetailDto> UpdateStatusAsync(Guid id, UpdateProjectStatusDto dto);

    // Cost line management (tracking only — no GL posting)
    Task<CapitalProjectDetailDto> PostCostToProjectAsync(Guid projectId, AddProjectCostDto dto);
    Task<CapitalProjectDetailDto> RemoveCostFromProjectAsync(Guid projectId, Guid costLineId);

    // Settlement rules
    Task<CapitalProjectDetailDto> AddSettlementRuleAsync(Guid projectId, AddSettlementRuleDto dto);
    Task<CapitalProjectDetailDto> RemoveSettlementRuleAsync(Guid projectId, Guid ruleId);

    // Capitalization — the key operation: AUC → Fixed Assets + GL journal
    Task<CapitalProjectDetailDto> CapitalizeProjectAsync(
        Guid projectId,
        CapitalizeCapitalProjectDto? dto = null,
        CancellationToken cancellationToken = default);
}
