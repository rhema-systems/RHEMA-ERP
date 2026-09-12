using System.Text.Json;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Publishes the HR awards system reports into each tenant's <c>Reports</c> table (FR-HR-113).
/// </summary>
/// <remarks>
/// <para>Cloned from <c>ProcurementStatutoryReportSeeder</c>, which is the established shape for an
/// application-owned report: the definition lives in code, the row exists so the reports screen can
/// list and schedule it, and the row is <b>repaired</b> rather than duplicated when the definition
/// changes. Editing the row in the database is therefore pointless — the next run puts it back.</para>
///
/// <para>⚠ The module lookup is <c>"HR"</c>, which is the name the tenant-module seed actually
/// writes. It is <c>SingleOrDefault</c>, so a tenant with no HR module row gets a report with a null
/// <c>ModuleId</c> rather than a failed seed — the report still works, it just does not sit under a
/// module heading.</para>
/// </remarks>
public sealed class HrAwardsReportSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HrAwardsReportSeeder> _logger;

    public HrAwardsReportSeeder(
        ApplicationDbContext context,
        ILogger<HrAwardsReportSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await _context.Tenants.AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var changed = 0;
        foreach (var tenantId in tenantIds)
            changed += await SeedTenantAsync(tenantId, cancellationToken);
        return changed;
    }

    public async Task<int> SeedTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var moduleId = await _context.TenantModules.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ModuleName == "HR")
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var queries = HrAwardsReportCatalogue.Definitions.Select(item => item.Query).ToList();
        var existing = await _context.Reports.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Query != null && queries.Contains(item.Query))
            .ToDictionaryAsync(item => item.Query!, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var now = DateTime.UtcNow;
        var changed = 0;

        foreach (var definition in HrAwardsReportCatalogue.Definitions)
        {
            var parameters = JsonSerializer.Serialize(HrAwardsReportCatalogue.BuildParameters());
            var columns = JsonSerializer.Serialize(definition.Columns.Select((column, index) => new ReportColumnDto
            {
                Name = column.Name,
                DisplayName = column.DisplayName,
                DataType = column.DataType,
                Format = column.Format,
                IsVisible = column.IsVisible,
                Order = index,
                AggregationType = column.AggregationType
            }));
            var visualization = JsonSerializer.Serialize(new ReportVisualizationDto { Type = "Table" });
            var tags = JsonSerializer.Serialize(definition.Tags);
            var isNew = false;
            if (!existing.TryGetValue(definition.Query, out var report))
            {
                report = new Report
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Query = definition.Query,
                    CreatedAt = now,
                    CreatedBy = "System"
                };
                _context.Reports.Add(report);
                isNew = true;
            }

            var needsRepair = isNew ||
                              report.Name != definition.Name ||
                              report.Description != definition.Description ||
                              report.Type != HrAwardsReportCatalogue.ReportType ||
                              report.Status != "published" ||
                              report.Parameters != parameters ||
                              report.Columns != columns ||
                              report.Visualization != visualization ||
                              report.Tags != tags ||
                              report.IsScheduled ||
                              report.ModuleId != moduleId ||
                              report.IsDeleted ||
                              report.DeletedAt.HasValue ||
                              report.DeletedBy != null;
            if (!needsRepair) continue;

            report.Name = definition.Name;
            report.Description = definition.Description;
            report.Type = HrAwardsReportCatalogue.ReportType;
            report.Status = "published";
            report.Parameters = parameters;
            report.Columns = columns;
            report.Visualization = visualization;
            report.Tags = tags;
            report.IsScheduled = false;
            report.ModuleId = moduleId;
            report.IsDeleted = false;
            report.DeletedAt = null;
            report.DeletedBy = null;
            report.UpdatedAt = now;
            report.UpdatedBy = "System";
            changed++;
        }

        if (changed > 0)
            await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Ensured {ReportCount} HR awards system reports for tenant {TenantId}.",
            HrAwardsReportCatalogue.Definitions.Count, tenantId);
        return changed;
    }
}
