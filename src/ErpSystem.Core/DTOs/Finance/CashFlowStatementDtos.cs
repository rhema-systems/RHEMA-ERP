using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// Cash Flow Statement output DTO (Statement of Cash Flows)
    /// </summary>
    public class CashFlowStatementDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string BookClassification { get; set; } = "IFRS";
        public string CurrencyCode { get; set; } = "GHS";
        
        // Three main sections
        public CashFlowSectionDto OperatingActivities { get; set; } = new();
        public CashFlowSectionDto InvestingActivities { get; set; } = new();
        public CashFlowSectionDto FinancingActivities { get; set; } = new();
        
        // Summary
        public decimal NetCashFromOperating { get; set; }
        public decimal NetCashFromInvesting { get; set; }
        public decimal NetCashFromFinancing { get; set; }
        public decimal NetIncreaseInCash { get; set; }
        public decimal CashAtBeginning { get; set; }
        public decimal CashAtEnd { get; set; }
        
        // Reconciliation check
        public bool IsReconciled => Math.Abs((CashAtBeginning + NetIncreaseInCash) - CashAtEnd) < 0.01m;
    }

    /// <summary>
    /// Section of Cash Flow Statement (Operating, Investing, or Financing)
    /// </summary>
    public class CashFlowSectionDto
    {
        public string SectionName { get; set; } = string.Empty;
        public int SectionOrder { get; set; }
        public List<CashFlowLineItemDto> LineItems { get; set; } = new();
        public decimal SectionTotal { get; set; }
    }

    /// <summary>
    /// Individual line item in Cash Flow Statement
    /// </summary>
    public class CashFlowLineItemDto
    {
        public string LineItemName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int LineOrder { get; set; }
        
        // Optional: drill-down support
        public List<string>? AccountNumbers { get; set; }
    }

    /// <summary>
    /// Request DTO for generating Cash Flow Statement
    /// </summary>
    public class CashFlowStatementRequestDto
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string BookClassification { get; set; } = "IFRS";
        public bool IncludeAccountDetails { get; set; } = false;
        
        /// <summary>
        /// Method: "Direct" or "Indirect" (default: Indirect)
        /// Indirect method starts with net income and adjusts for non-cash items
        /// Direct method shows actual cash receipts and payments
        /// </summary>
        public string Method { get; set; } = "Indirect";
    }
}
