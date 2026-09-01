using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Service interface for ExternalAssociate operations.
/// </summary>
public interface IExternalAssociateService
{
    // ── Queries ───────────────────────────────────────────────────────────────
    Task<ExternalAssociateDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Throws <c>ArgumentException</c> when no associate carries the number — it used to answer a null.</summary>
    Task<ExternalAssociateDto> GetByAssociateNumberAsync(string associateNumber, CancellationToken ct = default);
    Task<IEnumerable<ExternalAssociateSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<ExternalAssociateSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm = null, bool? isActive = null, CancellationToken ct = default);
    Task<IEnumerable<ExternalAssociateSummaryDto>> GetActiveAsync(CancellationToken ct = default);
    Task<IEnumerable<ExternalAssociateSearchResultDto>> SearchAsync(string q, int limit = 20, CancellationToken ct = default);

    // ── CRUD ──────────────────────────────────────────────────────────────────
    Task<ExternalAssociateDto> CreateAsync(CreateExternalAssociateDto dto, Guid tenantId, Guid createdByUserId, CancellationToken ct = default);
    Task<ExternalAssociateDto> UpdateAsync(UpdateExternalAssociateDto dto, Guid updatedByUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    // ── Workflow ──────────────────────────────────────────────────────────────
    Task<ExternalAssociateDto> ActivateAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ExternalAssociateDto> DeactivateAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The associate as an entity, for the photo endpoints to read and stamp.</summary>
    Task<ExternalAssociate?> GetEntityForPhotoAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Records the controlled upload that now holds this associate's photograph.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>PicturePath</c> is deliberately NOT cleared — it is the ported location and may be the
    /// only copy of an older image, and the download prefers the gated record and falls back to it.
    /// </remarks>
    Task<ExternalAssociate> AttachPhotoAsync(Guid id, Guid fileUploadRecordId, CancellationToken ct = default);
}
