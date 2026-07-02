using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ErpSystem.Api.Services.Finance.AR
{
    public class ArReportsService : IArReportsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<ArReportsService> _logger;

        public ArReportsService(
            IUnitOfWork _unitOfWork,
            ICurrentUserService currentUser,
            ILogger<ArReportsService> logger)
        {
            this._unitOfWork = _unitOfWork;
            _currentUser = currentUser;
            _logger = logger;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;

        public async Task<AgingReportDto> GetAgingReportAsync(DateTime? asOfDate = null, Guid? customerId = null, CancellationToken cancellationToken = default)
        {
            var effectiveDate = asOfDate ?? DateTime.UtcNow;

            var query = _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    (i.TotalAmount - i.PaidAmount) > 0 &&
                    (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Overdue));

            if (customerId.HasValue)
                query = query.Where(i => i.BusinessPartnerId == customerId.Value);

            var invoices = await query.ToListAsync(cancellationToken);

            // Group by customer
            var customerGroups = invoices.GroupBy(i => new { i.CustomerId, i.CustomerName });

            var customerAging = new List<CustomerAgingDto>();

            foreach (var group in customerGroups)
            {
                var aging = new CustomerAgingDto
                {
                    CustomerId = group.Key.CustomerId,
                    CustomerCode = string.Empty,
                    CustomerName = group.Key.CustomerName,
                    Phone = null,
                    Email = null
                };

                foreach (var invoice in group)
                {
                    var daysOverdue = invoice.DueDate.HasValue
                        ? (effectiveDate - invoice.DueDate.Value).Days
                        : (effectiveDate - invoice.InvoiceDate).Days;

                    if (daysOverdue < 0 || !invoice.DueDate.HasValue)
                        aging.Current += invoice.BalanceAmount;
                    else if (daysOverdue <= 30)
                        aging.Days1To30 += invoice.BalanceAmount;
                    else if (daysOverdue <= 60)
                        aging.Days31To60 += invoice.BalanceAmount;
                    else if (daysOverdue <= 90)
                        aging.Days61To90 += invoice.BalanceAmount;
                    else
                        aging.Days90Plus += invoice.BalanceAmount;
                }

                aging.TotalOutstanding = aging.Current + aging.Days1To30 + aging.Days31To60 + aging.Days61To90 + aging.Days90Plus;

                customerAging.Add(aging);
            }

            var report = new AgingReportDto
            {
                AsOfDate = effectiveDate,
                Customers = customerAging.OrderByDescending(c => c.TotalOutstanding).ToList(),
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
            var effectiveDate = asOfDate ?? DateTime.UtcNow;

            var query = _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    (i.TotalAmount - i.PaidAmount) > 0 &&
                    (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Overdue));

            if (customerId.HasValue)
                query = query.Where(i => i.BusinessPartnerId == customerId.Value);

            var invoices = await query.ToListAsync(cancellationToken);

            var customerGroups = invoices.GroupBy(i => new { i.CustomerId, i.CustomerName });

            var customerDetailedAging = new List<CustomerDetailedAgingDto>();

            foreach (var group in customerGroups)
            {
                var detailedAging = new CustomerDetailedAgingDto
                {
                    CustomerId = group.Key.CustomerId,
                    CustomerCode = string.Empty,
                    CustomerName = group.Key.CustomerName
                };

                foreach (var invoice in group)
                {
                    var daysOverdue = invoice.DueDate.HasValue
                        ? (effectiveDate - invoice.DueDate.Value).Days
                        : (effectiveDate - invoice.InvoiceDate).Days;

                    string agingBucket;
                    if (daysOverdue < 0 || !invoice.DueDate.HasValue)
                        agingBucket = "Current";
                    else if (daysOverdue <= 30)
                        agingBucket = "1-30 Days";
                    else if (daysOverdue <= 60)
                        agingBucket = "31-60 Days";
                    else if (daysOverdue <= 90)
                        agingBucket = "61-90 Days";
                    else
                        agingBucket = "90+ Days";

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
                        AgingBucket = agingBucket
                    });
                }

                detailedAging.TotalOutstanding = detailedAging.Invoices.Sum(i => i.BalanceAmount);
                customerDetailedAging.Add(detailedAging);
            }

            var report = new DetailedAgingReportDto
            {
                AsOfDate = effectiveDate,
                Customers = customerDetailedAging.OrderByDescending(c => c.TotalOutstanding).ToList(),
                Summary = new AgingSummaryDto
                {
                    GrandTotal = customerDetailedAging.Sum(c => c.TotalOutstanding),
                    TotalCustomers = customerDetailedAging.Count
                }
            };

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

            var openingBalance = openingInvoices.Sum(i => i.TotalAmount) - openingPayments.Sum(p => p.AllocatedAmount);

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

            var transactions = new List<StatementTransactionDto>();
            var runningBalance = openingBalance;

            // Combine and sort transactions
            foreach (var invoice in periodInvoices)
            {
                runningBalance += invoice.TotalAmount;
                transactions.Add(new StatementTransactionDto
                {
                    TransactionDate = invoice.InvoiceDate,
                    TransactionType = "Invoice",
                    Reference = invoice.InvoiceNumber,
                    Description = invoice.Notes ?? "Sales Invoice",
                    Debit = invoice.TotalAmount,
                    Credit = 0,
                    Balance = runningBalance
                });
            }

            foreach (var payment in periodPayments)
            {
                runningBalance -= payment.AllocatedAmount;
                transactions.Add(new StatementTransactionDto
                {
                    TransactionDate = payment.PaymentDate,
                    TransactionType = payment.IsCreditNote ? "CreditNote" : "Payment",
                    Reference = payment.PaymentNumber,
                    Description = payment.Notes ?? $"Payment - {payment.PaymentMethod}",
                    Debit = 0,
                    Credit = payment.AllocatedAmount,
                    Balance = runningBalance
                });
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
                Transactions = transactions.OrderBy(t => t.TransactionDate).ToList(),
                TotalInvoices = periodInvoices.Sum(i => i.TotalAmount),
                TotalPayments = periodPayments.Sum(p => p.AllocatedAmount),
                ClosingBalance = runningBalance
            };

            return statement;
        }

        public async Task<CollectionsDashboardDto> GetCollectionsDashboardAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var overdueInvoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    i.DueDate.HasValue &&
                    i.DueDate.Value < now &&
                    (i.TotalAmount - i.PaidAmount) > 0)
                .ToListAsync(cancellationToken);

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

            var allOutstanding = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i =>
                    i.TenantId == TenantId &&
                    (i.TotalAmount - i.PaidAmount) > 0)
                .SumAsync(i => i.TotalAmount - i.PaidAmount, cancellationToken);

            var dashboard = new CollectionsDashboardDto
            {
                TotalOutstanding = allOutstanding,
                TotalOverdue = overdueInvoices.Sum(i => i.BalanceAmount),
                OverdueInvoiceCount = overdueInvoices.Count,
                OverdueCustomers = customerGroups
            };

            return dashboard;
        }

        public async Task<ArSummaryDto> GetArSummaryAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var allInvoices = await _unitOfWork.Repository<Invoice>()
                .GetQueryable(i => i.TenantId == TenantId)
                .ToListAsync(cancellationToken);

            var overdueInvoices = allInvoices.Where(i =>
                i.DueDate.HasValue &&
                i.DueDate.Value < now &&
                i.BalanceAmount > 0).ToList();

            var summary = new ArSummaryDto
            {
                TotalOutstanding = allInvoices.Where(i => i.BalanceAmount > 0).Sum(i => i.BalanceAmount),
                TotalOverdue = overdueInvoices.Sum(i => i.BalanceAmount),
                TotalCurrent = allInvoices.Where(i =>
                    i.BalanceAmount > 0 &&
                    (!i.DueDate.HasValue || i.DueDate.Value >= now)).Sum(i => i.BalanceAmount),
                TotalInvoices = allInvoices.Count,
                OverdueInvoices = overdueInvoices.Count,
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

        public async Task<byte[]> ExportAgingReportAsync(DateTime? asOfDate = null, string format = "Excel", CancellationToken cancellationToken = default)
        {
            // Placeholder - would integrate with an Excel/PDF library
            await Task.CompletedTask;
            throw new NotImplementedException("Export functionality will be implemented with an Excel/PDF library.");
        }

        public async Task<byte[]> ExportCustomerStatementAsync(Guid customerId, DateTime fromDate, DateTime toDate, string format = "PDF", CancellationToken cancellationToken = default)
        {
            // Placeholder - would integrate with a PDF library
            await Task.CompletedTask;
            throw new NotImplementedException("Export functionality will be implemented with a PDF library.");
        }
    }
}
