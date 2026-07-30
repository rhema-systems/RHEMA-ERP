import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  push: vi.fn(),
  requestChallenge: vi.fn(),
  verifyAndIssue: vi.fn(),
  startSession: vi.fn(),
  setSessionToken: vi.fn(),
  getPublicSecuritySettings: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mocks.push }),
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

vi.mock('@/services/procurement-supplier-applicant-access.service', () => ({
  supplierApplicantAccessService: {
    requestChallenge: mocks.requestChallenge,
    verifyAndIssue: mocks.verifyAndIssue,
    startSession: mocks.startSession,
    setSessionToken: mocks.setSessionToken,
  },
}));

vi.mock('@/services/settings', () => ({
  settingsService: {
    getPublicSecuritySettings: mocks.getPublicSecuritySettings,
  },
}));

vi.mock('@/components/security/PublicCaptchaChallenge', async () => {
  const React = await import('react');
  return {
    PublicCaptchaChallenge: React.forwardRef<
      { reset(): void },
      { id: string; onChange(token: string | null): void }
    >(function CaptchaMock({ id, onChange }, ref) {
      React.useImperativeHandle(ref, () => ({
        reset: () => onChange(null),
      }), [onChange]);
      return (
        <button
          type="button"
          data-testid={`${id}-complete`}
          onClick={() => onChange(`response-${id}`)}
        >
          Complete CAPTCHA
        </button>
      );
    }),
  };
});

import SupplierApplicationAccessPage from './page';

describe('supplier application CAPTCHA lifecycle', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.history.replaceState({}, '', '/supplier-application');
    mocks.getPublicSecuritySettings.mockResolvedValue({
      captchaEnabled: true,
      captchaProvider: 'recaptcha',
      recaptchaSiteKey: 'public-site-key',
      hCaptchaSiteKey: null,
    });
    mocks.requestChallenge.mockResolvedValue({
      message: 'Sent',
      maskedContact: 'su***@example.test',
    });
    mocks.verifyAndIssue.mockResolvedValue({
      registrationId: 'registration-1',
      registrationNumber: 'REG-001',
      tokenId: 'token-1',
      tokenReference: 'TOK-001',
      applicationToken: 'application-token',
      feeMode: 'Free',
      tokenStatus: 'Active',
      paymentStatus: 'Exempt',
      totalAmount: 0,
      currencyCode: 'GHS',
      deliveryStatus: 'Sent',
      message: 'Issued',
    });
    mocks.startSession.mockResolvedValue({
      sessionToken: 'restricted-session',
      expiresAtUtc: '2026-07-28T00:00:00Z',
      paymentOnly: false,
      registrationId: 'registration-1',
    });
  });

  it('migrates a retained draft through verified contact without requesting replacement profile data', async () => {
    const retainedRegistrationId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
    window.history.replaceState(
      {},
      '',
      `/supplier-application?retainedRegistrationId=${retainedRegistrationId}`
    );
    render(<SupplierApplicationAccessPage />);

    await screen.findByText(/original application data and audit ownership/i);
    expect(screen.queryByLabelText('Company name')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Registration category')).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Email address'), {
      target: { value: 'retained@example.test' },
    });
    fireEvent.click(screen.getByTestId('supplier-apply-captcha-complete'));
    fireEvent.click(screen.getByRole('button', {
      name: 'Send verification code',
    }));
    await waitFor(() => expect(mocks.requestChallenge).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText('Six-digit verification code'), {
      target: { value: '123456' },
    });
    fireEvent.click(screen.getByTestId('supplier-apply-captcha-complete'));
    fireEvent.click(screen.getByRole('button', {
      name: 'Verify and secure existing application',
    }));

    await waitFor(() => {
      expect(mocks.verifyAndIssue).toHaveBeenCalledWith(
        expect.objectContaining({
          contact: 'retained@example.test',
          companyName: '',
          retainedRegistrationId,
          recaptchaToken: 'response-supplier-apply-captcha',
        })
      );
    });
  });

  it('sends a fresh CAPTCHA response for challenge, issue, and login', async () => {
    render(<SupplierApplicationAccessPage />);

    await screen.findByTestId('supplier-apply-captcha-complete');
    fireEvent.change(screen.getByLabelText('Email address'), {
      target: { value: 'supplier@example.test' },
    });
    fireEvent.change(screen.getByLabelText('Company name'), {
      target: { value: 'Supplier Ltd' },
    });
    fireEvent.click(screen.getByTestId('supplier-apply-captcha-complete'));
    fireEvent.click(screen.getByRole('button', {
      name: 'Send verification code',
    }));

    await waitFor(() => {
      expect(mocks.requestChallenge).toHaveBeenCalledWith(
        expect.objectContaining({
          recaptchaToken: 'response-supplier-apply-captcha',
        })
      );
    });

    fireEvent.change(screen.getByLabelText('Six-digit verification code'), {
      target: { value: '123456' },
    });
    fireEvent.click(screen.getByTestId('supplier-apply-captcha-complete'));
    fireEvent.click(screen.getByRole('button', {
      name: 'Verify and issue token',
    }));

    await waitFor(() => {
      expect(mocks.verifyAndIssue).toHaveBeenCalledWith(
        expect.objectContaining({
          recaptchaToken: 'response-supplier-apply-captcha',
        })
      );
    });

    const loginTab = screen.getByRole('tab', { name: 'Token login' });
    fireEvent.mouseDown(loginTab, { button: 0, ctrlKey: false });
    fireEvent.click(loginTab);
    fireEvent.change(await screen.findByLabelText('Application token'), {
      target: { value: 'application-token' },
    });
    fireEvent.click(await screen.findByTestId(
      'supplier-login-captcha-complete'
    ));
    fireEvent.click(screen.getByRole('button', {
      name: 'Open application',
    }));

    await waitFor(() => {
      expect(mocks.startSession).toHaveBeenCalledWith({
        applicationToken: 'application-token',
        recaptchaToken: 'response-supplier-login-captcha',
      });
      expect(mocks.setSessionToken).toHaveBeenCalledWith(
        'restricted-session'
      );
      expect(mocks.push).toHaveBeenCalledWith(
        '/supplier-application/portal'
      );
    });
  });
});
