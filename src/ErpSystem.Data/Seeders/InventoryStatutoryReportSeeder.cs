using System.Text.Json;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public sealed class InventoryStatutoryReportSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InventoryStatutoryReportSeeder> _logger;

    public InventoryStatutoryReportSeeder(
        ApplicationDbContext context,
        ILogger<InventoryStatutoryReportSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await _context.Tenants.AsNoTracking().Where(item => !item.IsDeleted)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        var changed = 0;
        foreach (var tenantId in tenantIds) changed += await SeedTenantAsync(tenantId, cancellationToken);
        return changed;
    }

    public async Task<int> SeedTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var moduleId = await _context.TenantModules.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ModuleName == "Inventory")
            .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
        var queries = InventoryStatutoryReportCatalogue.Definitions.Select(item => item.Query).ToList();
        var existing = await _context.Reports.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Query != null && queries.Contains(item.Query))
            .ToDictionaryAsync(item => item.Query!, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var now = DateTime.UtcNow;
        var changed = 0;

        foreach (var definition in InventoryStatutoryReportCatalogue.Definitions)
        {
            var parameters = JsonSerializer.Serialize(InventoryStatutoryReportCatalogue.BuildParameters());
            var columns = JsonSerializer.Serialize(definition.Columns.Select((column, index) => new ReportColumnDto
            {
                Name = column.Name, DisplayName = column.DisplayName, DataType = column.DataType,
                Format = column.Format, IsVisible = column.IsVisible, Order = index,
                AggregationType = column.AggregationType
            }));
            var visualization = JsonSerializer.Serialize(new ReportVisualizationDto { Type = "Table" });
            var tags = JsonSerializer.Serialize(definition.Tags);
            var isNew = false;
            if (!existing.TryGetValue(definition.Query, out var report))
            {
                report = new Report
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, Query = definition.Query,
                    CreatedAt = now, CreatedBy = "System"
                };
                _context.Reports.Add(report);
                isNew = true;
            }

            var needsRepair = isNew || report.Name != definition.Name || report.Description != definition.Description ||
                              report.Type != InventoryStatutoryReportCatalogue.ReportType || report.Status != "published" ||
                              report.Parameters != parameters || report.Columns != columns ||
                              report.Visualization != visualization || report.Tags != tags || report.IsScheduled ||
                              report.ModuleId != moduleId || report.IsDeleted || report.DeletedAt.HasValue || report.DeletedBy != null;
            if (!needsRepair) continue;

            report.Name = definition.Name;
            report.Description = definition.Description;
            report.Type = InventoryStatutoryReportCatalogue.ReportType;
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

        if (changed > 0) await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Ensured {ReportCount} TDC inventory statutory reports for tenant {TenantId}.",
            InventoryStatutoryReportCatalogue.Definitions.Count, tenantId);
        return changed;
    }
}
