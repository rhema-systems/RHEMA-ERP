import {
  PaymentMethodType,
  type CashierTillSession,
  type LiquidityAccount,
  type LiquidityAccountType,
} from '@/types/cash-management';

export const liquidityTypeForPaymentMethod = (
  type?: PaymentMethodType
): LiquidityAccountType => {
  switch (type) {
    case PaymentMethodType.Cheque:
      return 'ChequesAwaitingDeposit';
    case PaymentMethodType.Card:
      return 'CardSettlementClearing';
    case PaymentMethodType.MobileMoney:
      return 'MobileMoneyClearing';
    case PaymentMethodType.Cash:
    default:
      return 'UndepositedCash';
  }
};

export const getEligibleReceiptLiquidityAccounts = (
  accounts: LiquidityAccount[] | undefined,
  paymentMethodType: PaymentMethodType | undefined,
  openTillSessions: CashierTillSession[] | undefined,
  currentUserId: string | undefined
): LiquidityAccount[] => {
  const expectedType = liquidityTypeForPaymentMethod(paymentMethodType);
  const openCashierTillIds = new Set(
    (openTillSessions ?? [])
      .filter(
        (session) =>
          session.status === 'Open' && session.cashierUserId === currentUserId
      )
      .map((session) => session.liquidityAccountId)
  );

  return (accounts ?? []).filter(
    (account) =>
      account.isActive &&
      (account.accountType === expectedType ||
        (paymentMethodType === PaymentMethodType.Cash &&
          account.accountType === 'CashTill' &&
          openCashierTillIds.has(account.id)))
  );
};
