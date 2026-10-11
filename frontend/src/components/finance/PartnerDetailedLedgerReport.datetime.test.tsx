import { describe, expect, it } from 'vitest';
import {
  formatLedgerBusinessDate,
  formatLedgerDateTime,
  resolveLedgerPostedDateTime,
} from './PartnerDetailedLedgerReport';

describe('customer detailed-ledger date/time', () => {
  it('renders the transaction time instead of truncating the ISO value to a date', () => {
    const rendered = formatLedgerDateTime('2026-10-02T07:15:00');

    expect(rendered).toContain('02 Oct 2026');
    expect(rendered).toMatch(/07:15/i);
  });

  it('keeps an invalid legacy value visible', () => {
    expect(formatLedgerDateTime('legacy-date')).toBe('legacy-date');
  });

  it('renders the business date independently from the posting timestamp', () => {
    expect(formatLedgerBusinessDate('2026-10-01T00:00:00')).toBe('01 Oct 2026');
    expect(resolveLedgerPostedDateTime({
      postedAt: '2026-10-02T07:15:00',
    })).toMatch(/02 Oct 2026.*07:15/i);
  });

  it('does not disguise a missing posting timestamp as the business date', () => {
    expect(resolveLedgerPostedDateTime({
      postedAt: null,
    })).toBe('-');
  });
});
