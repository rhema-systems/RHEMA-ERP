'use client';

import React, { useEffect, useState } from 'react';
import { AlertTriangle, BellRing, CheckCircle2, CircleSlash2, ClipboardCheck, FileText, Loader2, Lock, RefreshCw, ShieldCheck, Trash2, Upload, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import { fileUploadService } from '@/services/file-upload.service';
import type { FinanceCloseCheckSnapshot, FinanceCloseWorkspace, FiscalPeriod } from '@/types/finance';

interface CloseWorkspaceDialogProps {
    period: FiscalPeriod;
    onClosed: () => Promise<void> | void;
}

const statusIcon = (check: FinanceCloseCheckSnapshot) => {
    if (check.status === 'Passed') return <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-green-600" />;
    if (check.status === 'NotApplicable') return <CircleSlash2 className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" />;
    if (check.status === 'Waived') return <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-blue-600" />;
    if (check.status === 'Warning') return <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />;
    return <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-600" />;
};

/**
 * Period-close UI backed entirely by persisted server evidence. The old page rendered four
 * unconditional green ticks, which could imply completion without having run a single check.
 * This dialog deliberately has no client-side bypass and mirrors the maker-checker lifecycle.
 */
export function CloseWorkspaceDialog({ period, onClosed }: CloseWorkspaceDialogProps) {
    const { toast } = useToast();
    const [open, setOpen] = useState(false);
    const [workspace, setWorkspace] = useState<FinanceCloseWorkspace | null>(null);
    const [preparerDeclaration, setPreparerDeclaration] = useState('');
    const [reviewerDeclaration, setReviewerDeclaration] = useState('');
    const [closingNotes, setClosingNotes] = useState('');
    const [taskEvidence, setTaskEvidence] = useState<Record<string, string>>({});
    const [waiverJustification, setWaiverJustification] = useState<Record<string, string>>({});
    const [waiverEvidence, setWaiverEvidence] = useState<Record<string, string>>({});
    const [waiverReviewComment, setWaiverReviewComment] = useState<Record<string, string>>({});
    const [uploadingTaskId, setUploadingTaskId] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);

    const evaluate = async () => {
        try {
            setLoading(true);
            setWorkspace(await financeDataService.evaluateFiscalPeriodClose(period.id));
        } catch (error: any) {
            toast({
                title: 'Close evaluation failed',
                description: error?.message || 'The period-close controls could not be evaluated.',
                variant: 'destructive',
            });
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        if (open) void evaluate();
        // A newly opened dialog must always create/refresh the server evidence. `period.id` is
        // stable for this row; the effect intentionally posts only when that identity/open state changes.
    }, [open, period.id]);

    const prepare = async () => {
        if (preparerDeclaration.trim().length < 20) {
            toast({ title: 'Declaration required', description: 'Enter at least 20 characters.', variant: 'destructive' });
            return;
        }

        try {
            setLoading(true);
            const updated = await financeDataService.prepareFiscalPeriodClose(period.id, preparerDeclaration.trim());
            setWorkspace(updated);
            setPreparerDeclaration('');
            toast({ title: 'Preparation certified', description: 'A second authorised user must review and close this cycle.' });
        } catch (error: any) {
            toast({ title: 'Preparation failed', description: error?.message || 'Unable to certify preparation.', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    const approveAndClose = async () => {
        if (reviewerDeclaration.trim().length < 20) {
            toast({ title: 'Review declaration required', description: 'Enter at least 20 characters.', variant: 'destructive' });
            return;
        }

        try {
            setLoading(true);
            await financeDataService.closeFiscalPeriod(period.id, reviewerDeclaration.trim(), closingNotes.trim() || undefined);
            toast({ title: 'Fiscal period closed', description: `Close cycle ${workspace?.cycleNumber ?? ''} was approved and preserved.` });
            setOpen(false);
            setWorkspace(null);
            setReviewerDeclaration('');
            setClosingNotes('');
            await onClosed();
        } catch (error: any) {
            // The server re-evaluates immediately before close. Surface its blocker/maker-checker
            // reason verbatim so users know whether to resolve data or obtain a second reviewer.
            toast({ title: 'Close approval failed', description: error?.message || 'Unable to close the fiscal period.', variant: 'destructive' });
            await evaluate();
        } finally {
            setLoading(false);
        }
    };

    const completeManualTask = async (taskId: string) => {
        const evidenceSummary = (taskEvidence[taskId] || '').trim();
        if (evidenceSummary.length < 20) {
            toast({
                title: 'Evidence summary required',
                description: 'Describe the retained evidence in at least 20 characters.',
                variant: 'destructive',
            });
            return;
        }

        try {
            setLoading(true);
            const updated = await financeDataService.updateFinanceCloseTask(period.id, taskId, {
                // Self-assignment plus completion is atomic. The server still enforces dependency,
                // assignee and immutable-completion rules for direct API callers.
                assignToCurrentUser: true,
                markCompleted: true,
                evidenceSummary,
            });
            setWorkspace(updated);
            setTaskEvidence(current => ({ ...current, [taskId]: '' }));
            toast({ title: 'Close task completed', description: 'The evidence summary is retained with this close cycle.' });
        } catch (error: any) {
            toast({ title: 'Task completion failed', description: error?.message || 'Unable to complete the close task.', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    const uploadTaskEvidence = async (taskId: string, file?: File) => {
        if (!file) return;
        const validation = fileUploadService.validateFile(file, 20);
        if (!validation.valid) {
            toast({ title: 'Evidence file rejected', description: validation.error, variant: 'destructive' });
            return;
        }

        try {
            setUploadingTaskId(taskId);
            // The binary first enters the shared controlled-upload boundary (policy, malware scan,
            // checksum and tenant storage). Finance then links the returned immutable file ID to
            // the task; it never persists browser paths or introduces a second upload mechanism.
            const uploaded = await fileUploadService.uploadSingleFile(file, 'finance-close-evidence');
            const task = workspace?.tasks.find(item => item.id === taskId);
            const updated = await financeDataService.linkFinanceCloseEvidence(period.id, taskId, {
                fileUploadRecordId: uploaded.fileId,
                evidenceType: task?.isAutomated ? 'Reconciliation' : 'SupportingDocument',
                description: `Evidence for ${task?.title || 'Finance close task'}`,
            });
            setWorkspace(updated);
            toast({ title: 'Evidence attached', description: `${file.name} is retained with this close cycle.` });
        } catch (error: any) {
            toast({ title: 'Evidence upload failed', description: error?.message || 'Unable to attach the evidence file.', variant: 'destructive' });
        } finally {
            setUploadingTaskId(null);
        }
    };

    const removeTaskEvidence = async (taskId: string, attachmentId: string) => {
        try {
            setLoading(true);
            setWorkspace(await financeDataService.removeFinanceCloseEvidence(period.id, taskId, attachmentId));
            toast({ title: 'Evidence link removed', description: 'The controlled file record remains subject to normal retention.' });
        } catch (error: any) {
            toast({ title: 'Evidence cannot be removed', description: error?.message || 'Unable to remove this evidence link.', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    const requestWaiver = async (check: FinanceCloseCheckSnapshot) => {
        const task = workspace?.tasks.find(item => item.checkCode === check.checkCode);
        const attachmentId = waiverEvidence[check.id] || task?.evidenceAttachments[0]?.id;
        const justification = (waiverJustification[check.id] || '').trim();
        if (!attachmentId) {
            toast({ title: 'Supporting evidence required', description: 'Attach and select evidence for this control first.', variant: 'destructive' });
            return;
        }
        if (justification.length < 50) {
            toast({ title: 'Detailed justification required', description: 'Explain the business need and mitigating control in at least 50 characters.', variant: 'destructive' });
            return;
        }

        try {
            setLoading(true);
            setWorkspace(await financeDataService.requestFinanceCloseWaiver(period.id, check.id, attachmentId, justification));
            setWaiverJustification(current => ({ ...current, [check.id]: '' }));
            toast({ title: 'Waiver requested', description: 'A separately authorised reviewer must approve or reject it.' });
        } catch (error: any) {
            toast({ title: 'Waiver request failed', description: error?.message || 'This exception cannot be waived.', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    const reviewWaiver = async (waiverId: string, approve: boolean) => {
        const comment = (waiverReviewComment[waiverId] || '').trim();
        if (comment.length < 20) {
            toast({ title: 'Review comment required', description: 'Enter at least 20 characters.', variant: 'destructive' });
            return;
        }

        try {
            setLoading(true);
            // Approval deliberately causes a full server re-evaluation. The UI never labels a
            // check Waived on its own; only a matching server-side evidence fingerprint can do so.
            setWorkspace(await financeDataService.reviewFinanceCloseWaiver(period.id, waiverId, approve, comment));
            setWaiverReviewComment(current => ({ ...current, [waiverId]: '' }));
            toast({ title: approve ? 'Waiver approved' : 'Waiver rejected', description: approve ? 'The close controls were re-evaluated against the approved fingerprint.' : 'The exception remains unresolved.' });
        } catch (error: any) {
            toast({ title: 'Waiver review failed', description: error?.message || 'Unable to record the waiver decision.', variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    };

    return (
        <Dialog open={open} onOpenChange={setOpen}>
            <DialogTrigger asChild>
                <Button variant="outline" size="sm">
                    <Lock className="mr-1 h-4 w-4" /> Close
                </Button>
            </DialogTrigger>
            <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
                <DialogHeader>
                    <DialogTitle>Finance close workspace: {period.periodName}</DialogTitle>
                    <DialogDescription>
                        Live, persisted evidence for the active numbered close cycle. Mandatory failures cannot be bypassed.
                    </DialogDescription>
                </DialogHeader>

                {loading && !workspace ? (
                    <div className="flex items-center justify-center gap-2 py-12 text-muted-foreground">
                        <Loader2 className="h-5 w-5 animate-spin" /> Evaluating Finance controls…
                    </div>
                ) : workspace ? (
                    <div className="space-y-5 py-2">
                        <div className="flex flex-wrap items-center gap-2 rounded-md border bg-muted/30 p-3 text-sm">
                            <Badge variant="outline">Cycle {workspace.cycleNumber}</Badge>
                            <Badge variant="outline">{workspace.templateName} v{workspace.templateVersion}</Badge>
                            <Badge variant={workspace.mandatoryBlockerCount > 0 ? 'destructive' : 'secondary'}>
                                {workspace.mandatoryBlockerCount} blocker{workspace.mandatoryBlockerCount === 1 ? '' : 's'}
                            </Badge>
                            <Badge variant="outline">Evaluation {workspace.evaluationNumber}</Badge>
                            <span className="ml-auto text-muted-foreground">Status: {workspace.status}</span>
                            <Button variant="ghost" size="sm" onClick={evaluate} disabled={loading}>
                                <RefreshCw className={`mr-1 h-4 w-4 ${loading ? 'animate-spin' : ''}`} /> Re-evaluate
                            </Button>
                        </div>

                        {workspace.alertDeliveries.length > 0 ? (
                            <div className="space-y-2 rounded-md border border-amber-200 bg-amber-50/30 p-3">
                                <div className="flex flex-wrap items-center gap-2">
                                    <BellRing className="h-4 w-4 text-amber-700" />
                                    <h3 className="text-sm font-semibold">Aging and escalation delivery</h3>
                                    <Badge variant="outline">{workspace.alertDeliveries.length} retained</Badge>
                                </div>
                                {/* The notification inbox is the recipient-facing surface. This compact
                                    audit view stays inside the close workspace so reviewers can prove that
                                    missed deadlines and pending approvals were followed up. */}
                                <div className="space-y-1">
                                    {workspace.alertDeliveries.slice(0, 8).map(alert => (
                                        <div key={alert.id} className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
                                            <Badge variant={alert.status === 'Failed' ? 'destructive' : 'outline'} className="text-[10px]">
                                                {alert.status}
                                            </Badge>
                                            <span className="font-medium">{alert.alertType.replace(/([a-z])([A-Z])/g, '$1 $2')}</span>
                                            <span className="text-muted-foreground">to {alert.recipientUserName}</span>
                                            <span className="ml-auto text-muted-foreground">
                                                {new Date(alert.deliveredAtUtc || alert.dueAtUtc).toLocaleString()}
                                            </span>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        ) : null}

                        <div className="space-y-2">
                            <h3 className="text-sm font-semibold">Latest automated checks</h3>
                            {workspace.checks.map(check => {
                                const task = workspace.tasks.find(item => item.checkCode === check.checkCode);
                                const snapshotWaiver = workspace.exceptionWaivers.find(item =>
                                    item.financeCloseCheckSnapshotId === check.id || item.id === check.appliedWaiverId);
                                return (
                                <div key={check.id} className="flex gap-3 rounded-md border p-3">
                                    {statusIcon(check)}
                                    <div className="min-w-0 flex-1 space-y-3">
                                        <div className="flex flex-wrap items-center gap-2">
                                            <p className="text-sm font-medium">{check.title}</p>
                                            <Badge variant="outline" className="text-xs">{check.status}</Badge>
                                            <Badge variant="outline" className="text-xs">{check.category}</Badge>
                                            {check.exceptionCount > 0 ? (
                                                <Badge variant={check.status === 'Failed' ? 'destructive' : 'secondary'} className="text-xs">
                                                    {check.exceptionCount} exception{check.exceptionCount === 1 ? '' : 's'}
                                                </Badge>
                                            ) : null}
                                            {check.exceptionAmount !== undefined && check.exceptionAmount !== null ? (
                                                <Badge variant="outline" className="text-xs tabular-nums">
                                                    Amount {check.exceptionAmount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                                                </Badge>
                                            ) : null}
                                        </div>
                                        {/* AP/AR close providers persist counts and amounts with the immutable
                                            snapshot. Showing those measurements here lets reviewers distinguish a
                                            control-account variance from a valid unapplied-advance review warning. */}
                                        <p className="mt-1 text-sm text-muted-foreground">{check.resultSummary}</p>

                                        {task?.evidenceAttachments.length ? (
                                            <div className="space-y-1 rounded bg-muted/30 p-2 text-xs">
                                                {task.evidenceAttachments.map(attachment => (
                                                    <div key={attachment.id} className="flex items-center gap-2">
                                                        <FileText className="h-3.5 w-3.5 text-muted-foreground" />
                                                        <a className="min-w-0 flex-1 truncate text-blue-700 hover:underline" href={attachment.fileUrl} target="_blank" rel="noreferrer">
                                                            {attachment.originalFileName}
                                                        </a>
                                                        <span className="text-muted-foreground">{fileUploadService.formatFileSize(attachment.fileSize)}</span>
                                                        {workspace.status === 'InProgress' ? (
                                                            <Button variant="ghost" size="icon" className="h-6 w-6" onClick={() => void removeTaskEvidence(task.id, attachment.id)} disabled={loading}>
                                                                <Trash2 className="h-3.5 w-3.5" />
                                                                <span className="sr-only">Remove evidence</span>
                                                            </Button>
                                                        ) : null}
                                                    </div>
                                                ))}
                                            </div>
                                        ) : null}

                                        {workspace.status === 'InProgress' && task ? (
                                            <div className="flex flex-wrap items-center gap-2">
                                                <Label htmlFor={`automated-evidence-${task.id}`} className="inline-flex cursor-pointer items-center rounded-md border px-3 py-1.5 text-xs font-medium hover:bg-muted">
                                                    {uploadingTaskId === task.id ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <Upload className="mr-1 h-3.5 w-3.5" />}
                                                    Attach evidence
                                                </Label>
                                                <input
                                                    id={`automated-evidence-${task.id}`}
                                                    type="file"
                                                    className="sr-only"
                                                    accept=".pdf,.doc,.docx,.xls,.xlsx,.csv,.txt,.jpg,.jpeg,.png"
                                                    disabled={uploadingTaskId !== null || loading}
                                                    onChange={event => {
                                                        void uploadTaskEvidence(task.id, event.target.files?.[0]);
                                                        event.currentTarget.value = '';
                                                    }}
                                                />
                                                <span className="text-xs text-muted-foreground">PDF, Office, CSV, text or image; controlled upload.</span>
                                            </div>
                                        ) : null}

                                        {snapshotWaiver ? (
                                            <div className="rounded-md border border-blue-200 bg-blue-50/40 p-3 text-xs">
                                                <div className="flex flex-wrap items-center gap-2">
                                                    <ShieldCheck className="h-4 w-4 text-blue-600" />
                                                    <span className="font-medium">Waiver {snapshotWaiver.status}</span>
                                                    <span>requested by {snapshotWaiver.requestedByUserName}</span>
                                                </div>
                                                <p className="mt-1 text-muted-foreground">{snapshotWaiver.justification}</p>
                                                {snapshotWaiver.reviewComment ? <p className="mt-1">Review: {snapshotWaiver.reviewComment}</p> : null}
                                            </div>
                                        ) : check.isWaivable && task ? (
                                            <div className="space-y-2 rounded-md border border-amber-200 bg-amber-50/30 p-3">
                                                <p className="text-xs font-medium">Controlled exception waiver</p>
                                                {task.evidenceAttachments.length > 1 ? (
                                                    <select
                                                        className="h-9 w-full rounded-md border bg-background px-3 text-sm"
                                                        value={waiverEvidence[check.id] || task.evidenceAttachments[0]?.id || ''}
                                                        onChange={event => setWaiverEvidence(current => ({ ...current, [check.id]: event.target.value }))}
                                                    >
                                                        {task.evidenceAttachments.map(attachment => (
                                                            <option key={attachment.id} value={attachment.id}>{attachment.originalFileName}</option>
                                                        ))}
                                                    </select>
                                                ) : null}
                                                <Textarea
                                                    value={waiverJustification[check.id] || ''}
                                                    onChange={event => setWaiverJustification(current => ({ ...current, [check.id]: event.target.value }))}
                                                    placeholder="Explain why the exact exception can be accepted temporarily and identify the mitigating Finance controlâ€¦"
                                                    maxLength={2000}
                                                    rows={2}
                                                />
                                                <Button size="sm" variant="outline" onClick={() => void requestWaiver(check)} disabled={loading || task.evidenceAttachments.length === 0}>
                                                    <ShieldCheck className="mr-1 h-4 w-4" /> Request waiver
                                                </Button>
                                            </div>
                                        ) : null}
                                    </div>
                                </div>
                                );
                            })}
                        </div>

                        {workspace.exceptionWaivers.some(waiver => waiver.status === 'Requested') ? (
                            <div className="space-y-2">
                                <h3 className="text-sm font-semibold">Pending waiver reviews</h3>
                                {workspace.exceptionWaivers.filter(waiver => waiver.status === 'Requested').map(waiver => (
                                    <div key={waiver.id} className="space-y-2 rounded-md border border-blue-200 p-3">
                                        <div className="flex flex-wrap items-center gap-2 text-sm">
                                            <ShieldCheck className="h-4 w-4 text-blue-600" />
                                            <span className="font-medium">{waiver.checkCode}</span>
                                            <Badge variant="outline">Requested by {waiver.requestedByUserName}</Badge>
                                            {!waiver.matchesLatestEvidence ? <Badge variant="destructive">Evidence changed</Badge> : null}
                                        </div>
                                        <p className="text-sm text-muted-foreground">{waiver.justification}</p>
                                        <Textarea
                                            value={waiverReviewComment[waiver.id] || ''}
                                            onChange={event => setWaiverReviewComment(current => ({ ...current, [waiver.id]: event.target.value }))}
                                            placeholder="Record your independent review basisâ€¦"
                                            maxLength={2000}
                                            rows={2}
                                        />
                                        <div className="flex gap-2">
                                            <Button size="sm" onClick={() => void reviewWaiver(waiver.id, true)} disabled={loading || !waiver.matchesLatestEvidence}>Approve and re-evaluate</Button>
                                            <Button size="sm" variant="destructive" onClick={() => void reviewWaiver(waiver.id, false)} disabled={loading}>Reject</Button>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        ) : null}

                        {workspace.tasks.some(task => !task.isAutomated && task.taskCode !== 'PREPARER_CERTIFICATION') && (
                            <div className="space-y-2">
                                <h3 className="text-sm font-semibold">Manual close tasks</h3>
                                {workspace.tasks
                                    .filter(task => !task.isAutomated && task.taskCode !== 'PREPARER_CERTIFICATION')
                                    .map(task => (
                                        <div key={task.id} className="space-y-3 rounded-md border p-3">
                                            <div className="flex items-start gap-3">
                                                <ClipboardCheck className={`mt-0.5 h-4 w-4 ${task.status === 'Completed' ? 'text-green-600' : task.isOverdue ? 'text-red-600' : 'text-slate-500'}`} />
                                                <div className="min-w-0 flex-1">
                                                    <div className="flex flex-wrap items-center gap-2">
                                                        <p className="text-sm font-medium">{task.title}</p>
                                                        <Badge variant={task.isMandatory ? 'default' : 'outline'}>{task.isMandatory ? 'Mandatory' : 'Optional'}</Badge>
                                                        <Badge variant="outline">{task.status}</Badge>
                                                        {task.isOverdue ? <Badge variant="destructive">Overdue</Badge> : null}
                                                    </div>
                                                    <p className="mt-1 text-xs text-muted-foreground">
                                                        Due {task.dueAt ? new Date(task.dueAt).toLocaleString() : 'date not set'}
                                                        {task.dependsOnTaskCode ? ` · Depends on ${task.dependsOnTaskCode}` : ''}
                                                    </p>
                                                    {task.evidenceSummary ? <p className="mt-2 text-sm">{task.evidenceSummary}</p> : null}
                                                    {task.evidenceAttachments.length ? (
                                                        <div className="mt-2 space-y-1 text-xs">
                                                            {task.evidenceAttachments.map(attachment => (
                                                                <div key={attachment.id} className="flex items-center gap-2">
                                                                    <FileText className="h-3.5 w-3.5 text-muted-foreground" />
                                                                    <a className="min-w-0 flex-1 truncate text-blue-700 hover:underline" href={attachment.fileUrl} target="_blank" rel="noreferrer">
                                                                        {attachment.originalFileName}
                                                                    </a>
                                                                    <span className="text-muted-foreground">{fileUploadService.formatFileSize(attachment.fileSize)}</span>
                                                                    {task.status !== 'Completed' && workspace.status === 'InProgress' ? (
                                                                        <Button variant="ghost" size="icon" className="h-6 w-6" onClick={() => void removeTaskEvidence(task.id, attachment.id)} disabled={loading}>
                                                                            <Trash2 className="h-3.5 w-3.5" />
                                                                            <span className="sr-only">Remove evidence</span>
                                                                        </Button>
                                                                    ) : null}
                                                                </div>
                                                            ))}
                                                        </div>
                                                    ) : null}
                                                </div>
                                            </div>
                                            {task.status !== 'Completed' && workspace.status === 'InProgress' ? (
                                                <div className="space-y-2 pl-7">
                                                    <div className="flex items-center gap-2">
                                                        <Label htmlFor={`manual-evidence-file-${task.id}`} className="inline-flex cursor-pointer items-center rounded-md border px-3 py-1.5 text-xs font-medium hover:bg-muted">
                                                            {uploadingTaskId === task.id ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <Upload className="mr-1 h-3.5 w-3.5" />}
                                                            Attach file evidence
                                                        </Label>
                                                        <input
                                                            id={`manual-evidence-file-${task.id}`}
                                                            type="file"
                                                            className="sr-only"
                                                            accept=".pdf,.doc,.docx,.xls,.xlsx,.csv,.txt,.jpg,.jpeg,.png"
                                                            disabled={uploadingTaskId !== null || loading}
                                                            onChange={event => {
                                                                void uploadTaskEvidence(task.id, event.target.files?.[0]);
                                                                event.currentTarget.value = '';
                                                            }}
                                                        />
                                                    </div>
                                                    <Label htmlFor={`task-evidence-${task.id}`}>Evidence summary</Label>
                                                    <Textarea
                                                        id={`task-evidence-${task.id}`}
                                                        value={taskEvidence[task.id] || ''}
                                                        onChange={event => setTaskEvidence(current => ({ ...current, [task.id]: event.target.value }))}
                                                        placeholder="Identify the Finance report, reconciliation, journal range, or retained file that supports completion…"
                                                        maxLength={2000}
                                                        rows={2}
                                                    />
                                                    <Button size="sm" onClick={() => void completeManualTask(task.id)} disabled={loading}>
                                                        Complete with evidence
                                                    </Button>
                                                </div>
                                            ) : null}
                                        </div>
                                    ))}
                            </div>
                        )}

                        {workspace.status === 'InProgress' && (
                            <div className="space-y-3 rounded-md border p-4">
                                <div>
                                    <h3 className="font-semibold">Preparer certification</h3>
                                    <p className="text-sm text-muted-foreground">
                                        Confirm that you reviewed the latest evidence. A different authorised user must approve the close.
                                    </p>
                                </div>
                                <Label htmlFor={`preparer-declaration-${period.id}`}>Declaration</Label>
                                <Textarea
                                    id={`preparer-declaration-${period.id}`}
                                    value={preparerDeclaration}
                                    onChange={event => setPreparerDeclaration(event.target.value)}
                                    placeholder="I reviewed the period-close evidence and confirm that all mandatory exceptions are resolved…"
                                    maxLength={2000}
                                    rows={3}
                                />
                                <Button onClick={prepare} disabled={loading || !workspace.canPrepare}>
                                    {loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                                    Certify preparation
                                </Button>
                            </div>
                        )}

                        {workspace.status === 'Prepared' && workspace.certification && (
                            <div className="space-y-3 rounded-md border border-blue-200 bg-blue-50/50 p-4">
                                <div>
                                    <h3 className="font-semibold">Second-person review and approval</h3>
                                    <p className="text-sm text-muted-foreground">
                                        Prepared by {workspace.certification.preparedByUserName || 'an authorised user'}.
                                        {!workspace.canApproveAndClose ? ' Sign in as a different authorised Finance closer to approve.' : ''}
                                    </p>
                                </div>
                                <Label htmlFor={`reviewer-declaration-${period.id}`}>Reviewer declaration</Label>
                                <Textarea
                                    id={`reviewer-declaration-${period.id}`}
                                    value={reviewerDeclaration}
                                    onChange={event => setReviewerDeclaration(event.target.value)}
                                    placeholder="I independently reviewed the latest close evidence and approve closing this fiscal period…"
                                    maxLength={2000}
                                    rows={3}
                                />
                                <Label htmlFor={`closing-notes-${period.id}`}>Closing notes (optional)</Label>
                                <Textarea
                                    id={`closing-notes-${period.id}`}
                                    value={closingNotes}
                                    onChange={event => setClosingNotes(event.target.value)}
                                    maxLength={2000}
                                    rows={2}
                                />
                            </div>
                        )}
                    </div>
                ) : (
                    <div className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
                        The close workspace could not be loaded. Re-open this dialog to retry.
                    </div>
                )}

                <DialogFooter>
                    <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
                    {workspace?.status === 'Prepared' && (
                        <Button onClick={approveAndClose} disabled={loading || !workspace.canApproveAndClose}>
                            {loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Lock className="mr-2 h-4 w-4" />}
                            Approve and close period
                        </Button>
                    )}
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
