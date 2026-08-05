import AxeBuilder from '@axe-core/playwright';
import { expect, Page, test } from '@playwright/test';

const batchId = 'b25c5af4-8f66-4b0f-89d9-c6da92eb5a22';
const periodId = '9135b91c-6059-45c7-8642-348c36e74e13';
const sessionId = '6bc52c60-6b2a-4466-a524-0acbf64a54de';

async function establishMockSession(page: Page) {
  const tenant = {
    id: '8ca2f96c-9029-4e32-87ae-4d17f739eb45',
    name: 'Journal Batch E2E Tenant',
    code: 'JBE2E',
    isActive: true,
    createdAt: '2026-07-29T00:00:00Z',
  };
  const user = {
    id: 'e10a183a-df26-46ef-a9a8-270882103083',
    username: 'journal.batch.e2e',
    email: 'journal.batch.e2e@example.test',
    firstName: 'Journal',
    lastName: 'Batch',
    roles: ['Administrator'],
    permissions: ['*'],
    isActive: true,
    currentTenantCode: tenant.code,
    accessibleTenants: [
      {
        tenantId: tenant.id,
        tenantCode: tenant.code,
        tenantName: tenant.name,
        accessLevel: 'Full',
        isDefault: true,
      },
    ],
  };
  await page.addInitScript(
    ({ mockUser, mockTenant }) => {
      const token = 'eyJhbGciOiJub25lIn0.eyJzdWIiOiJlMmUiLCJleHAiOjQxMDI0NDQ4MDB9.e2e';
      localStorage.setItem('authToken', token);
      localStorage.setItem('token', token);
      localStorage.setItem('tokenExpiry', '4102444800000');
      localStorage.setItem('user', JSON.stringify(mockUser));
      localStorage.setItem('currentTenantCode', mockTenant.code);
      localStorage.setItem('currentTenant', JSON.stringify(mockTenant));
    },
    { mockUser: user, mockTenant: tenant },
  );
  await page.route('**/api/auth/me', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(user) }),
  );
  await page.route('**/api/tenant', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([tenant]) }),
  );
}

async function expectNoSeriousAccessibilityViolations(page: Page) {
  const result = await new AxeBuilder({ page }).analyze();
  const serious = result.violations.filter(
    violation => violation.impact === 'serious' || violation.impact === 'critical',
  );
  expect(
    serious,
    serious
      .flatMap(violation =>
        violation.nodes.map(
          node => `${violation.id}: ${violation.help}; ${node.target.join(' ')}; ${node.html}`,
        ),
      )
      .join('\n'),
  ).toEqual([]);
}

test.describe('Journal batches release gates', () => {
  test.beforeEach(async ({ page }) => {
    await establishMockSession(page);
    await page.route('**/api/finance/journal-batches?**', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: batchId,
              batchNumber: 'JB-2026-00001',
              description: 'Month-end close',
              fiscalPeriodName: 'July 2026',
              bookClassification: 'IFRS',
              controlCurrencyCode: 'GHS',
              expectedDebitTotal: 100,
              actualDebitTotal: 100,
              variance: 0,
              entryCount: 1,
              postedEntryCount: 0,
              approvalStatus: 'Draft',
              displayStatus: 'Draft',
            },
          ],
          totalCount: 1,
          pageNumber: 1,
          pageSize: 200,
          totalPages: 1,
        }),
      }),
    );
    await page.route('**/api/finance/fiscal-periods', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: periodId,
            periodName: 'July 2026',
            periodCode: '2026-07',
            periodStatus: 'Open',
            status: 'Open',
            isOpen: true,
          },
        ]),
      }),
    );
  });

  test('register, creation form, and import preview meet serious accessibility gate', async ({ page }) => {
    for (const [route, heading] of [
      ['/finance/journal-batches', 'Journal Batches'],
      ['/finance/journal-batches/new', 'New journal batch'],
      ['/finance/journal-batches/import', 'Import journal batch'],
    ] as const) {
      await page.goto(route);
      await expect(page.getByRole('heading', { level: 1, name: heading })).toBeVisible();
      await expectNoSeriousAccessibilityViolations(page);
    }
  });

  test('creates a controlled batch through the user workflow', async ({ page }) => {
    let createPayload: Record<string, unknown> | undefined;
    await page.route('**/api/finance/journal-batches', async route => {
      if (route.request().method() !== 'POST') {
        await route.fallback();
        return;
      }
      createPayload = route.request().postDataJSON();
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: batchId,
          batchNumber: 'JB-2026-00001',
          description: 'Month-end close',
          items: [],
        }),
      });
    });

    await page.goto('/finance/journal-batches/new');
    await page.getByLabel('Description').fill('Month-end close');
    await page.getByLabel('Fiscal period').click();
    await page.getByRole('option', { name: 'July 2026' }).click();
    await page.getByLabel('Expected debit total').fill('100');
    await page.getByLabel('Expected journal count (optional)').fill('1');
    await page.getByRole('button', { name: 'Create batch' }).click();

    await expect(page).toHaveURL(new RegExp(`/finance/journal-batches/${batchId}$`), {
      timeout: 15_000,
    });
    expect(createPayload).toMatchObject({
      description: 'Month-end close',
      fiscalPeriodId: periodId,
      expectedDebitTotal: 100,
      expectedJournalCount: 1,
      controlCurrencyCode: 'GHS',
    });
  });

  test('previews and commits a spreadsheet import through the user workflow', async ({ page }) => {
    await page.route('**/api/finance/journal-batches/imports/preview', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          sessionId,
          previewToken: 'preview-token',
          expiresAt: '2026-07-29T12:00:00Z',
          isValid: true,
          templateVersion: '1',
          fileName: 'journal-batch.xlsx',
          journalCount: 1,
          lineCount: 2,
          expectedDebitTotal: 100,
          actualDebitTotal: 100,
          issues: [],
        }),
      }),
    );
    await page.route(`**/api/finance/journal-batches/imports/${sessionId}/commit`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: batchId,
          batchNumber: 'JB-2026-00001',
          description: 'Imported month-end close',
          items: [],
        }),
      }),
    );

    await page.goto('/finance/journal-batches/import');
    await page.getByLabel('Journal batch workbook').setInputFiles({
      name: 'journal-batch.xlsx',
      mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      buffer: Buffer.from('release-gate-placeholder'),
    });
    await page.getByRole('button', { name: 'Preview' }).click();
    await expect(page.getByText('Workbook is ready to import')).toBeVisible();
    await page.getByRole('button', { name: 'Commit import' }).click();

    await expect(page).toHaveURL(new RegExp(`/finance/journal-batches/${batchId}$`), {
      timeout: 15_000,
    });
  });
});
