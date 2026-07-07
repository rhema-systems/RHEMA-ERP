'use client';

import * as React from 'react';
import { AlertTriangle, Archive, CalendarDays, FileCheck2, Plus, RefreshCw, UserRoundCog } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { workflowApiService } from '@/services/workflow-api.service';
import {
  WorkflowDelegationKind,
  type SaveWorkflowDelegationRequest,
  type WorkflowDelegationDto,
  type WorkflowDelegationScopeOptionsDto,
  type WorkflowSlaBreachesDto,
  type WorkflowDirectoryUser,
  type WorkflowWorkingCalendarDto,
} from '@/types/workflow';
import { WorkflowEvidenceGovernancePanel } from './WorkflowEvidenceGovernancePanel';
import { WorkflowReasonDialog } from './WorkflowReasonDialog';

const defaultDelegation = (): SaveWorkflowDelegationRequest => ({
  delegateUserId: '', kind: WorkflowDelegationKind.Authority, effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: '', reason: '', allowRedelegation: false,
});

const defaultScopeOptions: WorkflowDelegationScopeOptionsDto = {
  modules: [],
  entityTypes: [],
  workflowDefinitions: [],
};

type DelegationScopeLevel = 'all' | 'module' | 'entity' | 'workflow' | 'step';

const defaultCalendar = (): WorkflowWorkingCalendarDto => ({
  name: 'Default working calendar', timeZoneId: 'UTC', workingDaysMask: 31,
  workDayStart: '08:00:00', workDayEnd: '17:00:00', holidays: [],
});

export function WorkflowGovernancePanel() {
  const [delegations, setDelegations] = React.useState<WorkflowDelegationDto[]>([]);
  const [users, setUsers] = React.useState<WorkflowDirectoryUser[]>([]);
  const [scopeOptions, setScopeOptions] = React.useState<WorkflowDelegationScopeOptionsDto>(defaultScopeOptions);
  const [delegation, setDelegation] = React.useState(defaultDelegation());
  const [scopeLevel, setScopeLevel] = React.useState<DelegationScopeLevel>('all');
  const [calendar, setCalendar] = React.useState(defaultCalendar());
  const [holidays, setHolidays] = React.useState('');
  const [slaDays, setSlaDays] = React.useState(30);
  const [sla, setSla] = React.useState<WorkflowSlaBreachesDto | null>(null);
  const [dialogOpen, setDialogOpen] = React.useState(false);
  const [revokeTarget, setRevokeTarget] = React.useState<WorkflowDelegationDto | null>(null);
  const [saving, setSaving] = React.useState(false);
  const [loadingSla, setLoadingSla] = React.useState(false);

  const load = React.useCallback(async () => {
    try {
      const [delegationRows, directoryUsers, workingCalendar, delegationScopeOptions] = await Promise.all([
        workflowApiService.getWorkflowDelegations(),
        workflowApiService.getWorkflowDirectoryUsers(),
        workflowApiService.getWorkflowCalendar(),
        workflowApiService.getWorkflowDelegationScopeOptions(),
      ]);
      setDelegations(delegationRows);
      setUsers(directoryUsers);
      setScopeOptions(delegationScopeOptions);
      if (workingCalendar) {
        setCalendar(workingCalendar);
        setHolidays(workingCalendar.holidays.join(', '));
      }
    } catch (error: any) { toast.error(error?.message || 'Failed to load workflow governance settings'); }
  }, []);
  React.useEffect(() => { void load(); }, [load]);

  const loadSla = React.useCallback(async () => {
    try {
      setLoadingSla(true);
      setSla(await workflowApiService.getWorkflowSlaBreaches(slaDays));
    } catch (error: any) { toast.error(error?.message || 'Failed to load SLA breaches'); }
    finally { setLoadingSla(false); }
  }, [slaDays]);
  React.useEffect(() => { void loadSla(); }, [loadSla]);

  const userName = (id: string) => {
    const user = users.find(item => item.id === id);
    return user ? `${user.firstName} ${user.lastName}`.trim() : id;
  };

  const selectedWorkflow = React.useMemo(() => scopeOptions.workflowDefinitions.find(item => item.id === delegation.workflowDefinitionId), [delegation.workflowDefinitionId, scopeOptions.workflowDefinitions]);
  const selectedStep = React.useMemo(() => selectedWorkflow?.steps.find(item => item.id === delegation.workflowStepId), [delegation.workflowStepId, selectedWorkflow]);

  const applyScopeLevel = (level: DelegationScopeLevel) => {
    setScopeLevel(level);
    setDelegation(current => ({
      ...current,
      module: undefined,
      entityType: undefined,
      workflowDefinitionId: undefined,
      workflowStepId: undefined,
    }));
  };

  const selectWorkflow = (workflowDefinitionId: string) => {
    const workflow = scopeOptions.workflowDefinitions.find(item => item.id === workflowDefinitionId);
    setDelegation(current => ({
      ...current,
      workflowDefinitionId,
      workflowStepId: undefined,
      entityType: workflow?.entityType,
      module: workflow?.module,
    }));
  };

  const scopeLabel = (item: WorkflowDelegationDto) => {
    const workflow = item.workflowDefinitionId ? scopeOptions.workflowDefinitions.find(def => def.id === item.workflowDefinitionId) : undefined;
    const step = item.workflowStepId ? workflow?.steps.find(value => value.id === item.workflowStepId) : undefined;
    if (workflow && step) return `${workflow.name} / ${step.name}`;
    if (workflow) return `${workflow.name} v${workflow.version}`;
    if (item.entityType) return item.entityType;
    if (item.module) return item.module;
    return 'All workflows';
  };

  const openDelegationDialog = () => {
    setDelegation(defaultDelegation());
    setScopeLevel('all');
    setDialogOpen(true);
  };

  const saveDelegation = async () => {
    if (!delegation.delegateUserId || !delegation.effectiveFrom || !delegation.effectiveTo || !delegation.reason.trim()) {
      toast.error('Delegate, date range, and reason are required.'); return;
    }
    if ((scopeLevel === 'workflow' || scopeLevel === 'step') && !delegation.workflowDefinitionId) {
      toast.error('Select the workflow for this delegation scope.'); return;
    }
    if (scopeLevel === 'step' && !delegation.workflowStepId) {
      toast.error('Select the workflow step for this delegation scope.'); return;
    }
    try {
      setSaving(true);
      const payload: SaveWorkflowDelegationRequest = {
        ...delegation,
        principalUserId: delegation.principalUserId || undefined,
        currencyCode: delegation.maximumAmount ? delegation.currencyCode || 'GHS' : undefined,
        effectiveFrom: new Date(delegation.effectiveFrom).toISOString(),
        effectiveTo: new Date(delegation.effectiveTo).toISOString(),
      };
      await workflowApiService.createWorkflowDelegation(payload);
      setDialogOpen(false); setDelegation(defaultDelegation()); await load(); toast.success('Delegation created');
    } catch (error: any) { toast.error(error?.message || 'Failed to create delegation'); }
    finally { setSaving(false); }
  };

  const revoke = async (reason: string) => {
    if (!revokeTarget) return;
    try {
      setSaving(true);
      await workflowApiService.revokeWorkflowDelegation(revokeTarget.id, reason);
      setRevokeTarget(null);
      await load();
      toast.success('Delegation revoked');
    }
    catch (error: any) { toast.error(error?.message || 'Failed to revoke delegation'); }
    finally { setSaving(false); }
  };

  const saveCalendar = async () => {
    try {
      setSaving(true);
      const parsedHolidays = holidays.split(',').map(value => value.trim()).filter(Boolean);
      await workflowApiService.saveWorkflowCalendar({ ...calendar, holidays: parsedHolidays });
      await load(); toast.success('Working calendar saved');
    } catch (error: any) { toast.error(error?.message || 'Failed to save working calendar'); }
    finally { setSaving(false); }
  };

  const weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  const escalationActionName = (action: number | string) => ['Notify', 'Reassign', 'Auto approve', 'Cancel', 'Notify manager'][Number(action)] || String(action);
  const slaBreaches = sla?.breaches ?? [];
  const slaEscalations = sla?.escalations ?? [];
  return (
    <Tabs defaultValue="delegations" className="space-y-4">
      <TabsList><TabsTrigger value="delegations"><UserRoundCog className="mr-2 h-4 w-4" />Delegation</TabsTrigger>
        <TabsTrigger value="calendar"><CalendarDays className="mr-2 h-4 w-4" />Working Calendar</TabsTrigger>
        <TabsTrigger value="sla"><AlertTriangle className="mr-2 h-4 w-4" />SLA Breaches</TabsTrigger>
        <TabsTrigger value="evidence"><FileCheck2 className="mr-2 h-4 w-4" />Evidence</TabsTrigger></TabsList>
      <TabsContent value="delegations" className="space-y-4">
        <div className="flex items-center justify-between"><div><h3 className="font-semibold">Authority and absence cover</h3>
          <p className="text-sm text-muted-foreground">Effective-dated approval authority with workflow, step, and monetary limits.</p></div>
          <Button onClick={openDelegationDialog}><Plus className="mr-2 h-4 w-4" />Add delegation</Button></div>
        <div className="overflow-hidden rounded-md border"><Table><TableHeader><TableRow><TableHead>Delegate</TableHead><TableHead>Type</TableHead>
          <TableHead>Scope</TableHead><TableHead>Authority</TableHead><TableHead>Effective</TableHead><TableHead>Status</TableHead><TableHead className="w-12" /></TableRow></TableHeader>
          <TableBody>{delegations.map(item => <TableRow key={item.id}><TableCell className="font-medium">{userName(item.delegateUserId)}</TableCell>
            <TableCell>{item.kind === WorkflowDelegationKind.OutOfOffice ? 'Out of office' : 'Authority'}</TableCell>
            <TableCell>{scopeLabel(item)}</TableCell>
            <TableCell>{item.maximumAmount ? `${item.currencyCode || ''} ${item.maximumAmount.toLocaleString()}` : 'No monetary cap'}</TableCell>
            <TableCell>{new Date(item.effectiveFrom).toLocaleDateString()} - {new Date(item.effectiveTo).toLocaleDateString()}</TableCell>
            <TableCell><Badge variant={item.isActive ? 'default' : 'secondary'}>{item.isActive ? 'Active' : 'Revoked'}</Badge></TableCell>
            <TableCell>{item.isActive && <Button size="icon" variant="ghost" title="Revoke delegation" onClick={() => setRevokeTarget(item)}><Archive className="h-4 w-4" /></Button>}</TableCell>
          </TableRow>)}</TableBody></Table></div>
      </TabsContent>
      <TabsContent value="calendar" className="max-w-3xl space-y-5">
        <div><h3 className="font-semibold">SLA working calendar</h3><p className="text-sm text-muted-foreground">Due dates and escalation timers skip non-working time.</p></div>
        <div className="grid gap-4 sm:grid-cols-2"><div><Label>Name</Label><Input value={calendar.name} onChange={e => setCalendar({...calendar, name:e.target.value})} /></div>
          <div><Label>Server time zone ID</Label><Input value={calendar.timeZoneId} onChange={e => setCalendar({...calendar, timeZoneId:e.target.value})} /></div>
          <div><Label>Work day starts</Label><Input type="time" value={calendar.workDayStart.slice(0,5)} onChange={e => setCalendar({...calendar, workDayStart:`${e.target.value}:00`})} /></div>
          <div><Label>Work day ends</Label><Input type="time" value={calendar.workDayEnd.slice(0,5)} onChange={e => setCalendar({...calendar, workDayEnd:`${e.target.value}:00`})} /></div></div>
        <div className="space-y-2"><Label>Working days</Label><div className="flex flex-wrap gap-4">{weekdays.map((day,index) => <label key={day} className="flex items-center gap-2 text-sm">
          <Checkbox checked={(calendar.workingDaysMask & (1 << index)) !== 0} onCheckedChange={checked => setCalendar({...calendar, workingDaysMask: checked ? calendar.workingDaysMask | (1 << index) : calendar.workingDaysMask & ~(1 << index)})} />{day}</label>)}</div></div>
        <div><Label>Holidays</Label><Input value={holidays} onChange={e => setHolidays(e.target.value)} placeholder="2026-01-01, 2026-03-06" /></div>
        <Button onClick={() => void saveCalendar()} disabled={saving}>Save calendar</Button>
      </TabsContent>
      <TabsContent value="sla" className="space-y-5">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div><h3 className="font-semibold">SLA breaches and escalations</h3>
            <p className="text-sm text-muted-foreground">Pending approvals past due and escalation actions executed by the workflow SLA worker.</p></div>
          <div className="flex items-end gap-2"><div><Label>Lookback days</Label><Input className="w-28" type="number" min="1" max="365" value={slaDays} onChange={event => setSlaDays(Number(event.target.value) || 30)} /></div>
            <Button variant="outline" onClick={() => void loadSla()} disabled={loadingSla}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>
        </div>
        <div className="grid gap-3 sm:grid-cols-3">
          <div className="rounded-md border bg-muted/30 p-4"><div className="text-sm text-muted-foreground">Open breaches</div><div className="mt-1 text-2xl font-semibold">{slaBreaches.length}</div></div>
          <div className="rounded-md border bg-muted/30 p-4"><div className="text-sm text-muted-foreground">Escalations</div><div className="mt-1 text-2xl font-semibold">{slaEscalations.length}</div></div>
          <div className="rounded-md border bg-muted/30 p-4"><div className="text-sm text-muted-foreground">Last checked</div><div className="mt-1 font-medium">{sla?.generatedAt ? new Date(sla.generatedAt).toLocaleString() : 'Not loaded'}</div></div>
        </div>
        <div className="overflow-hidden rounded-md border"><Table><TableHeader><TableRow><TableHead>Workflow</TableHead><TableHead>Step</TableHead><TableHead>Responsible</TableHead><TableHead>Due</TableHead><TableHead className="text-right">Overdue</TableHead></TableRow></TableHeader>
          <TableBody>{slaBreaches.length === 0 ? <TableRow><TableCell colSpan={5} className="py-8 text-center text-muted-foreground">No open SLA breaches.</TableCell></TableRow> : slaBreaches.map(item => <TableRow key={item.approvalId}>
            <TableCell><div className="font-medium">{item.workflowName}</div><div className="text-xs text-muted-foreground">{item.entityType} / {item.entityId}</div></TableCell>
            <TableCell>{item.stepName}</TableCell>
            <TableCell>{item.approverName || item.approverRole || 'Unassigned'}</TableCell>
            <TableCell>{item.dueDate ? new Date(item.dueDate).toLocaleString() : 'No due date'}</TableCell>
            <TableCell className="text-right"><Badge variant="destructive">{item.hoursOverdue}h</Badge></TableCell>
          </TableRow>)}</TableBody></Table></div>
        <div className="overflow-hidden rounded-md border"><Table><TableHeader><TableRow><TableHead>Escalation</TableHead><TableHead>Workflow</TableHead><TableHead>Target</TableHead><TableHead>Executed</TableHead><TableHead>Result</TableHead></TableRow></TableHeader>
          <TableBody>{slaEscalations.length === 0 ? <TableRow><TableCell colSpan={5} className="py-8 text-center text-muted-foreground">No escalation actions in the selected period.</TableCell></TableRow> : slaEscalations.map(item => <TableRow key={item.id}>
            <TableCell><Badge variant="outline">{escalationActionName(item.action)}</Badge><div className="mt-1 text-xs text-muted-foreground">Rule {item.ruleIndex + 1}</div></TableCell>
            <TableCell>{item.approval ? <><div className="font-medium">{item.approval.workflowName}</div><div className="text-xs text-muted-foreground">{item.approval.stepName}</div></> : 'Approval not found'}</TableCell>
            <TableCell>{item.targetUserName || item.targetRole || 'Configured target'}</TableCell>
            <TableCell>{new Date(item.executedAt).toLocaleString()}</TableCell>
            <TableCell><div className="max-w-xs truncate" title={item.result}>{item.result || 'Completed'}</div></TableCell>
          </TableRow>)}</TableBody></Table></div>
      </TabsContent>
      <TabsContent value="evidence"><WorkflowEvidenceGovernancePanel /></TabsContent>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}><DialogContent className="sm:max-w-3xl"><DialogHeader><DialogTitle>Add delegation</DialogTitle></DialogHeader>
        <div className="grid gap-4 sm:grid-cols-2">
          <div><Label>Delegating user</Label><Select value={delegation.principalUserId || undefined} onValueChange={value => setDelegation({...delegation,principalUserId:value})}><SelectTrigger><SelectValue placeholder="Current user" /></SelectTrigger><SelectContent>{users.map(user => <SelectItem key={user.id} value={user.id}>{user.firstName} {user.lastName}</SelectItem>)}</SelectContent></Select></div>
          <div><Label>Delegate</Label><Select value={delegation.delegateUserId} onValueChange={value => setDelegation({...delegation,delegateUserId:value})}><SelectTrigger><SelectValue placeholder="Select user" /></SelectTrigger><SelectContent>{users.map(user => <SelectItem key={user.id} value={user.id}>{user.firstName} {user.lastName}</SelectItem>)}</SelectContent></Select></div>
          <div><Label>Type</Label><Select value={String(delegation.kind)} onValueChange={value => setDelegation({...delegation,kind:Number(value) as WorkflowDelegationKind})}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="0">Authority delegation</SelectItem><SelectItem value="1">Out of office substitute</SelectItem></SelectContent></Select></div>
          <div><Label>Scope</Label><Select value={scopeLevel} onValueChange={value => applyScopeLevel(value as DelegationScopeLevel)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>
            <SelectItem value="all">All workflows</SelectItem><SelectItem value="module">Module</SelectItem><SelectItem value="entity">Entity type</SelectItem><SelectItem value="workflow">Specific workflow</SelectItem><SelectItem value="step">Specific workflow step</SelectItem>
          </SelectContent></Select></div>
          {scopeLevel === 'module' && <div className="sm:col-span-2"><Label>Module</Label><Select value={delegation.module || undefined} onValueChange={value => setDelegation({...delegation,module:value,entityType:undefined,workflowDefinitionId:undefined,workflowStepId:undefined})}><SelectTrigger><SelectValue placeholder="Select module" /></SelectTrigger><SelectContent>{scopeOptions.modules.map(item => <SelectItem key={item.id} value={item.name}>{item.name} ({item.code})</SelectItem>)}</SelectContent></Select></div>}
          {scopeLevel === 'entity' && <div className="sm:col-span-2"><Label>Entity type</Label><Select value={delegation.entityType || undefined} onValueChange={value => setDelegation({...delegation,entityType:value,module:undefined,workflowDefinitionId:undefined,workflowStepId:undefined})}><SelectTrigger><SelectValue placeholder="Select entity type" /></SelectTrigger><SelectContent>{scopeOptions.entityTypes.map(item => <SelectItem key={item.id} value={item.name}>{item.name}</SelectItem>)}</SelectContent></Select></div>}
          {(scopeLevel === 'workflow' || scopeLevel === 'step') && <div className="sm:col-span-2"><Label>Workflow</Label><Select value={delegation.workflowDefinitionId || undefined} onValueChange={selectWorkflow}><SelectTrigger><SelectValue placeholder="Select workflow" /></SelectTrigger><SelectContent>{scopeOptions.workflowDefinitions.map(item => <SelectItem key={item.id} value={item.id}>{item.name} v{item.version} / {item.entityType}</SelectItem>)}</SelectContent></Select></div>}
          {scopeLevel === 'step' && <div className="sm:col-span-2"><Label>Workflow step</Label><Select value={delegation.workflowStepId || undefined} onValueChange={value => setDelegation({...delegation,workflowStepId:value})} disabled={!selectedWorkflow}><SelectTrigger><SelectValue placeholder="Select workflow step" /></SelectTrigger><SelectContent>{(selectedWorkflow?.steps || []).map(item => <SelectItem key={item.id} value={item.id}>{item.order}. {item.name}</SelectItem>)}</SelectContent></Select></div>}
          {(selectedWorkflow || selectedStep) && <div className="sm:col-span-2 rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
            {selectedWorkflow ? <div><span className="font-medium text-foreground">{selectedWorkflow.name}</span> applies to {selectedWorkflow.entityType}{selectedWorkflow.module ? ` in ${selectedWorkflow.module}` : ''}.</div> : null}
            {selectedStep ? <div>Delegation is narrowed to step <span className="font-medium text-foreground">{selectedStep.name}</span>.</div> : null}
          </div>}
          <div><Label>Maximum amount</Label><Input type="number" min="0" value={delegation.maximumAmount || ''} onChange={e => setDelegation({...delegation,maximumAmount:e.target.value ? Number(e.target.value):undefined,currencyCode:e.target.value ? delegation.currencyCode || 'GHS':undefined})} /></div>
          <div><Label>Effective from</Label><Input type="date" value={delegation.effectiveFrom.slice(0,10)} onChange={e => setDelegation({...delegation,effectiveFrom:e.target.value})} /></div>
          <div><Label>Effective to</Label><Input type="date" value={delegation.effectiveTo.slice(0,10)} onChange={e => setDelegation({...delegation,effectiveTo:e.target.value})} /></div>
          <div className="sm:col-span-2"><Label>Reason</Label><Textarea value={delegation.reason} onChange={e => setDelegation({...delegation,reason:e.target.value})} /></div>
          <label className="sm:col-span-2 flex items-center gap-3 text-sm"><Switch checked={delegation.allowRedelegation} onCheckedChange={checked => setDelegation({...delegation,allowRedelegation:checked})} />Allow the delegate to re-delegate this authority</label></div>
        <DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)}>Cancel</Button><Button onClick={() => void saveDelegation()} disabled={saving}>Create delegation</Button></DialogFooter>
      </DialogContent></Dialog>
      <WorkflowReasonDialog
        open={!!revokeTarget}
        onOpenChange={open => !open && setRevokeTarget(null)}
        title="Revoke delegation"
        description={revokeTarget ? (
          <div className="space-y-2">
            <p className="text-sm text-muted-foreground">Revoking this delegation immediately removes delegated approval authority for the selected scope.</p>
            <div className="rounded-md border bg-muted/30 p-3 text-sm">
              <div className="font-medium text-foreground">{userName(revokeTarget.delegateUserId)}</div>
              <div className="text-muted-foreground">{scopeLabel(revokeTarget)}</div>
            </div>
          </div>
        ) : undefined}
        reasonLabel="Revocation reason"
        reasonPlaceholder="Explain why this delegation is being revoked"
        confirmText="Revoke delegation"
        variant="destructive"
        isLoading={saving}
        onConfirm={revoke}
      />
    </Tabs>
  );
}
