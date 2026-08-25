import { describe, expect, it } from 'vitest';
import type { FinanceDimensionAccountRule } from '@/types/finance';
import {
  getApplicableManualDimensionRules,
  getDefaultManualDimensionValues,
  getMissingRequiredManualDimension,
} from './manual-journal-dimensions';

const rule = (overrides: Partial<FinanceDimensionAccountRule> = {}): FinanceDimensionAccountRule => ({
  id: crypto.randomUUID(), accountId: 'expense', accountNumber: '6000', accountName: 'Expense',
  financeDimensionDefinitionId: 'department', dimensionCode: 'DEPT', dimensionName: 'Department',
  ruleType: 'Required', effectiveDate: '2025-01-01', isActive: true, ...overrides,
});

describe('manual journal dimension rules', () => {
  it('selects the most specific effective manual-journal rule', () => {
    const selected = getApplicableManualDimensionRules([
      rule({ id: 'global', ruleType: 'Optional' }),
      rule({ id: 'manual', sourceModule: 'GL', sourceDocumentType: 'ManualJournalEntry', postingAction: 'Post' }),
      rule({ id: 'future', effectiveDate: '2027-01-01' }),
    ], 'expense', '2026-01-15');
    expect(selected).toHaveLength(1);
    expect(selected[0].id).toBe('manual');
  });

  it('applies defaults and identifies an unresolved required value', () => {
    expect(getDefaultManualDimensionValues([
      rule({ defaultValueCode: 'FIN' }),
    ], 'expense', '2026-01-15')).toEqual({ DEPT: 'FIN' });

    expect(getMissingRequiredManualDimension([
      rule(),
    ], 'expense', '2026-01-15', {})).toMatchObject({ dimensionCode: 'DEPT' });
  });

  it('does not apply a rule scoped to an uncertified producer', () => {
    expect(getApplicableManualDimensionRules([
      rule({ sourceModule: 'AP', sourceDocumentType: 'VendorInvoice', postingAction: 'Post' }),
    ], 'expense', '2026-01-15')).toEqual([]);
  });
});
