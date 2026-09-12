using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// Slice 16 — SHE controlled document register (FR-SHE-246 version control,
// FR-SHE-170 filing and retrieval). Files ride the shared controlled-upload
// gate and the central DMS; this service owns the register semantics only.
// ============================================================================

public interface ISheControlledDocumentService
{
    Task<IEnumerable<SheControlledDocumentSummaryDto>> GetAllAsync(
        SheControlledDocumentCategory? category = null,
        SheControlledDocumentStatus? status = null,
        string? search = null,
        int? dueForReviewInDays = null,
        CancellationToken cancellationToken = default);

    Task<SheControlledDocumentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SheControlledDocumentDto> CreateAsync(CreateSheControlledDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheControlledDocumentDto> UpdateAsync(UpdateSheControlledDocumentDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Refused once any version exists — controlled documents are history, not rows to tidy.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new revision: pushes the bytes through the controlled-upload
    /// gate, then registers the document in the central DMS (first version) or
    /// appends the next version to its existing DMS record. The stored upload
    /// is removed again if the register write fails.
    /// </summary>
    Task<SheControlledDocumentDto> UploadVersionAsync(
        SheControlledDocumentVersionUpload upload, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Resolves a version row to its download identifiers, proving it belongs to the document.</summary>
    Task<SheControlledDocumentVersionFile> GetVersionFileAsync(
        Guid documentId, Guid versionId, CancellationToken cancellationToken = default);

    Task<SheControlledDocumentDto> ActivateAsync(ActivateSheControlledDocumentDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<SheControlledDocumentDto> StartReviewAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<SheControlledDocumentDto> ArchiveAsync(ArchiveSheControlledDocumentDto dto, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The version-upload request. Carries an open-stream factory rather than a
/// framework file type so the service stays out of the HTTP layer; the gate
/// opens the stream up to three times (scan, checksum, store).
/// </summary>
public sealed class SheControlledDocumentVersionUpload
{
    public required Guid DocumentId { get; init; }
    public required Guid ActorUserId { get; init; }
    public string? ActorName { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
    public required Func<Stream> OpenReadStream { get; init; }
    public string? ChangeSummary { get; init; }
}

/// <summary>What the download endpoint needs to serve a version's bytes.</summary>
public sealed class SheControlledDocumentVersionFile
{
    public required Guid DocumentRecordId { get; init; }
    public required Guid VersionId { get; init; }
    public Guid? FileUploadRecordId { get; init; }
    public required string FileName { get; init; }
    public string? ContentType { get; init; }
}
