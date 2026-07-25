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
/// Workflow approval status for cash/bank transactions.
/// </summary>
public enum CashTransactionApprovalStatus
{
    Captured = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Returned = 5,
    Cancelled = 6,
    Posted = 7
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
    Rejected = 5,
    Cancelled = 6
}

/// <summary>
/// Supported reconciliation adjustment types.
/// </summary>
public enum ReconciliationAdjustmentType
{
    BankCharge = 1,
    BankFee = 2,
    InterestIncome = 3,
    AdjustmentReceipt = 4,
    AdjustmentPayment = 5,
    CorrectionReceipt = 6,
    CorrectionPayment = 7
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
