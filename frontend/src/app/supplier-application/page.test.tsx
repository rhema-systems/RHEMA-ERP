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
  writeText: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mocks.push }),
}));

vi.mock('sonner', () => ({
  toast: {
    success: mocks.toastSuccess,
    error: mocks.toastError,
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
      {
        id: string;
        theme?: 'light' | 'dark';
        onChange(token: string | null): void;
      }
    >(function CaptchaMock({ id, theme, onChange }, ref) {
      React.useImperativeHandle(
        ref,
        () => ({
          reset: () => onChange(null),
        }),
        [onChange]
      );
      return (
        <button
          type="button"
          data-testid={`${id}-complete`}
          data-theme={theme}
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
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: mocks.writeText },
    });
    mocks.writeText.mockResolvedValue(undefined);
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
      paymentOnly: false,
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
    expect(
      screen.queryByLabelText('Registration category')
    ).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Email address'), {
      target: { value: 'retained@example.test' },
    });
    fireEvent.click(screen.getByTestId('supplier-apply-captcha-complete'));
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Send verification code',
      })
    );
    await waitFor(() => expect(mocks.requestChallenge).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText('Six-digit verification code'), {
      target: { value: '123456' },
    });
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Verify and secure existing application',
      })
    );

    await waitFor(() => {
      expect(mocks.verifyAndIssue).toHaveBeenCalledWith(
        expect.objectContaining({
          contact: 'retained@example.test',
          companyName: '',
          retainedRegistrationId,
        })
      );
    });
    expect(mocks.verifyAndIssue.mock.calls[0][0]).not.toHaveProperty(
      'recaptchaToken'
    );
  });

  it('uses CAPTCHA once for OTP challenge and separately for token login', async () => {
    render(<SupplierApplicationAccessPage />);

    expect(
      await screen.findByTestId('supplier-apply-captcha-complete')
    ).toHaveAttribute('data-theme', 'light');
    fireEvent.change(screen.getByLabelText('Email address'), {
      target: { value: 'supplier@example.test' },
    });
    fireEvent.change(screen.getByLabelText('Company name'), {
      target: { value: 'Supplier Ltd' },
    });
    fireEvent.click(screen.getByTestId('supplier-apply-captcha-complete'));
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Send verification code',
      })
    );

    await waitFor(() => {
      expect(mocks.requestChallenge).toHaveBeenCalledWith(
        expect.objectContaining({
          recaptchaToken: 'response-supplier-apply-captcha',
        })
      );
    });
    expect(
      screen.queryByTestId('supplier-apply-captcha-complete')
    ).not.toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Six-digit verification code'), {
      target: { value: '123456' },
    });
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Verify and continue',
      })
    );

    await waitFor(() => {
      expect(mocks.verifyAndIssue).toHaveBeenCalledTimes(1);
    });
    expect(mocks.verifyAndIssue.mock.calls[0][0]).not.toHaveProperty(
      'recaptchaToken'
    );

    const copyButton = await screen.findByRole('button', {
      name: 'Copy application token',
    });
    fireEvent.click(copyButton);
    await waitFor(() => {
      expect(mocks.writeText).toHaveBeenCalledWith('application-token');
      expect(mocks.toastSuccess).toHaveBeenCalledWith(
        'Application token copied.'
      );
    });

    mocks.writeText.mockRejectedValueOnce(new Error('Clipboard unavailable'));
    fireEvent.click(copyButton);
    await waitFor(() => {
      expect(mocks.toastError).toHaveBeenCalledWith(
        'Could not copy the token. Select it and copy it manually.'
      );
    });

    const loginTab = screen.getByRole('tab', { name: 'Token login' });
    fireEvent.mouseDown(loginTab, { button: 0, ctrlKey: false });
    fireEvent.click(loginTab);
    fireEvent.change(await screen.findByLabelText('Application token'), {
      target: { value: 'application-token' },
    });
    fireEvent.click(
      await screen.findByTestId('supplier-login-captcha-complete')
    );
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Open application',
      })
    );

    await waitFor(() => {
      expect(mocks.startSession).toHaveBeenCalledWith({
        applicationToken: 'application-token',
        recaptchaToken: 'response-supplier-login-captcha',
      });
      expect(mocks.setSessionToken).toHaveBeenCalledWith('restricted-session');
      expect(mocks.push).toHaveBeenCalledWith('/supplier-application/portal');
    });
  });

  it('withholds a paid token and opens only the restricted payment session', async () => {
    mocks.verifyAndIssue.mockResolvedValueOnce({
      registrationId: 'registration-paid',
      registrationNumber: 'REG-PAID-001',
      tokenId: 'token-paid',
      tokenReference: 'TOK-PAID-001',
      applicationToken: null,
      applicantSessionToken: 'payment-only-session',
      paymentSessionToken: 'payment-only-session',
      paymentSessionExpiresAtUtc: '2026-08-05T02:00:00Z',
      paymentOnly: true,
      feeMode: 'Paid',
      tokenStatus: 'AwaitingPayment',
      paymentStatus: 'Pending',
      totalAmount: 100,
      currencyCode: 'GHS',
      deliveryStatus: 'WithheldPendingPayment',
      message: 'Payment verification is required.',
    });
    render(<SupplierApplicationAccessPage />);

    fireEvent.change(await screen.findByLabelText('Email address'), {
      target: { value: 'paid@example.test' },
    });
    fireEvent.change(screen.getByLabelText('Company name'), {
      target: { value: 'Paid Supplier Ltd' },
    });
    fireEvent.click(screen.getByTestId('supplier-apply-captcha-complete'));
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Send verification code',
      })
    );
    await waitFor(() => expect(mocks.requestChallenge).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText('Six-digit verification code'), {
      target: { value: '123456' },
    });
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Verify and continue',
      })
    );

    await screen.findByText(/application token is withheld/i);
    expect(
      screen.queryByRole('button', {
        name: 'Copy application token',
      })
    ).not.toBeInTheDocument();
    expect(mocks.setSessionToken).toHaveBeenCalledWith('payment-only-session');
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Continue to payment',
      })
    );
    expect(mocks.push).toHaveBeenCalledWith('/supplier-application/portal');
  });

  it('recovers the existing application after contact re-verification', async () => {
    mocks.verifyAndIssue.mockResolvedValueOnce({
      registrationId: 'registration-existing',
      registrationNumber: 'REG-EXISTING-001',
      tokenId: 'token-existing',
      tokenReference: 'TOK-EXISTING-001',
      applicationToken: null,
      applicantSessionToken: 'rotated-recovery-session',
      applicantSessionExpiresAtUtc: '2026-08-05T03:00:00Z',
      paymentOnly: false,
      resumedExistingApplication: true,
      feeMode: 'Free',
      tokenStatus: 'Active',
      paymentStatus: 'NotRequired',
      totalAmount: 0,
      currencyCode: 'GHS',
      deliveryStatus: 'ExistingApplicationResumed',
      message: 'Existing application recovered.',
    });
    render(<SupplierApplicationAccessPage />);

    fireEvent.change(await screen.findByLabelText('Email address'), {
      target: { value: 'existing@example.test' },
    });
    fireEvent.change(screen.getByLabelText('Company name'), {
      target: { value: 'Existing Supplier Ltd' },
    });
    fireEvent.click(screen.getByTestId('supplier-apply-captcha-complete'));
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Send verification code',
      })
    );
    await waitFor(() => expect(mocks.requestChallenge).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText('Six-digit verification code'), {
      target: { value: '123456' },
    });
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Verify and continue',
      })
    );

    expect(await screen.findByText(/was recovered/i)).toBeInTheDocument();
    expect(
      screen.getByText(/no duplicate registration or token was created/i)
    ).toBeInTheDocument();
    expect(mocks.setSessionToken).toHaveBeenCalledWith(
      'rotated-recovery-session'
    );
    expect(mocks.toastSuccess).toHaveBeenCalledWith(
      'Existing application recovered securely.'
    );
    expect(
      screen.queryByRole('button', {
        name: 'Copy application token',
      })
    ).not.toBeInTheDocument();
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Continue application',
      })
    );
    expect(mocks.push).toHaveBeenCalledWith('/supplier-application/portal');
  });
});
