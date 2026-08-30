import { expect, test } from '@playwright/test';

const required = (name: string): string => {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} must be configured for INV-REQ-FU-003 acceptance.`);
  return value;
};

test('INV-REQ-FU-003 renders the governed reversed fixed-asset return', async ({ browser }) => {
  test.setTimeout(3 * 60_000);
  const api = process.env.E2E_API_URL?.trim() || 'http://127.0.0.1:5100';
  const requisitionNumber = required('INV_FU003_REQUISITION_NUMBER');
  const returnVoucherNumber = required('INV_FU003_RETURN_VOUCHER_NUMBER');
  const expectedValue = required('INV_FU003_EXPECTED_VALUE');
  const tenantCode = process.env.INV_FU003_TENANT_CODE?.trim() || 'DEFAULT';
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
    if (response.status() >= 400) {
      materialErrors.push(`response: ${response.status()} ${response.request().method()} ${response.url()}`);
    }
  });

  await page.goto('/login');
  await expect(page.getByRole('button', { name: 'Sign In', exact: true })).toBeVisible();
  const response = await context.request.post(`${api}/api/auth/login`, {
    data: {
      username: required('INV_FU003_USERNAME'),
      password: required('INV_FU003_PASSWORD'),
      tenantCode,
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

  const voucherResponse = await context.request.get(
    `${api}/api/inventory/requisitions/return-vouchers`,
    { headers: { Authorization: `Bearer ${login.token}` } },
  );
  const voucherBody = await voucherResponse.text();
  expect(voucherResponse.status(), voucherBody).toBe(200);
  const vouchers = JSON.parse(voucherBody) as Array<{
    voucherNumber?: string;
    requisitionNumber?: string;
    status?: string;
    totalValue?: number;
  }>;
  const expectedVoucher = vouchers.find(voucher => voucher.voucherNumber === returnVoucherNumber);
  expect(expectedVoucher).toMatchObject({ requisitionNumber, status: 'Reversed' });
  expect(Number(expectedVoucher?.totalValue).toFixed(2)).toBe(Number(expectedValue).toFixed(2));

  await page.goto('/inventory/requisitions');
  await expect(page.getByRole('heading', { name: 'Inventory Requisitions', exact: true })).toBeVisible({ timeout: 90_000 });
  await page.getByPlaceholder('Search requisitions...', { exact: true }).fill(requisitionNumber);
  const row = page.getByRole('row').filter({ hasText: requisitionNumber });
  await expect(row).toContainText('Issued', { timeout: 30_000 });
  await row.getByRole('button', { name: 'Return Items', exact: true }).click();
  await expect(page.getByRole('heading', { name: `Return Issued Items#${requisitionNumber}`, exact: true })).toBeVisible({ timeout: 30_000 });
  const voucher = page.getByText(returnVoucherNumber, { exact: true });
  await expect(voucher).toBeVisible();
  const voucherCard = voucher.locator('..').locator('..');
  await expect(voucherCard).toContainText('Reversed');
  await expect(voucherCard).toContainText(expectedValue);
  expect(materialErrors, materialErrors.join('\n')).toEqual([]);

  await context.close();
});
