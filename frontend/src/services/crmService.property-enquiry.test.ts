import { afterEach, describe, expect, it, vi } from 'vitest';
import { crmService, type CreateCrmActivityDto } from './crmService';

describe('CRM property enquiry activity linkage', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends the selected property enquiry with the completed contact activity', async () => {
    const request: CreateCrmActivityDto = {
      subject: 'Completed sales call',
      activityType: 'Call',
      description: 'Confirmed the buyer remains interested.',
      activityDate: '2026-10-01',
      activityStatus: 'Completed',
      priority: 2,
      propertyEnquiryTicketId: 'enquiry-42',
      requiresFollowUp: false,
    };
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          activityId: 'activity-1',
          ...request,
          propertyEnquiryTicketNumber: 'PE-042',
        }),
        { status: 201, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    await crmService.createActivity(request);

    expect(fetchMock).toHaveBeenCalledWith('/api/crm/activities', {
      method: 'POST',
      headers: expect.any(Object),
      body: JSON.stringify(request),
    });
  });
});
