namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Service interface for file upload operations
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Uploads a file and returns the file path
    /// </summary>
    Task<FileUploadResult> UploadFileAsync(Stream fileStream, string fileName, string folder, string? subFolder = null);

    /// <summary>
    /// Deletes a file from storage
    /// </summary>
    Task DeleteFileAsync(string filePath);

    /// <summary>
    /// Gets the full URL for a file path
    /// </summary>
    string GetFileUrl(string filePath);

    /// <summary>
    /// Validates file type and size
    /// </summary>
    bool ValidateFile(string fileName, long fileSize, string[] allowedExtensions, long maxSizeBytes);
}

/// <summary>
/// Result of file upload operation
/// </summary>
public class FileUploadResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
}