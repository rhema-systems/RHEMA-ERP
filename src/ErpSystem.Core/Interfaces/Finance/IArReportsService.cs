using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Service contract for AR reporting and analytics.
/// </summary>
public interface IArReportsService
{
    /// <summary>
    /// Generates customer aging report.
    /// Groups outstanding invoices by age buckets: 0-30, 31-60, 61-90, 90+ days.
    /// </summary>
    /// <param name="asOfDate">Date to calculate aging from (default: today)</param>
    /// <param name="businessPartnerId">Optional canonical Business Partner filter.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AgingReportDto> GetAgingReportAsync(DateTime? asOfDate = null, Guid? businessPartnerId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds AR settlement balances from posted customer invoices, receipts, allocations, credit notes, withholding, and posting events.
    /// </summary>
    Task<SubledgerSettlementRebuildResultDto> RebuildSettlementReadModelAsync(DateTime? asOfDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles AR settlement read-model outstanding amounts to the posted AR control GL balance.
    /// </summary>
    Task<SubledgerControlReconciliationDto> GetControlReconciliationAsync(DateTime? asOfDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows posted customer advances and other unapplied receipts separately from invoice aging.
    /// </summary>
    Task<SubledgerUnappliedSettlementReportDto> GetUnappliedSettlementsAsync(
        DateTime? asOfDate = null,
        Guid? businessPartnerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates detailed aging report with invoice-level breakdown.
    /// </summary>
    Task<DetailedAgingReportDto> GetDetailedAgingReportAsync(DateTime? asOfDate = null, Guid? businessPartnerId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates customer statement for a specific period.
    /// Shows:
    /// - Opening balance
    /// - Invoices issued
    /// - Payments received
    /// - Closing balance
    /// </summary>
    Task<CustomerStatementDto> GetCustomerStatementAsync(Guid businessPartnerId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates customer detailed ledger balances and period movements for one or more customers.
    /// </summary>
    Task<CustomerDetailedLedgerReportDto> GetCustomerDetailedLedgerAsync(
        DateTime fromDate,
        DateTime toDate,
        IReadOnlyCollection<Guid>? businessPartnerIds = null,
        bool showCustomerCurrency = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets collections dashboard metrics.
    /// Highlights overdue accounts and prioritizes collection activities.
    /// </summary>
    Task<CollectionsDashboardDto> GetCollectionsDashboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets AR summary metrics.
    /// - Total outstanding
    /// - Average days to payment
    /// - Total overdue
    /// - Bad debt provision
    /// </summary>
    Task<ArSummaryDto> GetArSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sales summary by customer, period, or product.
    /// </summary>
    Task<List<SalesSummaryDto>> GetSalesSummaryAsync(SalesSummaryQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets payment collection trends over time.
    /// </summary>
    Task<List<PaymentTrendDto>> GetPaymentTrendsAsync(DateTime fromDate, DateTime toDate, string groupBy = "Month", CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports aging report to Excel/PDF.
    /// </summary>
    Task<byte[]> ExportAgingReportAsync(DateTime? asOfDate = null, string format = "Csv", CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports customer statement to PDF.
    /// </summary>
    Task<byte[]> ExportCustomerStatementAsync(Guid businessPartnerId, DateTime fromDate, DateTime toDate, string format = "PDF", CancellationToken cancellationToken = default);
}
