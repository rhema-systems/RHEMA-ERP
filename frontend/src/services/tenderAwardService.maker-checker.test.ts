import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { approveAward, createAward, rejectAward } from './tenderAwardService';

describe('tender award maker-checker service', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', {
      getItem: vi.fn().mockReturnValue('test-token'),
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('submits an award recommendation through the create endpoint', async () => {
    const response = { id: 'award-1', status: 'PendingApproval' };
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(response), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);
    const recommendation = {
      tenderId: 'tender-1',
      tenderBidId: 'bid-1',
      awardedAmount: 125000,
      currency: 'GHS',
      awardJustification: 'Highest evaluated responsive bid',
    };

    await expect(createAward(recommendation)).resolves.toEqual(response);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/TenderAwards',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify(recommendation),
      })
    );
  });

  it('posts approval notes to the pending recommendation endpoint', async () => {
    const response = { id: 'award-1', status: 'Awarded' };
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(response), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await expect(approveAward('award-1', { notes: 'Approved by committee' })).resolves.toEqual(response);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/TenderAwards/award-1/approve',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ notes: 'Approved by committee' }),
      })
    );
  });

  it('posts the required rejection reason and surfaces ProblemDetails', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          detail: 'The award maker cannot approve or reject their own recommendation.',
          code: 'AWARD_MAKER_CHECKER_VIOLATION',
        }),
        { status: 409, headers: { 'Content-Type': 'application/problem+json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    await expect(rejectAward('award-1', { reason: 'Evaluation requires correction' })).rejects.toThrow(
      'The award maker cannot approve or reject their own recommendation. (AWARD_MAKER_CHECKER_VIOLATION)'
    );
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/TenderAwards/award-1/reject',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ reason: 'Evaluation requires correction' }),
      })
    );
  });
});
