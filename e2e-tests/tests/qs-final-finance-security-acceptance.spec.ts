import {
  expect,
  test,
  type APIRequestContext,
  type APIResponse,
  type Browser,
} from '@playwright/test';

const api = process.env.E2E_API_URL?.trim() || 'http://127.0.0.1:5000';
const baseUrl = process.env.E2E_BASE_URL?.trim() || 'http://127.0.0.1:3100';

const required = (name: string): string => {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} must be configured for QS acceptance.`);
  return value;
};

type Actor = { name: string; headers: { Authorization: string } };
type FinanceDocument = {
  id: string;
  status: string | number;
  journalEntryId?: string | null;
  reversalJournalEntryId?: string | null;
  paidAmount?: number;
  balanceAmount?: number;
};

const paymentStatus = (status: string | number): string =>
  typeof status === 'string'
    ? status
    : ({
        1: 'Draft',
        2: 'PendingAuthorization',
        3: 'Authorized',
        4: 'Processed',
        9: 'Reversed',
      }[status] ?? String(status));

const invoiceStatus = (status: string | number): string =>
  typeof status === 'string'
    ? status
    : ({
        1: 'Draft',
        2: 'PendingApproval',
        3: 'Approved',
        4: 'PartiallyPaid',
        5: 'Paid',
      }[status] ?? String(status));

async function json<T>(
  response: APIResponse,
  label: string,
  allowed = [200],
): Promise<T> {
  const body = await response.text();
  expect(allowed, `${label}: HTTP ${response.status()} ${body}`).toContain(
    response.status(),
  );
  return body ? (JSON.parse(body) as T) : (undefined as T);
}

async function login(
  request: APIRequestContext,
  name: string,
  username: string,
  password: string,
): Promise<Actor> {
  const value = await json<{ token?: string }>(
    await request.post(`${api}/api/auth/login`, {
      data: { username, password, rememberMe: false },
    }),
    `${name} login`,
  );
  expect(value.token, `${name} must receive a bearer token.`).toBeTruthy();
  return { name, headers: { Authorization: `Bearer ${value.token!}` } };
}

async function financeApprovals(
  request: APIRequestContext,
  actor: Actor,
  entityId: string,
): Promise<number> {
  const queue = await json<
    Array<{
      approvalId: string;
      entityId: string;
      canApprove: boolean;
      approveDisabledReason?: string;
    }>
  >(
    await request.get(
      `${api}/api/finance/approvals/pending?page=1&pageSize=100`,
      {
        headers: actor.headers,
      },
    ),
    `${actor.name} Finance approval queue`,
  );
  const pending = queue.filter(
    (item) => item.entityId.toLowerCase() === entityId.toLowerCase(),
  );
  for (const item of pending) {
    expect(
      item.canApprove,
      item.approveDisabledReason || `${actor.name} cannot approve`,
    ).toBe(true);
    await json(
      await request.post(
        `${api}/api/finance/approvals/${item.approvalId}/approve`,
        {
          headers: actor.headers,
          data: { comments: 'QS final cross-owner acceptance approval.' },
        },
      ),
      `${actor.name} Finance approval`,
    );
  }
  return pending.length;
}

async function expectDenied(
  response: APIResponse,
  label: string,
  allowed = [403],
) {
  const body = await response.text();
  expect(allowed, `${label}: HTTP ${response.status()} ${body}`).toContain(
    response.status(),
  );
  if (response.status() === 403) {
    expect(
      body,
      `${label} must return a bounded authorization response.`,
    ).not.toContain('System.');
  }
}

test.describe.serial('QS final Finance and security acceptance', () => {
  test.setTimeout(12 * 60_000);

  test('posts the QS AP invoice, allocates and posts payment, balances GL, and reverses idempotently', async ({
    browser,
    request,
  }) => {
    const certificateId = required('QS_ACCEPTANCE_CERTIFICATE_ID');
    const invoiceId = required('QS_ACCEPTANCE_INVOICE_ID');
    const supplierId = required('QS_ACCEPTANCE_SUPPLIER_ID');
    const bankAccountId = required('QS_ACCEPTANCE_BANK_ACCOUNT_ID');
    const paymentMethodId = required('QS_ACCEPTANCE_PAYMENT_METHOD_ID');
    const financePassword = required('QS_ACCEPTANCE_FINANCE_PASSWORD');

    const apOfficer = await login(
      request,
      'AP officer',
      'ap.officer',
      financePassword,
    );
    const approvalActors = await Promise.all(
      [
        ['accounts officer', 'accounts.officer'],
        ['finance manager', 'finance.manager'],
        ['financial controller', 'financial.controller'],
        ['chief accountant', 'chief.accountant'],
        ['managing director', 'managing.director'],
      ].map(([name, username]) =>
        login(request, name, username, financePassword),
      ),
    );

    let invoice = await json<FinanceDocument>(
      await request.get(`${api}/api/ap/invoices/${invoiceId}`, {
        headers: apOfficer.headers,
      }),
      'read QS-linked AP invoice',
    );
    if (invoiceStatus(invoice.status) === 'Draft') {
      invoice = await json<FinanceDocument>(
        await request.post(`${api}/api/ap/invoices/${invoiceId}/submit`, {
          headers: apOfficer.headers,
        }),
        'submit QS-linked AP invoice',
      );
    }
    for (
      let pass = 0;
      pass < 8 && invoiceStatus(invoice.status) === 'PendingApproval';
      pass++
    ) {
      let acted = 0;
      for (const actor of approvalActors) {
        acted += await financeApprovals(request, actor, invoiceId);
      }
      invoice = await json<FinanceDocument>(
        await request.get(`${api}/api/ap/invoices/${invoiceId}`, {
          headers: apOfficer.headers,
        }),
        'refresh QS-linked AP invoice',
      );
      expect(
        acted,
        'At least one independently assigned actor must advance each approval pass.',
      ).toBeGreaterThan(0);
    }
    expect(
      ['Approved', 'PartiallyPaid', 'Paid'],
      'A rerun may resume an invoice whose governed payment is already posted.',
    ).toContain(invoiceStatus(invoice.status));
    if (!invoice.journalEntryId) {
      const controller = approvalActors.find(
        (value) => value.name === 'financial controller',
      )!;
      invoice = await json<FinanceDocument>(
        await request.post(`${api}/api/ap/invoices/${invoiceId}/post`, {
          headers: controller.headers,
        }),
        'idempotently recover approved QS-linked AP invoice posting',
      );
    }
    expect(invoice.journalEntryId).toBeTruthy();

    const reference = 'QS-E2E-FINAL-ACCEPTANCE';
    const page = await json<{ items: FinanceDocument[] }>(
      await request.get(
        `${api}/api/ap/payments?page=1&pageSize=20&searchTerm=${reference}`,
        {
          headers: apOfficer.headers,
        },
      ),
      'find rerunnable QS acceptance payment',
    );
    let payment = page.items?.[0];
    if (!payment) {
      payment = await json<FinanceDocument>(
        await request.post(`${api}/api/ap/payments`, {
          headers: apOfficer.headers,
          data: {
            supplierId,
            paymentDate: new Date().toISOString(),
            totalAmount: 50000,
            paymentMethod: 3,
            paymentMethodId,
            currencyCode: 'GHS',
            exchangeRate: 1,
            bankAccountId,
            transactionReference: reference,
            notes: 'QS certificate to Finance payment and reversal acceptance.',
            allocations: [
              {
                vendorInvoiceId: invoiceId,
                allocatedAmount: 50000,
                paymentCurrencyAmount: 50000,
                discountAmount: 0,
                withholdingTaxAmount: 0,
                notes:
                  'Partial settlement of the governed QS certificate invoice.',
              },
            ],
          },
        }),
        'create allocated QS acceptance payment',
        [201],
      );
    }

    if (paymentStatus(payment.status) !== 'Reversed') {
      if (paymentStatus(payment.status) === 'Draft') {
        payment = await json<FinanceDocument>(
          await request.post(`${api}/api/ap/payments/${payment.id}/submit`, {
            headers: apOfficer.headers,
            data: {
              isExceptionalPayment: false,
              requestEvidenceException: true,
              evidenceExceptionReason:
                'Controlled test-only exception for the final QS cross-owner acceptance lifecycle.',
            },
          }),
          'submit allocated QS acceptance payment',
        );
      }
      for (
        let pass = 0;
        pass < 8 && paymentStatus(payment.status) === 'PendingAuthorization';
        pass++
      ) {
        let acted = 0;
        for (const actor of approvalActors) {
          acted += await financeApprovals(request, actor, payment.id);
        }
        payment = await json<FinanceDocument>(
          await request.get(`${api}/api/ap/payments/${payment.id}`, {
            headers: apOfficer.headers,
          }),
          'refresh QS acceptance payment',
        );
        expect(
          acted,
          'At least one independently assigned actor must advance each payment approval pass.',
        ).toBeGreaterThan(0);
      }
      if (paymentStatus(payment.status) === 'Authorized') {
        payment = await json<FinanceDocument>(
          await request.post(`${api}/api/ap/payments/${payment.id}/post`, {
            headers: apOfficer.headers,
          }),
          'post allocated QS acceptance payment',
        );
      }
      expect(paymentStatus(payment.status)).toBe('Processed');
      expect(payment.journalEntryId).toBeTruthy();

      const paidCertificate = await json<{
        financePaidAmount: number;
        financeBalanceAmount: number;
        reconciliationStatus: string;
      }>(
        await request.get(
          `${api}/api/quantity-survey/payment-certificates/${certificateId}`,
          {
            headers: (
              await login(
                request,
                'QS checker',
                required('QS_ACCEPTANCE_CHECKER_USERNAME'),
                required('QS_ACCEPTANCE_CHECKER_PASSWORD'),
              )
            ).headers,
          },
        ),
        'read partial Finance settlement through QS',
      );
      expect(paidCertificate.financePaidAmount).toBeCloseTo(50000, 2);
      expect(paidCertificate.financeBalanceAmount).toBeCloseTo(1460500, 2);
      expect(paidCertificate.reconciliationStatus).toMatch(/Partial|Balanced/i);

      const controller = approvalActors.find(
        (value) => value.name === 'financial controller',
      )!;
      payment = await json<FinanceDocument>(
        await request.post(`${api}/api/ap/payments/${payment.id}/reverse`, {
          headers: controller.headers,
          data: {
            reason:
              'Reverse the QS final acceptance payment to prove balanced and idempotent correction.',
          },
        }),
        'reverse QS acceptance payment',
      );
    }

    expect(paymentStatus(payment.status)).toBe('Reversed');
    expect(payment.journalEntryId).toBeTruthy();
    expect(payment.reversalJournalEntryId).toBeTruthy();

    const controller = approvalActors.find(
      (value) => value.name === 'financial controller',
    )!;
    const retry = await json<FinanceDocument>(
      await request.post(`${api}/api/ap/payments/${payment.id}/reverse`, {
        headers: controller.headers,
        data: {
          reason:
            'Retry the QS final acceptance reversal to prove the same durable correction is returned.',
        },
      }),
      'retry QS acceptance reversal',
    );
    expect(retry.reversalJournalEntryId).toBe(payment.reversalJournalEntryId);

    invoice = await json<FinanceDocument>(
      await request.get(`${api}/api/ap/invoices/${invoiceId}`, {
        headers: apOfficer.headers,
      }),
      'read invoice after reversal',
    );
    expect(invoiceStatus(invoice.status)).toBe('Approved');
    expect(invoice.paidAmount).toBe(0);
    expect(invoice.balanceAmount).toBeCloseTo(1510500, 2);

    const trace = await json<{
      payment: FinanceDocument;
      postings: unknown[];
      auditEvents: unknown[];
    }>(
      await request.get(`${api}/api/ap/payments/${payment.id}/trace`, {
        headers: controller.headers,
      }),
      'read payment source-to-ledger trace',
    );
    expect(trace.postings.length).toBeGreaterThanOrEqual(2);
    expect(trace.auditEvents.length).toBeGreaterThanOrEqual(2);

    const qs = await login(
      request,
      'QS checker browser',
      required('QS_ACCEPTANCE_CHECKER_USERNAME'),
      required('QS_ACCEPTANCE_CHECKER_PASSWORD'),
    );
    const context = await browser.newContext({ baseURL: baseUrl });
    const pageUi = await context.newPage();
    await pageUi.goto('/login');
    await pageUi.evaluate(
      (bearer) => {
        localStorage.setItem('authToken', bearer);
        localStorage.setItem('token', bearer);
      },
      qs.headers.Authorization.replace('Bearer ', ''),
    );
    await pageUi.goto(
      `/development/projects/${required('QS_ACCEPTANCE_PROJECT_ID')}/commercial-admin`,
      { waitUntil: 'domcontentloaded', timeout: 90_000 },
    );
    await expect(
      pageUi.getByText('Loading project workspace...', { exact: true }),
    ).toBeHidden({
      timeout: 120_000,
    });
    await expect(
      pageUi.getByText('IPC-2026-00003', { exact: true }),
    ).toBeVisible({ timeout: 30_000 });
    await context.close();
  });

  test('rejects missing-role, cross-project, cross-partner, and cross-tenant direct API attempts', async ({
    request,
  }) => {
    const financePassword = required('QS_ACCEPTANCE_FINANCE_PASSWORD');
    const unauthorized = await login(
      request,
      'non-QS Finance user',
      'accounts.officer',
      financePassword,
    );
    const checker = await login(
      request,
      'QS checker',
      required('QS_ACCEPTANCE_CHECKER_USERNAME'),
      required('QS_ACCEPTANCE_CHECKER_PASSWORD'),
    );
    const external = await login(
      request,
      'external contractor',
      required('QS_ACCEPTANCE_CONTRACTOR_USERNAME'),
      required('QS_ACCEPTANCE_CONTRACTOR_PASSWORD'),
    );
    const projectId = required('QS_ACCEPTANCE_PROJECT_ID');
    const certificateId = required('QS_ACCEPTANCE_CERTIFICATE_ID');
    const boqVersionId = required('QS_ACCEPTANCE_BOQ_VERSION_ID');
    const valuationId = required('QS_ACCEPTANCE_VALUATION_ID');
    const contractId = required('QS_ACCEPTANCE_CONTRACT_ID');
    const unrelatedProjectId = required('QS_ACCEPTANCE_UNASSIGNED_PROJECT_ID');
    const crossTenantProjectId = required(
      'QS_ACCEPTANCE_CROSS_TENANT_PROJECT_ID',
    );
    const unknownId = 'd6050000-0000-4000-8000-000000000001';

    const denied: Array<[string, () => Promise<APIResponse>]> = [
      [
        'BoQ approval',
        () =>
          request.post(
            `${api}/api/projects/${projectId}/boq-versions/${boqVersionId}/approve`,
            { headers: unauthorized.headers, data: {} },
          ),
      ],
      [
        'rate update',
        () =>
          request.put(`${api}/api/quantity-survey/rate-library/${unknownId}`, {
            headers: unauthorized.headers,
            data: {},
          }),
      ],
      [
        'valuation approval',
        () =>
          request.post(
            `${api}/api/quantity-survey/valuation-worksheets/worksheets/${valuationId}/approve`,
            { headers: unauthorized.headers, data: {} },
          ),
      ],
      [
        'certificate approval',
        () =>
          request.post(
            `${api}/api/quantity-survey/payment-certificates/${certificateId}/approve`,
            { headers: unauthorized.headers, data: {} },
          ),
      ],
      [
        'variation approval',
        () =>
          request.post(
            `${api}/api/quantity-survey/variations/${unknownId}/approve`,
            { headers: unauthorized.headers, data: {} },
          ),
      ],
      [
        'retention release',
        () =>
          request.post(
            `${api}/api/procurement/works-closeout/contracts/${contractId}/actions`,
            { headers: unauthorized.headers, data: {} },
          ),
      ],
      [
        'report drilldown',
        () =>
          request.get(
            `${api}/api/projects/${projectId}/quantity-survey-cost-dashboard`,
            { headers: unauthorized.headers },
          ),
      ],
      [
        'audit document',
        () =>
          request.get(
            `${api}/api/quantity-survey/payment-certificates/${certificateId}/document`,
            { headers: unauthorized.headers },
          ),
      ],
    ];
    for (const [label, send] of denied) await expectDenied(await send(), label);

    await expectDenied(
      await request.get(
        `${api}/api/projects/${projectId}/quantity-survey-cost-dashboard`,
      ),
      'anonymous QS report',
      [401],
    );
    await expectDenied(
      await request.get(
        `${api}/api/projects/${crossTenantProjectId}/quantity-survey-cost-dashboard`,
        {
          headers: checker.headers,
        },
      ),
      'cross-tenant QS project',
      [403, 404],
    );
    await expectDenied(
      await request.get(
        `${api}/api/projects/external/my-projects/${unrelatedProjectId}/valuation-worksheets/${valuationId}`,
        { headers: external.headers },
      ),
      'cross-project external valuation',
      [403, 404],
    );
    await expectDenied(
      await request.get(
        `${api}/api/projects/external/my-projects/${crossTenantProjectId}/valuation-worksheets/${valuationId}`,
        { headers: external.headers },
      ),
      'cross-tenant external valuation',
      [403, 404],
    );
    await expectDenied(
      await request.get(
        `${api}/api/quantity-survey/payment-certificates/${certificateId}`,
        {
          headers: external.headers,
        },
      ),
      'external actor on internal certificate API',
    );
  });
});
