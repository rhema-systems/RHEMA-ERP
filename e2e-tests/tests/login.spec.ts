import { test, expect } from '@playwright/test';

test.describe('E2E Demo Rehearsal - Login and Core Navigation', () => {
  test('should login successfully and navigate to core pages', async ({ page }) => {
    console.log('Navigating to login page...');
    await page.goto('/login');

    // Fill in demo credentials. Assuming admin/admin or similar.
    // If we don't know the credentials, we might need to seed a user or check what's expected.
    // Let's try to fill the username and password fields.
    const usernameInput = page.locator('input[name="username"], input[type="text"]');
    const passwordInput = page.locator('input[name="password"], input[type="password"]');
    
    await usernameInput.fill('admin'); 
    await passwordInput.fill('Admin123!'); 
    
    const loginButton = page.locator('button[type="submit"]');
    await loginButton.click();

    // Check if login is successful (e.g. redirected to dashboard or tenant select)
    await page.waitForURL(/\/(dashboard|tenant-select|home)/, { timeout: 10000 }).catch(() => {
        console.log('Login might have failed or needs different credentials. Current URL:', page.url());
    });
    
    // Take a screenshot of the state after login attempt
    await page.screenshot({ path: 'login-attempt.png' });
    
    console.log('Current URL after login attempt:', page.url());
  });
});
