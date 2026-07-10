using System.Net.Http.Headers;
using System.Text;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Workflow;

public sealed class WorkflowIntegrationQueueBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<WorkflowIntegrationQueueBackgroundService> _logger;
    public WorkflowIntegrationQueueBackgroundService(IServiceScopeFactory scopes, IHttpClientFactory clients,
        ILogger<WorkflowIntegrationQueueBackgroundService> logger) { _scopes = scopes; _clients = clients; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunCycle(stoppingToken); } catch (Exception error) { _logger.LogError(error, "Workflow integration queue cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task RunCycle(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var rows = await db.WorkflowIntegrationExecutions.IgnoreQueryFilters().Where(item => !item.IsDeleted &&
            (item.Status == WorkflowExecutionQueueStatus.Pending || item.Status == WorkflowExecutionQueueStatus.Failed) &&
            (!item.NextAttemptAt.HasValue || item.NextAttemptAt <= now)).OrderBy(item => item.CreatedAt).Take(50).ToListAsync(cancellationToken);
        var client = _clients.CreateClient(nameof(WorkflowIntegrationQueueBackgroundService));
        foreach (var row in rows)
        {
            row.Status = WorkflowExecutionQueueStatus.Processing; row.AttemptCount++; row.LastAttemptAt = now;
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, row.Endpoint);
                request.Headers.TryAddWithoutValidation("Idempotency-Key", row.IdempotencyKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(row.RequestPayload ?? "{}", Encoding.UTF8, "application/json");
                using var response = await client.SendAsync(request, cancellationToken);
                row.ResponsePayload = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode) throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
                row.Status = WorkflowExecutionQueueStatus.Succeeded; row.LastError = null; row.NextAttemptAt = null;
            }
            catch (Exception error)
            {
                row.LastError = error.Message;
                var retry = WorkflowPlatformPolicy.GetRetryDecision(row.AttemptCount, row.MaximumAttempts, now);
                row.Status = retry.Status;
                row.NextAttemptAt = retry.NextAttemptAt;
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
