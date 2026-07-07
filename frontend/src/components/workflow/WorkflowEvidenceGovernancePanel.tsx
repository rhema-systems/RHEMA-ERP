'use client';

import * as React from 'react';
import { FileCheck2, Search, ShieldCheck, ShieldOff } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { workflowApiService } from '@/services/workflow-api.service';
import type {
  WorkflowDelegationScopeOptionsDto,
  WorkflowEvidenceDocumentDto,
  WorkflowEvidencePolicyDto,
  WorkflowEvidenceReviewInstanceDto,
} from '@/types/workflow';
import { WorkflowReasonDialog } from './WorkflowReasonDialog';

const defaultPolicy: WorkflowEvidencePolicyDto = {
  allowedExtensions: ['.pdf', '.png', '.jpg', '.jpeg'],
  maximumFileSizeBytes: 10 * 1024 * 1024,
  retentionDays: 2555,
  requireMalwareScan: true,
};

const defaultScopeOptions: WorkflowDelegationScopeOptionsDto = {
  modules: [],
  entityTypes: [],
  workflowDefinitions: [],
};

const verificationLabel = (status: number) => ['Pending', 'Verified', 'Rejected'][status] || 'Unknown';
const instanceStatusLabel = (status: number | string) => ['Created', 'In Progress', 'Completed', 'Cancelled', 'Failed', 'Suspended', 'Waiting'][Number(status)] || String(status);
const stepStatusLabel = (status: number | string) => ['Pending', 'In Progress', 'Completed', 'Cancelled', 'Failed'][Number(status)] || String(status);

type EvidenceAction = 'verify' | 'reject' | 'applyHold' | 'releaseHold';

interface PendingEvidenceAction {
  item: WorkflowEvidenceDocumentDto;
  action: EvidenceAction;
}

export function WorkflowEvidenceGovernancePanel() {
  const [policy, setPolicy] = React.useState(defaultPolicy);
  const [extensions, setExtensions] = React.useState(defaultPolicy.allowedExtensions.join(', '));
  const [scopeOptions, setScopeOptions] = React.useState<WorkflowDelegationScopeOptionsDto>(defaultScopeOptions);
  const [reviewInstances, setReviewInstances] = React.useState<WorkflowEvidenceReviewInstanceDto[]>([]);
  const [reviewSearch, setReviewSearch] = React.useState('');
  const [reviewWorkflowDefinitionId, setReviewWorkflowDefinitionId] = React.useState('all');
  const [reviewEntityType, setReviewEntityType] = React.useState('all');
  const [selectedInstanceId, setSelectedInstanceId] = React.useState('');
  const [stepInstanceId, setStepInstanceId] = React.useState('');
  const [evidence, setEvidence] = React.useState<WorkflowEvidenceDocumentDto[]>([]);
  const [saving, setSaving] = React.useState(false);
  const [pendingAction, setPendingAction] = React.useState<PendingEvidenceAction | null>(null);
  const [savingAction, setSavingAction] = React.useState(false);
  const [loadingInstances, setLoadingInstances] = React.useState(false);
  const [loadingEvidence, setLoadingEvidence] = React.useState(false);
  const selectedInstanceIdRef = React.useRef('');

  React.useEffect(() => {
    selectedInstanceIdRef.current = selectedInstanceId;
  }, [selectedInstanceId]);

  React.useEffect(() => {
    void workflowApiService.getWorkflowEvidencePolicy().then(value => {
      setPolicy(value);
      setExtensions(value.allowedExtensions.join(', '));
    }).catch((error: any) => toast.error(error?.message || 'Failed to load evidence policy'));
    void workflowApiService.getWorkflowDelegationScopeOptions()
      .then(setScopeOptions)
      .catch((error: any) => toast.error(error?.message || 'Failed to load workflow selectors'));
  }, []);

  const selectedInstance = React.useMemo(() => reviewInstances.find(item => item.id === selectedInstanceId), [reviewInstances, selectedInstanceId]);
  const selectedStep = React.useMemo(() => selectedInstance?.steps.find(item => item.stepInstanceId === stepInstanceId), [selectedInstance, stepInstanceId]);

  const loadReviewInstances = React.useCallback(async () => {
    try {
      setLoadingInstances(true);
      const rows = await workflowApiService.getWorkflowEvidenceReviewInstances({
        search: reviewSearch.trim() || undefined,
        workflowDefinitionId: reviewWorkflowDefinitionId === 'all' ? undefined : reviewWorkflowDefinitionId,
        entityType: reviewEntityType === 'all' ? undefined : reviewEntityType,
        pageSize: 100,
      });
      setReviewInstances(rows);
      const currentInstance = rows.find(item => item.id === selectedInstanceIdRef.current);
      const next = currentInstance || rows[0];
      const nextStep = next?.steps.find(step => step.stepInstanceId === next.currentStepInstanceId) ||
        next?.steps.find(step => step.evidence.total > 0) ||
        next?.steps[0];
      setSelectedInstanceId(next?.id || '');
      setStepInstanceId(nextStep?.stepInstanceId || '');
    } catch (error: any) { toast.error(error?.message || 'Failed to load workflow instances'); }
    finally { setLoadingInstances(false); }
  }, [reviewEntityType, reviewSearch, reviewWorkflowDefinitionId]);

  React.useEffect(() => { void loadReviewInstances(); }, [loadReviewInstances]);

  const save = async () => {
    const allowedExtensions = extensions.split(',').map(value => value.trim().toLowerCase())
      .filter(Boolean).map(value => value.startsWith('.') ? value : `.${value}`);
    if (!allowedExtensions.length || policy.retentionDays < 2555 || policy.maximumFileSizeBytes <= 0) {
      toast.error('Extensions, a positive file-size limit, and at least 2,555 retention days are required.');
      return;
    }
    try {
      setSaving(true);
      const saved = await workflowApiService.saveWorkflowEvidencePolicy({ ...policy, allowedExtensions });
      setPolicy(saved); setExtensions(saved.allowedExtensions.join(', '));
      toast.success('Evidence policy saved');
    } catch (error: any) { toast.error(error?.message || 'Failed to save evidence policy'); }
    finally { setSaving(false); }
  };

  const loadEvidence = React.useCallback(async (stepId = stepInstanceId) => {
    if (!stepId.trim()) return;
    try {
      setLoadingEvidence(true);
      setEvidence(await workflowApiService.getWorkflowStepEvidence(stepId.trim()));
    } catch (error: any) { toast.error(error?.message || 'Failed to load step evidence'); }
    finally { setLoadingEvidence(false); }
  }, [stepInstanceId]);

  React.useEffect(() => {
    if (stepInstanceId) void loadEvidence(stepInstanceId);
    else setEvidence([]);
  }, [loadEvidence, stepInstanceId]);

  const chooseInstance = (instanceId: string) => {
    const instance = reviewInstances.find(item => item.id === instanceId);
    setSelectedInstanceId(instanceId);
    const step = instance?.steps.find(item => item.stepInstanceId === instance.currentStepInstanceId) ||
      instance?.steps.find(item => item.evidence.total > 0) ||
      instance?.steps[0];
    setStepInstanceId(step?.stepInstanceId || '');
  };

  const openEvidenceAction = (item: WorkflowEvidenceDocumentDto, action: EvidenceAction) => {
    setPendingAction({ item, action });
  };

  const submitEvidenceAction = async (reason: string) => {
    if (!pendingAction) return;
    try {
      setSavingAction(true);
      if (pendingAction.action === 'verify' || pendingAction.action === 'reject') {
        const accepted = pendingAction.action === 'verify';
        await workflowApiService.verifyWorkflowEvidence(pendingAction.item.id, accepted, reason || undefined);
        toast.success(accepted ? 'Evidence verified' : 'Evidence rejected');
      } else {
        const enabled = pendingAction.action === 'applyHold';
        await workflowApiService.setWorkflowEvidenceLegalHold(pendingAction.item.id, enabled, reason);
        toast.success(enabled ? 'Legal hold applied' : 'Legal hold released');
      }
      setPendingAction(null);
      await loadEvidence();
    } catch (error: any) { toast.error(error?.message || 'Failed to update evidence'); }
    finally { setSavingAction(false); }
  };

  const actionDialogCopy = (action?: EvidenceAction) => {
    switch (action) {
      case 'verify':
        return {
          title: 'Verify evidence',
          confirmText: 'Verify',
          reasonLabel: 'Verification notes',
          reasonPlaceholder: 'Optional notes for audit history',
          requireReason: false,
        };
      case 'reject':
        return {
          title: 'Reject evidence',
          confirmText: 'Reject',
          reasonLabel: 'Rejection reason',
          reasonPlaceholder: 'Explain why this evidence cannot satisfy the workflow requirement',
          requireReason: true,
          variant: 'destructive' as const,
        };
      case 'applyHold':
        return {
          title: 'Apply legal hold',
          confirmText: 'Apply hold',
          reasonLabel: 'Legal hold reason',
          reasonPlaceholder: 'State the audit, legal, or investigation reason for this hold',
          requireReason: true,
        };
      case 'releaseHold':
        return {
          title: 'Release legal hold',
          confirmText: 'Release hold',
          reasonLabel: 'Release reason',
          reasonPlaceholder: 'State why this hold can be released',
          requireReason: true,
          variant: 'destructive' as const,
        };
      default:
        return {
          title: 'Update evidence',
          confirmText: 'Save',
          reasonLabel: 'Reason',
          reasonPlaceholder: 'Enter a clear audit reason',
          requireReason: true,
        };
    }
  };

  const dialogCopy = actionDialogCopy(pendingAction?.action);

  return <div className="space-y-8">
    <section className="max-w-4xl space-y-5">
      <div><h3 className="font-semibold">Document evidence policy</h3>
        <p className="text-sm text-muted-foreground">Tenant-wide upload, malware scanning, and audit-retention controls.</p></div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2"><Label>Allowed file extensions</Label><Input value={extensions} onChange={event => setExtensions(event.target.value)} placeholder=".pdf, .png, .jpg" /></div>
        <div><Label>Maximum file size (MB)</Label><Input type="number" min="1" value={Math.round(policy.maximumFileSizeBytes / 1024 / 1024)} onChange={event => setPolicy({...policy, maximumFileSizeBytes: Number(event.target.value) * 1024 * 1024})} /></div>
        <div><Label>Retention days</Label><Input type="number" min="2555" value={policy.retentionDays} onChange={event => setPolicy({...policy, retentionDays: Number(event.target.value)})} /></div>
        <label className="sm:col-span-2 flex items-center gap-3 text-sm"><Switch checked={policy.requireMalwareScan} onCheckedChange={checked => setPolicy({...policy, requireMalwareScan: checked})} />Require malware scanning before evidence can satisfy a workflow requirement</label>
      </div>
      <Button onClick={() => void save()} disabled={saving}><FileCheck2 className="mr-2 h-4 w-4" />Save evidence policy</Button>
    </section>

    <section className="space-y-4 border-t pt-6">
      <div><h3 className="font-semibold">Evidence review</h3>
        <p className="text-sm text-muted-foreground">Select a workflow instance and step to review versions, verification state, hashes, retention, and legal holds.</p></div>
      <div className="grid gap-3 lg:grid-cols-[1fr_220px_220px_auto]">
        <Input value={reviewSearch} onChange={event => setReviewSearch(event.target.value)} placeholder="Search workflow, entity type, or entity ID" />
        <Select value={reviewWorkflowDefinitionId} onValueChange={setReviewWorkflowDefinitionId}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All workflows</SelectItem>{scopeOptions.workflowDefinitions.map(item => <SelectItem key={item.id} value={item.id}>{item.name} v{item.version}</SelectItem>)}</SelectContent></Select>
        <Select value={reviewEntityType} onValueChange={setReviewEntityType}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All entity types</SelectItem>{scopeOptions.entityTypes.map(item => <SelectItem key={item.id} value={item.name}>{item.name}</SelectItem>)}</SelectContent></Select>
        <Button variant="outline" onClick={() => void loadReviewInstances()} disabled={loadingInstances}><Search className="mr-2 h-4 w-4" />Find</Button>
      </div>
      <div className="grid gap-3 lg:grid-cols-2">
        <div><Label>Workflow instance</Label><Select value={selectedInstanceId || undefined} onValueChange={chooseInstance}><SelectTrigger><SelectValue placeholder="Select workflow instance" /></SelectTrigger><SelectContent>{reviewInstances.map(item => <SelectItem key={item.id} value={item.id}>{item.workflowName} / {item.entityType} / {String(item.entityId).slice(0, 8)}</SelectItem>)}</SelectContent></Select></div>
        <div><Label>Workflow step</Label><Select value={stepInstanceId || undefined} onValueChange={setStepInstanceId} disabled={!selectedInstance}><SelectTrigger><SelectValue placeholder="Select workflow step" /></SelectTrigger><SelectContent>{(selectedInstance?.steps || []).map(item => <SelectItem key={item.stepInstanceId} value={item.stepInstanceId}>{item.stepName} ({item.evidence.total})</SelectItem>)}</SelectContent></Select></div>
      </div>
      {selectedInstance && <div className="rounded-md border bg-muted/30 p-3 text-sm"><div className="font-medium">{selectedInstance.workflowName}</div><div className="text-muted-foreground">{selectedInstance.entityType} / {selectedInstance.entityId} / {instanceStatusLabel(selectedInstance.status)}</div></div>}
      <div className="overflow-hidden rounded-md border"><Table><TableHeader><TableRow><TableHead>Step</TableHead><TableHead>Status</TableHead><TableHead>Evidence</TableHead><TableHead>Verification</TableHead><TableHead className="text-right">Action</TableHead></TableRow></TableHeader>
        <TableBody>{!selectedInstance ? <TableRow><TableCell colSpan={5} className="py-8 text-center text-muted-foreground">Select a workflow instance.</TableCell></TableRow> : selectedInstance.steps.map(item => <TableRow key={item.stepInstanceId} className={item.stepInstanceId === stepInstanceId ? 'bg-muted/40' : ''}>
          <TableCell><div className="font-medium">{item.stepName}</div><div className="text-xs text-muted-foreground">{item.dueDate ? `Due ${new Date(item.dueDate).toLocaleString()}` : 'No due date'}</div></TableCell>
          <TableCell>{stepStatusLabel(item.status)}</TableCell>
          <TableCell>{item.evidence.total} document{item.evidence.total === 1 ? '' : 's'}</TableCell>
          <TableCell><div className="flex flex-wrap gap-1"><Badge variant="secondary">{item.evidence.pending} pending</Badge><Badge variant="default">{item.evidence.verified} verified</Badge>{item.evidence.rejected > 0 ? <Badge variant="destructive">{item.evidence.rejected} rejected</Badge> : null}{item.evidence.legalHold > 0 ? <Badge variant="destructive">{item.evidence.legalHold} hold</Badge> : null}</div></TableCell>
          <TableCell className="text-right"><Button size="sm" variant="outline" onClick={() => setStepInstanceId(item.stepInstanceId)}>Review</Button></TableCell>
        </TableRow>)}</TableBody></Table></div>
      {selectedStep && <div className="text-sm text-muted-foreground">Reviewing evidence for <span className="font-medium text-foreground">{selectedStep.stepName}</span>.</div>}
      <div className="overflow-hidden rounded-md border"><Table><TableHeader><TableRow><TableHead>Document</TableHead><TableHead>Version</TableHead><TableHead>Integrity</TableHead><TableHead>Verification</TableHead><TableHead>Retention</TableHead><TableHead className="text-right">Actions</TableHead></TableRow></TableHeader>
        <TableBody>{loadingEvidence ? <TableRow><TableCell colSpan={6} className="py-8 text-center text-muted-foreground">Loading evidence...</TableCell></TableRow> : evidence.length === 0 ? <TableRow><TableCell colSpan={6} className="py-8 text-center text-muted-foreground">No evidence loaded.</TableCell></TableRow> : evidence.map(item => <TableRow key={item.id}>
          <TableCell><div className="font-medium">{item.documentName || item.fileName}</div><div className="text-xs text-muted-foreground">{item.documentType || 'Document'}{item.isExpired ? ' / Expired' : ''}</div></TableCell>
          <TableCell>v{item.version}{item.isCurrent ? <Badge variant="outline" className="ml-2">Current</Badge> : null}</TableCell>
          <TableCell><div className="max-w-32 truncate font-mono text-xs" title={item.sha256}>{item.sha256}</div><div className="text-xs text-muted-foreground">Scan {item.malwareScanStatus === 1 ? 'clean' : item.malwareScanStatus === 2 ? 'failed' : 'pending'}</div></TableCell>
          <TableCell><Badge variant={item.verificationStatus === 1 ? 'default' : item.verificationStatus === 2 ? 'destructive' : 'secondary'}>{verificationLabel(item.verificationStatus)}</Badge></TableCell>
          <TableCell>
            <div>{new Date(item.retainUntil).toLocaleDateString()}</div>
            {item.isLegalHold ? (
              <div className="mt-1 space-y-1">
                <Badge variant="destructive" title={item.legalHoldReason || undefined}>Legal hold</Badge>
                {item.legalHoldAt ? <div className="text-xs text-muted-foreground">{new Date(item.legalHoldAt).toLocaleString()}</div> : null}
                {item.legalHoldReason ? <div className="max-w-40 truncate text-xs text-muted-foreground" title={item.legalHoldReason}>{item.legalHoldReason}</div> : null}
              </div>
            ) : null}
          </TableCell>
          <TableCell><div className="flex justify-end gap-1"><Button size="sm" variant="outline" onClick={() => openEvidenceAction(item, 'verify')}>Verify</Button><Button size="sm" variant="outline" onClick={() => openEvidenceAction(item, 'reject')}>Reject</Button>
            <Button size="icon" variant="ghost" title={item.isLegalHold ? 'Release legal hold' : 'Apply legal hold'} onClick={() => openEvidenceAction(item, item.isLegalHold ? 'releaseHold' : 'applyHold')}>{item.isLegalHold ? <ShieldOff className="h-4 w-4" /> : <ShieldCheck className="h-4 w-4" />}</Button></div></TableCell>
        </TableRow>)}</TableBody></Table></div>
    </section>

    <WorkflowReasonDialog
      open={!!pendingAction}
      onOpenChange={open => !open && setPendingAction(null)}
      title={dialogCopy.title}
      description={pendingAction ? (
        <div className="space-y-2">
          <p className="text-sm text-muted-foreground">
            This action will be recorded against the workflow evidence history.
          </p>
          <div className="rounded-md border bg-muted/30 p-3 text-sm">
            <div className="font-medium text-foreground">
              {pendingAction.item.documentName || pendingAction.item.fileName}
            </div>
            <div className="text-muted-foreground">
              {pendingAction.item.documentType || 'Document'} / v{pendingAction.item.version}
            </div>
          </div>
        </div>
      ) : undefined}
      reasonLabel={dialogCopy.reasonLabel}
      reasonPlaceholder={dialogCopy.reasonPlaceholder}
      confirmText={dialogCopy.confirmText}
      requireReason={dialogCopy.requireReason}
      variant={dialogCopy.variant}
      isLoading={savingAction}
      onConfirm={submitEvidenceAction}
    />
  </div>;
}
