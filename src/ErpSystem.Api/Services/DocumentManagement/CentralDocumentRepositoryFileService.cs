using System.Text.Json;
using ErpSystem.Core.Entities;
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
        var sourceModule = Required(
            request.SourceModule, nameof(request.SourceModule), 120);
        var accessProfile = Required(
            request.AccessProfile, nameof(request.AccessProfile), 120);
        var templateCode = Trim(request.MetadataTemplateCode, 80);
        ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate? template = null;

        if (request.RequirePublishedGovernance)
        {
            if (string.IsNullOrWhiteSpace(templateCode))
            {
                throw new InvalidOperationException(
                    "A published Central DMS metadata template is required for this document.");
            }

            template = await _db.CentralDocumentMetadataTemplates
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                        item.TenantId == request.TenantId &&
                        item.TemplateCode == templateCode &&
                        item.IsActive &&
                        item.PublishedAt.HasValue &&
                        !item.IsDeleted,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Published Central DMS metadata template '{templateCode}' is unavailable for this tenant.");

            if (!string.Equals(template.Module, sourceModule, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(template.DocumentType, documentType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The Central DMS metadata template does not match the source module and document type.");
            }

            if (!string.Equals(template.AccessProfile, accessProfile, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The Central DMS access profile must match the published metadata template.");
            }

            var hasAccessRules = await _db.CentralDocumentAccessRules
                .AsNoTracking()
                .AnyAsync(item =>
                        item.TenantId == request.TenantId &&
                        item.AccessProfile == accessProfile &&
                        item.IsActive &&
                        item.CanView &&
                        !item.IsDeleted &&
                        (item.Module == null || item.Module == sourceModule) &&
                        (!string.IsNullOrWhiteSpace(item.RoleName) ||
                         !string.IsNullOrWhiteSpace(item.PermissionKey)),
                    cancellationToken);
            if (!hasAccessRules)
            {
                throw new InvalidOperationException(
                    $"Central DMS access profile '{accessProfile}' has no active view rule.");
            }

            var hasRetentionPolicy = await _db.CentralDocumentRetentionPolicies
                .AsNoTracking()
                .AnyAsync(item =>
                        item.TenantId == request.TenantId &&
                        item.IsActive &&
                        !item.IsDeleted &&
                        (item.Module == null || item.Module == sourceModule) &&
                        (item.DocumentType == null || item.DocumentType == documentType),
                    cancellationToken);
            if (!hasRetentionPolicy)
            {
                throw new InvalidOperationException(
                    $"No active Central DMS retention policy covers '{sourceModule}/{documentType}'.");
            }

            ValidateRequiredMetadata(template, request.MetadataValues);
        }

        var duplicateMetadataKeys = request.MetadataValues
            .Select(value => NormalizeMetadataField(
                string.IsNullOrWhiteSpace(value.FieldKey) ? value.FieldLabel : value.FieldKey))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateMetadataKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Central DMS metadata contains duplicate fields: {string.Join(", ", duplicateMetadataKeys)}.");
        }

        var actorName = string.IsNullOrWhiteSpace(request.ActorName)
            ? request.ActorUserId.ToString()
            : request.ActorName.Trim();
        var documentReference = BuildDocumentReference(
            sourceModule, now);

        var record = new CentralDocumentRecord
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            DocumentReference = documentReference,
            Title = Required(request.Title, nameof(request.Title), 250),
            SourceModule = sourceModule,
            SourceLabel = Required(
                request.SourceLabel, nameof(request.SourceLabel), 500),
            SourceEntityType = Required(
                request.SourceEntityType, nameof(request.SourceEntityType), 150),
            SourceRecordId = request.SourceRecordId,
            SourceRecordReference = Trim(request.SourceRecordReference, 180),
            MetadataTemplateCode = templateCode,
            RepositoryStatus = "Linked",
            RepositoryPath = upload.FilePath,
            CurrentVersion = Required(
                request.VersionNumber, nameof(request.VersionNumber), 120),
            VersionStatus = Required(
                request.VersionStatus, nameof(request.VersionStatus), 80),
            AnnotationStatus = "PDF rendition required",
            CommentStatus = "No comments",
            AccessProfile = accessProfile,
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

        foreach (var value in request.MetadataValues
                     .Where(value => !string.IsNullOrWhiteSpace(value.FieldLabel)))
        {
            var fieldLabel = Required(value.FieldLabel, nameof(value.FieldLabel), 200);
            var fieldKey = NormalizeMetadataField(
                string.IsNullOrWhiteSpace(value.FieldKey) ? fieldLabel : value.FieldKey);
            if (string.IsNullOrWhiteSpace(fieldKey))
            {
                continue;
            }

            _db.CentralDocumentMetadataValues.Add(new CentralDocumentMetadataValue
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                DocumentRecordId = record.Id,
                TemplateCode = templateCode,
                FieldKey = fieldKey,
                FieldLabel = fieldLabel,
                FieldValue = Trim(value.FieldValue, 2000),
                ValueType = Required(value.ValueType, nameof(value.ValueType), 50),
                Source = sourceModule,
                CapturedAt = now,
                CapturedById = request.ActorUserId,
                CreatedAt = now,
                CreatedBy = actorName,
                CreatedById = request.ActorUserId
            });
        }

        var actorExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.ActorUserId, cancellationToken);
        if (actorExists)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                UserId = request.ActorUserId,
                Username = actorName,
                Action = "CentralDmsDocumentRegistered",
                Resource = request.SourceEntityType,
                ResourceId = request.SourceRecordId.ToString(),
                NewValues = JsonSerializer.Serialize(new
                {
                    record.Id,
                    versionId = version.Id,
                    uploadId = upload.Id,
                    record.DocumentReference,
                    sourceModule,
                    documentType,
                    templateCode,
                    accessProfile
                }),
                IpAddress = "system",
                Timestamp = now
            });
        }

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

    private static void ValidateRequiredMetadata(
        ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate template,
        IReadOnlyList<CentralDocumentMetadataRegistrationValue> values)
    {
        string[] requiredFields;
        try
        {
            requiredFields = JsonSerializer.Deserialize<string[]>(template.RequiredFieldsJson) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Central DMS metadata template '{template.TemplateCode}' is invalid.", exception);
        }

        var supplied = values
            .Where(value => !string.IsNullOrWhiteSpace(value.FieldValue))
            .SelectMany(value => new[]
            {
                NormalizeMetadataField(value.FieldKey),
                NormalizeMetadataField(value.FieldLabel)
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = requiredFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Where(field => !supplied.Contains(NormalizeMetadataField(field)))
            .Select(field => field.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Central DMS metadata is incomplete. Missing: {string.Join(", ", missing)}.");
        }
    }

    private static string NormalizeMetadataField(string? value) =>
        new((value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}
