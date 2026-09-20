import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { ReceiptSourceControlCard } from './ReceiptSourceControlCard';

describe('ReceiptSourceControlCard', () => {
  it('shows authoritative capacity and the full decision register', () => {
    const markup = renderToStaticMarkup(
      <ReceiptSourceControlCard
        loading={false}
        readiness={{
          purchaseOrderId: 'po-0501',
          orderNumber: 'PO-0501',
          purchaseOrderStatus: 'Partially Received',
          sourceReference: 'CON-0501',
          sourceIntegrityHash: 'A'.repeat(64),
          tolerancePercent: 5,
          sourceValid: true,
          canReceive: true,
          code: 'RCV_SOURCE_READY',
          message: 'The receipt remains within governed capacity.',
          evaluatedAtUtc: '2026-07-31T00:00:00Z',
          decisionKeys: Array.from(
            { length: 14 },
            (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`
          ),
          lines: [
            {
              purchaseOrderItemId: 'line-1',
              inventoryItemId: 'item-1',
              itemCode: 'ITEM-1',
              itemName: 'Governed item',
              unitOfMeasure: 'EA',
              orderedQuantity: 100,
              previouslyReceiptedQuantity: 94.5,
              toleranceQuantity: 5,
              maximumReceivableQuantity: 105,
              remainingQuantity: 10.5,
              requestedQuantity: 0,
              allowed: true,
              code: 'RCV_LINE_CAPACITY_AVAILABLE',
              message: 'Capacity remains.',
              integrityHash: 'B'.repeat(64),
            },
          ],
          requiredActions: [],
        }}
      />
    );

    expect(markup).toContain('Ready');
    expect(markup).toContain('10.5');
    expect(markup).toContain('Max. incl. tolerance');
    expect(markup).not.toContain('>Remaining<');
    expect(markup).toContain('DEC-001');
    expect(markup).toContain('DEC-014');
  });

  it('fails closed when readiness cannot be loaded', () => {
    const markup = renderToStaticMarkup(
      <ReceiptSourceControlCard
        loading={false}
        readiness={null}
        error="Receipt source could not be verified."
      />
    );

    expect(markup).toContain('Blocked');
    expect(markup).toContain('Receipt source could not be verified.');
  });
});
