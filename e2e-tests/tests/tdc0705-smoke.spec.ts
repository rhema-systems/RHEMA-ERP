import { expect, test } from '@playwright/test';

const tenant = {
  id: '00000000-0000-0000-0000-000000000001',
  code: 'DEMO',
  name: 'Demo tenant',
  description: 'TDC-0705 browser smoke',
  isActive: true,
  createdAt: '2026-08-05T00:00:00Z',
};

const report = {
  id: '07050000-0000-0000-0000-000000000001',
  name: 'Procurement spend',
  description: 'Published procurement report',
  category: 'Procurement',
  type: 'Table',
  status: 'published',
  isActive: true,
};

const template = {
  id: '07050000-0000-0000-0000-000000000002',
  reportId: report.id,
  reportName: report.name,
  templateKey: 'BOARD-QUARTERLY-SPEND',
  version: 2,
  name: 'Board quarterly spend',
  description: 'Board pack',
  category: 'Procurement',
  type: 'Table',
  audience: 'Board',
  cadence: 'Quarterly',
  status: 'Published',
  defaultOutputFormat: 'Online',
  outputFormats: ['Online', 'XLSX', 'PDF'],
  savedFilters: { pageSize: 25 },
  generationMetadata: { preparedFor: 'Board' },
  isCustom: true,
  createdBy: 'TDC smoke',
  createdAt: '2026-08-05T00:00:00Z',
  usageCount: 1,
  rowVersion: 'AAAAAAAAB9U=',
};

test('TDC-0705 renders the shared configurable template lifecycle', async ({ page }) => {
  const pageErrors: string[] = [];
  const serverErrors: string[] = [];
  page.on('pageerror', error => pageErrors.push(error.message));
  page.on('response', response => {
    if (response.status() >= 500) serverErrors.push(`${response.status()} ${response.url()}`);
  });

  await page.addInitScript(({ currentTenant }) => {
    localStorage.setItem('authToken', 'tdc0705.browser.smoke');
    localStorage.setItem('token', 'tdc0705.browser.smoke');
    localStorage.setItem('currentTenantCode', currentTenant.code);
    localStorage.setItem('currentTenant', JSON.stringify(currentTenant));
    localStorage.setItem('user', JSON.stringify({
      id: '58cafd8b-42ce-4f67-0dbb-08de862e82ee',
      username: 'tdc0705-smoke',
      email: 'smoke@local.test',
      roles: ['TenantAdmin'],
      permissions: ['reports.create', 'reports.read'],
      currentTenantCode: currentTenant.code,
    }));
  }, { currentTenant: tenant });

  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname.toLowerCase();
    const headers = {
      'access-control-allow-origin': 'http://localhost:3000',
      'access-control-allow-headers': 'authorization,content-type',
      'access-control-allow-methods': 'GET,POST,PUT,PATCH,DELETE,OPTIONS',
    };
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers });
      return;
    }
    if (path === '/api/tenant') {
      await route.fulfill({ json: [tenant], headers });
      return;
    }
    if (path === '/api/reports/templates') {
      await route.fulfill({ json: [template], headers });
      return;
    }
    if (path === '/api/reports/admin') {
      await route.fulfill({ json: [report], headers });
      return;
    }
    if (path === '/api/notifications') {
      await route.fulfill({ json: { items: [], totalCount: 0, pageNumber: 1, pageSize: 10 }, headers });
      return;
    }
    await route.fulfill({ json: [], headers });
  });

  await page.goto('/administration/reports');
  await expect(page.getByRole('tab', { name: 'Templates' })).toBeVisible({ timeout: 30_000 });
  await page.getByRole('tab', { name: 'Templates' }).click();

  await expect(page.getByText('Board quarterly spend', { exact: true })).toBeVisible();
  await expect(page.getByText('BOARD-QUARTERLY-SPEND · v2 · Procurement spend', { exact: true })).toBeVisible();
  await expect(page.getByText('Board', { exact: true })).toBeVisible();
  await expect(page.getByText('Quarterly', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Generate Online' })).toBeVisible();
  await expect(page.getByLabel('Clone revision')).toBeVisible();
  await expect(page.getByLabel('Archive')).toBeVisible();
  await expect(page.getByRole('button', { name: 'New template' })).toBeVisible();

  expect(pageErrors).toEqual([]);
  expect(serverErrors).toEqual([]);
});
