import { expect, test } from '@playwright/test';

test('QS-0507 renders the governed material-reconciliation workspace', async ({ page }) => {
  const projectId = process.env.QS0507_PROJECT_ID;
  const username = process.env.QS0507_USERNAME;
  const password = process.env.QS0507_PASSWORD;
  test.skip(
    !projectId || !username || !password,
    'QS-0507 project fixture and verified development credentials are not configured.',
  );

  test.setTimeout(180_000);
  await page.setViewportSize({ width: 1440, height: 1000 });
  const apiFailures: string[] = [];
  const pageErrors: string[] = [];
  const consoleErrors: string[] = [];
  page.on('response', (response) => {
    if (response.url().includes('/api/quantity-survey/material-reconciliations') && response.status() >= 500)
      apiFailures.push(`${response.status()} ${response.url()}`);
  });
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });

  await page.goto('/login');
  await page.locator('input[name="username"], input[type="text"]').fill(username!);
  await page.locator('input[name="password"], input[type="password"]').fill(password!);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/(dashboard|tenant-select|home)/, { timeout: 30_000 });
  if (page.url().includes('tenant-select')) {
    const selectButton = page.locator('button').filter({ hasText: 'Select' });
    if ((await selectButton.count()) === 1) {
      await selectButton.click();
      await page.waitForURL(/\/(dashboard|home)/, { timeout: 30_000 });
    }
  }

  await page.goto(`/development/projects/${projectId}/commercial-admin`);
  await page.getByRole('button', { name: 'Material reconciliation', exact: true }).click();
  await expect(page.getByRole('dialog')).toBeVisible({ timeout: 45_000 });
  await expect(page.getByText('Governed material reconciliation', { exact: true })).toBeVisible();
  await expect(page.getByText('central-DMS evidence', { exact: false })).toBeVisible();
  await expect(page.getByText('Select valuation and Works contract', { exact: true })).toBeVisible();

  expect(apiFailures).toEqual([]);
  expect(pageErrors).toEqual([]);
  expect(consoleErrors).toEqual([]);
});
