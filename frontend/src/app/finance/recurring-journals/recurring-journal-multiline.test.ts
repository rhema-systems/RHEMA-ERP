import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import {
  editRecurringJournalLine,
  newEditableRecurringJournalLine,
  summarizeRecurringJournalLines,
  toRecurringJournalLineInputs,
  type EditableRecurringJournalLine,
} from './recurring-journal-lines';

const line = (patch: Partial<EditableRecurringJournalLine>): EditableRecurringJournalLine => ({
  key: crypto.randomUUID(), accountId: crypto.randomUUID(), debitAmount: '', creditAmount: '',
  description: '', dimensionValuesJson: '{}', ...patch,
});

describe('Recurring journal multiline editor', () => {
  it('accepts any balanced combination of debit and credit lines and reports totals', () => {
    const summary = summarizeRecurringJournalLines([
      line({ debitAmount: '600.25' }), line({ debitAmount: '399.75' }),
      line({ creditAmount: '750' }), line({ creditAmount: '250' }),
    ]);

    expect(summary).toEqual({
      totalDebit: 1000, totalCredit: 1000, difference: 0,
      isBalanced: true, isValid: true, errors: [],
    });
  });

  it('rejects too few lines, missing accounts, zero lines, both-sided lines and imbalance', () => {
    const summary = summarizeRecurringJournalLines([
      line({ accountId: '', debitAmount: '100', creditAmount: '50' }),
    ]);

    expect(summary.isValid).toBe(false);
    expect(summary.errors.join(' ')).toContain('At least two');
    expect(summary.errors.join(' ')).toContain('eligible posting account');
    expect(summary.errors.join(' ')).toContain('never both');
    expect(summary.errors.join(' ')).toContain('Total debits must equal');
  });

  it('retains persisted line identity while new rows use stable client keys', () => {
    const persistedId = crypto.randomUUID();
    const edited = editRecurringJournalLine({
      id: persistedId, lineNumber: 1, accountId: crypto.randomUUID(), accountCode: '6100',
      accountName: 'Expense', isDebit: true, fixedAmount: 125, description: 'Accrual',
      dimensionValuesJson: '{"DEPT":"FIN"}',
    });
    const added = newEditableRecurringJournalLine();
    const inputs = toRecurringJournalLineInputs([edited, { ...added, accountId: crypto.randomUUID(), creditAmount: '125' }]);

    expect(inputs[0].id).toBe(persistedId);
    expect(inputs[0].dimensionValuesJson).toBe('{"DEPT":"FIN"}');
    expect(inputs[1].id).toBeUndefined();
    expect(added.key).toBeTruthy();
  });

  it('renders add/remove controls, balance evidence and disabled invalid submission', () => {
    const grid = readFileSync(resolve(process.cwd(), 'src/app/finance/recurring-journals/recurring-journal-line-grid.tsx'), 'utf8');
    const create = readFileSync(resolve(process.cwd(), 'src/app/finance/recurring-journals/new/page.tsx'), 'utf8');
    expect(grid).toContain('Add line');
    expect(grid).toContain('Remove line');
    expect(grid).toContain('Total debit');
    expect(grid).toContain('Total credit');
    expect(grid).toContain('Difference');
    expect(create).toContain('disabled={saving || !canSubmit}');
  });
});
