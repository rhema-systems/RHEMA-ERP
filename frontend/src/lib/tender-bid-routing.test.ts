import { describe, expect, it } from 'vitest';
import { getSupplierTenderBidPath } from './tender-bid-routing';

describe('supplier tender bid routing', () => {
  it('resumes a draft through initiation so access and payment are reconciled', () => {
    expect(
      getSupplierTenderBidPath('tender-1', { id: 'bid-1', status: 'Draft' })
    ).toBe('/external-portal/tenders/tender-1/initiate-bid');
  });

  it('opens a submitted bid as a read-only bid record', () => {
    expect(
      getSupplierTenderBidPath('tender-1', { id: 'bid-1', status: 'Submitted' })
    ).toBe('/external-portal/my-bids/bid-1');
  });
});
