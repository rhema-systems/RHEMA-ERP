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
    const fetch = vi.spyOn(globalThis, 'fetch')
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
    expect(fetch.mock.calls.map(([, init]) =>
      JSON.parse(String(init?.body)) as { recaptchaToken: string }
    )).toEqual([
      expect.objectContaining({ recaptchaToken: 'captcha-request' }),
      expect.objectContaining({ recaptchaToken: 'captcha-verify' }),
      expect.objectContaining({ recaptchaToken: 'captcha-session' }),
    ]);
  });

  it('uses normal internal authentication for resend and activation retry', async () => {
    localStorage.setItem('authToken', 'internal-admin-token');
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
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
});
