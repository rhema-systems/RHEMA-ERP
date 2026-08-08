using System.Text.RegularExpressions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErpSystem.Core.Services;

/// <summary>
/// Local file system storage implementation
/// </summary>
public partial class LocalFileStorageService : IFileStorageService
{
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly StorageProviderOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly string _basePath;
    private readonly string _privateBasePath;
    private readonly string _baseUrl;

    public string ProviderName => "Local";
    private const string PrivatePathPrefix = "private/";
    private static readonly char[] second = new[] { ' ', '.', ',', ';' };

    public LocalFileStorageService(
        ILogger<LocalFileStorageService> logger,
        IOptions<StorageProviderOptions> options,
        IHostEnvironment environment)
    {
        _logger = logger;
        _options = options.Value;
        _environment = environment;

        // Configure base path
        if (_options.Local.UseWebRoot)
        {
            // For web applications, assume wwwroot is in ContentRootPath
            var webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
            _basePath = Path.Combine(webRootPath, _options.Local.BasePath);
        }
        else
        {
            _basePath = Path.IsPathRooted(_options.Local.BasePath)
                ? _options.Local.BasePath
                : Path.Combine(_environment.ContentRootPath, _options.Local.BasePath);
        }
        _privateBasePath = ResolvePrivateBasePath();

        // Configure base URL
        _baseUrl = _options.Local.BaseUrl ?? $"/{_options.Local.BasePath}";
        if (_baseUrl.EndsWith('/'))
        {
            _baseUrl = _baseUrl.TrimEnd('/');
        }

        // Ensure directory exists
        if (_options.Local.CreateDirectoryIfNotExists && !Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
            _logger.LogInformation("Created storage directory: {BasePath}", _basePath);
        }
        if (_options.Local.CreateDirectoryIfNotExists && !Directory.Exists(_privateBasePath))
        {
            Directory.CreateDirectory(_privateBasePath);
            _logger.LogInformation("Created private storage directory: {BasePath}", _privateBasePath);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string folderPath)
    {
        try
        {
            // Generate unique filename
            var uniqueFileName = GenerateUniqueFileName(fileName);

            // Create directory path
            var fullDirectoryPath = Path.Combine(_basePath, folderPath);
            Directory.CreateDirectory(fullDirectoryPath);

            // Full file path
            var filePath = Path.Combine(fullDirectoryPath, uniqueFileName);
            var relativeFilePath = Path.Combine(folderPath, uniqueFileName).Replace('\\', '/');

            // Save file
            using (var fileStreamWriter = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                await fileStream.CopyToAsync(fileStreamWriter);
                await fileStreamWriter.FlushAsync();
            }

            _logger.LogInformation("File uploaded: {FileName} -> {FilePath}", fileName, relativeFilePath);
            return relativeFilePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<Stream> DownloadFileAsync(string filePath, Guid fileId)
    {
        try
        {
            var fullPath = ResolvePhysicalPath(filePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            return await Task.FromResult(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<FileStorageResult> UploadFileAsync(FileUploadRequest request)
    {
        try
        {
            // Generate unique filename
            var uniqueFileName = GenerateUniqueFileName(request.FileName);

            // Create directory path based on category and date
            var datePath = DateTime.UtcNow.ToString("yyyy/MM");
            var categoryPath = SanitizePathComponent(request.Category);
            var tenantPath = !string.IsNullOrEmpty(request.TenantId)
                ? SanitizePathComponent(request.TenantId)
                : "global";

            var relativePath = Path.Combine(categoryPath, tenantPath, datePath);
            var privateFile = IsPrivateCategory(request.Category);
            var storageRoot = privateFile ? _privateBasePath : _basePath;
            var fullDirectoryPath = Path.Combine(storageRoot, relativePath);

            // Ensure directory exists
            Directory.CreateDirectory(fullDirectoryPath);

            // Full file path
            var filePath = Path.Combine(fullDirectoryPath, uniqueFileName);
            var relativeFilePath = Path.Combine(relativePath, uniqueFileName).Replace('\\', '/');
            var storedFilePath = privateFile ? $"{PrivatePathPrefix}{relativeFilePath}" : relativeFilePath;

            // Check if file already exists and handle overwrite
            if (File.Exists(filePath) && !request.OverwriteExisting)
            {
                return new FileStorageResult
                {
                    Success = false,
                    ErrorMessage = "File already exists and overwrite is not allowed",
                    FileName = uniqueFileName,
                    OriginalFileName = request.FileName,
                    FilePath = storedFilePath,
                    PublicUrl = GetPublicUrl(storedFilePath),
                    FileSize = request.FileSize,
                    ContentType = request.ContentType,
                    Category = request.Category,
                    TenantId = request.TenantId,
                    StorageProvider = ProviderName
                };
            }

            // Save file
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                await request.FileStream.CopyToAsync(fileStream);
                await fileStream.FlushAsync();
            }

            // Get actual file size
            var fileInfo = new FileInfo(filePath);
            var actualFileSize = fileInfo.Length;

            _logger.LogInformation("File uploaded successfully: {OriginalFileName} -> {FilePath} ({FileSize} bytes)",
                request.FileName, storedFilePath, actualFileSize);

            return new FileStorageResult
            {
                Success = true,
                FileName = uniqueFileName,
                OriginalFileName = request.FileName,
                FilePath = storedFilePath,
                PublicUrl = GetPublicUrl(storedFilePath),
                FileSize = actualFileSize,
                ContentType = request.ContentType,
                Category = request.Category,
                TenantId = request.TenantId,
                Metadata = request.Metadata,
                StorageProvider = ProviderName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", request.FileName);
            return new FileStorageResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileName = request.FileName,
                OriginalFileName = request.FileName,
                FilePath = "",
                PublicUrl = "",
                FileSize = request.FileSize,
                ContentType = request.ContentType,
                Category = request.Category,
                TenantId = request.TenantId,
                StorageProvider = ProviderName
            };
        }
    }

    public async Task<MultipleFileStorageResult> UploadMultipleFilesAsync(MultipleFileUploadRequest request)
    {
        var result = new MultipleFileStorageResult
        {
            TotalFiles = request.Files.Count
        };

        foreach (var fileRequest in request.Files)
        {
            try
            {
                var uploadResult = await UploadFileAsync(fileRequest);
                if (uploadResult.Success)
                {
                    result.SuccessfulUploads.Add(uploadResult);
                }
                else
                {
                    result.Errors.Add(new FileStorageError
                    {
                        FileName = fileRequest.FileName,
                        ErrorMessage = uploadResult.ErrorMessage ?? "Unknown error"
                    });

                    if (request.StopOnFirstError)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(new FileStorageError
                {
                    FileName = fileRequest.FileName,
                    ErrorMessage = ex.Message,
                    Exception = ex
                });

                if (request.StopOnFirstError)
                {
                    break;
                }
            }
        }

        return result;
    }

    public async Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            var fullPath = ResolvePhysicalPath(filePath);

            if (!File.Exists(fullPath))
            {
                _logger.LogDebug(
                    "File is already absent; treating storage deletion as successful: {FilePath}",
                    filePath);
                return true;
            }

            File.Delete(fullPath);

            // Clean up empty directories
            await CleanupEmptyDirectoriesAsync(Path.GetDirectoryName(fullPath)!);

            _logger.LogInformation("File deleted successfully: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        try
        {
            var fullPath = ResolvePhysicalPath(filePath);
            return File.Exists(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file existence: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<FileInfoResult?> GetFileInfoAsync(string filePath)
    {
        try
        {
            var fullPath = ResolvePhysicalPath(filePath);

            if (!File.Exists(fullPath))
            {
                return null;
            }

            var fileInfo = new FileInfo(fullPath);

            return new FileInfoResult
            {
                FileName = fileInfo.Name,
                FilePath = filePath,
                FileSize = fileInfo.Length,
                ContentType = GetContentType(fileInfo.Extension),
                CreatedAt = fileInfo.CreationTimeUtc,
                LastModified = fileInfo.LastWriteTimeUtc,
                StorageProvider = ProviderName,
                Exists = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info: {FilePath}", filePath);
            return null;
        }
    }

    public async Task<string> GetPublicUrlAsync(string filePath)
    {
        return GetPublicUrl(filePath);
    }

    public async Task<string> GetTemporaryUrlAsync(string filePath, TimeSpan expiration)
    {
        // For local storage, return the same public URL
        // In a production scenario, you might implement signed URLs with expiration
        return GetPublicUrl(filePath);
    }

    public async Task<bool> CopyFileAsync(string sourceFilePath, string destinationFilePath)
    {
        try
        {
            var sourcePath = ResolvePhysicalPath(sourceFilePath);
            var destinationPath = ResolvePhysicalPath(destinationFilePath);

            if (!File.Exists(sourcePath))
            {
                return false;
            }

            // Ensure destination directory exists
            var destinationDir = Path.GetDirectoryName(destinationPath)!;
            Directory.CreateDirectory(destinationDir);

            File.Copy(sourcePath, destinationPath, true);

            _logger.LogInformation("File copied: {SourcePath} -> {DestinationPath}",
                sourceFilePath, destinationFilePath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying file: {SourcePath} -> {DestinationPath}",
                sourceFilePath, destinationFilePath);
            return false;
        }
    }

    public async Task<bool> MoveFileAsync(string sourceFilePath, string destinationFilePath)
    {
        try
        {
            var sourcePath = ResolvePhysicalPath(sourceFilePath);
            var destinationPath = ResolvePhysicalPath(destinationFilePath);

            if (!File.Exists(sourcePath))
            {
                return false;
            }

            // Ensure destination directory exists
            var destinationDir = Path.GetDirectoryName(destinationPath)!;
            Directory.CreateDirectory(destinationDir);

            File.Move(sourcePath, destinationPath);

            // Clean up empty directories
            await CleanupEmptyDirectoriesAsync(Path.GetDirectoryName(sourcePath)!);

            _logger.LogInformation("File moved: {SourcePath} -> {DestinationPath}",
                sourceFilePath, destinationFilePath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving file: {SourcePath} -> {DestinationPath}",
                sourceFilePath, destinationFilePath);
            return false;
        }
    }

    public async Task<IEnumerable<FileInfoResult>> ListFilesAsync(string directoryPath, string? searchPattern = null)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, directoryPath);

            if (!Directory.Exists(fullPath))
            {
                return Enumerable.Empty<FileInfoResult>();
            }

            var pattern = searchPattern ?? "*.*";
            var files = Directory.GetFiles(fullPath, pattern, SearchOption.AllDirectories);

            var results = new List<FileInfoResult>();

            foreach (var filePath in files)
            {
                var fileInfo = new FileInfo(filePath);
                var relativePath = Path.GetRelativePath(_basePath, filePath).Replace('\\', '/');

                results.Add(new FileInfoResult
                {
                    FileName = fileInfo.Name,
                    FilePath = relativePath,
                    FileSize = fileInfo.Length,
                    ContentType = GetContentType(fileInfo.Extension),
                    CreatedAt = fileInfo.CreationTimeUtc,
                    LastModified = fileInfo.LastWriteTimeUtc,
                    StorageProvider = ProviderName,
                    Exists = true
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files in directory: {DirectoryPath}", directoryPath);
            return Enumerable.Empty<FileInfoResult>();
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check if base directory exists and is writable
            if (!Directory.Exists(_basePath))
            {
                return false;
            }

            // Try to create a temporary file to test write access
            var testFile = Path.Combine(_basePath, $"health_check_{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(testFile, "health check");
            File.Delete(testFile);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for local storage");
            return false;
        }
    }

    #region Private Methods

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
        sanitized = MyRegex().Replace(sanitized, "_").ToLowerInvariant();

        // Limit length
        if (sanitized.Length > 50)
        {
            sanitized = sanitized[..50];
        }

        return string.IsNullOrEmpty(sanitized) ? "file" : sanitized;
    }

    private static string SanitizePathComponent(string pathComponent)
    {
        // Sanitize path component for use in file path
        var invalidChars = Path.GetInvalidPathChars().Union(second);
        var sanitized = new string(pathComponent.Where(c => !invalidChars.Contains(c)).ToArray());
        return sanitized.ToLowerInvariant();
    }

    private string GetPublicUrl(string filePath)
    {
        if (IsPrivatePath(filePath))
        {
            return string.Empty;
        }

        return $"{_baseUrl}/{filePath}";
    }

    private static bool IsPrivateCategory(string? category)
        => !string.IsNullOrWhiteSpace(category)
            && (category.StartsWith("central-dms", StringComparison.OrdinalIgnoreCase)
                || category.Equals(ControlledFileUploadCategories.DocumentManagement, StringComparison.OrdinalIgnoreCase)
                || category.Equals(ControlledFileUploadCategories.SupplierRegistrationEvidence, StringComparison.OrdinalIgnoreCase)
                || category.StartsWith("quantity-survey-", StringComparison.OrdinalIgnoreCase)
                || category.StartsWith("estate-land-acquisition-documents", StringComparison.OrdinalIgnoreCase)
                || category.StartsWith("estate-managed-asset-documents", StringComparison.OrdinalIgnoreCase)
                || category.StartsWith("procedure-case-documents", StringComparison.OrdinalIgnoreCase)
                // Every HR document family is personal data: CVs, identity documents,
                // sick-note certificates, disciplinary evidence, medical exam results.
                // A prefix rule rather than a list, so a future hr-* category is private
                // by default instead of by remembering to opt in here.
                || category.StartsWith("hr-", StringComparison.OrdinalIgnoreCase));

    private static bool IsPrivatePath(string? filePath)
        => !string.IsNullOrWhiteSpace(filePath)
            && filePath.Replace('\\', '/').TrimStart('/').StartsWith(PrivatePathPrefix, StringComparison.OrdinalIgnoreCase);

    private string ResolvePrivateBasePath()
    {
        if (!string.IsNullOrWhiteSpace(_options.Local.PrivateBasePath))
        {
            return Path.IsPathRooted(_options.Local.PrivateBasePath)
                ? _options.Local.PrivateBasePath
                : Path.Combine(_environment.ContentRootPath, _options.Local.PrivateBasePath);
        }

        // Estate/DMS private uploads must follow persistent storage when BasePath is external, while staying out of the public URL folder.
        if (Path.IsPathRooted(_options.Local.BasePath))
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_basePath)) ?? _environment.ContentRootPath, "secure-file-storage");
        }

        if (!_options.Local.UseWebRoot && _options.Local.BasePath.Contains(Path.DirectorySeparatorChar))
        {
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_basePath)) ?? _environment.ContentRootPath, "secure-file-storage");
        }

        return Path.Combine(_environment.ContentRootPath, "secure-file-storage");
    }

    private string ResolvePhysicalPath(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/').TrimStart('/');
        if (IsPrivatePath(normalizedPath))
        {
            return ResolveRootedPath(_privateBasePath, normalizedPath[PrivatePathPrefix.Length..]);
        }

        return ResolveRootedPath(_basePath, normalizedPath);
    }

    private static string ResolveRootedPath(string storageRoot, string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('\\', '/').TrimStart('/');
        if (Path.IsPathRooted(normalizedRelativePath)
            || normalizedRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            throw new UnauthorizedAccessException("The requested file path is outside the configured storage root.");
        }

        var fullRoot = Path.GetFullPath(storageRoot);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelativePath));
        if (!IsPathInsideRoot(fullPath, fullRoot))
        {
            throw new UnauthorizedAccessException("The requested file path is outside the configured storage root.");
        }

        return fullPath;
    }

    private static bool IsPathInsideRoot(string fullPath, string fullRoot)
    {
        var root = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".rtf" => "application/rtf",
            _ => "application/octet-stream"
        };
    }

    private async Task CleanupEmptyDirectoriesAsync(string directoryPath)
    {
        try
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            // Don't delete storage roots.
            var fullDirectoryPath = Path.GetFullPath(directoryPath);
            if (fullDirectoryPath == Path.GetFullPath(_basePath)
                || fullDirectoryPath == Path.GetFullPath(_privateBasePath))
            {
                return;
            }

            // Check if directory is empty
            if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath);
                _logger.LogDebug("Cleaned up empty directory: {DirectoryPath}", directoryPath);

                // Recursively clean up parent directories
                await CleanupEmptyDirectoriesAsync(Path.GetDirectoryName(directoryPath)!);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup empty directory: {DirectoryPath}", directoryPath);
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MyRegex();

    #endregion
}
