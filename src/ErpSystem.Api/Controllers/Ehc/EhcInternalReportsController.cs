using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
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

    [HttpGet("summary")]
    public async Task<ActionResult> GetSummary(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return Ok(new { success = true, data = new { } });
            }

            var now = DateTime.UtcNow;
            var baseQ = _db.EhcTickets
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId && !t.IsDeleted);

            var openQ = baseQ
                .Where(t => t.Status != EhcTicketStatus.Closed && t.Status != EhcTicketStatus.Resolved);

            var total = await baseQ.CountAsync(cancellationToken);
            var open = await openQ.CountAsync(cancellationToken);

            var firstResponseBreaches = await openQ
                .Where(t => t.FirstRespondedAt == null && t.FirstResponseDueAt.HasValue && t.FirstResponseDueAt.Value <= now)
                .CountAsync(cancellationToken);

            var resolutionBreaches = await openQ
                .Where(t => t.ResolvedAt == null && t.ResolutionDueAt.HasValue && t.ResolutionDueAt.Value <= now)
                .CountAsync(cancellationToken);

            var byStatus = await baseQ
                .GroupBy(t => t.Status)
                .Select(g => new { status = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToListAsync(cancellationToken);

            var byPriority = await baseQ
                .GroupBy(t => t.Priority)
                .Select(g => new { priority = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToListAsync(cancellationToken);

            var byType = await baseQ
                .GroupBy(t => t.TicketType)
                .Select(g => new { ticketType = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToListAsync(cancellationToken);

            var byDepartment = await baseQ
                .GroupBy(t => t.AssignedDepartmentId)
                .Select(g => new { departmentId = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToListAsync(cancellationToken);

            var departmentIds = byDepartment.Where(x => x.departmentId.HasValue && x.departmentId.Value != Guid.Empty).Select(x => x.departmentId!.Value).ToList();
            var departmentNames = await _db.Departments
                .AsNoTracking()
                .Where(d => departmentIds.Contains(d.Id) && d.TenantId == tenantId && !d.IsDeleted)
                .Select(d => new { d.Id, d.Name })
                .ToListAsync(cancellationToken);

            var deptNameById = departmentNames.ToDictionary(x => x.Id, x => x.Name);
            var byDepartmentNamed = byDepartment.Select(x => new
            {
                departmentId = x.departmentId,
                departmentName = x.departmentId.HasValue && deptNameById.TryGetValue(x.departmentId.Value, out var name) ? name : "Unassigned",
                x.count
            }).ToList();

            var byCategory = await baseQ
                .GroupBy(t => t.CategoryId)
                .Select(g => new { categoryId = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToListAsync(cancellationToken);

            var categoryIds = byCategory.Where(x => x.categoryId.HasValue && x.categoryId.Value != Guid.Empty).Select(x => x.categoryId!.Value).ToList();
            var categoryNames = await _db.EhcTicketCategories
                .AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id) && c.TenantId == tenantId && !c.IsDeleted)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync(cancellationToken);

            var catNameById = categoryNames.ToDictionary(x => x.Id, x => x.Name);
            var byCategoryNamed = byCategory.Select(x => new
            {
                categoryId = x.categoryId,
                categoryName = x.categoryId.HasValue && catNameById.TryGetValue(x.categoryId.Value, out var name) ? name : "Uncategorized",
                x.count
            }).ToList();

            int? avgFirstResponseMinutes = null;
            int? avgResolutionMinutes = null;

            var firstResponseMinutes = await baseQ
                .Where(t => t.FirstRespondedAt.HasValue)
                .Select(t => EF.Functions.DateDiffMinute(t.CreatedAt, t.FirstRespondedAt!.Value))
                .ToListAsync(cancellationToken);

            if (firstResponseMinutes.Count > 0)
            {
                avgFirstResponseMinutes = (int)Math.Round(firstResponseMinutes.Average());
            }

            var resolutionMinutes = await baseQ
                .Where(t => t.ResolvedAt.HasValue)
                .Select(t => EF.Functions.DateDiffMinute(t.CreatedAt, t.ResolvedAt!.Value))
                .ToListAsync(cancellationToken);

            if (resolutionMinutes.Count > 0)
            {
                avgResolutionMinutes = (int)Math.Round(resolutionMinutes.Average());
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    totals = new
                    {
                        total,
                        open,
                        firstResponseBreaches,
                        resolutionBreaches,
                        avgFirstResponseMinutes,
                        avgResolutionMinutes
                    },
                    byStatus,
                    byPriority,
                    byType,
                    byDepartment = byDepartmentNamed,
                    byCategory = byCategoryNamed
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building EHC helpdesk summary report");
            return StatusCode(500, new { success = false, message = "Failed to load report" });
        }
    }
}

