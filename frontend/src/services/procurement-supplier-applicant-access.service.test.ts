import { beforeEach, describe, expect, it, vi } from 'vitest';

import { supplierApplicantAccessService as service } from './procurement-supplier-applicant-access.service';

const response = (body: unknown = {}) =>
  Promise.resolve(
    new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    })
  );

describe('supplier applicant access client', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    sessionStorage.clear();
    localStorage.clear();
  });

  it('keeps the restricted applicant bearer in session storage only', async () => {
    service.setSessionToken('restricted-applicant-token');
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
      response({
        registrationId: 'registration-1',
        documents: [],
        statusHistory: [],
      })
    );

    await service.portal();

    expect(sessionStorage.getItem('tdc-supplier-applicant-session')).toBe(
      'restricted-applicant-token'
    );
    expect(localStorage.getItem('tdc-supplier-applicant-session')).toBeNull();
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining('/procurement/supplier-applicant-access/portal'),
      expect.objectContaining({
        headers: { Authorization: 'Bearer restricted-applicant-token' },
      })
    );
  });

  it('uses public verification and token-login routes before portal access', async () => {
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(() =>
        response({ message: 'Sent', maskedContact: 's***@example.com' })
      )
      .mockImplementationOnce(() =>
        response({ registrationId: 'registration-1', applicationToken: 'once' })
      )
      .mockImplementationOnce(() =>
        response({ registrationId: 'registration-1', sessionToken: 'session' })
      );

    await service.requestChallenge({
      channel: 'Email',
      contact: 'supplier@example.com',
      recaptchaToken: 'captcha-request',
    });
    await service.verifyAndIssue({
      channel: 'Email',
      contact: 'supplier@example.com',
      otpCode: '123456',
      companyName: 'Supplier Ltd',
      registrationCategory: 'Goods',
      recaptchaToken: 'captcha-verify',
    });
    await service.startSession({
      applicationToken: 'application-token',
      recaptchaToken: 'captcha-session',
    });

    expect(fetch.mock.calls.map(([url]) => String(url))).toEqual([
      expect.stringContaining('/verification-challenges'),
      expect.stringContaining('/verified-applications'),
      expect.stringContaining('/sessions'),
    ]);
    expect(
      fetch.mock.calls.map(
        ([, init]) =>
          JSON.parse(String(init?.body)) as { recaptchaToken: string }
      )
    ).toEqual([
      expect.objectContaining({ recaptchaToken: 'captcha-request' }),
      expect.objectContaining({ recaptchaToken: 'captcha-verify' }),
      expect.objectContaining({ recaptchaToken: 'captcha-session' }),
    ]);
  });

  it('uses normal internal authentication for resend and activation retry', async () => {
    localStorage.setItem('authToken', 'internal-admin-token');
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementation(() =>
        Promise.resolve(new Response(null, { status: 204 }))
      );

    await service.resend('registration-1');
    await service.retryActivation('registration-1');

    expect(fetch.mock.calls.map(([url]) => String(url))).toEqual([
      expect.stringContaining('/credential/resend'),
      expect.stringContaining('/activation/retry'),
    ]);
    expect(fetch.mock.calls[0][1]).toEqual(
      expect.objectContaining({
        headers: { Authorization: 'Bearer internal-admin-token' },
      })
    );
  });

  it('uses internal authentication and dedicated routes for contact correction', async () => {
    localStorage.setItem('authToken', 'internal-admin-token');
    const fetch = vi
      .spyOn(globalThis, 'fetch')
      .mockImplementationOnce(() =>
        response({
          message: 'Sent',
          channel: 'Email',
          maskedContact: 'su***@test',
        })
      )
      .mockImplementationOnce(() =>
        response({
          registrationId: 'registration-1',
          contactCorrected: true,
          provisioningRetried: true,
          credentialDelivered: true,
        })
      );

    await service.requestContactCorrectionChallenge('registration-1', {
      channel: 'Email',
      contact: 'supplier@test.example',
    });
    await service.confirmContactCorrection('registration-1', {
      channel: 'Email',
      contact: 'supplier@test.example',
      otpCode: '123456',
      reason: 'Correct approved supplier contact.',
    });

    expect(fetch.mock.calls.map(([url]) => String(url))).toEqual([
      expect.stringContaining('/contact-correction/challenges'),
      expect.stringContaining('/contact-correction/confirm'),
    ]);
    expect(fetch.mock.calls[0][1]).toEqual(
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: 'Bearer internal-admin-token',
        }),
      })
    );
    expect(JSON.parse(String(fetch.mock.calls[1][1]?.body))).toEqual(
      expect.objectContaining({ otpCode: '123456' })
    );
  });

  it('includes the safe ProblemDetails correlation reference in contact correction failures', async () => {
    localStorage.setItem('authToken', 'internal-admin-token');
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          status: 401,
          detail: 'The verification code is invalid.',
          code: 'SUPPLIER_APPLICANT_CONTACT_CORRECTION_OTP_INVALID',
          correlationId: 'contact-correction-reference-123',
        }),
        {
          status: 401,
          headers: { 'Content-Type': 'application/problem+json' },
        }
      )
    );

    const action = service.confirmContactCorrection('registration-1', {
      channel: 'Email',
      contact: 'supplier@test.example',
      otpCode: '654321',
      reason: 'Correct approved supplier contact.',
    });

    await expect(action).rejects.toMatchObject({
      message:
        'The verification code is invalid. Reference: contact-correction-reference-123',
      status: 401,
      code: 'SUPPLIER_APPLICANT_CONTACT_CORRECTION_OTP_INVALID',
      correlationId: 'contact-correction-reference-123',
    });
  });
});
