export const RECEIVE_PROSPECT_DEPOSIT_PERMISSION =
  'Finance.AR.Payments.Receive';
export const REVERSE_PROSPECT_DEPOSIT_PERMISSION =
  'Finance.AR.Payments.Reverse';

export const getPropertyEnquiryDepositAccess = (
  hasPermission: (permission: string) => boolean
) => ({
  canClear: hasPermission(RECEIVE_PROSPECT_DEPOSIT_PERMISSION),
  canReverse: hasPermission(REVERSE_PROSPECT_DEPOSIT_PERMISSION),
});
