// FILE: src/ErpSystem.Core/DTOs/Finance/AccountDtos.cs

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// READ DTO: Represents a General Ledger Account as returned by the Finance API.
    /// 
    /// BUSINESS CONTEXT:
    /// - This is the primary "Chart of Accounts" record that users will work with.
    /// - Supports segmented account structures (up to 20 segments), multi-currency,
    ///   and multiple classification frameworks.
    /// 
    /// TYPICAL USE CASES (FRONTEND):
    /// - Displaying account lists in Chart of Accounts screens.
    /// - Loading account details for editing.
    /// - Providing account dropdowns in Journal Entry, AP, AR, and other modules.
    /// </summary>
    public class AccountDto
    {
        #region Identity & Tenant

        /// <summary>
        /// Unique identifier of the account (GUID).
        /// 
        /// - System-generated primary key.
        /// - Used in URLs, API routes, and as the main reference in other modules.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Tenant identifier for multi-tenancy.
        /// 
        /// - Ensures that accounts are isolated per organization.
        /// - Typically not editable by frontend; derived from user context.
        /// </summary>
        public Guid TenantId { get; set; }

        #endregion

        #region Core Account Identity

        /// <summary>
        /// System-generated unique account code.
        /// 
        /// EXAMPLES:
        /// - "FA-2026-001"
        /// - "BLDG-HQ-001"
        /// 
        /// NOTES:
        /// - Human-readable identifier used in UIs and reports.
        /// - Can be different from AccountNumber (which may be fully segmented).
        /// </summary>
        public string AccountCode { get; set; } = string.Empty;

        /// <summary>
        /// Complete account number as used in postings and financial reports.
        /// 
        /// EXAMPLES:
        /// - Segmented: "001-FIN-1500-MACH-CC100-LOC05"
        /// - Simple:    "1000" or "1500-001"
        /// 
        /// - Built from underlying segments where segmented accounting is enabled.
        /// - Up to 100 characters to support complex segment structures.
        /// </summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Descriptive name of the account.
        /// 
        /// EXAMPLES:
        /// - "Cash - Operating Account - Main Bank"
        /// - "Buildings - Head Office"
        /// - "Revenue - Product Sales"
        /// 
        /// UI:
        /// - Typically displayed alongside AccountNumber.
        /// - Used in search & filters.
        /// </summary>
        public string AccountName { get; set; } = string.Empty;

        #endregion

        #region Classification

        /// <summary>
        /// Primary financial statement classification for this account.
        /// 
        /// EXAMPLES:
        /// - Asset
        /// - Liability
        /// - Equity
        /// - Revenue
        /// - Expense
        /// 
        /// PURPOSE:
        /// - Determines normal balance side (debit/credit).
        /// - Used for Balance Sheet and P&amp;L grouping.
        /// </summary>
        public string AccountType { get; set; } = string.Empty;

        /// <summary>
        /// Sub-classification within the account type for reporting.
        /// 
        /// EXAMPLES:
        /// - For Asset: "Current Assets", "Fixed Assets", "Intangible Assets"
        /// - For Expense: "Operating Expenses", "Administrative Expenses"
        /// </summary>
        public string? AccountCategory { get; set; }

        /// <summary>
        /// Further sub-classification for granular reporting.
        /// 
        /// EXAMPLES:
        /// - For Fixed Assets: "Land", "Buildings", "Equipment"
        /// - For Revenue: "Product Sales", "Service Income"
        /// </summary>
        public string? AccountSubCategory { get; set; }

        #endregion

        #region Multi-Currency

        /// <summary>
        /// Primary currency code for this account.
        /// 
        /// FORMAT:
        /// - ISO 4217 three-letter code (e.g. "GHS", "USD", "EUR").
        /// 
        /// NOTE:
        /// - Defaults to the tenant's base currency (e.g. "GHS" for Ghana).
        /// </summary>
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether this account supports multiple currencies.
        /// 
        /// TRUE:
        /// - Account tracks balances in both foreign and base currency.
        /// - Typical for bank, cash, receivables, and payables accounts.
        /// 
        /// FALSE:
        /// - Account accepts transactions only in <see cref="CurrencyCode"/>.
        /// </summary>
        public bool IsMultiCurrency { get; set; }

        #endregion

        #region Framework Flags (IFRS / Statutory / Local)

        /// <summary>
        /// Indicates if account participates in IFRS reporting.
        /// </summary>
        public bool IsIFRSClassified { get; set; }

        /// <summary>
        /// Indicates if account participates in the base/statutory reporting framework.
        /// </summary>
        public bool IsBaseFrameworkClassified { get; set; }

        /// <summary>
        /// Indicates if account participates in a local/management reporting framework.
        /// </summary>
        public bool IsLocalFrameworkClassified { get; set; }

        #endregion

        #region Control/Posting Flags

        /// <summary>
        /// Indicates whether this is a control account (e.g., AR, AP, Inventory).
        /// 
        /// NOTES:
        /// - Control accounts are typically not posted to directly by users.
        /// - They are fed by sub-ledger modules (AR, AP, Inventory).
        /// </summary>
        public bool IsControlAccount { get; set; }

        /// <summary>
        /// Indicates whether this account is allowed to receive direct postings.
        /// 
        /// TRUE:
        /// - Users can post journals directly to this account.
        /// 
        /// FALSE:
        /// - Posting is restricted (e.g., purely control/technical accounts).
        /// </summary>
        public bool IsPostingAllowed { get; set; }

        #endregion

        #region BusinessEntity Base Fields

        /// <summary>
        /// Human-readable reference number for this account (optional).
        /// 
        /// EXAMPLES:
        /// - "ACC-000123"
        /// - External code or integration reference.
        /// </summary>
        public string ReferenceNumber { get; set; } = string.Empty;

        /// <summary>
        /// Business status of the account.
        /// 
        /// EXAMPLES:
        /// - "Active"
        /// - "Inactive"
        /// - "Closed"
        /// - "PendingApproval"
        /// 
        /// NOTE:
        /// - Should be consistent with AccountStatus enum on the entity.
        /// </summary>
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Date from which this account becomes effective/usable.
        /// </summary>
        public DateTime? EffectiveDate { get; set; }

        /// <summary>
        /// Optional date after which the account is considered expired for new postings.
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// JSON metadata for extensibility and module-specific annotations.
        /// 
        /// - Can store custom tags, flags, integration references, etc.
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Free-form tags for search and categorization.
        /// 
        /// EXAMPLES:
        /// - "BANK; GHANA; MAIN"
        /// - "CAPEX; PROJECTS"
        /// </summary>
        public string? Tags { get; set; }

        /// <summary>
        /// Priority level (1–10) for internal sorting or importance.
        /// 
        /// - 10 = highest priority.
        /// - Can be used in custom ordering logic.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Indicates if the account is currently active based on status and dates.
        /// 
        /// NOTE:
        /// - This is a computed/derived value at the DTO level, populated by the service.
        /// </summary>
        public bool IsActive { get; set; }

        #endregion

        #region Segmented Structure

        /// <summary>
        /// Indicates whether this account uses a segmented structure.
        /// 
        /// TRUE:
        /// - AccountNumber is composed from one or more segments.
        /// - Segment values are stored in <see cref="SegmentValues"/>.
        /// 
        /// FALSE:
        /// - AccountNumber is a simple value without segment breakdown.
        /// </summary>
        public bool IsSegmented { get; set; }

        /// <summary>
        /// Collection of segment values that make up this account (if segmented).
        /// 
        /// EXAMPLE:
        /// - Company, Department, Natural Account, Cost Center, Project, etc.
        /// 
        /// NOTE:
        /// - Populated by joining AccountSegmentValue, AccountSegmentStructure,
        ///   and SegmentLookupValue where applicable.
        /// </summary>
        public IReadOnlyCollection<AccountSegmentValueDto> SegmentValues { get; set; }
            = Array.Empty<AccountSegmentValueDto>();

        #endregion

        #region Audit

        /// <summary>
        /// UTC timestamp when this account was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// UTC timestamp when this account was last updated (if ever).
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Username/identifier of the user who created this account.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Username/identifier of the user who last updated this account.
        /// </summary>
        public string? UpdatedBy { get; set; }

        #endregion
    }

    // ========================================================================
    // CREATE DTO
    // ========================================================================

    /// <summary>
    /// CREATE DTO: Payload for creating a new GL Account.
    /// 
    /// USAGE:
    /// - POST /api/finance/accounts
    /// - Used by Chart of Accounts maintenance screens when adding a new account.
    /// 
    /// DESIGN:
    /// - Does NOT expose Id or TenantId (system-managed).
    /// - Includes core identity, classification, currency, and optional segment values.
    /// </summary>
    public class AccountCreateDto
    {
        /// <summary>
        /// System-generated account code (optional on create).
        /// 
        /// NOTES:
        /// - If omitted, backend can generate a code based on numbering rules.
        /// - If supplied, backend will validate uniqueness.
        /// </summary>
        [MaxLength(50)]
        public string? AccountCode { get; set; }

        /// <summary>
        /// Complete account number.
        /// 
        /// SEGMENTED CASE:
        /// - May be auto-constructed from SegmentValues by the backend.
        /// 
        /// NON-SEGMENTED CASE:
        /// - Must be supplied and unique per tenant.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Descriptive name of the account.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>
        /// Primary account type classification (e.g., Asset, Liability, Revenue, Expense).
        /// 
        /// IMPLEMENTATION:
        /// - Typically maps to an AccountType enum in the domain layer.
        /// - DTO uses string for flexibility and clearer OpenAPI docs.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string AccountType { get; set; } = string.Empty;

        /// <summary>
        /// Optional category for grouping accounts in reports.
        /// </summary>
        [MaxLength(100)]
        public string? AccountCategory { get; set; }

        /// <summary>
        /// Optional sub-category for finer-grained grouping.
        /// </summary>
        [MaxLength(100)]
        public string? AccountSubCategory { get; set; }

        /// <summary>
        /// Primary currency code (ISO 4217).
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether this account can operate in multiple currencies.
        /// </summary>
        public bool IsMultiCurrency { get; set; } = false;

        /// <summary>
        /// Flags whether account participates in IFRS reporting.
        /// </summary>
        public bool IsIFRSClassified { get; set; } = true;

        /// <summary>
        /// Flags whether account participates in base/statutory reporting.
        /// </summary>
        public bool IsBaseFrameworkClassified { get; set; } = true;

        /// <summary>
        /// Flags whether account participates in local/management reporting.
        /// </summary>
        public bool IsLocalFrameworkClassified { get; set; } = true;

        /// <summary>
        /// Indicates whether this account is a control account (e.g., AR, AP, Inventory).
        /// </summary>
        public bool IsControlAccount { get; set; } = false;

        /// <summary>
        /// Indicates whether direct postings to this account are allowed.
        /// </summary>
        public bool IsPostingAllowed { get; set; } = true;

        /// <summary>
        /// Optional human-readable reference number.
        /// 
        /// - Can be used to store external system references or custom codes.
        /// </summary>
        [MaxLength(50)]
        public string? ReferenceNumber { get; set; }

        /// <summary>
        /// Initial business status (typically "Active" or "PendingApproval").
        /// </summary>
        [MaxLength(50)]
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Optional effective date for this account.
        /// </summary>
        public DateTime? EffectiveDate { get; set; }

        /// <summary>
        /// Optional expiration date after which the account is not used for new postings.
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Optional JSON metadata for extensibility.
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Optional tags for filtering and search.
        /// </summary>
        [MaxLength(500)]
        public string? Tags { get; set; }

        /// <summary>
        /// Initial priority level (1–10).
        /// </summary>
        public int Priority { get; set; } = 5;

        /// <summary>
        /// Indicates whether this account uses segmented structure.
        /// 
        /// TRUE:
        /// - SegmentValues should be supplied.
        /// 
        /// FALSE:
        /// - AccountNumber is treated as a simple, non-segmented string.
        /// </summary>
        public bool IsSegmented { get; set; } = false;

        /// <summary>
        /// Optional list of segment values that define the account's AccountNumber.
        /// 
        /// NOTES:
        /// - For segmented accounts, the backend can construct AccountNumber from these.
        /// - For non-segmented accounts, this collection is typically empty.
        /// </summary>
        public List<AccountSegmentValueCreateDto> SegmentValues { get; set; }
            = new();
    }

    // ========================================================================
    // UPDATE DTO
    // ========================================================================

    /// <summary>
    /// UPDATE DTO: Payload for updating an existing GL Account.
    /// 
    /// USAGE:
    /// - PUT /api/finance/accounts/{id}
    /// - Used by Chart of Accounts maintenance screens when editing accounts.
    /// 
    /// DESIGN:
    /// - Includes Id to identify the record.
    /// - Fields mirror <see cref="AccountCreateDto"/> to keep API consistent.
    /// </summary>
    public class AccountUpdateDto
    {
        /// <summary>
        /// Identifier of the account being updated.
        /// </summary>
        [Required]
        public Guid Id { get; set; }

        /// <summary>
        /// System-generated account code (optional).
        /// </summary>
        [MaxLength(50)]
        public string? AccountCode { get; set; }

        /// <summary>
        /// Complete account number.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Descriptive name of the account.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>
        /// Primary account type classification.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string AccountType { get; set; } = string.Empty;

        /// <summary>
        /// Optional category for grouping accounts.
        /// </summary>
        [MaxLength(100)]
        public string? AccountCategory { get; set; }

        /// <summary>
        /// Optional sub-category for further grouping.
        /// </summary>
        [MaxLength(100)]
        public string? AccountSubCategory { get; set; }

        /// <summary>
        /// Primary currency code.
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether this account can operate in multiple currencies.
        /// </summary>
        public bool IsMultiCurrency { get; set; } = false;

        /// <summary>
        /// Flags IFRS classification.
        /// </summary>
        public bool IsIFRSClassified { get; set; } = true;

        /// <summary>
        /// Flags base/statutory classification.
        /// </summary>
        public bool IsBaseFrameworkClassified { get; set; } = true;

        /// <summary>
        /// Flags local/management classification.
        /// </summary>
        public bool IsLocalFrameworkClassified { get; set; } = true;

        /// <summary>
        /// Indicates whether this account is a control account.
        /// </summary>
        public bool IsControlAccount { get; set; } = false;

        /// <summary>
        /// Indicates whether direct postings are allowed.
        /// </summary>
        public bool IsPostingAllowed { get; set; } = true;

        /// <summary>
        /// Updated human-readable reference number.
        /// </summary>
        [MaxLength(50)]
        public string? ReferenceNumber { get; set; }

        /// <summary>
        /// Updated status string.
        /// </summary>
        [MaxLength(50)]
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Updated effective date.
        /// </summary>
        public DateTime? EffectiveDate { get; set; }

        /// <summary>
        /// Updated expiration date.
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Updated JSON metadata.
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Updated tags string.
        /// </summary>
        [MaxLength(500)]
        public string? Tags { get; set; }

        /// <summary>
        /// Updated priority level (1–10).
        /// </summary>
        public int Priority { get; set; } = 5;

        /// <summary>
        /// Indicates whether this account is segmented.
        /// </summary>
        public bool IsSegmented { get; set; } = false;

        /// <summary>
        /// Updated list of segment values defining this account.
        /// 
        /// NOTES:
        /// - Service layer must enforce strict rules if account already has postings.
        /// - In many implementations, segment changes after posting are restricted.
        /// </summary>
        public List<AccountSegmentValueUpdateDto> SegmentValues { get; set; }
            = new();
    }
}
