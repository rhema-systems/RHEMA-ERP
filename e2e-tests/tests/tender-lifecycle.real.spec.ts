import { expect, test, type Browser, type Page, type Response, type TestInfo } from '@playwright/test';
import {
  lifecycleDisabledReason,
  lifecycleEnabled,
  loadTenderLifecycleFixture,
  monitorProcurementBrowser,
  newAuthenticatedActor,
  problemCode,
  responseJson,
  writeTenderLifecycleResult,
  type BrowserDiagnostics,
  type TenderActor,
  type TenderLifecycleFixture,
  type TenderLifecycleResult,
} from '../support/tender-lifecycle';

type LifecycleState = {
  prePublicationTenderId?: string;
  prePublicationTenderNumber?: string;
  supplierBidId?: string;
  supplierBidNumber?: string;
  bidId?: string;
  bidNumber?: string;
  secondSupplierEvaluationId?: string;
  evaluationId?: string;
  awardId?: string;
  awardAmount?: number;
  awardCurrency?: string;
  handoffId?: string;
  purchaseOrderId?: string;
  contractId?: string;
  contractNumber?: string;
  contractActivationId?: string;
  alternateHandoffCompleted?: boolean;
  evaluationPendingReason?: string;
};

type BidDto = {
  id: string;
  bidNumber: string;
  status: string;
  totalBidAmount?: number;
  selectedLotIds?: string[];
  items?: Array<{
    tenderItemDescription: string;
    offeredQuantity: number;
    unitPrice: number;
    deliveryDays?: number;
    brand?: string;
    model?: string;
  }>;
};

type AwardDto = { id: string; status: string; awardedAmount: number; currency: string };
type TenderDto = { id: string; tenderNumber: string; status: string };
type TenderDocumentReadinessDto = {
  hasRegister: boolean;
  effectiveTemplateVersionId?: string;
  currencyCode?: string;
  issuanceCount: number;
  ready: boolean;
};
type TenderDocumentRegisterDto = {
  id: string;
  sourceId: string;
  rowVersion: string;
};
type ContractDto = {
  id: string;
  contractNumber: string;
  status: string;
  tenderAwardId: string;
  tenderId: string;
  businessPartnerId: string;
  tenderBidId?: string;
  contractValue: number;
  currency: string;
  rowVersion: string;
};
type ContractActivationDto = {
  id: string;
  contractId: string;
  status: string;
  workflowInstanceId?: string;
  submittedById: string;
  decidedById?: string;
  integrityHash: string;
  rowVersion: string;
};
type ContractActivationOverviewDto = {
  contractId: string;
  isReady: boolean;
  canSubmit: boolean;
  requiredEvidenceKeys: string[];
};
type TenderControlDto = {
  status: string;
  rowVersion: string;
  recommendedBidId?: string;
  awardBidId?: string;
  contractReference?: string;
  bidderAcceptanceReference?: string;
};

const state: LifecycleState = {};
let fixture: TenderLifecycleFixture;
let lifecycleResult: TenderLifecycleResult;

const recordCheckpoint = (
  checkpoint: string,
  records: Partial<TenderLifecycleResult['records']> = {},
  status?: TenderLifecycleResult['status'],
): void => {
  const checkpoints: TenderLifecycleResult['checkpoints'] = {
    ...lifecycleResult.checkpoints,
    [checkpoint]: 'Passed',
  };
  const requestedStatus = status ?? lifecycleResult.status;
  lifecycleResult = {
    ...lifecycleResult,
    status:
      requestedStatus === 'Passed' && Object.values(checkpoints).includes('Pending')
        ? 'InProgress'
        : requestedStatus,
    updatedAtUtc: new Date().toISOString(),
    records: { ...lifecycleResult.records, ...records },
    checkpoints,
  };
  writeTenderLifecycleResult(lifecycleResult);
};

const recordPending = (checkpoint: string): void => {
  lifecycleResult = {
    ...lifecycleResult,
    status: 'InProgress',
    updatedAtUtc: new Date().toISOString(),
    checkpoints: { ...lifecycleResult.checkpoints, [checkpoint]: 'Pending' },
  };
  writeTenderLifecycleResult(lifecycleResult);
};

const skipEvaluationDependentGate = (checkpoint: string): void => {
  if (!state.evaluationPendingReason) return;
  recordPending(checkpoint);
  test.skip(true, `Pending: ${state.evaluationPendingReason}`);
};

const closeActor = async (
  context: Awaited<ReturnType<typeof newAuthenticatedActor>>['context'],
  diagnostics: BrowserDiagnostics,
  testInfo: TestInfo,
): Promise<void> => {
  await diagnostics.attach(testInfo);
  await context.close();
};

const withActor = async (
  browser: Browser,
  actor: TenderActor,
  testInfo: TestInfo,
  action: (page: Page, diagnostics: BrowserDiagnostics) => Promise<void>,
): Promise<void> => {
  const { context, page } = await newAuthenticatedActor(
    browser,
    actor,
    fixture.tenantCode,
  );
  const diagnostics = monitorProcurementBrowser(page);
  try {
    await action(page, diagnostics);
  } finally {
    await closeActor(context, diagnostics, testInfo);
  }
};

const successfulProcurementMutation = (
  page: Page,
  method: string | string[],
  endpoint: RegExp,
): Promise<Response> => {
  const methods = Array.isArray(method) ? method : [method];
  return page
    .waitForResponse(
      (response) =>
        methods.includes(response.request().method()) &&
        endpoint.test(new URL(response.url()).pathname),
      { timeout: 45_000 },
    )
    .then(async (response) => {
      if (response.ok()) return response;
      const body = (await response.text()).slice(0, 4_000);
      throw new Error(
        `${response.request().method()} ${new URL(response.url()).pathname} failed ` +
          `with HTTP ${response.status()}: ${body}`,
      );
    });
};

const getActorToken = (page: Page): Promise<string | null> =>
  page.evaluate(() => localStorage.getItem('authToken') || localStorage.getItem('token'));

const acceptanceApiUrl = (path: string): string => {
  const apiBaseUrl = process.env.E2E_API_URL?.replace(/\/$/, '');
  if (!apiBaseUrl) throw new Error('E2E_API_URL is required for direct authenticated API assertions.');
  return `${apiBaseUrl}${path}`;
};

const getSameOriginJson = async <T>(page: Page, path: string): Promise<T> => {
  const token = await getActorToken(page);
  const response = await page.request.get(acceptanceApiUrl(path), {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });
  const body = await response.text();
  if (!response.ok()) {
    throw new Error(`GET ${path} failed with HTTP ${response.status()}: ${body.slice(0, 500)}`);
  }
  return JSON.parse(body) as T;
};

const sameOriginMutation = async <T>(
  page: Page,
  path: string,
  method: 'POST' | 'PUT' | 'DELETE',
  data?: unknown,
): Promise<{ status: number; body: T }> => {
  const token = await getActorToken(page);
  const response = await page.request.fetch(acceptanceApiUrl(path), {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    ...(data === undefined ? {} : { data }),
  });
  const text = await response.text();
  let body: unknown = {};
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = { detail: text };
    }
  }
  return { status: response.status(), body: body as T };
};

const inputBesideLabel = (scope: Page | ReturnType<Page['getByRole']>, label: RegExp) =>
  scope
    .locator('label')
    .filter({ hasText: label })
    .first()
    .locator('xpath=following-sibling::input[1]');

const textareaBesideLabel = (scope: Page | ReturnType<Page['getByRole']>, label: RegExp) =>
  scope
    .locator('label')
    .filter({ hasText: label })
    .first()
    .locator('xpath=following-sibling::textarea[1]');

const goToNextBidStep = async (page: Page): Promise<void> => {
  await page.getByRole('button', { name: /^Next$/ }).click();
};

const createAndActivateRealContract = async (
  browser: Browser,
  testInfo: TestInfo,
): Promise<void> => {
  const awardId = state.awardId;
  expect(awardId, 'The controlled award must materialize a real TenderAward.').toBeTruthy();
  expect(state.awardAmount).toBeGreaterThan(0);
  expect(state.awardCurrency).toBeTruthy();
  let activation: ContractActivationDto | undefined;
  await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
    await page.goto('/dashboard');
    const created = await sameOriginMutation<ContractDto>(page, '/api/procurement/Contracts', 'POST', {
      tenderAwardId: awardId,
      contractTitle: `Tender lifecycle contract ${fixture.runId}`,
      contractType: 'Supply',
      contractValue: state.awardAmount,
      currency: state.awardCurrency,
      startDate: fixture.handoff.contractStartDate,
      endDate: fixture.handoff.contractEndDate,
      paymentTerms: 'Net 30 days',
      scopeOfWork: 'Deliver the exact independently awarded tender scope.',
      notes: `Real award-to-contract handoff ${fixture.runId}`,
    });
    expect(created.status).toBe(201);
    expect(created.body.status).toBe('Draft');
    expect(created.body.tenderAwardId).toBe(awardId);
    expect(created.body.contractValue).toBe(state.awardAmount);
    state.contractId = created.body.id;
    state.contractNumber = created.body.contractNumber;

    const overview = await getSameOriginJson<ContractActivationOverviewDto>(
      page, `/api/procurement/contract-activations/contracts/${created.body.id}`,
    );
    expect(overview.isReady).toBe(true);
    expect(overview.canSubmit).toBe(true);
    expect(overview.requiredEvidenceKeys).toHaveLength(0);
    const submitted = await sameOriginMutation<ContractActivationDto>(
      page,
      `/api/procurement/contract-activations/contracts/${created.body.id}/submit`,
      'POST',
      {
        reason: 'Independent contract activation acceptance.',
        idempotencyKey: `TE2E-${fixture.runId}-CONTRACT-ACTIVATION`,
        contractRowVersion: created.body.rowVersion,
        evidence: [],
      },
    );
    expect(submitted.status).toBe(201);
    expect(submitted.body.status).toBe('PendingApproval');
    expect(submitted.body.workflowInstanceId).toBeTruthy();
    activation = submitted.body;
    state.contractActivationId = submitted.body.id;
    await diagnostics.assertClean();
  });

  await withActor(browser, 'CONTRACT_APPROVER', testInfo, async (page, diagnostics) => {
    await page.goto('/dashboard');
    expect(activation).toBeTruthy();
    const decided = await sameOriginMutation<ContractActivationDto>(
      page, `/api/procurement/contract-activations/${activation!.id}/decision`, 'POST',
      { approved: true, comment: 'Independent legal approval.', rowVersion: activation!.rowVersion },
    );
    expect(decided.status).toBe(200);
    expect(decided.body.status).toBe('Approved');
    expect(decided.body.decidedById).not.toBe(decided.body.submittedById);
    const activated = await sameOriginMutation<ContractDto>(
      page, `/api/procurement/contract-activations/${activation!.id}/activate`, 'POST',
      {
        contractorSignatoryName: 'Tender E2E Supplier Signatory',
        comment: 'Activate the independently approved contract.',
        rowVersion: decided.body.rowVersion,
      },
    );
    expect(activated.status).toBe(200);
    expect(activated.body.status).toBe('Active');
    await diagnostics.assertClean();
    recordCheckpoint('real-contract-handoff-activated', {
      contractId: activated.body.id,
      contractActivationId: activation!.id,
    });
  });
};

test.describe.serial('real SQL Server tender lifecycle', () => {
  test.skip(!lifecycleEnabled(), lifecycleDisabledReason());
  test.setTimeout(3 * 60_000);

  test.beforeAll(() => {
    fixture = loadTenderLifecycleFixture();
    lifecycleResult = {
      schemaVersion: 1,
      runId: fixture.runId,
      tenantCode: fixture.tenantCode,
      status: 'InProgress',
      updatedAtUtc: new Date().toISOString(),
      records: {
        tenderId: fixture.competition.tenderId,
        tenderNumber: fixture.competition.tenderNumber,
        supplierTenderId: fixture.supplierLifecycle.tenderId,
        supplierTenderNumber: fixture.supplierLifecycle.tenderNumber,
        evaluationTenderId: fixture.competition.tenderId,
        evaluationTenderNumber: fixture.competition.tenderNumber,
        evaluationBidId: fixture.competition.firstSupplierBidId,
        evaluationBidNumber: fixture.competition.firstSupplierBidNumber,
      },
      checkpoints: {},
    };
    writeTenderLifecycleResult(lifecycleResult);
  });

  test('00 - validates deterministic fixture and nine independent actor sessions', async ({
    browser,
  }, testInfo) => {
    expect(fixture.schemaVersion).toBe(1);
    expect(fixture.runId).not.toMatch(/sample|example|placeholder/i);

    for (const actor of [
      'PROCUREMENT_OFFICER',
      'SUPPLIER',
      'SUPPLIER_B',
      'EVALUATOR',
      'EVALUATOR_B',
      'APPROVER',
      'ETC_APPROVER',
      'CONTRACT_APPROVER',
      'UNAUTHORIZED',
    ] as const) {
      await withActor(browser, actor, testInfo, async (page, diagnostics) => {
        await expect(page).not.toHaveURL(/\/login/);
        const actorIdentity = await page.evaluate(() => {
          const stored = localStorage.getItem('user');
          if (!stored) return null;
          const user = JSON.parse(stored) as { id?: string; username?: string };
          return { id: user.id || '', username: user.username || '' };
        });
        expect(actorIdentity?.id || actorIdentity?.username, `${actor} must have an authenticated identity.`).toBeTruthy();
        await diagnostics.assertClean();
      });
    }
    recordCheckpoint('actor-isolation');
  });

  test('00A - approved PR opens the tender wizard and creates one source-linked draft', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      const source = fixture.prePublication;
      await page.goto(`/procurement/purchase-requisitions/${source.approvedRequisitionId}`);
      await expect(
        page.getByRole('heading', { name: source.requisitionNumber, exact: true }),
      ).toBeVisible({ timeout: 45_000 });
      await expect(page.getByText(/Ready for sourcing/i).first()).toBeVisible();
      const createTender = page.getByRole('button', { name: 'Create Tender' });
      await expect(createTender).toBeEnabled();
      await createTender.click();
      await expect(page).toHaveURL(
        new RegExp(`/procurement/tenders/new\\?fromRequisitionId=${source.approvedRequisitionId}`),
      );

      const titleInput = page.locator('#title');
      await expect(titleInput).toHaveValue(`Tender for ${source.requisitionNumber}`, {
        timeout: 45_000,
      });
      await titleInput.fill(source.title);
      await expect(titleInput).toHaveValue(source.title);
      await page.locator('#description').fill(source.description);
      await page.locator('#submissionDeadline').fill(source.submissionDeadline);
      await page.locator('#openingDate').fill(source.openingDate);
      await page.locator('#evaluationTemplate').click();
      await page.getByRole('option', { name: new RegExp(source.evaluationTemplateName, 'i') }).click();

      const createResponse = successfulProcurementMutation(
        page,
        'POST',
        /\/api\/procurement\/Tenders$/i,
      );
      await page.getByRole('button', { name: /^Next$/ }).click();
      const tender = await responseJson<TenderDto>(await createResponse);
      state.prePublicationTenderId = tender.id;
      state.prePublicationTenderNumber = tender.tenderNumber;
      expect(tender.status).toMatch(/draft/i);
      await expect(page.getByText('Tender Lots', { exact: true }).first()).toBeVisible();
      for (const item of source.expectedItemDescriptions) {
        await expect(page.getByText(item, { exact: false }).first()).toBeVisible();
      }
      recordCheckpoint('pr-tender-draft-created', {
        sourcePurchaseRequisitionId: source.approvedRequisitionId,
        prePublicationTenderId: tender.id,
        prePublicationTenderNumber: tender.tenderNumber,
      });
      await diagnostics.assertClean();
    });
  });

  test('00B - tender draft reopens with template, dates, lots, and approved PR lines intact', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      const source = fixture.prePublication;
      await page.goto(`/procurement/tenders/${state.prePublicationTenderId}/edit`);
      await expect(page.getByRole('heading', { name: 'Edit Tender' })).toBeVisible({ timeout: 45_000 });
      await expect(page.getByText(/Step 2 of 7: Tender Lots/i)).toBeVisible();
      for (const item of source.expectedItemDescriptions) {
        await expect(page.getByText(item, { exact: false }).first()).toBeVisible();
      }
      await page.getByRole('button', { name: /^Previous$/ }).click();
      await expect(page.locator('#title')).toHaveValue(source.title, { timeout: 45_000 });
      await expect(page.locator('#description')).toHaveValue(source.description);
      await expect(page.locator('#submissionDeadline')).toHaveValue(source.submissionDeadline);
      await expect(page.locator('#openingDate')).toHaveValue(source.openingDate);
      await expect(page.locator('#evaluationTemplate')).toContainText(source.evaluationTemplateName);
      await page.getByRole('button', { name: /^Next$/ }).click();
      for (const item of source.expectedItemDescriptions) {
        await expect(page.getByText(item, { exact: false }).first()).toBeVisible();
      }
      const saveResponse = successfulProcurementMutation(
        page,
        'PUT',
        new RegExp(`/api/procurement/Tenders/${state.prePublicationTenderId}$`, 'i'),
      );
      await page.getByRole('button', { name: 'Save Draft' }).click();
      expect((await saveResponse).ok()).toBe(true);
      await diagnostics.assertClean();
      recordCheckpoint('tender-draft-reopened');
    });
  });

  test('00C - maker submits and independent approver approves the prepared tender', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      await page.goto(`/procurement/tenders/${state.prePublicationTenderId}`);
      await page.getByRole('tab', { name: /Workflow/i }).click();
      const submitResponse = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/Tenders/${state.prePublicationTenderId}/submit$`, 'i'),
      );
      await page.getByRole('button', { name: 'Submit for Approval' }).click();
      await page.getByRole('dialog').getByRole('button', { name: 'Submit for Approval' }).click();
      expect((await submitResponse).ok()).toBe(true);
      await expect(page.getByText('Submitted', { exact: true }).first()).toBeVisible();
      await diagnostics.assertClean();
    });

    await withActor(browser, 'APPROVER', testInfo, async (page, diagnostics) => {
      await page.goto(`/procurement/tenders/${state.prePublicationTenderId}`);
      await page.getByRole('tab', { name: /Workflow/i }).click();
      const approveResponse = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/Tenders/${state.prePublicationTenderId}/approve$`, 'i'),
      );
      await page.getByRole('button', { name: /^Approve$/ }).first().click();
      await page.getByRole('dialog').getByRole('button', { name: /^Approve$/ }).click();
      expect((await approveResponse).ok()).toBe(true);
      await expect(page.getByText('Approved', { exact: true }).first()).toBeVisible();
      await diagnostics.assertClean();
    });
    recordCheckpoint('tender-independent-approval');
  });

  test('00D - approved tender publishes with controlled dates and statutory references', async ({
    browser,
  }, testInfo) => {
    const publication = fixture.prePublication.publication;
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      const source = fixture.prePublication;
      const tenderId = state.prePublicationTenderId;
      expect(tenderId, 'The browser-created tender ID from 00A is required.').toBeTruthy();

      const readinessPath =
        `/api/procurement/tender-document-register/readiness?sourceType=Tender&sourceId=${tenderId}`;
      const readiness = await getSameOriginJson<TenderDocumentReadinessDto>(page, readinessPath);
      expect(readiness.hasRegister).toBe(false);
      expect(readiness.ready, 'The exact Published tender-document template must be effective.').toBe(true);
      expect(readiness.effectiveTemplateVersionId).toBeTruthy();
      expect(readiness.currencyCode).toBeTruthy();

      const deadlineUtc = new Date(source.submissionDeadline).toISOString();
      const openingUtc = new Date(source.openingDate).toISOString();
      const register = await sameOriginMutation<TenderDocumentRegisterDto>(
        page,
        '/api/procurement/tender-document-register/bind',
        'POST',
        {
          sourceType: 'Tender',
          sourceId: tenderId,
          templateVersionId: readiness.effectiveTemplateVersionId,
          submissionDeadlineUtc: deadlineUtc,
          openingScheduledAtUtc: openingUtc,
          bidValidityUntilUtc: new Date(new Date(deadlineUtc).getTime() + 30 * 86_400_000).toISOString(),
          feeMode: 'Free',
          feeAmount: 0,
          currencyCode: readiness.currencyCode,
        },
      );
      expect(register.status, JSON.stringify(register.body)).toBe(201);
      expect(register.body.sourceId).toBe(tenderId);
      expect(register.body.rowVersion).toBeTruthy();

      const invitedSupplier = publication.invitedSupplier;
      const invitation = await sameOriginMutation<{ message?: string }>(
        page,
        `/api/procurement/Tenders/${tenderId}/invitations`,
        'POST',
        {
          businessPartnerIds: [invitedSupplier.businessPartnerId],
          externalRecipientEmails: [],
          sendNotifications: false,
        },
      );
      expect(invitation.status, JSON.stringify(invitation.body)).toBe(200);

      const issuance = await sameOriginMutation<{ id: string; businessPartnerId: string }>(
        page,
        '/api/procurement/tender-document-register/issue',
        'POST',
        {
          sourceType: 'Tender',
          sourceId: tenderId,
          businessPartnerId: invitedSupplier.businessPartnerId,
          recipientName: invitedSupplier.name,
          recipientEmail: invitedSupplier.email,
          amountPaid: 0,
          receiptNumber: `TE2E-${fixture.runId}-PUBLICATION-ISSUE`,
          issueChannel: 'ExternalPortal',
          evidenceReference: `TE2E-${fixture.runId}-PUBLICATION-ISSUE-EVIDENCE`,
          registerRowVersion: register.body.rowVersion,
        },
      );
      expect(issuance.status, JSON.stringify(issuance.body)).toBe(201);
      expect(issuance.body.id).toBeTruthy();
      expect(issuance.body.businessPartnerId).toBe(invitedSupplier.businessPartnerId);

      const dispatchReady = await getSameOriginJson<TenderDocumentReadinessDto>(page, readinessPath);
      expect(dispatchReady.hasRegister).toBe(true);
      expect(dispatchReady.ready).toBe(true);
      expect(dispatchReady.issuanceCount).toBe(1);

      await page.goto(`/procurement/tenders/${tenderId}`);
      await expect(page.getByText('Invitations (1)', { exact: true })).toBeVisible();
      await page.getByRole('button', { name: 'Publish' }).click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toContainText('Publish Tender');
      const textInputs = dialog.locator('input');
      await inputBesideLabel(dialog, /Submission deadline/i).fill(source.submissionDeadline);
      await inputBesideLabel(dialog, /Controlled bid opening|Bid opening/i).fill(source.openingDate);
      await inputBesideLabel(dialog, /Advertisement reference|Solicitation reference/i).fill(source.publication.advertisementReference);
      await inputBesideLabel(dialog, /Publication channel/i).fill(source.publication.publicationChannel);
      await inputBesideLabel(dialog, /Approved document reference/i).fill(source.publication.tenderDocumentReference);
      await inputBesideLabel(dialog, /Document version/i).fill(source.publication.tenderDocumentVersion);
      await inputBesideLabel(dialog, /Advertisement evidence|Publication evidence/i).fill(source.publication.advertisementEvidenceReference);
      expect(await textInputs.count()).toBeGreaterThan(1);
      const confirmPublish = dialog.getByRole('button', { name: 'Publish Tender' });
      await confirmPublish.scrollIntoViewIfNeeded();
      await expect(confirmPublish).toBeVisible();
      await expect(confirmPublish).toBeEnabled();
      const publishResponse = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/Tenders/${tenderId}/publish$`, 'i'),
      );
      await confirmPublish.click();
      const published = await responseJson<TenderDto>(await publishResponse);
      expect(published.status).toMatch(/published/i);
      await expect(page.getByText('Published', { exact: true }).first()).toBeVisible();
      await diagnostics.assertClean();
      recordCheckpoint('tender-published');
    });
  });

  test('01 - supplier association, payment evidence, bid draft persistence, and sealed submission', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'SUPPLIER', testInfo, async (page, diagnostics) => {
      const source = fixture.supplierLifecycle;
      diagnostics.allowFailure(
        new RegExp(`/api/procurement/TenderBids/my-draft-bid/${source.tenderId}$`, 'i'),
        [404],
      );
      await page.goto(`/external-portal/tenders/${source.tenderId}/initiate-bid`);
      await expect(page.getByRole('heading', { name: 'Initiate Bid Submission' })).toBeVisible({
        timeout: 45_000,
      });
      await expect(page.getByText(source.tenderNumber, { exact: false }).first()).toBeVisible();

      if (await page.getByText(/already created a tender assignment/i).first().isHidden().catch(() => true)) {
        const selfAssociation = page.getByLabel('Associate only myself with this Tender');
        await selfAssociation.click();
        await expect(selfAssociation).toBeChecked();
      }
      const associationNext = page.getByRole('button', { name: /^Next$/ });
      await expect(associationNext).toBeEnabled({ timeout: 45_000 });
      await associationNext.click();

      await expect(
        page.getByRole('heading', { name: /^(Accept Agreement|Payment Verification)$/ }),
        'The association and bid-draft preparation must complete before the next initiation step is asserted.',
      ).toBeVisible({ timeout: 45_000 });

      if (await page.getByText(/supplier declaration document/i).first().isVisible().catch(() => false)) {
        await page.locator('#accept-declaration').check();
        await page.getByRole('button', { name: /^Next$/ }).click();
        await expect(page.getByRole('heading', { name: 'Payment Verification' })).toBeVisible({
          timeout: 45_000,
        });
      }

      const paymentInput = page.locator('input[id^="payment-reference-"]').first();
      await expect(
        paymentInput,
        'The fresh lifecycle tender must contain a positive mandatory fee so payment evidence and verification are exercised.',
      ).toBeVisible({ timeout: 45_000 });
      await paymentInput.fill(source.paymentReference!);
      const paymentResponse = successfulProcurementMutation(
        page,
        'POST',
        /\/api\/procurement\/TenderBids\/[^/]+\/payments$/i,
      );
      await page.getByRole('button', { name: 'Submit Payment for Verification' }).click();
      expect((await paymentResponse).ok()).toBe(true);
      await expect(page.getByText(/awaiting verification|evidence awaiting verification/i).first()).toBeVisible();

      const proceed = page.getByRole('button', { name: 'Proceed to Bid Submission' });
      if (await proceed.isVisible().catch(() => false)) await proceed.click();
      await expect(page).toHaveURL(new RegExp(`/external-portal/tenders/${source.tenderId}/submit-bid`));
      await expect(page.getByRole('heading', { name: 'Submit Bid' })).toBeVisible({ timeout: 45_000 });

      await page.locator(`[id="${source.lotId}"]`).check();
      const saveLots = successfulProcurementMutation(
        page,
        ['POST', 'PUT'],
        /\/api\/procurement\/TenderBids(?:\/[^/]+)?$/i,
      );
      await goToNextBidStep(page);
      expect((await saveLots).ok()).toBe(true);
      await expect(page.getByText(source.expectedItemDescription, { exact: false }).first()).toBeVisible();

      const itemRow = page
        .getByText(source.expectedItemDescription, { exact: false })
        .first()
        .locator('xpath=ancestor::tr');
      const numberInputs = itemRow.locator('input[type="number"]');
      await numberInputs.nth(0).fill(String(source.offeredQuantity));
      await numberInputs.nth(1).fill(String(source.unitPrice));
      await numberInputs.nth(2).fill(String(source.deliveryDays));
      await itemRow.getByPlaceholder('Brand').fill(source.brand);
      await itemRow.getByPlaceholder('Model').fill(source.model);
      const saveItems = successfulProcurementMutation(
        page,
        'PUT',
        /\/api\/procurement\/TenderBids\/[^/]+$/i,
      );
      await goToNextBidStep(page);
      expect((await saveItems).ok()).toBe(true);

      await page.locator('#technicalProposal').fill(source.technicalProposal);
      await page.locator('#commercialProposal').fill(source.commercialProposal);
      await page.locator('#deliveryDays').fill(String(source.deliveryDays));
      await page.locator('#paymentTerms').fill('Net 30 days after accepted delivery.');
      await page.locator('#warrantyTerms').fill('Twelve month replacement warranty.');
      const saveDraft = successfulProcurementMutation(
        page,
        'PUT',
        /\/api\/procurement\/TenderBids\/[^/]+$/i,
      );
      await page.getByRole('button', { name: 'Save Draft' }).click();
      expect((await saveDraft).ok()).toBe(true);

      const draft = await getSameOriginJson<BidDto>(
        page,
        `/api/procurement/TenderBids/my-draft-bid/${source.tenderId}`,
      );
      state.supplierBidId = draft.id;
      state.supplierBidNumber = draft.bidNumber;
      expect(draft.status).toMatch(/draft/i);
      expect(draft.selectedLotIds).toContain(source.lotId);
      expect(draft.items).toContainEqual(
        expect.objectContaining({
          tenderItemDescription: source.expectedItemDescription,
          offeredQuantity: source.offeredQuantity,
          unitPrice: source.unitPrice,
          deliveryDays: source.deliveryDays,
          brand: source.brand,
          model: source.model,
        }),
      );

      await page.reload();
      await expect(page.getByText('Step 4 of 5', { exact: true })).toBeVisible({
        timeout: 45_000,
      });
      for (let step = 4; step > 1; step -= 1) {
        await page.getByRole('button', { name: 'Previous' }).click();
      }
      const persistedLot = page.locator(`[id="${source.lotId}"]`);
      await expect(persistedLot).toBeChecked();
      await goToNextBidStep(page);
      const persistedRow = page
        .getByText(source.expectedItemDescription, { exact: false })
        .first()
        .locator('xpath=ancestor::tr');
      await expect(persistedRow.locator('input[type="number"]').nth(1)).toHaveValue(String(source.unitPrice));
      await expect(persistedRow.getByPlaceholder('Model')).toHaveValue(source.model);

      await goToNextBidStep(page);
      await expect(page.locator('#technicalProposal')).toHaveValue(source.technicalProposal);
      await expect(page.locator('#commercialProposal')).toHaveValue(source.commercialProposal);
      await goToNextBidStep(page);

      for (const [documentType, documentPath] of Object.entries(source.requiredDocumentPaths || {})) {
        const fileInput = page.locator(`[id="file-${documentType}"]`);
        const documentRow = fileInput.locator(
          'xpath=ancestor::div[contains(concat(" ", normalize-space(@class), " "), " p-4 ")][1]',
        );
        await fileInput.setInputFiles(documentPath);
        await expect(documentRow.getByText(/Selected:/i)).toBeVisible();
        await documentRow.getByRole('button', { name: /^Upload$/ }).click();
        await expect(page.getByText(/uploaded successfully/i).first()).toBeVisible();
      }

      await goToNextBidStep(page);
      await expect(page.getByText(/review/i).first()).toBeVisible();
      const submitResponse = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/TenderBids/${state.supplierBidId}/submit$`, 'i'),
      );
      await page.getByRole('button', { name: 'Submit Bid' }).click();
      await page.getByRole('dialog').getByRole('button', { name: 'Yes, Submit Bid' }).click();
      expect((await submitResponse).ok()).toBe(true);
      await expect(page).toHaveURL(new RegExp(`/external-portal/my-bids/${state.supplierBidId}$`));
      await expect(page.getByText(/submitted/i).first()).toBeVisible();
      await diagnostics.assertClean();
      recordCheckpoint('supplier-bid-submitted', {
        supplierBidId: state.supplierBidId,
        supplierBidNumber: state.supplierBidNumber,
      });
    });
  });

  test('02 - officer verifies payment while premature opening remains fail closed', async ({
    browser,
  }, testInfo) => {
    expect(state.supplierBidId, 'Supplier stage must retain the created bid ID.').toBeTruthy();
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      await page.goto(`/procurement/bids/${state.supplierBidId}`);
      await expect(page.getByText(state.supplierBidNumber!, { exact: false }).first()).toBeVisible({ timeout: 45_000 });
      const paymentsTab = page.getByRole('tab', { name: /Payments/i });
      if (await paymentsTab.count()) await paymentsTab.click();
      else await page.getByText(/^Payments \(\d+\)$/).click();

      const approvePayment = page.getByRole('button', { name: /Approve payment/i }).first();
      await expect(
        approvePayment,
        'A pending mandatory tender fee must expose the authorized back-office verification action.',
      ).toBeVisible({ timeout: 45_000 });
      await expect(approvePayment).toBeEnabled();
      await approvePayment.click();
      const verifyResponse = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/TenderBids/${state.supplierBidId}/payments/[^/]+/verify$`, 'i'),
      );
      await page.getByRole('dialog').getByRole('button', { name: 'Approve payment' }).click();
      const verifiedPaymentResponse = await verifyResponse;
      expect(verifiedPaymentResponse.status()).toBe(200);
      expect(verifiedPaymentResponse.ok()).toBe(true);
      await expect(page.getByText('Verified', { exact: true }).first()).toBeVisible();

      const openingEndpoint = new RegExp(
        `/api/procurement/TenderBids/${state.supplierBidId}/open$`,
        'i',
      );
      diagnostics.allowFailure(openingEndpoint, [400, 409, 422]);
      const prematureOpen = await sameOriginMutation<{ detail?: string }>(
        page,
        `/api/procurement/TenderBids/${state.supplierBidId}/open`,
        'POST',
      );
      expect([400, 409, 422]).toContain(prematureOpen.status);
      const retained = await getSameOriginJson<BidDto>(
        page,
        `/api/procurement/TenderBids/${state.supplierBidId}`,
      );
      expect(retained.status).toMatch(/submitted/i);
      await diagnostics.assertClean();
      recordCheckpoint('payment-verified-premature-opening-denied');
    });
  });

  test('03 - no-committee NCT source remains fail closed before controlled scoring', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'EVALUATOR', testInfo, async (page, diagnostics) => {
      const source = fixture.committee.absent;
      const readiness = await getSameOriginJson<{
        sourceExists: boolean;
        hasControl: boolean;
        compositionReady: boolean;
        quorumMet: boolean;
        blockedReasons: string[];
      }>(page, `/api/procurement/evaluation-committees/readiness?sourceType=Tender&sourceId=${source.tenderId}`);
      expect(readiness.sourceExists).toBe(true);
      expect(readiness.hasControl).toBe(false);
      expect(readiness.compositionReady).toBe(false);
      expect(readiness.quorumMet).toBe(false);
      expect(readiness.blockedReasons.join(' ')).toMatch(/no source-specific evaluation committee/i);
      await diagnostics.assertClean();
      recordCheckpoint('committee-absent-gate');
    });
  });

  test('04 - draft committee NCT source remains fail closed before controlled scoring', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'EVALUATOR', testInfo, async (page, diagnostics) => {
      const source = fixture.committee.draft;
      const readiness = await getSameOriginJson<{
        sourceExists: boolean;
        hasControl: boolean;
        status: string | number;
        quorumMet: boolean;
        blockedReasons: string[];
      }>(page, `/api/procurement/evaluation-committees/readiness?sourceType=Tender&sourceId=${source.tenderId}`);
      expect(readiness.sourceExists).toBe(true);
      expect(readiness.hasControl).toBe(true);
      expect(String(readiness.status)).toMatch(/draft|0/i);
      expect(readiness.quorumMet).toBe(false);
      expect(readiness.blockedReasons.join(' ')).toMatch(/not active|quorum/i);
      await diagnostics.assertClean();
      recordCheckpoint('committee-draft-gate');
    });
  });

  test('04A - closed evaluation tender completes formal opening for both sealed bids', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      const competition = fixture.competition;
      if (competition.opening.mode === 'Controlled') {
        await page.goto(`/procurement/tenders/${competition.tenderId}/controls`);
        await expect(page.getByText(/Complete signed public opening/i)).toBeVisible({
          timeout: 45_000,
        });
        await inputBesideLabel(page, /Opening evidence/i).fill(
          competition.opening.evidenceReference,
        );
        await inputBesideLabel(page, /Officer signature reference/i).fill(
          competition.opening.officerSignatureReference,
        );
        await inputBesideLabel(page, /Observer name/i).fill(
          competition.opening.observerName,
        );
        await inputBesideLabel(page, /Observer signature reference/i).fill(
          competition.opening.observerSignatureReference,
        );
        const openingResponse = successfulProcurementMutation(
          page,
          'POST',
          new RegExp(`/api/procurement/tenders/${competition.tenderId}/controls/opening$`, 'i'),
        );
        await page.getByRole('button', { name: 'Complete public opening' }).click();
        expect((await openingResponse).ok()).toBe(true);
        await expect(page.getByText(/Public opening/i).first()).toBeVisible();
      } else {
        for (const bidId of [competition.firstSupplierBidId, competition.secondSupplierBidId]) {
          await page.goto(`/procurement/bids/${bidId}`);
          await page.getByRole('button', { name: 'Mark as Opened' }).click();
          const openResponse = successfulProcurementMutation(
            page,
            'POST',
            new RegExp(`/api/procurement/TenderBids/${bidId}/open$`, 'i'),
          );
          await page.getByRole('dialog').getByRole('button', { name: 'Mark as Opened' }).click();
          expect((await openResponse).ok()).toBe(true);
        }
      }

      const firstBid = await getSameOriginJson<BidDto>(
        page,
        `/api/procurement/TenderBids/${competition.firstSupplierBidId}`,
      );
      const secondBid = await getSameOriginJson<BidDto>(
        page,
        `/api/procurement/TenderBids/${competition.secondSupplierBidId}`,
      );
      expect(firstBid.status).toMatch(/opened|under.?evaluation/i);
      expect(secondBid.status).toMatch(/opened|under.?evaluation/i);
      await diagnostics.assertClean();
      recordCheckpoint('formal-opening-complete', {
        evaluationBidId: competition.firstSupplierBidId,
        evaluationBidNumber: competition.firstSupplierBidNumber,
        secondSupplierBidId: competition.secondSupplierBidId,
        secondSupplierBidNumber: competition.secondSupplierBidNumber,
      });
    });
  });

  test('05 - active committee signs the controlled NCT technical evaluation', async ({
    browser,
  }, testInfo) => {
    const activeScenario = fixture.committee.active;
    state.bidId = activeScenario.bidId;
    state.bidNumber = activeScenario.bidNumber;
    expect(activeScenario.bidId).toBeTruthy();
    await withActor(browser, 'EVALUATOR', testInfo, async (page, diagnostics) => {
      await page.goto(`/procurement/tenders/${activeScenario.tenderId}`);
      await page.getByRole('tab', { name: 'Evaluation Committee' }).click();
      await expect(page.getByRole('button', { name: 'Assign Evaluators' })).toHaveCount(0);
      await expect(page.getByRole('button', { name: 'NCT / ICT Controls' })).toBeVisible();

      await page.goto(`/procurement/bids/${activeScenario.bidId}`);
      await page.getByRole('tab', { name: /^Evaluation/ }).click();
      await expect(page.getByRole('button', { name: 'Create Evaluation' })).toHaveCount(0);
      await expect(page.getByRole('button', { name: 'Create First Evaluation' })).toHaveCount(0);
      await page.getByRole('button', { name: 'NCT / ICT Controls' }).click();
      await expect(page).toHaveURL(
        new RegExp(`/procurement/tenders/${activeScenario.tenderId}/controls$`, 'i')
      );
      await expect(page.getByRole('heading', { name: /NCT statutory controls/i })).toBeVisible({
        timeout: 45_000,
      });
      const technicalCard = page
        .getByRole('heading', { name: 'Signed technical evaluation', exact: true })
        .locator('xpath=ancestor::div[contains(concat(" ", normalize-space(@class), " "), " rounded-2xl ")][1]');
      await expect(technicalCard).toBeVisible();
      await inputBesideLabel(technicalCard, /Signed evidence reference/i).fill(
        `${fixture.evaluation.evidenceReference}-TECHNICAL`,
      );
      const technicalScores = technicalCard.locator('input[type="number"]');
      expect(await technicalScores.count()).toBe(2);
      await technicalScores.nth(0).fill('95');
      await technicalScores.nth(1).fill('85');
      const qualified = technicalCard.locator('input[type="checkbox"]');
      expect(await qualified.count()).toBe(2);
      await qualified.nth(0).check();
      await qualified.nth(1).check();
      const submitResponse = successfulProcurementMutation(
        page,
        'PUT',
        new RegExp(`/api/procurement/tenders/${activeScenario.tenderId}/controls/technical-evaluation$`, 'i'),
      );
      await technicalCard.getByRole('button', { name: 'Save technical evaluation' }).click();
      const submitted = await responseJson<TenderControlDto>(await submitResponse);
      expect(submitted.status).toBe('TechnicalEvaluated');
      await expect(
        page.getByText('Signed financial evaluation and recommendation', { exact: true }),
      ).toBeVisible();
      await diagnostics.assertClean();
      recordCheckpoint('controlled-technical-evaluation');
    });
  });

  test('05A - competing Supplier B bid remains distinct after formal opening', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'SUPPLIER_B', testInfo, async (page, diagnostics) => {
      const competition = fixture.competition;
      await page.goto(`/external-portal/my-bids/${competition.secondSupplierBidId}`);
      await expect(page.getByText(competition.secondSupplierBidNumber, { exact: false }).first()).toBeVisible({
        timeout: 45_000,
      });
      await expect(page.getByText(/Submitted|Opened|Under Evaluation/i).first()).toBeVisible();
      await diagnostics.assertClean();
      recordCheckpoint('second-supplier-bid-visible', {
        secondSupplierBidId: competition.secondSupplierBidId,
        secondSupplierBidNumber: competition.secondSupplierBidNumber,
      });
    });
  });

  test('05B - independent financial evaluator signs the NCT recommendation', async ({
    browser,
  }, testInfo) => {
      await withActor(browser, 'EVALUATOR_B', testInfo, async (page, diagnostics) => {
      const competition = fixture.competition;
      await page.goto(`/procurement/tenders/${competition.tenderId}/controls`);
      const financialCard = page
        .getByRole('heading', {
          name: 'Signed financial evaluation and recommendation',
          exact: true,
        })
        .locator('xpath=ancestor::div[contains(concat(" ", normalize-space(@class), " "), " rounded-2xl ")][1]');
      await expect(financialCard).toBeVisible({ timeout: 45_000 });
      await inputBesideLabel(financialCard, /Signed evidence reference/i).fill(
        `${fixture.evaluation.evidenceReference}-FINANCIAL`,
      );
      const scoreInputs = financialCard
        .locator('label')
        .filter({ hasText: 'Score / 100' })
        .locator('xpath=following-sibling::input[1]');
      expect(await scoreInputs.count()).toBe(2);
      await scoreInputs.nth(0).fill('95');
      await scoreInputs.nth(1).fill('85');
      const amountInputs = financialCard
        .locator('label')
        .filter({ hasText: 'Evaluated amount' })
        .locator('xpath=following-sibling::input[1]');
      expect(await amountInputs.count()).toBe(2);
      await amountInputs.nth(0).fill('24000');
      await amountInputs.nth(1).fill('28000');
      await financialCard.locator('select').selectOption(competition.firstSupplierBidId);
      await textareaBesideLabel(financialCard, /Recommendation reason/i).fill(
        fixture.evaluation.recommendation,
      );
      const submitResponse = successfulProcurementMutation(
        page,
        'PUT',
        new RegExp(`/api/procurement/tenders/${competition.tenderId}/controls/financial-evaluation$`, 'i'),
      );
      await financialCard.getByRole('button', { name: 'Save financial recommendation' }).click();
      const submitted = await responseJson<TenderControlDto>(await submitResponse);
      expect(submitted.status).toBe('FinancialEvaluated');
      expect(submitted.recommendedBidId).toBe(competition.firstSupplierBidId);
      await expect(page.getByText('Submit exact authority/PPA workflow', { exact: true })).toBeVisible();
      await diagnostics.assertClean();
      recordCheckpoint('controlled-financial-recommendation');
    });
  });

  test('05C - Head of Procurement submits the exact authority workflow', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'APPROVER', testInfo, async (page, diagnostics) => {
      const tenderId = fixture.competition.tenderId;
      await page.goto(`/procurement/tenders/${tenderId}/controls`);
      await expect(page.getByText('Submit exact authority/PPA workflow', { exact: true })).toBeVisible({
        timeout: 45_000,
      });
      const response = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/tenders/${tenderId}/controls/approval/submit$`, 'i'),
      );
      await page.getByRole('button', { name: 'Submit for approval' }).click();
      const control = await responseJson<TenderControlDto>(await response);
      expect(control.status).toBe('PendingApproval');
      await expect(page.getByText('Authority and PPA decision', { exact: true })).toBeVisible();
      await diagnostics.assertClean();
      recordCheckpoint('controlled-authority-workflow-submitted');
    });
  });

  test('05D - published tender amendment persists and refreshes immutably', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      await page.goto(`/procurement/tenders/${fixture.supplierLifecycle.tenderId}`);
      await page.getByRole('tab', { name: /Amendments/i }).click();
      await page.getByRole('button', { name: 'Issue amendment' }).click();
      const dialog = page.getByRole('dialog');
      await textareaBesideLabel(dialog, /Reason and public summary/i).fill(
        fixture.regression.tenderAmendmentDescription,
      );
      await textareaBesideLabel(dialog, /Exact changes/i).fill(fixture.regression.tenderAmendmentChanges);
      const revisionResponse = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/Tenders/${fixture.supplierLifecycle.tenderId}/revisions$`, 'i'),
      );
      await dialog.getByRole('button', { name: 'Issue amendment' }).click();
      const revision = await responseJson<{ id: string; revisionNumber: string }>(await revisionResponse);
      expect(revision.id).toBeTruthy();
      await page.reload();
      await page.getByRole('tab', { name: /Amendments/i }).click();
      await expect(page.getByText(fixture.regression.tenderAmendmentDescription, { exact: true }).first()).toBeVisible();
      await diagnostics.assertClean();
      recordCheckpoint('tender-amendment', { tenderRevisionId: revision.id });
    });
  });

  test('05E - repeated controlled technical evaluation is rejected without stage regression', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'EVALUATOR', testInfo, async (page, diagnostics) => {
      diagnostics.allowFailure(
        new RegExp(`/api/procurement/tenders/${fixture.competition.tenderId}/controls/technical-evaluation$`, 'i'),
        [400, 409, 422],
      );
      await page.goto('/dashboard');
      const current = await getSameOriginJson<TenderControlDto>(
        page,
        `/api/procurement/tenders/${fixture.competition.tenderId}/controls`,
      );
      const repeated = await sameOriginMutation<{ code?: string; detail?: string }>(
        page,
        `/api/procurement/tenders/${fixture.competition.tenderId}/controls/technical-evaluation`,
        'PUT',
        {
          evidenceReference: `${fixture.evaluation.evidenceReference}-REPEAT`,
          scores: [
            { bidId: fixture.competition.firstSupplierBidId, score: 95, qualified: true, reason: 'Repeat probe' },
            { bidId: fixture.competition.secondSupplierBidId, score: 85, qualified: true, reason: 'Repeat probe' },
          ],
          rowVersion: current.rowVersion,
        },
      );
      expect([400, 409, 422]).toContain(repeated.status);
      expect(repeated.status).not.toBe(500);
      const retained = await getSameOriginJson<TenderControlDto>(
        page,
        `/api/procurement/tenders/${fixture.competition.tenderId}/controls`,
      );
      expect(retained.status).toBe('PendingApproval');
      await diagnostics.assertClean();
      recordCheckpoint('controlled-evaluation-retry-denied');
    });
  });

  test('06 - unauthorized user receives controlled server denial for bid evaluation', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'UNAUTHORIZED', testInfo, async (page, diagnostics) => {
      const bidEndpoint = new RegExp(
        `/api/procurement/TenderBids/${fixture.competition.firstSupplierBidId}$`,
        'i',
      );
      const evaluationEndpoint = new RegExp(
        `/api/procurement/tenders/${fixture.competition.tenderId}/controls/technical-evaluation$`,
        'i',
      );
      diagnostics.allowFailure(bidEndpoint, [403, 404]);
      diagnostics.allowFailure(evaluationEndpoint, [403, 404]);
      await page.goto('/dashboard');
      const deniedRead = await page.evaluate(async (bidId) => {
        const token = localStorage.getItem('authToken') || localStorage.getItem('token');
        const response = await fetch(`/api/procurement/TenderBids/${bidId}`, {
          headers: token ? { Authorization: `Bearer ${token}` } : {},
        });
        return response.status;
      }, fixture.competition.firstSupplierBidId);
      expect([403, 404]).toContain(deniedRead);
      const deniedEvaluation = await sameOriginMutation<{ code?: string; detail?: string }>(
        page,
        `/api/procurement/tenders/${fixture.competition.tenderId}/controls/technical-evaluation`,
        'PUT',
        {
          evidenceReference: `${fixture.evaluation.evidenceReference}-UNAUTHORIZED`,
          scores: [
            { bidId: fixture.competition.firstSupplierBidId, score: 100, qualified: true, reason: 'Must not persist' },
            { bidId: fixture.competition.secondSupplierBidId, score: 100, qualified: true, reason: 'Must not persist' },
          ],
          rowVersion: 'AAAAAAAAAAA=',
        },
      );
      expect([403, 404]).toContain(deniedEvaluation.status);
      expect(deniedEvaluation.status).not.toBe(500);
      await diagnostics.assertClean();
      recordCheckpoint('unauthorized-evaluation-server-denied');
    });
  });

  test('06A - direct unauthorized and cross-tenant APIs return controlled denial without mutation', async ({
    browser,
  }, testInfo) => {
    let beforeCount = 0;
    let crossTenantVerified = false;
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      const foreignTenderId = fixture.regression.crossTenantTenderId;
      expect(foreignTenderId, 'The guarded fixture must provide a real foreign-tenant tender ID.').toBeTruthy();
      const foreignTenderEndpoint = new RegExp(`/api/procurement/Tenders/${foreignTenderId}$`, 'i');
      const foreignRevisionEndpoint = new RegExp(
        `/api/procurement/Tenders/${foreignTenderId}/revisions$`,
        'i',
      );
      diagnostics.allowFailure(foreignTenderEndpoint, [403, 404]);
      diagnostics.allowFailure(foreignRevisionEndpoint, [403, 404]);
      await page.goto('/dashboard');
      const crossTenantRead = await page.evaluate(async (id) => {
        const token = localStorage.getItem('authToken') || localStorage.getItem('token');
        const response = await fetch(`/api/procurement/Tenders/${id}`, {
          headers: token ? { Authorization: `Bearer ${token}` } : {},
        });
        return response.status;
      }, foreignTenderId!);
      expect([403, 404]).toContain(crossTenantRead);
      const crossTenantMutation = await sameOriginMutation<{ code?: string; detail?: string }>(
        page,
        `/api/procurement/Tenders/${foreignTenderId}/revisions`,
        'POST',
        {
          revisionType: 'Amendment',
          description: `${fixture.runId} cross-tenant mutation must not persist`,
          changes: 'Tenant-isolation negative probe.',
          requiresRebid: false,
          sendNotifications: false,
        },
      );
      expect([403, 404]).toContain(crossTenantMutation.status);
      crossTenantVerified = true;

      await page.goto(`/procurement/tenders/${fixture.supplierLifecycle.tenderId}`);
      const revisions = await getSameOriginJson<unknown[]>(
        page,
        `/api/procurement/Tenders/${fixture.supplierLifecycle.tenderId}/revisions`,
      );
      beforeCount = revisions.length;
      await diagnostics.assertClean();
    });

    await withActor(browser, 'UNAUTHORIZED', testInfo, async (page, diagnostics) => {
      diagnostics.allowFailure(
        new RegExp(`/api/procurement/Tenders/${fixture.supplierLifecycle.tenderId}/revisions$`, 'i'),
        [403, 404],
      );
      await page.goto('/dashboard');
      const deniedMutation = await sameOriginMutation<{ code?: string; detail?: string }>(
        page,
        `/api/procurement/Tenders/${fixture.supplierLifecycle.tenderId}/revisions`,
        'POST',
        {
          revisionType: 'Amendment',
          description: `${fixture.runId} unauthorized mutation must not persist`,
          changes: 'Negative authorization probe.',
          requiresRebid: false,
          sendNotifications: false,
        },
      );
      expect([403, 404]).toContain(deniedMutation.status);
      await diagnostics.assertClean();
    });

    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      await page.goto(`/procurement/tenders/${fixture.supplierLifecycle.tenderId}`);
      const revisions = await getSameOriginJson<unknown[]>(
        page,
        `/api/procurement/Tenders/${fixture.supplierLifecycle.tenderId}/revisions`,
      );
      expect(revisions).toHaveLength(beforeCount);
      await diagnostics.assertClean();
    });
    if (crossTenantVerified) recordCheckpoint('cross-tenant-api-isolation');
    else recordPending('cross-tenant-api-isolation');
    recordCheckpoint('unauthorized-api-nonmutation');
  });

  test('07 - independent authority approves the controlled recommendation', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'ETC_APPROVER', testInfo, async (page, diagnostics) => {
      const tenderId = fixture.competition.tenderId;
      await page.goto(`/procurement/tenders/${tenderId}/controls`);
      await expect(page.getByText('Authority and PPA decision', { exact: true })).toBeVisible({
        timeout: 45_000,
      });
      await inputBesideLabel(page, /Authority approval reference/i).fill(
        `TE2E-${fixture.runId}-AUTHORITY`,
      );
      await textareaBesideLabel(page, /Comments/i).fill(fixture.award.approvalNotes);
      const approvalResponse = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/tenders/${tenderId}/controls/approval/decision$`, 'i'),
      );
      await page.getByRole('button', { name: 'Approve' }).click();
      const approved = await responseJson<TenderControlDto>(await approvalResponse);
      expect(approved.status).toBe('Approved');
      await diagnostics.assertClean();
      recordCheckpoint('controlled-authority-approved');
    });
  });

  test('08 - authority approver records current readiness and the approved award', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'APPROVER', testInfo, async (page, diagnostics) => {
      const tenderId = fixture.competition.tenderId;
      await page.goto(`/procurement/tenders/${tenderId}/award-readiness`);
      await expect(page.locator('[data-testid="award-readiness-workspace"]')).toBeVisible({
        timeout: 45_000,
      });
      const evaluate = page.getByRole('button', { name: /Evaluate award readiness|Re-evaluate/i }).first();
      const evaluateResponse = successfulProcurementMutation(
        page,
        'POST',
        /\/api\/procurement\/award-readiness\/evaluate$/i,
      );
      await evaluate.click();
      expect((await evaluateResponse).ok()).toBe(true);
      const register = page.locator('[data-testid="award-readiness-register"]');
      await expect(register).toContainText(/Ready/i);
      await expect(register).toContainText(/current/i);

      await page.goto(`/procurement/tenders/${tenderId}/controls`);
      await expect(page.getByText('Record approved award', { exact: true })).toBeVisible({
        timeout: 45_000,
      });
      await inputBesideLabel(page, /Award reference/i).fill(`TE2E-${fixture.runId}-AWARD`);
      await inputBesideLabel(page, /Award evidence/i).fill(
        `TE2E-${fixture.runId}-AWARD-EVIDENCE`,
      );
      const awardResponse = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/tenders/${tenderId}/controls/award$`, 'i'),
      );
      await page.getByRole('button', { name: 'Record award' }).click();
      const award = await responseJson<TenderControlDto>(await awardResponse);
      expect(award.status).toBe('Awarded');
      expect(award.awardBidId).toBe(fixture.competition.firstSupplierBidId);
      const materializedAward = await getSameOriginJson<AwardDto>(
        page,
        `/api/procurement/TenderAwards/by-tender/${tenderId}`,
      );
      expect(materializedAward.status).toBe('Awarded');
      expect(materializedAward.id).toBeTruthy();
      state.awardId = materializedAward.id;
      state.awardAmount = materializedAward.awardedAmount;
      state.awardCurrency = materializedAward.currency;
      await diagnostics.assertClean();
      recordCheckpoint('controlled-award-readiness-current');
      recordCheckpoint('controlled-award-recorded', { awardId: materializedAward.id });
    });
  });

  test('09 - real contract is independently activated before its controlled execution record', async ({
    browser,
  }, testInfo) => {
    await createAndActivateRealContract(browser, testInfo);
    expect(state.contractNumber).toBeTruthy();
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      const tenderId = fixture.competition.tenderId;
      await page.goto(`/procurement/tenders/${tenderId}/controls`);
      await expect(page.getByText('Record executed contract', { exact: true })).toBeVisible({
        timeout: 45_000,
      });
      await inputBesideLabel(page, /Contract reference/i).fill(
        state.contractNumber!,
      );
      await inputBesideLabel(page, /Contract evidence/i).fill(
        `TE2E-${fixture.runId}-CONTRACT-EVIDENCE`,
      );
      const contractResponse = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/tenders/${tenderId}/controls/contract$`, 'i'),
      );
      await page.getByRole('button', { name: 'Record contract' }).click();
      const control = await responseJson<TenderControlDto>(await contractResponse);
      expect(control.status).toBe('Contracted');
      expect(control.contractReference).toBe(state.contractNumber);
      await diagnostics.assertClean();
      recordCheckpoint('controlled-contract-recorded');
    });
  });

  test('10 - successful bidder acceptance completes all fourteen statutory milestones', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      const tenderId = fixture.competition.tenderId;
      await page.goto(`/procurement/tenders/${tenderId}/controls`);
      await expect(
        page.getByText('Record successful bidder acceptance', { exact: true }),
      ).toBeVisible({ timeout: 45_000 });
      await inputBesideLabel(page, /Acceptance reference/i).fill(
        `TE2E-${fixture.runId}-ACCEPTANCE`,
      );
      await inputBesideLabel(page, /Acceptance evidence/i).fill(
        `TE2E-${fixture.runId}-ACCEPTANCE-EVIDENCE`,
      );
      const acceptanceResponse = successfulProcurementMutation(
        page,
        'POST',
        new RegExp(`/api/procurement/tenders/${tenderId}/controls/acceptance$`, 'i'),
      );
      await page.getByRole('button', { name: 'Complete statutory record' }).click();
      const control = await responseJson<TenderControlDto>(await acceptanceResponse);
      expect(control.status).toBe('Accepted');
      expect(control.bidderAcceptanceReference).toBe(`TE2E-${fixture.runId}-ACCEPTANCE`);
      for (let decision = 1; decision <= 14; decision += 1) {
        const code = `DEC-${String(decision).padStart(3, '0')}`;
        await expect(page.locator(`[data-testid="milestone-${code}"]`)).toContainText('Complete');
      }
      await diagnostics.assertClean();
      recordCheckpoint('controlled-bidder-acceptance');
    });
  });

  test('10A - controlled tender award cannot bypass the contract lifecycle through direct PO conversion', async ({
    browser,
  }, testInfo) => {
    expect(fixture.handoff.alternateMode).toBe('PO');
    const awardId = state.awardId;
    expect(awardId, 'The controlled award must materialize a real TenderAward.').toBeTruthy();
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      await page.goto('/dashboard');
      const endpoint = /\/api\/procurement\/TenderAwards\/create-purchase-order$/i;
      diagnostics.allowFailure(endpoint, [400, 409, 422]);
      const denied = await sameOriginMutation<
        string | { code?: string; detail?: string; message?: string }
      >(
        page,
        '/api/procurement/TenderAwards/create-purchase-order',
        'POST',
        {
          tenderAwardId: awardId,
          requiredDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
          deliveryAddress: 'Tender lifecycle guarded acceptance location',
          paymentTerms: 'Net 30 days',
          notes: `Real alternate award conversion ${fixture.runId}`,
          autoApprove: false,
        },
      );
      expect([400, 409, 422]).toContain(denied.status);
      const denialText = typeof denied.body === 'string'
        ? denied.body
        : `${denied.body.detail ?? ''} ${denied.body.message ?? ''}`;
      expect(denialText).toContain(
        'controlled approval, contract, and bidder-acceptance lifecycle',
      );
      await diagnostics.assertClean();
      recordCheckpoint('controlled-direct-po-bypass-denied', {
        awardId,
      });
    });
  });

  test('10B - activated contract and independent workflow remain queryable', async ({
    browser,
  }, testInfo) => {
    expect(state.contractId).toBeTruthy();
    expect(state.contractActivationId).toBeTruthy();
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      await page.goto('/dashboard');
      const contract = await getSameOriginJson<ContractDto>(
        page, `/api/procurement/Contracts/${state.contractId}`,
      );
      expect(contract.status).toBe('Active');
      expect(contract.tenderAwardId).toBe(state.awardId);
      const overview = await getSameOriginJson<ContractActivationOverviewDto>(
        page, `/api/procurement/contract-activations/contracts/${state.contractId}`,
      );
      expect(overview.isReady).toBe(true);
      await diagnostics.assertClean();
    });
  });

  test('11 - accepted statutory control remains intact after refresh', async ({
    browser,
  }, testInfo) => {
    await withActor(browser, 'PROCUREMENT_OFFICER', testInfo, async (page, diagnostics) => {
      const tenderId = fixture.competition.tenderId;
      await page.goto(`/procurement/tenders/${tenderId}/controls`);
      await page.reload();
      const control = await getSameOriginJson<TenderControlDto>(
        page,
        `/api/procurement/tenders/${tenderId}/controls`,
      );
      expect(control.status).toBe('Accepted');
      expect(control.awardBidId).toBe(fixture.competition.firstSupplierBidId);
      expect(control.contractReference).toBe(`TE2E-${fixture.runId}-CONTRACT`);
      expect(control.bidderAcceptanceReference).toBe(`TE2E-${fixture.runId}-ACCEPTANCE`);
      await diagnostics.assertClean();
      recordCheckpoint('controlled-lifecycle-refresh', {}, 'Passed');
    });
  });
});
