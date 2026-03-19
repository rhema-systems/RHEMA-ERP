using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ErpSystem.Api.Services.Finance.AP
{
    /// <summary>
    /// Provides AP reporting: supplier aging, cash requirement forecast,
    /// supplier statements, withholding tax summaries, and AP dashboard metrics.
    /// </summary>
    public class ApReportsService : IApReportsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<ApReportsService> _logger;

        public ApReportsService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ILogger<ApReportsService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _logger = logger;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;

        // ═════════════════════════════════════════════════════════════════
        //  AGING REPORT
        // ═════════════════════════════════════════════════════════════════

        public async Task<ApAgingReportDto> GetAgingReportAsync(
            DateTime? asOfDate = null, Guid? supplierId = null, CancellationToken cancellationToken = default)
        {
            var date = asOfDate ?? DateTime.UtcNow;

            var queryable = _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft &&
                    (i.TotalAmount - i.PaidAmount) > 0);

            if (supplierId.HasValue)
                queryable = queryable.Where(i => i.SupplierId == supplierId.Value);

            var invoices = await queryable.ToListAsync(cancellationToken);

            var report = new ApAgingReportDto
            {
                AsOfDate = date,
                CurrencyCode = "USD"
            };

            // Group by supplier
            var bySupplier = invoices.GroupBy(i => new { i.SupplierId, i.SupplierName });

            foreach (var group in bySupplier)
            {
                var detail = new SupplierAgingDetailDto
                {
                    SupplierId = group.Key.SupplierId,
                    SupplierName = group.Key.SupplierName,
                    InvoiceCount = group.Count()
                };

                foreach (var invoice in group)
                {
                    var balance = invoice.TotalAmount - invoice.PaidAmount;
                    var agingDate = invoice.DueDate ?? invoice.InvoiceDate;
                    var daysOutstanding = (int)(date - agingDate).TotalDays;

                    if (daysOutstanding <= 30)
                        detail.Current += balance;
                    else if (daysOutstanding <= 60)
                        detail.ThirtyDays += balance;
                    else if (daysOutstanding <= 90)
                        detail.SixtyDays += balance;
                    else
                        detail.NinetyPlusDays += balance;

                    detail.TotalOutstanding += balance;

                    if (detail.OldestInvoiceDate == null || invoice.InvoiceDate < detail.OldestInvoiceDate)
                        detail.OldestInvoiceDate = invoice.InvoiceDate;
                }

                report.SupplierDetails.Add(detail);
            }

            report.TotalOutstanding = report.SupplierDetails.Sum(d => d.TotalOutstanding);
            report.Current = report.SupplierDetails.Sum(d => d.Current);
            report.ThirtyDays = report.SupplierDetails.Sum(d => d.ThirtyDays);
            report.SixtyDays = report.SupplierDetails.Sum(d => d.SixtyDays);
            report.NinetyPlusDays = report.SupplierDetails.Sum(d => d.NinetyPlusDays);
            report.TotalSuppliers = report.SupplierDetails.Count;
            report.TotalInvoices = invoices.Count;

            return report;
        }

        public async Task<ApAgingReportDto> GetDetailedAgingReportAsync(
            DateTime? asOfDate = null, Guid? supplierId = null, CancellationToken cancellationToken = default)
        {
            var report = await GetAgingReportAsync(asOfDate, supplierId, cancellationToken);
            var date = report.AsOfDate;

            // Reload invoices to populate per-invoice details
            var queryable = _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft &&
                    (i.TotalAmount - i.PaidAmount) > 0);

            if (supplierId.HasValue)
                queryable = queryable.Where(i => i.SupplierId == supplierId.Value);

            var invoices = await queryable.ToListAsync(cancellationToken);
            var bySupplier = invoices.GroupBy(i => i.SupplierId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var detail in report.SupplierDetails)
            {
                if (bySupplier.TryGetValue(detail.SupplierId, out var supplierInvoices))
                {
                    detail.Invoices = supplierInvoices.Select(i =>
                    {
                        var agingDate = i.DueDate ?? i.InvoiceDate;
                        var daysOutstanding = (int)(date - agingDate).TotalDays;
                        var bucket = daysOutstanding <= 30 ? "Current"
                                   : daysOutstanding <= 60 ? "31-60"
                                   : daysOutstanding <= 90 ? "61-90"
                                   : "90+";

                        return new ApAgingInvoiceDto
                        {
                            InvoiceId = i.Id,
                            InvoiceNumber = i.InvoiceNumber,
                            InvoiceDate = i.InvoiceDate,
                            DueDate = i.DueDate,
                            TotalAmount = i.TotalAmount,
                            BalanceAmount = i.TotalAmount - i.PaidAmount,
                            DaysOutstanding = Math.Max(0, daysOutstanding),
                            AgingBucket = bucket
                        };
                    }).OrderBy(i => i.DueDate).ToList();
                }
            }

            return report;
        }

        // ═════════════════════════════════════════════════════════════════
        //  CASH REQUIREMENT FORECAST
        // ═════════════════════════════════════════════════════════════════

        public async Task<CashRequirementForecastDto> GetCashRequirementForecastAsync(
            DateTime? asOfDate = null, CancellationToken cancellationToken = default)
        {
            var now = asOfDate ?? DateTime.UtcNow;
            var today = now.Date;

            var invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    (i.Status == VendorInvoiceStatus.Approved ||
                     i.Status == VendorInvoiceStatus.PartiallyPaid ||
                     i.Status == VendorInvoiceStatus.Overdue) &&
                    (i.TotalAmount - i.PaidAmount) > 0)
                .ToListAsync(cancellationToken);

            var forecast = new CashRequirementForecastDto
            {
                AsOfDate = today,
                CurrencyCode = "USD",
                TotalPayable = invoices.Sum(i => i.TotalAmount - i.PaidAmount)
            };

            // Define periods
            var periods = new[]
            {
                ("Overdue", DateTime.MinValue, today.AddDays(-1)),
                ("This Week", today, today.AddDays(6 - (int)today.DayOfWeek)),
                ("Next Week", today.AddDays(7 - (int)today.DayOfWeek), today.AddDays(13 - (int)today.DayOfWeek)),
                ("Next 30 Days", today, today.AddDays(30)),
                ("31-60 Days", today.AddDays(31), today.AddDays(60)),
                ("61-90 Days", today.AddDays(61), today.AddDays(90))
            };

            foreach (var (name, start, end) in periods)
            {
                IEnumerable<VendorInvoice> periodInvoices;

                if (name == "Overdue")
                {
                    periodInvoices = invoices.Where(i => i.DueDate.HasValue && i.DueDate.Value < today);
                    forecast.OverdueAmount = periodInvoices.Sum(i => i.TotalAmount - i.PaidAmount);
                }
                else
                {
                    periodInvoices = invoices.Where(i =>
                        i.DueDate.HasValue &&
                        i.DueDate.Value >= start &&
                        i.DueDate.Value <= end);
                }

                var periodList = periodInvoices.ToList();
                forecast.Periods.Add(new CashRequirementPeriodDto
                {
                    Period = name,
                    PeriodStart = start == DateTime.MinValue ? today : start,
                    PeriodEnd = end,
                    AmountDue = periodList.Sum(i => i.TotalAmount - i.PaidAmount),
                    InvoiceCount = periodList.Count,
                    DiscountAvailable = periodList
                        .Where(i => i.EarlyPaymentDiscountPercentage > 0
                            && i.EarlyPaymentDiscountDueDate.HasValue
                            && i.EarlyPaymentDiscountDueDate.Value >= today)
                        .Sum(i => i.EarlyPaymentDiscountAmount)
                });
            }

            return forecast;
        }

        // ═════════════════════════════════════════════════════════════════
        //  SUPPLIER STATEMENT
        // ═════════════════════════════════════════════════════════════════

        public async Task<SupplierStatementDto> GetSupplierStatementAsync(
            Guid supplierId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            var supplier = await _unitOfWork.Repository<Supplier>()
                .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == supplierId);

            if (supplier == null)
                throw new KeyNotFoundException($"Supplier with Id '{supplierId}' not found.");

            var statement = new SupplierStatementDto
            {
                SupplierId = supplierId,
                SupplierName = supplier.Name,
                SupplierCode = supplier.SupplierCode,
                FromDate = fromDate,
                ToDate = toDate,
                CurrencyCode = "USD"
            };

            // Calculate opening balance — sum of unpaid invoices before fromDate
            var priorInvoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.SupplierId == supplierId &&
                    i.InvoiceDate < fromDate &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft)
                .ToListAsync(cancellationToken);

            var priorPayments = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.SupplierId == supplierId &&
                    p.PaymentDate < fromDate &&
                    p.Status != VendorPaymentStatus.Voided)
                .ToListAsync(cancellationToken);

            statement.OpeningBalance = priorInvoices.Sum(i => i.TotalAmount) - priorPayments.Sum(p => p.TotalAmount);

            // Get period invoices
            var periodInvoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.SupplierId == supplierId &&
                    i.InvoiceDate >= fromDate &&
                    i.InvoiceDate <= toDate &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft)
                .OrderBy(i => i.InvoiceDate)
                .ToListAsync(cancellationToken);

            // Get period payments
            var periodPayments = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.SupplierId == supplierId &&
                    p.PaymentDate >= fromDate &&
                    p.PaymentDate <= toDate &&
                    p.Status != VendorPaymentStatus.Voided)
                .OrderBy(p => p.PaymentDate)
                .ToListAsync(cancellationToken);

            statement.TotalInvoices = periodInvoices.Sum(i => i.TotalAmount);
            statement.TotalPayments = periodPayments.Sum(p => p.TotalAmount);
            statement.ClosingBalance = statement.OpeningBalance + statement.TotalInvoices - statement.TotalPayments;

            // Build statement lines
            var lines = new List<SupplierStatementLineDto>();
            decimal runningBalance = statement.OpeningBalance;

            // Combine and sort by date
            var allTransactions = periodInvoices
                .Select(i => new
                {
                    Date = i.InvoiceDate,
                    Type = "Invoice",
                    DocumentNumber = i.InvoiceNumber,
                    Reference = i.SupplierInvoiceNumber ?? i.Reference,
                    Debit = i.TotalAmount,
                    Credit = 0m
                })
                .Concat(periodPayments.Select(p => new
                {
                    Date = p.PaymentDate,
                    Type = "Payment",
                    DocumentNumber = p.PaymentNumber,
                    Reference = p.TransactionReference ?? p.ChequeNumber,
                    Debit = 0m,
                    Credit = p.TotalAmount
                }))
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Type);

            foreach (var txn in allTransactions)
            {
                runningBalance += txn.Debit - txn.Credit;
                lines.Add(new SupplierStatementLineDto
                {
                    Date = txn.Date,
                    TransactionType = txn.Type,
                    DocumentNumber = txn.DocumentNumber,
                    Reference = txn.Reference,
                    Debit = txn.Debit,
                    Credit = txn.Credit,
                    RunningBalance = runningBalance
                });
            }

            statement.Lines = lines;
            return statement;
        }

        // ═════════════════════════════════════════════════════════════════
        //  WITHHOLDING TAX SUMMARY
        // ═════════════════════════════════════════════════════════════════

        public async Task<WithholdingTaxSummaryDto> GetWithholdingTaxSummaryAsync(
            DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            var payments = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.PaymentDate >= fromDate &&
                    p.PaymentDate <= toDate &&
                    p.Status != VendorPaymentStatus.Voided &&
                    p.WithholdingTaxAmount > 0)
                .Include(p => p.Supplier)
                .ToListAsync(cancellationToken);

            var summary = new WithholdingTaxSummaryDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                CurrencyCode = "USD",
                TotalWithheld = payments.Sum(p => p.WithholdingTaxAmount),
                TransactionCount = payments.Count,
                SupplierCount = payments.Select(p => p.SupplierId).Distinct().Count()
            };

            summary.BySupplier = payments
                .GroupBy(p => new { p.SupplierId, p.Supplier.Name, p.Supplier.TaxId })
                .Select(g => new WithholdingTaxBySupplierDto
                {
                    SupplierId = g.Key.SupplierId,
                    SupplierName = g.Key.Name,
                    TaxId = g.Key.TaxId,
                    TotalInvoiceAmount = g.Sum(p => p.TotalAmount),
                    TotalWithholdingTax = g.Sum(p => p.WithholdingTaxAmount),
                    TotalNetPayment = g.Sum(p => p.TotalAmount - p.WithholdingTaxAmount),
                    TransactionCount = g.Count()
                })
                .OrderByDescending(s => s.TotalWithholdingTax)
                .ToList();

            return summary;
        }

        // ═════════════════════════════════════════════════════════════════
        //  AP SUMMARY (DASHBOARD)
        // ═════════════════════════════════════════════════════════════════

        public async Task<ApSummaryDto> GetApSummaryAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft)
                .ToListAsync(cancellationToken);

            var outstanding = invoices.Where(i => (i.TotalAmount - i.PaidAmount) > 0).ToList();
            var overdue = outstanding.Where(i => i.DueDate.HasValue && i.DueDate.Value < now).ToList();

            // Payments this month
            var monthPayments = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.PaymentDate >= monthStart &&
                    p.Status != VendorPaymentStatus.Voided)
                .ToListAsync(cancellationToken);

            // Average days to payment
            var paidInvoices = invoices.Where(i => i.Status == VendorInvoiceStatus.Paid && i.DueDate.HasValue).ToList();
            var avgDays = paidInvoices.Any()
                ? paidInvoices.Average(i => (i.UpdatedAt ?? i.CreatedAt).Subtract(i.InvoiceDate).TotalDays)
                : 0;

            // Discounts — taken vs missed
            var discountEligible = invoices.Where(i => i.EarlyPaymentDiscountPercentage > 0);
            var taken = discountEligible.Where(i =>
                i.Status == VendorInvoiceStatus.Paid &&
                i.EarlyPaymentDiscountDueDate.HasValue &&
                (i.UpdatedAt ?? i.CreatedAt) <= i.EarlyPaymentDiscountDueDate.Value)
                .Sum(i => i.EarlyPaymentDiscountAmount);

            var missed = discountEligible.Where(i =>
                i.EarlyPaymentDiscountDueDate.HasValue &&
                i.EarlyPaymentDiscountDueDate.Value < now &&
                (i.Status != VendorInvoiceStatus.Paid
                 || (i.UpdatedAt ?? i.CreatedAt) > i.EarlyPaymentDiscountDueDate.Value))
                .Sum(i => i.EarlyPaymentDiscountAmount);

            // Pending approvals and batches
            var pendingApprovals = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && i.Status == VendorInvoiceStatus.PendingApproval)
                .CountAsync(cancellationToken);

            var pendingBatches = await _unitOfWork.Repository<PaymentBatch>()
                .GetQueryable(b => b.TenantId == TenantId &&
                    (b.Status == PaymentBatchStatus.Draft || b.Status == PaymentBatchStatus.PendingApproval))
                .CountAsync(cancellationToken);

            return new ApSummaryDto
            {
                TotalOutstanding = outstanding.Sum(i => i.TotalAmount - i.PaidAmount),
                TotalOverdue = overdue.Sum(i => i.TotalAmount - i.PaidAmount),
                OutstandingInvoiceCount = outstanding.Count,
                OverdueInvoiceCount = overdue.Count,
                AverageDaysToPayment = (decimal)avgDays,
                TotalPaidThisMonth = monthPayments.Sum(p => p.TotalAmount),
                DiscountsTaken = taken,
                DiscountsMissed = missed,
                WithholdingTaxThisMonth = monthPayments.Sum(p => p.WithholdingTaxAmount),
                PendingApprovalCount = pendingApprovals,
                PendingBatchCount = pendingBatches
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  EXPORTS (STUBS)
        // ═════════════════════════════════════════════════════════════════

        public async Task<byte[]> ExportAgingReportAsync(DateTime? asOfDate = null, string format = "Excel", CancellationToken cancellationToken = default)
        {
            // Placeholder — integrate with QuestPDF / ClosedXML
            var report = await GetAgingReportAsync(asOfDate, null, cancellationToken);
            _logger.LogInformation("Export aging report requested in {Format} format", format);
            throw new NotImplementedException($"Aging report export in {format} format will be implemented with the reporting library.");
        }

        public async Task<byte[]> ExportSupplierStatementAsync(Guid supplierId, DateTime fromDate, DateTime toDate, string format = "PDF", CancellationToken cancellationToken = default)
        {
            var statement = await GetSupplierStatementAsync(supplierId, fromDate, toDate, cancellationToken);
            _logger.LogInformation("Export supplier statement requested for {SupplierId} in {Format} format", supplierId, format);
            throw new NotImplementedException($"Supplier statement export in {format} format will be implemented with the reporting library.");
        }
    }
}
