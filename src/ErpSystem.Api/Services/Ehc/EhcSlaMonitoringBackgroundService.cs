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

        var tenants = await db.Tenants
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.Status == TenantStatus.Active)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Only monitor "active" tickets
            var q = db.EhcTickets
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId && !t.IsDeleted)
                .Where(t => t.Status != Core.Enums.EhcTicketStatus.Closed)
                .Where(t => t.Status != Core.Enums.EhcTicketStatus.Resolved);

            var tickets = await q
                .Select(t => new
                {
                    t.Id,
                    t.TicketNumber,
                    t.Status,
                    t.AssignedToUserId,
                    t.RequesterUserId,
                    t.FirstResponseDueAt,
                    t.ResolutionDueAt,
                    t.FirstRespondedAt,
                    t.ResolvedAt
                })
                .ToListAsync(cancellationToken);

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
            }
        }

        if (auditEvents.Count > 0)
        {
            db.EhcTicketAuditEvents.AddRange(auditEvents);
            await db.SaveChangesAsync(cancellationToken);
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
