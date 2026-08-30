'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { ArrowRightLeft, RefreshCw, Send, ShieldX } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { civilEngineeringDevelopmentApprovalFileService } from '@/services/civil-engineering-development-approval-file.service';
import { civilEngineeringDevelopmentApprovalHandoffService } from '@/services/civil-engineering-development-approval-handoff.service';
import type { CivilEngineeringDevelopmentApprovalFile } from '@/types/civil-engineering-development-approval-file';
import type {
  CivilEngineeringDevelopmentApprovalHandoff,
  CivilEngineeringDevelopmentApprovalHandoffLookups,
  CivilEngineeringPermittingSection,
} from '@/types/civil-engineering-development-approval-handoff';

const managePermission = 'civil-engineering.permitting.manage';
const readPermission = 'civil-engineering.workspace.read';
const none = '__select__';
const sections: Record<CivilEngineeringPermittingSection, string> = {
  BuildingInspectorate: 'Building Inspectorate',
  Architecture: 'Architecture',
  CivilEngineering: 'Civil Engineering',
  HeadOfDepartment: 'Head of Department',
  GeodeticEngineering: 'Geodetic Engineering',
  TownPlanning: 'Town Planning',
  OtherTechnicalSection: 'Other technical section',
};

const formatDate = (value: string) => new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
}).format(new Date(value));

const errorText = (error: unknown, fallback: string) => {
  const value = error as { response?: { detail?: string; correlationId?: string }; message?: string };
  const detail = value.response?.detail || value.message || fallback;
  return `${detail}${value.response?.correlationId ? ` Reference: ${value.response.correlationId}` : ''}`;
};

const sectionLabel = (section: string) => sections[section as CivilEngineeringPermittingSection] ?? section;

export default function CivilEngineeringDevelopmentApprovalHandoffsPage() {
  const { hasPermission } = useAuth();
  const { toast } = useToast();
  const canRead = hasPermission(readPermission);
  const canManage = hasPermission(managePermission);
  const [files, setFiles] = useState<CivilEngineeringDevelopmentApprovalFile[]>([]);
  const [fileId, setFileId] = useState('');
  const [lookups, setLookups] = useState<CivilEngineeringDevelopmentApprovalHandoffLookups>();
  const [history, setHistory] = useState<CivilEngineeringDevelopmentApprovalHandoff[]>([]);
  const [toSection, setToSection] = useState<CivilEngineeringPermittingSection | ''>('');
  const [recipientRoleId, setRecipientRoleId] = useState(none);
  const [recipientUserId, setRecipientUserId] = useState(none);
  const [coverNote, setCoverNote] = useState('');
  const [handoffDueDate, setHandoffDueDate] = useState('');
  const [evidenceIds, setEvidenceIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const selectedFile = useMemo(() => files.find((item) => item.id === fileId), [files, fileId]);
  const selectedRole = useMemo(
    () => lookups?.recipientRoles.find((item) => item.roleId === recipientRoleId),
    [lookups, recipientRoleId],
  );
  const today = new Date().toISOString().slice(0, 10);
  const fileDueDate = selectedFile?.dueDate ? selectedFile.dueDate.slice(0, 10) : undefined;

  const load = useCallback(async (selectedId?: string) => {
    if (!canRead && !canManage) {
      setLoading(false);
      return;
    }

    setLoading(true);
    try {
      const source = canRead ? await civilEngineeringDevelopmentApprovalFileService.list() : [];
      setFiles(source);
      const id = selectedId || fileId || source[0]?.id || '';
      setFileId(id);
      if (id && canManage) {
        const [context, events] = await Promise.all([
          civilEngineeringDevelopmentApprovalHandoffService.lookups(id),
          civilEngineeringDevelopmentApprovalHandoffService.list(id),
        ]);
        setLookups(context);
        setHistory(events);
      } else {
        setLookups(undefined);
        setHistory([]);
      }
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Development-file routing could not be loaded',
        description: errorText(error, 'Refresh and try again.'),
      });
    } finally {
      setLoading(false);
    }
  }, [canManage, canRead, fileId, toast]);

  useEffect(() => {
    void load();
  }, [load]);

  const changeFile = (id: string) => {
    setToSection('');
    setRecipientRoleId(none);
    setRecipientUserId(none);
    setCoverNote('');
    setHandoffDueDate('');
    setEvidenceIds([]);
    void load(id);
  };

  const toggleEvidence = (versionId: string) => {
    setEvidenceIds((values) => values.includes(versionId)
      ? values.filter((item) => item !== versionId)
      : [...values, versionId]);
  };

  const submit = async () => {
    if (!fileId || !toSection || recipientRoleId === none || recipientUserId === none || !handoffDueDate) {
      toast({
        variant: 'destructive',
        title: 'Complete the controlled handoff',
        description: 'Choose a destination section, configured role, active recipient and due date.',
      });
      return;
    }

    setSaving(true);
    try {
      const evidence = (lookups?.documents ?? [])
        .filter((item) => evidenceIds.includes(item.centralDocumentVersionId))
        .map((item) => ({
          centralDocumentRecordId: item.centralDocumentRecordId,
          centralDocumentVersionId: item.centralDocumentVersionId,
        }));
      await civilEngineeringDevelopmentApprovalHandoffService.create(fileId, {
        clientRequestId: crypto.randomUUID(),
        toSection,
        recipientRoleId,
        recipientUserId,
        coverNote: coverNote.trim() || null,
        dueDate: handoffDueDate,
        evidence,
      });
      setToSection('');
      setRecipientRoleId(none);
      setRecipientUserId(none);
      setCoverNote('');
      setHandoffDueDate('');
      setEvidenceIds([]);
      toast({
        title: 'Development file handed off',
        description: 'The immutable routing event and its DMS evidence are now in the tenant audit history.',
      });
      await load(fileId);
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Development-file handoff was not recorded',
        description: errorText(error, 'Refresh the file and confirm that you are its active recipient with a configured CIV-CFG-009 role.'),
      });
    } finally {
      setSaving(false);
    }
  };

  if (!canRead && !canManage) {
    return <Alert><ShieldX className="h-4 w-4" /><AlertTitle>Civil permitting access required</AlertTitle><AlertDescription>You do not have access to development-file handoffs.</AlertDescription></Alert>;
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold"><ArrowRightLeft className="h-6 w-6" />Development-file handoffs</h1>
          <p className="mt-1 text-sm text-muted-foreground">Controlled, append-only movement of a site-inspected development file between responsible sections.</p>
        </div>
        <Button variant="outline" size="sm" onClick={() => void load(fileId)} disabled={loading || saving}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">Select development approval file</CardTitle></CardHeader>
        <CardContent>
          <Select value={fileId || none} onValueChange={changeFile}>
            <SelectTrigger className="max-w-2xl"><SelectValue placeholder="Select file" /></SelectTrigger>
            <SelectContent>
              <SelectItem value={none} disabled>Select file</SelectItem>
              {files.map((item) => <SelectItem key={item.id} value={item.id}>{item.fileNumber} · {item.applicationReference} · {item.applicantName}</SelectItem>)}
            </SelectContent>
          </Select>
          {selectedFile ? <p className="mt-2 text-sm text-muted-foreground">{selectedFile.projectLabel} · {selectedFile.propertyLabel} · Current section: {sectionLabel(selectedFile.currentSection)} · {selectedFile.status}</p> : null}
        </CardContent>
      </Card>

      {canManage && selectedFile && lookups ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Route file to the next section</CardTitle>
            <CardDescription>The destination role and recipient are restricted to the frozen CIV-CFG-009 handoff controls. The file must already have a governed site inspection.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
              <div className="space-y-1">
                <Label>Destination section</Label>
                <Select value={toSection || none} onValueChange={(value) => setToSection(value === none ? '' : value as CivilEngineeringPermittingSection)}>
                  <SelectTrigger><SelectValue placeholder="Select section" /></SelectTrigger>
                  <SelectContent><SelectItem value={none} disabled>Select section</SelectItem>{lookups.sections.map((item) => <SelectItem key={item} value={item}>{sectionLabel(item)}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label>Configured recipient role</Label>
                <Select value={recipientRoleId} onValueChange={(value) => { setRecipientRoleId(value); setRecipientUserId(none); }}>
                  <SelectTrigger><SelectValue placeholder="Select role" /></SelectTrigger>
                  <SelectContent><SelectItem value={none} disabled>Select role</SelectItem>{lookups.recipientRoles.map((item) => <SelectItem key={item.roleId} value={item.roleId}>{item.roleName}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label>Active recipient</Label>
                <Select value={recipientUserId} disabled={!selectedRole} onValueChange={setRecipientUserId}>
                  <SelectTrigger><SelectValue placeholder="Select recipient" /></SelectTrigger>
                  <SelectContent><SelectItem value={none} disabled>Select recipient</SelectItem>{selectedRole?.recipients.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label>Recipient due date</Label>
                <Input type="date" value={handoffDueDate} min={today} max={fileDueDate} onChange={(event) => setHandoffDueDate(event.target.value)} />
              </div>
              <div className="space-y-1">
                <Label>Cover note (optional)</Label>
                <Input value={coverNote} maxLength={2000} placeholder="Controlled routing context" onChange={(event) => setCoverNote(event.target.value)} />
              </div>
            </div>

            <div className="space-y-2">
              <Label>Current Published DMS drawings / technical evidence (optional)</Label>
              <div className="max-h-48 space-y-2 overflow-auto rounded-md border p-3">
                {lookups.documents.map((item) => <label key={item.centralDocumentVersionId} className="flex items-start gap-2 text-sm"><Checkbox checked={evidenceIds.includes(item.centralDocumentVersionId)} onCheckedChange={() => toggleEvidence(item.centralDocumentVersionId)} /><span>{item.documentReference} · v{item.versionNumber} · {item.title}</span></label>)}
                {!lookups.documents.length ? <p className="text-sm text-muted-foreground">No current Published document is available under the file’s active Civil DMS controls.</p> : null}
              </div>
            </div>
            <div className="flex justify-end"><Button disabled={saving || selectedFile.status !== 'SiteInspectionCompleted'} onClick={() => void submit()}><Send className="mr-2 h-4 w-4" />Record handoff</Button></div>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader><CardTitle className="text-base">Handoff timeline</CardTitle><CardDescription>Every movement is immutable. SCE review and HOD decision occur in their own permitting stages.</CardDescription></CardHeader>
        <CardContent className="space-y-3">
          {loading ? <p className="text-sm text-muted-foreground">Loading handoff history…</p> : null}
          {!loading && fileId && !history.length ? <p className="rounded border border-dashed p-3 text-sm text-muted-foreground">No inter-section handoff has been recorded for this file.</p> : null}
          {history.map((item) => (
            <article key={item.id} className="rounded-lg border p-4">
              <div className="flex flex-col gap-2 md:flex-row md:justify-between">
                <div>
                  <div className="flex flex-wrap items-center gap-2"><Badge variant="outline">#{item.sequenceNumber}</Badge><span className="font-medium">{sectionLabel(item.fromSection)} → {sectionLabel(item.toSection)}</span></div>
                  <p className="mt-2 text-sm">{item.fromUserName} routed to {item.recipientUserName} ({item.recipientRoleName})</p>
                  <p className="mt-1 text-xs text-muted-foreground">Recipient due {formatDate(item.dueDate)}</p>
                  {item.coverNote ? <p className="mt-1 whitespace-pre-wrap break-words text-sm text-muted-foreground">{item.coverNote}</p> : null}
                  {item.evidence.length ? <p className="mt-2 text-xs text-muted-foreground">DMS evidence: {item.evidence.map((value) => value.documentReference).filter(Boolean).join(', ')}</p> : null}
                </div>
                <p className="text-xs text-muted-foreground">Sent {formatDate(item.createdAt)}</p>
              </div>
            </article>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
