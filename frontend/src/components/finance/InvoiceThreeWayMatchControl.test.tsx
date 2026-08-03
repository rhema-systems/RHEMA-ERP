import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { InvoiceThreeWayMatchControl } from './InvoiceThreeWayMatchControl';

describe('InvoiceThreeWayMatchControl', () => {
  it('renders the hard stop, configured tolerances and complete decision lineage', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const markup = renderToStaticMarkup(
      <QueryClientProvider client={client}>
        <InvoiceThreeWayMatchControl
          invoiceId="invoice-0504"
          canEvaluate={false}
          initialReadiness={{
            vendorInvoiceId: 'invoice-0504',
            matchingType: 'ThreeWay',
            matchingStatus: 'MatchException',
            isMatched: false,
            discrepancies: [{
              itemDescription: 'Controlled stock item',
              discrepancyType: 'Quantity',
              invoiceValue: 11,
              expectedValue: 10,
              variance: 1,
              variancePercentage: 10,
              exceptionEligible: true,
            }],
            invoiceTotal: 110,
            purchaseOrderTotal: 100,
            goodsReceiptTotal: 10,
            tolerancePercentage: 1,
            priceTolerancePercentage: 1,
            quantityTolerancePercentage: 2,
            isRequired: true,
            approvalReady: false,
            approvedExceptionApplied: false,
            message: 'Mandatory three-way matching failed with 1 tolerance variance.',
            configurationProfileCode: 'TDC-PROCUREMENT',
            configurationProfileVersion: 1,
            decisionKeys: Array.from({ length: 14 }, (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`),
            checks: [{
              checkKey: 'AP-MATCH-QUANTITY',
              label: 'Accepted cumulative quantity',
              passed: false,
              exceptionEligible: true,
              message: 'Cumulative quantity exceeds accepted receipts.',
            }],
          }}
        />
      </QueryClientProvider>
    );

    expect(markup).toContain('Mandatory three-way matching');
    expect(markup).toContain('Approval blocked');
    expect(markup).toContain('Price tolerance');
    expect(markup).toContain('Cumulative quantity tolerance');
    expect(markup).toContain('TDC-PROCUREMENT v1');
    expect(markup).toContain('Exception eligible');
    expect(markup).toContain('TDC-0507');
    expect(markup).toContain('DEC-001');
    expect(markup).toContain('DEC-014');
    expect(markup).not.toContain('Re-evaluate');
  });
});
