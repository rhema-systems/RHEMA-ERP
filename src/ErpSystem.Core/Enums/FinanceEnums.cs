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
