using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface ITaxReportingService
{
    Task<GhanaTaxSnapshotReportDto> GetOutputTaxReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<GhanaTaxSnapshotReportDto> GetInputTaxReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<GhanaTaxSnapshotReportDto> GetNetVatSummaryAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<GhanaTaxTreatmentReportDto> GetExemptZeroRatedOutOfScopeReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<GhanaTaxWithholdingReportDto> GetVatWithholdingReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<GhanaTaxWithholdingReportDto> GetWhtPayableReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<GhanaTaxWithholdingReportDto> GetWhtReceivableReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<GhanaTaxAccountReconciliationReportDto> GetTaxAccountReconciliationReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<GhanaTaxConfigurationHistoryReportDto> GetTaxConfigurationHistoryReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<GhanaTaxCovidLevyDiagnosticReportDto> GetCurrentActiveCovidLevyDiagnosticReportAsync(
        TaxReportRequestDto request,
        CancellationToken cancellationToken = default);
}
