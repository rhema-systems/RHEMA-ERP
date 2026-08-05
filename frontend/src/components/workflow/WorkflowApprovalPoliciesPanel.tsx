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
  WorkflowConditionType,
  WorkflowLogicalOperator,
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
    evidenceRequirements: [], allowEvidenceException: false,
    evidenceExceptionApproverRole: 'Managing Director', minimumExceptionReasonLength: 30,
    requiresManagingDirectorApproval: false, managingDirectorApproverRole: 'Managing Director',
  },
});

export function WorkflowApprovalPoliciesPanel({ entityTypes }: { entityTypes: WorkflowEntityTypeInfo[] }) {
  const [policies, setPolicies] = React.useState<WorkflowApprovalPolicySetDto[]>([]);
  const [form, setForm] = React.useState<SaveWorkflowApprovalPolicyRequest>(emptyForm());
  const [roles, setRoles] = React.useState('');
  const [evidenceLines, setEvidenceLines] = React.useState('');
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
    const executiveRole = policy?.approvalConfig.managingDirectorApproverRole;
    // The conditional executive rule is edited through its dedicated control. Omitting it from
    // the ordinary role list prevents a cloned TDC policy from becoming an unconditional MD step.
    setRoles(policy?.approvalConfig.approverRules
      .filter(rule => !(rule.condition && rule.role === executiveRole))
      .map(rule => rule.role).filter(Boolean).join(', ') || '');
    setEvidenceLines(policy?.approvalConfig.evidenceRequirements?.map(requirement =>
      [requirement.requirementKey, requirement.documentName, requirement.documentType || '', requirement.minimumDocuments, requirement.requireVerification ? 'yes' : 'no'].join(' | ')
    ).join('\n') || '');
    setOpen(true);
  };

  const save = async () => {
    const roleList = roles.split(',').map(value => value.trim()).filter(Boolean);
    if (!form.code.trim() || !form.name.trim() || !form.entityType || roleList.length === 0) {
      toast.error('Code, name, entity type, and at least one approver role are required.');
      return;
    }
    const parsedEvidenceLines = evidenceLines.split('\n').map(line => line.trim()).filter(Boolean);
    const invalidEvidenceLine = parsedEvidenceLines.findIndex(line => {
      const [requirementKey, documentName] = line.split('|').map(value => value.trim());
      return !requirementKey || !documentName;
    });
    if (invalidEvidenceLine >= 0) {
      toast.error(`Evidence line ${invalidEvidenceLine + 1} needs a key and document name.`);
      return;
    }
    const evidenceRequirements = parsedEvidenceLines.map(line => {
      const [requirementKey, documentName, documentType, minimumDocuments, requireVerification] = line.split('|').map(value => value.trim());
      return {
        requirementKey,
        documentName,
        documentType: documentType || undefined,
        minimumDocuments: Math.max(Number(minimumDocuments) || 1, 1),
        requireVerification: !['no', 'false', '0'].includes((requireVerification || 'yes').toLowerCase()),
      };
    });
    const executiveRequired = Boolean(
      form.approvalConfig.requiresManagingDirectorApproval || form.approvalConfig.allowEvidenceException
    );
    const executiveRole = form.approvalConfig.managingDirectorApproverRole?.trim() || 'Managing Director';
    const sequential = executiveRequired || form.approvalConfig.activationMode === WorkflowApprovalActivationMode.Sequential;
    const ordinaryRules = roleList.filter(role => role.toLowerCase() !== executiveRole.toLowerCase()).map((role, index) => ({
      assignmentType: WorkflowAssignmentType.Role,
      role,
      priority: roleList.length - index,
      approvalGroup: sequential ? index + 1 : 1,
    }));
    if (ordinaryRules.length === 0) {
      toast.error('At least one ordinary approver role is required before the executive stage.');
      return;
    }
    const request: SaveWorkflowApprovalPolicyRequest = {
      ...form,
      approvalConfig: {
        ...form.approvalConfig,
        activationMode: sequential ? WorkflowApprovalActivationMode.Sequential : WorkflowApprovalActivationMode.Parallel,
        evidenceRequirements,
        managingDirectorApproverRole: executiveRole,
        evidenceExceptionApproverRole: form.approvalConfig.evidenceExceptionApproverRole?.trim() || executiveRole,
        minimumExceptionReasonLength: Math.max(form.approvalConfig.minimumExceptionReasonLength || 30, 20),
        approverRules: executiveRequired ? [
          ...ordinaryRules,
          {
            assignmentType: WorkflowAssignmentType.Role,
            role: executiveRole,
            priority: 1,
            approvalGroup: ordinaryRules.length + 1,
            condition: {
              conditionType: WorkflowConditionType.Expression,
              expression: 'requiresManagingDirectorApproval == true',
              logicalOperator: WorkflowLogicalOperator.And,
            },
          },
        ] : ordinaryRules,
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
    <Dialog open={open} onOpenChange={setOpen}><DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto"><DialogHeader><DialogTitle>{editingId ? 'Edit Approval Policy Draft' : 'New Approval Policy'}</DialogTitle></DialogHeader>
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
        <div className="flex items-center justify-between border p-3">
          <div><Label>Require Managing Director</Label><p className="text-xs text-muted-foreground">Adds a conditional final authority stage.</p></div>
          <Switch checked={Boolean(form.approvalConfig.requiresManagingDirectorApproval)} onCheckedChange={checked => setForm({ ...form, approvalConfig: { ...form.approvalConfig, requiresManagingDirectorApproval: checked } })} />
        </div>
        <div className="flex items-center justify-between border p-3">
          <div><Label>Allow evidence exception</Label><p className="text-xs text-muted-foreground">Exceptions still require executive approval.</p></div>
          <Switch checked={Boolean(form.approvalConfig.allowEvidenceException)} onCheckedChange={checked => setForm({ ...form, approvalConfig: { ...form.approvalConfig, allowEvidenceException: checked } })} />
        </div>
        <div><Label>Executive approver role</Label><Input value={form.approvalConfig.managingDirectorApproverRole || ''} onChange={e => setForm({ ...form, approvalConfig: { ...form.approvalConfig, managingDirectorApproverRole: e.target.value, evidenceExceptionApproverRole: e.target.value } })} /></div>
        <div><Label>Minimum exception reason</Label><Input type="number" min={20} value={form.approvalConfig.minimumExceptionReasonLength ?? 30} onChange={e => setForm({ ...form, approvalConfig: { ...form.approvalConfig, minimumExceptionReasonLength: Number(e.target.value) || 30 } })} /></div>
        <div className="md:col-span-2">
          <Label>Evidence requirements</Label>
          <Textarea
            className="mt-1 min-h-28 font-mono text-xs"
            value={evidenceLines}
            onChange={e => setEvidenceLines(e.target.value)}
            placeholder={'payment-support | Approved payment supporting pack | PaymentSupport | 1 | yes'}
          />
          <p className="mt-1 text-xs text-muted-foreground">One per line: key | document name | document type | minimum documents | verification yes/no.</p>
        </div>
        <div className="md:col-span-2"><Label>Description</Label><Textarea value={form.description || ''} onChange={e => setForm({ ...form, description: e.target.value })} /></div>
      </div><DialogFooter><Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button><Button disabled={saving} onClick={save}>{saving ? 'Saving...' : 'Save Draft'}</Button></DialogFooter>
    </DialogContent></Dialog>
  </div>;
}
