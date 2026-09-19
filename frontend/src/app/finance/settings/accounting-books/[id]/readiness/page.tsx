'use client';

import React from 'react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { AlertCircle, ArrowLeft, Loader2, RefreshCw, ShieldCheck } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { getAccountingBookAccess, isIndependentChecker } from '@/components/finance/accounting-books/accounting-book-access';
import { SearchableOptionPicker } from '@/components/finance/accounting-books/searchable-option-picker';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountingBook, AccountingBookActivationReadiness, AccountingBookInitialization,
    AccountingBookInitializationLine, AccountingBookInitializationMode, AccountingBookInitializationPreparation,
    AccountingBookPeriod, AccountingBookPeriodStatus, FiscalPeriod } from '@/types/finance';

type PeriodAction = { period: AccountingBookPeriod; action: 'request' | 'approve' | 'reject'; target?: AccountingBookPeriodStatus };
const transitions: Record<AccountingBookPeriodStatus, AccountingBookPeriodStatus[]> = {
    Future: ['Open'], Open: ['Closed', 'Locked'], Closed: ['Open', 'Locked'], Locked: ['Open'],
};
const modes: AccountingBookInitializationMode[] = ['IndependentOpeningBalances', 'BaseBookCopyAtCutoff', 'BaseBalancesWithOpeningAdjustments'];
const money = (value: number) => new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);

export default function AccountingBookReadinessPage() {
    const params = useParams<{ id: string }>();
    const bookId = params.id;
    const { user, hasPermission, isLoading: authLoading, error: authError } = useAuth();
    const access = getAccountingBookAccess(hasPermission);
    const { toast } = useToast();
    const [book, setBook] = useState<AccountingBook | null>(null);
    const [books, setBooks] = useState<AccountingBook[]>([]);
    const [periods, setPeriods] = useState<AccountingBookPeriod[]>([]);
    const [fiscalPeriods, setFiscalPeriods] = useState<FiscalPeriod[]>([]);
    const [initialization, setInitialization] = useState<AccountingBookInitialization | null>(null);
    const [readiness, setReadiness] = useState<AccountingBookActivationReadiness | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [busy, setBusy] = useState(false);
    const [newPeriodId, setNewPeriodId] = useState('');
    const [periodAction, setPeriodAction] = useState<PeriodAction | null>(null);
    const [actionReason, setActionReason] = useState('');
    const [mode, setMode] = useState<AccountingBookInitializationMode>('IndependentOpeningBalances');
    const [cutoff, setCutoff] = useState('');
    const [sourceBookId, setSourceBookId] = useState('');
    const [initReason, setInitReason] = useState('');
    const [idempotencyKey, setIdempotencyKey] = useState('');
    const [preparation, setPreparation] = useState<AccountingBookInitializationPreparation | null>(null);
    const [lines, setLines] = useState<AccountingBookInitializationLine[]>([]);
    const [initDecision, setInitDecision] = useState<'approve' | 'reject' | null>(null);
    const [initDecisionReason, setInitDecisionReason] = useState('');

    const load = useCallback(async () => {
        if (!access.canRead) return;
        setLoading(true); setError(null);
        try {
            const [selected, allBooks, bookPeriods, allPeriods, current, state] = await Promise.all([
                financeDataService.getAccountingBook(bookId), financeDataService.getAccountingBooks(true),
                financeDataService.getAccountingBookPeriods(bookId), financeDataService.getFiscalPeriods(),
                financeDataService.getAccountingBookInitialization(bookId), financeDataService.getAccountingBookActivationReadiness(bookId),
            ]);
            setBook(selected); setBooks(allBooks); setPeriods(bookPeriods); setFiscalPeriods(allPeriods);
            setInitialization(current); setReadiness(state);
        } catch (reason) { setError(reason instanceof Error ? reason.message : 'Book readiness could not be loaded.'); }
        finally { setLoading(false); }
    }, [access.canRead, bookId]);

    useEffect(() => { if (!authLoading && access.canRead) void load(); if (!authLoading && !access.canRead) setLoading(false); }, [access.canRead, authLoading, load]);
    const availablePeriods = useMemo(() => fiscalPeriods.filter(item => !periods.some(period => period.fiscalPeriodId === item.id)), [fiscalPeriods, periods]);
    const periodOptions = useMemo(() => availablePeriods.map(period => ({ value: period.id, label: `${period.periodCode} — ${period.periodName}` })), [availablePeriods]);
    const sourceBooks = books.filter(item => item.id !== bookId && item.bookType !== 'Delta' && item.lifecycleStatus === 'Active');

    const selectPeriodAction = (next: PeriodAction) => {
        const isSameAction = periodAction?.period.id === next.period.id &&
            periodAction.action === next.action && periodAction.target === next.target;
        setPeriodAction(next);
        if (!isSameAction) setActionReason('');
    };

    const selectInitializationDecision = (next: 'approve' | 'reject') => {
        if (initDecision !== next) setInitDecisionReason('');
        setInitDecision(next);
    };

    const run = async (action: () => Promise<unknown>, success: string) => {
        setBusy(true);
        try { await action(); toast({ title: success }); setPeriodAction(null); setInitDecision(null); await load(); }
        catch (reason) { toast({ title: 'Governed action failed', description: reason instanceof Error ? reason.message : 'The request failed.', variant: 'destructive' }); }
        finally { setBusy(false); }
    };

    const addFuturePeriod = async () => {
        const fiscalPeriodId = newPeriodId;
        if (!fiscalPeriodId) return;
        await run(async () => {
            await financeDataService.createAccountingBookPeriod(bookId, fiscalPeriodId);
            // The selected fiscal period is removed from the available options after creation.
            // Clear its internal identifier so the picker returns to its client-facing placeholder.
            setNewPeriodId('');
        }, 'Book period created');
    };

    const prepare = async () => {
        if (!cutoff || (mode !== 'IndependentOpeningBalances' && !sourceBookId)) return;
        setBusy(true);
        try {
            const result = await financeDataService.prepareAccountingBookInitialization(bookId, mode, cutoff, sourceBookId || null);
            setPreparation(result);
            setLines(result.accounts.map(item => {
                const signed = item.authoritativeSignedBalance;
                return { accountId: item.accountId, currencyCode: result.functionalCurrencyCode,
                    openingDebit: signed > 0 ? signed : 0, openingCredit: signed < 0 ? -signed : 0,
                    baseBookSignedBalance: mode === 'IndependentOpeningBalances' ? 0 : signed, openingAdjustment: 0 };
            }));
        } catch (reason) { setPreparation(null); toast({ title: 'Preparation failed', description: reason instanceof Error ? reason.message : 'The request failed.', variant: 'destructive' }); }
        finally { setBusy(false); }
    };

    const updateLine = (index: number, field: 'openingDebit' | 'openingCredit' | 'openingAdjustment', value: number) => setLines(current => current.map((line, position) => {
        if (position !== index) return line;
        const next = { ...line, [field]: value };
        if (field === 'openingAdjustment' && mode === 'BaseBalancesWithOpeningAdjustments') {
            const signed = next.baseBookSignedBalance + value;
            next.openingDebit = signed > 0 ? signed : 0; next.openingCredit = signed < 0 ? -signed : 0;
        }
        return next;
    }));

    if (authLoading || loading) return <div className="flex min-h-[320px] items-center justify-center"><Loader2 className="h-6 w-6 animate-spin" aria-label="Loading book readiness" /></div>;
    if (authError) return <Alert variant="destructive"><AlertTitle>Authentication unavailable</AlertTitle><AlertDescription>Book readiness access could not be verified.</AlertDescription></Alert>;
    if (!access.canRead) return <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Permission required</AlertTitle><AlertDescription>Finance.Read is required to view book readiness.</AlertDescription></Alert>;
    if (error || !book) return <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Book readiness unavailable</AlertTitle><AlertDescription>{error || 'The accounting book was not found.'} <Button variant="link" className="h-auto p-0" onClick={() => void load()}><RefreshCw className="mr-1 h-3.5 w-3.5" />Retry</Button></AlertDescription></Alert>;

    return <div className="space-y-6 p-6">
        <div><Button asChild variant="ghost" className="px-0"><Link href="/finance/settings/accounting-books"><ArrowLeft className="mr-2 h-4 w-4" />Accounting books</Link></Button><h1 className="text-2xl font-semibold">{book.code} readiness</h1><p className="text-muted-foreground">Govern exact-book periods and immutable initialization evidence. This does not enable automatic parallel posting.</p></div>
        <Alert className={readiness?.isReady ? 'border-emerald-300 bg-emerald-50' : 'border-amber-300 bg-amber-50'}><ShieldCheck className="h-4 w-4" /><AlertTitle>{readiness?.isReady ? 'Activation evidence ready' : 'Activation blocked'}</AlertTitle><AlertDescription>{readiness?.isReady ? `Approved initialization and ${readiness.readyPeriodCount} required open period(s) are ready.` : readiness?.blockers.join(' ') || 'Readiness evidence is incomplete.'}</AlertDescription></Alert>

        <Card><CardHeader><CardTitle>Accounting-book periods</CardTitle><CardDescription>The tenant fiscal calendar remains the outer lock; these exact-book states can only restrict it further.</CardDescription></CardHeader><CardContent className="space-y-4">
            {periods.length === 0 ? <p className="text-muted-foreground">No exact-book periods are configured.</p> : periods.map(period => <div key={period.id} className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3"><div><p className="font-medium">{period.fiscalPeriodCode}</p><p className="text-sm text-muted-foreground">{period.startDate.slice(0, 10)} – {period.endDate.slice(0, 10)}</p></div><div className="flex items-center gap-2"><Badge>{period.status}</Badge>{period.pendingStatus && <Badge variant="outline">Pending {period.pendingStatus}</Badge>}{access.canManagePeriods && !period.pendingStatus && transitions[period.status].map(target => <Button key={target} size="sm" variant="outline" onClick={() => selectPeriodAction({ period, action: 'request', target })}>Request {target}…</Button>)}{access.canApprovePeriods && period.pendingStatus && isIndependentChecker(user?.id, period.requestedByUserId) && <><Button size="sm" onClick={() => selectPeriodAction({ period, action: 'approve' })}>Approve period…</Button><Button size="sm" variant="destructive" onClick={() => selectPeriodAction({ period, action: 'reject' })}>Reject period…</Button></>}{period.pendingStatus && user?.id && period.requestedByUserId?.toLowerCase() === user.id.toLowerCase() && <span className="text-xs text-muted-foreground">Awaiting a different authorized checker.</span>}</div></div>)}
            {access.canManagePeriods && <div className="flex gap-2"><div className="min-w-0 flex-1"><SearchableOptionPicker label="Fiscal period" value={newPeriodId} options={periodOptions} onChange={setNewPeriodId} placeholder="Select another fiscal period" searchPlaceholder="Search period code or name…" emptyMessage="No additional fiscal periods are available." disabled={busy || periodOptions.length === 0} /></div><Button disabled={!newPeriodId || busy || !periodOptions.some(option => option.value === newPeriodId)} onClick={() => void addFuturePeriod()}>Add Future period</Button></div>}
            {periodAction && <div className="space-y-2 rounded-md border p-3"><p className="font-medium">{periodAction.action === 'request' ? `Request ${periodAction.target} for ${periodAction.period.fiscalPeriodCode}` : `${periodAction.action === 'approve' ? 'Approve' : 'Reject'} ${periodAction.period.fiscalPeriodCode} period opening`}</p><p className="text-sm text-muted-foreground">This action is not applied until you confirm it below.</p><Label htmlFor="period-reason">{periodAction.action === 'request' ? `Reason to request ${periodAction.target}` : `Reason for ${periodAction.action === 'approve' ? 'approval' : 'rejection'}`}</Label><Textarea id="period-reason" value={actionReason} onChange={event => setActionReason(event.target.value)} /><div className="flex gap-2"><Button variant="outline" onClick={() => setPeriodAction(null)}>Cancel</Button><Button variant={periodAction.action === 'reject' ? 'destructive' : 'default'} disabled={!actionReason.trim() || busy} onClick={() => void run(() => periodAction.action === 'request' ? financeDataService.requestAccountingBookPeriodTransition(bookId, periodAction.period.id, periodAction.target ?? '', actionReason, periodAction.period.rowVersion) : financeDataService.decideAccountingBookPeriodTransition(bookId, periodAction.period.id, periodAction.action, actionReason, periodAction.period.rowVersion), periodAction.action === 'request' ? `Period ${periodAction.target} request submitted` : `Period ${periodAction.action === 'approve' ? 'approved' : 'rejected'}`)}>{periodAction.action === 'request' ? `Submit ${periodAction.target} request` : `Confirm period ${periodAction.action === 'approve' ? 'approval' : 'rejection'}`}</Button></div></div>}
        </CardContent></Card>

        <Card><CardHeader><CardTitle>Book initialization</CardTitle><CardDescription>Opening evidence must cover every eligible mapped account and reconcile to posted exact-book authority.</CardDescription></CardHeader><CardContent className="space-y-4">
            {initialization ? <div className="grid gap-2 text-sm sm:grid-cols-3"><div><span className="text-muted-foreground">Version</span><p>{initialization.version}</p></div><div><span className="text-muted-foreground">Status</span><div className="mt-1"><Badge>{initialization.status}</Badge></div></div><div><span className="text-muted-foreground">Mode</span><p>{initialization.mode}</p></div><div><span className="text-muted-foreground">Coverage</span><p>{initialization.coveredAccountCount}/{initialization.requiredAccountCount}</p></div><div><span className="text-muted-foreground">Balanced</span><p>{initialization.isBalanced ? 'Yes' : 'No'}</p></div><div><span className="text-muted-foreground">Cutoff period</span><p>{initialization.cutoffFiscalPeriodCode} · {initialization.cutoffDate.slice(0, 10)}</p></div>{initialization.decidedByUserId && <div className="sm:col-span-3"><span className="text-muted-foreground">Decision evidence</span><p>{initialization.status} by {initialization.decidedByName || `user ${initialization.decidedByUserId}`}{initialization.decidedAtUtc ? ` on ${new Date(initialization.decidedAtUtc).toLocaleString()}` : ''}</p>{initialization.decisionReason && <p className="text-muted-foreground">Reason: {initialization.decisionReason}</p>}</div>}<div className="sm:col-span-3"><span className="text-muted-foreground">Evidence verification code</span><p className="break-all font-mono text-xs">{initialization.evidenceFingerprint}</p><p className="mt-1 text-xs text-muted-foreground">A system-generated SHA-256 checksum of the approved initialization evidence. Finance re-derives it, together with the underlying reconciliation checksum, to detect changes before activation.</p></div></div> : <p className="text-muted-foreground">No initialization evidence has been prepared.</p>}
            {access.canManageInitialization && (!initialization || initialization.status === 'Draft' || initialization.status === 'Rejected') && <div className="space-y-3 rounded-md border p-4"><div className="grid gap-3 sm:grid-cols-2"><div><Label>Initialization mode</Label><Select value={mode} onValueChange={value => { setMode(value as AccountingBookInitializationMode); setPreparation(null); }}><SelectTrigger aria-label="Initialization mode"><SelectValue /></SelectTrigger><SelectContent>{modes.map(item => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select></div><div><Label htmlFor="cutoff">Cutoff date</Label><Input id="cutoff" type="date" value={cutoff} onChange={event => { setCutoff(event.target.value); setPreparation(null); }} /></div>{mode !== 'IndependentOpeningBalances' && <div className="sm:col-span-2"><Label>Source full book</Label><Select value={sourceBookId} onValueChange={value => { setSourceBookId(value); setPreparation(null); }}><SelectTrigger aria-label="Source full book"><SelectValue placeholder="Select source book" /></SelectTrigger><SelectContent>{sourceBooks.map(item => <SelectItem key={item.id} value={item.id}>{item.code} — {item.name}</SelectItem>)}</SelectContent></Select></div>}<div><Label htmlFor="init-key">Preparation reference</Label><Input id="init-key" value={idempotencyKey} onChange={event => setIdempotencyKey(event.target.value)} aria-describedby="init-key-help" /><p id="init-key-help" className="mt-1 text-xs text-muted-foreground">A unique business reference that prevents this initialization evidence from being saved twice.</p></div><div><Label htmlFor="init-reason">Preparation reason</Label><Input id="init-reason" value={initReason} onChange={event => setInitReason(event.target.value)} /></div></div><Button variant="outline" disabled={busy || !cutoff || (mode !== 'IndependentOpeningBalances' && !sourceBookId)} onClick={() => void prepare()}>Load governed preparation</Button>
                {preparation && <><p className="text-sm text-muted-foreground">Resolved cutoff period: {preparation.cutoffFiscalPeriodCode}</p><div className="space-y-2">{preparation.accounts.map((account, index) => <div key={account.accountId} className="grid gap-2 rounded border p-2 sm:grid-cols-6"><div className="sm:col-span-2"><p className="font-medium">{account.accountNumber} — {account.accountName}</p><p className="text-xs text-muted-foreground">{account.accountClassificationCode}</p></div><div><Label>Debit</Label><Input aria-label={`${account.accountNumber} debit`} type="number" value={lines[index]?.openingDebit ?? 0} onChange={event => updateLine(index, 'openingDebit', Number(event.target.value))} /></div><div><Label>Credit</Label><Input aria-label={`${account.accountNumber} credit`} type="number" value={lines[index]?.openingCredit ?? 0} onChange={event => updateLine(index, 'openingCredit', Number(event.target.value))} /></div><div><Label>Base signed</Label><p className="pt-2">{money(lines[index]?.baseBookSignedBalance ?? 0)} {preparation.functionalCurrencyCode}</p></div><div><Label>Adjustment</Label><Input aria-label={`${account.accountNumber} adjustment`} type="number" disabled={mode !== 'BaseBalancesWithOpeningAdjustments'} value={lines[index]?.openingAdjustment ?? 0} onChange={event => updateLine(index, 'openingAdjustment', Number(event.target.value))} /></div></div>)}</div><Button disabled={busy || !idempotencyKey.trim() || !initReason.trim()} onClick={() => void run(() => financeDataService.configureAccountingBookInitialization(bookId, { mode, cutoffDate: cutoff, cutoffFiscalPeriodId: preparation.cutoffFiscalPeriodId, cutoffFiscalPeriodCode: preparation.cutoffFiscalPeriodCode, sourceAccountingBookId: sourceBookId || null, sourceAccountingBookCode: preparation.sourceAccountingBookCode ?? null, idempotencyKey: idempotencyKey.trim(), reason: initReason.trim(), lines, rowVersion: initialization?.rowVersion }), 'Initialization evidence saved')}>Save draft evidence</Button></>}
            </div>}
            {access.canManageInitialization && initialization?.status === 'Draft' && <Button disabled={busy} onClick={() => void run(() => financeDataService.submitAccountingBookInitialization(bookId), 'Initialization submitted for independent approval')}>Submit initialization</Button>}
            {access.canApproveInitialization && initialization?.status === 'PendingApproval' && isIndependentChecker(user?.id, initialization.preparedByUserId) && <div className="space-y-3"><p className="text-sm text-muted-foreground">Review the initialization evidence, then choose one decision. Period approval above is a separate action.</p><div className="flex gap-2"><Button onClick={() => selectInitializationDecision('approve')}>Approve initialization…</Button><Button variant="destructive" onClick={() => selectInitializationDecision('reject')}>Reject initialization…</Button></div>{initDecision && <div className="space-y-2 rounded-md border p-3"><p className="font-medium">{initDecision === 'approve' ? 'Approve' : 'Reject'} initialization evidence</p><p className="text-sm text-muted-foreground">This action is not applied until you confirm it below.</p><Label htmlFor="init-decision">Reason for {initDecision === 'approve' ? 'approval' : 'rejection'}</Label><Textarea id="init-decision" value={initDecisionReason} onChange={event => setInitDecisionReason(event.target.value)} /><div className="flex gap-2"><Button variant="outline" onClick={() => setInitDecision(null)}>Cancel</Button><Button variant={initDecision === 'reject' ? 'destructive' : 'default'} disabled={busy || !initDecisionReason.trim()} onClick={() => void run(() => financeDataService.decideAccountingBookInitialization(bookId, initDecision, initDecisionReason, initialization.rowVersion), `Initialization ${initDecision === 'approve' ? 'approved' : 'rejected'}`)}>Confirm initialization {initDecision === 'approve' ? 'approval' : 'rejection'}</Button></div></div>}</div>}
            {initialization?.status === 'PendingApproval' && user?.id && initialization.preparedByUserId.toLowerCase() === user.id.toLowerCase() && <p className="text-xs text-muted-foreground">Awaiting a different authorized checker.</p>}
        </CardContent></Card>
    </div>;
}
