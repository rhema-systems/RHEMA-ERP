'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { BadgeCheck, CircleDollarSign, RefreshCw, Send, ShieldX } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringMaintenanceCostingHandoffService } from '@/services/civil-engineering-maintenance-costing-handoff.service';
import type { CivilEngineeringMaintenanceCostingHandoff, CivilEngineeringMaintenanceCostingHandoffLookups } from '@/types/civil-engineering-maintenance-costing-handoff';

const readPermission = 'civil-engineering.workspace.read';
const managePermission = 'civil-engineering.maintenance.manage';
const none = '__none__';
const blank = () => ({ assessmentId: '', projectId: '', estimateId: '', projectBudgetId: none, requisitionId: none, contractId: none });
const date = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Not yet checked';
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || fallback}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

export default function CivilEngineeringMaintenanceCostingHandoffsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission(readPermission);
  const canManage = hasPermission(managePermission);
  const [lookups, setLookups] = useState<CivilEngineeringMaintenanceCostingHandoffLookups>();
  const [handoffs, setHandoffs] = useState<CivilEngineeringMaintenanceCostingHandoff[]>([]);
  const [form, setForm] = useState(blank);
  const [reason, setReason] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!canRead && !canManage) { setLoading(false); return; }
    setLoading(true);
    const [handoffResult, lookupResult] = await Promise.allSettled([
      canRead ? civilEngineeringMaintenanceCostingHandoffService.list() : Promise.resolve([]),
      canManage ? civilEngineeringMaintenanceCostingHandoffService.lookups() : Promise.resolve(undefined),
    ]);
    if (handoffResult.status === 'fulfilled') setHandoffs(handoffResult.value); else toast({ variant: 'destructive', title: 'Costing handoffs could not be loaded', description: errorText(handoffResult.reason, 'Refresh and try again.') });
    if (lookupResult.status === 'fulfilled') setLookups(lookupResult.value); else setLookups(undefined);
    setLoading(false);
  }, [canManage, canRead, toast]);

  useEffect(() => { void load(); }, [load]);
  const assessment = useMemo(() => lookups?.approvedAssessments.find((item) => item.id === form.assessmentId), [form.assessmentId, lookups]);
  const sameProject = <T extends { projectId?: string | null }>(items: T[]) => items.filter((item) => item.projectId === form.projectId);
  const estimates = sameProject(lookups?.approvedEstimates || []);
  const budgets = sameProject(lookups?.approvedProjectBudgets || []);
  const requisitions = sameProject(lookups?.purchaseRequisitions || []);
  const contracts = sameProject(lookups?.contracts || []);
  const update = (patch: Partial<ReturnType<typeof blank>>) => setForm((current) => ({ ...current, ...patch }));

  const create = async () => {
    if (!form.assessmentId || !form.projectId || !form.estimateId) { toast({ variant: 'destructive', title: 'Complete the controlled links', description: 'Select the approved Civil scope, delivery project and approved QS estimate.' }); return; }
    setSaving(true);
    try {
      await civilEngineeringMaintenanceCostingHandoffService.create({ clientRequestId: crypto.randomUUID(), assessmentId: form.assessmentId, projectId: form.projectId, quantitySurveyEstimateVersionId: form.estimateId, projectBudgetRevisionId: form.projectBudgetId === none ? null : form.projectBudgetId, purchaseRequisitionId: form.requisitionId === none ? null : form.requisitionId, contractId: form.contractId === none ? null : form.contractId });
      toast({ title: 'Costing handoff created', description: 'The Civil record links existing QS, budget and Procurement records without copying them.' }); setForm(blank()); await load();
    } catch (error) { toast({ variant: 'destructive', title: 'Costing handoff was not created', description: errorText(error, 'Check the selected project and controlled owner records.') }); } finally { setSaving(false); }
  };

  const act = async (item: CivilEngineeringMaintenanceCostingHandoff, action: 'SubmitCosting' | 'ApproveCosting' | 'RejectCosting' | 'RefreshAuthoritativeStatus') => {
    const note = reason[item.id]?.trim() || null;
    if (action === 'RejectCosting' && (!note || note.length < 5)) { toast({ variant: 'destructive', title: 'Reason required', description: 'Record a clear rejection reason before rejecting the costing handoff.' }); return; }
    setSaving(true);
    try {
      await civilEngineeringMaintenanceCostingHandoffService.act(item.id, { clientRequestId: crypto.randomUUID(), action, reason: note, rowVersion: item.rowVersion });
      toast({ title: action === 'RefreshAuthoritativeStatus' ? 'Authoritative status refreshed' : 'Costing handoff updated', description: action === 'RefreshAuthoritativeStatus' ? 'Live QS, budget, Procurement and contract states were revalidated.' : 'The shared workflow and immutable audit history were updated.' }); setReason((current) => ({ ...current, [item.id]: '' })); await load();
    } catch (error) { toast({ variant: 'destructive', title: 'Costing handoff was not updated', description: errorText(error, 'Check the assigned workflow step and current owner-record status.') }); } finally { setSaving(false); }
  };

  if (!canRead && !canManage) return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil maintenance access required</AlertTitle><AlertDescription>You do not have access to Civil maintenance costing handoffs.</AlertDescription></Alert>;
  return <div className="space-y-5"><div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h1 className="flex items-center gap-2 text-2xl font-semibold"><CircleDollarSign className="h-6 w-6" />Maintenance costing handoffs</h1><p className="mt-1 text-sm text-muted-foreground">Route approved Civil remediation scopes through the controlled QS, budget and Procurement records.</p></div><Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>{canManage && lookups ? <Card><CardHeader><CardTitle className="text-base">Create controlled handoff</CardTitle><CardDescription>Only existing authorized records can be linked. The source modules remain the authoritative owners of estimates, budgets, requisitions and contracts.</CardDescription></CardHeader><CardContent className="grid gap-3 md:grid-cols-3"><div className="space-y-1"><Label>Approved Civil scope</Label><Select value={form.assessmentId} onValueChange={(assessmentId) => { const selected = lookups.approvedAssessments.find((item) => item.id === assessmentId); update({ assessmentId, projectId: selected?.projectId || '', estimateId: '', projectBudgetId: none, requisitionId: none, contractId: none }); }}><SelectTrigger><SelectValue placeholder="Select scope" /></SelectTrigger><SelectContent>{lookups.approvedAssessments.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Delivery project</Label><Select value={form.projectId} onValueChange={(projectId) => update({ projectId, estimateId: '', projectBudgetId: none, requisitionId: none, contractId: none })} disabled={Boolean(assessment?.projectId)}><SelectTrigger><SelectValue placeholder="Select project" /></SelectTrigger><SelectContent>{lookups.projects.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Approved QS estimate / cost plan</Label><Select value={form.estimateId} onValueChange={(estimateId) => update({ estimateId })} disabled={!form.projectId}><SelectTrigger><SelectValue placeholder="Select approved estimate" /></SelectTrigger><SelectContent>{estimates.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Approved project budget</Label><Select value={form.projectBudgetId} onValueChange={(projectBudgetId) => update({ projectBudgetId })} disabled={!form.projectId}><SelectTrigger><SelectValue placeholder="Select if applicable" /></SelectTrigger><SelectContent><SelectItem value={none}>Not linked</SelectItem>{budgets.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Purchase requisition</Label><Select value={form.requisitionId} onValueChange={(requisitionId) => update({ requisitionId })} disabled={!form.projectId}><SelectTrigger><SelectValue placeholder="Select if applicable" /></SelectTrigger><SelectContent><SelectItem value={none}>Not linked</SelectItem>{requisitions.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Project contract</Label><Select value={form.contractId} onValueChange={(contractId) => update({ contractId })} disabled={!form.projectId}><SelectTrigger><SelectValue placeholder="Select if available" /></SelectTrigger><SelectContent><SelectItem value={none}>Not linked</SelectItem>{contracts.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><div><Button disabled={saving} onClick={() => void create()}><BadgeCheck className="mr-2 h-4 w-4" />Create handoff</Button></div></CardContent></Card> : null}<Card><CardHeader><CardTitle className="text-base">Current handoffs</CardTitle><CardDescription>Costing approval runs through shared Workflow. Award status is returned only after live owner records revalidate.</CardDescription></CardHeader><CardContent className="space-y-4">{loading ? <p className="text-sm text-muted-foreground">Loading controlled handoffs…</p> : null}{!loading && !handoffs.length ? <p className="rounded border border-dashed p-3 text-sm text-muted-foreground">No approved Civil scopes have been handed off for costing.</p> : null}{handoffs.map((item) => <article key={item.id} className="rounded-lg border p-4"><div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between"><div><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{item.intakeNumber}</span><Badge variant="outline">{item.stage}</Badge><Badge variant="secondary">{item.approvalStatus}</Badge></div><p className="mt-2 text-sm text-muted-foreground">{item.projectLabel} · {item.estimateLabel} · {item.currencyCode} {item.estimateAmount.toLocaleString()}</p><p className="mt-1 text-sm text-muted-foreground">Budget: {item.projectBudgetLabel || 'Not linked'} · Requisition: {item.purchaseRequisitionLabel || 'Not linked'} · Contract: {item.contractLabel || 'Not linked'}</p>{item.lastRevalidationSummary ? <p className="mt-2 text-sm">Current owner status: {item.lastRevalidationSummary} <span className="text-muted-foreground">({date(item.lastRevalidatedAt)})</span></p> : null}{item.rejectionReason ? <p className="mt-2 text-sm text-destructive">{item.rejectionReason}</p> : null}</div>{canManage ? <div className="flex flex-wrap gap-2">{item.stage === 'Draft' ? <Button disabled={saving} onClick={() => void act(item, 'SubmitCosting')}><Send className="mr-2 h-4 w-4" />Submit costing</Button> : null}{item.stage === 'CostingReview' ? <><Button variant="outline" disabled={saving} onClick={() => void act(item, 'RejectCosting')}>Reject</Button><Button disabled={saving} onClick={() => void act(item, 'ApproveCosting')}>Approve costing</Button></> : null}{item.stage === 'ProcurementAndAward' ? <Button disabled={saving} onClick={() => void act(item, 'RefreshAuthoritativeStatus')}><RefreshCw className="mr-2 h-4 w-4" />Refresh award status</Button> : null}</div> : null}</div>{canManage && item.stage === 'CostingReview' ? <div className="mt-3 space-y-1"><Label>Decision reason</Label><Textarea rows={2} maxLength={2000} value={reason[item.id] || ''} onChange={(event) => setReason((current) => ({ ...current, [item.id]: event.target.value }))} placeholder="Required for rejection; retained with the workflow decision." /></div> : null}</article>)}</CardContent></Card></div>;
}
