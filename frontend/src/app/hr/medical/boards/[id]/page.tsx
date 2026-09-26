'use client';

import { useRef, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Ban, CalendarPlus, ClipboardList, Download, FileText, Gavel, Loader2, Paperclip, Trash2, Undo2,
  UserPlus, Users,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import {
  EMPTY_CASE,
  MedicalBoardCaseFields,
  NO_EXAM,
  caseDraftComplete,
  type MedicalBoardCaseDraft,
} from '@/components/hr/medical/MedicalBoardCaseFields';
import { medicalBoardService } from '@/services/hr/medical-board.service';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import {
  MEDICAL_BOARD_CASE_STATUS_LABEL,
  MEDICAL_BOARD_KIND_LABEL,
  MEDICAL_BOARD_MEMBER_ROLE_LABEL,
  MEDICAL_BOARD_OUTCOME_LABEL,
  MEDICAL_BOARD_PURPOSE_LABEL,
  boardStatusLabel,
  type MedicalBoardCase,
  type MedicalBoardMemberRole,
  type MedicalBoardOutcome,
  type MedicalBoardSitting,
} from '@/types/hr/medical-board';

type MemberMode = 'physician' | 'employee' | 'external';
type DialogKind = null | 'case' | 'member' | 'sitting' | 'attendance' | 'decide' | 'withdraw' | 'cancel';

/** Radix Select cannot carry an empty value, so "the board itself" is a sentinel. */
const THE_BOARD = '__board';

const fileSize = (bytes?: number | null) =>
  bytes == null ? '' : bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

const day = (d?: string | null) => (d ? d.slice(0, 10) : undefined);

function Row({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

/** Who was present, deciding members first, so the quorum can be read at a glance. */
function presentLine(s: MedicalBoardSitting) {
  if (s.attendees.length === 0) return 'Not recorded';
  return s.attendees
    .map((a) => `${a.displayName} (${MEDICAL_BOARD_MEMBER_ROLE_LABEL[a.role].toLowerCase()})`)
    .join(', ');
}

const decidingCount = (s: MedicalBoardSitting) => s.attendees.filter((a) => a.decides).length;

/**
 * One medical board: the panel, the cases before it, when it met and who was there, and each
 * case's finding (round 5, lanes K and K-II-a).
 *
 * ⚠ **Two ratchets.** The board: Requested → Convened → Concluded — by itself, when its last open
 * case closes with one decided — with cancel/dissolve available until then. Each case: Listed →
 * Decided (at a sitting) or Withdrawn. Nothing un-decides: a finding that needs revisiting is a case
 * before a new board. So buttons disappear rather than failing.
 *
 * ⚠ **Who decided is who was present.** A case is decided at a recorded sitting, and that sitting's
 * attendance is the panel that decided it — so membership can change between sittings without
 * rewriting any finding, and a sitting's attendance freezes once something is decided at it.
 */
export default function MedicalBoardDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState<DialogKind>(null);

  // add a case
  const [caseDraft, setCaseDraft] = useState<MedicalBoardCaseDraft>(EMPTY_CASE);

  // member form — a registered physician first: the board's clinicians are who it rests on (lane K3)
  const [memberMode, setMemberMode] = useState<MemberMode>('physician');
  const [memberPhysicianId, setMemberPhysicianId] = useState('');
  const [memberEmployeeId, setMemberEmployeeId] = useState('');
  const [memberName, setMemberName] = useState('');
  const [institution, setInstitution] = useState('');
  const [role, setRole] = useState<MedicalBoardMemberRole>('Member');

  // sitting form, and attendance (for a new sitting or an existing one)
  const [sittingDate, setSittingDate] = useState(new Date().toISOString().slice(0, 10));
  const [venue, setVenue] = useState('');
  const [notes, setNotes] = useState('');
  const [present, setPresent] = useState<Set<string>>(new Set());
  const [attendanceSittingId, setAttendanceSittingId] = useState('');

  // deciding / withdrawing a case
  const [caseId, setCaseId] = useState('');
  const [decideSittingId, setDecideSittingId] = useState('');
  const [outcome, setOutcome] = useState<MedicalBoardOutcome>('Fit');
  const [findings, setFindings] = useState('');
  const [recommendation, setRecommendation] = useState('');
  const [restrictions, setRestrictions] = useState('');
  const [reviewDueDate, setReviewDueDate] = useState('');
  const [retire, setRetire] = useState(false);
  const [withdrawReason, setWithdrawReason] = useState('');

  // cancel form — the server refuses a blank reason, so the button waits for one
  const [cancelReason, setCancelReason] = useState('');

  // documents (lanes K4, K-II-a)
  const fileInput = useRef<HTMLInputElement>(null);
  const [documentDescription, setDocumentDescription] = useState('');
  const [documentCaseId, setDocumentCaseId] = useState(THE_BOARD);

  const queryKey = ['hr', 'medical-boards', id];
  const { data: board, isLoading } = useQuery({
    queryKey,
    queryFn: () => medicalBoardService.getById(id),
    enabled: !!id,
  });

  const { data: documents = [] } = useQuery({
    queryKey: ['hr', 'medical-boards', id, 'documents'],
    queryFn: () => medicalBoardService.getDocuments(id),
    enabled: !!id,
  });

  // The register, active physicians only, loaded when the member dialog opens.
  const { data: physicians = [] } = useQuery({
    queryKey: ['hr', 'medical-physicians'],
    queryFn: () => medicalFacilityService.getPhysicians(),
    enabled: dialog === 'member',
  });
  const activePhysicians = physicians.filter((p) => p.isActive);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'medical-boards'] });

  const run = async (what: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await refresh();
      toast({ title: what });
      setDialog(null);
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Action failed.', variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-16">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  if (!board) {
    return <div className="p-6"><EmptyState icon={Gavel} title="Board not found" /></div>;
  }

  const open = board.status === 'Requested' || board.status === 'Convened';
  const canConvene = board.status === 'Requested';
  const canSit = board.status === 'Convened';
  const sittings = board.sittings ?? [];
  const cases = board.cases ?? [];
  const listed = cases.filter((c) => c.status === 'Listed');
  // ⚠ A case is decided AT a sitting, by those present — so deciding waits for a sitting.
  const canDecide = board.status === 'Convened' && sittings.length > 0;

  // ⚠ One action, two words (lane K5): before convening it is only a request; after, a panel.
  const dissolving = board.status === 'Convened';
  const stopLabel = dissolving ? 'Dissolve the board' : 'Cancel the request';

  const caseBeingActedOn = cases.find((c) => c.id === caseId);
  const decideSitting = sittings.find((s) => s.id === decideSittingId);

  const openSitting = () => {
    // Everyone on the panel ticked: the usual sitting is the whole panel, and unticking is visible.
    setPresent(new Set(board.members.map((m) => m.id)));
    setDialog('sitting');
  };

  const openAttendance = (s: MedicalBoardSitting) => {
    setAttendanceSittingId(s.id);
    setPresent(new Set(s.attendees.map((a) => a.memberId).filter((mid) => board.members.some((m) => m.id === mid))));
    setDialog('attendance');
  };

  const openDecide = (c: MedicalBoardCase) => {
    setCaseId(c.id);
    // The latest sitting is the usual one. Chosen, not assumed: the server needs it named.
    setDecideSittingId(sittings.length ? sittings[sittings.length - 1].id : '');
    setOutcome('Fit');
    setFindings('');
    setRecommendation('');
    setRestrictions('');
    setReviewDueDate('');
    setRetire(false);
    setDialog('decide');
  };

  const openWithdraw = (c: MedicalBoardCase) => {
    setCaseId(c.id);
    setWithdrawReason('');
    setDialog('withdraw');
  };

  const togglePresent = (memberId: string) =>
    setPresent((prev) => {
      const next = new Set(prev);
      if (next.has(memberId)) next.delete(memberId);
      else next.add(memberId);
      return next;
    });

  const upload = async (file: File) => {
    await run('Document attached', async () => {
      await medicalBoardService.uploadDocument(
        id, file, documentDescription.trim() || null, documentCaseId === THE_BOARD ? null : documentCaseId,
      );
      setDocumentDescription('');
    });
    // Cleared either way, or choosing the same file again after a refusal would do nothing.
    if (fileInput.current) fileInput.current.value = '';
  };

  const attendanceTicks = (
    <div className="space-y-2">
      <Label>Present</Label>
      {board.members.length === 0 ? (
        <p className="text-sm text-muted-foreground">Nobody is on the panel yet.</p>
      ) : (
        <div className="space-y-1.5">
          {board.members.map((m) => (
            <label key={m.id} className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={present.has(m.id)} onChange={() => togglePresent(m.id)} />
              <span>{m.displayName}</span>
              <span className="text-xs text-muted-foreground">
                {MEDICAL_BOARD_MEMBER_ROLE_LABEL[m.role]}
                {m.decides ? ' · decides' : ' · attends without deciding'}
              </span>
            </label>
          ))}
        </div>
      )}
      <p className="text-xs text-muted-foreground">
        ⚠ Who was present is who decides whatever is decided at this sitting. A chair or member
        counts towards the quorum; a secretary or observer does not.
      </p>
    </div>
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={board.boardNumber}
        description={`${MEDICAL_BOARD_KIND_LABEL[board.kind] ?? board.kind} · ${cases.length} case${cases.length === 1 ? '' : 's'}`}
        backHref="/hr/medical/boards"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={boardStatusLabel(board)} />
            {open && (
              <Button variant="outline" onClick={() => { setCaseDraft(EMPTY_CASE); setDialog('case'); }}>
                <ClipboardList className="mr-2 h-4 w-4" /> Add a case
              </Button>
            )}
            {open && (
              <Button variant="outline" onClick={() => setDialog('member')}>
                <UserPlus className="mr-2 h-4 w-4" /> Appoint a member
              </Button>
            )}
            {canConvene && (
              <Button
                variant="outline"
                disabled={busy}
                onClick={() => run('Board convened', () => medicalBoardService.convene(id))}
              >
                <Users className="mr-2 h-4 w-4" /> Convene
              </Button>
            )}
            {canSit && (
              <Button variant="outline" onClick={openSitting}>
                <CalendarPlus className="mr-2 h-4 w-4" /> Record a sitting
              </Button>
            )}
            {/*
              ⚠ Available until the board reports and not after — the same ratchet as everything
              else here. It sits last and destructive because it ends the board; it is not an undo,
              and there is no un-cancel either. Its word follows the status (lane K5).
            */}
            {open && (
              <Button variant="destructive" onClick={() => setDialog('cancel')}>
                <Ban className="mr-2 h-4 w-4" /> {stopLabel}
              </Button>
            )}
          </div>
        }
      />

      {/* ⚠ Said on the screen, because the buttons above disappear and somebody will wonder why. */}
      {board.status === 'Concluded' && (
        <div className="rounded-md border border-amber-300/60 bg-amber-50 p-4 text-sm dark:border-amber-900/60 dark:bg-amber-950/40">
          <p className="font-medium">This board has reported{board.concludedOn ? ` (${day(board.concludedOn)})` : ''}.</p>
          <p className="mt-1">
            Every case before it is decided or withdrawn, and its members, sittings and findings are
            now fixed — leave or separation may already rest on them.{' '}
            <strong>A finding that needs revisiting is a case before a new board</strong> — which is
            also how it works on paper.
          </p>
        </div>
      )}

      {/*
        ⚠ Nothing left to decide, yet the board is open: every case was withdrawn. It does not
        "report" on nothing — HR adds a case or stops the board, saying why.
      */}
      {open && cases.length > 0 && listed.length === 0 && (
        <div className="rounded-md border border-amber-300/60 bg-amber-50 p-4 text-sm dark:border-amber-900/60 dark:bg-amber-950/40">
          Nothing is left before this board: every case has been withdrawn and none was decided. Add a
          case, or {dissolving ? 'dissolve the board' : 'cancel the request'} and say why.
        </div>
      )}

      {/*
        ⚠ The reason was being WRITTEN and read by nothing until lane K5's panel. `CancelAsync`
        insists on one, and a required field nothing shows is a question nobody answers twice.
      */}
      {board.status === 'Cancelled' && (
        <div className="rounded-md border border-red-300/60 bg-red-50 p-4 text-sm dark:border-red-900/60 dark:bg-red-950/40">
          <p className="font-medium">
            {board.wasDissolved
              ? 'This board was dissolved after it was convened, before it reported.'
              : 'This request was cancelled before a board was convened.'}
          </p>
          {(board.cancelledOn || board.cancelledByName) && (
            <p className="mt-1 text-muted-foreground">
              {board.wasDissolved ? 'Dissolved' : 'Cancelled'}
              {board.cancelledOn ? ` on ${day(board.cancelledOn)}` : ''}
              {board.cancelledByName ? ` by ${board.cancelledByName}` : ''}
            </p>
          )}
          {board.cancellationReason && (
            <p className="mt-1 whitespace-pre-wrap">“{board.cancellationReason}”</p>
          )}
          <p className="mt-2 text-muted-foreground">
            {board.wasDissolved ? 'Its members and sittings stay on the record below. ' : ''}
            Its open cases were withdrawn with this reason. A case never decided satisfies no evidence
            rule and justifies no medical retirement. <strong>A board that still needs to sit is a new
            board.</strong>
          </p>
        </div>
      )}

      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">The board</CardTitle></CardHeader>
        <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
          <Row label="Convened by" value={MEDICAL_BOARD_KIND_LABEL[board.kind] ?? board.kind} />
          <Row label="Requested" value={`${day(board.requestedOn)}${board.requestedByName ? ` by ${board.requestedByName}` : ''}`} />
          <Row label="Convened" value={day(board.convenedOn)} />
          <Row label="Facility" value={board.facilityName} />
          <Row label="Reported" value={day(board.concludedOn)} />
        </CardContent>
      </Card>

      {/* ── Cases (lane K-II-a) ─────────────────────────────────────────── */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Cases</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {cases.length === 0 && (
            <p className="text-sm text-muted-foreground">Nobody is before this board.</p>
          )}
          {board.status === 'Convened' && sittings.length === 0 && listed.length > 0 && (
            <p className="text-xs text-muted-foreground">
              Record a sitting, with who was present, before deciding a case: a case is decided at a
              sitting, by the members there.
            </p>
          )}
          {cases.map((c) => (
            <div key={c.id} className="rounded-md border p-4">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <div className="font-medium">
                    {c.employeeName}
                    <span className="ml-2 text-xs text-muted-foreground">{c.employeeNumber}</span>
                  </div>
                  <div className="text-sm text-muted-foreground">
                    {MEDICAL_BOARD_PURPOSE_LABEL[c.purpose] ?? c.purpose}
                    {/* ⚠ Said on the case, because a leave request refused on it would otherwise puzzle. */}
                    {!c.coversAbsence && ' · not about an absence, so leave cannot rest on it'}
                  </div>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <StatusBadge status={MEDICAL_BOARD_CASE_STATUS_LABEL[c.status]} />
                  {c.status === 'Listed' && canDecide && (
                    <Button size="sm" onClick={() => openDecide(c)}>
                      <Gavel className="mr-2 h-4 w-4" /> Record the finding
                    </Button>
                  )}
                  {c.status === 'Listed' && open && (
                    <Button size="sm" variant="outline" onClick={() => openWithdraw(c)}>
                      <Undo2 className="mr-2 h-4 w-4" /> Withdraw
                    </Button>
                  )}
                </div>
              </div>

              <div className="mt-2 grid grid-cols-2 gap-x-6 md:grid-cols-3">
                <Row label="Reason" value={c.reason} />
                <Row label="Listed" value={`${day(c.requestedOn)}${c.requestedByName ? ` by ${c.requestedByName}` : ''}`} />
                <Row
                  label="Based on an examination"
                  value={
                    c.basedOnExamDate
                      ? `${day(c.basedOnExamDate)}${c.basedOnExamResult ? ` · ${MEDICAL_BOARD_OUTCOME_LABEL[c.basedOnExamResult]}` : ''}`
                      : undefined
                  }
                />
                <Row
                  label="Health record"
                  value={
                    c.healthProfileId ? (
                      <Link href={`/hr/medical/health/${c.healthProfileId}`} className="underline underline-offset-2">
                        Open
                      </Link>
                    ) : (
                      'None on file'
                    )
                  }
                />
              </div>

              {c.status === 'Concluded' && (
                <div className="mt-3 space-y-2 border-t pt-3">
                  <div className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
                    <Row label="Finding" value={c.outcome ? MEDICAL_BOARD_OUTCOME_LABEL[c.outcome] : '—'} />
                    <Row label="Decided at the sitting of" value={day(c.decidedAtSittingDate)} />
                    <Row
                      label="Decided by"
                      value={
                        c.decidedBy.length
                          ? c.decidedBy.join(', ')
                          : 'Not recorded — decided before attendance was kept'
                      }
                    />
                    <Row label="Recorded" value={`${day(c.concludedOn) ?? '—'}${c.concludedByName ? ` by ${c.concludedByName}` : ''}`} />
                    <Row label="Review due" value={day(c.reviewDueDate)} />
                    <Row label="Medical retirement" value={c.recommendsMedicalRetirement ? 'Recommended' : 'Not recommended'} />
                  </div>
                  {c.findings && <Row label="Findings" value={<span className="whitespace-pre-wrap">{c.findings}</span>} />}
                  <Row label="Recommendation" value={<span className="whitespace-pre-wrap">{c.recommendation}</span>} />
                  {c.restrictions && <Row label="Restrictions" value={<span className="whitespace-pre-wrap">{c.restrictions}</span>} />}
                  {c.recommendsMedicalRetirement && (
                    <p className="text-sm text-muted-foreground">
                      ⚠ A <strong>recommendation, not an act</strong>. Retiring somebody on medical
                      grounds is a separation, raised in that module, which can name this board.
                    </p>
                  )}
                </div>
              )}

              {c.status === 'Withdrawn' && (
                <div className="mt-3 border-t pt-3 text-sm">
                  <span className="text-muted-foreground">
                    Withdrawn{c.withdrawnOn ? ` on ${day(c.withdrawnOn)}` : ''}
                    {c.withdrawnByName ? ` by ${c.withdrawnByName}` : ''}:
                  </span>{' '}
                  <span className="whitespace-pre-wrap">{c.withdrawalReason ?? '—'}</span>
                </div>
              )}
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">Members</CardTitle></CardHeader>
        <CardContent className="p-0">
          {(board.members?.length ?? 0) === 0 ? (
            <EmptyState
              icon={Users}
              title="Nobody appointed yet"
              description="A board is its panel — it cannot be convened until at least one member is appointed."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Decides</TableHead>
                  <TableHead>Kind</TableHead>
                  <TableHead>From</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {board.members.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell className="font-medium">{m.displayName}</TableCell>
                    <TableCell>{MEDICAL_BOARD_MEMBER_ROLE_LABEL[m.role]}</TableCell>
                    <TableCell className="text-muted-foreground">{m.decides ? 'Yes' : 'No'}</TableCell>
                    <TableCell className="text-muted-foreground">{m.memberKind}</TableCell>
                    <TableCell className="text-muted-foreground">{m.institution ?? '—'}</TableCell>
                    <TableCell className="text-right">
                      {open && (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={busy}
                          onClick={() => run('Member removed', () => medicalBoardService.removeMember(id, m.id))}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
          {open && (board.members?.length ?? 0) > 0 && (
            <p className="px-4 py-2 text-xs text-muted-foreground">
              Removing a member does not change a past sitting: its attendance records who sat.
            </p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">Sittings</CardTitle></CardHeader>
        <CardContent className="p-0">
          {sittings.length === 0 ? (
            <EmptyState
              icon={CalendarPlus}
              title="It has not met"
              description="A case is decided at a sitting, by the members present — so nothing can be decided until the board has sat."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Venue</TableHead>
                  <TableHead>Present</TableHead>
                  <TableHead>Decided</TableHead>
                  <TableHead>Notes</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {sittings.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell>{day(s.sittingDate)}</TableCell>
                    <TableCell>{s.venue ?? '—'}</TableCell>
                    <TableCell className="text-sm">{presentLine(s)}</TableCell>
                    <TableCell>{s.casesDecided || '—'}</TableCell>
                    <TableCell className="whitespace-pre-wrap text-muted-foreground">{s.notes ?? '—'}</TableCell>
                    <TableCell className="text-right">
                      {/* ⚠ Fixed once a case is decided at it: it is then the panel that decided. */}
                      {open && s.casesDecided === 0 && (
                        <Button variant="ghost" size="sm" onClick={() => openAttendance(s)}>
                          Attendance
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

      {/*
        ── Documents (lanes K4, K-II-a) ─────────────────────────────────────
        ⚠ Attachable at any status: the signed report usually arrives after the board has
        reported. Removable only while it is open — afterwards its papers are part of the record.
        A paper can be about the board, or about one case.
      */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-base">Documents</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex flex-wrap items-end gap-2">
            <div className="min-w-[16rem] flex-1 space-y-1.5">
              <Label htmlFor="document-description">Description</Label>
              <Input
                id="document-description"
                value={documentDescription}
                onChange={(e) => setDocumentDescription(e.target.value)}
                placeholder="e.g. specialist's report, signed minutes"
              />
            </div>
            <div className="w-64 space-y-1.5">
              <Label htmlFor="document-case">About</Label>
              <Select value={documentCaseId} onValueChange={setDocumentCaseId}>
                <SelectTrigger id="document-case"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={THE_BOARD}>The board as a whole</SelectItem>
                  {cases.map((c) => (
                    <SelectItem key={c.id} value={c.id}>{c.employeeName}&apos;s case</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <input
              ref={fileInput}
              type="file"
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) void upload(file);
              }}
            />
            <Button variant="outline" disabled={busy} onClick={() => fileInput.current?.click()}>
              <Paperclip className="mr-2 h-4 w-4" /> Attach a document
            </Button>
          </div>
          <p className="text-xs text-muted-foreground">
            ⚠ Medical-grade content, checked for viruses on upload and visible to holders of the
            medical permissions only.
            {!open && ' This board is settled: papers can still be added, but none can be removed.'}
          </p>

          {documents.length === 0 ? (
            <p className="py-2 text-sm text-muted-foreground">No documents attached.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>File</TableHead>
                  <TableHead>About</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>Attached</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {documents.map((d) => (
                  <TableRow key={d.id}>
                    <TableCell className="font-medium">
                      <span className="inline-flex items-center gap-2">
                        <FileText className="h-4 w-4 text-muted-foreground" /> {d.fileName}
                      </span>
                      <span className="block text-xs text-muted-foreground">{fileSize(d.fileSize)}</span>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {d.caseId ? `${d.caseEmployeeName ?? 'A case'}` : 'The board'}
                    </TableCell>
                    <TableCell className="text-muted-foreground">{d.description ?? '—'}</TableCell>
                    <TableCell className="text-muted-foreground">
                      {d.uploadDate?.slice(0, 10)}
                      {d.uploadedByName ? ` · ${d.uploadedByName}` : ''}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() =>
                          medicalBoardService.downloadDocument(id, d).catch((e: any) =>
                            toast({ title: 'Error', description: e?.message || 'Could not download.', variant: 'destructive' }),
                          )
                        }
                      >
                        <Download className="h-4 w-4" />
                      </Button>
                      {open && (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={busy}
                          onClick={() => run('Document removed', () => medicalBoardService.removeDocument(id, d.id))}
                        >
                          <Trash2 className="h-4 w-4" />
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

      {/* ── Add a case ───────────────────────────────────────────────────── */}
      <Dialog open={dialog === 'case'} onOpenChange={(o) => setDialog(o ? 'case' : null)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Add a case</DialogTitle>
            <DialogDescription>
              Another employee before this board, asked their own question and decided on their own
              finding. ⚠ Somebody already before the board, or sitting on it, cannot be added.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <MedicalBoardCaseFields value={caseDraft} onChange={setCaseDraft} enabled={dialog === 'case'} idPrefix="add-case" />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !caseDraftComplete(caseDraft)}
              onClick={() =>
                run('Case added', async () => {
                  if (!caseDraft.purpose) return;
                  await medicalBoardService.addCase(id, {
                    employeeId: caseDraft.employeeId,
                    purpose: caseDraft.purpose,
                    reason: caseDraft.reason.trim(),
                    basedOnExamId: caseDraft.examId === NO_EXAM ? null : caseDraft.examId,
                  });
                  setCaseDraft(EMPTY_CASE);
                })
              }
            >
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Appoint a member ─────────────────────────────────────────────── */}
      <Dialog open={dialog === 'member'} onOpenChange={(o) => setDialog(o ? 'member' : null)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Appoint a member</DialogTitle>
            <DialogDescription>
              A board is not only doctors — it commonly carries HR as secretary and a staff
              representative alongside the clinicians.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="member-mode">Who is this?</Label>
              <Select value={memberMode} onValueChange={(v) => setMemberMode(v as MemberMode)}>
                <SelectTrigger id="member-mode"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="physician">A physician on the register</SelectItem>
                  <SelectItem value="employee">Somebody who works here</SelectItem>
                  <SelectItem value="external">Somebody from outside, not on the register</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {memberMode === 'physician' ? (
              <div className="space-y-2">
                <Label htmlFor="member-physician">Physician <span className="text-red-500">*</span></Label>
                <Select value={memberPhysicianId} onValueChange={setMemberPhysicianId}>
                  <SelectTrigger id="member-physician">
                    <SelectValue placeholder={activePhysicians.length ? 'Choose from the register' : 'The register is empty'} />
                  </SelectTrigger>
                  <SelectContent>
                    {activePhysicians.map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.fullName}
                        {p.specialization ? ` · ${p.specialization}` : ''}
                        {p.facilityName ? ` · ${p.facilityName}` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  Somebody missing? Add them under{' '}
                  <Link href="/hr/medical/physicians" className="underline underline-offset-2">Physicians</Link>
                  , or appoint them from outside by name.
                </p>
              </div>
            ) : memberMode === 'employee' ? (
              <div className="space-y-2">
                <Label>Employee <span className="text-red-500">*</span></Label>
                <EmployeePicker value={memberEmployeeId || null} onChange={(v) => setMemberEmployeeId(v ?? '')} />
                <p className="text-xs text-muted-foreground">
                  ⚠ Nobody whose case is before this board can sit on it.
                </p>
              </div>
            ) : (
              <div className="space-y-2">
                <Label htmlFor="member-name">Name <span className="text-red-500">*</span></Label>
                <Input
                  id="member-name"
                  value={memberName}
                  onChange={(e) => setMemberName(e.target.value)}
                  placeholder="e.g. Dr K. Owusu"
                />
              </div>
            )}

            <div className="space-y-2">
              <Label htmlFor="member-institution">From</Label>
              <Input
                id="member-institution"
                value={institution}
                onChange={(e) => setInstitution(e.target.value)}
                placeholder="e.g. Ridge Hospital, or HR Department"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="member-role">Role</Label>
              <Select value={role} onValueChange={(v) => setRole(v as MedicalBoardMemberRole)}>
                <SelectTrigger id="member-role"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {(Object.keys(MEDICAL_BOARD_MEMBER_ROLE_LABEL) as MedicalBoardMemberRole[]).map((r) => (
                    <SelectItem key={r} value={r}>{MEDICAL_BOARD_MEMBER_ROLE_LABEL[r]}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                A board has one chair. The chair and members decide; a secretary or observer attends
                without deciding.
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={
                busy ||
                (memberMode === 'physician'
                  ? !memberPhysicianId
                  : memberMode === 'employee'
                    ? !memberEmployeeId
                    : !memberName.trim())
              }
              onClick={() =>
                run('Member appointed', async () => {
                  // ⚠ Exactly one identity per row (lane K7) — the server refuses two.
                  await medicalBoardService.addMember(id, {
                    physicianId: memberMode === 'physician' ? memberPhysicianId : null,
                    employeeId: memberMode === 'employee' ? memberEmployeeId : null,
                    memberName: memberMode === 'external' ? memberName.trim() : null,
                    institution: institution.trim() || null,
                    role,
                  });
                  setMemberPhysicianId('');
                  setMemberEmployeeId('');
                  setMemberName('');
                  setInstitution('');
                  setRole('Member');
                })
              }
            >
              Appoint
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Record a sitting ─────────────────────────────────────────────── */}
      <Dialog open={dialog === 'sitting'} onOpenChange={(o) => setDialog(o ? 'sitting' : null)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Record a sitting</DialogTitle>
            <DialogDescription>A board may meet more than once, and decide different cases at different sittings.</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="sitting-date">Date <span className="text-red-500">*</span></Label>
              <Input id="sitting-date" type="date" value={sittingDate} onChange={(e) => setSittingDate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="sitting-venue">Venue</Label>
              <Input id="sitting-venue" value={venue} onChange={(e) => setVenue(e.target.value)} />
            </div>
            {attendanceTicks}
            <div className="space-y-2">
              <Label htmlFor="sitting-notes">Notes</Label>
              <Textarea id="sitting-notes" rows={4} value={notes} onChange={(e) => setNotes(e.target.value)} />
              <p className="text-xs text-muted-foreground">
                ⚠ Medical-grade content. Visible to holders of the medical permissions only.
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !sittingDate}
              onClick={() =>
                run('Sitting recorded', async () => {
                  await medicalBoardService.recordSitting(id, {
                    sittingDate,
                    venue: venue.trim() || null,
                    notes: notes.trim() || null,
                    attendeeMemberIds: Array.from(present),
                  });
                  setVenue('');
                  setNotes('');
                })
              }
            >
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Attendance at an existing sitting ────────────────────────────── */}
      <Dialog open={dialog === 'attendance'} onOpenChange={(o) => setDialog(o ? 'attendance' : null)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Who was present</DialogTitle>
            <DialogDescription>
              The sitting of {day(sittings.find((s) => s.id === attendanceSittingId)?.sittingDate)}. ⚠ Once
              a case is decided at it, this is fixed.
            </DialogDescription>
          </DialogHeader>
          {attendanceTicks}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy}
              onClick={() =>
                run('Attendance recorded', () =>
                  medicalBoardService.setSittingAttendance(id, attendanceSittingId, Array.from(present)),
                )
              }
            >
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Record a case's finding ──────────────────────────────────────── */}
      <Dialog open={dialog === 'decide'} onOpenChange={(o) => setDialog(o ? 'decide' : null)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>The finding on {caseBeingActedOn?.employeeName}</DialogTitle>
            <DialogDescription>
              ⚠ This cannot be undone. Leave or separation may rest on what it says. The board
              reports by itself once no case is left open.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="decide-sitting">Decided at the sitting of <span className="text-red-500">*</span></Label>
              <Select value={decideSittingId} onValueChange={setDecideSittingId}>
                <SelectTrigger id="decide-sitting"><SelectValue placeholder="Choose the sitting" /></SelectTrigger>
                <SelectContent>
                  {sittings.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {day(s.sittingDate)} · {s.attendees.length} present, {decidingCount(s)} deciding
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {decideSitting && (
                <p className="text-xs text-muted-foreground">
                  Decided by those present: {presentLine(decideSitting)}.
                  {decidingCount(decideSitting) === 0 &&
                    ' ⚠ Nobody who decides was recorded there — record the attendance first.'}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="outcome">Finding <span className="text-red-500">*</span></Label>
              <Select value={outcome} onValueChange={(v) => setOutcome(v as MedicalBoardOutcome)}>
                <SelectTrigger id="outcome"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {(Object.keys(MEDICAL_BOARD_OUTCOME_LABEL) as MedicalBoardOutcome[]).map((o) => (
                    <SelectItem key={o} value={o}>{MEDICAL_BOARD_OUTCOME_LABEL[o]}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="recommendation">Recommendation <span className="text-red-500">*</span></Label>
              <Textarea
                id="recommendation"
                rows={3}
                value={recommendation}
                onChange={(e) => setRecommendation(e.target.value)}
                placeholder="e.g. light duties for eight weeks, reviewed thereafter."
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="findings">Findings</Label>
              <Textarea id="findings" rows={3} value={findings} onChange={(e) => setFindings(e.target.value)} />
            </div>

            <div className="space-y-2">
              <Label htmlFor="restrictions">Restrictions</Label>
              <Input id="restrictions" value={restrictions} onChange={(e) => setRestrictions(e.target.value)} />
            </div>

            <div className="space-y-2">
              <Label htmlFor="review-due">Review due</Label>
              <Input id="review-due" type="date" value={reviewDueDate} onChange={(e) => setReviewDueDate(e.target.value)} />
            </div>

            <label className="flex items-start gap-2 text-sm">
              <input
                type="checkbox"
                className="mt-1"
                checked={retire}
                onChange={(e) => setRetire(e.target.checked)}
              />
              <span>
                The board recommends <strong>retirement on medical grounds</strong>.
                <span className="mt-1 block text-xs text-muted-foreground">
                  ⚠ A recommendation, not an act. Retiring somebody is a separation, raised in that
                  module, which can name this board.
                </span>
              </span>
            </label>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !decideSittingId || !recommendation.trim()}
              onClick={() =>
                run('Finding recorded', () =>
                  medicalBoardService.concludeCase(id, caseId, {
                    sittingId: decideSittingId,
                    outcome,
                    findings: findings.trim() || null,
                    recommendation: recommendation.trim(),
                    restrictions: restrictions.trim() || null,
                    reviewDueDate: reviewDueDate || null,
                    recommendsMedicalRetirement: retire,
                  }),
                )
              }
            >
              Record the finding
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Withdraw a case ──────────────────────────────────────────────── */}
      <Dialog open={dialog === 'withdraw'} onOpenChange={(o) => setDialog(o ? 'withdraw' : null)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Withdraw {caseBeingActedOn?.employeeName}&apos;s case?</DialogTitle>
            <DialogDescription>
              The case leaves the board without a finding, so nothing can rest on it. ⚠ There is no
              undoing it; a question that still needs answering is a case before a new board.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label htmlFor="withdraw-reason">Why is it being withdrawn?</Label>
            <Textarea
              id="withdraw-reason"
              rows={3}
              value={withdrawReason}
              onChange={(e) => setWithdrawReason(e.target.value)}
              placeholder="e.g. the employee returned to work fully fit before the board sat"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Keep the case</Button>
            <Button
              variant="destructive"
              disabled={busy || !withdrawReason.trim()}
              onClick={() =>
                run('Case withdrawn', () => medicalBoardService.withdrawCase(id, caseId, withdrawReason.trim()))
              }
            >
              Withdraw
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/*
        ⚠ The reason is REQUIRED by the service, which refuses a blank one, so the button waits
        for it rather than letting the server say no. It is required because a cancelled board is
        the one state that looks like an administrative accident from the outside — the panel never
        met, or met and was stood down, and only the reason distinguishes them.
      */}
      <Dialog open={dialog === 'cancel'} onOpenChange={(o) => setDialog(o ? 'cancel' : null)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>{dissolving ? 'Dissolve this board?' : 'Cancel this request for a board?'}</DialogTitle>
            <DialogDescription>
              {dissolving
                ? 'The panel has been convened. Dissolving it stands it down before it reports; its members and sittings stay on the record.'
                : 'No panel has been convened yet, so this cancels the request.'}{' '}
              Every case still open is withdrawn with the reason. ⚠ There is no undoing it. A board
              that still needs to sit is a new board — which is also how it works on paper.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label htmlFor="cancel-reason">
                {dissolving ? 'Why is it being dissolved?' : 'Why is it being cancelled?'}
              </Label>
              <Textarea
                id="cancel-reason"
                rows={3}
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                placeholder="e.g. the employee resigned before the board could sit"
              />
            </div>

            {/*
              ⚠ Said here rather than discovered later. Cancelling does not unlink anything: a
              leave request that names this board goes on naming it. What changes is that its case
              is withdrawn, and a withdrawn case satisfies no evidence rule. One already approved on
              a decided case stays approved.
            */}
            <p className="rounded-md border border-amber-300/60 bg-amber-50 p-3 text-xs dark:border-amber-900/60 dark:bg-amber-950/40">
              If a leave request already points at this board, {dissolving ? 'dissolving' : 'cancelling'}{' '}
              does <strong>not</strong> unlink it — but a case that was never decided satisfies no
              evidence rule, so that request will be refused at submission until it names another
              board or attaches a recommendation. Leave already approved on a decided case stays approved.
            </p>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>
              Keep the board
            </Button>
            <Button
              variant="destructive"
              disabled={busy || !cancelReason.trim()}
              onClick={() =>
                run(dissolving ? 'Board dissolved' : 'Request cancelled', () =>
                  medicalBoardService.cancel(id, cancelReason.trim()),
                )
              }
            >
              {stopLabel}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
