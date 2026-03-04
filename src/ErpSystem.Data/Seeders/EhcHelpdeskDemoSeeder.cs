using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Ehc.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public sealed class EhcHelpdeskDemoSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger _logger;

    public EhcHelpdeskDemoSeeder(ApplicationDbContext db, ILogger logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return;

        var now = DateTime.UtcNow;
        const string demoTag = "[DEMO]";

        var agent = await FindUserAsync(tenantId, "helpdesk.agent", cancellationToken);
        var supervisor = await FindUserAsync(tenantId, "helpdesk.supervisor", cancellationToken);
        var manager = await FindUserAsync(tenantId, "helpdesk.manager", cancellationToken);
        var employee = await FindUserAsync(tenantId, "employee", cancellationToken);
        var external = await FindUserAsync(tenantId, "external", cancellationToken);

        if (external == null)
        {
            _logger.LogWarning("Skipping EHC helpdesk demo seeding: external user not found for tenant {TenantId}", tenantId);
            return;
        }

        var techCategory = await _db.EhcTicketCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Code == "TECH", cancellationToken);

        var complaintsCategory = await _db.EhcTicketCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Code == "COMPLAINTS", cancellationToken);

        var generalCategory = await _db.EhcTicketCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Code == "GENERAL", cancellationToken);

        var loginSub = await _db.EhcTicketCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Code == "LOGIN", cancellationToken);

        var bugSub = await _db.EhcTicketCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Code == "BUG", cancellationToken);

        var serviceSub = await _db.EhcTicketCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.Code == "SERVICE", cancellationToken);

        await EnsureRootCauseCodesAsync(tenantId, now, cancellationToken);
        await EnsureCannedResponsesAsync(tenantId, now, techCategory?.Id, cancellationToken);
        await EnsureAgentReplyProfileAsync(tenantId, now, agent?.Id, cancellationToken);

        var calendarJson = await EnsureBusinessHoursSlaTemplateAsync(tenantId, now, techCategory?.Id, cancellationToken);

        await EnsureTicketsAsync(
            tenantId,
            now,
            calendarJson,
            generalCategory?.Id,
            techCategory?.Id,
            complaintsCategory?.Id,
            loginSub?.Id,
            bugSub?.Id,
            serviceSub?.Id,
            agent?.Id,
            supervisor?.Id,
            manager?.Id,
            employee?.Id,
            external.Id,
            demoTag,
            cancellationToken);

        await EnsureTicketLinksAndProblemsAsync(tenantId, now, agent?.Id, manager?.Id, demoTag, cancellationToken);
        await EnsureServiceCatalogDemoAsync(tenantId, now, manager?.Id, external.Id, demoTag, cancellationToken);
        await EnsureInboundChannelsDemoAsync(tenantId, now, demoTag, cancellationToken);
        await EnsureComplianceDemoAsync(tenantId, now, techCategory?.Id, manager?.Id, demoTag, cancellationToken);
    }

    private async Task<ApplicationUser?> FindUserAsync(Guid tenantId, string userName, CancellationToken cancellationToken)
    {
        var normalized = userName.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UserName == normalized, cancellationToken);
    }

    private async Task EnsureRootCauseCodesAsync(Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var items = new[]
        {
            new { Code = "NETWORK", Name = "Network / Connectivity", Description = "Connectivity, ISP, DNS, Wi-Fi, VPN issues" },
            new { Code = "USER_ERROR", Name = "User Error / Training", Description = "User error or training needed" },
            new { Code = "SYSTEM_BUG", Name = "System Bug", Description = "Confirmed software defect" },
            new { Code = "THIRDPARTY", Name = "Third-Party Dependency", Description = "Issue depends on external vendor/service" },
            new { Code = "UNKNOWN", Name = "Unknown / Investigating", Description = "Root cause not yet known" }
        };

        var existing = await _db.EhcRootCauseCodes
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .ToDictionaryAsync(r => r.Code, r => r, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var item in items)
        {
            if (existing.ContainsKey(item.Code)) continue;
            _db.EhcRootCauseCodes.Add(new EhcRootCauseCode
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = item.Code,
                Name = item.Name,
                Description = item.Description,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "System"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCannedResponsesAsync(Guid tenantId, DateTime now, Guid? techCategoryId, CancellationToken cancellationToken)
    {
        var items = new[]
        {
            new
            {
                Code = "ACKNOWLEDGE",
                Title = "Acknowledgement",
                Body = "Hi {RequesterName},\n\nThanks for reaching out. We’ve received ticket {TicketNumber} and will update you shortly.\n\nRegards,\n{AgentSignature}"
            },
            new
            {
                Code = "NEED_INFO",
                Title = "Need More Information",
                Body = "Hi {RequesterName},\n\nTo help us resolve ticket {TicketNumber}, please share:\n- Steps to reproduce\n- Screenshot (if possible)\n- When you last saw it working\n\nRegards,\n{AgentSignature}"
            },
            new
            {
                Code = "RESOLVED",
                Title = "Resolution Summary",
                Body = "Hi {RequesterName},\n\nTicket {TicketNumber} has been resolved.\n\nSummary:\n- {ResolutionSummary}\n\nIf the issue persists, reply to this message.\n\nRegards,\n{AgentSignature}"
            }
        };

        var existing = await _db.EhcCannedResponses
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .ToDictionaryAsync(r => r.Code, r => r, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var item in items)
        {
            if (existing.ContainsKey(item.Code)) continue;
            _db.EhcCannedResponses.Add(new EhcCannedResponse
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = item.Code,
                Title = item.Title,
                Body = item.Body,
                IsActive = true,
                AppliesToType = EhcTicketType.Helpdesk,
                CategoryId = techCategoryId,
                CreatedAt = now,
                CreatedBy = "System"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAgentReplyProfileAsync(Guid tenantId, DateTime now, Guid? agentUserId, CancellationToken cancellationToken)
    {
        if (!agentUserId.HasValue || agentUserId.Value == Guid.Empty) return;

        var existing = await _db.EhcAgentReplyProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == agentUserId.Value, cancellationToken);

        if (existing != null)
        {
            if (string.IsNullOrWhiteSpace(existing.Signature))
            {
                existing.Signature = "Helpdesk Agent\nDefault Company";
                existing.UpdatedAt = now;
                existing.UpdatedBy = "System";
                await _db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        _db.EhcAgentReplyProfiles.Add(new EhcAgentReplyProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = agentUserId.Value,
            Signature = "Helpdesk Agent\nDefault Company",
            IsSignatureEnabled = true,
            AppendSignatureToReplies = true,
            CreatedAt = now,
            CreatedBy = "System"
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> EnsureBusinessHoursSlaTemplateAsync(Guid tenantId, DateTime now, Guid? techCategoryId, CancellationToken cancellationToken)
    {
        var raw = JsonSerializer.Serialize(new EhcSlaCalendarConfiguration
        {
            TimeZoneId = "Eastern Standard Time",
            WorkdayStart = "09:00",
            WorkdayEnd = "17:00",
            WorkingDays = new[] { 1, 2, 3, 4, 5 },
            Holidays = new[] { "2026-01-01", "2026-12-25" }
        });

        if (!EhcSlaCalendarConfiguration.TryParseAndNormalize(raw, out var normalized, out var error))
        {
            _logger.LogWarning("Failed to normalize demo SLA calendar configuration: {Error}", error);
            normalized = null;
        }

        const string slaName = "Business Hours - Medium";
        var existing = await _db.EhcSlaTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Name == slaName, cancellationToken);

        if (existing == null)
        {
            _db.EhcSlaTemplates.Add(new EhcSlaTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = slaName,
                IsActive = true,
                TicketType = EhcTicketType.Helpdesk,
                Priority = EhcTicketPriority.Medium,
                CategoryId = techCategoryId,
                FirstResponseMinutes = 90,
                ResolutionMinutes = 1440,
                CalendarConfigurationJson = normalized,
                CreatedAt = now,
                CreatedBy = "System"
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(existing.CalendarConfigurationJson) && !string.IsNullOrWhiteSpace(normalized))
        {
            existing.CalendarConfigurationJson = normalized;
            existing.UpdatedAt = now;
            existing.UpdatedBy = "System";
            await _db.SaveChangesAsync(cancellationToken);
        }

        return normalized;
    }

    private async Task EnsureTicketsAsync(
        Guid tenantId,
        DateTime now,
        string? calendarJson,
        Guid? generalCategoryId,
        Guid? techCategoryId,
        Guid? complaintsCategoryId,
        Guid? loginSubId,
        Guid? bugSubId,
        Guid? serviceSubId,
        Guid? agentUserId,
        Guid? supervisorUserId,
        Guid? managerUserId,
        Guid? employeeUserId,
        Guid externalUserId,
        string demoTag,
        CancellationToken cancellationToken)
    {
        var slas = await _db.EhcSlaTemplates
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        EhcSlaTemplate? SlaFor(EhcTicketPriority p)
        {
            var match = slas.FirstOrDefault(x =>
                x.Priority == p &&
                (x.TicketType == null || x.TicketType == EhcTicketType.Helpdesk));
            return match ?? slas.FirstOrDefault(x => x.Priority == p) ?? slas.FirstOrDefault();
        }

        var existingTicketNumbersList = await _db.EhcTickets
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && t.TicketNumber.StartsWith("TKT-DEMO-"))
            .Select(t => t.TicketNumber)
            .ToListAsync(cancellationToken);

        var existingTicketNumbers = new HashSet<string>(existingTicketNumbersList, StringComparer.OrdinalIgnoreCase);

        var random = new Random(12345);
        const int ticketCount = 30;

        for (var i = 1; i <= ticketCount; i++)
        {
            var ticketNumber = $"TKT-DEMO-{i:0000}";
            if (existingTicketNumbers.Contains(ticketNumber)) continue;

            var createdAt = now.AddDays(-random.Next(0, 45)).AddMinutes(random.Next(0, 1440));
            createdAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);

            var priority = (EhcTicketPriority)(1 + (i % 4));
            var type = i % 9 == 0 ? EhcTicketType.Complaint : EhcTicketType.Helpdesk;
            var source = (EhcTicketSource)(1 + (i % 7));

            var status = (i % 10) switch
            {
                0 => EhcTicketStatus.Closed,
                1 => EhcTicketStatus.Resolved,
                2 => EhcTicketStatus.PendingUser,
                3 => EhcTicketStatus.PendingThirdParty,
                4 => EhcTicketStatus.InProgress,
                5 => EhcTicketStatus.Acknowledged,
                _ => EhcTicketStatus.New
            };

            var requesterId = source == EhcTicketSource.Internal && employeeUserId.HasValue
                ? employeeUserId.Value
                : externalUserId;

            var assignedTo = status == EhcTicketStatus.New ? null : agentUserId;

            var categoryId = type == EhcTicketType.Complaint
                ? complaintsCategoryId
                : (techCategoryId ?? generalCategoryId);

            var subcategoryId = type == EhcTicketType.Complaint
                ? serviceSubId
                : (i % 2 == 0 ? loginSubId : bugSubId);

            var sla = SlaFor(priority);
            var appliedCalendar = priority == EhcTicketPriority.Medium ? calendarJson : sla?.CalendarConfigurationJson;
            var firstMins = sla?.FirstResponseMinutes ?? 120;
            var resMins = sla?.ResolutionMinutes ?? 2880;

            var firstDue = EhcSlaTimeCalculator.CalculateDueAtUtc(createdAt, Math.Max(1, firstMins), appliedCalendar);
            var resDue = EhcSlaTimeCalculator.CalculateDueAtUtc(createdAt, Math.Max(1, resMins), appliedCalendar);

            DateTime? firstRespondedAt = null;
            DateTime? resolvedAt = null;
            DateTime? closedAt = null;

            var shouldHaveFirstResponse = status is
                EhcTicketStatus.Acknowledged or
                EhcTicketStatus.InProgress or
                EhcTicketStatus.PendingUser or
                EhcTicketStatus.PendingThirdParty or
                EhcTicketStatus.Resolved or
                EhcTicketStatus.Closed;

            if (shouldHaveFirstResponse)
            {
                firstRespondedAt = i % 6 == 0 ? firstDue.AddMinutes(30) : firstDue.AddMinutes(-random.Next(5, 45));
            }

            if (status is EhcTicketStatus.Resolved or EhcTicketStatus.Closed)
            {
                resolvedAt = i % 7 == 0 ? resDue.AddMinutes(120) : resDue.AddMinutes(-random.Next(10, 180));
            }

            if (status == EhcTicketStatus.Closed && resolvedAt.HasValue)
            {
                closedAt = resolvedAt.Value.AddHours(random.Next(1, 48));
            }

            var ticket = new EhcTicket
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TicketNumber = ticketNumber,
                TicketType = type,
                CategoryId = categoryId,
                SubcategoryId = subcategoryId,
                Priority = priority,
                Source = source,
                Subject = $"{demoTag} {(type == EhcTicketType.Complaint ? "Complaint" : "Helpdesk")} - {priority} #{i}",
                Description = $"{demoTag} Demo ticket seeded for testing. Source={source}, Priority={priority}, Status={status}.",
                RequesterUserId = requesterId,
                AssignedToUserId = assignedTo,
                Status = status,
                FirstResponseDueAt = firstDue,
                ResolutionDueAt = resDue,
                FirstRespondedAt = firstRespondedAt,
                ResolvedAt = resolvedAt,
                ClosedAt = closedAt,
                AppliedSlaTemplateId = sla?.Id,
                AppliedFirstResponseMinutes = firstMins,
                AppliedResolutionMinutes = resMins,
                AppliedSlaCalendarConfigurationJson = appliedCalendar,
                CreatedAt = createdAt,
                CreatedBy = "System"
            };

            _db.EhcTickets.Add(ticket);

            _db.EhcTicketMessages.AddRange(
                new EhcTicketMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = ticket.Id,
                    Body = $"{demoTag} Requester message: Please assist with this issue.\n\nTicket: {ticket.TicketNumber}",
                    IsInternal = false,
                    AuthorUserId = requesterId,
                    CreatedAt = createdAt.AddMinutes(2),
                    CreatedBy = "System"
                },
                new EhcTicketMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = ticket.Id,
                    Body = $"{demoTag} Agent note: Investigating and will update shortly.",
                    IsInternal = true,
                    AuthorUserId = supervisorUserId ?? assignedTo ?? managerUserId,
                    CreatedAt = createdAt.AddMinutes(15),
                    CreatedBy = "System"
                });

            if (assignedTo.HasValue)
            {
                _db.EhcTicketMessages.Add(new EhcTicketMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = ticket.Id,
                    Body = $"{demoTag} Agent reply: Thanks, we’re on it. Current status: {status}.",
                    IsInternal = false,
                    AuthorUserId = assignedTo.Value,
                    CreatedAt = createdAt.AddMinutes(25),
                    CreatedBy = "System"
                });
            }

            _db.EhcTicketStatusHistories.Add(new EhcTicketStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TicketId = ticket.Id,
                FromStatus = null,
                ToStatus = EhcTicketStatus.New,
                ChangedByUserId = requesterId,
                Notes = $"{demoTag} Created",
                CreatedAt = createdAt,
                CreatedBy = "System"
            });

            if (status != EhcTicketStatus.New)
            {
                _db.EhcTicketStatusHistories.Add(new EhcTicketStatusHistory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = ticket.Id,
                    FromStatus = EhcTicketStatus.New,
                    ToStatus = status,
                    ChangedByUserId = assignedTo ?? supervisorUserId ?? managerUserId,
                    Notes = $"{demoTag} Auto-progressed for demo",
                    CreatedAt = createdAt.AddHours(2),
                    CreatedBy = "System"
                });
            }

            if (supervisorUserId.HasValue)
            {
                _db.EhcTicketWatchers.Add(new EhcTicketWatcher
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = ticket.Id,
                    UserId = supervisorUserId.Value,
                    CreatedAt = createdAt.AddMinutes(5),
                    CreatedBy = "System"
                });
            }

            if (status is EhcTicketStatus.Resolved or EhcTicketStatus.Closed)
            {
                _db.EhcTicketFeedbacks.Add(new EhcTicketFeedback
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = ticket.Id,
                    SubmittedByUserId = requesterId,
                    Rating = 1 + (i % 5),
                    Comment = $"{demoTag} Demo feedback comment for {ticket.TicketNumber}.",
                    CreatedAt = (resolvedAt ?? createdAt).AddHours(6),
                    CreatedBy = "System"
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var rootCause = await _db.EhcRootCauseCodes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && !r.IsDeleted && r.Code == "SYSTEM_BUG", cancellationToken);

        if (rootCause == null) return;

        var needsRca = await _db.EhcTickets
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TicketNumber.StartsWith("TKT-DEMO-") && t.RootCauseId == null)
            .OrderBy(t => t.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var t in needsRca)
        {
            t.RootCauseId = rootCause.Id;
            t.RootCauseDetails = $"{demoTag} Root cause analysis: intermittent bug triggered under load.";
            t.ResolutionSummary = $"{demoTag} Applied patch and restarted service.";
            t.UpdatedAt = now;
            t.UpdatedBy = "System";
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTicketLinksAndProblemsAsync(
        Guid tenantId,
        DateTime now,
        Guid? agentUserId,
        Guid? managerUserId,
        string demoTag,
        CancellationToken cancellationToken)
    {
        var demoTickets = await _db.EhcTickets
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TicketNumber.StartsWith("TKT-DEMO-"))
            .OrderBy(t => t.TicketNumber)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (demoTickets.Count < 4) return;

        var t1 = demoTickets[0];
        var t2 = demoTickets[1];
        var t3 = demoTickets[2];

        await EnsureTicketLinkAsync(
            tenantId,
            now,
            t2.Id,
            t1.Id,
            EhcTicketLinkType.DuplicateOf,
            $"{demoTag} Duplicate of {t1.TicketNumber}",
            cancellationToken);

        await EnsureTicketLinkAsync(
            tenantId,
            now,
            t3.Id,
            t1.Id,
            EhcTicketLinkType.ParentOf,
            $"{demoTag} Parent ticket {t1.TicketNumber}",
            cancellationToken);

        var problems = new[]
        {
            new
            {
                Number = "PRB-DEMO-0001",
                Title = $"{demoTag} Intermittent login failures",
                Description = $"{demoTag} Multiple incidents related to login failures.",
                Priority = EhcTicketPriority.High
            },
            new
            {
                Number = "PRB-DEMO-0002",
                Title = $"{demoTag} Slow application performance",
                Description = $"{demoTag} Recurring reports of slowness during peak hours.",
                Priority = EhcTicketPriority.Medium
            }
        };

        foreach (var p in problems)
        {
            var existing = await _db.EhcProblems
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProblemNumber == p.Number, cancellationToken);

            if (existing != null) continue;

            var problem = new EhcProblem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProblemNumber = p.Number,
                Title = p.Title,
                Description = p.Description,
                Priority = p.Priority,
                Status = EhcProblemStatus.InProgress,
                OwnerUserId = managerUserId ?? agentUserId,
                CreatedAt = now.AddDays(-10),
                CreatedBy = "System"
            };

            _db.EhcProblems.Add(problem);

            foreach (var ticket in demoTickets.Take(5))
            {
                _db.EhcProblemTicketLinks.Add(new EhcProblemTicketLink
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProblemId = problem.Id,
                    TicketId = ticket.Id,
                    Notes = $"{demoTag} Linked for analysis",
                    CreatedAt = now.AddDays(-9),
                    CreatedBy = "System"
                });
            }

            _db.EhcCapaTasks.AddRange(
                new EhcCapaTask
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProblemId = problem.Id,
                    Title = $"{demoTag} Create monitoring alert",
                    Description = $"{demoTag} Add alert for spike in login errors.",
                    Status = EhcCapaTaskStatus.InProgress,
                    AssignedToUserId = agentUserId,
                    DueAt = now.AddDays(7),
                    CreatedAt = now.AddDays(-8),
                    CreatedBy = "System"
                },
                new EhcCapaTask
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProblemId = problem.Id,
                    Title = $"{demoTag} Publish KB article",
                    Description = $"{demoTag} Document workaround and resolution steps.",
                    Status = EhcCapaTaskStatus.Open,
                    AssignedToUserId = managerUserId ?? agentUserId,
                    DueAt = now.AddDays(14),
                    CreatedAt = now.AddDays(-8),
                    CreatedBy = "System"
                });

            _db.EhcProblemAuditEvents.Add(new EhcProblemAuditEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProblemId = problem.Id,
                EventType = "Created",
                Title = $"{demoTag} Problem created",
                Body = $"{demoTag} Seeded problem record for testing dashboards.",
                ActorUserId = managerUserId ?? agentUserId,
                CreatedAt = now.AddDays(-10),
                CreatedBy = "System"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTicketLinkAsync(
        Guid tenantId,
        DateTime now,
        Guid ticketId,
        Guid relatedTicketId,
        EhcTicketLinkType linkType,
        string notes,
        CancellationToken cancellationToken)
    {
        var exists = await _db.EhcTicketLinks
            .IgnoreQueryFilters()
            .AnyAsync(l =>
                l.TenantId == tenantId &&
                l.TicketId == ticketId &&
                l.RelatedTicketId == relatedTicketId &&
                l.LinkType == linkType,
                cancellationToken);

        if (exists) return;

        _db.EhcTicketLinks.Add(new EhcTicketLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TicketId = ticketId,
            RelatedTicketId = relatedTicketId,
            LinkType = linkType,
            Notes = notes,
            CreatedAt = now,
            CreatedBy = "System"
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureServiceCatalogDemoAsync(
        Guid tenantId,
        DateTime now,
        Guid? managerUserId,
        Guid requesterUserId,
        string demoTag,
        CancellationToken cancellationToken)
    {
        var types = new[]
        {
            new
            {
                Code = "ACCESS_REQUEST",
                Name = "Access Request",
                Description = "Request access to an application or system",
                FormDefinitionJson = JsonSerializer.Serialize(new
                {
                    fields = new object[]
                    {
                        new { key = "system", type = "text", required = true, label = "System" },
                        new { key = "accessLevel", type = "select", required = true, label = "Access Level", options = new[] { "Read", "Write", "Admin" } },
                        new { key = "justification", type = "textarea", required = true, label = "Justification" }
                    }
                })
            },
            new
            {
                Code = "EQUIPMENT",
                Name = "Equipment Request",
                Description = "Request a laptop, monitor, peripherals, etc.",
                FormDefinitionJson = JsonSerializer.Serialize(new
                {
                    fields = new object[]
                    {
                        new { key = "item", type = "select", required = true, label = "Item", options = new[] { "Laptop", "Monitor", "Keyboard", "Mouse" } },
                        new { key = "quantity", type = "number", required = true, label = "Quantity" },
                        new { key = "neededBy", type = "date", required = false, label = "Needed By" }
                    }
                })
            },
            new
            {
                Code = "SOFTWARE",
                Name = "Software Install",
                Description = "Request installation of approved software",
                FormDefinitionJson = JsonSerializer.Serialize(new
                {
                    fields = new object[]
                    {
                        new { key = "softwareName", type = "text", required = true, label = "Software Name" },
                        new { key = "version", type = "text", required = false, label = "Version" },
                        new { key = "businessReason", type = "textarea", required = true, label = "Business Reason" }
                    }
                })
            }
        };

        var existingTypes = await _db.EhcServiceRequestTypes
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId)
            .ToDictionaryAsync(t => t.Code, t => t, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var t in types)
        {
            if (existingTypes.ContainsKey(t.Code)) continue;
            _db.EhcServiceRequestTypes.Add(new EhcServiceRequestType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = t.Code,
                Name = t.Name,
                Description = $"{demoTag} {t.Description}",
                IsActive = true,
                WorkflowName = null,
                FormDefinitionJson = t.FormDefinitionJson,
                CreatedAt = now.AddDays(-20),
                CreatedBy = "System"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var allTypes = await _db.EhcServiceRequestTypes
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        if (allTypes.Count == 0) return;

        var requestTypeByCode = allTypes.ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        var wf = await EnsureServiceRequestWorkflowDefinitionAsync(tenantId, now, demoTag, cancellationToken);

        var demoRequests = new[]
        {
            new { Number = "SR-26-000001", TypeCode = "ACCESS_REQUEST", Status = EhcServiceRequestStatus.PendingApproval, Title = $"{demoTag} Access to Finance app" },
            new { Number = "SR-26-000002", TypeCode = "EQUIPMENT", Status = EhcServiceRequestStatus.Submitted, Title = $"{demoTag} Request a monitor" },
            new { Number = "SR-26-000003", TypeCode = "SOFTWARE", Status = EhcServiceRequestStatus.Approved, Title = $"{demoTag} Install Visual Studio" },
            new { Number = "SR-26-000004", TypeCode = "ACCESS_REQUEST", Status = EhcServiceRequestStatus.Rejected, Title = $"{demoTag} Access to HR app" },
            new { Number = "SR-26-000005", TypeCode = "EQUIPMENT", Status = EhcServiceRequestStatus.Fulfilled, Title = $"{demoTag} Laptop request" },
            new { Number = "SR-26-000006", TypeCode = "SOFTWARE", Status = EhcServiceRequestStatus.Closed, Title = $"{demoTag} Install approved plugin" }
        };

        foreach (var r in demoRequests)
        {
            var exists = await _db.EhcServiceRequests
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.RequestNumber == r.Number, cancellationToken);

            if (exists) continue;
            if (!requestTypeByCode.TryGetValue(r.TypeCode, out var requestType)) continue;

            var submittedAt = now.AddDays(-new Random(r.Number.GetHashCode()).Next(1, 25));

            var request = new EhcServiceRequest
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RequestTypeId = requestType.Id,
                RequestNumber = r.Number,
                RequesterUserId = requesterUserId,
                Status = r.Status,
                Title = r.Title,
                FormDataJson = JsonSerializer.Serialize(new
                {
                    note = $"{demoTag} Demo request data",
                    requestNumber = r.Number
                }),
                WorkflowInstanceId = null,
                SubmittedAtUtc = submittedAt,
                ApprovedAtUtc = r.Status is EhcServiceRequestStatus.Approved or EhcServiceRequestStatus.Fulfilled or EhcServiceRequestStatus.Closed ? submittedAt.AddDays(1) : null,
                ApprovedByUserId = r.Status is EhcServiceRequestStatus.Approved or EhcServiceRequestStatus.Fulfilled or EhcServiceRequestStatus.Closed ? managerUserId : null,
                RejectedAtUtc = r.Status == EhcServiceRequestStatus.Rejected ? submittedAt.AddDays(1) : null,
                RejectedByUserId = r.Status == EhcServiceRequestStatus.Rejected ? managerUserId : null,
                RejectionReason = r.Status == EhcServiceRequestStatus.Rejected ? $"{demoTag} Not approved for this role." : null,
                CreatedAt = submittedAt,
                CreatedBy = "System"
            };

            _db.EhcServiceRequests.Add(request);
            _db.EhcServiceRequestAuditEvents.Add(new EhcServiceRequestAuditEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceRequestId = request.Id,
                EventType = "Seeded",
                Title = $"{demoTag} Demo request seeded",
                Body = $"{demoTag} Seeded service request {request.RequestNumber} ({requestType.Code}).",
                IsInternal = true,
                ActorUserId = managerUserId,
                CreatedAt = submittedAt,
                CreatedBy = "System"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Ensure the pending approval request has a workflow instance + approval record.
        if (!wf.HasValue) return;

        var pendingRequest = await _db.EhcServiceRequests
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && !r.IsDeleted && r.RequestNumber == "SR-26-000001", cancellationToken);

        if (pendingRequest == null) return;

        if (pendingRequest.WorkflowInstanceId.HasValue && pendingRequest.WorkflowInstanceId.Value != Guid.Empty) return;

        var submittedAtUtc = pendingRequest.SubmittedAtUtc ?? pendingRequest.CreatedAt;
        var wfInstanceId = await EnsurePendingApprovalWorkflowInstanceAsync(
            tenantId,
            wf.Value,
            pendingRequest.Id,
            submittedAtUtc,
            managerUserId,
            cancellationToken);

        if (wfInstanceId.HasValue)
        {
            pendingRequest.WorkflowInstanceId = wfInstanceId.Value;
            pendingRequest.UpdatedAt = now;
            pendingRequest.UpdatedBy = "System";
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<(Guid EntityTypeId, Guid WorkflowDefinitionId, Guid ApprovalStepId)?> EnsureServiceRequestWorkflowDefinitionAsync(
        Guid tenantId,
        DateTime now,
        string demoTag,
        CancellationToken cancellationToken)
    {
        var entityType = await _db.WorkflowEntityTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(et => et.TenantId == tenantId && et.Code == "SERVICE_REQUEST", cancellationToken);

        if (entityType == null)
        {
            entityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "SERVICE_REQUEST",
                Name = "Service Request",
                Description = "EHC Service Request",
                EntityClassName = typeof(EhcServiceRequest).FullName,
                IsActive = true,
                DisplayOrder = 60,
                Icon = "clipboard",
                ColorCode = "#4F46E5",
                CreatedAt = now,
                CreatedBy = "System"
            };
            _db.WorkflowEntityTypes.Add(entityType);
            await _db.SaveChangesAsync(cancellationToken);
        }

        const string wfName = "Service Request - Demo Approval";
        var wfDef = await _db.WorkflowDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.EntityTypeId == entityType.Id && w.Name == wfName, cancellationToken);

        if (wfDef == null)
        {
            wfDef = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EntityTypeId = entityType.Id,
                Name = wfName,
                Description = $"{demoTag} Minimal approval workflow for service requests",
                Version = 1,
                IsActive = true,
                Configuration = "{}",
                CreatedAt = now,
                CreatedBy = "System"
            };

            var stepSubmit = new WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = wfDef.Id,
                Name = "Submitted",
                Description = "Request submitted",
                StepType = WorkflowStepType.Manual,
                Order = 1,
                IsStartStep = true,
                IsEndStep = false,
                CreatedAt = now,
                CreatedBy = "System"
            };

            var stepApprove = new WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = wfDef.Id,
                Name = "Manager Approval",
                Description = "Approval required",
                StepType = WorkflowStepType.Approval,
                Order = 2,
                IsStartStep = false,
                IsEndStep = false,
                RequiredRole = "HelpdeskManager",
                CreatedAt = now,
                CreatedBy = "System"
            };

            var stepFulfill = new WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = wfDef.Id,
                Name = "Fulfillment",
                Description = "Fulfill request",
                StepType = WorkflowStepType.Manual,
                Order = 3,
                IsStartStep = false,
                IsEndStep = true,
                CreatedAt = now,
                CreatedBy = "System"
            };

            _db.WorkflowDefinitions.Add(wfDef);
            _db.WorkflowSteps.AddRange(stepSubmit, stepApprove, stepFulfill);

            _db.WorkflowTransitions.AddRange(
                new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WorkflowDefinitionId = wfDef.Id,
                    FromStepId = stepSubmit.Id,
                    ToStepId = stepApprove.Id,
                    Name = "Submit -> Approve",
                    IsDefault = true,
                    Priority = 0,
                    CreatedAt = now,
                    CreatedBy = "System"
                },
                new WorkflowTransition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WorkflowDefinitionId = wfDef.Id,
                    FromStepId = stepApprove.Id,
                    ToStepId = stepFulfill.Id,
                    Name = "Approve -> Fulfill",
                    IsDefault = true,
                    Priority = 0,
                    CreatedAt = now,
                    CreatedBy = "System"
                });

            await _db.SaveChangesAsync(cancellationToken);
        }

        var approvalStepId = await _db.WorkflowSteps.AsNoTracking()
            .Where(s =>
                s.TenantId == tenantId &&
                !s.IsDeleted &&
                s.WorkflowDefinitionId == wfDef.Id &&
                s.StepType == WorkflowStepType.Approval)
            .OrderBy(s => s.Order)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (approvalStepId == Guid.Empty) return null;

        return (entityType.Id, wfDef.Id, approvalStepId);
    }

    private async Task<Guid?> EnsurePendingApprovalWorkflowInstanceAsync(
        Guid tenantId,
        (Guid EntityTypeId, Guid WorkflowDefinitionId, Guid ApprovalStepId) wf,
        Guid entityId,
        DateTime submittedAtUtc,
        Guid? approverUserId,
        CancellationToken cancellationToken)
    {
        var existing = await _db.WorkflowInstances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i =>
                i.TenantId == tenantId &&
                i.EntityTypeId == wf.EntityTypeId &&
                i.EntityId == entityId,
                cancellationToken);

        if (existing != null) return existing.Id;

        var initiatorId = approverUserId ?? await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (initiatorId == Guid.Empty) return null;

        var wfInstance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowDefinitionId = wf.WorkflowDefinitionId,
            EntityId = entityId,
            EntityTypeId = wf.EntityTypeId,
            Status = WorkflowInstanceStatus.InProgress,
            Priority = WorkflowPriority.Normal,
            InitiatedById = initiatorId,
            StartedById = initiatorId,
            CreatedDate = submittedAtUtc,
            StartedDate = submittedAtUtc,
            CurrentStepId = wf.ApprovalStepId,
            DataContext = "{}",
            Data = "{}",
            CreatedAt = submittedAtUtc,
            CreatedBy = "System"
        };

        var stepInstance = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowInstanceId = wfInstance.Id,
            WorkflowStepId = wf.ApprovalStepId,
            Status = WorkflowStepInstanceStatus.Pending,
            AssignedToId = approverUserId,
            CreatedDate = submittedAtUtc,
            DueDate = submittedAtUtc.AddDays(3),
            CreatedAt = submittedAtUtc,
            CreatedBy = "System"
        };

        var approval = new WorkflowApproval
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StepInstanceId = stepInstance.Id,
            ApproverId = approverUserId,
            ApproverRole = "HelpdeskManager",
            Status = WorkflowApprovalStatus.Pending,
            RequestedDate = submittedAtUtc,
            DueDate = submittedAtUtc.AddDays(3),
            Priority = WorkflowPriority.Normal,
            CreatedAt = submittedAtUtc,
            CreatedBy = "System"
        };

        _db.WorkflowInstances.Add(wfInstance);
        _db.WorkflowStepInstances.Add(stepInstance);
        _db.WorkflowApprovals.Add(approval);

        await _db.SaveChangesAsync(cancellationToken);
        return wfInstance.Id;
    }

    private async Task EnsureInboundChannelsDemoAsync(
        Guid tenantId,
        DateTime now,
        string demoTag,
        CancellationToken cancellationToken)
    {
        const string mailbox = "support@default.com";
        var emailChannel = await _db.EhcInboundEmailChannels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.MailboxAddress == mailbox, cancellationToken);

        if (emailChannel == null)
        {
            emailChannel = new EhcInboundEmailChannel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MailboxAddress = mailbox,
                FolderName = "Inbox",
                IsEnabled = false,
                RequireKnownSender = true,
                AutoProvisionUnknownSenders = false,
                DefaultTicketType = EhcTicketType.Helpdesk,
                DefaultPriority = EhcTicketPriority.Medium,
                UseGraphWebhook = false,
                LastError = $"{demoTag} Seeded channel for testing (disabled by default).",
                CreatedAt = now.AddDays(-5),
                CreatedBy = "System"
            };

            _db.EhcInboundEmailChannels.Add(emailChannel);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var smsChannel = await _db.EhcInboundMessagingChannels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.Provider == EhcInboundMessagingProvider.Twilio &&
                c.Source == EhcTicketSource.Sms &&
                c.ToAddress == "+15550100",
                cancellationToken);

        if (smsChannel == null)
        {
            smsChannel = new EhcInboundMessagingChannel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = EhcInboundMessagingProvider.Twilio,
                IsEnabled = false,
                Source = EhcTicketSource.Sms,
                ToAddress = "+15550100",
                RequireKnownSender = true,
                AutoProvisionUnknownSenders = false,
                DefaultTicketType = EhcTicketType.Helpdesk,
                DefaultPriority = EhcTicketPriority.Medium,
                CreatedAt = now.AddDays(-5),
                CreatedBy = "System"
            };

            _db.EhcInboundMessagingChannels.Add(smsChannel);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var whatsappChannel = await _db.EhcInboundMessagingChannels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.Provider == EhcInboundMessagingProvider.Twilio &&
                c.Source == EhcTicketSource.WhatsApp &&
                c.ToAddress == "+15550200",
                cancellationToken);

        if (whatsappChannel == null)
        {
            whatsappChannel = new EhcInboundMessagingChannel
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = EhcInboundMessagingProvider.Twilio,
                IsEnabled = false,
                Source = EhcTicketSource.WhatsApp,
                ToAddress = "+15550200",
                RequireKnownSender = true,
                AutoProvisionUnknownSenders = false,
                DefaultTicketType = EhcTicketType.Helpdesk,
                DefaultPriority = EhcTicketPriority.Medium,
                CreatedAt = now.AddDays(-5),
                CreatedBy = "System"
            };

            _db.EhcInboundMessagingChannels.Add(whatsappChannel);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var ticket = await _db.EhcTickets.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TicketNumber.StartsWith("TKT-DEMO-"))
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket == null) return;

        var emailMsgExists = await _db.EhcInboundEmailMessages
            .IgnoreQueryFilters()
            .AnyAsync(m => m.TenantId == tenantId && m.GraphMessageId == "DEMO-GRAPH-1", cancellationToken);

        if (!emailMsgExists)
        {
            _db.EhcInboundEmailMessages.Add(new EhcInboundEmailMessage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ChannelId = emailChannel.Id,
                GraphMessageId = "DEMO-GRAPH-1",
                InternetMessageId = "<demo-1@default.com>",
                ConversationId = "DEMO-CONV-1",
                TicketId = ticket.Id,
                FromAddress = "external@default.com",
                Subject = $"{demoTag} Email-created ticket {ticket.TicketNumber}",
                ReceivedAtUtc = now.AddDays(-2),
                CreatedAt = now.AddDays(-2),
                CreatedBy = "System"
            });

            _db.EhcInboundEmailWebhookQueueItems.Add(new EhcInboundEmailWebhookQueueItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ChannelId = emailChannel.Id,
                GraphMessageId = "DEMO-GRAPH-1",
                ReceivedAtUtc = now.AddDays(-2),
                ProcessedAtUtc = now.AddDays(-2).AddMinutes(1),
                CreatedAt = now.AddDays(-2),
                CreatedBy = "System"
            });
        }

        var smsMsgExists = await _db.EhcInboundMessagingMessages
            .IgnoreQueryFilters()
            .AnyAsync(m => m.TenantId == tenantId && m.ProviderMessageId == "DEMO-SMS-1", cancellationToken);

        if (!smsMsgExists)
        {
            _db.EhcInboundMessagingMessages.Add(new EhcInboundMessagingMessage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ChannelId = smsChannel.Id,
                ProviderMessageId = "DEMO-SMS-1",
                FromAddress = "+15550001",
                ToAddress = smsChannel.ToAddress,
                Body = $"{demoTag} SMS message for {ticket.TicketNumber}",
                ReceivedAtUtc = now.AddDays(-1),
                TicketId = ticket.Id,
                CreatedAt = now.AddDays(-1),
                CreatedBy = "System"
            });
        }

        var waMsgExists = await _db.EhcInboundMessagingMessages
            .IgnoreQueryFilters()
            .AnyAsync(m => m.TenantId == tenantId && m.ProviderMessageId == "DEMO-WA-1", cancellationToken);

        if (!waMsgExists)
        {
            _db.EhcInboundMessagingMessages.Add(new EhcInboundMessagingMessage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ChannelId = whatsappChannel.Id,
                ProviderMessageId = "DEMO-WA-1",
                FromAddress = "+15550002",
                ToAddress = whatsappChannel.ToAddress,
                Body = $"{demoTag} WhatsApp message for {ticket.TicketNumber}",
                ReceivedAtUtc = now.AddDays(-1).AddMinutes(-30),
                TicketId = ticket.Id,
                CreatedAt = now.AddDays(-1).AddMinutes(-30),
                CreatedBy = "System"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureComplianceDemoAsync(
        Guid tenantId,
        DateTime now,
        Guid? techCategoryId,
        Guid? managerUserId,
        string demoTag,
        CancellationToken cancellationToken)
    {
        if (techCategoryId.HasValue)
        {
            var exceptionExists = await _db.EhcRetentionCategoryExceptions
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.CategoryId == techCategoryId.Value, cancellationToken);

            if (!exceptionExists)
            {
                _db.EhcRetentionCategoryExceptions.Add(new EhcRetentionCategoryException
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CategoryId = techCategoryId.Value,
                    IsActive = true,
                    AuditEventRetentionDays = 730,
                    Notes = $"{demoTag} Keep audit events longer for technical tickets.",
                    CreatedAt = now.AddDays(-30),
                    CreatedBy = "System"
                });
            }
        }

        var ticket = await _db.EhcTickets.AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.TicketNumber.StartsWith("TKT-DEMO-"))
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket != null)
        {
            var holdExists = await _db.EhcLegalHolds
                .IgnoreQueryFilters()
                .AnyAsync(h => h.TenantId == tenantId && h.TicketId == ticket.Id && h.IsActive, cancellationToken);

            if (!holdExists)
            {
                _db.EhcLegalHolds.Add(new EhcLegalHold
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TicketId = ticket.Id,
                    IsActive = true,
                    Reason = $"{demoTag} Preserve ticket for audit/compliance testing.",
                    ReferenceNumber = "LH-DEMO-0001",
                    ReleasedAtUtc = null,
                    ReleasedByUserId = null,
                    CreatedAt = now.AddDays(-5),
                    CreatedBy = "System"
                });
            }
        }

        const string exportFilePath = "demo/ehc/compliance/audit-export.csv";
        var exportExists = await _db.EhcComplianceAuditExports
            .IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == tenantId && e.FileUploadRecord.FilePath == exportFilePath, cancellationToken);

        if (!exportExists)
        {
            var uploadedBy = managerUserId ?? await _db.Users.AsNoTracking()
                .Where(u => u.TenantId == tenantId)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (uploadedBy == Guid.Empty) uploadedBy = Guid.NewGuid();

            var file = new FileUploadRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Category = "ehc-compliance-audit-export",
                FilePath = exportFilePath,
                StoredFileName = "audit-export-demo.csv",
                OriginalFileName = "audit-export-demo.csv",
                ContentType = "text/csv",
                FileSize = 1024,
                StorageProvider = "Demo",
                UploadedByUserId = uploadedBy,
                VirusScanStatus = FileVirusScanStatus.Skipped,
                CreatedAt = now.AddDays(-2),
                CreatedBy = "System"
            };

            var payload = $"{demoTag}|{tenantId}|{now:O}";
            var sha = ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

            _db.FileUploadRecords.Add(file);
            _db.EhcComplianceAuditExports.Add(new EhcComplianceAuditExport
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FromUtc = now.AddDays(-30),
                ToUtc = now,
                TicketId = ticket?.Id,
                CategoryId = techCategoryId,
                FileUploadRecordId = file.Id,
                Sha256 = sha,
                RowCount = 25,
                CreatedAt = now.AddDays(-2),
                CreatedBy = "System"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
