using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// Income Statement (Profit & Loss) output DTO
    /// </summary>
    public class IncomeStatementDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string BookClassification { get; set; } = "IFRS";
        public string CurrencyCode { get; set; } = "GHS";
        
        public List<IncomeStatementSectionDto> Sections { get; set; } = new();
        
        // Summary totals
        public decimal TotalRevenue { get; set; }
        public decimal TotalCostOfSales { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal TotalOperatingExpenses { get; set; }
        public decimal OperatingProfit { get; set; }
        public decimal TotalOtherIncome { get; set; }
        public decimal TotalOtherExpenses { get; set; }
        public decimal ProfitBeforeTax { get; set; }
        public decimal TaxExpense { get; set; }
        public decimal NetProfit { get; set; }
        public FinancialStatementLayoutExecutionDto? LayoutExecution { get; set; }
        public List<string> PresentationWarnings { get; set; } = new();
    }

    /// <summary>
    /// Section of Income Statement (Revenue, Cost of Sales, Operating Expenses, etc.)
    /// </summary>
    public class IncomeStatementSectionDto
    {
        public string SectionName { get; set; } = string.Empty;
        public int SectionOrder { get; set; }
        public List<IncomeStatementLineItemDto> LineItems { get; set; } = new();
        public decimal SectionTotal { get; set; }
    }

    /// <summary>
    /// Individual line item on Income Statement
    /// </summary>
    public class IncomeStatementLineItemDto
    {
        public string LineItemName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int LineOrder { get; set; }
        
        // Optional: drill-down support
        public List<string>? AccountNumbers { get; set; }
    }

    /// <summary>
    /// Request DTO for generating Income Statement
    /// </summary>
    public class IncomeStatementRequestDto
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string BookClassification { get; set; } = "IFRS";
        public bool IncludeAccountDetails { get; set; } = false;
        public List<Guid> AccountIds { get; set; } = new();
        public List<FinanceSegmentFilterDto> SegmentFilters { get; set; } = new();
        public List<FinanceDimensionFilterDto> DimensionFilters { get; set; } = new();
        public Guid? LayoutId { get; set; }
        public bool UseDefaultLayout { get; set; } = true;
    }
}
