import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { InvoiceMatchExceptionControl } from './InvoiceMatchExceptionControl';
import type { VendorInvoiceMatchExceptionOverview } from '@/types/ap';

const overview: VendorInvoiceMatchExceptionOverview = {
  vendorInvoiceId: 'invoice-0507',
  invoiceNumber: 'AP-0507',
  matchingReadiness: {
    vendorInvoiceId: 'invoice-0507',
    matchingType: 'ThreeWay',
    matchingStatus: 'MatchException',
    isMatched: false,
    discrepancies: [{
      itemDescription: 'Controlled item',
      discrepancyType: 'Price',
      invoiceValue: 110,
      expectedValue: 100,
      variance: 10,
      variancePercentage: 10,
      exceptionEligible: true,
    }],
    invoiceTotal: 110,
    purchaseOrderTotal: 100,
    goodsReceiptTotal: 100,
    tolerancePercentage: 1,
    priceTolerancePercentage: 1,
    quantityTolerancePercentage: 1,
    isRequired: true,
    approvalReady: false,
    approvedExceptionApplied: false,
    message: 'An independently approved exception is required.',
    decisionKeys: Array.from({ length: 14 }, (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`),
    checks: [],
  },
  canRequest: true,
  canDecide: false,
  canCancel: false,
  canCompleteCorrectiveAction: false,
  requiredEvidenceKeys: ['ROOT_CAUSE_EVIDENCE', 'CORRECTIVE_ACTION_PLAN'],
  decisionKeys: Array.from({ length: 14 }, (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`),
  history: [],
};

describe('InvoiceMatchExceptionControl', () => {
  it('keeps relevant exception actions without internal labels in compact mode', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const markup = renderToStaticMarkup(
      <QueryClientProvider client={client}>
        <InvoiceMatchExceptionControl compact invoiceId="invoice-0507" initialOverview={overview} />
      </QueryClientProvider>
    );
    expect(markup).toContain('Matching exceptions');
    expect(markup).toContain('Request exception');
    expect(markup).not.toContain('AP-006');
    expect(markup).not.toContain('DEC-001');
    expect(markup).not.toContain('does not create, allocate');
  });

  it('omits the empty exception panel in compact mode', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const markup = renderToStaticMarkup(
      <QueryClientProvider client={client}>
        <InvoiceMatchExceptionControl compact invoiceId="invoice-0507" initialOverview={{ ...overview, canRequest: false }} />
      </QueryClientProvider>
    );
    expect(markup).toBe('');
  });

  it('renders the dedicated AP-006 lifecycle and keeps payment allocation out of the control', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const markup = renderToStaticMarkup(
      <QueryClientProvider client={client}>
        <InvoiceMatchExceptionControl invoiceId="invoice-0507" initialOverview={overview} />
      </QueryClientProvider>
    );

    expect(markup).toContain('AP-006 match-exception register');
    expect(markup).toContain('Request exception');
    expect(markup).toContain('does not create, allocate, authorize, or post a supplier payment');
    expect(markup).toContain('DEC-001');
    expect(markup).toContain('DEC-014');
    expect(markup).not.toContain('Allocate payment');
  });
});
