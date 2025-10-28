using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Services;

public class SimpleFileUploadService : IFileUploadService
{
    public Task<FileUploadResult> UploadFileAsync(Stream fileStream, string fileName, string folder, string? subFolder = null)
    {
        // Simple implementation - just return a mock file path
        var filePath = $"/uploads/{folder}/{subFolder}/{Guid.NewGuid()}_{fileName}";
        var result = new FileUploadResult
        {
            FilePath = filePath,
            FileName = fileName,
            FileUrl = filePath,
            FileSize = fileStream.Length,
            ContentType = "application/octet-stream"
        };
        return Task.FromResult(result);
    }

    public Task DeleteFileAsync(string filePath)
    {
        // Simple implementation - nothing to do for now
        return Task.CompletedTask;
    }

    public string GetFileUrl(string filePath)
    {
        // Simple implementation - just return the file path as URL
        return filePath;
    }

    public bool ValidateFile(string fileName, long fileSize, string[] allowedExtensions, long maxSizeBytes)
    {
        // Simple implementation - always return true
        return true;
    }
}
