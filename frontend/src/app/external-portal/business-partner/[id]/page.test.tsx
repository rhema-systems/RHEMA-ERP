import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getMyRegistrations: vi.fn(),
  getById: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useParams: () => ({ id: 'registration-1' }),
}));

vi.mock('@/services/businessPartnerRegistrationService', () => ({
  businessPartnerRegistrationService: {
    getMyRegistrations: mocks.getMyRegistrations,
    getById: mocks.getById,
  },
}));

import ExternalBusinessPartnerRegistrationDetailsPage from './page';

const summary = {
  id: 'registration-1',
  applicationNumber: 'APP-001',
  partnerType: 'Supplier',
  registrationCategory: 'Goods',
  status: 'Approved',
  companyName: 'Example Goods Ltd',
  completionPercentage: 100,
  createdAt: '2026-09-03T10:00:00Z',
};

describe('external supplier registration details', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getMyRegistrations.mockResolvedValue([summary]);
    mocks.getById.mockResolvedValue({
      ...summary,
      registrationData: JSON.stringify({
        physicalAddress: '1 Supplier Street',
        city: 'Accra',
        country: 'Ghana',
        contacts: [
          {
            contactName: 'Procurement Contact',
            contactTitle: 'Manager',
            email: 'contact@example.test',
            isPrimary: true,
          },
        ],
        bankAccounts: [
          {
            bankName: 'Example Bank',
            branchName: 'Central Branch',
            accountName: 'Example Goods Ltd',
            accountNumber: '9876543210',
            currency: 'GHS',
            isPrimary: true,
          },
        ],
      }),
      documents: [],
      statusHistory: [],
    });
  });

  it('loads only an owned registration and displays repeatable contacts and masked bank accounts', async () => {
    render(<ExternalBusinessPartnerRegistrationDetailsPage />);

    expect(
      await screen.findByRole('heading', { name: 'Example Goods Ltd' })
    ).toBeVisible();
    expect(mocks.getMyRegistrations).toHaveBeenCalledTimes(1);
    expect(mocks.getById).toHaveBeenCalledWith('registration-1');

    const contactsTab = screen.getByRole('tab', { name: 'Contacts (1)' });
    fireEvent.mouseDown(contactsTab, { button: 0, ctrlKey: false });
    fireEvent.click(contactsTab);
    expect(await screen.findByText('Procurement Contact')).toBeVisible();

    const bankTab = screen.getByRole('tab', { name: 'Bank Accounts (1)' });
    fireEvent.mouseDown(bankTab, { button: 0, ctrlKey: false });
    fireEvent.click(bankTab);
    expect(await screen.findByText('Example Bank')).toBeVisible();
    expect(screen.getByText('•••••• 3210')).toBeVisible();
    expect(screen.queryByText('9876543210')).not.toBeInTheDocument();
  });

  it('does not request details for a registration outside the supplier account', async () => {
    mocks.getMyRegistrations.mockResolvedValue([]);

    render(<ExternalBusinessPartnerRegistrationDetailsPage />);

    expect(await screen.findByText('Registration unavailable')).toBeVisible();
    await waitFor(() => expect(mocks.getById).not.toHaveBeenCalled());
  });
});
