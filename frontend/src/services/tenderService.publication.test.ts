import { afterEach, expect, it, vi } from 'vitest';
import { tenderService } from './tenderService';

afterEach(() => vi.unstubAllGlobals());

it('retains the server publication detail and structured code without retrying', async () => {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(
      JSON.stringify({
        detail: 'The exact document binding is required.',
        code: 'TENDER_DOCUMENT_REGISTER_REQUIRED',
      }),
      { status: 422 }
    )
  );
  vi.stubGlobal('fetch', fetchMock);
  await expect(
    tenderService.publishTender('tender-1', {
      submissionDeadline: '2026-10-01T17:00:00Z',
      invitedBusinessPartnerIds: [],
      sendNotifications: false,
    })
  ).rejects.toThrow(
    'The exact document binding is required. (TENDER_DOCUMENT_REGISTER_REQUIRED)'
  );
  expect(fetchMock).toHaveBeenCalledTimes(1);
});
