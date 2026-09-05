using System.Security.Claims;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Api.Services.HR.EmployeeImport;

/// <summary>
/// Writes confirmed employee imports in the background, one batch per DI scope.
/// </summary>
/// <remarks>
/// <para>There is no job queue in this API; the session row is the work item
/// (<c>Status = CommitRequested</c>). The host only paces and locks — the logic is
/// <see cref="IEmployeeImportService.CommitBatchAsync"/>, so a future "run now" endpoint shares it.</para>
/// <para>⚠ The employee service takes its tenant and actor from the HTTP context, which a background
/// scope does not have. Each batch therefore runs under a principal built from the session — the user
/// who requested the commit, in their tenant — installed on <see cref="IHttpContextAccessor"/> for the
/// duration of the scope. Without this every row would be refused with "no tenant".</para>
/// </remarks>
public sealed class EmployeeImportCommitBackgroundService : BackgroundService
{
    private const int BatchSize = 25;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LockLease = TimeSpan.FromMinutes(10);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmployeeImportCommitBackgroundService> _logger;

    public EmployeeImportCommitBackgroundService(IServiceProvider serviceProvider, ILogger<EmployeeImportCommitBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); // let migrations and seeding finish
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Employee import committer tick failed");
            }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        List<EmployeeImportPendingCommitDto> pending;
        using (var scope = _serviceProvider.CreateScope())
        {
            var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();
            await using var leader = await lockService.TryAcquireAsync("bg:employee-import-commit", LockLease, cancellationToken);
            if (leader == null) return;

            pending = await scope.ServiceProvider.GetRequiredService<IEmployeeImportService>()
                .FindSessionsAwaitingCommitAsync(cancellationToken);
            if (pending.Count == 0) return;

            foreach (var item in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await CommitSessionAsync(item, cancellationToken);
            }
        }
    }

    private async Task CommitSessionAsync(EmployeeImportPendingCommitDto item, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Employee import committer: session {SessionId} (tenant {TenantId})", item.SessionId, item.TenantId);
        var more = true;
        while (more && !cancellationToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            using var _ = ActAs(scope, item);
            var service = scope.ServiceProvider.GetRequiredService<IEmployeeImportService>();
            try
            {
                more = await service.CommitBatchAsync(item.SessionId, BatchSize, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Employee import committer: session {SessionId} failed", item.SessionId);
                try
                {
                    await service.MarkCommitFailedAsync(item.SessionId, ex.Message, CancellationToken.None);
                }
                catch (Exception inner)
                {
                    _logger.LogError(inner, "Employee import committer: could not record the failure on session {SessionId}", item.SessionId);
                }
                return;
            }
        }
    }

    /// <summary>Installs the requesting user's identity on the scope's HTTP context accessor, and removes it after.</summary>
    private static IDisposable ActAs(IServiceScope scope, EmployeeImportPendingCommitDto item)
    {
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var name = string.IsNullOrWhiteSpace(item.RequestedByName) ? "employee-import" : item.RequestedByName;
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, item.RequestedByUserId.ToString()),
            new Claim(ClaimTypes.Name, name),
            new Claim("full_name", name),
            new Claim("tenant_id", item.TenantId.ToString()),
        ], authenticationType: "EmployeeImportCommit");
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = scope.ServiceProvider,
        };
        return new Reset(accessor);
    }

    private sealed class Reset(IHttpContextAccessor accessor) : IDisposable
    {
        public void Dispose() => accessor.HttpContext = null;
    }
}
