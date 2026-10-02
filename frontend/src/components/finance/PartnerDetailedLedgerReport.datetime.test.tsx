import { describe, expect, it } from 'vitest';
import { formatLedgerDateTime } from './PartnerDetailedLedgerReport';

describe('customer detailed-ledger date/time', () => {
  it('renders the transaction time instead of truncating the ISO value to a date', () => {
    const rendered = formatLedgerDateTime('2026-10-02T07:15:00');

    expect(rendered).toContain('02 Oct 2026');
    expect(rendered).toMatch(/07:15/i);
  });

  it('keeps an invalid legacy value visible', () => {
    expect(formatLedgerDateTime('legacy-date')).toBe('legacy-date');
  });
});
