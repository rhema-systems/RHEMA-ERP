using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Models;
using ErpSystem.Core.Services.Ehc.Sla;
using ErpSystem.Shared;
using ErpSystem.Web.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Services.Ehc;

/// <summary>
/// Polls Microsoft Graph (Microsoft 365) for inbound email and creates/appends EHC tickets with threading by conversationId.
/// </summary>
public sealed class EhcMicrosoft365InboundEmailBackgroundService : BackgroundService
{
    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex OriginalMessageRegex = new(
        @"^\s*-{2,}\s*Original Message\s*-{2,}\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex ReplyHeaderRegex = new(
        @"^\s*On\s.+\swrote:\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex OutlookHeaderRegex = new(
        @"^\s*From:\s.+\R\s*Sent:\s.+\R\s*To:\s.+\R\s*Subject:\s.+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly Regex SignatureRegex = new(
        @"\R--\s*\R.*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<Microsoft365InboundEmailOptions> _options;
    private readonly ILogger<EhcMicrosoft365InboundEmailBackgroundService> _logger;

    public EhcMicrosoft365InboundEmailBackgroundService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<Microsoft365InboundEmailOptions> options,
        ILogger<EhcMicrosoft365InboundEmailBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var opt = _options.CurrentValue;
            var delaySeconds = Math.Max(10, opt.PollingIntervalSeconds);

            if (!opt.IsConfigured)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                continue;
            }

            try
            {
                await PollOnceAsync(opt, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EHC inbound email poll failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }

    private async Task PollOnceAsync(Microsoft365InboundEmailOptions opt, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpSystem.Data.ApplicationDbContext>();
        var ticketRepo = scope.ServiceProvider.GetRequiredService<IEhcTicketRepository>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var storageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        var channels = await db.EhcInboundEmailChannels
            .Where(c => !c.IsDeleted && c.IsEnabled)
            .OrderBy(c => c.TenantId)
            .ThenBy(c => c.MailboxAddress)
            .ToListAsync(cancellationToken);

        if (channels.Count == 0)
        {
            return;
        }

        var token = await GetGraphTokenAsync(opt, cancellationToken);
        foreach (var channel in channels)
        {
            try
            {
                await ProcessChannelAsync(db, ticketRepo, userManager, storageService, opt, token, channel, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                channel.LastError = ex.Message;
                channel.ConsecutiveFailureCount++;
                channel.LastAttemptAtUtc ??= DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogError(ex, "Failed processing inbound email channel {ChannelId} ({Mailbox})", channel.Id, channel.MailboxAddress);
            }
        }
    }

    private async Task ProcessChannelAsync(
        ErpSystem.Data.ApplicationDbContext db,
        IEhcTicketRepository ticketRepo,
        UserManager<ApplicationUser> userManager,
        IFileStorageService storageService,
        Microsoft365InboundEmailOptions opt,
        AccessToken token,
        EhcInboundEmailChannel channel,
        CancellationToken cancellationToken)
    {
        channel.LastAttemptAtUtc = DateTime.UtcNow;
        var processed = 0;

        if (!channel.DefaultCategoryId.HasValue || channel.DefaultCategoryId.Value == Guid.Empty)
        {
            channel.LastError = "DefaultCategoryId is not configured.";
            channel.ConsecutiveFailureCount++;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var categoryExists = await db.EhcTicketCategories
            .AsNoTracking()
            .AnyAsync(c => c.Id == channel.DefaultCategoryId.Value && c.TenantId == channel.TenantId && !c.IsDeleted, cancellationToken);
        if (!categoryExists)
        {
            channel.LastError = "DefaultCategoryId is invalid.";
            channel.ConsecutiveFailureCount++;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var client = _httpClientFactory.CreateClient("MicrosoftGraph");
        var pageUrl = !string.IsNullOrWhiteSpace(channel.GraphDeltaLink)
            ? channel.GraphDeltaLink!.Trim()
            : BuildDeltaUrl(channel.MailboxAddress, opt.PageSize);

        string? deltaLink = null;
        var pages = 0;

        processed += await ProcessWebhookQueueAsync(
            db,
            ticketRepo,
            userManager,
            storageService,
            opt,
            token,
            client,
            channel,
            cancellationToken);

        while (!string.IsNullOrWhiteSpace(pageUrl) && pages < opt.MaxPagesPerPoll)
        {
            pages++;

            var (ok, body, statusCode) = await SendGraphJsonWithRetryAsync(
                client,
                token,
                () => new HttpRequestMessage(HttpMethod.Get, pageUrl),
                cancellationToken);

            if (!ok)
            {
                channel.LastError = $"Graph error {(int)statusCode}: {body}";
                channel.ConsecutiveFailureCount++;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("@odata.deltaLink", out var dl) && dl.ValueKind == JsonValueKind.String)
            {
                deltaLink = dl.GetString();
            }

            if (doc.RootElement.TryGetProperty("value", out var valueEl) && valueEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var msgEl in valueEl.EnumerateArray())
                {
                    if (msgEl.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var graphMessage = TryParseGraphMessage(msgEl);
                    if (graphMessage == null)
                    {
                        continue;
                    }

                    if (await ProcessMessageAsync(db, ticketRepo, userManager, storageService, opt, token, client, channel, graphMessage.Value, cancellationToken))
                    {
                        processed++;
                    }
                }
            }

            pageUrl = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl) && nl.ValueKind == JsonValueKind.String
                ? nl.GetString()
                : null;
        }

        if (!string.IsNullOrWhiteSpace(deltaLink))
        {
            channel.GraphDeltaLink = deltaLink;
        }

        channel.LastSyncedAtUtc = DateTime.UtcNow;
        channel.LastSuccessAtUtc = DateTime.UtcNow;
        channel.LastProcessedMessageCount = processed;
        channel.ConsecutiveFailureCount = 0;
        channel.LastError = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<(bool ok, string body, System.Net.HttpStatusCode statusCode)> SendGraphJsonWithRetryAsync(
        HttpClient client,
        AccessToken token,
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        var attempt = 0;
        var delay = TimeSpan.FromSeconds(1);

        while (true)
        {
            attempt++;
            using var req = createRequest();
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            using var res = await client.SendAsync(req, cancellationToken);
            var body = await res.Content.ReadAsStringAsync(cancellationToken);

            if (res.IsSuccessStatusCode)
            {
                return (true, body, res.StatusCode);
            }

            var isRetryable = res.StatusCode is System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.GatewayTimeout;
            if (!isRetryable || attempt >= maxAttempts)
            {
                return (false, body, res.StatusCode);
            }

            var retryAfter = res.Headers.RetryAfter?.Delta;
            var wait = retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero ? retryAfter.Value : delay;
            await Task.Delay(wait, cancellationToken);
            delay = TimeSpan.FromSeconds(Math.Min(30, delay.TotalSeconds * 2));
        }
    }

    private async Task<int> ProcessWebhookQueueAsync(
        ErpSystem.Data.ApplicationDbContext db,
        IEhcTicketRepository ticketRepo,
        UserManager<ApplicationUser> userManager,
        IFileStorageService storageService,
        Microsoft365InboundEmailOptions opt,
        AccessToken token,
        HttpClient graphClient,
        EhcInboundEmailChannel channel,
        CancellationToken cancellationToken)
    {
        try
        {
            var queued = await db.EhcInboundEmailWebhookQueueItems
                .Where(q => q.TenantId == channel.TenantId && q.ChannelId == channel.Id && !q.IsDeleted && q.ProcessedAtUtc == null)
                .OrderBy(q => q.ReceivedAtUtc)
                .Take(25)
                .ToListAsync(cancellationToken);

            if (queued.Count == 0)
            {
                return 0;
            }

            var processed = 0;
            foreach (var q in queued)
            {
                try
                {
                    var msg = await FetchGraphMessageByIdAsync(token, graphClient, channel.MailboxAddress, q.GraphMessageId, cancellationToken);
                    if (msg == null)
                    {
                        q.LastError = "Message not found or missing internetMessageId.";
                        q.ProcessedAtUtc = DateTime.UtcNow;
                        continue;
                    }

                    if (await ProcessMessageAsync(db, ticketRepo, userManager, storageService, opt, token, graphClient, channel, msg.Value, cancellationToken))
                    {
                        processed++;
                    }

                    q.LastError = null;
                    q.ProcessedAtUtc = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    q.LastError = ex.Message;
                    q.ProcessedAtUtc = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            return processed;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Webhook queue processing failed for channel {ChannelId}", channel.Id);
            return 0;
        }
    }

    private async Task<GraphMessage?> FetchGraphMessageByIdAsync(
        AccessToken token,
        HttpClient graphClient,
        string mailboxAddress,
        string graphMessageId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mailboxAddress) || string.IsNullOrWhiteSpace(graphMessageId))
        {
            return null;
        }

        var mailbox = Uri.EscapeDataString(mailboxAddress);
        var messageId = Uri.EscapeDataString(graphMessageId);
        var url = $"https://graph.microsoft.com/v1.0/users/{mailbox}/messages/{messageId}?$select=id,internetMessageId,conversationId,subject,body,from,receivedDateTime,hasAttachments";

        var (ok, body, _) = await SendGraphJsonWithRetryAsync(
            graphClient,
            token,
            () => new HttpRequestMessage(HttpMethod.Get, url),
            cancellationToken);

        if (!ok)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryParseGraphMessage(doc.RootElement);
    }

    private async Task<bool> ProcessMessageAsync(
        ErpSystem.Data.ApplicationDbContext db,
        IEhcTicketRepository ticketRepo,
        UserManager<ApplicationUser> userManager,
        IFileStorageService storageService,
        Microsoft365InboundEmailOptions opt,
        AccessToken token,
        HttpClient graphClient,
        EhcInboundEmailChannel channel,
        GraphMessage msg,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(msg.InternetMessageId))
        {
            return false;
        }

        var internetId = msg.InternetMessageId.Trim();
        var already = await db.EhcInboundEmailMessages
            .AsNoTracking()
            .AnyAsync(m =>
                    m.TenantId == channel.TenantId &&
                    m.ChannelId == channel.Id &&
                    !m.IsDeleted &&
                    m.InternetMessageId == internetId,
                cancellationToken);
        if (already)
        {
            return false;
        }

        Guid? ticketId = null;
        if (!string.IsNullOrWhiteSpace(msg.ConversationId))
        {
            var conv = msg.ConversationId.Trim();
            ticketId = await db.EhcInboundEmailMessages
                .AsNoTracking()
                .Where(m => m.TenantId == channel.TenantId && m.ChannelId == channel.Id && !m.IsDeleted && m.ConversationId == conv)
                .OrderByDescending(m => m.ReceivedAtUtc)
                .Select(m => (Guid?)m.TicketId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var sender = (msg.FromAddress ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(sender))
        {
            return false;
        }

        var requester = await ResolveOrCreateUserAsync(userManager, channel, sender, msg.FromName, cancellationToken);
        if (requester == null)
        {
            return false;
        }

        EhcTicket ticket;
        EhcTicketMessage ticketMessage;

        if (!ticketId.HasValue)
        {
            var created = await CreateTicketFromEmailAsync(db, ticketRepo, channel, requester.Id, msg, opt, cancellationToken);
            ticket = created.ticket;
            ticketMessage = created.message;
            ticketId = ticket.Id;
        }
        else
        {
            var existingTicket = await db.EhcTickets.FirstOrDefaultAsync(t => t.Id == ticketId.Value && t.TenantId == channel.TenantId && !t.IsDeleted, cancellationToken);
            if (existingTicket == null)
            {
                ticketId = null;
                return false;
            }

            ticket = existingTicket;

            ticket.UpdatedAt = DateTime.UtcNow;
            ticket.UpdatedBy = "EmailIngest";

            ticketMessage = new EhcTicketMessage
            {
                Id = Guid.NewGuid(),
                TenantId = channel.TenantId,
                TicketId = ticket.Id,
                Body = Truncate(NormalizeBody(msg.BodyContentType, msg.BodyContent), opt.MaxMessageBodyChars),
                IsInternal = false,
                AuthorUserId = requester.Id,
                CreatedAt = msg.ReceivedAtUtc ?? DateTime.UtcNow,
                CreatedBy = "EmailIngest",
                CreatedById = requester.Id
            };
            db.EhcTicketMessages.Add(ticketMessage);
        }

        if (msg.HasAttachments)
        {
            await TryIngestEmailAttachmentsAsync(
                db,
                storageService,
                opt,
                token,
                graphClient,
                channel,
                ticket,
                ticketMessage,
                msg,
                cancellationToken);
        }

        db.EhcInboundEmailMessages.Add(new EhcInboundEmailMessage
        {
            Id = Guid.NewGuid(),
            TenantId = channel.TenantId,
            ChannelId = channel.Id,
            GraphMessageId = msg.Id,
            InternetMessageId = internetId,
            ConversationId = string.IsNullOrWhiteSpace(msg.ConversationId) ? null : msg.ConversationId.Trim(),
            TicketId = ticketId.Value,
            FromAddress = sender,
            Subject = msg.Subject,
            ReceivedAtUtc = msg.ReceivedAtUtc ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "EmailIngest"
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<(EhcTicket ticket, EhcTicketMessage message)> CreateTicketFromEmailAsync(
        ErpSystem.Data.ApplicationDbContext db,
        IEhcTicketRepository ticketRepo,
        EhcInboundEmailChannel channel,
        Guid requesterUserId,
        GraphMessage msg,
        Microsoft365InboundEmailOptions opt,
        CancellationToken cancellationToken)
    {
        var now = msg.ReceivedAtUtc ?? DateTime.UtcNow;
        var ticketNumber = await ticketRepo.GenerateTicketNumberAsync(channel.TenantId, cancellationToken);

        var ticket = new EhcTicket
        {
            Id = Guid.NewGuid(),
            TenantId = channel.TenantId,
            TicketNumber = ticketNumber,
            TicketType = channel.DefaultTicketType,
            CategoryId = channel.DefaultCategoryId,
            Priority = channel.DefaultPriority,
            Source = EhcTicketSource.Email,
            Subject = string.IsNullOrWhiteSpace(msg.Subject) ? "(No subject)" : Truncate(msg.Subject.Trim(), 200),
            Description = Truncate(NormalizeBody(msg.BodyContentType, msg.BodyContent), opt.MaxMessageBodyChars),
            RequesterUserId = requesterUserId,
            Status = EhcTicketStatus.New,
            CreatedAt = now,
            CreatedBy = "EmailIngest",
            CreatedById = requesterUserId
        };

        var sla = await ResolveSlaTemplateAsync(db, channel, ticket, cancellationToken);
        if (sla != null)
        {
            ticket.FirstResponseDueAt = EhcSlaTimeCalculator.CalculateDueAtUtc(now, Math.Max(1, sla.FirstResponseMinutes), sla.CalendarConfigurationJson);
            ticket.ResolutionDueAt = EhcSlaTimeCalculator.CalculateDueAtUtc(now, Math.Max(1, sla.ResolutionMinutes), sla.CalendarConfigurationJson);
            ticket.AppliedSlaTemplateId = sla.Id;
            ticket.AppliedFirstResponseMinutes = sla.FirstResponseMinutes;
            ticket.AppliedResolutionMinutes = sla.ResolutionMinutes;
            ticket.AppliedSlaCalendarConfigurationJson = sla.CalendarConfigurationJson;
        }

        db.EhcTickets.Add(ticket);
        var message = new EhcTicketMessage
        {
            Id = Guid.NewGuid(),
            TenantId = channel.TenantId,
            TicketId = ticket.Id,
            Body = Truncate(NormalizeBody(msg.BodyContentType, msg.BodyContent), opt.MaxMessageBodyChars),
            IsInternal = false,
            AuthorUserId = requesterUserId,
            CreatedAt = now,
            CreatedBy = "EmailIngest",
            CreatedById = requesterUserId
        };
        db.EhcTicketMessages.Add(message);
        return (ticket, message);
    }

    private sealed record EffectiveUploadPolicy(bool IsEnabled, long MaxFileSizeBytes, HashSet<string> AllowedExtensions, HashSet<string> AllowedMimeTypes);

    private static readonly HashSet<string> DefaultAllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".txt", ".rtf",
        ".doc", ".docx",
        ".xls", ".xlsx", ".csv",
        ".png", ".jpg", ".jpeg", ".gif", ".webp"
    };

    private static async Task<EffectiveUploadPolicy> GetEffectivePolicyAsync(
        ErpSystem.Data.ApplicationDbContext db,
        Guid tenantId,
        string category,
        CancellationToken cancellationToken)
    {
        var policy = await db.FileUploadPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.Category == category)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        policy ??= await db.FileUploadPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.Category == "*")
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var enabled = policy?.IsEnabled ?? true;
        var maxSize = policy?.MaxFileSizeBytes ?? 10 * 1024 * 1024;

        HashSet<string> ParseExt(string? csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var ext = raw.Trim();
                if (string.IsNullOrWhiteSpace(ext)) continue;
                if (!ext.StartsWith('.')) ext = "." + ext;
                set.Add(ext.ToLowerInvariant());
            }
            return set;
        }

        HashSet<string> ParseMime(string? csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var v = raw.Trim();
                if (string.IsNullOrWhiteSpace(v)) continue;
                set.Add(v);
            }
            return set;
        }

        var allowedExt = ParseExt(policy?.AllowedExtensionsCsv);
        if (allowedExt.Count == 0)
        {
            allowedExt = new HashSet<string>(DefaultAllowedExtensions, StringComparer.OrdinalIgnoreCase);
        }

        var allowedMimes = ParseMime(policy?.AllowedMimeTypesCsv);

        return new EffectiveUploadPolicy(enabled, maxSize, allowedExt, allowedMimes);
    }

    private async Task TryIngestEmailAttachmentsAsync(
        ErpSystem.Data.ApplicationDbContext db,
        IFileStorageService storageService,
        Microsoft365InboundEmailOptions opt,
        AccessToken token,
        HttpClient graphClient,
        EhcInboundEmailChannel channel,
        EhcTicket ticket,
        EhcTicketMessage ticketMessage,
        GraphMessage msg,
        CancellationToken cancellationToken)
    {
        if (!opt.IngestAttachments) return;
        if (string.IsNullOrWhiteSpace(msg.Id)) return;

        var policy = await GetEffectivePolicyAsync(db, channel.TenantId, "ehc-ticket", cancellationToken);
        if (!policy.IsEnabled) return;

        var mailbox = Uri.EscapeDataString(channel.MailboxAddress);
        var messageId = Uri.EscapeDataString(msg.Id);
        var listUrl = $"https://graph.microsoft.com/v1.0/users/{mailbox}/messages/{messageId}/attachments?$top=50&$select=id,name,contentType,size,isInline,@odata.type";

        var (ok, listBody, statusCode) = await SendGraphJsonWithRetryAsync(
            graphClient,
            token,
            () => new HttpRequestMessage(HttpMethod.Get, listUrl),
            cancellationToken);
        if (!ok)
        {
            _logger.LogWarning("Graph attachments list failed for mailbox {Mailbox} message {MessageId}: {StatusCode}", channel.MailboxAddress, msg.Id, (int)statusCode);
            return;
        }

        using var doc = JsonDocument.Parse(listBody);
        if (!doc.RootElement.TryGetProperty("value", out var valueEl) || valueEl.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var processed = 0;
        foreach (var attEl in valueEl.EnumerateArray())
        {
            if (processed >= Math.Max(0, opt.MaxAttachmentsPerMessage))
            {
                break;
            }

            if (attEl.ValueKind != JsonValueKind.Object) continue;

            var odataType = attEl.TryGetProperty("@odata.type", out var tEl) && tEl.ValueKind == JsonValueKind.String ? tEl.GetString() : null;
            if (!string.Equals(odataType, "#microsoft.graph.fileAttachment", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isInline = attEl.TryGetProperty("isInline", out var inEl) && inEl.ValueKind == JsonValueKind.True;
            if (isInline) continue;

            var attId = attEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
            var name = attEl.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String ? nEl.GetString() : null;
            var contentType = attEl.TryGetProperty("contentType", out var ctEl) && ctEl.ValueKind == JsonValueKind.String ? ctEl.GetString() : null;
            var size = attEl.TryGetProperty("size", out var sEl) && sEl.ValueKind == JsonValueKind.Number ? sEl.GetInt64() : 0L;

            if (string.IsNullOrWhiteSpace(attId) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (size <= 0 || size > Math.Min(policy.MaxFileSizeBytes, opt.MaxAttachmentSizeBytes))
            {
                continue;
            }

            var ext = Path.GetExtension(name).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(ext) && policy.AllowedExtensions.Count > 0 && !policy.AllowedExtensions.Contains(ext))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(contentType) && policy.AllowedMimeTypes.Count > 0 && !policy.AllowedMimeTypes.Contains(contentType))
            {
                continue;
            }

            var detailUrl = $"https://graph.microsoft.com/v1.0/users/{mailbox}/messages/{messageId}/attachments/{Uri.EscapeDataString(attId)}?$select=id,name,contentType,size,contentBytes,isInline,@odata.type";
            var (ok2, detailBody, status2) = await SendGraphJsonWithRetryAsync(
                graphClient,
                token,
                () => new HttpRequestMessage(HttpMethod.Get, detailUrl),
                cancellationToken);
            if (!ok2)
            {
                _logger.LogWarning("Graph attachment fetch failed for mailbox {Mailbox} message {MessageId}: {StatusCode}", channel.MailboxAddress, msg.Id, (int)status2);
                continue;
            }

            using var detailDoc = JsonDocument.Parse(detailBody);
            var bytesB64 = detailDoc.RootElement.TryGetProperty("contentBytes", out var cbEl) && cbEl.ValueKind == JsonValueKind.String ? cbEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(bytesB64))
            {
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(bytesB64);
            }
            catch
            {
                continue;
            }

            if (bytes.Length <= 0 || bytes.LongLength != size)
            {
                // Best-effort: allow mismatch but enforce configured size caps.
                if (bytes.LongLength > Math.Min(policy.MaxFileSizeBytes, opt.MaxAttachmentSizeBytes))
                {
                    continue;
                }
            }

            await using var contentStream = new MemoryStream(bytes);
            var upload = await storageService.UploadFileAsync(new FileUploadRequest
            {
                FileStream = contentStream,
                FileName = name,
                ContentType = contentType ?? "application/octet-stream",
                FileSize = bytes.LongLength,
                Category = "ehc-ticket",
                TenantId = channel.TenantId.ToString(),
                OverwriteExisting = false
            });
            if (!upload.Success || string.IsNullOrWhiteSpace(upload.FilePath))
            {
                continue;
            }

            var now = DateTime.UtcNow;
            var uploaderId = ticketMessage.AuthorUserId ?? ticket.RequesterUserId;

            db.FileUploadRecords.Add(new FileUploadRecord
            {
                Id = Guid.NewGuid(),
                TenantId = channel.TenantId,
                Category = "ehc-ticket",
                FilePath = upload.FilePath,
                StoredFileName = upload.FileName,
                OriginalFileName = upload.OriginalFileName,
                ContentType = upload.ContentType,
                FileSize = upload.FileSize,
                StorageProvider = upload.StorageProvider,
                UploadedByUserId = uploaderId,
                VirusScanStatus = FileVirusScanStatus.Skipped,
                ScannedAtUtc = null,
                VirusScanMessage = null,
                CreatedAt = now,
                CreatedBy = "EmailIngest",
                CreatedById = uploaderId
            });

            db.EhcTicketAttachments.Add(new EhcTicketAttachment
            {
                Id = Guid.NewGuid(),
                TenantId = channel.TenantId,
                TicketId = ticket.Id,
                MessageId = ticketMessage.Id,
                FilePath = upload.FilePath,
                FileName = string.IsNullOrWhiteSpace(upload.OriginalFileName) ? name : upload.OriginalFileName,
                ContentType = upload.ContentType ?? contentType,
                FileSize = upload.FileSize > 0 ? upload.FileSize : bytes.LongLength,
                IsInternal = false,
                CreatedAt = now,
                CreatedBy = "EmailIngest",
                CreatedById = uploaderId
            });

            processed++;
        }
    }

    private static async Task<EhcSlaTemplate?> ResolveSlaTemplateAsync(
        ErpSystem.Data.ApplicationDbContext db,
        EhcInboundEmailChannel channel,
        EhcTicket ticket,
        CancellationToken cancellationToken)
    {
        var templates = await db.EhcSlaTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == channel.TenantId && t.IsActive && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        if (templates.Count == 0)
        {
            return null;
        }

        var candidates = templates
            .Where(t =>
                (!t.CategoryId.HasValue || t.CategoryId == ticket.CategoryId) &&
                (!t.TicketType.HasValue || t.TicketType == ticket.TicketType) &&
                (!t.Priority.HasValue || t.Priority == ticket.Priority))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates
            .Select(t => new
            {
                Template = t,
                Score =
                    (t.CategoryId.HasValue && t.CategoryId == ticket.CategoryId ? 100 : 0) +
                    (t.TicketType.HasValue && t.TicketType == ticket.TicketType ? 10 : 0) +
                    (t.Priority.HasValue && t.Priority == ticket.Priority ? 1 : 0)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Template.FirstResponseMinutes)
            .FirstOrDefault();

        return best?.Template;
    }

    private async Task<ApplicationUser?> ResolveOrCreateUserAsync(
        UserManager<ApplicationUser> userManager,
        EhcInboundEmailChannel channel,
        string email,
        string? displayName,
        CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.TenantId == channel.TenantId && u.Email != null && u.Email.ToLower() == email, cancellationToken);

        if (user != null)
        {
            return user;
        }

        if (channel.RequireKnownSender)
        {
            channel.LastError = $"Unknown sender '{email}' (RequireKnownSender=true).";
            return null;
        }

        if (!channel.AutoProvisionUnknownSenders)
        {
            channel.LastError = $"Unknown sender '{email}' (AutoProvisionUnknownSenders=false).";
            return null;
        }

        var (firstName, lastName) = SplitName(displayName, email);

        user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = channel.TenantId,
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            AuthenticationProvider = AuthenticationProvider.Local,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "EmailIngest"
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            channel.LastError = $"Failed to auto-provision sender '{email}': {string.Join("; ", result.Errors.Select(e => e.Description))}";
            return null;
        }

        return user;
    }

    private static (string firstName, string lastName) SplitName(string? displayName, string email)
    {
        var cleaned = (displayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            var local = email.Split('@').FirstOrDefault() ?? "User";
            return (local, "Email");
        }

        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return (parts[0], "Email");
        }

        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private static string BuildDeltaUrl(string mailboxAddress, int pageSize)
    {
        var u = Uri.EscapeDataString(mailboxAddress);
        var top = Math.Clamp(pageSize, 1, 100);
        return $"https://graph.microsoft.com/v1.0/users/{u}/mailFolders/inbox/messages/delta?$top={top}&$select=id,internetMessageId,conversationId,subject,body,from,receivedDateTime,hasAttachments";
    }

    private static async Task<AccessToken> GetGraphTokenAsync(Microsoft365InboundEmailOptions opt, CancellationToken cancellationToken)
    {
        var credential = new ClientSecretCredential(opt.TenantId, opt.ClientId, opt.ClientSecret);
        var ctx = new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
        return await credential.GetTokenAsync(ctx, cancellationToken);
    }

    private static GraphMessage? TryParseGraphMessage(JsonElement el)
    {
        var id = el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
        var internetMessageId = el.TryGetProperty("internetMessageId", out var imEl) && imEl.ValueKind == JsonValueKind.String ? imEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(internetMessageId))
        {
            return null;
        }

        var conversationId = el.TryGetProperty("conversationId", out var cEl) && cEl.ValueKind == JsonValueKind.String ? cEl.GetString() : null;
        var subject = el.TryGetProperty("subject", out var sEl) && sEl.ValueKind == JsonValueKind.String ? sEl.GetString() : null;

        string? fromAddress = null;
        string? fromName = null;
        if (el.TryGetProperty("from", out var fromEl) && fromEl.ValueKind == JsonValueKind.Object &&
            fromEl.TryGetProperty("emailAddress", out var eaEl) && eaEl.ValueKind == JsonValueKind.Object)
        {
            if (eaEl.TryGetProperty("address", out var aEl) && aEl.ValueKind == JsonValueKind.String)
            {
                fromAddress = aEl.GetString();
            }

            if (eaEl.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String)
            {
                fromName = nEl.GetString();
            }
        }

        DateTime? receivedUtc = null;
        if (el.TryGetProperty("receivedDateTime", out var rEl) && rEl.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(rEl.GetString(), out var parsed))
        {
            receivedUtc = parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
        }

        var bodyType = "text";
        var bodyContent = string.Empty;
        if (el.TryGetProperty("body", out var bEl) && bEl.ValueKind == JsonValueKind.Object)
        {
            if (bEl.TryGetProperty("contentType", out var ctEl) && ctEl.ValueKind == JsonValueKind.String)
            {
                bodyType = ctEl.GetString() ?? "text";
            }
            if (bEl.TryGetProperty("content", out var c2El) && c2El.ValueKind == JsonValueKind.String)
            {
                bodyContent = c2El.GetString() ?? string.Empty;
            }
        }

        var hasAttachments = el.TryGetProperty("hasAttachments", out var haEl) && haEl.ValueKind == JsonValueKind.True;

        return new GraphMessage(
            Id: id!,
            InternetMessageId: internetMessageId!,
            ConversationId: conversationId,
            Subject: subject,
            FromAddress: fromAddress,
            FromName: fromName,
            ReceivedAtUtc: receivedUtc,
            BodyContentType: bodyType,
            BodyContent: bodyContent,
            HasAttachments: hasAttachments);
    }

    private static string NormalizeBody(string? contentType, string? content)
    {
        var text = content ?? string.Empty;
        if (string.Equals(contentType, "html", StringComparison.OrdinalIgnoreCase))
        {
            text = HtmlTagRegex.Replace(text, string.Empty);
        }

        text = System.Net.WebUtility.HtmlDecode(text).Trim();
        text = StripQuotedText(text);
        return text.Trim();
    }

    private static string StripQuotedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var working = text.Replace("\r\n", "\n");

        var cutAt = int.MaxValue;
        foreach (var m in new[]
        {
            ReplyHeaderRegex.Match(working),
            OriginalMessageRegex.Match(working),
            OutlookHeaderRegex.Match(working)
        })
        {
            if (m.Success)
            {
                cutAt = Math.Min(cutAt, m.Index);
            }
        }

        if (cutAt != int.MaxValue && cutAt >= 0)
        {
            working = working.Substring(0, cutAt);
        }

        working = SignatureRegex.Replace(working, string.Empty);
        working = working.Replace("\n", Environment.NewLine);
        return working.Trim();
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        if (max < 1)
        {
            return string.Empty;
        }

        return s.Length <= max ? s : s.Substring(0, max);
    }

    private readonly record struct GraphMessage(
        string Id,
        string InternetMessageId,
        string? ConversationId,
        string? Subject,
        string? FromAddress,
        string? FromName,
        DateTime? ReceivedAtUtc,
        string? BodyContentType,
        string? BodyContent,
        bool HasAttachments);
}
