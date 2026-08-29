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
import { documentManagementService, type CentralDocumentRecord } from '@/services/document-management.service';
import {
  inventoryManagementService,
  type WarehouseLocationDto,
} from '@/services/inventoryManagementService';
import {
  purchasingService,
  type ProcurementReceiptInspectionEvidenceKind,
  type ProcurementReceiptInspectionEvidenceRequest,
  type ProcurementReceiptInspectionOverviewDto,
  type ProcurementReceiptResolutionKind,
  type ProcurementReceiptResolutionStatus,
} from '@/services/purchasingService';

type LineDraft = {
  purchaseOrderReceiptItemId: string;
  acceptedQuantity: string;
  rejectedQuantity: string;
  rejectionReason: string;
  inspectionNotes: string;
  quarantineLocationId: string;
};

type EvidenceDraft = {
  requirementKey: string;
  evidenceKind: ProcurementReceiptInspectionEvidenceKind;
  evidenceId: string;
  evidenceReference: string;
  documentRecordId?: string;
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

const friendlyRequirement = (value: string) => value
  .replace(/[_-]+/g, ' ')
  .toLowerCase()
  .replace(/(^|\s)\S/g, (letter) => letter.toUpperCase());

export const buildReceiptInspectionEvidenceRequests = (
  actionKey: string,
  rows: EvidenceDraft[]
): ProcurementReceiptInspectionEvidenceRequest[] => rows.map((row) => {
  const id = row.evidenceId.trim();
  if (!id || !row.evidenceReference.trim())
    throw new Error(`Select current published evidence for ${friendlyRequirement(row.requirementKey)}.`);
  return {
    actionKey,
    requirementKey: row.requirementKey,
    referenceKind: row.evidenceKind,
    workflowEvidenceDocumentId: row.evidenceKind === 0 ? id : undefined,
    fileUploadRecordId: row.evidenceKind === 1 ? id : undefined,
    evidenceReference: row.evidenceReference.trim(),
  };
});

export const receiptInspectionResolutionActionKey = (
  resolutionKind: ProcurementReceiptResolutionKind,
  resolutionStatus: ProcurementReceiptResolutionStatus
) => {
  if (resolutionKind === 1 && resolutionStatus === 1) return 'ReturnAuthorization';
  if (resolutionKind === 1 && resolutionStatus === 2) return 'ReturnDispatch';
  if (resolutionKind === 2 && resolutionStatus === 1) return 'ReplacementRequest';
  if (resolutionKind === 2 && resolutionStatus === 4) return 'ReplacementReceipt';
  throw new Error('The return or replacement route is not ready for progression.');
};

export function ReceiptInspectionControl({ receiptId, initialOverview, external = false, onChanged }: Props) {
  const [overview, setOverview] = useState<ProcurementReceiptInspectionOverviewDto | null>(initialOverview ?? null);
  const [loading, setLoading] = useState(!initialOverview);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lines, setLines] = useState<LineDraft[]>([]);
  const [comment, setComment] = useState('');
  const [reference, setReference] = useState('');
  const [resolutionKind, setResolutionKind] = useState<ProcurementReceiptResolutionKind>(1);
  const [warehouseLocations, setWarehouseLocations] = useState<WarehouseLocationDto[]>([]);
  const [dmsRecords, setDmsRecords] = useState<CentralDocumentRecord[]>([]);
  const [optionsLoading, setOptionsLoading] = useState(false);
  const [evidenceRows, setEvidenceRows] = useState<EvidenceDraft[]>(() =>
    (initialOverview?.evidenceRequirementKeys?.length
      ? initialOverview.evidenceRequirementKeys
      : ['INSPECTION_REPORT']).map((requirementKey) => ({
        requirementKey,
        evidenceKind: 1,
        evidenceId: '',
        evidenceReference: '',
      })));

  const current = overview?.current;

  useEffect(() => {
    if (external || !overview?.warehouseId) {
      setWarehouseLocations([]);
      return;
    }
    let cancelled = false;
    void inventoryManagementService.getWarehouseLocations(overview.warehouseId)
      .then((rows) => {
        if (!cancelled) setWarehouseLocations(rows.filter((row) => row.isActive));
      })
      .catch((loadError) => {
        if (!cancelled) toast.error(messageOf(loadError));
      });
    return () => { cancelled = true; };
  }, [external, overview?.warehouseId]);

  useEffect(() => {
    if (!(overview?.canSubmit || overview?.canAcknowledge || overview?.canResolve || overview?.canClose)) {
      setDmsRecords([]);
      return;
    }
    let cancelled = false;
    setOptionsLoading(true);
    void documentManagementService.getRecords('Procurement')
      .then((records) => {
        if (!cancelled) setDmsRecords(records.filter((record) =>
          record.lifecycleStatus === 'Active' &&
          record.versionStatus === 'Published' &&
          Boolean(record.currentVersion)));
      })
      .catch((loadError) => {
        if (!cancelled) toast.error(messageOf(loadError));
      })
      .finally(() => {
        if (!cancelled) setOptionsLoading(false);
      });
    return () => { cancelled = true; };
  }, [overview?.canAcknowledge, overview?.canClose, overview?.canResolve, overview?.canSubmit]);

  const selectEvidence = async (index: number, documentRecordId: string) => {
    try {
      setOptionsLoading(true);
      const detail = await documentManagementService.getRecord(documentRecordId);
      const version = detail?.versions.find((item) =>
        item.versionNumber === detail.record.currentVersion &&
        item.status === 'Published' &&
        Boolean(item.fileUploadRecordId));
      if (!detail || !version?.fileUploadRecordId)
        throw new Error('Select a central-DMS record with a current published file version.');
      const fileUploadRecordId = version.fileUploadRecordId;
      setEvidenceRows((rows) => rows.map((row, rowIndex) => rowIndex === index ? {
        ...row,
        evidenceKind: 1,
        documentRecordId,
        evidenceId: fileUploadRecordId,
        evidenceReference: `${detail.record.documentReference} / ${version.versionNumber}`,
      } : row));
    } catch (loadError) {
      toast.error(messageOf(loadError));
    } finally {
      setOptionsLoading(false);
    }
  };

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

  const evidenceRequirementKeys = useMemo(() => {
    const configured = overview?.evidenceRequirementKeys ?? [];
    return configured.length > 0 ? configured : ['INSPECTION_REPORT'];
  }, [overview?.evidenceRequirementKeys]);

  useEffect(() => {
    setEvidenceRows((existing) => evidenceRequirementKeys.map((requirementKey) => {
      const currentRow = existing.find((row) => row.requirementKey === requirementKey);
      return currentRow ?? {
        requirementKey,
        evidenceKind: 1,
        evidenceId: '',
        evidenceReference: '',
      };
    }));
  }, [evidenceRequirementKeys]);

  const totals = useMemo(() => lines.reduce(
    (value, line) => ({
      accepted: value.accepted + (Number(line.acceptedQuantity) || 0),
      rejected: value.rejected + (Number(line.rejectedQuantity) || 0),
    }),
    { accepted: 0, rejected: 0 }
  ), [lines]);

  const controlledEvidence = (actionKey: string) =>
    buildReceiptInspectionEvidenceRequests(actionKey, evidenceRows);

  const run = async (work: () => Promise<unknown>, success: string) => {
    try {
      setBusy(true);
      await work();
      toast.success(success);
      setComment('');
      setReference('');
      setEvidenceRows((rows) => rows.map((row) => ({
        ...row,
        evidenceId: '',
        evidenceReference: '',
      })));
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
        <CardHeader><CardTitle className="text-base">Inspection lines</CardTitle><CardDescription>Accepted quantities are posted only after independent workflow approval. Select a controlled same-warehouse quarantine location for rejected quantities.</CardDescription></CardHeader>
        <CardContent className="space-y-4">
          {lines.map((line, index) => {
            const source = current.lines[index];
            return <div key={line.purchaseOrderReceiptItemId} className="rounded-lg border p-4 space-y-3">
              <div className="flex justify-between gap-3"><div className="font-medium">{source.itemCode} · {source.itemName}</div><Badge variant="outline">Received {source.receivedQuantity} {source.unitOfMeasure}</Badge></div>
              <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-5">
                <div><Label>Accepted</Label><Input type="number" min="0" step="0.0001" disabled={!overview.canEdit || busy} value={line.acceptedQuantity} onChange={(event) => setLines((rows) => rows.map((row, i) => i === index ? { ...row, acceptedQuantity: event.target.value } : row))} /></div>
                <div><Label>Rejected</Label><Input type="number" min="0" step="0.0001" disabled={!overview.canEdit || busy} value={line.rejectedQuantity} onChange={(event) => setLines((rows) => rows.map((row, i) => i === index ? { ...row, rejectedQuantity: event.target.value } : row))} /></div>
                <div><Label>Rejection reason</Label><Input disabled={!overview.canEdit || busy} value={line.rejectionReason} onChange={(event) => setLines((rows) => rows.map((row, i) => i === index ? { ...row, rejectionReason: event.target.value } : row))} /></div>
                <div><Label>Quarantine location</Label><Select disabled={!overview.canEdit || busy} value={line.quarantineLocationId || '__none__'} onValueChange={(value) => setLines((rows) => rows.map((row, i) => i === index ? { ...row, quarantineLocationId: value === '__none__' ? '' : value } : row))}><SelectTrigger><SelectValue placeholder="Select location" /></SelectTrigger><SelectContent><SelectItem value="__none__">Not required</SelectItem>{warehouseLocations.map((location) => <SelectItem key={location.id} value={location.id}>{location.locationCode} · {location.name || location.locationType}</SelectItem>)}</SelectContent></Select></div>
                <div><Label>Inspection notes</Label><Input disabled={!overview.canEdit || busy} value={line.inspectionNotes} onChange={(event) => setLines((rows) => rows.map((row, i) => i === index ? { ...row, inspectionNotes: event.target.value } : row))} /></div>
              </div>
            </div>;
          })}
          {overview.canEdit && <div className="flex flex-wrap items-end justify-between gap-3"><div className="text-sm">Draft totals: accepted {totals.accepted}, rejected {totals.rejected}</div><Button disabled={busy || !comment.trim()} onClick={() => void save()}>Save inspection</Button></div>}
        </CardContent>
      </Card>}

      {(overview.canSubmit || overview.canAcknowledge || overview.canResolve || overview.canClose) && <Card>
        <CardHeader><CardTitle className="text-base">Controlled evidence</CardTitle><CardDescription>Select the current published central-DMS document for each configured receipt requirement.</CardDescription></CardHeader>
        <CardContent className="space-y-4">
          {evidenceRows.map((row, index) => <div key={row.requirementKey} className="rounded-lg border p-3">
            <div className="mb-3 flex items-center justify-between gap-2"><Label>{friendlyRequirement(row.requirementKey)}</Label><Badge variant="outline">Required evidence</Badge></div>
            <div className="grid gap-3 md:grid-cols-2">
              <div><Label>Published DMS document</Label><Select disabled={busy || optionsLoading} value={row.documentRecordId || '__none__'} onValueChange={(value) => { if (value !== '__none__') void selectEvidence(index, value); }}><SelectTrigger><SelectValue placeholder="Select evidence" /></SelectTrigger><SelectContent><SelectItem value="__none__">Select current evidence</SelectItem>{dmsRecords.map((record) => <SelectItem key={record.id} value={record.id}>{record.documentReference} · {record.title}</SelectItem>)}</SelectContent></Select></div>
              <div><Label>Linked evidence</Label><Input readOnly value={row.evidenceReference} placeholder={optionsLoading ? 'Loading controlled documents…' : 'No evidence selected'} /></div>
            </div>
          </div>)}
        </CardContent>
      </Card>}

      <Card>
        <CardHeader><CardTitle className="text-base">Lifecycle action</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div><Label>Comments</Label><Textarea value={comment} onChange={(event) => setComment(event.target.value)} placeholder="State the inspection, decision, acknowledgement, or closure basis." /></div>
          {(overview.canAcknowledge || overview.canResolve || overview.canClose) && <div><Label>Reference</Label><Input value={reference} onChange={(event) => setReference(event.target.value)} placeholder="Supplier reference, dispatch note, replacement receipt, or closure reference" /></div>}
          {overview.canResolve && <div className="max-w-xs"><Label>Resolution route</Label><Select value={String(resolutionKind)} onValueChange={(value) => setResolutionKind(Number(value) as ProcurementReceiptResolutionKind)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="1">Return</SelectItem><SelectItem value="2">Replacement</SelectItem></SelectContent></Select></div>}
          <div className="flex flex-wrap gap-2">
            {!external && current.status === 3 && <Button disabled={busy} onClick={() => void run(() => purchasingService.initializeReceiptInspection(receiptId), 'Replacement inspection initialized.')}>
              Reinitialize inspection
            </Button>}
            {overview.canSubmit && <Button disabled={busy || !comment.trim()} onClick={() => void run(() => purchasingService.submitReceiptInspection(current.id, { comment: comment.trim(), rowVersion: current.rowVersion, evidence: controlledEvidence('SubmitReceiptInspection') }), 'Inspection submitted for independent approval.')}>Submit</Button>}
            {overview.canDecide && <><Button disabled={busy || !comment.trim()} onClick={() => void run(() => purchasingService.decideReceiptInspection(current.id, { approved: true, comment: comment.trim(), rowVersion: current.rowVersion }), 'Inspection approved and accepted stock posted.')}>Approve</Button><Button variant="destructive" disabled={busy || !comment.trim()} onClick={() => void run(() => purchasingService.decideReceiptInspection(current.id, { approved: false, comment: comment.trim(), rowVersion: current.rowVersion }), 'Inspection rejected by workflow.')}>Reject</Button></>}
            {overview.canAcknowledge && <><Button disabled={busy || !comment.trim() || !reference.trim()} onClick={() => void run(() => purchasingService.acknowledgeReceiptInspection(current.id, { acknowledged: true, reference: reference.trim(), comment: comment.trim(), idempotencyKey: requestKey('supplier-ack'), rowVersion: current.rowVersion, evidence: controlledEvidence('SupplierAcknowledgement') }), 'Rejection note acknowledged.')}>{current.supplierAcknowledgementStatus === 3 ? 'Acknowledge after dispute' : 'Acknowledge rejection'}</Button>{current.supplierAcknowledgementStatus === 1 && <Button variant="destructive" disabled={busy || !comment.trim() || !reference.trim()} onClick={() => void run(() => purchasingService.acknowledgeReceiptInspection(current.id, { acknowledged: false, reference: reference.trim(), comment: comment.trim(), idempotencyKey: requestKey('supplier-dispute'), rowVersion: current.rowVersion, evidence: evidenceRows.some((row) => row.evidenceId.trim()) ? controlledEvidence('SupplierDispute') : [] }), 'Rejection note disputed.')}>Dispute</Button>}</>}
            {overview.canResolve && <Button disabled={busy || !comment.trim() || !reference.trim()} onClick={() => void run(() => purchasingService.resolveReceiptInspection(current.id, { resolutionKind, reference: reference.trim(), comment: comment.trim(), idempotencyKey: requestKey('receipt-resolution'), rowVersion: current.rowVersion, evidence: controlledEvidence(receiptInspectionResolutionActionKey(resolutionKind, current.resolutionStatus)) }), 'Return or replacement progression recorded.')}>Progress {resolutionKind === 1 ? 'return' : 'replacement'}</Button>}
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
