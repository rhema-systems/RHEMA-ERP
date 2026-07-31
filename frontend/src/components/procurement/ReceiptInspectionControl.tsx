'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, CheckCircle2, Clock3, Loader2, RefreshCw, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { Textarea } from '@/components/ui/textarea';
import {
  purchasingService,
  type ProcurementReceiptInspectionEvidenceKind,
  type ProcurementReceiptInspectionEvidenceRequest,
  type ProcurementReceiptInspectionOverviewDto,
  type ProcurementReceiptResolutionKind,
} from '@/services/purchasingService';

type LineDraft = {
  purchaseOrderReceiptItemId: string;
  acceptedQuantity: string;
  rejectedQuantity: string;
  rejectionReason: string;
  inspectionNotes: string;
  quarantineLocationId: string;
};

type Props = {
  receiptId: string;
  initialOverview?: ProcurementReceiptInspectionOverviewDto;
  external?: boolean;
  onChanged?: () => void;
};

const statusLabels = [
  'Draft', 'Pending approval', 'Approved', 'Rejected', 'Quality hold',
  'Return pending', 'Replacement pending', 'Closure ready', 'Closed',
  'Revalidation failed', 'Cancelled',
];
const acknowledgementLabels = ['Not required', 'Pending', 'Acknowledged', 'Disputed'];
const resolutionLabels = [
  'Not required', 'Required', 'Authorized', 'Dispatched',
  'Replacement requested', 'Replacement received', 'Closed',
];
const actionLabels = [
  'Created', 'Saved', 'Submitted', 'Approved', 'Rejected', 'Rejection note issued',
  'Supplier acknowledged', 'Supplier disputed', 'Return authorized', 'Return dispatched',
  'Replacement requested', 'Replacement received', 'Closed', 'Revalidation failed', 'Cancelled',
];

const requestKey = (prefix: string) =>
  `${prefix}-${typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : Date.now()}`;

const messageOf = (error: unknown) =>
  error instanceof Error ? error.message : 'The request could not be completed.';

export function ReceiptInspectionControl({ receiptId, initialOverview, external = false, onChanged }: Props) {
  const [overview, setOverview] = useState<ProcurementReceiptInspectionOverviewDto | null>(initialOverview ?? null);
  const [loading, setLoading] = useState(!initialOverview);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lines, setLines] = useState<LineDraft[]>([]);
  const [comment, setComment] = useState('');
  const [reference, setReference] = useState('');
  const [resolutionKind, setResolutionKind] = useState<ProcurementReceiptResolutionKind>(1);
  const [evidenceKind, setEvidenceKind] = useState<ProcurementReceiptInspectionEvidenceKind>(1);
  const [evidenceId, setEvidenceId] = useState('');
  const [evidenceReference, setEvidenceReference] = useState('');
  const [requirementKey, setRequirementKey] = useState('INSPECTION_REPORT');

  const current = overview?.current;

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      setOverview(await purchasingService.getReceiptInspectionControl(receiptId));
    } catch (loadError) {
      setError(messageOf(loadError));
    } finally {
      setLoading(false);
    }
  }, [receiptId]);

  useEffect(() => {
    if (!initialOverview) void load();
  }, [initialOverview, load]);

  useEffect(() => {
    setOverview(initialOverview ?? null);
  }, [initialOverview]);

  useEffect(() => {
    setLines((current?.lines ?? []).map((line) => ({
      purchaseOrderReceiptItemId: line.purchaseOrderReceiptItemId,
      acceptedQuantity: String(line.acceptedQuantity),
      rejectedQuantity: String(line.rejectedQuantity),
      rejectionReason: line.rejectionReason ?? '',
      inspectionNotes: line.inspectionNotes ?? '',
      quarantineLocationId: line.quarantineLocationId ?? '',
    })));
    if (current?.resolutionKind === 1 || current?.resolutionKind === 2)
      setResolutionKind(current.resolutionKind);
  }, [current]);

  const totals = useMemo(() => lines.reduce(
    (value, line) => ({
      accepted: value.accepted + (Number(line.acceptedQuantity) || 0),
      rejected: value.rejected + (Number(line.rejectedQuantity) || 0),
    }),
    { accepted: 0, rejected: 0 }
  ), [lines]);

  const controlledEvidence = (actionKey: string): ProcurementReceiptInspectionEvidenceRequest[] => {
    const id = evidenceId.trim();
    if (!id || !evidenceReference.trim() || !requirementKey.trim())
      throw new Error('Evidence ID, evidence reference, and requirement key are required.');
    return [{
      actionKey,
      requirementKey: requirementKey.trim(),
      referenceKind: evidenceKind,
      workflowEvidenceDocumentId: evidenceKind === 0 ? id : undefined,
      fileUploadRecordId: evidenceKind === 1 ? id : undefined,
      evidenceReference: evidenceReference.trim(),
    }];
  };

  const run = async (work: () => Promise<unknown>, success: string) => {
    try {
      setBusy(true);
      await work();
      toast.success(success);
      setComment('');
      setReference('');
      setEvidenceId('');
      setEvidenceReference('');
      await load();
      onChanged?.();
    } catch (operationError) {
      toast.error(messageOf(operationError));
    } finally {
      setBusy(false);
    }
  };

  const save = () => {
    if (!current || !comment.trim()) return toast.error('Inspection comments are required.');
    return run(() => purchasingService.saveReceiptInspection(receiptId, {
      comment: comment.trim(),
      idempotencyKey: requestKey('receipt-inspection-save'),
      rowVersion: current.rowVersion,
      lines: lines.map((line) => ({
        purchaseOrderReceiptItemId: line.purchaseOrderReceiptItemId,
        acceptedQuantity: Number(line.acceptedQuantity),
        rejectedQuantity: Number(line.rejectedQuantity),
        rejectionReason: line.rejectionReason.trim() || undefined,
        inspectionNotes: line.inspectionNotes.trim() || undefined,
        quarantineLocationId: line.quarantineLocationId.trim() || undefined,
      })),
    }), 'Inspection quantities saved.');
  };

  if (loading) return (
    <Card data-testid="receipt-inspection-loading"><CardContent className="flex items-center gap-2 py-10 text-sm text-muted-foreground">
      <Loader2 className="h-4 w-4 animate-spin" /> Loading governed inspection…
    </CardContent></Card>
  );

  if (error) return (
    <Alert variant="destructive" data-testid="receipt-inspection-error">
      <AlertTriangle className="h-4 w-4" /><AlertTitle>Inspection control unavailable</AlertTitle>
      <AlertDescription className="space-y-3"><p>{error}</p><Button variant="outline" size="sm" onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Retry</Button></AlertDescription>
    </Alert>
  );

  if (!overview || !current) return (
    <Card data-testid="receipt-inspection-empty">
      <CardHeader><CardTitle>Governed receipt inspection</CardTitle><CardDescription>No inspection case exists for this receipt.</CardDescription></CardHeader>
      {!external && <CardContent><Button disabled={busy} onClick={() => void run(() => purchasingService.initializeReceiptInspection(receiptId), 'Inspection initialized.')}>
        {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Initialize inspection
      </Button></CardContent>}
    </Card>
  );

  return (
    <div className="space-y-5" data-testid={external ? 'supplier-receipt-inspection-control' : 'receipt-inspection-control'}>
      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div><CardTitle className="flex items-center gap-2"><ShieldCheck className="h-5 w-5" />Governed receipt inspection</CardTitle>
              <CardDescription>{overview.receiptNumber} · PO {overview.purchaseOrderNumber} · {overview.supplierName}</CardDescription></div>
            <div className="flex gap-2"><Badge>{statusLabels[current.status] ?? current.status}</Badge>{current.qualityHold && <Badge variant="destructive">Quality hold</Badge>}
              <Button variant="outline" size="sm" disabled={busy} onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>
          </div>
        </CardHeader>
        <CardContent className="space-y-5">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {[['Received', current.receivedQuantity], ['Accepted / stock eligible', current.acceptedQuantity], ['Rejected / quarantined', current.rejectedQuantity], ['Pending', current.pendingQuantity]].map(([label, value]) =>
              <div key={String(label)} className="rounded-lg border p-3"><div className="text-xs text-muted-foreground">{label}</div><div className="text-xl font-semibold">{Number(value).toLocaleString()}</div></div>)}
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="rounded-lg bg-muted/50 p-3 text-sm">AP eligible <strong>{current.apEligibleQuantity}</strong></div>
            <div className="rounded-lg bg-muted/50 p-3 text-sm">AP blocked <strong>{current.apBlockedQuantity}</strong></div>
            <div className="rounded-lg bg-muted/50 p-3 text-sm">Stock posted <strong>{current.stockPostedQuantity}</strong></div>
          </div>
          {current.qualityHoldReason && <Alert><AlertTriangle className="h-4 w-4" /><AlertTitle>{current.rejectionNoteNumber ?? 'Quality hold'}</AlertTitle><AlertDescription>{current.qualityHoldReason}</AlertDescription></Alert>}
          <div className="flex flex-wrap gap-2 text-xs"><Badge variant="outline">Supplier: {acknowledgementLabels[current.supplierAcknowledgementStatus]}</Badge><Badge variant="outline">Resolution: {resolutionLabels[current.resolutionStatus]}</Badge>{overview.decisionKeys.map((key) => <Badge key={key} variant="secondary">{key}</Badge>)}</div>
        </CardContent>
      </Card>

      {!external && <Card>
        <CardHeader><CardTitle className="text-base">Inspection lines</CardTitle><CardDescription>Accepted quantities are posted only after independent workflow approval. Rejected quantities require a reason and same-warehouse quarantine location ID.</CardDescription></CardHeader>
        <CardContent className="space-y-4">
          {lines.map((line, index) => {
            const source = current.lines[index];
            return <div key={line.purchaseOrderReceiptItemId} className="rounded-lg border p-4 space-y-3">
              <div className="flex justify-between gap-3"><div className="font-medium">{source.itemCode} · {source.itemName}</div><Badge variant="outline">Received {source.receivedQuantity} {source.unitOfMeasure}</Badge></div>
              <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-5">
                <div><Label>Accepted</Label><Input type="number" min="0" step="0.0001" disabled={!overview.canEdit || busy} value={line.acceptedQuantity} onChange={(event) => setLines((rows) => rows.map((row, i) => i === index ? { ...row, acceptedQuantity: event.target.value } : row))} /></div>
                <div><Label>Rejected</Label><Input type="number" min="0" step="0.0001" disabled={!overview.canEdit || busy} value={line.rejectedQuantity} onChange={(event) => setLines((rows) => rows.map((row, i) => i === index ? { ...row, rejectedQuantity: event.target.value } : row))} /></div>
                <div><Label>Rejection reason</Label><Input disabled={!overview.canEdit || busy} value={line.rejectionReason} onChange={(event) => setLines((rows) => rows.map((row, i) => i === index ? { ...row, rejectionReason: event.target.value } : row))} /></div>
                <div><Label>Quarantine location ID</Label><Input disabled={!overview.canEdit || busy} value={line.quarantineLocationId} onChange={(event) => setLines((rows) => rows.map((row, i) => i === index ? { ...row, quarantineLocationId: event.target.value } : row))} /></div>
                <div><Label>Inspection notes</Label><Input disabled={!overview.canEdit || busy} value={line.inspectionNotes} onChange={(event) => setLines((rows) => rows.map((row, i) => i === index ? { ...row, inspectionNotes: event.target.value } : row))} /></div>
              </div>
            </div>;
          })}
          {overview.canEdit && <div className="flex flex-wrap items-end justify-between gap-3"><div className="text-sm">Draft totals: accepted {totals.accepted}, rejected {totals.rejected}</div><Button disabled={busy || !comment.trim()} onClick={() => void save()}>Save inspection</Button></div>}
        </CardContent>
      </Card>}

      {(overview.canSubmit || overview.canAcknowledge || overview.canResolve || overview.canClose) && <Card>
        <CardHeader><CardTitle className="text-base">Controlled evidence</CardTitle><CardDescription>Reference an already-retained workflow evidence document or malware-clean central DMS upload.</CardDescription></CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
          <div><Label>Evidence source</Label><Select value={String(evidenceKind)} onValueChange={(value) => setEvidenceKind(Number(value) as ProcurementReceiptInspectionEvidenceKind)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="1">Central DMS upload</SelectItem><SelectItem value="0">Workflow evidence</SelectItem></SelectContent></Select></div>
          <div><Label>{evidenceKind === 1 ? 'File upload record ID' : 'Workflow evidence ID'}</Label><Input value={evidenceId} onChange={(event) => setEvidenceId(event.target.value)} /></div>
          <div><Label>Requirement key</Label><Input value={requirementKey} onChange={(event) => setRequirementKey(event.target.value)} /></div>
          <div><Label>Evidence reference</Label><Input value={evidenceReference} onChange={(event) => setEvidenceReference(event.target.value)} /></div>
        </CardContent>
      </Card>}

      <Card>
        <CardHeader><CardTitle className="text-base">Lifecycle action</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div><Label>Comments</Label><Textarea value={comment} onChange={(event) => setComment(event.target.value)} placeholder="State the inspection, decision, acknowledgement, or closure basis." /></div>
          {(overview.canAcknowledge || overview.canResolve || overview.canClose) && <div><Label>Reference</Label><Input value={reference} onChange={(event) => setReference(event.target.value)} placeholder="Supplier reference, dispatch note, replacement receipt, or closure reference" /></div>}
          {overview.canResolve && <div className="max-w-xs"><Label>Resolution route</Label><Select value={String(resolutionKind)} onValueChange={(value) => setResolutionKind(Number(value) as ProcurementReceiptResolutionKind)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="1">Return</SelectItem><SelectItem value="2">Replacement</SelectItem></SelectContent></Select></div>}
          <div className="flex flex-wrap gap-2">
            {overview.canSubmit && <Button disabled={busy || !comment.trim()} onClick={() => void run(() => purchasingService.submitReceiptInspection(current.id, { comment: comment.trim(), rowVersion: current.rowVersion, evidence: controlledEvidence('SubmitReceiptInspection') }), 'Inspection submitted for independent approval.')}>Submit</Button>}
            {overview.canDecide && <><Button disabled={busy || !comment.trim()} onClick={() => void run(() => purchasingService.decideReceiptInspection(current.id, { approved: true, comment: comment.trim(), rowVersion: current.rowVersion }), 'Inspection approved and accepted stock posted.')}>Approve</Button><Button variant="destructive" disabled={busy || !comment.trim()} onClick={() => void run(() => purchasingService.decideReceiptInspection(current.id, { approved: false, comment: comment.trim(), rowVersion: current.rowVersion }), 'Inspection rejected by workflow.')}>Reject</Button></>}
            {overview.canAcknowledge && <><Button disabled={busy || !comment.trim() || !reference.trim()} onClick={() => void run(() => purchasingService.acknowledgeReceiptInspection(current.id, { acknowledged: true, reference: reference.trim(), comment: comment.trim(), idempotencyKey: requestKey('supplier-ack'), rowVersion: current.rowVersion, evidence: controlledEvidence('SupplierAcknowledgement') }), 'Rejection note acknowledged.')}>Acknowledge rejection</Button><Button variant="destructive" disabled={busy || !comment.trim() || !reference.trim()} onClick={() => void run(() => purchasingService.acknowledgeReceiptInspection(current.id, { acknowledged: false, reference: reference.trim(), comment: comment.trim(), idempotencyKey: requestKey('supplier-dispute'), rowVersion: current.rowVersion, evidence: evidenceId.trim() ? controlledEvidence('SupplierDispute') : [] }), 'Rejection note disputed.')}>Dispute</Button></>}
            {overview.canResolve && <Button disabled={busy || !comment.trim() || !reference.trim()} onClick={() => void run(() => purchasingService.resolveReceiptInspection(current.id, { resolutionKind, reference: reference.trim(), comment: comment.trim(), idempotencyKey: requestKey('receipt-resolution'), rowVersion: current.rowVersion, evidence: controlledEvidence('ResolutionEvidence') }), 'Return or replacement progression recorded.')}>Progress {resolutionKind === 1 ? 'return' : 'replacement'}</Button>}
            {overview.canClose && <Button disabled={busy || !comment.trim() || !reference.trim()} onClick={() => void run(() => purchasingService.closeReceiptInspection(current.id, { resolutionKind: current.resolutionKind, reference: reference.trim(), comment: comment.trim(), idempotencyKey: requestKey('receipt-closure'), rowVersion: current.rowVersion, evidence: controlledEvidence('ClosureEvidence') }), 'Inspection and quality hold closed.')}>Close case</Button>}
            {busy && <Loader2 className="h-5 w-5 animate-spin self-center" />}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base flex items-center gap-2"><Clock3 className="h-4 w-4" />Immutable action history</CardTitle></CardHeader>
        <CardContent>{current.actions.length === 0 ? <p className="text-sm text-muted-foreground">No actions recorded.</p> : <div className="space-y-3">{[...current.actions].sort((a, b) => b.sequence - a.sequence).map((action) => <div key={action.id} className="flex gap-3 rounded-lg border p-3"><CheckCircle2 className="mt-0.5 h-4 w-4 text-emerald-600" /><div className="min-w-0"><div className="font-medium">#{action.sequence} {actionLabels[action.actionType] ?? action.actionType}</div><div className="text-sm text-muted-foreground">{action.comment}</div><div className="text-xs text-muted-foreground">{action.actorName} · {new Date(action.occurredAtUtc).toLocaleString()} · {action.reference}</div></div></div>)}</div>}</CardContent>
      </Card>
      <Separator />
      <p className="text-xs text-muted-foreground">Profile and policy lineage: {overview.decisionKeys.join(', ')}. Acceptance, stock, supplier response, and AP eligibility remain traceable to this case.</p>
    </div>
  );
}
