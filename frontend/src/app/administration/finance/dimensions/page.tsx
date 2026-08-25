'use client';

import { useEffect, useMemo, useState } from 'react';
import { AlertCircle, Loader2, Plus, RefreshCw } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import type {
    Account,
    FinanceDimensionAccountRule,
    FinanceDimensionDefinition,
    UpsertFinanceDimensionAccountRule,
    UpsertFinanceDimensionDefinition,
    UpsertFinanceDimensionValue,
} from '@/types/finance';

const today = () => new Date().toISOString().slice(0, 10);

const emptyDefinition = (): UpsertFinanceDimensionDefinition => ({
    code: '', name: '', description: '', classification: 'Analytical', valueSourceType: 'Lookup',
    isActive: true, displayOrder: 0,
});

const emptyValue = (): UpsertFinanceDimensionValue => ({
    code: '', name: '', effectiveDate: today(), isActive: true, displayOrder: 0,
});

const emptyRule = (): UpsertFinanceDimensionAccountRule => ({
    accountId: '', financeDimensionDefinitionId: '', ruleType: 'Optional',
    sourceModule: 'GL', sourceDocumentType: 'ManualJournalEntry', postingAction: 'Post',
    effectiveDate: today(), isActive: true,
});

export default function FinanceDimensionsPage() {
    const { toast } = useToast();
    const [definitions, setDefinitions] = useState<FinanceDimensionDefinition[]>([]);
    const [rules, setRules] = useState<FinanceDimensionAccountRule[]>([]);
    const [accounts, setAccounts] = useState<Account[]>([]);
    const [selectedDefinitionId, setSelectedDefinitionId] = useState('');
    const [definitionForm, setDefinitionForm] = useState(emptyDefinition());
    const [valueForm, setValueForm] = useState(emptyValue());
    const [ruleForm, setRuleForm] = useState(emptyRule());
    const [editingDefinitionId, setEditingDefinitionId] = useState<string | null>(null);
    const [editingValueId, setEditingValueId] = useState<string | null>(null);
    const [editingRuleId, setEditingRuleId] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const selectedDefinition = useMemo(
        () => definitions.find(item => item.id === selectedDefinitionId),
        [definitions, selectedDefinitionId],
    );

    const load = async () => {
        setLoading(true);
        setError(null);
        try {
            const [dimensionRows, ruleRows, accountRows] = await Promise.all([
                financeDataService.getFinanceDimensions(true),
                financeDataService.getFinanceDimensionRules(),
                financeDataService.getAccounts({ status: 'Active' }),
            ]);
            setDefinitions(dimensionRows);
            setRules(ruleRows);
            setAccounts(accountRows.filter(account => account.allowDirectPosting ?? true));
            if (!selectedDefinitionId && dimensionRows.length) setSelectedDefinitionId(dimensionRows[0].id);
        } catch (cause) {
            setError(cause instanceof Error ? cause.message : 'Failed to load Finance coding dimensions.');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { void load(); }, []);

    const saveDefinition = async () => {
        setSaving(true);
        try {
            const result = editingDefinitionId
                ? await financeDataService.updateFinanceDimension(editingDefinitionId, definitionForm)
                : await financeDataService.createFinanceDimension(definitionForm);
            setDefinitionForm(emptyDefinition());
            setEditingDefinitionId(null);
            setSelectedDefinitionId(result.id);
            await load();
            toast({ title: editingDefinitionId ? 'Dimension updated' : 'Dimension created', description: `${result.code} is available to certified Finance producers.` });
        } catch (cause) {
            toast({ title: 'Dimension not saved', description: cause instanceof Error ? cause.message : 'Request failed.', variant: 'destructive' });
        } finally { setSaving(false); }
    };

    const saveValue = async () => {
        if (!selectedDefinitionId) return;
        setSaving(true);
        try {
            if (editingValueId) {
                await financeDataService.updateFinanceDimensionValue(selectedDefinitionId, editingValueId, valueForm);
            } else {
                await financeDataService.createFinanceDimensionValue(selectedDefinitionId, valueForm);
            }
            setValueForm(emptyValue());
            setEditingValueId(null);
            await load();
            toast({ title: 'Value created' });
        } catch (cause) {
            toast({ title: 'Value not saved', description: cause instanceof Error ? cause.message : 'Request failed.', variant: 'destructive' });
        } finally { setSaving(false); }
    };

    const saveRule = async () => {
        setSaving(true);
        try {
            if (editingRuleId) {
                await financeDataService.updateFinanceDimensionRule(editingRuleId, ruleForm);
            } else {
                await financeDataService.createFinanceDimensionRule(ruleForm);
            }
            setRuleForm(emptyRule());
            setEditingRuleId(null);
            await load();
            toast({ title: 'Manual-journal rule created' });
        } catch (cause) {
            toast({ title: 'Rule not saved', description: cause instanceof Error ? cause.message : 'Request failed.', variant: 'destructive' });
        } finally { setSaving(false); }
    };

    return (
        <div className="container mx-auto space-y-6 p-6">
            <div className="flex items-start justify-between gap-4">
                <div>
                    <h1 className="text-3xl font-bold">Finance Coding Dimensions</h1>
                    <p className="text-muted-foreground">Configure transaction classifications independently of the natural GL account.</p>
                </div>
                <Button variant="outline" onClick={() => void load()} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
            </div>

            <Alert>
                <AlertCircle className="h-4 w-4" />
                <AlertTitle>Controlled rollout</AlertTitle>
                <AlertDescription>
                    Manual journals are the first certified producer. Required, fixed, and prohibited rules must remain scoped to
                    GL / ManualJournalEntry / Post until each operational adapter has its own tested contract.
                </AlertDescription>
            </Alert>

            {error && <Alert variant="destructive"><AlertTitle>Unable to load</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
            {loading ? <div className="flex items-center gap-2"><Loader2 className="h-4 w-4 animate-spin" />Loading dimensions…</div> : (
                <div className="grid gap-6 xl:grid-cols-3">
                    <Card>
                        <CardHeader><CardTitle>Definitions</CardTitle><CardDescription>{definitions.length} configured</CardDescription></CardHeader>
                        <CardContent className="space-y-4">
                            <div className="space-y-1"><Label>Code</Label><Input value={definitionForm.code} onChange={event => setDefinitionForm({ ...definitionForm, code: event.target.value.toUpperCase() })} /></div>
                            <div className="space-y-1"><Label>Name</Label><Input value={definitionForm.name} onChange={event => setDefinitionForm({ ...definitionForm, name: event.target.value })} /></div>
                            <div className="space-y-1"><Label>Description</Label><Textarea value={definitionForm.description} onChange={event => setDefinitionForm({ ...definitionForm, description: event.target.value })} /></div>
                            <div className="grid grid-cols-2 gap-3">
                                <div className="space-y-1"><Label>Classification</Label><Select value={definitionForm.classification} onValueChange={value => setDefinitionForm({ ...definitionForm, classification: value as UpsertFinanceDimensionDefinition['classification'] })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Analytical">Analytical</SelectItem><SelectItem value="Balancing">Balancing</SelectItem><SelectItem value="Derived">Derived</SelectItem></SelectContent></Select></div>
                                <div className="space-y-1"><Label>Source</Label><Select value={definitionForm.valueSourceType} onValueChange={value => setDefinitionForm({ ...definitionForm, valueSourceType: value as UpsertFinanceDimensionDefinition['valueSourceType'] })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Lookup">Lookup</SelectItem><SelectItem value="EntityBacked">Entity-backed</SelectItem></SelectContent></Select></div>
                            </div>
                            {definitionForm.valueSourceType === 'EntityBacked' && <div className="space-y-1"><Label>Source entity type</Label><Input value={definitionForm.sourceEntityType ?? ''} onChange={event => setDefinitionForm({ ...definitionForm, sourceEntityType: event.target.value })} /></div>}
                            <div className="flex items-center gap-2"><Switch checked={definitionForm.isActive} onCheckedChange={checked => setDefinitionForm({ ...definitionForm, isActive: checked })} /><Label>Active</Label></div>
                            <div className="flex gap-2"><Button onClick={() => void saveDefinition()} disabled={saving || !definitionForm.code || !definitionForm.name}><Plus className="mr-2 h-4 w-4" />{editingDefinitionId ? 'Save definition' : 'Create definition'}</Button>{editingDefinitionId && <Button variant="outline" onClick={() => { setEditingDefinitionId(null); setDefinitionForm(emptyDefinition()); }}>Cancel</Button>}</div>
                            <div className="space-y-2 border-t pt-4">
                                {definitions.map(item => <button key={item.id} type="button" onClick={() => { setSelectedDefinitionId(item.id); setEditingDefinitionId(item.id); setDefinitionForm({ code: item.code, name: item.name, description: item.description, classification: item.classification, valueSourceType: item.valueSourceType, sourceEntityType: item.sourceEntityType, isActive: item.isActive, displayOrder: item.displayOrder }); }} className={`w-full rounded border p-3 text-left ${selectedDefinitionId === item.id ? 'border-primary bg-primary/5' : ''}`}><span className="font-mono font-semibold">{item.code}</span><span className="ml-2">{item.name}</span><div className="text-xs text-muted-foreground">{item.classification} · {item.valueSourceType} · {item.isActive ? 'Active' : 'Inactive'}</div></button>)}
                            </div>
                        </CardContent>
                    </Card>

                    <Card>
                        <CardHeader><CardTitle>Values</CardTitle><CardDescription>{selectedDefinition ? `${selectedDefinition.code} · ${selectedDefinition.values.length} values` : 'Select a definition'}</CardDescription></CardHeader>
                        <CardContent className="space-y-4">
                            {selectedDefinition?.valueSourceType === 'Lookup' ? <>
                                <div className="space-y-1"><Label>Value code</Label><Input value={valueForm.code} onChange={event => setValueForm({ ...valueForm, code: event.target.value.toUpperCase() })} /></div>
                                <div className="space-y-1"><Label>Value name</Label><Input value={valueForm.name} onChange={event => setValueForm({ ...valueForm, name: event.target.value })} /></div>
                                <div className="grid grid-cols-2 gap-3"><div className="space-y-1"><Label>Effective date</Label><Input type="date" value={valueForm.effectiveDate} onChange={event => setValueForm({ ...valueForm, effectiveDate: event.target.value })} /></div><div className="space-y-1"><Label>Expiry date</Label><Input type="date" value={valueForm.expiryDate ?? ''} onChange={event => setValueForm({ ...valueForm, expiryDate: event.target.value || undefined })} /></div></div>
                                <div className="flex items-center gap-2"><Switch checked={valueForm.isActive} onCheckedChange={checked => setValueForm({ ...valueForm, isActive: checked })} /><Label>Active</Label></div>
                                <div className="flex gap-2"><Button onClick={() => void saveValue()} disabled={saving || !selectedDefinitionId || !valueForm.code || !valueForm.name}><Plus className="mr-2 h-4 w-4" />{editingValueId ? 'Save value' : 'Create value'}</Button>{editingValueId && <Button variant="outline" onClick={() => { setEditingValueId(null); setValueForm(emptyValue()); }}>Cancel</Button>}</div>
                            </> : <p className="text-sm text-muted-foreground">Entity-backed values are provisioned by their certified owner adapter, not typed manually here.</p>}
                            <div className="space-y-2 border-t pt-4">{selectedDefinition?.values.map(value => <button type="button" key={value.id} className="w-full rounded border p-3 text-left" onClick={() => { setEditingValueId(value.id); setValueForm({ code: value.code, name: value.name, parentValueId: value.parentValueId, sourceEntityType: value.sourceEntityType, sourceEntityId: value.sourceEntityId, effectiveDate: value.effectiveDate.slice(0, 10), expiryDate: value.expiryDate?.slice(0, 10), isActive: value.isActive, displayOrder: value.displayOrder }); }}><span className="font-mono font-semibold">{value.code}</span><span className="ml-2">{value.name}</span><div className="text-xs text-muted-foreground">{value.isActive ? 'Active' : 'Inactive'} from {value.effectiveDate.slice(0, 10)}</div></button>)}</div>
                        </CardContent>
                    </Card>

                    <Card>
                        <CardHeader><CardTitle>Account rules</CardTitle><CardDescription>Certified manual-journal scope only</CardDescription></CardHeader>
                        <CardContent className="space-y-4">
                            <div className="space-y-1"><Label>Account</Label><Select value={ruleForm.accountId} onValueChange={accountId => setRuleForm({ ...ruleForm, accountId })}><SelectTrigger><SelectValue placeholder="Select account" /></SelectTrigger><SelectContent>{accounts.map(account => <SelectItem key={account.id} value={account.id}>{account.accountNumber} — {account.accountName}</SelectItem>)}</SelectContent></Select></div>
                            <div className="space-y-1"><Label>Dimension</Label><Select value={ruleForm.financeDimensionDefinitionId} onValueChange={financeDimensionDefinitionId => setRuleForm({ ...ruleForm, financeDimensionDefinitionId, defaultDimensionValueId: undefined })}><SelectTrigger><SelectValue placeholder="Select dimension" /></SelectTrigger><SelectContent>{definitions.filter(item => item.isActive).map(item => <SelectItem key={item.id} value={item.id}>{item.code} — {item.name}</SelectItem>)}</SelectContent></Select></div>
                            <div className="space-y-1"><Label>Rule</Label><Select value={ruleForm.ruleType} onValueChange={ruleType => setRuleForm({ ...ruleForm, ruleType: ruleType as UpsertFinanceDimensionAccountRule['ruleType'] })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Optional">Optional</SelectItem><SelectItem value="Required">Required</SelectItem><SelectItem value="Fixed">Fixed</SelectItem><SelectItem value="Prohibited">Prohibited</SelectItem></SelectContent></Select></div>
                            {(ruleForm.ruleType === 'Fixed' || ruleForm.ruleType === 'Required' || ruleForm.ruleType === 'Optional') && <div className="space-y-1"><Label>Default value (optional except Fixed)</Label><Select value={ruleForm.defaultDimensionValueId ?? '__none__'} onValueChange={value => setRuleForm({ ...ruleForm, defaultDimensionValueId: value === '__none__' ? undefined : value })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="__none__">No default</SelectItem>{definitions.find(item => item.id === ruleForm.financeDimensionDefinitionId)?.values.filter(value => value.isActive).map(value => <SelectItem key={value.id} value={value.id}>{value.code} — {value.name}</SelectItem>)}</SelectContent></Select></div>}
                            <div className="space-y-1"><Label>Effective date</Label><Input type="date" value={ruleForm.effectiveDate} onChange={event => setRuleForm({ ...ruleForm, effectiveDate: event.target.value })} /></div>
                            <div className="flex items-center gap-2"><Switch checked={ruleForm.isActive} onCheckedChange={checked => setRuleForm({ ...ruleForm, isActive: checked })} /><Label>Active</Label></div>
                            <div className="flex gap-2"><Button onClick={() => void saveRule()} disabled={saving || !ruleForm.accountId || !ruleForm.financeDimensionDefinitionId}><Plus className="mr-2 h-4 w-4" />{editingRuleId ? 'Save rule' : 'Create rule'}</Button>{editingRuleId && <Button variant="outline" onClick={() => { setEditingRuleId(null); setRuleForm(emptyRule()); }}>Cancel</Button>}</div>
                            <div className="space-y-2 border-t pt-4">{rules.map(rule => <button type="button" key={rule.id} className="w-full rounded border p-3 text-left" onClick={() => { setEditingRuleId(rule.id); setRuleForm({ accountId: rule.accountId, financeDimensionDefinitionId: rule.financeDimensionDefinitionId, ruleType: rule.ruleType, defaultDimensionValueId: rule.defaultDimensionValueId, sourceModule: rule.sourceModule, sourceDocumentType: rule.sourceDocumentType, postingAction: rule.postingAction, effectiveDate: rule.effectiveDate.slice(0, 10), expiryDate: rule.expiryDate?.slice(0, 10), isActive: rule.isActive }); }}><div className="font-medium">{rule.accountNumber} · {rule.dimensionCode}</div><div className="text-sm">{rule.ruleType}{rule.defaultValueCode ? ` = ${rule.defaultValueCode}` : ''}</div><div className="text-xs text-muted-foreground">{rule.sourceModule ?? '*'} / {rule.sourceDocumentType ?? '*'} / {rule.postingAction ?? '*'}</div></button>)}</div>
                        </CardContent>
                    </Card>
                </div>
            )}
        </div>
    );
}
