import { describe, expect, it } from 'vitest';
import { getQuoteSubmitLabel, isQuoteSubmissionOpen } from './rfq-quote-state';

describe('RFQ quote revision state', () => {
  const now = new Date('2026-08-25T12:00:00Z');

  it('allows a sent RFQ before its deadline', () => {
    expect(isQuoteSubmissionOpen('Sent', '2026-08-25T12:01:00Z', now)).toBe(true);
  });

  it('closes submission at the deadline', () => {
    expect(isQuoteSubmissionOpen('Sent', '2026-08-25T12:00:00Z', now)).toBe(false);
  });

  it('labels a pre-deadline resubmission as an update', () => {
    expect(getQuoteSubmitLabel(true, true)).toBe('Update & Resubmit');
  });
});
