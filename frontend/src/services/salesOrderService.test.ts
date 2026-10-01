import { describe, expect, it } from 'vitest';

import { toBackendCreateSalesOrderDto, type CreateSalesOrderDto } from './salesOrderService';

describe('toBackendCreateSalesOrderDto', () => {
  it('applies the selected document tax group to every order line', () => {
    const taxGroupId = '11111111-1111-1111-1111-111111111111';
    const order: CreateSalesOrderDto = {
      businessPartnerId: '22222222-2222-2222-2222-222222222222',
      taxGroupId,
      lines: [
        { itemName: 'Plot A', quantity: 1, unitPrice: 1000 },
        { itemName: 'Facility fee', quantity: 1, unitPrice: 200 },
      ],
    };

    const payload = toBackendCreateSalesOrderDto(order);

    expect(payload.taxGroupId).toBe(taxGroupId);
    expect(payload.lines).toHaveLength(2);
    expect(payload.lines.every((line) => line.taxGroupId === taxGroupId)).toBe(true);
    expect(payload.lines.every((line) => line.taxRate === undefined)).toBe(true);
  });
});
