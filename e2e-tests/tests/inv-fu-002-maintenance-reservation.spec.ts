import { expect, test } from '@playwright/test';

const required = (name: string): string => {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} must be configured for INV-REQ-FU-002 acceptance.`);
  return value;
};

test('INV-REQ-FU-002 renders the governed work-order reservation read-back', async ({ browser }) => {
  test.setTimeout(3 * 60_000);
  const api = process.env.E2E_API_URL?.trim() || 'http://127.0.0.1:5000';
  const workOrderId = required('INV_FU002_WORK_ORDER_ID');
  const itemCode = required('INV_FU002_ITEM_CODE');
  const expectedUsage = required('INV_FU002_EXPECTED_USAGE');
  const tenantCode = process.env.INV_FU002_TENANT_CODE?.trim() || 'DEFAULT';
  const context = await browser.newContext({
    baseURL: process.env.E2E_BASE_URL?.trim() || 'http://127.0.0.1:3001',
  });
  const page = await context.newPage();
  const materialErrors: string[] = [];
  const isExpectedBaseCurrencyDenial = (value: string) =>
    value.includes('/finance/Currencies/base') && value.includes('403');

  page.on('pageerror', error => materialErrors.push(`pageerror: ${error.message}`));
  page.on('console', message => {
    if (message.type() !== 'error') return;
    const value = message.text();
    // The work-order page formats money with a safe local fallback. A Stores Officer is
    // intentionally denied the Finance-only base-currency endpoint; the response listener
    // below proves that this is the only allowed HTTP denial in the browser journey.
    if (isExpectedBaseCurrencyDenial(value) || value.includes('Failed to load resource: the server responded with a status of 403')) return;
    materialErrors.push(`console: ${value}`);
  });
  page.on('requestfailed', request =>
    materialErrors.push(`requestfailed: ${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`),
  );
  page.on('response', response => {
    if (response.status() >= 400 &&
        !(response.status() === 403 && response.url().includes('/finance/Currencies/base'))) {
      materialErrors.push(`response: ${response.status()} ${response.request().method()} ${response.url()}`);
    }
  });

  await page.goto('/login');
  await expect(page.getByRole('button', { name: 'Sign In', exact: true })).toBeVisible();
  const response = await context.request.post(`${api}/api/auth/login`, {
    data: {
      username: required('INV_FU002_USERNAME'),
      password: required('INV_FU002_PASSWORD'),
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

  const detailResponse = await context.request.get(`${api}/api/maintenance/work-orders/${workOrderId}`, {
    headers: { Authorization: `Bearer ${login.token}` },
  });
  const detailBody = await detailResponse.text();
  expect(detailResponse.status(), detailBody).toBe(200);
  const detail = JSON.parse(detailBody) as { id?: string; parts?: Array<{ itemCode?: string; status?: string }> };
  expect(detail.id?.toLowerCase()).toBe(workOrderId.toLowerCase());
  expect(detail.parts?.some(part => part.itemCode === itemCode && part.status === 'Returned')).toBe(true);

  await page.goto(`/maintenance/work-orders?id=${encodeURIComponent(workOrderId)}`);
  await expect(page.getByRole('heading', { name: 'Work Order Details' })).toBeVisible({ timeout: 90_000 });
  await page.getByRole('tab', { name: 'Parts' }).click();
  const acceptedRow = page.getByRole('row').filter({ hasText: itemCode }).filter({ hasText: 'Returned' });
  await expect(acceptedRow).toContainText(expectedUsage, { timeout: 30_000 });
  await expect(acceptedRow).toContainText('Returned');
  expect(materialErrors, materialErrors.join('\n')).toEqual([]);

  await context.close();
});
