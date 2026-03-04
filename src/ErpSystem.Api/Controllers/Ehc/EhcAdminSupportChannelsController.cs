using System.Net.Mail;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using ErpSystem.Web.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/channels")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminSupportChannelsController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<Microsoft365InboundEmailOptions> _m365Options;
    private readonly ILogger<EhcAdminSupportChannelsController> _logger;

    public EhcAdminSupportChannelsController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<Microsoft365InboundEmailOptions> m365Options,
        ILogger<EhcAdminSupportChannelsController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _httpClientFactory = httpClientFactory;
        _m365Options = m365Options;
        _logger = logger;
    }

    public sealed class EhcInboundEmailChannelDto
    {
        public Guid Id { get; set; }
        public string MailboxAddress { get; set; } = string.Empty;
        public string FolderName { get; set; } = "Inbox";
        public bool IsEnabled { get; set; }
        public bool RequireKnownSender { get; set; }
        public bool AutoProvisionUnknownSenders { get; set; }
        public Guid? DefaultCategoryId { get; set; }
        public EhcTicketType DefaultTicketType { get; set; }
        public EhcTicketPriority DefaultPriority { get; set; }
        public bool UseGraphWebhook { get; set; }
        public string? GraphSubscriptionId { get; set; }
        public DateTime? GraphSubscriptionExpiresAtUtc { get; set; }
        public DateTime? LastWebhookReceivedAtUtc { get; set; }
        public DateTime? LastAttemptAtUtc { get; set; }
        public DateTime? LastSuccessAtUtc { get; set; }
        public int LastProcessedMessageCount { get; set; }
        public int ConsecutiveFailureCount { get; set; }
        public DateTime? LastSyncedAtUtc { get; set; }
        public string? LastError { get; set; }
    }

    public sealed class UpsertEhcInboundEmailChannelRequestDto
    {
        public string MailboxAddress { get; set; } = string.Empty;
        public string? FolderName { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool RequireKnownSender { get; set; } = true;
        public bool AutoProvisionUnknownSenders { get; set; } = false;
        public Guid? DefaultCategoryId { get; set; }
        public EhcTicketType DefaultTicketType { get; set; } = EhcTicketType.Helpdesk;
        public EhcTicketPriority DefaultPriority { get; set; } = EhcTicketPriority.Medium;
        public bool UseGraphWebhook { get; set; } = false;
    }

    [HttpGet("email-inbound")]
    public async Task<ActionResult> ListInboundEmail(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcInboundEmailChannelDto>() });
        }

        var items = await _db.EhcInboundEmailChannels
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .OrderByDescending(c => c.IsEnabled)
            .ThenBy(c => c.MailboxAddress)
            .ToListAsync(cancellationToken);

        var data = items.Select(c => new EhcInboundEmailChannelDto
        {
            Id = c.Id,
            MailboxAddress = c.MailboxAddress,
            FolderName = c.FolderName,
            IsEnabled = c.IsEnabled,
            RequireKnownSender = c.RequireKnownSender,
            AutoProvisionUnknownSenders = c.AutoProvisionUnknownSenders,
            DefaultCategoryId = c.DefaultCategoryId,
            DefaultTicketType = c.DefaultTicketType,
            DefaultPriority = c.DefaultPriority,
            UseGraphWebhook = c.UseGraphWebhook,
            GraphSubscriptionId = c.GraphSubscriptionId,
            GraphSubscriptionExpiresAtUtc = c.GraphSubscriptionExpiresAtUtc,
            LastWebhookReceivedAtUtc = c.LastWebhookReceivedAtUtc,
            LastAttemptAtUtc = c.LastAttemptAtUtc,
            LastSuccessAtUtc = c.LastSuccessAtUtc,
            LastProcessedMessageCount = c.LastProcessedMessageCount,
            ConsecutiveFailureCount = c.ConsecutiveFailureCount,
            LastSyncedAtUtc = c.LastSyncedAtUtc,
            LastError = c.LastError
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpPost("email-inbound")]
    public async Task<ActionResult> CreateInboundEmail([FromBody] UpsertEhcInboundEmailChannelRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            if (!TryValidateMailbox(request.MailboxAddress, out var mailbox, out var mailboxError))
            {
                return BadRequest(new { success = false, message = mailboxError });
            }

            if (!request.DefaultCategoryId.HasValue || request.DefaultCategoryId.Value == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "DefaultCategoryId is required for inbound email." });
            }

            var folderName = string.IsNullOrWhiteSpace(request.FolderName) ? "Inbox" : request.FolderName.Trim();
            if (!string.Equals(folderName, "Inbox", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Only Inbox folder is supported at the moment." });
            }

            var exists = await _db.EhcInboundEmailChannels
                .AsNoTracking()
                .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.MailboxAddress == mailbox, cancellationToken);
            if (exists)
            {
                return BadRequest(new { success = false, message = "This mailbox is already configured." });
            }

            var now = DateTime.UtcNow;
            var channel = new EhcInboundEmailChannel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MailboxAddress = mailbox,
                FolderName = "Inbox",
                IsEnabled = request.IsEnabled,
                RequireKnownSender = request.RequireKnownSender,
                AutoProvisionUnknownSenders = request.AutoProvisionUnknownSenders,
                DefaultCategoryId = request.DefaultCategoryId,
                DefaultTicketType = request.DefaultTicketType,
                DefaultPriority = request.DefaultPriority,
                UseGraphWebhook = request.UseGraphWebhook,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System"
            };

            _db.EhcInboundEmailChannels.Add(channel);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new EhcInboundEmailChannelDto
                {
                    Id = channel.Id,
                    MailboxAddress = channel.MailboxAddress,
                    FolderName = channel.FolderName,
                    IsEnabled = channel.IsEnabled,
                    RequireKnownSender = channel.RequireKnownSender,
                    AutoProvisionUnknownSenders = channel.AutoProvisionUnknownSenders,
                    DefaultCategoryId = channel.DefaultCategoryId,
                    DefaultTicketType = channel.DefaultTicketType,
                    DefaultPriority = channel.DefaultPriority,
                    UseGraphWebhook = channel.UseGraphWebhook,
                    GraphSubscriptionId = channel.GraphSubscriptionId,
                    GraphSubscriptionExpiresAtUtc = channel.GraphSubscriptionExpiresAtUtc,
                    LastWebhookReceivedAtUtc = channel.LastWebhookReceivedAtUtc,
                    LastAttemptAtUtc = channel.LastAttemptAtUtc,
                    LastSuccessAtUtc = channel.LastSuccessAtUtc,
                    LastProcessedMessageCount = channel.LastProcessedMessageCount,
                    ConsecutiveFailureCount = channel.ConsecutiveFailureCount,
                    LastSyncedAtUtc = channel.LastSyncedAtUtc,
                    LastError = channel.LastError
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inbound email channel");
            return StatusCode(500, new { success = false, message = "Failed to create inbound email channel" });
        }
    }

    [HttpPut("email-inbound/{id:guid}")]
    public async Task<ActionResult> UpdateInboundEmail(Guid id, [FromBody] UpsertEhcInboundEmailChannelRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var channel = await _db.EhcInboundEmailChannels.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
            if (channel == null)
            {
                return NotFound(new { success = false, message = "Channel not found." });
            }

            if (!TryValidateMailbox(request.MailboxAddress, out var mailbox, out var mailboxError))
            {
                return BadRequest(new { success = false, message = mailboxError });
            }

            if (!request.DefaultCategoryId.HasValue || request.DefaultCategoryId.Value == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "DefaultCategoryId is required for inbound email." });
            }

            var folderName = string.IsNullOrWhiteSpace(request.FolderName) ? "Inbox" : request.FolderName.Trim();
            if (!string.Equals(folderName, "Inbox", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Only Inbox folder is supported at the moment." });
            }

            var exists = await _db.EhcInboundEmailChannels
                .AsNoTracking()
                .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Id != id && c.MailboxAddress == mailbox, cancellationToken);
            if (exists)
            {
                return BadRequest(new { success = false, message = "This mailbox is already configured." });
            }

            channel.MailboxAddress = mailbox;
            channel.FolderName = "Inbox";
            channel.IsEnabled = request.IsEnabled;
            channel.RequireKnownSender = request.RequireKnownSender;
            channel.AutoProvisionUnknownSenders = request.AutoProvisionUnknownSenders;
            channel.DefaultCategoryId = request.DefaultCategoryId;
            channel.DefaultTicketType = request.DefaultTicketType;
            channel.DefaultPriority = request.DefaultPriority;
            channel.UseGraphWebhook = request.UseGraphWebhook;
            channel.UpdatedAt = DateTime.UtcNow;
            channel.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inbound email channel {ChannelId}", id);
            return StatusCode(500, new { success = false, message = "Failed to update inbound email channel" });
        }
    }

    [HttpDelete("email-inbound/{id:guid}")]
    public async Task<ActionResult> DeleteInboundEmail(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var channel = await _db.EhcInboundEmailChannels.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
            if (channel == null)
            {
                return NotFound(new { success = false, message = "Channel not found." });
            }

            channel.IsDeleted = true;
            channel.DeletedAt = DateTime.UtcNow;
            channel.DeletedBy = _currentUserService.UserName ?? "System";
            channel.IsEnabled = false;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inbound email channel {ChannelId}", id);
            return StatusCode(500, new { success = false, message = "Failed to delete inbound email channel" });
        }
    }

    [HttpPost("email-inbound/{id:guid}/test")]
    public async Task<ActionResult> TestInboundEmail(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var channel = await _db.EhcInboundEmailChannels
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
            if (channel == null)
            {
                return NotFound(new { success = false, message = "Channel not found." });
            }

            var opt = _m365Options.CurrentValue;
            if (!opt.IsConfigured)
            {
                return BadRequest(new { success = false, message = "Microsoft365InboundEmail is not configured in the API settings." });
            }

            var token = await GetGraphTokenAsync(opt, cancellationToken);
            var client = _httpClientFactory.CreateClient("MicrosoftGraph");

            var mailbox = Uri.EscapeDataString(channel.MailboxAddress);
            var url = $"https://graph.microsoft.com/v1.0/users/{mailbox}/mailFolders/inbox?$select=id,displayName,totalItemCount,unreadItemCount";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            using var res = await client.SendAsync(req, cancellationToken);
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                return BadRequest(new { success = false, message = $"Graph error {(int)res.StatusCode}: {body}" });
            }

            using var doc = JsonDocument.Parse(body);
            return Ok(new { success = true, data = doc.RootElement });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing inbound email channel {ChannelId}", id);
            return StatusCode(500, new { success = false, message = "Failed to test channel." });
        }
    }

    public sealed class SubscribeWebhookRequestDto
    {
        public int? ExpiresInMinutes { get; set; }
    }

    [HttpPost("email-inbound/{id:guid}/webhook/subscribe")]
    public async Task<ActionResult> SubscribeWebhook(Guid id, [FromBody] SubscribeWebhookRequestDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var channel = await _db.EhcInboundEmailChannels
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
            if (channel == null)
            {
                return NotFound(new { success = false, message = "Channel not found." });
            }

            var opt = _m365Options.CurrentValue;
            if (!opt.IsConfigured)
            {
                return BadRequest(new { success = false, message = "Microsoft365InboundEmail is not configured in the API settings." });
            }

            if (string.IsNullOrWhiteSpace(opt.WebhookBaseUrl) || !Uri.TryCreate(opt.WebhookBaseUrl, UriKind.Absolute, out var baseUri))
            {
                return BadRequest(new { success = false, message = "Microsoft365InboundEmail.WebhookBaseUrl is required to use webhooks." });
            }

            var notificationUrl = new Uri(baseUri, "/api/ehc/inbound/microsoft365/webhook").ToString();
            var expiresMinutes = Math.Clamp(dto?.ExpiresInMinutes ?? 60 * 24, 10, 60 * 72);
            var expiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);

            channel.GraphClientState ??= Guid.NewGuid().ToString("N");

            var token = await GetGraphTokenAsync(opt, cancellationToken);
            var client = _httpClientFactory.CreateClient("MicrosoftGraph");

            var resource = $"users/{channel.MailboxAddress}/mailFolders('Inbox')/messages";
            var payload = new
            {
                changeType = "created",
                notificationUrl,
                resource,
                expirationDateTime = expiresAt.ToString("o"),
                clientState = channel.GraphClientState
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/subscriptions");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            using var res = await client.SendAsync(req, cancellationToken);
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                return BadRequest(new { success = false, message = $"Graph error {(int)res.StatusCode}: {body}" });
            }

            using var doc = JsonDocument.Parse(body);
            var subscriptionId = doc.RootElement.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
            var exp = doc.RootElement.TryGetProperty("expirationDateTime", out var expEl) && expEl.ValueKind == JsonValueKind.String ? expEl.GetString() : null;

            channel.GraphSubscriptionId = subscriptionId;
            channel.GraphSubscriptionExpiresAtUtc = DateTime.TryParse(exp, out var parsed) ? (parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime()) : expiresAt;
            channel.UseGraphWebhook = true;
            channel.UpdatedAt = DateTime.UtcNow;
            channel.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, data = new { channel.GraphSubscriptionId, channel.GraphSubscriptionExpiresAtUtc, notificationUrl } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing Graph webhook for channel {ChannelId}", id);
            return StatusCode(500, new { success = false, message = "Failed to subscribe webhook." });
        }
    }

    private static bool TryValidateMailbox(string mailboxAddress, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(mailboxAddress))
        {
            error = "MailboxAddress is required.";
            return false;
        }

        var trimmed = mailboxAddress.Trim();
        try
        {
            var addr = new MailAddress(trimmed);
            normalized = addr.Address.Trim().ToLowerInvariant();
            return true;
        }
        catch
        {
            error = "MailboxAddress must be a valid email address.";
            return false;
        }
    }

    private static async Task<AccessToken> GetGraphTokenAsync(Microsoft365InboundEmailOptions opt, CancellationToken cancellationToken)
    {
        var credential = new ClientSecretCredential(opt.TenantId, opt.ClientId, opt.ClientSecret);
        var ctx = new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
        return await credential.GetTokenAsync(ctx, cancellationToken);
    }
}
