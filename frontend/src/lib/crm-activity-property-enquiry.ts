import type { PropertyEnquiryQueueItem } from '@/services/propertyEnquiryService';

export function resolveActivityPropertyEnquiryContext(
  propertyEnquiries: PropertyEnquiryQueueItem[],
  leadId?: string
) {
  const leadPropertyEnquiries = leadId
    ? propertyEnquiries.filter((enquiry) => enquiry.crmLeadId === leadId)
    : [];

  return {
    inheritedPropertyEnquiry:
      leadPropertyEnquiries.length === 1 ? leadPropertyEnquiries[0] : null,
    selectablePropertyEnquiries: leadId
      ? leadPropertyEnquiries
      : propertyEnquiries,
    showPropertyEnquirySelector:
      !leadId || leadPropertyEnquiries.length > 1,
  };
}
