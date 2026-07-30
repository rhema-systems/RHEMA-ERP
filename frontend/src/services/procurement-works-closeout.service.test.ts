import { beforeEach, describe, expect, it, vi } from 'vitest';
import { procurementWorksCloseoutService } from './procurement-works-closeout.service';

describe('procurement Works closeout client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'internal-token');
    vi.stubGlobal('crypto', { randomUUID: () => 'tdc-0409-correlation' });
  });

  it('loads the tenant-safe Works closeout overview', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({
        contractId: 'contract-0409',
        contractNumber: 'CON-0409',
        decisionKeys: ['DEC-001', 'DEC-014'],
        history: [],
      }), { status: 200, headers: { 'Content-Type': 'application/json' } })
    );
    vi.stubGlobal('fetch', fetchMock);

    await procurementWorksCloseoutService.getOverview('contract-0409');

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/works-closeout/contracts/contract-0409',
      expect.objectContaining({
        cache: 'no-store',
        headers: expect.objectContaining({
          Authorization: 'Bearer internal-token',
        }),
      })
    );
  });

  it('submits exact project and central-DMS evidence without an auto-post flag', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: 'action-0409' }), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await procurementWorksCloseoutService.submit('contract-0409', {
      actionType: 'InitialTakeover',
      projectHandoverItemId: 'handover-1',
      effectiveAtUtc: '2026-07-30T12:00:00Z',
      reason: 'Practical completion independently verified.',
      idempotencyKey: 'idempotency-0409',
      contractRowVersion: 'AQID',
      evidence: [{
        requirementKey: 'practical-completion-certificate',
        referenceKind: 'CentralDocumentUpload',
        fileUploadRecordId: 'upload-1',
        evidenceReference: 'dms:version-1',
      }],
    });

    const options = fetchMock.mock.calls[0][1];
    expect(fetchMock.mock.calls[0][0]).toBe(
      '/api/procurement/works-closeout/contracts/contract-0409/actions'
    );
    expect(options).toEqual(expect.objectContaining({
      method: 'POST',
      headers: expect.objectContaining({
        Authorization: 'Bearer internal-token',
        'X-Correlation-ID': 'tdc-0409-correlation',
      }),
    }));
    expect(options.body).toContain('"projectHandoverItemId":"handover-1"');
    expect(options.body).toContain('"fileUploadRecordId":"upload-1"');
    expect(options.body).not.toContain('amountAutoPosted');
  });

  it('sends only the server row version and independent outcome on decision', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: 'action-0409', status: 'Approved' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await procurementWorksCloseoutService.decide(
      'action-0409', true, 'Approved after site verification.', 'BAUG'
    );

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/works-closeout/actions/action-0409/decision',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          approved: true,
          comment: 'Approved after site verification.',
          rowVersion: 'BAUG',
        }),
      })
    );
  });
});
