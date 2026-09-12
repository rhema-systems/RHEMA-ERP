import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const state = vi.hoisted(() => ({
    payment: {} as Record<string, unknown>,
    control: {} as Record<string, unknown>,
    queries: [] as Array<{ queryKey: string[]; enabled?: boolean }>,
}));
vi.mock('next/navigation', () => ({ useParams: () => ({ id: 'payment-1' }), useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@tanstack/react-query', () => ({
    useQuery: (options: { queryKey: string[]; enabled?: boolean }) => {
        state.queries.push(options);
        return {
            data: options.queryKey[0] === 'vendor-payment' ? state.payment
                : options.queryKey[0] === 'vendor-payment-control' ? state.control
                    : options.queryKey[0] === 'finance-settings' ? { minimumReversalReasonLength: 20 } : undefined,
            isLoading: false, refetch: vi.fn(), error: null,
        };
    },
}));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ user: { id: 'operator' }, hasPermission: () => true }) }));
vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));
vi.mock('@/services/accountsPayableService', () => ({ accountsPayableService: {} }));
vi.mock('@/services/finance/finance-data.service', () => ({ financeDataService: {} }));
vi.mock('@/services/workflow-api.service', () => ({ workflowApiService: {} }));
vi.mock('@/services/document-output.service', () => ({ DOCUMENT_TYPES: {}, documentOutputService: {} }));
vi.mock('@/components/finance/ap/SupplierDebitNoteApplicationsCard', () => ({ SupplierDebitNoteApplicationsCard: () => null }));
vi.mock('@/components/finance/InvoicePaymentSodControl', () => ({
    InvoicePaymentSodControl: () => <div>Human approval independence check</div>,
}));

import Page from './page';

describe('Vendor payment optional approval presentation', () => {
    beforeEach(() => {
        state.queries = [];
        state.payment = {
            id: 'payment-1', paymentNumber: 'VP-1', supplierId: 'supplier-1', supplierName: 'Supplier',
            paymentDate: '2026-09-12', createdAt: '2026-09-12', status: 'Draft', approvalRequired: true,
            totalAmount: 100, allocatedAmount: 100, unallocatedAmount: 0, currencyCode: 'GHS', exchangeRate: 1,
            withholdingTaxAmount: 0, withholdingTaxRate: 0, discountTaken: 0, allocations: [],
        };
        state.control = {
            approvalRequired: false, canSubmit: true, evidenceRequirements: [], evidenceDocuments: [], blockingReasons: [],
        };
    });
    it('uses Complete for a draft without an active workflow and does not request approver independence', () => {
        const html = renderToStaticMarkup(<Page />);
        expect(html).toContain('Complete');
        expect(html).not.toContain('Submit for Approval');
        expect(html).not.toContain('Human approval independence check');
        expect(state.queries.find(item => item.queryKey[0] === 'vendor-payment-sod-readiness')?.enabled).toBe(false);
    });
    it('shows Ready to post and retains the normal Post action for directly completed payments', () => {
        state.payment.status = 'Authorized';
        state.payment.approvalRequired = false;
        const html = renderToStaticMarkup(<Page />);
        expect(html).toContain('Ready to post');
        expect(html).toContain('Post');
        expect(html).not.toContain('Human approval independence check');
    });
    it('preserves active approval controls', () => {
        state.control.approvalRequired = true;
        const html = renderToStaticMarkup(<Page />);
        expect(html).toContain('Submit for Approval');
        expect(html).toContain('Human approval independence check');
    });
    it('does not infer optional approval when the server has not supplied a decision', () => {
        delete state.control.approvalRequired;
        const html = renderToStaticMarkup(<Page />);
        expect(html).toContain('Submit for Approval');
        expect(html).toContain('Human approval independence check');
    });
});
