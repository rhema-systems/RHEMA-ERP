import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { VendorPaymentReadinessControl } from './VendorPaymentReadinessControl';

describe('VendorPaymentReadinessControl', () => {
    it('renders a fail-closed hard stop and the complete decision register', () => {
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
        expect(markup).toContain('TDC-0506');
        expect(markup).toContain('TDC-0507');
        expect(markup).toContain('DEC-001');
        expect(markup).toContain('DEC-014');
    });
});
