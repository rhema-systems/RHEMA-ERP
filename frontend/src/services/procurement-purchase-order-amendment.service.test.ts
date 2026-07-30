import { beforeEach, describe, expect, it, vi } from 'vitest';

import { apiService } from '@/services/api.service';
import { procurementPurchaseOrderAmendmentService as service } from './procurement-purchase-order-amendment.service';

vi.mock('@/services/api.service', () => ({
  apiService: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe('procurementPurchaseOrderAmendmentService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('keeps the internal overview scoped to its purchase order', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ amendments: [] });

    await service.overview('po-0406');

    expect(apiService.get).toHaveBeenCalledWith(
      '/procurement/purchase-order-amendments/purchase-orders/po-0406'
    );
  });

  it('uses separate controlled lifecycle endpoints', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'amendment-0406' });
    const lifecycle = {
      rowVersion: 'row-version',
      comment: 'Controlled amendment evidence retained.',
      evidenceReference: 'MINUTE-0406',
    };

    await service.submit('amendment-0406', lifecycle);
    await service.decide('amendment-0406', {
      ...lifecycle,
      approved: true,
    });
    await service.dispatch('amendment-0406', {
      channel: 'SupplierPortal',
      destination: 'supplier-portal',
      dispatchReference: 'DISPATCH-0406',
      documentReference: 'PO-AMENDMENT-0406',
      organizationSignatureEvidenceReference: 'SIGNATURE-0406',
      dispatchEvidenceReference: 'DISPATCH-EVIDENCE-0406',
      idempotencyKey: 'dispatch-0406',
    });

    expect(apiService.post).toHaveBeenNthCalledWith(
      1,
      '/procurement/purchase-order-amendments/amendment-0406/submit',
      lifecycle
    );
    expect(apiService.post).toHaveBeenNthCalledWith(
      2,
      '/procurement/purchase-order-amendments/amendment-0406/decision',
      expect.objectContaining({ approved: true })
    );
    expect(apiService.post).toHaveBeenNthCalledWith(
      3,
      '/procurement/purchase-order-amendments/amendment-0406/dispatches',
      expect.objectContaining({ channel: 'SupplierPortal' })
    );
  });

  it('keeps supplier acknowledgement on the external supplier-scoped route', async () => {
    vi.mocked(apiService.get).mockResolvedValue([]);
    vi.mocked(apiService.post).mockResolvedValue({ id: 'ack-0406' });

    await service.externalOverview();
    await service.acknowledge(
      'dispatch-0406',
      {
        outcome: 'Accepted',
        acknowledgementChannel: 'SupplierPortal',
        acknowledgementReference: 'ACK-0406',
        evidenceReference: 'ACK-EVIDENCE-0406',
        comments: 'Supplier accepted the amendment.',
        idempotencyKey: 'ack-0406',
      },
      true
    );

    expect(apiService.get).toHaveBeenCalledWith(
      '/procurement/purchase-order-amendments/external'
    );
    expect(apiService.post).toHaveBeenCalledWith(
      '/procurement/purchase-order-amendments/external/dispatches/dispatch-0406/acknowledgements',
      expect.objectContaining({ outcome: 'Accepted' })
    );
  });
});
