'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
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
import { financeDataService } from '@/services/finance/finance-data.service';
import { journalBatchDataService } from '@/services/finance/journal-batch-data.service';
import type { Account, CreateAccountTransactionDto } from '@/types/finance';
import type {
    JournalBatchDetail,
    JournalBatchItem,
    JournalBatchValidation,
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

export default function JournalBatchDetailPage() {
    const { id } = useParams<{ id: string }>();
    const router = useRouter();
    const { toast } = useToast();
    const [batch, setBatch] = useState<JournalBatchDetail | null>(null);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [validation, setValidation] = useState<JournalBatchValidation | null>(null);
    const [loading, setLoading] = useState(true);
    const [busy, setBusy] = useState<string | null>(null);
    const [postSelection, setPostSelection] = useState<string[]>([]);
    const [reviewDecisions, setReviewDecisions] = useState<Record<string, 'Approved' | 'Rejected'>>({});
    const [reviewComments, setReviewComments] = useState<Record<string, string>>({});
    const [stageComment, setStageComment] = useState('');
    const [existingJournalId, setExistingJournalId] = useState('');
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

    const readyItems = useMemo(() => batch?.items.filter((item) => item.reviewStatus === 'Approved' && item.postingStatus === 'Ready') ?? [], [batch]);
    const pendingItems = useMemo(() => batch?.items.filter((item) => item.reviewStatus === 'Pending') ?? [], [batch]);
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
        } catch (error: any) {
            toast({ title: `${success} failed`, description: error.message, variant: 'destructive' });
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
        const reason = window.prompt('Reason for reversing the entire batch:');
        if (!reason?.trim()) return;
        const reversalDate = window.prompt('Reversal date (YYYY-MM-DD):', today());
        if (!reversalDate) return;
        try {
            setBusy('reverse');
            const reversal = await journalBatchDataService.createReversal(id, reason, reversalDate);
            router.push(`/finance/journal-batches/${reversal.id}`);
        } catch (error: any) {
            toast({ title: 'Reversal failed', description: error.message, variant: 'destructive' });
        } finally {
            setBusy(null);
        }
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
                    {batch.canReverseBatch && <Button variant="destructive" onClick={reverse} disabled={busy !== null}><RotateCcw className="mr-2 h-4 w-4" />Reverse entire batch</Button>}
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
                    {batch.canSubmit && <Button onClick={() => run('submit', () => journalBatchDataService.submit(id), 'Batch submitted')} disabled={busy !== null}><Send className="mr-2 h-4 w-4" />Submit for approval</Button>}
                    {batch.approvalStatus === 'PendingApproval' && <Button variant="outline" onClick={() => run('withdraw', () => journalBatchDataService.withdraw(id), batch.batchType === 'Reversal' ? 'Reversal cancelled' : 'Batch withdrawn')} disabled={busy !== null}>{batch.batchType === 'Reversal' ? 'Cancel reversal' : 'Withdraw'}</Button>}
                    <Button variant="ghost" onClick={load}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
                </CardContent>
            </Card>

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
                <CardHeader><CardTitle>Journal entries</CardTitle><CardDescription>Each row is an independently balanced journal. Approval and posting can be selective.</CardDescription></CardHeader>
                <CardContent className="space-y-4">
                    <div className="overflow-x-auto rounded-md border">
                        <table className="w-full text-sm">
                            <thead className="bg-muted/50"><tr><th className="p-3 text-center">Post</th><th className="p-3 text-left">Journal</th><th className="p-3 text-left">Description</th><th className="p-3 text-right">Debit</th><th className="p-3 text-left">Review</th><th className="p-3 text-left">Posting</th><th className="p-3 text-right">Actions</th></tr></thead>
                            <tbody>
                                {batch.items.map((item) => (
                                    <tr className="border-t align-top" key={item.id}>
                                        <td className="p-3 text-center"><Checkbox disabled={item.postingStatus !== 'Ready'} checked={postSelection.includes(item.id)} onCheckedChange={(checked) => setPostSelection((current) => checked === true ? [...new Set([...current, item.id])] : current.filter((value) => value !== item.id))} /></td>
                                        <td className="p-3"><Link className="font-mono text-primary hover:underline" href={`/finance/journal-entries/${item.journalEntryId}`}>{item.journalEntryNumber}</Link><div className="text-xs text-muted-foreground">{new Date(item.entryDate).toLocaleDateString()}</div></td>
                                        <td className="p-3">{item.description}<div className="text-xs text-muted-foreground">{item.lineCount} lines</div></td>
                                        <td className="p-3 text-right">{money(item.totalDebit, batch.controlCurrencyCode)}</td>
                                        <td className="p-3">
                                            {item.reviewStatus === 'Pending' && batch.canReview ? (
                                                <div className="min-w-52 space-y-2">
                                                    <Select value={reviewDecisions[item.id] || 'Approved'} onValueChange={(value: 'Approved' | 'Rejected') => setReviewDecisions({ ...reviewDecisions, [item.id]: value })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Approved">Approve</SelectItem><SelectItem value="Rejected">Reject</SelectItem></SelectContent></Select>
                                                    {reviewDecisions[item.id] === 'Rejected' && <Input placeholder="Required rejection reason" value={reviewComments[item.id] || ''} onChange={(event) => setReviewComments({ ...reviewComments, [item.id]: event.target.value })} />}
                                                </div>
                                            ) : <><Badge variant={item.reviewStatus === 'Rejected' ? 'destructive' : 'outline'}>{item.reviewStatus}</Badge>{item.finalRejectionReason && <div className="mt-1 max-w-xs text-xs text-destructive">{item.finalRejectionReason}</div>}</>}
                                        </td>
                                        <td className="p-3"><Badge variant="outline">{item.postingStatus}</Badge></td>
                                        <td className="p-3 text-right">
                                            {batch.canEdit && <Button size="sm" variant="ghost" onClick={() => editItem(item)}>Edit</Button>}
                                            {batch.canEdit && <Button size="sm" variant="ghost" onClick={() => run(`remove-${item.id}`, () => journalBatchDataService.removeJournal(id, item.journalEntryId), 'Journal removed')}><Trash2 className="h-4 w-4 text-destructive" /></Button>}
                                        </td>
                                    </tr>
                                ))}
                                {batch.items.length === 0 && <tr><td colSpan={7} className="p-8 text-center text-muted-foreground">No journal entries have been added.</td></tr>}
                            </tbody>
                        </table>
                    </div>

                    {batch.canReview && pendingItems.length > 0 && (
                        <div className="flex flex-wrap items-end justify-between gap-3 rounded-md border p-4">
                            <div className="min-w-72 flex-1 space-y-2"><Label>Stage comment</Label><Input value={stageComment} onChange={(event) => setStageComment(event.target.value)} placeholder="Optional comment applying to this workflow stage" /></div>
                            <Button onClick={submitReview} disabled={busy !== null}><CheckCircle2 className="mr-2 h-4 w-4" />Submit all {pendingItems.length} decisions</Button>
                        </div>
                    )}

                    {readyItems.length > 0 && (
                        <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-4">
                            <div><div className="font-medium">{postSelection.length} of {readyItems.length} approved entries selected</div><div className="text-sm text-muted-foreground">Each posting run is atomic; unselected approved entries remain ready for a later run.</div></div>
                            <div className="flex gap-2"><Button variant="outline" onClick={() => setPostSelection(readyItems.map((item) => item.id))}>Select all ready</Button><Button onClick={() => run('post', () => journalBatchDataService.post(id, postSelection), 'Posting run completed')} disabled={postSelection.length === 0 || busy !== null}>Post selected</Button></div>
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
                            <div className="space-y-2"><Label>Attach an existing draft journal by ID</Label><Input value={existingJournalId} onChange={(event) => setExistingJournalId(event.target.value)} placeholder="00000000-0000-0000-0000-000000000000" /></div>
                            <Button className="self-end" variant="outline" onClick={() => run('attach', () => journalBatchDataService.attachJournal(id, existingJournalId), 'Journal attached')} disabled={!existingJournalId || busy !== null}>Attach</Button>
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
        </div>
    );
}
