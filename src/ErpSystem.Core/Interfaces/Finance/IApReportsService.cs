using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Service contract for AP reporting and analytics.
/// Provides supplier aging, cash forecasts, supplier statements,
/// withholding tax summaries, and AP dashboard metrics.
/// </summary>
public interface IApReportsService
{
    /// <summary>
    /// Generates supplier aging report.
    /// Groups outstanding invoices by age buckets: 0-30, 31-60, 61-90, 90+ days.
    /// </summary>
    /// <param name="asOfDate">Date to calculate aging from (default: today)</param>
    /// <param name="supplierId">Optional: Filter by specific supplier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ApAgingReportDto> GetAgingReportAsync(DateTime? asOfDate = null, Guid? supplierId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds AP settlement balances from posted supplier invoices, payments, allocations, withholding, and posting events.
    /// </summary>
    Task<SubledgerSettlementRebuildResultDto> RebuildSettlementReadModelAsync(DateTime? asOfDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles AP settlement read-model outstanding amounts to the posted AP control GL balance.
    /// </summary>
    Task<SubledgerControlReconciliationDto> GetControlReconciliationAsync(DateTime? asOfDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows posted supplier advances and other unapplied vendor payments separately from invoice aging.
    /// </summary>
    Task<SubledgerUnappliedSettlementReportDto> GetUnappliedSettlementsAsync(
        DateTime? asOfDate = null,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates detailed aging report with invoice-level breakdown per supplier.
    /// </summary>
    Task<ApAgingReportDto> GetDetailedAgingReportAsync(DateTime? asOfDate = null, Guid? supplierId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates cash requirement forecast showing payables due in upcoming periods.
    /// Periods: Overdue, This Week, Next Week, This Month, Next 30/60/90 days.
    /// Includes available early payment discounts.
    /// </summary>
    Task<CashRequirementForecastDto> GetCashRequirementForecastAsync(DateTime? asOfDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a supplier statement for a specific period showing:
    /// - Opening balance
    /// - Invoices received
    /// - Payments made
    /// - Closing balance
    /// </summary>
    Task<SupplierStatementDto> GetSupplierStatementAsync(Guid supplierId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates supplier detailed ledger balances and period movements for one or more suppliers.
    /// Supplier ids may be legacy Supplier ids or unified Business Partner ids.
    /// </summary>
    Task<SupplierDetailedLedgerReportDto> GetSupplierDetailedLedgerAsync(
        DateTime fromDate,
        DateTime toDate,
        IReadOnlyCollection<Guid>? supplierIds = null,
        bool showSupplierCurrency = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates withholding tax summary report grouped by supplier.
    /// Shows total WHT withheld, invoice amounts, and net payments.
    /// </summary>
    Task<WithholdingTaxSummaryDto> GetWithholdingTaxSummaryAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets AP summary metrics for the dashboard:
    /// - Total outstanding / overdue
    /// - Average days to payment
    /// - Discounts taken / missed
    /// - Pending approvals / batches
    /// </summary>
    Task<ApSummaryDto> GetApSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports aging report to Excel or PDF.
    /// </summary>
    Task<byte[]> ExportAgingReportAsync(DateTime? asOfDate = null, string format = "Csv", CancellationToken cancellationToken = default);

}
