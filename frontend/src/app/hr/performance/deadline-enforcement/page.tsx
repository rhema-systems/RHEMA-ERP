'use client';

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { AlertTriangle, FastForward, Info, ListChecks, ShieldAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { useToast } from '@/hooks/use-toast';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { deadlineEnforcementService } from '@/services/hr/outcomes.service';
import type {
  AppraisalSubStatus,
  AppraisalTransitionReport,
  DeadlineEnforcementResult,
} from '@/types/hr/outcomes';

/**
 * HR's manual override on a stalled appraisal pipeline. Both actions are audited against you.
 *
 * ⚠ **The cycle sweep does nothing unless `autoLockOnDeadline` is switched on** for that cycle's
 * settings profile. The endpoint honours the setting rather than ignoring it, so with the
 * setting off the run reports zero advanced however many appraisals are overdue — which reads
 * as "nothing was overdue" unless the screen says otherwise. It does.
 *
 * There is no background job. Nothing here happens on a schedule; someone has to run it.
 */
const SUB_STATUSES: AppraisalSubStatus[] = [
  'GoalSetting',
  'PeerNomination',
  'SelfEvaluation',
  'PeerEvaluation',
  'ManagerEvaluation',
  'PendingCalibration',
  'CalibrationInProgress',
  'PendingHRReview',
  'HRReviewInProgress',
  'PendingConversation',
  'PendingAcknowledgment',
];

const AUTO = '__auto__';

export default function DeadlineEnforcementPage() {
  const { toast } = useToast();
  const [cycleId, setCycleId] = useState('');
  const [result, setResult] = useState<DeadlineEnforcementResult | null>(null);

  const [appraisalId, setAppraisalId] = useState('');
  const [targetSubStatus, setTargetSubStatus] = useState<string>(AUTO);
  const [reason, setReason] = useState('');

  // The transition report (closure B8): read-only; a row is handed to the advance form below.
  const [report, setReport] = useState<AppraisalTransitionReport | null>(null);
  const checkCycle = useMutation({
    mutationFn: () => deadlineEnforcementService.transitionReport(cycleId),
    onSuccess: (r) => setReport(r),
    onError: (e: Error) =>
      toast({ title: 'Could not check the cycle', description: e.message, variant: 'destructive' }),
  });

  const sweep = useMutation({
    mutationFn: () => deadlineEnforcementService.enforce(cycleId),
    onSuccess: (r) => {
      setResult(r);
      toast({
        title: r.autoLockEnabled ? 'Sweep complete' : 'Nothing was changed',
        description: r.autoLockEnabled
          ? `${r.advanced} of ${r.evaluated} appraisal(s) advanced.`
          : 'Auto-lock on deadline is switched off for this cycle.',
        variant: r.autoLockEnabled ? 'default' : 'destructive',
      });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not run the sweep', description: e.message, variant: 'destructive' }),
  });

  const advance = useMutation({
    mutationFn: () =>
      deadlineEnforcementService.advance(appraisalId.trim(), {
        targetSubStatus: targetSubStatus === AUTO ? null : (targetSubStatus as AppraisalSubStatus),
        reason: reason.trim(),
      }),
    onSuccess: (r) => {
      // A refusal is an error status (422 for a step the appraisal is not at, 400 for a finished
      // one) and lands in onError with the server's message; the flag is kept as a guard.
      if (r.success) {
        toast({
          title: 'Appraisal advanced',
          description: `${humanizeEnum(r.previousSubStatus)} → ${humanizeEnum(r.newSubStatus)}.`,
        });
        setReason('');
      } else {
        toast({
          title: 'Not advanced',
          description: r.errorMessage ?? 'The step could not be advanced.',
          variant: 'destructive',
        });
      }
    },
    onError: (e: Error) =>
      toast({ title: 'Could not advance', description: e.message, variant: 'destructive' }),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Deadline enforcement"
        description="Push a stalled appraisal past a step someone else was supposed to complete."
        backHref="/hr/performance"
      />

      <Alert>
        <ShieldAlert className="h-4 w-4" />
        <AlertTitle>Every override is recorded against you</AlertTitle>
        <AlertDescription>
          Advancing a step skips work somebody owed — a self-evaluation that was never submitted,
          a manager who never scored. The reason you give is stored verbatim in the audit log
          against your name, and is the only explanation anyone will find later.
        </AlertDescription>
      </Alert>

      {/* ── Cycle sweep ──────────────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <FastForward className="h-4 w-4" />
            Advance overdue appraisals in a cycle
          </CardTitle>
          <CardDescription>
            Examines every active appraisal in the cycle and advances the ones whose current step
            has passed its deadline.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <CycleSelect value={cycleId} onChange={setCycleId} standalone={false} />

          <div className="flex justify-end">
            <Button onClick={() => sweep.mutate()} disabled={!cycleId || sweep.isPending}>
              {sweep.isPending ? 'Running…' : 'Run the sweep'}
            </Button>
          </div>

          {result && (
            <div className="space-y-4">
              {!result.autoLockEnabled && (
                <Alert variant="destructive">
                  <AlertTriangle className="h-4 w-4" />
                  <AlertTitle>Auto-lock is off for this cycle — nothing was changed</AlertTitle>
                  <AlertDescription>
                    The sweep honours the cycle&apos;s <strong>auto-lock on deadline</strong>
                    setting. Turn it on in the cycle&apos;s settings profile if overdue steps
                    should be advanced automatically, or use the single-appraisal override below.
                  </AlertDescription>
                </Alert>
              )}

              <MetricTiles
                tiles={[
                  { label: 'Examined', value: result.evaluated },
                  {
                    label: 'Advanced',
                    value: result.advanced,
                    tone: result.advanced > 0 ? 'warning' : 'default',
                  },
                  {
                    label: 'Auto-lock',
                    value: result.autoLockEnabled ? 'On' : 'Off',
                    tone: result.autoLockEnabled ? 'default' : 'danger',
                  },
                ]}
              />

              {result.messages.length > 0 && (
                <div className="rounded-lg border p-4">
                  <p className="mb-2 text-sm font-medium">What happened</p>
                  <ul className="space-y-1 text-sm text-muted-foreground">
                    {result.messages.map((m, i) => (
                      <li key={i}>{m}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* ── Transition report (closure B8) ───────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ListChecks className="h-4 w-4" />
            Records ahead of the gates
          </CardTitle>
          <CardDescription>
            Appraisals in the cycle above whose recorded work runs ahead of where the gates now hold
            them — a self-evaluation submitted before the goals were agreed, a manager&apos;s
            evaluation before the self-evaluation. Usually left by work done before the gates were
            enforced, or by a profile changed mid-cycle. Nothing here moves anything: waive the
            step through the advance below, or complete it.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex justify-end">
            <Button
              variant="outline"
              onClick={() => checkCycle.mutate()}
              disabled={!cycleId || checkCycle.isPending}
            >
              {checkCycle.isPending ? 'Checking…' : 'Check the cycle'}
            </Button>
          </div>

          {report && (
            <div className="space-y-3">
              <p className="text-sm text-muted-foreground">
                {report.rows.length === 0
                  ? `All ${report.examined} in-flight appraisal(s) agree with the gates.`
                  : `${report.rows.length} of ${report.examined} in-flight appraisal(s) have work recorded ahead of the gates.`}
              </p>
              {report.rows.length > 0 && (
                <div className="overflow-x-auto rounded-lg border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Employee</TableHead>
                        <TableHead>Held at</TableHead>
                        <TableHead>Recorded ahead</TableHead>
                        <TableHead className="w-[170px]" />
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {report.rows.map((row) => (
                        <TableRow key={row.appraisalId}>
                          <TableCell>
                            <div className="font-medium">{row.employeeName}</div>
                            <div className="text-xs text-muted-foreground">
                              {[row.employeeNumber, row.appraisalNumber].filter(Boolean).join(' · ')}
                            </div>
                          </TableCell>
                          <TableCell>
                            <Badge variant="outline">{row.stepLabel}</Badge>
                            {row.reason && (
                              <p className="mt-1 text-xs text-muted-foreground">{row.reason}</p>
                            )}
                          </TableCell>
                          <TableCell className="text-sm">
                            <ul className="list-disc space-y-0.5 pl-4">
                              {row.recordedAhead.map((what) => (
                                <li key={what}>{what}</li>
                              ))}
                            </ul>
                          </TableCell>
                          <TableCell>
                            {row.canWaive ? (
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => {
                                  setAppraisalId(row.appraisalId);
                                  setTargetSubStatus(AUTO);
                                  toast({
                                    title: 'Filled in below',
                                    description: `Give the reason and advance ${row.employeeName} past ${row.stepLabel}.`,
                                  });
                                }}
                              >
                                Waive this step
                              </Button>
                            ) : (
                              <span className="text-xs text-muted-foreground">
                                Complete the step — it cannot be waived.
                              </span>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* ── Single advance ───────────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <FastForward className="h-4 w-4" />
            Advance one appraisal
          </CardTitle>
          <CardDescription>
            Works whether or not auto-lock is on. An advance moves the appraisal past the step it is
            at — leave the step on &ldquo;Whatever is blocking&rdquo;, or name that same step; any
            other step is refused. Before the manager&apos;s evaluation, the advance is also the
            recorded waiver of that step (a missing goal, peer or self-evaluation).
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="appraisalId">Appraisal id</Label>
              <Input
                id="appraisalId"
                value={appraisalId}
                onChange={(e) => setAppraisalId(e.target.value)}
                placeholder="Paste it from the appraisal's URL"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="targetSubStatus">Step to advance past</Label>
              <Select value={targetSubStatus} onValueChange={setTargetSubStatus}>
                <SelectTrigger id="targetSubStatus">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={AUTO}>Whatever is blocking</SelectItem>
                  {SUB_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {humanizeEnum(s)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="reason">
              Reason<span className="ml-0.5 text-red-500">*</span>
            </Label>
            <Textarea
              id="reason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Why the step is being skipped, and on whose authority."
            />
          </div>

          <div className="flex items-center justify-end gap-3">
            <p className="flex items-center gap-1 text-xs text-muted-foreground">
              <Info className="h-3 w-3" />
              A refusal names the step the appraisal is at.
            </p>
            <Button
              onClick={() => advance.mutate()}
              disabled={!appraisalId.trim() || !reason.trim() || advance.isPending}
            >
              {advance.isPending ? 'Advancing…' : 'Advance'}
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
