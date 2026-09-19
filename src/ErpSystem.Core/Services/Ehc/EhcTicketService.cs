using System.Text.RegularExpressions;
using System.Text.Json;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Services.Ehc.Sla;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Ehc;

public sealed class EhcTicketService : IEhcTicketService
{
    private const string DefaultWorkflowName = "EHC Ticket";
    private const string ExternalPortalTicketPathPrefix = "/support/tickets";
    private const string EhcUploadCategory = "ehc-ticket";
    private const string PropertyListingLeadSource = "estate-public-listing";
    private const string LegacyPropertyListingLeadSource = "state-public-listing";
    private const string SalesAndMarketingOrganizationUnitCode = "UNIT-MKT";

    private readonly IEhcTicketRepository _ticketRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppEventBus _appEventBus;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IWorkflowStepRepository _workflowStepRepository;
    private readonly IWorkflowStepInstanceRepository _workflowStepInstanceRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<EhcTicketService> _logger;

    public EhcTicketService(
        IEhcTicketRepository ticketRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        IAppEventBus appEventBus,
        IWorkflowEngine workflowEngine,
        IWorkflowStepRepository workflowStepRepository,
        IWorkflowStepInstanceRepository workflowStepInstanceRepository,
        IFileStorageService fileStorageService,
        ILogger<EhcTicketService> logger)
    {
        _ticketRepository = ticketRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _appEventBus = appEventBus;
        _workflowEngine = workflowEngine;
        _workflowStepRepository = workflowStepRepository;
        _workflowStepInstanceRepository = workflowStepInstanceRepository;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    private async Task AddAuditEventAsync(
        EhcTicket ticket,
        string eventType,
        string? title,
        string? body,
        bool isInternal,
        Guid? actorUserId,
        object? data = null)
    {
        if (ticket == null) throw new ArgumentNullException(nameof(ticket));
        if (ticket.TenantId == Guid.Empty || ticket.Id == Guid.Empty) return;

        var ev = new EhcTicketAuditEvent
        {
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            EventType = (eventType ?? string.Empty).Trim(),
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
            IsInternal = isInternal,
            ActorUserId = actorUserId,
            DataJson = data == null ? null : JsonSerializer.Serialize(data),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = actorUserId
        };

        await _unitOfWork.Repository<EhcTicketAuditEvent>().AddAsync(ev);
    }

    /// <summary>
    /// Moves a public property enquiry into CRM only after Helpdesk has assigned it to the active
    /// Sales and Marketing organization unit and advanced it from New. Every EHC interaction is linked once, so a
    /// transient retry or later update can safely backfill the CRM timeline without duplicates.
    /// </summary>
    private async Task<bool> IsAssignedToActiveSalesOrganizationUnitAsync(EhcTicket ticket, CancellationToken cancellationToken)
    {
        return ticket.AssignedOrganizationUnitId.HasValue &&
               await _unitOfWork.Repository<OrganizationUnit>().GetQueryable(unit =>
                       unit.Id == ticket.AssignedOrganizationUnitId.Value &&
                       unit.TenantId == ticket.TenantId &&
                       !unit.IsDeleted &&
                       unit.IsActive &&
                       unit.Code == SalesAndMarketingOrganizationUnitCode)
                   .AnyAsync(cancellationToken);
    }

    private async Task SynchronizePropertyEnquiryCrmAsync(EhcTicket ticket, CancellationToken cancellationToken)
    {
        if (ticket.TicketType != EhcTicketType.Enquiry ||
            ticket.Status == EhcTicketStatus.New ||
            !ticket.AssignedOrganizationUnitId.HasValue ||
            string.IsNullOrWhiteSpace(ticket.PropertyListingContextJson))
        {
            return;
        }

        if (!await IsAssignedToActiveSalesOrganizationUnitAsync(ticket, cancellationToken))
        {
            return;
        }

        EhcPropertyListingContextDto? property;
        try
        {
            property = JsonSerializer.Deserialize<EhcPropertyListingContextDto>(ticket.PropertyListingContextJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Property listing snapshot for EHC ticket {TicketId} is invalid", ticket.Id);
            return;
        }

        if (property == null || !IsPropertyListingLeadSource(property.Source))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var changed = false;
        if (!ticket.CrmLeadId.HasValue)
        {
            var (firstName, lastName) = SplitCrmContactName(property.ContactName, property.BusinessPartnerName);
            var lead = new Lead
            {
                TenantId = ticket.TenantId,
                ReferenceNumber = ticket.TicketNumber,
                Status = "Active",
                EffectiveDate = now,
                FirstName = firstName,
                LastName = lastName,
                CompanyName = Clip(property.BusinessPartnerName, 100),
                Email = Clip(property.ContactEmail, 100),
                Phone = Clip(property.ContactPhone, 20),
                LeadSource = PropertyListingLeadSource,
                LeadStatus = "Qualified",
                QualificationScore = 40,
                EstimatedValue = property.Price ?? 0m,
                AssignedToId = ticket.AssignedToUserId,
                Notes = Clip(BuildPropertySnapshotSummary(property, ticket), 2000),
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "EHC property enquiry",
                CreatedById = ticket.CreatedById
            };
            await _unitOfWork.Repository<Lead>().AddAsync(lead);
            ticket.CrmLeadId = lead.Id;
            changed = true;
        }

        if (!ticket.CrmOpportunityId.HasValue)
        {
            var opportunity = new Opportunity
            {
                TenantId = ticket.TenantId,
                ReferenceNumber = ticket.TicketNumber,
                Status = "Active",
                EffectiveDate = now,
                Name = Clip($"Property enquiry: {property.ListingName}", 200)!,
                Description = Clip($"{BuildPropertySnapshotSummary(property, ticket)}\n\n{ticket.Description}", 2000),
                LeadId = ticket.CrmLeadId,
                Stage = "Qualification",
                Probability = 10,
                Amount = property.Price ?? 0m,
                Currency = NormalizeCurrency(property.Currency),
                ExpectedCloseDate = now.AddDays(90),
                LeadSource = PropertyListingLeadSource,
                OpportunityType = "New Business",
                AssignedToId = ticket.AssignedToUserId,
                Notes = $"Source ticket: {ticket.TicketNumber}",
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName ?? "EHC property enquiry",
                CreatedById = ticket.CreatedById
            };
            await _unitOfWork.Repository<Opportunity>().AddAsync(opportunity);
            ticket.CrmOpportunityId = opportunity.Id;
            changed = true;
        }

        if (changed)
        {
            ticket.UpdatedAt = now;
            await _unitOfWork.Repository<EhcTicket>().UpdateAsync(ticket);
        }

        var linkRepository = _unitOfWork.Repository<EhcCrmEngagementLink>();
        var sourceKeys = (await linkRepository
                .GetQueryable(l => l.TenantId == ticket.TenantId && l.TicketId == ticket.Id && !l.IsDeleted)
                .Select(l => l.SourceKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var sourcePrefix = $"EhcTicket:{ticket.Id}";
        var initial = SplitCrmActivityText($"Public property listing enquiry received.\n{BuildPropertySnapshotSummary(property, ticket)}\n\n{ticket.Description}");
        await AddCrmActivityIfMissingAsync(ticket, sourceKeys, $"{sourcePrefix}:Created", "InitialEnquiry",
            $"Property enquiry {ticket.TicketNumber} received", "Email", initial.Description, initial.Notes,
            ticket.CreatedAt, ticket.RequesterUserId, requiresFollowUp: true);

        var handoff = SplitCrmActivityText($"Helpdesk assigned this enquiry to the Sales department. Current ticket status: {ticket.Status}.\n{BuildPropertySnapshotSummary(property, ticket)}");
        await AddCrmActivityIfMissingAsync(ticket, sourceKeys, $"{sourcePrefix}:SalesHandoff", "SalesHandoff",
            $"Property enquiry {ticket.TicketNumber} handed to Sales", "Task", handoff.Description, handoff.Notes,
            ticket.UpdatedAt ?? now, ticket.AssignedToUserId, requiresFollowUp: true);

        var messages = await _unitOfWork.Repository<EhcTicketMessage>()
            .GetQueryable(m => m.TenantId == ticket.TenantId && m.TicketId == ticket.Id && !m.IsDeleted)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            var direction = message.IsInternal
                ? "Internal note"
                : message.AuthorUserId == ticket.RequesterUserId ? "Supplier message" : "Sales response";
            var details = SplitCrmActivityText($"{direction} on {ticket.TicketNumber}.\n\n{message.Body}");
            await AddCrmActivityIfMissingAsync(ticket, sourceKeys, $"{sourcePrefix}:Message:{message.Id}",
                message.IsInternal ? "InternalNote" : direction.Replace(" ", string.Empty),
                $"{direction}: {ticket.TicketNumber}", message.IsInternal ? "Note" : "Email", details.Description, details.Notes,
                message.CreatedAt, message.AuthorUserId, requiresFollowUp: false);
        }

        var statusHistory = await _unitOfWork.Repository<EhcTicketStatusHistory>()
            .GetQueryable(h => h.TenantId == ticket.TenantId && h.TicketId == ticket.Id && !h.IsDeleted)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(cancellationToken);
        foreach (var history in statusHistory.Where(h => h.FromStatus.HasValue || h.ToStatus != EhcTicketStatus.New))
        {
            var details = SplitCrmActivityText($"Ticket status changed from {history.FromStatus?.ToString() ?? "None"} to {history.ToStatus}.\n\n{history.Notes ?? "No transition notes were entered."}");
            await AddCrmActivityIfMissingAsync(ticket, sourceKeys, $"{sourcePrefix}:Status:{history.Id}", "StatusChange",
                $"Status changed to {history.ToStatus}: {ticket.TicketNumber}", "Note", details.Description, details.Notes,
                history.CreatedAt, history.ChangedByUserId, requiresFollowUp: false);
        }

        var attachments = await _unitOfWork.Repository<EhcTicketAttachment>()
            .GetQueryable(a => a.TenantId == ticket.TenantId && a.TicketId == ticket.Id && !a.IsDeleted)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
        foreach (var attachment in attachments)
        {
            var details = SplitCrmActivityText($"{(attachment.IsInternal ? "Internal" : "External")} attachment added to {ticket.TicketNumber}: {attachment.FileName}.");
            await AddCrmActivityIfMissingAsync(ticket, sourceKeys, $"{sourcePrefix}:Attachment:{attachment.Id}", "Attachment",
                $"Attachment added: {ticket.TicketNumber}", "Note", details.Description, details.Notes,
                attachment.CreatedAt, attachment.CreatedById, requiresFollowUp: false);
        }

        if (changed || sourceKeys.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task PublishPropertyEnquirySalesReadyAsync(EhcTicket ticket, Guid? triggeredByUserId,
        CancellationToken cancellationToken)
    {
        if (ticket.Status == EhcTicketStatus.New || !await IsAssignedToActiveSalesOrganizationUnitAsync(ticket, cancellationToken))
        {
            return;
        }

        await PublishTicketTopicAsync(
            ticket.TenantId,
            activity: "PropertyEnquirySalesReady",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: triggeredByUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                ["ActionUrl"] = $"/sales/property-enquiries?id={ticket.Id}"
            },
            cancellationToken);
    }

    private async Task AddCrmActivityIfMissingAsync(EhcTicket ticket, ISet<string> sourceKeys, string sourceKey,
        string engagementType, string subject, string activityType, string description, string? notes,
        DateTime activityDate, Guid? assignedToUserId, bool requiresFollowUp)
    {
        if (!sourceKeys.Add(sourceKey))
        {
            return;
        }

        var activity = new Activity
        {
            TenantId = ticket.TenantId,
            ReferenceNumber = ticket.TicketNumber,
            Status = "Active",
            Subject = Clip(subject, 200)!,
            ActivityType = activityType,
            Description = Clip(description, 2000),
            ActivityDate = activityDate,
            ActivityStatus = "Completed",
            Priority = 2,
            AssignedToId = assignedToUserId ?? ticket.AssignedToUserId,
            LeadId = ticket.CrmLeadId,
            OpportunityId = ticket.CrmOpportunityId,
            Notes = Clip(notes, 2000),
            RequiresFollowUp = requiresFollowUp,
            CreatedAt = activityDate,
            CreatedBy = _currentUserService.UserName ?? "EHC property enquiry",
            CreatedById = ticket.CreatedById
        };
        await _unitOfWork.Repository<Activity>().AddAsync(activity);
        await _unitOfWork.Repository<EhcCrmEngagementLink>().AddAsync(new EhcCrmEngagementLink
        {
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            CrmActivityId = activity.Id,
            SourceKey = sourceKey,
            EngagementType = engagementType,
            CreatedAt = activityDate,
            CreatedBy = _currentUserService.UserName ?? "EHC property enquiry",
            CreatedById = ticket.CreatedById
        });
    }

    private static (string Description, string? Notes) SplitCrmActivityText(string text)
    {
        var normalized = text.Trim();
        return normalized.Length <= 2000
            ? (normalized, null)
            : (normalized[..2000], Clip(normalized[2000..], 2000));
    }

    private static (string FirstName, string LastName) SplitCrmContactName(string? contactName, string? companyName)
    {
        var name = string.IsNullOrWhiteSpace(contactName) ? companyName : contactName;
        var parts = (name ?? "Property enquiry contact").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => ("Property", "Enquiry"),
            1 => (Clip(parts[0], 100)!, "Enquiry"),
            _ => (Clip(parts[0], 100)!, Clip(string.Join(' ', parts.Skip(1)), 100)!)
        };
    }

    private static bool IsPropertyListingLeadSource(string? source) =>
        string.Equals(source, PropertyListingLeadSource, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(source, LegacyPropertyListingLeadSource, StringComparison.OrdinalIgnoreCase);

    private static string BuildPropertySnapshotSummary(EhcPropertyListingContextDto property, EhcTicket ticket) =>
        $"Source: {property.Source}; Listing: {property.ListingReference} — {property.ListingName}; " +
        $"Type: {property.ListingType}; Currency: {property.Currency}; Listing ID: {property.ListingId}; EHC ticket: {ticket.TicketNumber}.";

    private static string NormalizeCurrency(string? currency) => string.IsNullOrWhiteSpace(currency)
        ? "USD"
        : Clip(currency.Trim().ToUpperInvariant(), 3)!;

    private static string? Clip(string? value, int maxLength) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim().Length <= maxLength ? value.Trim() : value.Trim()[..maxLength];

    private async Task PublishTicketTopicAsync(
        Guid tenantId,
        string activity,
        string audience,
        Guid ticketId,
        Guid? triggeredByUserId,
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) return;

        try
        {
            if (audience == "Internal" && (activity == "Created" || activity == "Message" || activity == "PropertyEnquirySalesReady"))
            {
                var propertyTicket = await _ticketRepository.Query()
                    .Where(t => t.Id == ticketId && t.TenantId == tenantId && !t.IsDeleted)
                    .Select(t => new { t.PropertyListingContextJson, t.Status, t.AssignedOrganizationUnitId })
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrEmpty(propertyTicket?.PropertyListingContextJson))
                {
                    var property = JsonSerializer.Deserialize<EhcPropertyListingContextDto>(propertyTicket.PropertyListingContextJson)!;
                    if (property != null && IsPropertyListingLeadSource(property.Source))
                    {
                        data["listingName"] = property.ListingName;
                        data["listingReference"] = property.ListingReference;
                        data["businessPartnerName"] = property.BusinessPartnerName;

                        if (activity is "Created" or "Message")
                        {
                            var salesReady = propertyTicket.Status != EhcTicketStatus.New &&
                                propertyTicket.AssignedOrganizationUnitId.HasValue &&
                                await _unitOfWork.Repository<OrganizationUnit>().GetQueryable(unit =>
                                        unit.Id == propertyTicket.AssignedOrganizationUnitId.Value &&
                                        unit.TenantId == tenantId &&
                                        !unit.IsDeleted &&
                                        unit.IsActive &&
                                        unit.Code == SalesAndMarketingOrganizationUnitCode)
                                    .AnyAsync(cancellationToken);
                            if (salesReady && activity == "Message")
                            {
                                activity = "PropertyEnquiryMessage";
                                data["ActionUrl"] = $"/sales/property-enquiries?id={ticketId}";
                            }
                            else
                            {
                                activity = "PropertyEnquiryTriage" + activity;
                                data["ActionUrl"] = $"/helpdesk/tickets/{ticketId}";
                            }
                        }
                    }
                }
            }
            if (string.Equals(audience, "Internal", StringComparison.OrdinalIgnoreCase))
            {
                await EnrichDataWithWatchersAsync(tenantId, ticketId, data, cancellationToken);
            }

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = "EhcTicket",
                EntityId = ticketId,
                Activity = activity,
                Audience = audience,
                TriggeredByUserId = triggeredByUserId,
                Data = data ?? new Dictionary<string, object>()
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish EHC ticket topic event {Activity}.{Audience} for ticket {TicketId}", activity, audience, ticketId);
        }
    }

    private async Task EnrichDataWithWatchersAsync(Guid tenantId, Guid ticketId, Dictionary<string, object> data, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || ticketId == Guid.Empty) return;
        if (data == null) return;
        if (data.ContainsKey("watcherUserIds")) return;

        try
        {
            var watcherIds = await _unitOfWork.Repository<EhcTicketWatcher>()
                .GetQueryable(w => w.TenantId == tenantId && !w.IsDeleted && w.TicketId == ticketId)
                .Select(w => w.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (watcherIds.Count > 0)
            {
                data["watcherUserIds"] = watcherIds;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to enrich EHC event data with watchers for ticket {TicketId}", ticketId);
        }
    }

    private static readonly Regex MentionBracedRegex = new(@"@\{(?<token>[^}]{1,200})\}", RegexOptions.Compiled);

    // Lightweight email mention support: "please check @someone@example.com"
    private static readonly Regex MentionEmailRegex = new(
        @"(?<![A-Za-z0-9_])@(?<email>[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string BuildMessagePreview(string body, int maxLen = 140)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        var s = body.Trim();
        s = Regex.Replace(s, "\\s+", " ");
        if (s.Length <= maxLen) return s;
        return s[..maxLen].TrimEnd() + "…";
    }

    private static readonly Regex DoubleBraceTokenRegex = new(@"\{\{\s*(?<name>[A-Za-z][A-Za-z0-9_]*)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex SingleBraceTokenRegex = new(@"\{\s*(?<name>[A-Za-z][A-Za-z0-9_]*)\s*\}", RegexOptions.Compiled);

    private static string ExpandReplyTokens(string text, Dictionary<string, string> tokens)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (tokens.Count == 0) return text;

        string Replace(Match m)
        {
            var name = (m.Groups["name"]?.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name)) return m.Value;
            return tokens.TryGetValue(name, out var v) ? v : m.Value;
        }

        // First expand {{Token}} style (preferred), then {Token}.
        var outText = DoubleBraceTokenRegex.Replace(text, Replace);
        outText = SingleBraceTokenRegex.Replace(outText, Replace);
        return outText;
    }

    private async Task<IReadOnlyList<Guid>> ResolveMentionedUserIdsAsync(Guid tenantId, string body, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) return Array.Empty<Guid>();
        if (string.IsNullOrWhiteSpace(body)) return Array.Empty<Guid>();

        var guidCandidates = new HashSet<Guid>();
        var normalizedEmailCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in MentionBracedRegex.Matches(body))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var token = (m.Groups["token"]?.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token)) continue;

            if (Guid.TryParse(token, out var gid) && gid != Guid.Empty)
            {
                guidCandidates.Add(gid);
                continue;
            }

            if (token.Contains('@'))
            {
                normalizedEmailCandidates.Add(token.Trim().ToUpperInvariant());
            }
        }

        foreach (Match m in MentionEmailRegex.Matches(body))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var email = (m.Groups["email"]?.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email)) continue;
            normalizedEmailCandidates.Add(email.ToUpperInvariant());
        }

        if (guidCandidates.Count == 0 && normalizedEmailCandidates.Count == 0) return Array.Empty<Guid>();

        var mentioned = new HashSet<Guid>();

        if (guidCandidates.Count > 0)
        {
            var ids = await _userManager.Users
                .AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.IsActive && guidCandidates.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            foreach (var id in ids) mentioned.Add(id);
        }

        if (normalizedEmailCandidates.Count > 0)
        {
            var ids = await _userManager.Users
                .AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.IsActive && u.NormalizedEmail != null && normalizedEmailCandidates.Contains(u.NormalizedEmail))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            foreach (var id in ids) mentioned.Add(id);
        }

        return mentioned.Count == 0 ? Array.Empty<Guid>() : mentioned.ToList();
    }

    private async Task<string> ResolveRequesterActionUrlAsync(EhcTicket ticket, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requester = await _userManager.FindByIdAsync(ticket.RequesterUserId.ToString());
        if (requester?.AuthenticationProvider == AuthenticationProvider.Local)
        {
            return $"{ExternalPortalTicketPathPrefix}/{ticket.Id}";
        }

        return $"/helpdesk/tickets/{ticket.Id}";
    }

    private static int GetRuleSpecificity(EhcWorkflowRoutingRule r)
    {
        var s = 0;
        if (r.TicketType.HasValue) s++;
        if (r.TicketPriority.HasValue) s++;
        if (r.CategoryId.HasValue && r.CategoryId.Value != Guid.Empty) s++;
        if (r.SubcategoryId.HasValue && r.SubcategoryId.Value != Guid.Empty) s++;
        if (r.AssignedDepartmentId.HasValue && r.AssignedDepartmentId.Value != Guid.Empty) s++;
        return s;
    }

    private async Task<string> ResolveWorkflowNameForTicketAsync(EhcTicket ticket, CancellationToken cancellationToken)
    {
        var tenantId = ticket.TenantId;
        if (tenantId == Guid.Empty)
        {
            return DefaultWorkflowName;
        }

        var repo = _unitOfWork.Repository<EhcWorkflowRoutingRule>();
        var rules = await repo
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && r.IsActive)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return DefaultWorkflowName;
        }

        bool Matches(EhcWorkflowRoutingRule r)
        {
            if (r.TicketType.HasValue && r.TicketType.Value != ticket.TicketType) return false;
            if (r.TicketPriority.HasValue && r.TicketPriority.Value != ticket.Priority) return false;
            if (r.AssignedDepartmentId.HasValue && r.AssignedDepartmentId.Value != Guid.Empty)
            {
                if (!ticket.AssignedDepartmentId.HasValue || ticket.AssignedDepartmentId.Value != r.AssignedDepartmentId.Value) return false;
            }
            if (r.CategoryId.HasValue && r.CategoryId.Value != Guid.Empty)
            {
                if (!ticket.CategoryId.HasValue || ticket.CategoryId.Value != r.CategoryId.Value) return false;
            }
            if (r.SubcategoryId.HasValue && r.SubcategoryId.Value != Guid.Empty)
            {
                if (!ticket.SubcategoryId.HasValue || ticket.SubcategoryId.Value != r.SubcategoryId.Value) return false;
            }
            return true;
        }

        var best = rules
            .Where(Matches)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(GetRuleSpecificity)
            .ThenBy(r => r.CreatedAt)
            .FirstOrDefault();

        var name = (best?.WorkflowName ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(name) ? DefaultWorkflowName : name;
    }

    public async Task<EhcTicketDetailDto> CreateExternalTicketAsync(CreateEhcTicketRequestDto request, CancellationToken cancellationToken = default)
        => await CreateExternalTicketCoreAsync(request, null, null, cancellationToken);

    public async Task<EhcTicketDetailDto> CreateExternalPropertyEnquiryAsync(
        CreateEhcTicketRequestDto request, EhcPropertyListingContextDto property, Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        if (!IsPropertyListingLeadSource(property.Source) || property.ListingId == Guid.Empty || submissionId == Guid.Empty)
            throw new ArgumentException("A property listing and submission identifier are required.");
        if (!Guid.TryParse(_currentUserService.UserId, out var requesterId) || !_currentUserService.TenantId.HasValue)
            throw new InvalidOperationException("An authenticated tenant account is required.");
        EhcTicketDetailDto? result = null;
        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"property-enquiry:{_currentUserService.TenantId}:{requesterId}:{submissionId}", cancellationToken);
                var existing = await _ticketRepository.Query().AsNoTracking().FirstOrDefaultAsync(t =>
                    t.TenantId == _currentUserService.TenantId && t.RequesterUserId == requesterId
                    && t.ExternalSubmissionId == submissionId && !t.IsDeleted, cancellationToken);
                if (existing != null)
                {
                    var saved = JsonSerializer.Deserialize<EhcPropertyListingContextDto>(existing.PropertyListingContextJson!);
                    if (saved?.ListingId != property.ListingId || saved.BusinessPartnerId != property.BusinessPartnerId || existing.Description != request.Description.Trim())
                        throw new ArgumentException("This submission identifier has already been used for a different enquiry.");
                    result = await GetMyTicketByIdAsync(existing.Id, cancellationToken);
                }
                else
                {
                    request.TicketType = EhcTicketType.Enquiry;
                    request.Source = EhcTicketSource.Web;
                    result = await CreateExternalTicketCoreAsync(request, property, submissionId, cancellationToken);
                }
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                _unitOfWork.ClearTrackedChanges();
                throw;
            }
        }, cancellationToken);
        return result ?? throw new InvalidOperationException("The enquiry could not be loaded.");
    }

    private async Task<EhcTicketDetailDto> CreateExternalTicketCoreAsync(CreateEhcTicketRequestDto request,
        EhcPropertyListingContextDto? property, Guid? submissionId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.", nameof(request.Description));
        }

        if (!request.CategoryId.HasValue || request.CategoryId.Value == Guid.Empty)
        {
            throw new ArgumentException("Category is required.", nameof(request.CategoryId));
        }

        var categoryRepo = _unitOfWork.Repository<EhcTicketCategory>();
        var category = await categoryRepo.GetByIdAsync(request.CategoryId.Value);
        if (category == null || category.TenantId != tenantId || category.IsDeleted)
        {
            throw new ArgumentException("Invalid category.", nameof(request.CategoryId));
        }

        if (request.SubcategoryId.HasValue && request.SubcategoryId.Value != Guid.Empty)
        {
            var sub = await categoryRepo.GetByIdAsync(request.SubcategoryId.Value);
            if (sub == null || sub.TenantId != tenantId || sub.IsDeleted)
            {
                throw new ArgumentException("Invalid subcategory.", nameof(request.SubcategoryId));
            }

            // Allow selecting a subcategory at any depth, as long as it's within the selected category subtree.
            var current = sub;
            var isWithinCategory = false;
            while (current.ParentCategoryId.HasValue && current.ParentCategoryId.Value != Guid.Empty)
            {
                if (current.ParentCategoryId.Value == category.Id)
                {
                    isWithinCategory = true;
                    break;
                }

                var parent = await categoryRepo.GetByIdAsync(current.ParentCategoryId.Value);
                if (parent == null || parent.TenantId != tenantId || parent.IsDeleted)
                {
                    break;
                }

                current = parent;
            }

            if (!isWithinCategory)
            {
                throw new ArgumentException("Invalid subcategory.", nameof(request.SubcategoryId));
            }
        }

        var ticketNumber = await _ticketRepository.GenerateTicketNumberAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;

        var ticket = new EhcTicket
        {
            PropertyListingContextJson = property == null ? null : JsonSerializer.Serialize(property),
            ExternalSubmissionId = submissionId,
            TenantId = tenantId,
            TicketNumber = ticketNumber,
            TicketType = request.TicketType,
            CategoryId = request.CategoryId,
            SubcategoryId = request.SubcategoryId,
            Priority = request.Priority,
            Source = request.Source,
            Subject = request.Subject?.Trim(),
            Description = request.Description.Trim(),
            RequesterUserId = requesterUserId,
            Status = EhcTicketStatus.New,
            RelatedEntityType = request.RelatedEntityType?.Trim(),
            RelatedEntityReference = request.RelatedEntityReference?.Trim(),
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId,
            CreatedAt = now
        };

        var sla = await ResolveSlaTemplateAsync(tenantId, request, cancellationToken);
        if (sla != null)
        {
            ticket.FirstResponseDueAt = EhcSlaTimeCalculator.CalculateDueAtUtc(now, Math.Max(1, sla.FirstResponseMinutes), sla.CalendarConfigurationJson);
            ticket.ResolutionDueAt = EhcSlaTimeCalculator.CalculateDueAtUtc(now, Math.Max(1, sla.ResolutionMinutes), sla.CalendarConfigurationJson);
            ticket.AppliedSlaTemplateId = sla.Id;
            ticket.AppliedFirstResponseMinutes = sla.FirstResponseMinutes;
            ticket.AppliedResolutionMinutes = sla.ResolutionMinutes;
            ticket.AppliedSlaCalendarConfigurationJson = sla.CalendarConfigurationJson;
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        await ticketRepo.AddAsync(ticket);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Start workflow instance (drives status transitions)
        var workflowName = await ResolveWorkflowNameForTicketAsync(ticket, cancellationToken);
        var workflowInstance = await _workflowEngine.StartWorkflowAsync(
            workflowName,
            ticket.Id,
            requesterUserId,
            dataContext: new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                ticketType = ticket.TicketType.ToString(),
                priority = ticket.Priority.ToString()
            });

        ticket.WorkflowInstanceId = workflowInstance.Id;
        await ticketRepo.UpdateAsync(ticket);

        var historyRepo = _unitOfWork.Repository<EhcTicketStatusHistory>();
        await historyRepo.AddAsync(new EhcTicketStatusHistory
        {
            TenantId = tenantId,
            TicketId = ticket.Id,
            FromStatus = null,
            ToStatus = EhcTicketStatus.New,
            ChangedByUserId = requesterUserId,
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId,
            CreatedAt = now,
            Notes = "Ticket created"
        });

        await AddAuditEventAsync(
            ticket,
            eventType: "TicketCreated",
            title: "Ticket created",
            body: $"Ticket {ticket.TicketNumber} created.",
            isInternal: false,
            actorUserId: requesterUserId,
            data: new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                ticketType = ticket.TicketType.ToString(),
                priority = ticket.Priority.ToString(),
                source = ticket.Source.ToString(),
                status = ticket.Status.ToString()
            });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Created",
            audience: "Requester",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["ticketType"] = ticket.TicketType.ToString(),
                ["priority"] = ticket.Priority.ToString(),
                ["source"] = ticket.Source.ToString(),
                ["status"] = ticket.Status.ToString(),
                ["requesterUserId"] = requesterUserId,
                ["ActionUrl"] = $"{ExternalPortalTicketPathPrefix}/{ticket.Id}"
            },
            cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Created",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["ticketType"] = ticket.TicketType.ToString(),
                ["priority"] = ticket.Priority.ToString(),
                ["source"] = ticket.Source.ToString(),
                ["status"] = ticket.Status.ToString(),
                ["requesterUserId"] = requesterUserId,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        var dto = await GetMyTicketByIdAsync(ticket.Id, cancellationToken);
        if (dto == null)
        {
            throw new InvalidOperationException("Failed to load created ticket.");
        }

        return dto;
    }

    public async Task<EhcTicketDetailDto> CreateInternalTicketAsync(CreateEhcTicketRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.", nameof(request.Description));
        }

        if (!request.CategoryId.HasValue || request.CategoryId.Value == Guid.Empty)
        {
            throw new ArgumentException("Category is required.", nameof(request.CategoryId));
        }

        Guid? assignedDepartmentId = request.AssignedDepartmentId;
        if (!assignedDepartmentId.HasValue || assignedDepartmentId.Value == Guid.Empty)
        {
            var employeeId = _currentUserService.EmployeeId;
            if (employeeId.HasValue && employeeId.Value != Guid.Empty)
            {
                var empRepo = _unitOfWork.Repository<Employee>();
                var emp = await empRepo.GetByIdAsync(employeeId.Value);
                if (emp != null && emp.TenantId == tenantId && !emp.IsDeleted && emp.IsActive)
                {
                    assignedDepartmentId = emp.DepartmentId;
                }
            }
        }

        if (assignedDepartmentId.HasValue && assignedDepartmentId.Value != Guid.Empty)
        {
            var deptRepo = _unitOfWork.Repository<Department>();
            var dept = await deptRepo.GetByIdAsync(assignedDepartmentId.Value);
            if (dept == null || dept.TenantId != tenantId || dept.IsDeleted || !dept.IsActive)
            {
                throw new ArgumentException("Invalid department.", nameof(request.AssignedDepartmentId));
            }
        }

        var categoryRepo = _unitOfWork.Repository<EhcTicketCategory>();
        var category = await categoryRepo.GetByIdAsync(request.CategoryId.Value);
        if (category == null || category.TenantId != tenantId || category.IsDeleted)
        {
            throw new ArgumentException("Invalid category.", nameof(request.CategoryId));
        }

        if (request.SubcategoryId.HasValue && request.SubcategoryId.Value != Guid.Empty)
        {
            var sub = await categoryRepo.GetByIdAsync(request.SubcategoryId.Value);
            if (sub == null || sub.TenantId != tenantId || sub.IsDeleted)
            {
                throw new ArgumentException("Invalid subcategory.", nameof(request.SubcategoryId));
            }

            // Allow selecting a subcategory at any depth, as long as it's within the selected category subtree.
            var current = sub;
            var isWithinCategory = false;
            while (current.ParentCategoryId.HasValue && current.ParentCategoryId.Value != Guid.Empty)
            {
                if (current.ParentCategoryId.Value == category.Id)
                {
                    isWithinCategory = true;
                    break;
                }

                var parent = await categoryRepo.GetByIdAsync(current.ParentCategoryId.Value);
                if (parent == null || parent.TenantId != tenantId || parent.IsDeleted)
                {
                    break;
                }

                current = parent;
            }

            if (!isWithinCategory)
            {
                throw new ArgumentException("Invalid subcategory.", nameof(request.SubcategoryId));
            }
        }

        var ticketNumber = await _ticketRepository.GenerateTicketNumberAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;

        var ticket = new EhcTicket
        {
            TenantId = tenantId,
            TicketNumber = ticketNumber,
            TicketType = request.TicketType,
            CategoryId = request.CategoryId,
            SubcategoryId = request.SubcategoryId,
            Priority = request.Priority,
            Source = request.Source,
            Subject = request.Subject?.Trim(),
            Description = request.Description.Trim(),
            RequesterUserId = requesterUserId,
            AssignedDepartmentId = assignedDepartmentId.HasValue && assignedDepartmentId.Value != Guid.Empty ? assignedDepartmentId : null,
            Status = EhcTicketStatus.New,
            RelatedEntityType = request.RelatedEntityType?.Trim(),
            RelatedEntityReference = request.RelatedEntityReference?.Trim(),
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId,
            CreatedAt = now
        };

        var sla = await ResolveSlaTemplateAsync(tenantId, request, cancellationToken);
        if (sla != null)
        {
            ticket.FirstResponseDueAt = EhcSlaTimeCalculator.CalculateDueAtUtc(now, Math.Max(1, sla.FirstResponseMinutes), sla.CalendarConfigurationJson);
            ticket.ResolutionDueAt = EhcSlaTimeCalculator.CalculateDueAtUtc(now, Math.Max(1, sla.ResolutionMinutes), sla.CalendarConfigurationJson);
            ticket.AppliedSlaTemplateId = sla.Id;
            ticket.AppliedFirstResponseMinutes = sla.FirstResponseMinutes;
            ticket.AppliedResolutionMinutes = sla.ResolutionMinutes;
            ticket.AppliedSlaCalendarConfigurationJson = sla.CalendarConfigurationJson;
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        await ticketRepo.AddAsync(ticket);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Start workflow instance (drives status transitions)
        var workflowName = await ResolveWorkflowNameForTicketAsync(ticket, cancellationToken);
        var workflowInstance = await _workflowEngine.StartWorkflowAsync(
            workflowName,
            ticket.Id,
            requesterUserId,
            dataContext: new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                ticketType = ticket.TicketType.ToString(),
                priority = ticket.Priority.ToString()
            });

        ticket.WorkflowInstanceId = workflowInstance.Id;
        await ticketRepo.UpdateAsync(ticket);

        var historyRepo = _unitOfWork.Repository<EhcTicketStatusHistory>();
        await historyRepo.AddAsync(new EhcTicketStatusHistory
        {
            TenantId = tenantId,
            TicketId = ticket.Id,
            FromStatus = null,
            ToStatus = EhcTicketStatus.New,
            ChangedByUserId = requesterUserId,
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId,
            CreatedAt = now,
            Notes = "Ticket created"
        });

        await AddAuditEventAsync(
            ticket,
            eventType: "TicketCreated",
            title: "Ticket created",
            body: $"Ticket {ticket.TicketNumber} created (internal).",
            isInternal: true,
            actorUserId: requesterUserId,
            data: new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                ticketType = ticket.TicketType.ToString(),
                priority = ticket.Priority.ToString(),
                source = ticket.Source.ToString(),
                status = ticket.Status.ToString()
            });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Created",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["ticketType"] = ticket.TicketType.ToString(),
                ["priority"] = ticket.Priority.ToString(),
                ["source"] = ticket.Source.ToString(),
                ["status"] = ticket.Status.ToString(),
                ["requesterUserId"] = requesterUserId,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        var dto = await GetTicketByIdAsync(ticket.Id, cancellationToken);
        if (dto == null)
        {
            throw new InvalidOperationException("Failed to load created ticket.");
        }

        return dto;
    }

    public async Task<IReadOnlyList<EhcTicketListItemDto>> GetMyTicketsAsync(
        int page = 1,
        int pageSize = 25,
        string? q = null,
        EhcTicketStatus? status = null,
        EhcTicketType? ticketType = null,
        EhcTicketPriority? priority = null,
        EhcTicketSource? source = null,
        Guid? categoryId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Array.Empty<EhcTicketListItemDto>();
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            return Array.Empty<EhcTicketListItemDto>();
        }

        var tickets = await _ticketRepository.GetForRequesterAsync(
            tenantId,
            requesterUserId,
            page,
            pageSize,
            q: q,
            status: status,
            ticketType: ticketType,
            priority: priority,
            source: source,
            categoryId: categoryId,
            createdFrom: createdFrom,
            createdTo: createdTo,
            cancellationToken: cancellationToken);

        var feedbackByTicketId = await GetLatestFeedbackByTicketIdsAsync(tenantId, tickets.Select(t => t.Id).ToList(), cancellationToken);
        return tickets.Select(t =>
        {
            feedbackByTicketId.TryGetValue(t.Id, out var f);
            return MapToExternalListItemDto(t, f?.Rating, f?.SubmittedAtUtc);
        }).ToList();
    }

    public async Task<EhcTicketDetailDto?> GetMyTicketByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            return null;
        }

        var ticket = await _ticketRepository.GetByIdForRequesterAsync(id, tenantId, requesterUserId, cancellationToken);
        return ticket == null ? null : await MapToDetailDtoAsync(ticket, includeInternal: false, cancellationToken);
    }

    public async Task<IReadOnlyList<EhcExternalTicketLinkDto>> GetMyTicketLinksAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty || ticketId == Guid.Empty)
            return Array.Empty<EhcExternalTicketLinkDto>();

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
            return Array.Empty<EhcExternalTicketLinkDto>();

        var ownsTicket = await _ticketRepository.Query()
            .AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Id == ticketId && t.RequesterUserId == requesterUserId, cancellationToken);
        if (!ownsTicket)
            throw new KeyNotFoundException("Ticket not found.");

        var linkRepo = _unitOfWork.Repository<EhcTicketLink>();
        var links = (await linkRepo.FindAsync(l =>
                l.TenantId == tenantId &&
                (l.TicketId == ticketId || l.RelatedTicketId == ticketId)))
            .OrderByDescending(l => l.CreatedAt)
            .ToList();

        if (links.Count == 0)
            return Array.Empty<EhcExternalTicketLinkDto>();

        var otherIds = links
            .Select(l => l.TicketId == ticketId ? l.RelatedTicketId : l.TicketId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (otherIds.Count == 0)
            return Array.Empty<EhcExternalTicketLinkDto>();

        // Only return linked tickets owned by the same requester (external portal isolation).
        var otherTickets = await _ticketRepository.Query()
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && otherIds.Contains(t.Id) && t.RequesterUserId == requesterUserId)
            .Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.Subject,
                t.Status,
                t.Priority,
                t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (otherTickets.Count == 0)
            return Array.Empty<EhcExternalTicketLinkDto>();

        var byId = otherTickets.ToDictionary(x => x.Id, x => x);

        static string RelationshipLabel(EhcTicketLink link, Guid currentTicketId)
        {
            var forward = link.TicketId == currentTicketId;
            return link.LinkType switch
            {
                EhcTicketLinkType.Related => "Related to",
                EhcTicketLinkType.ParentOf => forward ? "Parent of" : "Child of",
                EhcTicketLinkType.DuplicateOf => forward ? "Duplicate of" : "Duplicate ticket",
                _ => "Related to"
            };
        }

        var outList = new List<EhcExternalTicketLinkDto>(links.Count);
        foreach (var link in links)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var otherId = link.TicketId == ticketId ? link.RelatedTicketId : link.TicketId;
            if (otherId == Guid.Empty) continue;
            if (!byId.TryGetValue(otherId, out var other)) continue;

            outList.Add(new EhcExternalTicketLinkDto
            {
                Id = link.Id,
                LinkType = link.LinkType,
                RelationshipLabel = RelationshipLabel(link, ticketId),
                LinkedTicketId = other.Id,
                LinkedTicketNumber = other.TicketNumber,
                LinkedSubject = other.Subject,
                LinkedStatus = other.Status,
                LinkedPriority = other.Priority,
                LinkedCreatedAt = other.CreatedAt,
                CreatedAt = link.CreatedAt
            });
        }

        return outList;
    }

    public async Task<EhcTicketDetailDto> SubmitMyTicketFeedbackAsync(Guid ticketId, SubmitEhcTicketFeedbackRequestDto request, CancellationToken cancellationToken = default)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("Ticket id is required.", nameof(ticketId));

        request ??= new SubmitEhcTicketFeedbackRequestDto();
        if (request.Rating < 1 || request.Rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5.");

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required.");

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
            throw new InvalidOperationException("Authenticated user context is required.");

        var ticket = await _ticketRepository.GetByIdForRequesterAsync(ticketId, tenantId, requesterUserId, cancellationToken);
        if (ticket == null)
            throw new KeyNotFoundException("Ticket not found.");

        if (ticket.Status is not (EhcTicketStatus.Resolved or EhcTicketStatus.Closed))
            throw new InvalidOperationException("Feedback can only be submitted after the ticket is resolved or closed.");

        var feedbackRepo = _unitOfWork.Repository<EhcTicketFeedback>();
        var existing = await feedbackRepo
            .GetQueryable(f => f.TenantId == tenantId && !f.IsDeleted && f.TicketId == ticket.Id && f.SubmittedByUserId == requesterUserId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null)
            throw new InvalidOperationException("Feedback was already submitted for this ticket.");

        var now = DateTime.UtcNow;
        var feedback = new EhcTicketFeedback
        {
            TenantId = tenantId,
            TicketId = ticket.Id,
            SubmittedByUserId = requesterUserId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId
        };

        await feedbackRepo.AddAsync(feedback);

        await AddAuditEventAsync(
            ticket,
            eventType: "FeedbackSubmitted",
            title: "Feedback submitted",
            body: $"Rating: {feedback.Rating}/5",
            isInternal: false,
            actorUserId: requesterUserId,
            data: new { rating = feedback.Rating });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "FeedbackSubmitted",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["rating"] = feedback.Rating,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        var refreshed = await _ticketRepository.GetByIdForRequesterAsync(ticketId, tenantId, requesterUserId, cancellationToken);
        if (refreshed == null)
            throw new KeyNotFoundException("Ticket not found.");

        return await MapToDetailDtoAsync(refreshed, includeInternal: false, cancellationToken);
    }

    public async Task<EhcTicketMessageDto> AddExternalMessageAsync(Guid ticketId, AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Message body is required.", nameof(request.Body));
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        // Ensure external user can only post to their own ticket
        var ticket = await _ticketRepository.GetByIdForRequesterAsync(ticketId, tenantId, requesterUserId, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var now = DateTime.UtcNow;
        var message = new EhcTicketMessage
        {
            TenantId = tenantId,
            TicketId = ticketId,
            Body = request.Body.Trim(),
            IsInternal = false,
            AuthorUserId = requesterUserId,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId
        };

        await _unitOfWork.Repository<EhcTicketMessage>().AddAsync(message);

        // If agent requested info, user replying should move back to InProgress (phase 1 rule)
        var didAutoResume = false;
        if (ticket.Status == EhcTicketStatus.PendingUser)
        {
            ticket.Status = EhcTicketStatus.InProgress;
            ticket.UpdatedAt = now;
            await _unitOfWork.Repository<EhcTicket>().UpdateAsync(ticket);
            await AddStatusHistoryAsync(ticket, from: EhcTicketStatus.PendingUser, to: EhcTicketStatus.InProgress, "User replied", requesterUserId);
            didAutoResume = true;
        }

        await AddAuditEventAsync(
            ticket,
            eventType: "RequesterMessage",
            title: "Requester replied",
            body: "Requester sent a message.",
            isInternal: false,
            actorUserId: requesterUserId,
            data: new { messageId = message.Id });

        if (didAutoResume)
        {
            await AddAuditEventAsync(
                ticket,
                eventType: "StatusChanged",
                title: "Status updated",
                body: "Status: PendingUser → InProgress",
                isInternal: false,
                actorUserId: requesterUserId,
                data: new { fromStatus = EhcTicketStatus.PendingUser.ToString(), toStatus = EhcTicketStatus.InProgress.ToString() });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SynchronizePropertyEnquiryCrmAsync(ticket, cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Message",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                ["requesterUserId"] = requesterUserId,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        if (didAutoResume)
        {
            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: requesterUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = EhcTicketStatus.PendingUser.ToString(),
                    ["toStatus"] = EhcTicketStatus.InProgress.ToString(),
                    ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
        }

        return new EhcTicketMessageDto
        {
            Id = message.Id,
            Body = message.Body,
            IsInternal = false,
            AuthorUserId = requesterUserId,
            AuthorName = _currentUserService.UserName,
            CreatedAt = message.CreatedAt
        };
    }

    public async Task<EhcTicketAttachmentDto> AddExternalAttachmentAsync(Guid ticketId, AddEhcTicketAttachmentRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath) || string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException("FilePath and FileName are required.");
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var requesterUserId) || requesterUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        // Ensure external user can only attach to their own ticket
        var ticket = await _ticketRepository.GetByIdForRequesterAsync(ticketId, tenantId, requesterUserId, cancellationToken);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var uploadRecord = await _unitOfWork.Repository<FileUploadRecord>()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && !r.IsDeleted && r.FilePath == request.FilePath.Trim());

        if (uploadRecord == null)
        {
            throw new ArgumentException("Invalid file reference. Please upload the file again.", nameof(request.FilePath));
        }

        if (!string.Equals(uploadRecord.Category, EhcUploadCategory, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid upload category for ticket attachments.", nameof(request.FilePath));
        }

        if (uploadRecord.UploadedByUserId != requesterUserId)
        {
            throw new UnauthorizedAccessException("You can only attach files you uploaded.");
        }

        var now = DateTime.UtcNow;
        var attachment = new EhcTicketAttachment
        {
            TenantId = tenantId,
            TicketId = ticketId,
            MessageId = request.MessageId,
            FilePath = uploadRecord.FilePath,
            FileName = string.IsNullOrWhiteSpace(uploadRecord.OriginalFileName) ? request.FileName.Trim() : uploadRecord.OriginalFileName,
            ContentType = uploadRecord.ContentType ?? request.ContentType,
            FileSize = uploadRecord.FileSize > 0 ? uploadRecord.FileSize : request.FileSize,
            IsInternal = false,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = requesterUserId
        };

        await _unitOfWork.Repository<EhcTicketAttachment>().AddAsync(attachment);

        await AddAuditEventAsync(
            ticket,
            eventType: "RequesterAttachment",
            title: "Attachment uploaded",
            body: $"Requester uploaded {attachment.FileName}.",
            isInternal: false,
            actorUserId: requesterUserId,
            data: new { attachmentId = attachment.Id, fileName = attachment.FileName, fileSize = attachment.FileSize });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SynchronizePropertyEnquiryCrmAsync(ticket, cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Attachment",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: requesterUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                ["requesterUserId"] = requesterUserId,
                ["fileName"] = attachment.FileName,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        return await MapToAttachmentDtoAsync(attachment);
    }

    public async Task<EhcTicketMessageDto> AddAgentMessageAsync(Guid ticketId, AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Message body is required.", nameof(request.Body));
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var authorUserId) || authorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var now = DateTime.UtcNow;
        var requesterActionUrl = await ResolveRequesterActionUrlAsync(ticket, cancellationToken);

        var requester = await _userManager.FindByIdAsync(ticket.RequesterUserId.ToString());
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TicketNumber"] = ticket.TicketNumber,
            ["ticketNumber"] = ticket.TicketNumber,
            ["Status"] = ticket.Status.ToString(),
            ["status"] = ticket.Status.ToString(),
            ["RequesterName"] = requester == null ? string.Empty : $"{requester.FirstName} {requester.LastName}".Trim(),
            ["requesterName"] = requester == null ? string.Empty : $"{requester.FirstName} {requester.LastName}".Trim(),
            ["PortalUrl"] = requesterActionUrl,
            ["portalUrl"] = requesterActionUrl,
        };

        var body = ExpandReplyTokens(request.Body.Trim(), tokens);

        // Optional signature appended for requester-facing replies.
        var profile = await _unitOfWork.Repository<EhcAgentReplyProfile>()
            .GetQueryable(p => p.TenantId == tenantId && !p.IsDeleted && p.UserId == authorUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile?.IsSignatureEnabled == true && profile.AppendSignatureToReplies && !string.IsNullOrWhiteSpace(profile.Signature))
        {
            var sig = ExpandReplyTokens(profile.Signature.Trim(), tokens);
            body = $"{body}\n\n--\n{sig}";
        }

        var message = new EhcTicketMessage
        {
            TenantId = tenantId,
            TicketId = ticketId,
            Body = body,
            IsInternal = false,
            AuthorUserId = authorUserId,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = authorUserId
        };

        await _unitOfWork.Repository<EhcTicketMessage>().AddAsync(message);

        // Mark first response timestamp when an internal agent first interacts
        if (ticket.FirstRespondedAt == null && ticket.Status is EhcTicketStatus.New or EhcTicketStatus.Reopened)
        {
            ticket.FirstRespondedAt = now;
            await ticketRepo.UpdateAsync(ticket);
        }

        await AddAuditEventAsync(
            ticket,
            eventType: "AgentMessage",
            title: "Support replied",
            body: "Support sent a message.",
            isInternal: false,
            actorUserId: authorUserId,
            data: new { messageId = message.Id });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SynchronizePropertyEnquiryCrmAsync(ticket, cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Message",
            audience: "Requester",
            ticketId: ticket.Id,
            triggeredByUserId: authorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["requesterUserId"] = ticket.RequesterUserId,
                ["ActionUrl"] = requesterActionUrl
            },
            cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "AgentMessage",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: authorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                ["messageId"] = message.Id,
                ["messagePreview"] = BuildMessagePreview(message.Body),
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        var mentionIds = await ResolveMentionedUserIdsAsync(tenantId, message.Body, cancellationToken);
        var filteredMentionIds = mentionIds.Where(id => id != Guid.Empty && id != authorUserId).Distinct().ToList();
        if (filteredMentionIds.Count > 0)
        {
            await PublishTicketTopicAsync(
                tenantId,
                activity: "Mention",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: authorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                    ["mentionedUserIds"] = filteredMentionIds,
                    ["mentionedByName"] = _currentUserService.UserName ?? "Someone",
                    ["messageId"] = message.Id,
                    ["messagePreview"] = BuildMessagePreview(message.Body),
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
        }

        return new EhcTicketMessageDto
        {
            Id = message.Id,
            Body = message.Body,
            IsInternal = false,
            AuthorUserId = authorUserId,
            AuthorName = _currentUserService.UserName,
            CreatedAt = message.CreatedAt
        };
    }

    public async Task<EhcTicketAttachmentDto> AddInternalAttachmentAsync(Guid ticketId, AddEhcTicketAttachmentRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath) || string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException("FilePath and FileName are required.");
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var uploadRecord = await _unitOfWork.Repository<FileUploadRecord>()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && !r.IsDeleted && r.FilePath == request.FilePath.Trim());

        if (uploadRecord == null)
        {
            throw new ArgumentException("Invalid file reference. Please upload the file again.", nameof(request.FilePath));
        }

        if (!string.Equals(uploadRecord.Category, EhcUploadCategory, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid upload category for ticket attachments.", nameof(request.FilePath));
        }

        if (uploadRecord.UploadedByUserId != actorUserId)
        {
            throw new UnauthorizedAccessException("You can only attach files you uploaded.");
        }

        EhcTicketMessage? message = null;
        if (request.MessageId.HasValue && request.MessageId.Value != Guid.Empty)
        {
            message = await _unitOfWork.Repository<EhcTicketMessage>().GetByIdAsync(request.MessageId.Value);
            if (message == null || message.TenantId != tenantId || message.TicketId != ticketId)
            {
                throw new ArgumentException("Invalid messageId.", nameof(request.MessageId));
            }
        }

        var now = DateTime.UtcNow;
        var isInternal = message?.IsInternal == true || request.IsInternal;

        var attachment = new EhcTicketAttachment
        {
            TenantId = tenantId,
            TicketId = ticketId,
            MessageId = request.MessageId,
            FilePath = uploadRecord.FilePath,
            FileName = string.IsNullOrWhiteSpace(uploadRecord.OriginalFileName) ? request.FileName.Trim() : uploadRecord.OriginalFileName,
            ContentType = uploadRecord.ContentType ?? request.ContentType,
            FileSize = uploadRecord.FileSize > 0 ? uploadRecord.FileSize : request.FileSize,
            IsInternal = isInternal,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = actorUserId
        };

        await _unitOfWork.Repository<EhcTicketAttachment>().AddAsync(attachment);

        await AddAuditEventAsync(
            ticket,
            eventType: isInternal ? "InternalAttachment" : "AgentAttachment",
            title: "Attachment uploaded",
            body: $"{(isInternal ? "Internal" : "Support")} attachment uploaded: {attachment.FileName}.",
            isInternal: isInternal,
            actorUserId: actorUserId,
            data: new { attachmentId = attachment.Id, fileName = attachment.FileName, fileSize = attachment.FileSize, isInternal });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SynchronizePropertyEnquiryCrmAsync(ticket, cancellationToken);

        // Notify requester if this attachment is external-facing.
        if (!isInternal)
        {
            var requesterActionUrl = await ResolveRequesterActionUrlAsync(ticket, cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "Attachment",
                audience: "Requester",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["requesterUserId"] = ticket.RequesterUserId,
                    ["fileName"] = attachment.FileName,
                    ["ActionUrl"] = requesterActionUrl
                },
                cancellationToken);
        }

        return await MapToAttachmentDtoAsync(attachment);
    }

    public async Task<IReadOnlyList<EhcTicketListItemDto>> GetTicketsAsync(
        int page = 1,
        int pageSize = 25,
        EhcTicketStatus? status = null,
        EhcTicketType? ticketType = null,
        EhcTicketPriority? priority = null,
        EhcTicketSource? source = null,
        Guid? categoryId = null,
        Guid? assignedDepartmentId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return Array.Empty<EhcTicketListItemDto>();
        }

        var tickets = await _ticketRepository.GetPagedFilteredAsync(
            tenantId,
            page,
            pageSize,
            status: status,
            ticketType: ticketType,
            priority: priority,
            source: source,
            categoryId: categoryId,
            assignedDepartmentId: assignedDepartmentId,
            createdFrom: createdFrom,
            createdTo: createdTo,
            cancellationToken: cancellationToken);

        var feedbackByTicketId = await GetLatestFeedbackByTicketIdsAsync(tenantId, tickets.Select(t => t.Id).ToList(), cancellationToken);
        return tickets.Select(t =>
        {
            feedbackByTicketId.TryGetValue(t.Id, out var f);
            return MapToInternalListItemDto(t, f?.Rating, f?.SubmittedAtUtc);
        }).ToList();
    }

    public async Task<EhcTicketDetailDto?> GetTicketByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        var ticket = await _ticketRepository.GetByIdWithDetailsAsync(id, tenantId, cancellationToken);
        return ticket == null ? null : await MapToDetailDtoAsync(ticket, includeInternal: true, cancellationToken);
    }

    public async Task AssignTicketAsync(Guid ticketId, Guid assignedToUserId, Guid? assignedOrganizationUnitId = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var previousAssignee = ticket.AssignedToUserId;
        var previousOrganizationUnit = ticket.AssignedOrganizationUnitId;

        OrganizationUnit? organizationUnit = null;
        if (assignedOrganizationUnitId.HasValue && assignedOrganizationUnitId.Value != Guid.Empty)
        {
            organizationUnit = await _unitOfWork.Repository<OrganizationUnit>().GetByIdAsync(assignedOrganizationUnitId.Value);
            if (organizationUnit == null || organizationUnit.TenantId != tenantId || organizationUnit.IsDeleted || !organizationUnit.IsActive)
            {
                throw new ArgumentException("Invalid organization unit.", nameof(assignedOrganizationUnitId));
            }
        }

        ticket.AssignedToUserId = assignedToUserId;
        ticket.AssignedOrganizationUnitId = organizationUnit?.Id;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedBy = _currentUserService.UserName;
        ticket.LastModifiedById = actorUserId;

        await ticketRepo.UpdateAsync(ticket);

        // Add an internal audit message for traceability (hidden from external users).
        if (previousAssignee != assignedToUserId || previousOrganizationUnit != ticket.AssignedOrganizationUnitId)
        {
            var assigneeName = assignedToUserId.ToString();

            var who = _currentUserService.UserName ?? "System";
            var body = organizationUnit != null
                ? $"Assignment updated by {who}: {assigneeName} • {organizationUnit.Name}"
                : $"Assignment updated by {who}: {assigneeName}";

            await _unitOfWork.Repository<EhcTicketMessage>().AddAsync(new EhcTicketMessage
            {
                TenantId = tenantId,
                TicketId = ticketId,
                Body = body,
                IsInternal = true,
                AuthorUserId = actorUserId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = who,
                CreatedById = actorUserId
            });

            await AddAuditEventAsync(
                ticket,
                eventType: "AssignmentChanged",
                title: "Assignment updated",
                body: body,
                isInternal: true,
                actorUserId: actorUserId,
                data: new
                {
                    previousAssignee = previousAssignee,
                    previousOrganizationUnit = previousOrganizationUnit,
                    assignedToUserId,
                    assignedOrganizationUnitId = ticket.AssignedOrganizationUnitId
                });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SynchronizePropertyEnquiryCrmAsync(ticket, cancellationToken);

        if (previousOrganizationUnit != ticket.AssignedOrganizationUnitId && ticket.Status != EhcTicketStatus.New)
        {
            await PublishPropertyEnquirySalesReadyAsync(ticket, actorUserId, cancellationToken);
        }

        if (previousAssignee != assignedToUserId || previousOrganizationUnit != ticket.AssignedOrganizationUnitId)
        {
            await PublishTicketTopicAsync(
                tenantId,
                activity: "Assigned",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["assignedToUserId"] = assignedToUserId,
                    ["assignedOrganizationUnitId"] = ticket.AssignedOrganizationUnitId ?? Guid.Empty,
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
        }
    }

    public async Task TransitionTicketAsync(Guid ticketId, EhcTicketStatus targetStatus, string? notes = null, Guid? workflowTransitionId = null, string? workflowTransitionName = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var fromStatus = ticket.Status;
        if (fromStatus == targetStatus)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var requesterActionUrl = await ResolveRequesterActionUrlAsync(ticket, cancellationToken);

        if (IsSlaPausedStatus(fromStatus) && !IsSlaPausedStatus(targetStatus))
        {
            await ExtendSlaDueDatesForPendingAsync(tenantId, ticket, now, cancellationToken);
        }

        // Pending statuses are represented as a workflow "RequestInformation" pause in phase 1.
        if (targetStatus is EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty)
        {
            if (fromStatus is EhcTicketStatus.Resolved or EhcTicketStatus.Closed)
            {
                throw new InvalidOperationException("Cannot request information for a resolved/closed ticket.");
            }

            await PauseWorkflowForInformationAsync(ticket, actorUserId, notes, cancellationToken);
            ticket.Status = targetStatus;
            ticket.UpdatedAt = now;
            await ticketRepo.UpdateAsync(ticket);
            await AddStatusHistoryAsync(ticket, fromStatus, targetStatus, notes, actorUserId);

            await AddAuditEventAsync(
                ticket,
                eventType: "StatusChanged",
                title: "Status updated",
                body: $"Status: {fromStatus} → {targetStatus}",
                isInternal: false,
                actorUserId: actorUserId,
                data: new { fromStatus = fromStatus.ToString(), toStatus = targetStatus.ToString() });

            if (!string.IsNullOrWhiteSpace(notes))
            {
                await AddAuditEventAsync(
                    ticket,
                    eventType: "StatusNotes",
                    title: "Transition notes",
                    body: notes,
                    isInternal: true,
                    actorUserId: actorUserId,
                    data: new { toStatus = targetStatus.ToString() });
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SynchronizePropertyEnquiryCrmAsync(ticket, cancellationToken);

            if (fromStatus == EhcTicketStatus.New)
            {
                await PublishPropertyEnquirySalesReadyAsync(ticket, actorUserId, cancellationToken);
            }

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Requester",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = targetStatus.ToString(),
                    ["requesterUserId"] = ticket.RequesterUserId,
                    ["ActionUrl"] = requesterActionUrl
                },
                cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = targetStatus.ToString(),
                    ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
            return;
        }

        // Resume from a pending state without advancing workflow (workflow step is already paused on the same step).
        if (targetStatus == EhcTicketStatus.InProgress && fromStatus is EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty)
        {
            ticket.Status = EhcTicketStatus.InProgress;
            ticket.UpdatedAt = now;
            ticket.UpdatedBy = _currentUserService.UserName;
            ticket.LastModifiedById = actorUserId;

            await ticketRepo.UpdateAsync(ticket);
            await AddStatusHistoryAsync(ticket, fromStatus, EhcTicketStatus.InProgress, notes ?? "Resumed", actorUserId);

            await AddAuditEventAsync(
                ticket,
                eventType: "StatusChanged",
                title: "Status updated",
                body: $"Status: {fromStatus} → InProgress",
                isInternal: false,
                actorUserId: actorUserId,
                data: new { fromStatus = fromStatus.ToString(), toStatus = EhcTicketStatus.InProgress.ToString() });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SynchronizePropertyEnquiryCrmAsync(ticket, cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Requester",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = EhcTicketStatus.InProgress.ToString(),
                    ["requesterUserId"] = ticket.RequesterUserId,
                    ["ActionUrl"] = requesterActionUrl
                },
                cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = EhcTicketStatus.InProgress.ToString(),
                    ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
            return;
        }

        if (targetStatus == EhcTicketStatus.Reopened)
        {
            if (fromStatus is not (EhcTicketStatus.Resolved or EhcTicketStatus.Closed))
            {
                throw new InvalidOperationException("Only resolved/closed tickets can be reopened.");
            }

            // Reopen is modeled as a new workflow instance in phase 1.
            var workflowInstance = await _workflowEngine.StartWorkflowAsync(DefaultWorkflowName, ticket.Id, actorUserId, new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                ticketType = ticket.TicketType.ToString(),
                priority = ticket.Priority.ToString(),
                reopenedFrom = ticket.Status.ToString()
            });

            ticket.WorkflowInstanceId = workflowInstance.Id;
            ticket.Status = EhcTicketStatus.Reopened;
            ticket.ResolvedAt = null;
            ticket.ClosedAt = null;
            ticket.UpdatedAt = now;
            await ticketRepo.UpdateAsync(ticket);
            await AddStatusHistoryAsync(ticket, fromStatus, EhcTicketStatus.Reopened, notes ?? "Ticket reopened", actorUserId);

            await AddAuditEventAsync(
                ticket,
                eventType: "StatusChanged",
                title: "Ticket reopened",
                body: $"Status: {fromStatus} → Reopened",
                isInternal: false,
                actorUserId: actorUserId,
                data: new { fromStatus = fromStatus.ToString(), toStatus = EhcTicketStatus.Reopened.ToString() });

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await SynchronizePropertyEnquiryCrmAsync(ticket, cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Requester",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = EhcTicketStatus.Reopened.ToString(),
                    ["requesterUserId"] = ticket.RequesterUserId,
                    ["ActionUrl"] = requesterActionUrl
                },
                cancellationToken);

            await PublishTicketTopicAsync(
                tenantId,
                activity: "StatusChanged",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: actorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["fromStatus"] = fromStatus.ToString(),
                    ["toStatus"] = EhcTicketStatus.Reopened.ToString(),
                    ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
            return;
        }

        if (!ticket.WorkflowInstanceId.HasValue || ticket.WorkflowInstanceId == Guid.Empty)
        {
            throw new InvalidOperationException("Ticket has no workflow instance.");
        }

        // Branching workflows: execute a specific transition when provided, otherwise resolve unambiguously by status.
        var availableTransitions = await _workflowEngine.GetAvailableTransitionsAsync(ticket.WorkflowInstanceId.Value, new
        {
            ticketId = ticket.Id,
            ticketNumber = ticket.TicketNumber
        });

        var candidates = new List<Core.Entities.Workflow.WorkflowTransition>();
        foreach (var t in availableTransitions ?? Enumerable.Empty<Core.Entities.Workflow.WorkflowTransition>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = t.ToStep ?? await _workflowStepRepository.GetByIdAsync(t.ToStepId);
            var stepName = step?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(stepName))
            {
                continue;
            }

            if (!Enum.TryParse<EhcTicketStatus>(stepName, ignoreCase: true, out var status))
            {
                continue;
            }

            if (status == targetStatus)
            {
                candidates.Add(t);
            }
        }

        Core.Entities.Workflow.WorkflowTransition? selected = null;
        if (workflowTransitionId.HasValue && workflowTransitionId.Value != Guid.Empty)
        {
            selected = candidates.FirstOrDefault(x => x.Id == workflowTransitionId.Value);
            if (selected == null)
            {
                throw new InvalidOperationException("Selected workflow transition is not available for the chosen status.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(workflowTransitionName))
        {
            var matches = candidates
                .Where(x => x.Name != null && x.Name.Equals(workflowTransitionName.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                throw new InvalidOperationException("Selected workflow transition is not available for the chosen status.");
            }
            if (matches.Count > 1)
            {
                throw new InvalidOperationException("Multiple workflow transitions match this name. Please select by transitionId.");
            }

            selected = matches[0];
        }
        else
        {
            if (candidates.Count == 1)
            {
                selected = candidates[0];
            }
            else if (candidates.Count > 1)
            {
                throw new InvalidOperationException("Multiple workflow transitions are available for the chosen status. Please select a specific transition.");
            }
        }

        if (selected == null)
        {
            var allowedStatuses = new HashSet<EhcTicketStatus>();
            foreach (var t in availableTransitions ?? Enumerable.Empty<Core.Entities.Workflow.WorkflowTransition>())
            {
                var step = t.ToStep ?? await _workflowStepRepository.GetByIdAsync(t.ToStepId);
                var stepName = step?.Name?.Trim();
                if (string.IsNullOrWhiteSpace(stepName)) continue;
                if (!Enum.TryParse<EhcTicketStatus>(stepName, ignoreCase: true, out var s)) continue;
                allowedStatuses.Add(s);
            }

            var allowedText = allowedStatuses.Count == 0
                ? "none"
                : string.Join(", ", allowedStatuses.OrderBy(x => x).Select(x => x.ToString()));

            throw new InvalidOperationException($"Transition '{fromStatus}' → '{targetStatus}' is not allowed by the workflow. Allowed next statuses: {allowedText}.");
        }

        await _workflowEngine.ExecuteTransitionAsync(ticket.WorkflowInstanceId.Value, actorUserId, selected.Id, new
        {
            ticketId = ticket.Id,
            ticketNumber = ticket.TicketNumber,
            targetStatus = targetStatus.ToString(),
            workflowTransitionId = selected.Id,
            workflowTransitionName = selected.Name
        });

        // Update ticket status derived from workflow step name (baseline: step names match status).
        ticket.Status = targetStatus;
        if (targetStatus == EhcTicketStatus.Acknowledged && ticket.FirstRespondedAt == null)
        {
            ticket.FirstRespondedAt = now;
        }
        if (targetStatus == EhcTicketStatus.Resolved)
        {
            ticket.ResolvedAt = now;
        }
        if (targetStatus == EhcTicketStatus.Closed)
        {
            ticket.ClosedAt = now;
        }

        ticket.UpdatedAt = now;
        ticket.UpdatedBy = _currentUserService.UserName;
        ticket.LastModifiedById = actorUserId;

        await ticketRepo.UpdateAsync(ticket);
        await AddStatusHistoryAsync(ticket, fromStatus, targetStatus, notes, actorUserId);

        await AddAuditEventAsync(
            ticket,
            eventType: "StatusChanged",
            title: "Status updated",
            body: $"Status: {fromStatus} → {targetStatus}",
            isInternal: false,
            actorUserId: actorUserId,
            data: new { fromStatus = fromStatus.ToString(), toStatus = targetStatus.ToString() });

        if (!string.IsNullOrWhiteSpace(notes))
        {
            await AddAuditEventAsync(
                ticket,
                eventType: "StatusNotes",
                title: "Transition notes",
                body: notes,
                isInternal: true,
                actorUserId: actorUserId,
                data: new { toStatus = targetStatus.ToString() });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SynchronizePropertyEnquiryCrmAsync(ticket, cancellationToken);

        if (fromStatus == EhcTicketStatus.New)
        {
            await PublishPropertyEnquirySalesReadyAsync(ticket, actorUserId, cancellationToken);
        }

        await PublishTicketTopicAsync(
            tenantId,
            activity: "StatusChanged",
            audience: "Requester",
            ticketId: ticket.Id,
            triggeredByUserId: actorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["fromStatus"] = fromStatus.ToString(),
                ["toStatus"] = targetStatus.ToString(),
                ["requesterUserId"] = ticket.RequesterUserId,
                ["ActionUrl"] = requesterActionUrl
            },
            cancellationToken);

        if (targetStatus is EhcTicketStatus.Resolved or EhcTicketStatus.Closed)
        {
            try
            {
                var requester = await _userManager.FindByIdAsync(ticket.RequesterUserId.ToString());
                // Phase 2: request CSAT only for external-portal (local-auth) requesters.
                if (requester?.AuthenticationProvider == AuthenticationProvider.Local)
                {
                    var alreadyHasFeedback = await _unitOfWork.Repository<EhcTicketFeedback>()
                        .GetQueryable(f => f.TenantId == tenantId && !f.IsDeleted && f.TicketId == ticket.Id)
                        .AnyAsync(cancellationToken);

                    if (!alreadyHasFeedback)
                    {
                        await PublishTicketTopicAsync(
                            tenantId,
                            activity: "FeedbackRequested",
                            audience: "Requester",
                            ticketId: ticket.Id,
                            triggeredByUserId: actorUserId,
                            data: new Dictionary<string, object>
                            {
                                ["ticketId"] = ticket.Id,
                                ["ticketNumber"] = ticket.TicketNumber,
                                ["status"] = targetStatus.ToString(),
                                ["requesterUserId"] = ticket.RequesterUserId,
                                ["ActionUrl"] = requesterActionUrl
                            },
                            cancellationToken);
                    }
                }
                else
                {
                    // No CSAT prompt for internal ERP requesters.
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to publish feedback-requested event for ticket {TicketId}", ticket.Id);
            }
        }

        await PublishTicketTopicAsync(
            tenantId,
            activity: "StatusChanged",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: actorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["fromStatus"] = fromStatus.ToString(),
                ["toStatus"] = targetStatus.ToString(),
                ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<EhcTicketAllowedTransitionDto>> GetAllowedTransitionsAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var result = new List<EhcTicketAllowedTransitionDto>();

        if (ticket.WorkflowInstanceId.HasValue && ticket.WorkflowInstanceId.Value != Guid.Empty)
        {
            var transitions = await _workflowEngine.GetAvailableTransitionsAsync(ticket.WorkflowInstanceId.Value, new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber
            });

            foreach (var t in transitions ?? Enumerable.Empty<Core.Entities.Workflow.WorkflowTransition>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var step = t.ToStep ?? await _workflowStepRepository.GetByIdAsync(t.ToStepId);
                var stepName = step?.Name?.Trim();
                if (string.IsNullOrWhiteSpace(stepName))
                {
                    continue;
                }

                if (!Enum.TryParse<EhcTicketStatus>(stepName, ignoreCase: true, out var status))
                {
                    continue;
                }

                result.Add(new EhcTicketAllowedTransitionDto
                {
                    TransitionId = t.Id,
                    TransitionName = t.Name,
                    TransitionDescription = t.Description,
                    TargetStatus = status
                });
            }
        }

        // Phase 1: allow requesting info from active tickets.
        if (ticket.Status is not (EhcTicketStatus.Resolved or EhcTicketStatus.Closed))
        {
            result.Add(new EhcTicketAllowedTransitionDto
            {
                TransitionName = "Request info (user)",
                TransitionDescription = "Pause the ticket and request more details from the requester.",
                TargetStatus = EhcTicketStatus.PendingUser
            });
            result.Add(new EhcTicketAllowedTransitionDto
            {
                TransitionName = "Request info (3rd party)",
                TransitionDescription = "Pause the ticket while awaiting information from an external party/vendor.",
                TargetStatus = EhcTicketStatus.PendingThirdParty
            });
        }

        // Phase 1: allow resume from pending.
        if (ticket.Status is EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty)
        {
            result.Add(new EhcTicketAllowedTransitionDto
            {
                TransitionName = "Resume",
                TransitionDescription = "Resume work on the ticket after pending information is received.",
                TargetStatus = EhcTicketStatus.InProgress
            });
        }

        // Phase 1: allow reopen from resolved/closed.
        if (ticket.Status is EhcTicketStatus.Resolved or EhcTicketStatus.Closed)
        {
            result.Add(new EhcTicketAllowedTransitionDto
            {
                TransitionName = "Reopen",
                TransitionDescription = "Reopen the ticket and restart the workflow lifecycle.",
                TargetStatus = EhcTicketStatus.Reopened
            });
        }

        return result
            .OrderBy(x => x.TargetStatus)
            .ThenBy(x => x.TransitionName)
            .ToList();
    }

    public async Task<EhcTicketDetailDto> UpdateTicketRcaAsync(Guid ticketId, UpdateEhcTicketRcaRequestDto request, CancellationToken cancellationToken = default)
    {
        request ??= new UpdateEhcTicketRcaRequestDto();

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.FirstOrDefaultAsync(
            t => t.Id == ticketId && t.TenantId == tenantId && !t.IsDeleted,
            t => t.RootCause,
            t => t.Category,
            t => t.Subcategory,
            t => t.AssignedDepartment,
            t => t.AssignedOrganizationUnit,
            t => t.AssignedToUser,
            t => t.RequesterUser,
            t => t.Messages!,
            t => t.Attachments!,
            t => t.StatusHistory!,
            t => t.AuditEvents!);

        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        if (ticket.TicketType != EhcTicketType.Complaint)
        {
            throw new InvalidOperationException("RCA fields are only applicable to Complaint tickets.");
        }

        Guid? rootCauseId = request.RootCauseId.HasValue && request.RootCauseId.Value == Guid.Empty
            ? (Guid?)null
            : request.RootCauseId;

        EhcRootCauseCode? rootCause = null;
        if (rootCauseId.HasValue)
        {
            rootCause = await _unitOfWork.Repository<EhcRootCauseCode>().FirstOrDefaultAsync(
                r => r.Id == rootCauseId.Value && r.TenantId == tenantId && !r.IsDeleted && r.IsActive);

            if (rootCause == null)
            {
                throw new ArgumentException("Selected root cause does not exist.");
            }
        }

        ticket.RootCauseId = rootCauseId;
        ticket.RootCause = rootCause;
        ticket.RootCauseDetails = string.IsNullOrWhiteSpace(request.RootCauseDetails) ? null : request.RootCauseDetails.Trim();
        ticket.ResolutionSummary = string.IsNullOrWhiteSpace(request.ResolutionSummary) ? null : request.ResolutionSummary.Trim();
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedBy = _currentUserService.UserName;
        ticket.LastModifiedById = actorUserId;

        await ticketRepo.UpdateAsync(ticket);

        await AddAuditEventAsync(
            ticket,
            eventType: "RcaUpdated",
            title: "RCA updated",
            body: $"Root cause: {(rootCause == null ? "—" : $"{rootCause.Code} - {rootCause.Name}")}",
            isInternal: true,
            actorUserId: actorUserId,
            data: new
            {
                rootCauseId = rootCauseId,
                rootCauseCode = rootCause?.Code,
                rootCauseName = rootCause?.Name
            });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "Updated",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: actorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        // Return full internal view.
        return await MapToDetailDtoAsync(ticket, includeInternal: true, cancellationToken);
    }

    private async Task<HashSet<EhcTicketStatus>> GetAllowedTransitionsInternalAsync(EhcTicket ticket, CancellationToken cancellationToken)
    {
        var statuses = new HashSet<EhcTicketStatus>();

        if (ticket.WorkflowInstanceId.HasValue && ticket.WorkflowInstanceId.Value != Guid.Empty)
        {
            var transitions = await _workflowEngine.GetAvailableTransitionsAsync(ticket.WorkflowInstanceId.Value, new
            {
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber
            });

            foreach (var t in transitions ?? Enumerable.Empty<Core.Entities.Workflow.WorkflowTransition>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var step = t.ToStep ?? await _workflowStepRepository.GetByIdAsync(t.ToStepId);
                var stepName = step?.Name?.Trim();
                if (string.IsNullOrWhiteSpace(stepName))
                {
                    continue;
                }

                if (!Enum.TryParse<EhcTicketStatus>(stepName, ignoreCase: true, out var status))
                {
                    continue;
                }

                statuses.Add(status);
            }
        }

        // Phase 1: allow resume from pending
        if (ticket.Status is EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty)
        {
            statuses.Add(EhcTicketStatus.InProgress);
        }

        return statuses;
    }

    public async Task<EhcTicketMessageDto> AddInternalCommentAsync(Guid ticketId, AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Message body is required.", nameof(request.Body));
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        if (!Guid.TryParse(_currentUserService.UserId, out var authorUserId) || authorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Authenticated user context is required.");
        }

        var ticketRepo = _unitOfWork.Repository<EhcTicket>();
        var ticket = await ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null || ticket.TenantId != tenantId)
        {
            throw new KeyNotFoundException("Ticket not found.");
        }

        var now = DateTime.UtcNow;
        var message = new EhcTicketMessage
        {
            TenantId = tenantId,
            TicketId = ticketId,
            Body = request.Body.Trim(),
            IsInternal = true,
            AuthorUserId = authorUserId,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = authorUserId
        };

        await _unitOfWork.Repository<EhcTicketMessage>().AddAsync(message);

        // Mark first response timestamp when an internal agent first interacts
        if (ticket.FirstRespondedAt == null && ticket.Status is EhcTicketStatus.New or EhcTicketStatus.Reopened)
        {
            ticket.FirstRespondedAt = now;
            await ticketRepo.UpdateAsync(ticket);
        }

        await AddAuditEventAsync(
            ticket,
            eventType: "InternalComment",
            title: "Internal note added",
            body: "An internal note was added.",
            isInternal: true,
            actorUserId: authorUserId,
            data: new { messageId = message.Id });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SynchronizePropertyEnquiryCrmAsync(ticket, cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "InternalComment",
            audience: "Internal",
            ticketId: ticket.Id,
            triggeredByUserId: authorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticket.TicketNumber,
                ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                ["messageId"] = message.Id,
                ["messagePreview"] = BuildMessagePreview(message.Body),
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken);

        var mentionIds = await ResolveMentionedUserIdsAsync(tenantId, message.Body, cancellationToken);
        var filteredMentionIds = mentionIds.Where(id => id != Guid.Empty && id != authorUserId).Distinct().ToList();
        if (filteredMentionIds.Count > 0)
        {
            await PublishTicketTopicAsync(
                tenantId,
                activity: "Mention",
                audience: "Internal",
                ticketId: ticket.Id,
                triggeredByUserId: authorUserId,
                data: new Dictionary<string, object>
                {
                    ["ticketId"] = ticket.Id,
                    ["ticketNumber"] = ticket.TicketNumber,
                    ["assignedToUserId"] = ticket.AssignedToUserId ?? Guid.Empty,
                    ["mentionedUserIds"] = filteredMentionIds,
                    ["mentionedByName"] = _currentUserService.UserName ?? "Someone",
                    ["messageId"] = message.Id,
                    ["messagePreview"] = BuildMessagePreview(message.Body),
                    ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                },
                cancellationToken);
        }

        return new EhcTicketMessageDto
        {
            Id = message.Id,
            Body = message.Body,
            IsInternal = true,
            AuthorUserId = authorUserId,
            AuthorName = _currentUserService.UserName,
            CreatedAt = message.CreatedAt
        };
    }

    private async Task<EhcSlaTemplate?> ResolveSlaTemplateAsync(Guid tenantId, CreateEhcTicketRequestDto request, CancellationToken cancellationToken)
    {
        var templates = (await _unitOfWork.Repository<EhcSlaTemplate>()
                .FindAsync(t => t.TenantId == tenantId && t.IsActive && !t.IsDeleted))
            .ToList();

        if (templates.Count == 0)
        {
            return null;
        }

        var candidates = templates
            .Where(t =>
                (!t.CategoryId.HasValue || t.CategoryId == request.CategoryId) &&
                (!t.TicketType.HasValue || t.TicketType == request.TicketType) &&
                (!t.Priority.HasValue || t.Priority == request.Priority))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        // Phase 1 scoring: prioritize most specific match.
        var best = candidates
            .Select(t => new
            {
                Template = t,
                Score =
                    (t.CategoryId.HasValue && t.CategoryId == request.CategoryId ? 100 : 0) +
                    (t.TicketType.HasValue && t.TicketType == request.TicketType ? 10 : 0) +
                    (t.Priority.HasValue && t.Priority == request.Priority ? 1 : 0)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Template.FirstResponseMinutes)
            .FirstOrDefault();

        return best?.Template;
    }

    private static bool IsSlaPausedStatus(EhcTicketStatus status) =>
        status is EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty;

    private async Task ExtendSlaDueDatesForPendingAsync(Guid tenantId, EhcTicket ticket, DateTime pauseEndUtc, CancellationToken cancellationToken)
    {
        var statusRepo = _unitOfWork.Repository<EhcTicketStatusHistory>();
        var pendingStart = await statusRepo
            .GetQueryable(h =>
                h.TenantId == tenantId &&
                !h.IsDeleted &&
                h.TicketId == ticket.Id &&
                (h.ToStatus == EhcTicketStatus.PendingUser || h.ToStatus == EhcTicketStatus.PendingThirdParty) &&
                (!h.FromStatus.HasValue || (h.FromStatus.Value != EhcTicketStatus.PendingUser && h.FromStatus.Value != EhcTicketStatus.PendingThirdParty)))
            .AsNoTracking()
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => (DateTime?)h.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (!pendingStart.HasValue) return;
        if (pauseEndUtc <= pendingStart.Value) return;

        var calendarJson = ticket.AppliedSlaCalendarConfigurationJson;
        if (string.IsNullOrWhiteSpace(calendarJson))
        {
            // Best-effort fallback for older tickets created before SLA snapshot fields existed.
            var best = await ResolveSlaTemplateAsync(tenantId, new CreateEhcTicketRequestDto
            {
                TicketType = ticket.TicketType,
                CategoryId = ticket.CategoryId,
                SubcategoryId = ticket.SubcategoryId,
                Priority = ticket.Priority,
                Source = ticket.Source,
                Subject = ticket.Subject,
                Description = ticket.Description
            }, cancellationToken);

            calendarJson = best?.CalendarConfigurationJson;
        }

        if (string.IsNullOrWhiteSpace(calendarJson))
        {
            var pauseDuration = pauseEndUtc - pendingStart.Value;
            if (pauseDuration <= TimeSpan.Zero) return;

            if (ticket.FirstRespondedAt == null &&
                ticket.FirstResponseDueAt.HasValue &&
                ticket.FirstResponseDueAt.Value > pendingStart.Value)
            {
                ticket.FirstResponseDueAt = ticket.FirstResponseDueAt.Value.Add(pauseDuration);
            }

            if (ticket.ResolvedAt == null &&
                ticket.ResolutionDueAt.HasValue &&
                ticket.ResolutionDueAt.Value > pendingStart.Value)
            {
                ticket.ResolutionDueAt = ticket.ResolutionDueAt.Value.Add(pauseDuration);
            }
            return;
        }

        var pausedBusinessMinutes = EhcSlaTimeCalculator.CalculateBusinessMinutesBetweenUtc(pendingStart.Value, pauseEndUtc, calendarJson);
        if (pausedBusinessMinutes <= 0) return;

        if (ticket.FirstRespondedAt == null &&
            ticket.FirstResponseDueAt.HasValue &&
            ticket.FirstResponseDueAt.Value > pendingStart.Value)
        {
            ticket.FirstResponseDueAt = EhcSlaTimeCalculator.CalculateDueAtUtc(ticket.FirstResponseDueAt.Value, pausedBusinessMinutes, calendarJson);
        }

        if (ticket.ResolvedAt == null &&
            ticket.ResolutionDueAt.HasValue &&
            ticket.ResolutionDueAt.Value > pendingStart.Value)
        {
            ticket.ResolutionDueAt = EhcSlaTimeCalculator.CalculateDueAtUtc(ticket.ResolutionDueAt.Value, pausedBusinessMinutes, calendarJson);
        }
    }

    private async Task AddStatusHistoryAsync(EhcTicket ticket, EhcTicketStatus? from, EhcTicketStatus to, string? notes, Guid actorUserId)
    {
        await _unitOfWork.Repository<EhcTicketStatusHistory>().AddAsync(new EhcTicketStatusHistory
        {
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = actorUserId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName,
            CreatedById = actorUserId
        });
    }

    private async Task PauseWorkflowForInformationAsync(EhcTicket ticket, Guid actorUserId, string? notes, CancellationToken cancellationToken)
    {
        if (!ticket.WorkflowInstanceId.HasValue || ticket.WorkflowInstanceId == Guid.Empty)
        {
            return;
        }

        var stepInstance = await _workflowStepInstanceRepository.GetCurrentStepAsync(ticket.WorkflowInstanceId.Value);
        if (stepInstance == null)
        {
            return;
        }

        await _workflowEngine.ProcessStepAsync(
            stepInstance.Id,
            actorUserId,
            WorkflowStepAction.RequestInformation,
            resultData: null,
            comments: notes ?? "Additional information requested");
    }

    private async Task AdvanceWorkflowToStatusAsync(EhcTicket ticket, EhcTicketStatus targetStatus, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (!ticket.WorkflowInstanceId.HasValue || ticket.WorkflowInstanceId == Guid.Empty)
        {
            throw new InvalidOperationException("Ticket has no workflow instance.");
        }

        // Baseline definition assumes step names match the status labels.
        // We advance linearly until we reach the desired step.
        for (var i = 0; i < 10; i++)
        {
            var stepInstance = await _workflowStepInstanceRepository.GetCurrentStepAsync(ticket.WorkflowInstanceId.Value);
            if (stepInstance == null)
            {
                break;
            }

            var step = stepInstance.WorkflowStep ?? await _workflowStepRepository.GetByIdAsync(stepInstance.WorkflowStepId);
            var stepName = step?.Name?.Trim() ?? string.Empty;
            if (string.Equals(stepName, targetStatus.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await _workflowEngine.ExecuteNextStepAsync(ticket.WorkflowInstanceId.Value, actorUserId, new
            {
                targetStatus = targetStatus.ToString()
            });
        }
    }

    private static EhcTicketListItemDto MapToExternalListItemDto(EhcTicket ticket, int? feedbackRating, DateTime? feedbackSubmittedAtUtc)
    {
        return new EhcTicketListItemDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            TicketType = ticket.TicketType,
            Priority = ticket.Priority,
            Source = ticket.Source,
            Status = ticket.Status,
            Subject = ticket.Subject,
            CategoryName = ticket.Category?.Name,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            FirstResponseDueAt = ticket.FirstResponseDueAt,
            ResolutionDueAt = ticket.ResolutionDueAt,
            FirstRespondedAt = ticket.FirstRespondedAt,
            ResolvedAt = ticket.ResolvedAt,
            ClosedAt = ticket.ClosedAt,
            FeedbackRating = feedbackRating,
            FeedbackSubmittedAt = feedbackSubmittedAtUtc,
            AssignedDepartmentName = null,
            AssignedToName = null,
            RequesterName = null,
            RequesterAuthenticationProvider = null
        };
    }

    private static EhcTicketListItemDto MapToInternalListItemDto(EhcTicket ticket, int? feedbackRating, DateTime? feedbackSubmittedAtUtc)
    {
        var requesterName = ticket.RequesterUser != null
            ? $"{ticket.RequesterUser.FirstName} {ticket.RequesterUser.LastName}".Trim()
            : null;

        return new EhcTicketListItemDto
        {
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            TicketType = ticket.TicketType,
            Priority = ticket.Priority,
            Source = ticket.Source,
            Status = ticket.Status,
            Subject = ticket.Subject,
            CategoryName = ticket.Category?.Name,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            FirstResponseDueAt = ticket.FirstResponseDueAt,
            ResolutionDueAt = ticket.ResolutionDueAt,
            FirstRespondedAt = ticket.FirstRespondedAt,
            ResolvedAt = ticket.ResolvedAt,
            ClosedAt = ticket.ClosedAt,
            FeedbackRating = feedbackRating,
            FeedbackSubmittedAt = feedbackSubmittedAtUtc,
            AssignedOrganizationUnitName = ticket.AssignedOrganizationUnit?.Name,
            AssignedDepartmentName = ticket.AssignedDepartment?.Name,
            AssignedToName = ticket.AssignedToUser != null
                ? $"{ticket.AssignedToUser.FirstName} {ticket.AssignedToUser.LastName}".Trim()
                : null,
            RequesterName = string.IsNullOrWhiteSpace(requesterName) ? ticket.RequesterUser?.UserName : requesterName,
            RequesterAuthenticationProvider = ticket.RequesterUser?.AuthenticationProvider.ToString()
        };
    }

    private sealed record FeedbackSnapshot(int Rating, DateTime SubmittedAtUtc);

    private async Task<Dictionary<Guid, FeedbackSnapshot>> GetLatestFeedbackByTicketIdsAsync(Guid tenantId, List<Guid> ticketIds, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || ticketIds.Count == 0)
        {
            return new Dictionary<Guid, FeedbackSnapshot>();
        }

        var distinct = ticketIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return new Dictionary<Guid, FeedbackSnapshot>();
        }

        var feedbackRepo = _unitOfWork.Repository<EhcTicketFeedback>();
        var rows = await feedbackRepo
            .GetQueryable(f => f.TenantId == tenantId && !f.IsDeleted && distinct.Contains(f.TicketId))
            .AsNoTracking()
            .GroupBy(f => f.TicketId)
            .Select(g => g
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new { x.TicketId, x.Rating, x.CreatedAt })
                .FirstOrDefault())
            .ToListAsync(cancellationToken);

        var dict = new Dictionary<Guid, FeedbackSnapshot>();
        foreach (var r in rows)
        {
            if (r == null) continue;
            if (r.TicketId == Guid.Empty) continue;
            dict[r.TicketId] = new FeedbackSnapshot(r.Rating, r.CreatedAt);
        }

        return dict;
    }

    private async Task<EhcTicketDetailDto> MapToDetailDtoAsync(EhcTicket ticket, bool includeInternal, CancellationToken cancellationToken)
    {
        var feedbackRepo = _unitOfWork.Repository<EhcTicketFeedback>();
        var feedback = await feedbackRepo
            .GetQueryable(f => f.TenantId == ticket.TenantId && !f.IsDeleted && f.TicketId == ticket.Id)
            .OrderByDescending(f => f.CreatedAt)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var audit = (ticket.AuditEvents ?? new List<EhcTicketAuditEvent>())
            .Where(e => includeInternal || !e.IsInternal)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new EhcTicketAuditEventDto
            {
                Id = e.Id,
                EventType = e.EventType,
                Title = e.Title,
                Body = e.Body,
                IsInternal = e.IsInternal,
                ActorUserId = includeInternal ? e.ActorUserId : null,
                ActorName = includeInternal && e.ActorUser != null
                    ? $"{e.ActorUser.FirstName} {e.ActorUser.LastName}".Trim()
                    : null,
                CreatedAt = e.CreatedAt
            })
            .ToList();

        var messages = new List<EhcTicketMessageDto>();
        foreach (var m in (ticket.Messages ?? new List<EhcTicketMessage>())
                     .Where(x => includeInternal || !x.IsInternal)
                     .OrderBy(x => x.CreatedAt))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var msgAttachments = (m.Attachments ?? new List<EhcTicketAttachment>())
                .Where(a => includeInternal || !a.IsInternal)
                .OrderBy(a => a.CreatedAt)
                .ToList();

            var mappedMsgAttachments = msgAttachments.Count == 0
                ? new List<EhcTicketAttachmentDto>()
                : (await Task.WhenAll(msgAttachments.Select(MapToAttachmentDtoAsync))).ToList();

            messages.Add(new EhcTicketMessageDto
            {
                Id = m.Id,
                Body = m.Body,
                IsInternal = m.IsInternal,
                AuthorUserId = m.AuthorUserId,
                AuthorName = m.AuthorUser != null
                    ? $"{m.AuthorUser.FirstName} {m.AuthorUser.LastName}".Trim()
                    : null,
                CreatedAt = m.CreatedAt,
                Attachments = mappedMsgAttachments
            });
        }

        var ticketAttachments = (ticket.Attachments ?? new List<EhcTicketAttachment>())
            .Where(a => includeInternal || !a.IsInternal)
            .OrderBy(a => a.CreatedAt)
            .ToList();

        var attachments = ticketAttachments.Count == 0
            ? new List<EhcTicketAttachmentDto>()
            : (await Task.WhenAll(ticketAttachments.Select(MapToAttachmentDtoAsync))).ToList();

        var history = (ticket.StatusHistory ?? new List<EhcTicketStatusHistory>())
            .OrderBy(h => h.CreatedAt)
            .Select(h => new EhcTicketStatusHistoryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus,
                ToStatus = h.ToStatus,
                ChangedByUserId = includeInternal ? h.ChangedByUserId : null,
                ChangedByName = includeInternal && h.ChangedByUser != null
                    ? $"{h.ChangedByUser.FirstName} {h.ChangedByUser.LastName}".Trim()
                    : null,
                Notes = h.Notes,
                CreatedAt = h.CreatedAt
            })
            .ToList();

        return new EhcTicketDetailDto
        {
            PropertyListing = string.IsNullOrWhiteSpace(ticket.PropertyListingContextJson) ? null : JsonSerializer.Deserialize<EhcPropertyListingContextDto>(ticket.PropertyListingContextJson),
            Id = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            TicketType = ticket.TicketType,
            Priority = ticket.Priority,
            Source = ticket.Source,
            Status = ticket.Status,
            Subject = ticket.Subject,
            Description = ticket.Description,
            CategoryName = ticket.Category?.Name,
            SubcategoryName = ticket.Subcategory?.Name,
            RelatedEntityType = ticket.RelatedEntityType,
            RelatedEntityReference = ticket.RelatedEntityReference,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            CreatedBy = includeInternal ? ticket.CreatedBy : null,
            UpdatedBy = includeInternal ? ticket.UpdatedBy : null,
            FirstResponseDueAt = includeInternal ? ticket.FirstResponseDueAt : null,
            ResolutionDueAt = includeInternal ? ticket.ResolutionDueAt : null,
            FirstRespondedAt = includeInternal ? ticket.FirstRespondedAt : null,
            ResolvedAt = ticket.ResolvedAt,
            ClosedAt = ticket.ClosedAt,
            AssignedOrganizationUnitId = includeInternal ? ticket.AssignedOrganizationUnitId : null,
            AssignedOrganizationUnitName = includeInternal ? ticket.AssignedOrganizationUnit?.Name : null,
            AssignedDepartmentId = includeInternal ? ticket.AssignedDepartmentId : null,
            AssignedDepartmentName = includeInternal ? ticket.AssignedDepartment?.Name : null,
            AssignedToUserId = includeInternal ? ticket.AssignedToUserId : null,
            AssignedToName = includeInternal && ticket.AssignedToUser != null
                ? $"{ticket.AssignedToUser.FirstName} {ticket.AssignedToUser.LastName}".Trim()
                : null,
            RequesterName = includeInternal
                ? (ticket.RequesterUser != null ? $"{ticket.RequesterUser.FirstName} {ticket.RequesterUser.LastName}".Trim() : null)
                : null,
            RequesterEmail = includeInternal ? ticket.RequesterUser?.Email : null,
            RequesterAuthenticationProvider = includeInternal ? ticket.RequesterUser?.AuthenticationProvider.ToString() : null,
            RootCauseId = includeInternal ? ticket.RootCauseId : null,
            RootCauseCode = includeInternal ? ticket.RootCause?.Code : null,
            RootCauseName = includeInternal ? ticket.RootCause?.Name : null,
            RootCauseDetails = includeInternal ? ticket.RootCauseDetails : null,
            ResolutionSummary = includeInternal ? ticket.ResolutionSummary : null,
            FeedbackRating = feedback?.Rating,
            FeedbackComment = feedback?.Comment,
            FeedbackSubmittedAt = feedback?.CreatedAt,
            Messages = messages,
            Attachments = attachments,
            StatusHistory = history,
            AuditTrail = audit
        };
    }

    public async Task<IReadOnlyList<EhcTicketLinkDto>> GetTicketLinksAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty || ticketId == Guid.Empty)
            return Array.Empty<EhcTicketLinkDto>();

        var exists = await _ticketRepository.Query()
            .AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Id == ticketId, cancellationToken);
        if (!exists)
            return Array.Empty<EhcTicketLinkDto>();

        var linkRepo = _unitOfWork.Repository<EhcTicketLink>();
        var links = (await linkRepo.FindAsync(l =>
                l.TenantId == tenantId &&
                (l.TicketId == ticketId || l.RelatedTicketId == ticketId)))
            .OrderByDescending(l => l.CreatedAt)
            .ToList();

        if (links.Count == 0)
            return Array.Empty<EhcTicketLinkDto>();

        var otherIds = links
            .Select(l => l.TicketId == ticketId ? l.RelatedTicketId : l.TicketId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (otherIds.Count == 0)
            return Array.Empty<EhcTicketLinkDto>();

        var otherTickets = await _ticketRepository.Query()
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && otherIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.Subject,
                t.Status,
                t.Priority,
                t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var byId = otherTickets.ToDictionary(x => x.Id, x => x);

        static string RelationshipLabel(EhcTicketLink link, Guid currentTicketId)
        {
            var forward = link.TicketId == currentTicketId;
            return link.LinkType switch
            {
                EhcTicketLinkType.Related => "Related to",
                EhcTicketLinkType.ParentOf => forward ? "Parent of" : "Child of",
                EhcTicketLinkType.DuplicateOf => forward ? "Duplicate of" : "Duplicate ticket",
                _ => "Related to"
            };
        }

        var outList = new List<EhcTicketLinkDto>(links.Count);
        foreach (var link in links)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var otherId = link.TicketId == ticketId ? link.RelatedTicketId : link.TicketId;
            if (otherId == Guid.Empty) continue;
            if (!byId.TryGetValue(otherId, out var other)) continue;

            outList.Add(new EhcTicketLinkDto
            {
                Id = link.Id,
                LinkType = link.LinkType,
                RelationshipLabel = RelationshipLabel(link, ticketId),
                LinkedTicketId = other.Id,
                LinkedTicketNumber = other.TicketNumber,
                LinkedSubject = other.Subject,
                LinkedStatus = other.Status,
                LinkedPriority = other.Priority,
                LinkedCreatedAt = other.CreatedAt,
                CreatedAt = link.CreatedAt,
                CreatedBy = link.CreatedBy
            });
        }

        return outList;
    }

    public async Task<EhcTicketLinkDto> CreateTicketLinkAsync(Guid ticketId, CreateEhcTicketLinkRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant context is required.");
        if (ticketId == Guid.Empty)
            throw new ArgumentException("TicketId is required.");

        request ??= new CreateEhcTicketLinkRequestDto();
        if (request.RelatedTicketId == Guid.Empty)
            throw new ArgumentException("RelatedTicketId is required.");
        if (request.RelatedTicketId == ticketId)
            throw new ArgumentException("A ticket cannot be linked to itself.");

        var actorUserId = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;

        var type = request.LinkType;
        var reverseDirection = request.ReverseDirection && type != EhcTicketLinkType.Related;
        var fromId = reverseDirection ? request.RelatedTicketId : ticketId;
        var toId = reverseDirection ? ticketId : request.RelatedTicketId;

        var fromTicket = await _ticketRepository.Query()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Id == fromId, cancellationToken);
        if (fromTicket == null)
            throw new ArgumentException("Ticket not found.");

        var toTicket = await _ticketRepository.Query()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && !t.IsDeleted && t.Id == toId, cancellationToken);
        if (toTicket == null)
            throw new ArgumentException("Related ticket not found.");

        var currentTicket = ticketId == fromId ? fromTicket : toTicket;
        var linkedTicket = ticketId == fromId ? toTicket : fromTicket;

        var linkRepo = _unitOfWork.Repository<EhcTicketLink>();

        if (type == EhcTicketLinkType.Related)
        {
            var exists = await linkRepo.ExistsAsync(l =>
                l.TenantId == tenantId &&
                l.LinkType == EhcTicketLinkType.Related &&
                ((l.TicketId == fromId && l.RelatedTicketId == toId) ||
                 (l.TicketId == toId && l.RelatedTicketId == fromId)));
            if (exists)
                throw new ArgumentException("A related-ticket link already exists.");
        }
        else
        {
            var exists = await linkRepo.ExistsAsync(l =>
                l.TenantId == tenantId &&
                l.LinkType == type &&
                l.TicketId == fromId &&
                l.RelatedTicketId == toId);
            if (exists)
                throw new ArgumentException("This ticket link already exists.");

            if (type == EhcTicketLinkType.ParentOf)
            {
                var inverseExists = await linkRepo.ExistsAsync(l =>
                    l.TenantId == tenantId &&
                    l.LinkType == EhcTicketLinkType.ParentOf &&
                    l.TicketId == toId &&
                    l.RelatedTicketId == fromId);
                if (inverseExists)
                    throw new ArgumentException("A parent/child link in the opposite direction already exists.");
            }
        }

        var entity = new EhcTicketLink
        {
            TenantId = tenantId,
            TicketId = fromId,
            RelatedTicketId = toId,
            LinkType = type,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName ?? "System",
            CreatedById = actorUserId
        };

        await linkRepo.AddAsync(entity);

        static string Describe(EhcTicketLinkType t, bool forward) => t switch
        {
            EhcTicketLinkType.Related => "Related to",
            EhcTicketLinkType.ParentOf => forward ? "Parent of" : "Child of",
            EhcTicketLinkType.DuplicateOf => forward ? "Duplicate of" : "Duplicate ticket",
            _ => "Related to"
        };

        var currentForward = entity.TicketId == currentTicket.Id;
        var linkedForward = entity.TicketId == linkedTicket.Id;
        var currentLabel = Describe(type, currentForward);
        var linkedLabel = Describe(type, linkedForward);

        await AddAuditEventAsync(
            currentTicket,
            eventType: "TicketLinkCreated",
            title: "Ticket link added",
            body: $"{currentLabel} {linkedTicket.TicketNumber}",
            isInternal: true,
            actorUserId: actorUserId,
            data: new
            {
                linkId = entity.Id,
                linkType = type.ToString(),
                relatedTicketId = linkedTicket.Id,
                relatedTicketNumber = linkedTicket.TicketNumber,
                notes = entity.Notes,
                reverseDirection = request.ReverseDirection
            });

        await AddAuditEventAsync(
            linkedTicket,
            eventType: "TicketLinkCreated",
            title: "Ticket link added",
            body: $"{linkedLabel} {currentTicket.TicketNumber}",
            isInternal: true,
            actorUserId: actorUserId,
            data: new
            {
                linkId = entity.Id,
                linkType = type.ToString(),
                relatedTicketId = currentTicket.Id,
                relatedTicketNumber = currentTicket.TicketNumber,
                notes = entity.Notes,
                reverseDirection = request.ReverseDirection
            });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "TicketLinkCreated",
            audience: "Internal",
            ticketId: ticketId,
            triggeredByUserId: actorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticketId,
                ["ticketNumber"] = currentTicket.TicketNumber,
                ["linkType"] = type.ToString(),
                ["relationshipLabel"] = currentLabel,
                ["linkedTicketId"] = linkedTicket.Id,
                ["linkedTicketNumber"] = linkedTicket.TicketNumber
            },
            cancellationToken);

        return new EhcTicketLinkDto
        {
            Id = entity.Id,
            LinkType = type,
            RelationshipLabel = currentLabel,
            LinkedTicketId = linkedTicket.Id,
            LinkedTicketNumber = linkedTicket.TicketNumber,
            LinkedSubject = linkedTicket.Subject,
            LinkedStatus = linkedTicket.Status,
            LinkedPriority = linkedTicket.Priority,
            LinkedCreatedAt = linkedTicket.CreatedAt,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy
        };
    }

    public async Task DeleteTicketLinkAsync(Guid ticketId, Guid linkId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant context is required.");
        if (ticketId == Guid.Empty || linkId == Guid.Empty)
            throw new ArgumentException("TicketId and LinkId are required.");

        var actorUserId = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;

        var linkRepo = _unitOfWork.Repository<EhcTicketLink>();
        var link = await linkRepo.GetByIdAsync(linkId);
        if (link == null || link.TenantId != tenantId)
            throw new ArgumentException("Ticket link not found.");

        if (link.TicketId != ticketId && link.RelatedTicketId != ticketId)
            throw new ArgumentException("Ticket link does not belong to this ticket.");

        link.IsDeleted = true;
        link.DeletedAt = DateTime.UtcNow;
        link.DeletedBy = _currentUserService.UserName ?? "System";
        link.UpdatedAt = DateTime.UtcNow;
        link.UpdatedBy = _currentUserService.UserName ?? "System";
        link.LastModifiedById = actorUserId;

        await linkRepo.UpdateAsync(link);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await PublishTicketTopicAsync(
            tenantId,
            activity: "TicketLinkDeleted",
            audience: "Internal",
            ticketId: ticketId,
            triggeredByUserId: actorUserId,
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticketId,
                ["linkId"] = linkId,
                ["linkType"] = link.LinkType.ToString()
            },
            cancellationToken);
    }

    public async Task<CloseEhcDuplicateTicketsResultDto> CloseDuplicateTicketsAsync(Guid ticketId, CloseEhcDuplicateTicketsRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant context is required.");
        if (ticketId == Guid.Empty)
            throw new ArgumentException("TicketId is required.");

        var targetStatus = request?.TargetStatus ?? EhcTicketStatus.Closed;
        if (targetStatus != EhcTicketStatus.Closed && targetStatus != EhcTicketStatus.Resolved)
            throw new ArgumentException("TargetStatus must be Closed or Resolved.");

        var master = await _ticketRepository.Query()
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.Id == ticketId)
            .Select(t => new { t.Id, t.TicketNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (master == null)
            throw new KeyNotFoundException("Ticket not found.");

        var linkRepo = _unitOfWork.Repository<EhcTicketLink>();
        var duplicateLinks = (await linkRepo.FindAsync(l =>
                l.TenantId == tenantId &&
                !l.IsDeleted &&
                l.RelatedTicketId == ticketId &&
                l.LinkType == EhcTicketLinkType.DuplicateOf))
            .ToList();

        var duplicateIds = duplicateLinks
            .Select(l => l.TicketId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var result = new CloseEhcDuplicateTicketsResultDto
        {
            TotalDuplicates = duplicateIds.Count
        };

        if (duplicateIds.Count == 0)
            return result;

        var dupTickets = await _ticketRepository.Query()
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && duplicateIds.Contains(t.Id))
            .Select(t => new { t.Id, t.TicketNumber, t.Status })
            .ToListAsync(cancellationToken);

        var byId = dupTickets.ToDictionary(x => x.Id, x => x);

        foreach (var id in duplicateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!byId.TryGetValue(id, out var t))
            {
                result.Failed.Add(new CloseEhcDuplicateTicketFailureDto
                {
                    TicketId = id,
                    TicketNumber = null,
                    Reason = "Ticket not found."
                });
                continue;
            }

            if (t.Status == EhcTicketStatus.Closed || (targetStatus == EhcTicketStatus.Resolved && t.Status == EhcTicketStatus.Resolved))
            {
                result.SkippedCount++;
                continue;
            }

            var baseNote = targetStatus == EhcTicketStatus.Closed
                ? $"Closed as duplicate of {master.TicketNumber}."
                : $"Resolved as duplicate of {master.TicketNumber}.";

            var extra = (request?.Notes ?? string.Empty).Trim();
            var notes = string.IsNullOrWhiteSpace(extra) ? baseNote : $"{baseNote} {extra}";

            try
            {
                await TransitionTicketAsync(id, targetStatus, notes, workflowTransitionId: null, workflowTransitionName: null, cancellationToken: cancellationToken);
                result.ClosedCount++;
            }
            catch (Exception ex)
            {
                result.Failed.Add(new CloseEhcDuplicateTicketFailureDto
                {
                    TicketId = id,
                    TicketNumber = t.TicketNumber,
                    Reason = ex.Message
                });
            }
        }

        return result;
    }

    private async Task<EhcTicketAttachmentDto> MapToAttachmentDtoAsync(EhcTicketAttachment attachment)
    {
        string? publicUrl = null;
        try
        {
            publicUrl = await _fileStorageService.GetPublicUrlAsync(attachment.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve public URL for EHC attachment {AttachmentId}", attachment.Id);
        }

        return new EhcTicketAttachmentDto
        {
            Id = attachment.Id,
            FilePath = attachment.FilePath,
            PublicUrl = publicUrl,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            IsInternal = attachment.IsInternal,
            CreatedAt = attachment.CreatedAt,
            CreatedBy = attachment.CreatedBy
        };
    }
}
