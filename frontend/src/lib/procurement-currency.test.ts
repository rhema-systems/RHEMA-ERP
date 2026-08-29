import { describe, expect, it } from 'vitest';
import {
  formatProcurementMoney,
  getProcurementBaseCurrency,
  normalizeProcurementCurrency,
} from './procurement-currency';

describe('procurement currency', () => {
  it('selects the active Finance base currency', () => {
    expect(getProcurementBaseCurrency([
      { code: 'USD', isBaseCurrency: false, isActive: true },
      { code: 'GHS', isBaseCurrency: true, isActive: true },
    ])).toBe('GHS');
  });

  it('renders the document currency instead of a hard-coded dollar symbol', () => {
    expect(formatProcurementMoney(4900, 'ghs')).toBe('GHS 4,900.00');
    expect(formatProcurementMoney(12, 'USD')).toBe('USD 12.00');
  });

  it('normalizes ISO currency codes and safely falls back', () => {
    expect(normalizeProcurementCurrency(' eur ')).toBe('EUR');
    expect(normalizeProcurementCurrency('')).toBe('GHS');
  });
});
