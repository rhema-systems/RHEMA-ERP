import { expect, test, type Page } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

type LoginPageStyle = 'LightCorporate' | 'DarkPremium';

const captureDirectory = path.resolve(
  __dirname,
  process.env.LOGIN_APPEARANCE_CAPTURE_DIR || '../../local-artifacts/login-appearance'
);

const appearances: LoginPageStyle[] = ['LightCorporate', 'DarkPremium'];
const viewports = [
  { name: 'desktop', width: 1440, height: 900 },
  { name: 'mobile', width: 390, height: 844 },
] as const;

async function prepareDeterministicLogin(page: Page, style: LoginPageStyle) {
  await page.route('**/api/public/config/login', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({ loginPageStyle: style }),
  }));

  await page.route('**/api/auth/security-settings', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({
      passwordMinLength: 8,
      passwordRequireUppercase: true,
      passwordRequireLowercase: true,
      passwordRequireDigits: true,
      passwordRequireSpecialChars: true,
      captchaEnabled: false,
      captchaProvider: 'recaptcha',
      recaptchaSiteKey: null,
      hCaptchaSiteKey: null,
    }),
  }));

  await page.route('**/api/tenant', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify([{
      id: 'capture-tenant',
      code: 'CAPTURE',
      name: 'Capture Tenant',
      isActive: true,
      allowSelfRegistration: true,
    }]),
  }));
}

test.describe('login appearance capture', () => {
  test.beforeAll(() => fs.mkdirSync(captureDirectory, { recursive: true }));

  for (const style of appearances) {
    for (const viewport of viewports) {
      test(`${style} ${viewport.name} has only the approved controls`, async ({ page }) => {
        await page.setViewportSize(viewport);
        await prepareDeterministicLogin(page, style);
        await page.goto('/login', { waitUntil: 'networkidle' });

        const shell = page.getByTestId('login-shell');
        await expect(shell).toHaveAttribute('data-login-style', style);

        const form = page.getByTestId('shared-login-form');
        await expect(form).toBeVisible();
        await expect(form.getByLabel('Username or Email Address')).toBeVisible();
        await expect(form.locator('input#password')).toBeVisible();
        await expect(form.getByRole('button', { name: 'Show password' })).toBeVisible();
        await expect(form.getByRole('link', { name: 'Forgot password?' })).toHaveAttribute('href', '/forgot-password');
        await expect(form.getByRole('button', { name: 'Sign In' })).toBeVisible();
        await expect(form.getByRole('link', { name: 'Apply as a supplier' })).toHaveAttribute('href', '/supplier-application');

        const controls = form.locator('input, button, a, select, textarea');
        await expect(controls).toHaveCount(6);
        await expect(form.getByText(/remember me/i)).toHaveCount(0);
        await expect(form.getByText(/microsoft|google|single sign-on|\bsso\b/i)).toHaveCount(0);

        await page.evaluate(async () => {
          await document.fonts.ready;
          await Promise.all(Array.from(document.images).map(image => image.complete
            ? Promise.resolve()
            : new Promise<void>(resolve => {
                image.addEventListener('load', () => resolve(), { once: true });
                image.addEventListener('error', () => resolve(), { once: true });
              })));
        });

        await page.screenshot({
          path: path.join(captureDirectory, `${style}-${viewport.name}.png`),
          fullPage: true,
          animations: 'disabled',
        });
      });
    }
  }
});
