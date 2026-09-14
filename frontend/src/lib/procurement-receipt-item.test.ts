import { describe, expect, it } from 'vitest';
import {
  buildReceiptItemCode,
  findNextPendingReceiptItemIndex,
  formatReceiptQuantitySummary,
  hasControlledInventoryItem,
} from './procurement-receipt-item';

describe('receipt quantity labels', () => {
  it('separates the outstanding order quantity from tolerance capacity', () => {
    expect(formatReceiptQuantitySummary(20, 0, 21)).toBe(
      'Ordered: 20 • Previously Received: 0 • Remaining: 20 • Max. incl. tolerance: 21',
    );
  });

  it('deducts previous receipts from the order, not the tolerance limit', () => {
    expect(formatReceiptQuantitySummary(20, 5, 16)).toContain('Remaining: 15 • Max. incl. tolerance: 16');
  });

  it('omits redundant maximum text when there is no extra tolerance capacity', () => {
    expect(formatReceiptQuantitySummary(20, 5, 15)).toBe('Ordered: 20 • Previously Received: 5 • Remaining: 15');
  });

  it('does not show negative outstanding quantities after an allowed over-receipt', () => {
    expect(formatReceiptQuantitySummary(20, 20.5, 0.5)).toContain('Remaining: 0 • Max. incl. tolerance: 0.5');
  });
});

describe('purchase-order receipt item decisions', () => {
  it('treats absent and empty inventory identifiers as missing', () => {
    expect(hasControlledInventoryItem(undefined)).toBe(false);
    expect(hasControlledInventoryItem('')).toBe(false);
    expect(
      hasControlledInventoryItem('00000000-0000-0000-0000-000000000000')
    ).toBe(false);
    expect(
      hasControlledInventoryItem('391c3a85-a628-431f-a50c-1c0333106d4b')
    ).toBe(true);
  });

  it('builds a controlled item-code proposal from the PO and description', () => {
    expect(buildReceiptItemCode('PO-2026-0001', 'Wireless Keyboard')).toBe(
      'RCV-PO-2026-0001-WIRELESS-KEYBOARD'
    );
    expect(buildReceiptItemCode('PO/1', '   ')).toBe('RCV-PO-1-ITEM');
    expect(buildReceiptItemCode('P'.repeat(80), 'I'.repeat(100))).toHaveLength(
      90
    );
  });

  it('advances one unresolved missing line at a time', () => {
    const items = [
      { missingItemDecision: 'create' as const },
      { missingItemDecision: 'pending' as const },
      { missingItemDecision: 'pending' as const },
      { missingItemDecision: 'existing' as const },
    ];

    expect(findNextPendingReceiptItemIndex(items, 0)).toBe(1);
    expect(findNextPendingReceiptItemIndex(items, 1)).toBe(2);
    expect(
      findNextPendingReceiptItemIndex(
        items.map(item => ({
          missingItemDecision:
            item.missingItemDecision === 'pending' ? 'skip' as const : item.missingItemDecision,
        })),
        1
      )
    ).toBeNull();
  });
});
