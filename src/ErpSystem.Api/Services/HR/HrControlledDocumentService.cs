using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Single entry point for HR document uploads: pushes the bytes through the shared
/// controlled-upload gate (policy, quotas, malware scan) and, for everything worth
/// cataloguing, registers the result in the central DMS.
/// </summary>
/// <remarks>
/// HR has eight upload sites. Without this they would each hand-roll the
/// upload → register → compensate-on-failure sequence, and any one of them getting it
/// subtly wrong reintroduces exactly the class of bug this work exists to remove.
/// </remarks>
public interface IHrControlledDocumentService
{
    /// <summary>
    /// Stores and scans the file, then optionally registers it in the central DMS.
    /// </summary>
    /// <exception cref="ControlledFileUploadException">
    /// The upload was rejected — oversized, wrong type, over quota, infected, or the
    /// scanner could not be reached. Carries the HTTP status the caller should return.
    /// </exception>
    Task<HrControlledDocument> UploadAsync(
        HrDocumentUploadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Undoes a successful <see cref="UploadAsync"/> when the caller then fails to persist
    /// the owning domain row. Best-effort: never throws, because it runs on a failure path
    /// where the original exception is the one worth surfacing.
    /// </summary>
    Task RollbackAsync(
        HrControlledDocument document,
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class HrDocumentUploadRequest
{
    public required Guid TenantId { get; init; }
    public required Guid ActorUserId { get; init; }
    public string? ActorName { get; init; }

    /// <summary>One of the <c>hr-</c> constants on <c>ControlledFileUploadCategories</c>.</summary>
    public required string Category { get; init; }

    public required IFormFile File { get; init; }

    /// <summary>
    /// Central-DMS registration details, or null to store the file without cataloguing it.
    /// Null is right for candidate profile photos: an avatar carries no retention value and
    /// one document record per photo is repository noise.
    /// </summary>
    public HrDocumentDmsRegistration? Registration { get; init; }
}

public sealed class HrDocumentDmsRegistration
{
    public required string SourceLabel { get; init; }
    public required string SourceEntityType { get; init; }

    /// <summary>The owning domain record. Must already exist when the upload is registered.</summary>
    public required Guid SourceRecordId { get; init; }

    public string? SourceRecordReference { get; init; }
    public required string Title { get; init; }
    public required string DocumentType { get; init; }

    /// <summary>
    /// Governs who can reach the document through the DMS UI. HR documents are personal
    /// data, so the default is the restricted profile rather than a module-wide one.
    /// </summary>
    public string AccessProfile { get; init; } = "HR restricted";

    public string? ChangeSummary { get; init; }
}

public sealed class HrControlledDocument
{
    public required Guid FileUploadRecordId { get; init; }
    public required string FilePath { get; init; }
    public required string OriginalFileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }

    /// <summary>Null when the upload was stored without DMS registration.</summary>
    public Guid? DocumentRecordId { get; init; }

    public Guid? DocumentVersionId { get; init; }
}

public sealed class HrControlledDocumentService : IHrControlledDocumentService
{
    /// <summary>Source module recorded on every HR document in the central DMS.</summary>
    private const string SourceModule = "HR";

    private readonly IControlledFileUploadService _controlledFiles;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ILogger<HrControlledDocumentService> _logger;

    public HrControlledDocumentService(
        IControlledFileUploadService controlledFiles,
        ICentralDocumentRepositoryFileService centralDocuments,
        ILogger<HrControlledDocumentService> logger)
    {
        _controlledFiles = controlledFiles;
        _centralDocuments = centralDocuments;
        _logger = logger;
    }

    public async Task<HrControlledDocument> UploadAsync(
        HrDocumentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var file = request.File;

        var upload = await _controlledFiles.UploadAsync(
            new ControlledFileUploadRequest
            {
                TenantId = request.TenantId,
                ActorUserId = request.ActorUserId,
                ActorName = request.ActorName,
                Category = request.Category,
                // Strip any directory component a client may have sent.
                FileName = Path.GetFileName(file.FileName),
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                FileSize = file.Length,
                // The gate opens this three times — scan, checksum, store. IFormFile
                // returns a fresh stream per call, which is exactly the contract it needs.
                OpenReadStream = file.OpenReadStream
            },
            cancellationToken);

        if (request.Registration is null)
        {
            return Describe(upload.Record, documentRecordId: null, documentVersionId: null);
        }

        try
        {
            var link = await _centralDocuments.RegisterAsync(
                new CentralDocumentRepositoryRegistration
                {
                    TenantId = request.TenantId,
                    ActorUserId = request.ActorUserId,
                    ActorName = request.ActorName,
                    FileUploadRecordId = upload.Record.Id,
                    SourceModule = SourceModule,
                    SourceLabel = request.Registration.SourceLabel,
                    SourceEntityType = request.Registration.SourceEntityType,
                    SourceRecordId = request.Registration.SourceRecordId,
                    SourceRecordReference = request.Registration.SourceRecordReference,
                    Title = request.Registration.Title,
                    DocumentType = request.Registration.DocumentType,
                    AccessProfile = request.Registration.AccessProfile,
                    ChangeSummary = request.Registration.ChangeSummary
                },
                cancellationToken);

            return Describe(upload.Record, link.DocumentRecordId, link.DocumentVersionId);
        }
        catch
        {
            // The bytes are already stored. Leaving them behind would orphan a
            // FileUploadRecord that nothing references and no cleanup pass would find.
            await SafeDeleteUploadAsync(
                request.TenantId, upload.Record.Id, request.ActorUserId, cancellationToken);
            throw;
        }
    }

    public async Task RollbackAsync(
        HrControlledDocument document,
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (document.DocumentRecordId is Guid recordId)
            {
                // Cascades to the underlying controlled upload.
                await _centralDocuments.DeleteAsync(
                    tenantId, recordId, actorUserId, cancellationToken);
                return;
            }

            await _controlledFiles.DeleteAsync(
                tenantId, document.FileUploadRecordId, actorUserId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to roll back HR document upload {UploadId} for tenant {TenantId}. " +
                "The stored file may be orphaned and needs manual review.",
                document.FileUploadRecordId, tenantId);
        }
    }

    private async Task SafeDeleteUploadAsync(
        Guid tenantId, Guid uploadId, Guid actorUserId, CancellationToken cancellationToken)
    {
        try
        {
            await _controlledFiles.DeleteAsync(
                tenantId, uploadId, actorUserId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to remove controlled upload {UploadId} after DMS registration failed " +
                "for tenant {TenantId}.",
                uploadId, tenantId);
        }
    }

    private static HrControlledDocument Describe(
        Core.Entities.FileUploadRecord record,
        Guid? documentRecordId,
        Guid? documentVersionId) => new()
    {
        FileUploadRecordId = record.Id,
        FilePath = record.FilePath,
        OriginalFileName = record.OriginalFileName,
        ContentType = string.IsNullOrWhiteSpace(record.ContentType)
            ? "application/octet-stream"
            : record.ContentType,
        FileSize = record.FileSize,
        DocumentRecordId = documentRecordId,
        DocumentVersionId = documentVersionId
    };
}
