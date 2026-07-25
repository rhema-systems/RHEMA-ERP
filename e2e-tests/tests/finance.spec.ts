import { test, expect } from '@playwright/test';

test.describe('Finance Enhancements E2E Flow', () => {
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

  test('should verify VAT/WHT and Enhancements', async ({ page }) => {
    console.log('Navigating to VAT/WHT Reports...');
    await page.goto('/finance/reports/tax');
    await expect(page.locator('body')).not.toContainText('Application error', { timeout: 10000 });

    console.log('Navigating to Unit Budgets...');
    await page.goto('/finance/unit-budgets');
    await expect(page.locator('body')).not.toContainText('Application error', { timeout: 10000 });

    console.log('Navigating to Exchange Rate Trends...');
    await page.goto('/finance/exchange-rates/trends');
    await expect(page.locator('body')).not.toContainText('Application error', { timeout: 10000 });
    // Check if chart container exists (recharts usually has class .recharts-wrapper)
    // await expect(page.locator('.recharts-wrapper')).toBeVisible({ timeout: 10000 });
  });
});
