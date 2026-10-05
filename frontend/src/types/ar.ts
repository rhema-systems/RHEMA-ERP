import type { ControlledDocumentIssueSummary } from '@/types/controlled-documents';
import type { FinanceSettlementDimensionComponent, FinanceSourceDocumentDimension, FinanceSourceDocumentDimensionInput } from './finance';

export interface Customer {
    id: string;
    customerCode: string;
    customerName: string;
    email?: string;
    phone?: string;
    address?: string;
    city?: string;
    country?: string;
    creditLimit: number;
    outstandingBalance: number;
    customerCreditBalance: number;
    paymentTermsDays: number;
    paymentTermId?: string | null;
    priceGroup?: string;
    currencyCode: string;
    lastPaymentDate?: string;
    overdueAmount?: number;
    isActive: boolean;
    isBlacklisted: boolean;
    isTransactionReady: boolean;
    readinessCode: string;
    readinessMessage: string;
    notes?: string;
    tenantId: string;
    createdAt: string;
    updatedAt?: string;
}

export interface CustomerCreateRequest {
    customerCode: string;
    customerName: string;
    email?: string;
    phone?: string;
    address?: string;
    city?: string;
    country?: string;
    creditLimit?: number;
    paymentTermsDays?: number;
    paymentTermId?: string | null;
    priceGroup?: string;
    currencyCode: string;
    notes?: string;
}

export interface CustomerUpdateRequest extends Partial<CustomerCreateRequest> {
    id: string;
    status?: 'Active' | 'Inactive' | 'OnHold';
    isActive?: boolean;
    currencyCode?: string;
}

export interface InvoiceLineItem {
    id: string;
    invoiceId: string;
    lineItemType: 'Product' | 'GLAccount';
    productId?: string;
    glAccountId?: string;
    glAccountCode?: string;
    glAccountName?: string;
    description: string;
    quantity: number;
    unitPrice: number;
    taxRate: number;
    taxAmount?: number;
    taxCode?: string;
    taxGroupId?: string | null;
    taxTreatment?: number;
    unit?: string;
    discountPercentage: number;
    discountAmount: number;
    lineTotal: number;
}

export interface Invoice {
    id: string;
    invoiceNumber: string;
    businessPartnerId: string;
    businessPartnerRoleId: string;
    businessPartnerArProfileVersionId: string;
    businessPartnerCode: string;
    businessPartnerLegalName?: string;
    businessPartnerTin?: string;
    customerName: string;
    customerAddress?: string;
    invoiceDate: string;
    dueDate: string | null;
    subTotal?: number;
    taxAmount?: number;
    totalAmount: number;
    roundingAdjustmentAmount?: number;
    financeRoundingEvidenceId?: string;
    paidAmount: number;
    balanceAmount: number;
    status: 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected' | 'ReadyToPost' | 'Sent' | 'Posted' | 'PartiallyPaid' | 'Paid' | 'Void' | 'Cancelled' | 'Overdue';
    approvalRequired?: boolean;
    workflowInstanceId?: string | null;
    currencyCode: string;
    exchangeRate: number;
    exchangeRateId?: string;
    currencyOverrideReason?: string | null;
    paymentTermsDays: number;
    paymentTermId?: string | null;
    discountAmount: number;
    discountReason?: string | null;
    taxGroupId?: string | null;
    reference?: string;
    isOpeningBalance: boolean;
    earlyPaymentDiscountPercentage?: number;
    earlyPaymentDiscountDueDate?: string;
    earlyPaymentDiscountAmount?: number;
    journalEntryId?: string;
    notes?: string;
    lineItems: InvoiceLineItem[];
    tenantId: string;
    createdAt: string;
    createdBy?: string;
    financeDimensions?: FinanceSourceDocumentDimension;
}

export interface InvoiceCreateRequest {
    businessPartnerId: string;
    businessPartnerRoleId?: string;
    invoiceDate: string;
    dueDate?: string;
    currencyCode: string;
    exchangeRate?: number;
    exchangeRateId?: string;
    currencyOverrideReason?: string | null;
    paymentTermsDays?: number;
    paymentTermId?: string | null;
    discountAmount?: number;
    discountReason?: string | null;
    taxGroupId?: string | null;
    isOpeningBalance?: boolean;
    notes?: string;
    lineItems: InvoiceLineItemRequest[];
    financeDimensions?: FinanceSourceDocumentDimensionInput;
}

export interface InvoiceUpdateRequest {
    id: string;
    invoiceDate: string;
    dueDate?: string;
    reference?: string;
    notes?: string;
    currencyCode: string;
    exchangeRate?: number;
    exchangeRateId?: string;
    discountAmount?: number;
    discountReason?: string | null;
    taxGroupId?: string | null;
    isOpeningBalance?: boolean;
    lineItems: InvoiceLineItemRequest[];
    financeDimensions?: FinanceSourceDocumentDimensionInput;
}

export interface InvoiceLineItemRequest {
    id?: string;
    lineItemType: 'Product' | 'GLAccount';
    productId?: string;
    glAccountId?: string;
    description: string;
    quantity: number;
    unitPrice: number;
    taxCode?: string;
    taxGroupId?: string | null;
    taxTreatment?: number;
    taxRate?: number;
    discountPercentage?: number;
}

export interface CustomerPayment {
    id: string;
    paymentNumber: string;
    businessPartnerId: string;
    businessPartnerRoleId: string;
    businessPartnerArProfileVersionId: string;
    businessPartnerCode: string;
    customerName: string;
    businessPartnerLegalName?: string;
    businessPartnerTaxIdentificationNumber?: string;
    paymentDate: string;
    totalAmount: number;
    allocatedAmount: number;
    unallocatedAmount: number;
    roundingAdjustmentAmount?: number;
    financeRoundingEvidenceId?: string;
    paymentMethod: string;
    paymentMethodId?: string;
    paymentMethodName?: string;
    bankAccountId?: string;
    bankAccountName?: string;
    liquidityAccountId?: string;
    liquidityAccountName?: string;
    liquidityAccountEntryId?: string;
    referenceNumber?: string;
    transactionReference?: string;
    checkNumber?: string;
    chequeDrawerBank?: string;
    paymentReference?: string;
    amount: number;
    status: 'Draft' | 'Approved' | 'Pending' | 'Posted' | 'Cleared' | 'Reversed' | 'Void' | 'Bounced';
    currencyCode: string;
    exchangeRate: number;
    /** Approved rate-master row frozen when this receipt was recorded. */
    exchangeRateId?: string;
    withholdingTaxId?: string;
    withholdingTaxAccountId?: string;
    withholdingTaxAmount: number;
    vatWithholdingTaxId?: string;
    vatWithholdingAccountId?: string;
    vatWithholdingAmount: number;
    withholdingCertificateNumber?: string;
    withholdingCertificateDate?: string;
    isCreditNote: boolean;
    notes?: string;
    clearedDate?: string;
    journalEntryId?: string;
    reversalJournalEntryId?: string;
    reversalPostingEventId?: string;
    reversalCashTransactionId?: string;
    reversalLiquidityAccountEntryId?: string;
    reversalDate?: string;
    reversedAt?: string;
    reversedById?: string;
    reversalReason?: string;
    receiptIssuance?: ControlledDocumentIssueSummary;
    bouncedDate?: string;
    bouncedReason?: string;
    createdAt: string;
    allocations?: PaymentAllocation[];
    financeDimensions?: FinanceSourceDocumentDimension;
    settlementDimensions?: FinanceSettlementDimensionComponent[];
}

export interface PaymentCreateRequest {
    businessPartnerId: string;
    businessPartnerRoleId?: string;
    paymentDate: string;
    totalAmount: number;
    paymentMethod: string;
    paymentMethodId?: string;
    referenceNumber?: string;
    bankAccountId?: string;
    liquidityAccountId?: string;
    checkNumber?: string;
    chequeDrawerBank?: string;
    transactionReference?: string;
    currencyCode: string;
    exchangeRate?: number;
    /** Optional approved rate selected by the UI; the API resolves the daily rate when omitted. */
    exchangeRateId?: string;
    withholdingTaxId?: string;
    withholdingTaxAccountId?: string;
    withholdingTaxAmount?: number;
    vatWithholdingTaxId?: string;
    vatWithholdingAccountId?: string;
    vatWithholdingAmount?: number;
    withholdingCertificateNumber?: string;
    withholdingCertificateDate?: string;
    notes?: string;
    isCreditNote?: boolean;
    allocations?: InvoiceAllocationRequest[];
    financeDimensions?: FinanceSourceDocumentDimensionInput;
}

export interface PaymentAllocation {
    id: string;
    customerPaymentId: string;
    invoiceId: string;
    invoiceNumber: string;
    allocatedAmount: number;
    /** Amount consumed from the receipt currency; differs from allocatedAmount for FX settlement. */
    paymentCurrencyAmount: number;
    invoiceCurrencyCode: string;
    paymentCurrencyCode: string;
    isCrossCurrency: boolean;
    invoiceSettlementExchangeRateId?: string;
    invoiceSettlementExchangeRate: number;
    paymentExchangeRateId?: string;
    paymentExchangeRate: number;
    paymentFunctionalAmount: number;
    settlementFunctionalAmount: number;
    discountAmount?: number;
    discountFunctionalAmount: number;
    withholdingTaxAmount: number;
    withholdingTaxFunctionalAmount: number;
    vatWithholdingAmount: number;
    vatWithholdingFunctionalAmount: number;
    allocationDate: string;
    notes?: string;
    isReversal: boolean;
    originalAllocationId?: string;
}

export interface ReverseCustomerPaymentRequest {
    reason: string;
    reversalDate?: string;
}

/** Complete Finance evidence for an AR receipt and any linked correction. */
export interface CustomerPaymentTrace {
    payment: CustomerPayment;
    postings: FinancePostingTrace[];
    operationalEntries: FinanceOperationalTrace[];
    auditEvents: FinanceAuditTrace[];
}

export interface FinancePostingTrace {
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
    lines: FinanceJournalLineTrace[];
}

export interface FinanceJournalLineTrace {
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

export interface FinanceOperationalTrace {
    recordType: string;
    recordId: string;
    originalRecordId?: string;
    reference: string;
    status: string;
    recordDate: string;
    amount: number;
    currencyCode: string;
    isReconciled: boolean;
}

export interface FinanceAuditTrace {
    auditLogId: string;
    eventType: string;
    timestamp: string;
    userId: string;
    username: string;
    beforeValuesJson?: string;
    detailsJson?: string;
}

export interface PaymentAllocationRequest {
    customerPaymentId: string;
    allocations: InvoiceAllocationRequest[];
}

export interface InvoiceAllocationRequest {
    invoiceId: string;
    allocatedAmount: number;
    /** Required when the receipt currency and invoice currency differ. */
    paymentCurrencyAmount?: number;
    invoiceSettlementExchangeRateId?: string;
    discountAmount?: number;
    /** Invoice-currency statutory deduction allocated to this invoice. */
    withholdingTaxAmount?: number;
    /** Invoice-currency VAT withholding allocated to this invoice. */
    vatWithholdingAmount?: number;
    notes?: string;
}

export interface PaymentAllocationResultDto {
    paymentId: string;
    allocatedAmount: number;
    remainingUnallocated: number;
    allocations: PaymentAllocation[];
}

export interface AgingBucket {
    bucketName: string;
    bucket?: string;
    amount: number;
    customerCount: number;
    invoiceCount?: number;
    percentage?: number;
}

export interface AgingReport {
    asOfDate: string;
    currencyCode: string;
    usesSettlementReadModel: boolean;
    buckets: AgingBucket[];
    summary: {
        totalCurrent: number;
        totalDays1To30: number;
        totalDays31To60: number;
        totalDays61To90: number;
        totalDays90Plus: number;
        grandTotal: number;
        totalCustomers: number;
        overdueCustomers: number;
    };
    customers: Array<{
        businessPartnerId: string;
        customerCode: string;
        customerName: string;
        current: number;
        days1To30: number;
        days31To60: number;
        days61To90: number;
        days90Plus: number;
        totalOutstanding: number;
    }>;
}

export interface DetailedLedgerCurrencyTotal {
    currencyCode: string;
    openingBalance: number;
    totalDebits: number;
    totalCredits: number;
    closingBalance: number;
}

export interface CustomerDetailedLedgerReport {
    fromDate: string;
    toDate: string;
    currencyCode: string;
    showCustomerCurrency: boolean;
    totalOpeningBalance: number;
    totalDebits: number;
    totalCredits: number;
    totalClosingBalance: number;
    currencyTotals: DetailedLedgerCurrencyTotal[];
    warnings: string[];
    customers: CustomerDetailedLedgerAccount[];
}

export interface CustomerDetailedLedgerAccount {
    businessPartnerId: string;
    customerCode: string;
    customerName: string;
    currencyCode: string;
    openingBalance: number;
    totalDebits: number;
    totalCredits: number;
    closingBalance: number;
    lines: CustomerDetailedLedgerLine[];
}

export interface CustomerDetailedLedgerLine {
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

export interface CollectionsDashboardStats {
    totalOutstanding: number;
    overdueAmount: number;
    collectedThisMonth: number;
    dso: number; // Days Sales Outstanding
}
