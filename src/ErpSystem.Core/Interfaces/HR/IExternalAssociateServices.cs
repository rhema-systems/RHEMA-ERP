using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Service interface for ExternalAssociate operations.
/// </summary>
public interface IExternalAssociateService
{
    // ── Queries ───────────────────────────────────────────────────────────────
    Task<ExternalAssociateDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ExternalAssociateDto?> GetByAssociateNumberAsync(string associateNumber, CancellationToken ct = default);
    Task<IEnumerable<ExternalAssociateSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<ExternalAssociateSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm = null, CancellationToken ct = default);
    Task<IEnumerable<ExternalAssociateSummaryDto>> GetActiveAsync(CancellationToken ct = default);
    Task<IEnumerable<ExternalAssociateSearchResultDto>> SearchAsync(string q, int limit = 20, CancellationToken ct = default);

    // ── CRUD ──────────────────────────────────────────────────────────────────
    Task<ExternalAssociateDto> CreateAsync(CreateExternalAssociateDto dto, Guid tenantId, Guid createdByUserId, CancellationToken ct = default);
    Task<ExternalAssociateDto> UpdateAsync(UpdateExternalAssociateDto dto, Guid updatedByUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    // ── Workflow ──────────────────────────────────────────────────────────────
    Task<ExternalAssociateDto> ActivateAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ExternalAssociateDto> DeactivateAsync(Guid id, Guid userId, CancellationToken ct = default);
}
