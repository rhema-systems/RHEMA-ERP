import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { z } from 'zod';

const pageSource = readFileSync(
  resolve(process.cwd(), 'src/app/finance/ar/payments/new/page.tsx'),
  'utf8',
);

describe('AR receipt destination contract', () => {
  it('accepts configured .NET GUIDs that are not RFC-versioned UUIDs', () => {
    const configuredRecordId = z.guid();

    expect(configuredRecordId.safeParse('00000009-0001-0000-0000-000000000001').success).toBe(true);
    expect(configuredRecordId.safeParse('00000007-1024-0000-0000-000000000001').success).toBe(true);
    expect(z.string().uuid().safeParse('00000007-1024-0000-0000-000000000001').success).toBe(false);
  });

  it('does not serialize empty or competing destination GUIDs', () => {
    expect(pageSource).toContain("value === '' || value == null ? undefined : value");
    expect(pageSource).toContain("z.guid('Select a valid configured record').optional()");
    expect(pageSource).not.toContain("z.string().uuid('Select a valid configured record').optional()");
    expect(pageSource).toContain('bankAccountId: directBankReceipt ? data.bankAccountId : undefined');
    expect(pageSource).toContain('liquidityAccountId: directBankReceipt ? undefined : data.liquidityAccountId');
  });

  it('keeps asynchronous receipt selects controlled for their full lifetime', () => {
    expect(pageSource).toContain("value={form.watch('businessPartnerId') ?? ''}");
    expect(pageSource).toContain("value={form.watch('paymentMethodId') ?? ''}");
    expect(pageSource).toContain("value={form.watch('bankAccountId') ?? ''}");
    expect(pageSource).toContain("value={form.watch('liquidityAccountId') ?? ''}");
  });

  it('places the payment method before the destination account it controls', () => {
    const paymentMethod = pageSource.indexOf('<Label htmlFor="paymentMethod">Payment Method</Label>');
    const bankDestination = pageSource.indexOf('<Label htmlFor="bankAccount">Deposit To Bank Account</Label>');
    const holdingDestination = pageSource.indexOf('<Label htmlFor="liquidityAccount">Receive Into Holding Account</Label>');

    expect(paymentMethod).toBeGreaterThan(-1);
    expect(paymentMethod).toBeLessThan(bankDestination);
    expect(paymentMethod).toBeLessThan(holdingDestination);
  });

  it('labels the displayed invoice date as the due date', () => {
    expect(pageSource).toContain('<th className="p-3 text-left">Due date</th>');
    expect(pageSource).toContain("inv.dueDate ? format(new Date(inv.dueDate), 'MMM dd, yyyy') : '-'");
    expect(pageSource).not.toContain('<th className="p-3 text-left">Date</th>');
  });

  it('separates recording new money from applying an existing on-account advance', () => {
    expect(pageSource).toContain("type ReceiptMode = 'new-receipt' | 'apply-account'");
    expect(pageSource).toContain('Apply payment on account');
    expect(pageSource).toContain('<CustomerAdvanceSelector');
    expect(pageSource).toContain('customerPaymentId: existingAdvancePaymentId');
    expect(pageSource).toContain("router.push(`/finance/ar/receipts/${existingAdvancePaymentId}`)");
  });
});
