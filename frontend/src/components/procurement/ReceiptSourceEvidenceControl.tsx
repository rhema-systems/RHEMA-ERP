'use client';

import { useCallback, useEffect, useState } from 'react';
import { Download, FileCheck2, Loader2, RefreshCw, ShieldCheck, Upload } from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  purchasingService,
  type ProcurementReceiptSourceEvidenceKind,
  type ProcurementReceiptSourceEvidenceOverviewDto,
} from '@/services/purchasingService';

const messageOf = (error: unknown) => error instanceof Error ? error.message : 'The request could not be completed.';

export function ReceiptSourceEvidenceControl({ receiptId }: { receiptId: string }) {
  const [overview, setOverview] = useState<ProcurementReceiptSourceEvidenceOverviewDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [kind, setKind] = useState<ProcurementReceiptSourceEvidenceKind>(1);
  const [reference, setReference] = useState('');
  const [documentDate, setDocumentDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [file, setFile] = useState<File | null>(null);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      setOverview(await purchasingService.getReceiptSourceEvidence(receiptId));
    } catch (loadError) {
      setError(messageOf(loadError));
    } finally {
      setLoading(false);
    }
  }, [receiptId]);

  useEffect(() => { void load(); }, [load]);

  const upload = async () => {
    if (!reference.trim()) return toast.error('Enter the supplier document reference.');
    if (!documentDate) return toast.error('Select the document date.');
    if (!file) return toast.error('Select the Waybill or VAT invoice copy.');
    try {
      setBusy(true);
      await purchasingService.uploadReceiptSourceEvidence(receiptId, {
        evidenceKind: kind,
        referenceNumber: reference.trim(),
        documentDate,
        clientRequestId: crypto.randomUUID(),
        file,
      });
      toast.success(kind === 1 ? 'Waybill evidence attached.' : 'VAT invoice copy attached for Finance/AP reference.');
      setReference('');
      setFile(null);
      const input = document.getElementById('receipt-source-evidence-file') as HTMLInputElement | null;
      if (input) input.value = '';
      await load();
    } catch (uploadError) {
      toast.error(messageOf(uploadError));
    } finally {
      setBusy(false);
    }
  };

  const download = async (id: string, name: string) => {
    try {
      setBusy(true);
      const blob = await purchasingService.downloadReceiptSourceEvidence(receiptId, id);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = name;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (downloadError) {
      toast.error(messageOf(downloadError));
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <Card><CardContent className="flex items-center gap-2 py-8 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" />Loading receipt evidence…</CardContent></Card>;
  if (error) return <Alert variant="destructive"><AlertTitle>Receipt evidence unavailable</AlertTitle><AlertDescription className="space-y-3"><p>{error}</p><Button variant="outline" size="sm" onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Retry</Button></AlertDescription></Alert>;
  if (!overview) return null;

  return <Card data-testid="receipt-source-evidence-control">
    <CardHeader>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <CardTitle className="flex items-center gap-2"><FileCheck2 className="h-5 w-5" />Supplier delivery evidence</CardTitle>
          <CardDescription>Waybill is mandatory before inspection acceptance. VAT invoice copies remain under Finance/AP ownership.</CardDescription>
        </div>
        <Badge variant={overview.waybillReady ? 'default' : 'destructive'}>
          {overview.waybillReady ? 'Waybill ready' : 'Waybill required'}
        </Badge>
      </div>
    </CardHeader>
    <CardContent className="space-y-4">
      <Alert>
        <ShieldCheck className="h-4 w-4" />
        <AlertTitle>Finance/AP ownership preserved</AlertTitle>
        <AlertDescription>{overview.financeOwnershipNotice}</AlertDescription>
      </Alert>

      {overview.canUpload && <div className="grid gap-3 rounded-lg border p-4 md:grid-cols-[190px_1fr_170px_minmax(220px,1fr)_auto] md:items-end">
        <div className="space-y-2">
          <Label>Evidence type</Label>
          <Select value={String(kind)} onValueChange={(value) => setKind(Number(value) as ProcurementReceiptSourceEvidenceKind)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent><SelectItem value="1">Waybill</SelectItem><SelectItem value="2">VAT invoice copy</SelectItem></SelectContent>
          </Select>
        </div>
        <div className="space-y-2"><Label>Document reference</Label><Input value={reference} maxLength={100} onChange={(event) => setReference(event.target.value)} placeholder={kind === 1 ? 'Waybill number' : 'Supplier invoice reference'} /></div>
        <div className="space-y-2"><Label>Document date</Label><Input type="date" value={documentDate} max={new Date().toISOString().slice(0, 10)} onChange={(event) => setDocumentDate(event.target.value)} /></div>
        <div className="space-y-2"><Label>Clean-scanned file</Label><Input id="receipt-source-evidence-file" type="file" accept=".pdf,.png,.jpg,.jpeg" onChange={(event) => setFile(event.target.files?.[0] ?? null)} /></div>
        <Button disabled={busy} onClick={() => void upload()}><Upload className="mr-2 h-4 w-4" />{overview.evidence.some((item) => item.evidenceKind === kind) ? 'Replace' : 'Attach'}</Button>
      </div>}

      {overview.inspectionEvidenceLocked && <p className="text-sm text-muted-foreground">Evidence is locked while this receipt is in its submitted inspection or resolution lifecycle.</p>}

      <div className="grid gap-3 md:grid-cols-2">
        {[1, 2].map((value) => {
          const evidence = overview.evidence.find((item) => item.evidenceKind === value);
          const label = value === 1 ? 'Waybill' : 'VAT invoice copy';
          return <div key={value} className="rounded-lg border p-4">
            <div className="mb-2 flex items-center justify-between gap-2"><div className="font-medium">{label}</div><Badge variant="outline">{evidence ? 'Attached' : value === 1 ? 'Missing' : 'Optional'}</Badge></div>
            {evidence ? <div className="space-y-1 text-sm">
              <p>{evidence.referenceNumber} · {new Date(evidence.documentDate).toLocaleDateString()}</p>
              <p className="truncate text-muted-foreground">{evidence.originalFileName}</p>
              <Button variant="outline" size="sm" disabled={busy} onClick={() => void download(evidence.id, evidence.originalFileName)}><Download className="mr-2 h-4 w-4" />Download</Button>
            </div> : <p className="text-sm text-muted-foreground">{value === 1 ? 'Inspection cannot be submitted or approved until attached.' : 'Attach when supplied; Finance/AP will perform authoritative invoice processing.'}</p>}
          </div>;
        })}
      </div>
    </CardContent>
  </Card>;
}
