import { mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { isAbsolute, resolve } from 'node:path';
import {
  expect,
  type Browser,
  type BrowserContext,
  type Page,
  type TestInfo,
} from '@playwright/test';

export type TenderActor =
  | 'PROCUREMENT_OFFICER'
  | 'SUPPLIER'
  | 'SUPPLIER_B'
  | 'EVALUATOR'
  | 'EVALUATOR_B'
  | 'APPROVER'
  | 'ETC_APPROVER'
  | 'CONTRACT_APPROVER'
  | 'UNAUTHORIZED';

export interface CommitteeScenario {
  tenderId: string;
  tenderNumber: string;
  bidId: string;
  bidNumber: string;
}

export interface TenderLifecycleFixture {
  schemaVersion: 1;
  runId: string;
  tenantCode?: string;
  prePublication: {
    approvedRequisitionId: string;
    requisitionNumber: string;
    title: string;
    description: string;
    tenderType: 'RFP' | 'ITB' | 'EOI';
    evaluationTemplateName: string;
    expectedItemDescriptions: string[];
    submissionDeadline: string;
    openingDate: string;
    publication: {
      advertisementReference: string;
      publicationChannel: string;
      tenderDocumentReference: string;
      tenderDocumentVersion: string;
      advertisementEvidenceReference: string;
      invitedSupplier: {
        businessPartnerId: string;
        name: string;
        email: string;
      };
    };
  };
  supplierLifecycle: {
    tenderId: string;
    tenderNumber: string;
    lotId: string;
    lotLabel: string;
    expectedItemDescription: string;
    offeredQuantity: number;
    unitPrice: number;
    deliveryDays: number;
    brand: string;
    model: string;
    technicalProposal: string;
    commercialProposal: string;
    paymentReference: string;
    requiredDocumentPaths?: Record<string, string>;
  };
  committee: {
    absent: CommitteeScenario;
    draft: CommitteeScenario;
    active: CommitteeScenario;
  };
  competition: {
    tenderId: string;
    tenderNumber: string;
    firstSupplierBidId: string;
    firstSupplierBidNumber: string;
    secondSupplierBidId: string;
    secondSupplierBidNumber: string;
    expectedWinnerBidNumber: string;
    opening: {
      mode: 'Controlled' | 'Legacy';
      evidenceReference: string;
      officerSignatureReference: string;
      observerName: string;
      observerSignatureReference: string;
    };
  };
  evaluation: {
    signatureReference: string;
    evidenceReference: string;
    technicalComments: string;
    commercialComments: string;
    overallComments: string;
    recommendation: string;
  };
  award: {
    justification: string;
    approvalNotes: string;
  };
  handoff: {
    mode: 'PO' | 'Contract';
    alternateApprovedAwardId: string;
    alternateMode: 'PO' | 'Contract';
    contractApprovedAwardId: string;
    contractAwardAmount: number;
    contractAwardCurrency: string;
    contractStartDate?: string;
    contractEndDate?: string;
  };
  regression: {
    tenderAmendmentDescription: string;
    tenderAmendmentChanges: string;
    crossTenantTenderId: string;
  };
}

export interface TenderLifecycleResult {
  schemaVersion: 1;
  runId: string;
  tenantCode?: string;
  status: 'InProgress' | 'Passed';
  updatedAtUtc: string;
  records: {
    sourcePurchaseRequisitionId?: string;
    prePublicationTenderId?: string;
    prePublicationTenderNumber?: string;
    tenderId?: string;
    tenderNumber?: string;
    supplierTenderId?: string;
    supplierTenderNumber?: string;
    evaluationTenderId?: string;
    evaluationTenderNumber?: string;
    supplierBidId?: string;
    supplierBidNumber?: string;
    evaluationBidId?: string;
    evaluationBidNumber?: string;
    secondSupplierBidId?: string;
    secondSupplierBidNumber?: string;
    evaluationId?: string;
    awardId?: string;
    purchaseOrderId?: string;
    contractId?: string;
    contractActivationId?: string;
    alternatePurchaseOrderId?: string;
    alternateContractId?: string;
    tenderRevisionId?: string;
    contractAmendmentId?: string;
  };
  checkpoints: Record<string, 'Passed' | 'Pending'>;
}

export interface CapturedHttpFailure {
  status: number;
  method: string;
  url: string;
  body: string;
}

export interface BrowserDiagnostics {
  httpFailures: CapturedHttpFailure[];
  pageErrors: string[];
  allowFailure(matcher: RegExp, statuses: number[]): void;
  assertClean(): Promise<void>;
  attach(testInfo: TestInfo): Promise<void>;
}

const requiredText = (value: unknown, path: string): string => {
  if (typeof value !== 'string' || !value.trim()) {
    throw new Error(`Tender lifecycle fixture requires a non-empty ${path}.`);
  }
  return value.trim();
};

const requiredPositiveNumber = (value: unknown, path: string): number => {
  if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) {
    throw new Error(`Tender lifecycle fixture requires ${path} to be greater than zero.`);
  }
  return value;
};

const validateCommitteeScenario = (
  scenario: CommitteeScenario,
  path: string,
): void => {
  requiredText(scenario?.tenderId, `${path}.tenderId`);
  requiredText(scenario?.tenderNumber, `${path}.tenderNumber`);
  requiredText(scenario?.bidId, `${path}.bidId`);
  requiredText(scenario?.bidNumber, `${path}.bidNumber`);
};

export const lifecycleEnabled = (): boolean =>
  process.env.TENDER_E2E_RUN === '1';

export const lifecycleDisabledReason = (): string =>
  'Destructive real-data lifecycle is opt-in. Set TENDER_E2E_RUN=1 and TENDER_E2E_FIXTURE to a fresh SQL Server fixture output file.';

export const loadTenderLifecycleFixture = (): TenderLifecycleFixture => {
  const configuredPath = process.env.TENDER_E2E_FIXTURE?.trim();
  if (!configuredPath) {
    throw new Error(
      'TENDER_E2E_FIXTURE must point to deterministic fresh-fixture JSON. See support/tender-lifecycle.fixture.example.json.',
    );
  }

  const fixturePath = isAbsolute(configuredPath)
    ? configuredPath
    : resolve(process.cwd(), configuredPath);
  let parsed: TenderLifecycleFixture;
  try {
    parsed = JSON.parse(readFileSync(fixturePath, 'utf8')) as TenderLifecycleFixture;
  } catch (error) {
    throw new Error(
      `Unable to read tender lifecycle fixture ${fixturePath}: ${error instanceof Error ? error.message : String(error)}`,
    );
  }

  if (parsed.schemaVersion !== 1) {
    throw new Error('Tender lifecycle fixture schemaVersion must be 1.');
  }
  requiredText(parsed.runId, 'runId');
  requiredText(parsed.prePublication?.approvedRequisitionId, 'prePublication.approvedRequisitionId');
  requiredText(parsed.prePublication?.requisitionNumber, 'prePublication.requisitionNumber');
  requiredText(parsed.prePublication?.title, 'prePublication.title');
  requiredText(parsed.prePublication?.description, 'prePublication.description');
  requiredText(parsed.prePublication?.evaluationTemplateName, 'prePublication.evaluationTemplateName');
  requiredText(parsed.prePublication?.submissionDeadline, 'prePublication.submissionDeadline');
  requiredText(parsed.prePublication?.openingDate, 'prePublication.openingDate');
  if (!Array.isArray(parsed.prePublication?.expectedItemDescriptions) || parsed.prePublication.expectedItemDescriptions.length < 2) {
    throw new Error('prePublication.expectedItemDescriptions must identify at least two approved PR lines.');
  }
  parsed.prePublication.expectedItemDescriptions.forEach((value, index) =>
    requiredText(value, `prePublication.expectedItemDescriptions[${index}]`),
  );
  requiredText(parsed.prePublication?.publication?.advertisementReference, 'prePublication.publication.advertisementReference');
  requiredText(parsed.prePublication?.publication?.publicationChannel, 'prePublication.publication.publicationChannel');
  requiredText(parsed.prePublication?.publication?.tenderDocumentReference, 'prePublication.publication.tenderDocumentReference');
  requiredText(parsed.prePublication?.publication?.tenderDocumentVersion, 'prePublication.publication.tenderDocumentVersion');
  requiredText(parsed.prePublication?.publication?.advertisementEvidenceReference, 'prePublication.publication.advertisementEvidenceReference');
  requiredText(
    parsed.prePublication?.publication?.invitedSupplier?.businessPartnerId,
    'prePublication.publication.invitedSupplier.businessPartnerId',
  );
  requiredText(
    parsed.prePublication?.publication?.invitedSupplier?.name,
    'prePublication.publication.invitedSupplier.name',
  );
  requiredText(
    parsed.prePublication?.publication?.invitedSupplier?.email,
    'prePublication.publication.invitedSupplier.email',
  );
  requiredText(parsed.supplierLifecycle?.tenderId, 'supplierLifecycle.tenderId');
  requiredText(parsed.supplierLifecycle?.tenderNumber, 'supplierLifecycle.tenderNumber');
  requiredText(parsed.supplierLifecycle?.lotId, 'supplierLifecycle.lotId');
  requiredText(parsed.supplierLifecycle?.lotLabel, 'supplierLifecycle.lotLabel');
  requiredText(
    parsed.supplierLifecycle?.expectedItemDescription,
    'supplierLifecycle.expectedItemDescription',
  );
  requiredPositiveNumber(
    parsed.supplierLifecycle?.offeredQuantity,
    'supplierLifecycle.offeredQuantity',
  );
  requiredPositiveNumber(parsed.supplierLifecycle?.unitPrice, 'supplierLifecycle.unitPrice');
  requiredPositiveNumber(
    parsed.supplierLifecycle?.deliveryDays,
    'supplierLifecycle.deliveryDays',
  );
  requiredText(parsed.supplierLifecycle?.brand, 'supplierLifecycle.brand');
  requiredText(parsed.supplierLifecycle?.model, 'supplierLifecycle.model');
  requiredText(
    parsed.supplierLifecycle?.paymentReference,
    'supplierLifecycle.paymentReference',
  );
  if ((parsed.supplierLifecycle?.technicalProposal || '').trim().length < 100) {
    throw new Error('supplierLifecycle.technicalProposal must contain at least 100 characters.');
  }
  if ((parsed.supplierLifecycle?.commercialProposal || '').trim().length < 100) {
    throw new Error('supplierLifecycle.commercialProposal must contain at least 100 characters.');
  }
  validateCommitteeScenario(parsed.committee?.absent, 'committee.absent');
  validateCommitteeScenario(parsed.committee?.draft, 'committee.draft');
  validateCommitteeScenario(parsed.committee?.active, 'committee.active');
  requiredText(parsed.competition?.tenderId, 'competition.tenderId');
  requiredText(parsed.competition?.tenderNumber, 'competition.tenderNumber');
  requiredText(parsed.competition?.firstSupplierBidId, 'competition.firstSupplierBidId');
  requiredText(parsed.competition?.firstSupplierBidNumber, 'competition.firstSupplierBidNumber');
  requiredText(parsed.competition?.secondSupplierBidId, 'competition.secondSupplierBidId');
  requiredText(parsed.competition?.secondSupplierBidNumber, 'competition.secondSupplierBidNumber');
  requiredText(parsed.competition?.expectedWinnerBidNumber, 'competition.expectedWinnerBidNumber');
  if (
    parsed.competition.expectedWinnerBidNumber !== parsed.competition.firstSupplierBidNumber &&
    parsed.competition.expectedWinnerBidNumber !== parsed.competition.secondSupplierBidNumber
  ) {
    throw new Error(
      'competition.expectedWinnerBidNumber must identify either the first or second supplier bid.',
    );
  }
  if (parsed.competition?.opening?.mode !== 'Controlled' && parsed.competition?.opening?.mode !== 'Legacy') {
    throw new Error('competition.opening.mode must be either Controlled or Legacy.');
  }
  requiredText(parsed.competition?.opening?.evidenceReference, 'competition.opening.evidenceReference');
  requiredText(parsed.competition?.opening?.officerSignatureReference, 'competition.opening.officerSignatureReference');
  requiredText(parsed.competition?.opening?.observerName, 'competition.opening.observerName');
  requiredText(parsed.competition?.opening?.observerSignatureReference, 'competition.opening.observerSignatureReference');
  if (
    parsed.committee.active.tenderId !== parsed.competition.tenderId ||
    parsed.committee.active.bidId !== parsed.competition.firstSupplierBidId
  ) {
    throw new Error('committee.active must identify the competition tender and first supplier bid.');
  }
  requiredText(parsed.evaluation?.signatureReference, 'evaluation.signatureReference');
  requiredText(parsed.evaluation?.evidenceReference, 'evaluation.evidenceReference');
  requiredText(parsed.evaluation?.technicalComments, 'evaluation.technicalComments');
  requiredText(parsed.evaluation?.commercialComments, 'evaluation.commercialComments');
  requiredText(parsed.evaluation?.overallComments, 'evaluation.overallComments');
  requiredText(parsed.evaluation?.recommendation, 'evaluation.recommendation');
  requiredText(parsed.award?.justification, 'award.justification');
  requiredText(parsed.award?.approvalNotes, 'award.approvalNotes');
  if (parsed.handoff?.mode !== 'PO' && parsed.handoff?.mode !== 'Contract') {
    throw new Error('handoff.mode must be either PO or Contract.');
  }
  requiredText(parsed.handoff?.alternateApprovedAwardId, 'handoff.alternateApprovedAwardId');
  requiredText(parsed.handoff?.contractApprovedAwardId, 'handoff.contractApprovedAwardId');
  requiredPositiveNumber(parsed.handoff?.contractAwardAmount, 'handoff.contractAwardAmount');
  requiredText(parsed.handoff?.contractAwardCurrency, 'handoff.contractAwardCurrency');
  if (parsed.handoff?.alternateMode !== 'PO' && parsed.handoff?.alternateMode !== 'Contract') {
    throw new Error('handoff.alternateMode must be either PO or Contract.');
  }
  if (parsed.handoff.alternateMode === parsed.handoff.mode) {
    throw new Error('handoff.mode and handoff.alternateMode must cover PO and Contract separately.');
  }
  if (parsed.handoff.mode === 'Contract') {
    requiredText(parsed.handoff.contractStartDate, 'handoff.contractStartDate');
    requiredText(parsed.handoff.contractEndDate, 'handoff.contractEndDate');
  }
  if (parsed.handoff.alternateMode === 'Contract') {
    requiredText(parsed.handoff.contractStartDate, 'handoff.contractStartDate');
    requiredText(parsed.handoff.contractEndDate, 'handoff.contractEndDate');
  }
  requiredText(parsed.regression?.tenderAmendmentDescription, 'regression.tenderAmendmentDescription');
  requiredText(parsed.regression?.tenderAmendmentChanges, 'regression.tenderAmendmentChanges');
  requiredText(parsed.regression?.crossTenantTenderId, 'regression.crossTenantTenderId');

  return parsed;
};

export const writeTenderLifecycleResult = (
  result: TenderLifecycleResult,
): void => {
  const configuredPath = process.env.TENDER_E2E_RESULT?.trim();
  if (!configuredPath) {
    throw new Error(
      'TENDER_E2E_RESULT must identify the non-secret lifecycle result JSON consumed by the SQL verifier.',
    );
  }
  const resultPath = isAbsolute(configuredPath)
    ? configuredPath
    : resolve(process.cwd(), configuredPath);
  const directory = resolve(resultPath, '..');
  mkdirSync(directory, { recursive: true });
  const temporaryPath = `${resultPath}.tmp`;
  writeFileSync(temporaryPath, `${JSON.stringify(result, null, 2)}\n`, 'utf8');
  renameSync(temporaryPath, resultPath);
};

const actorEnv = (actor: TenderActor, suffix: string): string | undefined =>
  process.env[`TENDER_E2E_${actor}_${suffix}`]?.trim() || undefined;

const selectTenantIfRequired = async (
  page: Page,
  tenantCode?: string,
): Promise<void> => {
  if (!page.url().includes('/tenant-select')) return;

  if (tenantCode) {
    const matchingCard = page
      .locator('[data-testid="tenant-card"], article, .card')
      .filter({ hasText: tenantCode })
      .first();
    if (await matchingCard.isVisible().catch(() => false)) {
      await matchingCard.getByRole('button', { name: /select|continue/i }).click();
    } else {
      await page.getByText(tenantCode, { exact: false }).first().click();
    }
  } else {
    const selectButtons = page.getByRole('button', { name: /select|continue/i });
    await expect(
      selectButtons,
      'Multiple tenants require fixture.tenantCode or TENDER_E2E_<ACTOR>_TENANT_CODE.',
    ).toHaveCount(1);
    await selectButtons.first().click();
  }
  await page.waitForURL((url) => !url.pathname.includes('/tenant-select'), {
    timeout: 30_000,
  });
};

export const newAuthenticatedActor = async (
  browser: Browser,
  actor: TenderActor,
  fixtureTenantCode?: string,
): Promise<{ context: BrowserContext; page: Page }> => {
  const storageState = actorEnv(actor, 'STORAGE_STATE');
  const context = await browser.newContext({
    baseURL: process.env.E2E_BASE_URL || 'http://localhost:3000',
    ignoreHTTPSErrors: true,
    ...(storageState ? { storageState } : {}),
  });
  const page = await context.newPage();

  if (storageState) {
    await page.goto('/dashboard');
    if (page.url().includes('/login')) {
      await context.close();
      throw new Error(
      `${actor} storage state is expired. Regenerate ${actorEnv(actor, 'STORAGE_STATE')}.`,
      );
    }
    await selectTenantIfRequired(
      page,
      actorEnv(actor, 'TENANT_CODE') || fixtureTenantCode,
    );
    return { context, page };
  }

  const username = actorEnv(actor, 'USERNAME');
  const password = actorEnv(actor, 'PASSWORD');
  if (!username || !password) {
    await context.close();
    throw new Error(
      `${actor} requires either TENDER_E2E_${actor}_STORAGE_STATE or both TENDER_E2E_${actor}_USERNAME and TENDER_E2E_${actor}_PASSWORD.`,
    );
  }

  await page.goto('/login');
  await page.locator('input[name="username"], input[type="text"]').first().fill(username);
  await page.locator('input[name="password"], input[type="password"]').first().fill(password);

  if (await page.locator('iframe[title*="reCAPTCHA" i], iframe[src*="recaptcha"]').count()) {
    await context.close();
    throw new Error(
      `${actor} login requires CAPTCHA. Supply a freshly generated TENDER_E2E_${actor}_STORAGE_STATE file; automated CAPTCHA bypass is intentionally prohibited.`,
    );
  }

  const loginResponse = page.waitForResponse(
    (response) =>
      /\/api\/auth\/login(?:\?|$)/i.test(response.url()) &&
      response.request().method() === 'POST',
    { timeout: 30_000 },
  );
  await page.getByRole('button', { name: /sign in|log in/i }).click();
  const response = await loginResponse;
  if (!response.ok()) {
    await context.close();
    throw new Error(`${actor} authentication failed with HTTP ${response.status()}.`);
  }
  await page.waitForURL((url) => !url.pathname.includes('/login'), {
    timeout: 30_000,
  });
  await selectTenantIfRequired(
    page,
    actorEnv(actor, 'TENANT_CODE') || fixtureTenantCode,
  );

  const hasToken = await page.evaluate(
    () => Boolean(localStorage.getItem('authToken') || localStorage.getItem('token')),
  );
  expect(hasToken, `${actor} login must issue the application's real bearer token.`).toBe(true);
  return { context, page };
};

const safeBody = async (response: import('@playwright/test').Response): Promise<string> => {
  try {
    const contentType = response.headers()['content-type'] || '';
    if (!/json|problem\+json|text/i.test(contentType)) return '<non-text response>';
    return (await response.text()).slice(0, 4_000);
  } catch {
    return '<response body unavailable>';
  }
};

export const monitorProcurementBrowser = (page: Page): BrowserDiagnostics => {
  const httpFailures: CapturedHttpFailure[] = [];
  const pageErrors: string[] = [];
  const allowed: Array<{ matcher: RegExp; statuses: number[] }> = [];

  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('response', async (response) => {
    if (response.status() < 400 || !/\/api\/procurement\//i.test(response.url())) return;
    httpFailures.push({
      status: response.status(),
      method: response.request().method(),
      url: response.url(),
      body: await safeBody(response),
    });
  });

  const unexpectedFailures = (): CapturedHttpFailure[] =>
    httpFailures.filter(
      (failure) =>
        !allowed.some(
          (entry) => entry.matcher.test(failure.url) && entry.statuses.includes(failure.status),
        ),
    );

  return {
    httpFailures,
    pageErrors,
    allowFailure(matcher, statuses) {
      allowed.push({ matcher, statuses });
    },
    async assertClean() {
      expect(pageErrors, 'No browser runtime errors are permitted.').toEqual([]);
      expect(unexpectedFailures(), 'No unexpected procurement HTTP failures are permitted.').toEqual([]);
    },
    async attach(testInfo) {
      await testInfo.attach('procurement-http-failures.json', {
        body: Buffer.from(JSON.stringify(httpFailures, null, 2)),
        contentType: 'application/json',
      });
      await testInfo.attach('browser-page-errors.json', {
        body: Buffer.from(JSON.stringify(pageErrors, null, 2)),
        contentType: 'application/json',
      });
    },
  };
};

export const waitForProcurementResponse = (
  page: Page,
  method: string,
  endpoint: RegExp,
  acceptedStatuses: number[],
) =>
  page.waitForResponse(
    (response) =>
      response.request().method() === method &&
      endpoint.test(new URL(response.url()).pathname) &&
      acceptedStatuses.includes(response.status()),
    { timeout: 45_000 },
  );

export const responseJson = async <T>(
  response: import('@playwright/test').Response,
): Promise<T> => {
  const body = await response.text();
  try {
    return JSON.parse(body) as T;
  } catch {
    throw new Error(
      `${response.request().method()} ${response.url()} returned HTTP ${response.status()} with non-JSON body: ${body.slice(0, 500)}`,
    );
  }
};

export const problemCode = async (
  response: import('@playwright/test').Response,
): Promise<string> => {
  const body = await responseJson<{
    code?: string;
    extensions?: { code?: string };
    Extensions?: { code?: string };
  }>(response);
  return body.code || body.extensions?.code || body.Extensions?.code || '';
};
