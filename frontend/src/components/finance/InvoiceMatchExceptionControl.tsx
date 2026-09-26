'use client';

import React, { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, CheckCircle2, Clock3, FileCheck2, Loader2, ShieldAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { accountsPayableService } from '@/services/accountsPayableService';
import type {
    CreateVendorInvoiceMatchExceptionRequest,
    VendorInvoiceMatchExceptionEvidenceKind,
    VendorInvoiceMatchExceptionEvidenceRequest,
    VendorInvoiceMatchExceptionOverview,
} from '@/types/ap';
import { invoiceThreeWayMatchQueryKey } from './InvoiceThreeWayMatchControl';

interface InvoiceMatchExceptionControlProps {
    invoiceId: string;
    initialOverview?: VendorInvoiceMatchExceptionOverview;
    compact?: boolean;
}

type EvidenceDraft = VendorInvoiceMatchExceptionEvidenceRequest & { referenceId: string };

const defaultEvidence = (requirementKey: string): EvidenceDraft => ({
    requirementKey,
    referenceKind: 'CentralDocument',
    referenceId: '',
    evidenceReference: '',
});

const toUtc = (value: string) => new Date(value).toISOString();

export const invoiceMatchExceptionQueryKey = (invoiceId: string) =>
    ['vendor-invoice-match-exceptions', invoiceId] as const;

export function InvoiceMatchExceptionControl({
    invoiceId,
    initialOverview,
    compact = false,
}: InvoiceMatchExceptionControlProps) {
    const queryClient = useQueryClient();
    const [showRequest, setShowRequest] = useState(false);
    const [rootCauseCategory, setRootCauseCategory] = useState('Supplier invoice variance');
    const [rootCauseDescription, setRootCauseDescription] = useState('');
    const [justification, setJustification] = useState('');
    const [correctiveAction, setCorrectiveAction] = useState('');
    const [ownerId, setOwnerId] = useState('');
    const [correctiveDue, setCorrectiveDue] = useState('');
    const [expiresAt, setExpiresAt] = useState('');
    const [evidence, setEvidence] = useState<EvidenceDraft[]>([
        defaultEvidence('ROOT_CAUSE_EVIDENCE'),
        defaultEvidence('CORRECTIVE_ACTION_PLAN'),
    ]);
    const [decisionComment, setDecisionComment] = useState('');
    const [completionNote, setCompletionNote] = useState('');
    const [completionEvidence, setCompletionEvidence] = useState<EvidenceDraft>(
        defaultEvidence('CORRECTIVE_ACTION_COMPLETION')
    );

    const overview = useQuery({
        queryKey: invoiceMatchExceptionQueryKey(invoiceId),
        queryFn: () => accountsPayableService.getMatchExceptionOverview(invoiceId),
        initialData: initialOverview,
        retry: 1,
    });

    const refresh = async () => {
        await Promise.all([
            queryClient.invalidateQueries({ queryKey: invoiceMatchExceptionQueryKey(invoiceId) }),
            queryClient.invalidateQueries({ queryKey: invoiceThreeWayMatchQueryKey(invoiceId) }),
            queryClient.invalidateQueries({ queryKey: ['vendor-invoice', invoiceId] }),
        ]);
    };

    const requestException = useMutation({
        mutationFn: (request: CreateVendorInvoiceMatchExceptionRequest) =>
            accountsPayableService.requestMatchException(invoiceId, request),
        onSuccess: async () => {
            setShowRequest(false);
            await refresh();
        },
    });
    const decide = useMutation({
        mutationFn: (approved: boolean) => {
            const activeException = overview.data?.active;
            if (!activeException) {
                throw new Error('There is no active match exception to decide.');
            }

            return accountsPayableService.decideMatchException(
                activeException.id,
                approved,
                decisionComment,
                activeException.rowVersion
            );
        },
        onSuccess: async () => {
            setDecisionComment('');
            await refresh();
        },
    });
    const cancel = useMutation({
        mutationFn: () => {
            const activeException = overview.data?.active;
            if (!activeException) {
                throw new Error('There is no active match exception to cancel.');
            }

            return accountsPayableService.cancelMatchException(
                activeException.id,
                decisionComment,
                activeException.rowVersion
            );
        },
        onSuccess: refresh,
    });
    const complete = useMutation({
        mutationFn: () => {
            const correctiveActionItem = overview.data?.correctiveActionItem;
            if (!correctiveActionItem) {
                throw new Error('There is no approved corrective action to complete.');
            }

            return accountsPayableService.completeMatchExceptionCorrectiveAction(
                correctiveActionItem.id,
                completionNote,
                correctiveActionItem.rowVersion,
                [materializeEvidence(completionEvidence)]
            );
        },
        onSuccess: async () => {
            setCompletionNote('');
            await refresh();
        },
    });

    const operationError = requestException.error || decide.error || cancel.error || complete.error;
    const requestValid = useMemo(() =>
        Boolean(rootCauseCategory.trim() && rootCauseDescription.trim() && justification.trim() &&
            correctiveAction.trim() && ownerId.trim() && correctiveDue && expiresAt &&
            evidence.every(item => item.referenceId.trim() && item.evidenceReference.trim())),
    [rootCauseCategory, rootCauseDescription, justification, correctiveAction, ownerId, correctiveDue, expiresAt, evidence]);

    if (overview.isLoading) {
        return (
            <Card aria-label="Invoice match exception control">
                <CardContent className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" /> {compact ? 'Loading matching exceptions…' : 'Loading AP-006 exception control…'}
                </CardContent>
            </Card>
        );
    }
    if (overview.isError || !overview.data) {
        return (
            <Alert variant="destructive" aria-label="Invoice match exception unavailable">
                <AlertCircle className="h-4 w-4" />
                <AlertTitle>{compact ? 'Matching exceptions unavailable' : 'AP-006 control unavailable'}</AlertTitle>
                <AlertDescription>{(overview.error as Error)?.message || 'Exception history could not be loaded.'}</AlertDescription>
            </Alert>
        );
    }

    const data = overview.data;
    if (!data.matchingReadiness.isRequired) return null;
    const active = data.active;
    const correctiveActionItem = data.correctiveActionItem;
    if (compact && !active && !data.canRequest && !correctiveActionItem && data.history.length === 0) return null;

    const submitRequest = () => requestException.mutate({
        rootCauseCategory: rootCauseCategory.trim(),
        rootCauseDescription: rootCauseDescription.trim(),
        justification: justification.trim(),
        correctiveAction: correctiveAction.trim(),
        correctiveActionOwnerId: ownerId.trim(),
        correctiveActionDueAtUtc: toUtc(correctiveDue),
        expiresAtUtc: toUtc(expiresAt),
        idempotencyKey: `ap006-${invoiceId}-${Date.now()}`,
        evidence: evidence.map(materializeEvidence),
    });

    return (
        <Card aria-label="Invoice match exception control" className={compact ? '' : 'border-amber-300'}>
            <CardHeader>
                <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                        <CardTitle className="flex items-center gap-2 text-lg">
                            <ShieldAlert className="h-5 w-5" /> {compact ? 'Matching exceptions' : 'AP-006 match-exception register'}
                        </CardTitle>
                        {!compact && <CardDescription>
                            Controlled tolerance exception only. It does not create, allocate, authorize, or post a supplier payment.
                        </CardDescription>}
                    </div>
                    <div className="flex gap-2">
                        {active && <Badge variant="outline">{active.status}</Badge>}
                        {!active && data.canRequest && (
                            <Button size="sm" onClick={() => setShowRequest(value => !value)}>
                                {showRequest ? 'Close request' : 'Request exception'}
                            </Button>
                        )}
                    </div>
                </div>
            </CardHeader>
            <CardContent className="space-y-5">
                {operationError && (
                    <Alert variant="destructive">
                        <AlertCircle className="h-4 w-4" />
                        <AlertTitle>Control action failed</AlertTitle>
                        <AlertDescription>{(operationError as Error).message}</AlertDescription>
                    </Alert>
                )}

                {!compact && !active && !data.canRequest && (
                    <Alert>
                        <FileCheck2 className="h-4 w-4" />
                        <AlertTitle>No eligible exception request</AlertTitle>
                        <AlertDescription>
                            AP-006 is available only for a current, exception-eligible three-way-match variance with no hard-stop failure.
                        </AlertDescription>
                    </Alert>
                )}

                {showRequest && data.canRequest && (
                    <div className="space-y-4 rounded-lg border p-4">
                        <div className="grid gap-4 md:grid-cols-2">
                            <Field label="Root-cause category">
                                <Input value={rootCauseCategory} onChange={event => setRootCauseCategory(event.target.value)} />
                            </Field>
                            <Field label="Corrective-action owner user ID">
                                <Input value={ownerId} onChange={event => setOwnerId(event.target.value)} placeholder="Tenant user UUID" />
                            </Field>
                            <Field label="Corrective-action due">
                                <Input type="datetime-local" value={correctiveDue} onChange={event => setCorrectiveDue(event.target.value)} />
                            </Field>
                            <Field label="Exception expires">
                                <Input type="datetime-local" value={expiresAt} onChange={event => setExpiresAt(event.target.value)} />
                            </Field>
                        </div>
                        <Field label="Root-cause analysis">
                            <Textarea value={rootCauseDescription} onChange={event => setRootCauseDescription(event.target.value)} />
                        </Field>
                        <Field label="Business justification">
                            <Textarea value={justification} onChange={event => setJustification(event.target.value)} />
                        </Field>
                        <Field label="Corrective-action plan">
                            <Textarea value={correctiveAction} onChange={event => setCorrectiveAction(event.target.value)} />
                        </Field>
                        <div className="space-y-3">
                            <Label>Required controlled evidence</Label>
                            {evidence.map((item, index) => (
                                <EvidenceEditor
                                    key={item.requirementKey}
                                    value={item}
                                    onChange={next => setEvidence(rows => rows.map((row, rowIndex) => rowIndex === index ? next : row))}
                                />
                            ))}
                        </div>
                        <Button disabled={!requestValid || requestException.isPending} onClick={submitRequest}>
                            {requestException.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                            Submit to dual Finance approval
                        </Button>
                    </div>
                )}

                {active && (
                    <div className="space-y-4 rounded-lg border p-4">
                        <div className="grid gap-3 text-sm md:grid-cols-4">
                            <Summary label="Root cause" value={`${active.rootCauseCategory}: ${active.rootCauseDescription}`} />
                            <Summary label="Corrective owner" value={active.correctiveActionOwnerName} />
                            <Summary label="Corrective due" value={new Date(active.correctiveActionDueAtUtc).toLocaleString()} />
                            <Summary label="Expires" value={new Date(active.expiresAtUtc).toLocaleString()} />
                        </div>
                        <div className="grid gap-3 text-sm md:grid-cols-3">
                            <Summary label="Workflow" value={active.workflowInstanceId || 'Starting'} />
                            <Summary label="Snapshot" value={active.invoiceSnapshotHash} />
                            <Summary label="Approval event" value={active.approvalControlEventId || 'Not approved'} />
                        </div>
                        {active.variances.map(variance => (
                            <div key={variance.id} className="flex flex-wrap justify-between gap-2 rounded-md bg-muted/40 p-3 text-sm">
                                <span className="font-medium">{variance.itemDescription} · {variance.varianceType}</span>
                                <span>{variance.variancePercentage}% variance / {variance.configuredTolerancePercent}% tolerance</span>
                            </div>
                        ))}

                        {(data.canDecide || data.canCancel) && (
                            <div className="space-y-2 border-t pt-4">
                                <Label htmlFor="ap006-decision-comment">Decision comment</Label>
                                <Textarea id="ap006-decision-comment" value={decisionComment} onChange={event => setDecisionComment(event.target.value)} />
                                <div className="flex flex-wrap gap-2">
                                    {data.canDecide && (
                                        <>
                                            <Button disabled={!decisionComment.trim() || decide.isPending} onClick={() => decide.mutate(true)}>
                                                Approve assigned stage
                                            </Button>
                                            <Button variant="destructive" disabled={!decisionComment.trim() || decide.isPending} onClick={() => decide.mutate(false)}>
                                                Reject request
                                            </Button>
                                        </>
                                    )}
                                    {data.canCancel && (
                                        <Button variant="outline" disabled={!decisionComment.trim() || cancel.isPending} onClick={() => cancel.mutate()}>
                                            Cancel request
                                        </Button>
                                    )}
                                </div>
                            </div>
                        )}

                    </div>
                )}

                {data.canCompleteCorrectiveAction && correctiveActionItem && (
                    <div className="space-y-3 rounded-lg border border-emerald-300 p-4">
                        <div className="flex items-center gap-2 font-semibold">
                            <CheckCircle2 className="h-4 w-4" /> Close corrective action #{correctiveActionItem.sequence}
                        </div>
                        <p className="text-sm text-muted-foreground">
                            {correctiveActionItem.correctiveAction} — owner {correctiveActionItem.correctiveActionOwnerName}
                        </p>
                        <Textarea value={completionNote} onChange={event => setCompletionNote(event.target.value)} placeholder="Completion outcome" />
                        <EvidenceEditor value={completionEvidence} onChange={setCompletionEvidence} />
                        <Button
                            disabled={!completionNote.trim() || !completionEvidence.referenceId.trim() ||
                                !completionEvidence.evidenceReference.trim() || complete.isPending}
                            onClick={() => complete.mutate()}
                        >
                            Record controlled completion
                        </Button>
                    </div>
                )}

                {data.history.length > 0 && (
                    <div className="space-y-2">
                        <h3 className="flex items-center gap-2 text-sm font-semibold"><Clock3 className="h-4 w-4" /> Exception history</h3>
                        {data.history.map(item => (
                            <div key={item.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3 text-sm">
                                <div>
                                    <span className="font-medium">#{item.sequence} · {item.varianceType}</span>
                                    <span className="ml-2 text-muted-foreground">requested by {item.requestedByName}</span>
                                </div>
                                <div className="flex items-center gap-2">
                                    <Badge variant="outline">{item.status}</Badge>
                                    <span className="text-xs text-muted-foreground">{item.evidence.length} evidence</span>
                                </div>
                            </div>
                        ))}
                    </div>
                )}

                {!compact && <div className="flex flex-wrap gap-1" aria-label="AP-006 decision lineage">
                    {data.decisionKeys.map(key => <Badge key={key} variant="outline">{key}</Badge>)}
                </div>}
            </CardContent>
        </Card>
    );
}

function materializeEvidence(value: EvidenceDraft): VendorInvoiceMatchExceptionEvidenceRequest {
    return {
        requirementKey: value.requirementKey,
        referenceKind: value.referenceKind,
        workflowEvidenceDocumentId: value.referenceKind === 'WorkflowEvidenceDocument' ? value.referenceId.trim() : undefined,
        fileUploadRecordId: value.referenceKind === 'CentralDocument' ? value.referenceId.trim() : undefined,
        evidenceReference: value.evidenceReference.trim(),
    };
}

function EvidenceEditor({ value, onChange }: { value: EvidenceDraft; onChange: (value: EvidenceDraft) => void }) {
    const setKind = (referenceKind: VendorInvoiceMatchExceptionEvidenceKind) => onChange({ ...value, referenceKind, referenceId: '' });
    return (
        <div className="grid gap-2 rounded-md border p-3 md:grid-cols-3">
            <div>
                <div className="text-xs text-muted-foreground">Requirement</div>
                <div className="text-sm font-medium">{value.requirementKey}</div>
            </div>
            <select
                aria-label={`${value.requirementKey} evidence kind`}
                className="h-10 rounded-md border bg-background px-3 text-sm"
                value={value.referenceKind}
                onChange={event => setKind(event.target.value as VendorInvoiceMatchExceptionEvidenceKind)}
            >
                <option value="CentralDocument">Central DMS upload</option>
                <option value="WorkflowEvidenceDocument">Workflow evidence</option>
            </select>
            <Input
                aria-label={`${value.requirementKey} controlled ID`}
                value={value.referenceId}
                onChange={event => onChange({ ...value, referenceId: event.target.value })}
                placeholder="Controlled evidence UUID"
            />
            <Input
                className="md:col-span-3"
                aria-label={`${value.requirementKey} reference`}
                value={value.evidenceReference}
                onChange={event => onChange({ ...value, evidenceReference: event.target.value })}
                placeholder="Evidence title or DMS reference"
            />
        </div>
    );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
    return <div className="space-y-2"><Label>{label}</Label>{children}</div>;
}

function Summary({ label, value }: { label: string; value: string }) {
    return <div className="min-w-0 rounded-md bg-muted/40 p-3"><div className="text-xs text-muted-foreground">{label}</div><div className="break-all font-medium">{value}</div></div>;
}
