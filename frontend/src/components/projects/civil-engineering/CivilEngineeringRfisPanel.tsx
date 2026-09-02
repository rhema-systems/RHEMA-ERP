'use client';

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { FileQuestion, FileText, RefreshCw, Send, ShieldCheck } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringRfiService } from '@/services/civil-engineering-rfi.service';
import type { CivilEngineeringRfiLookups, CivilEngineeringRfiRouting } from '@/types/civil-engineering-rfi';

const workspacePermission = 'civil-engineering.workspace.read';
const supervisionPermission = 'civil-engineering.supervision.manage';
const formatDate = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Not available';
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || fallback}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};
type Props = { projectId: string };

export function CivilEngineeringRfisPanel({ projectId }: Props) {
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canRead = hasPermission(workspacePermission);
  const canManage = hasPermission(supervisionPermission);
  const [items, setItems] = useState<CivilEngineeringRfiRouting[]>([]);
  const [lookups, setLookups] = useState<CivilEngineeringRfiLookups>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [responseText, setResponseText] = useState<Record<string, string>>({});
  const [decisionReason, setDecisionReason] = useState<Record<string, string>>({});
  const [documentKeys, setDocumentKeys] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    if (!canRead) { setLoading(false); return; }
    setLoading(true);
    const [list, context] = await Promise.allSettled([
      civilEngineeringRfiService.list(projectId),
      canManage ? civilEngineeringRfiService.lookups(projectId) : Promise.resolve(undefined),
    ]);
    if (list.status === 'fulfilled') setItems(list.value);
    else toast({ variant: 'destructive', title: 'Governed RFIs could not be loaded', description: errorText(list.reason, 'Refresh the project workspace and try again.') });
    if (context.status === 'fulfilled' && context.value) setLookups(context.value);
    else setLookups(undefined);
    setLoading(false);
  }, [canManage, canRead, projectId, toast]);

  useEffect(() => { void load(); }, [load]);

  const responseDocument = useCallback((routingId: string) => {
    const key = documentKeys[routingId] || (lookups?.documents[0] ? `${lookups.documents[0].centralDocumentRecordId}:${lookups.documents[0].centralDocumentVersionId}` : '');
    return lookups?.documents.find((value) => `${value.centralDocumentRecordId}:${value.centralDocumentVersionId}` === key);
  }, [documentKeys, lookups]);

  const submitResponse = async (item: CivilEngineeringRfiRouting) => {
    const text = responseText[item.id]?.trim() || '';
    const document = responseDocument(item.id);
    if (text.length < 10 || !document) {
      toast({ variant: 'destructive', title: 'Complete the controlled RFI response', description: 'Provide the Project Engineer response and select its current Published DMS evidence.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringRfiService.submitProjectEngineerResponse(item.id, {
        clientRequestId: crypto.randomUUID(), rowVersion: item.rowVersion, responseText: text,
        centralDocumentRecordId: document.centralDocumentRecordId, centralDocumentVersionId: document.centralDocumentVersionId,
      });
      setResponseText((current) => ({ ...current, [item.id]: '' }));
      toast({ title: 'RFI response routed', description: 'The versioned response has been routed to the assigned Project Manager through the configured workflow.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'RFI response was not submitted', description: errorText(error, 'Only the routed current Project Engineer can submit this response.') });
    } finally { setSaving(false); }
  };

  const processResponse = async (item: CivilEngineeringRfiRouting, approve: boolean) => {
    const reason = decisionReason[item.id]?.trim() || '';
    if (reason.length < 3) {
      toast({ variant: 'destructive', title: 'Project Manager comment required', description: 'Provide a concise decision comment before continuing.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringRfiService.processProjectManagerResponse(item.id, { clientRequestId: crypto.randomUUID(), rowVersion: item.rowVersion, approve, reason });
      setDecisionReason((current) => ({ ...current, [item.id]: '' }));
      toast({ title: approve ? 'RFI response approved' : 'RFI response returned', description: approve ? 'The approved answer is now recorded on the authoritative project RFI.' : 'The Project Engineer can submit a new versioned response.' });
      await load();
    } catch (error) {
      toast({ variant: 'destructive', title: 'RFI decision was not saved', description: errorText(error, 'Only the routed Project Manager at the active workflow step can process this response.') });
    } finally { setSaving(false); }
  };

  const options = useMemo(() => lookups?.documents || [], [lookups]);
  if (!canRead) return null;

  return (
    <Card className="border-slate-200/70 shadow-sm">
      <CardHeader className="gap-3 pb-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <CardTitle className="flex items-center gap-2 text-base"><FileQuestion className="h-4 w-4 text-violet-600" />Governed contractor and consultant RFIs</CardTitle>
          <CardDescription>External RFIs route to the appointed Project Engineer; versioned DMS-backed responses require Project Manager workflow approval.</CardDescription>
        </div>
        <Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {loading ? <p className="text-sm text-slate-500">Loading governed RFIs…</p> : null}
        {canManage && !loading && !lookups ? <p className="rounded border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">RFI history is available, but the controlled actions require an effective Civil supervision policy, an active Project Engineer appointment, Project Manager and published RFI workflow.</p> : null}
        {!loading && !items.length ? <p className="rounded border border-dashed border-slate-300 bg-slate-50 p-3 text-sm text-slate-600">No governed contractor or consultant RFIs have been raised for this project.</p> : null}
        {items.map((item) => {
          const document = responseDocument(item.id);
          const canRespond = item.status === 'AwaitingProjectEngineerResponse' || item.status === 'ReturnedToProjectEngineer';
          const canDecide = item.status === 'AwaitingProjectManagerApproval' && item.approvalStatus === 'Pending';
          return (
            <article key={item.id} className="rounded-lg border border-slate-200 bg-white p-4">
              <div className="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2"><span className="font-medium text-slate-900">{item.referenceNumber} · {item.subject}</span><Badge variant="outline">{item.priority}</Badge><Badge variant={item.approvalStatus === 'Approved' ? 'default' : item.approvalStatus === 'Rejected' ? 'destructive' : 'secondary'}>{item.approvalStatus}</Badge><Badge variant="outline">{item.status}</Badge></div>
                  <p className="mt-2 whitespace-pre-wrap break-words text-sm text-slate-600">{item.question}</p>
                  <p className="mt-2 text-xs text-slate-500">Raised by: {item.externalBusinessPartnerName} · PE: {item.projectEngineerName} · PM: {item.projectManagerName} · {formatDate(item.raisedDate)}</p>
                </div>
              </div>
              <div className="mt-3 flex flex-wrap gap-2 text-xs text-slate-600">{item.evidence.map((value) => <span key={value.centralDocumentVersionId} className="inline-flex items-center gap-1 rounded bg-slate-100 px-2 py-1"><FileText className="h-3 w-3" />{value.documentReference} v{value.versionNumber}</span>)}</div>
              {canManage && lookups && canRespond ? <section className="mt-4 rounded border border-violet-200 bg-violet-50/40 p-3"><div className="mb-2 flex items-center gap-2 text-sm font-medium text-slate-900"><Send className="h-4 w-4 text-violet-700" />Project Engineer response</div><div className="grid gap-3 md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]"><div className="space-y-2"><Label>Versioned response</Label><Textarea rows={3} value={responseText[item.id] || ''} onChange={(event) => setResponseText((current) => ({ ...current, [item.id]: event.target.value }))} placeholder="Provide the controlled technical response for Project Manager approval." /></div><div className="space-y-2"><Label>Current Published DMS response evidence</Label><Select value={documentKeys[item.id] || (document ? `${document.centralDocumentRecordId}:${document.centralDocumentVersionId}` : '')} onValueChange={(value) => setDocumentKeys((current) => ({ ...current, [item.id]: value }))}><SelectTrigger><SelectValue placeholder="Select DMS evidence" /></SelectTrigger><SelectContent>{options.map((value) => <SelectItem key={value.centralDocumentVersionId} value={`${value.centralDocumentRecordId}:${value.centralDocumentVersionId}`}>{value.documentReference} · v{value.versionNumber}</SelectItem>)}</SelectContent></Select></div></div><div className="mt-3 flex justify-end"><Button size="sm" disabled={saving || !options.length} onClick={() => void submitResponse(item)}><Send className="mr-2 h-4 w-4" />Submit to Project Manager</Button></div></section> : null}
              {canManage && canDecide ? <section className="mt-4 rounded border border-amber-200 bg-amber-50 p-3"><div className="mb-2 flex items-center gap-2 text-sm font-medium text-slate-900"><ShieldCheck className="h-4 w-4 text-amber-700" />Project Manager workflow decision</div><Textarea rows={2} value={decisionReason[item.id] || ''} onChange={(event) => setDecisionReason((current) => ({ ...current, [item.id]: event.target.value }))} placeholder="Project Manager approval or return comment" /><div className="mt-3 flex justify-end gap-2"><Button variant="outline" size="sm" disabled={saving} onClick={() => void processResponse(item, false)}>Return to Project Engineer</Button><Button size="sm" disabled={saving} onClick={() => void processResponse(item, true)}>Approve response</Button></div></section> : null}
              <section className="mt-4 border-t border-slate-100 pt-3"><p className="mb-2 text-sm font-medium text-slate-800">Versioned Project Engineer response history</p>{item.responses.length ? <div className="space-y-2">{item.responses.map((response) => <div key={response.id} className="rounded bg-slate-50 px-3 py-2 text-sm"><p className="font-medium text-slate-800">Version {response.sequence} · {response.respondedByName} <span className="font-normal text-slate-500">· {formatDate(response.timestamp)}</span></p><p className="mt-1 whitespace-pre-wrap break-words text-slate-600">{response.responseText}</p><p className="mt-1 text-xs text-slate-500">Current Published DMS evidence linked.</p></div>)}</div> : <p className="text-sm text-slate-500">No Project Engineer response has been submitted yet.</p>}</section>
            </article>
          );
        })}
      </CardContent>
    </Card>
  );
}
