using ErpSystem.Core.DTOs.Reports;

namespace ErpSystem.Core.Interfaces;

public interface IReportTemplateLifecycleService
{
    Task<IReadOnlyList<ReportTemplateDto>> GetAsync(Guid tenantId, Guid userId, bool isAdministrator,
        string? audience = null, string? cadence = null, string? status = null, CancellationToken cancellationToken = default);
    Task<ReportTemplateDto?> GetByIdAsync(Guid templateId, Guid tenantId, Guid userId, bool isAdministrator,
        CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> CreateAsync(CreateReportTemplateDto request, Guid tenantId, Guid userId, bool isAdministrator,
        CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> UpdateAsync(Guid templateId, UpdateReportTemplateDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> PublishAsync(Guid templateId, ReportTemplateLifecycleActionDto request, Guid tenantId,
        Guid userId, bool isAdministrator, CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> ArchiveAsync(Guid templateId, ReportTemplateLifecycleActionDto request, Guid tenantId,
        Guid userId, bool isAdministrator, CancellationToken cancellationToken = default);
    Task<ReportTemplateDto> CloneAsync(Guid templateId, CloneReportTemplateDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid templateId, ReportTemplateLifecycleActionDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default);
    Task<ReportResultDto> ExecuteAsync(Guid templateId, GenerateReportTemplateDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default);
    Task<ReportExportResultDto> ExportAsync(Guid templateId, GenerateReportTemplateDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default);
}
