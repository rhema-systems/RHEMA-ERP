using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// General Ledger Account entity supporting segmented account structure,
    /// multi-currency operations, and multiple accounting books (IFRS/Local Statutory/Management).
    /// 
    /// PATTERN: This entity follows the enhanced specifications for segmented accounts
    /// with up to 20 segments and reporting dimension control.
    /// </summary>
    public class Account : BusinessEntity
    {
        #region Core Identity and Structure

        /// <summary>
        /// System-generated unique account code (e.g., FA-2026-001, BLDG-HQ-001)
        /// Human-readable identifier displayed in reports and user interfaces.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string AccountCode { get; set; } = string.Empty;

        /// <summary>
        /// Complete account number constructed from segments OR simple numeric sequence.
        /// 
        /// Examples:
        /// - Segmented: "001-FIN-1500-MACH-CC100-LOC05" (Company-Dept-Account-SubAccount-CostCenter-Location)
        /// - Standard: "1000" or "1500-001" (simple numbering)
        /// 
        /// Max 100 characters to support complex segment combinations with separators.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Descriptive name of the account.
        /// Example: "Cash - Operating Account - Main Bank" or "Buildings - Head Office"
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>
        /// Classification of account for financial statement presentation.
        /// Determines normal balance side (debit or credit) and statement grouping.
        /// </summary>
        [Required]
        public AccountType AccountType { get; set; }

        /// <summary>
        /// Sub-classification within account type for detailed reporting.
        /// Example: For Asset type - "Current Assets", "Fixed Assets", "Intangible Assets"
        /// </summary>
        [MaxLength(100)]
        public string? AccountCategory { get; set; }

        /// <summary>
        /// Further sub-classification for granular reporting.
        /// Example: For Fixed Assets category - "Land", "Buildings", "Equipment"
        /// </summary>
        [MaxLength(100)]
        public string? AccountSubCategory { get; set; }

        /// <summary>
        /// Detailed description of account purpose and usage guidelines.
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Reference to parent account for hierarchical chart of accounts structure.
        /// Enables drill-down and roll-up reporting.
        /// NULL indicates this is a top-level account.
        /// </summary>
        public Guid? ParentAccountId { get; set; }

        /// <summary>
        /// Indicates whether this account uses segmented account structure.
        /// TRUE: Account number constructed from segment values in AccountSegmentValue table.
        /// FALSE: Account uses simple/standard numbering scheme.
        /// </summary>
        public bool IsSegmented { get; set; } = true;

        #endregion

        #region Multi-Currency Properties

        /// <summary>
        /// Primary currency for this account (defaults to home/base currency).
        /// ISO 4217 three-letter currency code (e.g., "GHS", "USD", "EUR").
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string CurrencyCode { get; set; } = "GHS"; // Default to Ghana Cedis

        /// <summary>
        /// Indicates if account can handle transactions in multiple currencies.
        /// TRUE: Account tracks balances in both foreign currency and base currency.
        /// FALSE: Account only accepts transactions in the specified CurrencyCode.
        /// 
        /// Typically enabled for: Cash, Bank, Receivables, Payables, Loan accounts.
        /// </summary>
        public bool IsMultiCurrency { get; set; } = false;

        #endregion

        #region Accounting Book Classification (IFRS / Local Statutory / Management)

        /// <summary>
        /// Indicates if account is used in IFRS financial reporting.
        /// Supports parallel accounting frameworks within single GL structure.
        /// </summary>
        public bool IsIFRSClassified { get; set; } = true;

        /// <summary>
        /// Indicates if account is used in Local Statutory reporting.
        /// Backed by the legacy Base flag until account/book mappings fully replace it.
        /// </summary>
        public bool IsBaseClassified { get; set; } = true;

        /// <summary>
        /// Indicates if account is used in Management reporting.
        /// Backed by the legacy Local flag until account/book mappings fully replace it.
        /// </summary>
        public bool IsLocalClassified { get; set; } = false;

        /// <summary>
        /// Financial statement line item for IFRS reporting.
        /// Maps account to specific line in Balance Sheet or Income Statement.
        /// </summary>
        [MaxLength(100)]
        public string? IFRSLineItem { get; set; }

        /// <summary>
        /// Financial statement line item for Local Statutory reporting.
        /// </summary>
        [MaxLength(100)]
        public string? BaseLineItem { get; set; }

        /// <summary>
        /// Financial statement line item for Management reporting.
        /// </summary>
        [MaxLength(100)]
        public string? LocalLineItem { get; set; }

        #endregion

        #region Account Controls and Posting Rules

        /// <summary>
        /// Determines if transactions can be posted directly to this account.
        /// FALSE: Account is header/summary only (transactions post to child accounts).
        /// TRUE: Account accepts direct transaction postings.
        /// </summary>
        public bool AllowDirectPosting { get; set; } = true;

        /// <summary>
        /// Indicates if this is a control account (e.g., Accounts Receivable, Accounts Payable).
        /// Control accounts summarize details from subsidiary ledgers.
        /// Direct posting is typically disabled for control accounts.
        /// </summary>
        public bool IsControlAccount { get; set; } = false;

        /// <summary>
        /// Requires department code/segment on all transactions to this account.
        /// Enables departmental cost tracking and reporting.
        /// </summary>
        public bool RequireDepartmentCode { get; set; } = false;

        /// <summary>
        /// Requires project code/segment on all transactions to this account.
        /// Enables project-level cost tracking and reporting.
        /// </summary>
        public bool RequireProjectCode { get; set; } = false;

        /// <summary>
        /// Enables budget tracking and variance analysis for this account.
        /// Budget vs. actual comparisons available when TRUE.
        /// </summary>
        public bool BudgetTrackingEnabled { get; set; } = false;

        /// <summary>
        /// Account status controlling availability for transactions.
        /// Only Active accounts appear in transaction entry screens.
        /// </summary>
        public AccountStatus Status { get; set; } = AccountStatus.Active;

        #endregion

        #region Balance Tracking (Base Currency)

        /// <summary>
        /// Current account balance in base currency (GHS).
        /// 
        /// For Asset, Expense accounts: Debit increases, Credit decreases.
        /// For Liability, Equity, Revenue accounts: Credit increases, Debit decreases.
        /// 
        /// Automatically updated by transaction posting process.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0;

        /// <summary>
        /// Cumulative debit amount posted to this account (base currency).
        /// Used for detailed transaction analysis and reconciliation.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DebitBalance { get; set; } = 0;

        /// <summary>
        /// Cumulative credit amount posted to this account (base currency).
        /// Used for detailed transaction analysis and reconciliation.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditBalance { get; set; } = 0;

        /// <summary>
        /// Opening balance for current fiscal period.
        /// Carries forward from prior period closing balance.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningBalance { get; set; } = 0;

        /// <summary>
        /// Date of last transaction posted to this account.
        /// Used for aging analysis and inactive account identification.
        /// </summary>
        public DateTime? LastTransactionDate { get; set; }

        #endregion

        #region Integration and Mapping

        /// <summary>
        /// Links account to Estate Management module for property-related accounts.
        /// Enables automatic transaction generation from estate operations.
        /// </summary>
        public Guid? EstateModuleLinkId { get; set; }

        /// <summary>
        /// Links account to HR/Payroll module for salary and benefit accounts.
        /// Enables automatic payroll posting and allocation.
        /// </summary>
        public Guid? PayrollModuleLinkId { get; set; }

        /// <summary>
        /// Links account to Procurement module for vendor-related accounts.
        /// Enables automatic invoice and payment processing.
        /// </summary>
        public Guid? ProcurementModuleLinkId { get; set; }

        /// <summary>
        /// Tax reporting category for automatic tax calculation and reporting.
        /// Example: "Input VAT", "Output VAT", "Withholding Tax"
        /// </summary>
        [MaxLength(50)]
        public string? TaxReportingCategory { get; set; }

        /// <summary>
        /// Cash flow statement classification (Operating, Investing, Financing).
        /// Used for automatic cash flow statement preparation.
        /// </summary>
        [MaxLength(50)]
        public string? CashFlowClassification { get; set; }

        #endregion

        #region Audit and Metadata

        /// <summary>
        /// Indicates if account is system-generated and protected from user deletion.
        /// System accounts are created during initial setup and module activation.
        /// </summary>
        public bool IsSystemAccount { get; set; } = false;

        /// <summary>
        /// Date when account was marked inactive (if Status = Inactive).
        /// NULL if account is currently active.
        /// </summary>
        public DateTime? InactivatedDate { get; set; }

        /// <summary>
        /// Reason for account inactivation or closure.
        /// Required when changing Status from Active to Inactive/Closed.
        /// </summary>
        [MaxLength(500)]
        public string? InactivationReason { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Parent account for hierarchical chart of accounts structure.
        /// NULL if this is a top-level account.
        /// </summary>
        [ForeignKey(nameof(ParentAccountId))]
        public virtual Account? ParentAccount { get; set; }

        /// <summary>
        /// Child accounts in hierarchical structure.
        /// Enables roll-up reporting from children to parent.
        /// </summary>
        public virtual ICollection<Account> ChildAccounts { get; set; } = new List<Account>();

        /// <summary>
        /// Segment values that compose this account's segmented account number.
        /// Empty collection if IsSegmented = false.
        /// 
        /// Example: For account "001-FIN-1500-MACH":
        /// - Segment 1: Company = "001"
        /// - Segment 2: Department = "FIN"
        /// - Segment 3: Account Type = "1500"
        /// - Segment 4: Sub-Account = "MACH"
        /// </summary>
        public virtual ICollection<AccountSegmentValue> SegmentValues { get; set; } = new List<AccountSegmentValue>();

        /// <summary>
        /// Currency links for multi-currency accounts.
        /// Defines which foreign currencies this account can accept and track.
        /// Empty collection if IsMultiCurrency = false.
        /// </summary>
        public virtual ICollection<AccountCurrencyLink> CurrencyLinks { get; set; } = new List<AccountCurrencyLink>();

        /// <summary>
        /// Accounting books where this account is available for posting and reporting.
        /// </summary>
        public virtual ICollection<AccountAccountingBook> AccountingBooks { get; set; } = new List<AccountAccountingBook>();

        /// <summary>
        /// All transactions posted to this account.
        /// Maintains complete transaction history for audit and reporting.
        /// </summary>
        public virtual ICollection<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();

        #endregion
    }
}
