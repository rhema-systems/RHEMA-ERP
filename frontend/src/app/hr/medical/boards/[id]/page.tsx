'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarPlus, Gavel, Loader2, Trash2, UserPlus, Users } from 'lucide-react';
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
import {
  MEDICAL_BOARD_MEMBER_ROLE_LABEL,
  MEDICAL_BOARD_OUTCOME_LABEL,
  type MedicalBoardMemberRole,
  type MedicalBoardOutcome,
} from '@/types/hr/medical-board';

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
  const [dialog, setDialog] = useState<null | 'member' | 'sitting' | 'conclude'>(null);

  // member form
  const [memberMode, setMemberMode] = useState<'employee' | 'external'>('external');
  const [memberEmployeeId, setMemberEmployeeId] = useState('');
  const [memberName, setMemberName] = useState('');
  const [institution, setInstitution] = useState('');
  const [role, setRole] = useState<MedicalBoardMemberRole>('Member');

  // sitting form
  const [sittingDate, setSittingDate] = useState(new Date().toISOString().slice(0, 10));
  const [venue, setVenue] = useState('');
  const [notes, setNotes] = useState('');

  // conclude form
  const [outcome, setOutcome] = useState<MedicalBoardOutcome>('Fit');
  const [findings, setFindings] = useState('');
  const [recommendation, setRecommendation] = useState('');
  const [restrictions, setRestrictions] = useState('');
  const [reviewDueDate, setReviewDueDate] = useState('');
  const [retire, setRetire] = useState(false);

  const queryKey = ['hr', 'medical-boards', id];
  const { data: board, isLoading } = useQuery({
    queryKey,
    queryFn: () => medicalBoardService.getById(id),
    enabled: !!id,
  });

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
  const canConclude = board.status === 'Convened' && (board.sittings?.length ?? 0) > 0;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={board.boardNumber}
        description={`${board.employeeName} · ${board.employeeNumber}`}
        backHref="/hr/medical/boards"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={board.status} />
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

      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-base">The board</CardTitle></CardHeader>
        <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
          <Row label="Reason" value={board.reason} />
          <Row label="Requested" value={`${board.requestedOn?.slice(0, 10)}${board.requestedByName ? ` by ${board.requestedByName}` : ''}`} />
          <Row label="Convened" value={board.convenedOn?.slice(0, 10)} />
          <Row label="Facility" value={board.facilityName} />
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
              <Select value={memberMode} onValueChange={(v) => setMemberMode(v as 'employee' | 'external')}>
                <SelectTrigger id="member-mode"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="employee">Somebody who works here</SelectItem>
                  <SelectItem value="external">Somebody from outside</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {memberMode === 'employee' ? (
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
              disabled={busy || (memberMode === 'employee' ? !memberEmployeeId : !memberName.trim())}
              onClick={() =>
                run('Member appointed', async () => {
                  await medicalBoardService.addMember(id, {
                    employeeId: memberMode === 'employee' ? memberEmployeeId : null,
                    memberName: memberMode === 'external' ? memberName.trim() : null,
                    institution: institution.trim() || null,
                    role,
                  });
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
    </div>
  );
}
