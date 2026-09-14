/**
 * Tax Module TypeScript Interfaces
 * Defines types for the simplified taxation module
 */

export enum TaxApplicability {
    Sales = 'Sales',
    Purchases = 'Purchases',
    Both = 'Both',
    RuleBased = 'RuleBased'
}

export enum TaxCategory {
    Standard = 'Standard',
    Withholding = 'Withholding',
    VatWithholding = 'VatWithholding',
    Levy = 'Levy',
    Excise = 'Excise'
}

export enum TaxCalculationMethod {
    Simple = 'Simple',
    Compound = 'Compound',
    Threshold = 'Threshold'
}

export enum CompoundBasis {
    BaseOnly = 'BaseOnly',
    Cumulative = 'Cumulative',
    Specific = 'Specific'
}

export interface TaxType {
    id: string;
    tenantId: string;
    code: string;
    name: string;
    description?: string | null;
    rate?: number; // Optional if using calculation method logic outside simple rate
    calculationMethod: TaxCalculationMethod;
    applicability: TaxApplicability;
    category?: TaxCategory;
    isInputTaxDeductible?: boolean;
    thresholdAmount?: number | null;
    compoundOrder?: number | null;
    isSystemDefined?: boolean;
    isActive: boolean;
    isLocked?: boolean;
    effectiveFrom?: string; // ISO Date
    taxPayableAccountId?: string | null;
    taxReceivableAccountId?: string | null;
    createdAt: string;
    updatedAt?: string | null;
}

// Alias for backward compatibility if needed, though TaxType is preferred
export type Tax = TaxType;

export interface TaxConfigurationVersion {
    id: string;
    taxId: string;
    versionNumber: number;
    code: string;
    name: string;
    description?: string | null;
    rate: number;
    effectiveFrom: string;
    applicability: TaxApplicability;
    category: TaxCategory;
    isActive: boolean;
    isInputTaxDeductible: boolean;
    thresholdAmount?: number | null;
    taxPayableAccountId?: string | null;
    taxReceivableAccountId?: string | null;
    validFrom: string;
    validTo?: string | null;
    changeReason?: string | null;
    changedBy?: string | null;
    isCurrent: boolean;
    isLocked: boolean;
}

export interface TaxGroupComponent {
    id: string;
    taxId: string;
    taxCode: string;
    taxName: string;
    taxRate: number;
    taxCategory: TaxCategory;
    calculationOrder: number;
    compoundBasis: CompoundBasis;
    appliesOnTaxCodes?: string[] | null;
}

export interface TaxGroup {
    id: string;
    tenantId: string;
    code: string;
    name: string;
    description?: string | null;
    applicability: TaxApplicability;
    isDefault: boolean;
    isActive: boolean;
    components: TaxGroupComponent[];
    createdAt: string;
    updatedAt?: string | null;
}

export interface TaxCalculationRequest {
    transactionType: string; // SaleOfGoods, etc. - mapped from helper
    baseAmount: number;
    taxGroupId?: string | null;
    manualTaxIds?: string[] | null;
    customerId?: string | null;
    supplierId?: string | null;
    documentId?: string | null;
    documentType?: string | null; // Invoice, Order, etc.
}

export interface TaxBreakdown {
    taxId: string;
    taxCode: string;
    taxName: string;
    taxCategory: TaxCategory;
    taxableAmount: number;
    taxRate: number;
    taxAmount: number;
    compoundBasis: CompoundBasis;
    calculationOrder: number;
    appliedOnTaxCodes?: string[] | null;
    isInputTaxDeductible: boolean;
    isManualOverride: boolean;
}

export interface TaxCalculationResult {
    baseAmount: number;
    taxGroupId?: string | null;
    taxGroupName?: string | null;
    taxBreakdowns: TaxBreakdown[];
    totalTaxAmount: number;
    grandTotal: number;
    effectiveTaxRate: number;
    hasManualOverrides: boolean;
    appliedTaxes: string[]; // List of codes
}

export interface TaxThresholdStatus {
    taxId: string;
    taxName: string;
    entityId: string;
    entityType: string;
    fiscalYear: number;
    cumulativeAmount: number;
    thresholdAmount: number;
    remainingAmount: number;
    isThresholdExceeded: boolean;
    thresholdExceededDate?: string | null;
    percentageUsed: number;
}

export interface TransactionWithTax {
    id: string;
    date: string;
    reference: string;
    partnerName: string;
    description: string;
    baseAmount: number;
    totalTax: number;
    totalAmount: number;
    taxCalculations: {
        taxTypeCode: string;
        taxAmount: number;
    }[];
}

export interface WHTSummaryEntry {
    supplierName: string;
    supplierTIN?: string;
    taxType: string;
    transactionCount: number;
    grossAmount: number;
    whtRate: number;
    whtAmount: number;
    netAmount: number;
}

export interface WhtCertificate {
    certificateId?: string | null;
    vendorPaymentId: string;
    paymentNumber: string;
    paymentStatus: string;
    supplierId: string;
    supplierName: string;
    supplierTin?: string | null;
    paymentDate: string;
    currencyCode: string;
    taxId?: string | null;
    taxCode?: string | null;
    taxName?: string | null;
    taxRate: number;
    taxableBase: number;
    withholdingAmount: number;
    netPaidAmount: number;
    taxAccountNumber?: string | null;
    taxAccountName?: string | null;
    certificateNumber?: string | null;
    certificateDate?: string | null;
    certificateStatus: 'Missing' | 'Issued' | 'Superseded' | 'Cancelled' | string;
    versionNumber: number;
    issuedAtUtc?: string | null;
    issuedByName?: string | null;
    lifecycleReason?: string | null;
    cancelledAtUtc?: string | null;
    cancellationReason?: string | null;
    journalEntryId?: string | null;
    remittanceId?: string | null;
    remittanceNumber?: string | null;
    remittanceStatus: string;
    versions: WhtCertificateVersion[];
}

export interface WhtCertificateVersion {
    certificateId: string;
    certificateNumber: string;
    versionNumber: number;
    status: string;
    issueDate: string;
    issuedAtUtc: string;
    issuedByName: string;
    lifecycleReason?: string | null;
    cancelledAtUtc?: string | null;
    cancellationReason?: string | null;
}

export interface WhtCertificateQuery {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    supplierId?: string;
    fromDate?: string;
    toDate?: string;
    status?: string;
}

export interface GenerateWhtCertificateDto {
    certificateNumber?: string;
    certificateDate?: string;
}

export interface WhtCalculationRequest {
    taxId: string;
    supplierId: string;
    paymentDate: string;
    taxableBase: number;
    excludeVendorPaymentId?: string;
    vendorInvoiceIds?: string[];
}

export interface WhtCalculationResult {
    taxId: string;
    taxCode: string;
    taxName: string;
    taxRate: number;
    taxableBase: number;
    cumulativeBefore: number;
    cumulativeAfter: number;
    thresholdAmount?: number | null;
    remainingBeforeThreshold: number;
    thresholdApplied: boolean;
    withholdingAmount: number;
    taxPayableAccountId?: string | null;
    calculationNote: string;
}

export interface WhtRemittanceLiability {
    vendorPaymentId: string;
    paymentNumber: string;
    paymentDate: string;
    supplierId: string;
    supplierName: string;
    supplierTin?: string | null;
    currencyCode: string;
    taxCode?: string | null;
    taxableBase: number;
    withholdingAmount: number;
    certificateId?: string | null;
    certificateNumber?: string | null;
    journalEntryId?: string | null;
}

export interface WhtRemittance {
    id: string;
    remittanceNumber: string;
    periodFrom: string;
    periodTo: string;
    dueDate: string;
    currencyCode: string;
    status: 'Draft' | 'Submitted' | 'Paid' | 'Cancelled' | string;
    totalWithholdingAmount: number;
    lineCount: number;
    submissionReference?: string | null;
    submittedAtUtc?: string | null;
    submittedByName?: string | null;
    paymentReference?: string | null;
    paymentDate?: string | null;
    authorityReceiptReference?: string | null;
    paidAtUtc?: string | null;
    paidByName?: string | null;
    notes?: string | null;
    cancelledAtUtc?: string | null;
    cancellationReason?: string | null;
    lines: WhtRemittanceLine[];
}

export interface WhtRemittanceLine {
    id: string;
    vendorPaymentId: string;
    certificateId?: string | null;
    paymentNumber: string;
    paymentDate: string;
    supplierId: string;
    supplierName: string;
    supplierTin?: string | null;
    taxCode?: string | null;
    taxableBase: number;
    withholdingAmount: number;
    journalEntryId?: string | null;
}

export interface FinancePagedResult<T> {
    items: T[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
    hasPrevious: boolean;
    hasNext: boolean;
}

export interface VATReconciliation {
    period: string;
    outputVAT: number;
    inputVAT: number;
    netVATPayable: number;
    outputNHIL: number;
    inputNHIL: number;
    netNHILPayable: number;
    outputGETFL: number;
    inputGETFL: number;
    netGETFLPayable: number;
    totalPayable: number;
}


// DTOs for creating/updating
export interface CreateTaxTypeDto {
    code: string;
    name: string;
    description?: string;
    rate?: number;
    calculationMethod: TaxCalculationMethod;
    applicability: TaxApplicability;
    category?: TaxCategory;
    isInputTaxDeductible?: boolean;
    thresholdAmount?: number | null;
    compoundOrder?: number;
    isActive?: boolean;
    effectiveFrom?: string;
    taxPayableAccountId?: string | null;
    taxReceivableAccountId?: string | null;
}

// Alias
export type CreateTaxDto = CreateTaxTypeDto;

export interface UpdateTaxTypeDto {
    name?: string;
    description?: string;
    rate?: number;
    calculationMethod?: TaxCalculationMethod;
    applicability?: TaxApplicability;
    category?: TaxCategory;
    isInputTaxDeductible?: boolean;
    thresholdAmount?: number | null;
    compoundOrder?: number;
    isActive?: boolean;
    effectiveFrom?: string;
    changeReason?: string;
    taxPayableAccountId?: string | null;
    taxReceivableAccountId?: string | null;
    clearTaxPayableAccount?: boolean;
    clearTaxReceivableAccount?: boolean;
}

// Alias
export type UpdateTaxDto = UpdateTaxTypeDto;

export interface CreateTaxGroupDto {
    code: string;
    name: string;
    description?: string;
    applicability: TaxApplicability;
    isDefault?: boolean;
    isActive?: boolean;
}

export interface UpdateTaxGroupDto {
    name?: string;
    description?: string;
    applicability?: TaxApplicability;
    isDefault?: boolean;
    isActive?: boolean;
}

export interface CreateTaxGroupComponentDto {
    taxId: string;
    calculationOrder: number;
    compoundBasis: CompoundBasis;
    appliesOnTaxCodes?: string[] | null;
}

export interface UpdateTaxGroupComponentDto {
    calculationOrder?: number;
    compoundBasis?: CompoundBasis;
    appliesOnTaxCodes?: string[] | null;
}

// ===== TAX RULES =====

export interface TaxRule {
    id: string;
    name: string;
    description?: string | null;
    priority: number;
    taxGroupId: string;
    taxGroupName: string;
    transactionType?: string | null;
    productCategoryId?: string | null;
    productCategoryName?: string | null;
    customerType?: string | null;
    serviceType?: string | null;
    isActive: boolean;
}

export interface CreateTaxRuleDto {
    name: string;
    description?: string;
    priority: number;
    taxGroupId: string;
    transactionType?: string;
    productCategoryId?: string;
    customerType?: string;
    serviceType?: string;
    isActive: boolean;
}

export interface UpdateTaxRuleDto extends CreateTaxRuleDto { }
