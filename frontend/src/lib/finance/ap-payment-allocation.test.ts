import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import {
  canSubmitApPayment,
  getApPaymentAllocationState,
} from './ap-payment-allocation';

describe('AP payment allocation completeness', () => {
  const pageSource = readFileSync(
    resolve(process.cwd(), 'src/app/finance/ap/payments/create/page.tsx'),
    'utf8'
  );

  it('blocks a payment with only part of its payment-currency cash allocated', () => {
    const state = getApPaymentAllocationState(1_000, [
      { invoiceCash: 600, paymentCurrencyCash: 600 },
    ]);

    expect(state).toMatchObject({
      disposition: 'partially-allocated',
      remainingPaymentCurrencyCash: 400,
    });
    expect(canSubmitApPayment({ allocationState: state })).toBe(false);
  });

  it('enables a complete allocation when other controls pass', () => {
    const state = getApPaymentAllocationState(1_000, [
      { invoiceCash: 1_000, paymentCurrencyCash: 1_000 },
    ]);

    expect(state).toMatchObject({
      disposition: 'fully-allocated',
      remainingPaymentCurrencyCash: 0,
    });
    expect(canSubmitApPayment({ allocationState: state })).toBe(true);
  });

  it('presents zero allocations as a deliberate supplier advance', () => {
    const state = getApPaymentAllocationState(1_000, []);

    expect(state.disposition).toBe('supplier-advance');
    expect(canSubmitApPayment({ allocationState: state })).toBe(true);
  });

  it('does not let discounts or WHT replace payment-currency cash', () => {
    const state = getApPaymentAllocationState(1_000, [
      {
        invoiceCash: 600,
        paymentCurrencyCash: 600,
        discount: 250,
        withholdingTax: 150,
      },
    ]);

    expect(state.disposition).toBe('partially-allocated');
    expect(state.remainingPaymentCurrencyCash).toBe(400);
    expect(canSubmitApPayment({ allocationState: state })).toBe(false);
  });

  it('blocks an over-allocation and reports its payment-currency excess', () => {
    const state = getApPaymentAllocationState(1_000, [
      { invoiceCash: 1_100, paymentCurrencyCash: 1_100 },
    ]);

    expect(state.disposition).toBe('over-allocated');
    expect(state.remainingPaymentCurrencyCash).toBe(-100);
    expect(canSubmitApPayment({ allocationState: state })).toBe(false);
  });

  it('returns to supplier-advance mode after every component is cleared', () => {
    const state = getApPaymentAllocationState(1_000, [
      {
        invoiceCash: 0,
        paymentCurrencyCash: 0,
        discount: 0,
        withholdingTax: 0,
        otherAdjustments: [0],
      },
    ]);

    expect(state.disposition).toBe('supplier-advance');
    expect(canSubmitApPayment({ allocationState: state })).toBe(true);
  });

  it('clears every stale allocation component when the supplier context changes', () => {
    expect(pageSource).toContain(
      'if (option.businessPartnerId !== selectedSupplierId) clearAllocationState();'
    );
    expect(pageSource).toMatch(
      /const clearAllocationState = \(\) => \{[\s\S]*setAllocations\(\{\}\);[\s\S]*setPaymentCurrencyAllocations\(\{\}\);[\s\S]*setDiscountAllocations\(\{\}\);[\s\S]*setWithholdingAllocations\(\{\}\);/
    );
    expect(pageSource).toContain(
      'if (val !== selectedBankAccountId) clearAllocationState();'
    );
  });

  it('allows Auto Allocate output that reaches a zero remainder', () => {
    const state = getApPaymentAllocationState(1_000, [
      { invoiceCash: 400, paymentCurrencyCash: 400 },
      { invoiceCash: 600, paymentCurrencyCash: 600 },
    ]);

    expect(state.disposition).toBe('fully-allocated');
    expect(canSubmitApPayment({ allocationState: state })).toBe(true);
  });

  it('normalizes at the same two-decimal boundary as the backend', () => {
    const state = getApPaymentAllocationState(1_000, [
      { invoiceCash: 333.333, paymentCurrencyCash: 333.333 },
      { invoiceCash: 666.667, paymentCurrencyCash: 666.667 },
    ]);

    expect(state.disposition).toBe('fully-allocated');
    expect(state.remainingPaymentCurrencyCash).toBe(0);

    const midpoint = getApPaymentAllocationState(1_000, [
      { invoiceCash: 999.995, paymentCurrencyCash: 999.995 },
    ]);
    expect(midpoint.disposition).toBe('fully-allocated');

    const oneCentShort = getApPaymentAllocationState(1_000, [
      { invoiceCash: 999.994, paymentCurrencyCash: 999.994 },
    ]);
    expect(oneCentShort).toMatchObject({
      disposition: 'partially-allocated',
      remainingPaymentCurrencyCash: 0.01,
    });
  });

  it('continues to block a selected invoice that fails payment readiness', () => {
    const state = getApPaymentAllocationState(1_000, [
      { invoiceCash: 1_000, paymentCurrencyCash: 1_000 },
    ]);

    expect(
      canSubmitApPayment({
        allocationState: state,
        hasPaymentReadinessFailure: true,
      })
    ).toBe(false);
  });

  it('continues to block missing FX evidence and incomplete amount pairs', () => {
    const state = getApPaymentAllocationState(1_000, [
      { invoiceCash: 100, paymentCurrencyCash: 1_000 },
    ]);

    expect(
      canSubmitApPayment({
        allocationState: state,
        hasFxEvidenceFailure: true,
      })
    ).toBe(false);
    expect(
      canSubmitApPayment({
        allocationState: state,
        hasIncompleteCurrencyPair: true,
      })
    ).toBe(false);
  });

  it('requires an allocation when applying an existing supplier advance', () => {
    const empty = getApPaymentAllocationState(1_000, []);
    const selected = getApPaymentAllocationState(1_000, [
      { invoiceCash: 600, paymentCurrencyCash: 600 },
    ]);

    expect(
      canSubmitApPayment({
        allocationState: empty,
        isAdvanceApplication: true,
      })
    ).toBe(false);
    expect(
      canSubmitApPayment({
        allocationState: selected,
        isAdvanceApplication: true,
      })
    ).toBe(true);
  });
});
