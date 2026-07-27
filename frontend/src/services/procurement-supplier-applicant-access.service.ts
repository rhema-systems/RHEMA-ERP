import type {
  SupplierApplicantAccessHistory,
  SupplierApplicantAccessSummary,
  SupplierApplicantChannel,
  SupplierApplicantIssueResult,
  SupplierApplicantPaymentMethod,
  SupplierApplicantPortal,
  SupplierApplicantSessionResult,
  SupplierRegistrationCategory,
} from '@/types/procurement-supplier-applicant-access';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const ROOT = `${API_BASE_URL}/procurement/supplier-applicant-access`;
const SESSION_KEY = 'tdc-supplier-applicant-session';

async function read<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(
      body.detail || body.message || 'The supplier application request failed.'
    ) as Error & { status?: number; code?: string };
    error.status = response.status;
    error.code = body.code || body.extensions?.code;
    throw error;
  }
  return body as T;
}

const json = (body: unknown, token?: string) => ({
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  },
  body: JSON.stringify(body),
});

export const supplierApplicantAccessService = {
  getSessionToken() {
    if (typeof window === 'undefined') return null;
    return sessionStorage.getItem(SESSION_KEY);
  },

  setSessionToken(token: string) {
    sessionStorage.setItem(SESSION_KEY, token);
  },

  clearSession() {
    sessionStorage.removeItem(SESSION_KEY);
  },

  requestChallenge(input: {
    tenantCode?: string;
    channel: SupplierApplicantChannel;
    contact: string;
    recaptchaToken?: string;
  }) {
    return fetch(`${ROOT}/verification-challenges`, json(input)).then((response) =>
      read<{ message: string; maskedContact: string }>(response)
    );
  },

  verifyAndIssue(input: {
    tenantCode?: string;
    channel: SupplierApplicantChannel;
    contact: string;
    otpCode: string;
    companyName: string;
    registrationCategory: SupplierRegistrationCategory;
    recaptchaToken?: string;
  }) {
    return fetch(`${ROOT}/verified-applications`, json(input)).then((response) =>
      read<SupplierApplicantIssueResult>(response)
    );
  },

  startSession(input: {
    tenantCode?: string;
    applicationToken: string;
    recaptchaToken?: string;
  }) {
    return fetch(`${ROOT}/sessions`, json(input)).then((response) =>
      read<SupplierApplicantSessionResult>(response)
    );
  },

  portal() {
    const token = this.getSessionToken();
    return fetch(`${ROOT}/portal`, {
      headers: { Authorization: `Bearer ${token}` },
    }).then((response) => read<SupplierApplicantPortal>(response));
  },

  updateApplication(input: {
    companyName: string;
    registrationCategory: SupplierRegistrationCategory;
    email?: string;
    phone?: string;
    registrationData: string;
  }) {
    const token = this.getSessionToken();
    return fetch(`${ROOT}/portal/application`, {
      ...json(input, token || undefined),
      method: 'PUT',
    }).then((response) => read<SupplierApplicantPortal>(response));
  },

  submit() {
    const token = this.getSessionToken();
    return fetch(`${ROOT}/portal/submit`, json({}, token || undefined)).then(
      (response) => read<SupplierApplicantPortal>(response)
    );
  },

  paymentMethods() {
    const token = this.getSessionToken();
    return fetch(`${ROOT}/portal/payment-methods`, {
      headers: { Authorization: `Bearer ${token}` },
    }).then((response) => read<SupplierApplicantPaymentMethod[]>(response));
  },

  recordPayment(input: {
    paymentMethodId: string;
    paymentReference?: string;
    rowVersion: string;
  }) {
    const token = this.getSessionToken();
    return fetch(
      `${ROOT}/portal/payments`,
      json(input, token || undefined)
    ).then((response) => read<unknown>(response));
  },

  uploadDocument(form: FormData) {
    const token = this.getSessionToken();
    return fetch(`${ROOT}/portal/documents`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` },
      body: form,
    }).then((response) => read<unknown>(response));
  },

  deleteDocument(documentId: string) {
    const token = this.getSessionToken();
    return fetch(`${ROOT}/portal/documents/${documentId}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${token}` },
    }).then(async (response) => {
      if (!response.ok) await read(response);
    });
  },

  adminSummary() {
    const token =
      typeof window === 'undefined'
        ? null
        : localStorage.getItem('authToken') || localStorage.getItem('token');
    return fetch(`${ROOT}/admin/summary`, {
      headers: { Authorization: `Bearer ${token}` },
    }).then((response) => read<SupplierApplicantAccessSummary>(response));
  },

  adminHistory() {
    const token =
      typeof window === 'undefined'
        ? null
        : localStorage.getItem('authToken') || localStorage.getItem('token');
    return fetch(`${ROOT}/admin/history`, {
      headers: { Authorization: `Bearer ${token}` },
    }).then((response) => read<SupplierApplicantAccessHistory[]>(response));
  },

  resend(registrationId: string) {
    const token =
      typeof window === 'undefined'
        ? null
        : localStorage.getItem('authToken') || localStorage.getItem('token');
    return fetch(
      `${ROOT}/admin/registrations/${registrationId}/credential/resend`,
      { method: 'POST', headers: { Authorization: `Bearer ${token}` } }
    ).then(async (response) => {
      if (!response.ok) await read(response);
    });
  },

  retryActivation(registrationId: string) {
    const token =
      typeof window === 'undefined'
        ? null
        : localStorage.getItem('authToken') || localStorage.getItem('token');
    return fetch(
      `${ROOT}/admin/registrations/${registrationId}/activation/retry`,
      { method: 'POST', headers: { Authorization: `Bearer ${token}` } }
    ).then(async (response) => {
      if (!response.ok) await read(response);
    });
  },
};
