'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import {
    AlertCircle,
    CheckCircle2,
    Copy,
    Download,
    FileCheck2,
    Loader2,
    Plus,
    RefreshCw,
    RotateCcw,
    Save,
    Send,
    Trash2,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { EligibleDraftJournalCombobox } from '@/components/finance/journal-batches/eligible-draft-journal-combobox';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useAuth } from '@/hooks/use-auth';
import { useWorkflowSummary } from '@/hooks/useWorkflowSummary';
import { financeDataService } from '@/services/finance/finance-data.service';
import { journalBatchDataService } from '@/services/finance/journal-batch-data.service';
import type { Account, CreateAccountTransactionDto } from '@/types/finance';
import type {
    JournalBatchDetail,
    JournalBatchItem,
    JournalBatchValidation,
    EligibleJournalBatchDraft,
    ReviewJournalBatchItem,
} from '@/types/journal-batches';

type EntryLine = {
    key: string;
    accountId: string;
    transactionType: 'Debit' | 'Credit';
    amount: string;
};

const newLine = (transactionType: 'Debit' | 'Credit'): EntryLine => ({
    key: crypto.randomUUID(),
    accountId: '',
    transactionType,
    amount: '',
});

const money = (value: number, currency: string) =>
    new Intl.NumberFormat('en-GH', { style: 'currency', currency }).format(value);

const today = () => new Date().toISOString().slice(0, 10);
const errorDescription = (error: any) => {
    const problem = error?.response?.data ?? error?.response ?? error;
    const detail = problem?.detail ?? problem?.message ?? error?.message ?? 'The action could not be completed.';
    const code = problem?.code ?? problem?.extensions?.code;
    return code ? `${detail} (${code})` : detail;
};

export default function JournalBatchDetailPage() {
    const { id } = useParams<{ id: string }>();
    const router = useRouter();
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const workflow = useWorkflowSummary({ entityType: 'JournalBatch', entityId: id });
    const [confirmation, setConfirmation] = useState<'submit' | 'post' | 'reverse' | null>(null);
    const [reversalReason, setReversalReason] = useState('');
    const [reversalDate, setReversalDate] = useState(today());
    const [batch, setBatch] = useState<JournalBatchDetail | null>(null);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [validation, setValidation] = useState<JournalBatchValidation | null>(null);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState<string | null>(null);
    const [postSelection, setPostSelection] = useState<string[]>([]);
    const [reviewDecisions, setReviewDecisions] = useState<Record<string, 'Approved' | 'Rejected'>>({});
    const [reviewComments, setReviewComments] = useState<Record<string, string>>({});
    const [stageComment, setStageComment] = useState('');
    const [selectedDraftJournal, setSelectedDraftJournal] = useState<EligibleJournalBatchDraft>();
    const [eligibleDrafts, setEligibleDrafts] = useState<EligibleJournalBatchDraft[]>([]);
    const [eligibleDraftSearch, setEligibleDraftSearch] = useState('');
    const [eligibleDraftsLoading, setEligibleDraftsLoading] = useState(false);
    const [eligibleDraftsError, setEligibleDraftsError] = useState<string>();
    const [eligibleDraftsOpen, setEligibleDraftsOpen] = useState(false);
    const eligibleDraftRequest = useRef(0);
    const [editingJournalId, setEditingJournalId] = useState<string | null>(null);
    const [entry, setEntry] = useState({
        transactionDate: today(),
        description: '',
        reference: '',
    });
    const [lines, setLines] = useState<EntryLine[]>([newLine('Debit'), newLine('Credit')]);

    const load = useCallback(async () => {
        try {
            setLoading(true);
            const result = await journalBatchDataService.getBatch(id);
            setBatch(result);
            setPostSelection((selected) => selected.filter((itemId) => result.items.some((item) => item.id === itemId && item.postingStatus === 'Ready')));
            setReviewDecisions((current) => {
                const next = { ...current };
                result.items.filter((item) => item.reviewStatus === 'Pending').forEach((item) => { next[item.id] ??= 'Approved'; });
                return next;
            });
        } catch (error: any) {
            toast({ title: 'Load failed', description: error.message, variant: 'destructive' });
        } finally {
            setLoading(false);
        }
    }, [id, toast]);

    useEffect(() => { load(); }, [load]);
    useEffect(() => {
        financeDataService.getAccounts({ status: 'Active', take: 500 })
            .then((result) => setAccounts(result.filter((account) => account.allowDirectPosting && !account.isControlAccount)))
            .catch(() => undefined);
    }, []);

    const loadEligibleDrafts = useCallback(async (search: string) => {
        const requestId = ++eligibleDraftRequest.current;
        try {
            setEligibleDraftsLoading(true);
            setEligibleDraftsError(undefined);
            const result = await journalBatchDataService.getEligibleDraftJournals(id, search);
            if (requestId !== eligibleDraftRequest.current) return;
            setEligibleDrafts(result);
        } catch (error: any) {
            if (requestId !== eligibleDraftRequest.current) return;
            setEligibleDrafts([]);
            setEligibleDraftsError(error.message || 'Eligible draft journals could not be loaded.');
        } finally {
            if (requestId === eligibleDraftRequest.current) setEligibleDraftsLoading(false);
        }
    }, [id]);

    useEffect(() => {
        if (!batch?.canEdit) return;
        const timeout = window.setTimeout(() => loadEligibleDrafts(eligibleDraftSearch), 250);
        return () => window.clearTimeout(timeout);
    }, [batch?.canEdit, eligibleDraftSearch, loadEligibleDrafts]);

    const readyItems = useMemo(() => batch?.items.filter((item) =>
        item.reviewStatus === (batch.approvalRequired === false ? 'NotRequired' : 'Approved') && item.postingStatus === 'Ready') ?? [], [batch]);
    const pendingItems = useMemo(() => batch?.items.filter((item) => item.reviewStatus === 'Pending') ?? [], [batch]);
    const showReview = batch?.approvalRequired !== false && (batch?.approvalStatus !== 'Draft' || !workflow.visibility.direct);
    const canReview = showReview && batch?.canReview && hasPermission('Finance.JournalBatches.Approve');
    const canPost = batch?.canPostAny && hasPermission('Finance.JournalBatches.Post');
    const lineDebit = lines.filter((line) => line.transactionType === 'Debit').reduce((sum, line) => sum + Number(line.amount || 0), 0);
    const lineCredit = lines.filter((line) => line.transactionType === 'Credit').reduce((sum, line) => sum + Number(line.amount || 0), 0);

    const resetEntry = () => {
        setEditingJournalId(null);
        setEntry({ transactionDate: today(), description: '', reference: '' });
        setLines([newLine('Debit'), newLine('Credit')]);
    };

    const run = async (name: string, action: () => Promise<unknown>, success: string, reload = true) => {
        try {
            setBusy(name);
            await action();
            toast({ title: success });
            if (reload) await load();
            return true;
        } catch (error: any) {
            toast({ title: `${success} failed`, description: errorDescription(error), variant: 'destructive' });
            return false;
        } finally {
            setBusy(null);
        }
    };

    const validate = async () => {
        try {
            setBusy('validate');
            const result = await journalBatchDataService.validate(id);
            setValidation(result);
            toast({ title: result.isValid ? 'Batch controls passed' : 'Batch controls failed' });
        } catch (error: any) {
            toast({ title: 'Validation failed', description: error.message, variant: 'destructive' });
        } finally {
            setBusy(null);
        }
    };

    const attachExistingJournal = async () => {
        if (!selectedDraftJournal) return;
        const attached = await run(
            'attach',
            () => journalBatchDataService.attachJournal(id, selectedDraftJournal.id),
            'Journal attached',
        );
        if (!attached) return;
        setSelectedDraftJournal(undefined);
        setEligibleDraftSearch('');
        await loadEligibleDrafts('');
    };

    const saveEntry = async () => {
        if (!batch) return;
        if (!entry.description.trim() || !entry.reference.trim() || lines.length < 2 || lines.some((line) => !line.accountId || Number(line.amount) <= 0)) {
            toast({ title: 'Incomplete journal', description: 'Description, reference, and valid account lines are required.', variant: 'destructive' });
            return;
        }
        if (Math.abs(lineDebit - lineCredit) > 0.005) {
            toast({ title: 'Journal is not balanced', description: `Debit ${lineDebit.toFixed(2)} does not equal credit ${lineCredit.toFixed(2)}.`, variant: 'destructive' });
            return;
        }
        const transactions: CreateAccountTransactionDto[] = lines.map((line, index) => ({
            accountId: line.accountId,
            transactionType: line.transactionType,
            amount: Number(line.amount),
            description: entry.description,
            reference: entry.reference,
            currencyCode: batch.controlCurrencyCode,
            lineNumber: index + 1,
        }));
        await run(
            'save-entry',
            () => editingJournalId
                ? journalBatchDataService.updateJournal(id, editingJournalId, {
                    transactionDate: entry.transactionDate,
                    description: entry.description,
                    reference: entry.reference,
                    bookClassification: batch.bookClassification,
                    transactions,
                })
                : journalBatchDataService.createJournal(id, {
                    journalEntry: {
                        transactionDate: entry.transactionDate,
                        description: entry.description,
                        reference: entry.reference,
                        journalType: 'General',
                        bookClassification: batch.bookClassification,
                        transactions,
                    },
                }),
            editingJournalId ? 'Journal updated' : 'Journal added',
        );
        resetEntry();
    };

    const editItem = async (item: JournalBatchItem) => {
        try {
            setBusy(`edit-${item.id}`);
            const journal = await financeDataService.getJournalEntryById(item.journalEntryId);
            setEditingJournalId(item.journalEntryId);
            setEntry({
                transactionDate: (journal.transactionDate || journal.entryDate).slice(0, 10),
                description: journal.description || '',
                reference: journal.reference || journal.referenceNumber || '',
            });
            setLines(journal.transactions.map((line) => ({
                key: line.id || crypto.randomUUID(),
                accountId: line.accountId,
                transactionType: line.transactionType,
                amount: String(line.amount),
            })));
            window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
        } catch (error: any) {
            toast({ title: 'Unable to edit journal', description: error.message, variant: 'destructive' });
        } finally {
            setBusy(null);
        }
    };

    const submitReview = () => {
        const decisions: ReviewJournalBatchItem[] = pendingItems.map((item) => ({
            journalBatchItemId: item.id,
            decision: reviewDecisions[item.id] || 'Approved',
            comment: reviewComments[item.id] || undefined,
        }));
        const missingReason = decisions.some((decision) => decision.decision === 'Rejected' && !decision.comment?.trim());
        if (missingReason) {
            toast({ title: 'Rejection reason required', description: 'Enter a reason beside every rejected journal.', variant: 'destructive' });
            return;
        }
        run('review', () => journalBatchDataService.reviewStage(id, decisions, stageComment || undefined), 'Review stage completed');
    };

    const reverse = async () => {
        if (!reversalReason.trim() || !reversalDate) return false;
        try {
            setBusy('reverse');
            const reversal = await journalBatchDataService.createReversal(id, reversalReason.trim(), reversalDate);
            router.push(`/finance/journal-batches/${reversal.id}`);
            return true;
        } catch (error: any) {
            toast({ title: 'Reversal failed', description: errorDescription(error), variant: 'destructive' });
            return false;
        } finally {
            setBusy(null);
        }
    };

    const submit = async () => {
        if (!workflow.visibility.known || !hasPermission('Finance.JournalBatches.SubmitForApproval')) return false;
        try {
            setBusy('submit');
            const result = await journalBatchDataService.submit(id);
            setBatch(result);
            void workflow.refresh();
            toast({ title: result.approvalRequired === false ? 'Ready to post' : 'Batch submitted',
                description: result.approvalRequired === false ? 'No approval process is active. Select the entries and post when ready.' : undefined });
            return true;
        } catch (error: any) {
            toast({ title: 'Submission failed', description: errorDescription(error), variant: 'destructive' });
            return false;
        } finally { setBusy(null); }
    };

    const copyBatch = async (rejectedOnly: boolean) => {
        try {
            setBusy(rejectedOnly ? 'copy-rejected' : 'copy');
            const copied = await journalBatchDataService.copy(id, rejectedOnly);
            router.push(`/finance/journal-batches/${copied.id}`);
        } catch (error: any) {
            toast({ title: 'Copy failed', description: error.message, variant: 'destructive' });
        } finally {
            setBusy(null);
        }
    };

    if (loading && !batch) return <div className="flex justify-center py-20"><Loader2 className="h-8 w-8 animate-spin" /></div>;
    if (!batch) return <Alert variant="destructive"><AlertTitle>Batch unavailable</AlertTitle></Alert>;

    return (
        <div className="space-y-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                    <div className="flex items-center gap-2"><h1 className="font-mono text-3xl font-bold">{batch.batchNumber}</h1><Badge variant="outline">{batch.displayStatus}</Badge>{batch.batchType === 'Reversal' && <Badge>Reversal</Badge>}</div>
                    <p className="text-muted-foreground">{batch.description}</p>
                </div>
                <div className="flex flex-wrap gap-2">
                    <Button variant="outline" onClick={() => journalBatchDataService.exportBatch(id, batch.batchNumber)}><Download className="mr-2 h-4 w-4" />Export</Button>
                    <Button variant="outline" onClick={() => copyBatch(false)} disabled={busy !== null}><Copy className="mr-2 h-4 w-4" />Copy</Button>
                    {batch.rejectedEntryCount > 0 && <Button variant="outline" onClick={() => copyBatch(true)} disabled={busy !== null}>Copy rejected</Button>}
                    {batch.canReverseBatch && hasPermission('Finance.JournalBatches.Reverse') && <Button variant="destructive" onClick={() => setConfirmation('reverse')} disabled={busy !== null}><RotateCcw className="mr-2 h-4 w-4" />Reverse entire batch</Button>}
                </div>
            </div>

            <Breadcrumb>
                <BreadcrumbList>
                    <BreadcrumbItem><BreadcrumbLink href="/finance">Finance</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbLink href="/finance/journal-batches">Journal Batches</BreadcrumbLink></BreadcrumbItem><BreadcrumbSeparator />
                    <BreadcrumbItem><BreadcrumbPage>{batch.batchNumber}</BreadcrumbPage></BreadcrumbItem>
                </BreadcrumbList>
            </Breadcrumb>

            <div className="grid gap-4 md:grid-cols-4">
                <Card><CardHeader className="pb-2"><CardDescription>Expected total</CardDescription><CardTitle>{money(batch.expectedDebitTotal, batch.controlCurrencyCode)}</CardTitle></CardHeader></Card>
                <Card><CardHeader className="pb-2"><CardDescription>Actual debit</CardDescription><CardTitle>{money(batch.actualDebitTotal, batch.controlCurrencyCode)}</CardTitle></CardHeader></Card>
                <Card><CardHeader className="pb-2"><CardDescription>Variance</CardDescription><CardTitle className={batch.variance === 0 ? 'text-emerald-600' : 'text-destructive'}>{money(batch.variance, batch.controlCurrencyCode)}</CardTitle></CardHeader></Card>
                <Card><CardHeader className="pb-2"><CardDescription>Entries</CardDescription><CardTitle>{batch.entryCount}{batch.expectedJournalCount ? ` / ${batch.expectedJournalCount}` : ''}</CardTitle></CardHeader></Card>
            </div>

            <Card>
                <CardHeader><CardTitle>Control actions</CardTitle><CardDescription>Validation is read-only. Submission freezes a content fingerprint for every entry.</CardDescription></CardHeader>
                <CardContent className="flex flex-wrap gap-2">
                    <Button variant="outline" onClick={validate} disabled={busy !== null}>{busy === 'validate' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileCheck2 className="mr-2 h-4 w-4" />}Validate</Button>
                    {batch.canSubmit && hasPermission('Finance.JournalBatches.SubmitForApproval') && <Button onClick={() => setConfirmation('submit')} disabled={busy !== null || !workflow.visibility.known}><Send className="mr-2 h-4 w-4" />{workflow.visibility.direct ? 'Prepare to post' : 'Submit for approval'}</Button>}
                    {batch.approvalStatus === 'PendingApproval' && hasPermission('Finance.JournalBatches.SubmitForApproval') && <Button variant="outline" onClick={() => run('withdraw', () => journalBatchDataService.withdraw(id), batch.batchType === 'Reversal' ? 'Reversal cancelled' : 'Batch withdrawn')} disabled={busy !== null}>{batch.batchType === 'Reversal' ? 'Cancel reversal' : 'Withdraw'}</Button>}
                    <Button variant="ghost" onClick={load}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
                </CardContent>
            </Card>

            {batch.canSubmit && workflow.error && <div role="alert" className="text-sm text-destructive">{workflow.error}<Button variant="link" onClick={() => void workflow.refresh()}>Retry workflow check</Button></div>}

            {validation && (
                <Alert variant={validation.isValid ? 'default' : 'destructive'}>
                    {validation.isValid ? <CheckCircle2 className="h-4 w-4" /> : <AlertCircle className="h-4 w-4" />}
                    <AlertTitle>{validation.isValid ? 'All controls passed' : `${validation.issues.length} control issue(s)`}</AlertTitle>
                    <AlertDescription>
                        {validation.issues.length === 0 ? 'The batch is balanced and agrees to its expected control total.' : validation.issues.map((issue) => issue.message).join(' • ')}
                    </AlertDescription>
                </Alert>
            )}

            <Card>
                <CardHeader><CardTitle>Journal entries</CardTitle><CardDescription>Each row is independently balanced. Select posting-ready entries for each posting run.</CardDescription></CardHeader>
                <CardContent className="space-y-4">
                    <div className="overflow-x-auto rounded-md border">
                        <table className="w-full text-sm">
                            <thead className="bg-muted/50"><tr><th className="p-3 text-center">Post</th><th className="p-3 text-left">Journal</th><th className="p-3 text-left">Description</th><th className="p-3 text-right">Debit</th>{showReview && <th className="p-3 text-left">Review</th>}<th className="p-3 text-left">Posting</th><th className="p-3 text-right">Actions</th></tr></thead>
                            <tbody>
                                {batch.items.map((item) => (
                                    <tr className="border-t align-top" key={item.id}>
                                        <td className="p-3 text-center"><Checkbox aria-label={`Select ${item.journalEntryNumber} for posting`} disabled={!canPost || !readyItems.some((ready) => ready.id === item.id)} checked={postSelection.includes(item.id)} onCheckedChange={(checked) => setPostSelection((current) => checked === true ? [...new Set([...current, item.id])] : current.filter((value) => value !== item.id))} /></td>
                                        <td className="p-3"><Link className="font-mono text-primary hover:underline" href={`/finance/journal-entries/${item.journalEntryId}`}>{item.journalEntryNumber}</Link><div className="text-xs text-muted-foreground">{new Date(item.entryDate).toLocaleDateString()}</div></td>
                                        <td className="p-3">{item.description}<div className="text-xs text-muted-foreground">{item.lineCount} lines</div></td>
                                        <td className="p-3 text-right">{money(item.totalDebit, batch.controlCurrencyCode)}</td>
                                        {showReview && <td className="p-3">
                                            {item.reviewStatus === 'Pending' && canReview ? (
                                                <div className="min-w-52 space-y-2">
                                                    <Select value={reviewDecisions[item.id] || 'Approved'} onValueChange={(value: 'Approved' | 'Rejected') => setReviewDecisions({ ...reviewDecisions, [item.id]: value })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Approved">Approve</SelectItem><SelectItem value="Rejected">Reject</SelectItem></SelectContent></Select>
                                                    {reviewDecisions[item.id] === 'Rejected' && <Input placeholder="Required rejection reason" value={reviewComments[item.id] || ''} onChange={(event) => setReviewComments({ ...reviewComments, [item.id]: event.target.value })} />}
                                                </div>
                                            ) : <><Badge variant={item.reviewStatus === 'Rejected' ? 'destructive' : 'outline'}>{item.reviewStatus}</Badge>{item.finalRejectionReason && <div className="mt-1 max-w-xs text-xs text-destructive">{item.finalRejectionReason}</div>}</>}
                                        </td>}
                                        <td className="p-3"><Badge variant="outline">{item.postingStatus}</Badge></td>
                                        <td className="p-3 text-right">
                                            {batch.canEdit && <Button size="sm" variant="ghost" onClick={() => editItem(item)}>Edit</Button>}
                                            {batch.canEdit && <Button size="sm" variant="ghost" onClick={() => run(`remove-${item.id}`, () => journalBatchDataService.removeJournal(id, item.journalEntryId), 'Journal removed')}><Trash2 className="h-4 w-4 text-destructive" /></Button>}
                                        </td>
                                    </tr>
                                ))}
                                {batch.items.length === 0 && <tr><td colSpan={showReview ? 7 : 6} className="p-8 text-center text-muted-foreground">No journal entries have been added.</td></tr>}
                            </tbody>
                        </table>
                    </div>

                    {canReview && pendingItems.length > 0 && (
                        <div className="flex flex-wrap items-end justify-between gap-3 rounded-md border p-4">
                            <div className="min-w-72 flex-1 space-y-2"><Label>Stage comment</Label><Input value={stageComment} onChange={(event) => setStageComment(event.target.value)} placeholder="Optional comment applying to this workflow stage" /></div>
                            <Button onClick={submitReview} disabled={busy !== null}><CheckCircle2 className="mr-2 h-4 w-4" />Submit all {pendingItems.length} decisions</Button>
                        </div>
                    )}

                    {canPost && readyItems.length > 0 && (
                        <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-4">
                            <div><div className="font-medium">{postSelection.length} of {readyItems.length} posting-ready entries selected</div><div className="text-sm text-muted-foreground">Each posting run is atomic; unselected entries remain ready for a later run.</div></div>
                            <div className="flex gap-2"><Button variant="outline" onClick={() => setPostSelection(readyItems.map((item) => item.id))}>Select all ready</Button><Button onClick={() => setConfirmation('post')} disabled={postSelection.length === 0 || busy !== null}>Post selected</Button></div>
                        </div>
                    )}
                </CardContent>
            </Card>

            {batch.canEdit && (
                <Card>
                    <CardHeader><CardTitle>{editingJournalId ? 'Edit journal entry' : 'Add journal entry'}</CardTitle><CardDescription>Enter at least one debit and one credit line. The journal must balance before it can be saved.</CardDescription></CardHeader>
                    <CardContent className="space-y-5">
                        <div className="grid gap-3 md:grid-cols-3">
                            <div className="space-y-2"><Label>Date</Label><Input type="date" value={entry.transactionDate} onChange={(event) => setEntry({ ...entry, transactionDate: event.target.value })} /></div>
                            <div className="space-y-2"><Label>Description</Label><Input value={entry.description} onChange={(event) => setEntry({ ...entry, description: event.target.value })} /></div>
                            <div className="space-y-2"><Label>Reference</Label><Input value={entry.reference} onChange={(event) => setEntry({ ...entry, reference: event.target.value })} /></div>
                        </div>
                        <div className="overflow-x-auto rounded-md border">
                            <table className="w-full text-sm">
                                <thead className="bg-muted/50"><tr><th className="p-3 text-left">Account</th><th className="p-3 text-left">Side</th><th className="p-3 text-right">Amount</th><th className="p-3"></th></tr></thead>
                                <tbody>{lines.map((line, index) => <tr className="border-t" key={line.key}>
                                    <td className="min-w-72 p-3"><Select value={line.accountId} onValueChange={(value) => setLines(lines.map((item, itemIndex) => itemIndex === index ? { ...item, accountId: value } : item))}><SelectTrigger><SelectValue placeholder="Select posting account" /></SelectTrigger><SelectContent>{accounts.map((account) => <SelectItem key={account.id} value={account.id}>{account.accountNumber} — {account.accountName}</SelectItem>)}</SelectContent></Select></td>
                                    <td className="w-44 p-3"><Select value={line.transactionType} onValueChange={(value: 'Debit' | 'Credit') => setLines(lines.map((item, itemIndex) => itemIndex === index ? { ...item, transactionType: value } : item))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Debit">Debit</SelectItem><SelectItem value="Credit">Credit</SelectItem></SelectContent></Select></td>
                                    <td className="w-48 p-3"><Input className="text-right" type="number" min="0.01" step="0.01" value={line.amount} onChange={(event) => setLines(lines.map((item, itemIndex) => itemIndex === index ? { ...item, amount: event.target.value } : item))} /></td>
                                    <td className="p-3"><Button variant="ghost" size="icon" disabled={lines.length <= 2} onClick={() => setLines(lines.filter((_, itemIndex) => itemIndex !== index))}><Trash2 className="h-4 w-4" /></Button></td>
                                </tr>)}</tbody>
                                <tfoot><tr className="border-t font-medium"><td className="p-3" colSpan={2}>Debit {money(lineDebit, batch.controlCurrencyCode)} / Credit {money(lineCredit, batch.controlCurrencyCode)}</td><td className="p-3 text-right" colSpan={2}>{Math.abs(lineDebit - lineCredit) < 0.005 ? <span className="text-emerald-600">Balanced</span> : <span className="text-destructive">Out by {money(Math.abs(lineDebit - lineCredit), batch.controlCurrencyCode)}</span>}</td></tr></tfoot>
                            </table>
                        </div>
                        <div className="flex flex-wrap justify-between gap-2">
                            <Button variant="outline" onClick={() => setLines([...lines, newLine('Debit')])}><Plus className="mr-2 h-4 w-4" />Add line</Button>
                            <div className="flex gap-2">{editingJournalId && <Button variant="outline" onClick={resetEntry}>Cancel edit</Button>}<Button onClick={saveEntry} disabled={busy !== null}><Save className="mr-2 h-4 w-4" />{editingJournalId ? 'Update journal' : 'Add journal'}</Button></div>
                        </div>
                        <div className="grid gap-3 border-t pt-4 md:grid-cols-[1fr_auto]">
                            <div className="space-y-2">
                                <Label>Attach an existing draft journal</Label>
                                <EligibleDraftJournalCombobox
                                    options={eligibleDrafts}
                                    selected={selectedDraftJournal}
                                    search={eligibleDraftSearch}
                                    onSearchChange={setEligibleDraftSearch}
                                    onSelect={setSelectedDraftJournal}
                                    open={eligibleDraftsOpen}
                                    onOpenChange={setEligibleDraftsOpen}
                                    loading={eligibleDraftsLoading}
                                    error={eligibleDraftsError}
                                    currencyCode={batch.controlCurrencyCode}
                                    disabled={busy !== null}
                                />
                                {selectedDraftJournal && (
                                    <div className="text-xs text-muted-foreground">
                                        Same period and book · <Link className="text-primary hover:underline" href={`/finance/journal-entries/${selectedDraftJournal.id}`} target="_blank">Open journal</Link>
                                    </div>
                                )}
                                {!selectedDraftJournal && !eligibleDraftsLoading && !eligibleDraftsError && eligibleDrafts.length === 0 && (
                                    <p className="text-xs text-muted-foreground">
                                        {batch.batchType === 'Reversal'
                                            ? 'Only system-generated reversal journals belonging to this reversal batch can be selected.'
                                            : 'Create or save a balanced manual journal as Draft in this batch\'s period and accounting book first.'}
                                    </p>
                                )}
                                {eligibleDraftsError && (
                                    <Button type="button" variant="link" className="h-auto p-0 text-xs" onClick={() => loadEligibleDrafts(eligibleDraftSearch)}>Retry loading journals</Button>
                                )}
                            </div>
                            <Button className="self-end" variant="outline" onClick={attachExistingJournal} disabled={!selectedDraftJournal || busy !== null}>
                                {busy === 'attach' && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Attach
                            </Button>
                        </div>
                    </CardContent>
                </Card>
            )}

            {batch.postingRuns.length > 0 && (
                <Card>
                    <CardHeader><CardTitle>Posting runs</CardTitle></CardHeader>
                    <CardContent><div className="space-y-2">{batch.postingRuns.map((runItem) => <div className="flex flex-wrap justify-between gap-2 rounded-md border p-3" key={runItem.id}><div><span className="font-medium">Run {runItem.runNumber}</span> <Badge variant="outline">{runItem.status}</Badge><div className="text-xs text-muted-foreground">{new Date(runItem.requestedAt).toLocaleString()}</div></div><div className="text-right">{runItem.selectedEntryCount} entries<div className="font-medium">{money(runItem.selectedDebitTotal, batch.controlCurrencyCode)}</div></div></div>)}</div></CardContent>
                </Card>
            )}
            <ConfirmationDialog open={confirmation === 'submit'} onOpenChange={(open) => { if (!open) setConfirmation(null); }}
                title={workflow.visibility.direct ? 'Prepare batch for posting?' : 'Submit batch for approval?'}
                description={workflow.visibility.direct ? 'Validate and freeze the entries. Posting remains a separate action.' : 'Validate and freeze the entries for the configured approval process.'}
                confirmText={workflow.visibility.direct ? 'Prepare to post' : 'Submit for approval'} onConfirm={submit} isLoading={busy === 'submit'} confirmDisabled={!workflow.visibility.known} />
            <ConfirmationDialog open={confirmation === 'post'} onOpenChange={(open) => { if (!open) setConfirmation(null); }}
                title="Post selected journals?" description={`${postSelection.length} selected journal(s) will post to the General Ledger in one atomic run.`}
                confirmText="Post selected" onConfirm={() => canPost ? run('post', () => journalBatchDataService.post(id, postSelection), 'Posting run completed') : false}
                isLoading={busy === 'post'} confirmDisabled={!canPost || postSelection.length === 0} />
            <ConfirmationDialog open={confirmation === 'reverse'} onOpenChange={(open) => { if (!open) setConfirmation(null); }} title="Reverse entire batch?"
                description="Create a reversal batch. Its configured approval process, if active, still applies before posting."
                confirmText="Create reversal" variant="destructive" onConfirm={reverse} isLoading={busy === 'reverse'} confirmDisabled={!reversalReason.trim() || !reversalDate}>
                <Label htmlFor="batch-reversal-reason">Reversal reason</Label><Textarea id="batch-reversal-reason" value={reversalReason} onChange={(event) => setReversalReason(event.target.value)} />
                <Label htmlFor="batch-reversal-date">Reversal date</Label><Input id="batch-reversal-date" type="date" value={reversalDate} onChange={(event) => setReversalDate(event.target.value)} />
            </ConfirmationDialog>
        </div>
    );
}
