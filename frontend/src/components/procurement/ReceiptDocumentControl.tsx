'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, CheckCircle2, Download, FileCheck2, Loader2, RefreshCw, ShieldAlert, XCircle } from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  purchasingService,
  type ProcurementReceiptDocumentDto,
  type ProcurementReceiptDocumentOverviewDto,
} from '@/services/purchasingService';

const status = ['Draft', 'Pending signatures', 'Issued', 'Cancelled'];
const reconciliation = ['Pending', 'Reconciled', 'Exception', 'Cancelled'];
const kind = ['GRN', 'MRN'];
const messageOf = (error: unknown) => error instanceof Error ? error.message : 'The request could not be completed.';

export function ReceiptDocumentControl({ receiptId }: { receiptId: string }) {
  const [overview, setOverview] = useState<ProcurementReceiptDocumentOverviewDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [comment, setComment] = useState('');
  const [signatureRole, setSignatureRole] = useState<Record<string, string>>({});

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

  const download = async (document: ProcurementReceiptDocumentDto) => {
    try {
      setBusy(true);
      const blob = await purchasingService.downloadReceiptDocument(document.id);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener,noreferrer');
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (downloadError) {
      toast.error(messageOf(downloadError));
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <Card data-testid="receipt-document-loading"><CardContent className="flex items-center gap-2 py-10 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />Loading GRN/MRN register…</CardContent></Card>;
  if (error) return <Alert variant="destructive" data-testid="receipt-document-error"><AlertTriangle className="h-4 w-4" /><AlertTitle>Receipt-document control unavailable</AlertTitle><AlertDescription className="space-y-3"><p>{error}</p><Button variant="outline" size="sm" onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Retry</Button></AlertDescription></Alert>;
  if (!overview) return null;

  return <div className="space-y-5" data-testid="receipt-document-control">
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div><CardTitle className="flex items-center gap-2"><FileCheck2 className="h-5 w-5" />GRN/MRN document control</CardTitle><CardDescription>{overview.receiptNumber} · PO {overview.purchaseOrderNumber} · {overview.supplierName}</CardDescription></div>
          <div className="flex flex-wrap gap-2"><Badge variant="outline">Profile v{overview.configurationProfileVersion || '—'}</Badge><Badge variant={overview.isReconciled ? 'default' : 'secondary'}>{overview.isReconciled ? 'Reconciled' : 'Open controls'}</Badge><Button variant="outline" size="sm" disabled={busy} onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-5">{overview.checks.map((check) => <div key={check.code} className="rounded-lg border p-3">{check.passed ? <CheckCircle2 className="mb-2 h-4 w-4 text-emerald-600" /> : <ShieldAlert className="mb-2 h-4 w-4 text-amber-600" />}<div className="font-medium">{check.label}</div><div className="text-xs text-muted-foreground">{check.message}</div></div>)}</div>
        <div className="flex flex-wrap gap-2">{overview.decisionKeys.map((key) => <Badge key={key} variant={key === 'DEC-013' ? 'default' : 'secondary'}>{key}</Badge>)}</div>
        <div className="flex flex-wrap gap-2">{overview.allowedActions.includes('ensure') && <Button disabled={busy || overview.documents.length > 0} onClick={() => void run(() => purchasingService.ensureReceiptDocuments(receiptId), 'Configured receipt-document register created.')}>Ensure configured documents</Button>}{overview.allowedActions.includes('reconcile') && <Button variant="outline" disabled={busy || overview.documents.length === 0} onClick={() => void run(() => purchasingService.reconcileReceiptDocuments(receiptId), 'Receipt-document register reconciled.')}>Reconcile</Button>}</div>
      </CardContent>
    </Card>

    {overview.documents.length === 0 && <Card data-testid="receipt-document-audit-empty">
      <CardContent className="py-5">
        <div className="text-sm font-medium">Immutable audit history</div>
        <p className="text-sm text-muted-foreground">No receipt-document actions have been recorded. Creating the configured register starts the append-only history.</p>
      </CardContent>
    </Card>}

    {overview.documents.map((document) => {
      const roles = missingRoles(document);
      const allowedActions = new Set(document.allowedActions);
      const signableRoles = document.allowedSignatureRoles.filter((role) => roles.some(
        (missingRole) => missingRole.toLowerCase() === role.toLowerCase()
      ));
      const selectedRole = signatureRole[document.id] ?? signableRoles[0] ?? '';
      return <Card key={document.id} data-testid={`receipt-document-${kind[document.documentKind].toLowerCase()}`}>
        <CardHeader><div className="flex flex-wrap items-start justify-between gap-3"><div><CardTitle>{kind[document.documentKind]} · {document.documentNumber}</CardTitle><CardDescription>Central template {document.templateCode} · prepared by {document.preparedByName}</CardDescription></div><div className="flex gap-2"><Badge>{status[document.status]}</Badge><Badge variant={document.reconciliationStatus === 1 ? 'default' : document.reconciliationStatus === 2 ? 'destructive' : 'outline'}>{reconciliation[document.reconciliationStatus]}</Badge></div></div></CardHeader>
        <CardContent className="space-y-4">
          {document.reconciliationMessage && <Alert><AlertTitle>Reconciliation</AlertTitle><AlertDescription>{document.reconciliationMessage}</AlertDescription></Alert>}
          <div className="grid gap-3 md:grid-cols-2"><div className="rounded-lg border p-3"><div className="text-xs text-muted-foreground">Source integrity</div><div className="break-all font-mono text-xs">{document.sourceIntegrityHash}</div></div><div className="rounded-lg border p-3"><div className="text-xs text-muted-foreground">Central DMS lineage</div><div className="text-sm">{document.centralDocumentRecordId ? `Record ${document.centralDocumentRecordId}` : 'Created when issued'}</div></div></div>
          <div><div className="mb-2 text-sm font-medium">Configured signatories</div><div className="flex flex-wrap gap-2">{document.requiredSignatures.length === 0 ? <Badge variant="outline">No signatures configured</Badge> : document.requiredSignatures.map((role) => { const signed = document.signatures.find((item) => item.requiredRole.toLowerCase() === role.toLowerCase()); return <Badge key={role} variant={signed ? 'default' : 'outline'}>{signed ? `${role}: ${signed.signedByName}` : `${role}: pending`}</Badge>; })}</div></div>
          {document.status < 2 && allowedActions.size > 0 && <div className="space-y-3 rounded-lg border p-4"><div><Label>Action comment / cancellation reason</Label><Textarea value={comment} onChange={(event) => setComment(event.target.value)} placeholder="State the basis for issue, signature, or cancellation." /></div>{allowedActions.has('sign') && signableRoles.length > 0 && <div className="max-w-sm"><Label>Signatory role</Label><Select value={selectedRole} onValueChange={(value) => setSignatureRole((current) => ({ ...current, [document.id]: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{signableRoles.map((role) => <SelectItem key={role} value={role}>{role}</SelectItem>)}</SelectContent></Select></div>}<div className="flex flex-wrap gap-2">{allowedActions.has('sign') && signableRoles.length > 0 && <Button variant="outline" disabled={busy || !selectedRole} onClick={() => void run(() => purchasingService.signReceiptDocument(document.id, { requiredRole: selectedRole, comment: comment.trim() || undefined, rowVersion: document.rowVersion }), `${selectedRole} signature recorded.`)}>Sign</Button>}{allowedActions.has('issue') && <Button disabled={busy || !comment.trim() || !allChecksPass || roles.length > 0} onClick={() => void run(() => purchasingService.issueReceiptDocument(document.id, { comment: comment.trim(), rowVersion: document.rowVersion }), `${kind[document.documentKind]} issued to the central DMS.`)}>Issue</Button>}{allowedActions.has('cancel') && <Button variant="destructive" disabled={busy || !comment.trim()} onClick={() => void run(() => purchasingService.cancelReceiptDocument(document.id, { reason: comment.trim(), rowVersion: document.rowVersion }), `${kind[document.documentKind]} cancelled with history retained.`)}><XCircle className="mr-2 h-4 w-4" />Cancel</Button>}</div></div>}
          {document.status === 2 && <div className="flex flex-wrap gap-2">{allowedActions.has('download') && <Button disabled={busy} onClick={() => void download(document)}><Download className="mr-2 h-4 w-4" />Open PDF</Button>}{allowedActions.has('cancel') && <><Button variant="destructive" disabled={busy || !comment.trim()} onClick={() => void run(() => purchasingService.cancelReceiptDocument(document.id, { reason: comment.trim(), rowVersion: document.rowVersion }), `${kind[document.documentKind]} cancelled with DMS history retained.`)}>Cancel issued document</Button><Textarea value={comment} onChange={(event) => setComment(event.target.value)} placeholder="Cancellation reason (required only to cancel)" /></>}</div>}
          <div><div className="mb-2 text-sm font-medium">Immutable audit history</div>{document.actions.length === 0 ? <p className="text-sm text-muted-foreground">No actions recorded.</p> : <div className="space-y-2">{document.actions.map((action) => <div key={action.id} className="rounded border p-3 text-sm"><div className="font-medium">{action.action} · {action.fromStatus || 'New'} → {action.toStatus}</div><div className="text-muted-foreground">{action.actorName} · {new Date(action.occurredAtUtc).toLocaleString()}</div>{action.reason && <div>{action.reason}</div>}</div>)}</div>}</div>
        </CardContent>
      </Card>;
    })}
  </div>;
}
