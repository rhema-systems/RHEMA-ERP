namespace ErpSystem.Core.Enums;

/// <summary>
/// Types of bank accounts
/// </summary>
public enum BankAccountType
{
    Checking = 1,
    Savings = 2,
    Credit = 3,
    Loan = 4,
    Investment = 5
}

/// <summary>
/// Types of cash transactions
/// </summary>
public enum CashTransactionType
{
    Receipt = 1,      // Money coming in
    Payment = 2,      // Money going out
    Transfer = 3      // Movement between accounts
}

/// <summary>
/// Status of bank reconciliation
/// </summary>
public enum ReconciliationStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Approved = 4,
    Rejected = 5
}

/// <summary>
/// Status of cheques
/// </summary>
public enum ChequeStatus
{
    Issued = 1,       // Cheque written but not presented
    Presented = 2,    // Presented to bank
    Cleared = 3,      // Successfully cleared
    Cancelled = 4,    // Cancelled before clearing
    Bounced = 5,      // Returned due to insufficient funds
    Stale = 6         // Expired (> 6 months old)
}

/// <summary>
/// Types of payment methods
/// </summary>
public enum PaymentMethodType
{
    Cash = 1,
    Cheque = 2,
    EFT = 3,              // Electronic Funds Transfer
    Card = 4,             // Credit/Debit card
    MobileMoney = 5,      // Ghana: MTN, Vodafone, AirtelTigo
    DirectDebit = 6,
    StandingOrder = 7,
    BankTransfer = 8
}
