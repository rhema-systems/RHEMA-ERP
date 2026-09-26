'use client';

import { useRef, useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Ban, CalendarPlus, Download, FileText, Gavel, Loader2, Paperclip, Trash2, UserPlus, Users,
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
import { medicalBoardService } from '@/services/hr/medical-board.service';
import { medicalFacilityService } from '@/services/hr/medical-reference.service';
import {
  MEDICAL_BOARD_MEMBER_ROLE_LABEL,
  MEDICAL_BOARD_OUTCOME_LABEL,
  MEDICAL_BOARD_PURPOSE_LABEL,
  boardStatusLabel,
  type MedicalBoardMemberRole,
  type MedicalBoardOutcome,
} from '@/types/hr/medical-board';

type MemberMode = 'physician' | 'employee' | 'external';

const fileSize = (bytes?: number | null) =>
  bytes == null ? '' : bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

function Row({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

/**
 * One medical board: who sits on it, when it met, and what it decided.
 *
 * ⚠ **The lifecycle is a ratchet.** Requested → Convened → Concluded, with cancel available until
 * it reports. There is no un-conclude, and once it has reported its membership and sittings are
 * frozen — they are part of what the recommendation MEANS, and a leave request approved on that
 * recommendation stays approved. So the buttons disappear rather than failing.
 */
export default function MedicalBoardDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [busy, setBusy] = useState(false);
  const [dialog, setDialog] = useState<null | 'member' | 'sitting' | 'conclude' | 'cancel'>(null);

  // member form — a registered physician first: the board's clinicians are who it rests on (lane K3)
  const [memberMode, setMemberMode] = useState<MemberMode>('physician');
  const [memberPhysicianId, setMemberPhysicianId] = useState('');
  const [memberEmployeeId, setMemberEmployeeId] = useState('');
  const [memberName, setMemberName] = useState('');
  const [institution, setInstitution] = useState('');
  const [role, setRole] = useState<MedicalBoardMemberRole>('Member');

  // sitting form
  const [sittingDate, setSittingDate] = useState(new Date().toISOString().slice(0, 10));
  const [venue, setVenue] = useState('');
  const [notes, setNotes] = useState('');

  // cancel form — the server refuses a blank reason, so the button waits for one
  const [cancelReason, setCancelReason] = useState('');

  // conclude form
  const [outcome, setOutcome] = useState<MedicalBoardOutcome>('Fit');
  const [findings, setFindings] = useState('');
  const [recommendation, setRecommendation] = useState('');
  const [restrictions, setRestrictions] = useState('');
  const [reviewDueDate, setReviewDueDate] = useState('');
  const [retire, setRetire] = useState(false);

  // documents (lane K4)
  const fileInput = useRef<HTMLInputElement>(null);
  const [documentDescription, setDocumentDescription] = useState('');

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
  // ⚠ Members too (lane K7): a convened board can lose its members, and one with nobody on it
  // cannot report. The server refuses it; the button waits for it.
  const canConclude =
    board.status === 'Convened' && (board.sittings?.length ?? 0) > 0 && (board.members?.length ?? 0) > 0;

  // ⚠ One action, two words (lane K5): before convening it is only a request; after, a panel.
  const dissolving = board.status === 'Convened';
  const stopLabel = dissolving ? 'Dissolve the board' : 'Cancel the request';

  const upload = async (file: File) => {
    await run('Document attached', async () => {
      await medicalBoardService.uploadDocument(id, file, documentDescription.trim() || null);
      setDocumentDescription('');
    });
    // Cleared either way, or choosing the same file again after a refusal would do nothing.
    if (fileInput.current) fileInput.current.value = '';
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={board.boardNumber}
        description={`${board.employeeName} · ${board.employeeNumber}`}
        backHref="/hr/medical/boards"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={boardStatusLabel(board)} />
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
              <Button variant="outline" onClick={() => setDialog('sitting')}>
                <CalendarPlus className="mr-2 h-4 w-4" /> Record a sitting
              </Button>
            )}
            {canConclude && (
              <Button onClick={() => setDialog('conclude')}>
                <Gavel className="mr-2 h-4 w-4" /> Report
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
          <p className="font-medium">This board has reported.</p>
          <p className="mt-1">
            Its members, its sittings and its finding are now fixed. They are part of what the
            recommendation means, and leave or separation may already rest on it.{' '}
            <strong>A finding that needs revisiting is a new board</strong> — which is also how it
            works on paper.
          </p>
        </div>
      )}

      {/*
        ⚠ The reason was being WRITTEN and read by nothing. `CancelAsync` insists on one — it
        refuses a blank — and until this panel existed the only sign a board had been cancelled was
        a grey badge, with the explanation sitting in a column no screen displayed. A required field
        that nothing shows is a question nobody answers twice.
      */}
      {board.status === 'Cancelled' && (
        <div className="rounded-md border border-red-300/60 bg-red-50 p-4 text-sm dark:border-red-900/60 dark:bg-red-950/40">
          {/* ⚠ Which it was, when and by whom (lane K5) — read back, not left to the badge. */}
          <p className="font-medium">
            {board.wasDissolved
              ? 'This board was dissolved after it was convened, before it reported.'
              : 'This request was cancelled before a board was convened.'}
          </p>
          {(board.cancelledOn || board.cancelledByName) && (
            <p className="mt-1 text-muted-foreground">
              {board.wasDissolved ? 'Dissolved' : 'Cancelled'}
              {board.cancelledOn ? ` on ${board.cancelledOn.slice(0, 10)}` : ''}
              {board.cancelledByName ? ` by ${board.cancelledByName}` : ''}
            </p>
          )}
          {board.cancellationReason && (
            <p className="mt-1 whitespace-pre-wrap">“{board.cancellationReason}”</p>
          )}
          <p className="mt-2 text-muted-foreground">
            {board.wasDissolved
              ? 'Its members and sittings stay on the record below. '
              : ''}
            It never reached a finding, so it satisfies no evidence rule and justifies no medical
            retirement. <strong>A board that still needs to sit is a new board.</strong>
          </p>
        </div>
      )}

      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">The board</CardTitle></CardHeader>
        <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
          <Row
            label="Purpose"
            value={
              <>
                {MEDICAL_BOARD_PURPOSE_LABEL[board.purpose] ?? board.purpose}
                {/* ⚠ Said on the board, because a leave request refused on it would otherwise puzzle. */}
                {!board.coversAbsence && (
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    Not about an absence, so leave cannot rest on it.
                  </span>
                )}
              </>
            }
          />
          <Row label="Reason" value={board.reason} />
          <Row label="Requested" value={`${board.requestedOn?.slice(0, 10)}${board.requestedByName ? ` by ${board.requestedByName}` : ''}`} />
          <Row label="Convened" value={board.convenedOn?.slice(0, 10)} />
          <Row label="Facility" value={board.facilityName} />
          <Row
            label="Based on an examination"
            value={
              board.basedOnExamDate
                ? `${board.basedOnExamDate.slice(0, 10)}${board.basedOnExamResult ? ` · ${MEDICAL_BOARD_OUTCOME_LABEL[board.basedOnExamResult]}` : ''}`
                : undefined
            }
          />
          <Row
            label="Health record"
            value={
              board.healthProfileId ? (
                <Link href={`/hr/medical/health/${board.healthProfileId}`} className="underline underline-offset-2">
                  Open
                </Link>
              ) : (
                'None on file'
              )
            }
          />
          <Row label="Concluded" value={board.concludedOn?.slice(0, 10)} />
          <Row label="Reported by" value={board.concludedByName} />
        </CardContent>
      </Card>

      {board.status === 'Concluded' && (
        <Card>
          <CardHeader className="pb-2"><CardTitle className="text-base">The finding</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            <div className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <Row
                label="Outcome"
                value={board.outcome ? MEDICAL_BOARD_OUTCOME_LABEL[board.outcome] : '—'}
              />
              <Row label="Review due" value={board.reviewDueDate?.slice(0, 10)} />
              <Row
                label="Medical retirement"
                value={board.recommendsMedicalRetirement ? 'Recommended' : 'Not recommended'}
              />
            </div>
            {board.findings && <Row label="Findings" value={<span className="whitespace-pre-wrap">{board.findings}</span>} />}
            <Row label="Recommendation" value={<span className="whitespace-pre-wrap">{board.recommendation}</span>} />
            {board.restrictions && <Row label="Restrictions" value={<span className="whitespace-pre-wrap">{board.restrictions}</span>} />}

            {board.recommendsMedicalRetirement && (
              <p className="text-sm text-muted-foreground">
                ⚠ This is a <strong>recommendation, not an act</strong>. Retiring somebody on medical
                grounds is a separation, raised in that module, which can point back at this board.
              </p>
            )}
          </CardContent>
        </Card>
      )}

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
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">Sittings</CardTitle></CardHeader>
        <CardContent className="p-0">
          {(board.sittings?.length ?? 0) === 0 ? (
            <EmptyState
              icon={CalendarPlus}
              title="It has not met"
              description="A board cannot report until it has sat at least once."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Venue</TableHead>
                  <TableHead>Notes</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {board.sittings.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell>{s.sittingDate?.slice(0, 10)}</TableCell>
                    <TableCell>{s.venue ?? '—'}</TableCell>
                    <TableCell className="whitespace-pre-wrap text-muted-foreground">{s.notes ?? '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/*
        ── Documents (lane K4) ───────────────────────────────────────────────
        ⚠ Attachable at any status: the signed report usually arrives after the board has
        reported. Removable only while it is open — afterwards its papers are part of the record.
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
                  ⚠ The employee this board is about cannot sit on it.
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
              <p className="text-xs text-muted-foreground">A board has one chair.</p>
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
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>Record a sitting</DialogTitle>
            <DialogDescription>A board may meet more than once before it reports.</DialogDescription>
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
                    sittingDate, venue: venue.trim() || null, notes: notes.trim() || null,
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

      {/* ── Report ───────────────────────────────────────────────────────── */}
      <Dialog open={dialog === 'conclude'} onOpenChange={(o) => setDialog(o ? 'conclude' : null)}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>The board reports</DialogTitle>
            <DialogDescription>
              ⚠ This cannot be undone. Its members and sittings are fixed from here, and leave or
              separation may rest on what it says.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
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
                  module, which can point back at this board.
                </span>
              </span>
            </label>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDialog(null)} disabled={busy}>Cancel</Button>
            <Button
              disabled={busy || !recommendation.trim()}
              onClick={() =>
                run('Board reported', () =>
                  medicalBoardService.conclude(id, {
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
              Report
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
              ⚠ There is no undoing it. A board that still needs to sit is a new board — which is
              also how it works on paper.
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
              leave request that names this board goes on naming it. What changes is that the board
              stops SATISFYING the evidence rule, which only a concluded board ever did — so a
              request still waiting to be submitted will be refused until it names another board or
              attaches a report. One already approved on this board stays approved, the same
              ratchet that applies when a board reports.
            */}
            <p className="rounded-md border border-amber-300/60 bg-amber-50 p-3 text-xs dark:border-amber-900/60 dark:bg-amber-950/40">
              If a leave request already points at this board, {dissolving ? 'dissolving' : 'cancelling'}{' '}
              does <strong>not</strong> unlink it — but a board that never reported satisfies no
              evidence rule, so that request will be refused at submission until it names another
              board or attaches a recommendation. Leave already approved on it stays approved.
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
