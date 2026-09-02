import { beforeEach, describe, expect, it, vi } from 'vitest';
import { tenderService } from './tenderService';

describe('tender evaluator candidate client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('uses the tender-scoped authorised candidate endpoint instead of the general user directory', async () => {
    const candidates = [
      {
        userId: 'evaluator-1',
        userName: 'tender.evaluator',
        fullName: 'Tender Evaluator',
        email: 'tender.evaluator@example.test',
        roleNames: ['TDC_EVALUATOR'],
      },
    ];
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(candidates), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await expect(
      tenderService.getEvaluatorCandidates('tender-1')
    ).resolves.toEqual(candidates);

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/Tenders/tender-1/evaluator-candidates',
      expect.objectContaining({ method: 'GET', headers: expect.any(Object) })
    );
    expect(String(fetchMock.mock.calls[0][0])).not.toBe('/api/user');
  });

  it('surfaces candidate endpoint ProblemDetails detail and code', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            status: 403,
            detail: 'Tender administration permission is required.',
            extensions: { code: 'TENDER_ADMIN_FORBIDDEN' },
          }),
          {
            status: 403,
            headers: { 'Content-Type': 'application/problem+json' },
          }
        )
      )
    );

    await expect(
      tenderService.getEvaluatorCandidates('tender-1')
    ).rejects.toThrow(
      'Tender administration permission is required. (TENDER_ADMIN_FORBIDDEN)'
    );
  });
});
