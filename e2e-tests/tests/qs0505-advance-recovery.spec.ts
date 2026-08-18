import { expect, test } from '@playwright/test';

test('QS-0505 renders the governed Finance advance-recovery workspace', async ({
  page,
}) => {
  const projectId = process.env.QS0505_PROJECT_ID;
  const username = process.env.QS0505_USERNAME;
  const password = process.env.QS0505_PASSWORD;
  test.skip(
    !projectId || !username || !password,
    'QS-0505 project fixture and verified development credentials are not configured.'
  );

  test.setTimeout(180_000);
  await page.setViewportSize({ width: 1440, height: 1000 });

  const apiFailures: string[] = [];
  const pageErrors: string[] = [];
  const consoleErrors: string[] = [];
  page.on('response', (response) => {
    if (
      response.url().includes('/api/quantity-survey/advance-recoveries') &&
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
    name: 'Advance recovery',
    exact: true,
  });
  await expect(trigger).toBeVisible({ timeout: 45_000 });
  await trigger.click();

  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(
    page.getByText('Governed advance recovery', { exact: true })
  ).toBeVisible();
  await expect(
    page.getByText('No governed recovery agreement yet.', { exact: true })
  ).toBeVisible({ timeout: 45_000 });
  await expect(
    page.getByText('Finance supplier advance and Works contract', {
      exact: true,
    })
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Prepare agreement', exact: true })
  ).toBeDisabled();

  expect(apiFailures).toEqual([]);
  expect(pageErrors).toEqual([]);
  expect(consoleErrors).toEqual([]);
});
