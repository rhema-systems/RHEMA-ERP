import { expect, test } from '@playwright/test';

const required = (name: string): string => {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} must be configured for INV-REQ-FU-004 acceptance.`);
  return value;
};

test('E2E-011 renders the completed damaged-transfer control lineage', async ({ browser }) => {
  test.setTimeout(8 * 60_000);
  const api = process.env.E2E_API_URL?.trim() || 'http://127.0.0.1:5100';
  const context = await browser.newContext({
    baseURL: process.env.E2E_BASE_URL?.trim() || 'http://127.0.0.1:3001',
  });
  const page = await context.newPage();
  const materialErrors: string[] = [];

  page.on('pageerror', error => materialErrors.push(`pageerror: ${error.message}`));
  page.on('console', message => {
    if (message.type() === 'error') materialErrors.push(`console: ${message.text()}`);
  });
  page.on('requestfailed', request =>
    materialErrors.push(`requestfailed: ${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`),
  );
  page.on('response', response => {
    if (response.status() >= 500) {
      materialErrors.push(`response: ${response.status()} ${response.request().method()} ${response.url()}`);
    }
  });

  await page.goto('/login');
  await expect(page.getByRole('button', { name: 'Sign In', exact: true })).toBeVisible();
  const response = await context.request.post(`${api}/api/auth/login`, {
    data: {
      username: required('INV_FU004_USERNAME'),
      password: required('INV_FU004_PASSWORD'),
      tenantCode: process.env.INV_FU004_TENANT_CODE?.trim() || 'DEFAULT',
      rememberMe: false,
    },
  });
  const body = await response.text();
  expect(response.status(), body).toBe(200);
  const login = JSON.parse(body) as { token?: string; refreshToken?: string };
  expect(login.token).toBeTruthy();

  await page.evaluate(
    ({ token, refreshToken }) => {
      localStorage.setItem('authToken', token);
      localStorage.setItem('token', token);
      if (refreshToken) localStorage.setItem('refreshToken', refreshToken);
    },
    { token: login.token!, refreshToken: login.refreshToken },
  );

  const transferNumber = required('INV_FU004_TRANSFER_NUMBER');
  await page.goto('/inventory/transfers');
  await expect(page.getByRole('heading', { name: 'Inventory Transfers', exact: true }).first()).toBeVisible({ timeout: 6 * 60_000 });
  await page.getByPlaceholder('Search transfers...', { exact: true }).fill(transferNumber);
  const row = page.locator('.border.rounded-lg').filter({ hasText: transferNumber });
  await expect(row).toContainText('Completed', { timeout: 30_000 });
  await row.getByRole('button', { name: 'View', exact: true }).click();
  const dialog = page.getByRole('dialog').filter({ hasText: `Transfer ${transferNumber}` });
  await expect(dialog).toBeVisible({ timeout: 30_000 });
  await dialog.getByRole('tab', { name: 'Transfer Controls', exact: true }).click();
  await expect(dialog.getByText('Immutable transfer action register', { exact: true })).toBeVisible();
  await expect(dialog.getByText('DiscrepancyResolved', { exact: true })).toBeVisible();
  await expect(dialog.getByText('Closed', { exact: true })).toBeVisible();
  await expect(dialog.getByText(/Resolution: REPLACEMENT_RECEIVED/)).toBeVisible();
  await expect(dialog.getByText('INV-FU-004-E2E-011 damage evidence', { exact: true }).first()).toBeVisible();
  expect(materialErrors, materialErrors.join('\n')).toEqual([]);

  await context.close();
});
