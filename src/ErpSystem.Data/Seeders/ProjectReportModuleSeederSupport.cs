using ErpSystem.Core.Entities;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Shared tenant-module boundary for system report packs that belong to the Projects workspace.
/// A tenant that has not enabled that module must not receive orphaned Civil or QS report entries.
/// </summary>
internal static class ProjectReportModuleSeederSupport
{
    private const string ProjectManagementModule = "Project Management";
    private const string SeederActor = "System";

    public static Task<Guid?> ResolveModuleIdAsync(
        ApplicationDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        context.TenantModules.IgnoreQueryFilters().AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.Status == ModuleStatus.Enabled &&
                (value.ModuleName == ProjectManagementModule ||
                 value.ModuleName == "Development" ||
                 value.ModuleName == "Projects"))
            .OrderBy(value => value.ModuleName == ProjectManagementModule ? 0 :
                value.ModuleName == "Development" ? 1 : 2)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public static async Task<int> RetireUnassignedCatalogueAsync(
        ApplicationDbContext context,
        Guid tenantId,
        IReadOnlyCollection<string> catalogueQueries,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var reports = await context.Reports.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.Query != null && catalogueQueries.Contains(value.Query))
            .ToListAsync(cancellationToken);

        foreach (var report in reports)
        {
            report.IsDeleted = true;
            report.DeletedAt = now;
            report.DeletedBy = SeederActor;
            report.UpdatedAt = now;
            report.UpdatedBy = SeederActor;
        }

        if (reports.Count > 0)
            await context.SaveChangesAsync(cancellationToken);

        return reports.Count;
    }
}
