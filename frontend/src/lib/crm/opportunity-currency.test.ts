import { describe, expect, it } from 'vitest';
import {
  buildOpportunityCurrencyOptions,
  resolveOpportunityCurrency,
} from './opportunity-currency';

const currencies = [
  { code: 'USD', name: 'US Dollar' },
  { code: 'GHS', name: 'Ghana Cedi', isBaseCurrency: true },
];

describe('opportunity currency controls', () => {
  it('prefers the linked property enquiry currency', () => {
    expect(resolveOpportunityCurrency(' usd ', currencies)).toBe('USD');
  });

  it('uses the configured base currency when no source currency exists', () => {
    expect(resolveOpportunityCurrency(undefined, currencies)).toBe('GHS');
  });

  it('keeps a linked currency visible when it is absent from the active list', () => {
    expect(buildOpportunityCurrencyOptions(currencies, 'eur')).toEqual([
      currencies[0],
      currencies[1],
      { code: 'EUR', name: 'Linked property currency' },
    ]);
  });
});
