namespace ErpSystem.Core.Enums;

/// <summary>
/// Account type enumeration for financial statement classification.
/// Determines normal balance side and statement grouping.
/// </summary>
public enum AccountType
{
    /// <summary>
    /// Asset accounts - resources owned by the entity.
    /// Normal balance: Debit
    /// Examples: Cash, Accounts Receivable, Inventory, Fixed Assets
    /// </summary>
    Asset = 1,

    /// <summary>
    /// Liability accounts - obligations owed by the entity.
    /// Normal balance: Credit
    /// Examples: Accounts Payable, Loans Payable, Accrued Expenses
    /// </summary>
    Liability = 2,

    /// <summary>
    /// Equity accounts - owner's residual interest in assets after liabilities.
    /// Normal balance: Credit
    /// Examples: Share Capital, Retained Earnings, Reserves
    /// </summary>
    Equity = 3,

    /// <summary>
    /// Revenue/Income accounts - inflows from business operations.
    /// Normal balance: Credit
    /// Examples: Sales Revenue, Service Income, Interest Income
    /// </summary>
    Revenue = 4,

    /// <summary>
    /// Expense accounts - costs incurred in business operations.
    /// Normal balance: Debit
    /// Examples: Salaries, Rent, Utilities, Depreciation
    /// </summary>
    Expense = 5
}

/// <summary>
/// Account status enumeration controlling account availability.
/// </summary>
public enum AccountStatus
{
    /// <summary>
    /// Active - Account available for transaction posting and reporting.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Inactive - Account not available for new transactions but visible in reports.
    /// Used when account is temporarily not in use but may be reactivated.
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// Closed - Account permanently closed, only visible in historical reports.
    /// Cannot be reactivated without admin intervention.
    /// </summary>
    Closed = 3,

    /// <summary>
    /// Pending Approval - New account awaiting approval before becoming active.
    /// </summary>
    PendingApproval = 4
}

public enum AccountClassificationStatus
{
    Draft = 1,
    Active = 2,
    Retired = 3
}

public enum RevaluationTreatment
{
    Exclude = 1,
    Include = 2
}

/// <summary>
/// Stable behavioural families used by Finance controls. Presentation headings belong to
/// configurable classifications and report layouts, not this vocabulary.
/// </summary>
public enum AccountClassificationSystemRole
{
    Cash = 1,
    Bank = 2,
    ReceivableControl = 3,
    PayableControl = 4,
    InventoryControl = 5,
    FixedAssetCost = 6,
    AccumulatedDepreciation = 7,
    AssetUnderConstruction = 8,
    InputTax = 9,
    OutputTax = 10,
    WhtReceivable = 11,
    WhtPayable = 12
}

/// <summary>
/// Period type enumeration for fiscal period generation.
/// Determines how many periods are created per fiscal year.
/// </summary>
public enum PeriodType
{
    /// <summary>
    /// Monthly periods - 12 periods per fiscal year.
    /// Period names: "January 2025", "February 2025", etc.
    /// </summary>
    Monthly = 1,

    /// <summary>
    /// Quarterly periods - 4 periods per fiscal year.
    /// Period names: "Q1 2025", "Q2 2025", etc.
    /// </summary>
    Quarterly = 2,

    /// <summary>
    /// Weekly periods - 52-53 periods per fiscal year.
    /// Period names: "Week 1 (Jan 1-7)", "Week 2 (Jan 8-14)", etc.
    /// </summary>
    Weekly = 3,

    /// <summary>
    /// Daily periods - 365-366 periods per fiscal year.
    /// Period names: "Jan 1, 2025", "Jan 2, 2025", etc.
    /// </summary>
    Daily = 4
}

/// <summary>
/// Period status enumeration for fiscal period workflow.
/// </summary>
public enum PeriodStatus
{
    /// <summary>
    /// Future - Period not yet started, no transactions allowed.
    /// </summary>
    Future = 1,

    /// <summary>
    /// Open - Active period, transactions can be posted.
    /// </summary>
    Open = 2,

    /// <summary>
    /// Closed - Period closed but can be reopened by authorized users.
    /// </summary>
    Closed = 3,

    /// <summary>
    /// Locked - Permanently locked, only admin can reopen with justification.
    /// </summary>
    Locked = 4
}

/// <summary>
/// Journal entry posting status.
/// </summary>
public enum JournalStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}

/// <summary>
/// Account transaction direction (debit or credit).
/// </summary>
public enum TransactionType
{
    Debit = 1,
    Credit = 2
}


