using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using ErpSystem.Api.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FileUploadController : ControllerBase
{
    private readonly ILogger<FileUploadController> _logger;
    private readonly IFileStorageService _storageService;
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IControlledFileUploadService _controlledFiles;

    // Allowed file extensions and MIME types
    private static readonly Dictionary<string, string[]> AllowedFileTypes = new()
    {
        { "image", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".ico" } },
        { "document", new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".rtf" } }
    };

    private static readonly string[] AllowedMimeTypes = new[]
    {
        "image/jpeg", "image/png", "image/gif", "image/bmp", "image/svg+xml", "image/webp", "image/x-icon", "image/vnd.microsoft.icon",
        "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "text/csv",
        "text/plain", "application/rtf"
    };
    public FileUploadController(
        ILogger<FileUploadController> logger,
        IFileStorageService storageService,
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IControlledFileUploadService controlledFiles)
    {
        _logger = logger;
        _storageService = storageService;
        _db = db;
        _currentUserService = currentUserService;
        _controlledFiles = controlledFiles;
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

            if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
            {
                return BadRequest(new { message = "User context is required" });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file provided or file is empty" });
            }

            var result = await _controlledFiles.UploadAsync(
                new ControlledFileUploadRequest
                {
                    TenantId = effectiveTenantId,
                    ActorUserId = actorUserId,
                    ActorName = _currentUserService.UserName,
                    Category = category,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    OpenReadStream = file.OpenReadStream
                },
                HttpContext.RequestAborted);
            var record = result.Record;
            return Ok(new FileUploadResult
            {
                Success = true,
                FileName = record.StoredFileName,
                OriginalFileName = record.OriginalFileName,
                FilePath = record.FilePath,
                PublicUrl = result.PublicUrl,
                FileSize = record.FileSize,
                ContentType = record.ContentType,
                Category = record.Category,
                TenantId = effectiveTenantId.ToString(),
                FileId = record.Id,
                UploadedAt = record.CreatedAt
            });
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new
            {
                code = ex.Code,
                message = ex.Message
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
    public Guid? FileId { get; set; }
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
