import { beforeEach, describe, expect, it, vi } from 'vitest';
import { contractService } from './contractService';

describe('contract activation client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    localStorage.setItem('token', 'internal-token');
  });

  it('loads the dedicated tenant-safe activation overview', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          contractId: 'contract-0407',
          contractNumber: 'CON-0407',
          contractStatus: 'Draft',
          isReady: false,
          canSubmit: false,
          canDecide: false,
          canActivate: false,
          requiredEvidenceKeys: ['legal-review'],
          decisionKeys: ['DEC-001', 'DEC-014'],
          checks: [],
          history: [],
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await contractService.getActivationOverview(
      'contract-0407'
    );

    expect(result.contractNumber).toBe('CON-0407');
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/contract-activations/contracts/contract-0407',
      expect.objectContaining({
        cache: 'no-store',
        headers: expect.objectContaining({
          Authorization: 'Bearer internal-token',
        }),
      })
    );
  });

  it('submits exact row-version and central-DMS evidence lineage', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: 'activation-0407' }), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await contractService.submitActivation('contract-0407', {
      reason: 'Ready for controlled activation.',
      idempotencyKey: 'idempotency-0407',
      contractRowVersion: 'AQID',
      evidence: [
        {
          requirementKey: 'legal-review',
          referenceKind: 'CentralDocumentUpload',
          fileUploadRecordId: 'upload-0407',
          evidenceReference: 'DMS-CON-0407',
        },
      ],
    });

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/contract-activations/contracts/contract-0407/submit',
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({
          Authorization: 'Bearer internal-token',
          'X-Correlation-ID': expect.any(String),
        }),
        body: expect.stringContaining('"contractRowVersion":"AQID"'),
      })
    );
  });

  it('surfaces structured fail-closed activation errors', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            code: 'CONTRACT_ACTIVATION_STALE',
            detail: 'The activation changed. Refresh and retry.',
          }),
          {
            status: 409,
            headers: { 'Content-Type': 'application/problem+json' },
          }
        )
      )
    );

    await expect(
      contractService.applyActivation(
        'activation-0407',
        'Supplier Signatory',
        'Activate.',
        'AQID'
      )
    ).rejects.toThrow('The activation changed. Refresh and retry.');
  });
});
