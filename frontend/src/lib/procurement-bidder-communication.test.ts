import { describe, expect, it, vi } from 'vitest';

import {
  bidderCommunicationExternalBackHref,
  bidderCommunicationInternalBackHref,
  bidderCommunicationOutcomeCounts,
  bidderCommunicationStandstillLabel,
  createBidderCommunicationIdempotencyKey,
  hasBidderCommunicationAction,
} from './procurement-bidder-communication';
import type { ProcurementBidderCommunicationOverview } from '@/types/procurement-bidder-communication';

describe('procurement bidder-communication helpers', () => {
  it('normalizes only server-returned actions and creates scoped idempotency keys', () => {
    expect(
      hasBidderCommunicationAction(
        ['ApproveLetter', 'Dispatch_Letter'],
        'dispatch-letter'
      )
    ).toBe(true);
    expect(
      hasBidderCommunicationAction(['ViewHistory'], 'ApproveLetter')
    ).toBe(false);

    vi.spyOn(crypto, 'randomUUID').mockReturnValue(
      '00000000-0000-4000-8000-000000002111'
    );
    expect(createBidderCommunicationIdempotencyKey('Approve Letter')).toBe(
      'tdc0211-approveletter-00000000-0000-4000-8000-000000002111'
    );
  });

  it('routes Tender, RFQ, and Exceptional sources through their existing journeys', () => {
    expect(bidderCommunicationInternalBackHref('Tender', 'tender-1')).toBe(
      '/procurement/tenders/tender-1/award-readiness'
    );
    expect(
      bidderCommunicationInternalBackHref('RequestForQuotation', 'rfq-1')
    ).toBe(
      '/procurement/rfqs/rfq-1/award-readiness'
    );
    expect(
      bidderCommunicationInternalBackHref(
        'ExceptionalSourcing',
        'tender-2'
      )
    ).toBe(
      '/procurement/tenders/tender-2/award-readiness?sourceType=ExceptionalSourcing'
    );
    expect(
      bidderCommunicationExternalBackHref('RequestForQuotation', 'rfq-1')
    ).toBe(
      '/external-portal/rfqs/rfq-1'
    );
  });

  it('uses server standstill state and server-derived recipients for summaries', () => {
    const overview = {
      standstillStartsAtUtc: '2026-07-24T00:00:00Z',
      standstillEndsAtUtc: '2026-08-07T00:00:00Z',
      standstillElapsed: false,
      recipients: [
        {
          outcome: 'Successful',
          letterVersions: [{ dispatches: [{ id: 'dispatch-1' }] }],
        },
        {
          outcome: 'Unsuccessful',
          letterVersions: [{ dispatches: [] }],
        },
      ],
    } as ProcurementBidderCommunicationOverview;

    expect(
      bidderCommunicationStandstillLabel(
        overview,
        new Date('2026-07-31T00:00:00Z').getTime()
      )
    ).toBe('7 days remaining');
    expect(bidderCommunicationOutcomeCounts(overview)).toEqual({
      successful: 1,
      unsuccessful: 1,
      approvedLetters: 2,
      dispatches: 1,
    });
  });
});
