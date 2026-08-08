using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// Balance Sheet (Statement of Financial Position) output DTO
    /// </summary>
    public class BalanceSheetDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public DateTime AsAtDate { get; set; }
        public string BookClassification { get; set; } = "IFRS"; // IFRS, LOCAL_STATUTORY, MANAGEMENT
        public string CurrencyCode { get; set; } = "GHS";
        
        public List<BalanceSheetSectionDto> Sections { get; set; } = new();
        
        // Summary totals
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }

        /// <summary>
        /// Populated when the report was presented through a published financial
        /// statement layout. Legacy sections and totals remain populated for
        /// backwards compatibility and reconciliation.
        /// </summary>
        public FinancialStatementLayoutExecutionDto? LayoutExecution { get; set; }

        public List<string> PresentationWarnings { get; set; } = new();
        
        // Validation
        public bool IsBalanced => Math.Abs(TotalAssets - (TotalLiabilities + TotalEquity)) < 0.01m;
    }

    /// <summary>
    /// Major section of Balance Sheet (Assets, Liabilities, Equity)
    /// </summary>
    public class BalanceSheetSectionDto
    {
        public string SectionName { get; set; } = string.Empty; // "Assets", "Liabilities", "Equity"
        public int SectionOrder { get; set; } // Display order
        public List<BalanceSheetCategoryDto> Categories { get; set; } = new();
        public decimal SectionTotal { get; set; }
    }

    /// <summary>
    /// Category within a section (e.g., Current Assets, Non-Current Assets)
    /// </summary>
    public class BalanceSheetCategoryDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int CategoryOrder { get; set; }
        public List<BalanceSheetLineItemDto> LineItems { get; set; } = new();
        public decimal CategoryTotal { get; set; }
    }

    /// <summary>
    /// Individual line item on Balance Sheet
    /// </summary>
    public class BalanceSheetLineItemDto
    {
        public string LineItemName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int LineOrder { get; set; }
        
        // Optional: drill-down support
        public List<string>? AccountNumbers { get; set; }
    }

    /// <summary>
    /// Request DTO for generating Balance Sheet
    /// </summary>
    public class BalanceSheetRequestDto
    {
        public DateTime AsAtDate { get; set; } = DateTime.UtcNow.Date;
        public string BookClassification { get; set; } = "IFRS";
        public bool IncludeAccountDetails { get; set; } = false;
        public List<Guid> AccountIds { get; set; } = new();
        public List<FinanceSegmentFilterDto> SegmentFilters { get; set; } = new();
        public Guid? LayoutId { get; set; }

        /// <summary>
        /// When no layout id is supplied, use the active default layout when one
        /// has an effective published version. If unavailable, the legacy
        /// account-classification presentation is returned.
        /// </summary>
        public bool UseDefaultLayout { get; set; } = true;
    }
}
