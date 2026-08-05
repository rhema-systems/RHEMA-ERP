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
                            AP-003 revalidates invoice state, the current three-way match, approved GRN inspection,
                            strict exception status, Finance authorization, and immutable audit before funds move.
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

                <p className="text-xs text-muted-foreground">
                    TDC-0506 owns payment processor/approver identity separation. TDC-0507 owns exception request,
                    approval, evidence, and reporting; this control only consumes a current approved result.
                </p>
                <div className="flex flex-wrap gap-1" aria-label="Payment decision register">
                    {decisionKeys.map((key) => <Badge key={key} variant="outline" className="font-mono text-[10px]">{key}</Badge>)}
                </div>
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
