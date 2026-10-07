import { describe, expect, it } from 'vitest';
import { getTicketSubmissionLabel } from './helpdesk-ticket-submission';

describe('helpdesk ticket submission label', () => {
  it('identifies a public property enquiry from its persisted submission lineage', () => {
    expect(
      getTicketSubmissionLabel({
        isPublicSiteSubmission: true,
        requesterAuthenticationProvider: null,
      })
    ).toBe('Public Site');
  });

  it('keeps authenticated external portal submissions distinct', () => {
    expect(
      getTicketSubmissionLabel({
        isPublicSiteSubmission: false,
        requesterAuthenticationProvider: 'Local',
      })
    ).toBe('External Portal');
  });
});
