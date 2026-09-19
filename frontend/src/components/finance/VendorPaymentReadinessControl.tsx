'use client';

import React from 'react';
import { AlertCircle, CheckCircle2, ShieldCheck } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import type { VendorPaymentInvoiceReadiness } from '@/types/ap';

interface VendorPaymentReadinessControlProps {
    readiness: VendorPaymentInvoiceReadiness[];
}

const userFacingCheckCopy: Record<string, { label: string; passed: string; failed: string }> = {
    'AP-PAYMENT-INVOICE-STATE': {
        label: 'Invoice approval status',
        passed: 'The invoice is approved and eligible for settlement.',
        failed: 'Approve the invoice before including it in a payment.',
    },
    'AP-PAYMENT-BALANCE': {
        label: 'Outstanding balance',
        passed: 'The invoice has an amount available to pay.',
        failed: 'The invoice has no payable balance remaining.',
    },
    'AP-PAYMENT-PERSISTED-MATCH': {
        label: 'Current invoice matching',
        passed: 'The payment uses the latest approved invoice-matching result.',
        failed: 'Revalidate the invoice match before payment.',
    },
    'AP-PAYMENT-RECEIPT-INSPECTION': {
        label: 'Receipt and inspection evidence',
        passed: 'The required receipt and inspection evidence is approved.',
        failed: 'Complete and approve the required receipt inspection before payment.',
    },
    'AP-PAYMENT-EXCEPTION': {
        label: 'Matching exceptions',
        passed: 'There is no unresolved matching exception.',
        failed: 'Resolve or obtain approval for the matching exception before payment.',
    },
    'AP-PAYMENT-AUDIT': {
        label: 'Audit trail',
        passed: 'The readiness decision can be retained in the audit trail.',
        failed: 'Payment is blocked because the audit trail is unavailable.',
    },
};

function checkPresentation(check: VendorPaymentInvoiceReadiness['checks'][number]) {
    const copy = userFacingCheckCopy[check.checkKey];
    return {
        label: copy?.label ?? check.label,
        message: copy ? (check.passed ? copy.passed : copy.failed) : check.message,
    };
}

export function VendorPaymentReadinessControl({ readiness }: VendorPaymentReadinessControlProps) {
    const ready = readiness.filter((item) => item.isPaymentReady);
    const blocked = readiness.filter((item) => !item.isPaymentReady);
    const decisionKeys = readiness[0]?.decisionKeys ?? [];
    const allReady = readiness.length > 0 && blocked.length === 0;

    return (
        <Card
            data-testid="vendor-payment-readiness-control"
            aria-label="Vendor payment readiness control"
            className={allReady ? 'border-emerald-300' : 'border-amber-300'}
        >
            <CardHeader className="pb-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                        <CardTitle className="flex items-center gap-2 text-lg">
                            <ShieldCheck className="h-5 w-5" /> Controlled invoice payment readiness
                        </CardTitle>
                        <CardDescription>
                            Finance revalidates invoice approval, matching, receipt evidence and unresolved exceptions
                            before payment.
                        </CardDescription>
                    </div>
                    <div className="flex gap-2">
                        <Badge className="bg-emerald-600">{ready.length} ready</Badge>
                        {blocked.length > 0 && <Badge className="bg-amber-600">{blocked.length} blocked</Badge>}
                    </div>
                </div>
            </CardHeader>
            <CardContent className="space-y-4">
                <Alert variant={allReady ? 'default' : 'destructive'}>
                    {allReady
                        ? <CheckCircle2 className="h-4 w-4" />
                        : <AlertCircle className="h-4 w-4" />}
                    <AlertTitle>{allReady ? 'Eligible invoices are payment ready' : 'Hard stop active'}</AlertTitle>
                    <AlertDescription>
                        {allReady
                            ? 'Every listed invoice passed the server-owned readiness gate. The server rechecks again during allocation, batch approval, processing, and posting.'
                            : readiness.length === 0
                                ? 'Authoritative readiness is unavailable. Invoice allocation remains disabled.'
                                : 'Blocked invoices remain visible for traceability but cannot be selected, auto-allocated, or submitted.'}
                    </AlertDescription>
                </Alert>

                {blocked.length > 0 && (
                    <div className="space-y-2" aria-label="Blocked invoice reasons">
                        {blocked.map((item) => (
                            <div key={item.vendorInvoiceId} className="rounded-md border border-amber-200 bg-amber-50/50 p-3 text-sm">
                                <div className="flex flex-wrap items-center justify-between gap-2">
                                    <span className="font-medium">{item.invoiceNumber}</span>
                                    <Badge variant="destructive">Payment blocked</Badge>
                                </div>
                                <p className="mt-1 text-muted-foreground">{item.message}</p>
                                <div className="mt-2 flex flex-wrap gap-1">
                                    {item.checks.filter((check) => !check.passed).map((check) => (
                                        <Badge key={check.checkKey} variant="outline" title={check.message}>
                                            {check.label}
                                        </Badge>
                                    ))}
                                </div>
                            </div>
                        ))}
                    </div>
                )}

                {readiness.map((item) => (
                    <details key={item.vendorInvoiceId} className="rounded-md border bg-muted/20 p-3">
                        <summary className="cursor-pointer font-medium">
                            Why {item.invoiceNumber} is {item.isPaymentReady ? 'payment ready' : 'blocked'}
                        </summary>
                        <div className="mt-3 space-y-2" aria-label={`Readiness checks for ${item.invoiceNumber}`}>
                            {item.checks.map((check) => {
                                const presentation = checkPresentation(check);
                                return (
                                    <div key={check.checkKey} className="flex items-start gap-2 text-sm">
                                        {check.passed
                                            ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
                                            : <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />}
                                        <div>
                                            <p className="font-medium">{presentation.label}</p>
                                            <p className="text-muted-foreground">{presentation.message}</p>
                                        </div>
                                    </div>
                                );
                            })}
                            <p className="text-xs text-muted-foreground">
                                Processor and approver separation is enforced again during payment approval.
                            </p>
                        </div>
                    </details>
                ))}

                <details className="text-xs text-muted-foreground">
                    <summary className="cursor-pointer font-medium text-foreground">Audit details</summary>
                    <div className="mt-2 space-y-2 rounded-md border bg-muted/20 p-3">
                        <p>
                            AP-003 records payment readiness. TDC-0506 governs processor/approver separation and
                            TDC-0507 governs approved matching exceptions.
                        </p>
                        {readiness[0]?.configurationProfileCode && (
                            <p>
                                Configuration: {readiness[0].configurationProfileCode}
                                {readiness[0].configurationProfileVersion
                                    ? ` v${readiness[0].configurationProfileVersion}`
                                    : ''}
                            </p>
                        )}
                        <div className="flex flex-wrap gap-1" aria-label="Payment decision register">
                            {decisionKeys.map((key) => (
                                <Badge key={key} variant="outline" className="font-mono text-[10px]">{key}</Badge>
                            ))}
                        </div>
                    </div>
                </details>
            </CardContent>
        </Card>
    );
}

export function VendorPaymentReadinessBadge({ readiness }: { readiness?: VendorPaymentInvoiceReadiness }) {
    if (!readiness) return <Badge variant="destructive">Readiness unavailable</Badge>;
    return readiness.isPaymentReady
        ? <Badge className="bg-emerald-600">Payment ready</Badge>
        : <Badge variant="destructive">Payment blocked</Badge>;
}
