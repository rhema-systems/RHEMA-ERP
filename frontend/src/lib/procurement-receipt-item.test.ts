import { describe, expect, it } from 'vitest';
import {
  buildReceiptItemCode,
  findNextPendingReceiptItemIndex,
  hasControlledInventoryItem,
} from './procurement-receipt-item';

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
