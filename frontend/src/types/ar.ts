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
    paymentTermsDays: number;
    paymentTermId?: string | null;
    priceGroup?: string;
    currencyCode: string;
    lastPaymentDate?: string;
    overdueAmount?: number;
    isActive?: boolean;
    notes?: string;
    status: 'Active' | 'Inactive' | 'OnHold';
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
    taxCode?: string;
    unit?: string;
    discountPercentage: number;
    discountAmount: number;
    lineTotal: number;
}

export interface Invoice {
    id: string;
    invoiceNumber: string;
    customerId: string;
    customerName: string;
    invoiceDate: string;
    dueDate: string;
    totalAmount: number;
    paidAmount: number;
    balanceAmount: number;
    status: 'Draft' | 'Sent' | 'Posted' | 'Paid' | 'Void' | 'Overdue';
    currencyCode: string;
    exchangeRate: number;
    paymentTermsDays: number;
    paymentTermId?: string | null;
    discountAmount: number;
    isOpeningBalance: boolean;
    notes?: string;
    lineItems: InvoiceLineItem[];
    tenantId: string;
    createdAt: string;
    createdBy?: string;
}

export interface InvoiceCreateRequest {
    customerId: string;
    invoiceDate: string;
    dueDate?: string;
    currencyCode: string;
    exchangeRate?: number;
    paymentTermsDays?: number;
    paymentTermId?: string | null;
    discountAmount?: number;
    taxGroupId?: string | null;
    isOpeningBalance?: boolean;
    notes?: string;
    lineItems: InvoiceLineItemRequest[];
}

export interface InvoiceLineItemRequest {
    lineItemType: 'Product' | 'GLAccount';
    productId?: string;
    glAccountId?: string;
    description: string;
    quantity: number;
    unitPrice: number;
    taxCode?: string;
    taxGroupId?: string | null;
    taxRate?: number;
    discountPercentage?: number;
}

export interface CustomerPayment {
    id: string;
    paymentNumber: string;
    customerId: string;
    customerName: string;
    paymentDate: string;
    totalAmount: number;
    allocatedAmount: number;
    unallocatedAmount: number;
    paymentMethod: string;
    paymentMethodId?: string;
    paymentMethodName?: string;
    referenceNumber?: string;
    paymentReference: string;
    amount: number;
    status: 'Draft' | 'Posted' | 'Void' | 'Bounced';
    currencyCode: string;
    exchangeRate: number;
    isCreditNote: boolean;
    notes?: string;
    clearedDate?: string;
    bouncedDate?: string;
    bouncedReason?: string;
    createdAt: string;
    allocations?: PaymentAllocation[];
}

export interface PaymentCreateRequest {
    customerId: string;
    paymentDate: string;
    totalAmount: number;
    paymentMethod: string;
    paymentMethodId?: string;
    referenceNumber?: string;
    bankAccountId?: string;
    checkNumber?: string;
    transactionReference?: string;
    currencyCode: string;
    exchangeRate?: number;
    notes?: string;
    isCreditNote?: boolean;
    allocations?: InvoiceAllocationRequest[];
}

export interface PaymentAllocation {
    id: string;
    customerPaymentId: string;
    invoiceId: string;
    invoiceNumber: string;
    allocatedAmount: number;
    discountAmount?: number;
    allocationDate: string;
    notes?: string;
}

export interface PaymentAllocationRequest {
    customerPaymentId: string;
    allocations: InvoiceAllocationRequest[];
}

export interface InvoiceAllocationRequest {
    invoiceId: string;
    allocatedAmount: number;
    discountAmount?: number;
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
    buckets: AgingBucket[];
    totalOutstanding: number;
    customerDetails?: any[]; // Simplified for summary view
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
    warnings: string[];
    customers: CustomerDetailedLedgerAccount[];
}

export interface CustomerDetailedLedgerAccount {
    customerId: string;
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
