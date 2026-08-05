'use client';

import React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, CheckCircle2, Loader2, RefreshCw, ShieldCheck } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { accountsPayableService } from '@/services/accountsPayableService';
import type { InvoiceMatchingResult } from '@/types/ap';

interface InvoiceThreeWayMatchControlProps {
    invoiceId: string;
    canEvaluate: boolean;
    initialReadiness?: InvoiceMatchingResult;
}

export const invoiceThreeWayMatchQueryKey = (invoiceId: string) =>
    ['vendor-invoice-three-way-match', invoiceId] as const;

export function InvoiceThreeWayMatchControl({
    invoiceId,
    canEvaluate,
    initialReadiness,
}: InvoiceThreeWayMatchControlProps) {
    const queryClient = useQueryClient();
    const readiness = useQuery({
        queryKey: invoiceThreeWayMatchQueryKey(invoiceId),
        queryFn: () => accountsPayableService.getThreeWayMatchReadiness(invoiceId),
        initialData: initialReadiness,
        retry: 1,
    });
    const evaluate = useMutation({
        mutationFn: () => accountsPayableService.performThreeWayMatch(invoiceId),
        onSuccess: (result) => {
            queryClient.setQueryData(invoiceThreeWayMatchQueryKey(invoiceId), result);
            queryClient.invalidateQueries({ queryKey: ['vendor-invoice', invoiceId] });
        },
    });

    if (readiness.isLoading) {
        return (
            <Card aria-label="Three-way matching control">
                <CardContent className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" /> Evaluating approval readiness…
                </CardContent>
            </Card>
        );
    }

    if (readiness.isError || !readiness.data) {
        return (
            <Alert variant="destructive" aria-label="Three-way matching unavailable">
                <AlertCircle className="h-4 w-4" />
                <AlertTitle>Matching control unavailable</AlertTitle>
                <AlertDescription className="flex items-center justify-between gap-4">
                    <span>{(readiness.error as Error)?.message || 'Approval readiness could not be evaluated.'}</span>
                    <Button size="sm" variant="outline" onClick={() => readiness.refetch()}>
                        <RefreshCw className="mr-2 h-4 w-4" /> Retry
                    </Button>
                </AlertDescription>
            </Alert>
        );
    }

    const result = evaluate.data ?? readiness.data;
    if (!result.isRequired) return null;

    return (
        <Card aria-label="Three-way matching control" className={result.approvalReady ? 'border-green-300' : 'border-amber-300'}>
            <CardHeader className="pb-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                        <CardTitle className="flex items-center gap-2 text-lg">
                            <ShieldCheck className="h-5 w-5" /> Mandatory three-way matching
                        </CardTitle>
                        <CardDescription>
                            Invoice, purchase-order price, and independently accepted receipt quantity must agree before approval.
                        </CardDescription>
                    </div>
                    <div className="flex items-center gap-2">
                        <Badge className={result.approvalReady ? 'bg-green-600' : 'bg-amber-600'}>
                            {result.approvedExceptionApplied
                                ? 'Approved exception'
                                : result.approvalReady
                                    ? 'Approval ready'
                                    : 'Approval blocked'}
                        </Badge>
                        {canEvaluate && (
                            <Button
                                size="sm"
                                variant="outline"
                                disabled={evaluate.isPending}
                                onClick={() => evaluate.mutate()}
                            >
                                {evaluate.isPending
                                    ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                    : <RefreshCw className="mr-2 h-4 w-4" />}
                                Re-evaluate
                            </Button>
                        )}
                    </div>
                </div>
            </CardHeader>
            <CardContent className="space-y-4">
                <Alert variant={result.approvalReady ? 'default' : 'destructive'}>
                    {result.approvalReady
                        ? <CheckCircle2 className="h-4 w-4" />
                        : <AlertCircle className="h-4 w-4" />}
                    <AlertTitle>{result.approvalReady ? 'Control passed' : 'Hard stop active'}</AlertTitle>
                    <AlertDescription>{result.message}</AlertDescription>
                </Alert>

                <div className="grid gap-3 text-sm sm:grid-cols-3">
                    <div className="rounded-md border p-3">
                        <div className="text-xs text-muted-foreground">Price tolerance</div>
                        <div className="font-semibold">{result.priceTolerancePercentage}%</div>
                    </div>
                    <div className="rounded-md border p-3">
                        <div className="text-xs text-muted-foreground">Cumulative quantity tolerance</div>
                        <div className="font-semibold">{result.quantityTolerancePercentage}%</div>
                    </div>
                    <div className="rounded-md border p-3">
                        <div className="text-xs text-muted-foreground">Configuration lineage</div>
                        <div className="font-semibold">
                            {result.configurationProfileCode
                                ? `${result.configurationProfileCode} v${result.configurationProfileVersion}`
                                : 'Not resolved'}
                        </div>
                    </div>
                </div>

                {result.checks.length > 0 && (
                    <div className="space-y-2">
                        <h3 className="text-sm font-semibold">Control checks</h3>
                        {result.checks.map((check) => (
                            <div key={check.checkKey} className="flex items-start justify-between gap-4 rounded-md border p-3 text-sm">
                                <div>
                                    <div className="font-medium">{check.label}</div>
                                    <div className="text-muted-foreground">{check.message}</div>
                                </div>
                                <Badge variant={check.passed ? 'secondary' : 'destructive'}>
                                    {check.passed ? 'Passed' : check.exceptionEligible ? 'Exception eligible' : 'Blocked'}
                                </Badge>
                            </div>
                        ))}
                    </div>
                )}

                {result.discrepancies.length > 0 && (
                    <div className="space-y-2">
                        <h3 className="text-sm font-semibold">Variances</h3>
                        {result.discrepancies.map((item, index) => (
                            <div key={`${item.itemDescription}-${item.discrepancyType}-${index}`} className="grid gap-2 rounded-md border p-3 text-sm sm:grid-cols-4">
                                <div className="sm:col-span-2">
                                    <div className="font-medium">{item.itemDescription}</div>
                                    <div className="text-muted-foreground">{item.discrepancyType}</div>
                                </div>
                                <div>
                                    <div className="text-xs text-muted-foreground">Invoice / expected</div>
                                    <div>{item.invoiceValue} / {item.expectedValue ?? '—'}</div>
                                </div>
                                <div>
                                    <div className="text-xs text-muted-foreground">Variance</div>
                                    <div>{item.variancePercentage}%</div>
                                </div>
                            </div>
                        ))}
                        <p className="text-xs text-muted-foreground">
                            This control consumes only a current, independently approved AP-006 exception. Exception request and approval remain part of TDC-0507.
                        </p>
                    </div>
                )}

                <div className="flex flex-wrap gap-1" aria-label="Decision register lineage">
                    {result.decisionKeys.map((key) => <Badge key={key} variant="outline">{key}</Badge>)}
                </div>
            </CardContent>
        </Card>
    );
}
