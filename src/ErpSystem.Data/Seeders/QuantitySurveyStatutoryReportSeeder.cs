using System.Text.Json;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services.QuantitySurvey;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public sealed class QuantitySurveyStatutoryReportSeeder(
    ApplicationDbContext context,
    ILogger<QuantitySurveyStatutoryReportSeeder> logger)
{
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await context.Tenants.AsNoTracking().Where(value => !value.IsDeleted)
            .Select(value => value.Id).ToListAsync(cancellationToken);
        var changed = 0;
        foreach (var tenantId in tenantIds) changed += await SeedTenantAsync(tenantId, cancellationToken);
        return changed;
    }

    public async Task<int> SeedTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var moduleId = await context.TenantModules.IgnoreQueryFilters().AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                (value.ModuleName == "Project Management" || value.ModuleName == "Development" || value.ModuleName == "Projects"))
            .OrderBy(value => value.ModuleName == "Project Management" ? 0 : value.ModuleName == "Development" ? 1 : 2)
            .Select(value => (Guid?)value.Id).FirstOrDefaultAsync(cancellationToken);
        var queries = QuantitySurveyStatutoryReportCatalogue.Definitions.Select(value => value.Query).ToList();
        var existing = await context.Reports.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenantId && value.Query != null && queries.Contains(value.Query))
            .ToDictionaryAsync(value => value.Query!, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var now = DateTime.UtcNow;
        var changed = 0;

        foreach (var definition in QuantitySurveyStatutoryReportCatalogue.Definitions)
        {
            var parameters = JsonSerializer.Serialize(QuantitySurveyStatutoryReportCatalogue.BuildParameters());
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
                report = new Report { Id = Guid.NewGuid(), TenantId = tenantId, Query = definition.Query, CreatedAt = now, CreatedBy = "System" };
                context.Reports.Add(report);
                isNew = true;
            }

            var needsRepair = isNew || report.Name != definition.Name || report.Description != definition.Description ||
                report.Type != QuantitySurveyStatutoryReportCatalogue.ReportType || report.Status != "published" ||
                report.Parameters != parameters || report.Columns != columns || report.Visualization != visualization ||
                report.Tags != tags || report.IsScheduled || report.ModuleId != moduleId || report.IsDeleted ||
                report.DeletedAt.HasValue || report.DeletedBy != null;
            if (!needsRepair) continue;

            report.Name = definition.Name;
            report.Description = definition.Description;
            report.Type = QuantitySurveyStatutoryReportCatalogue.ReportType;
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

        if (changed > 0) await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Ensured {ReportCount} TDC Quantity Survey reports for tenant {TenantId}.",
            QuantitySurveyStatutoryReportCatalogue.Definitions.Count, tenantId);
        return changed;
    }
}
