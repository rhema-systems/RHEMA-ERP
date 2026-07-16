using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<ApReportsService> _logger;
        private readonly ISubledgerSettlementReadModelService _settlementReadModelService;
        private readonly IFinanceAuditService? _financeAuditService;

        public ApReportsService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ITenantSettingsService tenantSettingsService,
            ILogger<ApReportsService> logger,
            ISubledgerSettlementReadModelService settlementReadModelService,
            IFinanceAuditService? financeAuditService = null)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
            _settlementReadModelService = settlementReadModelService ?? throw new ArgumentNullException(nameof(settlementReadModelService));
            _financeAuditService = financeAuditService;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

        // ═════════════════════════════════════════════════════════════════
        //  AGING REPORT
        // ═════════════════════════════════════════════════════════════════

        public async Task<ApAgingReportDto> GetAgingReportAsync(
            DateTime? asOfDate = null, Guid? supplierId = null, CancellationToken cancellationToken = default)
        {
            // The constructor requires the projection service so production aging cannot
            // silently regress to mutable VendorInvoice.PaidAmount snapshots.
            if (_settlementReadModelService != null)
                return await GetSettlementReadModelAgingReportAsync(asOfDate, supplierId, includeInvoiceDetails: false, cancellationToken);

            var date = asOfDate ?? DateTime.UtcNow;
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            var queryable = _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft &&
                    (i.TotalAmount - i.PaidAmount) > 0);
            queryable = ApplyPostedApInvoiceFilter(queryable);

            if (supplierId.HasValue)
                queryable = queryable.Where(i => i.SupplierId == supplierId.Value);

            var invoices = await queryable.ToListAsync(cancellationToken);
            var adjustments = await GetPostedApAdjustmentsAsync(supplierId, cancellationToken);

            var report = new ApAgingReportDto
            {
                AsOfDate = date,
                CurrencyCode = baseCurrencyCode
            };

            var detailBySupplier = new Dictionary<Guid, SupplierAgingDetailDto>();

            SupplierAgingDetailDto GetOrCreateDetail(Guid id, string name)
            {
                if (detailBySupplier.TryGetValue(id, out var existing))
                    return existing;

                var detail = new SupplierAgingDetailDto
                {
                    SupplierId = id,
                    SupplierName = name
                };
                detailBySupplier[id] = detail;
                report.SupplierDetails.Add(detail);
                return detail;
            }

            foreach (var invoice in invoices)
            {
                var detail = GetOrCreateDetail(invoice.SupplierId, invoice.SupplierName);
                var balance = invoice.TotalAmount - invoice.PaidAmount;
                AddToSupplierAgingBucket(detail, balance, invoice.DueDate, invoice.InvoiceDate, date);
                detail.InvoiceCount++;

                if (detail.OldestInvoiceDate == null || invoice.InvoiceDate < detail.OldestInvoiceDate)
                    detail.OldestInvoiceDate = invoice.InvoiceDate;
            }

            foreach (var adjustment in adjustments)
            {
                if (!adjustment.SupplierId.HasValue)
                    continue;

                var amount = GetSignedApSubledgerAmount(adjustment);
                if (amount == 0)
                    continue;

                var detail = GetOrCreateDetail(adjustment.SupplierId.Value, adjustment.Supplier?.Name ?? "Supplier");
                AddToSupplierAgingBucket(detail, amount, adjustment.DueDate, adjustment.AdjustmentDate, date);
                detail.InvoiceCount++;

                if (detail.OldestInvoiceDate == null || adjustment.AdjustmentDate < detail.OldestInvoiceDate)
                    detail.OldestInvoiceDate = adjustment.AdjustmentDate;
            }

            report.SupplierDetails = report.SupplierDetails
                .Where(d => d.TotalOutstanding != 0)
                .OrderByDescending(d => d.TotalOutstanding)
                .ToList();
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
            // See GetAgingReportAsync: mandatory dependency prevents a silent legacy fallback.
            if (_settlementReadModelService != null)
                return await GetSettlementReadModelAgingReportAsync(asOfDate, supplierId, includeInvoiceDetails: true, cancellationToken);

            var report = await GetAgingReportAsync(asOfDate, supplierId, cancellationToken);
            var date = report.AsOfDate;

            // Reload invoices to populate per-invoice details
            var queryable = _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft &&
                    (i.TotalAmount - i.PaidAmount) > 0);
            queryable = ApplyPostedApInvoiceFilter(queryable);

            if (supplierId.HasValue)
                queryable = queryable.Where(i => i.SupplierId == supplierId.Value);

            var invoices = await queryable.ToListAsync(cancellationToken);
            var adjustments = await GetPostedApAdjustmentsAsync(supplierId, cancellationToken);
            var bySupplier = invoices.GroupBy(i => i.SupplierId).ToDictionary(g => g.Key, g => g.ToList());
            var adjustmentsBySupplier = adjustments
                .Where(a => a.SupplierId.HasValue)
                .GroupBy(a => a.SupplierId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

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
                    }).ToList();
                }

                if (adjustmentsBySupplier.TryGetValue(detail.SupplierId, out var supplierAdjustments))
                {
                    detail.Invoices.AddRange(supplierAdjustments.Select(a =>
                    {
                        var amount = GetSignedApSubledgerAmount(a);
                        var agingDate = a.DueDate ?? a.AdjustmentDate;
                        var daysOutstanding = (int)(date - agingDate).TotalDays;
                        var bucket = daysOutstanding <= 30 ? "Current"
                                   : daysOutstanding <= 60 ? "31-60"
                                   : daysOutstanding <= 90 ? "61-90"
                                   : "90+";

                        return new ApAgingInvoiceDto
                        {
                            InvoiceId = a.Id,
                            InvoiceNumber = a.AdjustmentNumber,
                            InvoiceDate = a.AdjustmentDate,
                            DueDate = a.DueDate,
                            TotalAmount = amount,
                            BalanceAmount = amount,
                            DaysOutstanding = Math.Max(0, daysOutstanding),
                            AgingBucket = bucket
                        };
                    }));
                }

                detail.Invoices = detail.Invoices.OrderBy(i => i.DueDate ?? i.InvoiceDate).ToList();
            }

            return report;
        }

        public Task<SubledgerSettlementRebuildResultDto> RebuildSettlementReadModelAsync(
            DateTime? asOfDate = null,
            CancellationToken cancellationToken = default)
        {
            if (_settlementReadModelService == null)
            {
                throw new InvalidOperationException("AP settlement read-model service is not configured.");
            }

            return _settlementReadModelService.RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = SubledgerSettlementModules.AccountsPayable,
                AsOfDate = asOfDate
            }, cancellationToken);
        }

        public Task<SubledgerControlReconciliationDto> GetControlReconciliationAsync(
            DateTime? asOfDate = null,
            CancellationToken cancellationToken = default)
        {
            if (_settlementReadModelService == null)
            {
                throw new InvalidOperationException("AP settlement read-model service is not configured.");
            }

            return _settlementReadModelService.GetControlReconciliationAsync(
                SubledgerSettlementModules.AccountsPayable,
                asOfDate,
                cancellationToken);
        }

        private async Task<ApAgingReportDto> GetSettlementReadModelAgingReportAsync(
            DateTime? asOfDate,
            Guid? supplierId,
            bool includeInvoiceDetails,
            CancellationToken cancellationToken)
        {
            var date = asOfDate ?? DateTime.UtcNow;
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            var rebuild = await _settlementReadModelService!.RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = SubledgerSettlementModules.AccountsPayable,
                AsOfDate = date,
                RecordAudit = false
            }, cancellationToken);

            var balances = (await _settlementReadModelService.GetBalancesAsync(
                    SubledgerSettlementModules.AccountsPayable,
                    date,
                    supplierId,
                    cancellationToken))
                .Where(b => b.OutstandingAmount != 0)
                .ToList();

            var documentIds = balances.Select(b => b.SourceDocumentId).Distinct().ToList();
            var invoiceNames = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i => i.TenantId == TenantId && documentIds.Contains(i.Id))
                .Select(i => new { i.Id, i.SupplierName })
                .ToDictionaryAsync(i => i.Id, i => i.SupplierName, cancellationToken);

            var adjustments = await GetPostedApAdjustmentsAsync(supplierId, cancellationToken);
            var report = new ApAgingReportDto
            {
                AsOfDate = date,
                CurrencyCode = baseCurrencyCode,
                UsesSettlementReadModel = true,
                Diagnostics = rebuild.Diagnostics
            };

            var detailBySupplier = new Dictionary<Guid, SupplierAgingDetailDto>();
            SupplierAgingDetailDto GetOrCreateDetail(Guid id, string name)
            {
                if (detailBySupplier.TryGetValue(id, out var existing))
                    return existing;

                var detail = new SupplierAgingDetailDto
                {
                    SupplierId = id,
                    SupplierName = name,
                    Invoices = new List<ApAgingInvoiceDto>()
                };
                detailBySupplier[id] = detail;
                report.SupplierDetails.Add(detail);
                return detail;
            }

            foreach (var balance in balances)
            {
                var detail = GetOrCreateDetail(
                    balance.CounterpartyId,
                    invoiceNames.GetValueOrDefault(balance.SourceDocumentId) ?? "Supplier");
                AddToSupplierAgingBucket(detail, balance.OutstandingAmount, balance.DueDate, balance.TransactionDate, date);
                detail.InvoiceCount++;

                if (detail.OldestInvoiceDate == null || balance.TransactionDate < detail.OldestInvoiceDate)
                    detail.OldestInvoiceDate = balance.TransactionDate;

                if (includeInvoiceDetails && detail.Invoices != null)
                {
                    detail.Invoices.Add(MapApBalanceToAgingInvoice(balance, date));
                }
            }

            foreach (var adjustment in adjustments)
            {
                if (!adjustment.SupplierId.HasValue)
                    continue;

                var amount = GetSignedApSubledgerAmount(adjustment);
                if (amount == 0)
                    continue;

                var detail = GetOrCreateDetail(adjustment.SupplierId.Value, adjustment.Supplier?.Name ?? "Supplier");
                AddToSupplierAgingBucket(detail, amount, adjustment.DueDate, adjustment.AdjustmentDate, date);
                detail.InvoiceCount++;

                if (detail.OldestInvoiceDate == null || adjustment.AdjustmentDate < detail.OldestInvoiceDate)
                    detail.OldestInvoiceDate = adjustment.AdjustmentDate;

                if (includeInvoiceDetails && detail.Invoices != null)
                {
                    detail.Invoices.Add(MapApAdjustmentToAgingInvoice(adjustment, date));
                }
            }

            report.SupplierDetails = report.SupplierDetails
                .Where(d => d.TotalOutstanding != 0)
                .OrderByDescending(d => d.TotalOutstanding)
                .ToList();
            report.TotalOutstanding = report.SupplierDetails.Sum(d => d.TotalOutstanding);
            report.Current = report.SupplierDetails.Sum(d => d.Current);
            report.ThirtyDays = report.SupplierDetails.Sum(d => d.ThirtyDays);
            report.SixtyDays = report.SupplierDetails.Sum(d => d.SixtyDays);
            report.NinetyPlusDays = report.SupplierDetails.Sum(d => d.NinetyPlusDays);
            report.TotalSuppliers = report.SupplierDetails.Count;
            report.TotalInvoices = balances.Count + adjustments.Count;

            await RecordReportAuditAsync(FinanceAuditEvents.ApAgingGeneratedFromSettlementReadModel, report, cancellationToken);
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
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            var invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    (i.Status == VendorInvoiceStatus.Approved ||
                     i.Status == VendorInvoiceStatus.PartiallyPaid ||
                     i.Status == VendorInvoiceStatus.Overdue) &&
                    (i.TotalAmount - i.PaidAmount) > 0)
                .ToListAsync(cancellationToken);
            var adjustments = (await GetPostedApAdjustmentsAsync(null, cancellationToken))
                .Where(a => GetSignedApSubledgerAmount(a) > 0)
                .ToList();

            var forecast = new CashRequirementForecastDto
            {
                AsOfDate = today,
                CurrencyCode = baseCurrencyCode,
                TotalPayable = invoices.Sum(i => i.TotalAmount - i.PaidAmount)
                    + adjustments.Sum(GetSignedApSubledgerAmount)
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
                    forecast.OverdueAmount += adjustments
                        .Where(a => (a.DueDate ?? a.AdjustmentDate) < today)
                        .Sum(GetSignedApSubledgerAmount);
                }
                else
                {
                    periodInvoices = invoices.Where(i =>
                        i.DueDate.HasValue &&
                        i.DueDate.Value >= start &&
                        i.DueDate.Value <= end);
                }

                var periodList = periodInvoices.ToList();
                var periodAdjustments = adjustments.Where(a =>
                {
                    var dueDate = a.DueDate ?? a.AdjustmentDate;
                    return dueDate >= start && dueDate <= end;
                }).ToList();
                forecast.Periods.Add(new CashRequirementPeriodDto
                {
                    Period = name,
                    PeriodStart = start == DateTime.MinValue ? today : start,
                    PeriodEnd = end,
                    AmountDue = periodList.Sum(i => i.TotalAmount - i.PaidAmount)
                        + periodAdjustments.Sum(GetSignedApSubledgerAmount),
                    InvoiceCount = periodList.Count + periodAdjustments.Count,
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
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            if (supplier == null)
                throw new KeyNotFoundException($"Supplier with Id '{supplierId}' not found.");

            var statement = new SupplierStatementDto
            {
                SupplierId = supplierId,
                SupplierName = supplier.Name,
                SupplierCode = supplier.SupplierCode,
                FromDate = fromDate,
                ToDate = toDate,
                CurrencyCode = baseCurrencyCode
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

            var allAdjustments = await GetPostedApAdjustmentsAsync(supplierId, cancellationToken);
            statement.OpeningBalance = priorInvoices.Sum(i => i.TotalAmount)
                - priorPayments.Sum(p => p.TotalAmount)
                + allAdjustments
                    .Where(a => a.AdjustmentDate < fromDate)
                    .Sum(GetSignedApSubledgerAmount);

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

            var periodAdjustments = allAdjustments
                .Where(a => a.AdjustmentDate >= fromDate && a.AdjustmentDate <= toDate)
                .ToList();

            statement.TotalInvoices = periodInvoices.Sum(i => i.TotalAmount)
                + periodAdjustments.Where(a => GetSignedApSubledgerAmount(a) > 0).Sum(GetSignedApSubledgerAmount);
            statement.TotalPayments = periodPayments.Sum(p => p.TotalAmount)
                + periodAdjustments.Where(a => GetSignedApSubledgerAmount(a) < 0).Sum(a => Math.Abs(GetSignedApSubledgerAmount(a)));
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
                .Concat(periodAdjustments.Select(a =>
                {
                    var amount = GetSignedApSubledgerAmount(a);
                    return new
                    {
                        Date = a.AdjustmentDate,
                        Type = GetAdjustmentTransactionType(a),
                        DocumentNumber = a.AdjustmentNumber,
                        Reference = a.Reference,
                        Debit = amount > 0 ? amount : 0m,
                        Credit = amount < 0 ? Math.Abs(amount) : 0m
                    };
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

        public async Task<SupplierDetailedLedgerReportDto> GetSupplierDetailedLedgerAsync(
            DateTime fromDate,
            DateTime toDate,
            IReadOnlyCollection<Guid>? supplierIds = null,
            bool showSupplierCurrency = false,
            CancellationToken cancellationToken = default)
        {
            var startDate = fromDate.Date;
            var endDate = toDate.Date;
            if (endDate < startDate)
                throw new ArgumentException("The end date must be on or after the start date.");

            var endExclusive = endDate.AddDays(1);
            var baseCurrencyCode = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync(), "GHS");
            var requestedSupplierIds = supplierIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList() ?? new List<Guid>();

            var selections = await GetSupplierLedgerSelectionsAsync(requestedSupplierIds, endExclusive, cancellationToken);
            var report = new SupplierDetailedLedgerReportDto
            {
                FromDate = startDate,
                ToDate = endDate,
                CurrencyCode = showSupplierCurrency ? "Supplier Currency" : baseCurrencyCode,
                ShowSupplierCurrency = showSupplierCurrency
            };

            foreach (var selection in selections.OrderBy(s => s.SupplierName).ThenBy(s => s.SupplierCode))
            {
                var reportCurrencyCode = showSupplierCurrency
                    ? NormalizeCurrency(selection.CurrencyCode, baseCurrencyCode)
                    : baseCurrencyCode;

                var transactions = selection.SupplierId.HasValue
                    ? await GetSupplierLedgerTransactionsAsync(selection, startDate, endExclusive, reportCurrencyCode, baseCurrencyCode, report.Warnings, cancellationToken)
                    : new List<SupplierLedgerTransaction>();

                var openingBalance = transactions
                    .Where(t => t.TransactionDate.Date < startDate)
                    .Sum(t => t.Credit - t.Debit);

                var periodTransactions = transactions
                    .Where(t => t.TransactionDate.Date >= startDate && t.TransactionDate < endExclusive)
                    .OrderBy(t => t.TransactionDate)
                    .ThenBy(t => t.TransactionType)
                    .ThenBy(t => t.DocumentNumber)
                    .ToList();

                var runningBalance = openingBalance;
                var lines = new List<SupplierDetailedLedgerLineDto>();

                foreach (var transaction in periodTransactions)
                {
                    runningBalance += transaction.Credit - transaction.Debit;
                    lines.Add(new SupplierDetailedLedgerLineDto
                    {
                        SourceDocumentId = transaction.SourceDocumentId,
                        TransactionDate = transaction.TransactionDate,
                        TransactionType = transaction.TransactionType,
                        DocumentNumber = transaction.DocumentNumber,
                        Reference = transaction.Reference,
                        Description = transaction.Description,
                        TransactionCurrencyCode = transaction.TransactionCurrencyCode,
                        ExchangeRate = transaction.ExchangeRate,
                        Debit = transaction.Debit,
                        Credit = transaction.Credit,
                        RunningBalance = runningBalance
                    });
                }

                var totalDebits = lines.Sum(l => l.Debit);
                var totalCredits = lines.Sum(l => l.Credit);
                var closingBalance = openingBalance + totalCredits - totalDebits;

                if (requestedSupplierIds.Count == 0 && openingBalance == 0m && closingBalance == 0m && lines.Count == 0)
                    continue;

                report.Suppliers.Add(new SupplierDetailedLedgerAccountDto
                {
                    SupplierId = selection.SupplierId ?? selection.BusinessPartnerId ?? Guid.Empty,
                    BusinessPartnerId = selection.BusinessPartnerId,
                    SupplierCode = selection.SupplierCode,
                    SupplierName = selection.SupplierName,
                    CurrencyCode = reportCurrencyCode,
                    OpeningBalance = RoundMoney(openingBalance),
                    TotalDebits = RoundMoney(totalDebits),
                    TotalCredits = RoundMoney(totalCredits),
                    ClosingBalance = RoundMoney(closingBalance),
                    Lines = lines
                });
            }

            report.TotalOpeningBalance = RoundMoney(report.Suppliers.Sum(s => s.OpeningBalance));
            report.TotalDebits = RoundMoney(report.Suppliers.Sum(s => s.TotalDebits));
            report.TotalCredits = RoundMoney(report.Suppliers.Sum(s => s.TotalCredits));
            report.TotalClosingBalance = RoundMoney(report.Suppliers.Sum(s => s.ClosingBalance));
            report.Warnings = report.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            return report;
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
            var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();

            var summary = new WithholdingTaxSummaryDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                CurrencyCode = baseCurrencyCode,
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

            var invoiceQuery = _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Draft);
            var invoices = await ApplyPostedApInvoiceFilter(invoiceQuery).ToListAsync(cancellationToken);

            var outstanding = invoices.Where(i => (i.TotalAmount - i.PaidAmount) > 0).ToList();
            var overdue = outstanding.Where(i => i.DueDate.HasValue && i.DueDate.Value < now).ToList();
            var adjustments = await GetPostedApAdjustmentsAsync(null, cancellationToken);
            var positiveAdjustments = adjustments.Where(a => GetSignedApSubledgerAmount(a) > 0).ToList();
            var overdueAdjustments = positiveAdjustments
                .Where(a => (a.DueDate ?? a.AdjustmentDate) < now)
                .ToList();

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
                TotalOutstanding = outstanding.Sum(i => i.TotalAmount - i.PaidAmount)
                    + adjustments.Sum(GetSignedApSubledgerAmount),
                TotalOverdue = overdue.Sum(i => i.TotalAmount - i.PaidAmount)
                    + overdueAdjustments.Sum(GetSignedApSubledgerAmount),
                OutstandingInvoiceCount = outstanding.Count + positiveAdjustments.Count,
                OverdueInvoiceCount = overdue.Count + overdueAdjustments.Count,
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

        public async Task<byte[]> ExportAgingReportAsync(DateTime? asOfDate = null, string format = "Csv", CancellationToken cancellationToken = default)
        {
            if (!string.Equals(format, "Csv", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("AP aging export supports Csv in the backend reporting foundation. Use the central finance report export service for other formats when added.");
            }

            var exportReport = await GetDetailedAgingReportAsync(asOfDate, null, cancellationToken);
            if (!exportReport.UsesSettlementReadModel)
            {
                throw new InvalidOperationException("AP aging export requires the AP settlement read model. Legacy operational-field aging is not allowed for production export.");
            }

            var csv = new StringBuilder();
            csv.AppendLine("Supplier,InvoiceNumber,InvoiceDate,DueDate,TotalAmount,SettledAmount,CreditedAmount,WithheldAmount,OutstandingAmount,AgingBucket,SettlementStatus,SourcePostingEventId,SourceJournalEntryId,Diagnostics");
            foreach (var supplier in exportReport.SupplierDetails)
            {
                foreach (var invoice in supplier.Invoices)
                {
                    csv.AppendLine(string.Join(",", new[]
                    {
                        Csv(supplier.SupplierName),
                        Csv(invoice.InvoiceNumber),
                        Csv(invoice.InvoiceDate.ToString("yyyy-MM-dd")),
                        Csv(invoice.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty),
                        invoice.TotalAmount.ToString("0.00"),
                        invoice.SettledAmount.ToString("0.00"),
                        invoice.CreditedAmount.ToString("0.00"),
                        invoice.WithheldAmount.ToString("0.00"),
                        invoice.BalanceAmount.ToString("0.00"),
                        Csv(invoice.AgingBucket),
                        Csv(invoice.SettlementStatus ?? string.Empty),
                        Csv(invoice.SourcePostingEventId?.ToString() ?? string.Empty),
                        Csv(invoice.SourceJournalEntryId?.ToString() ?? string.Empty),
                        Csv(invoice.DiagnosticFlags ?? string.Empty)
                    }));
                }
            }

            await RecordReportAuditAsync(FinanceAuditEvents.ApAgingExported, exportReport, cancellationToken);
            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> ExportSupplierStatementAsync(Guid supplierId, DateTime fromDate, DateTime toDate, string format = "PDF", CancellationToken cancellationToken = default)
        {
            var statement = await GetSupplierStatementAsync(supplierId, fromDate, toDate, cancellationToken);
            _logger.LogInformation("Export supplier statement requested for {SupplierId} in {Format} format", supplierId, format);
            throw new NotImplementedException($"Supplier statement export in {format} format will be implemented with the reporting library.");
        }

        private Task<List<SubledgerAdjustmentJournal>> GetPostedApAdjustmentsAsync(
            Guid? supplierId,
            CancellationToken cancellationToken)
        {
            IQueryable<SubledgerAdjustmentJournal> query = _unitOfWork.Repository<SubledgerAdjustmentJournal>()
                .GetQueryable(a =>
                    a.TenantId == TenantId &&
                    a.Module == SubledgerModules.AccountsPayable &&
                    a.Status == SubledgerAdjustmentStatuses.Posted &&
                    !a.IsDeleted)
                .Include(a => a.Supplier);

            if (supplierId.HasValue)
                query = query.Where(a => a.SupplierId == supplierId.Value);

            return query.ToListAsync(cancellationToken);
        }

        private IQueryable<VendorInvoice> ApplyPostedApInvoiceFilter(IQueryable<VendorInvoice> query)
        {
            var postedInvoiceEvents = _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(e =>
                    e.TenantId == TenantId &&
                    e.SourceDocumentType == "VendorInvoice" &&
                    e.PostingAction == "Post" &&
                    e.PostingStatus == "Posted");

            return query.Where(i => postedInvoiceEvents.Any(e => e.SourceDocumentId == i.Id));
        }

        private static void AddToSupplierAgingBucket(
            SupplierAgingDetailDto detail,
            decimal amount,
            DateTime? dueDate,
            DateTime transactionDate,
            DateTime asOfDate)
        {
            var agingDate = dueDate ?? transactionDate;
            var daysOutstanding = (int)(asOfDate - agingDate).TotalDays;

            if (daysOutstanding <= 30)
                detail.Current += amount;
            else if (daysOutstanding <= 60)
                detail.ThirtyDays += amount;
            else if (daysOutstanding <= 90)
                detail.SixtyDays += amount;
            else
                detail.NinetyPlusDays += amount;

            detail.TotalOutstanding += amount;
        }

        private static decimal GetSignedApSubledgerAmount(SubledgerAdjustmentJournal adjustment)
        {
            var isCredit = string.Equals(adjustment.AdjustmentType, SubledgerAdjustmentTypes.Credit, StringComparison.OrdinalIgnoreCase);
            return isCredit ? adjustment.Amount : -adjustment.Amount;
        }

        private static string GetAdjustmentTransactionType(SubledgerAdjustmentJournal adjustment)
        {
            return string.Equals(adjustment.Purpose, SubledgerAdjustmentPurposes.OpeningBalance, StringComparison.OrdinalIgnoreCase)
                ? "Opening Balance"
                : "Adjustment";
        }

        private static ApAgingInvoiceDto MapApBalanceToAgingInvoice(
            SubledgerSettlementBalance balance,
            DateTime asOfDate)
        {
            var agingDate = balance.DueDate ?? balance.TransactionDate;
            var daysOutstanding = (int)(asOfDate - agingDate).TotalDays;
            return new ApAgingInvoiceDto
            {
                InvoiceId = balance.SourceDocumentId,
                InvoiceNumber = balance.SourceDocumentNumber,
                InvoiceDate = balance.TransactionDate,
                DueDate = balance.DueDate,
                TotalAmount = balance.OriginalDocumentAmount,
                SettledAmount = balance.SettledAmount,
                CreditedAmount = balance.CreditedAmount,
                WithheldAmount = balance.WithheldAmount,
                BalanceAmount = balance.OutstandingAmount,
                SourcePostingEventId = balance.SourcePostingEventId,
                SourceJournalEntryId = balance.SourceJournalEntryId,
                SettlementStatus = balance.SettlementStatus,
                DiagnosticFlags = balance.DiagnosticFlags,
                DaysOutstanding = Math.Max(0, daysOutstanding),
                AgingBucket = daysOutstanding <= 30 ? "Current"
                    : daysOutstanding <= 60 ? "31-60"
                    : daysOutstanding <= 90 ? "61-90"
                    : "90+"
            };
        }

        private static ApAgingInvoiceDto MapApAdjustmentToAgingInvoice(
            SubledgerAdjustmentJournal adjustment,
            DateTime asOfDate)
        {
            var amount = GetSignedApSubledgerAmount(adjustment);
            var agingDate = adjustment.DueDate ?? adjustment.AdjustmentDate;
            var daysOutstanding = (int)(asOfDate - agingDate).TotalDays;
            return new ApAgingInvoiceDto
            {
                InvoiceId = adjustment.Id,
                InvoiceNumber = adjustment.AdjustmentNumber,
                InvoiceDate = adjustment.AdjustmentDate,
                DueDate = adjustment.DueDate,
                TotalAmount = amount,
                BalanceAmount = amount,
                DaysOutstanding = Math.Max(0, daysOutstanding),
                AgingBucket = daysOutstanding <= 30 ? "Current"
                    : daysOutstanding <= 60 ? "31-60"
                    : daysOutstanding <= 90 ? "61-90"
                    : "90+",
                SettlementStatus = "PostedAdjustment"
            };
        }

        private async Task<List<SupplierLedgerSelection>> GetSupplierLedgerSelectionsAsync(
            IReadOnlyCollection<Guid> requestedSupplierIds,
            DateTime endExclusive,
            CancellationToken cancellationToken)
        {
            var suppliers = await _unitOfWork.Repository<Supplier>()
                .GetQueryable(s => s.TenantId == TenantId)
                .ToListAsync(cancellationToken);

            var partners = await _unitOfWork.Repository<BusinessPartner>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    (p.PartnerType == "Supplier" || p.PartnerType == "Contractor" || p.PartnerType == "Both"))
                .ToListAsync(cancellationToken);

            var selectedIds = requestedSupplierIds.Count > 0
                ? requestedSupplierIds.ToHashSet()
                : await GetSupplierIdsWithLedgerActivityAsync(endExclusive, cancellationToken);

            var selections = new List<SupplierLedgerSelection>();
            foreach (var id in selectedIds)
            {
                var supplier = suppliers.FirstOrDefault(s => s.Id == id);
                var partner = partners.FirstOrDefault(p => p.Id == id);

                if (supplier == null && partner != null)
                    supplier = FindMatchingSupplier(partner, suppliers);

                if (partner == null && supplier != null)
                    partner = FindMatchingSupplierPartner(supplier, partners);

                if (supplier == null && partner == null)
                    continue;

                selections.Add(new SupplierLedgerSelection(
                    supplier?.Id,
                    partner?.Id,
                    partner?.PartnerCode ?? supplier?.SupplierCode ?? string.Empty,
                    partner?.PartnerName ?? supplier?.Name ?? "Supplier",
                    partner?.Currency ?? "GHS"));
            }

            return selections
                .GroupBy(s => s.BusinessPartnerId ?? s.SupplierId ?? Guid.Empty)
                .Select(g => g.First())
                .ToList();
        }

        private async Task<HashSet<Guid>> GetSupplierIdsWithLedgerActivityAsync(DateTime endExclusive, CancellationToken cancellationToken)
        {
            var invoiceSupplierIds = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.InvoiceDate < endExclusive &&
                    i.Status != VendorInvoiceStatus.Draft &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Rejected)
                .Select(i => i.SupplierId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var paymentSupplierIds = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.PaymentDate < endExclusive &&
                    p.Status != VendorPaymentStatus.Draft &&
                    p.Status != VendorPaymentStatus.Voided &&
                    p.Status != VendorPaymentStatus.Failed)
                .Select(p => p.SupplierId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var adjustmentSupplierIds = await _unitOfWork.Repository<SubledgerAdjustmentJournal>()
                .GetQueryable(a =>
                    a.TenantId == TenantId &&
                    a.Module == SubledgerModules.AccountsPayable &&
                    a.Status == SubledgerAdjustmentStatuses.Posted &&
                    a.SupplierId.HasValue &&
                    a.AdjustmentDate < endExclusive)
                .Select(a => a.SupplierId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            return invoiceSupplierIds
                .Concat(paymentSupplierIds)
                .Concat(adjustmentSupplierIds)
                .ToHashSet();
        }

        private async Task<List<SupplierLedgerTransaction>> GetSupplierLedgerTransactionsAsync(
            SupplierLedgerSelection selection,
            DateTime startDate,
            DateTime endExclusive,
            string reportCurrencyCode,
            string baseCurrencyCode,
            ICollection<string> warnings,
            CancellationToken cancellationToken)
        {
            if (!selection.SupplierId.HasValue)
                return new List<SupplierLedgerTransaction>();

            var supplierId = selection.SupplierId.Value;
            var transactions = new List<SupplierLedgerTransaction>();

            var invoices = await _unitOfWork.Repository<VendorInvoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.SupplierId == supplierId &&
                    i.InvoiceDate < endExclusive &&
                    i.Status != VendorInvoiceStatus.Draft &&
                    i.Status != VendorInvoiceStatus.Voided &&
                    i.Status != VendorInvoiceStatus.Rejected)
                .ToListAsync(cancellationToken);

            foreach (var invoice in invoices)
            {
                var amount = AmountForLedgerCurrency(
                    invoice.TotalAmount,
                    invoice.BaseCurrencyAmount,
                    invoice.CurrencyCode,
                    invoice.ExchangeRate,
                    reportCurrencyCode,
                    baseCurrencyCode,
                    warnings,
                    invoice.InvoiceNumber);

                transactions.Add(new SupplierLedgerTransaction(
                    invoice.Id,
                    invoice.InvoiceDate,
                    "Invoice",
                    invoice.InvoiceNumber,
                    invoice.SupplierInvoiceNumber ?? invoice.Reference,
                    invoice.Notes ?? "Supplier invoice",
                    NormalizeCurrency(invoice.CurrencyCode, baseCurrencyCode),
                    NormalizeExchangeRate(invoice.ExchangeRate),
                    0m,
                    amount));
            }

            var payments = await _unitOfWork.Repository<VendorPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.SupplierId == supplierId &&
                    p.PaymentDate < endExclusive &&
                    p.Status != VendorPaymentStatus.Draft &&
                    p.Status != VendorPaymentStatus.Voided &&
                    p.Status != VendorPaymentStatus.Failed)
                .Include(p => p.Allocations)
                .ToListAsync(cancellationToken);

            foreach (var payment in payments)
            {
                var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, baseCurrencyCode);
                var paymentExchangeRate = NormalizeExchangeRate(payment.ExchangeRate);
                var reference = payment.TransactionReference ?? payment.ChequeNumber;

                var cashAmount = AmountForLedgerCurrency(
                    payment.TotalAmount,
                    null,
                    paymentCurrency,
                    paymentExchangeRate,
                    reportCurrencyCode,
                    baseCurrencyCode,
                    warnings,
                    payment.PaymentNumber);

                transactions.Add(new SupplierLedgerTransaction(
                    payment.Id,
                    payment.PaymentDate,
                    "Payment",
                    payment.PaymentNumber,
                    reference,
                    $"Supplier payment - {payment.PaymentMethod}",
                    paymentCurrency,
                    paymentExchangeRate,
                    cashAmount,
                    0m));

                var activeAllocations = payment.Allocations.Where(a => !a.IsReversal).ToList();
                var discountTaken = activeAllocations.Sum(a => a.DiscountAmount);
                if (discountTaken == 0m)
                    discountTaken = payment.DiscountTaken;

                if (discountTaken > 0m)
                {
                    var discountAmount = AmountForLedgerCurrency(
                        discountTaken,
                        null,
                        paymentCurrency,
                        paymentExchangeRate,
                        reportCurrencyCode,
                        baseCurrencyCode,
                        warnings,
                        payment.PaymentNumber);

                    transactions.Add(new SupplierLedgerTransaction(
                        payment.Id,
                        payment.PaymentDate,
                        "Discount Taken",
                        payment.PaymentNumber,
                        reference,
                        "Supplier settlement discount",
                        paymentCurrency,
                        paymentExchangeRate,
                        discountAmount,
                        0m));
                }

                var withholdingTaxAmount = activeAllocations.Sum(a => a.WithholdingTaxAmount);
                if (withholdingTaxAmount == 0m)
                    withholdingTaxAmount = payment.WithholdingTaxAmount;

                if (withholdingTaxAmount > 0m)
                {
                    var whtAmount = AmountForLedgerCurrency(
                        withholdingTaxAmount,
                        null,
                        paymentCurrency,
                        paymentExchangeRate,
                        reportCurrencyCode,
                        baseCurrencyCode,
                        warnings,
                        payment.PaymentNumber);

                    transactions.Add(new SupplierLedgerTransaction(
                        payment.Id,
                        payment.PaymentDate,
                        "Withholding Tax",
                        payment.PaymentNumber,
                        payment.WithholdingCertificateNumber ?? reference,
                        "Withholding tax on supplier payment",
                        paymentCurrency,
                        paymentExchangeRate,
                        whtAmount,
                        0m));
                }
            }

            var adjustments = await GetPostedApAdjustmentsAsync(supplierId, cancellationToken);
            foreach (var adjustment in adjustments.Where(a => a.AdjustmentDate < endExclusive))
            {
                var signedAmount = GetSignedApSubledgerAmount(adjustment);
                if (signedAmount == 0m)
                    continue;

                var adjustmentAmount = AmountForLedgerCurrency(
                    Math.Abs(signedAmount),
                    Math.Abs(adjustment.BaseCurrencyAmount),
                    adjustment.CurrencyCode,
                    adjustment.ExchangeRate,
                    reportCurrencyCode,
                    baseCurrencyCode,
                    warnings,
                    adjustment.AdjustmentNumber);

                transactions.Add(new SupplierLedgerTransaction(
                    adjustment.Id,
                    adjustment.AdjustmentDate,
                    GetAdjustmentTransactionType(adjustment),
                    adjustment.AdjustmentNumber,
                    adjustment.Reference,
                    adjustment.Reason,
                    NormalizeCurrency(adjustment.CurrencyCode, baseCurrencyCode),
                    NormalizeExchangeRate(adjustment.ExchangeRate),
                    signedAmount < 0m ? adjustmentAmount : 0m,
                    signedAmount > 0m ? adjustmentAmount : 0m));
            }

            return transactions;
        }

        private static Supplier? FindMatchingSupplier(BusinessPartner partner, IEnumerable<Supplier> suppliers)
        {
            return suppliers.FirstOrDefault(s =>
                s.Id == partner.Id ||
                string.Equals(s.SupplierCode, partner.PartnerCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Name, partner.PartnerName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(s.Email) &&
                 !string.IsNullOrWhiteSpace(partner.PrimaryEmail) &&
                 string.Equals(s.Email, partner.PrimaryEmail, StringComparison.OrdinalIgnoreCase)));
        }

        private static BusinessPartner? FindMatchingSupplierPartner(Supplier supplier, IEnumerable<BusinessPartner> partners)
        {
            return partners.FirstOrDefault(p =>
                p.Id == supplier.Id ||
                string.Equals(p.PartnerCode, supplier.SupplierCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.PartnerName, supplier.Name, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(p.PrimaryEmail) &&
                 !string.IsNullOrWhiteSpace(supplier.Email) &&
                 string.Equals(p.PrimaryEmail, supplier.Email, StringComparison.OrdinalIgnoreCase)));
        }

        private static decimal AmountForLedgerCurrency(
            decimal transactionAmount,
            decimal? baseCurrencyAmount,
            string? transactionCurrencyCode,
            decimal exchangeRate,
            string reportCurrencyCode,
            string baseCurrencyCode,
            ICollection<string> warnings,
            string documentNumber)
        {
            var transactionCurrency = NormalizeCurrency(transactionCurrencyCode, baseCurrencyCode);
            var reportCurrency = NormalizeCurrency(reportCurrencyCode, baseCurrencyCode);
            var baseCurrency = NormalizeCurrency(baseCurrencyCode, "GHS");

            if (string.Equals(reportCurrency, transactionCurrency, StringComparison.OrdinalIgnoreCase))
                return RoundMoney(transactionAmount);

            var resolvedBaseAmount = baseCurrencyAmount.HasValue && baseCurrencyAmount.Value != 0m
                ? baseCurrencyAmount.Value
                : ToBaseCurrencyAmount(transactionAmount, transactionCurrency, baseCurrency, exchangeRate);

            if (string.Equals(reportCurrency, baseCurrency, StringComparison.OrdinalIgnoreCase))
                return RoundMoney(resolvedBaseAmount);

            warnings.Add($"Document {documentNumber} is in {transactionCurrency}; shown using base currency {baseCurrency} because no direct {reportCurrency} amount is stored.");
            return RoundMoney(resolvedBaseAmount);
        }

        private static decimal ToBaseCurrencyAmount(decimal transactionAmount, string transactionCurrencyCode, string baseCurrencyCode, decimal exchangeRate)
        {
            if (string.Equals(transactionCurrencyCode, baseCurrencyCode, StringComparison.OrdinalIgnoreCase))
                return transactionAmount;

            return transactionAmount * NormalizeExchangeRate(exchangeRate);
        }

        private static string NormalizeCurrency(string? currencyCode, string fallback)
        {
            return string.IsNullOrWhiteSpace(currencyCode)
                ? fallback.Trim().ToUpperInvariant()
                : currencyCode.Trim().ToUpperInvariant();
        }

        private static decimal NormalizeExchangeRate(decimal exchangeRate)
        {
            return exchangeRate <= 0m ? 1m : exchangeRate;
        }

        private static decimal RoundMoney(decimal amount)
        {
            return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        }

        private sealed record SupplierLedgerSelection(
            Guid? SupplierId,
            Guid? BusinessPartnerId,
            string SupplierCode,
            string SupplierName,
            string CurrencyCode);

        private sealed record SupplierLedgerTransaction(
            Guid SourceDocumentId,
            DateTime TransactionDate,
            string TransactionType,
            string DocumentNumber,
            string? Reference,
            string Description,
            string TransactionCurrencyCode,
            decimal ExchangeRate,
            decimal Debit,
            decimal Credit);

        private async Task RecordReportAuditAsync(string eventType, object report, CancellationToken cancellationToken)
        {
            if (_financeAuditService == null)
            {
                return;
            }

            try
            {
                await _financeAuditService.RecordAsync(new FinanceAuditEventDto
                {
                    EventType = eventType,
                    TenantId = TenantId,
                    SourceModule = "AP",
                    SourceDocumentType = "AgingReport",
                    Resource = "Finance.AP.AgingReport",
                    ResourceId = TenantId.ToString(),
                    AfterValues = report
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record AP report audit event {EventType}", eventType);
            }
        }

        private static string Csv(string value)
        {
            if (value.Contains('"', StringComparison.Ordinal))
            {
                value = value.Replace("\"", "\"\"", StringComparison.Ordinal);
            }

            return value.Contains(',', StringComparison.Ordinal) ||
                   value.Contains('\r', StringComparison.Ordinal) ||
                   value.Contains('\n', StringComparison.Ordinal) ||
                   value.Contains('"', StringComparison.Ordinal)
                ? $"\"{value}\""
                : value;
        }
    }
}
