namespace ErpSystem.Core.Models;

/// <summary>
/// Request for uploading a single file
/// </summary>
public class FileUploadRequest
{
    public required Stream FileStream { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long FileSize { get; set; }
    public required string Category { get; set; }
    public string? TenantId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public bool OverwriteExisting { get; set; } = false;
}

/// <summary>
/// Request for uploading multiple files
/// </summary>
public class MultipleFileUploadRequest
{
    public required List<FileUploadRequest> Files { get; set; }
    public string Category { get; set; } = "general";
    public string? TenantId { get; set; }
    public bool StopOnFirstError { get; set; } = false;
}

/// <summary>
/// Result of a file storage operation
/// </summary>
public class FileStorageResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public required string FileName { get; set; }
    public required string OriginalFileName { get; set; }
    public required string FilePath { get; set; }
    public required string PublicUrl { get; set; }
    public long FileSize { get; set; }
    public required string ContentType { get; set; }
    public required string Category { get; set; }
    public string? TenantId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public required string StorageProvider { get; set; }
}

/// <summary>
/// Result of multiple file upload operation
/// </summary>
public class MultipleFileStorageResult
{
    public List<FileStorageResult> SuccessfulUploads { get; set; } = new();
    public List<FileStorageError> Errors { get; set; } = new();
    public int TotalFiles { get; set; }
    public int SuccessfulCount => SuccessfulUploads.Count;
    public int FailedCount => Errors.Count;
    public bool HasErrors => Errors.Count > 0;
}

/// <summary>
/// File storage error information
/// </summary>
public class FileStorageError
{
    public required string FileName { get; set; }
    public required string ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// File information result
/// </summary>
public class FileInfoResult
{
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public long FileSize { get; set; }
    public required string ContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public required string StorageProvider { get; set; }
    public bool Exists { get; set; }
}

/// <summary>
/// Storage provider configuration
/// </summary>
public class StorageProviderOptions
{
    public const string SectionName = "FileStorage";

    public StorageProviderType Provider { get; set; } = StorageProviderType.Local;
    public LocalStorageOptions Local { get; set; } = new();
    public AzureBlobStorageOptions Azure { get; set; } = new();
    public AwsS3StorageOptions Aws { get; set; } = new();

    // General settings
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
    public bool EnableImageOptimization { get; set; } = false;
    public int MaxImageWidth { get; set; } = 2048;
    public int MaxImageHeight { get; set; } = 2048;
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
    public string[] AllowedMimeTypes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Storage provider types
/// </summary>
public enum StorageProviderType
{
    Local,
    AzureBlob,
    AwsS3,
    GoogleCloud
}

/// <summary>
/// Local storage configuration
/// </summary>
public class LocalStorageOptions
{
    public string BasePath { get; set; } = "uploads";
    public string? BaseUrl { get; set; }
    public bool UseWebRoot { get; set; } = true;
    public bool CreateDirectoryIfNotExists { get; set; } = true;
    public FilePermissions DefaultPermissions { get; set; } = FilePermissions.ReadWrite;
}

/// <summary>
/// Azure Blob Storage configuration
/// </summary>
public class AzureBlobStorageOptions
{
    public string? ConnectionString { get; set; }
    public string? ContainerName { get; set; }
    public string? CdnUrl { get; set; }
    public bool PublicAccess { get; set; } = true;
    public int TemporaryUrlExpirationHours { get; set; } = 24;
}

/// <summary>
/// AWS S3 storage configuration
/// </summary>
public class AwsS3StorageOptions
{
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? BucketName { get; set; }
    public string? Region { get; set; }
    public string? CdnUrl { get; set; }
    public bool PublicAccess { get; set; } = true;
    public int TemporaryUrlExpirationHours { get; set; } = 24;
}

/// <summary>
/// File permissions for local storage
/// </summary>
[Flags]
public enum FilePermissions
{
    None = 0,
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write,
    Execute = 4,
    Full = Read | Write | Execute
}

/// <summary>
/// Migration options
/// </summary>
public class MigrationOptions
{
    public string? SourceDirectoryPath { get; set; }
    public string? DestinationDirectoryPath { get; set; }
    public bool DeleteSourceAfterMigration { get; set; } = false;
    public bool VerifyIntegrity { get; set; } = true;
    public int BatchSize { get; set; } = 100;
    public bool ContinueOnError { get; set; } = true;
    public List<string> IncludePatterns { get; set; } = new();
    public List<string> ExcludePatterns { get; set; } = new();
}

/// <summary>
/// Migration result
/// </summary>
public class MigrationResult
{
    public bool Success { get; set; }
    public int TotalFiles { get; set; }
    public int MigratedFiles { get; set; }
    public int FailedFiles { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Migration validation result
/// </summary>
public class MigrationValidationResult
{
    public bool IsValid { get; set; }
    public int TotalFiles { get; set; }
    public int ValidFiles { get; set; }
    public int InvalidFiles { get; set; }
    public List<string> MissingFiles { get; set; } = new();
    public List<string> SizeMismatches { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
