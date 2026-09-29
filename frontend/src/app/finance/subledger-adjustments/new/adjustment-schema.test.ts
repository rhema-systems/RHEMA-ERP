import { describe, expect, it } from 'vitest';
import { adjustmentSchema } from './adjustment-schema';

const request = {
  module: 'AR', businessPartnerId: 'customer', adjustmentDate: '2026-09-27',
  adjustmentType: 'Debit', amount: 10, currencyCode: 'GHS', exchangeRate: 1,
  reason: 'Customer balance adjustment',
};

describe('subledger adjustment account selection', () => {
  it.each(['StandardAdjustment', 'FinanceCharge', 'Writeoff', 'OverpaymentWriteoff'])(
    '%s requires an explicit contra account before submission', purpose => {
      for (const contraAccountId of ['', '   ']) {
        const result = adjustmentSchema.safeParse({ ...request, purpose, contraAccountId });
        expect(result.success).toBe(false);
        if (!result.success) expect(result.error.issues).toContainEqual(expect.objectContaining({
          path: ['contraAccountId'], message: 'Contra account is required',
        }));
      }
      expect(adjustmentSchema.safeParse({ ...request, purpose,
        contraAccountId: '909bf9c3-c9b8-472d-a99f-8f777a308b0a' }).success).toBe(true);
    },
  );
});
