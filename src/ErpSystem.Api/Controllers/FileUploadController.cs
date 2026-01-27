using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
        IFileStorageService storageService)
    {
        _logger = logger;
        _fileUploadOptions = fileUploadOptions.Value;
        _environment = environment;
        _storageService = storageService;
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
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file provided or file is empty" });
            }

            // Validate file size
            if (file.Length > _fileUploadOptions.MaxFileSizeBytes)
            {
                return BadRequest(new { message = $"File size exceeds maximum allowed size of {_fileUploadOptions.MaxFileSizeBytes / 1024 / 1024}MB" });
            }

            // Validate file type
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!IsAllowedFileType(extension, file.ContentType))
            {
                return BadRequest(new { message = $"File type '{extension}' is not allowed" });
            }

            // Create storage request
            var uploadRequest = new FileUploadRequest
            {
                FileStream = file.OpenReadStream(),
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                Category = category,
                TenantId = tenantId,
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
                TenantId = result.TenantId,
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
            if (string.IsNullOrEmpty(filePath))
            {
                return BadRequest(new { message = "File path is required" });
            }

            // Security check - ensure the file path is safe
            if (filePath.Contains("..") || filePath.StartsWith("/") || filePath.StartsWith("\\"))
            {
                return BadRequest(new { message = "Invalid file path" });
            }

            // Use storage service to delete file
            var deleted = await _storageService.DeleteFileAsync(filePath);

            if (!deleted)
            {
                return NotFound(new { message = "File not found or could not be deleted" });
            }

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
