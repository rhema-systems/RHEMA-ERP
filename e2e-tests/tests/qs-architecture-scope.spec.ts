import { expect, test } from '@playwright/test';

// Real login and real project data. No mocked API responses or generated bearer tokens.
test('architecture QS screens retain core work and hide optional tools', async ({ page }) => {
  test.skip(process.env.QS_SCOPE_INTERACTIVE !== '1', 'Enable QS_SCOPE_INTERACTIVE and sign in in the headed browser.');
  test.setTimeout(12 * 60_000);
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('response', response => {
    if (response.status() >= 500 && /\/api\//.test(response.url())) errors.push(`${response.status()} ${new URL(response.url()).pathname}`);
  });
  await page.goto('/login');
  console.log('Sign in in the test browser and open a project you are authorized to access.');
  await page.waitForURL(/\/development\/projects\/[0-9a-f-]{36}(?:\/|$)/i, { timeout: 10 * 60_000 });
  const projectRoot = new URL(page.url()).pathname.match(/\/development\/projects\/[0-9a-f-]{36}/i)![0];
  await page.goto(`${projectRoot}/packages`);
  await expect(page.getByText('Work Components & BOQ', { exact: true })).toBeVisible({ timeout: 60_000 });
  await expect(page.getByRole('button', { name: /Joint measurements/i })).toHaveCount(0);
  await page.screenshot({ path: '../local-artifacts/qs-architecture-packages.png', fullPage: true });
  await page.goto(`${projectRoot}/commercial-admin`);
  await expect(page.getByRole('navigation', { name: 'QS commercial process' })).toBeVisible({ timeout: 60_000 });
  for (const id of ['qs-valuations', 'qs-certificates', 'qs-changes', 'qs-retention']) await expect(page.locator(`#${id}`)).toBeVisible();
  await expect(page.getByRole('button', { name: /Advance recovery|Final account/i })).toHaveCount(0);
  await expect(page.getByText('QS Escalation Formulas', { exact: true })).toHaveCount(0);
  await page.screenshot({ path: '../local-artifacts/qs-architecture-commercial.png', fullPage: true });
  expect(errors).toEqual([]);
});
