using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.DocumentManagement;

public sealed class CentralDocumentRepositoryFileService : ICentralDocumentRepositoryFileService
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IControlledFileUploadService _controlledFiles;

    public CentralDocumentRepositoryFileService(
        ApplicationDbContext db,
        IFileStorageService storage,
        IControlledFileUploadService controlledFiles)
    {
        _db = db;
        _storage = storage;
        _controlledFiles = controlledFiles;
    }

    public async Task<CentralDocumentRepositoryLink> RegisterAsync(
        CentralDocumentRepositoryRegistration request,
        CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty ||
            request.ActorUserId == Guid.Empty ||
            request.FileUploadRecordId == Guid.Empty ||
            request.SourceRecordId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant, actor, upload, and source-record identifiers are required.");
        }

        var upload = await _db.FileUploadRecords
            .SingleOrDefaultAsync(item =>
                    item.Id == request.FileUploadRecordId &&
                    item.TenantId == request.TenantId &&
                    !item.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The controlled upload was not found for this tenant.");

        if (upload.VirusScanStatus != FileVirusScanStatus.Clean)
        {
            throw new InvalidOperationException(
                "Only a clean controlled upload can be registered in the central DMS.");
        }

        var alreadyLinked = await _db.CentralDocumentVersions
            .AnyAsync(item =>
                    item.TenantId == request.TenantId &&
                    item.FileUploadRecordId == upload.Id &&
                    !item.IsDeleted,
                cancellationToken);
        if (alreadyLinked)
        {
            throw new InvalidOperationException(
                "The controlled upload is already registered in the central DMS.");
        }

        var now = DateTime.UtcNow;
        var documentType = Required(
            request.DocumentType, nameof(request.DocumentType), 120);
        var actorName = string.IsNullOrWhiteSpace(request.ActorName)
            ? request.ActorUserId.ToString()
            : request.ActorName.Trim();
        var documentReference = BuildDocumentReference(
            request.SourceModule, now);

        var record = new CentralDocumentRecord
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            DocumentReference = documentReference,
            Title = Required(request.Title, nameof(request.Title), 250),
            SourceModule = Required(
                request.SourceModule, nameof(request.SourceModule), 120),
            SourceLabel = Required(
                request.SourceLabel, nameof(request.SourceLabel), 500),
            SourceEntityType = Required(
                request.SourceEntityType, nameof(request.SourceEntityType), 150),
            SourceRecordId = request.SourceRecordId,
            SourceRecordReference = Trim(request.SourceRecordReference, 180),
            MetadataTemplateCode = Trim(request.MetadataTemplateCode, 80),
            RepositoryStatus = "Linked",
            RepositoryPath = upload.FilePath,
            CurrentVersion = Required(
                request.VersionNumber, nameof(request.VersionNumber), 120),
            VersionStatus = Required(
                request.VersionStatus, nameof(request.VersionStatus), 80),
            AnnotationStatus = "PDF rendition required",
            CommentStatus = "No comments",
            AccessProfile = Required(
                request.AccessProfile, nameof(request.AccessProfile), 120),
            RetentionStatus = "Current",
            LifecycleStatus = "Active",
            Notes = Trim(
                string.IsNullOrWhiteSpace(request.Notes)
                    ? $"Document type: {documentType}"
                    : $"Document type: {documentType}. {request.Notes}",
                1000),
            CreatedAt = now,
            CreatedBy = actorName,
            CreatedById = request.ActorUserId
        };

        var version = new CentralDocumentVersion
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            DocumentRecordId = record.Id,
            VersionNumber = record.CurrentVersion,
            Status = record.VersionStatus,
            RepositoryPath = upload.FilePath,
            FileName = upload.OriginalFileName,
            ContentType = string.IsNullOrWhiteSpace(upload.ContentType)
                ? "application/octet-stream"
                : upload.ContentType,
            FileSize = upload.FileSize,
            FileUploadRecordId = upload.Id,
            ChangeSummary = Trim(request.ChangeSummary, 1000),
            CreatedByUserId = request.ActorUserId,
            CreatedAt = now,
            CreatedBy = actorName,
            CreatedById = request.ActorUserId
        };

        _db.CentralDocumentRecords.Add(record);
        _db.CentralDocumentVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);

        return new CentralDocumentRepositoryLink
        {
            DocumentRecordId = record.Id,
            DocumentVersionId = version.Id,
            FileUploadRecordId = upload.Id,
            DocumentReference = record.DocumentReference,
            VersionNumber = version.VersionNumber
        };
    }

    public async Task<CentralDocumentRepositoryContent?> OpenAsync(
        Guid tenantId,
        Guid documentRecordId,
        Guid documentVersionId,
        CancellationToken cancellationToken = default)
    {
        var activeRecord = await _db.CentralDocumentRecords
            .AsNoTracking()
            .AnyAsync(item =>
                    item.Id == documentRecordId &&
                    item.TenantId == tenantId &&
                    !item.IsDeleted,
                cancellationToken);
        if (!activeRecord)
        {
            return null;
        }

        var version = await _db.CentralDocumentVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                    item.Id == documentVersionId &&
                    item.DocumentRecordId == documentRecordId &&
                    item.TenantId == tenantId &&
                    !item.IsDeleted,
                cancellationToken);
        if (version?.FileUploadRecordId is not Guid uploadId)
        {
            return null;
        }

        var upload = await _db.FileUploadRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                    item.Id == uploadId &&
                    item.TenantId == tenantId &&
                    !item.IsDeleted,
                cancellationToken);
        if (upload is null ||
            !StoragePathsEqual(upload.FilePath, version.RepositoryPath))
        {
            return null;
        }

        var stream = await _storage.DownloadFileAsync(
            upload.FilePath, upload.Id);
        return new CentralDocumentRepositoryContent
        {
            Content = stream,
            FileName = upload.OriginalFileName,
            ContentType = string.IsNullOrWhiteSpace(upload.ContentType)
                ? "application/octet-stream"
                : upload.ContentType,
            FileSize = upload.FileSize,
            UploadRecord = upload
        };
    }

    public async Task DeleteAsync(
        Guid tenantId,
        Guid documentRecordId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.CentralDocumentRecords
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item =>
                    item.Id == documentRecordId &&
                    item.TenantId == tenantId,
                cancellationToken);
        if (record is null)
        {
            return;
        }

        var versions = await _db.CentralDocumentVersions
            .IgnoreQueryFilters()
            .Where(item =>
                item.DocumentRecordId == documentRecordId &&
                item.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var uploadIds = versions
            .Where(item => item.FileUploadRecordId.HasValue)
            .Select(item => item.FileUploadRecordId!.Value)
            .Distinct()
            .ToList();

        var now = DateTime.UtcNow;
        if (!record.IsDeleted)
        {
            record.IsDeleted = true;
            record.DeletedAt = now;
            record.DeletedBy = actorUserId.ToString();
            record.UpdatedAt = now;
            record.LastModifiedById = actorUserId;
        }

        foreach (var version in versions.Where(item => !item.IsDeleted))
        {
            version.IsDeleted = true;
            version.DeletedAt = now;
            version.DeletedBy = actorUserId.ToString();
            version.UpdatedAt = now;
            version.LastModifiedById = actorUserId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        foreach (var uploadId in uploadIds)
        {
            await _controlledFiles.DeleteAsync(
                tenantId, uploadId, actorUserId, cancellationToken);
        }
    }

    private static string BuildDocumentReference(
        string sourceModule,
        DateTime now)
    {
        var normalized = new string(sourceModule
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .Take(12)
            .ToArray());
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "MODULE";
        }

        return $"DMS-{normalized}-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
    }

    private static string Required(
        string? value,
        string parameterName,
        int maxLength)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException(
                $"{parameterName} is required.", parameterName);
        }

        return result.Length <= maxLength
            ? result
            : result[..maxLength];
    }

    private static string? Trim(string? value, int maxLength)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        return result.Length <= maxLength
            ? result
            : result[..maxLength];
    }

    private static bool StoragePathsEqual(string? first, string? second) =>
        string.Equals(
            first?.Replace('\\', '/').TrimStart('/'),
            second?.Replace('\\', '/').TrimStart('/'),
            StringComparison.OrdinalIgnoreCase);
}
