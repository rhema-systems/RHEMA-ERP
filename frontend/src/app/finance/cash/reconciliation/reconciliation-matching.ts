import {
    CashTransactionType,
    type UnmatchedStatementLine,
    type UnmatchedTransaction,
} from '@/types/cash-management';

export function isReconciliationDirectionCompatible(
    transaction: UnmatchedTransaction,
    line: UnmatchedStatementLine,
) {
    if (
        transaction.transactionType === CashTransactionType.Receipt
        || transaction.transactionType === CashTransactionType.Deposit
    ) {
        return line.creditAmount > 0 && line.debitAmount === 0;
    }

    if (
        transaction.transactionType === CashTransactionType.Payment
        || transaction.transactionType === CashTransactionType.ReturnedCheque
    ) {
        return line.debitAmount > 0 && line.creditAmount === 0;
    }

    if (transaction.transactionType === CashTransactionType.Transfer) {
        const transactionNumber = transaction.transactionNumber.toUpperCase();
        if (transactionNumber.endsWith('-OUT')) {
            return line.debitAmount > 0 && line.creditAmount === 0;
        }
        if (transactionNumber.endsWith('-IN')) {
            return line.creditAmount > 0 && line.debitAmount === 0;
        }
    }

    return false;
}
