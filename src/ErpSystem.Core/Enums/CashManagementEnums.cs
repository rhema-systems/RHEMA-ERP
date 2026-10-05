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
    Transfer = 3,     // Movement between accounts
    Deposit = 4,      // Posted liquidity settlement into a bank account
    ReturnedCheque = 5 // Bank debit raised when a deposited cheque is returned
}

/// <summary>
/// Identifies which bank-account side of one transfer a <see cref="CashTransaction"/> row
/// represents. Transfer legs used to be inferred only from an OUT/IN document-number suffix;
/// retaining an explicit value makes cross-currency amounts and reconciliation lineage safe to
/// query even when a document-number format is changed later.
/// </summary>
public enum BankTransferLeg
{
    Outgoing = 1,
    Incoming = 2
}

/// <summary>
/// Operational stores and settlement channels that hold monetary value before it reaches
/// (or after it leaves) a physical bank account.
/// </summary>
public enum LiquidityAccountType
{
    Bank = 1,
    UndepositedCash = 2,
    ChequesAwaitingDeposit = 3,
    MobileMoneyClearing = 4,
    CardSettlementClearing = 5,
    CashTill = 6,
    OtherSettlementClearing = 7
}

public enum LiquidityEntryDirection
{
    Increase = 1,
    Decrease = 2
}

public enum LiquidityEntryType
{
    CustomerReceipt = 1,
    DirectReceipt = 2,
    CashExpense = 3,
    PettyCashReplenishment = 4,
    CustomerRefund = 5,
    OtherPayment = 6,
    DepositTransfer = 7,
    Settlement = 8,
    ReturnedCheque = 9,
    Reversal = 10,
    Adjustment = 11
}

/// <summary>
/// Operational lifecycle for a physical cashier till. A submitted session is frozen for
/// independent review; returning it re-opens the same session for a corrected denomination count.
/// </summary>
public enum CashierTillSessionStatus
{
    Open = 1,
    PendingReview = 2,
    Closed = 3,
    Cancelled = 4
}

public enum BankDepositStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Posted = 4,
    Returned = 5,
    Rejected = 6,
    Cancelled = 7,
    Reversed = 8
}

/// <summary>
/// Operational acknowledgement that the destination bank accepted a posted deposit. This is
/// deliberately separate from GL posting and bank-statement reconciliation: each answers a
/// different control question and therefore retains its own timestamp and actor.
/// </summary>
public enum BankDepositConfirmationStatus
{
    Pending = 1,
    Confirmed = 2
}

public enum BankDepositAllocationType
{
    Receipt = 1,
    Deduction = 2
}

public enum DepositPolicy
{
    DepositIntact = 1,
    ControlledNetBanking = 2
}

public enum ReturnedChequeCaseStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Posted = 4,
    Returned = 5,
    Rejected = 6,
    Cancelled = 7,
    Reversed = 8
}

public enum ReturnedChequeChargeTreatment
{
    CustomerRecoverable = 1,
    BankChargeExpense = 2,
    Split = 3
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
