using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for Opportunity/Deal management — the pipeline tracking engine.
/// Handles the tenant-configured opportunity lifecycle and its closed outcomes.
/// </summary>
public interface IOpportunityService
{
    // ── CRUD ─────────────────────────────────────────────────────────────

    Task<OpportunityDetailDto> CreateAsync(CreateOpportunityDto dto);
    Task<OpportunityDetailDto> UpdateAsync(Guid id, UpdateOpportunityDto dto);
    Task<OpportunityDetailDto?> GetByIdAsync(Guid id);
    Task<PagedResult<OpportunitySummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null,
        string? stage = null,
        Guid? customerId = null,
        Guid? assignedToId = null,
        string? opportunityType = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    // ── Pipeline Stage Transitions ───────────────────────────────────────

    /// <summary>
    /// Advance opportunity to the next pipeline stage
    /// </summary>
    Task<OpportunityDetailDto> AdvanceStageAsync(Guid id, string newStage, string? notes = null);

    /// <summary>
    /// Close the opportunity as Won
    /// </summary>
    Task<OpportunityDetailDto> CloseWonAsync(Guid id, string? notes = null);

    /// <summary>
    /// Close the opportunity as Lost
    /// </summary>
    Task<OpportunityDetailDto> CloseLostAsync(Guid id, CloseOpportunityDto dto);

    // ── Queries ──────────────────────────────────────────────────────────

    /// <summary>
    /// Get pipeline summary grouped by stage with totals
    /// </summary>
    Task<List<OpportunitySummaryDto>> GetPipelineAsync(Guid? assignedToId = null);
    Task<List<OpportunitySummaryDto>> GetByCustomerAsync(Guid customerId);
}
