import { afterEach, expect, it, vi } from 'vitest';
import { submitBid } from './tenderBidService';

afterEach(() => vi.unstubAllGlobals());

it('preserves the actionable submission ProblemDetails and code', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
    detail: 'The effective document was not issued to this supplier.',
    code: 'TENDER_DOCUMENT_ISSUE_REQUIRED',
  }), { status: 400 })));
  await expect(submitBid('saved-bid', { confirmSubmission: true })).rejects.toThrow(
    'The effective document was not issued to this supplier. (TENDER_DOCUMENT_ISSUE_REQUIRED)');
});

it('accepts a successful empty submission response', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 204 })));
  await expect(submitBid('saved-bid', { confirmSubmission: true })).resolves.toBeUndefined();
});
