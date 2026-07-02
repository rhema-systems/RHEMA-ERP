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
    }
}
