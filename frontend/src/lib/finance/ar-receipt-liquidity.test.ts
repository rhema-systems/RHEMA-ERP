import { describe, expect, it } from 'vitest';

import {
  PaymentMethodType,
  type CashierTillSession,
  type LiquidityAccount,
} from '@/types/cash-management';
import { getEligibleReceiptLiquidityAccounts } from './ar-receipt-liquidity';

const account = (
  id: string,
  accountType: LiquidityAccount['accountType']
): LiquidityAccount => ({
  id,
  code: id.toUpperCase(),
  name: id,
  accountType,
  currency: 'GHS',
  glAccountId: `${id}-gl`,
  glAccountNumber: '1100',
  glAccountName: 'Cash control',
  allowsNegativeBalance: false,
  allowsManualAllocations: true,
  isActive: true,
  isSystemAccount: false,
  currentBalance: 0,
  settlementBalance: 0,
  balanceDifference: 0,
  isReconciled: true,
  balanceAuthority: 'PrimaryBookGL',
  availableToSettle: 0,
  openEntryCount: 0,
  rowVersion: 'AQ==',
});

const session = (
  liquidityAccountId: string,
  cashierUserId: string,
  status: CashierTillSession['status'] = 'Open'
) =>
  ({
    liquidityAccountId,
    cashierUserId,
    status,
  }) as CashierTillSession;

describe('getEligibleReceiptLiquidityAccounts', () => {
  const accounts = [
    account('undeposited', 'UndepositedCash'),
    account('my-till', 'CashTill'),
    account('other-till', 'CashTill'),
    account('cheques', 'ChequesAwaitingDeposit'),
  ];

  it('offers Undeposited Cash and only the signed-in cashier open till for cash receipts', () => {
    const result = getEligibleReceiptLiquidityAccounts(
      accounts,
      PaymentMethodType.Cash,
      [session('my-till', 'cashier-1'), session('other-till', 'cashier-2')],
      'cashier-1'
    );

    expect(result.map((item) => item.id)).toEqual(['undeposited', 'my-till']);
  });

  it('does not offer a closed till', () => {
    const result = getEligibleReceiptLiquidityAccounts(
      accounts,
      PaymentMethodType.Cash,
      [session('my-till', 'cashier-1', 'Closed')],
      'cashier-1'
    );

    expect(result.map((item) => item.id)).toEqual(['undeposited']);
  });

  it('keeps non-cash methods restricted to their configured holding type', () => {
    const result = getEligibleReceiptLiquidityAccounts(
      accounts,
      PaymentMethodType.Cheque,
      [session('my-till', 'cashier-1')],
      'cashier-1'
    );

    expect(result.map((item) => item.id)).toEqual(['cheques']);
  });
});
