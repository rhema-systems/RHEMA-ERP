import { describe, expect, it } from 'vitest';
import {
  getPropertyEnquiryDepositAccess,
  RECEIVE_PROSPECT_DEPOSIT_PERMISSION,
  REVERSE_PROSPECT_DEPOSIT_PERMISSION,
} from './property-enquiry-deposit-access';

describe('property enquiry deposit access', () => {
  it('exposes clearance and reversal independently from their Finance permissions', () => {
    const clearOnly = getPropertyEnquiryDepositAccess(
      (permission) => permission === RECEIVE_PROSPECT_DEPOSIT_PERMISSION
    );
    const reverseOnly = getPropertyEnquiryDepositAccess(
      (permission) => permission === REVERSE_PROSPECT_DEPOSIT_PERMISSION
    );

    expect(clearOnly).toEqual({ canClear: true, canReverse: false });
    expect(reverseOnly).toEqual({ canClear: false, canReverse: true });
  });

  it('hides both controlled actions when neither permission is granted', () => {
    expect(getPropertyEnquiryDepositAccess(() => false)).toEqual({
      canClear: false,
      canReverse: false,
    });
  });
});
