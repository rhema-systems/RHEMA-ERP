import { describe, expect, it } from 'vitest';
import { formatCrmAmount, formatCrmCurrencyTotals, formatCrmMissingCurrencyCount } from './crmMoney';

describe('CRM money display', () => {
  it('uses each recorded currency without assigning dollars to mixed totals', () => {
    const result = formatCrmCurrencyTotals([
      { currency: 'GHS', amount: 120000 },
      { currency: 'USD', amount: 5000 },
    ]);

    expect(result).toContain('GHS');
    expect(result).toContain('USD');
    expect(result).toContain('120,000');
    expect(result).toContain('5,000');
  });

  it('makes missing currency explicit instead of assuming USD', () => {
    expect(formatCrmAmount(55000, undefined)).toBe('55,000 (currency not recorded)');
    expect(formatCrmCurrencyTotals([])).toBe('—');
    expect(formatCrmMissingCurrencyCount(2)).toBe('2 records without currency omitted');
  });
});
