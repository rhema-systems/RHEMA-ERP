'use client';

import React from 'react';
import { AlertTriangle, CheckCircle2, Loader2, ShieldCheck } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import type { InvoicePaymentSodReadiness } from '@/types/ap';

interface InvoicePaymentSodControlProps {
    readiness?: InvoicePaymentSodReadiness;
    isLoading?: boolean;
    error?: string | null;
}

export function InvoicePaymentSodControl({ readiness, isLoading, error }: InvoicePaymentSodControlProps) {
    if (isLoading) {
        return (
            <Card>
                <CardContent className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" /> Checking invoice processor separation...
                </CardContent>
            </Card>
        );
    }

    if (!readiness || error) {
        return (
            <Alert variant="destructive">
                <AlertTriangle className="h-4 w-4" />
                <AlertTitle>Payment approval unavailable</AlertTitle>
                <AlertDescription>
                    {error || 'The authoritative AP-004 separation-of-duties decision is unavailable. Approval remains blocked.'}
                </AlertDescription>
            </Alert>
        );
    }

    return (
        <Card data-testid="invoice-payment-sod-control">
            <CardHeader className="space-y-3">
                <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                        <CardTitle className="flex items-center gap-2 text-base">
                            <ShieldCheck className="h-4 w-4" /> Invoice processor vs payment approver
                        </CardTitle>
                        <CardDescription>
                            AP-004 · TDC-0506 · server-owned invoice submission and workflow approval identities
                        </CardDescription>
                    </div>
                    <Badge variant={readiness.canApprove ? 'default' : 'destructive'}>
                        {readiness.canApprove ? 'Independent approver' : 'Approval blocked'}
                    </Badge>
                </div>
                <Alert variant={readiness.canApprove ? 'default' : 'destructive'}>
                    {readiness.canApprove
                        ? <CheckCircle2 className="h-4 w-4" />
                        : <AlertTriangle className="h-4 w-4" />}
                    <AlertTitle>{readiness.code}</AlertTitle>
                    <AlertDescription>{readiness.message}</AlertDescription>
                </Alert>
            </CardHeader>
            <CardContent className="space-y-4">
                <div className="grid gap-2 text-sm sm:grid-cols-3">
                    <div className="rounded-md border p-3">
                        <p className="text-xs uppercase text-muted-foreground">Current actor</p>
                        <p className="mt-1 break-all font-mono text-xs">{readiness.currentActorUserId}</p>
                    </div>
                    <div className="rounded-md border p-3">
                        <p className="text-xs uppercase text-muted-foreground">Processor lineage</p>
                        <p className="mt-1 font-medium">{readiness.hasInvoiceProcessorLineage ? 'Complete' : 'Incomplete'}</p>
                    </div>
                    <div className="rounded-md border p-3">
                        <p className="text-xs uppercase text-muted-foreground">Effective policy</p>
                        <p className="mt-1 font-medium">
                            {readiness.policyCode
                                ? `${readiness.policyCode} v${readiness.policyVersion ?? '?'}`
                                : 'Unavailable'}
                        </p>
                    </div>
                </div>

                {readiness.invoices.length === 0 ? (
                    <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
                        No invoice is allocated. The control still verifies that the required hard-stop policy is effective.
                    </p>
                ) : (
                    <div className="space-y-2">
                        <p className="text-xs font-semibold uppercase text-muted-foreground">Protected invoices</p>
                        {readiness.invoices.map((invoice) => (
                            <div key={invoice.vendorInvoiceId} className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3 text-sm">
                                <div>
                                    <p className="font-medium">{invoice.invoiceNumber}</p>
                                    <p className="break-all font-mono text-xs text-muted-foreground">
                                        Processor: {invoice.invoiceProcessorUserId || 'Missing'}
                                    </p>
                                </div>
                                <Badge variant={invoice.processorLineagePresent && !invoice.conflictsWithCurrentActor ? 'outline' : 'destructive'}>
                                    {!invoice.processorLineagePresent
                                        ? 'Lineage missing'
                                        : invoice.conflictsWithCurrentActor
                                            ? 'Same-user conflict'
                                            : 'Independent'}
                                </Badge>
                            </div>
                        ))}
                    </div>
                )}

                <div className="flex flex-wrap gap-1.5" aria-label="Decision register">
                    {readiness.decisionKeys.map((key) => <Badge key={key} variant="outline">{key}</Badge>)}
                </div>
            </CardContent>
        </Card>
    );
}
