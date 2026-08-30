import { describe, expect, it } from 'vitest';

import { buildSplitAwardLines, getAwardableRfqQuotes } from './rfq-award';
import type { RfqItemDto, RfqQuoteDto } from '@/services/rfqService';

const item: RfqItemDto = {
  id: 'item-1', rfqId: 'rfq-1', lineNumber: 1, description: 'Laptop', quantity: 2, unitOfMeasure: 'EA',
};

const quote = (id: string, status: string): RfqQuoteDto => ({
  id,
  rfqId: 'rfq-1',
  businessPartnerId: `supplier-${id}`,
  partnerCode: id,
  partnerName: id,
  status,
  currency: 'GHS',
  totalAmount: 100,
  items: [{ id: `line-${id}`, rfqItemId: item.id, unitPrice: 50, lineTotal: 100 }],
});

describe('RFQ split award selection', () => {
  it('offers only submitted quotes for award', () => {
    expect(getAwardableRfqQuotes([
      quote('submitted', 'Submitted'),
      quote('late', 'LateRejected'),
      quote('draft', 'Draft'),
    ]).map((candidate) => candidate.id)).toEqual(['submitted']);
  });

  it('builds lines only from an awardable quote', () => {
    expect(buildSplitAwardLines(
      [item],
      [quote('submitted', 'Submitted')],
      { [item.id]: 'submitted' },
      { [item.id]: 'Best compliant price' }
    )).toEqual([{ rfqItemId: item.id, quoteId: 'submitted', awardReason: 'Best compliant price' }]);
  });

  it('rejects a stale non-submitted selection before calling the API', () => {
    expect(() => buildSplitAwardLines(
      [item],
      [quote('late', 'LateRejected')],
      { [item.id]: 'late' },
      {}
    )).toThrow('Select a submitted supplier quote for RFQ line 1.');
  });
});
