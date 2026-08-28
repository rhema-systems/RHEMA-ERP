export interface ReceiptDocumentEnumDisplay {
  code: number | null;
  label: string;
}

export interface ReceiptDocumentKindDisplay extends ReceiptDocumentEnumDisplay {
  testId: string;
}

const normalizeEnumValue = (value: unknown): string =>
  String(value ?? '')
    .trim()
    .replace(/[\s_-]+/g, '')
    .toLowerCase();

const fallbackLabel = (value: unknown, fallback: string): string => {
  const text = String(value ?? '').trim();
  return text || fallback;
};

export const receiptDocumentKindDisplay = (value: unknown): ReceiptDocumentKindDisplay => {
  switch (normalizeEnumValue(value)) {
    case '0':
    case 'grn':
    case 'goodsreceiptnote':
      return { code: 0, label: 'GRN', testId: 'grn' };
    case '1':
    case 'mrn':
    case 'materialreceiptnote':
    case 'materialsreceiptnote':
      return { code: 1, label: 'MRN', testId: 'mrn' };
    default:
      return { code: null, label: fallbackLabel(value, 'Receipt document'), testId: 'unknown' };
  }
};

export const receiptDocumentStatusDisplay = (value: unknown): ReceiptDocumentEnumDisplay => {
  switch (normalizeEnumValue(value)) {
    case '0':
    case 'draft':
      return { code: 0, label: 'Draft' };
    case '1':
    case 'pendingsignatures':
      return { code: 1, label: 'Pending signatures' };
    case '2':
    case 'issued':
      return { code: 2, label: 'Issued' };
    case '3':
    case 'cancelled':
    case 'canceled':
      return { code: 3, label: 'Cancelled' };
    default:
      return { code: null, label: fallbackLabel(value, 'Unknown status') };
  }
};

export const receiptDocumentReconciliationDisplay = (value: unknown): ReceiptDocumentEnumDisplay => {
  switch (normalizeEnumValue(value)) {
    case '0':
    case 'pending':
      return { code: 0, label: 'Pending' };
    case '1':
    case 'reconciled':
      return { code: 1, label: 'Reconciled' };
    case '2':
    case 'exception':
      return { code: 2, label: 'Exception' };
    case '3':
    case 'cancelled':
    case 'canceled':
      return { code: 3, label: 'Cancelled' };
    default:
      return { code: null, label: fallbackLabel(value, 'Unknown reconciliation status') };
  }
};
