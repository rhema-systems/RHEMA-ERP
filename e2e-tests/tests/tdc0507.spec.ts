import { expect, test } from '@playwright/test';

const invoiceId = '05070000-0000-0000-0000-000000000001';
const purchaseOrderId = '05070000-0000-0000-0000-000000000002';

const matchingReadiness = {
  vendorInvoiceId: invoiceId,
  matchingType: 'ThreeWay',
  matchingStatus: 'Unmatched',
  isMatched: false,
  discrepancies: [{
    itemDescription: 'Controlled browser-smoke variance',
    discrepancyType: 'Price',
    invoiceValue: 105,
    expectedValue: 100,
    variance: 5,
    variancePercentage: 5,
    exceptionEligible: true,
  }],
  invoiceTotal: 105,
  purchaseOrderTotal: 100,
  goodsReceiptTotal: 100,
  tolerancePercentage: 2,
  priceTolerancePercentage: 2,
  quantityTolerancePercentage: 2,
  isRequired: true,
  approvalReady: false,
  approvedExceptionApplied: false,
  snapshotHash: 'a'.repeat(64),
  message: 'An AP-006 exception is required.',
  configurationProfileCode: 'TDC-PROCUREMENT',
  configurationProfileVersion: 1,
  decisionKeys: Array.from({ length: 14 }, (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`),
  checks: [{
    checkKey: 'price-tolerance',
    label: 'Price tolerance',
    passed: false,
    exceptionEligible: true,
    message: 'The variance can enter the controlled exception workflow.',
  }],
};

test('TDC-0507 renders its dedicated AP-006 control without a payment-allocation action', async ({ page }) => {
  await page.goto('/login');
  await page.locator('input[name="username"], input[type="text"]').fill('admin');
  await page.locator('input[name="password"], input[type="password"]').fill('Admin123!');
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/(dashboard|tenant-select|home)/, { timeout: 30_000 });

  if (page.url().includes('tenant-select')) {
    const selectButton = page.locator('button').filter({ hasText: 'Select' });
    if (await selectButton.count() === 1) {
      await selectButton.click();
      await page.waitForURL(/\/(dashboard|home)/, { timeout: 30_000 });
    }
  }

  await page.route('**/api/ap/invoices/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith(`/${invoiceId}/match/readiness`)) {
      await route.fulfill({ json: matchingReadiness });
      return;
    }
    if (path.endsWith(`/${invoiceId}/match-exceptions`)) {
      await route.fulfill({
        json: {
          vendorInvoiceId: invoiceId,
          invoiceNumber: 'INV-TDC-0507',
          matchingReadiness,
          canRequest: true,
          canDecide: false,
          canCancel: false,
          canCompleteCorrectiveAction: false,
          requiredEvidenceKeys: ['ROOT_CAUSE_EVIDENCE', 'CORRECTIVE_ACTION_PLAN'],
          decisionKeys: matchingReadiness.decisionKeys,
          history: [],
        },
      });
      return;
    }
    if (path.endsWith(`/${invoiceId}`)) {
      await route.fulfill({
        json: {
          id: invoiceId,
          invoiceNumber: 'INV-TDC-0507',
          supplierInvoiceNumber: 'SUP-0507',
          supplierId: '05070000-0000-0000-0000-000000000003',
          supplierName: 'TDC Browser Smoke Supplier',
          purchaseOrderId,
          purchaseOrderNumber: 'PO-TDC-0507',
          invoiceDate: new Date().toISOString(),
          dueDate: new Date(Date.now() + 86_400_000).toISOString(),
          subTotal: 100,
          taxAmount: 5,
          discountAmount: 0,
          totalAmount: 105,
          paidAmount: 0,
          balanceAmount: 105,
          currencyCode: 'GHS',
          exchangeRate: 1,
          baseCurrencyAmount: 105,
          paymentTermsDays: 30,
          earlyPaymentDiscountPercentage: 0,
          earlyPaymentDiscountAmount: 0,
          withholdingTaxRate: 0,
          withholdingTaxAmount: 0,
          matchingType: 'ThreeWay',
          matchingStatus: 'Unmatched',
          matchingPriceTolerancePercent: 2,
          matchingQuantityTolerancePercent: 2,
          status: 'Draft',
          approvalStatus: 'Draft',
          isOpeningBalance: false,
          lineItems: [],
          paymentAllocations: [],
          createdAt: new Date().toISOString(),
        },
      });
      return;
    }
    await route.continue();
  });

  await page.goto(`/finance/ap/invoices/${invoiceId}`);
  await expect(page.getByText('AP-006 match-exception register', { exact: true })).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(
    'Controlled tolerance exception only. It does not create, allocate, authorize, or post a supplier payment.',
    { exact: true },
  )).toBeVisible();

  const requestButton = page.getByRole('button', { name: 'Request exception' });
  await expect(requestButton).toBeVisible();
  await requestButton.click();
  await expect(page.getByText('Required controlled evidence', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Submit to dual Finance approval' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Allocate payment' })).toHaveCount(0);
});
