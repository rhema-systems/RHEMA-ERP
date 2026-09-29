'use client';

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { AlertCircle, Loader2, Pencil, Plus, RefreshCw, Search, ShieldCheck } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { Account, FinanceDimensionAccountRule, FinanceDimensionDefinition, FinanceDimensionRouteCertification, UpsertFinanceDimensionAccountRule, UpsertFinanceDimensionDefinition, UpsertFinanceDimensionValue } from '@/types/finance';

const today = () => new Date().toISOString().slice(0, 10);
const blankDefinition = (): UpsertFinanceDimensionDefinition => ({ code: '', name: '', description: '', classification: 'Analytical', valueSourceType: 'Lookup', isActive: true, displayOrder: 0 });
const blankValue = (): UpsertFinanceDimensionValue => ({ code: '', name: '', effectiveDate: today(), isActive: true, displayOrder: 0 });
const blankRule = (): UpsertFinanceDimensionAccountRule => ({ accountId: '', financeDimensionDefinitionId: '', ruleType: 'Optional', routeId: 'ManualJournalEntry', effectiveDate: today(), isActive: true });
const ruleHelp = {
    Optional: 'Users may provide a value, but the transaction can proceed without one.',
    Required: 'The transaction cannot proceed until a value is supplied.',
    Fixed: 'The configured value is applied automatically and cannot be changed.',
    Prohibited: 'This dimension cannot be used for the selected account and route.',
};
const classificationHelp = {
    Analytical: 'Used for reporting and analysis. A filtered slice is not required to balance independently.',
    Balancing: 'Reserved for dimensions with a separately configured and certified balancing policy. Selecting this does not create balancing entries by itself.',
    Derived: 'Resolved by a certified source adapter from trusted business evidence rather than freely entered by a user.',
};
const valueSourceHelp = {
    Lookup: 'Finance owns the list and authorized users maintain its effective-dated values here.',
    EntityBacked: 'Another module owns the values. Finance receives them through a certified adapter; duplicate manual values are not allowed.',
};
const entitySources = [
    ['Project', 'Project / Development'], ['EstateManagedAsset', 'Estate / Property / Site'],
    ['Contract', 'Contract'], ['BankAccount', 'Bank account'], ['FixedAsset', 'Fixed asset'],
    ['EmployeeAssignment', 'Employee assignment / Department'],
] as const;
const friendlyRoute = (route?: string) => route ? route.replace(/^finance\./, '').replace(/[.-]/g, ' ').replace(/\b\w/g, c => c.toUpperCase()) : 'All applicable routes';

export default function FinanceDimensionsPage() {
    const { toast } = useToast();
    const [definitions, setDefinitions] = useState<FinanceDimensionDefinition[]>([]);
    const [rules, setRules] = useState<FinanceDimensionAccountRule[]>([]);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [routes, setRoutes] = useState<FinanceDimensionRouteCertification[]>([]);
    const [selectedId, setSelectedId] = useState('');
    const [definitionForm, setDefinitionForm] = useState(blankDefinition());
    const [valueForm, setValueForm] = useState(blankValue());
    const [ruleForm, setRuleForm] = useState(blankRule());
    const [editingDefinition, setEditingDefinition] = useState<string | null>(null);
    const [editingValue, setEditingValue] = useState<string | null>(null);
    const [editingRule, setEditingRule] = useState<string | null>(null);
    const [dialog, setDialog] = useState<'definition' | 'value' | 'rule' | null>(null);
    const [dimensionSearch, setDimensionSearch] = useState('');
    const [contentSearch, setContentSearch] = useState('');
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const selected = useMemo(() => definitions.find(d => d.id === selectedId), [definitions, selectedId]);
    const selectedRules = useMemo(() => rules.filter(r => r.financeDimensionDefinitionId === selectedId), [rules, selectedId]);
    const visibleDefinitions = definitions.filter(d => `${d.code} ${d.name}`.toLowerCase().includes(dimensionSearch.toLowerCase()));
    const visibleValues = (selected?.values ?? []).filter(v => `${v.code} ${v.name}`.toLowerCase().includes(contentSearch.toLowerCase()));
    const visibleRules = selectedRules.filter(r => `${r.accountNumber} ${r.accountName} ${r.sourceRoute ?? ''} ${r.ruleType}`.toLowerCase().includes(contentSearch.toLowerCase()));

    const load = async () => {
        setLoading(true); setError(null);
        try {
            const [ds, rs, accts, routeRows] = await Promise.all([financeDataService.getFinanceDimensions(true), financeDataService.getFinanceDimensionRules(), financeDataService.getAccounts({ status: 'Active' }), financeDataService.getFinanceDimensionCertificationRoutes()]);
            setDefinitions(ds); setRules(rs); setAccounts(accts.filter(a => a.allowDirectPosting ?? true)); setRoutes(routeRows);
            setSelectedId(current => ds.some(d => d.id === current) ? current : (ds[0]?.id ?? ''));
        } catch (cause) { setError(cause instanceof Error ? cause.message : 'Failed to load Finance coding dimensions.'); }
        finally { setLoading(false); }
    };
    useEffect(() => { void load(); }, []);

    const openDefinition = (item?: FinanceDimensionDefinition) => {
        setEditingDefinition(item?.id ?? null);
        setDefinitionForm(item ? { code: item.code, name: item.name, description: item.description, classification: item.classification, valueSourceType: item.valueSourceType, sourceEntityType: item.sourceEntityType, isActive: item.isActive, displayOrder: item.displayOrder } : blankDefinition());
        setDialog('definition');
    };
    const openValue = (value?: FinanceDimensionDefinition['values'][number]) => {
        setEditingValue(value?.id ?? null);
        setValueForm(value ? { code: value.code, name: value.name, parentValueId: value.parentValueId, sourceEntityType: value.sourceEntityType, sourceEntityId: value.sourceEntityId, effectiveDate: value.effectiveDate.slice(0, 10), expiryDate: value.expiryDate?.slice(0, 10), isActive: value.isActive, displayOrder: value.displayOrder } : blankValue());
        setDialog('value');
    };
    const openRule = (rule?: FinanceDimensionAccountRule) => {
        setEditingRule(rule?.id ?? null);
        setRuleForm(rule ? { accountId: rule.accountId, financeDimensionDefinitionId: rule.financeDimensionDefinitionId, ruleType: rule.ruleType, defaultDimensionValueId: rule.defaultDimensionValueId, routeId: rule.routeId, sourceModule: rule.sourceModule, sourceDocumentType: rule.sourceDocumentType, postingAction: rule.postingAction, effectiveDate: rule.effectiveDate.slice(0, 10), expiryDate: rule.expiryDate?.slice(0, 10), isActive: rule.isActive } : { ...blankRule(), financeDimensionDefinitionId: selectedId });
        setDialog('rule');
    };
    const saveDefinition = async () => {
        setSaving(true);
        try { const result = editingDefinition ? await financeDataService.updateFinanceDimension(editingDefinition, definitionForm) : await financeDataService.createFinanceDimension(definitionForm); setSelectedId(result.id); setDialog(null); await load(); toast({ title: editingDefinition ? 'Dimension updated' : 'Dimension created' }); }
        catch (cause) { toast({ title: 'Dimension not saved', description: cause instanceof Error ? cause.message : 'Request failed.', variant: 'destructive' }); }
        finally { setSaving(false); }
    };
    const saveValue = async () => {
        if (!selectedId) return; setSaving(true);
        try { if (editingValue) await financeDataService.updateFinanceDimensionValue(selectedId, editingValue, valueForm); else await financeDataService.createFinanceDimensionValue(selectedId, valueForm); setDialog(null); await load(); toast({ title: editingValue ? 'Value updated' : 'Value created' }); }
        catch (cause) { toast({ title: 'Value not saved', description: cause instanceof Error ? cause.message : 'Request failed.', variant: 'destructive' }); }
        finally { setSaving(false); }
    };
    const saveRule = async () => {
        setSaving(true);
        try { if (editingRule) await financeDataService.updateFinanceDimensionRule(editingRule, ruleForm); else await financeDataService.createFinanceDimensionRule(ruleForm); setDialog(null); await load(); toast({ title: editingRule ? 'Rule updated' : 'Rule created' }); }
        catch (cause) { toast({ title: 'Rule not saved', description: cause instanceof Error ? cause.message : 'Request failed.', variant: 'destructive' }); }
        finally { setSaving(false); }
    };

    return <div className="container mx-auto space-y-5 p-6">
        <div className="flex flex-col justify-between gap-4 md:flex-row md:items-start">
            <div><h1 className="text-3xl font-bold">Finance Coding Dimensions</h1><p className="text-muted-foreground">Manage the classifications used to analyse transactions beyond the natural GL account.</p></div>
            <div className="flex flex-wrap gap-2"><Button variant="outline" asChild><Link href="/administration/finance/dimensions/readiness"><ShieldCheck className="mr-2 h-4 w-4" />Route readiness</Link></Button><Button variant="outline" onClick={() => void load()} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button><Button onClick={() => openDefinition()}><Plus className="mr-2 h-4 w-4" />New dimension</Button></div>
        </div>
        <Alert className="py-3"><AlertCircle className="h-4 w-4" /><AlertTitle>Controlled rollout</AlertTitle><AlertDescription>AP and AR currently capture dimensions optionally until each transaction route passes readiness checks and is promoted.</AlertDescription></Alert>
        {error && <Alert variant="destructive"><AlertTitle>Unable to load dimensions</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
        {loading ? <div className="flex min-h-48 items-center justify-center gap-2 text-muted-foreground"><Loader2 className="h-5 w-5 animate-spin" />Loading dimensions…</div> :
        <div className="grid gap-5 lg:grid-cols-[300px_minmax(0,1fr)]">
            <Card className="h-fit lg:sticky lg:top-4"><CardHeader className="pb-3"><CardTitle className="text-lg">Dimensions</CardTitle><CardDescription>{definitions.length} configured</CardDescription><SearchBox value={dimensionSearch} onChange={setDimensionSearch} placeholder="Search dimensions…" /></CardHeader><CardContent className="space-y-1 p-2 pt-0">
                {visibleDefinitions.map(item => <button key={item.id} type="button" onClick={() => { setSelectedId(item.id); setContentSearch(''); }} className={`w-full rounded-md border px-3 py-3 text-left transition-colors ${selectedId === item.id ? 'border-primary bg-primary/5 shadow-sm' : 'border-transparent hover:bg-muted/60'}`}><div className="flex justify-between gap-2"><div className="min-w-0"><div className="truncate font-semibold">{item.name}</div><div className="font-mono text-xs text-muted-foreground">{item.code}</div></div><Badge variant="secondary">{item.values.length}</Badge></div><div className="mt-2 text-xs text-muted-foreground">{item.classification} · {item.valueSourceType === 'Lookup' ? 'Lookup' : 'Entity-backed'}{!item.isActive && ' · Inactive'}</div></button>)}
                {!visibleDefinitions.length && <p className="p-6 text-center text-sm text-muted-foreground">No dimensions found.</p>}
            </CardContent></Card>
            {selected && <Card className="min-w-0"><CardHeader className="border-b pb-4"><div className="flex justify-between gap-4"><div><div className="mb-2 flex gap-2"><Badge>{selected.isActive ? 'Active' : 'Inactive'}</Badge><Badge variant="outline">{selected.classification}</Badge></div><CardTitle>{selected.name}</CardTitle><CardDescription className="mt-1"><span className="font-mono">{selected.code}</span>{selected.description ? ` · ${selected.description}` : ''}</CardDescription></div><Button variant="outline" size="sm" onClick={() => openDefinition(selected)}><Pencil className="mr-2 h-4 w-4" />Edit</Button></div></CardHeader><CardContent className="pt-5">
                <Tabs defaultValue="values" onValueChange={() => setContentSearch('')}><TabsList className="grid h-auto w-full grid-cols-2 sm:w-auto sm:grid-cols-4"><TabsTrigger value="values">Values ({selected.values.length})</TabsTrigger><TabsTrigger value="rules">Account rules ({selectedRules.length})</TabsTrigger><TabsTrigger value="usage">Usage</TabsTrigger><TabsTrigger value="settings">Settings</TabsTrigger></TabsList>
                    <TabsContent value="values" className="space-y-4 pt-3"><Toolbar search={contentSearch} setSearch={setContentSearch} placeholder="Search values…" action={selected.valueSourceType === 'Lookup' ? <Button onClick={() => openValue()}><Plus className="mr-2 h-4 w-4" />Add value</Button> : undefined} />{selected.valueSourceType === 'EntityBacked' && <Alert><AlertCircle className="h-4 w-4" /><AlertTitle>Managed by {selected.sourceEntityType ?? 'the source module'}</AlertTitle><AlertDescription>Values are synchronized automatically and cannot be created here.</AlertDescription></Alert>}<div className="rounded-md border"><Table><TableHeader><TableRow><TableHead>Code</TableHead><TableHead>Name</TableHead><TableHead>Status</TableHead><TableHead>Effective period</TableHead><TableHead /></TableRow></TableHeader><TableBody>{visibleValues.map(v => <TableRow key={v.id}><TableCell className="font-mono font-semibold">{v.code}</TableCell><TableCell>{v.name}</TableCell><TableCell><Badge variant="secondary">{v.isActive ? 'Active' : 'Inactive'}</Badge></TableCell><TableCell>{v.effectiveDate.slice(0, 10)}{v.expiryDate ? ` – ${v.expiryDate.slice(0, 10)}` : ' onward'}</TableCell><TableCell className="text-right"><Button variant="ghost" size="sm" onClick={() => openValue(v)} disabled={selected.valueSourceType !== 'Lookup'} aria-label={`Edit ${v.code}`}><Pencil className="h-4 w-4" /></Button></TableCell></TableRow>)}{!visibleValues.length && <EmptyRow columns={5} text="No values found." />}</TableBody></Table></div></TabsContent>
                    <TabsContent value="rules" className="space-y-4 pt-3"><Toolbar search={contentSearch} setSearch={setContentSearch} placeholder="Search account or route…" action={<Button onClick={() => openRule()}><Plus className="mr-2 h-4 w-4" />Add account rule</Button>} /><div className="rounded-md border"><Table><TableHeader><TableRow><TableHead>Account</TableHead><TableHead>Transaction route</TableHead><TableHead>Rule</TableHead><TableHead>Default</TableHead><TableHead /></TableRow></TableHeader><TableBody>{visibleRules.map(r => <TableRow key={r.id}><TableCell><div className="font-medium">{r.accountNumber}</div><div className="text-xs text-muted-foreground">{r.accountName}</div></TableCell><TableCell><div>{friendlyRoute(r.sourceRoute)}</div>{r.sourceRoute && <div className="font-mono text-xs text-muted-foreground">{r.sourceRoute}</div>}</TableCell><TableCell><Badge variant="outline">{r.ruleType}</Badge></TableCell><TableCell>{r.defaultValueCode ?? '—'}</TableCell><TableCell className="text-right"><Button variant="ghost" size="sm" onClick={() => openRule(r)}><Pencil className="h-4 w-4" /></Button></TableCell></TableRow>)}{!visibleRules.length && <EmptyRow columns={5} text="No account rules configured." />}</TableBody></Table></div></TabsContent>
                    <TabsContent value="usage" className="pt-3"><div className="grid gap-4 sm:grid-cols-3"><Metric label="Configured values" value={selected.values.length} detail={`${selected.values.filter(v => v.isActive).length} active`} /><Metric label="Accounts with rules" value={new Set(selectedRules.map(r => r.accountId)).size} detail="Across certified routes" /><Metric label="Transaction routes" value={new Set(selectedRules.map(r => r.routeId ?? r.sourceRoute)).size} detail="With specific rules" /></div><Alert className="mt-4"><AlertCircle className="h-4 w-4" /><AlertTitle>Review usage before deactivation</AlertTitle><AlertDescription>Existing posted transactions retain their captured dimension evidence.</AlertDescription></Alert></TabsContent>
                    <TabsContent value="settings" className="pt-3"><div className="max-w-2xl rounded-md border p-5"><dl className="grid gap-4 sm:grid-cols-2"><Setting label="Classification" value={selected.classification} /><Setting label="Value source" value={selected.valueSourceType === 'Lookup' ? 'Finance lookup list' : `Entity-backed · ${selected.sourceEntityType ?? 'Source adapter'}`} /><Setting label="Display order" value={String(selected.displayOrder)} /><Setting label="Lifecycle" value={selected.isActive ? 'Active for new coding' : 'Inactive for new coding'} /></dl><Button variant="outline" className="mt-5" onClick={() => openDefinition(selected)}><Pencil className="mr-2 h-4 w-4" />Edit settings</Button></div></TabsContent>
                </Tabs>
            </CardContent></Card>}
        </div>}

        <Dialog open={dialog === 'definition'} onOpenChange={open => !open && setDialog(null)}>
            <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
                <DialogHeader><DialogTitle>{editingDefinition ? 'Edit dimension' : 'New dimension'}</DialogTitle><DialogDescription>Define a transaction classification and where its valid values come from.</DialogDescription></DialogHeader>
                <div className="grid gap-4 py-2 sm:grid-cols-2">
                    <Field label="Code">
                        <Input value={definitionForm.code} disabled={Boolean(editingDefinition)} onChange={e => setDefinitionForm({ ...definitionForm, code: e.target.value.toUpperCase() })} />
                        {editingDefinition && <p className="text-xs text-muted-foreground">The code is a permanent integration identity and cannot change after creation.</p>}
                    </Field>
                    <Field label="Name"><Input value={definitionForm.name} onChange={e => setDefinitionForm({ ...definitionForm, name: e.target.value })} /></Field>
                    <div className="space-y-1.5 sm:col-span-2"><Label>Description</Label><Textarea value={definitionForm.description ?? ''} onChange={e => setDefinitionForm({ ...definitionForm, description: e.target.value })} /><p className="text-xs text-muted-foreground">Explain what business responsibility or activity this dimension identifies.</p></div>
                    <Field label="Classification">
                        <Select value={definitionForm.classification} onValueChange={v => setDefinitionForm({ ...definitionForm, classification: v as typeof definitionForm.classification })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Analytical">Analytical</SelectItem><SelectItem value="Balancing">Balancing (requires certified policy)</SelectItem><SelectItem value="Derived">Derived by source adapter</SelectItem></SelectContent></Select>
                        <p className="text-xs text-muted-foreground">{classificationHelp[definitionForm.classification]}</p>
                    </Field>
                    <Field label="Value source">
                        <Select value={definitionForm.valueSourceType} onValueChange={v => setDefinitionForm({ ...definitionForm, valueSourceType: v as typeof definitionForm.valueSourceType, sourceEntityType: v === 'Lookup' ? undefined : definitionForm.sourceEntityType })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Lookup">Finance lookup list</SelectItem><SelectItem value="EntityBacked">Managed by another module</SelectItem></SelectContent></Select>
                        <p className="text-xs text-muted-foreground">{valueSourceHelp[definitionForm.valueSourceType]}</p>
                    </Field>
                    {definitionForm.valueSourceType === 'EntityBacked' && <Field label="Owning entity"><Select value={definitionForm.sourceEntityType ?? ''} onValueChange={sourceEntityType => setDefinitionForm({ ...definitionForm, sourceEntityType })}><SelectTrigger><SelectValue placeholder="Select the authoritative source" /></SelectTrigger><SelectContent>{entitySources.map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent></Select><p className="text-xs text-muted-foreground">Only certified adapters may provision values from this owner.</p></Field>}
                    <Field label="Display order"><Input type="number" value={definitionForm.displayOrder} onChange={e => setDefinitionForm({ ...definitionForm, displayOrder: Number(e.target.value) })} /><p className="text-xs text-muted-foreground">Lower numbers appear earlier in coding screens.</p></Field>
                    <Toggle label="Active for new coding" value={definitionForm.isActive} change={isActive => setDefinitionForm({ ...definitionForm, isActive })} />
                </div>
                <SaveFooter close={() => setDialog(null)} save={saveDefinition} saving={saving} disabled={!definitionForm.code || !definitionForm.name || (definitionForm.valueSourceType === 'EntityBacked' && !definitionForm.sourceEntityType)} label={editingDefinition ? 'Save changes' : 'Create dimension'} />
            </DialogContent>
        </Dialog>
        <Dialog open={dialog === 'value'} onOpenChange={open => !open && setDialog(null)}><DialogContent className="sm:max-w-xl"><DialogHeader><DialogTitle>{editingValue ? 'Edit value' : `Add ${selected?.name ?? 'dimension'} value`}</DialogTitle><DialogDescription>Values are available only during their active effective period.</DialogDescription></DialogHeader><div className="grid gap-4 py-2 sm:grid-cols-2"><Field label="Value code"><Input value={valueForm.code} onChange={e => setValueForm({ ...valueForm, code: e.target.value.toUpperCase() })} /></Field><Field label="Value name"><Input value={valueForm.name} onChange={e => setValueForm({ ...valueForm, name: e.target.value })} /></Field><Field label="Effective date"><Input type="date" value={valueForm.effectiveDate} onChange={e => setValueForm({ ...valueForm, effectiveDate: e.target.value })} /></Field><Field label="Expiry date"><Input type="date" value={valueForm.expiryDate ?? ''} onChange={e => setValueForm({ ...valueForm, expiryDate: e.target.value || undefined })} /></Field><Field label="Display order"><Input type="number" value={valueForm.displayOrder} onChange={e => setValueForm({ ...valueForm, displayOrder: Number(e.target.value) })} /></Field><Toggle label="Active" value={valueForm.isActive} change={isActive => setValueForm({ ...valueForm, isActive })} /></div><SaveFooter close={() => setDialog(null)} save={saveValue} saving={saving} disabled={!valueForm.code || !valueForm.name} label={editingValue ? 'Save changes' : 'Add value'} /></DialogContent></Dialog>
        <Dialog open={dialog === 'rule'} onOpenChange={open => !open && setDialog(null)}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>{editingRule ? 'Edit account rule' : 'Add account rule'}</DialogTitle><DialogDescription>Control whether {selected?.name ?? 'this dimension'} is used for an account on a transaction route.</DialogDescription></DialogHeader><div className="grid gap-4 py-2 sm:grid-cols-2"><div className="space-y-1.5 sm:col-span-2"><Label>Transaction route</Label><Select value={ruleForm.routeId ?? 'ManualJournalEntry'} onValueChange={v => setRuleForm({ ...ruleForm, routeId: v as typeof ruleForm.routeId })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{routes.map(r => <SelectItem key={r.routeId} value={r.routeId}>{friendlyRoute(r.sourceRoute)} · {r.state}</SelectItem>)}</SelectContent></Select><p className="text-xs text-muted-foreground">The business transaction where this rule is enforced.</p></div><div className="space-y-1.5 sm:col-span-2"><Label>GL account</Label><Select value={ruleForm.accountId} onValueChange={accountId => setRuleForm({ ...ruleForm, accountId })}><SelectTrigger><SelectValue placeholder="Select account" /></SelectTrigger><SelectContent>{accounts.map(a => <SelectItem key={a.id} value={a.id}>{a.accountNumber} — {a.accountName}</SelectItem>)}</SelectContent></Select></div><Field label="Rule"><Select value={ruleForm.ruleType} onValueChange={v => setRuleForm({ ...ruleForm, ruleType: v as typeof ruleForm.ruleType, defaultDimensionValueId: v === 'Prohibited' ? undefined : ruleForm.defaultDimensionValueId })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{Object.keys(ruleHelp).map(v => <SelectItem key={v} value={v}>{v}</SelectItem>)}</SelectContent></Select><p className="text-xs text-muted-foreground">{ruleHelp[ruleForm.ruleType]}</p></Field>{ruleForm.ruleType !== 'Prohibited' && <Field label={ruleForm.ruleType === 'Fixed' ? 'Fixed value' : 'Default value (optional)'}><Select value={ruleForm.defaultDimensionValueId ?? '__none__'} onValueChange={v => setRuleForm({ ...ruleForm, defaultDimensionValueId: v === '__none__' ? undefined : v })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="__none__">No default</SelectItem>{selected?.values.filter(v => v.isActive).map(v => <SelectItem key={v.id} value={v.id}>{v.code} — {v.name}</SelectItem>)}</SelectContent></Select></Field>}<Field label="Effective date"><Input type="date" value={ruleForm.effectiveDate} onChange={e => setRuleForm({ ...ruleForm, effectiveDate: e.target.value })} /></Field><Toggle label="Active" value={ruleForm.isActive} change={isActive => setRuleForm({ ...ruleForm, isActive })} /></div><SaveFooter close={() => setDialog(null)} save={saveRule} saving={saving} disabled={!ruleForm.accountId || (ruleForm.ruleType === 'Fixed' && !ruleForm.defaultDimensionValueId)} label={editingRule ? 'Save changes' : 'Create rule'} /></DialogContent></Dialog>
    </div>;
}

function SearchBox({ value, onChange, placeholder }: { value: string; onChange: (value: string) => void; placeholder: string }) { return <div className="relative pt-2"><Search className="absolute left-3 top-5 h-4 w-4 text-muted-foreground" /><Input className="pl-9" value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder} /></div>; }
function Toolbar({ search, setSearch, placeholder, action }: { search: string; setSearch: (value: string) => void; placeholder: string; action?: React.ReactNode }) { return <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center"><div className="relative max-w-sm flex-1"><Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" /><Input className="pl-9" value={search} onChange={e => setSearch(e.target.value)} placeholder={placeholder} /></div>{action}</div>; }
function EmptyRow({ columns, text }: { columns: number; text: string }) { return <TableRow><TableCell colSpan={columns} className="h-28 text-center text-muted-foreground">{text}</TableCell></TableRow>; }
function Metric({ label, value, detail }: { label: string; value: number; detail: string }) { return <Card><CardHeader className="pb-2"><CardDescription>{label}</CardDescription><CardTitle className="text-3xl">{value}</CardTitle></CardHeader><CardContent className="text-sm text-muted-foreground">{detail}</CardContent></Card>; }
function Setting({ label, value }: { label: string; value: string }) { return <div><dt className="text-sm text-muted-foreground">{label}</dt><dd className="font-medium">{value}</dd></div>; }
function Field({ label, children }: { label: string; children: React.ReactNode }) { return <div className="space-y-1.5"><Label>{label}</Label>{children}</div>; }
function Toggle({ label, value, change }: { label: string; value: boolean; change: (value: boolean) => void }) { return <div className="flex items-center gap-2 pt-6"><Switch checked={value} onCheckedChange={change} /><Label>{label}</Label></div>; }
function SaveFooter({ close, save, saving, disabled, label }: { close: () => void; save: () => Promise<void>; saving: boolean; disabled: boolean; label: string }) { return <DialogFooter><Button variant="outline" onClick={close}>Cancel</Button><Button onClick={() => void save()} disabled={saving || disabled}>{saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}{label}</Button></DialogFooter>; }
