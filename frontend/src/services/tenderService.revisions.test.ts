import { beforeEach, describe, expect, it, vi } from 'vitest';

import { tenderService } from './tenderService';

describe('tender amendment API client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('issues the complete governed revision payload', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(
          JSON.stringify({ id: 'revision-1', revisionNumber: 'REV-001' }),
          { status: 201, headers: { 'Content-Type': 'application/json' } }
        )
      );
    vi.stubGlobal('fetch', fetchMock);

    const request = {
      revisionType: 'DeadlineExtension' as const,
      description: 'Extend the deadline after issuing clarification answers.',
      changes: 'Submission deadline moved by seven days.',
      newSubmissionDeadline: '2026-09-15T17:00:00.000Z',
      requiresRebid: true,
      sendNotifications: true,
    };

    await tenderService.createTenderRevision('tender-1', request);

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/procurement/Tenders/tender-1/revisions',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify(request),
      })
    );
  });

  it('surfaces the immutable-control conflict code', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            message: 'The tender cannot be revised after controlled opening.',
            code: 'TENDER_REVISION_OPENING_LOCKED',
          }),
          { status: 409 }
        )
      )
    );

    await expect(
      tenderService.createTenderRevision('tender-1', {
        revisionType: 'Addendum',
        description: 'Attempted late addendum.',
        requiresRebid: false,
        sendNotifications: true,
      })
    ).rejects.toThrow(
      'The tender cannot be revised after controlled opening. (TENDER_REVISION_OPENING_LOCKED)'
    );
  });
});
