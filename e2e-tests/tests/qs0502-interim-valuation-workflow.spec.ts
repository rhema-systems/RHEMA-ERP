import { expect, test } from '@playwright/test';

test('QS-0502 renders the governed interim-valuation workflow control', async ({
  page,
}) => {
  const projectId = process.env.QS0502_PROJECT_ID;
  const interimValuationId = process.env.QS0502_VALUATION_ID;
  const username = process.env.QS0502_USERNAME;
  const password = process.env.QS0502_PASSWORD;
  test.skip(
    !projectId || !interimValuationId || !username || !password,
    'QS-0502 governed smoke fixture and credentials are not configured.',
  );

  test.setTimeout(180_000);
  await page.setViewportSize({ width: 1440, height: 1000 });

  const valuationFailures: string[] = [];
  const pageErrors: string[] = [];
  page.on('response', (response) => {
    if (
      response.url().includes('/api/quantity-survey/valuation-worksheets') &&
      response.status() >= 500
    ) {
      valuationFailures.push(`${response.status()} ${response.url()}`);
    }
  });
  page.on('pageerror', (error) => pageErrors.push(error.message));

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
  await expect(
    page.getByRole('tab', { name: 'Commercial Admin', exact: true }),
  ).toHaveAttribute('data-state', 'active');

  const valuationCard = page.locator('div.rounded-lg.border.p-4').filter({
    hasText: 'QS-0502 browser acceptance fixture',
  });
  await expect(valuationCard).toHaveCount(1);
  await expect(
    valuationCard.getByRole('button', { name: 'Worksheet', exact: true }),
  ).toBeVisible();
  await valuationCard
    .getByRole('button', { name: 'Worksheet', exact: true })
    .click();

  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(
    page.getByText('Line valuation worksheet', { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText('Loading recorded measurements and prior certificates…', {
      exact: true,
    }),
  ).toBeHidden({ timeout: 45_000 });
  await expect(
    page.getByText('No approved published BoQ is available.', { exact: true }),
  ).toBeVisible({
    timeout: 10_000,
  });

  await page.setViewportSize({ width: 1024, height: 768 });
  await expect(page.getByRole('dialog')).toBeVisible();
  expect(valuationFailures).toEqual([]);
  expect(pageErrors).toEqual([]);
});
