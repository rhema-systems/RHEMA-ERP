using ErpSystem.Core.DTOs.Reports;

namespace ErpSystem.Core.Services
{
    public interface IReportsService
    {
        Task<List<ReportDefinitionDto>> GetReportsAsync(Guid tenantId, Guid userId, string? type = null, string? status = null, bool? favoriteOnly = null);
        Task<ReportDefinitionDto?> GetReportAsync(Guid reportId, Guid tenantId);
        Task<ReportDefinitionDto> CreateReportAsync(CreateReportDto createReportDto, Guid tenantId, Guid userId);
        Task<ReportDefinitionDto?> UpdateReportAsync(Guid reportId, UpdateReportDto updateReportDto, Guid tenantId, Guid userId);
        Task<bool> DeleteReportAsync(Guid reportId, Guid tenantId, Guid userId);
        
        Task<ReportResultDto> ExecuteReportAsync(Guid reportId, ExecuteReportDto executeReportDto, Guid tenantId, Guid userId);
        Task<ReportExportResultDto> ExportReportAsync(Guid reportId, ExportReportDto exportReportDto, Guid tenantId, Guid userId);
        
        Task<ReportScheduleDto> ScheduleReportAsync(Guid reportId, CreateReportScheduleDto scheduleDto, Guid tenantId, Guid userId);
        Task<ReportScheduleDto?> GetReportScheduleAsync(Guid scheduleId, Guid tenantId);
        Task<bool> DeleteReportScheduleAsync(Guid scheduleId, Guid tenantId, Guid userId);
        
        Task<List<ReportTemplateDto>> GetReportTemplatesAsync(Guid tenantId, string? category = null);
        Task<ReportAnalyticsDto> GetReportAnalyticsAsync(Guid tenantId, string period, string? tenantFilter = null, bool isSuperAdmin = false);
        Task<bool> ToggleFavoriteAsync(Guid reportId, Guid tenantId, Guid userId);
    }
}