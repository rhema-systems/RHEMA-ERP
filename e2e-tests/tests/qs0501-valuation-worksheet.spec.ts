import { expect, test } from '@playwright/test';

test('QS-0501 renders the authorized line-valuation worksheet control', async ({ page }) => {
  const projectId = process.env.QS0501_PROJECT_ID;
  const interimValuationId = process.env.QS0501_VALUATION_ID;
  test.skip(!projectId || !interimValuationId, 'QS-0501 governed smoke fixture is not configured.');

  test.setTimeout(120_000);
  await page.setViewportSize({ width: 1440, height: 1000 });

  const worksheetFailures: string[] = [];
  const pageErrors: string[] = [];
  page.on('response', (response) => {
    if (response.url().includes('/api/quantity-survey/valuation-worksheets') && response.status() >= 500) {
      worksheetFailures.push(`${response.status()} ${response.url()}`);
    }
  });
  page.on('pageerror', (error) => pageErrors.push(error.message));

  await page.goto('/login');
  await page.locator('input[name="username"], input[type="text"]').fill(process.env.QS0501_USERNAME ?? 'admin');
  await page.locator('input[name="password"], input[type="password"]').fill(process.env.QS0501_PASSWORD ?? 'Admin123!');
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/(dashboard|tenant-select|home)/, { timeout: 30_000 });

  if (page.url().includes('tenant-select')) {
    const selectButton = page.locator('button').filter({ hasText: 'Select' });
    if (await selectButton.count() === 1) {
      await selectButton.click();
      await page.waitForURL(/\/(dashboard|home)/, { timeout: 30_000 });
    }
  }

  await page.goto(`/development/projects/${projectId}/commercial-admin`);
  await expect(page.getByRole('tab', { name: 'Commercial Admin', exact: true })).toHaveAttribute('data-state', 'active');

  const valuationCard = page.locator('div.rounded-lg.border.p-4').filter({
    hasText: 'QS-0501 browser acceptance fixture',
  });
  await expect(valuationCard).toHaveCount(1);
  await expect(valuationCard.getByRole('button', { name: 'Worksheet', exact: true })).toBeVisible();
  await valuationCard.getByRole('button', { name: 'Worksheet', exact: true }).click();

  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByText('Line valuation worksheet', { exact: true })).toBeVisible();
  await expect(page.getByText('No approved published BoQ is available.', { exact: true })).toBeVisible();

  await page.setViewportSize({ width: 1024, height: 768 });
  await expect(page.getByRole('dialog')).toBeVisible();
  expect(worksheetFailures).toEqual([]);
  expect(pageErrors).toEqual([]);
});
