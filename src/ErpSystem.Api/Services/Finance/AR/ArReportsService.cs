using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
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

namespace ErpSystem.Api.Services.Finance.AR
{
    public class ArReportsService : IArReportsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<ArReportsService> _logger;
        private readonly ISubledgerSettlementReadModelService _settlementReadModelService;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly ITenantSettingsService? _tenantSettingsService;

        public ArReportsService(
            IUnitOfWork _unitOfWork,
            ICurrentUserService currentUser,
            ILogger<ArReportsService> logger,
            ISubledgerSettlementReadModelService settlementReadModelService,
            IFinanceAuditService? financeAuditService = null,
            ITenantSettingsService? tenantSettingsService = null)
        {
            this._unitOfWork = _unitOfWork;
            _currentUser = currentUser;
            _logger = logger;
            _settlementReadModelService = settlementReadModelService ?? throw new ArgumentNullException(nameof(settlementReadModelService));
            _financeAuditService = financeAuditService;
            _tenantSettingsService = tenantSettingsService;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

        public async Task<SubledgerUnappliedSettlementReportDto> GetUnappliedSettlementsAsync(
            DateTime? asOfDate = null,
            Guid? customerId = null,
            CancellationToken cancellationToken = default)
        {
            var date = asOfDate ?? DateTime.UtcNow;
            var rebuild = await _settlementReadModelService.RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = SubledgerSettlementModules.AccountsReceivable,
                AsOfDate = date,
                RecordAudit = false
            }, cancellationToken);

            var balances = (await _settlementReadModelService.GetUnappliedBalancesAsync(
                    SubledgerSettlementModules.AccountsReceivable,
                    date,
                    customerId,
                    cancellationToken))
                .Where(b => b.UnappliedAmount != 0m)
                .ToList();

            var customerIds = balances.Select(b => b.CounterpartyId).Distinct().ToList();
            var customerNames = await _unitOfWork.Repository<BusinessPartner>()
                .GetQueryable(p => p.TenantId == TenantId && customerIds.Contains(p.Id) && !p.IsDeleted)
                .Select(p => new { p.Id, p.PartnerName })
                .ToDictionaryAsync(p => p.Id, p => p.PartnerName, cancellationToken);

            var report = new SubledgerUnappliedSettlementReportDto
            {
                SourceModule = SubledgerSettlementModules.AccountsReceivable,
                AsOfDate = date,
                TotalUnappliedAmount = balances.Sum(b => b.UnappliedAmount),
                CounterpartyCount = balances.Select(b => b.CounterpartyId).Distinct().Count(),
                Diagnostics = rebuild.Diagnostics
            };

            report.Lines = balances.Select(b => new SubledgerUnappliedSettlementBalanceDto
            {
                Id = b.Id,
                SourceModule = b.SourceModule,
                CounterpartyId = b.CounterpartyId,
                CounterpartyName = customerNames.GetValueOrDefault(b.CounterpartyId) ?? "Customer",
                SettlementSourceType = b.SettlementSourceType,
                SettlementSourceId = b.SettlementSourceId,
                SettlementSourceNumber = b.SettlementSourceNumber,
                Classification = b.Classification,
                SettlementPostingEventId = b.SettlementPostingEventId,
                SettlementJournalEntryId = b.SettlementJournalEntryId,
                SettlementDate = b.SettlementDate,
                DocumentCurrencyCode = b.DocumentCurrencyCode,
                FunctionalCurrencyCode = b.FunctionalCurrencyCode,
                OriginalAmount = b.OriginalAmount,
                AppliedAmount = b.AppliedAmount,
                UnappliedAmount = b.UnappliedAmount,
                HasDiagnostics = b.HasDiagnostics,
                DiagnosticFlags = b.DiagnosticFlags
            }).ToList();

            await RecordReportAuditAsync(FinanceAuditEvents.ArUnappliedSettlementsGenerated, report, cancellationToken);
            return report;
        }

        public async Task<AgingReportDto> GetAgingReportAsync(DateTime? asOfDate = null, Guid? customerId = null, CancellationToken cancellationToken = default)
        {
            // The constructor requires the projection service. This branch is deliberately
            // retained as the only supported report path; legacy snapshots cannot be selected.
            if (_settlementReadModelService != null)
                return await GetSettlementReadModelAgingReportAsync(asOfDate, customerId, cancellationToken);

            var effectiveDate = asOfDate ?? DateTime.UtcNow;

            var query = _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    (i.TotalAmount - i.PaidAmount) > 0 &&
                    (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Overdue));
            query = ApplyPostedArInvoiceFilter(query);

            if (customerId.HasValue)
                query = query.Where(i => i.BusinessPartnerId == customerId.Value);

            var invoices = await query.ToListAsync(cancellationToken);
            var adjustments = await GetPostedArAdjustmentsAsync(customerId, cancellationToken);

            var customerAging = new List<CustomerAgingDto>();
            var customerAgingById = new Dictionary<Guid, CustomerAgingDto>();

            CustomerAgingDto GetOrCreateCustomerAging(Guid id, string name)
            {
                if (customerAgingById.TryGetValue(id, out var existing))
                    return existing;

                var aging = new CustomerAgingDto
                {
                    CustomerId = id,
                    CustomerCode = string.Empty,
                    CustomerName = name,
                    Phone = null,
                    Email = null
                };
                customerAgingById[id] = aging;
                customerAging.Add(aging);
                return aging;
            }

            foreach (var invoice in invoices)
            {
                var aging = GetOrCreateCustomerAging(invoice.CustomerId, invoice.CustomerName);
                AddToAgingBucket(aging, invoice.BalanceAmount, invoice.DueDate, invoice.InvoiceDate, effectiveDate);
            }

            foreach (var adjustment in adjustments)
            {
                var amount = GetSignedSubledgerAmount(adjustment);
                if (amount == 0)
                    continue;

                var aging = GetOrCreateCustomerAging(
                    adjustment.BusinessPartnerId,
                    adjustment.BusinessPartnerName);
                AddToAgingBucket(aging, amount, adjustment.DueDate, adjustment.AdjustmentDate, effectiveDate);
            }

            foreach (var aging in customerAging)
                aging.TotalOutstanding = aging.Current + aging.Days1To30 + aging.Days31To60 + aging.Days61To90 + aging.Days90Plus;

            var report = new AgingReportDto
            {
                AsOfDate = effectiveDate,
                Customers = customerAging.Where(c => c.TotalOutstanding != 0).OrderByDescending(c => c.TotalOutstanding).ToList(),
                Summary = new AgingSummaryDto
                {
                    TotalCurrent = customerAging.Sum(c => c.Current),
                    TotalDays1To30 = customerAging.Sum(c => c.Days1To30),
                    TotalDays31To60 = customerAging.Sum(c => c.Days31To60),
                    TotalDays61To90 = customerAging.Sum(c => c.Days61To90),
                    TotalDays90Plus = customerAging.Sum(c => c.Days90Plus),
                    GrandTotal = customerAging.Sum(c => c.TotalOutstanding),
                    TotalCustomers = customerAging.Count,
                    OverdueCustomers = customerAging.Count(c => c.Days1To30 + c.Days31To60 + c.Days61To90 + c.Days90Plus > 0)
                }
};
            
            // Populate Buckets
            report.Buckets = new List<AgingBucketDto>
            {
                new AgingBucketDto
                {
                    BucketName = "Current",
                    Amount = report.Summary.TotalCurrent,
                    CustomerCount = customerAging.Count(c => c.Current > 0),
                    Percentage = report.Summary.GrandTotal > 0 ? (report.Summary.TotalCurrent / report.Summary.GrandTotal) * 100 : 0
                },
                new AgingBucketDto
                {
                    BucketName = "1-30 Days",
                    Amount = report.Summary.TotalDays1To30,
                    CustomerCount = customerAging.Count(c => c.Days1To30 > 0),
                    Percentage = report.Summary.GrandTotal > 0 ? (report.Summary.TotalDays1To30 / report.Summary.GrandTotal) * 100 : 0
                },
                new AgingBucketDto
                {
                    BucketName = "31-60 Days",
                    Amount = report.Summary.TotalDays31To60,
                    CustomerCount = customerAging.Count(c => c.Days31To60 > 0),
                    Percentage = report.Summary.GrandTotal > 0 ? (report.Summary.TotalDays31To60 / report.Summary.GrandTotal) * 100 : 0
                },
                new AgingBucketDto
                {
                    BucketName = "61-90 Days",
                    Amount = report.Summary.TotalDays61To90,
                    CustomerCount = customerAging.Count(c => c.Days61To90 > 0),
                    Percentage = report.Summary.GrandTotal > 0 ? (report.Summary.TotalDays61To90 / report.Summary.GrandTotal) * 100 : 0
                },
                new AgingBucketDto
                {
                    BucketName = "90+ Days",
                    Amount = report.Summary.TotalDays90Plus,
                    CustomerCount = customerAging.Count(c => c.Days90Plus > 0),
                    Percentage = report.Summary.GrandTotal > 0 ? (report.Summary.TotalDays90Plus / report.Summary.GrandTotal) * 100 : 0
                }
            };

            _logger.LogInformation("Generated aging report as of {Date}: {Total:C} outstanding across {Count} customers",
                effectiveDate, report.Summary.GrandTotal, report.Summary.TotalCustomers);

            return report;
        }

        public async Task<DetailedAgingReportDto> GetDetailedAgingReportAsync(DateTime? asOfDate = null, Guid? customerId = null, CancellationToken cancellationToken = default)
        {
            // See GetAgingReportAsync: mandatory dependency prevents a silent legacy fallback.
            if (_settlementReadModelService != null)
                return await GetSettlementReadModelDetailedAgingReportAsync(asOfDate, customerId, cancellationToken);

            var effectiveDate = asOfDate ?? DateTime.UtcNow;

            var query = _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    (i.TotalAmount - i.PaidAmount) > 0 &&
                    (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Overdue));
            query = ApplyPostedArInvoiceFilter(query);

            if (customerId.HasValue)
                query = query.Where(i => i.BusinessPartnerId == customerId.Value);

            var invoices = await query.ToListAsync(cancellationToken);
            var adjustments = await GetPostedArAdjustmentsAsync(customerId, cancellationToken);

            var customerDetailedAging = new List<CustomerDetailedAgingDto>();
            var customerDetailedById = new Dictionary<Guid, CustomerDetailedAgingDto>();

            CustomerDetailedAgingDto GetOrCreateDetailed(Guid id, string name)
            {
                if (customerDetailedById.TryGetValue(id, out var existing))
                    return existing;

                var detailedAging = new CustomerDetailedAgingDto
                {
                    CustomerId = id,
                    CustomerCode = string.Empty,
                    CustomerName = name
                };
                customerDetailedById[id] = detailedAging;
                customerDetailedAging.Add(detailedAging);
                return detailedAging;
            }

            foreach (var invoice in invoices)
            {
                var detailedAging = GetOrCreateDetailed(invoice.CustomerId, invoice.CustomerName);
                var daysOverdue = GetDaysOverdue(invoice.DueDate, invoice.InvoiceDate, effectiveDate);

                detailedAging.Invoices.Add(new InvoiceAgingDto
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    DueDate = invoice.DueDate,
                    DaysOverdue = Math.Max(0, daysOverdue),
                    TotalAmount = invoice.TotalAmount,
                    PaidAmount = invoice.PaidAmount,
                    BalanceAmount = invoice.BalanceAmount,
                    AgingBucket = GetAgingBucket(daysOverdue, invoice.DueDate)
                });
            }

            foreach (var adjustment in adjustments)
            {
                var amount = GetSignedSubledgerAmount(adjustment);
                if (amount == 0)
                    continue;

                var detailedAging = GetOrCreateDetailed(
                    adjustment.BusinessPartnerId,
                    adjustment.BusinessPartnerName);
                var daysOverdue = GetDaysOverdue(adjustment.DueDate, adjustment.AdjustmentDate, effectiveDate);

                detailedAging.Invoices.Add(new InvoiceAgingDto
                {
                    InvoiceId = adjustment.Id,
                    InvoiceNumber = adjustment.AdjustmentNumber,
                    InvoiceDate = adjustment.AdjustmentDate,
                    DueDate = adjustment.DueDate,
                    DaysOverdue = Math.Max(0, daysOverdue),
                    TotalAmount = amount,
                    PaidAmount = 0,
                    BalanceAmount = amount,
                    AgingBucket = GetAgingBucket(daysOverdue, adjustment.DueDate)
                });
            }

            foreach (var detailedAging in customerDetailedAging)
                detailedAging.TotalOutstanding = detailedAging.Invoices.Sum(i => i.BalanceAmount);

            var report = new DetailedAgingReportDto
            {
                AsOfDate = effectiveDate,
                Customers = customerDetailedAging.Where(c => c.TotalOutstanding != 0).OrderByDescending(c => c.TotalOutstanding).ToList(),
                Summary = new AgingSummaryDto
                {
                    GrandTotal = customerDetailedAging.Sum(c => c.TotalOutstanding),
                    TotalCustomers = customerDetailedAging.Count
                }
            };

            return report;
        }

        public Task<SubledgerSettlementRebuildResultDto> RebuildSettlementReadModelAsync(
            DateTime? asOfDate = null,
            CancellationToken cancellationToken = default)
        {
            if (_settlementReadModelService == null)
            {
                throw new InvalidOperationException("AR settlement read-model service is not configured.");
            }

            return _settlementReadModelService.RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = SubledgerSettlementModules.AccountsReceivable,
                AsOfDate = asOfDate
            }, cancellationToken);
        }

        public Task<SubledgerControlReconciliationDto> GetControlReconciliationAsync(
            DateTime? asOfDate = null,
            CancellationToken cancellationToken = default)
        {
            if (_settlementReadModelService == null)
            {
                throw new InvalidOperationException("AR settlement read-model service is not configured.");
            }

            return _settlementReadModelService.GetControlReconciliationAsync(
                SubledgerSettlementModules.AccountsReceivable,
                asOfDate,
                cancellationToken);
        }

        private async Task<AgingReportDto> GetSettlementReadModelAgingReportAsync(
            DateTime? asOfDate,
            Guid? customerId,
            CancellationToken cancellationToken)
        {
            var effectiveDate = asOfDate ?? DateTime.UtcNow;
            var rebuild = await _settlementReadModelService!.RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = SubledgerSettlementModules.AccountsReceivable,
                AsOfDate = effectiveDate,
                RecordAudit = false
            }, cancellationToken);

            var balances = (await _settlementReadModelService.GetBalancesAsync(
                    SubledgerSettlementModules.AccountsReceivable,
                    effectiveDate,
                    customerId,
                    cancellationToken))
                .Where(b => b.OutstandingAmount != 0)
                .ToList();

            var functionalCurrencyCode = await ResolveAgingFunctionalCurrencyAsync(balances);

            var customerNames = await LoadArCustomerNamesAsync(balances.Select(b => b.SourceDocumentId).Distinct().ToList(), cancellationToken);
            var adjustments = await GetPostedArAdjustmentsAsync(customerId, cancellationToken);

            var customerAging = new List<CustomerAgingDto>();
            var byId = new Dictionary<Guid, CustomerAgingDto>();
            CustomerAgingDto GetOrCreate(Guid id, string name)
            {
                if (byId.TryGetValue(id, out var existing))
                    return existing;

                var aging = new CustomerAgingDto
                {
                    CustomerId = id,
                    CustomerCode = string.Empty,
                    CustomerName = name
                };
                byId[id] = aging;
                customerAging.Add(aging);
                return aging;
            }

            foreach (var balance in balances)
            {
                var aging = GetOrCreate(
                    balance.CounterpartyId,
                    customerNames.GetValueOrDefault(balance.SourceDocumentId) ?? "Customer");
                AddToAgingBucket(
                    aging,
                    ToFunctionalAmount(balance, balance.OutstandingAmount),
                    balance.DueDate,
                    balance.TransactionDate,
                    effectiveDate);
            }

            foreach (var adjustment in adjustments)
            {
                var amount = GetSignedSubledgerFunctionalAmount(adjustment);
                if (amount == 0)
                    continue;

                var aging = GetOrCreate(adjustment.BusinessPartnerId, adjustment.BusinessPartnerName);
                AddToAgingBucket(aging, amount, adjustment.DueDate, adjustment.AdjustmentDate, effectiveDate);
            }

            foreach (var aging in customerAging)
                aging.TotalOutstanding = aging.Current + aging.Days1To30 + aging.Days31To60 + aging.Days61To90 + aging.Days90Plus;

            var report = new AgingReportDto
            {
                AsOfDate = effectiveDate,
                CurrencyCode = functionalCurrencyCode,
                UsesSettlementReadModel = true,
                Customers = customerAging.Where(c => c.TotalOutstanding != 0).OrderByDescending(c => c.TotalOutstanding).ToList(),
                Diagnostics = rebuild.Diagnostics,
                Summary = new AgingSummaryDto
                {
                    TotalCurrent = customerAging.Sum(c => c.Current),
                    TotalDays1To30 = customerAging.Sum(c => c.Days1To30),
                    TotalDays31To60 = customerAging.Sum(c => c.Days31To60),
                    TotalDays61To90 = customerAging.Sum(c => c.Days61To90),
                    TotalDays90Plus = customerAging.Sum(c => c.Days90Plus),
                    GrandTotal = customerAging.Sum(c => c.TotalOutstanding),
                    TotalCustomers = customerAging.Count,
                    OverdueCustomers = customerAging.Count(c => c.Days1To30 + c.Days31To60 + c.Days61To90 + c.Days90Plus > 0)
                }
            };

            report.Buckets = BuildAgingBuckets(report.Summary, customerAging);
            await RecordReportAuditAsync(FinanceAuditEvents.ArAgingGeneratedFromSettlementReadModel, report, cancellationToken);
            return report;
        }

        private async Task<DetailedAgingReportDto> GetSettlementReadModelDetailedAgingReportAsync(
            DateTime? asOfDate,
            Guid? customerId,
            CancellationToken cancellationToken)
        {
            var effectiveDate = asOfDate ?? DateTime.UtcNow;
            var rebuild = await _settlementReadModelService!.RebuildAsync(new SubledgerSettlementRebuildRequestDto
            {
                SourceModule = SubledgerSettlementModules.AccountsReceivable,
                AsOfDate = effectiveDate,
                RecordAudit = false
            }, cancellationToken);

            var balances = (await _settlementReadModelService.GetBalancesAsync(
                    SubledgerSettlementModules.AccountsReceivable,
                    effectiveDate,
                    customerId,
                    cancellationToken))
                .Where(b => b.OutstandingAmount != 0)
                .ToList();

            var functionalCurrencyCode = await ResolveAgingFunctionalCurrencyAsync(balances);

            var customerNames = await LoadArCustomerNamesAsync(balances.Select(b => b.SourceDocumentId).Distinct().ToList(), cancellationToken);
            var adjustments = await GetPostedArAdjustmentsAsync(customerId, cancellationToken);
            var detailed = new List<CustomerDetailedAgingDto>();
            var byId = new Dictionary<Guid, CustomerDetailedAgingDto>();
            CustomerDetailedAgingDto GetOrCreate(Guid id, string name)
            {
                if (byId.TryGetValue(id, out var existing))
                    return existing;

                var row = new CustomerDetailedAgingDto
                {
                    CustomerId = id,
                    CustomerCode = string.Empty,
                    CustomerName = name
                };
                byId[id] = row;
                detailed.Add(row);
                return row;
            }

            foreach (var balance in balances)
            {
                var row = GetOrCreate(
                    balance.CounterpartyId,
                    customerNames.GetValueOrDefault(balance.SourceDocumentId) ?? "Customer");
                row.Invoices.Add(MapArBalanceToAgingInvoice(balance, effectiveDate));
            }

            foreach (var adjustment in adjustments)
            {
                var amount = GetSignedSubledgerFunctionalAmount(adjustment);
                if (amount == 0)
                    continue;

                var row = GetOrCreate(adjustment.BusinessPartnerId, adjustment.BusinessPartnerName);
                row.Invoices.Add(MapArAdjustmentToAgingInvoice(adjustment, effectiveDate, functionalCurrencyCode));
            }

            foreach (var row in detailed)
                row.TotalOutstanding = row.Invoices.Sum(i => i.BalanceAmount);

            var customerRows = detailed.Where(c => c.TotalOutstanding != 0).OrderByDescending(c => c.TotalOutstanding).ToList();
            var report = new DetailedAgingReportDto
            {
                AsOfDate = effectiveDate,
                CurrencyCode = functionalCurrencyCode,
                UsesSettlementReadModel = true,
                Customers = customerRows,
                Diagnostics = rebuild.Diagnostics,
                Summary = new AgingSummaryDto
                {
                    GrandTotal = customerRows.Sum(c => c.TotalOutstanding),
                    TotalCustomers = customerRows.Count
                }
            };

            await RecordReportAuditAsync(FinanceAuditEvents.ArAgingGeneratedFromSettlementReadModel, report, cancellationToken);
            return report;
        }

        public async Task<CustomerStatementDto> GetCustomerStatementAsync(Guid customerId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        {
            var customer = await _unitOfWork.Repository<BusinessPartner>()
                .FirstOrDefaultAsync(c =>
                    c.TenantId == TenantId &&
                    c.Id == customerId &&
                    !c.IsDeleted &&
                    (c.PartnerType == "Customer" || c.PartnerType == "Both"));

            if (customer == null)
                throw new KeyNotFoundException($"Customer with Id '{customerId}' not found.");

            // Get opening balance (invoices before fromDate)
            var openingInvoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.BusinessPartnerId == customerId &&
                    i.InvoiceDate < fromDate)
                .ToListAsync(cancellationToken);

            var openingPayments = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.CustomerId == customerId &&
                    p.PaymentDate < fromDate)
                .ToListAsync(cancellationToken);

            var openingAdjustments = await GetPostedArAdjustmentsAsync(customerId, cancellationToken);
            var openingBalance = openingInvoices.Sum(i => i.TotalAmount)
                - openingPayments.Sum(p => p.AllocatedAmount)
                + openingAdjustments
                    .Where(a => a.AdjustmentDate < fromDate)
                    .Sum(GetSignedSubledgerAmount);

            // Get transactions in period
            var periodInvoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.BusinessPartnerId == customerId &&
                    i.InvoiceDate >= fromDate &&
                    i.InvoiceDate <= toDate)
                .OrderBy(i => i.InvoiceDate)
                .ToListAsync(cancellationToken);

            var periodPayments = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.CustomerId == customerId &&
                    p.PaymentDate >= fromDate &&
                    p.PaymentDate <= toDate)
                .OrderBy(p => p.PaymentDate)
                .ToListAsync(cancellationToken);

            var periodAdjustments = openingAdjustments
                .Where(a => a.AdjustmentDate >= fromDate && a.AdjustmentDate <= toDate)
                .ToList();

            var transactions = new List<StatementTransactionDto>();
            var runningBalance = openingBalance;

            var orderedTransactions = periodInvoices
                .Select(invoice => new StatementTransactionDto
                {
                    TransactionDate = invoice.InvoiceDate,
                    TransactionType = "Invoice",
                    Reference = invoice.InvoiceNumber,
                    Description = invoice.Notes ?? "Sales Invoice",
                    Debit = invoice.TotalAmount,
                    Credit = 0
                })
                .Concat(periodPayments.Select(payment => new StatementTransactionDto
                {
                    TransactionDate = payment.PaymentDate,
                    TransactionType = payment.IsCreditNote ? "CreditNote" : "Payment",
                    Reference = payment.PaymentNumber,
                    Description = payment.Notes ?? $"Payment - {payment.PaymentMethod}",
                    Debit = 0,
                    Credit = payment.AllocatedAmount
                }))
                .Concat(periodAdjustments.Select(adjustment =>
                {
                    var amount = GetSignedSubledgerAmount(adjustment);
                    return new StatementTransactionDto
                    {
                        TransactionDate = adjustment.AdjustmentDate,
                        TransactionType = GetAdjustmentTransactionType(adjustment),
                        Reference = adjustment.AdjustmentNumber,
                        Description = adjustment.Reason,
                        Debit = amount > 0 ? amount : 0,
                        Credit = amount < 0 ? Math.Abs(amount) : 0
                    };
                }))
                .OrderBy(t => t.TransactionDate)
                .ThenBy(t => t.Reference)
                .ToList();

            foreach (var transaction in orderedTransactions)
            {
                runningBalance += transaction.Debit - transaction.Credit;
                transaction.Balance = runningBalance;
                transactions.Add(transaction);
            }

            var statement = new CustomerStatementDto
            {
                CustomerId = customerId,
                CustomerCode = customer.CustomerAccountNumber ?? customer.PartnerCode,
                CustomerName = customer.PartnerName,
                CustomerAddress = customer.PhysicalAddress ?? customer.MailingAddress,
                FromDate = fromDate,
                ToDate = toDate,
                OpeningBalance = openingBalance,
                Transactions = transactions,
                TotalInvoices = periodInvoices.Sum(i => i.TotalAmount) + periodAdjustments.Where(a => GetSignedSubledgerAmount(a) > 0).Sum(GetSignedSubledgerAmount),
                TotalPayments = periodPayments.Sum(p => p.AllocatedAmount) + periodAdjustments.Where(a => GetSignedSubledgerAmount(a) < 0).Sum(a => Math.Abs(GetSignedSubledgerAmount(a))),
                ClosingBalance = runningBalance
            };

            return statement;
        }

        public async Task<CustomerDetailedLedgerReportDto> GetCustomerDetailedLedgerAsync(
            DateTime fromDate,
            DateTime toDate,
            IReadOnlyCollection<Guid>? customerIds = null,
            bool showCustomerCurrency = false,
            CancellationToken cancellationToken = default)
        {
            var startDate = fromDate.Date;
            var endDate = toDate.Date;
            if (endDate < startDate)
                throw new ArgumentException("The end date must be on or after the start date.");

            var endExclusive = endDate.AddDays(1);
            var baseCurrencyCode = NormalizeCurrency(
                _tenantSettingsService == null ? "GHS" : await _tenantSettingsService.GetBaseCurrencyAsync(),
                "GHS");
            var requestedCustomerIds = customerIds?
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList() ?? new List<Guid>();

            var customers = await GetCustomerLedgerSelectionsAsync(requestedCustomerIds, endExclusive, cancellationToken);
            var report = new CustomerDetailedLedgerReportDto
            {
                FromDate = startDate,
                ToDate = endDate,
                CurrencyCode = showCustomerCurrency ? "Customer Currency" : baseCurrencyCode,
                ShowCustomerCurrency = showCustomerCurrency
            };

            foreach (var customer in customers.OrderBy(c => c.CustomerName).ThenBy(c => c.CustomerCode))
            {
                var reportCurrencyCode = showCustomerCurrency
                    ? NormalizeCurrency(customer.CurrencyCode, baseCurrencyCode)
                    : baseCurrencyCode;

                var transactions = await GetCustomerLedgerTransactionsAsync(customer, endExclusive, reportCurrencyCode, baseCurrencyCode, report.Warnings, cancellationToken);
                var openingBalance = transactions
                    .Where(t => t.TransactionDate.Date < startDate)
                    .Sum(t => t.Debit - t.Credit);

                var periodTransactions = transactions
                    .Where(t => t.TransactionDate.Date >= startDate && t.TransactionDate < endExclusive)
                    .OrderBy(t => t.TransactionDate)
                    .ThenBy(t => t.TransactionType)
                    .ThenBy(t => t.DocumentNumber)
                    .ToList();

                var runningBalance = openingBalance;
                var lines = new List<CustomerDetailedLedgerLineDto>();

                foreach (var transaction in periodTransactions)
                {
                    runningBalance += transaction.Debit - transaction.Credit;
                    lines.Add(new CustomerDetailedLedgerLineDto
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
                var closingBalance = openingBalance + totalDebits - totalCredits;

                if (requestedCustomerIds.Count == 0 && openingBalance == 0m && closingBalance == 0m && lines.Count == 0)
                    continue;

                report.Customers.Add(new CustomerDetailedLedgerAccountDto
                {
                    CustomerId = customer.CustomerId,
                    CustomerCode = customer.CustomerCode,
                    CustomerName = customer.CustomerName,
                    CurrencyCode = reportCurrencyCode,
                    OpeningBalance = RoundMoney(openingBalance),
                    TotalDebits = RoundMoney(totalDebits),
                    TotalCredits = RoundMoney(totalCredits),
                    ClosingBalance = RoundMoney(closingBalance),
                    Lines = lines
                });
            }

            report.TotalOpeningBalance = RoundMoney(report.Customers.Sum(c => c.OpeningBalance));
            report.TotalDebits = RoundMoney(report.Customers.Sum(c => c.TotalDebits));
            report.TotalCredits = RoundMoney(report.Customers.Sum(c => c.TotalCredits));
            report.TotalClosingBalance = RoundMoney(report.Customers.Sum(c => c.ClosingBalance));
            report.CurrencyTotals = report.Customers
                .GroupBy(c => c.CurrencyCode, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new DetailedLedgerCurrencyTotalDto
                {
                    CurrencyCode = group.Key,
                    OpeningBalance = RoundMoney(group.Sum(c => c.OpeningBalance)),
                    TotalDebits = RoundMoney(group.Sum(c => c.TotalDebits)),
                    TotalCredits = RoundMoney(group.Sum(c => c.TotalCredits)),
                    ClosingBalance = RoundMoney(group.Sum(c => c.ClosingBalance))
                })
                .ToList();
            report.Warnings = report.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            return report;
        }

        public async Task<CollectionsDashboardDto> GetCollectionsDashboardAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var overdueQuery = _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.DueDate.HasValue &&
                    i.DueDate.Value < now &&
                    (i.TotalAmount - i.PaidAmount) > 0);
            var overdueInvoices = await ApplyPostedArInvoiceFilter(overdueQuery).ToListAsync(cancellationToken);

            var customerGroups = overdueInvoices
                .GroupBy(i => new { i.CustomerId, i.CustomerName })
                .Select(g => new OverdueCustomerDto
                {
                    CustomerId = g.Key.CustomerId,
                    CustomerName = g.Key.CustomerName,
                    TotalOverdue = g.Sum(i => i.BalanceAmount),
                    OverdueInvoiceCount = g.Count(),
                    DaysOldest = g.Max(i => i.DueDate.HasValue ? (now - i.DueDate.Value).Days : 0),
                    Phone = null,
                    Email = null,
                    Priority = g.Sum(i => i.BalanceAmount) > 10000 ? "High" :
                              g.Sum(i => i.BalanceAmount) > 5000 ? "Medium" : "Low"
                })
                .OrderByDescending(c => c.TotalOverdue)
                .Take(50)
                .ToList();

            var outstandingQuery = _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    (i.TotalAmount - i.PaidAmount) > 0);
            var allOutstanding = await ApplyPostedArInvoiceFilter(outstandingQuery)
                .SumAsync(i => i.TotalAmount - i.PaidAmount, cancellationToken);
            var allAdjustments = await GetPostedArAdjustmentsAsync(null, cancellationToken);
            var adjustmentOutstanding = allAdjustments.Sum(GetSignedSubledgerAmount);
            var overdueAdjustmentAmount = allAdjustments
                .Where(a => GetSignedSubledgerAmount(a) > 0 && (a.DueDate ?? a.AdjustmentDate) < now)
                .Sum(GetSignedSubledgerAmount);

            var dashboard = new CollectionsDashboardDto
            {
                TotalOutstanding = allOutstanding + adjustmentOutstanding,
                TotalOverdue = overdueInvoices.Sum(i => i.BalanceAmount) + overdueAdjustmentAmount,
                OverdueInvoiceCount = overdueInvoices.Count,
                OverdueCustomers = customerGroups
            };

            return dashboard;
        }

        public async Task<ArSummaryDto> GetArSummaryAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var allInvoiceQuery = _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId);
            var allInvoices = await ApplyPostedArInvoiceFilter(allInvoiceQuery).ToListAsync(cancellationToken);

            var overdueInvoices = allInvoices.Where(i =>
                i.DueDate.HasValue &&
                i.DueDate.Value < now &&
                i.BalanceAmount > 0).ToList();
            var adjustments = await GetPostedArAdjustmentsAsync(null, cancellationToken);
            var positiveAdjustments = adjustments.Where(a => GetSignedSubledgerAmount(a) > 0).ToList();
            var overdueAdjustments = positiveAdjustments
                .Where(a => (a.DueDate ?? a.AdjustmentDate) < now)
                .ToList();
            var currentAdjustmentAmount = adjustments
                .Where(a => (a.DueDate ?? a.AdjustmentDate) >= now)
                .Sum(GetSignedSubledgerAmount);

            var summary = new ArSummaryDto
            {
                TotalOutstanding = allInvoices.Where(i => i.BalanceAmount > 0).Sum(i => i.BalanceAmount)
                    + adjustments.Sum(GetSignedSubledgerAmount),
                TotalOverdue = overdueInvoices.Sum(i => i.BalanceAmount)
                    + overdueAdjustments.Sum(GetSignedSubledgerAmount),
                TotalCurrent = allInvoices.Where(i =>
                    i.BalanceAmount > 0 &&
                    (!i.DueDate.HasValue || i.DueDate.Value >= now)).Sum(i => i.BalanceAmount)
                    + currentAdjustmentAmount,
                TotalInvoices = allInvoices.Count,
                OverdueInvoices = overdueInvoices.Count + overdueAdjustments.Count,
                // Simplified calculation - would need payment history for accurate DSO
                AverageDaysToPayment = 30, // Placeholder
                BadDebtProvision = 0, // Would be configured
                CollectionRate = 0 // Would need period calculation
            };

            return summary;
        }

        public async Task<List<SalesSummaryDto>> GetSalesSummaryAsync(SalesSummaryQueryDto query, CancellationToken cancellationToken = default)
        {
            var invoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.InvoiceDate >= query.FromDate &&
                    i.InvoiceDate <= query.ToDate &&
                    (!query.CustomerId.HasValue || i.BusinessPartnerId == query.CustomerId.Value))
                .ToListAsync(cancellationToken);

            // Group by the specified dimension
            var summary = new List<SalesSummaryDto>();

            if (query.GroupBy == "Customer")
            {
                var customerGroups = invoices.GroupBy(i => new { i.CustomerId, i.CustomerName });
                summary = customerGroups.Select(g => new SalesSummaryDto
                {
                    CustomerId = g.Key.CustomerId,
                    CustomerName = g.Key.CustomerName,
                    TotalSales = g.Sum(i => i.TotalAmount),
                    TotalCollected = g.Sum(i => i.PaidAmount),
                    Outstanding = g.Sum(i => i.BalanceAmount),
                    InvoiceCount = g.Count()
                }).ToList();
            }
            else
            {
                // Group by period (Month default)
                summary.Add(new SalesSummaryDto
                {
                    Period = $"{query.FromDate:yyyy-MM} to {query.ToDate:yyyy-MM}",
                    TotalSales = invoices.Sum(i => i.TotalAmount),
                    TotalCollected = invoices.Sum(i => i.PaidAmount),
                    Outstanding = invoices.Sum(i => i.BalanceAmount),
                    InvoiceCount = invoices.Count
                });
            }

            return summary;
        }

        public async Task<List<PaymentTrendDto>> GetPaymentTrendsAsync(DateTime fromDate, DateTime toDate, string groupBy = "Month", CancellationToken cancellationToken = default)
        {
            var payments = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.PaymentDate >= fromDate &&
                    p.PaymentDate <= toDate)
                .ToListAsync(cancellationToken);

            // Group by month for now (could expand to other periods)
            var monthlyGroups = payments.GroupBy(p => new DateTime(p.PaymentDate.Year, p.PaymentDate.Month, 1));

            var trends = monthlyGroups.Select(g => new PaymentTrendDto
            {
                Period = g.Key.ToString("MMM yyyy"),
                PeriodStart = g.Key,
                TotalCollected = g.Sum(p => p.AllocatedAmount),
                PaymentCount = g.Count(),
                AveragePaymentAmount = g.Average(p => p.TotalAmount)
            }).OrderBy(t => t.PeriodStart).ToList();

            return trends;
        }

        public async Task<byte[]> ExportAgingReportAsync(DateTime? asOfDate = null, string format = "Csv", CancellationToken cancellationToken = default)
        {
            if (!string.Equals(format, "Csv", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("AR aging export supports Csv in the backend reporting foundation. Use the central finance report export service for other formats when added.");
            }

            var exportReport = await GetDetailedAgingReportAsync(asOfDate, null, cancellationToken);
            if (!exportReport.UsesSettlementReadModel)
            {
                throw new InvalidOperationException("AR aging export requires the AR settlement read model. Legacy operational-field aging is not allowed for production export.");
            }

            var csv = new StringBuilder();
            csv.AppendLine("Customer,InvoiceNumber,InvoiceDate,DueDate,TotalAmount,PaidAmount,CreditedAmount,WithheldAmount,OutstandingAmount,AgingBucket,SettlementStatus,SourcePostingEventId,SourceJournalEntryId,Diagnostics");
            foreach (var customer in exportReport.Customers)
            {
                foreach (var invoice in customer.Invoices)
                {
                    csv.AppendLine(string.Join(",", new[]
                    {
                        Csv(customer.CustomerName),
                        Csv(invoice.InvoiceNumber),
                        Csv(invoice.InvoiceDate.ToString("yyyy-MM-dd")),
                        Csv(invoice.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty),
                        invoice.TotalAmount.ToString("0.00"),
                        invoice.PaidAmount.ToString("0.00"),
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

            await RecordReportAuditAsync(FinanceAuditEvents.ArAgingExported, exportReport, cancellationToken);
            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> ExportCustomerStatementAsync(Guid customerId, DateTime fromDate, DateTime toDate, string format = "PDF", CancellationToken cancellationToken = default)
        {
            // Placeholder - would integrate with a PDF library
            await Task.CompletedTask;
            throw new NotImplementedException("Export functionality will be implemented with a PDF library.");
        }

        private Task<List<SubledgerAdjustmentJournal>> GetPostedArAdjustmentsAsync(
            Guid? customerId,
            CancellationToken cancellationToken)
        {
            IQueryable<SubledgerAdjustmentJournal> query = _unitOfWork.Repository<SubledgerAdjustmentJournal>()
                .GetQueryable(a =>
                    a.TenantId == TenantId &&
                    a.Module == SubledgerModules.AccountsReceivable &&
                    a.Status == SubledgerAdjustmentStatuses.Posted &&
                    !a.IsDeleted)
                .Include(a => a.BusinessPartner);

            if (customerId.HasValue)
                query = query.Where(a => a.BusinessPartnerId == customerId.Value);

            return query.ToListAsync(cancellationToken);
        }

        private IQueryable<Invoice> ApplyPostedArInvoiceFilter(IQueryable<Invoice> query)
        {
            var postedInvoiceEvents = _unitOfWork.Repository<FinancePostingEvent>()
                .GetQueryable(e =>
                    e.TenantId == TenantId &&
                    e.SourceDocumentType == "CustomerInvoice" &&
                    e.PostingAction == "Post" &&
                    e.PostingStatus == "Posted");

            return query.Where(i => postedInvoiceEvents.Any(e => e.SourceDocumentId == i.Id));
        }

        private static void AddToAgingBucket(
            CustomerAgingDto aging,
            decimal amount,
            DateTime? dueDate,
            DateTime transactionDate,
            DateTime asOfDate)
        {
            var daysOverdue = GetDaysOverdue(dueDate, transactionDate, asOfDate);

            if (daysOverdue < 0 || !dueDate.HasValue)
                aging.Current += amount;
            else if (daysOverdue <= 30)
                aging.Days1To30 += amount;
            else if (daysOverdue <= 60)
                aging.Days31To60 += amount;
            else if (daysOverdue <= 90)
                aging.Days61To90 += amount;
            else
                aging.Days90Plus += amount;
        }

        private static int GetDaysOverdue(DateTime? dueDate, DateTime transactionDate, DateTime asOfDate)
        {
            return dueDate.HasValue
                ? (asOfDate - dueDate.Value).Days
                : (asOfDate - transactionDate).Days;
        }

        private static string GetAgingBucket(int daysOverdue, DateTime? dueDate)
        {
            if (daysOverdue < 0 || !dueDate.HasValue) return "Current";
            if (daysOverdue <= 30) return "1-30 Days";
            if (daysOverdue <= 60) return "31-60 Days";
            if (daysOverdue <= 90) return "61-90 Days";
            return "90+ Days";
        }

        private async Task<Dictionary<Guid, string>> LoadArCustomerNamesAsync(
            IReadOnlyCollection<Guid> invoiceIds,
            CancellationToken cancellationToken)
        {
            if (invoiceIds.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            return await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId && invoiceIds.Contains(i.Id))
                .Select(i => new { i.Id, i.CustomerName })
                .ToDictionaryAsync(i => i.Id, i => i.CustomerName, cancellationToken);
        }

        private static List<AgingBucketDto> BuildAgingBuckets(AgingSummaryDto summary, IReadOnlyCollection<CustomerAgingDto> customerAging)
        {
            return new List<AgingBucketDto>
            {
                new()
                {
                    BucketName = "Current",
                    Amount = summary.TotalCurrent,
                    CustomerCount = customerAging.Count(c => c.Current > 0),
                    Percentage = summary.GrandTotal > 0 ? summary.TotalCurrent / summary.GrandTotal * 100 : 0
                },
                new()
                {
                    BucketName = "1-30 Days",
                    Amount = summary.TotalDays1To30,
                    CustomerCount = customerAging.Count(c => c.Days1To30 > 0),
                    Percentage = summary.GrandTotal > 0 ? summary.TotalDays1To30 / summary.GrandTotal * 100 : 0
                },
                new()
                {
                    BucketName = "31-60 Days",
                    Amount = summary.TotalDays31To60,
                    CustomerCount = customerAging.Count(c => c.Days31To60 > 0),
                    Percentage = summary.GrandTotal > 0 ? summary.TotalDays31To60 / summary.GrandTotal * 100 : 0
                },
                new()
                {
                    BucketName = "61-90 Days",
                    Amount = summary.TotalDays61To90,
                    CustomerCount = customerAging.Count(c => c.Days61To90 > 0),
                    Percentage = summary.GrandTotal > 0 ? summary.TotalDays61To90 / summary.GrandTotal * 100 : 0
                },
                new()
                {
                    BucketName = "90+ Days",
                    Amount = summary.TotalDays90Plus,
                    CustomerCount = customerAging.Count(c => c.Days90Plus > 0),
                    Percentage = summary.GrandTotal > 0 ? summary.TotalDays90Plus / summary.GrandTotal * 100 : 0
                }
            };
        }

        private static InvoiceAgingDto MapArBalanceToAgingInvoice(
            SubledgerSettlementBalance balance,
            DateTime asOfDate)
        {
            var daysOverdue = GetDaysOverdue(balance.DueDate, balance.TransactionDate, asOfDate);
            return new InvoiceAgingDto
            {
                InvoiceId = balance.SourceDocumentId,
                InvoiceNumber = balance.SourceDocumentNumber,
                InvoiceDate = balance.TransactionDate,
                DueDate = balance.DueDate,
                DaysOverdue = Math.Max(0, daysOverdue),
                TotalAmount = RoundMoney(balance.OriginalFunctionalAmount),
                PaidAmount = ToFunctionalAmount(balance, balance.SettledAmount),
                CreditedAmount = ToFunctionalAmount(balance, balance.CreditedAmount),
                WithheldAmount = ToFunctionalAmount(balance, balance.WithheldAmount),
                BalanceAmount = ToFunctionalAmount(balance, balance.OutstandingAmount),
                CurrencyCode = NormalizeCurrency(balance.FunctionalCurrencyCode, "GHS"),
                DocumentCurrencyCode = NormalizeCurrency(balance.DocumentCurrencyCode, "GHS"),
                DocumentTotalAmount = RoundMoney(balance.OriginalDocumentAmount),
                DocumentPaidAmount = RoundMoney(balance.SettledAmount),
                DocumentCreditedAmount = RoundMoney(balance.CreditedAmount),
                DocumentWithheldAmount = RoundMoney(balance.WithheldAmount),
                DocumentBalanceAmount = RoundMoney(balance.OutstandingAmount),
                SourcePostingEventId = balance.SourcePostingEventId,
                SourceJournalEntryId = balance.SourceJournalEntryId,
                SettlementStatus = balance.SettlementStatus,
                DiagnosticFlags = balance.DiagnosticFlags,
                AgingBucket = GetAgingBucket(daysOverdue, balance.DueDate)
            };
        }

        private static InvoiceAgingDto MapArAdjustmentToAgingInvoice(
            SubledgerAdjustmentJournal adjustment,
            DateTime asOfDate,
            string functionalCurrencyCode)
        {
            var amount = GetSignedSubledgerFunctionalAmount(adjustment);
            var documentAmount = GetSignedSubledgerAmount(adjustment);
            var daysOverdue = GetDaysOverdue(adjustment.DueDate, adjustment.AdjustmentDate, asOfDate);
            return new InvoiceAgingDto
            {
                InvoiceId = adjustment.Id,
                InvoiceNumber = adjustment.AdjustmentNumber,
                InvoiceDate = adjustment.AdjustmentDate,
                DueDate = adjustment.DueDate,
                DaysOverdue = Math.Max(0, daysOverdue),
                TotalAmount = amount,
                PaidAmount = 0,
                BalanceAmount = amount,
                CurrencyCode = functionalCurrencyCode,
                DocumentCurrencyCode = NormalizeCurrency(adjustment.CurrencyCode, "GHS"),
                DocumentTotalAmount = documentAmount,
                DocumentBalanceAmount = documentAmount,
                AgingBucket = GetAgingBucket(daysOverdue, adjustment.DueDate),
                SettlementStatus = "PostedAdjustment"
            };
        }

        private async Task<string> ResolveAgingFunctionalCurrencyAsync(
            IReadOnlyCollection<SubledgerSettlementBalance> balances)
        {
            if (_tenantSettingsService != null)
            {
                return NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync(), "GHS");
            }

            var currencies = balances
                .Select(balance => NormalizeCurrency(balance.FunctionalCurrencyCode, "GHS"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (currencies.Count > 1)
            {
                throw new InvalidOperationException("AR aging cannot consolidate balances with multiple functional currencies.");
            }

            return currencies.SingleOrDefault() ?? "GHS";
        }

        private static decimal ToFunctionalAmount(SubledgerSettlementBalance balance, decimal documentAmount)
        {
            if (documentAmount == 0m)
                return 0m;

            if (balance.OriginalDocumentAmount == 0m)
                return RoundMoney(documentAmount);

            return RoundMoney(documentAmount * balance.OriginalFunctionalAmount / balance.OriginalDocumentAmount);
        }

        private static decimal GetSignedSubledgerFunctionalAmount(SubledgerAdjustmentJournal adjustment)
        {
            var documentAmount = GetSignedSubledgerAmount(adjustment);
            if (documentAmount == 0m)
                return 0m;

            var functionalAmount = adjustment.BaseCurrencyAmount != 0m
                ? Math.Abs(adjustment.BaseCurrencyAmount)
                : Math.Abs(adjustment.Amount) * NormalizeExchangeRate(adjustment.ExchangeRate);

            return RoundMoney(documentAmount > 0m ? functionalAmount : -functionalAmount);
        }

        private async Task<List<CustomerLedgerSelection>> GetCustomerLedgerSelectionsAsync(
            IReadOnlyCollection<Guid> requestedCustomerIds,
            DateTime endExclusive,
            CancellationToken cancellationToken)
        {
            var selectedIds = requestedCustomerIds.Count > 0
                ? requestedCustomerIds.ToHashSet()
                : await GetCustomerIdsWithLedgerActivityAsync(endExclusive, cancellationToken);

            var customers = await _unitOfWork.Repository<BusinessPartner>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    selectedIds.Contains(p.Id) &&
                    (p.PartnerType == "Customer" || p.PartnerType == "Both"))
                .ToListAsync(cancellationToken);

            return customers
                .Select(c => new CustomerLedgerSelection(
                    c.Id,
                    c.CustomerAccountNumber ?? c.PartnerCode,
                    c.PartnerName,
                    c.Currency ?? "GHS"))
                .ToList();
        }

        private async Task<HashSet<Guid>> GetCustomerIdsWithLedgerActivityAsync(DateTime endExclusive, CancellationToken cancellationToken)
        {
            var invoiceCustomerIds = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.InvoiceDate < endExclusive &&
                    i.Status != InvoiceStatus.Draft &&
                    i.Status != InvoiceStatus.Cancelled)
                .Select(i => i.BusinessPartnerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var paymentCustomerIds = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.PaymentDate < endExclusive &&
                    p.Status != "Pending" &&
                    p.Status != "Cancelled" &&
                    p.Status != "Bounced")
                .Select(p => p.CustomerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var adjustmentCustomerIds = await _unitOfWork.Repository<SubledgerAdjustmentJournal>()
                .GetQueryable(a =>
                    a.TenantId == TenantId &&
                    a.Module == SubledgerModules.AccountsReceivable &&
                    a.Status == SubledgerAdjustmentStatuses.Posted &&
                    a.BusinessPartnerId != Guid.Empty &&
                    a.AdjustmentDate < endExclusive)
                .Select(a => a.BusinessPartnerId)
                .Distinct()
                .ToListAsync(cancellationToken);

            return invoiceCustomerIds
                .Concat(paymentCustomerIds)
                .Concat(adjustmentCustomerIds)
                .ToHashSet();
        }

        private async Task<List<CustomerLedgerTransaction>> GetCustomerLedgerTransactionsAsync(
            CustomerLedgerSelection customer,
            DateTime endExclusive,
            string reportCurrencyCode,
            string baseCurrencyCode,
            ICollection<string> warnings,
            CancellationToken cancellationToken)
        {
            var transactions = new List<CustomerLedgerTransaction>();

            var invoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.BusinessPartnerId == customer.CustomerId &&
                    i.InvoiceDate < endExclusive &&
                    i.Status != InvoiceStatus.Draft &&
                    i.Status != InvoiceStatus.Cancelled)
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

                transactions.Add(new CustomerLedgerTransaction(
                    invoice.Id,
                    invoice.InvoiceDate,
                    invoice.IsOpeningBalance ? "Opening Invoice" : "Invoice",
                    invoice.InvoiceNumber,
                    invoice.Reference,
                    invoice.Notes ?? "Customer invoice",
                    NormalizeCurrency(invoice.CurrencyCode, baseCurrencyCode),
                    NormalizeExchangeRate(invoice.ExchangeRate),
                    amount,
                    0m));
            }

            var payments = await _unitOfWork.Repository<CustomerPayment>()
                .GetQueryable(p =>
                    p.TenantId == TenantId &&
                    p.CustomerId == customer.CustomerId &&
                    p.PaymentDate < endExclusive &&
                    p.Status != "Pending" &&
                    p.Status != "Cancelled" &&
                    p.Status != "Bounced")
                .Include(p => p.Allocations)
                .ToListAsync(cancellationToken);

            foreach (var payment in payments)
            {
                var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, baseCurrencyCode);
                var paymentExchangeRate = NormalizeExchangeRate(payment.ExchangeRate);
                var reference = payment.TransactionReference ?? payment.CheckNumber;

                var paymentAmount = AmountForLedgerCurrency(
                    payment.TotalAmount,
                    null,
                    paymentCurrency,
                    paymentExchangeRate,
                    reportCurrencyCode,
                    baseCurrencyCode,
                    warnings,
                    payment.PaymentNumber);

                transactions.Add(new CustomerLedgerTransaction(
                    payment.Id,
                    payment.PaymentDate,
                    payment.IsCreditNote ? "Credit Note" : "Payment",
                    payment.PaymentNumber,
                    reference,
                    payment.Notes ?? $"Customer payment - {payment.PaymentMethod}",
                    paymentCurrency,
                    paymentExchangeRate,
                    0m,
                    paymentAmount));

                var activeAllocations = payment.Allocations.Where(a => !a.IsReversal).ToList();
                var discountAllowed = activeAllocations.Sum(a => a.DiscountAmount);
                if (discountAllowed > 0m)
                {
                    var discountAmount = AmountForLedgerCurrency(
                        discountAllowed,
                        null,
                        paymentCurrency,
                        paymentExchangeRate,
                        reportCurrencyCode,
                        baseCurrencyCode,
                        warnings,
                        payment.PaymentNumber);

                    transactions.Add(new CustomerLedgerTransaction(
                        payment.Id,
                        payment.PaymentDate,
                        "Discount Allowed",
                        payment.PaymentNumber,
                        reference,
                        "Customer settlement discount",
                        paymentCurrency,
                        paymentExchangeRate,
                        0m,
                        discountAmount));
                }

                if (payment.WithholdingTaxAmount > 0m)
                {
                    var whtAmount = AmountForLedgerCurrency(
                        payment.WithholdingTaxAmount,
                        null,
                        paymentCurrency,
                        paymentExchangeRate,
                        reportCurrencyCode,
                        baseCurrencyCode,
                        warnings,
                        payment.PaymentNumber);

                    transactions.Add(new CustomerLedgerTransaction(
                        payment.Id,
                        payment.PaymentDate,
                        "Withholding Tax",
                        payment.PaymentNumber,
                        payment.WithholdingCertificateNumber ?? reference,
                        "Withholding tax on customer receipt",
                        paymentCurrency,
                        paymentExchangeRate,
                        0m,
                        whtAmount));
                }

                if (payment.VatWithholdingAmount > 0m)
                {
                    var vatWhtAmount = AmountForLedgerCurrency(
                        payment.VatWithholdingAmount,
                        null,
                        paymentCurrency,
                        paymentExchangeRate,
                        reportCurrencyCode,
                        baseCurrencyCode,
                        warnings,
                        payment.PaymentNumber);

                    transactions.Add(new CustomerLedgerTransaction(
                        payment.Id,
                        payment.PaymentDate,
                        "VAT Withholding",
                        payment.PaymentNumber,
                        payment.WithholdingCertificateNumber ?? reference,
                        "VAT withholding on customer receipt",
                        paymentCurrency,
                        paymentExchangeRate,
                        0m,
                        vatWhtAmount));
                }
            }

            var adjustments = await GetPostedArAdjustmentsAsync(customer.CustomerId, cancellationToken);
            foreach (var adjustment in adjustments.Where(a => a.AdjustmentDate < endExclusive))
            {
                var signedAmount = GetSignedSubledgerAmount(adjustment);
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

                transactions.Add(new CustomerLedgerTransaction(
                    adjustment.Id,
                    adjustment.AdjustmentDate,
                    GetAdjustmentTransactionType(adjustment),
                    adjustment.AdjustmentNumber,
                    adjustment.Reference,
                    adjustment.Reason,
                    NormalizeCurrency(adjustment.CurrencyCode, baseCurrencyCode),
                    NormalizeExchangeRate(adjustment.ExchangeRate),
                    signedAmount > 0m ? adjustmentAmount : 0m,
                    signedAmount < 0m ? adjustmentAmount : 0m));
            }

            return transactions;
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

        private sealed record CustomerLedgerSelection(
            Guid CustomerId,
            string CustomerCode,
            string CustomerName,
            string CurrencyCode);

        private sealed record CustomerLedgerTransaction(
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
                    SourceModule = "AR",
                    SourceDocumentType = "AgingReport",
                    Resource = "Finance.AR.AgingReport",
                    ResourceId = TenantId.ToString(),
                    AfterValues = report
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record AR report audit event {EventType}", eventType);
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

        private static decimal GetSignedSubledgerAmount(SubledgerAdjustmentJournal adjustment)
        {
            var isDebit = string.Equals(adjustment.AdjustmentType, SubledgerAdjustmentTypes.Debit, StringComparison.OrdinalIgnoreCase);
            return isDebit ? adjustment.Amount : -adjustment.Amount;
        }

        private static string GetAdjustmentTransactionType(SubledgerAdjustmentJournal adjustment)
        {
            return string.Equals(adjustment.Purpose, SubledgerAdjustmentPurposes.OpeningBalance, StringComparison.OrdinalIgnoreCase)
                ? "Opening Balance"
                : "Adjustment";
        }
    }
}
