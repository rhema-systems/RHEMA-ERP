'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, ShieldCheck } from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/components/ui/use-toast';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { exchangeRateOverrideService } from '@/services/finance/exchange-rate-override.service';

interface Props {
    sourceDocumentType: string;
    sourceDocumentId: string;
    transactionCurrencyCode: string;
    governedExchangeRateId?: string | null;
    governedRate?: number | null;
    canRequestForDocument: boolean;
    ineligibleReason?: string;
}

export function TransactionExchangeRateOverridePanel({
    sourceDocumentType,
    sourceDocumentId,
    transactionCurrencyCode,
    governedExchangeRateId,
    governedRate,
    canRequestForDocument,
    ineligibleReason,
}: Props) {
    const queryClient = useQueryClient();
    const { hasPermission } = useAuth();
    const { toast } = useToast();
    const [requestedRate, setRequestedRate] = useState('');
    const [reason, setReason] = useState('');
    const queryKey = ['transaction-exchange-rate-overrides', sourceDocumentType, sourceDocumentId];
    const canRequest = hasPermission('Finance.FX.TransactionRateOverride.Request');

    const history = useQuery({
        queryKey,
        queryFn: () => exchangeRateOverrideService.list(sourceDocumentType, sourceDocumentId),
        enabled: Boolean(sourceDocumentId),
    });
    const latest = history.data?.[0];
    const active = history.data?.find((item) => item.status === 'PendingApproval' || item.status === 'Approved');
    const effectiveGovernedRate = latest?.governedRate ?? governedRate;
    const effectiveRateId = latest?.governedExchangeRateId ?? governedExchangeRateId;

    const requestOverride = useMutation({
        mutationFn: () => {
            if (!effectiveRateId) throw new Error('Governed exchange-rate evidence is missing.');
            return exchangeRateOverrideService.create({
                sourceDocumentType,
                sourceDocumentId,
                transactionCurrencyCode,
                governedExchangeRateId: effectiveRateId,
                requestedRate: Number(requestedRate),
                reason: reason.trim(),
            });
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey });
            setRequestedRate('');
            setReason('');
            toast({
                title: 'Override submitted',
                description: 'The governed rate remains in force until an independent approver approves this request.',
            });
        },
        onError: (error: any) => toast({
            title: 'Override request failed',
            description: error?.message || 'The request could not be submitted.',
            variant: 'destructive',
        }),
    });

    if (!governedExchangeRateId && !latest) return null;

    const validRate = Number.isFinite(Number(requestedRate)) && Number(requestedRate) > 0;
    const validReason = reason.trim().length >= 10;

    return (
        <Card className="border-amber-200 bg-amber-50/30 no-print">
            <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                    <ShieldCheck className="h-4 w-4" /> Transaction exchange-rate control
                </CardTitle>
                <CardDescription>
                    The governed rate is retained as evidence. An approved override applies only to this transaction and is consumed when posting succeeds.
                </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
                <div className="grid gap-3 text-sm sm:grid-cols-3">
                    <div><span className="text-muted-foreground">Currency</span><p className="font-medium">{transactionCurrencyCode}</p></div>
                    <div><span className="text-muted-foreground">Governed rate</span><p className="font-medium">{effectiveGovernedRate ?? '—'}</p></div>
                    <div><span className="text-muted-foreground">Active state</span><p><Badge variant="outline">{active?.status ?? 'Governed rate'}</Badge></p></div>
                </div>

                {latest && (
                    <div className="rounded-md border bg-background p-3 text-sm">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                            <span className="font-medium">{latest.governedRate} → {latest.requestedRate}</span>
                            <Badge variant="secondary">{latest.status}</Badge>
                        </div>
                        <p className="mt-1 text-muted-foreground">{latest.reason}</p>
                        {latest.rejectionReason && <p className="mt-1 text-destructive">Rejected: {latest.rejectionReason}</p>}
                        <p className="mt-2 text-xs text-muted-foreground">
                            Governed source: {latest.governedRateSource} · {new Date(latest.governedRateEffectiveDate).toLocaleDateString()}
                        </p>
                    </div>
                )}

                {canRequest && canRequestForDocument && !active && effectiveRateId ? (
                    <div className="grid gap-3 md:grid-cols-[180px_1fr_auto] md:items-end">
                        <div className="space-y-1">
                            <Label htmlFor={`fx-rate-${sourceDocumentId}`}>Requested rate</Label>
                            <Input id={`fx-rate-${sourceDocumentId}`} type="number" min="0.000001" step="0.000001"
                                value={requestedRate} onChange={(event) => setRequestedRate(event.target.value)} />
                        </div>
                        <div className="space-y-1">
                            <Label htmlFor={`fx-reason-${sourceDocumentId}`}>Reason (required)</Label>
                            <Textarea id={`fx-reason-${sourceDocumentId}`} value={reason}
                                onChange={(event) => setReason(event.target.value)}
                                placeholder="Explain why this transaction must differ from the governed rate." />
                        </div>
                        <Button disabled={!validRate || !validReason || requestOverride.isPending}
                            onClick={() => requestOverride.mutate()}>
                            Submit for approval
                        </Button>
                    </div>
                ) : (
                    <div className="flex items-start gap-2 text-sm text-muted-foreground">
                        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                        <span>{active
                            ? 'A request is already awaiting approval or approved for posting.'
                            : !canRequest
                                ? 'You do not have permission to request a transaction-specific rate.'
                                : ineligibleReason || 'Overrides are available only while this transaction remains editable and unposted.'}</span>
                    </div>
                )}

                {active?.workflowInstanceId && (
                    <p className="text-xs text-muted-foreground">
                        Approval is performed by a different user in the <Link className="underline" href="/finance/approvals">Finance approval inbox</Link>.
                    </p>
                )}
            </CardContent>
        </Card>
    );
}
