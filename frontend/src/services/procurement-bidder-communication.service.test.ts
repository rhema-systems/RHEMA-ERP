import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementBidderCommunicationService as service } from './procurement-bidder-communication.service';

describe('procurement bidder-communication API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses authenticated source-scoped internal and supplier-scoped status paths', async () => {
    await service.getOverview('ExceptionalSourcing', 'tender-1');
    await service.getExternalStatus('RequestForQuotation', 'rfq-1');

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      '/procurement/bidder-communications/ExceptionalSourcing/tender-1'
    );
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/procurement/bidder-communications/external/RequestForQuotation/rfq-1'
    );
  });

  it('targets immutable letter, dispatch, acknowledgement, appeal, and security resources', async () => {
    const request = { idempotencyKey: 'tdc0211-1' };
    await service.approveLetter('Tender', 'tender-1', 'recipient-1', {
      ...request,
      contentReference: 'LETTER-1',
      contentChecksumSha256: 'a'.repeat(64),
      templateVersionId: 'template-version-1',
      workflowInstanceId: 'workflow-1',
      approvalReference: 'APPROVAL-1',
      approvalEvidenceReference: 'EVIDENCE-1',
      expectedRegisterIntegrityHash: 'b'.repeat(64),
    });
    await service.dispatchLetter('Tender', 'tender-1', 'letter-1', {
      ...request,
      channel: 'SupplierPortal',
      destination: 'supplier-1',
      dispatchReference: 'DISPATCH-1',
      dispatchEvidenceReference: 'EVIDENCE-2',
    });
    await service.acknowledge(
      'Tender',
      'tender-1',
      'dispatch-1',
      {
        ...request,
        outcome: 'Accepted',
        acknowledgementChannel: 'SupplierPortal',
        acknowledgementReference: 'ACK-1',
        evidenceReference: 'EVIDENCE-3',
      },
      true
    );
    await service.fileAppeal(
      'Tender',
      'tender-1',
      'recipient-1',
      {
        ...request,
        grounds: 'Review requested.',
        evidenceReference: 'EVIDENCE-4',
      },
      true
    );
    await service.actOnSecurity('Tender', 'tender-1', 'security-1', {
      ...request,
      actionType: 'Returned',
      workflowInstanceId: 'workflow-2',
      actionReference: 'RETURN-1',
      reason: 'Statutory windows elapsed.',
      evidenceReference: 'EVIDENCE-5',
    });

    expect(api.post.mock.calls.map(([path]) => path)).toEqual([
      '/procurement/bidder-communications/Tender/tender-1/recipients/recipient-1/letters/approve',
      '/procurement/bidder-communications/Tender/tender-1/letters/letter-1/dispatch',
      '/procurement/bidder-communications/external/Tender/tender-1/dispatches/dispatch-1/acknowledgement',
      '/procurement/bidder-communications/external/Tender/tender-1/appeals',
      '/procurement/bidder-communications/Tender/tender-1/securities/security-1/actions',
    ]);
  });
});
