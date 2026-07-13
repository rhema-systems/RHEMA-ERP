export type DocumentNumberingModule = 'Finance' | 'Sales';

export type DocumentSequenceResetPolicy = 'Never' | 'Yearly' | 'Monthly';

export const FinanceDocumentTypes = {
  JournalEntry: 'JournalEntry',
  UnitJournalEntry: 'UnitJournalEntry',
  // These must match the backend FinanceDocumentTypes constants in IDocumentNumberingService.cs;
  // getDefinition matches documentType exactly, so a renamed constant silently loses the tenant sequence.
  FinancePurchaseOrder: 'FinancePurchaseOrder',
  FinancePurchaseOrderReceipt: 'FinancePurchaseOrderReceipt',
  APSupplierReturn: 'APSupplierReturn',
  APInvoice: 'APInvoice',
  APPayment: 'APPayment',
  APPaymentBatch: 'APPaymentBatch',
  ARInvoice: 'ARInvoice',
  ARPayment: 'ARPayment',
  ARCreditNote: 'ARCreditNote',
  CashReceipt: 'CashReceipt',
  CashPayment: 'CashPayment',
  BankTransfer: 'BankTransfer',
  AssetTransfer: 'AssetTransfer',
  AssetDisposal: 'AssetDisposal',
  AssetVerification: 'AssetVerification',
  FixedAssetJournal: 'FixedAssetJournal',
  LeaseJournal: 'LeaseJournal',
  CurrencyRevaluation: 'CurrencyRevaluation',
  YearEndClose: 'YearEndClose',
} as const;

export const SalesDocumentTypes = {
  Quote: 'Quote',
  SalesOrder: 'SalesOrder',
  DeliveryNote: 'DeliveryNote',
  SalesAgreement: 'SalesAgreement',
  ReturnOrder: 'ReturnOrder',
  CreditNote: 'CreditNote',
  Refund: 'Refund',
  CommissionStatement: 'CommissionStatement',
} as const;

export interface DocumentSequenceDefinition {
  id: string;
  tenantId: string;
  module: DocumentNumberingModule | string;
  documentType: string;
  name: string;
  format: string;
  nextNumber: number;
  startNumber: number;
  minimumDigits: number;
  resetPolicy: DocumentSequenceResetPolicy | string;
  lastResetPeriodKey?: string | null;
  isContinuous: boolean;
  allowManualEntry: boolean;
  isActive: boolean;
  isDefault: boolean;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  description?: string | null;
}

export interface UpdateDocumentSequenceDefinition {
  name?: string;
  format?: string;
  nextNumber?: number;
  startNumber?: number;
  minimumDigits?: number;
  resetPolicy?: DocumentSequenceResetPolicy | string;
  isContinuous?: boolean;
  allowManualEntry?: boolean;
  isActive?: boolean;
  isDefault?: boolean;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  description?: string | null;
}

export interface GenerateDocumentNumberRequest {
  module: string;
  documentType: string;
  tenantId?: string;
  documentDate?: string;
  entityType?: string;
  entityId?: string;
}

export interface GeneratedDocumentNumber {
  documentNumber: string;
}
