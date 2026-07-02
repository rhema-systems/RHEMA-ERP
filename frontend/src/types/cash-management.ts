// Cash Management & Bank Reconciliation Types

export enum BankAccountType {
    Checking = 'Checking',
    Savings = 'Savings',
    Credit = 'Credit',
    Loan = 'Loan',
    Investment = 'Investment'
}

export enum CashTransactionType {
    Receipt = 'Receipt',
    Payment = 'Payment',
    Transfer = 'Transfer'
}

export enum ReconciliationStatus {
    Pending = 'Pending',
    InProgress = 'InProgress',
    Completed = 'Completed',
    Approved = 'Approved',
    Rejected = 'Rejected'
}

export enum ChequeStatus {
    Issued = 'Issued',
    Presented = 'Presented',
    Cleared = 'Cleared',
    Cancelled = 'Cancelled',
    Bounced = 'Bounced',
    Stale = 'Stale'
}

export enum PaymentMethodType {
    Cash = 'Cash',
    Cheque = 'Cheque',
    EFT = 'EFT',
    Card = 'Card',
    MobileMoney = 'MobileMoney',
    DirectDebit = 'DirectDebit',
    StandingOrder = 'StandingOrder',
    BankTransfer = 'BankTransfer'
}

// Entity Interfaces

export interface BankAccount {
    id: string;
    accountNumber: string;
    accountName: string;
    bankName: string;
    bankBranch?: string;
    currency: string;
    accountType: BankAccountType;
    glAccountId?: string;
    glAccountNumber?: string;
    glAccountName?: string;
    currentBalance: number;
    availableBalance: number;
    openingBalance: number;
    isActive: boolean;
    openingDate: string;
    closingDate?: string;
    notes?: string;
    createdAt: string;
    createdBy?: string;
}

export interface CashTransaction {
    id: string;
    transactionNumber: string;
    transactionDate: string;
    transactionType: CashTransactionType;
    bankAccountId: string;
    bankAccountName: string;
    toBankAccountId?: string;
    toBankAccountName?: string;
    amount: number;
    currency: string;
    exchangeRate?: number;
    baseAmount: number;
    paymentMethodId?: string;
    paymentMethodName?: string;
    referenceNumber?: string;
    payeeOrPayer?: string;
    description?: string;
    glAccountId?: string;
    glAccountNumber?: string;
    glAccountName?: string;
    isReconciled: boolean;
    reconciliationId?: string;
    chequeId?: string;
    chequeNumber?: string;
    isPosted: boolean;
    postedDate?: string;
    createdAt: string;
    createdBy?: string;
}

export interface BankStatement {
    id: string;
    bankAccountId: string;
    statementDate: string;
    statementNumber?: string;
    openingBalance: number;
    closingBalance: number;
    totalDebits: number;
    totalCredits: number;
    importedAt: string;
    importedBy?: string;
    notes?: string;
}

export interface BankStatementLine {
    id: string;
    bankStatementId: string;
    transactionDate: string;
    valueDate?: string;
    description?: string;
    referenceNumber?: string;
    debitAmount: number;
    creditAmount: number;
    balance: number;
    isMatched: boolean;
    matchedTransactionId?: string;
    reconciliationMatchId?: string;
}

export interface BankReconciliation {
    id: string;
    bankAccountId: string;
    bankAccountName: string;
    reconciliationDate: string;
    statementId?: string;
    statementBalance: number;
    bookBalance: number;
    difference: number;
    status: ReconciliationStatus;
    matchedCount: number;
    unmatchedBookCount: number;
    unmatchedStatementCount: number;
    reconciledBy?: string;
    reconciledByName?: string;
    reconciledAt?: string;
    approvedBy?: string;
    approvedByName?: string;
    approvedAt?: string;
    notes?: string;
    createdAt: string;
}

export interface ReconciliationMatch {
    id: string;
    reconciliationId: string;
    cashTransactionId: string;
    bankStatementLineId: string;
    isAutoMatched: boolean;
    matchConfidence?: number;
    matchedAt: string;
    notes?: string;
}

export interface Cheque {
    id: string;
    chequeNumber: string;
    bankAccountId: string;
    bankAccountName: string;
    issueDate: string;
    payeeName?: string;
    amount: number;
    currency: string;
    status: ChequeStatus;
    presentedDate?: string;
    clearedDate?: string;
    cancelledDate?: string;
    memo?: string;
    cashTransactionId?: string;
    statusReason?: string;
    createdAt: string;
}

export interface PaymentMethod {
    id: string;
    name: string;
    code?: string;
    type: PaymentMethodType;
    description?: string;
    isActive: boolean;
    requiresBankAccount: boolean;
    requiresReference: boolean;
    defaultGLAccountId?: string;
    defaultGLAccountNumber?: string;
    defaultGLAccountName?: string;
}

// DTO Interfaces

export interface CreateBankAccountDto {
    accountNumber: string;
    accountName: string;
    bankName: string;
    bankBranch?: string;
    currency: string;
    accountType: BankAccountType;
    glAccountId?: string;
    openingBalance: number;
    openingBalanceExchangeRate?: number;
    openingDate: string;
    notes?: string;
}

export interface UpdateBankAccountDto {
    accountName: string;
    bankName: string;
    bankBranch?: string;
    glAccountId?: string;
    isActive: boolean;
    notes?: string;
}

export interface CreateCashReceiptDto {
    transactionDate: string;
    bankAccountId: string;
    amount: number;
    currency: string;
    exchangeRate?: number;
    paymentMethodId?: string;
    referenceNumber?: string;
    payerName?: string;
    description?: string;
    glAccountId?: string;
}

export interface CreateCashPaymentDto {
    transactionDate: string;
    bankAccountId: string;
    amount: number;
    currency: string;
    exchangeRate?: number;
    paymentMethodId?: string;
    referenceNumber?: string;
    payeeName?: string;
    description?: string;
    glAccountId?: string;
    chequeId?: string;
}

export interface CreateBankTransferDto {
    transactionDate: string;
    fromBankAccountId: string;
    toBankAccountId: string;
    amount: number;
    exchangeRate?: number;
    referenceNumber?: string;
    description?: string;
}

export interface StartReconciliationDto {
    bankAccountId: string;
    reconciliationDate: string;
    statementBalance: number;
    statementId?: string;
}

export interface CreateManualMatchDto {
    reconciliationId: string;
    cashTransactionId: string;
    bankStatementLineId: string;
    notes?: string;
}

export interface ReconciliationSummary {
    reconciliationId: string;
    statementBalance: number;
    bookBalance: number;
    difference: number;
    totalMatches: number;
    autoMatches: number;
    manualMatches: number;
    unmatchedBookTransactions: UnmatchedTransaction[];
    unmatchedStatementLines: UnmatchedStatementLine[];
}

export interface UnmatchedTransaction {
    id: string;
    transactionDate: string;
    description: string;
    amount: number;
    referenceNumber?: string;
}

export interface UnmatchedStatementLine {
    id: string;
    transactionDate: string;
    description: string;
    amount: number;
    referenceNumber?: string;
}

// Summary/Report Interfaces

export interface CashPositionSummary {
    totalBalance: number;
    currency: string;
    accountCount: number;
    byAccountType: {
        type: BankAccountType;
        balance: number;
        count: number;
    }[];
    byCurrency: {
        currency: string;
        balance: number;
        count: number;
    }[];
}

export interface CashFlowSummary {
    period: string;
    openingBalance: number;
    receipts: number;
    payments: number;
    transfers: number;
    closingBalance: number;
    netChange: number;
}
