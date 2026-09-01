'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { ClipboardCheck, Link2, RefreshCw, ShieldX, Wrench } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringMaintenanceExecutionLinkService } from '@/services/civil-engineering-maintenance-execution-link.service';
import type { CivilEngineeringMaintenanceExecutionLink, CivilEngineeringMaintenanceExecutionLookups } from '@/types/civil-engineering-maintenance-execution-link';

const readPermission = 'civil-engineering.workspace.read';
const managePermission = 'civil-engineering.maintenance.manage';
const none = '__none__';
const blank = () => ({ handoffId: '', linkMode: 'CreateJobCard' as const, maintenanceTypeId: '', priorityLevelId: '', jobCardId: none, workOrderId: none });
const date = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Not yet synchronized';
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || fallback}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

export default function CivilEngineeringMaintenanceExecutionLinksPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission(readPermission);
  const canManage = hasPermission(managePermission);
  const [lookups, setLookups] = useState<CivilEngineeringMaintenanceExecutionLookups>();
  const [items, setItems] = useState<CivilEngineeringMaintenanceExecutionLink[]>([]);
  const [form, setForm] = useState(blank);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!canRead && !canManage) { setLoading(false); return; }
    setLoading(true);
    const [records, options] = await Promise.allSettled([
      canRead ? civilEngineeringMaintenanceExecutionLinkService.list() : Promise.resolve([]),
      canManage ? civilEngineeringMaintenanceExecutionLinkService.lookups() : Promise.resolve(undefined),
    ]);
    if (records.status === 'fulfilled') setItems(records.value);
    else toast({ variant: 'destructive', title: 'Maintenance execution links could not be loaded', description: errorText(records.reason, 'Refresh and try again.') });
    setLookups(options.status === 'fulfilled' ? options.value : undefined);
    setLoading(false);
  }, [canManage, canRead, toast]);

  useEffect(() => { void load(); }, [load]);
  const handoff = useMemo(() => lookups?.awardedHandoffs.find((value) => value.id === form.handoffId), [form.handoffId, lookups]);
  const sameAsset = <T extends { maintenanceAssetId?: string | null }>(values: T[]) => values.filter((value) => value.maintenanceAssetId === handoff?.maintenanceAssetId);
  const jobCards = sameAsset(lookups?.jobCards || []);
  const workOrders = sameAsset(lookups?.workOrders || []);
  const set = (patch: Partial<ReturnType<typeof blank>>) => setForm((current) => ({ ...current, ...patch }));

  const create = async () => {
    if (!form.handoffId) { toast({ variant: 'destructive', title: 'Select an awarded Civil scope', description: 'Only an awarded asset-based handoff can enter the existing Maintenance lifecycle.' }); return; }
    if (form.linkMode === 'CreateJobCard' && (!form.maintenanceTypeId || !form.priorityLevelId)) { toast({ variant: 'destructive', title: 'Select Maintenance controls', description: 'Use the configured Maintenance type and priority selectors for the new job card.' }); return; }
    if (form.linkMode === 'LinkExisting' && form.jobCardId === none && form.workOrderId === none) { toast({ variant: 'destructive', title: 'Select a Maintenance record', description: 'Link an existing job card or work order for this controlled asset.' }); return; }
    setSaving(true);
    try {
      await civilEngineeringMaintenanceExecutionLinkService.create({
        clientRequestId: crypto.randomUUID(), handoffId: form.handoffId, linkMode: form.linkMode,
        maintenanceTypeId: form.linkMode === 'CreateJobCard' ? form.maintenanceTypeId : null,
        priorityLevelId: form.linkMode === 'CreateJobCard' ? form.priorityLevelId : null,
        jobCardId: form.linkMode === 'LinkExisting' && form.jobCardId !== none ? form.jobCardId : null,
        workOrderId: form.linkMode === 'LinkExisting' && form.workOrderId !== none ? form.workOrderId : null,
      });
      toast({ title: form.linkMode === 'CreateJobCard' ? 'Maintenance job card created' : 'Maintenance record linked', description: 'Civil keeps the approved scope lineage while Maintenance remains the lifecycle owner.' });
      setForm(blank()); await load();
    } catch (error) { toast({ variant: 'destructive', title: 'Execution link was not created', description: errorText(error, 'Check the awarded scope, project assignment and selected Maintenance records.') }); }
    finally { setSaving(false); }
  };

  const refresh = async (item: CivilEngineeringMaintenanceExecutionLink) => {
    setSaving(true);
    try {
      await civilEngineeringMaintenanceExecutionLinkService.refresh(item.id, { clientRequestId: crypto.randomUUID(), rowVersion: item.rowVersion });
      toast({ title: 'Maintenance status synchronized', description: 'The display now reflects the authoritative job-card and work-order lifecycle.' }); await load();
    } catch (error) { toast({ variant: 'destructive', title: 'Execution status was not synchronized', description: errorText(error, 'Refresh the record and check the linked Maintenance lifecycle.') }); }
    finally { setSaving(false); }
  };

  if (!canRead && !canManage) return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil maintenance access required</AlertTitle><AlertDescription>You do not have access to Civil Maintenance execution links.</AlertDescription></Alert>;

  return <div className="space-y-5">
    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h1 className="flex items-center gap-2 text-2xl font-semibold"><Wrench className="h-6 w-6" />Maintenance execution links</h1><p className="mt-1 text-sm text-muted-foreground">Connect awarded Civil scopes to the existing Maintenance job-card and work-order lifecycle.</p></div><Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>
    {canManage && lookups ? <Card><CardHeader><CardTitle className="text-base">Create or link Maintenance execution</CardTitle><CardDescription>A new job card uses controlled Maintenance selectors. Existing records must belong to the asset on the awarded Civil scope.</CardDescription></CardHeader><CardContent className="grid gap-3 md:grid-cols-3"><div className="space-y-1"><Label>Awarded Civil scope</Label><Select value={form.handoffId} onValueChange={(handoffId) => set({ handoffId, jobCardId: none, workOrderId: none })}><SelectTrigger><SelectValue placeholder="Select awarded scope" /></SelectTrigger><SelectContent>{lookups.awardedHandoffs.map((value) => <SelectItem key={value.id} value={value.id}>{value.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Maintenance action</Label><Select value={form.linkMode} onValueChange={(linkMode: 'CreateJobCard' | 'LinkExisting') => set({ linkMode, maintenanceTypeId: '', priorityLevelId: '', jobCardId: none, workOrderId: none })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="CreateJobCard">Create Maintenance job card</SelectItem><SelectItem value="LinkExisting">Link existing Maintenance record</SelectItem></SelectContent></Select></div>{form.linkMode === 'CreateJobCard' ? <><div className="space-y-1"><Label>Maintenance type</Label><Select value={form.maintenanceTypeId} onValueChange={(maintenanceTypeId) => set({ maintenanceTypeId })}><SelectTrigger><SelectValue placeholder="Select type" /></SelectTrigger><SelectContent>{lookups.maintenanceTypes.map((value) => <SelectItem key={value.id} value={value.id}>{value.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Maintenance priority</Label><Select value={form.priorityLevelId} onValueChange={(priorityLevelId) => set({ priorityLevelId })}><SelectTrigger><SelectValue placeholder="Select priority" /></SelectTrigger><SelectContent>{lookups.priorityLevels.map((value) => <SelectItem key={value.id} value={value.id}>{value.label}</SelectItem>)}</SelectContent></Select></div></> : <><div className="space-y-1"><Label>Existing job card</Label><Select value={form.jobCardId} onValueChange={(jobCardId) => set({ jobCardId })} disabled={!handoff}><SelectTrigger><SelectValue placeholder="Optional if work order selected" /></SelectTrigger><SelectContent><SelectItem value={none}>Not selected</SelectItem>{jobCards.map((value) => <SelectItem key={value.id} value={value.id}>{value.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Existing work order</Label><Select value={form.workOrderId} onValueChange={(workOrderId) => set({ workOrderId })} disabled={!handoff}><SelectTrigger><SelectValue placeholder="Optional if job card selected" /></SelectTrigger><SelectContent><SelectItem value={none}>Not selected</SelectItem>{workOrders.map((value) => <SelectItem key={value.id} value={value.id}>{value.label}</SelectItem>)}</SelectContent></Select></div></>}<div className="flex items-end"><Button disabled={saving || !handoff} onClick={() => void create()}><Link2 className="mr-2 h-4 w-4" />{form.linkMode === 'CreateJobCard' ? 'Create job card' : 'Link record'}</Button></div></CardContent></Card> : null}
    <Card><CardHeader><CardTitle className="text-base">Execution status</CardTitle><CardDescription>Maintenance status is shown from the authoritative job-card/work-order records; Civil supervision evidence stays attached to the approved scope.</CardDescription></CardHeader><CardContent className="space-y-3">{loading ? <p className="text-sm text-muted-foreground">Loading execution links…</p> : null}{!loading && !items.length ? <p className="rounded border border-dashed p-3 text-sm text-muted-foreground">No awarded Civil scope is linked to Maintenance execution.</p> : null}{items.map((item) => <article key={item.id} className="rounded-lg border p-4"><div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between"><div><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{item.intakeNumber}</span><Badge variant="outline">{item.stage}</Badge><Badge variant="secondary">{item.status}</Badge></div><p className="mt-2 text-sm text-muted-foreground">{item.projectLabel} · {item.maintenanceAssetLabel}</p><p className="mt-1 text-sm text-muted-foreground">Job card: {item.jobCardNumber || 'Not linked'}{item.jobCardStatus ? ` (${item.jobCardStatus})` : ''} · Work order: {item.workOrderNumber || 'Not generated'}{item.workOrderStatus ? ` (${item.workOrderStatus})` : ''}</p><p className="mt-2 text-sm">{item.lastOwnerStatusSummary || 'Owner lifecycle has not yet been synchronized.'} <span className="text-muted-foreground">({date(item.lastRevalidatedAt)})</span></p></div>{canManage ? <Button variant="outline" disabled={saving} onClick={() => void refresh(item)}><ClipboardCheck className="mr-2 h-4 w-4" />Synchronize</Button> : null}</div></article>)}</CardContent></Card>
  </div>;
}
