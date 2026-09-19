import { describe, expect, it } from 'vitest';
import {
  allocateDocumentTradeDiscount,
  calculateLineTradeDiscount,
  calculateNetTradeDiscountLineAmount,
} from './invoice-trade-discount';

const line = (sourceLineId: string, netAmount: number) => ({ sourceLineId, netAmount });

describe('invoice document trade discount allocation', () => {
  it('allocates by net source-line value', () => {
    expect(allocateDocumentTradeDiscount([line('a', 90), line('b', 180)], 27)).toEqual([9, 18]);
  });

  it('assigns the rounding residual deterministically to the final eligible line', () => {
    const allocations = allocateDocumentTradeDiscount(
      [line('a', 1), line('b', 1), line('c', 1)],
      1
    );

    expect(allocations).toEqual([0.33, 0.33, 0.34]);
    expect(allocations.reduce((total, amount) => total + amount, 0)).toBe(1);
  });

  it('does not allocate a document discount to non-positive lines', () => {
    expect(
      allocateDocumentTradeDiscount([line('a', -10), line('b', 0), line('c', 100)], 10)
    ).toEqual([0, 0, 10]);
  });

  it('rounds the line trade discount before deriving its net amount', () => {
    expect(calculateLineTradeDiscount(19.99, 12.5)).toBe(2.5);
    expect(calculateNetTradeDiscountLineAmount(19.99, 12.5)).toBe(17.49);
  });
});
