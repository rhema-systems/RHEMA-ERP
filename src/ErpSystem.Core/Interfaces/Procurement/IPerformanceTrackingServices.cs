using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Service interface for supplier performance metrics
/// </summary>
public interface ISupplierPerformanceService
{
    Task<SupplierPerformanceMetricDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<SupplierPerformanceMetricDto>> GetByBusinessPartnerAsync(Guid businessPartnerId);
    Task<SupplierPerformanceMetricDto?> GetByBusinessPartnerAndPeriodAsync(Guid businessPartnerId, string metricPeriod, int year, int? month, int? quarter);
    Task<IEnumerable<PerformanceTrendDto>> GetTrendsAsync(Guid businessPartnerId, int numberOfPeriods = 12);
    Task<SupplierPerformanceMetricDto> CalculateMetricsAsync(Guid businessPartnerId, string metricPeriod, int year, int? month, int? quarter);
    Task<PerformanceReportCardDto> GetReportCardAsync(Guid businessPartnerId, string reportPeriod);
    Task<SupplierPerformanceMetricDto> CreateAsync(CreateSupplierPerformanceMetricDto createDto);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Service interface for quality incidents
/// </summary>
public interface IQualityIncidentService
{
    Task<QualityIncidentDto?> GetByIdAsync(Guid id);
    Task<QualityIncidentDto?> GetByIncidentNumberAsync(string incidentNumber);
    Task<IEnumerable<QualityIncidentDto>> GetByBusinessPartnerAsync(Guid businessPartnerId);
    Task<IEnumerable<QualityIncidentDto>> GetByStatusAsync(string status);
    Task<IEnumerable<QualityIncidentDto>> GetBySeverityAsync(string severity);
    Task<IEnumerable<QualityIncidentDto>> GetOpenIncidentsAsync();
    Task<IEnumerable<QualityIncidentDto>> GetRecentIncidentsAsync(Guid businessPartnerId, int days = 90);
    Task<QualityIncidentDto> CreateAsync(CreateQualityIncidentDto createDto);
    Task<QualityIncidentDto> UpdateAsync(Guid id, UpdateQualityIncidentDto updateDto);
    Task<QualityIncidentDto> AcknowledgeAsync(Guid id);
    Task<QualityIncidentDto> ResolveAsync(Guid id, UpdateQualityIncidentDto updateDto);
    Task<QualityIncidentDto> SubmitSupplierResponseAsync(Guid id, SupplierResponseDto responseDto);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Service interface for performance reviews
/// </summary>
public interface IPerformanceReviewService
{
    Task<PerformanceReviewDto?> GetByIdAsync(Guid id);
    Task<PerformanceReviewDto?> GetByReviewNumberAsync(string reviewNumber);
    Task<IEnumerable<PerformanceReviewDto>> GetByBusinessPartnerAsync(Guid businessPartnerId);
    Task<PerformanceReviewDto?> GetLatestReviewAsync(Guid businessPartnerId);
    Task<IEnumerable<PerformanceReviewDto>> GetByPeriodAsync(string reviewPeriod);
    Task<IEnumerable<PerformanceReviewDto>> GetByStatusAsync(string status);
    Task<PerformanceReviewDto> CreateAsync(CreatePerformanceReviewDto createDto);
    Task<PerformanceReviewDto> UpdateAsync(Guid id, CreatePerformanceReviewDto updateDto);
    Task<PerformanceReviewDto> SubmitAsync(Guid id);
    Task<PerformanceReviewDto> AcknowledgeAsync(Guid id, string? supplierComments);
    Task<PerformanceReviewDto> FinalizeAsync(Guid id);
    Task DeleteAsync(Guid id);
}

