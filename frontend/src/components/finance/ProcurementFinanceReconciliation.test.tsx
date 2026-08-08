import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { ProcurementFinanceReconciliation } from './ProcurementFinanceReconciliation';
import type { ProcurementFinanceReconciliationReport } from '@/types/ap';

const report: ProcurementFinanceReconciliationReport = {
    asOfDate: '2026-07-31T00:00:00Z',
    generatedAtUtc: '2026-07-31T20:00:00Z',
    ruleCode: 'AP-005',
    taskCode: 'TDC-0508',
    decisionKeys: Array.from({ length: 14 }, (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`),
    isReconciled: false,
    purchaseOrderCount: 1,
    issueCount: 1,
    unbalancedPostingCount: 0,
    controlledReversalCount: 2,
    apControlReconciliation: {
        sourceModule: 'AP',
        asOfDate: '2026-07-31T00:00:00Z',
        readModelOutstanding: 100,
        postedGlControlBalance: 100,
        variance: 0,
        documentCount: 1,
        diagnosticCount: 0,
        diagnostics: [],
    },
    currencySummaries: [{
        currencyCode: 'GHS',
        purchaseOrderAmount: 100,
        commitmentAmount: 100,
        acceptedReceiptAmount: 100,
        invoiceAmount: 100,
        settledAmount: 80,
        invoicePostedAmount: 100,
        paymentPostedAmount: 80,
        retentionHeldAmount: 10,
        retentionReleasedAmount: 0,
        milestoneAmount: 100,
    }],
    rows: [{
        purchaseOrderId: 'po-0508',
        purchaseOrderNumber: 'PO-0508',
        purchaseOrderStatus: 'Approved',
        currencyCode: 'GHS',
        purchaseOrderAmount: 100,
        commitmentAmount: 100,
        commitmentGroupOrderAmount: 100,
        acceptedReceiptAmount: 100,
        invoiceAmount: 100,
        settledAmount: 80,
        invoicePostedAmount: 100,
        paymentPostedAmount: 80,
        retentionHeldAmount: 10,
        retentionReleasedAmount: 0,
        retentionOutstandingAmount: 10,
        milestoneAmount: 100,
        completedMilestoneAmount: 100,
        invoicedMilestoneAmount: 100,
        paidMilestoneAmount: 80,
        invoiceCount: 1,
        paymentCount: 1,
        postingCount: 4,
        controlledReversalCount: 2,
        isReconciled: false,
        issues: [{
            code: 'PAYMENT_GL_VARIANCE',
            severity: 'Error',
            area: 'GL',
            message: 'Payment and GL differ.',
            expectedAmount: 100,
            actualAmount: 80,
            varianceAmount: -20,
        }],
    }],
};

describe('ProcurementFinanceReconciliation', () => {
    it('renders the shared AP-005 controls without creating a parallel posting or allocation action', () => {
        const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
        const markup = renderToStaticMarkup(
            <QueryClientProvider client={client}>
                <ProcurementFinanceReconciliation initialData={report} />
            </QueryClientProvider>
        );

        expect(markup).toContain('Procurement and Finance reconciliation');
        expect(markup).toContain('AP-005');
        expect(markup).toContain('TDC-0508');
        expect(markup).toContain('DEC-001');
        expect(markup).toContain('DEC-014');
        expect(markup).toContain('PAYMENT_GL_VARIANCE');
        expect(markup).toContain('does not allocate payments or post money');
        expect(markup).not.toContain('Create payment');
        expect(markup).not.toContain('Post journal');
    });
});
