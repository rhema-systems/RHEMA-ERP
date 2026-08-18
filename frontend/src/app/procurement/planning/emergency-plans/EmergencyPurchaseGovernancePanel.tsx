'use client';

import { useEffect, useState } from 'react';
import { CheckCircle2, FileCheck2, Loader2, Send, ShieldCheck, Siren } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  emergencyProcurementPlanService,
  type EmergencyProcurementPlanDetailDto,
  type EmergencyPurchaseGovernanceOptionsDto,
} from '@/services/procurementPlanningService';

interface Props {
  plan: EmergencyProcurementPlanDetailDto;
  onUpdated: (plan: EmergencyProcurementPlanDetailDto) => void;
}

export default function EmergencyPurchaseGovernancePanel({ plan, onUpdated }: Props) {
  const { hasPermission, hasRole } = useAuth();
  const canManage = hasPermission('procurement.sourcing.manage');
  const canApprove = hasPermission('procurement.sourcing.approve') &&
    (hasRole('TDC_MANAGING_DIRECTOR') || hasRole('TDC_BOARD_APPROVER'));
  const canAudit = hasRole('TDC_INTERNAL_AUDIT') && hasPermission('procurement.audit.read');
  const [options, setOptions] = useState<EmergencyPurchaseGovernanceOptionsDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [requisitionId, setRequisitionId] = useState('');
  const [ruleId, setRuleId] = useState('');
  const [documentVersionId, setDocumentVersionId] = useState('');
  const [justification, setJustification] = useState('');
  const [note, setNote] = useState('');
  const [approvalReference, setApprovalReference] = useState('');
  const [decision, setDecision] = useState<'Approve' | 'Reject'>('Approve');
  const [exceptionalTenderId, setExceptionalTenderId] = useState('');

  const needsOptions = canManage && (plan.status === 'Draft' || plan.status === 'Triggered');
  useEffect(() => {
    if (!needsOptions) return;
    emergencyProcurementPlanService.getGovernanceOptions()
      .then(setOptions)
      .catch((error) => toast.error(error instanceof Error ? error.message : 'Failed to load governed options'));
  }, [needsOptions]);

  const execute = async (action: () => Promise<EmergencyProcurementPlanDetailDto>, success: string) => {
    try {
      setBusy(true);
      const updated = await action();
      onUpdated(updated);
      toast.success(success);
      setNote('');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'The emergency-purchase action failed');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <CardTitle className="text-base">Emergency purchase control</CardTitle>
          <Badge variant="outline">{plan.status}</Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {plan.purchaseRequisitionNumber && (
          <div className="grid gap-3 rounded-md border p-3 text-sm md:grid-cols-4">
            <div><span className="text-muted-foreground">Requisition</span><div className="font-medium">{plan.purchaseRequisitionNumber}</div></div>
            <div><span className="text-muted-foreground">Rule</span><div className="font-medium">{plan.exceptionRuleCode}</div></div>
            <div><span className="text-muted-foreground">Authority</span><div className="font-medium">{plan.approvalAuthority || '-'}</div></div>
            <div><span className="text-muted-foreground">Evidence</span><div className="break-all font-medium">{plan.evidenceReference}</div></div>
          </div>
        )}

        {plan.status === 'Draft' && canManage && (
          <div className="grid gap-3 lg:grid-cols-2">
            <div className="space-y-1.5">
              <Label>Draft purchase requisition</Label>
              <Select value={requisitionId} onValueChange={setRequisitionId}>
                <SelectTrigger><SelectValue placeholder="Select requisition" /></SelectTrigger>
                <SelectContent>{options?.requisitions.map(item => (
                  <SelectItem key={item.id} value={item.id}>{item.requisitionNumber} · {item.category || 'Unclassified'} · {item.currency} {item.totalAmount.toLocaleString()}</SelectItem>
                ))}</SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label>Effective emergency exception rule</Label>
              <Select value={ruleId} onValueChange={setRuleId}>
                <SelectTrigger><SelectValue placeholder="Select policy rule" /></SelectTrigger>
                <SelectContent>{options?.exceptionRules.map(item => (
                  <SelectItem key={item.id} value={item.id}>{item.ruleCode} · {item.name} · {item.approverRole}</SelectItem>
                ))}</SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5 lg:col-span-2">
              <Label>Published central DMS evidence</Label>
              <Select value={documentVersionId} onValueChange={setDocumentVersionId}>
                <SelectTrigger><SelectValue placeholder="Select clean published document" /></SelectTrigger>
                <SelectContent>{options?.evidenceDocuments.map(item => (
                  <SelectItem key={item.versionId} value={item.versionId}>{item.reference} · {item.title}</SelectItem>
                ))}</SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5 lg:col-span-2">
              <Label>Emergency reason</Label>
              <Textarea value={justification} onChange={event => setJustification(event.target.value)} maxLength={2000} />
            </div>
            <Button className="w-fit" disabled={busy || !requisitionId || !ruleId || !documentVersionId || justification.trim().length < 20}
              onClick={() => execute(() => emergencyProcurementPlanService.prepareException(plan.id, {
                purchaseRequisitionId: requisitionId,
                exceptionRuleId: ruleId,
                centralDocumentVersionId: documentVersionId,
                justification: justification.trim(),
                rowVersion: plan.rowVersion,
              }), 'Emergency exception prepared')}>
              {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <FileCheck2 className="mr-2 h-4 w-4" />}Prepare exception
            </Button>
          </div>
        )}

        {plan.status === 'Prepared' && canManage && (
          <Button disabled={busy} onClick={() => execute(
            () => emergencyProcurementPlanService.submitForAudit(plan.id, plan.rowVersion, note || undefined),
            'Submitted to Internal Audit',
          )}><Send className="mr-2 h-4 w-4" />Submit to Internal Audit</Button>
        )}

        {plan.status === 'PendingAudit' && canAudit && (
          <div className="space-y-3">
            <div className="space-y-1.5"><Label>Internal Audit vouch note</Label><Textarea value={note} onChange={event => setNote(event.target.value)} maxLength={1000} /></div>
            <Button disabled={busy || note.trim().length < 10} onClick={() => execute(
              () => emergencyProcurementPlanService.vouch(plan.id, plan.rowVersion, note.trim()),
              'Emergency exception vouched',
            )}><ShieldCheck className="mr-2 h-4 w-4" />Vouch evidence</Button>
          </div>
        )}

        {plan.status === 'AuditVouched' && canManage && (
          <Button disabled={busy} onClick={() => execute(
            () => emergencyProcurementPlanService.submitForApproval(plan.id, plan.rowVersion, note || undefined),
            'Submitted to configured authority',
          )}><Send className="mr-2 h-4 w-4" />Submit for {plan.approvalAuthority || 'authority'} approval</Button>
        )}

        {plan.status === 'PendingApproval' && canApprove && (
          <div className="grid gap-3 md:grid-cols-2">
            <div className="space-y-1.5"><Label>Decision</Label><Select value={decision} onValueChange={value => setDecision(value as 'Approve' | 'Reject')}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Approve">Approve</SelectItem><SelectItem value="Reject">Reject</SelectItem></SelectContent></Select></div>
            <div className="space-y-1.5"><Label>Authority reference</Label><Input value={approvalReference} onChange={event => setApprovalReference(event.target.value)} maxLength={200} /></div>
            <div className="space-y-1.5 md:col-span-2"><Label>Comments</Label><Textarea value={note} onChange={event => setNote(event.target.value)} maxLength={1000} /></div>
            <Button className="w-fit" disabled={busy || (decision === 'Approve' && !approvalReference.trim())} onClick={() => execute(
              () => emergencyProcurementPlanService.decide(plan.id, { rowVersion: plan.rowVersion, action: decision, approvalReference: approvalReference.trim(), comments: note || undefined }),
              `Emergency exception ${decision.toLowerCase()}d`,
            )}><CheckCircle2 className="mr-2 h-4 w-4" />Record decision</Button>
          </div>
        )}

        {plan.status === 'Approved' && canManage && (
          <Button disabled={busy} onClick={() => execute(
            () => emergencyProcurementPlanService.triggerGoverned(plan.id, plan.rowVersion, note || undefined),
            'Emergency purchase triggered',
          )}><Siren className="mr-2 h-4 w-4" />Trigger approved purchase</Button>
        )}

        {plan.status === 'Triggered' && canManage && (
          <div className="grid gap-3 lg:grid-cols-2">
            <div className="space-y-1.5"><Label>Filed exceptional-sourcing case</Label><Select value={exceptionalTenderId} onValueChange={setExceptionalTenderId}><SelectTrigger><SelectValue placeholder="Select filed case" /></SelectTrigger><SelectContent>{options?.filedExceptionalSourcing.map(item => <SelectItem key={item.tenderId} value={item.tenderId}>{item.tenderNumber} · {item.filingReference}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-1.5"><Label>Post-award central DMS evidence</Label><Select value={documentVersionId} onValueChange={setDocumentVersionId}><SelectTrigger><SelectValue placeholder="Select clean published document" /></SelectTrigger><SelectContent>{options?.evidenceDocuments.map(item => <SelectItem key={item.versionId} value={item.versionId}>{item.reference} · {item.title}</SelectItem>)}</SelectContent></Select></div>
            <div className="space-y-1.5 lg:col-span-2"><Label>Post-award justification</Label><Textarea value={justification} onChange={event => setJustification(event.target.value)} maxLength={2000} /></div>
            <Button className="w-fit" disabled={busy || !exceptionalTenderId || !documentVersionId || justification.trim().length < 20} onClick={() => execute(
              () => emergencyProcurementPlanService.filePostAward(plan.id, { rowVersion: plan.rowVersion, exceptionalSourcingTenderId: exceptionalTenderId, centralDocumentVersionId: documentVersionId, justification: justification.trim() }),
              'Post-award emergency record filed',
            )}><FileCheck2 className="mr-2 h-4 w-4" />Complete filing</Button>
          </div>
        )}

        {plan.status === 'Filed' && <div className="flex items-center gap-2 text-sm text-emerald-700"><CheckCircle2 className="h-4 w-4" />Emergency-purchase governance and post-award filing are complete.</div>}
      </CardContent>
    </Card>
  );
}
