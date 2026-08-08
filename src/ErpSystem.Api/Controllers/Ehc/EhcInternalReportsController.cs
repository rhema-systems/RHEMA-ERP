using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Services.Ehc.Sla;
using ErpSystem.Shared;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/reports")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin + "," +
    Constants.Roles.Employee)]
public sealed class EhcInternalReportsController : ControllerBase
{
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcInternalReportsController> _logger;

    public EhcInternalReportsController(
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<EhcInternalReportsController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private sealed class SlaCompliancePointDto
    {
        public string Date { get; set; } = string.Empty; // yyyy-MM-dd
        public int TotalTickets { get; set; }

        public int FirstResponseMet { get; set; }
        public int FirstResponseBreached { get; set; }
        public int ResolutionMet { get; set; }
        public int ResolutionBreached { get; set; }

        public double? FirstResponseCompliancePercent { get; set; }
        public double? ResolutionCompliancePercent { get; set; }
    }

    private sealed class AgentPerformanceRowDto
    {
        public Guid? AgentUserId { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public int TotalAssigned { get; set; }
        public int OpenAssigned { get; set; }
        public int ResolvedAssigned { get; set; }
        public int FirstResponseBreaches { get; set; }
        public int ResolutionBreaches { get; set; }
        public int? AvgFirstResponseMinutes { get; set; }
        public int? AvgResolutionMinutes { get; set; }
    }

    private sealed class EscalationRowDto
    {
        public Guid PolicyId { get; set; }
        public string PolicyName { get; set; } = string.Empty;
        public string Trigger { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Count { get; set; }
    }

    private sealed class HelpdeskSummaryTotalsDto
    {
        public int Total { get; set; }
        public int Open { get; set; }
        public int FirstResponseBreaches { get; set; }
        public int ResolutionBreaches { get; set; }
        public int? AvgFirstResponseMinutes { get; set; }
        public int? AvgResolutionMinutes { get; set; }
    }

    private sealed class StatusCountDto
    {
        public EhcTicketStatus Status { get; set; }
        public int Count { get; set; }
    }

    private sealed class PriorityCountDto
    {
        public EhcTicketPriority Priority { get; set; }
        public int Count { get; set; }
    }

    private sealed class TypeCountDto
    {
        public EhcTicketType TicketType { get; set; }
        public int Count { get; set; }
    }

    private sealed class DepartmentCountDto
    {
        public Guid? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class CategoryCountDto
    {
        public Guid? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class RootCauseCountDto
    {
        public Guid? RootCauseId { get; set; }
        public string RootCauseName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class FeedbackSummaryDto
    {
        public int FeedbackCount { get; set; }
        public double? AvgRating { get; set; }
        public int ResolvedOrClosedTickets { get; set; }
        public double? ResponseRatePercent { get; set; }
        public Dictionary<int, int> RatingDistribution { get; set; } = new(); // 1-5
    }

    private sealed class FeedbackByAgentRowDto
    {
        public Guid? AgentUserId { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public int FeedbackCount { get; set; }
        public double? AvgRating { get; set; }
    }

    private sealed class FeedbackByDepartmentRowDto
    {
        public Guid? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int FeedbackCount { get; set; }
        public double? AvgRating { get; set; }
    }

    private sealed class FeedbackTrendPointDto
    {
        public string Date { get; set; } = string.Empty; // yyyy-MM-dd
        public int FeedbackCount { get; set; }
        public double? AvgRating { get; set; }
    }

    private sealed class ProblemLinkTrendPointDto
    {
        public string Date { get; set; } = string.Empty; // yyyy-MM-dd
        public int LinkedTickets { get; set; }
        public int DistinctProblems { get; set; }
    }

    private sealed class ProblemStatusCountDto
    {
        public EhcProblemStatus Status { get; set; }
        public int Count { get; set; }
    }

    private sealed class ProblemPriorityCountDto
    {
        public EhcTicketPriority Priority { get; set; }
        public int Count { get; set; }
    }

    private sealed class ProblemDepartmentCountDto
    {
        public Guid? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class TopRecurringProblemDto
    {
        public Guid ProblemId { get; set; }
        public string ProblemNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public EhcProblemStatus Status { get; set; }
        public EhcTicketPriority Priority { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public int LinkedTicketsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class ProblemsSummaryDto
    {
        public int TotalProblems { get; set; }
        public int OpenProblems { get; set; }
        public int ProblemsCreatedLastDays { get; set; }
        public int LinkedTicketsLastDays { get; set; }
        public List<ProblemStatusCountDto> ByStatus { get; set; } = new();
        public List<ProblemPriorityCountDto> ByPriority { get; set; } = new();
        public List<ProblemDepartmentCountDto> ByDepartment { get; set; } = new();
        public List<TopRecurringProblemDto> TopRecurring { get; set; } = new();
    }

    private sealed class HelpdeskSummaryDto
    {
        public HelpdeskSummaryTotalsDto Totals { get; set; } = new();
        public List<StatusCountDto> ByStatus { get; set; } = new();
        public List<PriorityCountDto> ByPriority { get; set; } = new();
        public List<TypeCountDto> ByType { get; set; } = new();
        public List<DepartmentCountDto> ByDepartment { get; set; } = new();
        public List<CategoryCountDto> ByCategory { get; set; } = new();
        public List<RootCauseCountDto> ByRootCause { get; set; } = new();
    }

    private Guid GetTenantIdOrEmpty()
        => _currentUserService.TenantId ?? Guid.Empty;

    private static DateTime ClampFromUtc(int days)
    {
        if (days < 1) days = 1;
        if (days > 365) days = 365;
        return DateTime.UtcNow.Date.AddDays(-days + 1);
    }

    private async Task<HelpdeskSummaryDto> BuildSummaryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var summary = new HelpdeskSummaryDto();
        if (tenantId == Guid.Empty)
            return summary;

        var now = DateTime.UtcNow;
        var baseQ = _db.EhcTickets
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        var openQ = baseQ
            .Where(t => t.Status != EhcTicketStatus.Closed && t.Status != EhcTicketStatus.Resolved);

        summary.Totals.Total = await baseQ.CountAsync(cancellationToken);
        summary.Totals.Open = await openQ.CountAsync(cancellationToken);

        summary.Totals.FirstResponseBreaches = await openQ
            .Where(t => t.FirstRespondedAt == null && t.FirstResponseDueAt.HasValue && t.FirstResponseDueAt.Value <= now)
            .CountAsync(cancellationToken);

        summary.Totals.ResolutionBreaches = await openQ
            .Where(t => t.ResolvedAt == null && t.ResolutionDueAt.HasValue && t.ResolutionDueAt.Value <= now)
            .CountAsync(cancellationToken);

        summary.ByStatus = await baseQ
            .GroupBy(t => t.Status)
            .Select(g => new StatusCountDto { Status = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        summary.ByPriority = await baseQ
            .GroupBy(t => t.Priority)
            .Select(g => new PriorityCountDto { Priority = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        summary.ByType = await baseQ
            .GroupBy(t => t.TicketType)
            .Select(g => new TypeCountDto { TicketType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var byDepartment = await baseQ
            .GroupBy(t => t.AssignedDepartmentId)
            .Select(g => new { departmentId = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync(cancellationToken);

        var departmentIds = byDepartment
            .Where(x => x.departmentId.HasValue && x.departmentId.Value != Guid.Empty)
            .Select(x => x.departmentId!.Value)
            .ToList();

        var departmentNames = await _db.Departments
            .AsNoTracking()
            .Where(d => departmentIds.Contains(d.Id) && d.TenantId == tenantId && !d.IsDeleted)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(cancellationToken);

        var deptNameById = departmentNames.ToDictionary(x => x.Id, x => x.Name);
        summary.ByDepartment = byDepartment.Select(x => new DepartmentCountDto
        {
            DepartmentId = x.departmentId,
            DepartmentName = x.departmentId.HasValue && deptNameById.TryGetValue(x.departmentId.Value, out var name) ? name : "Unassigned",
            Count = x.count
        }).ToList();

        var byCategory = await baseQ
            .GroupBy(t => t.CategoryId)
            .Select(g => new { categoryId = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync(cancellationToken);

        var categoryIds = byCategory
            .Where(x => x.categoryId.HasValue && x.categoryId.Value != Guid.Empty)
            .Select(x => x.categoryId!.Value)
            .ToList();

        var categoryNames = await _db.EhcTicketCategories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id) && c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        var catNameById = categoryNames.ToDictionary(x => x.Id, x => x.Name);
        summary.ByCategory = byCategory.Select(x => new CategoryCountDto
        {
            CategoryId = x.categoryId,
            CategoryName = x.categoryId.HasValue && catNameById.TryGetValue(x.categoryId.Value, out var name) ? name : "Uncategorized",
            Count = x.count
        }).ToList();

        var byRootCause = await baseQ
            .Where(t => t.TicketType == EhcTicketType.Complaint && t.RootCauseId.HasValue && t.RootCauseId.Value != Guid.Empty)
            .GroupBy(t => t.RootCauseId)
            .Select(g => new { rootCauseId = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToListAsync(cancellationToken);

        var rootCauseIds = byRootCause
            .Where(x => x.rootCauseId.HasValue && x.rootCauseId.Value != Guid.Empty)
            .Select(x => x.rootCauseId!.Value)
            .ToList();

        var rootCauseNames = await _db.EhcRootCauseCodes
            .AsNoTracking()
            .Where(rc => rootCauseIds.Contains(rc.Id) && rc.TenantId == tenantId && !rc.IsDeleted)
            .Select(rc => new { rc.Id, rc.Name })
            .ToListAsync(cancellationToken);

        var rootCauseNameById = rootCauseNames.ToDictionary(x => x.Id, x => x.Name);
        summary.ByRootCause = byRootCause.Select(x => new RootCauseCountDto
        {
            RootCauseId = x.rootCauseId,
            RootCauseName = x.rootCauseId.HasValue && rootCauseNameById.TryGetValue(x.rootCauseId.Value, out var name) ? name : "Unspecified",
            Count = x.count
        }).ToList();

        var firstResponseMinutes = await baseQ
            .Where(t => t.FirstRespondedAt.HasValue)
            .Select(t => EF.Functions.DateDiffMinute(t.CreatedAt, t.FirstRespondedAt!.Value))
            .ToListAsync(cancellationToken);

        if (firstResponseMinutes.Count > 0)
            summary.Totals.AvgFirstResponseMinutes = (int)Math.Round(firstResponseMinutes.Average());

        var resolutionMinutes = await baseQ
            .Where(t => t.ResolvedAt.HasValue)
            .Select(t => EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt!.Value))
            .ToListAsync(cancellationToken);

        if (resolutionMinutes.Count > 0)
            summary.Totals.AvgResolutionMinutes = (int)Math.Round(resolutionMinutes.Average());

        return summary;
    }

    private async Task<FeedbackSummaryDto> BuildFeedbackSummaryAsync(Guid tenantId, int days, CancellationToken cancellationToken)
    {
        var fromUtc = ClampFromUtc(days);

        var ticketsQ = _db.EhcTickets
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.CreatedAt >= fromUtc);

        var resolvedOrClosed = await ticketsQ
            .Where(t => t.Status == EhcTicketStatus.Resolved || t.Status == EhcTicketStatus.Closed)
            .CountAsync(cancellationToken);

        var feedbackQ = _db.EhcTicketFeedbacks
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && !f.IsDeleted && f.CreatedAt >= fromUtc && f.Rating >= 1 && f.Rating <= 5);

        var feedbackCount = await feedbackQ.CountAsync(cancellationToken);
        var avg = feedbackCount == 0 ? (double?)null : await feedbackQ.AverageAsync(f => (double)f.Rating, cancellationToken);

        var dist = await feedbackQ
            .GroupBy(f => f.Rating)
            .Select(g => new { rating = g.Key, count = g.Count() })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<int, int>();
        for (var r = 1; r <= 5; r++)
        {
            map[r] = dist.FirstOrDefault(x => x.rating == r)?.count ?? 0;
        }

        double? responseRate = null;
        if (resolvedOrClosed > 0)
        {
            responseRate = Math.Round((feedbackCount * 100.0) / resolvedOrClosed, 2);
        }

        return new FeedbackSummaryDto
        {
            FeedbackCount = feedbackCount,
            AvgRating = avg.HasValue ? Math.Round(avg.Value, 2) : null,
            ResolvedOrClosedTickets = resolvedOrClosed,
            ResponseRatePercent = responseRate,
            RatingDistribution = map
        };
    }

    private async Task<List<FeedbackByAgentRowDto>> BuildFeedbackByAgentAsync(Guid tenantId, int days, CancellationToken cancellationToken)
    {
        var fromUtc = ClampFromUtc(days);

        var rows = await (
            from f in _db.EhcTicketFeedbacks.AsNoTracking()
            join t in _db.EhcTickets.AsNoTracking() on f.TicketId equals t.Id
            where f.TenantId == tenantId && !f.IsDeleted && f.CreatedAt >= fromUtc
               && t.TenantId == tenantId && !t.IsDeleted
               && f.Rating >= 1 && f.Rating <= 5
            group new { f, t } by t.AssignedToUserId into g
            select new
            {
                agentUserId = g.Key,
                count = g.Count(),
                avg = g.Average(x => (double)x.f.Rating)
            }
        ).ToListAsync(cancellationToken);

        var agentIds = rows
            .Where(r => r.agentUserId.HasValue && r.agentUserId.Value != Guid.Empty)
            .Select(r => r.agentUserId!.Value)
            .Distinct()
            .ToList();

        var names = await _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && agentIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
            .ToListAsync(cancellationToken);

        var nameById = names.ToDictionary(x => x.Id, x => x.Name);

        return rows
            .Select(r => new FeedbackByAgentRowDto
            {
                AgentUserId = r.agentUserId,
                AgentName = r.agentUserId.HasValue && r.agentUserId.Value != Guid.Empty && nameById.TryGetValue(r.agentUserId.Value, out var n) ? n : "Unassigned",
                FeedbackCount = r.count,
                AvgRating = Math.Round(r.avg, 2)
            })
            .OrderByDescending(x => x.FeedbackCount)
            .ThenByDescending(x => x.AvgRating ?? 0)
            .ToList();
    }

    private async Task<List<FeedbackByDepartmentRowDto>> BuildFeedbackByDepartmentAsync(Guid tenantId, int days, CancellationToken cancellationToken)
    {
        var fromUtc = ClampFromUtc(days);

        var rows = await (
            from f in _db.EhcTicketFeedbacks.AsNoTracking()
            join t in _db.EhcTickets.AsNoTracking() on f.TicketId equals t.Id
            where f.TenantId == tenantId && !f.IsDeleted && f.CreatedAt >= fromUtc
               && t.TenantId == tenantId && !t.IsDeleted
               && f.Rating >= 1 && f.Rating <= 5
            group new { f, t } by t.AssignedDepartmentId into g
            select new
            {
                departmentId = g.Key,
                count = g.Count(),
                avg = g.Average(x => (double)x.f.Rating)
            }
        ).ToListAsync(cancellationToken);

        var deptIds = rows
            .Where(r => r.departmentId.HasValue && r.departmentId.Value != Guid.Empty)
            .Select(r => r.departmentId!.Value)
            .Distinct()
            .ToList();

        var names = await _db.Departments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && deptIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(cancellationToken);

        var nameById = names.ToDictionary(x => x.Id, x => x.Name);

        return rows
            .Select(r => new FeedbackByDepartmentRowDto
            {
                DepartmentId = r.departmentId,
                DepartmentName = r.departmentId.HasValue && r.departmentId.Value != Guid.Empty && nameById.TryGetValue(r.departmentId.Value, out var n) ? n : "Unassigned",
                FeedbackCount = r.count,
                AvgRating = Math.Round(r.avg, 2)
            })
            .OrderByDescending(x => x.FeedbackCount)
            .ThenByDescending(x => x.AvgRating ?? 0)
            .ToList();
    }

    private async Task<List<FeedbackTrendPointDto>> BuildFeedbackTrendAsync(Guid tenantId, int days, CancellationToken cancellationToken)
    {
        var fromUtc = ClampFromUtc(days);

        var raw = await _db.EhcTicketFeedbacks
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && !f.IsDeleted && f.CreatedAt >= fromUtc && f.Rating >= 1 && f.Rating <= 5)
            .GroupBy(f => f.CreatedAt.Date)
            .Select(g => new
            {
                date = g.Key,
                FeedbackCount = g.Count(),
                AvgRating = g.Average(x => (double?)x.Rating)
            })
            .OrderBy(x => x.date)
            .ToListAsync(cancellationToken);

        return raw
            .Select(x => new FeedbackTrendPointDto
            {
                Date = x.date.ToString("yyyy-MM-dd"),
                FeedbackCount = x.FeedbackCount,
                AvgRating = x.AvgRating.HasValue ? Math.Round(x.AvgRating.Value, 2) : null
            })
            .ToList();
    }

    private async Task<ProblemsSummaryDto> BuildProblemsSummaryAsync(Guid tenantId, int days, int top, CancellationToken cancellationToken)
    {
        var summary = new ProblemsSummaryDto();
        if (tenantId == Guid.Empty)
            return summary;

        days = ClampDays(days);
        top = Math.Clamp(top, 1, 50);
        var fromUtc = ClampFromUtc(days);

        var baseQ = _db.EhcProblems
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted);

        summary.TotalProblems = await baseQ.CountAsync(cancellationToken);
        summary.OpenProblems = await baseQ
            .Where(p => p.Status != EhcProblemStatus.Resolved && p.Status != EhcProblemStatus.Closed)
            .CountAsync(cancellationToken);

        summary.ProblemsCreatedLastDays = await baseQ
            .Where(p => p.CreatedAt >= fromUtc)
            .CountAsync(cancellationToken);

        summary.LinkedTicketsLastDays = await _db.EhcProblemTicketLinks
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.CreatedAt >= fromUtc)
            .CountAsync(cancellationToken);

        summary.ByStatus = await baseQ
            .GroupBy(p => p.Status)
            .Select(g => new ProblemStatusCountDto { Status = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        summary.ByPriority = await baseQ
            .GroupBy(p => p.Priority)
            .Select(g => new ProblemPriorityCountDto { Priority = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var byDept = await baseQ
            .GroupBy(p => p.DepartmentId)
            .Select(g => new { departmentId = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync(cancellationToken);

        var deptIds = byDept
            .Where(x => x.departmentId.HasValue && x.departmentId.Value != Guid.Empty)
            .Select(x => x.departmentId!.Value)
            .Distinct()
            .ToList();

        var deptNames = await _db.Departments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && !d.IsDeleted && deptIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(cancellationToken);

        var deptNameById = deptNames.ToDictionary(x => x.Id, x => x.Name);
        summary.ByDepartment = byDept.Select(x => new ProblemDepartmentCountDto
        {
            DepartmentId = x.departmentId,
            DepartmentName = x.departmentId.HasValue && x.departmentId.Value != Guid.Empty && deptNameById.TryGetValue(x.departmentId.Value, out var n) ? n : "Unassigned",
            Count = x.count
        }).ToList();

        var linkCounts = await _db.EhcProblemTicketLinks
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted)
            .GroupBy(l => l.ProblemId)
            .Select(g => new { problemId = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(top)
            .ToListAsync(cancellationToken);

        var topIds = linkCounts.Select(x => x.problemId).ToList();
        if (topIds.Count == 0)
            return summary;

        var problems = await _db.EhcProblems
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && topIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.ProblemNumber,
                p.Title,
                p.Status,
                p.Priority,
                p.DepartmentId,
                departmentName = p.Department != null ? p.Department.Name : null,
                ownerName = p.OwnerUser != null ? (p.OwnerUser.FirstName + " " + p.OwnerUser.LastName).Trim() : null,
                p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var probById = problems.ToDictionary(x => x.Id, x => x);
        summary.TopRecurring = linkCounts
            .Where(x => probById.ContainsKey(x.problemId))
            .Select(x =>
            {
                var p = probById[x.problemId];
                return new TopRecurringProblemDto
                {
                    ProblemId = p.Id,
                    ProblemNumber = p.ProblemNumber,
                    Title = p.Title,
                    Status = p.Status,
                    Priority = p.Priority,
                    DepartmentName = p.departmentName ?? "Unassigned",
                    OwnerName = p.ownerName ?? "Unassigned",
                    LinkedTicketsCount = x.count,
                    CreatedAt = p.CreatedAt
                };
            })
            .ToList();

        return summary;
    }

    private async Task<List<ProblemLinkTrendPointDto>> BuildProblemLinkTrendAsync(Guid tenantId, int days, CancellationToken cancellationToken)
    {
        days = ClampDays(days);
        var fromUtc = ClampFromUtc(days);

        if (tenantId == Guid.Empty)
            return new List<ProblemLinkTrendPointDto>();

        var raw = await _db.EhcProblemTicketLinks
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.CreatedAt >= fromUtc)
            .GroupBy(l => l.CreatedAt.Date)
            .Select(g => new
            {
                date = g.Key,
                LinkedTickets = g.Count(),
                DistinctProblems = g.Select(x => x.ProblemId).Distinct().Count()
            })
            .OrderBy(x => x.date)
            .ToListAsync(cancellationToken);

        return raw
            .Select(x => new ProblemLinkTrendPointDto
            {
                Date = x.date.ToString("yyyy-MM-dd"),
                LinkedTickets = x.LinkedTickets,
                DistinctProblems = x.DistinctProblems
            })
            .ToList();
    }

    private static int ClampDays(int days) => Math.Clamp(days, 1, 365);

    private static void PdfKeyValueRow(TableDescriptor t, string key, string value)
    {
        t.Cell().Text(key).FontColor(Colors.Grey.Darken2);
        t.Cell().Text(value);
    }

    private static void PdfTableHeader(TableDescriptor t, params string[] headers)
    {
        foreach (var h in headers)
        {
            t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(h).SemiBold();
        }
    }

    private static void PdfTableRow(TableDescriptor t, params string[] values)
    {
        foreach (var v in values)
        {
            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(v ?? string.Empty);
        }
    }

    private static DateTime EnsureUtc(DateTime dt)
        => dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private static TimeZoneInfo ResolveTimeZoneOrUtc(string? calendarJson)
    {
        if (string.IsNullOrWhiteSpace(calendarJson))
        {
            return TimeZoneInfo.Utc;
        }

        if (!EhcSlaCalendarConfiguration.TryParse(calendarJson, out var cfg, out _))
        {
            return TimeZoneInfo.Utc;
        }

        var tzId = cfg?.TimeZoneId;
        if (string.IsNullOrWhiteSpace(tzId))
        {
            return TimeZoneInfo.Utc;
        }

        return EhcSlaCalendarConfiguration.TryResolveTimeZone(tzId, out var tz, out _)
            ? tz
            : TimeZoneInfo.Utc;
    }

    private static TimeZoneInfo ResolveTimeZoneFromTicketCalendarOrDefault(
        string? ticketCalendarJson,
        TimeZoneInfo defaultTimeZone,
        Dictionary<string, TimeZoneInfo> cache)
    {
        if (string.IsNullOrWhiteSpace(ticketCalendarJson))
        {
            return defaultTimeZone;
        }

        var key = ticketCalendarJson.Trim();
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var tz = ResolveTimeZoneOrUtc(key);
        if (tz.Equals(TimeZoneInfo.Utc) && !defaultTimeZone.Equals(TimeZoneInfo.Utc))
        {
            tz = defaultTimeZone;
        }

        cache[key] = tz;
        return tz;
    }

    private async Task<List<SlaCompliancePointDto>> BuildSlaComplianceAsync(Guid tenantId, int days, CancellationToken cancellationToken)
    {
        var points = new List<SlaCompliancePointDto>();
        if (tenantId == Guid.Empty) return points;

        days = ClampDays(days);
        var now = DateTime.UtcNow;

        // Group by the SLA calendar's local date (time zone from SLA templates / ticket snapshot calendar).
        // Use the most recently updated active SLA template timezone as the chart timezone fallback.
        var chartCalendarJson = await _db.EhcSlaTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive && !t.IsDeleted && !string.IsNullOrWhiteSpace(t.CalendarConfigurationJson))
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Select(t => t.CalendarConfigurationJson)
            .FirstOrDefaultAsync(cancellationToken);

        var chartTimeZone = ResolveTimeZoneOrUtc(chartCalendarJson);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, chartTimeZone);
        var startLocal = DateTime.SpecifyKind(localNow.Date.AddDays(-(days - 1)), DateTimeKind.Unspecified);
        var endLocalExclusive = DateTime.SpecifyKind(startLocal.AddDays(days), DateTimeKind.Unspecified);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, chartTimeZone);
        var endUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(endLocalExclusive, chartTimeZone);

        var startLocalDate = DateOnly.FromDateTime(startLocal);
        var endLocalExclusiveDate = DateOnly.FromDateTime(endLocalExclusive);

        var baseQ = _db.EhcTickets
            .AsNoTracking()
            .Where(t =>
                t.TenantId == tenantId &&
                !t.IsDeleted &&
                t.CreatedAt >= startUtc &&
                t.CreatedAt < endUtcExclusive);

        var rows = await baseQ
            .Select(t => new
            {
                t.CreatedAt,
                t.Status,
                t.FirstResponseDueAt,
                t.FirstRespondedAt,
                t.ResolutionDueAt,
                t.ResolvedAt,
                t.AppliedSlaCalendarConfigurationJson
            })
            .ToListAsync(cancellationToken);

        var tzCache = new Dictionary<string, TimeZoneInfo>(StringComparer.Ordinal);

        var agg = new Dictionary<DateOnly, (int total, int frMet, int frBreached, int resMet, int resBreached)>();
        foreach (var r in rows)
        {
            var tz = ResolveTimeZoneFromTicketCalendarOrDefault(r.AppliedSlaCalendarConfigurationJson, chartTimeZone, tzCache);
            var createdUtc = EnsureUtc(r.CreatedAt);
            var createdLocal = TimeZoneInfo.ConvertTimeFromUtc(createdUtc, tz);
            var localDate = DateOnly.FromDateTime(createdLocal);

            if (localDate < startLocalDate || localDate >= endLocalExclusiveDate)
            {
                continue;
            }

            var isPaused = r.Status is EhcTicketStatus.PendingUser or EhcTicketStatus.PendingThirdParty;

            var frMet = r.FirstResponseDueAt.HasValue &&
                        r.FirstRespondedAt.HasValue &&
                        r.FirstRespondedAt.Value <= r.FirstResponseDueAt.Value;

            var frBreached = r.FirstResponseDueAt.HasValue &&
                             !isPaused &&
                             ((r.FirstRespondedAt.HasValue && r.FirstRespondedAt.Value > r.FirstResponseDueAt.Value) ||
                              (!r.FirstRespondedAt.HasValue && r.FirstResponseDueAt.Value <= now));

            var resMet = r.ResolutionDueAt.HasValue &&
                         r.ResolvedAt.HasValue &&
                         r.ResolvedAt.Value <= r.ResolutionDueAt.Value;

            var resBreached = r.ResolutionDueAt.HasValue &&
                              !isPaused &&
                              ((r.ResolvedAt.HasValue && r.ResolvedAt.Value > r.ResolutionDueAt.Value) ||
                               (!r.ResolvedAt.HasValue && r.ResolutionDueAt.Value <= now));

            if (!agg.TryGetValue(localDate, out var a))
            {
                a = (0, 0, 0, 0, 0);
            }

            a.total += 1;
            if (frMet) a.frMet += 1;
            if (frBreached) a.frBreached += 1;
            if (resMet) a.resMet += 1;
            if (resBreached) a.resBreached += 1;
            agg[localDate] = a;
        }

        points = new List<SlaCompliancePointDto>(days);
        for (var i = 0; i < days; i++)
        {
            var d = startLocal.AddDays(i);
            var key = DateOnly.FromDateTime(d);
            if (!agg.TryGetValue(key, out var x))
            {
                points.Add(new SlaCompliancePointDto { Date = d.ToString("yyyy-MM-dd") });
                continue;
            }

            var frTotal = x.frMet + x.frBreached;
            var resTotal = x.resMet + x.resBreached;

            points.Add(new SlaCompliancePointDto
            {
                Date = d.ToString("yyyy-MM-dd"),
                TotalTickets = x.total,
                FirstResponseMet = x.frMet,
                FirstResponseBreached = x.frBreached,
                ResolutionMet = x.resMet,
                ResolutionBreached = x.resBreached,
                FirstResponseCompliancePercent = frTotal > 0 ? Math.Round(100.0 * x.frMet / frTotal, 1) : null,
                ResolutionCompliancePercent = resTotal > 0 ? Math.Round(100.0 * x.resMet / resTotal, 1) : null
            });
        }

        return points;
    }

    private async Task<List<AgentPerformanceRowDto>> BuildAgentPerformanceAsync(Guid tenantId, int days, CancellationToken cancellationToken)
    {
        var rows = new List<AgentPerformanceRowDto>();
        if (tenantId == Guid.Empty) return rows;

        days = ClampDays(days);
        var now = DateTime.UtcNow;
        var start = now.Date.AddDays(-(days - 1));

        var baseQ = _db.EhcTickets
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.CreatedAt >= start);

        var grouped = await baseQ
            .GroupBy(t => t.AssignedToUserId)
            .Select(g => new
            {
                agentUserId = g.Key,
                totalAssigned = g.Count(),
                openAssigned = g.Count(t => t.Status != EhcTicketStatus.Closed && t.Status != EhcTicketStatus.Resolved),
                resolvedAssigned = g.Count(t => t.Status == EhcTicketStatus.Closed || t.Status == EhcTicketStatus.Resolved),
                frBreaches = g.Count(t => t.FirstRespondedAt == null && t.FirstResponseDueAt.HasValue && t.FirstResponseDueAt.Value <= now),
                resBreaches = g.Count(t => t.ResolvedAt == null && t.ResolutionDueAt.HasValue && t.ResolutionDueAt.Value <= now),
                avgFirstResponse = g.Where(t => t.FirstRespondedAt.HasValue)
                    .Select(t => (int?)EF.Functions.DateDiffMinute(t.CreatedAt, t.FirstRespondedAt!.Value))
                    .Average(),
                avgResolution = g.Where(t => t.ResolvedAt.HasValue)
                    .Select(t => (int?)EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt!.Value))
                    .Average()
            })
            .ToListAsync(cancellationToken);

        var agentIds = grouped
            .Where(x => x.agentUserId.HasValue && x.agentUserId.Value != Guid.Empty)
            .Select(x => x.agentUserId!.Value)
            .Distinct()
            .ToList();

        var agentNames = await _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && agentIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
            .ToListAsync(cancellationToken);

        var nameById = agentNames.ToDictionary(
            x => x.Id,
            x =>
            {
                var full = $"{x.FirstName} {x.LastName}".Trim();
                return string.IsNullOrWhiteSpace(full) ? (x.UserName ?? x.Id.ToString()) : full;
            });

        rows = grouped
            .Select(x => new AgentPerformanceRowDto
            {
                AgentUserId = x.agentUserId,
                AgentName = x.agentUserId.HasValue && x.agentUserId.Value != Guid.Empty && nameById.TryGetValue(x.agentUserId.Value, out var n) ? n : "Unassigned",
                TotalAssigned = x.totalAssigned,
                OpenAssigned = x.openAssigned,
                ResolvedAssigned = x.resolvedAssigned,
                FirstResponseBreaches = x.frBreaches,
                ResolutionBreaches = x.resBreaches,
                AvgFirstResponseMinutes = x.avgFirstResponse.HasValue ? (int)Math.Round(x.avgFirstResponse.Value) : null,
                AvgResolutionMinutes = x.avgResolution.HasValue ? (int)Math.Round(x.avgResolution.Value) : null
            })
            .OrderByDescending(r => r.TotalAssigned)
            .ToList();

        return rows;
    }

    private async Task<List<EscalationRowDto>> BuildEscalationsAsync(Guid tenantId, int days, CancellationToken cancellationToken)
    {
        var rows = new List<EscalationRowDto>();
        if (tenantId == Guid.Empty) return rows;

        days = ClampDays(days);
        var now = DateTime.UtcNow;
        var start = now.Date.AddDays(-(days - 1));

        var baseQ = _db.EhcEscalationExecutions
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.ExecutedAtUtc >= start);

        var grouped = await baseQ
            .GroupBy(e => new { e.PolicyId, e.Level })
            .Select(g => new { g.Key.PolicyId, g.Key.Level, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync(cancellationToken);

        var policyIds = grouped.Select(x => x.PolicyId).Distinct().ToList();
        var policies = await _db.EhcEscalationPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && policyIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Trigger })
            .ToListAsync(cancellationToken);

        var policyById = policies.ToDictionary(x => x.Id, x => x);

        rows = grouped.Select(x =>
        {
            var p = policyById.TryGetValue(x.PolicyId, out var found) ? found : null;
            return new EscalationRowDto
            {
                PolicyId = x.PolicyId,
                PolicyName = p?.Name ?? "Unknown",
                Trigger = p?.Trigger.ToString() ?? "Unknown",
                Level = x.Level,
                Count = x.count
            };
        }).ToList();

        return rows;
    }

    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var summary = await BuildSummaryAsync(tenantId, cancellationToken);
            return Ok(new { success = true, data = summary });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC helpdesk summary report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }

    [HttpGet("sla-compliance")]
    public async Task<ActionResult> GetSlaCompliance([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var points = await BuildSlaComplianceAsync(tenantId, days, cancellationToken);
            return Ok(new { success = true, data = points });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC SLA compliance report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }

    [HttpGet("agent-performance")]
    public async Task<ActionResult> GetAgentPerformance([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var rows = await BuildAgentPerformanceAsync(tenantId, days, cancellationToken);
            return Ok(new { success = true, data = rows });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC agent performance report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }

    [HttpGet("escalations")]
    public async Task<ActionResult> GetEscalations([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var rows = await BuildEscalationsAsync(tenantId, days, cancellationToken);
            return Ok(new { success = true, data = rows });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC escalation report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }

    [HttpGet("feedback/summary")]
    public async Task<ActionResult> GetFeedbackSummary([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var result = await BuildFeedbackSummaryAsync(tenantId, days, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC feedback summary report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }

    [HttpGet("feedback/by-agent")]
    public async Task<ActionResult> GetFeedbackByAgent([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var rows = await BuildFeedbackByAgentAsync(tenantId, days, cancellationToken);
            return Ok(new { success = true, data = rows });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC feedback by-agent report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }

    [HttpGet("feedback/by-department")]
    public async Task<ActionResult> GetFeedbackByDepartment([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var rows = await BuildFeedbackByDepartmentAsync(tenantId, days, cancellationToken);
            return Ok(new { success = true, data = rows });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC feedback by-department report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }

    [HttpGet("feedback/trend")]
    public async Task<ActionResult> GetFeedbackTrend([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var points = await BuildFeedbackTrendAsync(tenantId, days, cancellationToken);
            return Ok(new { success = true, data = points });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC feedback trend report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }

    [HttpGet("problems/summary")]
    public async Task<ActionResult> GetProblemsSummary([FromQuery] int days = 30, [FromQuery] int top = 15, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var result = await BuildProblemsSummaryAsync(tenantId, days, top, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC problems summary report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }

    [HttpGet("problems/link-trend")]
    public async Task<ActionResult> GetProblemLinkTrend([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var result = await BuildProblemLinkTrendAsync(tenantId, days, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC problem link trend report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }

    [HttpGet("export/agent-performance.xlsx")]
    public async Task<IActionResult> ExportAgentPerformanceExcel([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var rows = await BuildAgentPerformanceAsync(tenantId, days, cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Agent Performance");

        var headers = new[]
        {
            "Agent",
            "Total Assigned",
            "Open Assigned",
            "Resolved Assigned",
            "First Response Breaches",
            "Resolution Breaches",
            "Avg First Response (min)",
            "Avg Resolution (min)"
        };

        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.AgentName;
            ws.Cell(r, 2).Value = row.TotalAssigned;
            ws.Cell(r, 3).Value = row.OpenAssigned;
            ws.Cell(r, 4).Value = row.ResolvedAssigned;
            ws.Cell(r, 5).Value = row.FirstResponseBreaches;
            ws.Cell(r, 6).Value = row.ResolutionBreaches;
            SetExcelValue(ws.Cell(r, 7), row.AvgFirstResponseMinutes);
            SetExcelValue(ws.Cell(r, 8), row.AvgResolutionMinutes);
            r++;
        }

        ws.ColumnsUsed().AdjustToContents();

        var bytes = ToByteArray(workbook);
        var fileName = $"ehc-agent-performance-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("export/sla-compliance.xlsx")]
    public async Task<IActionResult> ExportSlaComplianceExcel([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var points = await BuildSlaComplianceAsync(tenantId, days, cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("SLA Compliance");

        var headers = new[]
        {
            "Date",
            "Total Tickets",
            "First Response Met",
            "First Response Breached",
            "First Response Compliance (%)",
            "Resolution Met",
            "Resolution Breached",
            "Resolution Compliance (%)"
        };

        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var p in points)
        {
            ws.Cell(r, 1).Value = p.Date;
            ws.Cell(r, 2).Value = p.TotalTickets;
            ws.Cell(r, 3).Value = p.FirstResponseMet;
            ws.Cell(r, 4).Value = p.FirstResponseBreached;
            SetExcelValue(ws.Cell(r, 5), p.FirstResponseCompliancePercent);
            ws.Cell(r, 6).Value = p.ResolutionMet;
            ws.Cell(r, 7).Value = p.ResolutionBreached;
            SetExcelValue(ws.Cell(r, 8), p.ResolutionCompliancePercent);
            r++;
        }

        ws.ColumnsUsed().AdjustToContents();

        var bytes = ToByteArray(workbook);
        var fileName = $"ehc-sla-compliance-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("export/escalations.xlsx")]
    public async Task<IActionResult> ExportEscalationsExcel([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var rows = await BuildEscalationsAsync(tenantId, days, cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Escalations");

        var headers = new[] { "Policy", "Trigger", "Level", "Count" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.PolicyName;
            ws.Cell(r, 2).Value = row.Trigger;
            ws.Cell(r, 3).Value = row.Level;
            ws.Cell(r, 4).Value = row.Count;
            r++;
        }

        ws.ColumnsUsed().AdjustToContents();

        var bytes = ToByteArray(workbook);
        var fileName = $"ehc-escalations-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("export/feedback-summary.xlsx")]
    public async Task<IActionResult> ExportFeedbackSummaryExcel([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var summary = await BuildFeedbackSummaryAsync(tenantId, days, cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Feedback Summary");

        ws.Cell(1, 1).Value = "Days";
        ws.Cell(1, 2).Value = days;
        ws.Cell(2, 1).Value = "Feedback Count";
        ws.Cell(2, 2).Value = summary.FeedbackCount;
        ws.Cell(3, 1).Value = "Average Rating";
        SetExcelValue(ws.Cell(3, 2), summary.AvgRating);
        ws.Cell(4, 1).Value = "Resolved/Closed Tickets";
        ws.Cell(4, 2).Value = summary.ResolvedOrClosedTickets;
        ws.Cell(5, 1).Value = "Response Rate (%)";
        SetExcelValue(ws.Cell(5, 2), summary.ResponseRatePercent);

        ws.Cell(7, 1).Value = "Rating";
        ws.Cell(7, 2).Value = "Count";
        ws.Range(7, 1, 7, 2).Style.Font.Bold = true;

        var r = 8;
        foreach (var kv in summary.RatingDistribution.OrderBy(x => x.Key))
        {
            ws.Cell(r, 1).Value = kv.Key;
            ws.Cell(r, 2).Value = kv.Value;
            r++;
        }

        ws.ColumnsUsed().AdjustToContents();

        var bytes = ToByteArray(workbook);
        var fileName = $"ehc-feedback-summary-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("export/feedback-by-agent.xlsx")]
    public async Task<IActionResult> ExportFeedbackByAgentExcel([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var rows = await BuildFeedbackByAgentAsync(tenantId, days, cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Feedback By Agent");

        var headers = new[] { "Agent", "Feedback Count", "Avg Rating" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.AgentName;
            ws.Cell(r, 2).Value = row.FeedbackCount;
            SetExcelValue(ws.Cell(r, 3), row.AvgRating);
            r++;
        }

        ws.ColumnsUsed().AdjustToContents();

        var bytes = ToByteArray(workbook);
        var fileName = $"ehc-feedback-by-agent-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("export/feedback-by-department.xlsx")]
    public async Task<IActionResult> ExportFeedbackByDepartmentExcel([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var rows = await BuildFeedbackByDepartmentAsync(tenantId, days, cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Feedback By Department");

        var headers = new[] { "Department", "Feedback Count", "Avg Rating" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.DepartmentName;
            ws.Cell(r, 2).Value = row.FeedbackCount;
            SetExcelValue(ws.Cell(r, 3), row.AvgRating);
            r++;
        }

        ws.ColumnsUsed().AdjustToContents();

        var bytes = ToByteArray(workbook);
        var fileName = $"ehc-feedback-by-department-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("export/feedback-summary.pdf")]
    public async Task<IActionResult> ExportFeedbackSummaryPdf([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var summary = await BuildFeedbackSummaryAsync(tenantId, days, cancellationToken);

        QuestPDF.Settings.License = LicenseType.Community;

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("CSAT Summary").FontSize(18).SemiBold();
                        col.Item().Text($"Range: last {days} days • Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Medium);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Item().PaddingBottom(6).Text("Totals").FontSize(12).SemiBold();

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                        });

                        PdfKeyValueRow(t, "Feedback count", summary.FeedbackCount.ToString("N0"));
                        PdfKeyValueRow(t, "Average rating", summary.AvgRating?.ToString("0.00") ?? "—");
                        PdfKeyValueRow(t, "Resolved/Closed tickets", summary.ResolvedOrClosedTickets.ToString("N0"));
                        PdfKeyValueRow(t, "Response rate (%)", summary.ResponseRatePercent?.ToString("0.00") ?? "—");
                    });

                    col.Item().PaddingTop(12).PaddingBottom(6).Text("Rating Distribution").FontSize(12).SemiBold();
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(80);
                            cols.RelativeColumn();
                        });

                        PdfTableHeader(t, "Rating", "Count");
                        foreach (var kv in summary.RatingDistribution.OrderBy(x => x.Key))
                        {
                            PdfTableRow(t, kv.Key.ToString(), kv.Value.ToString("N0"));
                        }
                    });
                });
            });
        }).GeneratePdf();

        var fileName = $"ehc-feedback-summary-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet("export/feedback-by-agent.pdf")]
    public async Task<IActionResult> ExportFeedbackByAgentPdf([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var rows = await BuildFeedbackByAgentAsync(tenantId, days, cancellationToken);

        QuestPDF.Settings.License = LicenseType.Community;

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("CSAT by Agent").FontSize(18).SemiBold();
                        col.Item().Text($"Range: last {days} days • Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Medium);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.ConstantColumn(90);
                            cols.ConstantColumn(90);
                        });

                        PdfTableHeader(t, "Agent", "Responses", "Avg Rating");
                        foreach (var r in rows)
                        {
                            PdfTableRow(t, r.AgentName, r.FeedbackCount.ToString("N0"), r.AvgRating?.ToString("0.00") ?? "—");
                        }
                    });
                });
            });
        }).GeneratePdf();

        var fileName = $"ehc-feedback-by-agent-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet("export/feedback-by-department.pdf")]
    public async Task<IActionResult> ExportFeedbackByDepartmentPdf([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var rows = await BuildFeedbackByDepartmentAsync(tenantId, days, cancellationToken);

        QuestPDF.Settings.License = LicenseType.Community;

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("CSAT by Department").FontSize(18).SemiBold();
                        col.Item().Text($"Range: last {days} days • Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Medium);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.ConstantColumn(90);
                            cols.ConstantColumn(90);
                        });

                        PdfTableHeader(t, "Department", "Responses", "Avg Rating");
                        foreach (var r in rows)
                        {
                            PdfTableRow(t, r.DepartmentName, r.FeedbackCount.ToString("N0"), r.AvgRating?.ToString("0.00") ?? "—");
                        }
                    });
                });
            });
        }).GeneratePdf();

        var fileName = $"ehc-feedback-by-department-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet("export/summary.pdf")]
    public async Task<IActionResult> ExportSummaryPdf(CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var summary = await BuildSummaryAsync(tenantId, cancellationToken);

            QuestPDF.Settings.License = LicenseType.Community;

            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(24);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Helpdesk Summary").FontSize(18).SemiBold();
                            col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Medium);
                        });
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingBottom(6).Text("Totals").FontSize(12).SemiBold();

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                            });

                            PdfKeyValueRow(t, "Total tickets", summary.Totals.Total.ToString("N0"));
                            PdfKeyValueRow(t, "Open tickets", summary.Totals.Open.ToString("N0"));
                            PdfKeyValueRow(t, "First response breaches", summary.Totals.FirstResponseBreaches.ToString("N0"));
                            PdfKeyValueRow(t, "Resolution breaches", summary.Totals.ResolutionBreaches.ToString("N0"));
                            PdfKeyValueRow(t, "Avg first response (min)", summary.Totals.AvgFirstResponseMinutes?.ToString() ?? "—");
                            PdfKeyValueRow(t, "Avg resolution (min)", summary.Totals.AvgResolutionMinutes?.ToString() ?? "—");
                        });

                        col.Item().PaddingTop(12).PaddingBottom(6).Text("By Status").FontSize(12).SemiBold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.ConstantColumn(90);
                            });
                            PdfTableHeader(t, "Status", "Count");
                            foreach (var r in summary.ByStatus)
                                PdfTableRow(t, r.Status.ToString(), r.Count.ToString("N0"));
                        });

                        col.Item().PaddingTop(10).PaddingBottom(6).Text("By Priority").FontSize(12).SemiBold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.ConstantColumn(90);
                            });
                            PdfTableHeader(t, "Priority", "Count");
                            foreach (var r in summary.ByPriority)
                                PdfTableRow(t, r.Priority.ToString(), r.Count.ToString("N0"));
                        });

                        col.Item().PaddingTop(10).PaddingBottom(6).Text("By Department").FontSize(12).SemiBold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.ConstantColumn(90);
                            });
                            PdfTableHeader(t, "Department", "Count");
                            foreach (var r in summary.ByDepartment)
                                PdfTableRow(t, r.DepartmentName, r.Count.ToString("N0"));
                        });

                        col.Item().PaddingTop(10).PaddingBottom(6).Text("Top Categories").FontSize(12).SemiBold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.ConstantColumn(90);
                            });
                            PdfTableHeader(t, "Category", "Count");
                            foreach (var r in summary.ByCategory)
                                PdfTableRow(t, r.CategoryName, r.Count.ToString("N0"));
                        });

                        if (summary.ByRootCause.Count > 0)
                        {
                            col.Item().PaddingTop(10).PaddingBottom(6).Text("Top Root Causes").FontSize(12).SemiBold();
                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn();
                                    cols.ConstantColumn(90);
                                });
                                PdfTableHeader(t, "Root cause", "Count");
                                foreach (var r in summary.ByRootCause)
                                    PdfTableRow(t, r.RootCauseName, r.Count.ToString("N0"));
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("ERP Helpdesk Report").FontColor(Colors.Grey.Medium);
                    });
                });
            }).GeneratePdf();

            var fileName = $"ehc-summary-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf";
            return File(bytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export EHC summary PDF");
            return StatusCode(500, new { success = false, message = "Failed to export PDF" });
        }
    }

    [HttpGet("export/summary.xlsx")]
    public async Task<IActionResult> ExportSummaryExcel(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var summary = await BuildSummaryAsync(tenantId, cancellationToken);

        using var workbook = new XLWorkbook();

        var totals = workbook.Worksheets.Add("Totals");
        totals.Cell(1, 1).Value = "Metric";
        totals.Cell(1, 2).Value = "Value";
        totals.Range(1, 1, 1, 2).Style.Font.Bold = true;
        var r = 2;
        void Row(string key, object? value)
        {
            totals.Cell(r, 1).Value = key;
            totals.Cell(r, 2).Value = value?.ToString() ?? "—";
            r++;
        }

        Row("Total tickets", summary.Totals.Total);
        Row("Open tickets", summary.Totals.Open);
        Row("First response breaches", summary.Totals.FirstResponseBreaches);
        Row("Resolution breaches", summary.Totals.ResolutionBreaches);
        Row("Avg first response (min)", summary.Totals.AvgFirstResponseMinutes);
        Row("Avg resolution (min)", summary.Totals.AvgResolutionMinutes);
        totals.ColumnsUsed().AdjustToContents();

        void AddCountSheet<T>(string sheetName, string leftHeader, IEnumerable<T> rows, Func<T, string> leftValue, Func<T, int> countValue)
        {
            var ws = workbook.Worksheets.Add(sheetName);
            ws.Cell(1, 1).Value = leftHeader;
            ws.Cell(1, 2).Value = "Count";
            ws.Range(1, 1, 1, 2).Style.Font.Bold = true;

            var rr = 2;
            foreach (var row in rows)
            {
                ws.Cell(rr, 1).Value = leftValue(row);
                ws.Cell(rr, 2).Value = countValue(row);
                rr++;
            }

            ws.ColumnsUsed().AdjustToContents();
        }

        AddCountSheet("By Status", "Status", summary.ByStatus, x => x.Status.ToString(), x => x.Count);
        AddCountSheet("By Priority", "Priority", summary.ByPriority, x => x.Priority.ToString(), x => x.Count);
        AddCountSheet("By Type", "Type", summary.ByType, x => x.TicketType.ToString(), x => x.Count);
        AddCountSheet("By Department", "Department", summary.ByDepartment, x => x.DepartmentName, x => x.Count);
        AddCountSheet("Top Categories", "Category", summary.ByCategory, x => x.CategoryName, x => x.Count);
        AddCountSheet("Top Root Causes", "Root Cause", summary.ByRootCause, x => x.RootCauseName, x => x.Count);

        var bytes = ToByteArray(workbook);
        var fileName = $"ehc-summary-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("export/agent-performance.pdf")]
    public async Task<IActionResult> ExportAgentPerformancePdf([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var rows = await BuildAgentPerformanceAsync(tenantId, days, cancellationToken);

            QuestPDF.Settings.License = LicenseType.Community;
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(24);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Agent Performance").FontSize(18).SemiBold();
                        col.Item().Text($"Last {ClampDays(days)} days • Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Medium);
                    });

                    page.Content().Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.ConstantColumn(60);
                            cols.ConstantColumn(50);
                            cols.ConstantColumn(60);
                            cols.ConstantColumn(70);
                            cols.ConstantColumn(70);
                            cols.ConstantColumn(70);
                            cols.ConstantColumn(70);
                        });

                        PdfTableHeader(t, "Agent", "Assigned", "Open", "Resolved", "FR Breach", "Res Breach", "Avg FR", "Avg Res");
                        foreach (var r in rows)
                        {
                            PdfTableRow(
                                t,
                                r.AgentName,
                                r.TotalAssigned.ToString("N0"),
                                r.OpenAssigned.ToString("N0"),
                                r.ResolvedAssigned.ToString("N0"),
                                r.FirstResponseBreaches.ToString("N0"),
                                r.ResolutionBreaches.ToString("N0"),
                                r.AvgFirstResponseMinutes?.ToString() ?? "—",
                                r.AvgResolutionMinutes?.ToString() ?? "—");
                        }
                    });
                });
            }).GeneratePdf();

            var fileName = $"ehc-agent-performance-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf";
            return File(bytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export EHC agent performance PDF");
            return StatusCode(500, new { success = false, message = "Failed to export PDF" });
        }
    }

    [HttpGet("export/sla-compliance.pdf")]
    public async Task<IActionResult> ExportSlaCompliancePdf([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var points = await BuildSlaComplianceAsync(tenantId, days, cancellationToken);

            QuestPDF.Settings.License = LicenseType.Community;
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(24);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("SLA Compliance").FontSize(18).SemiBold();
                        col.Item().Text($"Last {ClampDays(days)} days • Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Medium);
                    });

                    page.Content().Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(80);
                            cols.ConstantColumn(60);
                            cols.ConstantColumn(60);
                            cols.ConstantColumn(70);
                            cols.ConstantColumn(60);
                            cols.ConstantColumn(60);
                            cols.ConstantColumn(70);
                        });

                        PdfTableHeader(t, "Date", "FR Met", "FR Br", "FR %", "Res Met", "Res Br", "Res %");
                        foreach (var p in points)
                        {
                            PdfTableRow(
                                t,
                                p.Date,
                                p.FirstResponseMet.ToString("N0"),
                                p.FirstResponseBreached.ToString("N0"),
                                p.FirstResponseCompliancePercent?.ToString("0.0") ?? "—",
                                p.ResolutionMet.ToString("N0"),
                                p.ResolutionBreached.ToString("N0"),
                                p.ResolutionCompliancePercent?.ToString("0.0") ?? "—");
                        }
                    });
                });
            }).GeneratePdf();

            var fileName = $"ehc-sla-compliance-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf";
            return File(bytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export EHC SLA compliance PDF");
            return StatusCode(500, new { success = false, message = "Failed to export PDF" });
        }
    }

    [HttpGet("export/escalations.pdf")]
    public async Task<IActionResult> ExportEscalationsPdf([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrEmpty();
            var rows = await BuildEscalationsAsync(tenantId, days, cancellationToken);

            QuestPDF.Settings.License = LicenseType.Community;
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(24);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Escalations").FontSize(18).SemiBold();
                        col.Item().Text($"Last {ClampDays(days)} days • Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontColor(Colors.Grey.Medium);
                    });

                    page.Content().Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.ConstantColumn(100);
                            cols.ConstantColumn(60);
                            cols.ConstantColumn(70);
                        });

                        PdfTableHeader(t, "Policy", "Trigger", "Level", "Count");
                        foreach (var r in rows)
                        {
                            PdfTableRow(t, r.PolicyName, r.Trigger, r.Level.ToString(), r.Count.ToString("N0"));
                        }
                    });
                });
            }).GeneratePdf();

            var fileName = $"ehc-escalations-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf";
            return File(bytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export EHC escalations PDF");
            return StatusCode(500, new { success = false, message = "Failed to export PDF" });
        }
    }

    private static byte[] ToByteArray(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetExcelValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case int number:
                cell.Value = number;
                return;
            case double number:
                cell.Value = number;
                return;
            case decimal number:
                cell.Value = number;
                return;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                return;
        }
    }
}

