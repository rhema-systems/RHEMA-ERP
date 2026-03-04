using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Services;

public sealed class NoOpFileVirusScanService : IFileVirusScanService
{
    public Task<FileVirusScanResult> ScanAsync(FileVirusScanRequest request, CancellationToken cancellationToken = default)
    {
        // Phase 2 baseline: provide a hook for future AV integration.
        return Task.FromResult(new FileVirusScanResult
        {
            Status = FileVirusScanStatus.Skipped,
            Message = "Virus scanning not configured."
        });
    }
}

