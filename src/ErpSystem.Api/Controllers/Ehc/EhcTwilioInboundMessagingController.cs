using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Ehc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/inbound/twilio")]
[AllowAnonymous]
public sealed class EhcTwilioInboundMessagingController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly IEhcTicketRepository _ticketRepo;
    private readonly ILogger<EhcTwilioInboundMessagingController> _logger;

    public EhcTwilioInboundMessagingController(
        ErpSystem.Data.ApplicationDbContext db,
        IEhcTicketRepository ticketRepo,
        ILogger<EhcTwilioInboundMessagingController> logger)
    {
        _db = db;
        _ticketRepo = ticketRepo;
        _logger = logger;
    }

    public sealed class TwilioInboundMessageForm
    {
        public string? MessageSid { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Body { get; set; }
    }

    [HttpPost("messages")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> ReceiveMessage([FromForm] TwilioInboundMessageForm form, CancellationToken cancellationToken)
    {
        try
        {
            var sid = (form.MessageSid ?? string.Empty).Trim();
            var fromRaw = (form.From ?? string.Empty).Trim();
            var toRaw = (form.To ?? string.Empty).Trim();
            var body = (form.Body ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(fromRaw) || string.IsNullOrWhiteSpace(toRaw) || string.IsNullOrWhiteSpace(body))
            {
                return Ok();
            }

            var source = (fromRaw.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase) || toRaw.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
                ? EhcTicketSource.WhatsApp
                : EhcTicketSource.Sms;

            var from = NormalizeAddress(fromRaw);
            var to = NormalizeAddress(toRaw);

            var channel = await _db.EhcInboundMessagingChannels
                .FirstOrDefaultAsync(c =>
                        !c.IsDeleted &&
                        c.IsEnabled &&
                        c.Provider == EhcInboundMessagingProvider.Twilio &&
                        c.Source == source &&
                        c.ToAddress == to,
                    cancellationToken);

            if (channel == null)
            {
                _logger.LogInformation("Twilio inbound message ignored (no channel). To={To} Source={Source}", to, source);
                return Ok();
            }

            var already = await _db.EhcInboundMessagingMessages
                .AsNoTracking()
                .AnyAsync(m => m.TenantId == channel.TenantId && m.ChannelId == channel.Id && !m.IsDeleted && m.ProviderMessageId == sid, cancellationToken);
            if (already)
            {
                return Ok();
            }

            var requester = await _db.Users
                .AsNoTracking()
                .Where(u => u.TenantId == channel.TenantId && u.IsActive && u.PhoneNumber == from)
                .Select(u => new { u.Id, u.UserName })
                .FirstOrDefaultAsync(cancellationToken);

            if (requester == null)
            {
                if (channel.RequireKnownSender)
                {
                    _logger.LogInformation("Twilio inbound message rejected (unknown sender). Tenant={TenantId} From={From}", channel.TenantId, from);
                    return Ok();
                }

                _logger.LogInformation("Twilio inbound message ignored (auto-provision not enabled). Tenant={TenantId} From={From}", channel.TenantId, from);
                return Ok();
            }

            var now = DateTime.UtcNow;

            // Basic threading: continue the most recent ticket for this sender on this channel (last 30 days).
            var existingTicketId = await _db.EhcInboundMessagingMessages
                .AsNoTracking()
                .Where(m => m.TenantId == channel.TenantId && m.ChannelId == channel.Id && !m.IsDeleted && m.FromAddress == from && m.ReceivedAtUtc >= now.AddDays(-30))
                .OrderByDescending(m => m.ReceivedAtUtc)
                .Select(m => (Guid?)m.TicketId)
                .FirstOrDefaultAsync(cancellationToken);

            EhcTicket ticket;
            if (existingTicketId.HasValue)
            {
                ticket = await _db.EhcTickets.FirstOrDefaultAsync(t => t.TenantId == channel.TenantId && t.Id == existingTicketId.Value && !t.IsDeleted, cancellationToken)
                         ?? await CreateNewTicketAsync(channel, requester.Id, requester.UserName, source, body, now, cancellationToken);
            }
            else
            {
                ticket = await CreateNewTicketAsync(channel, requester.Id, requester.UserName, source, body, now, cancellationToken);
            }

            _db.EhcTicketMessages.Add(new EhcTicketMessage
            {
                Id = Guid.NewGuid(),
                TenantId = channel.TenantId,
                TicketId = ticket.Id,
                Body = body,
                IsInternal = false,
                AuthorUserId = requester.Id,
                CreatedAt = now,
                CreatedBy = "TwilioIngest",
                CreatedById = requester.Id
            });

            _db.EhcInboundMessagingMessages.Add(new EhcInboundMessagingMessage
            {
                Id = Guid.NewGuid(),
                TenantId = channel.TenantId,
                ChannelId = channel.Id,
                ProviderMessageId = sid,
                FromAddress = from,
                ToAddress = to,
                Body = body,
                ReceivedAtUtc = now,
                TicketId = ticket.Id,
                CreatedAt = now,
                CreatedBy = "TwilioIngest",
                CreatedById = requester.Id
            });

            await _db.SaveChangesAsync(cancellationToken);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Twilio inbound message");
            return Ok();
        }
    }

    private async Task<EhcTicket> CreateNewTicketAsync(
        EhcInboundMessagingChannel channel,
        Guid requesterUserId,
        string? requesterUserName,
        EhcTicketSource source,
        string body,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var ticketNumber = await _ticketRepo.GenerateTicketNumberAsync(channel.TenantId, cancellationToken);
        var subject = body.Length <= 120 ? body : body.Substring(0, 120).TrimEnd() + "…";

        var ticket = new EhcTicket
        {
            Id = Guid.NewGuid(),
            TenantId = channel.TenantId,
            TicketNumber = ticketNumber,
            TicketType = channel.DefaultTicketType,
            CategoryId = channel.DefaultCategoryId,
            Priority = channel.DefaultPriority,
            Source = source,
            Subject = subject,
            Description = body,
            RequesterUserId = requesterUserId,
            Status = EhcTicketStatus.New,
            CreatedAt = now,
            CreatedBy = requesterUserName ?? "TwilioIngest",
            CreatedById = requesterUserId
        };

        _db.EhcTickets.Add(ticket);
        return ticket;
    }

    private static string NormalizeAddress(string address)
    {
        var trimmed = (address ?? string.Empty).Trim();
        if (trimmed.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring("whatsapp:".Length);
        }
        return trimmed.Trim();
    }
}

