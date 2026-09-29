'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import {
  CheckCircle2,
  ClipboardCheck,
  Gavel,
  Lock,
  PlayCircle,
  Scale,
  SlidersHorizontal,
  TriangleAlert,
  Users,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime } from '@/lib/hr/attendance-format';
import { calibrationSessionService } from '@/services/hr/calibration.service';
import type { CalibrationCriterion, CalibrationMatrixRow } from '@/types/hr/calibration';

/**
 * One calibration session: the grid, the panel, and the decisions it took.
 *
 * **The lifecycle is four steps, and the last two are different decisions.** Opening links every
 * appraisal in scope to the session so their phase reads "calibration in progress". Completing
 * closes the room. *Committing* is separate and irreversible: it writes the agreed ratings onto
 * the appraisals at the calibration step (their manager has submitted) and lifts the gate on them —
 * including the people the panel discussed and left alone, who are calibrated too and would
 * otherwise sit blocked. Appraisals not at that step are left alone and listed in the commit's
 * result with the reason (performance closure A4).
 *
 * ⚠ Adjustments are only accepted while the session is open. Once it is completed the grid is
 * read-only, and the only remaining action is to commit it.
 *
 * **The overall score is the headline; individual criteria are behind a disclosure.** Restating the
 * final number is what a panel does in practice, so that is the default action. Per-criterion
 * adjustment is available underneath for the cases where the disagreement is about one specific
 * thing — each criterion is its own adjustment record, and the rationale is shared because it is
 * one panel decision.
 */
export default function CalibrationSessionDetailPage() {
  const params = useParams<{ id: string }>();
  const sessionId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [adjustRow, setAdjustRow] = useState<CalibrationMatrixRow | null>(null);
  const [criterionScores, setCriterionScores] = useState<Record<string, string>>({});
  const [adjustScore, setAdjustScore] = useState('');
  const [adjustRationale, setAdjustRationale] = useState('');
  const [completeOpen, setCompleteOpen] = useState(false);
  const [meetingNotes, setMeetingNotes] = useState('');
  const [commitOpen, setCommitOpen] = useState(false);
  const [addPanelist, setAddPanelist] = useState('');
  const [panelistRole, setPanelistRole] = useState('Manager');

  const session = useQuery({
    queryKey: ['hr', 'calibration-session', sessionId],
    queryFn: () => calibrationSessionService.getById(sessionId),
    enabled: !!sessionId,
    retry: false,
  });

  const matrix = useQuery({
    queryKey: ['hr', 'calibration-matrix', sessionId],
    queryFn: () => calibrationSessionService.getMatrix(sessionId),
    enabled: !!sessionId,
  });

  const participants = useQuery({
    queryKey: ['hr', 'calibration-participants', sessionId],
    queryFn: () => calibrationSessionService.getParticipants(sessionId),
    enabled: !!sessionId,
  });

  // Only fetched once a row is open for calibration — the panel does not need every appraisal's
  // criteria to read the grid.
  const criteria = useQuery({
    queryKey: ['hr', 'calibration-criteria', sessionId, adjustRow?.appraisalId],
    queryFn: () =>
      calibrationSessionService.getAppraisalCriteria(sessionId, adjustRow?.appraisalId ?? ''),
    enabled: !!adjustRow?.appraisalId,
  });

  const adjustments = useQuery({
    queryKey: ['hr', 'calibration-adjustments', sessionId],
    queryFn: () => calibrationSessionService.getAdjustments(sessionId),
    enabled: !!sessionId,
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'calibration-session', sessionId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'calibration-matrix', sessionId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'calibration-adjustments', sessionId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'calibration-sessions'] });
  };

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e.message, variant: 'destructive' });

  const open = useMutation({
    mutationFn: () => calibrationSessionService.open(sessionId),
    onSuccess: (s) => {
      toast({
        title: 'Session opened',
        description: `You are the facilitator. ${s.sessionName} now covers its scope.`,
      });
      refresh();
    },
    onError: fail('Could not open the session'),
  });

  const start = useMutation({
    mutationFn: () => calibrationSessionService.start(sessionId),
    onSuccess: () => {
      toast({ title: 'Session started' });
      refresh();
    },
    onError: fail('Could not start the session'),
  });

  const complete = useMutation({
    mutationFn: () =>
      calibrationSessionService.complete(sessionId, { meetingNotes: meetingNotes.trim() || null }),
    onSuccess: () => {
      toast({
        title: 'Session closed',
        description: 'The panel has been notified. Commit the ratings when you are ready.',
      });
      setCompleteOpen(false);
      refresh();
    },
    onError: fail('Could not complete the session'),
  });

  const commit = useMutation({
    mutationFn: () => calibrationSessionService.applyAdjustments(sessionId),
    onSuccess: (result) => {
      toast({
        title: 'Ratings committed',
        description:
          `${result.appraisalsCalibrated} appraisal(s) calibrated, ` +
          `${result.scoresChanged} score(s) changed from ${result.adjustmentsApplied} adjustment(s).` +
          (result.appraisalsSkipped > 0
            ? ` ${result.appraisalsSkipped} left as they were: ` +
              result.skipped
                .slice(0, 3)
                .map((s) => `${s.employeeName ?? 'an appraisal'} (${s.reason})`)
                .join('; ') +
              (result.appraisalsSkipped > 3 ? '; …' : '.')
            : ''),
      });
      setCommitOpen(false);
      refresh();
    },
    onError: fail('Could not commit the session'),
  });

  const saveAdjustment = useMutation({
    // The row is passed in rather than read off state, so the call cannot be made without one.
    mutationFn: (row: CalibrationMatrixRow) => {
      // The panel is restating the final number, so the original recorded against the decision
      // is whatever the appraisal stood at going in.
      const existing = row.adjustments.find((a) => !a.templateItemId);
      const payload = {
        performanceAppraisalId: row.appraisalId,
        templateItemId: null,
        originalScore: row.preCalibrationScore ?? null,
        adjustedScore: Number(adjustScore),
        rationale: adjustRationale.trim() || null,
      };
      return existing
        ? calibrationSessionService.updateAdjustment(sessionId, existing.id, {
            ...payload,
            id: existing.id,
          })
        : calibrationSessionService.addAdjustment(sessionId, payload);
    },
    onSuccess: () => {
      toast({ title: 'Adjustment recorded' });
      setAdjustRow(null);
      setAdjustScore('');
      setAdjustRationale('');
      refresh();
    },
    onError: fail('Could not record the adjustment'),
  });

  /**
   * One criterion's adjustment. Separate from the overall-score mutation because they are separate
   * records server-side: `templateItemId` null is the overall restatement, non-null is the
   * criterion. The rationale is shared — it is the same panel decision.
   */
  const saveCriterionAdjustment = useMutation({
    mutationFn: ({
      row,
      criterion,
    }: {
      row: CalibrationMatrixRow;
      criterion: CalibrationCriterion;
    }) => {
      const payload = {
        performanceAppraisalId: row.appraisalId,
        templateItemId: criterion.templateItemId,
        // A KPI is scored by its achievement, so that is the figure being moved.
        originalScore: (criterion.isKpi ? criterion.managerAchievementPercent : criterion.managerScore) ?? null,
        adjustedScore: Number(criterionScores[criterion.templateItemId]),
        rationale: adjustRationale.trim() || null,
      };
      return criterion.adjustmentId
        ? calibrationSessionService.updateAdjustment(sessionId, criterion.adjustmentId, {
            ...payload,
            id: criterion.adjustmentId,
          })
        : calibrationSessionService.addAdjustment(sessionId, payload);
    },
    onSuccess: () => {
      toast({ title: 'Criterion adjustment recorded' });
      criteria.refetch();
      refresh();
    },
    onError: fail('Could not record the criterion adjustment'),
  });

  const clearAdjustment = useMutation({
    mutationFn: (adjustmentId: string) =>
      calibrationSessionService.removeAdjustment(sessionId, adjustmentId),
    onSuccess: () => {
      toast({ title: 'Adjustment removed' });
      refresh();
    },
    onError: fail('Could not remove the adjustment'),
  });

  const addParticipant = useMutation({
    mutationFn: () =>
      calibrationSessionService.addParticipant(sessionId, {
        calibrationSessionId: sessionId,
        employeeId: addPanelist,
        role: panelistRole.trim() || 'Manager',
        attended: false,
      }),
    onSuccess: () => {
      toast({ title: 'Added to the panel' });
      setAddPanelist('');
      queryClient.invalidateQueries({ queryKey: ['hr', 'calibration-participants', sessionId] });
    },
    onError: fail('Could not add the panelist'),
  });

  const removeParticipant = useMutation({
    mutationFn: (participantId: string) =>
      calibrationSessionService.removeParticipant(sessionId, participantId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'calibration-participants', sessionId] });
    },
    onError: fail('Could not remove the panelist'),
  });

  const markAttendance = useMutation({
    mutationFn: ({ id, attended }: { id: string; attended: boolean }) =>
      calibrationSessionService.recordAttendance(sessionId, id, attended),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'calibration-participants', sessionId] });
    },
    onError: fail('Could not record attendance'),
  });

  const data = session.data;
  const grid = matrix.data;

  // Only appraisals at the calibration step are committed; the rest are reported as skipped.
  const uncommitted = useMemo(
    () => (grid?.rows ?? []).filter((r) => !r.isCalibrated && r.appraisalStatus === 'Governance').length,
    [grid],
  );

  // What a commit will do, for its confirmation: the server calibrates what is in governance (and a
  // final appraisal only when this session adjusted it), and lists everything else as skipped.
  const commitCounts = useMemo(() => {
    const rows = grid?.rows ?? [];
    const atStep = rows.filter((r) => r.appraisalStatus === 'Governance');
    return {
      atStep: atStep.length,
      leftAlone: atStep.filter((r) => (r.adjustments ?? []).length === 0).length,
      notAtStep: rows.length - atStep.length,
    };
  }, [grid]);

  if (session.isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (session.isError || !data) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Calibration session" backHref="/hr/performance/calibration" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not load this session"
              description={(session.error as Error)?.message ?? 'It may have been deleted.'}
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  const isOpen = data.status === 'InProgress';
  const isCompleted = data.status === 'Completed';
  const canAdjust = isOpen;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={data.sessionName}
        description={describeScope(data.organizationUnitName, data.organizationLevelName)}
        backHref="/hr/performance/calibration"
        actions={
          <div className="flex items-center gap-2">
            {data.status === 'Pending' && (
              <Button onClick={() => open.mutate()} disabled={open.isPending}>
                <PlayCircle className="mr-2 h-4 w-4" />
                {open.isPending ? 'Opening…' : 'Open session'}
              </Button>
            )}
            {isOpen && !data.startedDate && (
              <Button variant="outline" onClick={() => start.mutate()} disabled={start.isPending}>
                <Gavel className="mr-2 h-4 w-4" />
                Mark convened
              </Button>
            )}
            {isOpen && (
              <Button onClick={() => setCompleteOpen(true)}>
                <CheckCircle2 className="mr-2 h-4 w-4" />
                Close session
              </Button>
            )}
            {isCompleted && uncommitted > 0 && (
              <Button onClick={() => setCommitOpen(true)}>
                <Lock className="mr-2 h-4 w-4" />
                Commit ratings
              </Button>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={data.status} />
        {data.cycleCode && (
          <span className="text-sm text-muted-foreground">Cycle {data.cycleCode}</span>
        )}
        {data.scheduledDate && (
          <span className="text-sm text-muted-foreground">
            Scheduled {formatDate(data.scheduledDate)}
          </span>
        )}
        {data.facilitatedByName && (
          <span className="text-sm text-muted-foreground">
            Facilitated by {data.facilitatedByName}
          </span>
        )}
      </div>

      {data.status === 'Pending' && (
        <Alert>
          <Scale className="h-4 w-4" />
          <AlertTitle>Not open yet</AlertTitle>
          <AlertDescription>
            Add the panel first, then open the session. Opening records you as facilitator and
            links every appraisal in scope, so no ratings can be recorded before it.
          </AlertDescription>
        </Alert>
      )}

      {isCompleted && uncommitted > 0 && (
        <Alert>
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>Closed, but not committed</AlertTitle>
          <AlertDescription>
            {uncommitted} appraisal(s) in this session have not been through the calibration gate.
            Until you commit, none of them can reach HR review on a cycle that requires
            calibration — including the ones the panel agreed to leave as they are.
          </AlertDescription>
        </Alert>
      )}

      <MetricTiles
        tiles={[
          { label: 'In scope', value: grid?.totalEmployees ?? 0, icon: Users },
          {
            label: 'Adjusted',
            value: grid?.adjustedCount ?? 0,
            hint: 'Rows the panel has moved',
            icon: SlidersHorizontal,
          },
          {
            label: 'Calibrated',
            value: grid?.calibratedCount ?? 0,
            icon: ClipboardCheck,
            tone: (grid?.calibratedCount ?? 0) === (grid?.totalEmployees ?? 0) ? 'success' : 'default',
          },
          {
            label: 'Average score',
            value: grid?.averageScore != null ? grid.averageScore.toFixed(1) : '—',
            hint: 'As the grid currently stands',
          },
        ]}
      />

      <Tabs defaultValue="grid">
        <TabsList>
          <TabsTrigger value="grid">Grid ({grid?.totalEmployees ?? 0})</TabsTrigger>
          <TabsTrigger value="panel">Panel ({participants.data?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="decisions">Decisions ({adjustments.data?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="notes">Notes</TabsTrigger>
        </TabsList>

        {/* ── Grid ─────────────────────────────────────────────────────────── */}
        <TabsContent value="grid" className="mt-4">
          <Card>
            <CardContent className="p-0">
              {matrix.isLoading ? (
                <div className="space-y-2 p-4">
                  {[0, 1, 2].map((i) => (
                    <Skeleton key={i} className="h-12 w-full" />
                  ))}
                </div>
              ) : (grid?.rows.length ?? 0) === 0 ? (
                <EmptyState
                  icon={Scale}
                  title="Nobody in scope"
                  description="No appraisal in this cycle falls inside the session's organization scope. Check the scope, or generate the cycle's appraisals first."
                />
              ) : (
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Employee</TableHead>
                        <TableHead>Manager</TableHead>
                        <TableHead className="text-right">Manager proposed</TableHead>
                        <TableHead className="text-right">Pre-calibration</TableHead>
                        <TableHead className="text-right">Calibrated</TableHead>
                        <TableHead className="text-right">Δ</TableHead>
                        <TableHead>Gate</TableHead>
                        <TableHead className="w-28" />
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {(grid?.rows ?? []).map((row) => (
                        <TableRow key={row.appraisalId}>
                          <TableCell>
                            <div className="font-medium">{row.employeeName}</div>
                            <div className="text-xs text-muted-foreground">
                              {row.employeeNumber}
                              {row.positionName ? ` · ${row.positionName}` : ''}
                            </div>
                            {row.departmentName && (
                              <div className="text-xs text-muted-foreground">
                                {row.departmentName}
                              </div>
                            )}
                          </TableCell>
                          <TableCell className="text-sm text-muted-foreground">
                            {row.managerName ?? '—'}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">
                            {score(row.managerProposedScore)}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">
                            {score(row.preCalibrationScore)}
                          </TableCell>
                          <TableCell className="text-right tabular-nums font-medium">
                            {score(row.calibratedScore)}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">
                            <Delta value={row.scoreAdjustment} />
                          </TableCell>
                          <TableCell>
                            {row.isCalibrated ? (
                              <StatusBadge status="Calibrated" />
                            ) : (
                              <span className="text-xs text-muted-foreground">Not yet</span>
                            )}
                          </TableCell>
                          <TableCell className="text-right">
                            {canAdjust ? (
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => {
                                  setAdjustRow(row);
                                  setAdjustScore(
                                    row.calibratedScore != null
                                      ? String(row.calibratedScore)
                                      : row.preCalibrationScore != null
                                        ? String(row.preCalibrationScore)
                                        : '',
                                  );
                                  setAdjustRationale(row.adjustmentRationale ?? '');
                                }}
                              >
                                Adjust
                              </Button>
                            ) : (
                              <Button variant="ghost" size="sm" asChild>
                                <Link href={`/hr/performance/hr-review/${row.appraisalId}`}>
                                  View
                                </Link>
                              </Button>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Panel ────────────────────────────────────────────────────────── */}
        <TabsContent value="panel" className="mt-4 space-y-4">
          {!isCompleted && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Add a panelist</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-wrap items-end gap-4">
                <div className="min-w-[260px] flex-1 space-y-2">
                  <Label htmlFor="panelist">Employee</Label>
                  <EmployeePicker
                    value={addPanelist || null}
                    onChange={(employeeId) => setAddPanelist(employeeId ?? '')}
                    placeholder="Search by name or number…"
                  />
                </div>
                <div className="w-40 space-y-2">
                  <Label htmlFor="panelistRole">Role in the room</Label>
                  <Input
                    id="panelistRole"
                    value={panelistRole}
                    onChange={(e) => setPanelistRole(e.target.value)}
                    placeholder="Manager"
                  />
                </div>
                <Button
                  onClick={() => addParticipant.mutate()}
                  disabled={!addPanelist || addParticipant.isPending}
                >
                  Add
                </Button>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardContent className="p-0">
              {(participants.data?.length ?? 0) === 0 ? (
                <EmptyState
                  icon={Users}
                  title="No panel yet"
                  description="Add the managers and HR staff who will sit on this calibration."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Panelist</TableHead>
                      <TableHead>Role</TableHead>
                      <TableHead>Attended</TableHead>
                      <TableHead className="w-24" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(participants.data ?? []).map((p) => (
                      <TableRow key={p.id}>
                        <TableCell className="font-medium">{p.employeeName}</TableCell>
                        <TableCell className="text-sm text-muted-foreground">{p.role}</TableCell>
                        <TableCell>
                          <Checkbox
                            checked={p.attended}
                            disabled={isCompleted}
                            onCheckedChange={(checked) =>
                              markAttendance.mutate({ id: p.id, attended: checked === true })
                            }
                            aria-label={`Attendance for ${p.employeeName}`}
                          />
                        </TableCell>
                        <TableCell className="text-right">
                          {!isCompleted && (
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => removeParticipant.mutate(p.id)}
                            >
                              Remove
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

        {/* ── Decisions ────────────────────────────────────────────────────── */}
        <TabsContent value="decisions" className="mt-4">
          <Card>
            <CardContent className="p-0">
              {(adjustments.data?.length ?? 0) === 0 ? (
                <EmptyState
                  icon={SlidersHorizontal}
                  title="No adjustments recorded"
                  description="A session with no adjustments is still meaningful: committing it confirms every rating at the calibration step as it stands."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Applies to</TableHead>
                      <TableHead className="text-right">From</TableHead>
                      <TableHead className="text-right">To</TableHead>
                      <TableHead>Rationale</TableHead>
                      <TableHead>Recorded</TableHead>
                      <TableHead className="w-24" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(adjustments.data ?? []).map((a) => (
                      <TableRow key={a.id}>
                        <TableCell>
                          <div className="font-medium">{a.employeeName ?? '—'}</div>
                          <div className="text-xs text-muted-foreground">{a.appraisalNumber}</div>
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {a.templateItemId ? (a.templateItemName ?? 'One criterion') : 'Overall score'}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {score(a.originalScore)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums font-medium">
                          {score(a.adjustedScore)}
                        </TableCell>
                        <TableCell className="max-w-xs text-sm text-muted-foreground">
                          {a.rationale ?? '—'}
                        </TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {formatDateTime(a.adjustmentDate)}
                          <div>{a.adjustedByName}</div>
                        </TableCell>
                        <TableCell className="text-right">
                          {canAdjust && (
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => clearAdjustment.mutate(a.id)}
                            >
                              Remove
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

        {/* ── Notes ────────────────────────────────────────────────────────── */}
        <TabsContent value="notes" className="mt-4 space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Agenda</CardTitle>
            </CardHeader>
            <CardContent className="whitespace-pre-wrap text-sm text-muted-foreground">
              {data.agenda || 'No agenda recorded.'}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Meeting notes</CardTitle>
            </CardHeader>
            <CardContent className="whitespace-pre-wrap text-sm text-muted-foreground">
              {data.meetingNotes || 'Recorded when the session is closed.'}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* ── Adjust dialog ──────────────────────────────────────────────────── */}
      <Dialog open={!!adjustRow} onOpenChange={(o) => !o && setAdjustRow(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Calibrate {adjustRow?.employeeName}</DialogTitle>
            <DialogDescription>
              Restates the overall score. Nothing changes on the appraisal until the session is
              committed.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <div className="text-muted-foreground">Manager proposed</div>
                <div className="tabular-nums">{score(adjustRow?.managerProposedScore)}</div>
              </div>
              <div>
                <div className="text-muted-foreground">Pre-calibration</div>
                <div className="tabular-nums">{score(adjustRow?.preCalibrationScore)}</div>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="adjustedScore">Calibrated score</Label>
              <Input
                id="adjustedScore"
                type="number"
                min={0}
                max={100}
                step="0.01"
                value={adjustScore}
                onChange={(e) => setAdjustScore(e.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="rationale">Rationale</Label>
              <Textarea
                id="rationale"
                rows={3}
                value={adjustRationale}
                onChange={(e) => setAdjustRationale(e.target.value)}
                placeholder="Why the panel moved this rating. Kept as the audit trail."
              />
            </div>

            {/* Per-criterion detail. Optional: a panel that only restates the final number never
                opens this, and each criterion is its own adjustment record. */}
            {(criteria.data ?? []).length > 0 && (
              <details className="rounded-lg border p-3">
                <summary className="cursor-pointer text-sm font-medium">
                  Adjust individual criteria ({criteria.data?.length})
                </summary>
                <p className="mt-2 text-xs text-muted-foreground">
                  Each criterion is recorded separately from the overall score above. Leave one
                  blank to leave it alone.
                </p>
                <div className="mt-3 space-y-3">
                  {(criteria.data ?? []).map((c) => (
                    <div key={c.templateItemId} className="grid grid-cols-[1fr_auto_auto] items-end gap-2">
                      <div className="space-y-1">
                        <p className="text-sm">{c.templateItemName ?? 'Unnamed criterion'}</p>
                        {c.isKpi ? (
                          <p className="text-xs text-muted-foreground">
                            Weight {c.weightUsed} · KPI achievement{' '}
                            <span className="tabular-nums">{score(c.managerAchievementPercent)}%</span>
                            {c.managerActualValue != null && c.kpiTargetValue != null && (
                              <> (actual {c.managerActualValue} of target {c.kpiTargetValue})</>
                            )}
                            . Enter the achievement % the panel agrees — it overrides the actual.
                          </p>
                        ) : (
                          <p className="text-xs text-muted-foreground">
                            Weight {c.weightUsed} · manager scored{' '}
                            <span className="tabular-nums">{score(c.managerScore)}</span>
                            {c.scaleTop < 100 && <> · scale 0–{c.scaleTop}</>}
                          </p>
                        )}
                      </div>
                      <Input
                        className="w-24"
                        type="number"
                        min={0}
                        max={c.scaleTop}
                        step={1}
                        aria-label={`Calibrated ${c.isKpi ? 'achievement %' : 'score'} for ${c.templateItemName ?? 'criterion'}`}
                        value={criterionScores[c.templateItemId] ?? ''}
                        onChange={(e) =>
                          setCriterionScores((prev) => ({
                            ...prev,
                            [c.templateItemId]: e.target.value,
                          }))
                        }
                      />
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={
                          !isValidCriterionScore(criterionScores[c.templateItemId] ?? '', c.scaleTop) ||
                          saveCriterionAdjustment.isPending
                        }
                        onClick={() =>
                          adjustRow &&
                          saveCriterionAdjustment.mutate({ row: adjustRow, criterion: c })
                        }
                      >
                        {c.adjustmentId ? 'Update' : 'Record'}
                      </Button>
                    </div>
                  ))}
                </div>
              </details>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAdjustRow(null)}>
              Cancel
            </Button>
            <Button
              onClick={() => adjustRow && saveAdjustment.mutate(adjustRow)}
              disabled={!adjustRow || !isValidScore(adjustScore) || saveAdjustment.isPending}
            >
              {saveAdjustment.isPending ? 'Saving…' : 'Record'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Close dialog ───────────────────────────────────────────────────── */}
      <Dialog open={completeOpen} onOpenChange={setCompleteOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Close the session</DialogTitle>
            <DialogDescription>
              No further adjustments can be recorded afterwards. The panel is notified that the
              ratings are ready to commit.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="meetingNotes">Meeting notes</Label>
            <Textarea
              id="meetingNotes"
              rows={4}
              value={meetingNotes}
              onChange={(e) => setMeetingNotes(e.target.value)}
              placeholder="What the panel concluded."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleteOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => complete.mutate()} disabled={complete.isPending}>
              {complete.isPending ? 'Closing…' : 'Close session'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Commit dialog ──────────────────────────────────────────────────── */}
      <Dialog open={commitOpen} onOpenChange={setCommitOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Commit the ratings</DialogTitle>
            <DialogDescription>
              This writes the agreed scores onto the {commitCounts.atStep} appraisal(s) whose manager
              has submitted and marks them calibrated — including the {commitCounts.leftAlone} the
              panel left as they are.
              {commitCounts.notAtStep > 0 &&
                ` The other ${commitCounts.notAtStep} are not at the calibration step yet and are left alone; the result lists them.`}{' '}
              It cannot be undone.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCommitOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => commit.mutate()} disabled={commit.isPending}>
              {commit.isPending ? 'Committing…' : 'Commit'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function score(value?: number | null): string {
  return value != null ? Number(value).toFixed(1) : '—';
}

function isValidScore(raw: string): boolean {
  if (!raw.trim()) return false;
  const n = Number(raw);
  return Number.isFinite(n) && n >= 0 && n <= 100;
}

/** A criterion score is a whole number on the item's own scale — the server refuses anything else. */
function isValidCriterionScore(raw: string, scaleTop: number): boolean {
  if (!raw.trim()) return false;
  const n = Number(raw);
  return Number.isInteger(n) && n >= 0 && n <= scaleTop;
}

function Delta({ value }: { value?: number | null }) {
  if (value == null || value === 0) return <span className="text-muted-foreground">—</span>;
  const up = value > 0;
  return (
    <span className={up ? 'text-emerald-600 dark:text-emerald-500' : 'text-red-600 dark:text-red-500'}>
      {up ? '+' : ''}
      {Number(value).toFixed(1)}
    </span>
  );
}

function describeScope(unitName?: string | null, levelName?: string | null): string {
  if (unitName) return `${unitName}, and every unit beneath it.`;
  if (levelName) return `Everyone at ${levelName}.`;
  return 'Everyone in the cycle — no organizational narrowing.';
}
