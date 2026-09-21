using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

public interface IPreEmploymentCheckService
{
    // Queries
    Task<PreEmploymentCheckDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PreEmploymentCheckDto?> GetByOfferIdAsync(Guid offerId, CancellationToken cancellationToken = default);
    Task<PreEmploymentCheckDetailDto> GetWithItemsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PreEmploymentCheckDto>> GetByStatusAsync(PreEmploymentCheckStatus status, CancellationToken cancellationToken = default);

    // CRUD
    Task<PreEmploymentCheckDto> CreateAsync(CreatePreEmploymentCheckDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    // Check items
    Task<IEnumerable<PreEmploymentCheckItemDto>> GetItemsAsync(Guid checkId, CancellationToken cancellationToken = default);
    Task<PreEmploymentCheckItemDto> AddItemAsync(CreatePreEmploymentCheckItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<PreEmploymentCheckItemDto> UpdateItemAsync(UpdatePreEmploymentCheckItemDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PreEmploymentCheckItemDto>> GetBlockingFailuresAsync(Guid checkId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PreEmploymentCheckItemDto>> GetItemsByStatusAsync(CheckItemStatus status, Guid? checkId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<PreEmploymentCheckItemDto>> GetMandatoryItemsAsync(Guid checkId, CancellationToken cancellationToken = default);

    // Reference check responses
    Task<ReferenceCheckResponseDto> AddReferenceResponseAsync(CreateReferenceCheckResponseDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ReferenceCheckResponseDto>> GetReferenceResponsesAsync(Guid checkItemId, CancellationToken cancellationToken = default);
    Task<ReferenceCheckResponseDto> UpdateReferenceResponseAsync(UpdateReferenceCheckResponseDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteReferenceResponseAsync(Guid referenceResponseId, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> CompleteCheckAsync(Guid checkId, Guid completedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ReferenceCheckResponseDto>> GetReferenceResponsesByRefereeAsync(Guid refereeId, CancellationToken cancellationToken = default);

    // Providers (round 3, lane G; D-14): which suppliers provide which checks
    Task<IEnumerable<PreEmploymentCheckProviderServiceDto>> GetProviderServicesAsync(PreEmploymentCheckType? checkType = null, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<PreEmploymentCheckProviderServiceDto>> AddProviderServicesAsync(CreatePreEmploymentCheckProviderServicesDto dto, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveProviderServiceAsync(Guid id, CancellationToken cancellationToken = default);

    // Template application
    Task<PreEmploymentCheckDetailDto> ApplyTemplateAsync(Guid checkId, Guid templateId, Guid appliedByUserId, bool overwriteExisting = false, CancellationToken ct = default);

    // ── Evidence documents ────────────────────────────────────────────────────
    //
    // ⚠ These take the ids the controlled-upload gate produced, never a caller-supplied path. The
    // `DocumentPath` fields that used to be set straight from the request payload are legacy and
    // read-only now.

    Task<PreEmploymentCheckItemDto> RecordItemDocumentAsync(
        Guid itemId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, CancellationToken cancellationToken = default);

    Task<ReferenceCheckResponseDto> RecordReferenceDocumentAsync(
        Guid responseId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string? fileName, CancellationToken cancellationToken = default);

    Task<PreEmploymentDocumentHandleDto?> GetItemDocumentHandleAsync(
        Guid itemId, CancellationToken cancellationToken = default);

    Task<PreEmploymentDocumentHandleDto?> GetReferenceDocumentHandleAsync(
        Guid responseId, CancellationToken cancellationToken = default);
}



// ============================================================================
// PRE-EMPLOYMENT CHECK TEMPLATE SERVICE
// ============================================================================

public interface IPreEmploymentCheckTemplateService
{
    Task<IEnumerable<PreEmploymentCheckTemplateDto>> GetAllAsync(CancellationToken ct = default);
    Task<PreEmploymentCheckTemplateDetailDto> GetWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<PreEmploymentCheckTemplateDetailDto> CreateAsync(CreatePreEmploymentCheckTemplateDto dto, Guid tenantId, Guid createdByUserId, CancellationToken ct = default);
    Task<PreEmploymentCheckTemplateDto> UpdateAsync(UpdatePreEmploymentCheckTemplateDto dto, Guid updatedByUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<PreEmploymentCheckTemplateItemDto> AddItemAsync(CreatePreEmploymentCheckTemplateItemDto dto, Guid tenantId, Guid createdByUserId, CancellationToken ct = default);
    Task<PreEmploymentCheckTemplateItemDto> UpdateItemAsync(UpdatePreEmploymentCheckTemplateItemDto dto, Guid updatedByUserId, CancellationToken ct = default);

    /// <summary>
    /// ⚠ Takes the owning template id as well as the item id. The previous signature took the item
    /// id alone, and the implementation found it by scanning every tenant's templates.
    /// </summary>
    Task<bool> DeleteItemAsync(Guid templateId, Guid itemId, CancellationToken ct = default);
}
