import type {
  RecurringJournalLineInput,
  RecurringJournalTemplateLine,
} from '@/services/finance/recurring-journal-data.service';

export interface EditableRecurringJournalLine {
  key: string;
  persistedId?: string;
  accountId: string;
  debitAmount: string;
  creditAmount: string;
  description: string;
  dimensionValuesJson: string;
}

export interface RecurringJournalLineSummary {
  totalDebit: number;
  totalCredit: number;
  difference: number;
  isBalanced: boolean;
  isValid: boolean;
  errors: string[];
}

export const formatRecurringJournalAmount = (value: number, currencyCode: string): string => {
  const normalizedCurrency = currencyCode.trim().toUpperCase();
  if (/^[A-Z]{3}$/.test(normalizedCurrency)) {
    try {
      return new Intl.NumberFormat('en-GH', {
        style: 'currency', currency: normalizedCurrency,
      }).format(value);
    } catch {
      // A tenant setting may be loading or invalid. Rendering totals must never crash the form.
    }
  }

  return new Intl.NumberFormat('en-GH', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
};

export const newEditableRecurringJournalLine = (
  key = globalThis.crypto.randomUUID()
): EditableRecurringJournalLine => ({
  key,
  accountId: '',
  debitAmount: '',
  creditAmount: '',
  description: '',
  dimensionValuesJson: '{}',
});

export const editRecurringJournalLine = (
  line: RecurringJournalTemplateLine
): EditableRecurringJournalLine => ({
  key: line.id,
  persistedId: line.id,
  accountId: line.accountId,
  debitAmount: line.isDebit ? String(line.fixedAmount) : '',
  creditAmount: line.isDebit ? '' : String(line.fixedAmount),
  description: line.description ?? '',
  dimensionValuesJson: line.dimensionValuesJson || '{}',
});

const amount = (value: string) => value.trim() === '' ? 0 : Number(value);

export const summarizeRecurringJournalLines = (
  lines: EditableRecurringJournalLine[]
): RecurringJournalLineSummary => {
  const errors: string[] = [];
  if (lines.length < 2) errors.push('At least two journal lines are required.');

  let totalDebit = 0;
  let totalCredit = 0;
  lines.forEach((line, index) => {
    const debit = amount(line.debitAmount);
    const credit = amount(line.creditAmount);
    if (!line.accountId) errors.push(`Line ${index + 1} requires an eligible posting account.`);
    if (!Number.isFinite(debit) || debit < 0 || !Number.isFinite(credit) || credit < 0)
      errors.push(`Line ${index + 1} contains an invalid amount.`);
    else if ((debit > 0) === (credit > 0))
      errors.push(`Line ${index + 1} must contain one positive debit or credit amount, never both.`);
    totalDebit += Number.isFinite(debit) ? debit : 0;
    totalCredit += Number.isFinite(credit) ? credit : 0;
    try { JSON.parse(line.dimensionValuesJson || '{}'); }
    catch { errors.push(`Line ${index + 1} contains invalid dimension evidence.`); }
  });

  totalDebit = Math.round(totalDebit * 100) / 100;
  totalCredit = Math.round(totalCredit * 100) / 100;
  const difference = Math.round((totalDebit - totalCredit) * 100) / 100;
  const isBalanced = totalDebit > 0 && difference === 0;
  if (!isBalanced) errors.push('Total debits must equal total credits.');
  return { totalDebit, totalCredit, difference, isBalanced, isValid: errors.length === 0, errors };
};

export const toRecurringJournalLineInputs = (
  lines: EditableRecurringJournalLine[]
): RecurringJournalLineInput[] => lines.map(line => {
  const debit = amount(line.debitAmount);
  const isDebit = debit > 0;
  return {
    id: line.persistedId,
    accountId: line.accountId,
    isDebit,
    fixedAmount: isDebit ? debit : amount(line.creditAmount),
    description: line.description.trim() || undefined,
    dimensionValuesJson: line.dimensionValuesJson.trim() || '{}',
  };
});
