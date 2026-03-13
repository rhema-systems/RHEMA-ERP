using System.Text.Json;
using ErpSystem.Core.Entities.Ehc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/inbound/microsoft365/webhook")]
[AllowAnonymous]
public sealed class EhcMicrosoft365InboundEmailWebhookController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ILogger<EhcMicrosoft365InboundEmailWebhookController> _logger;

    public EhcMicrosoft365InboundEmailWebhookController(
        ErpSystem.Data.ApplicationDbContext db,
        ILogger<EhcMicrosoft365InboundEmailWebhookController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Validate([FromQuery] string? validationToken)
    {
        if (!string.IsNullOrWhiteSpace(validationToken))
        {
            return Content(validationToken, "text/plain");
        }

        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromQuery] string? validationToken, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(validationToken))
        {
            // Graph validation may arrive as POST in some setups; respond with the token as plain text.
            return Content(validationToken, "text/plain");
        }

        try
        {
            if (!body.TryGetProperty("value", out var valueEl) || valueEl.ValueKind != JsonValueKind.Array)
            {
                return Ok();
            }

            var now = DateTime.UtcNow;
            var anyChanges = false;

            foreach (var n in valueEl.EnumerateArray())
            {
                if (n.ValueKind != JsonValueKind.Object) continue;

                var subscriptionId = n.TryGetProperty("subscriptionId", out var sEl) && sEl.ValueKind == JsonValueKind.String ? sEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(subscriptionId)) continue;

                var clientState = n.TryGetProperty("clientState", out var csEl) && csEl.ValueKind == JsonValueKind.String ? csEl.GetString() : null;

                string? messageId = null;
                if (n.TryGetProperty("resourceData", out var rdEl) && rdEl.ValueKind == JsonValueKind.Object &&
                    rdEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                {
                    messageId = idEl.GetString();
                }

                if (string.IsNullOrWhiteSpace(messageId)) continue;

                var channel = await _db.EhcInboundEmailChannels
                    .FirstOrDefaultAsync(c => !c.IsDeleted && c.IsEnabled && c.GraphSubscriptionId == subscriptionId, cancellationToken);
                if (channel == null) continue;

                if (!string.IsNullOrWhiteSpace(channel.GraphClientState) &&
                    !string.IsNullOrWhiteSpace(clientState) &&
                    !string.Equals(channel.GraphClientState, clientState, StringComparison.Ordinal))
                {
                    _logger.LogWarning("Graph webhook clientState mismatch for channel {ChannelId}", channel.Id);
                    continue;
                }

                channel.LastWebhookReceivedAtUtc = now;

                var exists = await _db.EhcInboundEmailWebhookQueueItems
                    .AsNoTracking()
                    .AnyAsync(q =>
                        q.TenantId == channel.TenantId &&
                        q.ChannelId == channel.Id &&
                        !q.IsDeleted &&
                        q.ProcessedAtUtc == null &&
                        q.GraphMessageId == messageId,
                        cancellationToken);

                if (!exists)
                {
                    _db.EhcInboundEmailWebhookQueueItems.Add(new EhcInboundEmailWebhookQueueItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = channel.TenantId,
                        ChannelId = channel.Id,
                        GraphMessageId = messageId,
                        ReceivedAtUtc = now,
                        CreatedAt = now,
                        CreatedBy = "GraphWebhook"
                    });
                }

                anyChanges = true;
            }

            if (anyChanges)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Microsoft 365 webhook notification");
            return Ok(); // Graph expects 2xx quickly; avoid retries storm
        }
    }
}

