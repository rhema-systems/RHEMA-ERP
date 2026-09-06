import { describe, expect, it } from 'vitest';
import {
  formatProcurementMoney,
  getProcurementBaseCurrency,
  getTenderBidCurrency,
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

  it('uses the matching saved bid currency, not the first bid or tender currency', () => {
    expect(getTenderBidCurrency('bid-2', [
      { id: 'bid-1', currency: 'USD' },
      { id: 'bid-2', currency: ' eur ' },
    ], 'GHS')).toBe('EUR');
  });

  it.each([undefined, '', 'invalid'])('falls back to the saved tender currency for %s', (currency) => {
    expect(getTenderBidCurrency('bid-1', [{ id: 'bid-1', currency }], 'GBP')).toBe('GBP');
  });

  it('uses the shared legacy fallback only when document currency is unavailable', () => {
    expect(getTenderBidCurrency('missing', undefined, 'EUR')).toBe('EUR');
    expect(getTenderBidCurrency('missing', undefined)).toBe('GHS');
  });
});
