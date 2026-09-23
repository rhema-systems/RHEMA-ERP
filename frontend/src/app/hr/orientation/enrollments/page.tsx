'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Loader2,
  Plus,
  Search,
  Users,
  UserPlus,
  UserMinus,
  X,
  AlertTriangle,
  RotateCcw,
  Award,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import { Progress } from '@/components/ui/progress';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useToast } from '@/components/ui/use-toast';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { employeeOrientationService } from '@/services/hr/employee-orientation.service';
import { orientationProgramService } from '@/services/hr/orientation-program.service';
import { orientationSessionService } from '@/services/hr/orientation-session.service';
import {
  ORIENTATION_COMPLETION_STATUS_OPTIONS,
  ORIENTATION_ENROLLMENT_SOURCE_OPTIONS,
  SELF_PACED_DELIVERY_MODES,
  OCCUPYING_ENROLLMENT_STATUSES,
} from '@/types/hr/orientation';
import type {
  EmployeeOrientationSummary,
  OrientationCompletionStatus,
  OrientationEnrollmentSource,
} from '@/types/hr/orientation';

type Scope = 'programme' | 'overdue' | 'due-soon' | 'status';

const ALL = '__all__';

interface Picked {
  id: string;
  label: string;
}

/**
 * Who is on which programme, how far through, and what is overdue — plus the way people get onto a
 * programme in the first place.
 *
 * The scope tabs map onto distinct server reads rather than filtering one list: overdue and due-soon
 * are computed server-side against each enrollment's own deadline, which the summary DTO does not
 * carry enough of to recompute here.
 */
export default function OrientationEnrollmentsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [scope, setScope] = useState<Scope>('programme');
  const [programId, setProgramId] = useState<string>('');
  const [completionStatus, setCompletionStatus] =
    useState<OrientationCompletionStatus>('InProgress');
  const [search, setSearch] = useState('');

  const [enrolOpen, setEnrolOpen] = useState(false);
  const [enrolProgramId, setEnrolProgramId] = useState('');
  const [enrolSessionId, setEnrolSessionId] = useState(ALL);
  const [enrolSource, setEnrolSource] = useState<OrientationEnrollmentSource>('HrAssigned');
  const [picked, setPicked] = useState<Picked[]>([]);
  const [pickerKey, setPickerKey] = useState(0);
  const [enrolling, setEnrolling] = useState(false);
  const [skippedNote, setSkippedNote] = useState<string | null>(null);

  const [withdrawTarget, setWithdrawTarget] = useState<EmployeeOrientationSummary | null>(null);
  const [withdrawReason, setWithdrawReason] = useState('');
  const [busy, setBusy] = useState(false);

  // Round 4, lane I-b: HR may re-enrol somebody whose enrolment HR ended, and may open the next
  // cycle of a recurring programme early. The automation still does neither by itself.
  const [againTarget, setAgainTarget] = useState<{
    row: EmployeeOrientationSummary;
    kind: 'reenrol' | 'nextCycle';
  } | null>(null);

  // Round 4, lane K-b: a certificated programme now issues its certificate at completion; HR issues
  // one here for somebody who completed before that, and reissues. There was no button at all.
  const [certifyTarget, setCertifyTarget] = useState<EmployeeOrientationSummary | null>(null);

  const { data: programs = [] } = useQuery({
    queryKey: ['hr', 'orientation-programs'],
    queryFn: () => orientationProgramService.getAll(),
  });
  const recurringProgramIds = useMemo(
    () => new Set(programs.filter((p) => p.isRecurring && p.recurrenceFrequency).map((p) => p.id)),
    [programs],
  );
  const certificatedProgramIds = useMemo(
    () => new Set(programs.filter((p) => p.isCertificateIssued).map((p) => p.id)),
    [programs],
  );

  // Sessions for the programme being enrolled onto. A session is optional — a self-paced programme
  // has none — so an empty list never blocks the dialog.
  // ⚠ Round 4, lane L: the loading / error / permission state is KEPT. `const { data: sessions = [] }`
  //   alone drew a refused request (403) exactly like "no sessions" — the demo's "empty dropdown".
  const {
    data: sessions = [],
    isLoading: sessionsLoading,
    isError: sessionsFailed,
    error: sessionsError,
  } = useQuery({
    queryKey: ['hr', 'orientation-sessions', 'program', enrolProgramId],
    queryFn: () => orientationSessionService.getByProgram(enrolProgramId),
    enabled: !!enrolProgramId,
    retry: false,
  });

  // Round 4, lane L — only programmes that take enrolments are offered (Active and in their dates,
  // the server's own definition), matching the sessions screen; and what the session dropdown says.
  const enrollablePrograms = useMemo(
    () => programs.filter((p) => p.acceptsEnrolment !== false),
    [programs],
  );
  const hiddenProgramCount = programs.length - enrollablePrograms.length;
  const enrolProgram = programs.find((p) => p.id === enrolProgramId);
  const enrolSelfPaced =
    !!enrolProgram && SELF_PACED_DELIVERY_MODES.includes(enrolProgram.defaultDeliveryMode);
  const sortedSessions = useMemo(
    () =>
      [...sessions].sort(
        (a, b) =>
          Number(b.acceptsEnrolment !== false) - Number(a.acceptsEnrolment !== false) ||
          String(a.scheduledStartAt ?? '').localeCompare(String(b.scheduledStartAt ?? '')),
      ),
    [sessions],
  );
  const openSessionCount = sessions.filter((x) => x.acceptsEnrolment !== false).length;

  const listKey = ['hr', 'orientation-enrollments', scope, programId, completionStatus];
  const ENDED_BY_HR: string[] = ['Withdrawn', 'Cancelled', 'NoShow'];
  const { data: enrollments = [], isLoading } = useQuery({
    queryKey: listKey,
    queryFn: () => {
      if (scope === 'overdue') return employeeOrientationService.getOverdue();
      if (scope === 'due-soon') return employeeOrientationService.getDueSoon(14);
      if (scope === 'status')
        return employeeOrientationService.getByCompletionStatus(completionStatus);
      return programId
        ? employeeOrientationService.getByProgram(programId)
        : Promise.resolve([] as EmployeeOrientationSummary[]);
    },
    enabled: scope !== 'programme' || !!programId,
  });

  const term = search.trim().toLowerCase();
  const filtered = term
    ? enrollments.filter(
        (e) =>
          (e.employeeName ?? '').toLowerCase().includes(term) ||
          (e.employeeNumber ?? '').toLowerCase().includes(term) ||
          (e.programTitle ?? '').toLowerCase().includes(term),
      )
    : enrollments;

  const tiles = useMemo(() => {
    const total = enrollments.length;
    const completed = enrollments.filter((e) => e.completionStatus === 'Completed').length;
    const overdue = enrollments.filter((e) => e.completionStatus === 'Overdue').length;
    const holding = enrollments.filter((e) =>
      OCCUPYING_ENROLLMENT_STATUSES.includes(e.enrollmentStatus),
    ).length;
    return [
      { label: 'Enrollments', value: total, icon: Users },
      {
        label: 'Holding a seat',
        value: holding,
        hint: 'Withdrawn and cancelled are excluded',
      },
      {
        label: 'Completed',
        value: completed,
        hint: total > 0 ? `${Math.round((completed / total) * 100)}% of the list` : undefined,
        tone: 'success' as const,
      },
      {
        label: 'Overdue',
        value: overdue,
        tone: overdue > 0 ? ('danger' as const) : ('default' as const),
      },
    ];
  }, [enrollments]);

  const addPicked = (id: string | null, label: string | null) => {
    if (!id) return;
    setPicked((prev) => (prev.some((p) => p.id === id) ? prev : [...prev, { id, label: label ?? id }]));
    // The picker collapses to a chip once it holds a value; remounting it returns it to a search box
    // so the next person can be added.
    setPickerKey((k) => k + 1);
  };

  const resetEnrolDialog = () => {
    setEnrolProgramId('');
    setEnrolSessionId(ALL);
    setEnrolSource('HrAssigned');
    setPicked([]);
    setPickerKey((k) => k + 1);
  };

  const runBulkEnrol = async () => {
    if (!enrolProgramId || picked.length === 0) return;
    setEnrolling(true);
    setSkippedNote(null);
    try {
      const created = await employeeOrientationService.bulkEnroll({
        programId: enrolProgramId,
        sessionId: enrolSessionId === ALL ? null : enrolSessionId,
        employeeIds: picked.map((p) => p.id),
        enrollmentSource: enrolSource,
      });

      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-enrollments'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-sessions'] });

      // The endpoint silently skips anyone already enrolled, so the counts are compared rather than
      // reporting the request as wholly successful.
      const skipped = picked.length - created.length;
      const waitlisted = created.filter((c) => c.enrollmentStatus === 'Waitlisted').length;

      toast({
        title: `${created.length} of ${picked.length} enrolled`,
        description:
          skipped > 0
            ? `${skipped} were skipped — already on the programme's current cycle, or they have completed a programme that does not recur.`
            : 'Everyone selected was enrolled.',
      });

      if (skipped > 0 || waitlisted > 0) {
        setSkippedNote(
          [
            skipped > 0
              ? `${skipped} skipped — already on the current cycle, or completed a programme that does not recur.`
              : null,
            waitlisted > 0
              ? `${waitlisted} placed on the waitlist — the session is full.`
              : null,
          ]
            .filter(Boolean)
            .join(' '),
        );
      }

      setEnrolOpen(false);
      resetEnrolDialog();
      // Land the user on what they just changed.
      setScope('programme');
      setProgramId(enrolProgramId);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to enrol.',
        variant: 'destructive',
      });
    } finally {
      setEnrolling(false);
    }
  };

  // The server decides by each person's LATEST enrolment on a programme, so the actions are offered
  // on that row only — an old withdrawn row beside a newer enrolment would only be refused.
  const latestRowIds = useMemo(() => {
    const latest = new Map<string, EmployeeOrientationSummary>();
    for (const e of enrollments) {
      const key = `${e.employeeId}|${e.programId}`;
      const seen = latest.get(key);
      if (!seen || String(e.enrolledAt) > String(seen.enrolledAt)) latest.set(key, e);
    }
    return new Set(Array.from(latest.values()).map((e) => e.id));
  }, [enrollments]);

  const runAgain = async () => {
    if (!againTarget) return false;
    setBusy(true);
    try {
      await employeeOrientationService.enroll({
        programId: againTarget.row.programId,
        employeeId: againTarget.row.employeeId,
        sessionId: null,
        enrollmentSource: 'HrAssigned',
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-enrollments'] });
      toast({
        title: againTarget.kind === 'reenrol' ? 'Re-enrolled' : 'Next cycle opened',
        description: 'The earlier enrolment stays on the record.',
      });
      setAgainTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Could not enrol',
        description: error?.message || 'Failed to enrol.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const runCertify = async () => {
    if (!certifyTarget) return false;
    setBusy(true);
    try {
      const issued = await employeeOrientationService.issueCertificate(certifyTarget.id, {
        employeeOrientationId: certifyTarget.id,
        reissue: !!certifyTarget.certificateIssued,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-enrollments'] });
      toast({
        title: certifyTarget.certificateIssued ? 'Certificate reissued' : 'Certificate issued',
        description: `${issued.certificateNumber} — ${certifyTarget.employeeName ?? 'the participant'} has been told.`,
      });
      setCertifyTarget(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Could not issue the certificate',
        description: error?.message || 'Failed to issue.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const runWithdraw = async () => {
    if (!withdrawTarget) return false;
    if (!withdrawReason.trim()) {
      toast({ title: 'A reason is required', variant: 'destructive' });
      return false;
    }
    setBusy(true);
    try {
      await employeeOrientationService.withdraw(withdrawTarget.id, {
        enrollmentId: withdrawTarget.id,
        withdrawalReason: withdrawReason.trim(),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-enrollments'] });
      toast({ title: 'Withdrawn', description: 'Their seat has been freed.' });
      setWithdrawTarget(null);
      setWithdrawReason('');
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to withdraw.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const emptyMessage =
    scope === 'programme' && !programId
      ? 'Choose a programme to see who is on it.'
      : scope === 'overdue'
        ? 'Nothing is overdue.'
        : scope === 'due-soon'
          ? 'Nothing falls due in the next fortnight.'
          : 'No enrollments match.';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Orientation Enrollments"
        description="Who is on which programme, how far through they are, and what has fallen due."
        backHref="/hr/orientation"
        actions={
          <Button onClick={() => setEnrolOpen(true)}>
            <UserPlus className="mr-2 h-4 w-4" />
            Enrol participants
          </Button>
        }
      />

      {skippedNote && (
        <Alert>
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Not everyone was enrolled as requested</AlertTitle>
          <AlertDescription>{skippedNote}</AlertDescription>
        </Alert>
      )}

      <MetricTiles tiles={tiles} />

      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <Select value={scope} onValueChange={(v) => setScope(v as Scope)}>
            <SelectTrigger className="w-[190px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="programme">By programme</SelectItem>
              <SelectItem value="status">By completion status</SelectItem>
              <SelectItem value="overdue">Overdue</SelectItem>
              <SelectItem value="due-soon">Due in 14 days</SelectItem>
            </SelectContent>
          </Select>

          {scope === 'programme' && (
            <Select value={programId} onValueChange={setProgramId}>
              <SelectTrigger className="w-[280px]">
                <SelectValue placeholder="Choose a programme…" />
              </SelectTrigger>
              <SelectContent>
                {programs.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.programCode} — {p.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}

          {scope === 'status' && (
            <Select
              value={completionStatus}
              onValueChange={(v) => setCompletionStatus(v as OrientationCompletionStatus)}
            >
              <SelectTrigger className="w-[220px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ORIENTATION_COMPLETION_STATUS_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}

          <div className="relative min-w-[220px] flex-1">
            <Search className="text-muted-foreground absolute left-2.5 top-2.5 h-4 w-4" />
            <Input
              placeholder="Search by name, number or programme…"
              className="pl-8"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <p className="text-muted-foreground p-6 text-sm">Loading enrollments…</p>
          ) : filtered.length === 0 ? (
            <EmptyState icon={Users} title="Nothing to show" description={emptyMessage} />
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Participant</TableHead>
                    <TableHead>Programme</TableHead>
                    <TableHead>Session</TableHead>
                    <TableHead>Enrollment</TableHead>
                    <TableHead>Completion</TableHead>
                    <TableHead className="w-[160px]">Progress</TableHead>
                    <TableHead>Due</TableHead>
                    <TableHead className="w-[60px]" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filtered.map((e) => (
                    <TableRow key={e.id}>
                      <TableCell className="font-medium">
                        {e.employeeName ?? '—'}
                        {e.employeeNumber && (
                          <div className="text-muted-foreground text-xs">{e.employeeNumber}</div>
                        )}
                      </TableCell>
                      <TableCell>
                        {e.programTitle ?? '—'}
                        {e.programCode && (
                          <div className="text-muted-foreground font-mono text-xs">
                            {e.programCode}
                          </div>
                        )}
                      </TableCell>
                      <TableCell>
                        {e.sessionId ? (
                          <Link
                            href={`/hr/orientation/sessions/${e.sessionId}`}
                            className="hover:underline"
                          >
                            {e.sessionTitle ?? 'Session'}
                          </Link>
                        ) : (
                          <span className="text-muted-foreground">Self-paced</span>
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={e.enrollmentStatus} />
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={e.completionStatus} />
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Progress value={e.progressPercentage} className="h-2 w-20" />
                          <span className="text-muted-foreground text-xs tabular-nums">
                            {e.progressPercentage}%
                          </span>
                        </div>
                        {e.finalScore != null && (
                          <div className="text-muted-foreground mt-0.5 text-xs">
                            Scored {e.finalScore}%{' '}
                            {e.isPassed ? (
                              <Badge variant="default" className="ml-1 text-[10px]">
                                Passed
                              </Badge>
                            ) : (
                              <Badge variant="destructive" className="ml-1 text-[10px]">
                                Failed
                              </Badge>
                            )}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {e.nextDueDate ? new Date(e.nextDueDate).toLocaleDateString() : '—'}
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon" className="h-8 w-8">
                              <span className="sr-only">Actions</span>⋯
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem asChild>
                              <Link href={`/me/orientation/${e.id}`}>View progress</Link>
                            </DropdownMenuItem>
                            {latestRowIds.has(e.id) && ENDED_BY_HR.includes(e.enrollmentStatus) && (
                              <DropdownMenuItem
                                onClick={() => setAgainTarget({ row: e, kind: 'reenrol' })}
                              >
                                <RotateCcw className="mr-2 h-4 w-4" />
                                Re-enrol
                              </DropdownMenuItem>
                            )}
                            {latestRowIds.has(e.id) &&
                              e.completionStatus === 'Completed' &&
                              !ENDED_BY_HR.includes(e.enrollmentStatus) &&
                              recurringProgramIds.has(e.programId) && (
                                <DropdownMenuItem
                                  onClick={() => setAgainTarget({ row: e, kind: 'nextCycle' })}
                                >
                                  <RotateCcw className="mr-2 h-4 w-4" />
                                  Open the next cycle now
                                </DropdownMenuItem>
                              )}
                            {e.completionStatus === 'Completed' &&
                              certificatedProgramIds.has(e.programId) && (
                                <DropdownMenuItem onClick={() => setCertifyTarget(e)}>
                                  <Award className="mr-2 h-4 w-4" />
                                  {e.certificateIssued ? 'Reissue certificate' : 'Issue certificate'}
                                </DropdownMenuItem>
                              )}
                            {e.enrollmentStatus !== 'Withdrawn' &&
                              e.enrollmentStatus !== 'Cancelled' && (
                                <DropdownMenuItem
                                  className="text-red-600"
                                  onClick={() => setWithdrawTarget(e)}
                                >
                                  <UserMinus className="mr-2 h-4 w-4" />
                                  Withdraw
                                </DropdownMenuItem>
                              )}
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog
        open={enrolOpen}
        onOpenChange={(o) => {
          setEnrolOpen(o);
          if (!o) resetEnrolDialog();
        }}
      >
        <DialogContent className="sm:max-w-[620px]">
          <DialogHeader>
            <DialogTitle>Enrol participants</DialogTitle>
            <DialogDescription>
              Anyone already on the programme and not finished is skipped, as is anyone who has
              completed a programme that does not recur. Someone HR withdrew can be enrolled again,
              and on a recurring programme a completed person starts their next cycle. If the chosen
              session is full and allows a waitlist, they are queued rather than confirmed.
            </DialogDescription>
          </DialogHeader>

          <div className="max-h-[60vh] space-y-4 overflow-y-auto py-2">
            <div className="space-y-2">
              <Label>Programme</Label>
              <Select
                value={enrolProgramId}
                onValueChange={(v) => {
                  setEnrolProgramId(v);
                  setEnrolSessionId(ALL);
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Choose a programme…" />
                </SelectTrigger>
                <SelectContent>
                  {enrollablePrograms.map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.programCode} — {p.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {hiddenProgramCount > 0 && (
                <p className="text-muted-foreground text-xs">
                  {hiddenProgramCount} programme{hiddenProgramCount === 1 ? '' : 's'} that{' '}
                  {hiddenProgramCount === 1 ? 'is' : 'are'} a draft, retired or outside{' '}
                  {hiddenProgramCount === 1 ? 'its' : 'their'} effective dates{' '}
                  {hiddenProgramCount === 1 ? 'is' : 'are'} not offered — they take no enrolments.
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label>Session</Label>
              <Select
                value={enrolSessionId}
                onValueChange={setEnrolSessionId}
                disabled={!enrolProgramId}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>
                    {enrolSelfPaced ? 'Self-paced — no session' : 'No session (enrol without one)'}
                  </SelectItem>
                  {sortedSessions.map((s) => {
                    const open = s.acceptsEnrolment !== false;
                    return (
                      <SelectItem key={s.id} value={s.id} disabled={!open}>
                        {s.title}
                        {s.scheduledStartAt
                          ? ` · ${new Date(s.scheduledStartAt).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })}`
                          : ''}
                        {open
                          ? s.maxParticipants
                            ? ` (${Math.max(0, s.maxParticipants - s.enrolledCount)} seats left)`
                            : ' (uncapped)'
                          : ` — not open: ${s.closedBecause ?? s.status}`}
                      </SelectItem>
                    );
                  })}
                </SelectContent>
              </Select>
              {enrolProgramId && (
                <p className="text-muted-foreground text-xs">
                  {sessionsLoading ? (
                    'Loading this programme’s sessions…'
                  ) : sessionsFailed ? (
                    (sessionsError as { status?: number } | null)?.status === 403 ? (
                      'You do not have permission to see this programme’s sessions. People can still be enrolled without one.'
                    ) : (
                      `Its sessions could not be loaded (${(sessionsError as Error | null)?.message ?? 'an error'}). People can still be enrolled without one.`
                    )
                  ) : sessions.length === 0 ? (
                    enrolSelfPaced ? (
                      'This programme is self-paced, so it has no sessions — people work through it on their own.'
                    ) : (
                      <>
                        No sessions are scheduled for this programme yet.{' '}
                        <Link
                          href={`/hr/orientation/sessions?schedule=${enrolProgramId}`}
                          className="underline underline-offset-2"
                        >
                          Schedule one
                        </Link>
                        , or enrol without a session.
                      </>
                    )
                  ) : openSessionCount === 0 ? (
                    'None of its sessions is open for enrolment — the closed ones are listed for reference. Enrol without a session, or open one from the sessions screen.'
                  ) : null}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label>How they came to be enrolled</Label>
              <Select
                value={enrolSource}
                onValueChange={(v) => setEnrolSource(v as OrientationEnrollmentSource)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ORIENTATION_ENROLLMENT_SOURCE_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label>Participants ({picked.length})</Label>
              <EmployeePicker key={pickerKey} value={null} onChange={addPicked} />
              {picked.length > 0 && (
                <div className="flex flex-wrap gap-2 pt-1">
                  {picked.map((p) => (
                    <Badge key={p.id} variant="secondary" className="gap-1 py-1 pl-2 pr-1">
                      {p.label}
                      <button
                        type="button"
                        aria-label={`Remove ${p.label}`}
                        className="hover:bg-muted rounded-sm p-0.5"
                        onClick={() =>
                          setPicked((prev) => prev.filter((x) => x.id !== p.id))
                        }
                      >
                        <X className="h-3 w-3" />
                      </button>
                    </Badge>
                  ))}
                </div>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEnrolOpen(false)} disabled={enrolling}>
              Cancel
            </Button>
            <Button
              onClick={runBulkEnrol}
              disabled={enrolling || !enrolProgramId || picked.length === 0}
            >
              {enrolling && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              <Plus className="mr-2 h-4 w-4" />
              Enrol {picked.length || ''}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={certifyTarget !== null}
        onOpenChange={(o) => !o && setCertifyTarget(null)}
        title={
          certifyTarget?.certificateIssued
            ? `Reissue ${certifyTarget.employeeName ?? 'this person'}'s certificate?`
            : `Issue ${certifyTarget?.employeeName ?? 'this person'} a certificate?`
        }
        description={
          certifyTarget?.certificateIssued
            ? `A new certificate for ${certifyTarget.programTitle ?? 'the programme'} replaces ${certifyTarget.certificateSerialNumber ?? 'the current one'}, which is marked reissued. They are told, in-app and by email.`
            : `A certificate for ${certifyTarget?.programTitle ?? 'the programme'}, with the next serial and the programme's validity. They are told, in-app and by email.`
        }
        confirmText={certifyTarget?.certificateIssued ? 'Reissue' : 'Issue certificate'}
        isLoading={busy}
        onConfirm={runCertify}
      />

      <ConfirmationDialog
        open={againTarget !== null}
        onOpenChange={(o) => !o && setAgainTarget(null)}
        title={
          againTarget?.kind === 'nextCycle'
            ? `Open the next cycle for ${againTarget.row.employeeName ?? 'this person'} now?`
            : `Re-enrol ${againTarget?.row.employeeName ?? 'this person'}?`
        }
        description={
          againTarget?.kind === 'nextCycle'
            ? `A new cycle of ${againTarget.row.programTitle ?? 'the programme'} starts today instead of waiting for the nightly renewal. The completed cycle stays on the record.`
            : `A new, self-paced enrolment on ${againTarget?.row.programTitle ?? 'the programme'}. The ${(againTarget?.row.enrollmentStatus ?? 'ended').toLowerCase()} enrolment stays on the record — and no rule would ever have done this by itself.`
        }
        confirmText={againTarget?.kind === 'nextCycle' ? 'Open the next cycle' : 'Re-enrol'}
        isLoading={busy}
        onConfirm={runAgain}
      />

      <Dialog
        open={withdrawTarget !== null}
        onOpenChange={(o) => {
          if (!o) {
            setWithdrawTarget(null);
            setWithdrawReason('');
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Withdraw from this programme</DialogTitle>
            <DialogDescription>
              {withdrawTarget
                ? `${withdrawTarget.employeeName ?? 'This participant'} keeps their record, but their seat is freed and their enrollment closed.`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2 py-2">
            <Label htmlFor="withdraw-reason">Reason</Label>
            <Textarea
              id="withdraw-reason"
              rows={3}
              value={withdrawReason}
              onChange={(e) => setWithdrawReason(e.target.value)}
              placeholder="Why are they being withdrawn?"
            />
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setWithdrawTarget(null)}
              disabled={busy}
            >
              Keep them on
            </Button>
            <Button variant="destructive" onClick={runWithdraw} disabled={busy}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Withdraw
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
