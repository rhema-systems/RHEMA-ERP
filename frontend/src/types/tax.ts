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
    effectiveFrom?: string; // ISO Date
    taxPayableAccountId?: string | null;
    taxReceivableAccountId?: string | null;
    createdAt: string;
    updatedAt?: string | null;
}

// Alias for backward compatibility if needed, though TaxType is preferred
export type Tax = TaxType;

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
    taxPayableAccountId?: string | null;
    taxReceivableAccountId?: string | null;
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
