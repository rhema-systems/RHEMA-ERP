using ErpSystem.Api.Controllers;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services;

/// <summary>
/// Processes durable physical-deletion work recorded on soft-deleted
/// FileUploadRecords. A failed provider response remains retryable and never
/// changes the already-committed domain/metadata transaction.
/// </summary>
public sealed class FileStorageCleanupProcessor
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly FileUploadOptions _options;
    private readonly ILogger<FileStorageCleanupProcessor> _logger;

    public FileStorageCleanupProcessor(
        ApplicationDbContext db,
        IFileStorageService storage,
        IOptions<FileUploadOptions> options,
        ILogger<FileStorageCleanupProcessor> logger)
    {
        _db = db;
        _storage = storage;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var batchSize = Math.Clamp(_options.StorageCleanupBatchSize, 1, 500);
        var records = await _db.FileUploadRecords
            .IgnoreQueryFilters()
            .Where(item =>
                item.IsDeleted &&
                !item.StorageDeletedAtUtc.HasValue &&
                (!item.StorageDeleteNextAttemptAtUtc.HasValue ||
                 item.StorageDeleteNextAttemptAtUtc <= now))
            .OrderBy(item => item.StorageDeleteNextAttemptAtUtc)
            .ThenBy(item => item.DeletedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attemptedAt = DateTime.UtcNow;
            record.StorageDeleteAttemptCount++;
            record.StorageDeleteLastAttemptAtUtc = attemptedAt;

            try
            {
                if (!string.Equals(
                        record.StorageProvider,
                        _storage.ProviderName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Storage provider '{record.StorageProvider}' is not available; " +
                        $"the active provider is '{_storage.ProviderName}'.");
                }

                var deleted = await _storage.DeleteFileAsync(record.FilePath);
                if (!deleted)
                {
                    throw new InvalidOperationException(
                        "The storage provider returned false while deleting the object.");
                }

                record.StorageDeletedAtUtc = DateTime.UtcNow;
                record.StorageDeleteNextAttemptAtUtc = null;
                record.StorageDeleteLastError = null;
            }
            catch (Exception exception)
            {
                record.StorageDeleteLastError =
                    Truncate(exception.Message, 2000);
                record.StorageDeleteNextAttemptAtUtc = attemptedAt.Add(
                    RetryDelay(record.StorageDeleteAttemptCount));
                _logger.LogWarning(
                    exception,
                    "Physical cleanup attempt {AttemptCount} failed for file record {FileUploadRecordId}; retry scheduled for {NextAttemptAtUtc}.",
                    record.StorageDeleteAttemptCount,
                    record.Id,
                    record.StorageDeleteNextAttemptAtUtc);
            }

            record.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return records.Count;
    }

    private static TimeSpan RetryDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 8);
        var seconds = Math.Min(5 * (1 << exponent), 3600);
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
