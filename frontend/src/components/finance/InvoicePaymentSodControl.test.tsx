import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { InvoicePaymentSodControl } from './InvoicePaymentSodControl';

describe('InvoicePaymentSodControl', () => {
    it('renders same-user hard stop and the complete decision register', () => {
        const markup = renderToStaticMarkup(
            <InvoicePaymentSodControl readiness={{
                sourceType: 'VendorPayment',
                sourceId: 'payment-0506',
                sourceReference: 'VP-0506',
                currentActorUserId: 'user-0506',
                canApprove: false,
                hasInvoiceProcessorLineage: true,
                code: 'AP_PAYMENT_SOD_CONFLICT',
                message: 'The same user cannot process an invoice and approve payment for it.',
                evaluatedAtUtc: '2026-07-31T21:30:00Z',
                policyCode: 'TDC-PROCUREMENT',
                policyVersion: 1,
                decisionKeys: Array.from({ length: 14 }, (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`),
                invoices: [{
                    vendorInvoiceId: 'invoice-0506',
                    invoiceNumber: 'INV-0506',
                    invoiceProcessorUserId: 'user-0506',
                    processorLineagePresent: true,
                    conflictsWithCurrentActor: true,
                }],
            }} />
        );

        expect(markup).toContain('AP-004');
        expect(markup).toContain('TDC-0506');
        expect(markup).toContain('Approval blocked');
        expect(markup).toContain('Same-user conflict');
        expect(markup).toContain('DEC-001');
        expect(markup).toContain('DEC-014');
    });
});
