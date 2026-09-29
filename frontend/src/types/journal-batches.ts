import type { CreateJournalEntryDto } from '@/types/finance';

export type JournalBatchApprovalStatus =
    | 'Draft'
    | 'PendingApproval'
    | 'PartiallyApproved'
    | 'Approved'
    | 'Rejected'
    | 'Cancelled'
    | 'ReadyToPost';
export type JournalBatchPostingStatus =
    | 'NotReady'
    | 'Ready'
    | 'Posting'
    | 'PartiallyPosted'
    | 'Posted';
export type JournalBatchReversalStatus =
    | 'NotReversed'
    | 'ReversalPending'
    | 'Reversed';
export type JournalBatchItemReviewStatus = 'Pending' | 'Approved' | 'Rejected' | 'NotRequired';
export type JournalBatchItemPostingStatus = 'NotEligible' | 'Ready' | 'Posting' | 'Posted' | 'Failed';

export interface JournalBatchListItem {
    id: string;
    batchNumber: string;
    description: string;
    fiscalPeriodId: string;
    fiscalPeriodName?: string;
    fiscalPeriodStartDate?: string;
    fiscalPeriodEndDate?: string;
    accountingBookId: string;
    bookClassification: string;
    accountingBookName: string;
    accountingBookType: 'PrimaryFull' | 'ParallelFull' | 'Delta';
    controlCurrencyCode: string;
    batchType: 'Standard' | 'Reversal';
    approvalStatus: JournalBatchApprovalStatus;
    approvalRequired?: boolean;
    postingStatus: JournalBatchPostingStatus;
    reversalStatus: JournalBatchReversalStatus;
    isVoided: boolean;
    displayStatus: string;
    expectedDebitTotal: number;
    actualDebitTotal: number;
    actualCreditTotal: number;
    variance: number;
    entryCount: number;
    expectedJournalCount?: number;
    approvedEntryCount: number;
    rejectedEntryCount: number;
    postedEntryCount: number;
    createdAt: string;
    submittedAt?: string;
    postingCompletedAt?: string;
}

export interface JournalBatchReview {
    id: string;
    workflowStageKey: string;
    decision: 'Approved' | 'Rejected';
    comment?: string;
    decidedByUserId: string;
    decidedAt: string;
}

export interface JournalBatchItem {
    id: string;
    sequenceNumber: number;
    journalEntryId: string;
    journalEntryNumber: string;
    entryDate: string;
    journalType: string;
    description: string;
    referenceNumber?: string;
    totalDebit: number;
    totalCredit: number;
    lineCount: number;
    reviewStatus: JournalBatchItemReviewStatus;
    postingStatus: JournalBatchItemPostingStatus;
    finalReviewedByUserId?: string;
    finalReviewedAt?: string;
    finalRejectionReason?: string;
    postingClaimRunId?: string;
    postingClaimedAt?: string;
    postedInRunId?: string;
    postedAt?: string;
    reversalJournalBatchItemId?: string;
    reviews: JournalBatchReview[];
}

export interface EligibleJournalBatchDraft {
    id: string;
    journalEntryNumber: string;
    entryDate: string;
    description: string;
    referenceNumber?: string;
    totalDebit: number;
    totalCredit: number;
    lineCount: number;
}

export interface JournalBatchPostingRun {
    id: string;
    runNumber: number;
    idempotencyKey: string;
    status: 'Pending' | 'Posting' | 'Posted' | 'Failed';
    requestedByUserId: string;
    requestedAt: string;
    startedAt?: string;
    completedAt?: string;
    selectedDebitTotal: number;
    selectedEntryCount: number;
    errorMessage?: string;
    journalBatchItemIds: string[];
}

export interface JournalBatchDetail extends JournalBatchListItem {
    approvedDebitTotal: number;
    rejectedDebitTotal: number;
    postedDebitTotal: number;
    remainingApprovedDebitTotal: number;
    pendingReviewCount: number;
    remainingApprovedEntryCount: number;
    lineCount: number;
    contentFingerprint?: string;
    notes?: string;
    submittedByUserId?: string;
    approvedByUserId?: string;
    workflowInstanceId?: string;
    approvedAt?: string;
    reviewCompletedAt?: string;
    reversalOfJournalBatchId?: string;
    reversalBatchId?: string;
    reversalReason?: string;
    voidedByUserId?: string;
    voidedAt?: string;
    voidReason?: string;
    rowVersion: string;
    canEdit: boolean;
    canSubmit: boolean;
    canReview: boolean;
    canPostAny: boolean;
    canReverseBatch: boolean;
    items: JournalBatchItem[];
    postingRuns: JournalBatchPostingRun[];
    attachmentIds: string[];
    attachments: JournalBatchAttachment[];
}

export interface JournalBatchAttachment {
    fileUploadRecordId: string;
    fileName: string;
    fileUrl: string;
    contentType?: string;
    fileSize: number;
    uploadedAt: string;
}

export interface JournalBatchListResult {
    totalCount: number;
    page: number;
    pageSize: number;
    items: JournalBatchListItem[];
}

export interface CreateJournalBatch {
    description: string;
    fiscalPeriodId: string;
    accountingBookId: string;
    controlCurrencyCode: string;
    expectedDebitTotal: number;
    expectedJournalCount?: number;
    notes?: string;
}

export interface EligibleJournalBatchBook {
    id: string;
    code: string;
    name: string;
    purpose: string;
    bookType: 'PrimaryFull' | 'Delta';
    functionalCurrencyCode?: string;
    isDefault: boolean;
}

export interface JournalBatchValidationIssue {
    code: string;
    message: string;
    journalBatchItemId?: string;
    journalEntryId?: string;
    severity: string;
}

export interface JournalBatchValidation {
    isValid: boolean;
    expectedDebitTotal: number;
    actualDebitTotal: number;
    actualCreditTotal: number;
    variance: number;
    entryCount: number;
    expectedJournalCount?: number;
    lineCount: number;
    issues: JournalBatchValidationIssue[];
}

export interface JournalBatchImportIssue {
    sheet: string;
    row: number;
    column?: string;
    value?: string;
    code: string;
    message: string;
    severity: string;
}

export interface JournalBatchImportPreview {
    sessionId: string;
    previewToken: string;
    expiresAt: string;
    isValid: boolean;
    templateVersion: string;
    fileName: string;
    controlCurrencyCode: string;
    journalCount: number;
    lineCount: number;
    expectedDebitTotal: number;
    actualDebitTotal: number;
    issues: JournalBatchImportIssue[];
}

export interface ReviewJournalBatchItem {
    journalBatchItemId: string;
    decision: 'Approved' | 'Rejected';
    comment?: string;
}

export interface CreateJournalBatchEntry {
    journalEntry: CreateJournalEntryDto;
}
