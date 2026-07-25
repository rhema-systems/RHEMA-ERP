import { test, expect } from '@playwright/test';

test.describe('Procure-to-Pay (P2P) Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto('/login');
    await page.fill('input[name="username"], input[type="text"]', 'admin');
    await page.fill('input[name="password"], input[type="password"]', 'Admin123!');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard|tenant-select|home)/);
    
    if (page.url().includes('tenant-select')) {
      const selectButton = page.locator('button').filter({ hasText: 'Select' }).first();
      if (await selectButton.isVisible({ timeout: 5000 }).catch(() => false)) {
        await selectButton.click();
        await page.waitForURL(/\/(dashboard|home)/, { timeout: 10000 });
      }
    }
  });

  test('should navigate through P2P', async ({ page }) => {
    console.log('Navigating to Purchase Orders...');
    await page.goto('/procurement/purchase-orders');
    await expect(page.locator('body')).not.toContainText('Application error', { timeout: 10000 });

    console.log('Navigating to Goods Receipts...');
    await page.goto('/procurement/purchase-receipts');
    await expect(page.locator('body')).not.toContainText('Application error', { timeout: 10000 });

    console.log('Navigating to Vendor Invoices...');
    await page.goto('/finance/vendor-invoices');
    await expect(page.locator('body')).not.toContainText('Application error', { timeout: 10000 });

    console.log('Navigating to Vendor Payments...');
    await page.goto('/finance/vendor-payments');
    await expect(page.locator('body')).not.toContainText('Application error', { timeout: 10000 });
  });
});
