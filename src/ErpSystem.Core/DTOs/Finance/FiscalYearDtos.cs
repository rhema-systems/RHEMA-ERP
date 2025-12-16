using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// READ DTO: Represents a Fiscal Year as returned by the Finance API.
    /// Used for annual financial period management.
    /// </summary>
    public class FiscalYearDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        
        /// <summary>Fiscal year name (e.g., "Fiscal Year 2025", "FY2025")</summary>
        public string FiscalYearName { get; set; } = string.Empty;
        
        /// <summary>Short code (e.g., "2025", "FY25")</summary>
        public string FiscalYearCode { get; set; } = string.Empty;
        
        /// <summary>Calendar year this fiscal year primarily falls in</summary>
        public int Year { get; set; }
        
        /// <summary>Fiscal year type: "Calendar", "Custom", "52-53 Week", "Quarterly"</summary>
        public string FiscalYearType { get; set; } = "Calendar";
        
        /// <summary>First day of the fiscal year</summary>
        public DateTime StartDate { get; set; }
        
        /// <summary>Last day of the fiscal year</summary>
        public DateTime EndDate { get; set; }
        
        /// <summary>Number of days in this fiscal year</summary>
        public int TotalDays { get; set; }
        
        /// <summary>Number of periods in this fiscal year (typically 12)</summary>
        public int NumberOfPeriods { get; set; }
        
        /// <summary>Status: "Future", "Open", "Closed", "Locked", "Archived"</summary>
        public string Status { get; set; } = "Future";
        
        /// <summary>TRUE if at least one period is open</summary>
        public bool IsActive { get; set; }
        
        /// <summary>TRUE if permanently locked</summary>
        public bool IsLocked { get; set; }
        
        /// <summary>Date when year was locked</summary>
        public DateTime? LockedDate { get; set; }
        
        /// <summary>TRUE if year-end close completed</summary>
        public bool IsClosed { get; set; }
        
        /// <summary>Date when year was closed</summary>
        public DateTime? ClosedDate { get; set; }
        
        /// <summary>TRUE if all periods within year are closed</summary>
        public bool AllPeriodsClosedValidated { get; set; }
        
        /// <summary>TRUE if retained earnings transfer completed</summary>
        public bool RetainedEarningsTransferComplete { get; set; }
        
        /// <summary>Net income transferred to retained earnings</summary>
        public decimal? NetIncomeTransferred { get; set; }
        
        /// <summary>TRUE if opening balances generated for next year</summary>
        public bool OpeningBalancesGenerated { get; set; }
        
        /// <summary>Reporting framework: "IFRS", "US GAAP", "Local GAAP", "Tax Basis"</summary>
        public string ReportingFramework { get; set; } = "IFRS";
        
        /// <summary>Base currency code for this fiscal year</summary>
        public string BaseCurrency { get; set; } = "GHS";
        
        /// <summary>Total journal entries in this year</summary>
        public int TotalJournalEntries { get; set; }
        
        /// <summary>Total transaction lines in this year</summary>
        public int TotalTransactionLines { get; set; }
        
        /// <summary>Total debits for the year</summary>
        public decimal TotalDebits { get; set; }
        
        /// <summary>Total credits for the year</summary>
        public decimal TotalCredits { get; set; }
        
        /// <summary>Total revenue for the year</summary>
        public decimal TotalRevenue { get; set; }
        
        /// <summary>Total expenses for the year</summary>
        public decimal TotalExpenses { get; set; }
        
        /// <summary>Net income for the year</summary>
        public decimal NetIncome { get; set; }
        
        /// <summary>TRUE if budget approved</summary>
        public bool IsBudgetApproved { get; set; }
        
        /// <summary>Budgeted revenue</summary>
        public decimal? BudgetedRevenue { get; set; }
        
        /// <summary>Budgeted expenses</summary>
        public decimal? BudgetedExpenses { get; set; }
        
        /// <summary>TRUE if external audit complete</summary>
        public bool IsAuditComplete { get; set; }
        
        /// <summary>Audit firm name</summary>
        public string? AuditFirm { get; set; }
        
        /// <summary>Audit opinion: "Unqualified", "Qualified", "Adverse", "Disclaimer"</summary>
        public string? AuditOpinion { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// CREATE DTO: Payload for creating a new fiscal year.
    /// </summary>
    public class CreateFiscalYearDto
    {
        [Required]
        [MaxLength(100)]
        public string FiscalYearName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(20)]
        public string FiscalYearCode { get; set; } = string.Empty;
        
        [Required]
        public int Year { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string FiscalYearType { get; set; } = "Calendar";
        
        [Required]
        public DateTime StartDate { get; set; }
        
        [Required]
        public DateTime EndDate { get; set; }
        
        [Required]
        [Range(1, 53)]
        public int NumberOfPeriods { get; set; } = 12;
        
        [Required]
        [MaxLength(50)]
        public string ReportingFramework { get; set; } = "IFRS";
        
        [Required]
        [MaxLength(3)]
        public string BaseCurrency { get; set; } = "GHS";
    }
}
