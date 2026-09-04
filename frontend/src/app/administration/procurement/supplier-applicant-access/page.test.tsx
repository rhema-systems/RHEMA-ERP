import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type {
  SupplierApplicantAccessHistory,
  SupplierApplicantAccessSummary,
} from '@/types/procurement-supplier-applicant-access';

const mocks = vi.hoisted(() => ({
  adminSummary: vi.fn(),
  adminHistory: vi.fn(),
  requestContactCorrectionChallenge: vi.fn(),
  confirmContactCorrection: vi.fn(),
  resend: vi.fn(),
  retryActivation: vi.fn(),
  toastSuccess: vi.fn(),
  toastWarning: vi.fn(),
  toastError: vi.fn(),
  hasPermission: vi.fn(),
  routerReplace: vi.fn(),
  getStoredUser: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mocks.routerReplace }),
}));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({
    hasPermission: mocks.hasPermission,
    isLoading: false,
  }),
}));

vi.mock('@/services/auth', () => ({
  authService: {
    getStoredUser: mocks.getStoredUser,
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: mocks.toastSuccess,
    warning: mocks.toastWarning,
    error: mocks.toastError,
  },
}));

vi.mock('@/services/procurement-supplier-applicant-access.service', () => ({
  supplierApplicantAccessService: {
    adminSummary: mocks.adminSummary,
    adminHistory: mocks.adminHistory,
    requestContactCorrectionChallenge: mocks.requestContactCorrectionChallenge,
    confirmContactCorrection: mocks.confirmContactCorrection,
    resend: mocks.resend,
    retryActivation: mocks.retryActivation,
  },
}));

import SupplierApplicantAccessAdministrationPage from './page';

const summary: SupplierApplicantAccessSummary = {
  totalApplications: 1,
  applicationInProgress: 0,
  pendingCredentialDelivery: 1,
  credentialDelivered: 0,
  activated: 0,
  rejected: 0,
  activationFailed: 0,
};

const pending: SupplierApplicantAccessHistory = {
  id: 'access-1',
  registrationId: 'registration-1',
  registrationNumber: 'APP-001',
  companyName: 'Supplier Ltd',
  verifiedChannel: 'Email',
  verifiedContactMasked: 'ol***@example.test',
  status: 'ApprovedPendingCredentialDelivery',
  verifiedAtUtc: '2026-08-09T12:00:00Z',
  notificationAttemptCount: 1,
};

const renderPage = () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <SupplierApplicantAccessAdministrationPage />
    </QueryClientProvider>
  );
};

const openAndVerify = async () => {
  fireEvent.click(
    await screen.findByRole('button', { name: 'Correct contact' })
  );
  fireEvent.change(screen.getByLabelText('New supplier contact'), {
    target: { value: 'new.owner@example.test' },
  });
  fireEvent.change(screen.getByLabelText('Correction reason'), {
    target: { value: 'Replace the contact after supplier OTP verification.' },
  });
  fireEvent.click(
    screen.getByRole('button', { name: 'Send verification code' })
  );
  await screen.findByText(/verification code sent to ne\*\*\*@example.test/i);
  fireEvent.change(screen.getByLabelText(/verification code sent to/i), {
    target: { value: '123456' },
  });
  fireEvent.click(
    screen.getByRole('button', { name: 'Verify, correct and provision' })
  );
  await waitFor(() => {
    expect(mocks.confirmContactCorrection).toHaveBeenCalledTimes(1);
  });
};

describe('supplier applicant verified-contact correction', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.hasPermission.mockReturnValue(true);
    mocks.getStoredUser.mockReturnValue({ roles: ['ExternalUser'] });
    mocks.adminSummary.mockResolvedValue(summary);
    mocks.adminHistory.mockResolvedValue([pending]);
    mocks.requestContactCorrectionChallenge.mockResolvedValue({
      message: 'Verification sent.',
      channel: 'Email',
      maskedContact: 'ne***@example.test',
    });
  });

  it('keeps the dialog open and shows the safe reference when confirmation fails', async () => {
    mocks.confirmContactCorrection.mockRejectedValue(
      new Error(
        'The verification code is invalid. Reference: contact-correction-reference-123'
      )
    );
    renderPage();

    await openAndVerify();

    expect(
      await screen.findByText(
        /the verification code is invalid\. reference: contact-correction-reference-123/i
      )
    ).toBeInTheDocument();
    expect(
      screen.getByRole('dialog', {
        name: 'Correct verified supplier contact',
      })
    ).toBeInTheDocument();
    expect(mocks.confirmContactCorrection).toHaveBeenCalledWith(
      'registration-1',
      expect.objectContaining({
        contact: 'new.owner@example.test',
        otpCode: '123456',
      })
    );
    expect(mocks.toastError).toHaveBeenCalledWith(
      expect.stringContaining('contact-correction-reference-123')
    );
  });

  it('closes only after the refreshed row confirms the persisted result', async () => {
    const delivered: SupplierApplicantAccessHistory = {
      ...pending,
      verifiedContactMasked: 'ne***@example.test',
      status: 'CredentialDelivered',
      loginIdentifier: 'new.owner@example.test',
      notificationAttemptCount: 2,
      lastNotificationStatus: 'TemporaryCredentialSent',
    };
    mocks.confirmContactCorrection.mockImplementation(async () => {
      mocks.adminHistory.mockResolvedValue([delivered]);
      return {
        registrationId: 'registration-1',
        maskedContact: 'ne***@example.test',
        status: 'CredentialDelivered',
        contactCorrected: true,
        provisioningRetried: true,
        credentialDelivered: true,
        message:
          'The verified supplier contact was corrected and temporary credentials were sent.',
      };
    });
    renderPage();

    await openAndVerify();

    await waitFor(() => {
      expect(
        screen.queryByRole('dialog', {
          name: 'Correct verified supplier contact',
        })
      ).not.toBeInTheDocument();
    });
    expect(
      screen.getByTestId('supplier-contact-correction-success')
    ).toHaveTextContent('temporary credentials were sent');
    expect(screen.getByText('ne***@example.test')).toBeInTheDocument();
    expect(mocks.toastSuccess).toHaveBeenCalled();
  });

  it('keeps the dialog open if the success response is not confirmed by the refreshed row', async () => {
    mocks.confirmContactCorrection.mockResolvedValue({
      registrationId: 'registration-1',
      maskedContact: 'ne***@example.test',
      status: 'CredentialDelivered',
      contactCorrected: true,
      provisioningRetried: true,
      credentialDelivered: true,
      message: 'Corrected.',
    });
    renderPage();

    await openAndVerify();

    expect(
      await screen.findByText(/refreshed supplier record did not confirm it/i)
    ).toBeInTheDocument();
    expect(
      screen.getByRole('dialog', {
        name: 'Correct verified supplier contact',
      })
    ).toBeInTheDocument();
  });

  it('redirects an external supplier before protected administration queries run', async () => {
    mocks.hasPermission.mockReturnValue(false);

    renderPage();

    await waitFor(() => {
      expect(mocks.routerReplace).toHaveBeenCalledWith('/external-portal');
    });
    expect(mocks.adminSummary).not.toHaveBeenCalled();
    expect(mocks.adminHistory).not.toHaveBeenCalled();
    expect(
      screen.queryByTestId('supplier-applicant-access-admin')
    ).not.toBeInTheDocument();
  });
});
