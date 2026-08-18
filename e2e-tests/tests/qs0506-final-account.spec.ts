import { expect, test } from '@playwright/test';

test('QS-0506 renders the governed final-account reconciliation workspace', async ({
  page,
}) => {
  const projectId = process.env.QS0506_PROJECT_ID;
  const username = process.env.QS0506_USERNAME;
  const password = process.env.QS0506_PASSWORD;
  test.skip(
    !projectId || !username || !password,
    'QS-0506 project fixture and CAPTCHA-cleared development credentials are not configured.',
  );

  test.setTimeout(180_000);
  await page.setViewportSize({ width: 1440, height: 1000 });

  const apiFailures: string[] = [];
  const pageErrors: string[] = [];
  const consoleErrors: string[] = [];
  page.on('response', (response) => {
    if (
      response.url().includes('/api/quantity-survey/final-accounts') &&
      response.status() >= 500
    ) {
      apiFailures.push(`${response.status()} ${response.url()}`);
    }
  });
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });

  await page.goto('/login');
  await page
    .locator('input[name="username"], input[type="text"]')
    .fill(username!);
  await page
    .locator('input[name="password"], input[type="password"]')
    .fill(password!);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/(dashboard|tenant-select|home)/, {
    timeout: 30_000,
  });

  if (page.url().includes('tenant-select')) {
    const selectButton = page.locator('button').filter({ hasText: 'Select' });
    if ((await selectButton.count()) === 1) {
      await selectButton.click();
      await page.waitForURL(/\/(dashboard|home)/, { timeout: 30_000 });
    }
  }

  await page.goto(`/development/projects/${projectId}/commercial-admin`);
  const trigger = page.getByRole('button', {
    name: 'Final account workspace',
    exact: true,
  });
  await expect(trigger).toBeVisible({ timeout: 45_000 });
  await trigger.click();

  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(
    page.getByText('Final account reconciliation', { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText('No governed final account has been prepared.', {
      exact: true,
    }),
  ).toBeVisible({ timeout: 45_000 });
  await expect(
    page.getByText(
      'Approved BoQ, contract changes, certificates, deductions, retention and Finance payments reconcile before closure.',
      { exact: true },
    ),
  ).toBeVisible();

  await page.setViewportSize({ width: 1024, height: 768 });
  await expect(page.getByRole('dialog')).toBeVisible();
  expect(apiFailures).toEqual([]);
  expect(pageErrors).toEqual([]);
  expect(consoleErrors).toEqual([]);
});
