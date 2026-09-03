'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { AlertTriangle, ChevronLeft, Loader2, Pencil, Plus, Search, Tags } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { AccountClassification, AccountClassificationWhereUsed, AccountingBook, AccountType, SaveAccountClassification } from '@/types/finance';
import { filterClassificationHierarchy, flattenClassificationHierarchy } from '@/components/finance/classifications/classification-hierarchy';
import { canConfigureClassifications } from '@/components/finance/classifications/classification-access';
import { useAuth } from '@/hooks/use-auth';

const accountTypes: AccountType[] = ['Asset', 'Liability', 'Equity', 'Revenue', 'Expense'];
const systemRoles = ['Cash', 'Bank', 'ReceivableControl', 'PayableControl', 'InventoryControl', 'FixedAssetCost', 'AccumulatedDepreciation', 'AssetUnderConstruction', 'InputTax', 'OutputTax', 'WhtReceivable', 'WhtPayable'];
const liabilityRoles = new Set(['PayableControl', 'OutputTax', 'WhtPayable']);

const blankForm = (bookId = ''): SaveAccountClassification => ({
    accountingBookId: bookId, code: '', name: '', description: '', coreAccountType: 'Asset',
    defaultRevaluationTreatment: 'Exclude', systemRole: null, isPostingClassification: true,
    status: 'Draft', displayOrder: 100,
});

export default function AccountClassificationsPage() {
    const { toast } = useToast();
    const { hasPermission } = useAuth();
    const canConfigure = canConfigureClassifications(hasPermission);
    const [books, setBooks] = useState<AccountingBook[]>([]);
    const [bookId, setBookId] = useState('');
    const [items, setItems] = useState<AccountClassification[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [search, setSearch] = useState('');
    const [status, setStatus] = useState('all');
    const [accountType, setAccountType] = useState('all');
    const [editing, setEditing] = useState<AccountClassification | null | undefined>(undefined);
    const [form, setForm] = useState<SaveAccountClassification>(blankForm());
    const [saving, setSaving] = useState(false);
    const [usage, setUsage] = useState<AccountClassificationWhereUsed | null>(null);
    const [usageLoading, setUsageLoading] = useState(false);
    const [retiring, setRetiring] = useState<AccountClassification | null>(null);
    const [retirementReason, setRetirementReason] = useState('');

    const load = useCallback(async (selectedBookId?: string) => {
        setLoading(true); setError(null);
        try {
            const loadedBooks = books.length ? books : await financeDataService.getAccountingBooks(true);
            if (!books.length) setBooks(loadedBooks);
            const selected = selectedBookId || bookId || loadedBooks.find(book => book.isDefault)?.id || loadedBooks[0]?.id || '';
            setBookId(selected);
            setItems(selected ? await financeDataService.getAccountClassifications(selected, true) : []);
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : 'Classifications could not be loaded.');
        } finally { setLoading(false); }
    }, [bookId, books]);

    useEffect(() => { void load(); }, []);

    const rows = useMemo(() => filterClassificationHierarchy(
        flattenClassificationHierarchy(items), search, status, accountType,
    ), [items, search, status, accountType]);

    const openEditor = (item?: AccountClassification) => {
        setEditing(item ?? null);
        setForm(item ? {
            accountingBookId: item.accountingBookId, parentClassificationId: item.parentClassificationId,
            code: item.code, name: item.name, description: item.description,
            coreAccountType: item.coreAccountType, defaultRevaluationTreatment: item.defaultRevaluationTreatment,
            systemRole: item.systemRole, isPostingClassification: item.isPostingClassification,
            status: item.status, displayOrder: item.displayOrder, rowVersion: item.rowVersion,
        } : blankForm(bookId));
    };

    const save = async () => {
        if (!form.code.trim() || !form.name.trim()) return;
        setSaving(true);
        try {
            if (editing) await financeDataService.updateAccountClassification(editing.id, form);
            else await financeDataService.createAccountClassification(form);
            toast({ title: editing ? 'Classification updated' : 'Classification created' });
            setEditing(undefined); await load(bookId);
        } catch (reason) {
            toast({ title: 'Could not save classification', description: reason instanceof Error ? reason.message : 'The request failed.', variant: 'destructive' });
        } finally { setSaving(false); }
    };

    const showUsage = async (item: AccountClassification) => {
        setUsageLoading(true); setUsage(null);
        try { setUsage(await financeDataService.getAccountClassificationWhereUsed(item.id)); }
        catch (reason) { toast({ title: 'Could not load where-used information', description: reason instanceof Error ? reason.message : 'The request failed.', variant: 'destructive' }); }
        finally { setUsageLoading(false); }
    };

    const retire = async () => {
        if (!retiring || !retirementReason.trim()) return;
        setSaving(true);
        try {
            await financeDataService.retireAccountClassification(retiring.id, retirementReason, retiring.rowVersion);
            toast({ title: 'Classification retired' }); setRetiring(null); setRetirementReason(''); await load(bookId);
        } catch (reason) { toast({ title: 'Could not retire classification', description: reason instanceof Error ? reason.message : 'The request failed.', variant: 'destructive' }); }
        finally { setSaving(false); }
    };

    const possibleParents = items.filter(item => item.id !== editing?.id && !item.isPostingClassification
        && item.status !== 'Retired' && item.coreAccountType === form.coreAccountType);
    const compatibleSystemRoles = systemRoles.filter(role =>
        form.coreAccountType === 'Liability' ? liabilityRoles.has(role) : form.coreAccountType === 'Asset' && !liabilityRoles.has(role));

    return <div className="space-y-6 p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
                <Button variant="ghost" size="sm" asChild className="mb-2 -ml-3"><Link href="/finance/settings"><ChevronLeft className="mr-1 h-4 w-4" />Finance settings</Link></Button>
                <h1 className="flex items-center gap-2 text-2xl font-semibold"><Tags className="h-6 w-6" />Account classifications</h1>
                <p className="text-muted-foreground">Configure book-specific hierarchy, presentation and default accounting behavior.</p>
            </div>
            {canConfigure && <Button onClick={() => openEditor()} disabled={!bookId}><Plus className="mr-2 h-4 w-4" />New classification</Button>}
        </div>

        <Card><CardHeader><CardTitle>Classification hierarchy</CardTitle><CardDescription>Accounts can be assigned only to active posting leaves compatible with their core account type.</CardDescription></CardHeader>
            <CardContent className="space-y-4">
                <div className="grid gap-3 md:grid-cols-4">
                    <Select value={bookId} onValueChange={value => void load(value)}><SelectTrigger aria-label="Accounting book"><SelectValue placeholder="Select book" /></SelectTrigger><SelectContent>{books.map(book => <SelectItem key={book.id} value={book.id}>{book.name} ({book.code})</SelectItem>)}</SelectContent></Select>
                    <div className="relative"><Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input className="pl-9" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search code or name" /></div>
                    <Select value={status} onValueChange={setStatus}><SelectTrigger aria-label="Lifecycle status"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All lifecycle states</SelectItem><SelectItem value="Draft">Draft</SelectItem><SelectItem value="Active">Active</SelectItem><SelectItem value="Retired">Retired</SelectItem></SelectContent></Select>
                    <Select value={accountType} onValueChange={setAccountType}><SelectTrigger aria-label="Core account type"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All account types</SelectItem>{accountTypes.map(type => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent></Select>
                </div>
                {loading && <div className="flex items-center justify-center gap-2 py-12 text-muted-foreground"><Loader2 className="h-5 w-5 animate-spin" />Loading classifications…</div>}
                {!loading && error && <Alert variant="destructive"><AlertTriangle className="h-4 w-4" /><AlertTitle>Unable to load classifications</AlertTitle><AlertDescription>{error} <Button variant="link" className="h-auto p-0" onClick={() => void load(bookId)}>Retry</Button></AlertDescription></Alert>}
                {!loading && !error && rows.length === 0 && <div className="rounded-md border border-dashed py-12 text-center text-muted-foreground">No classifications match these filters.</div>}
                {!loading && !error && rows.length > 0 && <div className="overflow-hidden rounded-md border">
                    {rows.map(({ classification: item, depth }) => <div key={item.id} className="flex flex-wrap items-center gap-3 border-b p-3 last:border-b-0" style={{ paddingLeft: `${12 + depth * 28}px` }}>
                        <div className="min-w-[260px] flex-1"><div className="font-medium">{item.code} — {item.name}</div><div className="text-xs text-muted-foreground">{item.coreAccountType}{item.systemRole ? ` · ${item.systemRole}` : ''} · Default revaluation: {item.defaultRevaluationTreatment}</div></div>
                        <Badge variant={item.status === 'Active' ? 'default' : 'secondary'}>{item.status}</Badge>
                        <Badge variant="outline">{item.isLeaf && item.isPostingClassification ? 'Posting leaf' : `${item.childCount} children`}</Badge>
                        <Button variant="ghost" size="sm" onClick={() => void showUsage(item)}>{item.enabledAccountCount}/{item.totalAccountCount} used</Button>
                        {canConfigure && <Button variant="ghost" size="icon" aria-label={`Edit ${item.code}`} disabled={item.status === 'Retired'} onClick={() => openEditor(item)}><Pencil className="h-4 w-4" /></Button>}
                        {canConfigure && item.status !== 'Retired' && <Button variant="outline" size="sm" disabled={!item.canRetire} onClick={() => setRetiring(item)}>Retire</Button>}
                    </div>)}
                </div>}
            </CardContent>
        </Card>

        <Dialog open={editing !== undefined} onOpenChange={open => { if (!open) setEditing(undefined); }}><DialogContent className="max-w-2xl"><DialogHeader><DialogTitle>{editing ? `Edit ${editing.code}` : 'Create classification'}</DialogTitle><DialogDescription>Codes become immutable after the classification is used by an account.</DialogDescription></DialogHeader>
            <div className="grid gap-4 md:grid-cols-2">
                <div><Label htmlFor="classification-code">Code</Label><Input id="classification-code" value={form.code} onChange={event => setForm({ ...form, code: event.target.value.toUpperCase() })} disabled={Boolean(editing?.totalAccountCount)} /></div>
                <div><Label htmlFor="classification-name">Name</Label><Input id="classification-name" value={form.name} onChange={event => setForm({ ...form, name: event.target.value })} /></div>
                <div><Label>Core account type</Label><Select value={form.coreAccountType} disabled={Boolean(editing?.totalAccountCount)} onValueChange={value => setForm({ ...form, coreAccountType: value as AccountType, parentClassificationId: null, systemRole: null })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{accountTypes.map(type => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent></Select></div>
                <div><Label>Parent</Label><Select value={form.parentClassificationId ?? 'none'} onValueChange={value => setForm({ ...form, parentClassificationId: value === 'none' ? null : value })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No parent</SelectItem>{possibleParents.map(parent => <SelectItem key={parent.id} value={parent.id}>{parent.code} — {parent.name}</SelectItem>)}</SelectContent></Select></div>
                <div><Label>Lifecycle state</Label><Select value={form.status} onValueChange={value => setForm({ ...form, status: value as SaveAccountClassification['status'] })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Draft">Draft</SelectItem><SelectItem value="Active">Active</SelectItem></SelectContent></Select></div>
                <div><Label>Default revaluation</Label><Select value={form.defaultRevaluationTreatment} onValueChange={value => setForm({ ...form, defaultRevaluationTreatment: value as 'Exclude' | 'Include' })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Exclude">Exclude</SelectItem><SelectItem value="Include">Include</SelectItem></SelectContent></Select></div>
                <div><Label>System role</Label><Select value={form.systemRole ?? 'none'} onValueChange={value => setForm({ ...form, systemRole: value === 'none' ? null : value })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No system role</SelectItem>{compatibleSystemRoles.map(role => <SelectItem key={role} value={role}>{role}</SelectItem>)}</SelectContent></Select></div>
                <div><Label htmlFor="display-order">Display order</Label><Input id="display-order" type="number" value={form.displayOrder} onChange={event => setForm({ ...form, displayOrder: Number(event.target.value) })} /></div>
                <div className="flex items-center justify-between rounded-md border p-3 md:col-span-2"><div><Label htmlFor="posting-classification">Posting classification</Label><p className="text-xs text-muted-foreground">Only leaf classifications can be used by GL accounts.</p></div><Switch id="posting-classification" checked={form.isPostingClassification} onCheckedChange={checked => setForm({ ...form, isPostingClassification: checked })} /></div>
                <div className="md:col-span-2"><Label htmlFor="classification-description">Description</Label><Textarea id="classification-description" value={form.description ?? ''} onChange={event => setForm({ ...form, description: event.target.value })} /></div>
            </div><DialogFooter><Button variant="outline" onClick={() => setEditing(undefined)}>Cancel</Button><Button disabled={saving || !form.code.trim() || !form.name.trim()} onClick={() => void save()}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save</Button></DialogFooter>
        </DialogContent></Dialog>

        <Dialog open={usage !== null || usageLoading} onOpenChange={open => { if (!open) setUsage(null); }}><DialogContent><DialogHeader><DialogTitle>Where used{usage ? `: ${usage.classificationCode}` : ''}</DialogTitle><DialogDescription>Enabled account assignments and live Draft layouts can block structural lifecycle changes. Published snapshots remain historical evidence.</DialogDescription></DialogHeader>{usageLoading ? <Loader2 className="mx-auto my-8 h-6 w-6 animate-spin" /> : usage && <div className="max-h-[60vh] space-y-4 overflow-y-auto"><div><h4 className="mb-2 text-sm font-medium">GL account assignments ({usage.mappings.length})</h4>{usage.mappings.length === 0 ? <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">No account assignments.</p> : usage.mappings.map(mapping => <div key={mapping.accountAccountingBookId} className="mb-2 flex justify-between rounded-md border p-3"><span>{mapping.accountCode} — {mapping.accountName}</span><Badge variant={mapping.isEnabled ? 'default' : 'secondary'}>{mapping.isEnabled ? 'Enabled' : 'Historical'}</Badge></div>)}</div><div><h4 className="mb-2 text-sm font-medium">Financial-statement layouts ({usage.layoutReferences.length})</h4>{usage.layoutReferences.length === 0 ? <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">No Draft or published layout references.</p> : usage.layoutReferences.map(reference => <div key={`${reference.versionId}-${reference.rowCode}`} className="mb-2 flex items-start justify-between gap-3 rounded-md border p-3"><span><span className="font-medium">{reference.layoutCode} — {reference.layoutName}</span><span className="block text-xs text-muted-foreground">Version {reference.versionNumber} · row {reference.rowCode}</span></span><Badge variant={reference.isHistoricalSnapshot ? 'outline' : 'secondary'}>{reference.isHistoricalSnapshot ? 'Published snapshot' : 'Live Draft'}</Badge></div>)}</div></div>}</DialogContent></Dialog>

        <Dialog open={retiring !== null} onOpenChange={open => { if (!open) setRetiring(null); }}><DialogContent><DialogHeader><DialogTitle>Retire {retiring?.code}</DialogTitle><DialogDescription>Retirement is audited and cannot proceed while active children or enabled account assignments exist.</DialogDescription></DialogHeader><div><Label htmlFor="retirement-reason">Reason</Label><Textarea id="retirement-reason" value={retirementReason} onChange={event => setRetirementReason(event.target.value)} placeholder="Explain why this classification is being retired" /></div><DialogFooter><Button variant="outline" onClick={() => setRetiring(null)}>Cancel</Button><Button variant="destructive" disabled={saving || !retirementReason.trim()} onClick={() => void retire()}>Retire classification</Button></DialogFooter></DialogContent></Dialog>
    </div>;
}
