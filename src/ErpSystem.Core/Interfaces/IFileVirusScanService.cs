using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces;

public sealed class FileVirusScanRequest
{
    public required Guid TenantId { get; init; }
    public required string Category { get; init; }
    public required string FileName { get; init; }
    public string? ContentType { get; init; }
    public long FileSize { get; init; }
    public required Stream Content { get; init; }
}

public sealed class FileVirusScanResult
{
    public FileVirusScanStatus Status { get; init; } = FileVirusScanStatus.Skipped;
    public string? Message { get; init; }
}

public interface IFileVirusScanService
{
    Task<FileVirusScanResult> ScanAsync(FileVirusScanRequest request, CancellationToken cancellationToken = default);
}

