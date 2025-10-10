using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces;

public interface IReportRepository : IGenericRepository<Report>
{
    Task<IEnumerable<Report>> GetReportsByTenantAsync(Guid tenantId, string? type = null, string? status = null);
    Task<IEnumerable<Report>> GetFavoriteReportsByUserAsync(Guid userId, Guid tenantId);
    Task<Report?> GetReportWithDetailsAsync(Guid reportId, Guid tenantId);
    Task<bool> IsReportFavoriteAsync(Guid reportId, Guid userId, Guid tenantId);
    Task<IEnumerable<Report>> GetScheduledReportsAsync(Guid tenantId);
    Task UpdateLastRunAsync(Guid reportId, DateTime lastRun, DateTime? nextRun = null);
}

public interface IReportScheduleRepository : IGenericRepository<ReportSchedule>
{
    Task<IEnumerable<ReportSchedule>> GetSchedulesByReportAsync(Guid reportId, Guid tenantId);
    Task<IEnumerable<ReportSchedule>> GetActiveSchedulesAsync(Guid tenantId);
    Task<IEnumerable<ReportSchedule>> GetSchedulesDueForExecutionAsync();
}

public interface IReportTemplateRepository : IGenericRepository<ReportTemplate>
{
    Task<IEnumerable<ReportTemplate>> GetTemplatesByTenantAsync(Guid tenantId, string? category = null);
    Task<IEnumerable<ReportTemplate>> GetPopularTemplatesAsync(Guid tenantId, int limit = 10);
    Task IncrementUsageCountAsync(Guid templateId);
}

public interface IReportExecutionRepository : IGenericRepository<ReportExecution>
{
    Task<IEnumerable<ReportExecution>> GetExecutionsByReportAsync(Guid reportId, Guid tenantId, int limit = 50);
    Task<IEnumerable<ReportExecution>> GetExecutionsByUserAsync(Guid userId, Guid tenantId, int limit = 50);
    Task<IEnumerable<ReportExecution>> GetRecentExecutionsAsync(Guid tenantId, int limit = 20);
    Task<ReportExecution?> GetLatestExecutionAsync(Guid reportId, Guid tenantId);
}

public interface IUserReportFavoriteRepository : IGenericRepository<UserReportFavorite>
{
    Task<UserReportFavorite?> GetFavoriteAsync(Guid reportId, Guid userId, Guid tenantId);
    Task<IEnumerable<UserReportFavorite>> GetUserFavoritesAsync(Guid userId, Guid tenantId);
    Task<bool> ToggleFavoriteAsync(Guid reportId, Guid userId, Guid tenantId);
}

public interface IReportExportRepository : IGenericRepository<ReportExport>
{
    Task<IEnumerable<ReportExport>> GetExportsByUserAsync(Guid userId, Guid tenantId, int limit = 50);
    Task<IEnumerable<ReportExport>> GetExportsByReportAsync(Guid reportId, Guid tenantId, int limit = 50);
    Task<long> GetTotalExportsSizeByUserAsync(Guid userId, Guid tenantId);
}

public interface IReportRoleAssignmentRepository : IGenericRepository<ReportRoleAssignment>
{
    Task<IEnumerable<ReportRoleAssignment>> GetAssignmentsByReportAsync(Guid reportId, Guid tenantId);
    Task<IEnumerable<ReportRoleAssignment>> GetAssignmentsByRoleAsync(Guid roleId, Guid tenantId);
    Task<ReportRoleAssignment?> GetAssignmentAsync(Guid reportId, Guid roleId, Guid tenantId);
    Task<IEnumerable<Guid>> GetAccessibleReportIdsForUserAsync(Guid userId, Guid tenantId);
    Task<bool> HasReportAccessAsync(Guid reportId, Guid userId, Guid tenantId, string permission);
    Task<bool> RemoveAssignmentsForReportAsync(Guid reportId, Guid tenantId);
    Task<bool> RemoveAssignmentsForRoleAsync(Guid roleId, Guid tenantId);
}
