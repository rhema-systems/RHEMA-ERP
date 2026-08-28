'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Loader2, UserPlus, Send, Gavel, Lock, Scale, CalendarPlus, Paperclip, Link2,
  Download, Trash2, Ban, CheckCircle2, AlertTriangle, EyeOff, FileText,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { employeeRelationsService } from '@/services/hr/employee-relations.service';
import { unionService } from '@/services/hr/union.service';
import { disciplineService } from '@/services/hr/discipline.service';
import { pipService } from '@/services/hr/pip.service';
import { safetyIncidentService } from '@/services/hr/safety-incident.service';
import {
  GRIEVANCE_LADDER, ER_CASE_TYPE_OPTIONS, GRIEVANCE_PARTY_ROLE_OPTIONS,
  CONFERENCE_TYPE_OPTIONS, RESOLUTION_OUTCOME_OPTIONS, DOCUMENT_SCOPE_OPTIONS,
  LINK_SOURCE_OPTIONS, SETTLED_GRIEVANCE_STATUSES,
  type Grievance, type GrievanceConference, type GrievancePartyRole,
  type GrievanceConferenceType, type GrievanceResolutionOutcome,
  type GrievanceDocumentScope, type EmployeeRelationsLinkSource,
} from '@/types/hr/employee-relations';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const fmtSize = (n: number) => (n < 1024 ? `${n} B` : n < 1048576 ? `${(n / 1024).toFixed(1)} KB` : `${(n / 1048576).toFixed(1)} MB`);
const levelLabel = (v: string) => GRIEVANCE_LADDER.find((l) => l.value === v)?.label ?? v;
const typeLabel = (v: string) => ER_CASE_TYPE_OPTIONS.find((t) => t.value === v)?.label ?? v;

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className="text-sm">{value ?? '—'}</div>
    </div>
  );
}

/**
 * ⚠ Distinguishes "nothing was written" from "you may not read it".
 *
 * A conference's notes arrive NULL both when none were taken and when the reader is neither HR nor
 * that conference's chair, with `notesRedacted` as the only thing separating the two. Rendering
 * both as an empty box tells the reader a falsehood in one of the two cases — and it is the more
 * consequential one, because a mediation whose notes are withheld looks like a mediation nobody
 * bothered to minute.
 */
function RedactableNotes({ notes, redacted }: { notes?: string | null; redacted: boolean }) {
  if (redacted) {
    return (
      <span className="inline-flex items-center gap-1 text-sm text-muted-foreground">
        <EyeOff className="h-3 w-3" /> Withheld — notes are visible to HR and the chair only
      </span>
    );
  }
  return <span className="whitespace-pre-wrap text-sm">{notes || '— none recorded —'}</span>;
}

const CASE_KEY = (id: string) => ['hr', 'employee-relations', 'case', id];

export default function EmployeeRelationsCaseFilePage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: c, isLoading, isError } = useQuery({
    queryKey: CASE_KEY(id),
    queryFn: () => employeeRelationsService.getById(id),
  });

  /**
   * Every write on this controller answers with the WHOLE case file, so the response seeds the
   * cache directly and the screen re-renders from the server's own view rather than from a guess.
   * That is why slice 9 moved all 25 `ToDto` call sites to `ToDtoAsync`: a response missing its
   * links would make the cross-references vanish from this screen on an unrelated save.
   *
   * Only the register is invalidated — invalidating the case key too would refetch what we were
   * just handed and flash the screen for nothing.
   */
  const useCaseAction = <TVars,>(fn: (v: TVars) => Promise<Grievance>, failureTitle: string, onDone?: () => void) =>
    useMutation({
      mutationFn: fn,
      onSuccess: (updated) => {
        queryClient.setQueryData(CASE_KEY(id), updated);
        queryClient.invalidateQueries({ queryKey: ['hr', 'employee-relations', 'register'] });
        onDone?.();
      },
      // The server's own refusal text. These rules — natural justice, the frozen decision, the
      // exhausted ladder — cannot be restated in the UI without drifting from them.
      onError: (e: any) => toast({ title: failureTitle, description: e?.message, variant: 'destructive' }),
    });

  // ── Dialog state ───────────────────────────────────────────────────────────
  const [assignOpen, setAssignOpen] = useState(false);
  const [assignee, setAssignee] = useState<string | null>(null);

  const [respondOpen, setRespondOpen] = useState(false);
  const [responseText, setResponseText] = useState('');
  const [resolvesIt, setResolvesIt] = useState(false);

  const [interpretOpen, setInterpretOpen] = useState(false);
  const [interpretation, setInterpretation] = useState('');

  const [partyOpen, setPartyOpen] = useState(false);
  const [partyRole, setPartyRole] = useState<GrievancePartyRole>('Respondent');
  const [partyEmployeeId, setPartyEmployeeId] = useState<string | null>(null);
  const [partyExternalName, setPartyExternalName] = useState('');
  const [partyExternalOrg, setPartyExternalOrg] = useState('');
  const [partyRepresents, setPartyRepresents] = useState<string>('none');
  const [partyUnion, setPartyUnion] = useState<string>('none');
  const [partyNotes, setPartyNotes] = useState('');

  const [standDownId, setStandDownId] = useState<string | null>(null);
  const [standDownReason, setStandDownReason] = useState('');

  const [openInvOpen, setOpenInvOpen] = useState(false);
  const [invEmployeeId, setInvEmployeeId] = useState<string | null>(null);
  const [invExternalName, setInvExternalName] = useState('');
  const [invExternalOrg, setInvExternalOrg] = useState('');
  const [invTarget, setInvTarget] = useState('');

  const [invFindings, setInvFindings] = useState('');
  const [invEvidence, setInvEvidence] = useState('');
  const [invRecommendation, setInvRecommendation] = useState('');
  const [completeInvOpen, setCompleteInvOpen] = useState(false);

  const [confOpen, setConfOpen] = useState(false);
  const [confType, setConfType] = useState<GrievanceConferenceType>('CaseConference');
  const [confWhen, setConfWhen] = useState('');
  const [confVenue, setConfVenue] = useState('');
  const [confChairId, setConfChairId] = useState<string | null>(null);
  const [confExternalChair, setConfExternalChair] = useState('');
  const [confExternalChairOrg, setConfExternalChairOrg] = useState('');
  const [confUnion, setConfUnion] = useState<string>('none');
  const [confPurpose, setConfPurpose] = useState('');

  const [holdFor, setHoldFor] = useState<GrievanceConference | null>(null);
  const [holdOutcome, setHoldOutcome] = useState('');
  const [holdNotes, setHoldNotes] = useState('');
  const [holdAttendance, setHoldAttendance] = useState<Record<string, boolean>>({});

  const [cancelFor, setCancelFor] = useState<GrievanceConference | null>(null);
  const [cancelReason, setCancelReason] = useState('');

  const [editFor, setEditFor] = useState<GrievanceConference | null>(null);
  const [editWhen, setEditWhen] = useState('');
  const [editVenue, setEditVenue] = useState('');
  const [editPurpose, setEditPurpose] = useState('');

  const [attendeeFor, setAttendeeFor] = useState<GrievanceConference | null>(null);
  const [attendeeEmployeeId, setAttendeeEmployeeId] = useState<string | null>(null);
  const [attendeeExternal, setAttendeeExternal] = useState('');
  const [attendeeOrg, setAttendeeOrg] = useState('');
  const [attendeeCapacity, setAttendeeCapacity] = useState('');

  const [uploadOpen, setUploadOpen] = useState(false);
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [uploadScope, setUploadScope] = useState<GrievanceDocumentScope>('Case');
  const [uploadDescription, setUploadDescription] = useState('');
  const [uploadStepId, setUploadStepId] = useState<string>('');
  const [uploadConferenceId, setUploadConferenceId] = useState<string>('');
  const [uploadSignedDate, setUploadSignedDate] = useState('');

  const [resolveOpen, setResolveOpen] = useState(false);
  const [outcome, setOutcome] = useState<Exclude<GrievanceResolutionOutcome, 'NotRecorded'>>('UpheldInPart');
  const [decision, setDecision] = useState('');
  const [remedy, setRemedy] = useState('');

  const [closeOpen, setCloseOpen] = useState(false);
  const [closureReason, setClosureReason] = useState('');

  const [linkOpen, setLinkOpen] = useState(false);
  const [linkSource, setLinkSource] = useState<EmployeeRelationsLinkSource>('DisciplinaryCase');
  const [linkSubject, setLinkSubject] = useState<string>('');
  const [linkRecordId, setLinkRecordId] = useState<string>('');

  // ── Mutations. Fixed order, every one of them. ─────────────────────────────
  const assign = useCaseAction((assignedToId: string) => employeeRelationsService.assign(id, { assignedToId }),
    'Not assigned', () => { setAssignOpen(false); setAssignee(null); });

  const respond = useCaseAction(() => employeeRelationsService.respond(id, {
    response: responseText.trim(), resolvesGrievance: resolvesIt,
  }), 'Not recorded', () => { setRespondOpen(false); setResponseText(''); setResolvesIt(false); });

  const recordInterpretation = useCaseAction(() => employeeRelationsService.recordHrInterpretation(id, {
    interpretation: interpretation.trim(),
  }), 'Not recorded', () => setInterpretOpen(false));

  const addParty = useCaseAction(() => employeeRelationsService.addParty(id, {
    role: partyRole,
    ...(partyEmployeeId ? { employeeId: partyEmployeeId } : { externalName: partyExternalName.trim() }),
    ...(!partyEmployeeId && partyExternalOrg.trim() ? { externalOrganisation: partyExternalOrg.trim() } : {}),
    ...(partyRepresents !== 'none' ? { representsEmployeeId: partyRepresents } : {}),
    ...(partyUnion !== 'none' ? { unionId: partyUnion } : {}),
    ...(partyNotes.trim() ? { notes: partyNotes.trim() } : {}),
  }), 'Not added', () => {
    setPartyOpen(false); setPartyEmployeeId(null); setPartyExternalName(''); setPartyExternalOrg('');
    setPartyRepresents('none'); setPartyUnion('none'); setPartyNotes('');
  });

  const standDown = useCaseAction((partyId: string) => employeeRelationsService.removeParty(id, partyId, {
    reason: standDownReason.trim(),
  }), 'Not stood down', () => { setStandDownId(null); setStandDownReason(''); });

  const openInvestigation = useCaseAction(() => employeeRelationsService.openInvestigation(id, {
    ...(invEmployeeId
      ? { investigatorId: invEmployeeId }
      : { externalInvestigatorName: invExternalName.trim(), externalInvestigatorOrganisation: invExternalOrg.trim() || undefined }),
    ...(invTarget ? { targetDate: invTarget } : {}),
  }), 'Not opened', () => { setOpenInvOpen(false); setInvEmployeeId(null); setInvExternalName(''); setInvExternalOrg(''); setInvTarget(''); });

  const updateInvestigation = useCaseAction(() => employeeRelationsService.updateInvestigation(id, {
    findings: invFindings.trim() || null,
    evidenceCollected: invEvidence.trim() || null,
    recommendation: invRecommendation.trim() || null,
    ...(invTarget ? { targetDate: invTarget } : {}),
  }), 'Not saved');

  const completeInvestigation = useCaseAction(() => employeeRelationsService.completeInvestigation(id, {
    findings: invFindings.trim(),
    evidenceCollected: invEvidence.trim() || null,
    recommendation: invRecommendation.trim() || null,
  }), 'Not completed', () => setCompleteInvOpen(false));

  const scheduleConference = useCaseAction(() => employeeRelationsService.scheduleConference(id, {
    conferenceType: confType,
    scheduledFor: confWhen,
    venue: confVenue.trim() || null,
    ...(confChairId ? { chairId: confChairId } : confExternalChair.trim() ? {
      externalChairName: confExternalChair.trim(),
      externalChairOrganisation: confExternalChairOrg.trim() || undefined,
    } : {}),
    ...(confUnion !== 'none' ? { unionId: confUnion } : {}),
    purpose: confPurpose.trim() || null,
  }), 'Not scheduled', () => {
    setConfOpen(false); setConfWhen(''); setConfVenue(''); setConfChairId(null);
    setConfExternalChair(''); setConfExternalChairOrg(''); setConfUnion('none'); setConfPurpose('');
  });

  // ⚠ These take their target as a VARIABLE rather than reading it back out of dialog state.
  // Closing over `editFor` / `holdFor` meant asserting non-null at the moment the request was
  // built, which the lint rule forbids and is right to: the dialog can be dismissed between the
  // click and the call, and the assertion turns that into a crash instead of a no-op.
  const updateConference = useCaseAction((v: { conferenceId: string }) =>
    employeeRelationsService.updateConference(id, v.conferenceId, {
      scheduledFor: editWhen || null,
      venue: editVenue.trim() || null,
      purpose: editPurpose.trim() || null,
    }), 'Not changed', () => setEditFor(null));

  const holdConference = useCaseAction((v: { conference: GrievanceConference }) =>
    employeeRelationsService.holdConference(id, v.conference.id, {
      outcome: holdOutcome.trim(),
      notes: holdNotes.trim() || null,
      attendance: v.conference.attendees.map((a) => ({ attendeeId: a.id, didAttend: holdAttendance[a.id] ?? false })),
    }), 'Not recorded', () => { setHoldFor(null); setHoldOutcome(''); setHoldNotes(''); setHoldAttendance({}); });

  const cancelConference = useCaseAction((v: { conferenceId: string }) =>
    employeeRelationsService.cancelConference(id, v.conferenceId, {
      reason: cancelReason.trim(),
    }), 'Not cancelled', () => { setCancelFor(null); setCancelReason(''); });

  const addAttendee = useCaseAction((v: { conferenceId: string }) =>
    employeeRelationsService.addConferenceAttendee(id, v.conferenceId, {
      ...(attendeeEmployeeId ? { employeeId: attendeeEmployeeId } : { externalName: attendeeExternal.trim() }),
      ...(!attendeeEmployeeId && attendeeOrg.trim() ? { externalOrganisation: attendeeOrg.trim() } : {}),
      ...(attendeeCapacity.trim() ? { capacity: attendeeCapacity.trim() } : {}),
    }), 'Not added', () => {
      setAttendeeFor(null); setAttendeeEmployeeId(null); setAttendeeExternal(''); setAttendeeOrg(''); setAttendeeCapacity('');
    });

  const removeAttendee = useCaseAction((v: { conferenceId: string; attendeeId: string }) =>
    employeeRelationsService.removeConferenceAttendee(id, v.conferenceId, v.attendeeId), 'Not removed');

  const uploadDocument = useMutation({
    mutationFn: (v: { file: File }) => employeeRelationsService.uploadDocument(id, v.file, {
      scope: uploadScope,
      description: uploadDescription.trim() || undefined,
      stepId: uploadScope === 'Step' && uploadStepId ? uploadStepId : undefined,
      conferenceId: uploadScope === 'Conference' && uploadConferenceId ? uploadConferenceId : undefined,
      agreementSignedDate: uploadScope === 'Agreement' && uploadSignedDate ? uploadSignedDate : undefined,
    }),
    // ⚠ The upload answers with the DOCUMENT, not the case file — the one write on this controller
    // that does. So this refetches rather than seeding the cache, and must not be folded into
    // useCaseAction, whose whole premise is that the response IS the case.
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CASE_KEY(id) });
      setUploadOpen(false); setUploadFile(null); setUploadDescription('');
      setUploadStepId(''); setUploadConferenceId(''); setUploadSignedDate('');
    },
    onError: (e: any) => toast({ title: 'Not uploaded', description: e?.message, variant: 'destructive' }),
  });

  const deleteDocument = useCaseAction((documentId: string) =>
    employeeRelationsService.deleteDocument(id, documentId), 'Not removed');

  const resolve = useCaseAction(() => employeeRelationsService.resolve(id, {
    outcome, decision: decision.trim(), remedyOrUndertakings: remedy.trim() || null,
  }), 'Not resolved', () => { setResolveOpen(false); setDecision(''); setRemedy(''); });

  const closeCase = useCaseAction(() => employeeRelationsService.close(id, { reason: closureReason.trim() }),
    'Not closed', () => { setCloseOpen(false); setClosureReason(''); });

  const linkRecord = useCaseAction(() => employeeRelationsService.linkSource(id, {
    source: linkSource, recordId: linkRecordId,
  }), 'Not linked', () => { setLinkOpen(false); setLinkRecordId(''); });

  const unlinkRecord = useCaseAction((source: EmployeeRelationsLinkSource) =>
    employeeRelationsService.unlinkSource(id, source), 'Not unlinked');

  // ── Lookups, fetched only when a dialog needs them ─────────────────────────
  const { data: unions } = useQuery({
    queryKey: ['hr', 'unions', 'active'],
    queryFn: () => unionService.getActive(),
    enabled: partyOpen || confOpen,
  });

  /**
   * The people this case is about — the primary party plus every ACTIVE party who is an employee.
   *
   * ⚠ This is the server's link rule, mirrored so the picker can only offer records it will accept.
   * The alternative — a free-text record id — would let the desk choose something the server then
   * refuses, which is how a correct rule comes to look like a broken screen.
   */
  const linkSubjects = c
    ? [
        { id: c.employeeId, name: `${c.employeeName} (primary party)` },
        // flatMap rather than filter+map: filtering does not narrow `employeeId` for the
        // compiler, so the map would need an assertion to say what the filter already guaranteed.
        ...c.parties.flatMap((p) =>
          p.isActive && p.employeeId
            ? [{ id: p.employeeId, name: `${p.displayName} (${p.roleName})` }]
            : []),
      ]
    : [];

  const linkFor = linkSubject || c?.employeeId || '';

  const { data: linkCandidates, isFetching: loadingCandidates } = useQuery({
    queryKey: ['hr', 'employee-relations', 'link-candidates', linkSource, linkFor],
    enabled: linkOpen && !!linkFor,
    queryFn: async () => {
      if (linkSource === 'DisciplinaryCase') {
        const rows = await disciplineService.getByEmployee(linkFor);
        return rows.map((r) => ({ id: r.id, label: `${r.caseNumber} · ${r.statusName} · ${fmtDate(r.incidentDate)}` }));
      }
      if (linkSource === 'PerformanceImprovementPlan') {
        const rows = await pipService.getByEmployee(linkFor);
        return rows.map((r) => ({ id: r.id, label: `${r.pipNumber} · ${r.status} · ${fmtDate(r.startDate)}` }));
      }
      // ⚠ `getForEmployee`, not `getByInvolvedEmployee`. The former is
      // `ReportedById || InvolvedPersons.Any(...)`, which is exactly the set the link rule accepts;
      // the latter omits the reporter, and would hide the clearest victimisation case in the module.
      const rows = await safetyIncidentService.getForEmployee(linkFor);
      return rows.map((r) => ({ id: r.id, label: `${r.incidentNumber} · ${r.statusName} · ${fmtDate(r.incidentDate)}` }));
    },
  });

  if (isLoading) {
    return <div className="flex items-center justify-center p-16"><Loader2 className="h-6 w-6 animate-spin text-muted-foreground" /></div>;
  }
  if (isError || !c) {
    return (
      <div className="p-6">
        <EmptyState title="Case not available" description="It does not exist, or it is not yours to read." />
      </div>
    );
  }

  const isSettled = SETTLED_GRIEVANCE_STATUSES.includes(c.status);
  const currentStep = c.steps[c.steps.length - 1];
  const awaits = c.awaitingResponse;
  // The server refuses closure until the ladder is exhausted — "Only a case that has exhausted the
  // escalation route can be closed unresolved." Offering the button earlier would be an affordance
  // for an action that always fails.
  const canClose = !isSettled && c.currentLevel === 'Board';
  const inv = c.investigation;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${c.grievanceNumber} · ${c.subject}`}
        description={`${typeLabel(c.caseType)} · ${c.employeeName}${c.employeeNumber ? ` (${c.employeeNumber})` : ''} · filed ${fmtDate(c.filedDate)}`}
        backHref="/hr/employee-relations"
        actions={
          <div className="flex flex-wrap gap-2">
            {!isSettled && (
              <>
                <Button variant="outline" onClick={() => setAssignOpen(true)}>
                  <UserPlus className="mr-2 h-4 w-4" /> Assign this rung
                </Button>
                <Button variant="outline" onClick={() => setRespondOpen(true)}>
                  <Send className="mr-2 h-4 w-4" /> Respond
                </Button>
                <Button onClick={() => setResolveOpen(true)}>
                  <Gavel className="mr-2 h-4 w-4" /> Record the decision
                </Button>
              </>
            )}
            {canClose && (
              <Button variant="outline" onClick={() => setCloseOpen(true)}>
                <Lock className="mr-2 h-4 w-4" /> Close unresolved
              </Button>
            )}
          </div>
        }
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card><CardContent className="pt-6"><Field label="Status" value={<StatusBadge status={c.statusName} />} /></CardContent></Card>
        <Card><CardContent className="pt-6"><Field label="Currently with" value={levelLabel(c.currentLevel)} /></CardContent></Card>
        <Card>
          <CardContent className="pt-6">
            <Field
              label="Answer owed"
              value={awaits
                ? <span className="text-amber-600">Yes — since {fmtDate(currentStep?.reachedDate)}</span>
                : 'No'}
            />
          </CardContent>
        </Card>
        <Card><CardContent className="pt-6"><Field label="Other parties" value={c.activePartyCount} /></CardContent></Card>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">The statement, in the employee&apos;s own words</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          {/* FR-HR-181 requires this retained: nothing amends it after filing. */}
          <p className="whitespace-pre-wrap text-sm">{c.statement}</p>
          {c.withdrawnDate && (
            <div className="rounded-md border border-dashed p-3 text-sm">
              <span className="font-medium">Withdrawn {fmtDate(c.withdrawnDate)}</span>
              {c.withdrawalReason ? ` — ${c.withdrawalReason}` : ''}
            </div>
          )}
          {c.closedDate && (
            <div className="rounded-md border border-dashed p-3 text-sm">
              <span className="font-medium">Closed unresolved {fmtDate(c.closedDate)}</span>
              {c.closureReason ? ` — ${c.closureReason}` : ''}
              {c.closedByName ? ` (${c.closedByName})` : ''}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle className="text-base">HR&apos;s interpretation</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">
              HR&apos;s reading of what the policy, the Conditions of Service or the CBA say about this
              case — distinct from HR&apos;s answer at its own rung, and read by every rung above it.
            </p>
          </div>
          {!isSettled && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => { setInterpretation(c.hrInterpretation ?? ''); setInterpretOpen(true); }}
            >
              {c.hrInterpretation ? 'Amend' : 'Record'}
            </Button>
          )}
        </CardHeader>
        <CardContent>
          {c.hrInterpretation ? (
            <>
              <p className="whitespace-pre-wrap text-sm">{c.hrInterpretation}</p>
              <p className="mt-2 text-xs text-muted-foreground">
                {c.hrInterpretationByName} · {fmtDateTime(c.hrInterpretationDate)}
                {isSettled && ' · frozen, the case is closed'}
              </p>
            </>
          ) : (
            <p className="text-sm text-muted-foreground">Not recorded.</p>
          )}
        </CardContent>
      </Card>

      <Tabs defaultValue="ladder">
        <TabsList className="flex-wrap">
          <TabsTrigger value="ladder">Ladder ({c.steps.length})</TabsTrigger>
          <TabsTrigger value="parties">Parties ({c.parties.length})</TabsTrigger>
          <TabsTrigger value="investigation">Investigation</TabsTrigger>
          <TabsTrigger value="conferences">Conferences ({c.conferences.length})</TabsTrigger>
          <TabsTrigger value="documents">Documents ({c.documents.length})</TabsTrigger>
          <TabsTrigger value="resolution">Resolution</TabsTrigger>
          <TabsTrigger value="links">Links ({c.links.length})</TabsTrigger>
        </TabsList>

        {/* ── Ladder ─────────────────────────────────────────────────────── */}
        <TabsContent value="ladder">
          <Card>
            <CardContent className="p-0">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>#</TableHead>
                    <TableHead>Rung</TableHead>
                    <TableHead>Reached</TableHead>
                    <TableHead>Named to answer</TableHead>
                    <TableHead>Response</TableHead>
                    <TableHead>Answered</TableHead>
                    <TableHead>Outcome</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {c.steps.map((s) => (
                    <TableRow key={s.id}>
                      <TableCell>{s.sequence}</TableCell>
                      <TableCell className="font-medium">{levelLabel(s.level)}</TableCell>
                      <TableCell>{fmtDate(s.reachedDate)}</TableCell>
                      <TableCell>
                        {/* Nobody named is normal, not broken: the responder matrix covers few units. */}
                        {s.assignedToName ?? <span className="text-muted-foreground">nobody named</span>}
                      </TableCell>
                      <TableCell className="max-w-md whitespace-pre-wrap text-sm">{s.response ?? '—'}</TableCell>
                      <TableCell>
                        {s.respondedDate ? (
                          <>
                            <div>{fmtDate(s.respondedDate)}</div>
                            <div className="text-xs text-muted-foreground">{s.respondedByName}</div>
                          </>
                        ) : '—'}
                      </TableCell>
                      <TableCell><StatusBadge status={s.outcomeName} /></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Parties ────────────────────────────────────────────────────── */}
        <TabsContent value="parties" className="space-y-4">
          <div className="flex justify-end">
            {!isSettled && (
              <Button variant="outline" onClick={() => setPartyOpen(true)}>
                <UserPlus className="mr-2 h-4 w-4" /> Add somebody
              </Button>
            )}
          </div>
          <Card>
            <CardContent className="p-0">
              {c.parties.length === 0 ? (
                <EmptyState title="Nobody else on this case" description="The primary party is the only person named." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Role</TableHead>
                      <TableHead>Who</TableHead>
                      <TableHead>Acting for</TableHead>
                      <TableHead>Added</TableHead>
                      <TableHead>Notes</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {c.parties.map((p) => (
                      <TableRow key={p.id} className={p.isActive ? '' : 'opacity-60'}>
                        <TableCell>{p.roleName}</TableCell>
                        <TableCell>
                          <div className="font-medium">{p.displayName}</div>
                          {p.externalOrganisation && (
                            <div className="text-xs text-muted-foreground">{p.externalOrganisation}</div>
                          )}
                          {/* Standing down is not a delete — the row stays, with its reason. */}
                          {!p.isActive && (
                            <div className="text-xs text-muted-foreground">
                              stood down {fmtDate(p.removedDate)}{p.removalReason ? ` — ${p.removalReason}` : ''}
                            </div>
                          )}
                        </TableCell>
                        <TableCell>{p.representsEmployeeName ?? p.unionName ?? '—'}</TableCell>
                        <TableCell>
                          <div>{fmtDate(p.addedDate)}</div>
                          <div className="text-xs text-muted-foreground">{p.addedByName}</div>
                        </TableCell>
                        <TableCell className="max-w-xs truncate">{p.notes ?? '—'}</TableCell>
                        <TableCell className="text-right">
                          {p.isActive && !isSettled && (
                            <Button variant="ghost" size="sm" onClick={() => setStandDownId(p.id)}>
                              Stand down
                            </Button>
                          )}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Investigation ──────────────────────────────────────────────── */}
        <TabsContent value="investigation">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="text-base">Investigation</CardTitle>
              {!inv && !isSettled && (
                <Button variant="outline" size="sm" onClick={() => setOpenInvOpen(true)}>Open one</Button>
              )}
            </CardHeader>
            <CardContent className="space-y-4">
              {!inv ? (
                <p className="text-sm text-muted-foreground">
                  No investigation has been opened on this case.
                </p>
              ) : (
                <>
                  <div className="grid gap-4 md:grid-cols-4">
                    <Field label="Investigator" value={inv.investigatorDisplayName} />
                    <Field label="Started" value={fmtDate(inv.startedDate)} />
                    <Field
                      label="Target"
                      value={inv.targetDate
                        ? <span className={inv.isOverdue ? 'text-destructive' : undefined}>
                            {fmtDate(inv.targetDate)}{inv.isOverdue ? ' · overdue' : ''}
                          </span>
                        : '—'}
                    />
                    <Field
                      label="Complete"
                      value={inv.isComplete ? `Yes · ${fmtDate(inv.completedDate)}` : 'No'}
                    />
                  </div>

                  {inv.isComplete ? (
                    <div className="space-y-4">
                      <Field label="Findings" value={<span className="whitespace-pre-wrap">{inv.findings}</span>} />
                      <Field label="Evidence collected" value={<span className="whitespace-pre-wrap">{inv.evidenceCollected ?? '—'}</span>} />
                      <Field label="Recommendation" value={<span className="whitespace-pre-wrap">{inv.recommendation ?? '—'}</span>} />
                      <p className="text-xs text-muted-foreground">
                        A completed investigation cannot be amended — the rungs above it rely on it.
                      </p>
                    </div>
                  ) : (
                    <div className="space-y-3">
                      <div className="space-y-1">
                        <Label htmlFor="inv-findings">Findings</Label>
                        <Textarea
                          id="inv-findings"
                          rows={5}
                          value={invFindings || (inv.findings ?? '')}
                          onChange={(e) => setInvFindings(e.target.value)}
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="inv-evidence">Evidence collected</Label>
                        <Textarea
                          id="inv-evidence"
                          rows={3}
                          value={invEvidence || (inv.evidenceCollected ?? '')}
                          onChange={(e) => setInvEvidence(e.target.value)}
                        />
                      </div>
                      <div className="space-y-1">
                        <Label htmlFor="inv-recommendation">Recommendation</Label>
                        <Textarea
                          id="inv-recommendation"
                          rows={3}
                          value={invRecommendation || (inv.recommendation ?? '')}
                          onChange={(e) => setInvRecommendation(e.target.value)}
                        />
                      </div>
                      <div className="flex gap-2">
                        <Button variant="outline" onClick={() => updateInvestigation.mutate(undefined as never)} disabled={updateInvestigation.isPending}>
                          Save progress
                        </Button>
                        <Button onClick={() => setCompleteInvOpen(true)}>Complete</Button>
                      </div>
                      <p className="text-xs text-muted-foreground">
                        Completing needs findings of at least 20 characters. An investigation reported
                        complete with nothing in it is worse than one still open.
                      </p>
                    </div>
                  )}
                </>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Conferences ────────────────────────────────────────────────── */}
        <TabsContent value="conferences" className="space-y-4">
          <div className="flex justify-end">
            {!isSettled && (
              <Button variant="outline" onClick={() => setConfOpen(true)}>
                <CalendarPlus className="mr-2 h-4 w-4" /> Schedule
              </Button>
            )}
          </div>
          {c.conferences.length === 0 ? (
            <Card><CardContent className="p-0"><EmptyState title="No meetings" description="No conference, mediation or union consultation has been scheduled." /></CardContent></Card>
          ) : (
            c.conferences.map((f) => (
              <Card key={f.id}>
                <CardHeader className="flex flex-row items-start justify-between">
                  <div>
                    <CardTitle className="text-base">
                      {f.conferenceTypeName} · {fmtDateTime(f.scheduledFor)}
                    </CardTitle>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Chaired by {f.chairDisplayName}{f.venue ? ` · ${f.venue}` : ''}
                      {f.unionName ? ` · ${f.unionName}` : ''}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusBadge status={f.statusName} />
                    {f.status === 'Scheduled' && !isSettled && (
                      <>
                        <Button variant="ghost" size="sm" onClick={() => {
                          setEditFor(f); setEditWhen(f.scheduledFor.slice(0, 16));
                          setEditVenue(f.venue ?? ''); setEditPurpose(f.purpose ?? '');
                        }}>Change</Button>
                        <Button variant="ghost" size="sm" onClick={() => {
                          setHoldFor(f);
                          setHoldAttendance(Object.fromEntries(f.attendees.map((a) => [a.id, true])));
                        }}>Record it</Button>
                        <Button variant="ghost" size="sm" onClick={() => setCancelFor(f)}>Cancel</Button>
                      </>
                    )}
                  </div>
                </CardHeader>
                <CardContent className="space-y-3">
                  {f.purpose && <Field label="Purpose" value={f.purpose} />}
                  {f.status === 'Held' && (
                    <>
                      <Field label="Outcome" value={<span className="whitespace-pre-wrap">{f.outcome}</span>} />
                      <div className="space-y-1">
                        <div className="text-xs uppercase tracking-wide text-muted-foreground">Notes</div>
                        <RedactableNotes notes={f.notes} redacted={f.notesRedacted} />
                      </div>
                    </>
                  )}
                  {f.status === 'Cancelled' && (
                    <Field label="Cancelled" value={`${fmtDate(f.cancelledDate)} — ${f.cancellationReason ?? ''}`} />
                  )}

                  <div className="space-y-2">
                    <div className="flex items-center justify-between">
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">
                        Attendees ({f.attendees.length})
                      </div>
                      {f.status === 'Scheduled' && !isSettled && (
                        <Button variant="ghost" size="sm" onClick={() => setAttendeeFor(f)}>Add</Button>
                      )}
                    </div>
                    {f.attendees.length === 0 ? (
                      <p className="text-sm text-muted-foreground">Nobody has been asked yet.</p>
                    ) : (
                      <ul className="space-y-1 text-sm">
                        {f.attendees.map((a) => (
                          <li key={a.id} className="flex items-center justify-between">
                            <span>
                              {a.displayName}
                              {a.capacity ? <span className="text-muted-foreground"> · {a.capacity}</span> : null}
                              {f.status === 'Held' && (
                                <Badge variant={a.didAttend ? 'default' : 'outline'} className="ml-2">
                                  {a.didAttend ? 'attended' : 'apologies'}
                                </Badge>
                              )}
                            </span>
                            {f.status === 'Scheduled' && !isSettled && (
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => removeAttendee.mutate({ conferenceId: f.id, attendeeId: a.id })}
                              >
                                <Trash2 className="h-3 w-3" />
                              </Button>
                            )}
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                </CardContent>
              </Card>
            ))
          )}
        </TabsContent>

        {/* ── Documents ──────────────────────────────────────────────────── */}
        <TabsContent value="documents" className="space-y-4">
          <div className="flex justify-end">
            <Button variant="outline" onClick={() => setUploadOpen(true)}>
              <Paperclip className="mr-2 h-4 w-4" /> Attach a document
            </Button>
          </div>
          <Card>
            <CardContent className="p-0">
              {c.documents.length === 0 ? (
                <EmptyState title="Nothing attached" description="No document has been added to this case." />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Where it belongs</TableHead>
                      <TableHead>File</TableHead>
                      <TableHead>Description</TableHead>
                      <TableHead>Added</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {c.documents.map((d) => (
                      <TableRow key={d.id}>
                        <TableCell>{d.scopeName}</TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <FileText className="h-4 w-4 text-muted-foreground" />
                            <div>
                              <div className="font-medium">{d.fileName}</div>
                              <div className="text-xs text-muted-foreground">{fmtSize(d.fileSize)}</div>
                            </div>
                          </div>
                        </TableCell>
                        <TableCell className="max-w-xs truncate">{d.description ?? '—'}</TableCell>
                        <TableCell>
                          <div>{fmtDate(d.uploadDate)}</div>
                          <div className="text-xs text-muted-foreground">{d.uploadedByName}</div>
                        </TableCell>
                        <TableCell className="text-right">
                          {/*
                            ⚠ Fetched as a blob with the bearer token, never linked by `filePath`:
                            the file lives outside the web root and an href to it is a guaranteed 404.
                          */}
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={async () => {
                              try {
                                const blob = await employeeRelationsService.downloadDocument(d.id);
                                const url = URL.createObjectURL(blob);
                                const a = document.createElement('a');
                                a.href = url;
                                a.download = d.fileName;
                                a.click();
                                URL.revokeObjectURL(url);
                              } catch (e: any) {
                                toast({ title: 'Not downloaded', description: e?.message, variant: 'destructive' });
                              }
                            }}
                          >
                            <Download className="h-4 w-4" />
                          </Button>
                          <Button variant="ghost" size="sm" onClick={() => deleteDocument.mutate(d.id)}>
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Resolution ─────────────────────────────────────────────────── */}
        <TabsContent value="resolution">
          <Card>
            <CardHeader><CardTitle className="text-base">The decision</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              {!c.resolution ? (
                <p className="text-sm text-muted-foreground">
                  {isSettled
                    ? 'This case ended without a recorded decision.'
                    : 'No decision has been recorded. Use “Record the decision” above.'}
                </p>
              ) : (
                <>
                  {/*
                    A resolved case whose outcome is NotRecorded came through the older
                    respond(resolvesGrievance) path, which had no outcome to record. Surfaced rather
                    than dressed up as a real answer — it is a genuine gap in the file.
                  */}
                  {c.resolution.outcomeIsMissing && (
                    <div className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm dark:bg-amber-950/30">
                      <AlertTriangle className="mt-0.5 h-4 w-4 text-amber-600" />
                      <span>
                        This case was settled through a step response, so no outcome was ever
                        recorded. Recording the decision now fills it in — the only change a
                        recorded decision accepts.
                      </span>
                    </div>
                  )}
                  <div className="grid gap-4 md:grid-cols-3">
                    <Field label="Outcome" value={c.resolution.outcomeName} />
                    <Field label="Decided at" value={levelLabel(c.resolution.decidedAtLevel)} />
                    <Field label="Decided" value={`${fmtDate(c.resolution.decidedDate)} · ${c.resolution.decidedByName ?? '—'}`} />
                  </div>
                  <Field label="Decision" value={<span className="whitespace-pre-wrap">{c.resolution.decision}</span>} />
                  <Field label="Remedy or undertakings" value={<span className="whitespace-pre-wrap">{c.resolution.remedyOrUndertakings ?? '—'}</span>} />

                  <div className="rounded-md border p-3">
                    <div className="text-xs uppercase tracking-wide text-muted-foreground">
                      The signed agreement — FR-HR-181&apos;s final artefact
                    </div>
                    <div className="mt-2 space-y-1 text-sm">
                      <div>
                        Signed agreement on file:{' '}
                        {c.resolution.agreementSignedDate
                          ? <span className="inline-flex items-center gap-1"><CheckCircle2 className="h-3 w-3 text-green-600" /> {fmtDate(c.resolution.agreementSignedDate)}</span>
                          : <span className="text-muted-foreground">not yet — attach it on the Documents tab with scope “Signed agreement”</span>}
                      </div>
                      <div>
                        Accepted by the employee:{' '}
                        {c.resolution.agreementAccepted
                          ? `${fmtDate(c.resolution.agreementAcceptedDate)} · ${c.resolution.agreementAcceptedByName ?? ''}`
                          : <span className="text-muted-foreground">not yet</span>}
                      </div>
                      {c.resolution.agreementAcceptanceComment && (
                        <div className="text-muted-foreground">“{c.resolution.agreementAcceptanceComment}”</div>
                      )}
                      {/* Acceptance is the employee's alone — no button here can stand in for it. */}
                      <p className="pt-1 text-xs text-muted-foreground">
                        Only the employee can accept, from their own portal. HR cannot accept on
                        their behalf, and there is deliberately no control here to do so.
                      </p>
                    </div>
                  </div>
                </>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Links ──────────────────────────────────────────────────────── */}
        <TabsContent value="links" className="space-y-4">
          <div className="flex justify-end">
            <Button variant="outline" onClick={() => setLinkOpen(true)}>
              <Link2 className="mr-2 h-4 w-4" /> Cross-reference a record
            </Button>
          </div>
          <Card>
            <CardContent className="p-0">
              {c.links.length === 0 ? (
                <EmptyState
                  title="Nothing cross-referenced"
                  description="This case is not linked to a safety incident, an improvement plan or a disciplinary case."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Kind</TableHead>
                      <TableHead>Reference</TableHead>
                      <TableHead>About</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {c.links.map((l) => (
                      <TableRow key={l.source} className={l.available ? '' : 'opacity-60'}>
                        <TableCell>{l.sourceLabel}</TableCell>
                        <TableCell className="font-medium">
                          {l.number}
                          {/* A retired source is shown dead rather than hidden: that the case WAS
                              linked is part of how it was handled. */}
                          {!l.available && (
                            <div className="text-xs text-muted-foreground">
                              the owning module has retired this record
                            </div>
                          )}
                        </TableCell>
                        <TableCell>{l.subjectEmployeeName ?? <span className="text-muted-foreground">—</span>}</TableCell>
                        <TableCell>{fmtDate(l.date)}</TableCell>
                        <TableCell>{l.status || '—'}</TableCell>
                        <TableCell className="text-right">
                          <Button variant="ghost" size="sm" onClick={() => unlinkRecord.mutate(l.source)}>
                            <Ban className="mr-1 h-3 w-3" /> Remove
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
          <p className="text-xs text-muted-foreground">
            A cross-reference is a pointer, not a window: it shows the reference and nothing of the
            record&apos;s substance. It is visible to HR only, and it can only point at a record
            about somebody already named on this case.
          </p>
        </TabsContent>
      </Tabs>

      {/* ══ Dialogs ═══════════════════════════════════════════════════════ */}

      <Dialog open={assignOpen} onOpenChange={setAssignOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Name who should answer</DialogTitle>
            <DialogDescription>
              The person answering at the {levelLabel(c.currentLevel)} rung. Naming them is what
              lets them see and answer this case.
            </DialogDescription>
          </DialogHeader>
          <EmployeePicker value={assignee} onChange={(v) => setAssignee(v)} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setAssignOpen(false)}>Cancel</Button>
            <Button
              disabled={!assignee || assign.isPending}
              onClick={() => { if (assignee) assign.mutate(assignee); }}
            >
              {assign.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Assign
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={respondOpen} onOpenChange={setRespondOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Answer at the {levelLabel(c.currentLevel)} rung</DialogTitle>
            <DialogDescription>
              Retained as part of the record. The employee decides whether it settles the matter —
              if it does not, they escalate.
            </DialogDescription>
          </DialogHeader>
          <Textarea rows={6} value={responseText} onChange={(e) => setResponseText(e.target.value)} />
          <div className="flex items-start gap-2">
            <Checkbox id="resolves" checked={resolvesIt} onCheckedChange={(v) => setResolvesIt(v === true)} />
            <Label htmlFor="resolves" className="text-sm font-normal">
              This answer settles the case.
              <span className="block text-xs text-muted-foreground">
                Prefer “Record the decision”, which captures a real outcome. Settling here leaves the
                outcome unrecorded.
              </span>
            </Label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRespondOpen(false)}>Cancel</Button>
            <Button
              disabled={responseText.trim().length < 10 || respond.isPending}
              onClick={() => respond.mutate(undefined as never)}
            >
              {respond.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Record the answer
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={interpretOpen} onOpenChange={setInterpretOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>HR&apos;s interpretation</DialogTitle>
            <DialogDescription>
              What the policy, the Conditions of Service or the CBA say about this case. Amendable
              while the case is open; frozen the moment it ends.
            </DialogDescription>
          </DialogHeader>
          <Textarea rows={8} value={interpretation} onChange={(e) => setInterpretation(e.target.value)} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setInterpretOpen(false)}>Cancel</Button>
            <Button
              disabled={!interpretation.trim() || recordInterpretation.isPending}
              onClick={() => recordInterpretation.mutate(undefined as never)}
            >Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={partyOpen} onOpenChange={setPartyOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Add somebody to the case</DialogTitle>
            <DialogDescription>
              An employee or an external person — representation is very often by a union official
              or a lawyer who is not on the payroll.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>Role</Label>
              <Select value={partyRole} onValueChange={(v) => setPartyRole(v as GrievancePartyRole)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {GRIEVANCE_PARTY_ROLE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Employee</Label>
              <EmployeePicker
                value={partyEmployeeId}
                onChange={(v) => { setPartyEmployeeId(v); if (v) setPartyExternalName(''); }}
                placeholder="Search staff, or leave blank for an external person"
              />
            </div>
            {!partyEmployeeId && (
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label htmlFor="ext-name">External name</Label>
                  <Input id="ext-name" value={partyExternalName} onChange={(e) => setPartyExternalName(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="ext-org">Organisation</Label>
                  <Input id="ext-org" value={partyExternalOrg} onChange={(e) => setPartyExternalOrg(e.target.value)} />
                </div>
              </div>
            )}
            {(partyRole === 'Representative' || partyRole === 'UnionRepresentative') && (
              <div className="space-y-1">
                <Label>Acting for</Label>
                <Select value={partyRepresents} onValueChange={setPartyRepresents}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Nobody in particular</SelectItem>
                    <SelectItem value={c.employeeId}>{c.employeeName} (primary party)</SelectItem>
                    {c.parties.flatMap((p) => (p.isActive && p.employeeId
                      ? [<SelectItem key={p.id} value={p.employeeId}>{p.displayName}</SelectItem>]
                      : []))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  A representative can only act for somebody already on this case.
                </p>
              </div>
            )}
            {partyRole === 'UnionRepresentative' && (
              <div className="space-y-1">
                <Label>Union</Label>
                <Select value={partyUnion} onValueChange={setPartyUnion}>
                  <SelectTrigger><SelectValue placeholder="Which union?" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Not specified</SelectItem>
                    {(unions ?? []).map((u) => (
                      <SelectItem key={u.id} value={u.id}>{u.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            <div className="space-y-1">
              <Label htmlFor="party-notes">Notes</Label>
              <Textarea id="party-notes" rows={2} value={partyNotes} onChange={(e) => setPartyNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPartyOpen(false)}>Cancel</Button>
            <Button
              disabled={(!partyEmployeeId && !partyExternalName.trim()) || addParty.isPending}
              onClick={() => addParty.mutate(undefined as never)}
            >Add</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={standDownId !== null} onOpenChange={(o) => !o && setStandDownId(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Stand this party down</DialogTitle>
            <DialogDescription>
              They stay on the file with the reason recorded — the case must still read correctly
              afterwards.
            </DialogDescription>
          </DialogHeader>
          <Textarea rows={3} value={standDownReason} onChange={(e) => setStandDownReason(e.target.value)} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setStandDownId(null)}>Cancel</Button>
            <Button
              disabled={!standDownReason.trim() || standDown.isPending}
              onClick={() => { if (standDownId) standDown.mutate(standDownId); }}
            >Stand down</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={openInvOpen} onOpenChange={setOpenInvOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Open an investigation</DialogTitle>
            <DialogDescription>
              The investigator may be external — a case about senior management is exactly the one
              that gets an outside investigator.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>Investigator</Label>
              <EmployeePicker
                value={invEmployeeId}
                onChange={(v) => { setInvEmployeeId(v); if (v) setInvExternalName(''); }}
                placeholder="Search staff, or leave blank for an external investigator"
              />
            </div>
            {!invEmployeeId && (
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label htmlFor="inv-ext">External investigator</Label>
                  <Input id="inv-ext" value={invExternalName} onChange={(e) => setInvExternalName(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="inv-org">Firm</Label>
                  <Input id="inv-org" value={invExternalOrg} onChange={(e) => setInvExternalOrg(e.target.value)} />
                </div>
              </div>
            )}
            <div className="space-y-1">
              <Label htmlFor="inv-target">Target date</Label>
              <Input id="inv-target" type="date" value={invTarget} onChange={(e) => setInvTarget(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpenInvOpen(false)}>Cancel</Button>
            <Button
              disabled={(!invEmployeeId && !invExternalName.trim()) || openInvestigation.isPending}
              onClick={() => openInvestigation.mutate(undefined as never)}
            >Open</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={completeInvOpen} onOpenChange={setCompleteInvOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Complete the investigation</DialogTitle>
            <DialogDescription>
              Findings are required and cannot be changed afterwards — every rung above this one
              will rely on them.
            </DialogDescription>
          </DialogHeader>
          <Textarea
            rows={8}
            value={invFindings || (inv?.findings ?? '')}
            onChange={(e) => setInvFindings(e.target.value)}
            placeholder="At least 20 characters"
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleteInvOpen(false)}>Cancel</Button>
            <Button
              disabled={(invFindings || inv?.findings || '').trim().length < 20 || completeInvestigation.isPending}
              onClick={() => completeInvestigation.mutate(undefined as never)}
            >Complete</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={confOpen} onOpenChange={setConfOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Schedule a meeting</DialogTitle>
            <DialogDescription>
              A case conference, a mediation, or the union consultation FR-HR-181 requires be
              retained.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>Kind</Label>
              <Select value={confType} onValueChange={(v) => setConfType(v as GrievanceConferenceType)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {CONFERENCE_TYPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1">
                <Label htmlFor="conf-when">When</Label>
                <Input id="conf-when" type="datetime-local" value={confWhen} onChange={(e) => setConfWhen(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="conf-venue">Venue</Label>
                <Input id="conf-venue" value={confVenue} onChange={(e) => setConfVenue(e.target.value)} />
              </div>
            </div>
            <div className="space-y-1">
              <Label>Chair</Label>
              <EmployeePicker
                value={confChairId}
                onChange={(v) => { setConfChairId(v); if (v) setConfExternalChair(''); }}
                placeholder="Search staff, or leave blank for an external chair"
              />
            </div>
            {!confChairId && (
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label htmlFor="conf-ext-chair">External chair</Label>
                  <Input id="conf-ext-chair" value={confExternalChair} onChange={(e) => setConfExternalChair(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="conf-ext-org">Organisation</Label>
                  <Input id="conf-ext-org" value={confExternalChairOrg} onChange={(e) => setConfExternalChairOrg(e.target.value)} />
                </div>
              </div>
            )}
            {confType === 'UnionConsultation' && (
              <div className="space-y-1">
                <Label>Union</Label>
                <Select value={confUnion} onValueChange={setConfUnion}>
                  <SelectTrigger><SelectValue placeholder="Which union?" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">Not specified</SelectItem>
                    {(unions ?? []).map((u) => (
                      <SelectItem key={u.id} value={u.id}>{u.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  A union consultation must name the union it was with.
                </p>
              </div>
            )}
            <div className="space-y-1">
              <Label htmlFor="conf-purpose">Purpose</Label>
              <Textarea id="conf-purpose" rows={2} value={confPurpose} onChange={(e) => setConfPurpose(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfOpen(false)}>Cancel</Button>
            <Button
              disabled={!confWhen || (!confChairId && !confExternalChair.trim()) || scheduleConference.isPending}
              onClick={() => scheduleConference.mutate(undefined as never)}
            >Schedule</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={editFor !== null} onOpenChange={(o) => !o && setEditFor(null)}>
        <DialogContent>
          <DialogHeader><DialogTitle>Change the meeting</DialogTitle></DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="edit-when">When</Label>
              <Input id="edit-when" type="datetime-local" value={editWhen} onChange={(e) => setEditWhen(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="edit-venue">Venue</Label>
              <Input id="edit-venue" value={editVenue} onChange={(e) => setEditVenue(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="edit-purpose">Purpose</Label>
              <Textarea id="edit-purpose" rows={2} value={editPurpose} onChange={(e) => setEditPurpose(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditFor(null)}>Cancel</Button>
            <Button
              disabled={!editFor || updateConference.isPending}
              onClick={() => { if (editFor) updateConference.mutate({ conferenceId: editFor.id }); }}
            >Save</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={holdFor !== null} onOpenChange={(o) => !o && setHoldFor(null)}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Record the meeting</DialogTitle>
            <DialogDescription>
              The outcome goes on the case file for everyone who may read it. The notes are visible
              to HR and the chair only.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="hold-outcome">Outcome</Label>
              <Textarea
                id="hold-outcome" rows={4} value={holdOutcome}
                onChange={(e) => setHoldOutcome(e.target.value)}
                placeholder="At least 20 characters"
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="hold-notes">Notes (restricted)</Label>
              <Textarea id="hold-notes" rows={4} value={holdNotes} onChange={(e) => setHoldNotes(e.target.value)} />
            </div>
            {holdFor && holdFor.attendees.length > 0 && (
              <div className="space-y-2">
                <Label>Who came</Label>
                {holdFor.attendees.map((a) => (
                  <div key={a.id} className="flex items-center gap-2">
                    <Checkbox
                      id={`att-${a.id}`}
                      checked={holdAttendance[a.id] ?? false}
                      onCheckedChange={(v) => setHoldAttendance((s) => ({ ...s, [a.id]: v === true }))}
                    />
                    <Label htmlFor={`att-${a.id}`} className="text-sm font-normal">{a.displayName}</Label>
                  </div>
                ))}
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setHoldFor(null)}>Cancel</Button>
            <Button
              disabled={!holdFor || holdOutcome.trim().length < 20 || holdConference.isPending}
              onClick={() => { if (holdFor) holdConference.mutate({ conference: holdFor }); }}
            >Record</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={cancelFor !== null} onOpenChange={(o) => !o && setCancelFor(null)}>
        <DialogContent>
          <DialogHeader><DialogTitle>Cancel the meeting</DialogTitle></DialogHeader>
          <Textarea rows={3} value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} placeholder="Why?" />
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelFor(null)}>Keep it</Button>
            <Button
              disabled={!cancelFor || cancelReason.trim().length < 5 || cancelConference.isPending}
              onClick={() => { if (cancelFor) cancelConference.mutate({ conferenceId: cancelFor.id }); }}
            >Cancel the meeting</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={attendeeFor !== null} onOpenChange={(o) => !o && setAttendeeFor(null)}>
        <DialogContent>
          <DialogHeader><DialogTitle>Ask somebody to the meeting</DialogTitle></DialogHeader>
          <div className="space-y-3">
            <EmployeePicker
              value={attendeeEmployeeId}
              onChange={(v) => { setAttendeeEmployeeId(v); if (v) setAttendeeExternal(''); }}
              placeholder="Search staff, or leave blank for an external attendee"
            />
            {!attendeeEmployeeId && (
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="space-y-1">
                  <Label htmlFor="att-ext">External name</Label>
                  <Input id="att-ext" value={attendeeExternal} onChange={(e) => setAttendeeExternal(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="att-org">Organisation</Label>
                  <Input id="att-org" value={attendeeOrg} onChange={(e) => setAttendeeOrg(e.target.value)} />
                </div>
              </div>
            )}
            <div className="space-y-1">
              <Label htmlFor="att-cap">In what capacity</Label>
              <Input
                id="att-cap" value={attendeeCapacity}
                onChange={(e) => setAttendeeCapacity(e.target.value)}
                placeholder="Complainant, union official, witness…"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAttendeeFor(null)}>Cancel</Button>
            <Button
              disabled={!attendeeFor || (!attendeeEmployeeId && !attendeeExternal.trim()) || addAttendee.isPending}
              onClick={() => { if (attendeeFor) addAttendee.mutate({ conferenceId: attendeeFor.id }); }}
            >Add</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={uploadOpen} onOpenChange={setUploadOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Attach a document</DialogTitle>
            <DialogDescription>
              Scanned and registered in the central document repository before it reaches the case.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="doc-file">File</Label>
              <Input id="doc-file" type="file" onChange={(e) => setUploadFile(e.target.files?.[0] ?? null)} />
            </div>
            <div className="space-y-1">
              <Label>Where it belongs</Label>
              <Select value={uploadScope} onValueChange={(v) => setUploadScope(v as GrievanceDocumentScope)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {DOCUMENT_SCOPE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {uploadScope === 'Step' && (
              <div className="space-y-1">
                <Label>Which rung</Label>
                <Select value={uploadStepId} onValueChange={setUploadStepId}>
                  <SelectTrigger><SelectValue placeholder="Pick a step" /></SelectTrigger>
                  <SelectContent>
                    {c.steps.map((s) => (
                      <SelectItem key={s.id} value={s.id}>{s.sequence}. {levelLabel(s.level)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {uploadScope === 'Conference' && (
              <div className="space-y-1">
                <Label>Which meeting</Label>
                <Select value={uploadConferenceId} onValueChange={setUploadConferenceId}>
                  <SelectTrigger><SelectValue placeholder="Pick a meeting" /></SelectTrigger>
                  <SelectContent>
                    {c.conferences.map((f) => (
                      <SelectItem key={f.id} value={f.id}>
                        {f.conferenceTypeName} · {fmtDate(f.scheduledFor)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {uploadScope === 'Agreement' && (
              <div className="space-y-1">
                <Label htmlFor="doc-signed">Date signed</Label>
                <Input id="doc-signed" type="date" value={uploadSignedDate} onChange={(e) => setUploadSignedDate(e.target.value)} />
                <p className="text-xs text-muted-foreground">
                  Attaching the signed agreement is what FR-HR-181 asks for, and it necessarily comes
                  after the decision — a resolved case still accepts it.
                </p>
              </div>
            )}
            <div className="space-y-1">
              <Label htmlFor="doc-desc">Description</Label>
              <Input id="doc-desc" value={uploadDescription} onChange={(e) => setUploadDescription(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setUploadOpen(false)}>Cancel</Button>
            <Button
              disabled={!uploadFile || uploadDocument.isPending}
              onClick={() => { if (uploadFile) uploadDocument.mutate({ file: uploadFile }); }}
            >
              {uploadDocument.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Attach
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={resolveOpen} onOpenChange={setResolveOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Record the decision</DialogTitle>
            <DialogDescription>
              FR-HR-181&apos;s resolution decision. It is frozen once written — the only later change
              it accepts is filling in an outcome that was never captured.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>Outcome</Label>
              <Select value={outcome} onValueChange={(v) => setOutcome(v as Exclude<GrievanceResolutionOutcome, 'NotRecorded'>)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {RESOLUTION_OUTCOME_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="res-decision">Decision</Label>
              <Textarea
                id="res-decision" rows={6} value={decision}
                onChange={(e) => setDecision(e.target.value)}
                placeholder="At least 20 characters"
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="res-remedy">Remedy or undertakings</Label>
              <Textarea id="res-remedy" rows={3} value={remedy} onChange={(e) => setRemedy(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setResolveOpen(false)}>Cancel</Button>
            <Button
              disabled={decision.trim().length < 20 || resolve.isPending}
              onClick={() => resolve.mutate(undefined as never)}
            >
              <Scale className="mr-2 h-4 w-4" />Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={closeOpen} onOpenChange={setCloseOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Close without resolving</DialogTitle>
            <DialogDescription>
              For a case the Board answered without resolving. It ends the ladder and stops the
              reminders — until slice 2 this state had no writer at all, and such cases sat under
              review for ever.
            </DialogDescription>
          </DialogHeader>
          <Textarea rows={4} value={closureReason} onChange={(e) => setClosureReason(e.target.value)} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloseOpen(false)}>Cancel</Button>
            <Button
              disabled={!closureReason.trim() || closeCase.isPending}
              onClick={() => closeCase.mutate(undefined as never)}
            >Close the case</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={linkOpen} onOpenChange={setLinkOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Cross-reference a record</DialogTitle>
            <DialogDescription>
              Only records about somebody already named on this case can be linked. If the person
              you want is not listed, add them as a party first.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>About whom</Label>
              <Select value={linkSubject || c.employeeId} onValueChange={(v) => { setLinkSubject(v); setLinkRecordId(''); }}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {linkSubjects.map((s) => (
                    <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Kind of record</Label>
              <Select
                value={linkSource}
                onValueChange={(v) => { setLinkSource(v as EmployeeRelationsLinkSource); setLinkRecordId(''); }}
              >
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {LINK_SOURCE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Record</Label>
              {loadingCandidates ? (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" /> Looking…
                </div>
              ) : (linkCandidates ?? []).length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  There is no record of this kind about that person.
                </p>
              ) : (
                <Select value={linkRecordId} onValueChange={setLinkRecordId}>
                  <SelectTrigger><SelectValue placeholder="Pick a record" /></SelectTrigger>
                  <SelectContent>
                    {(linkCandidates ?? []).map((r) => (
                      <SelectItem key={r.id} value={r.id}>{r.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </div>
            <p className="text-xs text-muted-foreground">
              One link of each kind per case. Changing one is remove-then-add, so that the file never
              silently re-points.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLinkOpen(false)}>Cancel</Button>
            <Button
              disabled={!linkRecordId || linkRecord.isPending}
              onClick={() => linkRecord.mutate(undefined as never)}
            >Link</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
