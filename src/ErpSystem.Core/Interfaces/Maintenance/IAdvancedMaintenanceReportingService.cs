using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Services.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Interface for advanced maintenance reporting service that integrates with the existing reporting system
/// </summary>
public interface IAdvancedMaintenanceReportingService
{
    #region Executive Dashboard Reports

    /// <summary>
    /// Generate executive maintenance dashboard
    /// </summary>
    Task<ExecutiveMaintenanceDashboardDto> GenerateExecutiveDashboardAsync(
        Guid tenantId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Generate predictive maintenance insights report
    /// </summary>
    Task<PredictiveMaintenanceInsightDto[]> GeneratePredictiveMaintenanceReportAsync(
        Guid tenantId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Generate maintenance trend analysis report
    /// </summary>
    Task<MaintenanceTrendAnalysisDto> GenerateTrendAnalysisReportAsync(
        Guid tenantId, DateTime startDate, DateTime endDate, string analysisPeriod = "Monthly");

    #endregion

    #region Asset Performance Reports

    /// <summary>
    /// Generate OEE (Overall Equipment Effectiveness) analysis report
    /// </summary>
    Task<OeeAnalysisDto[]> GenerateOeeAnalysisReportAsync(
        Guid tenantId, DateTime startDate, DateTime endDate, Guid? assetId = null);

    /// <summary>
    /// Generate equipment lifecycle analysis report
    /// </summary>
    Task<EquipmentLifecycleAnalysisDto[]> GenerateLifecycleAnalysisReportAsync(
        Guid tenantId, Guid? assetId = null);

    /// <summary>
    /// Generate energy consumption analysis report
    /// </summary>
    Task<EnergyConsumptionAnalysisDto[]> GenerateEnergyConsumptionReportAsync(
        Guid tenantId, DateTime startDate, DateTime endDate, Guid? assetId = null);

    #endregion

    #region Compliance and Regulatory Reports

    /// <summary>
    /// Generate regulatory compliance report
    /// </summary>
    Task<RegulatoryComplianceReportDto> GenerateComplianceReportAsync(
        Guid tenantId, string regulatoryFramework, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Generate industry benchmarking report
    /// </summary>
    Task<IndustryBenchmarkDto[]> GenerateBenchmarkingReportAsync(Guid tenantId, string industry);

    #endregion

    #region PDF/Excel Export Methods

    /// <summary>
    /// Export executive dashboard to PDF
    /// </summary>
    Task<byte[]> ExportExecutiveDashboardToPdfAsync(
        ExecutiveMaintenanceDashboardDto dashboard, string reportTitle = "Executive Maintenance Dashboard");

    /// <summary>
    /// Export maintenance report to Excel
    /// </summary>
    Task<byte[]> ExportMaintenanceReportToExcelAsync(
        object reportData, string reportType, string worksheetName = "Maintenance Report");

    #endregion

    #region Report Template Integration

    /// <summary>
    /// Create maintenance report template
    /// </summary>
    Task<ReportDefinitionDto> CreateMaintenanceReportTemplateAsync(
        Guid tenantId, Guid userId, string reportType, CreateMaintenanceReportTemplateDto template);

    /// <summary>
    /// Execute and export maintenance report
    /// </summary>
    Task<ReportExportResultDto> ExecuteAndExportMaintenanceReportAsync(
        Guid reportId, Guid tenantId, Guid userId, ExecuteMaintenanceReportDto executeDto);

    #endregion
}