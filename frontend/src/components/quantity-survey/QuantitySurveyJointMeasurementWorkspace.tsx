'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Download, FileUp, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  quantitySurveyJointMeasurementService as service,
  type JointMeasurement,
  type JointMeasurementEvidenceType,
  type JointMeasurementLookups,
  type JointMeasurementRevision,
} from '@/services/quantity-survey-joint-measurement.service';

type Props = { projectId: string; external?: boolean };
type RequestState = { fingerprint: string; id: string };

const emptyLookups: JointMeasurementLookups = {
  approvedBoqLines: [],
  contractorPartners: [],
  consultantPartners: [],
  recordedMeasurements: [],
};
const evidenceTypes: JointMeasurementEvidenceType[] = [
  'RequestEvidence',
  'SitePhoto',
  'AttendanceRecord',
  'SignedMeasurementRecord',
  'SupportingDocument',
];
const terminalStatuses = new Set(['Applied', 'Rejected', 'Cancelled']);
const dateTimeValue = (value?: string | null) =>
  value ? new Date(value).toLocaleString() : '—';
const asIso = (value: string) =>
  value ? new Date(value).toISOString() : undefined;
const statusLabel = (value: string) =>
  value.replace(/([a-z])([A-Z])/g, '$1 $2');
const download = (blob: Blob, fileName: string) => {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
};

export function QuantitySurveyJointMeasurementWorkspace({
  projectId,
  external = false,
}: Props) {
  const { hasPermission } = useAuth();
  const canManage = external || hasPermission('quantity-survey.measurements.manage');
  const canApprove = !external && hasPermission('quantity-survey.transactions.approve');
  const canAudit = !external && hasPermission('quantity-survey.audit.read');
  const requestIds = useRef<Record<string, RequestState>>({});
  const [loading, setLoading] = useState(false);
  const [lookups, setLookups] = useState(emptyLookups);
  const [items, setItems] = useState<JointMeasurement[]>([]);
  const [selected, setSelected] = useState<JointMeasurement>();
  const [boqLineId, setBoqLineId] = useState('');
  const [contractorId, setContractorId] = useState('');
  const [title, setTitle] = useState('');
  const [requestReason, setRequestReason] = useState('');
  const [proposedQuantity, setProposedQuantity] = useState('');
  const [requestedLocation, setRequestedLocation] = useState('');
  const [preferredStart, setPreferredStart] = useState('');
  const [preferredEnd, setPreferredEnd] = useState('');
  const [consultantId, setConsultantId] = useState('');
  const [scheduledStart, setScheduledStart] = useState('');
  const [scheduledEnd, setScheduledEnd] = useState('');
  const [scheduledLocation, setScheduledLocation] = useState('');
  const [measurementId, setMeasurementId] = useState('');
  const [actionReason, setActionReason] = useState('');
  const [attendanceNotes, setAttendanceNotes] = useState('');
  const [endorsementNotes, setEndorsementNotes] = useState('');
  const [attested, setAttested] = useState(false);
  const [evidenceType, setEvidenceType] =
    useState<JointMeasurementEvidenceType>('SupportingDocument');
  const [evidenceTitle, setEvidenceTitle] = useState('');
  const [file, setFile] = useState<File>();
  const [history, setHistory] = useState<JointMeasurementRevision[]>([]);

  const requestId = (key: string, payload: unknown) => {
    const fingerprint = JSON.stringify(payload);
    const current = requestIds.current[key];
    if (current?.fingerprint === fingerprint) return current.id;
    const next = { fingerprint, id: crypto.randomUUID() };
    requestIds.current[key] = next;
    return next.id;
  };
  const complete = (key: string) => delete requestIds.current[key];

  const load = useCallback(
    async (preferredId?: string) => {
      setLoading(true);
      try {
        const [nextLookups, page] = await Promise.all([
          service.lookups(projectId, external),
          service.list(projectId, external),
        ]);
        setLookups(nextLookups);
        setItems(page.items);
        setSelected((current) =>
          page.items.find((item) => item.id === (preferredId ?? current?.id)) ??
          page.items[0]
        );
      } catch (error) {
        toast.error(error instanceof Error ? error.message : 'Failed to load joint measurements');
      } finally {
        setLoading(false);
      }
    },
    [external, projectId]
  );

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!canAudit || !selected) {
      setHistory([]);
      return;
    }
    void service.history(selected.id).then(setHistory).catch(() => setHistory([]));
  }, [canAudit, selected]);

  const myPartnerId = external ? lookups.contractorPartners[0]?.id : undefined;
  const myParticipants = useMemo(
    () =>
      selected?.participants.filter((participant) =>
        external
          ? participant.businessPartnerId === myPartnerId
          : participant.participantType === 'InternalRole'
      ) ?? [],
    [external, myPartnerId, selected]
  );

  const run = async (
    key: string,
    action: () => Promise<JointMeasurement>,
    success: string
  ) => {
    setLoading(true);
    try {
      const value = await action();
      complete(key);
      setSelected(value);
      await load(value.id);
      toast.success(success);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'The action could not be completed');
    } finally {
      setLoading(false);
    }
  };

  const create = async () => {
    if (!boqLineId || (!external && !contractorId) || title.trim().length < 3 || requestReason.trim().length < 10) {
      toast.error('Select the approved BoQ item and contractor, then enter a title and reason.');
      return;
    }
    if (preferredStart && preferredEnd && new Date(preferredEnd) <= new Date(preferredStart)) {
      toast.error('Preferred end must be after preferred start.');
      return;
    }
    const payload = {
      projectId,
      projectBoqVersionLineId: boqLineId,
      contractorBusinessPartnerId: external ? undefined : contractorId,
      title: title.trim(),
      reason: requestReason.trim(),
      contractorProposedQuantity: proposedQuantity ? Number(proposedQuantity) : undefined,
      requestedSiteLocation: requestedLocation.trim() || undefined,
      preferredStartAt: asIso(preferredStart),
      preferredEndAt: asIso(preferredEnd),
    };
    await run(
      'create',
      () => service.create(projectId, { ...payload, clientRequestId: requestId('create', payload) }, external),
      'Joint remeasurement request created'
    );
  };

  const lifecycle = async (
    name: 'submit' | 'review' | 'approve' | 'reject',
    success: string
  ) => {
    if (!selected || actionReason.trim().length < 5) {
      toast.error('Enter a reason of at least five characters.');
      return;
    }
    const payload = { reason: actionReason.trim(), rowVersion: selected.rowVersion };
    const key = `${name}:${selected.id}`;
    const request = { ...payload, clientRequestId: requestId(key, payload) };
    await run(
      key,
      () =>
        name === 'submit'
          ? service.submit(projectId, selected.id, request, external)
          : name === 'review'
            ? service.review(selected.id, request)
            : name === 'approve'
              ? service.approve(selected.id, request)
              : service.reject(selected.id, request),
      success
    );
  };

  const schedule = async () => {
    if (!selected || !consultantId || !scheduledStart || !scheduledEnd || scheduledLocation.trim().length < 3) {
      toast.error('Select the consultant and complete the schedule.');
      return;
    }
    if (new Date(scheduledEnd) <= new Date(scheduledStart)) {
      toast.error('Scheduled end must be after scheduled start.');
      return;
    }
    const payload = {
      consultantBusinessPartnerId: consultantId,
      scheduledStartAt: asIso(scheduledStart),
      scheduledEndAt: asIso(scheduledEnd),
      siteLocation: scheduledLocation.trim(),
      rowVersion: selected.rowVersion,
    };
    const key = `schedule:${selected.id}`;
    await run(
      key,
      () => service.schedule(selected.id, { ...payload, clientRequestId: requestId(key, payload) }),
      'Joint measurement scheduled'
    );
  };

  const linkMeasurement = async () => {
    if (!selected || !measurementId) {
      toast.error('Select a Recorded measurement sheet.');
      return;
    }
    const payload = { measurementSheetId: measurementId, rowVersion: selected.rowVersion };
    const key = `measurement:${selected.id}`;
    await run(
      key,
      () => service.linkMeasurement(selected.id, { ...payload, clientRequestId: requestId(key, payload) }),
      'Recorded measurement linked'
    );
  };

  const attend = async (participantId: string) => {
    if (!selected) return;
    const payload = { notes: attendanceNotes.trim() || undefined, rowVersion: selected.rowVersion };
    const key = `attendance:${selected.id}:${participantId}`;
    await run(
      key,
      () => service.attend(projectId, selected.id, participantId, { ...payload, clientRequestId: requestId(key, payload) }, external),
      'Attendance recorded'
    );
  };

  const endorse = async (participantId: string) => {
    if (!selected || !attested) {
      toast.error('Confirm the displayed attestation before endorsing.');
      return;
    }
    const payload = {
      notes: endorsementNotes.trim() || undefined,
      rowVersion: selected.rowVersion,
      signature: {
        method: 0,
        attestation: selected.endorsementAttestation,
        signedAt: new Date().toISOString(),
      },
    };
    const key = `endorse:${selected.id}:${participantId}`;
    await run(
      key,
      () => service.endorse(projectId, selected.id, participantId, { ...payload, clientRequestId: requestId(key, payload) }),
      'Joint measurement endorsed'
    );
    setAttested(false);
  };

  const addEvidence = async () => {
    if (!selected || !file || evidenceTitle.trim().length < 3) {
      toast.error('Select a file and enter an evidence title.');
      return;
    }
    const key = `evidence:${selected.id}:${file.name}:${evidenceType}`;
    setLoading(true);
    try {
      await service.addEvidence(
        projectId,
        selected.id,
        {
          clientRequestId: requestId(key, { file: file.name, fileSize: file.size, evidenceType, evidenceTitle }),
          evidenceType,
          title: evidenceTitle.trim(),
          file,
        },
        external
      );
      complete(key);
      setFile(undefined);
      setEvidenceTitle('');
      await load(selected.id);
      toast.success('Evidence stored in the central document repository');
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Evidence upload failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      {canManage && (
        <Card>
          <CardHeader className="pb-3"><CardTitle className="text-base">New remeasurement request</CardTitle></CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <div className="grid gap-1.5 xl:col-span-2">
              <Label>Approved BoQ item</Label>
              <Select value={boqLineId} onValueChange={setBoqLineId}>
                <SelectTrigger><SelectValue placeholder="Select approved BoQ item" /></SelectTrigger>
                <SelectContent>{lookups.approvedBoqLines.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            {!external && <div className="grid gap-1.5 xl:col-span-2"><Label>Contractor</Label><Select value={contractorId} onValueChange={setContractorId}><SelectTrigger><SelectValue placeholder="Select contractor" /></SelectTrigger><SelectContent>{lookups.contractorPartners.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div>}
            <div className="grid gap-1.5 xl:col-span-2"><Label>Title</Label><Input value={title} onChange={(event) => setTitle(event.target.value)} /></div>
            <div className="grid gap-1.5"><Label>Proposed quantity</Label><Input type="number" min="0.0001" step="0.0001" value={proposedQuantity} onChange={(event) => setProposedQuantity(event.target.value)} /></div>
            <div className="grid gap-1.5"><Label>Site location</Label><Input value={requestedLocation} onChange={(event) => setRequestedLocation(event.target.value)} /></div>
            <div className="grid gap-1.5"><Label>Preferred start</Label><Input type="datetime-local" value={preferredStart} onChange={(event) => setPreferredStart(event.target.value)} /></div>
            <div className="grid gap-1.5"><Label>Preferred end</Label><Input type="datetime-local" value={preferredEnd} onChange={(event) => setPreferredEnd(event.target.value)} /></div>
            <div className="grid gap-1.5 md:col-span-2 xl:col-span-4"><Label>Reason</Label><Textarea rows={2} value={requestReason} onChange={(event) => setRequestReason(event.target.value)} /></div>
            <Button disabled={loading} onClick={create}>Create request</Button>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-4 lg:grid-cols-[260px,1fr]">
        <Card>
          <CardHeader className="flex-row items-center justify-between space-y-0 pb-3">
            <CardTitle className="text-base">Requests</CardTitle>
            <Button size="icon" variant="ghost" disabled={loading} onClick={() => void load(selected?.id)}><RefreshCw className="h-4 w-4" /></Button>
          </CardHeader>
          <CardContent className="space-y-2">
            {items.map((item) => (
              <button key={item.id} type="button" onClick={() => setSelected(item)} className={`w-full rounded-md border p-3 text-left text-sm ${selected?.id === item.id ? 'border-blue-500 bg-blue-50/60 dark:bg-blue-950/30' : ''}`}>
                <div className="font-medium">{item.requestNumber}</div>
                <div className="truncate text-muted-foreground">{item.title}</div>
                <Badge variant="outline" className="mt-2">{statusLabel(item.status)}</Badge>
              </button>
            ))}
            {!items.length && <div className="text-sm text-muted-foreground">No joint-measurement requests.</div>}
          </CardContent>
        </Card>

        {selected ? (
          <div className="space-y-4">
            <Card>
              <CardContent className="space-y-4 p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div><div className="text-lg font-semibold">{selected.requestNumber} · {selected.title}</div><div className="text-sm text-muted-foreground">{selected.boqLineLabel}</div></div>
                  <div className="flex gap-2"><Badge>{statusLabel(selected.status)}</Badge><Badge variant="outline">{selected.approvalStatus}</Badge></div>
                </div>
                <div className="grid gap-3 text-sm sm:grid-cols-2 xl:grid-cols-4">
                  <div><div className="text-muted-foreground">Contractor</div><div className="font-medium">{selected.contractorName}</div></div>
                  <div><div className="text-muted-foreground">Consultant</div><div className="font-medium">{selected.consultantName || 'Not assigned'}</div></div>
                  <div><div className="text-muted-foreground">Approved quantity</div><div className="font-medium">{selected.previousQuantity.toLocaleString()} {selected.unitOfMeasure || ''}</div></div>
                  <div><div className="text-muted-foreground">Recorded quantity</div><div className="font-medium">{selected.recordedQuantity?.toLocaleString() ?? 'Not linked'}</div></div>
                  <div><div className="text-muted-foreground">Schedule</div><div className="font-medium">{dateTimeValue(selected.scheduledStartAt)}</div></div>
                  <div><div className="text-muted-foreground">Site</div><div className="font-medium">{selected.scheduledSiteLocation || selected.requestedSiteLocation || 'Not set'}</div></div>
                  <div><div className="text-muted-foreground">Measurement</div><div className="font-medium">{selected.measurementSheetReference || 'Not linked'}</div></div>
                  <div><div className="text-muted-foreground">BoQ revision</div><div className="font-medium">{selected.remeasurementVersionNumber ? `v${selected.remeasurementVersionNumber}` : 'Not created'}</div></div>
                </div>
                <div className="rounded-md border bg-muted/20 p-3 text-sm">{selected.reason}</div>
              </CardContent>
            </Card>

            {!external && canManage && selected.status === 'Submitted' && (
              <Card><CardHeader className="pb-3"><CardTitle className="text-base">Schedule joint measurement</CardTitle></CardHeader><CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <div className="grid gap-1.5"><Label>Consultant</Label><Select value={consultantId} onValueChange={setConsultantId}><SelectTrigger><SelectValue placeholder="Select consultant" /></SelectTrigger><SelectContent>{lookups.consultantPartners.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-1.5"><Label>Site location</Label><Input value={scheduledLocation} onChange={(event) => setScheduledLocation(event.target.value)} /></div>
                <div className="grid gap-1.5"><Label>Start</Label><Input type="datetime-local" value={scheduledStart} onChange={(event) => setScheduledStart(event.target.value)} /></div>
                <div className="grid gap-1.5"><Label>End</Label><Input type="datetime-local" value={scheduledEnd} onChange={(event) => setScheduledEnd(event.target.value)} /></div>
                <Button disabled={loading} onClick={schedule}>Schedule</Button>
              </CardContent></Card>
            )}

            {!external && canManage && !selected.measurementSheetId && ['Scheduled', 'AwaitingAttendance'].includes(selected.status) && (
              <Card><CardContent className="flex flex-wrap items-end gap-3 p-4"><div className="grid min-w-[300px] flex-1 gap-1.5"><Label>Recorded measurement for this BoQ item</Label><Select value={measurementId} onValueChange={setMeasurementId}><SelectTrigger><SelectValue placeholder="Select Recorded measurement" /></SelectTrigger><SelectContent>{lookups.recordedMeasurements.map((item) => <SelectItem key={item.id} value={item.id}>{item.label}</SelectItem>)}</SelectContent></Select></div><Button disabled={loading} onClick={linkMeasurement}>Link measurement</Button></CardContent></Card>
            )}

            {myParticipants.length > 0 && ['Scheduled', 'AwaitingAttendance', 'AwaitingEndorsements'].includes(selected.status) && (
              <Card><CardHeader className="pb-3"><CardTitle className="text-base">My attendance and endorsement</CardTitle></CardHeader><CardContent className="space-y-3">
                <Textarea rows={2} placeholder="Attendance or endorsement note (optional)" value={selected.status === 'AwaitingEndorsements' ? endorsementNotes : attendanceNotes} onChange={(event) => selected.status === 'AwaitingEndorsements' ? setEndorsementNotes(event.target.value) : setAttendanceNotes(event.target.value)} />
                {selected.status === 'AwaitingEndorsements' && <label className="flex items-start gap-2 rounded-md border p-3 text-sm"><Checkbox checked={attested} onCheckedChange={(value) => setAttested(value === true)} /><span>{selected.endorsementAttestation}</span></label>}
                {myParticipants.map((participant) => <div key={participant.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3"><div><div className="font-medium">{participant.businessPartnerName || participant.requiredRoleName || participant.participantType}</div><div className="text-sm text-muted-foreground">{participant.attendanceStatus}{participant.attendedByName ? ` · ${participant.attendedByName}` : ''}</div></div>{participant.attendanceStatus !== 'Attended' && ['Scheduled', 'AwaitingAttendance'].includes(selected.status) ? <Button disabled={loading} onClick={() => void attend(participant.id)}>Record attendance</Button> : external && selected.status === 'AwaitingEndorsements' && !selected.endorsements.some((item) => item.businessPartnerId === participant.businessPartnerId) ? <Button disabled={loading || !attested} onClick={() => void endorse(participant.id)}>Endorse</Button> : null}</div>)}
              </CardContent></Card>
            )}

            {!terminalStatuses.has(selected.status) && !['PendingApproval', 'ApprovedPendingBoqRevision', 'BoqWorkflowPending'].includes(selected.status) && (
              <Card><CardHeader className="pb-3"><CardTitle className="text-base">Evidence</CardTitle></CardHeader><CardContent className="grid gap-3 md:grid-cols-[180px,1fr,1fr,auto]"><Select value={evidenceType} onValueChange={(value) => setEvidenceType(value as JointMeasurementEvidenceType)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{evidenceTypes.map((item) => <SelectItem key={item} value={item}>{statusLabel(item)}</SelectItem>)}</SelectContent></Select><Input placeholder="Evidence title" value={evidenceTitle} onChange={(event) => setEvidenceTitle(event.target.value)} /><Input type="file" onChange={(event) => setFile(event.target.files?.[0])} /><Button disabled={loading} onClick={addEvidence}><FileUp className="mr-2 h-4 w-4" />Upload</Button></CardContent></Card>
            )}

            <Card><CardHeader className="pb-3"><CardTitle className="text-base">Participants and evidence</CardTitle></CardHeader><CardContent className="grid gap-4 xl:grid-cols-2"><div className="space-y-2">{selected.participants.map((participant) => <div key={participant.id} className="rounded-md border p-3 text-sm"><div className="font-medium">{participant.businessPartnerName || participant.requiredRoleName || participant.participantType}</div><div className="text-muted-foreground">{participant.participantType} · {participant.attendanceStatus}</div></div>)}</div><div className="space-y-2">{selected.evidence.map((item) => <div key={item.id} className="flex items-center justify-between rounded-md border p-3 text-sm"><div><div className="font-medium">{item.title}</div><div className="text-muted-foreground">{item.originalFileName} · {statusLabel(String(item.evidenceType))}</div></div><Button size="icon" variant="ghost" onClick={async () => download(await service.evidenceContent(projectId, selected.id, item.id, external), item.originalFileName)}><Download className="h-4 w-4" /></Button></div>)}</div></CardContent></Card>

            {!external && (canManage || canApprove) && (
              <Card><CardContent className="space-y-3 p-4"><Textarea rows={2} placeholder="Reason for this lifecycle action" value={actionReason} onChange={(event) => setActionReason(event.target.value)} /><div className="flex flex-wrap gap-2">{canManage && selected.status === 'Draft' && <Button disabled={loading} onClick={() => void lifecycle('submit', 'Request submitted')}>Submit request</Button>}{canManage && selected.status === 'ReadyForReview' && <Button disabled={loading} onClick={() => void lifecycle('review', 'Submitted to approval workflow')}>Submit for approval</Button>}{canApprove && selected.status === 'PendingApproval' && <Button disabled={loading} onClick={() => void lifecycle('approve', 'Joint measurement approved')}>Approve</Button>}{canApprove && selected.status === 'PendingApproval' && <Button variant="destructive" disabled={loading} onClick={() => void lifecycle('reject', 'Joint measurement rejected')}>Reject</Button>}</div></CardContent></Card>
            )}
            {external && selected.status === 'Draft' && <Card><CardContent className="space-y-3 p-4"><Textarea rows={2} placeholder="Submission reason" value={actionReason} onChange={(event) => setActionReason(event.target.value)} /><Button disabled={loading} onClick={() => void lifecycle('submit', 'Request submitted')}>Submit request</Button></CardContent></Card>}

            {canAudit && history.length > 0 && <Card><CardHeader className="pb-3"><CardTitle className="text-base">Audit history</CardTitle></CardHeader><CardContent className="space-y-2">{history.map((item) => <div key={item.id} className="rounded-md border p-3 text-sm"><div className="font-medium">{statusLabel(item.action)}</div><div className="text-muted-foreground">{item.actorName} · {dateTimeValue(item.createdAt)} · {item.correlationId}</div>{item.reason && <div className="mt-1">{item.reason}</div>}</div>)}</CardContent></Card>}
          </div>
        ) : <Card><CardContent className="py-16 text-center text-muted-foreground">Select or create a joint-measurement request.</CardContent></Card>}
      </div>
    </div>
  );
}
