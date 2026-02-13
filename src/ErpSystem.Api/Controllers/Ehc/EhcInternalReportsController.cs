using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using OfficeOpenXml;
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

    private async Task<List<SlaCompliancePointDto>> BuildSlaComplianceAsync(Guid tenantId, int days, CancellationToken cancellationToken)
    {
        var points = new List<SlaCompliancePointDto>();
        if (tenantId == Guid.Empty) return points;

        days = ClampDays(days);
        var now = DateTime.UtcNow;
        var start = now.Date.AddDays(-(days - 1));

        var baseQ = _db.EhcTickets
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.CreatedAt >= start);

        var raw = await baseQ
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new
            {
                date = g.Key,
                total = g.Count(),
                frMet = g.Count(t => t.FirstResponseDueAt.HasValue &&
                                    t.FirstRespondedAt.HasValue &&
                                    t.FirstRespondedAt.Value <= t.FirstResponseDueAt.Value),
                frBreached = g.Count(t => t.FirstResponseDueAt.HasValue &&
                                         ((t.FirstRespondedAt.HasValue && t.FirstRespondedAt.Value > t.FirstResponseDueAt.Value) ||
                                          (!t.FirstRespondedAt.HasValue && t.FirstResponseDueAt.Value <= now))),
                resMet = g.Count(t => t.ResolutionDueAt.HasValue &&
                                     t.ResolvedAt.HasValue &&
                                     t.ResolvedAt.Value <= t.ResolutionDueAt.Value),
                resBreached = g.Count(t => t.ResolutionDueAt.HasValue &&
                                          ((t.ResolvedAt.HasValue && t.ResolvedAt.Value > t.ResolutionDueAt.Value) ||
                                           (!t.ResolvedAt.HasValue && t.ResolutionDueAt.Value <= now)))
            })
            .ToListAsync(cancellationToken);

        var byDate = raw.ToDictionary(x => x.date, x => x);

        points = new List<SlaCompliancePointDto>(days);
        for (var i = 0; i < days; i++)
        {
            var d = start.AddDays(i);
            if (!byDate.TryGetValue(d, out var x))
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

    [HttpGet("export/agent-performance.xlsx")]
    public async Task<IActionResult> ExportAgentPerformanceExcel([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var rows = await BuildAgentPerformanceAsync(tenantId, days, cancellationToken);

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Agent Performance");

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
            ws.Cells[1, c + 1].Value = headers[c];
            ws.Cells[1, c + 1].Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var row in rows)
        {
            ws.Cells[r, 1].Value = row.AgentName;
            ws.Cells[r, 2].Value = row.TotalAssigned;
            ws.Cells[r, 3].Value = row.OpenAssigned;
            ws.Cells[r, 4].Value = row.ResolvedAssigned;
            ws.Cells[r, 5].Value = row.FirstResponseBreaches;
            ws.Cells[r, 6].Value = row.ResolutionBreaches;
            ws.Cells[r, 7].Value = row.AvgFirstResponseMinutes;
            ws.Cells[r, 8].Value = row.AvgResolutionMinutes;
            r++;
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();

        var bytes = await package.GetAsByteArrayAsync(cancellationToken);
        var fileName = $"ehc-agent-performance-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("export/sla-compliance.xlsx")]
    public async Task<IActionResult> ExportSlaComplianceExcel([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var points = await BuildSlaComplianceAsync(tenantId, days, cancellationToken);

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("SLA Compliance");

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
            ws.Cells[1, c + 1].Value = headers[c];
            ws.Cells[1, c + 1].Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var p in points)
        {
            ws.Cells[r, 1].Value = p.Date;
            ws.Cells[r, 2].Value = p.TotalTickets;
            ws.Cells[r, 3].Value = p.FirstResponseMet;
            ws.Cells[r, 4].Value = p.FirstResponseBreached;
            ws.Cells[r, 5].Value = p.FirstResponseCompliancePercent;
            ws.Cells[r, 6].Value = p.ResolutionMet;
            ws.Cells[r, 7].Value = p.ResolutionBreached;
            ws.Cells[r, 8].Value = p.ResolutionCompliancePercent;
            r++;
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();

        var bytes = await package.GetAsByteArrayAsync(cancellationToken);
        var fileName = $"ehc-sla-compliance-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("export/escalations.xlsx")]
    public async Task<IActionResult> ExportEscalationsExcel([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantIdOrEmpty();
        var rows = await BuildEscalationsAsync(tenantId, days, cancellationToken);

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Escalations");

        var headers = new[] { "Policy", "Trigger", "Level", "Count" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cells[1, c + 1].Value = headers[c];
            ws.Cells[1, c + 1].Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var row in rows)
        {
            ws.Cells[r, 1].Value = row.PolicyName;
            ws.Cells[r, 2].Value = row.Trigger;
            ws.Cells[r, 3].Value = row.Level;
            ws.Cells[r, 4].Value = row.Count;
            r++;
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();

        var bytes = await package.GetAsByteArrayAsync(cancellationToken);
        var fileName = $"ehc-escalations-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage();

        var totals = package.Workbook.Worksheets.Add("Totals");
        totals.Cells[1, 1].Value = "Metric";
        totals.Cells[1, 2].Value = "Value";
        totals.Cells[1, 1, 1, 2].Style.Font.Bold = true;
        var r = 2;
        void Row(string key, object? value)
        {
            totals.Cells[r, 1].Value = key;
            totals.Cells[r, 2].Value = value?.ToString() ?? "—";
            r++;
        }

        Row("Total tickets", summary.Totals.Total);
        Row("Open tickets", summary.Totals.Open);
        Row("First response breaches", summary.Totals.FirstResponseBreaches);
        Row("Resolution breaches", summary.Totals.ResolutionBreaches);
        Row("Avg first response (min)", summary.Totals.AvgFirstResponseMinutes);
        Row("Avg resolution (min)", summary.Totals.AvgResolutionMinutes);
        totals.Cells[totals.Dimension.Address].AutoFitColumns();

        void AddCountSheet<T>(string sheetName, string leftHeader, IEnumerable<T> rows, Func<T, string> leftValue, Func<T, int> countValue)
        {
            var ws = package.Workbook.Worksheets.Add(sheetName);
            ws.Cells[1, 1].Value = leftHeader;
            ws.Cells[1, 2].Value = "Count";
            ws.Cells[1, 1, 1, 2].Style.Font.Bold = true;

            var rr = 2;
            foreach (var row in rows)
            {
                ws.Cells[rr, 1].Value = leftValue(row);
                ws.Cells[rr, 2].Value = countValue(row);
                rr++;
            }

            ws.Cells[ws.Dimension.Address].AutoFitColumns();
        }

        AddCountSheet("By Status", "Status", summary.ByStatus, x => x.Status.ToString(), x => x.Count);
        AddCountSheet("By Priority", "Priority", summary.ByPriority, x => x.Priority.ToString(), x => x.Count);
        AddCountSheet("By Type", "Type", summary.ByType, x => x.TicketType.ToString(), x => x.Count);
        AddCountSheet("By Department", "Department", summary.ByDepartment, x => x.DepartmentName, x => x.Count);
        AddCountSheet("Top Categories", "Category", summary.ByCategory, x => x.CategoryName, x => x.Count);
        AddCountSheet("Top Root Causes", "Root Cause", summary.ByRootCause, x => x.RootCauseName, x => x.Count);

        var bytes = await package.GetAsByteArrayAsync(cancellationToken);
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
}

