/**
 * Journal Entry Response Normalizer
 * 
 * Transforms raw backend JournalEntryDto/AccountTransactionDto responses
 * into the enriched JournalEntry shape expected by frontend UI components.
 * 
 * This bridges the gap between:
 *   Backend (JournalEntryDto):  journalNumber, transactionDate, totalDebit, status
 *   Frontend (JournalEntry):   journalEntryNumber, entryDate, totalDebitAmount, postingStatus
 */

import type { JournalEntry, AccountTransaction, PostingStatus } from '@/types/finance';

/**
 * Normalizes a raw backend journal entry response into the enriched
 * frontend JournalEntry type with backward-compatible aliases.
 */
export function normalizeJournalEntry(raw: any): JournalEntry {
    const transactions = (raw.transactions || []).map(normalizeAccountTransaction);
    const inferredCurrency =
        raw.primaryCurrency ||
        raw.currencyCode ||
        transactions.find((t: AccountTransaction) => (t as any).currencyCode)?.currencyCode ||
        'GHS';

    const normalizedAttachments = Array.isArray(raw.attachments) && raw.attachments.length > 0
        ? raw.attachments.map((a: any) => ({
            id: a.id || a.fileId || `${a.fileName || 'att'}-${a.uploadedAt || raw.id || '0'}`,
            journalEntryId: a.journalEntryId || raw.id || '',
            fileId: a.fileId || a.id || '',
            fileName: a.fileName || a.originalFileName || `Attachment ${String(a.fileId || a.id || '').slice(0, 8)}`,
            fileUrl: a.fileUrl || a.publicUrl || a.url || '',
            contentType: a.contentType || 'application/octet-stream',
            uploadedAt: a.uploadedAt || raw.createdAt || new Date().toISOString(),
            uploadedBy: a.uploadedBy || raw.createdBy || raw.createdById || '',
        }))
        : Array.isArray(raw.attachmentIds)
            ? raw.attachmentIds.map((fileId: string) => ({
                id: fileId,
                journalEntryId: raw.id || '',
                fileId,
                fileName: `Attachment ${String(fileId).slice(0, 8)}`,
                fileUrl: '',
                contentType: 'application/octet-stream',
                uploadedAt: raw.createdAt || new Date().toISOString(),
                uploadedBy: raw.createdBy || raw.createdById || '',
            }))
            : [];
    
    const totalDebit = raw.totalDebit ?? transactions
        .filter((t: AccountTransaction) => t.transactionType === 'Debit')
        .reduce((sum: number, t: AccountTransaction) => sum + t.amount, 0);
    
    const totalCredit = raw.totalCredit ?? transactions
        .filter((t: AccountTransaction) => t.transactionType === 'Credit')
        .reduce((sum: number, t: AccountTransaction) => sum + t.amount, 0);

    const status = raw.status || raw.postingStatus || 'Draft';

    return {
        ...raw,
        // Primary backend fields
        journalNumber: raw.journalNumber || raw.journalEntryNumber || '',
        transactionDate: raw.transactionDate || raw.entryDate || '',
        description: raw.description || '',
        reference: raw.reference || raw.referenceNumber || undefined,
        totalDebit,
        totalCredit,
        status,
        isReversed: raw.isReversed ?? false,
        reversalJournalId: raw.reversalJournalId || raw.reversalJournalEntryId || undefined,
        postedDate: raw.postedDate || raw.postingDate || undefined,
        requiresApproval: raw.requiresApproval ?? false,
        transactions,
        attachments: normalizedAttachments,

        // Backward-compatible aliases for UI components
        journalEntryNumber: raw.journalNumber || raw.journalEntryNumber || '',
        entryDate: raw.transactionDate || raw.entryDate || '',
        referenceNumber: raw.reference || raw.referenceNumber || undefined,
        totalDebitAmount: totalDebit,
        totalCreditAmount: totalCredit,
        postingStatus: status as PostingStatus,
        reversalJournalEntryId: raw.reversalJournalId || raw.reversalJournalEntryId || undefined,
        postingDate: raw.postedDate || raw.postingDate || undefined,
        createdBy: raw.createdBy || raw.createdByUserName || raw.createdById || '',
        updatedBy: raw.updatedBy || '',
        primaryCurrency: inferredCurrency,
        journalType: raw.journalType || raw.sourceDocumentType || raw.sourceModule || 'General',
    };
}

/**
 * Normalizes a raw backend AccountTransactionDto into the enriched
 * AccountTransaction type with debitAmount/creditAmount helpers.
 */
export function normalizeAccountTransaction(raw: any): AccountTransaction {
    const transactionType = raw.transactionType || 'Debit';
    const amount = raw.amount || 0;

    return {
        ...raw,
        id: raw.id || '',
        accountId: raw.accountId || '',
        accountName: raw.accountName || '',
        accountNumber: raw.accountNumber || '',
        accountCode: raw.accountNumber || raw.accountCode || '',
        journalEntryId: raw.journalEntryId || '',
        amount,
        transactionType,
        description: raw.description || '',
        transactionDate: raw.transactionDate || '',
        reference: raw.reference || '',
        balanceAfter: raw.balanceAfter || 0,
        // Computed debit/credit split for UI table display
        debitAmount: transactionType === 'Debit' ? amount : 0,
        creditAmount: transactionType === 'Credit' ? amount : 0,
        lineNumber: raw.lineNumber,
    };
}

/**
 * Normalizes an array of journal entries.
 */
export function normalizeJournalEntries(rawList: any[]): JournalEntry[] {
    return (rawList || []).map(normalizeJournalEntry);
}
