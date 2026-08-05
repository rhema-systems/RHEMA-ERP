import { mkdirSync } from 'node:fs';
import { expect, test } from '@playwright/test';

test('TDC-0509 renders the shared GRN and MRN receipt-document control', async ({ page }) => {
  test.setTimeout(90_000);
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

  const receiptId = process.env.TDC0509_RECEIPT_ID ?? 'a89bbc04-4ddb-46e7-889f-3583c9e36063';
  await page.goto(`/procurement/purchase-receipts/${receiptId}`);
  await page.getByRole('tab', { name: /GRN.*MRN|documents/i }).click();

  await expect(page.getByTestId('receipt-document-control')).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText('GRN/MRN document control', { exact: true })).toBeVisible();
  await expect(page.locator('body')).toContainText('DEC-001');
  await expect(page.locator('body')).toContainText('DEC-014');
  await expect(page.locator('body')).toContainText(/central DMS/i);
  await expect(page.locator('body')).toContainText(/inspection|acceptance/i);
  await expect(page.locator('body')).toContainText(/immutable audit history/i);

  mkdirSync('artifacts/tdc0509-browser-smoke', { recursive: true });
  await page.screenshot({
    path: 'artifacts/tdc0509-browser-smoke/receipt-document-control.png',
    fullPage: true,
  });

  expect(serverErrors).toEqual([]);
  expect(pageErrors).toEqual([]);
});
