using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FileUploadController : ControllerBase
{
    private readonly ILogger<FileUploadController> _logger;
    private readonly FileUploadOptions _fileUploadOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly IFileStorageService _storageService;
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileVirusScanService _virusScanService;

    // Allowed file extensions and MIME types
    private static readonly Dictionary<string, string[]> AllowedFileTypes = new()
    {
        { "image", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".ico" } },
        { "document", new[] { ".pdf", ".doc", ".docx", ".txt", ".rtf" } }
    };

    private static readonly string[] AllowedMimeTypes = new[]
    {
        "image/jpeg", "image/png", "image/gif", "image/bmp", "image/svg+xml", "image/webp", "image/x-icon", "image/vnd.microsoft.icon",
        "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain", "application/rtf"
    };
    private static readonly char[] second = new[] { ' ', '.', ',', ';' };

    public FileUploadController(
        ILogger<FileUploadController> logger,
        IOptions<FileUploadOptions> fileUploadOptions,
        IWebHostEnvironment environment,
        IFileStorageService storageService,
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IFileVirusScanService virusScanService)
    {
        _logger = logger;
        _fileUploadOptions = fileUploadOptions.Value;
        _environment = environment;
        _storageService = storageService;
        _db = db;
        _currentUserService = currentUserService;
        _virusScanService = virusScanService;
    }

    /// <summary>
    /// Upload a single file
    /// </summary>
    /// <param name="file">The file to upload</param>
    /// <param name="category">File category (tenant-branding, user-avatars, etc.)</param>
    /// <param name="tenantId">Optional tenant ID for organization</param>
    /// <returns>File upload result</returns>
    [HttpPost("single")]
    public async Task<ActionResult<FileUploadResult>> UploadSingleFile(
        IFormFile file,
        [FromForm] string category = "general",
        [FromForm] string? tenantId = null)
    {
        try
        {
            var effectiveTenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (User.IsInRole(Constants.Roles.SuperAdmin) && !string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var overrideTenant) && overrideTenant != Guid.Empty)
            {
                effectiveTenantId = overrideTenant;
            }

            if (effectiveTenantId == Guid.Empty)
            {
                return BadRequest(new { message = "Tenant context is required" });
            }

            if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
            {
                return BadRequest(new { message = "User context is required" });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file provided or file is empty" });
            }

            category = NormalizeCategory(category);
            if (string.IsNullOrWhiteSpace(category))
            {
                return BadRequest(new { message = "Category is required" });
            }

            var policy = await GetEffectivePolicyAsync(effectiveTenantId, category);
            if (!policy.IsEnabled)
            {
                return BadRequest(new { message = "File uploads are disabled for this tenant/category" });
            }

            // Validate file size
            var maxFileSizeBytes = policy.MaxFileSizeBytes ?? _fileUploadOptions.MaxFileSizeBytes;
            if (file.Length > maxFileSizeBytes)
            {
                return BadRequest(new { message = $"File size exceeds maximum allowed size of {maxFileSizeBytes / 1024 / 1024}MB" });
            }

            // Validate file type (extension is the primary guard; MIME type is a best-effort signal)
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!policy.AllowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = $"File type '{extension}' is not allowed" });
            }

            if (!string.IsNullOrWhiteSpace(file.ContentType) && policy.AllowedMimeTypes.Count > 0 && !policy.AllowedMimeTypes.Contains(file.ContentType))
            {
                return BadRequest(new { message = $"File MIME type '{file.ContentType}' is not allowed" });
            }

            // Enforce quotas before uploading (best-effort; accurate by summing recorded uploads)
            if (policy.MaxTenantTotalBytes.HasValue && policy.MaxTenantTotalBytes.Value > 0)
            {
                var used = await _db.FileUploadRecords
                    .AsNoTracking()
                    .Where(r => r.TenantId == effectiveTenantId && !r.IsDeleted)
                    .Select(r => (long?)r.FileSize)
                    .SumAsync() ?? 0;

                if (used + file.Length > policy.MaxTenantTotalBytes.Value)
                {
                    return BadRequest(new { message = "Storage quota exceeded for this tenant" });
                }
            }

            if (policy.MaxCategoryTotalBytes.HasValue && policy.MaxCategoryTotalBytes.Value > 0)
            {
                var used = await _db.FileUploadRecords
                    .AsNoTracking()
                    .Where(r => r.TenantId == effectiveTenantId && !r.IsDeleted && r.Category == category)
                    .Select(r => (long?)r.FileSize)
                    .SumAsync() ?? 0;

                if (used + file.Length > policy.MaxCategoryTotalBytes.Value)
                {
                    return BadRequest(new { message = "Storage quota exceeded for this category" });
                }
            }

            // Virus scan hook (no-op by default)
            var scanStatus = ErpSystem.Core.Enums.FileVirusScanStatus.Skipped;
            string? scanMessage = null;
            DateTime? scannedAtUtc = null;
            if (policy.RequireVirusScan)
            {
                using var scanStream = file.OpenReadStream();
                var scan = await _virusScanService.ScanAsync(new FileVirusScanRequest
                {
                    TenantId = effectiveTenantId,
                    Category = category,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    Content = scanStream
                });

                scanStatus = scan?.Status ?? ErpSystem.Core.Enums.FileVirusScanStatus.Error;
                scanMessage = scan?.Message;
                scannedAtUtc = DateTime.UtcNow;

                if (scanStatus == ErpSystem.Core.Enums.FileVirusScanStatus.Infected)
                {
                    return BadRequest(new { message = "Upload rejected (file failed virus scan)" });
                }
            }

            // Create storage request
            var uploadRequest = new FileUploadRequest
            {
                FileStream = file.OpenReadStream(),
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                Category = category,
                TenantId = effectiveTenantId.ToString(),
                OverwriteExisting = false
            };

            // Upload using storage service
            var result = await _storageService.UploadFileAsync(uploadRequest);

            if (!result.Success)
            {
                return BadRequest(new { message = result.ErrorMessage ?? "Upload failed" });
            }

            _logger.LogInformation("File uploaded successfully using {StorageProvider}: {FileName} -> {FilePath}",
                result.StorageProvider, file.FileName, result.FilePath);

            // Record upload for quotas/auditability
            try
            {
                _db.FileUploadRecords.Add(new ErpSystem.Core.Entities.FileUploadRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = effectiveTenantId,
                    Category = category,
                    FilePath = result.FilePath,
                    StoredFileName = result.FileName,
                    OriginalFileName = result.OriginalFileName,
                    ContentType = result.ContentType,
                    FileSize = result.FileSize,
                    StorageProvider = result.StorageProvider,
                    UploadedByUserId = actorUserId,
                    VirusScanStatus = scanStatus,
                    ScannedAtUtc = scannedAtUtc,
                    VirusScanMessage = scanMessage,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUserService.UserName,
                    CreatedById = actorUserId
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record uploaded file metadata for {FilePath}", result.FilePath);
            }

            // Convert to legacy format for backward compatibility
            return Ok(new FileUploadResult
            {
                Success = result.Success,
                FileName = result.FileName,
                OriginalFileName = result.OriginalFileName,
                FilePath = result.FilePath,
                PublicUrl = result.PublicUrl,
                FileSize = result.FileSize,
                ContentType = result.ContentType,
                Category = result.Category,
                TenantId = effectiveTenantId.ToString(),
                UploadedAt = result.UploadedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", file?.FileName);
            return StatusCode(500, new { message = "An error occurred while uploading the file", details = ex.Message });
        }
    }

    /// <summary>
    /// Upload multiple files
    /// </summary>
    /// <param name="files">The files to upload</param>
    /// <param name="category">File category</param>
    /// <param name="tenantId">Optional tenant ID</param>
    /// <returns>Multiple file upload results</returns>
    [HttpPost("multiple")]
    public async Task<ActionResult<MultipleFileUploadResult>> UploadMultipleFiles(
        List<IFormFile> files,
        [FromForm] string category = "general",
        [FromForm] string? tenantId = null)
    {
        var results = new List<FileUploadResult>();
        var errors = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var result = await UploadSingleFile(file, category, tenantId);
                if (result.Result is OkObjectResult okResult)
                {
                    results.Add((FileUploadResult)okResult.Value!);
                }
                else
                {
                    errors.Add($"Failed to upload {file.FileName}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error uploading {file.FileName}: {ex.Message}");
                _logger.LogError(ex, "Error uploading file in batch: {FileName}", file.FileName);
            }
        }

        return Ok(new MultipleFileUploadResult
        {
            SuccessfulUploads = results,
            Errors = errors,
            TotalFiles = files.Count,
            SuccessfulCount = results.Count,
            FailedCount = errors.Count
        });
    }

    /// <summary>
    /// Delete an uploaded file
    /// </summary>
    /// <param name="filePath">Relative path to the file</param>
    /// <returns>Delete result</returns>
    [HttpDelete]
    public async Task<ActionResult> DeleteFile([FromQuery] string filePath)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
                return BadRequest(new { message = "Tenant context is required" });

            if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
                return BadRequest(new { message = "User context is required" });

            if (string.IsNullOrEmpty(filePath))
            {
                return BadRequest(new { message = "File path is required" });
            }

            // Security check - ensure the file path is safe
            if (filePath.Contains("..") || filePath.StartsWith("/") || filePath.StartsWith("\\"))
            {
                return BadRequest(new { message = "Invalid file path" });
            }

            var record = await _db.FileUploadRecords.FirstOrDefaultAsync(r => r.TenantId == tenantId && !r.IsDeleted && r.FilePath == filePath);
            if (record == null)
            {
                return NotFound(new { message = "File not found" });
            }

            // Compliance exports should be treated as immutable (best-effort guardrail).
            if (string.Equals(record.Category, "ehc-audit-export", StringComparison.OrdinalIgnoreCase) && !User.IsInRole(Constants.Roles.SuperAdmin))
            {
                return Forbid();
            }

            var isInternalAdmin =
                User.IsInRole(Constants.Roles.SuperAdmin) ||
                User.IsInRole(Constants.Roles.TenantAdmin) ||
                User.IsInRole(Constants.Roles.HelpdeskManager) ||
                User.IsInRole(Constants.Roles.HelpdeskSupervisor) ||
                User.IsInRole(Constants.Roles.HelpdeskAgent);

            if (!isInternalAdmin && record.UploadedByUserId != actorUserId)
            {
                return Forbid();
            }

            // Use storage service to delete file
            var deleted = await _storageService.DeleteFileAsync(filePath);

            if (!deleted)
            {
                return NotFound(new { message = "File not found or could not be deleted" });
            }

            record.IsDeleted = true;
            record.DeletedAt = DateTime.UtcNow;
            record.DeletedBy = _currentUserService.UserName;
            record.LastModifiedById = actorUserId;
            record.UpdatedAt = DateTime.UtcNow;
            record.UpdatedBy = _currentUserService.UserName;
            await _db.SaveChangesAsync();

            _logger.LogInformation("File deleted successfully using {StorageProvider}: {FilePath}",
                _storageService.ProviderName, filePath);

            return Ok(new { message = "File deleted successfully", filePath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
            return StatusCode(500, new { message = "An error occurred while deleting the file" });
        }
    }

    private static bool IsAllowedFileType(string extension, string contentType)
    {
        // Check extension
        var isExtensionAllowed = AllowedFileTypes.Values.Any(extensions => extensions.Contains(extension));

        // Check MIME type
        var isMimeTypeAllowed = AllowedMimeTypes.Contains(contentType);

        return isExtensionAllowed && isMimeTypeAllowed;
    }

    private string GenerateUniqueFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
        var sanitizedFileName = SanitizeFileName(fileNameWithoutExtension);
        var uniqueId = Guid.NewGuid().ToString("N")[..8]; // Use first 8 characters of GUID
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        return $"{sanitizedFileName}_{timestamp}_{uniqueId}{extension}";
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove invalid characters and limit length
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        sanitized = sanitized.Replace(" ", "_").ToLowerInvariant();

        // Limit length
        if (sanitized.Length > 50)
        {
            sanitized = sanitized[..50];
        }

        return string.IsNullOrEmpty(sanitized) ? "file" : sanitized;
    }

    private static string SanitizeCategory(string category)
    {
        // Sanitize category for use in file path
        var invalidChars = Path.GetInvalidPathChars().Union(second);
        var sanitized = new string(category.Where(c => !invalidChars.Contains(c)).ToArray());
        return sanitized.ToLowerInvariant();
    }

    private sealed class EffectiveFileUploadPolicy
    {
        public bool IsEnabled { get; set; } = true;
        public long? MaxFileSizeBytes { get; set; }
        public long? MaxTenantTotalBytes { get; set; }
        public long? MaxCategoryTotalBytes { get; set; }
        public bool RequireVirusScan { get; set; } = false;
        public HashSet<string> AllowedExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AllowedMimeTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeCategory(string category)
    {
        var c = (category ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(c)) return string.Empty;
        c = c.Replace('\\', '/');
        c = c.Replace("..", string.Empty);
        c = c.Trim('/');
        return c.ToLowerInvariant();
    }

    private static HashSet<string> ParseCsvSet(string? csv, bool ensureLeadingDot = false)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(csv)) return set;

        foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = raw.Trim();
            if (string.IsNullOrWhiteSpace(token)) continue;
            token = token.ToLowerInvariant();
            if (ensureLeadingDot && !token.StartsWith('.')) token = "." + token;
            set.Add(token);
        }

        return set;
    }

    private static HashSet<string> DefaultAllowedExtensions()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var exts in AllowedFileTypes.Values)
        {
            foreach (var e in exts) set.Add(e);
        }
        return set;
    }

    private static HashSet<string> DefaultAllowedMimeTypes()
    {
        return new HashSet<string>(AllowedMimeTypes, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<EffectiveFileUploadPolicy> GetEffectivePolicyAsync(Guid tenantId, string category)
    {
        var items = await _db.FileUploadPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && (p.Category == "*" || p.Category == category))
            .ToListAsync();

        var global = items.FirstOrDefault(p => p.Category == "*");
        var specific = items.FirstOrDefault(p => p.Category == category);

        // If a specific category policy exists, it wins for IsEnabled.
        var isEnabled = specific?.IsEnabled ?? global?.IsEnabled ?? true;

        var allowedExt = ParseCsvSet(specific?.AllowedExtensionsCsv, ensureLeadingDot: true);
        if (allowedExt.Count == 0) allowedExt = ParseCsvSet(global?.AllowedExtensionsCsv, ensureLeadingDot: true);
        if (allowedExt.Count == 0) allowedExt = DefaultAllowedExtensions();

        var allowedMime = ParseCsvSet(specific?.AllowedMimeTypesCsv, ensureLeadingDot: false);
        if (allowedMime.Count == 0) allowedMime = ParseCsvSet(global?.AllowedMimeTypesCsv, ensureLeadingDot: false);
        if (allowedMime.Count == 0) allowedMime = DefaultAllowedMimeTypes();

        return new EffectiveFileUploadPolicy
        {
            IsEnabled = isEnabled,
            MaxFileSizeBytes = specific?.MaxFileSizeBytes ?? global?.MaxFileSizeBytes,
            MaxTenantTotalBytes = global?.MaxTenantTotalBytes,
            MaxCategoryTotalBytes = specific?.MaxCategoryTotalBytes ?? global?.MaxCategoryTotalBytes,
            RequireVirusScan = specific?.RequireVirusScan ?? global?.RequireVirusScan ?? false,
            AllowedExtensions = allowedExt,
            AllowedMimeTypes = allowedMime
        };
    }
}

// Configuration options
public class FileUploadOptions
{
    public const string SectionName = "FileUpload";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB default
    public string UploadPath { get; set; } = "uploads";
    public bool EnableImageOptimization { get; set; } = false;
    public int MaxImageWidth { get; set; } = 2048;
    public int MaxImageHeight { get; set; } = 2048;
}

// Response DTOs
public class FileUploadResult
{
    public bool Success { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class MultipleFileUploadResult
{
    public List<FileUploadResult> SuccessfulUploads { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public int TotalFiles { get; set; }
    public int SuccessfulCount { get; set; }
    public int FailedCount { get; set; }
}
