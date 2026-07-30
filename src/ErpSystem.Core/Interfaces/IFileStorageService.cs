using ErpSystem.Core.Models;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Abstraction for file storage operations - supports multiple providers (Local, Azure, AWS, etc.)
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Upload a single file to storage
    /// </summary>
    Task<FileStorageResult> UploadFileAsync(FileUploadRequest request);

    /// <summary>
    /// Upload a file with stream, filename, and folder path
    /// </summary>
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string folderPath);

    /// <summary>
    /// Download a file from storage
    /// </summary>
    Task<Stream> DownloadFileAsync(string filePath, Guid fileId);

    /// <summary>
    /// Upload multiple files to storage
    /// </summary>
    Task<MultipleFileStorageResult> UploadMultipleFilesAsync(MultipleFileUploadRequest request);

    /// <summary>
    /// Delete a file from storage. Implementations must be idempotent: return
    /// true when the object is absent after the operation (including when it
    /// was already absent), and false only when absence could not be ensured.
    /// </summary>
    Task<bool> DeleteFileAsync(string filePath);

    /// <summary>
    /// Check if file exists in storage
    /// </summary>
    Task<bool> FileExistsAsync(string filePath);

    /// <summary>
    /// Get file info (size, last modified, etc.)
    /// </summary>
    Task<FileInfoResult?> GetFileInfoAsync(string filePath);

    /// <summary>
    /// Get public URL for accessing the file
    /// </summary>
    Task<string> GetPublicUrlAsync(string filePath);

    /// <summary>
    /// Get a temporary download URL (useful for private files)
    /// </summary>
    Task<string> GetTemporaryUrlAsync(string filePath, TimeSpan expiration);

    /// <summary>
    /// Copy file from one location to another within the same storage
    /// </summary>
    Task<bool> CopyFileAsync(string sourceFilePath, string destinationFilePath);

    /// <summary>
    /// Move file from one location to another within the same storage
    /// </summary>
    Task<bool> MoveFileAsync(string sourceFilePath, string destinationFilePath);

    /// <summary>
    /// List files in a directory/container
    /// </summary>
    Task<IEnumerable<FileInfoResult>> ListFilesAsync(string directoryPath, string? searchPattern = null);

    /// <summary>
    /// Get storage provider name
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Check if storage provider is available
    /// </summary>
    Task<bool> IsHealthyAsync();
}

/// <summary>
/// Migration service for moving files between storage providers
/// </summary>
public interface IFileStorageMigrationService
{
    /// <summary>
    /// Migrate files from source storage to destination storage
    /// </summary>
    Task<MigrationResult> MigrateFilesAsync(
        IFileStorageService sourceStorage,
        IFileStorageService destinationStorage,
        MigrationOptions options);

    /// <summary>
    /// Validate migration integrity
    /// </summary>
    Task<MigrationValidationResult> ValidateMigrationAsync(
        IFileStorageService sourceStorage,
        IFileStorageService destinationStorage,
        string directoryPath);
}
