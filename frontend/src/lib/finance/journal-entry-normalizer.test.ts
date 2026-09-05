import { describe, expect, it } from 'vitest';
import { normalizeJournalEntry } from './journal-entry-normalizer';

describe('normalizeJournalEntry', () => {
  it('preserves Finance source lineage used by journal inquiry filters', () => {
    const normalized = normalizeJournalEntry({
      id: 'journal-1',
      journalNumber: 'PRO-20260829-0001',
      transactionDate: '2026-08-29T00:00:00Z',
      status: 'Posted',
      sourceModule: 'Procurement',
      originModuleCode: 'PROC',
      sourceDocumentId: 'payment-1',
      sourceDocumentType: 'SupplierOnboardingTokenPayment',
      fiscalPeriodId: 'period-1',
      transactions: [],
    });

    expect(normalized.sourceModule).toBe('Procurement');
    expect(normalized.originModuleCode).toBe('PROC');
    expect(normalized.sourceDocumentId).toBe('payment-1');
    expect(normalized.sourceDocumentType).toBe('SupplierOnboardingTokenPayment');
    expect(normalized.fiscalPeriodId).toBe('period-1');
  });
});
