export type VendorInvoiceStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'PartiallyPaid' | 'Paid' | 'Overdue' | 'Voided' | 'Rejected' | 'OnHold';
export type InvoiceMatchingType = 'None' | 'TwoWay' | 'ThreeWay';
export type InvoiceMatchingStatus = 'Unmatched' | 'TwoWayMatched' | 'ThreeWayMatched' | 'MatchException';
export type VendorPaymentStatus = 'Draft' | 'PendingAuthorization' | 'Authorized' | 'Processed' | 'Cleared' | 'Voided' | 'Failed' | 'Reconciled' | 'Reversed';
export type VendorPaymentMethod = 'BankTransfer' | 'Cheque' | 'Cash' | 'WireTransfer' | 'MobileMoney' | 'DirectDebit' | 'Other';
export type PaymentBatchStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Processing' | 'Completed' | 'PartiallyCompleted' | 'Cancelled';
export type VendorInvoiceMatchExceptionStatus = 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled' | 'Expired';
export type VendorInvoiceMatchExceptionEvidenceKind = 'WorkflowEvidenceDocument' | 'CentralDocument';
export type VendorInvoiceMatchCorrectiveActionStatus = 'Planned' | 'Completed';

export interface VendorInvoice {
    id: string;
    invoiceNumber: string;
    supplierInvoiceNumber?: string;
    supplierId: string;
    supplierName: string;
    purchaseOrderId?: string;
    purchaseOrderNumber?: string;
    invoiceDate: string;
    receivedDate?: string;
    dueDate?: string;
    subTotal: number;
    taxAmount: number;
    discountAmount: number;
    totalAmount: number;
    paidAmount: number;
    balanceAmount: number;
    currencyCode: string;
    exchangeRate: number;
    baseCurrencyAmount: number;
    paymentTermsDays: number;
    earlyPaymentDiscountPercentage: number;
    earlyPaymentDiscountDueDate?: string;
    earlyPaymentDiscountAmount: number;
    withholdingTaxRate: number;
    withholdingTaxAmount: number;
    matchingType: InvoiceMatchingType;
    matchingStatus: InvoiceMatchingStatus;
    matchingNotes?: string;
    matchingControlEventId?: string;
    matchingSnapshotHash?: string;
    matchingEvaluatedAtUtc?: string;
    matchingPriceTolerancePercent: number;
    matchingQuantityTolerancePercent: number;
    matchExceptionControlEventId?: string;
    status: VendorInvoiceStatus;
    approvalStatus: string;
    expenseAccountId?: string;
    expenseAccountName?: string;
    apAccountId?: string;
    apAccountName?: string;
    notes?: string;
    reference?: string;
    isOpeningBalance: boolean;
    approvedByUserId?: string;
    approvedAt?: string;
    lineItems: VendorInvoiceLineItem[];
    paymentAllocations: VendorPaymentAllocation[];
    createdAt: string;
    updatedAt?: string;
}

export interface InvoiceMatchingResult {
    vendorInvoiceId: string;
    matchingType: InvoiceMatchingType;
    matchingStatus: InvoiceMatchingStatus;
    isMatched: boolean;
    discrepancies: MatchingDiscrepancy[];
    invoiceTotal: number;
    purchaseOrderTotal?: number;
    goodsReceiptTotal?: number;
    tolerancePercentage: number;
    priceTolerancePercentage: number;
    quantityTolerancePercentage: number;
    isRequired: boolean;
    approvalReady: boolean;
    approvedExceptionApplied: boolean;
    matchingControlEventId?: string;
    matchExceptionControlEventId?: string;
    snapshotHash?: string;
    evaluatedAtUtc?: string;
    message: string;
    configurationProfileCode?: string;
    configurationProfileVersion?: number;
    decisionKeys: string[];
    checks: InvoiceMatchingCheck[];
}

export interface InvoiceMatchingCheck {
    checkKey: string;
    label: string;
    passed: boolean;
    exceptionEligible: boolean;
    message: string;
}

export interface MatchingDiscrepancy {
    itemDescription: string;
    discrepancyType: string;
    invoiceValue: number;
    expectedValue?: number;
    variance: number;
    variancePercentage: number;
    exceptionEligible: boolean;
}

export interface VendorInvoiceMatchExceptionEvidenceRequest {
    requirementKey: string;
    referenceKind: VendorInvoiceMatchExceptionEvidenceKind;
    workflowEvidenceDocumentId?: string;
    fileUploadRecordId?: string;
    evidenceReference: string;
}

export interface CreateVendorInvoiceMatchExceptionRequest {
    rootCauseCategory: string;
    rootCauseDescription: string;
    justification: string;
    correctiveAction: string;
    correctiveActionOwnerId: string;
    correctiveActionDueAtUtc: string;
    expiresAtUtc: string;
    idempotencyKey: string;
    evidence: VendorInvoiceMatchExceptionEvidenceRequest[];
}

export interface VendorInvoiceMatchExceptionVariance {
    id: string;
    varianceType: string;
    itemDescription: string;
    actualValue: number;
    expectedValue: number;
    variance: number;
    variancePercentage: number;
    configuredTolerancePercent: number;
}

export interface VendorInvoiceMatchExceptionEvidence {
    id: string;
    requirementKey: string;
    referenceKind: VendorInvoiceMatchExceptionEvidenceKind;
    workflowEvidenceDocumentId?: string;
    fileUploadRecordId?: string;
    evidenceReference: string;
    evidenceHash: string;
}

export interface VendorInvoiceMatchExceptionAction {
    id: string;
    sequence: number;
    action: string;
    fromStatus: VendorInvoiceMatchExceptionStatus;
    toStatus: VendorInvoiceMatchExceptionStatus;
    actorUserId: string;
    actorName: string;
    comment: string;
    occurredAtUtc: string;
}

export interface VendorInvoiceMatchException {
    id: string;
    vendorInvoiceId: string;
    invoiceNumber: string;
    purchaseOrderId: string;
    purchaseOrderNumber: string;
    sequence: number;
    status: VendorInvoiceMatchExceptionStatus;
    varianceType: string;
    priceTolerancePercent: number;
    quantityTolerancePercent: number;
    rootCauseCategory: string;
    rootCauseDescription: string;
    justification: string;
    correctiveAction: string;
    correctiveActionOwnerId: string;
    correctiveActionOwnerName: string;
    correctiveActionDueAtUtc: string;
    correctiveActionStatus: VendorInvoiceMatchCorrectiveActionStatus;
    correctiveActionCompletedAtUtc?: string;
    correctiveActionCompletedById?: string;
    correctiveActionCompletionNote?: string;
    expiresAtUtc: string;
    invoiceSnapshotHash: string;
    configurationProfileId?: string;
    configurationProfileVersion?: number;
    workflowInstanceId?: string;
    requestedById: string;
    requestedByName: string;
    requestedAtUtc: string;
    finalApprovedById?: string;
    finalApprovedByName?: string;
    finalApprovedAtUtc?: string;
    approvalControlEventId?: string;
    integrityHash: string;
    rowVersion: string;
    variances: VendorInvoiceMatchExceptionVariance[];
    evidence: VendorInvoiceMatchExceptionEvidence[];
    actions: VendorInvoiceMatchExceptionAction[];
}

export interface VendorInvoiceMatchExceptionOverview {
    vendorInvoiceId: string;
    invoiceNumber: string;
    matchingReadiness: InvoiceMatchingResult;
    canRequest: boolean;
    canDecide: boolean;
    canCancel: boolean;
    canCompleteCorrectiveAction: boolean;
    requiredEvidenceKeys: string[];
    decisionKeys: string[];
    active?: VendorInvoiceMatchException;
    correctiveActionItem?: VendorInvoiceMatchException;
    history: VendorInvoiceMatchException[];
}

export interface VendorInvoiceMatchExceptionReportRow {
    exceptionId: string;
    vendorInvoiceId: string;
    invoiceNumber: string;
    supplierId: string;
    supplierName: string;
    purchaseOrderId: string;
    purchaseOrderNumber: string;
    status: VendorInvoiceMatchExceptionStatus;
    varianceType: string;
    maximumVariancePercentage: number;
    rootCauseCategory: string;
    rootCauseDescription: string;
    correctiveAction: string;
    correctiveActionOwnerName: string;
    correctiveActionDueAtUtc: string;
    correctiveActionStatus: VendorInvoiceMatchCorrectiveActionStatus;
    requestedAtUtc: string;
    expiresAtUtc: string;
    requestedByName: string;
    finalApprovedByName?: string;
    finalApprovedAtUtc?: string;
    workflowInstanceId?: string;
    approvalControlEventId?: string;
    evidenceCount: number;
}

export interface VendorInvoiceMatchExceptionReport {
    fromDate: string;
    toDate: string;
    status?: VendorInvoiceMatchExceptionStatus;
    supplierId?: string;
    totalCount: number;
    approvedCount: number;
    expiredCount: number;
    openCorrectiveActionCount: number;
    rows: VendorInvoiceMatchExceptionReportRow[];
}

export interface VendorInvoiceCreateRequest {
    supplierInvoiceNumber?: string;
    supplierId: string;
    purchaseOrderId?: string;
    invoiceDate: string;
    receivedDate?: string;
    dueDate?: string;
    currencyCode?: string;
    exchangeRate?: number;
    paymentTermsDays?: number;
    paymentTermId?: string;
    earlyPaymentDiscountPercentage?: number;
    earlyPaymentDiscountDueDate?: string;
    taxGroupId?: string | null;
    withholdingTaxRate?: number;
    withholdingTaxId?: string | null;
    withholdingTaxAccountId?: string | null;
    matchingType?: InvoiceMatchingType;
    expenseAccountId?: string;
    apAccountId?: string;
    notes?: string;
    reference?: string;
    isOpeningBalance?: boolean;
    lineItems: VendorInvoiceLineItemCreateRequest[];
}

export interface VendorInvoiceUpdateRequest extends VendorInvoiceCreateRequest {
    id: string;
}

export interface VendorInvoiceLineItem {
    id: string;
    vendorInvoiceId: string;
    lineItemType: string;
    glAccountId?: string;
    glAccountName?: string;
    purchaseOrderItemId?: string;
    description: string;
    quantity: number;
    unitPrice: number;
    lineTotal: number;
    taxRate: number;
    taxAmount: number;
    taxCode?: string;
    taxGroupId?: string | null;
    discountPercentage: number;
    discountAmount: number;
    unit?: string;
    inventoryItemId?: string;
    warehouseId?: string;
    locationId?: string;
    serialNumber?: string;
    lotNumber?: string;
    expirationDate?: string;
}

export interface VendorInvoiceLineItemCreateRequest {
    lineItemType?: string;
    glAccountId?: string;
    purchaseOrderItemId?: string;
    description: string;
    quantity?: number;
    unitPrice: number;
    taxRate?: number;
    taxCode?: string;
    taxGroupId?: string | null;
    discountPercentage?: number;
    unit?: string;
    inventoryItemId?: string;
    warehouseId?: string;
    locationId?: string;
    serialNumber?: string;
    lotNumber?: string;
    expirationDate?: string;
}

export interface VendorPayment {
    id: string;
    paymentNumber: string;
    supplierId: string;
    supplierName: string;
    paymentDate: string;
    totalAmount: number;
    allocatedAmount: number;
    unallocatedAmount: number;
    paymentMethod: VendorPaymentMethod;
    paymentMethodId?: string;
    paymentMethodName?: string;
    currencyCode: string;
    exchangeRate: number;
    bankAccountId?: string;
    bankAccountName?: string;
    chequeNumber?: string;
    transactionReference?: string;
    withholdingTaxRate: number;
    withholdingTaxAmount: number;
    withholdingTaxBaseAmount: number;
    withholdingTaxCumulativeBefore: number;
    withholdingTaxThresholdAmount?: number | null;
    withholdingTaxThresholdApplied: boolean;
    withholdingTaxCalculationNote?: string;
    discountTaken: number;
    status: VendorPaymentStatus;
    submittedById?: string;
    submittedAt?: string;
    workflowInstanceId?: string;
    appliedApprovalPolicySetId?: string;
    appliedApprovalPolicyCode?: string;
    approvalControlSnapshotHash?: string;
    isExceptionalPayment: boolean;
    exceptionalPaymentReason?: string;
    requiresManagingDirectorApproval: boolean;
    managingDirectorApprovedById?: string;
    managingDirectorApprovedAt?: string;
    evidenceExceptionRequested: boolean;
    evidenceExceptionReason?: string;
    evidenceExceptionRequestedById?: string;
    evidenceExceptionRequestedAt?: string;
    evidenceExceptionApprovedById?: string;
    evidenceExceptionApprovedAt?: string;
    authorizedById?: string;
    authorizedDate?: string;
    invoicePaymentSodControlEventId?: string;
    paymentBatchId?: string;
    paymentBatchNumber?: string;
    journalEntryId?: string;
    reversalJournalEntryId?: string;
    reversalPostingEventId?: string;
    reversalDate?: string;
    reversedAt?: string;
    reversedById?: string;
    reversalReason?: string;
    notes?: string;
    createdAt: string;
    allocations: VendorPaymentAllocation[];
}

export interface VendorPaymentCreateRequest {
    supplierId: string;
    paymentDate: string;
    totalAmount: number;
    paymentMethod?: VendorPaymentMethod;
    paymentMethodId?: string;
    currencyCode?: string;
    exchangeRate?: number;
    bankAccountId?: string;
    chequeNumber?: string;
    transactionReference?: string;
    withholdingTaxRate?: number;
    withholdingTaxAmount?: number;
    withholdingTaxBaseAmount?: number;
    withholdingTaxId?: string;
    withholdingTaxAccountId?: string | null;
    withholdingCertificateNumber?: string;
    withholdingCertificateDate?: string;
    notes?: string;
    allocations?: VendorPaymentAllocationCreateRequest[];
}

export interface VendorPaymentAllocation {
    id: string;
    vendorPaymentId: string;
    vendorInvoiceId: string;
    invoiceNumber: string;
    allocatedAmount: number;
    discountAmount: number;
    withholdingTaxAmount: number;
    allocationDate: string;
    notes?: string;
    isReversal: boolean;
    paymentReadinessControlEventId?: string;
    paymentReadinessSnapshotHash?: string;
    paymentReadinessEvaluatedAtUtc?: string;
}

export interface VendorPaymentAllocationCreateRequest {
    vendorInvoiceId: string;
    allocatedAmount: number;
    discountAmount?: number;
    withholdingTaxAmount?: number;
    notes?: string;
}

/** Explicit exception decisions captured when a maker submits a direct AP payment. */
export interface SubmitVendorPaymentRequest {
    isExceptionalPayment: boolean;
    exceptionalPaymentReason?: string;
    requestEvidenceException: boolean;
    evidenceExceptionReason?: string;
}

/**
 * Server-computed payment-control readiness. Keeping this as a dedicated read model means the
 * client never guesses which effective-dated policy or evidence subset applies.
 */
export interface VendorPaymentControl {
    paymentId: string;
    policyCode?: string;
    policySetId?: string;
    policySnapshotHash?: string;
    workflowInstanceId?: string;
    workflowStatus?: string;
    currentStepInstanceId?: string;
    currentStepName?: string;
    isExceptionalPayment: boolean;
    requiresManagingDirectorApproval: boolean;
    managingDirectorApprovalCompleted: boolean;
    evidenceExceptionRequested: boolean;
    evidenceExceptionApproved: boolean;
    evidenceRequirementsSatisfied: boolean;
    canSubmit: boolean;
    minimumExceptionReasonLength: number;
    evidenceRequirements: VendorPaymentEvidenceRequirementStatus[];
    evidenceDocuments: VendorPaymentEvidenceDocument[];
    blockingReasons: string[];
}

export interface VendorPaymentEvidenceRequirementStatus {
    requirementKey: string;
    documentName: string;
    documentType?: string;
    minimumDocuments: number;
    requireVerification: boolean;
    currentDocumentCount: number;
    verifiedDocumentCount: number;
    isSatisfied: boolean;
}

export interface VendorPaymentEvidenceDocument {
    id: string;
    attachmentId: string;
    requirementKey?: string;
    documentName?: string;
    documentType?: string;
    fileName: string;
    verificationStatus: string;
    malwareScanStatus: string;
    uploadedAt: string;
    uploadedById: string;
    verifiedById?: string;
    verifiedAt?: string;
    verificationNotes?: string;
    sha256: string;
}

export interface ReverseVendorPaymentRequest {
    reason: string;
    reversalDate?: string;
}

/** Source-to-ledger evidence returned by the AP payment trace endpoint. */
export interface VendorPaymentTrace {
    payment: VendorPayment;
    postings: VendorPaymentPostingTrace[];
    auditEvents: VendorPaymentAuditTrace[];
}

export interface VendorPaymentPostingTrace {
    postingEventId: string;
    postingAction: string;
    postingStatus: string;
    postingDate: string;
    postedAt?: string;
    journalEntryId?: string;
    journalEntryNumber?: string;
    originalJournalEntryId?: string;
    reversalJournalEntryId?: string;
    totalDebitAmount: number;
    totalCreditAmount: number;
    functionalCurrencyCode: string;
    lines: VendorPaymentJournalLineTrace[];
}

export interface VendorPaymentJournalLineTrace {
    transactionId: string;
    lineNumber: number;
    accountId: string;
    accountNumber: string;
    accountName: string;
    description: string;
    debitAmount: number;
    creditAmount: number;
    transactionCurrency: string;
    foreignCurrencyAmount?: number;
    exchangeRate?: number;
    originalTransactionId?: string;
    reversalTransactionId?: string;
}

export interface VendorPaymentAuditTrace {
    auditLogId: string;
    eventType: string;
    timestamp: string;
    userId: string;
    username: string;
    beforeValuesJson?: string;
    detailsJson?: string;
}

export interface PaymentBatch {
    id: string;
    batchNumber: string;
    description?: string;
    batchDate: string;
    dueDateFrom?: string;
    dueDateTo?: string;
    totalAmount: number;
    paymentCount: number;
    paymentMethod: VendorPaymentMethod;
    paymentMethodId?: string;
    paymentMethodName?: string;
    bankAccountId?: string;
    bankAccountName?: string;
    status: PaymentBatchStatus;
    createdById?: string;
    approvedById?: string;
    approvedDate?: string;
    invoicePaymentSodControlEventId?: string;
    processedById?: string;
    processedDate?: string;
    notes?: string;
    createdAt: string;
    items: PaymentBatchItem[];
}

export interface PaymentBatchCreateRequest {
    description?: string;
    batchDate: string;
    dueDateFrom?: string;
    dueDateTo?: string;
    paymentMethod?: VendorPaymentMethod;
    paymentMethodId?: string;
    bankAccountId?: string;
    notes?: string;
    invoiceIds: string[];
}

export interface PaymentBatchItem {
    id: string;
    vendorPaymentId: string;
    paymentNumber: string;
    supplierName: string;
    amount: number;
    itemStatus: string;
    failureReason?: string;
    invoices: PaymentBatchInvoice[];
}

export interface PaymentBatchInvoice {
    id: string;
    vendorInvoiceId: string;
    invoiceNumber: string;
    amount: number;
    status: string;
    failureReason?: string;
    paymentReadinessControlEventId?: string;
    paymentReadinessSnapshotHash?: string;
    paymentReadinessEvaluatedAtUtc?: string;
}

export interface InvoicePaymentSodReadiness {
    sourceType: 'VendorPayment' | 'PaymentBatch';
    sourceId: string;
    sourceReference: string;
    currentActorUserId: string;
    canApprove: boolean;
    hasInvoiceProcessorLineage: boolean;
    code: string;
    message: string;
    evaluatedAtUtc: string;
    controlEventId?: string;
    policySetId?: string;
    policyCode?: string;
    policyVersion?: number;
    ruleId?: string;
    ruleCode?: string;
    decisionKeys: string[];
    invoices: InvoicePaymentSodInvoice[];
}

export interface InvoicePaymentSodInvoice {
    vendorInvoiceId: string;
    invoiceNumber: string;
    invoiceProcessorUserId?: string;
    submittedAtUtc?: string;
    processorLineagePresent: boolean;
    conflictsWithCurrentActor: boolean;
}

// Reports
export interface ApAgingReport {
    asOfDate: string;
    currencyCode: string;
    totalOutstanding: number;
    current: number;
    thirtyDays: number;
    sixtyDays: number;
    ninetyPlusDays: number;
    totalSuppliers: number;
    totalInvoices: number;
    supplierDetails: SupplierAgingDetail[];
}

export interface SupplierAgingDetail {
    supplierId: string;
    supplierName: string;
    supplierCode?: string;
    totalOutstanding: number;
    current: number;
    thirtyDays: number;
    sixtyDays: number;
    ninetyPlusDays: number;
    invoiceCount: number;
    oldestInvoiceDate?: string;
    invoices?: ApAgingInvoice[];
}

export interface ApAgingInvoice {
    invoiceId: string;
    invoiceNumber: string;
    invoiceDate: string;
    dueDate?: string;
    totalAmount: number;
    balanceAmount: number;
    daysOutstanding: number;
    agingBucket: string;
}

export interface SupplierDetailedLedgerReport {
    fromDate: string;
    toDate: string;
    currencyCode: string;
    showSupplierCurrency: boolean;
    totalOpeningBalance: number;
    totalDebits: number;
    totalCredits: number;
    totalClosingBalance: number;
    warnings: string[];
    suppliers: SupplierDetailedLedgerAccount[];
}

export interface SupplierDetailedLedgerAccount {
    supplierId: string;
    businessPartnerId?: string;
    supplierCode: string;
    supplierName: string;
    currencyCode: string;
    openingBalance: number;
    totalDebits: number;
    totalCredits: number;
    closingBalance: number;
    lines: SupplierDetailedLedgerLine[];
}

export interface SupplierDetailedLedgerLine {
    sourceDocumentId: string;
    transactionDate: string;
    transactionType: string;
    documentNumber: string;
    reference?: string;
    description: string;
    transactionCurrencyCode: string;
    exchangeRate: number;
    debit: number;
    credit: number;
    runningBalance: number;
}

export interface CashRequirementForecast {
    asOfDate: string;
    currencyCode: string;
    totalPayable: number;
    overdueAmount: number;
    periods: CashRequirementPeriod[];
}

export interface CashRequirementPeriod {
    period: string;
    periodStart: string;
    periodEnd: string;
    amountDue: number;
    invoiceCount: number;
    discountAvailable: number;
}

export interface ApSummaryStats {
    totalOutstanding: number;
    totalOverdue: number;
    outstandingInvoiceCount: number;
    overdueInvoiceCount: number;
    averageDaysToPayment: number;
    totalPaidThisMonth: number;
    discountsTaken: number;
    discountsMissed: number;
    withholdingTaxThisMonth: number;
    pendingApprovalCount: number;
    pendingBatchCount: number;
}

export interface SubledgerControlReconciliation {
    sourceModule: string;
    asOfDate: string;
    controlAccountId?: string;
    controlAccountNumber?: string;
    controlAccountName?: string;
    readModelOutstanding: number;
    postedGlControlBalance: number;
    variance: number;
    documentCount: number;
    diagnosticCount: number;
    diagnostics: Array<{
        sourceModule: string;
        code: string;
        message: string;
        sourceDocumentId?: string;
        settlementSourceId?: string;
        postingEventId?: string;
        varianceAmount?: number;
    }>;
}

export interface ProcurementFinanceReconciliationReport {
    asOfDate: string;
    generatedAtUtc: string;
    ruleCode: 'AP-005';
    taskCode: 'TDC-0508';
    decisionKeys: string[];
    isReconciled: boolean;
    purchaseOrderCount: number;
    issueCount: number;
    unbalancedPostingCount: number;
    controlledReversalCount: number;
    apControlReconciliation: SubledgerControlReconciliation;
    currencySummaries: ProcurementFinanceReconciliationCurrencySummary[];
    rows: ProcurementFinanceReconciliationRow[];
}

export interface ProcurementFinanceReconciliationCurrencySummary {
    currencyCode: string;
    purchaseOrderAmount: number;
    commitmentAmount: number;
    acceptedReceiptAmount: number;
    invoiceAmount: number;
    settledAmount: number;
    invoicePostedAmount: number;
    paymentPostedAmount: number;
    retentionHeldAmount: number;
    retentionReleasedAmount: number;
    milestoneAmount: number;
}

export interface ProcurementFinanceReconciliationRow {
    purchaseOrderId: string;
    purchaseOrderNumber: string;
    purchaseOrderStatus: string;
    currencyCode: string;
    sourceRequisitionId?: string;
    contractId?: string;
    purchaseOrderAmount: number;
    commitmentAmount: number;
    commitmentGroupOrderAmount: number;
    acceptedReceiptAmount: number;
    invoiceAmount: number;
    settledAmount: number;
    invoicePostedAmount: number;
    paymentPostedAmount: number;
    retentionHeldAmount: number;
    retentionReleasedAmount: number;
    retentionOutstandingAmount: number;
    milestoneAmount: number;
    completedMilestoneAmount: number;
    invoicedMilestoneAmount: number;
    paidMilestoneAmount: number;
    invoiceCount: number;
    paymentCount: number;
    postingCount: number;
    controlledReversalCount: number;
    isReconciled: boolean;
    issues: ProcurementFinanceReconciliationIssue[];
}

export interface ProcurementFinanceReconciliationIssue {
    code: string;
    severity: 'Error' | 'Warning' | string;
    area: string;
    message: string;
    expectedAmount?: number;
    actualAmount?: number;
    varianceAmount?: number;
    sourceDocumentId?: string;
}

export interface OutstandingVendorInvoice {
    invoiceId: string;
    invoiceNumber: string;
    supplierInvoiceNumber?: string;
    invoiceDate: string;
    dueDate?: string;
    totalAmount: number;
    paidAmount: number;
    balanceAmount: number;
    daysOverdue: number;
    earlyPaymentDiscountPercentage?: number;
    earlyPaymentDiscountDueDate?: string;
    isDiscountAvailable: boolean;
    discountAmount?: number;
    paymentReadiness?: VendorPaymentInvoiceReadiness;
}

export interface VendorPaymentInvoiceReadiness {
    vendorInvoiceId: string;
    invoiceNumber: string;
    invoiceStatus: VendorInvoiceStatus;
    outstandingAmount: number;
    isPaymentReady: boolean;
    invoiceStateReady: boolean;
    threeWayMatchRequired: boolean;
    threeWayMatchReady: boolean;
    receiptInspectionReady: boolean;
    approvedExceptionApplied: boolean;
    persistedMatchCurrent: boolean;
    matchingControlEventId?: string;
    matchExceptionControlEventId?: string;
    matchSnapshotHash?: string;
    paymentReadinessControlEventId?: string;
    snapshotHash: string;
    evaluatedAtUtc: string;
    message: string;
    configurationProfileCode?: string;
    configurationProfileVersion?: number;
    decisionKeys: string[];
    checks: InvoiceMatchingCheck[];
}
