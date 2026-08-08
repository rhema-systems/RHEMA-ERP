using System.Security.Cryptography;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services;

/// <summary>
/// Applies the shared tenant file-upload policy before any storage provider is
/// called, and persists the shared FileUploadRecord used by evidence controls.
/// </summary>
public sealed class ControlledFileUploadService : IControlledFileUploadService
{
    private static readonly HashSet<string> ActiveContentExtensions = new(
        [".svg", ".svgz"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ActiveContentMimeTypes = new(
        ["image/svg+xml"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> DefaultExtensions = new(
        [
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".rtf"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> DefaultMimeTypes = new(
        [
            "image/jpeg", "image/png", "image/gif", "image/bmp",
            "image/webp", "image/x-icon", "image/vnd.microsoft.icon",
            "application/pdf", "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/csv", "text/plain", "application/rtf"
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly FileUploadOptions _options;
    private readonly IFileStorageService _storage;
    private readonly IFileVirusScanService _virusScan;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ControlledFileUploadService> _logger;
    private readonly HashSet<string> _cleanScanRequiredCategories;

    public ControlledFileUploadService(
        IOptions<FileUploadOptions> options,
        IFileStorageService storage,
        IFileVirusScanService virusScan,
        ApplicationDbContext db,
        ILogger<ControlledFileUploadService> logger)
    {
        _options = options.Value;
        _storage = storage;
        _virusScan = virusScan;
        _db = db;
        _logger = logger;
        _cleanScanRequiredCategories = new HashSet<string>(
            ControlledFileUploadCategories.SystemCleanScanRequired,
            StringComparer.OrdinalIgnoreCase);
        foreach (var category in ParseCsv(
                     _options.RequiredCleanScanCategoriesCsv,
                     ensureDot: false))
        {
            _cleanScanRequiredCategories.Add(
                NormalizeCategory(category));
        }
    }

    public async Task<ControlledFileUploadResult> UploadAsync(
        ControlledFileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
            throw Failure("FILE_TENANT_REQUIRED", "Tenant context is required.", 400);
        if (request.ActorUserId == Guid.Empty)
            throw Failure("FILE_ACTOR_REQUIRED", "User context is required.", 400);
        if (request.FileSize <= 0)
            throw Failure("FILE_EMPTY", "The uploaded file is empty.", 400);

        var category = NormalizeCategory(request.Category);
        if (string.IsNullOrWhiteSpace(category))
            throw Failure("FILE_CATEGORY_REQUIRED", "File category is required.", 400);

        var safeName = Path.GetFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(safeName))
            throw Failure("FILE_NAME_INVALID", "The file name is invalid.", 400);

        var extension = Path.GetExtension(safeName);
        if (ActiveContentExtensions.Contains(extension) ||
            ActiveContentMimeTypes.Contains(request.ContentType))
            throw Failure(
                "FILE_ACTIVE_CONTENT_NOT_ALLOWED",
                "Active-content file formats cannot be uploaded to publicly served storage.",
                422);

        var policy = await GetEffectivePolicyAsync(
            request.TenantId, category, cancellationToken);
        if (!policy.IsEnabled)
            throw Failure("FILE_UPLOAD_DISABLED",
                "File uploads are disabled for this tenant and category.", 409);

        var maxFileSize = policy.MaxFileSizeBytes ?? _options.MaxFileSizeBytes;
        if (request.FileSize > maxFileSize)
            throw Failure("FILE_TOO_LARGE",
                $"File size exceeds the maximum allowed size of {maxFileSize} bytes.", 413);

        if (!policy.AllowedExtensions.Contains(extension))
            throw Failure("FILE_EXTENSION_NOT_ALLOWED",
                $"File type '{extension}' is not allowed.", 422);

        if (!string.IsNullOrWhiteSpace(request.ContentType) &&
            policy.AllowedMimeTypes.Count > 0 &&
            !policy.AllowedMimeTypes.Contains(request.ContentType))
            throw Failure("FILE_MIME_NOT_ALLOWED",
                $"File MIME type '{request.ContentType}' is not allowed.", 422);

        await EnforceQuotasAsync(
            request.TenantId, category, request.FileSize, policy, cancellationToken);

        var scanStatus = FileVirusScanStatus.Skipped;
        string? scanMessage = null;
        DateTime? scannedAtUtc = null;
        if (policy.RequireVirusScan)
        {
            await using var scanStream = request.OpenReadStream();
            FileVirusScanResult? scan;
            try
            {
                scan = await _virusScan.ScanAsync(new FileVirusScanRequest
                {
                    TenantId = request.TenantId,
                    Category = category,
                    FileName = safeName,
                    ContentType = request.ContentType,
                    FileSize = request.FileSize,
                    Content = scanStream
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Virus scanning failed for tenant {TenantId}, category {Category}.",
                    request.TenantId,
                    category);
                throw Failure(
                    "FILE_VIRUS_SCAN_INCOMPLETE",
                    "Upload rejected because a clean virus-scan result was not obtained.",
                    422);
            }

            scanStatus = scan?.Status ?? FileVirusScanStatus.Error;
            scanMessage = scan?.Message;
            scannedAtUtc = DateTime.UtcNow;
            if (scanStatus != FileVirusScanStatus.Clean)
                throw Failure(
                    scanStatus == FileVirusScanStatus.Infected
                        ? "FILE_VIRUS_DETECTED"
                        : "FILE_VIRUS_SCAN_INCOMPLETE",
                    scanStatus == FileVirusScanStatus.Infected
                        ? "Upload rejected because the file failed virus scanning."
                        : "Upload rejected because a clean virus-scan result was not obtained.",
                    422);
        }

        string checksum;
        await using (var checksumStream = request.OpenReadStream())
        {
            checksum = Convert.ToHexString(
                    await SHA256.HashDataAsync(checksumStream, cancellationToken))
                .ToLowerInvariant();
        }

        await using var uploadStream = request.OpenReadStream();
        FileStorageResult storageResult;
        try
        {
            storageResult = await _storage.UploadFileAsync(new FileUploadRequest
            {
                FileStream = uploadStream,
                FileName = safeName,
                ContentType = request.ContentType,
                FileSize = request.FileSize,
                Category = category,
                TenantId = request.TenantId.ToString(),
                OverwriteExisting = false,
                Metadata =
                {
                    ["checksumSha256"] = checksum,
                    ["uploadedByUserId"] = request.ActorUserId.ToString()
                }
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Storage upload failed for tenant {TenantId}, category {Category}.",
                request.TenantId,
                category);
            throw Failure(
                "FILE_STORAGE_FAILED",
                "The storage provider could not complete the upload.",
                502);
        }

        if (!storageResult.Success)
            throw Failure("FILE_STORAGE_FAILED",
                storageResult.ErrorMessage ?? "The storage provider rejected the upload.", 502);

        var record = new FileUploadRecord
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Category = category,
            FilePath = storageResult.FilePath,
            StoredFileName = storageResult.FileName,
            OriginalFileName = storageResult.OriginalFileName,
            ContentType = storageResult.ContentType,
            FileSize = storageResult.FileSize,
            StorageProvider = storageResult.StorageProvider,
            UploadedByUserId = request.ActorUserId,
            VirusScanStatus = scanStatus,
            ScannedAtUtc = scannedAtUtc,
            VirusScanMessage = scanMessage,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.ActorName,
            CreatedById = request.ActorUserId
        };

        try
        {
            _db.FileUploadRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await _storage.DeleteFileAsync(storageResult.FilePath);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(cleanupException,
                    "Failed to roll back stored file {FilePath} after metadata persistence failed.",
                    storageResult.FilePath);
            }

            throw;
        }

        return new ControlledFileUploadResult
        {
            Record = record,
            ChecksumSha256 = checksum,
            PublicUrl = storageResult.PublicUrl
        };
    }

    public async Task DeleteAsync(
        Guid tenantId,
        Guid fileUploadRecordId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.FileUploadRecords
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item =>
                    item.Id == fileUploadRecordId &&
                    item.TenantId == tenantId,
                cancellationToken)
            ?? throw Failure("FILE_RECORD_NOT_FOUND",
                "The tenant file record was not found.", 404);

        var isRegistrationEvidence = await _db
            .BusinessPartnerRegistrationDocuments
            .IgnoreQueryFilters()
            .AnyAsync(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.FileUploadRecordId == fileUploadRecordId,
                cancellationToken);
        if (isRegistrationEvidence)
            throw Failure(
                "FILE_RECORD_REFERENCED_BY_REGISTRATION_EVIDENCE",
                "The file cannot be deleted while it is referenced by active supplier registration evidence.",
                409);

        var isCentralDocumentVersion = await _db.CentralDocumentVersions
            .IgnoreQueryFilters()
            .AnyAsync(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.FileUploadRecordId == fileUploadRecordId,
                cancellationToken);
        if (isCentralDocumentVersion)
            throw Failure(
                "FILE_RECORD_REFERENCED_BY_CENTRAL_DMS",
                "The file cannot be deleted while it is referenced by an active central DMS version.",
                409);

        var isFinanceCloseEvidence = await _db.FinanceCloseEvidenceAttachments
            .IgnoreQueryFilters()
            .AnyAsync(item =>
                    item.TenantId == tenantId &&
                    !item.IsDeleted &&
                    item.FileUploadRecordId == fileUploadRecordId,
                cancellationToken);
        if (isFinanceCloseEvidence)
            throw Failure(
                "FILE_RECORD_REFERENCED_BY_FINANCE_CLOSE_EVIDENCE",
                "The file cannot be deleted while it is referenced by active Finance close evidence.",
                409);

        if (record.IsDeleted)
        {
            if (!record.StorageDeletedAtUtc.HasValue &&
                !record.StorageDeleteNextAttemptAtUtc.HasValue)
            {
                record.StorageDeleteNextAttemptAtUtc = DateTime.UtcNow;
                record.UpdatedAt = DateTime.UtcNow;
                record.LastModifiedById = actorUserId;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var now = DateTime.UtcNow;
        record.IsDeleted = true;
        record.DeletedAt = now;
        record.DeletedBy = actorUserId.ToString();
        record.UpdatedAt = now;
        record.LastModifiedById = actorUserId;
        record.StorageDeleteNextAttemptAtUtc = now;
        record.StorageDeleteLastError = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnforceQuotasAsync(
        Guid tenantId,
        string category,
        long additionalBytes,
        EffectiveFileUploadPolicy policy,
        CancellationToken cancellationToken)
    {
        if (policy.MaxTenantTotalBytes is > 0)
        {
            var used = await _db.FileUploadRecords.AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .SumAsync(item => (long?)item.FileSize, cancellationToken) ?? 0;
            if (used + additionalBytes > policy.MaxTenantTotalBytes.Value)
                throw Failure("FILE_TENANT_QUOTA_EXCEEDED",
                    "Storage quota has been exceeded for this tenant.", 409);
        }

        if (policy.MaxCategoryTotalBytes is > 0)
        {
            var used = await _db.FileUploadRecords.AsNoTracking()
                .Where(item => item.TenantId == tenantId &&
                               item.Category == category &&
                               !item.IsDeleted)
                .SumAsync(item => (long?)item.FileSize, cancellationToken) ?? 0;
            if (used + additionalBytes > policy.MaxCategoryTotalBytes.Value)
                throw Failure("FILE_CATEGORY_QUOTA_EXCEEDED",
                    "Storage quota has been exceeded for this category.", 409);
        }
    }

    private async Task<EffectiveFileUploadPolicy> GetEffectivePolicyAsync(
        Guid tenantId,
        string category,
        CancellationToken cancellationToken)
    {
        var rows = await _db.FileUploadPolicies.AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                           !item.IsDeleted &&
                           (item.Category == "*" || item.Category == category))
            .ToListAsync(cancellationToken);
        var global = rows.FirstOrDefault(item => item.Category == "*");
        var specific = rows.FirstOrDefault(item => item.Category == category);

        var allowedExtensions = ParseCsv(
            specific?.AllowedExtensionsCsv ?? global?.AllowedExtensionsCsv, true);
        if (allowedExtensions.Count == 0)
            allowedExtensions = new HashSet<string>(
                DefaultExtensions, StringComparer.OrdinalIgnoreCase);

        var allowedMimeTypes = ParseCsv(
            specific?.AllowedMimeTypesCsv ?? global?.AllowedMimeTypesCsv, false);
        if (allowedMimeTypes.Count == 0)
            allowedMimeTypes = new HashSet<string>(
                DefaultMimeTypes, StringComparer.OrdinalIgnoreCase);

        return new EffectiveFileUploadPolicy
        {
            IsEnabled = specific?.IsEnabled ?? global?.IsEnabled ?? true,
            MaxFileSizeBytes = specific?.MaxFileSizeBytes ?? global?.MaxFileSizeBytes,
            MaxTenantTotalBytes = global?.MaxTenantTotalBytes,
            MaxCategoryTotalBytes =
                specific?.MaxCategoryTotalBytes ?? global?.MaxCategoryTotalBytes,
            RequireVirusScan =
                _cleanScanRequiredCategories.Contains(category) ||
                (specific?.RequireVirusScan ?? global?.RequireVirusScan ?? false),
            AllowedExtensions = allowedExtensions,
            AllowedMimeTypes = allowedMimeTypes
        };
    }

    private static HashSet<string> ParseCsv(string? value, bool ensureDot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
            return result;
        foreach (var raw in value.Split(
                     ',', StringSplitOptions.RemoveEmptyEntries |
                          StringSplitOptions.TrimEntries))
        {
            var item = raw.Trim().ToLowerInvariant();
            if (ensureDot && !item.StartsWith('.'))
                item = $".{item}";
            if (item.Length > 0)
                result.Add(item);
        }
        return result;
    }

    private static string NormalizeCategory(string value) =>
        (value ?? string.Empty)
        .Trim()
        .Replace('\\', '/')
        .Replace("..", string.Empty)
        .Trim('/')
        .ToLowerInvariant();

    private static ControlledFileUploadException Failure(
        string code,
        string message,
        int statusCode) =>
        new(code, message, statusCode);

    private sealed class EffectiveFileUploadPolicy
    {
        public bool IsEnabled { get; init; }
        public long? MaxFileSizeBytes { get; init; }
        public long? MaxTenantTotalBytes { get; init; }
        public long? MaxCategoryTotalBytes { get; init; }
        public bool RequireVirusScan { get; init; }
        public HashSet<string> AllowedExtensions { get; init; } = [];
        public HashSet<string> AllowedMimeTypes { get; init; } = [];
    }
}
