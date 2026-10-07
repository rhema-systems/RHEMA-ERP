import React from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CrmActivitiesPage from './page';

const mocks = vi.hoisted(() => ({
  getActivities: vi.fn(),
  getLeads: vi.fn(),
  getOpportunities: vi.fn(),
  getActivity: vi.fn(),
  createActivity: vi.fn(),
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
    getActivity: mocks.getActivity,
    createActivity: mocks.createActivity,
  },
}));
vi.mock('@/components/hr/common/EmployeePicker', () => ({
  EmployeePicker: ({ onChange }: { onChange: (id: string, label: string) => void }) => (
    <button type="button" onClick={() => onChange('employee-1', 'Akosua Mensah')}>
      Add Akosua Mensah
    </button>
  ),
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
    mocks.createActivity.mockResolvedValue({
      activityId: 'activity-1',
      subject: 'Customer meeting',
      activityType: 'Meeting',
      activityStatus: 'Planned',
      activityDate: '2026-10-07T09:00:00Z',
      requiresFollowUp: false,
      priority: 2,
      isOverdue: false,
      createdAt: '2026-10-07T09:00:00Z',
      internalAttendees: [
        {
          employeeId: 'employee-1',
          employeeNumber: 'EMP-001',
          displayName: 'Akosua Mensah',
        },
      ],
      externalAttendees: 'External Guest, guest@example.com',
    });
    mocks.getActivity.mockImplementation(() => mocks.createActivity.mock.results[0]?.value);
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

  it('submits selected employees separately from comma-separated external attendees', async () => {
    render(<CrmActivitiesPage />);

    const dialog = await screen.findByRole('dialog');
    const form = within(dialog);
    fireEvent.change(form.getByLabelText('Subject'), {
      target: { value: 'Customer meeting' },
    });
    fireEvent.click(form.getByRole('button', { name: 'Add Akosua Mensah' }));
    fireEvent.change(form.getByLabelText('External attendees'), {
      target: { value: 'External Guest, guest@example.com' },
    });
    fireEvent.click(form.getByRole('button', { name: 'Save Activity' }));

    await waitFor(() =>
      expect(mocks.createActivity).toHaveBeenCalledWith(
        expect.objectContaining({
          internalAttendeeEmployeeIds: ['employee-1'],
          externalAttendees: 'External Guest, guest@example.com',
        })
      )
    );
  });
});
