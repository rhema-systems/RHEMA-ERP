import { expect, test } from '@playwright/test';

const required = (name: string): string => {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} must be configured for QS acceptance.`);
  return value;
};

test('authenticated QS browser and tenant-scoped API prerequisite acceptance', async ({
  page,
  request,
}) => {
  test.skip(
    process.env.QS_ACCEPTANCE_INTERACTIVE !== '1',
    'Set QS_ACCEPTANCE_INTERACTIVE=1 and complete CAPTCHA in the headed browser.',
  );
  test.setTimeout(12 * 60_000);

  const baseUrl = process.env.E2E_API_URL?.trim() || 'http://127.0.0.1:5000';
  const projectId = required('QS_ACCEPTANCE_PROJECT_ID');
  const valuationId = required('QS_ACCEPTANCE_VALUATION_ID');
  const boqVersionId = required('QS_ACCEPTANCE_BOQ_VERSION_ID');
  const contractorId = required('QS_ACCEPTANCE_CONTRACTOR_ID');
  const consultantId = required('QS_ACCEPTANCE_CONSULTANT_ID');
  const username = required('QS_ACCEPTANCE_USERNAME');
  const password = required('QS_ACCEPTANCE_PASSWORD');

  const serverErrors: string[] = [];
  const pageErrors: string[] = [];
  page.on('response', (response) => {
    if (response.status() >= 500) {
      serverErrors.push(`${response.status()} ${response.url()}`);
    }
  });
  page.on('pageerror', (error) => pageErrors.push(error.message));

  await page.goto('/login');
  await page
    .locator('input[name="username"], input[type="text"]')
    .fill(username);
  await page
    .locator('input[name="password"], input[type="password"]')
    .fill(password);

  console.log(
    `ACTION_REQUIRED: complete CAPTCHA and click Sign In for ${username}. The test will continue automatically.`,
  );
  await page.waitForURL(/\/(dashboard|tenant-select|home)/, {
    timeout: 8 * 60_000,
  });

  if (page.url().includes('/tenant-select')) {
    const select = page.getByRole('button', { name: /select/i });
    await expect(select).toHaveCount(1);
    await select.click();
    await page.waitForURL(/\/(dashboard|home)/, { timeout: 30_000 });
  }

  const token = await page.evaluate(
    () => localStorage.getItem('authToken') || localStorage.getItem('token'),
  );
  expect(
    token,
    'A successful real login must issue the normal bearer token.',
  ).toBeTruthy();
  const headers = { Authorization: `Bearer ${token}` };

  const lookupsResponse = await request.get(
    `${baseUrl}/api/quantity-survey/valuation-worksheets/lookups?projectId=${projectId}`,
    { headers },
  );
  expect(lookupsResponse.status()).toBe(200);
  const lookups = (await lookupsResponse.json()) as {
    approvedBoqVersions: Array<{ id: string; lineCount: number }>;
    contractors: Array<{ id: string }>;
    consultants: Array<{ id: string }>;
  };
  expect(lookups.approvedBoqVersions).toContainEqual(
    expect.objectContaining({ id: boqVersionId, lineCount: 14 }),
  );
  expect(lookups.contractors.map((value) => value.id)).toContain(contractorId);
  expect(lookups.consultants.map((value) => value.id)).toContain(consultantId);

  const previewResponse = await request.get(
    `${baseUrl}/api/quantity-survey/valuation-worksheets/${valuationId}?projectBoqVersionId=${boqVersionId}`,
    { headers },
  );
  expect(previewResponse.status()).toBe(200);
  const preview = (await previewResponse.json()) as {
    id: string | null;
    projectId: string;
    projectInterimValuationId: string;
    projectBoqVersionId: string;
    status: string;
    lines: unknown[];
  };
  expect(preview).toMatchObject({
    id: null,
    projectId,
    projectInterimValuationId: valuationId,
    projectBoqVersionId: boqVersionId,
    status: 'Draft',
  });
  expect(preview.lines).toHaveLength(14);

  const dashboardResponse = await request.get(
    `${baseUrl}/api/projects/${projectId}/quantity-survey-cost-dashboard`,
    { headers },
  );
  expect(dashboardResponse.status()).toBe(200);
  expect(await dashboardResponse.json()).toMatchObject({ projectId });

  const forbiddenApproval = await request.post(
    `${baseUrl}/api/quantity-survey/valuation-worksheets/worksheets/ffffffff-ffff-ffff-ffff-ffffffffffff/approve`,
    {
      headers,
      data: {
        clientRequestId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
        rowVersion: 'acceptance-direct-api-attempt',
        reason: 'Acceptance proof that a preparer cannot approve a valuation.',
      },
    },
  );
  expect(forbiddenApproval.status()).toBe(403);
  expect(await forbiddenApproval.json()).toMatchObject({
    title: 'Permission denied',
    status: 403,
    requiredPermissions: ['quantity-survey.transactions.approve'],
  });

  await page.goto(`/development/projects/${projectId}/commercial-admin`);
  await expect(page.getByText('QS governed E2E interim valuation')).toBeVisible(
    {
      timeout: 60_000,
    },
  );
  await expect(
    page.getByRole('button', { name: 'Worksheet', exact: true }).first(),
  ).toBeVisible();

  const governedWorkspaces = [
    ['Payment certificate workspace', 'Governed payment certificates'],
    ['Advance recovery', 'Governed advance recovery'],
    ['Material reconciliation', 'Governed material reconciliation'],
    ['Final account workspace', 'Final account reconciliation'],
  ] as const;
  for (const [buttonName, dialogTitle] of governedWorkspaces) {
    const trigger = page.getByRole('button', {
      name: buttonName,
      exact: true,
    });
    await expect(trigger).toBeVisible({ timeout: 45_000 });
    await trigger.click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByText(dialogTitle, { exact: true })).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeHidden();
  }

  expect(serverErrors).toEqual([]);
  expect(pageErrors).toEqual([]);
});
