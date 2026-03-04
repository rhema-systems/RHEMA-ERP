using System.Text.Json;
using ErpSystem.Api.Services;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Ehc;

public sealed class EhcSlaMonitoringBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EhcSlaMonitoringBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);
    private const int FirstResponseWarningMinutes = 15;
    private const int ResolutionWarningMinutes = 60;

    public EhcSlaMonitoringBackgroundService(IServiceScopeFactory scopeFactory, ILogger<EhcSlaMonitoringBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EHC SLA monitoring background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EHC SLA monitoring failed");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("EHC SLA monitoring background service stopped");
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();
        await using var leader = await lockService.TryAcquireAsync(
            lockName: "bg:ehc-sla-monitoring",
            leaseDuration: TimeSpan.FromMinutes(9),
            cancellationToken: cancellationToken);

        if (leader == null)
        {
            _logger.LogDebug("Skipping EHC SLA monitoring run (lock not acquired)");
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<ErpSystem.Data.ApplicationDbContext>();
        var redis = scope.ServiceProvider.GetRequiredService<IRedisService>();
        var appEventBus = scope.ServiceProvider.GetRequiredService<IAppEventBus>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var now = DateTime.UtcNow;
        var auditEvents = new List<EhcTicketAuditEvent>();
        var queuedNotifications = new List<Notification>();
        var escalationExecutions = new List<EhcEscalationExecution>();
        var ticketMessages = new List<EhcTicketMessage>();

        var tenants = await db.Tenants
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.Status == TenantStatus.Active)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var escalationPolicies = await db.EhcEscalationPolicies
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.IsActive)
                .Include(p => p.Levels)
                .OrderByDescending(p => p.Priority)
                .ToListAsync(cancellationToken);

            // Only monitor "active" tickets
            var q = db.EhcTickets
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId && !t.IsDeleted)
                .Where(t => t.Status != Core.Enums.EhcTicketStatus.Closed)
                .Where(t => t.Status != Core.Enums.EhcTicketStatus.Resolved)
                .Where(t => t.Status != Core.Enums.EhcTicketStatus.PendingUser)
                .Where(t => t.Status != Core.Enums.EhcTicketStatus.PendingThirdParty);

            var tickets = await q
                .Select(t => new TicketSlaRow
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber,
                    Status = t.Status,
                    TicketType = t.TicketType,
                    Priority = t.Priority,
                    CategoryId = t.CategoryId,
                    SubcategoryId = t.SubcategoryId,
                    AssignedDepartmentId = t.AssignedDepartmentId,
                    AssignedToUserId = t.AssignedToUserId,
                    RequesterUserId = t.RequesterUserId,
                    FirstResponseDueAt = t.FirstResponseDueAt,
                    ResolutionDueAt = t.ResolutionDueAt,
                    FirstRespondedAt = t.FirstRespondedAt,
                    ResolvedAt = t.ResolvedAt
                })
                .ToListAsync(cancellationToken);

            var policyIds = escalationPolicies.Select(p => p.Id).ToList();
            var ticketIds = tickets.Select(t => t.Id).ToList();

            var executed = new HashSet<(Guid TicketId, Guid PolicyId, int Level)>();
            if (policyIds.Count > 0 && ticketIds.Count > 0)
            {
                var existingExecutions = await db.EhcEscalationExecutions
                    .AsNoTracking()
                    .Where(e => e.TenantId == tenantId && ticketIds.Contains(e.TicketId) && policyIds.Contains(e.PolicyId) && !e.IsDeleted)
                    .Select(e => new { e.TicketId, e.PolicyId, e.Level })
                    .ToListAsync(cancellationToken);

                foreach (var e in existingExecutions)
                {
                    executed.Add((e.TicketId, e.PolicyId, e.Level));
                }
            }

            foreach (var t in tickets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // First response warning (near-breach)
                if (t.FirstRespondedAt == null && t.FirstResponseDueAt.HasValue && t.FirstResponseDueAt.Value > now)
                {
                    var timeLeft = t.FirstResponseDueAt.Value - now;
                    if (timeLeft <= TimeSpan.FromMinutes(FirstResponseWarningMinutes))
                    {
                        var key = $"ehc:sla:first:warn:{tenantId}:{t.Id}";
                        if (!await redis.ExistsAsync(key, cancellationToken))
                        {
                            auditEvents.Add(new EhcTicketAuditEvent
                            {
                                TenantId = tenantId,
                                TicketId = t.Id,
                                EventType = "SlaWarning",
                                Title = "SLA warning: first response due soon",
                                Body = $"First response SLA is due in {Math.Max(1, (int)Math.Ceiling(timeLeft.TotalMinutes))} minute(s).",
                                IsInternal = true,
                                ActorUserId = null,
                                DataJson = JsonSerializer.Serialize(new
                                {
                                    ticketNumber = t.TicketNumber,
                                    assignedToUserId = t.AssignedToUserId,
                                    dueAt = t.FirstResponseDueAt,
                                    minutesLeft = Math.Max(1, (int)Math.Ceiling(timeLeft.TotalMinutes))
                                }),
                                CreatedAt = now,
                                CreatedBy = "System"
                            });

                            await PublishInternalAsync(
                                appEventBus,
                                tenantId,
                                t.Id,
                                triggeredByUserId: null,
                                activity: "SlaWarning",
                                data: new Dictionary<string, object>
                                {
                                    ["ticketId"] = t.Id,
                                    ["ticketNumber"] = t.TicketNumber,
                                    ["assignedToUserId"] = t.AssignedToUserId ?? Guid.Empty,
                                    ["dueAt"] = t.FirstResponseDueAt?.ToString("O") ?? string.Empty,
                                    ["minutesLeft"] = Math.Max(1, (int)Math.Ceiling(timeLeft.TotalMinutes)),
                                    ["ActionUrl"] = $"/helpdesk/tickets/{t.Id}"
                                },
                                cancellationToken: cancellationToken);

                            await redis.SetAsync(key, true, expiry: TimeSpan.FromHours(2), cancellationToken: cancellationToken);
                        }
                    }
                }

                // First response breach
                if (t.FirstRespondedAt == null && t.FirstResponseDueAt.HasValue && t.FirstResponseDueAt.Value <= now)
                {
                    var key = $"ehc:sla:first:breach:{tenantId}:{t.Id}";
                    if (!await redis.ExistsAsync(key, cancellationToken))
                    {
                        auditEvents.Add(new EhcTicketAuditEvent
                        {
                            TenantId = tenantId,
                            TicketId = t.Id,
                            EventType = "SlaBreach",
                            Title = "SLA breach: first response",
                            Body = "Ticket exceeded the first response SLA.",
                            IsInternal = true,
                            ActorUserId = null,
                            DataJson = JsonSerializer.Serialize(new
                            {
                                ticketNumber = t.TicketNumber,
                                assignedToUserId = t.AssignedToUserId,
                                dueAt = t.FirstResponseDueAt
                            }),
                            CreatedAt = now,
                            CreatedBy = "System"
                        });

                        await PublishInternalAsync(
                            appEventBus,
                            tenantId,
                            t.Id,
                            triggeredByUserId: null,
                            activity: "SlaBreach",
                            data: new Dictionary<string, object>
                            {
                                ["ticketId"] = t.Id,
                                ["ticketNumber"] = t.TicketNumber,
                                ["assignedToUserId"] = t.AssignedToUserId ?? Guid.Empty,
                                ["breachKind"] = "FirstResponse",
                                ["ActionUrl"] = $"/helpdesk/tickets/{t.Id}"
                            },
                            cancellationToken: cancellationToken);

                        // Escalation baseline: if unassigned, auto-assign to an active supervisor/manager.
                        if (!t.AssignedToUserId.HasValue || t.AssignedToUserId.Value == Guid.Empty)
                        {
                            await TryAutoAssignAsync(db, userManager, appEventBus, tenantId, t.Id, t.TicketNumber, "first response breach", cancellationToken);
                        }

                        await redis.SetAsync(key, true, expiry: TimeSpan.FromHours(6), cancellationToken: cancellationToken);
                    }
                }

                // Resolution warning (near-breach)
                if (t.ResolvedAt == null && t.ResolutionDueAt.HasValue && t.ResolutionDueAt.Value > now)
                {
                    var timeLeft = t.ResolutionDueAt.Value - now;
                    if (timeLeft <= TimeSpan.FromMinutes(ResolutionWarningMinutes))
                    {
                        var key = $"ehc:sla:res:warn:{tenantId}:{t.Id}";
                        if (!await redis.ExistsAsync(key, cancellationToken))
                        {
                            auditEvents.Add(new EhcTicketAuditEvent
                            {
                                TenantId = tenantId,
                                TicketId = t.Id,
                                EventType = "SlaWarning",
                                Title = "SLA warning: resolution due soon",
                                Body = $"Resolution SLA is due in {Math.Max(1, (int)Math.Ceiling(timeLeft.TotalMinutes))} minute(s).",
                                IsInternal = true,
                                ActorUserId = null,
                                DataJson = JsonSerializer.Serialize(new
                                {
                                    ticketNumber = t.TicketNumber,
                                    assignedToUserId = t.AssignedToUserId,
                                    dueAt = t.ResolutionDueAt,
                                    minutesLeft = Math.Max(1, (int)Math.Ceiling(timeLeft.TotalMinutes))
                                }),
                                CreatedAt = now,
                                CreatedBy = "System"
                            });

                            await PublishInternalAsync(
                                appEventBus,
                                tenantId,
                                t.Id,
                                triggeredByUserId: null,
                                activity: "SlaWarning",
                                data: new Dictionary<string, object>
                                {
                                    ["ticketId"] = t.Id,
                                    ["ticketNumber"] = t.TicketNumber,
                                    ["assignedToUserId"] = t.AssignedToUserId ?? Guid.Empty,
                                    ["dueAt"] = t.ResolutionDueAt?.ToString("O") ?? string.Empty,
                                    ["minutesLeft"] = Math.Max(1, (int)Math.Ceiling(timeLeft.TotalMinutes)),
                                    ["ActionUrl"] = $"/helpdesk/tickets/{t.Id}"
                                },
                                cancellationToken: cancellationToken);

                            await redis.SetAsync(key, true, expiry: TimeSpan.FromHours(4), cancellationToken: cancellationToken);
                        }
                    }
                }

                // Resolution breach
                if (t.ResolvedAt == null && t.ResolutionDueAt.HasValue && t.ResolutionDueAt.Value <= now)
                {
                    var key = $"ehc:sla:res:breach:{tenantId}:{t.Id}";
                    if (!await redis.ExistsAsync(key, cancellationToken))
                    {
                        auditEvents.Add(new EhcTicketAuditEvent
                        {
                            TenantId = tenantId,
                            TicketId = t.Id,
                            EventType = "SlaBreach",
                            Title = "SLA breach: resolution",
                            Body = "Ticket exceeded the resolution SLA.",
                            IsInternal = true,
                            ActorUserId = null,
                            DataJson = JsonSerializer.Serialize(new
                            {
                                ticketNumber = t.TicketNumber,
                                assignedToUserId = t.AssignedToUserId,
                                dueAt = t.ResolutionDueAt
                            }),
                            CreatedAt = now,
                            CreatedBy = "System"
                        });

                        await PublishInternalAsync(
                            appEventBus,
                            tenantId,
                            t.Id,
                            triggeredByUserId: null,
                            activity: "SlaBreach",
                            data: new Dictionary<string, object>
                            {
                                ["ticketId"] = t.Id,
                                ["ticketNumber"] = t.TicketNumber,
                                ["assignedToUserId"] = t.AssignedToUserId ?? Guid.Empty,
                                ["breachKind"] = "Resolution",
                                ["ActionUrl"] = $"/helpdesk/tickets/{t.Id}"
                            },
                            cancellationToken: cancellationToken);

                        // Escalation baseline: if unassigned, auto-assign to an active supervisor/manager.
                        if (!t.AssignedToUserId.HasValue || t.AssignedToUserId.Value == Guid.Empty)
                        {
                            await TryAutoAssignAsync(db, userManager, appEventBus, tenantId, t.Id, t.TicketNumber, "resolution breach", cancellationToken);
                        }

                        await redis.SetAsync(key, true, expiry: TimeSpan.FromHours(6), cancellationToken: cancellationToken);
                    }
                }

                if (escalationPolicies.Count > 0)
                {
                    await ApplyEscalationsAsync(
                        db,
                        userManager,
                        appEventBus,
                        tenantId,
                        now,
                        t,
                        escalationPolicies,
                        executed,
                        escalationExecutions,
                        queuedNotifications,
                        ticketMessages,
                        auditEvents,
                        cancellationToken);
                }
            }
        }

        if (ticketMessages.Count > 0) db.EhcTicketMessages.AddRange(ticketMessages);
        if (auditEvents.Count > 0) db.EhcTicketAuditEvents.AddRange(auditEvents);
        if (escalationExecutions.Count > 0) db.EhcEscalationExecutions.AddRange(escalationExecutions);
        if (queuedNotifications.Count > 0) db.Notifications.AddRange(queuedNotifications);

        if (ticketMessages.Count > 0 || auditEvents.Count > 0 || escalationExecutions.Count > 0 || queuedNotifications.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class TicketSlaRow
    {
        public Guid Id { get; init; }
        public string TicketNumber { get; init; } = string.Empty;
        public Core.Enums.EhcTicketStatus Status { get; init; }
        public Core.Enums.EhcTicketType TicketType { get; init; }
        public Core.Enums.EhcTicketPriority Priority { get; init; }
        public Guid? CategoryId { get; init; }
        public Guid? SubcategoryId { get; init; }
        public Guid? AssignedDepartmentId { get; init; }
        public Guid? AssignedToUserId { get; init; }
        public Guid RequesterUserId { get; init; }
        public DateTime? FirstResponseDueAt { get; init; }
        public DateTime? ResolutionDueAt { get; init; }
        public DateTime? FirstRespondedAt { get; init; }
        public DateTime? ResolvedAt { get; init; }
    }

    private static async Task ApplyEscalationsAsync(
        ErpSystem.Data.ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAppEventBus appEventBus,
        Guid tenantId,
        DateTime now,
        TicketSlaRow ticket,
        List<EhcEscalationPolicy> policies,
        HashSet<(Guid TicketId, Guid PolicyId, int Level)> executed,
        List<EhcEscalationExecution> executions,
        List<Notification> notifications,
        List<EhcTicketMessage> messages,
        List<EhcTicketAuditEvent> auditEvents,
        CancellationToken cancellationToken)
    {
        // Only evaluate policies against tickets with SLA timers.
        foreach (var policy in policies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (policy.Levels == null || policy.Levels.Count == 0) continue;

            if (policy.TicketType.HasValue && policy.TicketType.Value != ticket.TicketType) continue;
            if (policy.TicketPriority.HasValue && policy.TicketPriority.Value != ticket.Priority) continue;
            if (policy.CategoryId.HasValue && policy.CategoryId.Value != ticket.CategoryId) continue;
            if (policy.SubcategoryId.HasValue && policy.SubcategoryId.Value != ticket.SubcategoryId) continue;
            if (policy.DepartmentId.HasValue && policy.DepartmentId.Value != ticket.AssignedDepartmentId) continue;

            if (!TryGetEscalationEligibility(policy, now, ticket, out var eligibleAtUtc))
            {
                continue;
            }

            var orderedLevels = policy.Levels
                .Where(l => !l.IsDeleted)
                .OrderBy(l => l.Level)
                .ToList();

            if (orderedLevels.Count == 0) continue;

            foreach (var level in orderedLevels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var executeAt = eligibleAtUtc.AddMinutes(Math.Max(0, level.DelayMinutes));
                if (now < executeAt) continue;

                var key = (ticket.Id, policy.Id, level.Level);
                if (executed.Contains(key)) continue;

                executed.Add(key);

                var reason = $"Escalation '{policy.Name}' level {level.Level} for {policy.Trigger}.";

                executions.Add(new EhcEscalationExecution
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = ticket.Id,
                    PolicyId = policy.Id,
                    Level = level.Level,
                    ExecutedAtUtc = now,
                    Reason = reason,
                    CreatedAt = now,
                    CreatedBy = "System"
                });

                // Resolve recipients
                var userIds = new HashSet<Guid>();
                if (level.NotifyAssignedAgent && ticket.AssignedToUserId.HasValue && ticket.AssignedToUserId.Value != Guid.Empty)
                {
                    userIds.Add(ticket.AssignedToUserId.Value);
                }

                if (!string.IsNullOrWhiteSpace(level.NotifyRolesJson))
                {
                    foreach (var role in DeserializeRoles(level.NotifyRolesJson))
                    {
                        try
                        {
                            var usersInRole = await userManager.GetUsersInRoleAsync(role);
                            foreach (var u in usersInRole.Where(u => u.IsActive && u.TenantId == tenantId))
                            {
                                userIds.Add(u.Id);
                            }
                        }
                        catch
                        {
                            // best-effort
                        }
                    }
                }

                if (level.NotifyUserId.HasValue && level.NotifyUserId.Value != Guid.Empty)
                {
                    userIds.Add(level.NotifyUserId.Value);
                }

                // Optional reassignment
                Guid? reassignedTo = null;
                if (level.ReassignToUserId.HasValue && level.ReassignToUserId.Value != Guid.Empty)
                {
                    reassignedTo = level.ReassignToUserId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(level.ReassignToRole))
                {
                    try
                    {
                        var usersInRole = await userManager.GetUsersInRoleAsync(level.ReassignToRole.Trim());
                        var pick = usersInRole.FirstOrDefault(u => u.IsActive && u.TenantId == tenantId);
                        if (pick != null) reassignedTo = pick.Id;
                    }
                    catch
                    {
                        // best-effort
                    }
                }

                if (reassignedTo.HasValue && reassignedTo.Value != Guid.Empty && (!ticket.AssignedToUserId.HasValue || ticket.AssignedToUserId.Value != reassignedTo.Value))
                {
                    var trackedTicket = await db.EhcTickets
                        .FirstOrDefaultAsync(x => x.Id == ticket.Id && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

                    if (trackedTicket != null)
                    {
                        trackedTicket.AssignedToUserId = reassignedTo.Value;
                        trackedTicket.UpdatedAt = now;
                        trackedTicket.UpdatedBy = "System";
                    }

                    userIds.Add(reassignedTo.Value);
                }

                if (level.AddInternalComment)
                {
                    messages.Add(new EhcTicketMessage
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        TicketId = ticket.Id,
                        Body = $"{reason}",
                        IsInternal = true,
                        AuthorUserId = null,
                        CreatedAt = now,
                        CreatedBy = "System"
                    });
                }

                auditEvents.Add(new EhcTicketAuditEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = ticket.Id,
                    EventType = "SlaEscalation",
                    Title = "Ticket escalated",
                    Body = reason,
                    IsInternal = true,
                    ActorUserId = null,
                    DataJson = JsonSerializer.Serialize(new
                    {
                        ticketNumber = ticket.TicketNumber,
                        trigger = policy.Trigger.ToString(),
                        policyId = policy.Id,
                        policyName = policy.Name,
                        level = level.Level,
                        notifiedUserIds = userIds.ToArray(),
                        reassignedToUserId = reassignedTo
                    }),
                    CreatedAt = now,
                    CreatedBy = "System"
                });

                var title = $"Escalation: {policy.Name} (L{level.Level})";
                var body = $"Ticket {(string)ticket.TicketNumber} requires attention. Trigger: {policy.Trigger}.";

                foreach (var uid in userIds)
                {
                    notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        RecipientId = uid,
                        NotificationType = "EhcEscalation",
                        Title = title,
                        Message = body,
                        Priority = "High",
                        Status = "Pending",
                        IsRead = false,
                        ScheduledFor = now,
                        SentAt = null,
                        AttemptCount = 0,
                        LastError = null,
                        DeliveryMethods = "InApp",
                        EntityType = "EhcTicket",
                        EntityId = ticket.Id,
                        ActionUrl = $"/helpdesk/tickets/{ticket.Id}",
                        AdditionalData = JsonSerializer.Serialize(new
                        {
                            ticketId = ticket.Id,
                            ticketNumber = ticket.TicketNumber,
                            policyId = policy.Id,
                            policyName = policy.Name,
                            level = level.Level,
                            trigger = policy.Trigger.ToString()
                        })
                    });
                }

                await appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = tenantId,
                    EntityType = "EhcTicket",
                    EntityId = ticket.Id,
                    Activity = "SlaEscalation",
                    Audience = "Internal",
                    TriggeredByUserId = null,
                    Data = new Dictionary<string, object>
                    {
                        ["ticketId"] = ticket.Id,
                        ["ticketNumber"] = ticket.TicketNumber,
                        ["policyId"] = policy.Id,
                        ["policyName"] = policy.Name,
                        ["level"] = level.Level,
                        ["trigger"] = policy.Trigger.ToString(),
                        ["TargetUserIds"] = userIds.ToArray(),
                        ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
                    }
                }, cancellationToken);
            }
        }
    }

    private static bool TryGetEscalationEligibility(EhcEscalationPolicy policy, DateTime now, TicketSlaRow ticket, out DateTime eligibleAtUtc)
    {
        eligibleAtUtc = default;

        var firstDue = ticket.FirstResponseDueAt;
        var resDue = ticket.ResolutionDueAt;
        var firstRespondedAt = ticket.FirstRespondedAt;
        var resolvedAt = ticket.ResolvedAt;

        switch (policy.Trigger)
        {
            case EhcEscalationTrigger.FirstResponseDueSoon:
                if (firstRespondedAt != null) return false;
                if (!firstDue.HasValue) return false;
                eligibleAtUtc = firstDue.Value.AddMinutes(-Math.Max(1, policy.DueSoonMinutes));
                return now >= eligibleAtUtc && now < firstDue.Value;

            case EhcEscalationTrigger.FirstResponseBreached:
                if (firstRespondedAt != null) return false;
                if (!firstDue.HasValue) return false;
                eligibleAtUtc = firstDue.Value;
                return now >= firstDue.Value;

            case EhcEscalationTrigger.ResolutionDueSoon:
                if (resolvedAt != null) return false;
                if (!resDue.HasValue) return false;
                eligibleAtUtc = resDue.Value.AddMinutes(-Math.Max(1, policy.DueSoonMinutes));
                return now >= eligibleAtUtc && now < resDue.Value;

            case EhcEscalationTrigger.ResolutionBreached:
                if (resolvedAt != null) return false;
                if (!resDue.HasValue) return false;
                eligibleAtUtc = resDue.Value;
                return now >= resDue.Value;

            default:
                return false;
        }
    }

    private static string[] DeserializeRoles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            var roles = JsonSerializer.Deserialize<string[]>(json);
            return roles?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                   ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static async Task TryAutoAssignAsync(
        ErpSystem.Data.ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAppEventBus appEventBus,
        Guid tenantId,
        Guid ticketId,
        string ticketNumber,
        string reason,
        CancellationToken cancellationToken)
    {
        var ticket = await db.EhcTickets
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);

        if (ticket == null)
        {
            return;
        }

        if (ticket.AssignedToUserId.HasValue && ticket.AssignedToUserId.Value != Guid.Empty)
        {
            return;
        }

        // Prefer supervisors, then managers, then any active agent.
        ApplicationUser? assignee = null;

        var supervisors = await userManager.GetUsersInRoleAsync(Constants.Roles.HelpdeskSupervisor);
        assignee = supervisors.FirstOrDefault(u => u.TenantId == tenantId && u.IsActive);

        if (assignee == null)
        {
            var managers = await userManager.GetUsersInRoleAsync(Constants.Roles.HelpdeskManager);
            assignee = managers.FirstOrDefault(u => u.TenantId == tenantId && u.IsActive);
        }

        if (assignee == null)
        {
            var agents = await userManager.GetUsersInRoleAsync(Constants.Roles.HelpdeskAgent);
            assignee = agents.FirstOrDefault(u => u.TenantId == tenantId && u.IsActive);
        }

        if (assignee == null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        ticket.AssignedToUserId = assignee.Id;
        ticket.UpdatedAt = now;
        ticket.UpdatedBy = "System";

        db.EhcTicketMessages.Add(new EhcTicketMessage
        {
            TenantId = tenantId,
            TicketId = ticket.Id,
            Body = $"Auto-assigned to {($"{assignee.FirstName} {assignee.LastName}").Trim()} due to SLA {reason}.",
            IsInternal = true,
            AuthorUserId = null,
            CreatedAt = now,
            CreatedBy = "System"
        });

        db.EhcTicketAuditEvents.Add(new EhcTicketAuditEvent
        {
            TenantId = tenantId,
            TicketId = ticket.Id,
            EventType = "SlaEscalation",
            Title = "Ticket auto-assigned",
            Body = $"Auto-assigned due to SLA {reason}.",
            IsInternal = true,
            ActorUserId = null,
            DataJson = JsonSerializer.Serialize(new
            {
                ticketNumber,
                assignedToUserId = assignee.Id,
                reason
            }),
            CreatedAt = now,
            CreatedBy = "System"
        });

        await db.SaveChangesAsync(cancellationToken);

        await PublishInternalAsync(
            appEventBus,
            tenantId,
            ticket.Id,
            triggeredByUserId: null,
            activity: "SlaEscalation",
            data: new Dictionary<string, object>
            {
                ["ticketId"] = ticket.Id,
                ["ticketNumber"] = ticketNumber,
                ["assignedToUserId"] = assignee.Id,
                ["reason"] = reason,
                ["ActionUrl"] = $"/helpdesk/tickets/{ticket.Id}"
            },
            cancellationToken: cancellationToken);
    }

    private static Task PublishInternalAsync(
        IAppEventBus appEventBus,
        Guid tenantId,
        Guid ticketId,
        Guid? triggeredByUserId,
        string activity,
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) return Task.CompletedTask;
        return appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = tenantId,
            EntityType = "EhcTicket",
            EntityId = ticketId,
            Activity = activity,
            Audience = "Internal",
            TriggeredByUserId = triggeredByUserId,
            Data = data ?? new Dictionary<string, object>()
        }, cancellationToken);
    }
}
