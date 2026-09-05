import { describe, expect, it } from 'vitest';

import {
  receiptDocumentKindDisplay,
  receiptDocumentReconciliationDisplay,
  receiptDocumentStatusDisplay,
} from './procurement-receipt-document';

describe('procurement receipt document enum display', () => {
  it.each([
    [0, 'GRN', 'grn'],
    ['0', 'GRN', 'grn'],
    ['Grn', 'GRN', 'grn'],
    [1, 'MRN', 'mrn'],
    ['Mrn', 'MRN', 'mrn'],
  ])('normalizes numeric and API string document kinds', (value, label, testId) => {
    expect(receiptDocumentKindDisplay(value)).toMatchObject({ label, testId });
  });

  it.each([
    ['Draft', 0, 'Draft'],
    ['PendingSignatures', 1, 'Pending signatures'],
    ['Issued', 2, 'Issued'],
    ['Cancelled', 3, 'Cancelled'],
    [2, 2, 'Issued'],
  ])('normalizes numeric and API string document statuses', (value, code, label) => {
    expect(receiptDocumentStatusDisplay(value)).toEqual({ code, label });
  });

  it.each([
    ['Pending', 0, 'Pending'],
    ['Reconciled', 1, 'Reconciled'],
    ['Exception', 2, 'Exception'],
    ['Cancelled', 3, 'Cancelled'],
    [1, 1, 'Reconciled'],
  ])('normalizes numeric and API string reconciliation statuses', (value, code, label) => {
    expect(receiptDocumentReconciliationDisplay(value)).toEqual({ code, label });
  });

  it('renders unknown values safely instead of dereferencing an undefined label', () => {
    expect(receiptDocumentKindDisplay('FutureDocument')).toEqual({
      code: null,
      label: 'FutureDocument',
      testId: 'unknown',
    });
    expect(receiptDocumentStatusDisplay(undefined)).toEqual({ code: null, label: 'Unknown status' });
    expect(receiptDocumentReconciliationDisplay(null)).toEqual({
      code: null,
      label: 'Unknown reconciliation status',
    });
  });
});
