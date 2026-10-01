import { describe, expect, it } from 'vitest';
import type { PropertyEnquiryQueueItem } from '@/services/propertyEnquiryService';
import { resolveActivityPropertyEnquiryContext } from './crm-activity-property-enquiry';

const enquiry = (
  id: string,
  crmLeadId?: string
): PropertyEnquiryQueueItem => ({
  id,
  crmLeadId,
  ticketNumber: `PE-${id}`,
  subject: `Enquiry ${id}`,
  status: 'Acknowledged',
  createdAt: '2026-10-01T00:00:00Z',
  requesterName: 'Ama Mensah',
});

describe('CRM activity property enquiry context', () => {
  it('inherits and hides the selector when the Lead has one enquiry', () => {
    const result = resolveActivityPropertyEnquiryContext(
      [enquiry('one', 'lead-1'), enquiry('other', 'lead-2')],
      'lead-1'
    );

    expect(result.inheritedPropertyEnquiry?.id).toBe('one');
    expect(result.showPropertyEnquirySelector).toBe(false);
  });

  it('requires selection only when a Lead has multiple enquiries', () => {
    const result = resolveActivityPropertyEnquiryContext(
      [enquiry('one', 'lead-1'), enquiry('two', 'lead-1')],
      'lead-1'
    );

    expect(result.inheritedPropertyEnquiry).toBeNull();
    expect(result.selectablePropertyEnquiries.map((item) => item.id)).toEqual([
      'one',
      'two',
    ]);
    expect(result.showPropertyEnquirySelector).toBe(true);
  });
});
