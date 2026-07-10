using System;
using System.Collections.Generic;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.DTOs.AR;

// ============= Aging Report DTOs =============

public class AgingReportDto
{
    public DateTime AsOfDate { get; set; }
    public bool UsesSettlementReadModel { get; set; }
    public List<CustomerAgingDto> Customers { get; set; } = new();
    public AgingSummaryDto Summary { get; set; } = new();
    public List<AgingBucketDto> Buckets { get; set; } = new();
    public List<SubledgerSettlementDiagnosticDto> Diagnostics { get; set; } = new();
}

public class CustomerAgingDto
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Current { get; set; }
    public decimal Days1To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal Days90Plus { get; set; }
    public decimal TotalOutstanding { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class AgingSummaryDto
{
    public decimal TotalCurrent { get; set; }
    public decimal TotalDays1To30 { get; set; }
    public decimal TotalDays31To60 { get; set; }
    public decimal TotalDays61To90 { get; set; }
    public decimal TotalDays90Plus { get; set; }
    public decimal GrandTotal { get; set; }
    public int TotalCustomers { get; set; }
    public int OverdueCustomers { get; set; }
}

public class DetailedAgingReportDto
{
    public DateTime AsOfDate { get; set; }
    public bool UsesSettlementReadModel { get; set; }
    public List<CustomerDetailedAgingDto> Customers { get; set; } = new();
    public AgingSummaryDto Summary { get; set; } = new();
    public List<SubledgerSettlementDiagnosticDto> Diagnostics { get; set; } = new();
}

public class CustomerDetailedAgingDto
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<InvoiceAgingDto> Invoices { get; set; } = new();
    public decimal TotalOutstanding { get; set; }
}

public class InvoiceAgingDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public int DaysOverdue { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal CreditedAmount { get; set; }
    public decimal WithheldAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public Guid? SourcePostingEventId { get; set; }
    public Guid? SourceJournalEntryId { get; set; }
    public string? SettlementStatus { get; set; }
    public string? DiagnosticFlags { get; set; }
    public string AgingBucket { get; set; } = string.Empty; // Current, 1-30, 31-60, 61-90, 90+
}

// ============= Customer Statement DTOs =============

public class CustomerStatementDto
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerAddress { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public List<StatementTransactionDto> Transactions { get; set; } = new();
    public decimal TotalInvoices { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal ClosingBalance { get; set; }
}

public class StatementTransactionDto
{
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = string.Empty; // Invoice, Payment, CreditNote
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
}

// ============= Collections Dashboard DTOs =============

public class CollectionsDashboardDto
{
    public decimal TotalOutstanding { get; set; }
    public decimal TotalOverdue { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public List<OverdueCustomerDto> OverdueCustomers { get; set; } = new();
    public List<AgingBucketDto> AgingBuckets { get; set; } = new();
}

public class OverdueCustomerDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalOverdue { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public int DaysOldest { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Priority { get; set; } = string.Empty; // High, Medium, Low
}

public class AgingBucketDto
{
    public string BucketName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int InvoiceCount { get; set; }
    public int CustomerCount { get; set; }
    public decimal Percentage { get; set; }
}

// ============= AR Summary DTOs =============

public class ArSummaryDto
{
    public decimal TotalOutstanding { get; set; }
    public decimal TotalOverdue { get; set; }
    public decimal TotalCurrent { get; set; }
    public int TotalInvoices { get; set; }
    public int OverdueInvoices { get; set; }
    public decimal AverageDaysToPayment { get; set; }
    public decimal BadDebtProvision { get; set; }
    public decimal CollectionRate { get; set; }
}

public class SalesSummaryDto
{
    public string Period { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal Outstanding { get; set; }
    public int InvoiceCount { get; set; }
}

public class SalesSummaryQueryDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? ProductId { get; set; }
    public string GroupBy { get; set; } = "Month"; // Day, Week, Month, Quarter, Year, Customer, Product
}

public class PaymentTrendDto
{
    public string Period { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public decimal TotalCollected { get; set; }
    public int PaymentCount { get; set; }
    public decimal AveragePaymentAmount { get; set; }
}
