using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

public interface IUnionService
{
    Task<IEnumerable<UnionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<UnionDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<UnionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UnionDto> CreateAsync(CreateUnionDto createDto, CancellationToken cancellationToken = default);
    Task<UnionDto> UpdateAsync(UpdateUnionDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Collective bargaining agreements
    Task<CollectiveBargainingAgreementDto> AddAgreementAsync(CreateCollectiveBargainingAgreementDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<CollectiveBargainingAgreementDto>> GetAgreementsAsync(Guid unionId, CancellationToken cancellationToken = default);
    Task<CollectiveBargainingAgreementDto> UpdateAgreementAsync(UpdateCollectiveBargainingAgreementDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAgreementAsync(Guid agreementId, CancellationToken cancellationToken = default);

    // Contacts (round 3, lane U; D-8)
    Task<IEnumerable<UnionContactDto>> GetContactsAsync(Guid unionId, CancellationToken cancellationToken = default);
    Task<UnionContactDto> AddContactAsync(Guid unionId, CreateUnionContactDto dto, CancellationToken cancellationToken = default);
    Task<UnionContactDto> UpdateContactAsync(UpdateUnionContactDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default);

    // Documents (round 3, lane U) — the file arrives through the controlled-upload gate; the ids are the gate's.
    Task<IEnumerable<UnionDocumentDto>> GetDocumentsAsync(Guid unionId, CancellationToken cancellationToken = default);
    Task<UnionDocumentDto> AddDocumentAsync(Guid unionId, Guid? agreementId, ErpSystem.Core.Enums.UnionDocumentKind kind, Guid uploadedById, string fileName, long? fileSize, string? description, CancellationToken cancellationToken = default, Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null);
    Task<ErpSystem.Core.Entities.HR.UnionDocument?> GetDocumentForDownloadAsync(Guid unionId, Guid documentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    // Logo (round 3, lane U) — a gated image, never a URL.
    Task<ErpSystem.Core.Entities.HR.Union?> GetUnionForLogoAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UnionDto> AttachLogoAsync(Guid id, Guid? fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId, string? fileName, string? mimeType, long? fileSizeBytes, CancellationToken cancellationToken = default);
}
