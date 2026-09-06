'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import axios from 'axios';
import { Pencil, Plus, RefreshCw, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  inventoryRequisitionService,
  InventoryIssueAccountingOptionsDto,
  InventoryIssueAccountingRuleDto,
  InventoryIssueAccountingRuleRequest,
} from '@/services/inventoryRequisitionService';

const itemTypes: Record<number, string> = {
  1: 'Stock item',
  2: 'Service',
  3: 'Non-stock item',
  4: 'Fixed asset',
};

const blankForm = (): InventoryIssueAccountingRuleRequest => ({
  inventoryCategoryId: '',
  itemType: 1,
  movementReasonCode: '',
  treatment: 1,
  isActive: true,
  effectiveFromUtc: new Date().toISOString(),
});

const toDateInput = (value?: string) => value ? new Date(value).toISOString().slice(0, 10) : '';
const fromDateInput = (value: string) => new Date(`${value}T00:00:00.000Z`).toISOString();

function errorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    return error.response?.data?.detail ?? error.response?.data?.title ?? error.message;
  }
  return error instanceof Error ? error.message : 'The request could not be completed.';
}

export default function InventoryIssueAccountingPage() {
  const [rules, setRules] = useState<InventoryIssueAccountingRuleDto[]>([]);
  const [options, setOptions] = useState<InventoryIssueAccountingOptionsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [editing, setEditing] = useState<InventoryIssueAccountingRuleDto | null>(null);
  const [form, setForm] = useState<InventoryIssueAccountingRuleRequest>(blankForm());
  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleting, setDeleting] = useState<InventoryIssueAccountingRuleDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const [ruleRows, optionRows] = await Promise.all([
        inventoryRequisitionService.getIssueAccountingRules(),
        inventoryRequisitionService.getIssueAccountingOptions(),
      ]);
      setRules(ruleRows);
      setOptions(optionRows);
    } catch (error) {
      setRules([]);
      setOptions(null);
      setLoadError(errorMessage(error));
      toast.error(errorMessage(error));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const availableReasons = useMemo(() => {
    if (!options) return [];
    return Object.entries(options.movementReasons).filter(([code]) =>
      form.itemType === 4 ? code === 'ASSET_CUSTODY' : code !== 'ASSET_CUSTODY');
  }, [form.itemType, options]);

  const openCreate = () => {
    setEditing(null);
    setForm(blankForm());
    setDialogOpen(true);
  };

  const openEdit = (rule: InventoryIssueAccountingRuleDto) => {
    setEditing(rule);
    setForm({
      inventoryCategoryId: rule.inventoryCategoryId,
      itemType: rule.itemType,
      movementReasonCode: rule.movementReasonCode,
      treatment: rule.treatment,
      expenseAccountId: rule.expenseAccountId,
      fixedAssetCategoryId: rule.fixedAssetCategoryId,
      isActive: rule.isActive,
      effectiveFromUtc: rule.effectiveFromUtc,
      effectiveToUtc: rule.effectiveToUtc,
      rowVersion: rule.rowVersion,
    });
    setDialogOpen(true);
  };

  const changeItemType = (itemType: number) => setForm(current => ({
    ...current,
    itemType,
    treatment: itemType === 4 ? 2 : 1,
    movementReasonCode: itemType === 4 ? 'ASSET_CUSTODY' : current.movementReasonCode === 'ASSET_CUSTODY' ? '' : current.movementReasonCode,
    expenseAccountId: itemType === 4 ? undefined : current.expenseAccountId,
    fixedAssetCategoryId: itemType === 4 ? current.fixedAssetCategoryId : undefined,
  }));

  const canSave = Boolean(
    form.inventoryCategoryId && form.movementReasonCode && form.effectiveFromUtc &&
    (form.treatment === 1 ? form.expenseAccountId : form.fixedAssetCategoryId));

  const save = async () => {
    if (!canSave) return;
    setSaving(true);
    try {
      const request = {
        ...form,
        expenseAccountId: form.treatment === 1 ? form.expenseAccountId : undefined,
        fixedAssetCategoryId: form.treatment === 2 ? form.fixedAssetCategoryId : undefined,
      };
      if (editing) await inventoryRequisitionService.updateIssueAccountingRule(editing.id, request);
      else await inventoryRequisitionService.createIssueAccountingRule(request);
      toast.success(editing ? 'Issue-accounting rule updated.' : 'Issue-accounting rule created.');
      setDialogOpen(false);
      await load();
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (rule: InventoryIssueAccountingRuleDto) => {
    try {
      await inventoryRequisitionService.deleteIssueAccountingRule(rule.id, rule.rowVersion);
      toast.success('Issue-accounting rule deleted.');
      await load();
    } catch (error) {
      toast.error(errorMessage(error));
      return false;
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Issue accounting & asset custody</h1>
          <p className="text-sm text-muted-foreground">Map controlled inventory issues to Finance expense accounts or Fixed Assets categories.</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => void load()} disabled={loading}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
          <Button onClick={openCreate} disabled={loading || !options}><Plus className="mr-2 h-4 w-4" />Add rule</Button>
        </div>
      </div>

      {loadError && <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{loadError}</div>}
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">Effective mappings</CardTitle></CardHeader>
        <CardContent className="overflow-x-auto">
          <Table>
            <TableHeader><TableRow><TableHead>Inventory category</TableHead><TableHead>Item type</TableHead><TableHead>Movement reason</TableHead><TableHead>Posting owner</TableHead><TableHead>Effective</TableHead><TableHead>Status</TableHead><TableHead className="text-right">Actions</TableHead></TableRow></TableHeader>
            <TableBody>
              {rules.map(rule => (
                <TableRow key={rule.id}>
                  <TableCell><div className="font-medium">{rule.inventoryCategoryCode}</div><div className="text-xs text-muted-foreground">{rule.inventoryCategoryName}</div></TableCell>
                  <TableCell>{itemTypes[rule.itemType] ?? rule.itemType}</TableCell>
                  <TableCell>{rule.movementReasonName}</TableCell>
                  <TableCell>{rule.treatment === 1 ? rule.expenseAccount : rule.fixedAssetCategory}</TableCell>
                  <TableCell className="whitespace-nowrap">{new Date(rule.effectiveFromUtc).toLocaleDateString()} — {rule.effectiveToUtc ? new Date(rule.effectiveToUtc).toLocaleDateString() : 'Open'}</TableCell>
                  <TableCell><Badge variant={rule.isActive ? 'default' : 'secondary'}>{rule.isActive ? 'Active' : 'Inactive'}</Badge></TableCell>
                  <TableCell><div className="flex justify-end gap-1"><Button size="icon" variant="ghost" aria-label="Edit rule" onClick={() => openEdit(rule)}><Pencil className="h-4 w-4" /></Button><Button size="icon" variant="ghost" aria-label="Delete rule" onClick={() => setDeleting(rule)}><Trash2 className="h-4 w-4 text-destructive" /></Button></div></TableCell>
                </TableRow>
              ))}
              {!loading && !loadError && rules.length === 0 && <TableRow><TableCell colSpan={7} className="py-8 text-center text-muted-foreground">No issue-accounting rules are configured.</TableCell></TableRow>}
              {loading && <TableRow><TableCell colSpan={7} className="py-8 text-center text-muted-foreground">Loading rules…</TableCell></TableRow>}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <ConfirmationDialog open={Boolean(deleting)} onOpenChange={open => { if (!open) setDeleting(null); }}
        title="Delete issue-accounting rule?" variant="destructive" confirmText="Delete rule"
        description={deleting ? `Delete the ${deleting.inventoryCategoryCode} / ${deleting.movementReasonName} rule? Future issues may be blocked without a replacement mapping.` : ''}
        onConfirm={() => deleting ? remove(deleting) : false} />
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader><DialogTitle>{editing ? 'Edit' : 'Add'} issue-accounting rule</DialogTitle></DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1"><Label>Inventory category</Label><Select value={form.inventoryCategoryId || 'none'} onValueChange={value => setForm(current => ({ ...current, inventoryCategoryId: value === 'none' ? '' : value }))}><SelectTrigger><SelectValue placeholder="Select category" /></SelectTrigger><SelectContent><SelectItem value="none">Select category</SelectItem>{options?.inventoryCategories.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-1"><Label>Item type</Label><Select value={String(form.itemType)} onValueChange={value => changeItemType(Number(value))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{Object.entries(itemTypes).map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-1"><Label>Movement reason</Label><Select value={form.movementReasonCode || 'none'} onValueChange={value => setForm(current => ({ ...current, movementReasonCode: value === 'none' ? '' : value }))}><SelectTrigger><SelectValue placeholder="Select reason" /></SelectTrigger><SelectContent><SelectItem value="none">Select reason</SelectItem>{availableReasons.map(([code, label]) => <SelectItem key={code} value={code}>{label}</SelectItem>)}</SelectContent></Select></div>
            {form.treatment === 1 ? (
              <div className="space-y-1"><Label>Finance expense account</Label><Select value={form.expenseAccountId || 'none'} onValueChange={value => setForm(current => ({ ...current, expenseAccountId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select expense account" /></SelectTrigger><SelectContent><SelectItem value="none">Select expense account</SelectItem>{options?.expenseAccounts.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>)}</SelectContent></Select></div>
            ) : (
              <div className="space-y-1"><Label>Fixed Assets category</Label><Select value={form.fixedAssetCategoryId || 'none'} onValueChange={value => setForm(current => ({ ...current, fixedAssetCategoryId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select asset category" /></SelectTrigger><SelectContent><SelectItem value="none">Select asset category</SelectItem>{options?.fixedAssetCategories.map(value => <SelectItem key={value.id} value={value.id}>{value.code} · {value.name}</SelectItem>)}</SelectContent></Select></div>
            )}
            <div className="space-y-1"><Label>Effective from</Label><Input type="date" value={toDateInput(form.effectiveFromUtc)} onChange={event => setForm(current => ({ ...current, effectiveFromUtc: fromDateInput(event.target.value) }))} /></div>
            <div className="space-y-1"><Label>Effective to</Label><Input type="date" value={toDateInput(form.effectiveToUtc)} onChange={event => setForm(current => ({ ...current, effectiveToUtc: event.target.value ? fromDateInput(event.target.value) : undefined }))} /></div>
            <label className="flex items-center gap-3 sm:col-span-2"><Switch checked={form.isActive} onCheckedChange={value => setForm(current => ({ ...current, isActive: value }))} /><span className="text-sm">Active for issue posting</span></label>
          </div>
          <DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)}>Cancel</Button><Button onClick={() => void save()} disabled={!canSave || saving}>{saving ? 'Saving…' : 'Save rule'}</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
