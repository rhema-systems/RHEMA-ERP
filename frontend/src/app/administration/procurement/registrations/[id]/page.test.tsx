import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getById: vi.fn(),
  getActiveLicenseTypes: vi.fn(),
  push: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useParams: () => ({ id: 'registration-review-1' }),
  useRouter: () => ({ push: mocks.push, back: vi.fn() }),
}));

vi.mock('@/services/registrationReviewService', () => ({
  registrationReviewService: {
    getById: mocks.getById,
  },
}));

vi.mock('@/services/partnerConfigService', () => ({
  licenseTypeService: {
    getActive: mocks.getActiveLicenseTypes,
  },
}));

import RegistrationDetailPage from './page';

describe('back-office supplier registration review', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getActiveLicenseTypes.mockResolvedValue([]);
    mocks.getById.mockResolvedValue({
      id: 'registration-review-1',
      applicationNumber: 'APP-REVIEW-001',
      companyName: 'Review Goods Limited',
      partnerType: 'Supplier',
      email: 'applicant@example.test',
      phone: '0000000000',
      status: 'Submitted',
      completionPercentage: 100,
      documents: [],
      statusHistory: [],
      registrationData: JSON.stringify({
        RegistrationData: JSON.stringify({
          contacts: [
            {
              contactName: 'Primary Reviewer Contact',
              contactTitle: 'Operations Manager',
              email: 'primary@example.test',
              isPrimary: true,
            },
            {
              contactName: 'Accounts Contact',
              contactTitle: 'Accountant',
              email: 'accounts@example.test',
              isPrimary: false,
            },
          ],
          bankAccounts: [
            {
              bankName: 'First Test Bank',
              accountName: 'Review Goods Limited',
              accountNumber: 'TEST-ACCOUNT-001',
              currency: 'GHS',
              isPrimary: true,
            },
            {
              bankName: 'Second Test Bank',
              accountName: 'Review Goods Limited',
              accountNumber: 'TEST-ACCOUNT-002',
              currency: 'USD',
              isPrimary: false,
            },
          ],
        }),
      }),
    });
  });

  it('shows all captured contacts and bank accounts in dedicated review tabs', async () => {
    render(<RegistrationDetailPage />);

    expect(
      await screen.findByRole('heading', { name: 'Review Goods Limited' })
    ).toBeVisible();
    expect(mocks.getById).toHaveBeenCalledWith('registration-review-1');

    const contactsTab = screen.getByRole('tab', { name: 'Contacts (2)' });
    fireEvent.mouseDown(contactsTab, { button: 0, ctrlKey: false });
    fireEvent.click(contactsTab);
    expect(await screen.findByText('Primary Reviewer Contact')).toBeVisible();
    expect(screen.getByText('Accounts Contact')).toBeVisible();

    const bankAccountsTab = screen.getByRole('tab', {
      name: 'Bank Accounts (2)',
    });
    fireEvent.mouseDown(bankAccountsTab, { button: 0, ctrlKey: false });
    fireEvent.click(bankAccountsTab);
    expect(await screen.findByText('First Test Bank')).toBeVisible();
    expect(screen.getByText('Second Test Bank')).toBeVisible();
  });
});
