using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Reclaims CVs uploaded to the public careers portal that were never attached to an application.
/// </summary>
/// <remarks>
/// <para>Most public CV uploads are abandoned — someone picks a file, then never finishes the
/// form. Without this they would accumulate indefinitely against the tenant's storage quota,
/// which is a poor outcome for files that are also personal data nobody asked us to keep.</para>
///
/// <para>Deletion goes through <see cref="IControlledFileUploadService"/> rather than touching
/// storage directly, so the physical removal rides the existing durable-cleanup machinery with
/// its retry and backoff. This is a separate worker rather than a new pass inside
/// <c>FileStorageCleanupProcessor</c> deliberately: consuming that mechanism through its public
/// interface gets the identical outcome without editing another module's code.</para>
/// </remarks>
public sealed class PublicCvUploadTicketSweeper : BackgroundService
{
    /// <summary>
    /// Grace period after expiry before the upload is removed. An expired ticket can no longer be
    /// claimed, so this is purely a margin for in-flight requests and clock skew.
    /// </summary>
    private static readonly TimeSpan RetentionAfterExpiry = TimeSpan.FromHours(24);

    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    private const int BatchSize = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PublicCvUploadTicketSweeper> _logger;

    public PublicCvUploadTicketSweeper(
        IServiceScopeFactory scopeFactory,
        ILogger<PublicCvUploadTicketSweeper> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A bad sweep must not take the worker down; the next pass retries.
                _logger.LogError(exception, "Public CV upload sweep failed.");
            }

            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var controlledFiles = scope.ServiceProvider
            .GetRequiredService<IControlledFileUploadService>();

        var cutoff = DateTime.UtcNow - RetentionAfterExpiry;
        var expired = await db.Set<PublicCvUploadTicket>()
            .Where(item =>
                item.ClaimedAtUtc == null &&
                item.ExpiresAtUtc < cutoff &&
                !item.IsDeleted)
            .OrderBy(item => item.ExpiresAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var ticket in expired)
        {
            try
            {
                await controlledFiles.DeleteAsync(
                    ticket.TenantId,
                    ticket.FileUploadRecordId,
                    ControlledFileUploadActors.PublicUploadSweeper,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                // Leave the ticket undeleted so the next sweep retries this one.
                _logger.LogWarning(exception,
                    "Could not remove unclaimed CV upload {UploadId} for tenant {TenantId}.",
                    ticket.FileUploadRecordId, ticket.TenantId);
                continue;
            }

            ticket.IsDeleted = true;
            ticket.DeletedAt = now;
            ticket.DeletedBy = "cv-upload-sweeper";
        }

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Swept {Count} unclaimed public CV upload(s).",
            expired.Count(item => item.IsDeleted));
    }
}
