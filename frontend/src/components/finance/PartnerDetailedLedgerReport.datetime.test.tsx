import { describe, expect, it } from 'vitest';
import { formatLedgerDateTime, resolveLedgerDisplayDateTime } from './PartnerDetailedLedgerReport';

describe('customer detailed-ledger date/time', () => {
  it('renders the transaction time instead of truncating the ISO value to a date', () => {
    const rendered = formatLedgerDateTime('2026-10-02T07:15:00');

    expect(rendered).toContain('02 Oct 2026');
    expect(rendered).toMatch(/07:15/i);
  });

  it('keeps an invalid legacy value visible', () => {
    expect(formatLedgerDateTime('legacy-date')).toBe('legacy-date');
  });

  it('prefers the authoritative posting timestamp while retaining the business date as fallback', () => {
    expect(resolveLedgerDisplayDateTime({
      transactionDate: '2026-10-01T00:00:00',
      postedAt: '2026-10-02T07:15:00',
    })).toMatch(/02 Oct 2026.*07:15/i);

    expect(resolveLedgerDisplayDateTime({
      transactionDate: '2026-10-01T09:30:00',
      postedAt: null,
    })).toMatch(/01 Oct 2026.*09:30/i);
  });
});
