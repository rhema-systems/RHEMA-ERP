using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/admin/channels/messaging-inbound")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin)]
public sealed class EhcAdminMessagingChannelsController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcAdminMessagingChannelsController> _logger;

    public EhcAdminMessagingChannelsController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcAdminMessagingChannelsController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public sealed class EhcInboundMessagingChannelDto
    {
        public Guid Id { get; set; }
        public EhcInboundMessagingProvider Provider { get; set; }
        public EhcTicketSource Source { get; set; }
        public string ToAddress { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool RequireKnownSender { get; set; }
        public bool AutoProvisionUnknownSenders { get; set; }
        public Guid? DefaultCategoryId { get; set; }
        public EhcTicketType DefaultTicketType { get; set; }
        public EhcTicketPriority DefaultPriority { get; set; }
    }

    public sealed class UpsertEhcInboundMessagingChannelRequestDto
    {
        public EhcInboundMessagingProvider Provider { get; set; } = EhcInboundMessagingProvider.Twilio;
        public EhcTicketSource Source { get; set; } = EhcTicketSource.Sms;
        public string ToAddress { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public bool RequireKnownSender { get; set; } = true;
        public bool AutoProvisionUnknownSenders { get; set; } = false;
        public Guid? DefaultCategoryId { get; set; }
        public EhcTicketType DefaultTicketType { get; set; } = EhcTicketType.Helpdesk;
        public EhcTicketPriority DefaultPriority { get; set; } = EhcTicketPriority.Medium;
    }

    [HttpGet]
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Ok(new { success = true, data = Array.Empty<EhcInboundMessagingChannelDto>() });
        }

        var items = await _db.EhcInboundMessagingChannels
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .OrderByDescending(c => c.IsEnabled)
            .ThenBy(c => c.Source)
            .ThenBy(c => c.ToAddress)
            .ToListAsync(cancellationToken);

        var data = items.Select(c => new EhcInboundMessagingChannelDto
        {
            Id = c.Id,
            Provider = c.Provider,
            Source = c.Source,
            ToAddress = c.ToAddress,
            IsEnabled = c.IsEnabled,
            RequireKnownSender = c.RequireKnownSender,
            AutoProvisionUnknownSenders = c.AutoProvisionUnknownSenders,
            DefaultCategoryId = c.DefaultCategoryId,
            DefaultTicketType = c.DefaultTicketType,
            DefaultPriority = c.DefaultPriority
        }).ToList();

        return Ok(new { success = true, data });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] UpsertEhcInboundMessagingChannelRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });

            if (string.IsNullOrWhiteSpace(request.ToAddress))
            {
                return BadRequest(new { success = false, message = "ToAddress is required." });
            }

            if (request.Source is not (EhcTicketSource.Sms or EhcTicketSource.WhatsApp))
            {
                return BadRequest(new { success = false, message = "Source must be Sms or WhatsApp." });
            }

            if (!request.DefaultCategoryId.HasValue || request.DefaultCategoryId.Value == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "DefaultCategoryId is required." });
            }

            var to = NormalizeAddress(request.ToAddress);
            var exists = await _db.EhcInboundMessagingChannels
                .AsNoTracking()
                .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Provider == request.Provider && c.Source == request.Source && c.ToAddress == to, cancellationToken);
            if (exists)
            {
                return BadRequest(new { success = false, message = "This destination address is already configured." });
            }

            var now = DateTime.UtcNow;
            var entity = new EhcInboundMessagingChannel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = request.Provider,
                Source = request.Source,
                ToAddress = to,
                IsEnabled = request.IsEnabled,
                RequireKnownSender = request.RequireKnownSender,
                AutoProvisionUnknownSenders = request.AutoProvisionUnknownSenders,
                DefaultCategoryId = request.DefaultCategoryId,
                DefaultTicketType = request.DefaultTicketType,
                DefaultPriority = request.DefaultPriority,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "System"
            };

            _db.EhcInboundMessagingChannels.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, data = new EhcInboundMessagingChannelDto
            {
                Id = entity.Id,
                Provider = entity.Provider,
                Source = entity.Source,
                ToAddress = entity.ToAddress,
                IsEnabled = entity.IsEnabled,
                RequireKnownSender = entity.RequireKnownSender,
                AutoProvisionUnknownSenders = entity.AutoProvisionUnknownSenders,
                DefaultCategoryId = entity.DefaultCategoryId,
                DefaultTicketType = entity.DefaultTicketType,
                DefaultPriority = entity.DefaultPriority
            }});
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inbound messaging channel");
            return StatusCode(500, new { success = false, message = "Failed to create channel" });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpsertEhcInboundMessagingChannelRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });

            var entity = await _db.EhcInboundMessagingChannels.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
            if (entity == null) return NotFound(new { success = false, message = "Channel not found." });

            if (string.IsNullOrWhiteSpace(request.ToAddress))
            {
                return BadRequest(new { success = false, message = "ToAddress is required." });
            }

            if (request.Source is not (EhcTicketSource.Sms or EhcTicketSource.WhatsApp))
            {
                return BadRequest(new { success = false, message = "Source must be Sms or WhatsApp." });
            }

            if (!request.DefaultCategoryId.HasValue || request.DefaultCategoryId.Value == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "DefaultCategoryId is required." });
            }

            var to = NormalizeAddress(request.ToAddress);
            var exists = await _db.EhcInboundMessagingChannels
                .AsNoTracking()
                .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Id != id && c.Provider == request.Provider && c.Source == request.Source && c.ToAddress == to, cancellationToken);
            if (exists)
            {
                return BadRequest(new { success = false, message = "This destination address is already configured." });
            }

            entity.Provider = request.Provider;
            entity.Source = request.Source;
            entity.ToAddress = to;
            entity.IsEnabled = request.IsEnabled;
            entity.RequireKnownSender = request.RequireKnownSender;
            entity.AutoProvisionUnknownSenders = request.AutoProvisionUnknownSenders;
            entity.DefaultCategoryId = request.DefaultCategoryId;
            entity.DefaultTicketType = request.DefaultTicketType;
            entity.DefaultPriority = request.DefaultPriority;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _currentUserService.UserName ?? "System";

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inbound messaging channel {ChannelId}", id);
            return StatusCode(500, new { success = false, message = "Failed to update channel" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty) return BadRequest(new { success = false, message = "Tenant context is required." });

            var entity = await _db.EhcInboundMessagingChannels.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
            if (entity == null) return NotFound(new { success = false, message = "Channel not found." });

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedBy = _currentUserService.UserName ?? "System";
            entity.IsEnabled = false;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inbound messaging channel {ChannelId}", id);
            return StatusCode(500, new { success = false, message = "Failed to delete channel" });
        }
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

