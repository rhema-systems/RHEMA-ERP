import { mkdirSync } from 'node:fs';
import { expect, test } from '@playwright/test';

test('TDC-0508 renders the real read-only procurement and Finance reconciliation', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1100 });
  const serverErrors: string[] = [];
  const pageErrors: string[] = [];
  page.on('response', response => {
    if (response.status() >= 500) serverErrors.push(`${response.status()} ${response.url()}`);
  });
  page.on('pageerror', error => pageErrors.push(error.message));

  await page.goto('/login');
  await page.locator('input[name="username"], input[type="text"]').fill('admin');
  await page.locator('input[name="password"], input[type="password"]').fill('Admin123!');
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/(dashboard|tenant-select|home)/, { timeout: 30_000 });

  if (page.url().includes('tenant-select')) {
    const selectButton = page.locator('button').filter({ hasText: 'Select' });
    if (await selectButton.count() === 1) {
      await selectButton.click();
      await page.waitForURL(/\/(dashboard|home)/, { timeout: 30_000 });
    }
  }

  await page.goto('/finance/ap/reports?tab=procurement-reconciliation');
  await expect(page.getByText('Procurement and Finance reconciliation', { exact: true }))
    .toBeVisible({ timeout: 30_000 });
  await expect(page.getByText('AP-005', { exact: true })).toBeVisible();
  await expect(page.getByText('TDC-0508', { exact: true })).toBeVisible();
  await expect(page.locator('body')).toContainText('DEC-001');
  await expect(page.locator('body')).toContainText('DEC-014');
  await expect(page.getByText('COMMITMENT_MISSING', { exact: true })).toBeVisible();
  await expect(page.getByText(/does not allocate payments or post money/i)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Export CSV' })).toBeVisible();
  await expect(page.getByRole('button', { name: /create payment/i })).toHaveCount(0);
  await expect(page.getByRole('button', { name: /post journal/i })).toHaveCount(0);

  mkdirSync('artifacts/tdc0508-browser-smoke', { recursive: true });
  await page.screenshot({
    path: 'artifacts/tdc0508-browser-smoke/procurement-finance-reconciliation.png',
    fullPage: true,
  });

  expect(serverErrors).toEqual([]);
  expect(pageErrors).toEqual([]);
});
