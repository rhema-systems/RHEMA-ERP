'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, FileCheck2, FileStack, Plus, RefreshCw, Send, ShieldX, Trash2 } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringMigrationService } from '@/services/civil-engineering-migration.service';
import { projectService, type ProjectDto } from '@/services/projectService';
import type { CivilEngineeringMigrationBatch, CivilEngineeringMigrationLookups, CivilEngineeringMigrationRecordInput, CivilEngineeringMigrationRecordType, CivilEngineeringMigrationSource } from '@/types/civil-engineering-migration';

const managePermission = 'civil-engineering.migration.manage';
const readPermission = 'civil-engineering.workspace.read';
const none = '__select__';
const formatDate = (value?: string | null) => value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—';
const readable = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');
const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  return `${value.response?.detail || value.message || fallback}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};
const newRecord = (): CivilEngineeringMigrationRecordInput => ({ recordType: 'Drawing', sourceReference: '', title: '' });

export default function CivilEngineeringMigrationBatchesPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canManage = hasPermission(managePermission);
  const canRead = hasPermission(readPermission) || canManage;
  const [projects, setProjects] = useState<ProjectDto[]>([]);
  const [projectId, setProjectId] = useState('');
  const [lookups, setLookups] = useState<CivilEngineeringMigrationLookups>();
  const [batches, setBatches] = useState<CivilEngineeringMigrationBatch[]>([]);
  const [sourceType, setSourceType] = useState<CivilEngineeringMigrationSource | ''>('');
  const [sourceRegisterReference, setSourceRegisterReference] = useState('');
  const [records, setRecords] = useState<CivilEngineeringMigrationRecordInput[]>([newRecord()]);
  const [reconciliationDocumentId, setReconciliationDocumentId] = useState(none);
  const [declarations, setDeclarations] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const selectedReconciliationDocument = useMemo(
    () => lookups?.reconciliationDocuments.find((value) => value.centralDocumentVersionId === reconciliationDocumentId),
    [lookups, reconciliationDocumentId],
  );

  const load = useCallback(async (selectedProjectId?: string) => {
    if (!canRead) { setLoading(false); return; }
    setLoading(true);
    try {
      const result = await projectService.getProjects({ page: 1, pageSize: 250 });
      setProjects(result.items);
      const nextProjectId = selectedProjectId || projectId || result.items[0]?.id || '';
      setProjectId(nextProjectId);
      if (!nextProjectId) { setBatches([]); setLookups(undefined); return; }
      const list = await civilEngineeringMigrationService.list(nextProjectId);
      setBatches(list);
      if (canManage) {
        try {
          const migrationLookups = await civilEngineeringMigrationService.lookups(nextProjectId);
          setLookups(migrationLookups);
          setSourceType((current) => current || migrationLookups.sourceTypes[0] || '');
        } catch (error) {
          setLookups(undefined);
          toast({ variant: 'destructive', title: 'Migration controls are unavailable', description: errorText(error, 'CIV-CFG-013 requires a published, verified policy and your active project role.') });
        }
      }
    } catch (error) {
      toast({ variant: 'destructive', title: 'Civil migration batches could not be loaded', description: errorText(error, 'Refresh and try again.') });
    } finally { setLoading(false); }
  }, [canManage, canRead, projectId, toast]);

  useEffect(() => { void load(); }, [load]);

  const updateRecord = (index: number, patch: Partial<CivilEngineeringMigrationRecordInput>) => {
    setRecords((current) => current.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item));
  };
  const selectDocument = (index: number, versionId: string) => {
    const document = lookups?.documents.find((value) => value.centralDocumentVersionId === versionId);
    updateRecord(index, {
      centralDocumentVersionId: versionId === none ? undefined : document?.centralDocumentVersionId,
      centralDocumentRecordId: versionId === none ? undefined : document?.centralDocumentRecordId,
    });
  };
  const resetDraft = () => {
    setSourceRegisterReference('');
    setRecords([newRecord()]);
    setReconciliationDocumentId(none);
  };

  const stage = async () => {
    if (!projectId || !sourceType || sourceRegisterReference.trim().length < 3 || records.some((value) => value.sourceReference.trim().length < 3 || value.title.trim().length < 3 || (value.recordType !== 'PhysicalFileReference' && !value.centralDocumentVersionId) || (value.recordType === 'PhysicalFileReference' && (value.physicalFileReference?.trim().length ?? 0) < 3))) {
      toast({ variant: 'destructive', title: 'Complete the staging records', description: 'Select the configured source and current Published DMS document for each non-physical record. Physical-file references require a legacy register reference.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringMigrationService.stage(projectId, {
        clientRequestId: crypto.randomUUID(), sourceType, sourceRegisterReference: sourceRegisterReference.trim(),
        records: records.map((value) => ({ ...value, sourceReference: value.sourceReference.trim(), title: value.title.trim(), recordDate: value.recordDate || undefined, centralDocumentRecordId: value.centralDocumentRecordId || undefined, centralDocumentVersionId: value.centralDocumentVersionId || undefined, physicalFileReference: value.physicalFileReference?.trim() || undefined })),
      });
      resetDraft();
      toast({ title: 'Civil records staged', description: 'The batch was validated before any owner module can receive it.' });
      await load(projectId);
    } catch (error) {
      toast({ variant: 'destructive', title: 'Civil records were not staged', description: errorText(error, 'Refresh the project and correct the controlled records.') });
    } finally { setSaving(false); }
  };

  const reconcile = async (batch: CivilEngineeringMigrationBatch) => {
    if (!projectId || !selectedReconciliationDocument || (declarations[batch.id] || '').trim().length < 5) {
      toast({ variant: 'destructive', title: 'Reconciliation evidence is required', description: 'Select the current Published reconciliation document configured by CIV-CFG-013 and provide the reconciliation declaration.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringMigrationService.reconcile(projectId, batch.id, { rowVersion: batch.rowVersion, reconciliationDocumentRecordId: selectedReconciliationDocument.centralDocumentRecordId, reconciliationDocumentVersionId: selectedReconciliationDocument.centralDocumentVersionId, declaration: declarations[batch.id].trim() });
      setReconciliationDocumentId(none); setDeclarations((current) => ({ ...current, [batch.id]: '' }));
      toast({ title: 'Migration batch reconciled', description: 'An independent configured sign-off is still required.' });
      await load(projectId);
    } catch (error) {
      toast({ variant: 'destructive', title: 'Migration reconciliation was not saved', description: errorText(error, 'The submitter cannot reconcile their own batch.') });
    } finally { setSaving(false); }
  };

  const signOff = async (batch: CivilEngineeringMigrationBatch) => {
    const declaration = declarations[batch.id] || '';
    if (!projectId || declaration.trim().length < 5) {
      toast({ variant: 'destructive', title: 'Sign-off declaration is required', description: 'Provide the independent acceptance declaration before signing off.' });
      return;
    }
    setSaving(true);
    try {
      await civilEngineeringMigrationService.signOff(projectId, batch.id, { rowVersion: batch.rowVersion, declaration: declaration.trim() });
      setDeclarations((current) => ({ ...current, [batch.id]: '' }));
      toast({ title: 'Migration batch is ready for owner posting', description: 'No owner record was created; use the receiving module’s controlled posting process.' });
      await load(projectId);
    } catch (error) {
      toast({ variant: 'destructive', title: 'Migration sign-off was not saved', description: errorText(error, 'The submitter and reconciler cannot sign off their own batch.') });
    } finally { setSaving(false); }
  };

  if (!canRead) return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil migration access required</AlertTitle><AlertDescription>You do not have permission to view Civil migration staging records.</AlertDescription></Alert>;
  return <div className="space-y-5">
    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between"><div><h1 className="flex items-center gap-2 text-2xl font-semibold"><FileStack className="h-6 w-6" />Civil migration workbench</h1><p className="mt-1 text-sm text-muted-foreground">Validate and independently sign off historical Civil records before their authoritative owner accepts them.</p></div><Button variant="outline" size="sm" onClick={() => void load()} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></div>
    <Card><CardContent className="grid gap-3 pt-6 md:grid-cols-[minmax(0,1fr)_auto]"><div className="space-y-1"><Label>Project</Label><Select value={projectId || none} onValueChange={(value) => void load(value === none ? '' : value)}><SelectTrigger><SelectValue placeholder="Select permitted project" /></SelectTrigger><SelectContent>{projects.map((project) => <SelectItem key={project.id} value={project.id}>{project.projectCode} · {project.projectName}</SelectItem>)}</SelectContent></Select></div><p className="self-end pb-2 text-xs text-muted-foreground">Owner posting remains outside this workbench.</p></CardContent></Card>
    {canManage && !loading && !lookups ? <Alert><AlertTitle>Controlled migration policy is unavailable</AlertTitle><AlertDescription>Staging requires an effective CIV-CFG-013 policy, selected owner/reviewer/sign-off roles, and an active reconciliation DMS template.</AlertDescription></Alert> : null}
    {canManage && lookups ? <Card><CardHeader><CardTitle className="text-base">Stage historical records</CardTitle><CardDescription>Identifiers and titles document legacy provenance. Record categories and DMS evidence are controlled selections; physical references are allowed only where CIV-CFG-013 permits them.</CardDescription></CardHeader><CardContent className="space-y-4"><div className="grid gap-3 md:grid-cols-2"><div className="space-y-1"><Label>Source type</Label><Select value={sourceType || none} onValueChange={(value) => setSourceType(value === none ? '' : value as CivilEngineeringMigrationSource)}><SelectTrigger><SelectValue placeholder="Select configured source" /></SelectTrigger><SelectContent>{lookups.sourceTypes.map((value) => <SelectItem key={value} value={value}>{readable(value)}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Legacy register reference</Label><Input value={sourceRegisterReference} maxLength={180} onChange={(event) => setSourceRegisterReference(event.target.value)} placeholder="e.g. CE-ARCHIVE-2024-017" /></div></div><div className="space-y-3">{records.map((record, index) => <div key={index} className="grid gap-3 rounded-lg border p-3 md:grid-cols-4"><div className="space-y-1"><Label>Record category</Label><Select value={record.recordType} onValueChange={(value) => updateRecord(index, { recordType: value as CivilEngineeringMigrationRecordType, ...(value === 'PhysicalFileReference' ? {} : { physicalFileReference: undefined }) })}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{lookups.recordTypes.map((value) => <SelectItem key={value} value={value}>{readable(value)}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Legacy source reference</Label><Input value={record.sourceReference} maxLength={180} onChange={(event) => updateRecord(index, { sourceReference: event.target.value })} /></div><div className="space-y-1"><Label>Title</Label><Input value={record.title} maxLength={250} onChange={(event) => updateRecord(index, { title: event.target.value })} /></div><div className="space-y-1"><Label>Record date</Label><Input type="date" value={record.recordDate || ''} onChange={(event) => updateRecord(index, { recordDate: event.target.value || undefined })} /></div>{record.recordType === 'PhysicalFileReference' ? <div className="space-y-1 md:col-span-3"><Label>Physical file register reference</Label><Input value={record.physicalFileReference || ''} maxLength={500} disabled={!lookups.preservePhysicalFileReference} onChange={(event) => updateRecord(index, { physicalFileReference: event.target.value })} placeholder="Controlled archive/shelf/file reference" /></div> : <div className="space-y-1 md:col-span-3"><Label>Current Published central-DMS file</Label><Select value={record.centralDocumentVersionId || none} onValueChange={(value) => selectDocument(index, value)}><SelectTrigger><SelectValue placeholder="Select current Published DMS file" /></SelectTrigger><SelectContent><SelectItem value={none}>Select document</SelectItem>{lookups.documents.map((document) => <SelectItem key={document.centralDocumentVersionId} value={document.centralDocumentVersionId}>{document.documentReference} · v{document.versionNumber} · {document.title}</SelectItem>)}</SelectContent></Select></div>}<div className="flex items-end justify-end">{records.length > 1 ? <Button variant="ghost" size="icon" aria-label="Remove staged record" onClick={() => setRecords((current) => current.filter((_, itemIndex) => itemIndex !== index))}><Trash2 className="h-4 w-4" /></Button> : null}</div></div>)}</div><div className="flex flex-wrap gap-2"><Button type="button" variant="outline" onClick={() => setRecords((current) => [...current, newRecord()])}><Plus className="mr-2 h-4 w-4" />Add record</Button><Button type="button" disabled={saving} onClick={() => void stage()}><Send className="mr-2 h-4 w-4" />Validate and stage</Button></div></CardContent></Card> : null}
    <Card><CardHeader><CardTitle className="text-base">Migration batches</CardTitle><CardDescription>Validation errors remain in the staging register. A signed-off batch is ready for an owner-controlled posting process, not automatically posted.</CardDescription></CardHeader><CardContent className="space-y-4">{loading ? <p className="text-sm text-muted-foreground">Loading Civil migration batches…</p> : null}{!loading && !batches.length ? <p className="rounded border border-dashed p-3 text-sm text-muted-foreground">No Civil migration batches have been staged for this project.</p> : null}{batches.map((batch) => <article key={batch.id} className="rounded-lg border p-4"><div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between"><div><div className="flex flex-wrap items-center gap-2"><span className="font-medium">{readable(batch.sourceType)} · {batch.sourceRegisterReference}</span><Badge variant={batch.errorCount ? 'destructive' : 'secondary'}>{readable(batch.status)}</Badge></div><p className="mt-1 text-sm text-muted-foreground">{batch.recordCount} record(s) · {batch.errorCount} validation error(s) · staged {formatDate(batch.createdAt)}</p></div>{batch.isReadyForOwnerPosting ? <Badge variant="outline" className="h-fit"><CheckCircle2 className="mr-1 h-3.5 w-3.5" />Ready for owner posting</Badge> : null}</div>{batch.issues.length ? <div className="mt-3 space-y-1 rounded bg-destructive/5 p-3 text-sm">{batch.issues.map((issue, index) => <p key={`${issue.code}-${index}`}>{issue.sequence ? `Record ${issue.sequence}: ` : ''}{issue.message}</p>)}</div> : null}<div className="mt-3 overflow-x-auto"><table className="w-full min-w-[720px] text-sm"><thead className="text-left text-muted-foreground"><tr><th className="pb-2 pr-3">Type</th><th className="pb-2 pr-3">Source</th><th className="pb-2 pr-3">Title</th><th className="pb-2 pr-3">DMS / physical reference</th><th className="pb-2">Validation</th></tr></thead><tbody>{batch.records.map((record) => <tr key={record.sequence} className="border-t"><td className="py-2 pr-3">{readable(record.recordType)}</td><td className="py-2 pr-3">{record.sourceReference}</td><td className="py-2 pr-3">{record.title}</td><td className="py-2 pr-3">{record.documentReference || record.physicalFileReference || '—'}</td><td className="py-2">{record.validationStatus}</td></tr>)}</tbody></table></div>{canManage && batch.status === 'AwaitingReconciliation' ? <div className="mt-4 grid gap-3 border-t pt-4 md:grid-cols-[minmax(0,1fr)_minmax(0,2fr)_auto]"><div className="space-y-1"><Label>Reconciliation DMS evidence</Label><Select value={reconciliationDocumentId} onValueChange={setReconciliationDocumentId}><SelectTrigger><SelectValue placeholder="Select configured evidence" /></SelectTrigger><SelectContent><SelectItem value={none}>Select evidence</SelectItem>{lookups?.reconciliationDocuments.map((document) => <SelectItem key={document.centralDocumentVersionId} value={document.centralDocumentVersionId}>{document.documentReference} · {document.title}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label>Independent reconciliation declaration</Label><Input value={declarations[batch.id] || ''} maxLength={2000} onChange={(event) => setDeclarations((current) => ({ ...current, [batch.id]: event.target.value }))} /></div><div className="flex items-end"><Button disabled={saving} onClick={() => void reconcile(batch)}><FileCheck2 className="mr-2 h-4 w-4" />Reconcile</Button></div></div> : null}{canManage && batch.status === 'Reconciled' ? <div className="mt-4 grid gap-3 border-t pt-4 md:grid-cols-[minmax(0,1fr)_auto]"><div className="space-y-1"><Label>Independent sign-off declaration</Label><Input value={declarations[batch.id] || ''} maxLength={2000} onChange={(event) => setDeclarations((current) => ({ ...current, [batch.id]: event.target.value }))} /></div><div className="flex items-end"><Button disabled={saving} onClick={() => void signOff(batch)}><CheckCircle2 className="mr-2 h-4 w-4" />Sign off</Button></div></div> : null}{batch.isReadyForOwnerPosting ? <p className="mt-3 text-xs text-muted-foreground">{batch.postingBoundary}</p> : null}</article>)}</CardContent></Card>
  </div>;
}
