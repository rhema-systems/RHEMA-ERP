import { afterEach, describe, expect, it, vi } from 'vitest';

import { getBidScorecard, submitEvaluation } from './tenderEvaluationService';

describe('tender evaluation API lifecycle', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('uses the controller scorecard route', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ tenderBidId: 'bid-1' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await getBidScorecard('bid-1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/TenderEvaluations/scorecard/bid/bid-1'),
      expect.any(Object)
    );
  });

  it('shows the committee ProblemDetails reason when submit is blocked', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            detail: 'Signed quorum evidence is missing.',
            code: 'EVALUATION_QUORUM_EVIDENCE_REQUIRED',
          }),
          { status: 422 }
        )
      )
    );

    await expect(
      submitEvaluation('evaluation-1', {
        confirmSubmission: true,
        signatureReference: 'SIG-1',
        evidenceReference: 'DMS-1',
        idempotencyKey: 'eval-submit-1',
      })
    ).rejects.toThrow(
      'Signed quorum evidence is missing. (EVALUATION_QUORUM_EVIDENCE_REQUIRED)'
    );
  });
});
