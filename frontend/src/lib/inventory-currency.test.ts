import { describe, expect, it } from 'vitest';
import {
  formatInventoryMoney,
  normalizeInventoryCurrency,
} from './inventory-currency';

describe('inventory currency formatting', () => {
  it('uses the supplied tenant currency rather than a hard-coded dollar symbol', () => {
    expect(formatInventoryMoney(180000, 'GHS')).toContain('GHS');
    expect(formatInventoryMoney(180000, 'GHS')).not.toContain('$');
    expect(formatInventoryMoney(180000, 'USD')).toContain('$');
  });

  it('normalizes valid currency codes and fails safely to the configured default', () => {
    expect(normalizeInventoryCurrency(' ghs ')).toBe('GHS');
    expect(normalizeInventoryCurrency('not-a-currency')).toBe('GHS');
  });
});
