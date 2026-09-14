'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, CheckCircle2, Download, Loader2, RefreshCw, ShieldAlert, XCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { ProcurementControlAccordion } from './ProcurementControlAccordion';
import { CentralDocumentViewerDialog, type CentralDocumentViewerFile } from '@/components/document-management/CentralDocumentViewerDialog';
import {
  purchasingService,
  type ProcurementReceiptDocumentDto,
  type ProcurementReceiptDocumentOverviewDto,
} from '@/services/purchasingService';
import {
  receiptDocumentKindDisplay,
  receiptDocumentReconciliationDisplay,
  receiptDocumentStatusDisplay,
} from '@/lib/procurement-receipt-document';

const messageOf = (error: unknown) => error instanceof Error ? error.message : 'The request could not be completed.';

export function ReceiptDocumentControl({ receiptId }: { receiptId: string }) {
  const [overview, setOverview] = useState<ProcurementReceiptDocumentOverviewDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [comment, setComment] = useState('');
  const [signatureRole, setSignatureRole] = useState<Record<string, string>>({});
  const [preview, setPreview] = useState<CentralDocumentViewerFile | null>(null);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      setOverview(await purchasingService.getReceiptDocumentControl(receiptId));
    } catch (loadError) {
      setError(messageOf(loadError));
    } finally {
      setLoading(false);
    }
  }, [receiptId]);

  useEffect(() => { void load(); }, [load]);

  const run = async (work: () => Promise<unknown>, success: string) => {
    try {
      setBusy(true);
      await work();
      toast.success(success);
      setComment('');
      await load();
    } catch (operationError) {
      toast.error(messageOf(operationError));
    } finally {
      setBusy(false);
    }
  };

  const missingRoles = (document: ProcurementReceiptDocumentDto) => document.requiredSignatures.filter(
    (role) => !document.signatures.some((signature) => signature.requiredRole.toLowerCase() === role.toLowerCase())
  );
  const allChecksPass = useMemo(() => overview?.checks.every((check) => check.passed) ?? false, [overview]);

  const download = (document: ProcurementReceiptDocumentDto) => {
    setPreview({
      title: document.documentNumber,
      fileName: `${document.documentNumber}.pdf`,
      contentType: 'application/pdf',
      repositoryPath: `/api/ProcurementReceiptDocuments/${encodeURIComponent(document.id)}/download`,
      sourceLabel: 'Issued goods receipt note',
    });
  };

  if (loading) return <Card data-testid="receipt-document-loading"><CardContent className="flex items-center gap-2 py-10 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />Loading GRN register…</CardContent></Card>;
  if (error) return <Alert variant="destructive" data-testid="receipt-document-error"><AlertTriangle className="h-4 w-4" /><AlertTitle>Receipt-document control unavailable</AlertTitle><AlertDescription className="space-y-3"><p>{error}</p><Button variant="outline" size="sm" onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Retry</Button></AlertDescription></Alert>;
  if (!overview) return null;

  const failedChecks = overview.checks.filter((check) => !check.passed);
  // MRN remains retained by the backend for history, but is not an operator action.
  // Never filter server checks or mutate the persisted register to hide a document.
  const visibleDocuments = overview.documents.filter((document) => receiptDocumentKindDisplay(document.documentKind).code !== 1);
  const grns = visibleDocuments.filter((document) => receiptDocumentKindDisplay(document.documentKind).code === 0);
  const grnsReconciled = grns.length > 0 && grns.every((document) => receiptDocumentReconciliationDisplay(document.reconciliationStatus).code === 1);
  const checkLabel = (check: ProcurementReceiptDocumentOverviewDto['checks'][number]) =>
    check.code === 'DOCUMENT_SET' ? 'Configured receipt documents' : check.label;
  const checkMessage = (check: ProcurementReceiptDocumentOverviewDto['checks'][number]) =>
    check.code === 'DOCUMENT_SET' && check.passed ? 'The configured receipt document register is complete.' : check.message;

  return <div className="space-y-5" data-testid="receipt-document-control">
    <ProcurementControlAccordion
      title="Receipt document checks"
      summary={`${overview.checks.length - failedChecks.length} of ${overview.checks.length} checks passed`}
      status={<Badge variant={allChecksPass ? 'default' : 'secondary'}>{allChecksPass ? 'Ready' : 'Needs attention'}</Badge>}
      actions={<Button variant="ghost" size="sm" aria-label="Refresh document checks" disabled={busy} onClick={() => void load()}><RefreshCw className="h-4 w-4" /></Button>}
      notice={failedChecks.length > 0 && <div><p className="font-medium">Before issuing documents</p><ul className="mt-1 list-disc space-y-1 pl-4">{failedChecks.map((check) => <li key={check.code}>{check.message.replace(/\bDEC-\d{3}\b/g, 'The document policy')}</li>)}</ul></div>}
    >
      <div className="space-y-4">
        <div className="grid gap-3 md:grid-cols-2">{overview.checks.map((check) => <div key={check.code} className="rounded-lg border p-3">{check.passed ? <CheckCircle2 className="mb-2 h-4 w-4 text-emerald-600" /> : <ShieldAlert className="mb-2 h-4 w-4 text-amber-600" />}<div className="font-medium">{checkLabel(check)}</div><div className="text-xs text-muted-foreground">{checkMessage(check)}</div></div>)}</div>
        <p className="text-xs text-muted-foreground">Policy profile version: {overview.configurationProfileVersion || '—'} · GRN reconciliation: {grnsReconciled ? 'Reconciled' : 'Not yet reconciled'}</p>
        <div className="flex flex-wrap gap-2" aria-label="Policy references">{overview.decisionKeys.map((key) => <Badge key={key} variant="outline">{key}</Badge>)}</div>
      </div>
    </ProcurementControlAccordion>
    <div className="flex flex-wrap gap-2">{overview.allowedActions.includes('ensure') && overview.documents.length === 0 && <Button disabled={busy} onClick={() => void run(() => purchasingService.ensureReceiptDocuments(receiptId), 'Configured receipt-document register created.')}>Create receipt documents</Button>}{overview.allowedActions.includes('reconcile') && <Button variant="outline" disabled={busy || overview.documents.length === 0} onClick={() => void run(() => purchasingService.reconcileReceiptDocuments(receiptId), 'Receipt-document register reconciled.')}>Reconcile</Button>}</div>

    {overview.documents.length === 0 && <p className="text-sm text-muted-foreground" data-testid="receipt-document-audit-empty">No receipt documents have been created yet.</p>}
    {overview.documents.length > 0 && grns.length === 0 && <Alert data-testid="receipt-document-grn-missing"><AlertTriangle className="h-4 w-4" /><AlertTitle>GRN unavailable</AlertTitle><AlertDescription>The saved register does not contain a GRN. Ask your administrator to review the receipt document configuration.</AlertDescription></Alert>}

    {visibleDocuments.map((document) => {
      const roles = missingRoles(document);
      const allowedActions = new Set(document.allowedActions);
      const signableRoles = document.allowedSignatureRoles.filter((role) => roles.some(
        (missingRole) => missingRole.toLowerCase() === role.toLowerCase()
      ));
      const selectedRole = signatureRole[document.id] ?? signableRoles[0] ?? '';
      const documentKind = receiptDocumentKindDisplay(document.documentKind);
      const documentStatus = receiptDocumentStatusDisplay(document.status);
      const documentReconciliation = receiptDocumentReconciliationDisplay(document.reconciliationStatus);
      const isMutable = documentStatus.code !== null && documentStatus.code < 2;
      const isIssued = documentStatus.code === 2;
      return <Card key={document.id} data-testid={`receipt-document-${documentKind.testId}`}>
        <CardHeader><div className="flex flex-wrap items-start justify-between gap-3"><div><CardTitle>{documentKind.label} · {document.documentNumber}</CardTitle><CardDescription>Prepared by {document.preparedByName}</CardDescription></div><div className="flex gap-2"><Badge>{documentStatus.label}</Badge>{documentReconciliation.code === 2 && <Badge variant="destructive">{documentReconciliation.label}</Badge>}</div></div></CardHeader>
        <CardContent className="space-y-4">
          {documentReconciliation.code === 2 && <Alert variant="destructive"><AlertTitle>Document needs attention</AlertTitle><AlertDescription>{document.reconciliationMessage || 'Resolve the document reconciliation exception before continuing.'}</AlertDescription></Alert>}
          <div><div className="mb-2 text-sm font-medium">Required signatures</div><div className="flex flex-wrap gap-2">{document.requiredSignatures.length === 0 ? <Badge variant="outline">No signatures required</Badge> : document.requiredSignatures.map((role) => { const signed = document.signatures.find((item) => item.requiredRole.toLowerCase() === role.toLowerCase()); return <Badge key={role} variant={signed ? 'default' : 'outline'}>{signed ? `${role}: ${signed.signedByName}` : `${role}: pending`}</Badge>; })}</div></div>
          {isMutable && allowedActions.size > 0 && <div className="space-y-3 rounded-lg border p-4"><div><Label>Action comment / cancellation reason</Label><Textarea value={comment} onChange={(event) => setComment(event.target.value)} placeholder="State the basis for issue, signature, or cancellation." /></div>{allowedActions.has('sign') && signableRoles.length > 0 && <div className="max-w-sm"><Label>Signatory role</Label><Select value={selectedRole} onValueChange={(value) => setSignatureRole((current) => ({ ...current, [document.id]: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{signableRoles.map((role) => <SelectItem key={role} value={role}>{role}</SelectItem>)}</SelectContent></Select></div>}<div className="flex flex-wrap gap-2">{allowedActions.has('sign') && signableRoles.length > 0 && <Button variant="outline" disabled={busy || !selectedRole} onClick={() => void run(() => purchasingService.signReceiptDocument(document.id, { requiredRole: selectedRole, comment: comment.trim() || undefined, rowVersion: document.rowVersion }), `${selectedRole} signature recorded.`)}>Sign</Button>}{allowedActions.has('issue') && <Button disabled={busy || !comment.trim() || !allChecksPass || roles.length > 0} onClick={() => void run(() => purchasingService.issueReceiptDocument(document.id, { comment: comment.trim(), rowVersion: document.rowVersion }), `${documentKind.label} issued to the central DMS.`)}>Issue</Button>}{allowedActions.has('cancel') && <Button variant="destructive" disabled={busy || !comment.trim()} onClick={() => void run(() => purchasingService.cancelReceiptDocument(document.id, { reason: comment.trim(), rowVersion: document.rowVersion }), `${documentKind.label} cancelled with history retained.`)}><XCircle className="mr-2 h-4 w-4" />Cancel</Button>}</div></div>}
          {isIssued && <div className="flex flex-wrap gap-2">{allowedActions.has('download') && <Button disabled={busy} onClick={() => void download(document)}><Download className="mr-2 h-4 w-4" />Open PDF</Button>}{allowedActions.has('cancel') && <><Button variant="destructive" disabled={busy || !comment.trim()} onClick={() => void run(() => purchasingService.cancelReceiptDocument(document.id, { reason: comment.trim(), rowVersion: document.rowVersion }), `${documentKind.label} cancelled with DMS history retained.`)}>Cancel issued document</Button><Textarea value={comment} onChange={(event) => setComment(event.target.value)} placeholder="Cancellation reason (required only to cancel)" /></>}</div>}
          <ProcurementControlAccordion title={`${documentKind.label} history & technical details`} summary={`${document.actions.length} recorded ${document.actions.length === 1 ? 'event' : 'events'}`}>
            <div className="space-y-4">
              <p className="text-sm">Template: {document.templateCode} · Reconciliation: {documentReconciliation.label}</p>
              {document.reconciliationMessage && <p className="text-sm text-muted-foreground">{document.reconciliationMessage}</p>}
              <div className="grid gap-3 md:grid-cols-2"><div><div className="text-xs text-muted-foreground">Source integrity</div><div className="break-all font-mono text-xs">{document.sourceIntegrityHash}</div></div><div><div className="text-xs text-muted-foreground">Central DMS lineage</div><div className="break-all text-sm">{document.centralDocumentRecordId ? `Record ${document.centralDocumentRecordId}` : 'Created when issued'}</div></div></div>
              <div><div className="mb-2 text-sm font-medium">Audit history</div>{document.actions.length === 0 ? <p className="text-sm text-muted-foreground">No actions recorded.</p> : <div className="space-y-2">{document.actions.map((action) => <div key={action.id} className="rounded border p-3 text-sm"><div className="font-medium">{action.action} · {action.fromStatus || 'New'} → {action.toStatus}</div><div className="text-muted-foreground">{action.actorName} · {new Date(action.occurredAtUtc).toLocaleString()}</div>{action.reason && <div>{action.reason}</div>}</div>)}</div>}</div>
            </div>
          </ProcurementControlAccordion>
        </CardContent>
      </Card>;
    })}
    <CentralDocumentViewerDialog file={preview} open={preview !== null}
      onOpenChange={open => { if (!open) setPreview(null); }} enableAnnotations={false} />
  </div>;
}
