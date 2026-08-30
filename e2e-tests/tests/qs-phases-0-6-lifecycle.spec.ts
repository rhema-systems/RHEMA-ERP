import { expect, test, type APIResponse, type Browser } from '@playwright/test';

const required = (name: string): string => {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} must be configured for QS acceptance.`);
  return value;
};

const ids = {
  measurement: 'd6000000-0000-4000-8000-000000000101',
  measurementLine: 'd6000000-0000-4000-8000-000000000102',
  measurementEvidence: 'd6000000-0000-4000-8000-000000000103',
  measurementRecord: 'd6000000-0000-4000-8000-000000000104',
  worksheet: 'd6020000-0000-4000-8000-000000000010',
  worksheetEvidence: 'd6020000-0000-4000-8000-000000000011',
  contractorClaim: 'd6020000-0000-4000-8000-000000000012',
  contractorSubmit: 'd6020000-0000-4000-8000-000000000013',
  qsReview: 'd6020000-0000-4000-8000-000000000024',
  vet: 'd6020000-0000-4000-8000-000000000014',
  consultantEndorse: 'd6020000-0000-4000-8000-000000000015',
  worksheetSubmit: 'd6020000-0000-4000-8000-000000000016',
  worksheetQsReview: 'd6020000-0000-4000-8000-000000000017',
  worksheetEngineeringConfirmation: 'd6020000-0000-4000-8000-000000000018',
  worksheetFinanceValidation: 'd6020000-0000-4000-8000-000000000025',
  worksheetFinalApproval: 'd6020000-0000-4000-8000-000000000026',
  certificate: 'd6020000-0000-4000-8000-000000000020',
  certificateSubmit: 'd6020000-0000-4000-8000-000000000021',
  certificateQsReview: 'd6020000-0000-4000-8000-000000000022',
  certificateEngineeringConfirmation: 'd6020000-0000-4000-8000-000000000023',
  certificateFinanceValidation: 'd6020000-0000-4000-8000-000000000027',
  certificateFinalApproval: 'd6020000-0000-4000-8000-000000000028',
};

const parsed = async <T>(response: APIResponse, label: string): Promise<T> => {
  const body = await response.text();
  expect(response.status(), `${label}: ${body}`).toBe(200);
  return JSON.parse(body) as T;
};

async function login(
  browser: Browser,
  label: string,
  username: string,
  password: string,
) {
  const api = process.env.E2E_API_URL?.trim() || 'http://127.0.0.1:5000';
  const context = await browser.newContext({
    baseURL: process.env.E2E_BASE_URL || 'http://127.0.0.1:3100',
  });
  const page = await context.newPage();
  await page.goto('/login');
  await expect(
    page.getByRole('button', { name: 'Sign In', exact: true }),
  ).toBeVisible();
  const response = await context.request.post(`${api}/api/auth/login`, {
    data: { username, password, rememberMe: false },
  });
  const responseText = await response.text();
  expect(response.status(), `${label} API login: ${responseText}`).toBe(200);
  const loginResult = JSON.parse(responseText) as {
    token?: string;
    refreshToken?: string;
  };
  const token = loginResult.token;
  expect(
    token,
    `${label} login must issue the normal bearer token.`,
  ).toBeTruthy();
  if (!token) throw new Error(`${label} login did not issue a bearer token.`);
  await page.evaluate(
    ({ bearer, refresh }) => {
      localStorage.setItem('authToken', bearer);
      localStorage.setItem('token', bearer);
      if (refresh) localStorage.setItem('refreshToken', refresh);
    },
    { bearer: token, refresh: loginResult.refreshToken },
  );
  await page.goto('/dashboard');
  await page.waitForURL((url) => !url.pathname.includes('/login'), {
    timeout: 90_000,
  });
  return { context, page, headers: { Authorization: `Bearer ${token}` } };
}

test('QS phases 0-6 governed role-separated review, approval, and Finance handoff lifecycle', async ({
  browser,
  request,
}) => {
  test.skip(
    process.env.QS_ACCEPTANCE_INTERACTIVE !== '1',
    'Set QS_ACCEPTANCE_INTERACTIVE=1 and complete each real CAPTCHA prompt.',
  );
  test.setTimeout(35 * 60_000);

  const api = process.env.E2E_API_URL?.trim() || 'http://127.0.0.1:5000';
  const projectId = required('QS_ACCEPTANCE_PROJECT_ID');
  const valuationId = required('QS_ACCEPTANCE_VALUATION_ID');
  const boqVersionId = required('QS_ACCEPTANCE_BOQ_VERSION_ID');
  const contractorId = required('QS_ACCEPTANCE_CONTRACTOR_ID');
  const consultantId = required('QS_ACCEPTANCE_CONSULTANT_ID');
  const measurementDate =
    process.env.QS_ACCEPTANCE_MEASUREMENT_DATE?.trim() ||
    `${new Date().toISOString().slice(0, 10)}T12:00:00Z`;

  const maker = await login(
    browser,
    'QS maker',
    required('QS_ACCEPTANCE_MAKER_USERNAME'),
    required('QS_ACCEPTANCE_MAKER_PASSWORD'),
  );
  const contractor = await login(
    browser,
    'contractor',
    required('QS_ACCEPTANCE_CONTRACTOR_USERNAME'),
    required('QS_ACCEPTANCE_CONTRACTOR_PASSWORD'),
  );
  const consultant = await login(
    browser,
    'consultant',
    required('QS_ACCEPTANCE_CONSULTANT_USERNAME'),
    required('QS_ACCEPTANCE_CONSULTANT_PASSWORD'),
  );
  const reviewer = await login(
    browser,
    'independent QS reviewer',
    required('QS_ACCEPTANCE_REVIEWER_USERNAME'),
    required('QS_ACCEPTANCE_REVIEWER_PASSWORD'),
  );
  const workflowReviewer = await login(
    browser,
    'independent workflow QS reviewer',
    required('QS_ACCEPTANCE_WORKFLOW_REVIEWER_USERNAME'),
    required('QS_ACCEPTANCE_WORKFLOW_REVIEWER_PASSWORD'),
  );
  const engineer = await login(
    browser,
    'engineering and project confirmer',
    required('QS_ACCEPTANCE_ENGINEER_USERNAME'),
    required('QS_ACCEPTANCE_ENGINEER_PASSWORD'),
  );
  const financeValidator = await login(
    browser,
    'Finance validator',
    required('QS_ACCEPTANCE_FINANCE_VALIDATOR_USERNAME'),
    required('QS_ACCEPTANCE_FINANCE_VALIDATOR_PASSWORD'),
  );
  const approver = await login(
    browser,
    'independent final approver',
    required('QS_ACCEPTANCE_CHECKER_USERNAME'),
    required('QS_ACCEPTANCE_CHECKER_PASSWORD'),
  );

  const measurementLookups = await parsed<{
    approvedBoqLines: Array<{ id: string }>;
  }>(
    await request.get(
      `${api}/api/quantity-survey/measurements/lookups?projectId=${projectId}`,
      { headers: maker.headers },
    ),
    'measurement lookups',
  );
  expect(measurementLookups.approvedBoqLines.length).toBeGreaterThan(0);
  const measurementBoqLineId =
    process.env.QS_ACCEPTANCE_BOQ_LINE_ID?.trim() ||
    measurementLookups.approvedBoqLines[0].id;
  expect(
    measurementLookups.approvedBoqLines.some(
      (line) => line.id.toLowerCase() === measurementBoqLineId.toLowerCase(),
    ),
    'The selected acceptance BoQ line must remain an approved project line.',
  ).toBe(true);

  let measurement = await parsed<{
    id: string;
    rowVersion: string;
    status: string;
    boqQuantity: number;
    totalMeasuredQuantity: number;
  }>(
    await request.post(`${api}/api/quantity-survey/measurements`, {
      headers: maker.headers,
      data: {
        clientRequestId: ids.measurement,
        projectId,
        projectBoqVersionLineId: measurementBoqLineId,
        projectDrawingId: null,
        sourceType: 1,
        title: 'QS phases 0-6 authenticated acceptance measurement',
        measurementDate,
        siteLocation: 'Authenticated acceptance test area',
        lines: [
          {
            clientLineKey: ids.measurementLine,
            sequence: 1,
            description: 'Verified work-done count',
            formulaType: 0,
            // Keep the golden-path certificate within the approved one-unit BoQ
            // line and the formal Works contract commitment. Over-measurement is
            // covered separately by the governed remeasurement/variation path.
            timesing: 1,
            length: null,
            width: null,
            height: null,
            isDeduction: false,
            notes:
              'Created through the authenticated QS API acceptance journey.',
          },
        ],
      },
    }),
    'create measurement',
  );

  if (measurement.status === 'Draft') {
    await parsed(
      await request.post(
        `${api}/api/quantity-survey/measurements/${measurement.id}/attachments`,
        {
          headers: maker.headers,
          multipart: {
            clientRequestId: ids.measurementEvidence,
            evidenceType: '3',
            title: 'Authenticated measurement evidence',
            file: {
              name: 'qs-acceptance-measurement.txt',
              mimeType: 'text/plain',
              buffer: Buffer.from(
                'QS acceptance evidence: site quantity independently recorded and retained through central DMS.',
              ),
            },
          },
        },
      ),
      'upload centrally scanned measurement evidence',
    );
    measurement = await parsed<typeof measurement>(
      await request.post(
        `${api}/api/quantity-survey/measurements/${measurement.id}/record`,
        {
          headers: maker.headers,
          data: {
            clientRequestId: ids.measurementRecord,
            rowVersion: measurement.rowVersion,
          },
        },
      ),
      'record measurement',
    );
  }
  expect(measurement.status).toBe('Recorded');
  expect(
    measurement.totalMeasuredQuantity,
    'The golden-path measurement must remain inside its approved BoQ quantity; over-measurement uses the governed remeasurement/variation flow.',
  ).toBeLessThanOrEqual(measurement.boqQuantity);

  const preview = await parsed<{
    id: string;
    rowVersion: string;
    status: string;
    approvalStatus: string;
    currentClaimedValue: number;
    currentCertifiedValue: number;
    certificateReady: boolean;
    evidence: unknown[];
    lines: Array<{
      projectBoqVersionLineId: string;
      measuredToDateQuantity: number;
      currentClaimedQuantity: number;
    }>;
  }>(
    await request.get(
      `${api}/api/quantity-survey/valuation-worksheets/${valuationId}?projectBoqVersionId=${boqVersionId}`,
      { headers: maker.headers },
    ),
    'valuation preview',
  );
  expect(
    preview.lines.some((line) => line.measuredToDateQuantity > 0),
    'Recorded measurement must feed the valuation source.',
  ).toBe(true);

  let worksheet = preview;
  if (worksheet.status === 'Draft') {
    worksheet = await parsed<typeof worksheet>(
      await request.put(
        `${api}/api/quantity-survey/valuation-worksheets/${valuationId}`,
        {
          headers: maker.headers,
          data: {
            clientRequestId: ids.worksheet,
            projectBoqVersionId: boqVersionId,
            contractorBusinessPartnerId: contractorId,
            consultantBusinessPartnerId: consultantId,
            rowVersion: worksheet.rowVersion || null,
            retentionPercentage: 5,
            lines: preview.lines.map((line) => ({
              projectBoqVersionLineId: line.projectBoqVersionLineId,
              currentClaimedQuantity: line.measuredToDateQuantity,
              currentCertifiedQuantity: line.measuredToDateQuantity,
              reviewNote:
                line.measuredToDateQuantity > 0
                  ? 'Matched to the recorded acceptance measurement.'
                  : null,
            })),
          },
        },
      ),
      'save governed valuation worksheet',
    );
  }
  expect(worksheet.currentClaimedValue).toBeGreaterThan(0);

  if (worksheet.status === 'Draft' && worksheet.evidence.length === 0) {
    await parsed(
      await request.post(
        `${api}/api/quantity-survey/valuation-worksheets/worksheets/${worksheet.id}/evidence`,
        {
          headers: maker.headers,
          multipart: {
            clientRequestId: ids.worksheetEvidence,
            evidenceType: '4',
            title: 'Authenticated valuation support',
            file: {
              name: 'qs-acceptance-valuation.csv',
              mimeType: 'text/csv',
              buffer: Buffer.from(
                'control,assertion\nmeasurement-reconciliation,Valuation quantities reconcile to the governed recorded measurement.\n',
              ),
            },
          },
        },
      ),
      'upload centrally scanned valuation evidence',
    );
    worksheet = await parsed<typeof worksheet>(
      await request.get(
        `${api}/api/quantity-survey/valuation-worksheets/${valuationId}?projectBoqVersionId=${boqVersionId}`,
        { headers: maker.headers },
      ),
      'reload valuation worksheet',
    );
  }

  if (worksheet.status === 'Draft') {
    const selfApproval = await request.post(
      `${api}/api/quantity-survey/valuation-worksheets/worksheets/${worksheet.id}/approve`,
      {
        headers: maker.headers,
        data: {
          clientRequestId: 'd6020000-0000-4000-8000-000000000019',
          rowVersion: worksheet.rowVersion,
          reason:
            'This direct maker approval must be denied and centrally audited.',
        },
      },
    );
    expect(selfApproval.status()).toBe(403);
  }

  worksheet = await parsed<typeof worksheet>(
    await request.get(
      `${api}/api/projects/external/my-projects/${projectId}/valuation-worksheets/${worksheet.id}`,
      { headers: contractor.headers },
    ),
    'load contractor valuation',
  );
  if (worksheet.status === 'Draft') {
    worksheet = await parsed<typeof worksheet>(
      await request.put(
        `${api}/api/projects/external/my-projects/${projectId}/valuation-worksheets/${worksheet.id}/claim`,
        {
          headers: contractor.headers,
          data: {
            clientRequestId: ids.contractorClaim,
            projectBoqVersionId: boqVersionId,
            contractorBusinessPartnerId: contractorId,
            consultantBusinessPartnerId: consultantId,
            rowVersion: worksheet.rowVersion,
            retentionPercentage: 5,
            lines: preview.lines.map((line) => ({
              projectBoqVersionLineId: line.projectBoqVersionLineId,
              currentClaimedQuantity: line.measuredToDateQuantity,
              currentCertifiedQuantity: 0,
              reviewNote: null,
            })),
          },
        },
      ),
      'save contractor claim',
    );
    worksheet = await parsed<typeof worksheet>(
      await request.post(
        `${api}/api/projects/external/my-projects/${projectId}/valuation-worksheets/${worksheet.id}/submit`,
        {
          headers: contractor.headers,
          data: {
            clientRequestId: ids.contractorSubmit,
            rowVersion: worksheet.rowVersion,
            notes: 'Authenticated contractor submission.',
            signature: {
              method: 0,
              attestation:
                'I confirm that this interim valuation claim is complete, accurate, and supported by the submitted evidence.',
              signedAt: new Date().toISOString(),
            },
          },
        },
      ),
      'submit contractor claim',
    );
  }
  expect([
    'ContractorSubmitted',
    'QsVetted',
    'ConsultantEndorsed',
    'PendingApproval',
    'Approved',
  ]).toContain(worksheet.status);

  if (worksheet.status === 'ContractorSubmitted') {
    worksheet = await parsed<typeof worksheet>(
      await request.put(
        `${api}/api/quantity-survey/valuation-worksheets/${valuationId}`,
        {
          headers: reviewer.headers,
          data: {
            clientRequestId: ids.qsReview,
            projectBoqVersionId: boqVersionId,
            contractorBusinessPartnerId: contractorId,
            consultantBusinessPartnerId: consultantId,
            rowVersion: worksheet.rowVersion,
            retentionPercentage: 5,
            lines: worksheet.lines.map((line) => ({
              projectBoqVersionLineId: line.projectBoqVersionLineId,
              currentClaimedQuantity: line.currentClaimedQuantity,
              currentCertifiedQuantity: line.currentClaimedQuantity,
              reviewNote:
                line.currentClaimedQuantity > 0
                  ? 'QS assessed the claimed quantity against the recorded measurement and evidence.'
                  : null,
            })),
          },
        },
      ),
      'record QS assessment of contractor claim',
    );
  }
  expect(worksheet.currentCertifiedValue).toBeGreaterThan(0);

  if (worksheet.status === 'UnderQsReview') {
    worksheet = await parsed<typeof worksheet>(
      await request.post(
        `${api}/api/quantity-survey/valuation-worksheets/worksheets/${worksheet.id}/vet`,
        {
          headers: reviewer.headers,
          data: {
            clientRequestId: ids.vet,
            rowVersion: worksheet.rowVersion,
            reason:
              'QS quantities and evidence reconcile to the recorded measurement.',
          },
        },
      ),
      'QS vetting',
    );
  }

  if (worksheet.status === 'QsVetted') {
    worksheet = await parsed<typeof worksheet>(
      await request.post(
        `${api}/api/projects/external/my-projects/${projectId}/valuation-worksheets/${worksheet.id}/endorse`,
        {
          headers: consultant.headers,
          data: {
            clientRequestId: ids.consultantEndorse,
            rowVersion: worksheet.rowVersion,
            notes: 'Independent authenticated consultant endorsement.',
            signature: {
              method: 0,
              attestation:
                'I confirm that I independently reviewed the QS-vetted interim valuation and endorse it for approval.',
              signedAt: new Date().toISOString(),
            },
          },
        },
      ),
      'consultant endorsement',
    );
  }

  if (worksheet.status === 'ConsultantEndorsed') {
    worksheet = await parsed<typeof worksheet>(
      await request.post(
        `${api}/api/quantity-survey/valuation-worksheets/worksheets/${worksheet.id}/submit-approval`,
        {
          headers: maker.headers,
          data: {
            clientRequestId: ids.worksheetSubmit,
            rowVersion: worksheet.rowVersion,
            reason: 'Submit the independently endorsed valuation for approval.',
          },
        },
      ),
      'submit valuation approval workflow',
    );
  }

  for (const stage of [
    {
      label: 'QS review',
      clientRequestId: ids.worksheetQsReview,
      actor: workflowReviewer,
    },
    {
      label: 'engineering and project confirmation',
      clientRequestId: ids.worksheetEngineeringConfirmation,
      actor: engineer,
    },
    {
      label: 'Finance validation',
      clientRequestId: ids.worksheetFinanceValidation,
      actor: financeValidator,
    },
    {
      label: 'independent final approval',
      clientRequestId: ids.worksheetFinalApproval,
      actor: approver,
    },
  ]) {
    if (worksheet.status === 'Approved') break;
    worksheet = await parsed<typeof worksheet>(
      await request.post(
        `${api}/api/quantity-survey/valuation-worksheets/worksheets/${worksheet.id}/approve`,
        {
          headers: stage.actor.headers,
          data: {
            clientRequestId: stage.clientRequestId,
            rowVersion: worksheet.rowVersion,
            reason: `Authenticated ${stage.label} of the valuation evidence and governed source lineage.`,
          },
        },
      ),
      `complete valuation ${stage.label}`,
    );
  }
  expect(worksheet).toMatchObject({
    status: 'Approved',
    approvalStatus: 'Approved',
    certificateReady: true,
  });

  let certificate = await parsed<{
    id: string;
    certificateNumber: string;
    rowVersion: string;
    status: string;
    approvalStatus: string;
    vendorInvoiceId: string | null;
    netCertifiedAmount: number;
    financeInvoiceAmount: number | null;
    financePaidAmount: number | null;
    financeBalanceAmount: number | null;
    reconciliationStatus: string;
  }>(
    await request.post(
      `${api}/api/quantity-survey/payment-certificates?projectId=${projectId}`,
      {
        headers: maker.headers,
        data: {
          clientRequestId: ids.certificate,
          valuationWorksheetId: worksheet.id,
          advanceRecoveryAgreementId: null,
          materialReconciliationId: null,
          paymentDueDate: '2026-09-10T12:00:00Z',
          advanceRecoveryAmount: 0,
          otherDeductionsAmount: 0,
          notes: 'Authenticated QS-to-Finance acceptance certificate.',
        },
      },
    ),
    'generate payment certificate',
  );

  if (certificate.status === 'Draft') {
    certificate = await parsed<typeof certificate>(
      await request.post(
        `${api}/api/quantity-survey/payment-certificates/${certificate.id}/submit`,
        {
          headers: maker.headers,
          data: {
            clientRequestId: ids.certificateSubmit,
            rowVersion: certificate.rowVersion,
            reason: 'Submit the reconciled payment certificate for approval.',
          },
        },
      ),
      'submit payment certificate',
    );
  }
  for (const stage of [
    {
      label: 'QS review',
      clientRequestId: ids.certificateQsReview,
      actor: workflowReviewer,
    },
    {
      label: 'engineering and project confirmation',
      clientRequestId: ids.certificateEngineeringConfirmation,
      actor: engineer,
    },
    {
      label: 'Finance validation',
      clientRequestId: ids.certificateFinanceValidation,
      actor: financeValidator,
    },
    {
      label: 'independent final approval',
      clientRequestId: ids.certificateFinalApproval,
      actor: approver,
    },
  ]) {
    if (certificate.status === 'Approved') break;
    certificate = await parsed<typeof certificate>(
      await request.post(
        `${api}/api/quantity-survey/payment-certificates/${certificate.id}/approve`,
        {
          headers: stage.actor.headers,
          data: {
            clientRequestId: stage.clientRequestId,
            rowVersion: certificate.rowVersion,
            reason: `Authenticated ${stage.label} of the payment certificate and governed valuation lineage.`,
          },
        },
      ),
      `complete payment-certificate ${stage.label}`,
    );
  }
  if (certificate.status === 'Approved' && !certificate.vendorInvoiceId) {
    certificate = await parsed<typeof certificate>(
      await request.post(
        `${api}/api/quantity-survey/payment-certificates/${certificate.id}/approve`,
        {
          headers: approver.headers,
          data: {
            clientRequestId: ids.certificateFinalApproval,
            rowVersion: certificate.rowVersion,
            reason:
              'Authenticated independent final approval of the payment certificate and governed valuation lineage.',
          },
        },
      ),
      'resume the committed certificate approval at its governed DMS and Finance boundaries',
    );
  }
  expect(certificate).toMatchObject({
    status: 'Approved',
    approvalStatus: 'Approved',
    reconciliationStatus: 'AwaitingFinancePosting',
  });
  expect(certificate.netCertifiedAmount).toBeGreaterThan(0);
  expect(certificate.vendorInvoiceId).toBeTruthy();
  expect(certificate.financeInvoiceAmount).toBeCloseTo(
    certificate.netCertifiedAmount,
    2,
  );
  expect(certificate.financePaidAmount).toBe(0);
  expect(certificate.financeBalanceAmount).toBeCloseTo(
    certificate.netCertifiedAmount,
    2,
  );

  const valuationHistory = await parsed<
    Array<{ action: string; actorName: string }>
  >(
    await request.get(
      `${api}/api/quantity-survey/valuation-worksheets/${valuationId}/history`,
      { headers: maker.headers },
    ),
    'valuation audit history',
  );
  expect(valuationHistory.map((value) => value.action)).toEqual(
    expect.arrayContaining([
      'CreateValuationWorksheet',
      'SubmitContractorValuation',
      'VetValuation',
      'EndorseValuation',
      'ApproveValuation',
    ]),
  );
  expect(
    new Set(valuationHistory.map((value) => value.actorName)).size,
    // Intermediate shared-workflow approvals are retained by the workflow
    // owner; the valuation revision owner records the final approval only.
    // These five actors cover maker, contractor, QS vetter, consultant and
    // final approver, while the successful stage requests above prove the
    // separate QS reviewer, engineer and Finance reviewer identities.
  ).toBeGreaterThanOrEqual(5);

  const certificateHistory = await parsed<Array<{ action: string }>>(
    await request.get(
      `${api}/api/quantity-survey/payment-certificates/${certificate.id}/history`,
      { headers: maker.headers },
    ),
    'payment-certificate audit history',
  );
  expect(certificateHistory.map((value) => value.action)).toEqual(
    expect.arrayContaining([
      'GeneratePaymentCertificate',
      'SubmitPaymentCertificate',
      'ApprovePaymentCertificate',
      'HandoffPaymentCertificateToAp',
    ]),
  );

  await maker.page.goto(`/development/projects/${projectId}/commercial-admin`, {
    waitUntil: 'domcontentloaded',
    timeout: 90_000,
  });
  await expect(
    maker.page.getByText('Loading project workspace...', { exact: true }),
  ).toBeHidden({ timeout: 120_000 });
  await expect(
    maker.page.getByRole('heading', {
      name: 'Payment Certificates',
      exact: true,
    }),
  ).toBeVisible({ timeout: 30_000 });
  await expect(
    maker.page.getByText(certificate.certificateNumber, { exact: true }),
  ).toBeVisible({ timeout: 30_000 });

  await Promise.all([
    maker.context.close(),
    contractor.context.close(),
    consultant.context.close(),
    reviewer.context.close(),
    workflowReviewer.context.close(),
    engineer.context.close(),
    financeValidator.context.close(),
    approver.context.close(),
  ]);
});
