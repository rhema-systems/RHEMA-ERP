import { describe, expect, it } from 'vitest';

import { canRecordArReceipt } from './ar-receipt-eligibility';

describe('canRecordArReceipt', () => {
  it('keeps a posted opening balance collectible for an authorised receipt user', () => {
    expect(
      canRecordArReceipt(
        {
          status: 'Sent',
          balanceAmount: 100_000,
          isOpeningBalance: true,
          journalEntryId: 'journal-1',
        },
        true
      )
    ).toBe(true);
  });

  it('hides receipt processing during approval or without opening posting evidence', () => {
    expect(
      canRecordArReceipt(
        {
          status: 'PendingApproval',
          balanceAmount: 100_000,
          isOpeningBalance: true,
        },
        true
      )
    ).toBe(false);
    expect(
      canRecordArReceipt(
        {
          status: 'Sent',
          balanceAmount: 100_000,
          isOpeningBalance: true,
        },
        true
      )
    ).toBe(false);
  });

  it('hides the action when receipt authority or a positive balance is absent', () => {
    const invoice = {
      status: 'Sent' as const,
      balanceAmount: 100_000,
      isOpeningBalance: false,
    };

    expect(canRecordArReceipt(invoice, false)).toBe(false);
    expect(canRecordArReceipt({ ...invoice, balanceAmount: 0 }, true)).toBe(
      false
    );
  });
});
