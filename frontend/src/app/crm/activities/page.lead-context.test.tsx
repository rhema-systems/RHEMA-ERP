import React from 'react';
import { render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CrmActivitiesPage from './page';

const mocks = vi.hoisted(() => ({
  getActivities: vi.fn(),
  getLeads: vi.fn(),
  getOpportunities: vi.fn(),
  listPartners: vi.fn(),
  listPropertyEnquiries: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams('leadId=lead-1&new=1'),
}));
vi.mock('@/services/crmService', () => ({
  crmService: {
    getActivities: mocks.getActivities,
    getLeads: mocks.getLeads,
    getOpportunities: mocks.getOpportunities,
  },
}));
vi.mock('@/services/businessPartnerService', () => ({
  businessPartnerService: {
    getAllPartnersForDropdown: mocks.listPartners,
  },
}));
vi.mock('@/services/propertyEnquiryService', () => ({
  propertyEnquiryService: {
    listAll: mocks.listPropertyEnquiries,
  },
}));

const emptyPage = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 100,
  totalPages: 1,
};

describe('CRM activity Lead context', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mocks.getActivities.mockResolvedValue({ ...emptyPage, pageSize: 12 });
    mocks.getLeads.mockResolvedValue({
      ...emptyPage,
      items: [{ leadId: 'lead-1', fullName: 'Ama Lead' }],
    });
    mocks.getOpportunities.mockResolvedValue(emptyPage);
    mocks.listPartners.mockResolvedValue([]);
    mocks.listPropertyEnquiries.mockResolvedValue([
      {
        id: 'ticket-1',
        crmLeadId: 'lead-1',
        ticketNumber: 'PE-001',
        subject: 'Airport property enquiry',
        status: 'Acknowledged',
        createdAt: '2026-10-01T00:00:00Z',
        requesterName: 'Ama Lead',
      },
    ]);
  });

  it('shows inherited Lead and enquiry context without editable selectors', async () => {
    render(<CrmActivitiesPage />);

    const dialog = await screen.findByRole('dialog');
    const form = within(dialog);
    expect(await form.findByText('Ama Lead')).toBeInTheDocument();
    expect(form.getByText('Linked automatically from the Lead page.')).toBeInTheDocument();
    expect(await form.findByText(/PE-001/)).toBeInTheDocument();
    expect(form.queryByText('No linked lead')).not.toBeInTheDocument();
    expect(form.queryByLabelText('Property Enquiry')).not.toBeInTheDocument();
  });
});
