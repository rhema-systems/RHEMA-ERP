'use client';

import * as React from 'react';
import { Copy, Edit3, Plus, Rocket, Archive } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { workflowApiService } from '@/services/workflow-api.service';
import {
  WorkflowApprovalActivationMode,
  WorkflowApprovalType,
  WorkflowAssignmentType,
  WorkflowDefinitionLifecycleStatus,
  WorkflowRejectionHandling,
  type SaveWorkflowApprovalPolicyRequest,
  type WorkflowApprovalPolicySetDto,
  type WorkflowEntityTypeInfo,
} from '@/types/workflow';

const emptyForm = (): SaveWorkflowApprovalPolicyRequest => ({
  code: '', name: '', description: '', module: '', entityType: '', category: '',
  currencyCode: 'GHS', effectiveFrom: new Date().toISOString().slice(0, 10),
  priority: 0, isActive: true,
  approvalConfig: {
    approvalType: WorkflowApprovalType.Single,
    activationMode: WorkflowApprovalActivationMode.Parallel,
    approverRules: [], minApprovalsRequired: 1,
    rejectionHandling: WorkflowRejectionHandling.StopWorkflow,
    preventInitiatorApproval: true, requireDistinctApprovers: true, conflictRules: [],
  },
});

export function WorkflowApprovalPoliciesPanel({ entityTypes }: { entityTypes: WorkflowEntityTypeInfo[] }) {
  const [policies, setPolicies] = React.useState<WorkflowApprovalPolicySetDto[]>([]);
  const [form, setForm] = React.useState<SaveWorkflowApprovalPolicyRequest>(emptyForm());
  const [roles, setRoles] = React.useState('');
  const [editingId, setEditingId] = React.useState<string>();
  const [open, setOpen] = React.useState(false);
  const [saving, setSaving] = React.useState(false);

  const load = React.useCallback(async () => {
    try { setPolicies(await workflowApiService.getApprovalPolicies()); }
    catch (error: any) { toast.error(error?.message || 'Failed to load approval policies'); }
  }, []);
  React.useEffect(() => { void load(); }, [load]);

  const edit = (policy?: WorkflowApprovalPolicySetDto) => {
    setEditingId(policy?.id);
    setForm(policy ? { ...policy, approvalConfig: { ...policy.approvalConfig } } : emptyForm());
    setRoles(policy?.approvalConfig.approverRules.map(rule => rule.role).filter(Boolean).join(', ') || '');
    setOpen(true);
  };

  const save = async () => {
    const roleList = roles.split(',').map(value => value.trim()).filter(Boolean);
    if (!form.code.trim() || !form.name.trim() || !form.entityType || roleList.length === 0) {
      toast.error('Code, name, entity type, and at least one approver role are required.');
      return;
    }
    const sequential = form.approvalConfig.activationMode === WorkflowApprovalActivationMode.Sequential;
    const request: SaveWorkflowApprovalPolicyRequest = {
      ...form,
      approvalConfig: {
        ...form.approvalConfig,
        approverRules: roleList.map((role, index) => ({
          assignmentType: WorkflowAssignmentType.Role,
          role,
          priority: roleList.length - index,
          approvalGroup: sequential ? index + 1 : 1,
        })),
      },
    };
    try {
      setSaving(true);
      if (editingId) await workflowApiService.updateApprovalPolicy(editingId, request);
      else await workflowApiService.createApprovalPolicy(request);
      setOpen(false);
      await load();
      toast.success('Approval policy draft saved');
    } catch (error: any) { toast.error(error?.message || 'Failed to save approval policy'); }
    finally { setSaving(false); }
  };

  const action = async (operation: () => Promise<unknown>, message: string) => {
    try { await operation(); await load(); toast.success(message); }
    catch (error: any) { toast.error(error?.message || 'Policy action failed'); }
  };

  return <div className="space-y-4">
    <div className="flex items-center justify-between">
      <div><h3 className="text-lg font-semibold">Approval Policy Sets</h3><p className="text-sm text-muted-foreground">Effective-dated approval rules shared across workflow definitions.</p></div>
      <Button onClick={() => edit()}><Plus className="mr-2 h-4 w-4" />New Policy</Button>
    </div>
    <div className="border">
      <Table><TableHeader><TableRow><TableHead>Code</TableHead><TableHead>Scope</TableHead><TableHead>Effective</TableHead><TableHead>Status</TableHead><TableHead className="text-right">Actions</TableHead></TableRow></TableHeader>
        <TableBody>{policies.map(policy => <TableRow key={policy.id}>
          <TableCell><div className="font-medium">{policy.code}</div><div className="text-xs text-muted-foreground">{policy.name}</div></TableCell>
          <TableCell><div>{policy.entityType || 'All entities'}</div><div className="text-xs text-muted-foreground">{[policy.module, policy.category].filter(Boolean).join(' / ') || 'General'}</div></TableCell>
          <TableCell className="text-sm">{new Date(policy.effectiveFrom).toLocaleDateString()} {policy.effectiveTo ? `to ${new Date(policy.effectiveTo).toLocaleDateString()}` : 'onward'}</TableCell>
          <TableCell><Badge variant="outline">{policy.lifecycleStatus}</Badge></TableCell>
          <TableCell><div className="flex justify-end gap-1">
            {policy.lifecycleStatus === WorkflowDefinitionLifecycleStatus.Draft && <><Button size="icon" variant="ghost" title="Edit" onClick={() => edit(policy)}><Edit3 className="h-4 w-4" /></Button><Button size="icon" variant="ghost" title="Publish" onClick={() => action(() => workflowApiService.publishApprovalPolicy(policy.id), 'Policy published')}><Rocket className="h-4 w-4" /></Button></>}
            <Button size="icon" variant="ghost" title="Clone draft" onClick={() => action(() => workflowApiService.cloneApprovalPolicyDraft(policy.id), 'Draft cloned')}><Copy className="h-4 w-4" /></Button>
            {policy.lifecycleStatus === WorkflowDefinitionLifecycleStatus.Published && <Button size="icon" variant="ghost" title="Retire" onClick={() => action(() => workflowApiService.retireApprovalPolicy(policy.id), 'Policy retired')}><Archive className="h-4 w-4" /></Button>}
          </div></TableCell>
        </TableRow>)}</TableBody>
      </Table>
    </div>
    <Dialog open={open} onOpenChange={setOpen}><DialogContent className="max-w-3xl"><DialogHeader><DialogTitle>{editingId ? 'Edit Approval Policy Draft' : 'New Approval Policy'}</DialogTitle></DialogHeader>
      <div className="grid gap-4 md:grid-cols-2">
        <div><Label>Code</Label><Input value={form.code} onChange={e => setForm({ ...form, code: e.target.value })} /></div>
        <div><Label>Name</Label><Input value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /></div>
        <div><Label>Module</Label><Input value={form.module || ''} onChange={e => setForm({ ...form, module: e.target.value })} /></div>
        <div><Label>Entity Type</Label><Select value={form.entityType || ''} onValueChange={value => setForm({ ...form, entityType: value })}><SelectTrigger><SelectValue placeholder="Select entity type" /></SelectTrigger><SelectContent>{entityTypes.map(type => <SelectItem key={type.code} value={type.name}>{type.name}</SelectItem>)}</SelectContent></Select></div>
        <div><Label>Category</Label><Input value={form.category || ''} onChange={e => setForm({ ...form, category: e.target.value })} /></div>
        <div><Label>Approver roles</Label><Input value={roles} placeholder="Manager, Finance, MD" onChange={e => setRoles(e.target.value)} /></div>
        <div><Label>Minimum amount</Label><Input type="number" value={form.minimumAmount ?? ''} onChange={e => setForm({ ...form, minimumAmount: e.target.value ? Number(e.target.value) : undefined })} /></div>
        <div><Label>Maximum amount</Label><Input type="number" value={form.maximumAmount ?? ''} onChange={e => setForm({ ...form, maximumAmount: e.target.value ? Number(e.target.value) : undefined })} /></div>
        <div><Label>Effective from</Label><Input type="date" value={form.effectiveFrom.slice(0, 10)} onChange={e => setForm({ ...form, effectiveFrom: e.target.value })} /></div>
        <div><Label>Effective to</Label><Input type="date" value={form.effectiveTo?.slice(0, 10) || ''} onChange={e => setForm({ ...form, effectiveTo: e.target.value || undefined })} /></div>
        <div><Label>Execution</Label><Select value={String(form.approvalConfig.activationMode ?? WorkflowApprovalActivationMode.Parallel)} onValueChange={value => setForm({ ...form, approvalConfig: { ...form.approvalConfig, activationMode: Number(value) as WorkflowApprovalActivationMode } })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="0">Parallel</SelectItem><SelectItem value="1">Sequential</SelectItem></SelectContent></Select></div>
        <div><Label>Approval rule</Label><Select value={String(form.approvalConfig.approvalType)} onValueChange={value => setForm({ ...form, approvalConfig: { ...form.approvalConfig, approvalType: Number(value) as WorkflowApprovalType } })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="0">Any / minimum</SelectItem><SelectItem value="1">All</SelectItem><SelectItem value="2">Consensus</SelectItem><SelectItem value="3">Majority</SelectItem></SelectContent></Select></div>
        <div className="flex items-center justify-between border p-3"><Label>Prevent initiator approval</Label><Switch checked={form.approvalConfig.preventInitiatorApproval} onCheckedChange={checked => setForm({ ...form, approvalConfig: { ...form.approvalConfig, preventInitiatorApproval: checked } })} /></div>
        <div className="flex items-center justify-between border p-3"><Label>Distinct approvers</Label><Switch checked={form.approvalConfig.requireDistinctApprovers} onCheckedChange={checked => setForm({ ...form, approvalConfig: { ...form.approvalConfig, requireDistinctApprovers: checked } })} /></div>
        <div className="md:col-span-2"><Label>Description</Label><Textarea value={form.description || ''} onChange={e => setForm({ ...form, description: e.target.value })} /></div>
      </div><DialogFooter><Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button><Button disabled={saving} onClick={save}>{saving ? 'Saving...' : 'Save Draft'}</Button></DialogFooter>
    </DialogContent></Dialog>
  </div>;
}
