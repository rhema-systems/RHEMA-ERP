'use client';

import React from 'react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { AlertCircle, ArrowLeft, BookOpen, Loader2, Pencil, Plus, RefreshCw, ShieldAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountingBook, AccountingBookLifecycleStatus, AccountingBookType, SaveAccountingBook } from '@/types/finance';
import { getAccountingBookAccess } from '@/components/finance/accounting-books/accounting-book-access';
import { SearchableOptionPicker } from '@/components/finance/accounting-books/searchable-option-picker';
import type { Currency } from '@/types/finance';

const bookTypes: AccountingBookType[] = ['PrimaryFull', 'ParallelFull', 'Delta'];
const lifecycleStates: AccountingBookLifecycleStatus[] = ['Draft', 'Configuring', 'Initializing', 'Active', 'Suspended', 'Retired'];
const lifecycleTargets: Record<AccountingBookLifecycleStatus, AccountingBookLifecycleStatus[]> = {
    Draft: ['Configuring', 'Retired'],
    Configuring: ['Initializing', 'Retired'],
    Initializing: ['Active'],
    Active: ['Suspended'],
    Suspended: ['Active', 'Retired'],
    Retired: [],
};

const blankForm = (): SaveAccountingBook => ({
    code: '',
    name: '',
    description: '',
    purpose: '',
    bookType: 'ParallelFull',
    functionalCurrencyCode: '',
    effectiveFromUtc: null,
    effectiveToUtc: null,
    baseAccountingBookId: null,
    sortOrder: 100,
});

const statusOf = (book: AccountingBook): AccountingBookLifecycleStatus =>
    book.lifecycleStatus ?? (book.isActive ? 'Active' : 'Draft');

const typeOf = (book: AccountingBook): AccountingBookType =>
    book.bookType ?? (book.isDefault ? 'PrimaryFull' : 'ParallelFull');

const dateValue = (value?: string | null) => value ? value.slice(0, 10) : '';
const utcDate = (value: string) => value ? `${value}T00:00:00.000Z` : null;

export default function AccountingBooksSettingsPage() {
    const { hasPermission, isLoading: authLoading, error: authError } = useAuth();
    const access = getAccountingBookAccess(hasPermission);
    const { toast } = useToast();
    const [books, setBooks] = useState<AccountingBook[]>([]);
    const [currencies, setCurrencies] = useState<Currency[]>([]);
    const [currencyLoading, setCurrencyLoading] = useState(true);
    const [currencyError, setCurrencyError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [editing, setEditing] = useState<AccountingBook | null | undefined>(undefined);
    const [form, setForm] = useState<SaveAccountingBook>(blankForm());
    const [detail, setDetail] = useState<AccountingBook | null>(null);
    const [detailLoading, setDetailLoading] = useState(false);
    const [detailError, setDetailError] = useState<string | null>(null);
    const [transitioning, setTransitioning] = useState<AccountingBook | null>(null);
    const [targetStatus, setTargetStatus] = useState<AccountingBookLifecycleStatus>('Configuring');
    const [transitionReason, setTransitionReason] = useState('');
    const [decision, setDecision] = useState<{ book: AccountingBook; action: 'approve' | 'reject' } | null>(null);
    const [decisionReason, setDecisionReason] = useState('');
    const [saving, setSaving] = useState(false);

    const load = useCallback(async () => {
        if (!access.canRead) return;
        setLoading(true);
        setError(null);
        try {
            const result = await financeDataService.getAccountingBooks(true);
            setBooks([...result].sort((left, right) => left.sortOrder - right.sortOrder || left.code.localeCompare(right.code)));
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : 'Accounting books could not be loaded.');
        } finally {
            setLoading(false);
        }
    }, [access.canRead]);

    useEffect(() => {
        if (!authLoading && access.canRead) void load();
        if (!authLoading && !access.canRead) setLoading(false);
    }, [access.canRead, authLoading, load]);

    const loadCurrencies = useCallback(async () => {
        setCurrencyLoading(true);
        setCurrencyError(null);
        try { setCurrencies(await financeDataService.getCurrencies()); }
        catch (reason) { setCurrencyError(reason instanceof Error ? reason.message : 'Currencies could not be loaded.'); }
        finally { setCurrencyLoading(false); }
    }, []);

    useEffect(() => { if (!authLoading && access.canManage) void loadCurrencies(); }, [access.canManage, authLoading, loadCurrencies]);

    const baseBookOptions = useMemo(() => books.filter(book =>
        book.id !== editing?.id && statusOf(book) !== 'Retired'), [books, editing?.id]);
    const currencyOptions = useMemo(() => {
        const active = currencies.filter(currency => currency.isActive)
            .map(currency => ({ value: currency.currencyCode, label: `${currency.currencyCode} — ${currency.currencyName}` }));
        const existingCode = editing?.functionalCurrencyCode;
        if (existingCode && !active.some(option => option.value === existingCode)) {
            active.push({ value: existingCode, label: `${existingCode} — current assignment (inactive)` });
        }
        return active.sort((left, right) => left.value.localeCompare(right.value));
    }, [currencies, editing?.functionalCurrencyCode]);

    const openEditor = (book?: AccountingBook) => {
        setEditing(book ?? null);
        setForm(book ? {
            code: book.code,
            name: book.name,
            description: book.description ?? '',
            purpose: book.purpose,
            bookType: typeOf(book),
            functionalCurrencyCode: book.functionalCurrencyCode ?? '',
            effectiveFromUtc: dateValue(book.effectiveFromUtc),
            effectiveToUtc: dateValue(book.effectiveToUtc),
            baseAccountingBookId: book.baseAccountingBookId ?? null,
            sortOrder: book.sortOrder,
            rowVersion: book.rowVersion,
        } : blankForm());
    };

    const openDetail = async (book: AccountingBook) => {
        setDetail(book);
        setDetailLoading(true);
        setDetailError(null);
        try { setDetail(await financeDataService.getAccountingBook(book.id)); }
        catch (reason) { setDetailError(reason instanceof Error ? reason.message : 'Book detail could not be loaded.'); }
        finally { setDetailLoading(false); }
    };

    const save = async () => {
        if (!form.code.trim() || !form.name.trim() || !form.purpose.trim()) return;
        setSaving(true);
        try {
            const request: SaveAccountingBook = {
                ...form,
                code: form.code.trim().toUpperCase(),
                name: form.name.trim(),
                purpose: form.purpose.trim(),
                functionalCurrencyCode: form.bookType === 'Delta' ? null : form.functionalCurrencyCode?.trim().toUpperCase(),
                baseAccountingBookId: form.bookType === 'Delta' ? form.baseAccountingBookId : null,
                effectiveFromUtc: utcDate(form.effectiveFromUtc ?? ''),
                effectiveToUtc: utcDate(form.effectiveToUtc ?? ''),
            };
            if (editing) await financeDataService.updateAccountingBook(editing.id, request);
            else await financeDataService.createAccountingBook(request);
            toast({ title: editing ? 'Accounting book updated' : 'Accounting book created' });
            setEditing(undefined);
            await load();
        } catch (reason) {
            toast({ title: 'Could not save accounting book', description: reason instanceof Error ? reason.message : 'The request failed.', variant: 'destructive' });
        } finally { setSaving(false); }
    };

    const openTransition = (book: AccountingBook) => {
        const firstTarget = lifecycleTargets[statusOf(book)][0];
        if (!firstTarget) return;
        setTransitioning(book);
        setTargetStatus(firstTarget);
        setTransitionReason('');
    };

    const requestTransition = async () => {
        if (!transitioning?.rowVersion || !transitionReason.trim()) return;
        setSaving(true);
        try {
            await financeDataService.requestAccountingBookTransition(transitioning.id, {
                targetStatus,
                reason: transitionReason.trim(),
                rowVersion: transitioning.rowVersion,
            });
            toast({ title: 'Lifecycle transition submitted', description: 'A different authorized user must approve this request.' });
            setTransitioning(null);
            await load();
        } catch (reason) {
            toast({ title: 'Could not request transition', description: reason instanceof Error ? reason.message : 'The request failed.', variant: 'destructive' });
        } finally { setSaving(false); }
    };

    const decideTransition = async () => {
        if (!decision?.book.rowVersion || !decisionReason.trim()) return;
        setSaving(true);
        try {
            const request = { reason: decisionReason.trim(), rowVersion: decision.book.rowVersion };
            if (decision.action === 'approve') await financeDataService.approveAccountingBookTransition(decision.book.id, request);
            else await financeDataService.rejectAccountingBookTransition(decision.book.id, request);
            toast({ title: decision.action === 'approve' ? 'Transition approved' : 'Transition rejected' });
            setDecision(null);
            await load();
        } catch (reason) {
            toast({ title: 'Could not record transition decision', description: reason instanceof Error ? reason.message : 'The request failed.', variant: 'destructive' });
        } finally { setSaving(false); }
    };

    if (authLoading || loading) {
        return <div className="flex min-h-[320px] items-center justify-center"><Loader2 className="h-6 w-6 animate-spin" aria-label="Loading accounting books" /></div>;
    }

    if (authError) {
        return <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Authentication unavailable</AlertTitle><AlertDescription>Accounting-book access could not be verified. Refresh after authentication is restored.</AlertDescription></Alert>;
    }

    if (!access.canRead) {
        return <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Permission required</AlertTitle><AlertDescription>Finance.Read is required to browse accounting-book configuration.</AlertDescription></Alert>;
    }

    const updateBlocked = Boolean(editing?.pendingLifecycleStatus);
    const structuralLocked = Boolean(editing && (
        editing.hasAccountingUse
        || editing.initializationStartedAtUtc
        || editing.pendingLifecycleStatus
        || ['Initializing', 'Active', 'Suspended', 'Retired'].includes(statusOf(editing))
    ));
    const targetIsBlockedActivation = targetStatus === 'Active' && !transitioning?.activationReady;

    return <div className="space-y-6 p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
                <Button asChild variant="ghost" className="mb-2 px-0"><Link href="/finance/settings"><ArrowLeft className="mr-2 h-4 w-4" />Finance settings</Link></Button>
                <h1 className="flex items-center gap-2 text-2xl font-semibold"><BookOpen className="h-6 w-6" />Accounting books</h1>
                <p className="text-muted-foreground">Govern ledger purpose, structural identity and lifecycle without enabling cross-book execution.</p>
            </div>
            <div className="flex flex-wrap gap-2"><Button asChild variant="outline"><Link href="/finance/settings/accounting-books/applicability">Applicability policy</Link></Button>{access.canManage && <Button onClick={() => openEditor()}><Plus className="mr-2 h-4 w-4" />New accounting book</Button>}</div>
        </div>

        <Alert className="border-amber-300 bg-amber-50 text-amber-950"><ShieldAlert className="h-4 w-4" /><AlertTitle>Parallel posting remains disabled</AlertTitle><AlertDescription>C4 now governs initialization and exact-book period readiness. Activation remains blocked until that evidence is approved and current; Finance still executes one concrete book at a time.</AlertDescription></Alert>

        {error ? <Alert variant="destructive"><AlertCircle className="h-4 w-4" /><AlertTitle>Could not load accounting books</AlertTitle><AlertDescription className="flex items-center justify-between gap-3"><span>{error}</span><Button size="sm" variant="outline" onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Retry</Button></AlertDescription></Alert>
            : books.length === 0 ? <Card><CardContent className="py-12 text-center"><p className="text-muted-foreground">No accounting books are configured.</p>{access.canManage && <Button className="mt-4" onClick={() => openEditor()}><Plus className="mr-2 h-4 w-4" />Create the first book</Button>}</CardContent></Card>
                : <div className="grid gap-4 lg:grid-cols-2">{books.map(book => {
                    const status = statusOf(book);
                    return <Card key={book.id}>
                        <CardHeader>
                            <div className="flex flex-wrap items-start justify-between gap-3"><div><CardTitle className="text-lg">{book.code} — {book.name}</CardTitle><CardDescription>{book.purpose}</CardDescription></div><div className="flex gap-2"><Badge variant={status === 'Active' ? 'default' : 'secondary'}>{status}</Badge>{book.isDefault && <Badge variant="outline">Primary/default</Badge>}</div></div>
                        </CardHeader>
                        <CardContent className="space-y-3 text-sm">
                            <div className="grid grid-cols-2 gap-2"><span><span className="text-muted-foreground">Type:</span> {typeOf(book)}</span><span><span className="text-muted-foreground">Currency:</span> {book.functionalCurrencyCode || 'Inherited from base'}</span><span><span className="text-muted-foreground">Effective:</span> {dateValue(book.effectiveFromUtc) || 'Not set'}</span><span><span className="text-muted-foreground">Base:</span> {book.baseAccountingBookCode || 'None'}</span></div>
                            {book.pendingLifecycleStatus && <Alert className={book.pendingLifecycleStatus === 'Active' && !book.activationReady ? 'border-amber-300 bg-amber-50' : undefined}><AlertTitle>Pending {book.pendingLifecycleStatus}</AlertTitle><AlertDescription>{book.pendingTransitionReason || 'Awaiting independent review.'}{book.pendingLifecycleStatus === 'Active' && !book.activationReady ? ` ${book.readinessMessage || 'Approved initialization and an open first exact-book period are required.'}` : ''}</AlertDescription></Alert>}
                            <div className="flex flex-wrap gap-2"><Button variant="outline" size="sm" onClick={() => void openDetail(book)}>View details</Button><Button asChild variant="outline" size="sm"><Link href={`/finance/settings/accounting-books/${book.id}/readiness`}>Periods &amp; initialization</Link></Button>{access.canManage && status !== 'Retired' && <Button variant="outline" size="sm" disabled={Boolean(book.pendingLifecycleStatus)} onClick={() => openEditor(book)}><Pencil className="mr-2 h-3.5 w-3.5" />Edit</Button>}{access.canRequestTransition && !book.pendingLifecycleStatus && lifecycleTargets[status].length > 0 && <Button size="sm" onClick={() => openTransition(book)}>Request transition</Button>}{access.canApproveTransition && book.pendingLifecycleStatus && <><Button size="sm" disabled={book.pendingLifecycleStatus === 'Active' && !book.activationReady} onClick={() => { setDecision({ book, action: 'approve' }); setDecisionReason(''); }}>Approve</Button><Button size="sm" variant="destructive" onClick={() => { setDecision({ book, action: 'reject' }); setDecisionReason(''); }}>Reject</Button></>}</div>
                        </CardContent>
                    </Card>;
                })}</div>}

        <Dialog open={editing !== undefined} onOpenChange={open => { if (!open) setEditing(undefined); }}><DialogContent className="max-w-2xl"><DialogHeader><DialogTitle>{editing ? `Edit ${editing.code}` : 'Create accounting book'}</DialogTitle><DialogDescription>Book type, purpose, currency, effective dates and base relationship become immutable once initialization or accounting use begins.</DialogDescription></DialogHeader>
            {structuralLocked && <Alert><ShieldAlert className="h-4 w-4" /><AlertTitle>Structural identity locked</AlertTitle><AlertDescription>{updateBlocked ? 'No changes can be saved while this book has a pending lifecycle transition.' : 'This lifecycle or its initialization/accounting evidence locks structural fields. Only display name, description and display order can be amended.'}</AlertDescription></Alert>}
            <div className="grid gap-4 sm:grid-cols-2">
                <div><Label htmlFor="book-code">Stable code</Label><Input id="book-code" value={form.code} disabled={structuralLocked || Boolean(editing?.isSystemDefined)} onChange={event => setForm({ ...form, code: event.target.value.toUpperCase() })} /></div>
                <div><Label htmlFor="book-name">Name</Label><Input id="book-name" value={form.name} disabled={updateBlocked} onChange={event => setForm({ ...form, name: event.target.value })} /></div>
                <div><Label>Book type</Label><Select value={form.bookType} disabled={structuralLocked} onValueChange={value => setForm({ ...form, bookType: value as AccountingBookType, baseAccountingBookId: null })}><SelectTrigger aria-label="Book type"><SelectValue /></SelectTrigger><SelectContent>{bookTypes.map(type => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent></Select></div>
                <div><Label htmlFor="book-purpose">Accounting purpose / principle</Label><Input id="book-purpose" value={form.purpose} disabled={structuralLocked} onChange={event => setForm({ ...form, purpose: event.target.value })} placeholder="e.g. IFRS reporting" /></div>
                {form.bookType === 'Delta' ? <div className="sm:col-span-2"><Label>Base accounting book</Label><Select value={form.baseAccountingBookId ?? ''} disabled={structuralLocked} onValueChange={value => setForm({ ...form, baseAccountingBookId: value })}><SelectTrigger aria-label="Base accounting book"><SelectValue placeholder="Select governed base book" /></SelectTrigger><SelectContent>{baseBookOptions.map(book => <SelectItem key={book.id} value={book.id}>{book.code} — {book.name}</SelectItem>)}</SelectContent></Select></div>
                    : <div><Label>Functional currency</Label><SearchableOptionPicker label="Functional currency" value={form.functionalCurrencyCode ?? ''} options={currencyOptions} onChange={value => setForm({ ...form, functionalCurrencyCode: value })} placeholder={currencyLoading ? 'Loading currencies…' : 'Select currency'} searchPlaceholder="Search code or name…" emptyMessage="No active currency found." disabled={structuralLocked || currencyLoading || Boolean(currencyError)} />{currencyError && <p className="mt-1 text-sm text-destructive">{currencyError} <Button type="button" variant="link" className="h-auto p-0" onClick={() => void loadCurrencies()}>Retry currencies</Button></p>}</div>}
                <div><Label htmlFor="book-order">Display order</Label><Input id="book-order" type="number" value={form.sortOrder} disabled={updateBlocked} onChange={event => setForm({ ...form, sortOrder: Number(event.target.value) })} /></div>
                <div><Label htmlFor="book-from">Effective from</Label><Input id="book-from" type="date" value={dateValue(form.effectiveFromUtc)} disabled={structuralLocked} onChange={event => setForm({ ...form, effectiveFromUtc: event.target.value })} /></div>
                <div><Label htmlFor="book-to">Effective to</Label><Input id="book-to" type="date" value={dateValue(form.effectiveToUtc)} disabled={structuralLocked} onChange={event => setForm({ ...form, effectiveToUtc: event.target.value })} /></div>
                <div className="sm:col-span-2"><Label htmlFor="book-description">Description</Label><Textarea id="book-description" value={form.description ?? ''} disabled={updateBlocked} onChange={event => setForm({ ...form, description: event.target.value })} /></div>
            </div><DialogFooter><Button variant="outline" onClick={() => setEditing(undefined)}>Cancel</Button><Button disabled={saving || updateBlocked || !form.code.trim() || !form.name.trim() || !form.purpose.trim() || (form.bookType === 'Delta' ? !form.baseAccountingBookId : currencyLoading || Boolean(currencyError) || !currencyOptions.some(option => option.value === form.functionalCurrencyCode))} onClick={() => void save()}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save book</Button></DialogFooter>
        </DialogContent></Dialog>

        <Dialog open={detail !== null} onOpenChange={open => { if (!open) setDetail(null); }}><DialogContent><DialogHeader><DialogTitle>{detail?.code} accounting-book details</DialogTitle><DialogDescription>Governed structure and lifecycle evidence for this tenant-owned book.</DialogDescription></DialogHeader>{detailLoading ? <Loader2 className="mx-auto my-8 h-6 w-6 animate-spin" aria-label="Loading book detail" /> : detailError ? <Alert variant="destructive"><AlertTitle>Could not load book detail</AlertTitle><AlertDescription>{detailError} <Button variant="link" className="h-auto p-0" onClick={() => detail && void openDetail(detail)}>Retry</Button></AlertDescription></Alert> : detail && <div className="grid gap-3 text-sm sm:grid-cols-2"><div><span className="text-muted-foreground">Lifecycle</span><p>{statusOf(detail)}</p></div><div><span className="text-muted-foreground">Type</span><p>{typeOf(detail)}</p></div><div><span className="text-muted-foreground">Purpose</span><p>{detail.purpose}</p></div><div><span className="text-muted-foreground">Functional currency</span><p>{detail.functionalCurrencyCode || 'Inherited from base'}</p></div><div><span className="text-muted-foreground">Accounting use</span><p>{detail.hasAccountingUse ? 'Yes — structure locked' : 'No'}</p></div><div><span className="text-muted-foreground">Activation readiness</span><p>{detail.activationReady ? 'Ready' : 'Not ready'}</p></div><div className="sm:col-span-2"><span className="text-muted-foreground">Readiness evidence</span><p>{detail.readinessMessage || 'Approved initialization and an open first exact-book period are required.'}</p></div></div>}</DialogContent></Dialog>

        <Dialog open={transitioning !== null} onOpenChange={open => { if (!open) setTransitioning(null); }}><DialogContent><DialogHeader><DialogTitle>Request lifecycle transition</DialogTitle><DialogDescription>This audited request requires a different authorized checker to approve it.</DialogDescription></DialogHeader><div className="space-y-4"><div><Label>Target lifecycle state</Label><Select value={targetStatus} onValueChange={value => setTargetStatus(value as AccountingBookLifecycleStatus)}><SelectTrigger aria-label="Target lifecycle state"><SelectValue /></SelectTrigger><SelectContent>{transitioning && lifecycleTargets[statusOf(transitioning)].map(status => <SelectItem key={status} value={status}>{status}</SelectItem>)}</SelectContent></Select></div>{targetStatus === 'Active' && <Alert className="border-amber-300 bg-amber-50"><ShieldAlert className="h-4 w-4" /><AlertTitle>Activation readiness required</AlertTitle><AlertDescription>{transitioning?.readinessMessage || 'Approved initialization and an open first exact-book period are required before activation.'}</AlertDescription></Alert>}<div><Label htmlFor="transition-reason">Reason</Label><Textarea id="transition-reason" value={transitionReason} onChange={event => setTransitionReason(event.target.value)} placeholder="Explain the governed lifecycle change" /></div></div><DialogFooter><Button variant="outline" onClick={() => setTransitioning(null)}>Cancel</Button><Button disabled={saving || !transitionReason.trim() || targetIsBlockedActivation} onClick={() => void requestTransition()}>Submit for approval</Button></DialogFooter></DialogContent></Dialog>

        <Dialog open={decision !== null} onOpenChange={open => { if (!open) setDecision(null); }}><DialogContent><DialogHeader><DialogTitle>{decision?.action === 'approve' ? 'Approve' : 'Reject'} pending transition</DialogTitle><DialogDescription>Maker-checker separation and current readiness are revalidated by Finance before the decision is committed.</DialogDescription></DialogHeader><div><Label htmlFor="decision-reason">Checker reason</Label><Textarea id="decision-reason" value={decisionReason} onChange={event => setDecisionReason(event.target.value)} /></div><DialogFooter><Button variant="outline" onClick={() => setDecision(null)}>Cancel</Button><Button variant={decision?.action === 'reject' ? 'destructive' : 'default'} disabled={saving || !decisionReason.trim()} onClick={() => void decideTransition()}>{decision?.action === 'approve' ? 'Approve transition' : 'Reject transition'}</Button></DialogFooter></DialogContent></Dialog>
    </div>;
}
