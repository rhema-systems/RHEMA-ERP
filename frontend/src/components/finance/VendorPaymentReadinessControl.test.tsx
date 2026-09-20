import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { VendorPaymentReadinessControl } from './VendorPaymentReadinessControl';

describe('VendorPaymentReadinessControl', () => {
    it('renders a fail-closed hard stop with an actionable reason and collapsed audit lineage', () => {
        const markup = renderToStaticMarkup(
            <VendorPaymentReadinessControl readiness={[{
                vendorInvoiceId: 'invoice-0505',
                invoiceNumber: 'INV-0505',
                invoiceStatus: 'Approved',
                outstandingAmount: 150,
                isPaymentReady: false,
                invoiceStateReady: true,
                threeWayMatchRequired: true,
                threeWayMatchReady: false,
                receiptInspectionReady: true,
                approvedExceptionApplied: false,
                persistedMatchCurrent: false,
                snapshotHash: 'a'.repeat(64),
                evaluatedAtUtc: '2026-07-31T00:00:00Z',
                message: 'Current mandatory match evidence is unavailable.',
                configurationProfileCode: 'TDC-PROCUREMENT',
                configurationProfileVersion: 1,
                decisionKeys: Array.from({ length: 14 }, (_, index) => `DEC-${String(index + 1).padStart(3, '0')}`),
                checks: [{
                    checkKey: 'AP-PAYMENT-MATCH',
                    label: 'Current mandatory match',
                    passed: false,
                    exceptionEligible: false,
                    message: 'The AP-002 event is missing or stale.',
                }],
            }]} />
        );

        expect(markup).toContain('Controlled invoice payment readiness');
        expect(markup).toContain('Hard stop active');
        expect(markup).toContain('Payment blocked');
        expect(markup).toContain('Current mandatory match');
        expect(markup).toContain('Why INV-0505 is blocked');
        expect(markup).toContain('Audit details');
        expect(markup).toContain('TDC-0506');
        expect(markup).toContain('TDC-0507');
        expect(markup).toContain('DEC-001');
        expect(markup).toContain('DEC-014');
    });

    it('presents server readiness checks in user-friendly language', () => {
        const markup = renderToStaticMarkup(
            <VendorPaymentReadinessControl readiness={[{
                vendorInvoiceId: 'invoice-ready',
                invoiceNumber: 'INV-READY',
                invoiceStatus: 'Approved',
                outstandingAmount: 250,
                isPaymentReady: true,
                invoiceStateReady: true,
                threeWayMatchRequired: true,
                threeWayMatchReady: true,
                receiptInspectionReady: true,
                approvedExceptionApplied: false,
                persistedMatchCurrent: true,
                snapshotHash: 'b'.repeat(64),
                evaluatedAtUtc: '2026-09-02T00:00:00Z',
                message: 'Invoice is ready.',
                decisionKeys: ['DEC-001'],
                checks: [{
                    checkKey: 'AP-PAYMENT-RECEIPT-INSPECTION',
                    label: 'Approved GRN and inspection',
                    passed: true,
                    exceptionEligible: false,
                    message: 'The latest independently approved receipt inspection is AP eligible.',
                }],
            }]} />
        );

        expect(markup).toContain('Why INV-READY is payment ready');
        expect(markup).toContain('Receipt and inspection evidence');
        expect(markup).toContain('The required receipt and inspection evidence is approved.');
        expect(markup).toContain('Processor and approver separation is enforced again during payment approval.');
        expect(markup).not.toContain('AP-002 event');
    });
});
