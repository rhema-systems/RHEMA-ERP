import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const pageSource = readFileSync(
  resolve(process.cwd(), 'src/app/finance/ar/payments/new/page.tsx'),
  'utf8',
);

describe('AR receipt destination contract', () => {
  it('does not serialize empty or competing destination GUIDs', () => {
    expect(pageSource).toContain("value === '' || value == null ? undefined : value");
    expect(pageSource).toContain('bankAccountId: directBankReceipt ? data.bankAccountId : undefined');
    expect(pageSource).toContain('liquidityAccountId: directBankReceipt ? undefined : data.liquidityAccountId');
  });

  it('places the payment method before the destination account it controls', () => {
    const paymentMethod = pageSource.indexOf('<Label htmlFor="paymentMethod">Payment Method</Label>');
    const bankDestination = pageSource.indexOf('<Label htmlFor="bankAccount">Deposit To Bank Account</Label>');
    const holdingDestination = pageSource.indexOf('<Label htmlFor="liquidityAccount">Receive Into Holding Account</Label>');

    expect(paymentMethod).toBeGreaterThan(-1);
    expect(paymentMethod).toBeLessThan(bankDestination);
    expect(paymentMethod).toBeLessThan(holdingDestination);
  });
});
