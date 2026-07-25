import { test, expect } from '@playwright/test';

test.describe('Order-to-Cash (O2C) Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto('/login');
    await page.fill('input[name="username"]', 'admin');
    await page.fill('input[name="password"]', 'Admin123!');
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

  test('should navigate through O2C', async ({ page }) => {
    // 1. Sales Order Creation
    console.log('Navigating to Sales Orders...');
    await page.goto('/sales/orders');
    
    // Check if the page loads successfully
    await expect(page.locator('h1').or(page.locator('h2')).filter({ hasText: /Sales Order/i }).first()).toBeVisible({ timeout: 15000 });
    
    // We can simulate clicking "New Order" if the button exists
    const newButton = page.locator('button').filter({ hasText: /New|Create|Add/i });
    if (await newButton.count() > 0) {
      await newButton.first().click();
      await page.waitForTimeout(1000); // Wait for potential modal or navigation
    }
    
    // Proceed to verify other screens
    await page.goto('/sales/deliveries');
    await expect(page.locator('body')).not.toContainText('Application error', { timeout: 10000 });

    await page.goto('/finance/invoices'); // Adjust path based on actual frontend routing
    await expect(page.locator('body')).not.toContainText('Application error', { timeout: 10000 });
  });
});
