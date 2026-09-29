'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import { CheckCircle2, Clock, Loader2, Pencil, RotateCcw, Trash2, TriangleAlert, Undo2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { PermissionGate } from '@/components/hr/common/PermissionGate';
import { Input } from '@/components/ui/input';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { AppraisalPhaseRail } from '@/components/hr/performance/AppraisalPhaseRail';
import { EmployeeTrendPanel } from '@/components/hr/performance/EmployeeTrendPanel';
import { PeerFeedbackPanel } from '@/components/hr/performance/PeerFeedbackPanel';
import { OutcomeRecommendationsPanel } from '@/components/hr/performance/OutcomeRecommendationsPanel';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import {
  appraisalWorkflowService,
  performanceAppraisalService,
} from '@/services/hr/appraisal-run.service';
import type { EvaluationSummary } from '@/types/hr/appraisal-run';

/**
 * HR's sign-off on one appraisal.
 *
 * Two outcomes, and they are not symmetrical:
 *   • **Finalise** settles the score — the panel's restated overall where a calibration committed
 *     one, otherwise the weighted mean of the submitted evaluations — records the sign-off, and
 *     pushes the rating onto the employee's talent records. Where it lands depends on the
 *     cycle: `Governance` when an acknowledgment is required (the employee closes it out), or
 *     `Completed` when one is not.
 *   • **Return to manager** reopens the manager's evaluation and puts the appraisal back to
 *     Active. Remarks are mandatory — they are the whole message the manager gets.
 *
 * The precondition panel is the important part of this screen. Finalising with a leg
 * outstanding is refused with 422, so what is missing is shown before the button is pressed
 * rather than as an error afterwards.
 */
export default function HRReviewDetailPage() {
  const params = useParams<{ id: string }>();
  const appraisalId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [approveOpen, setApproveOpen] = useState(false);
  const [returnOpen, setReturnOpen] = useState(false);
  const [remarks, setRemarks] = useState('');
  const [returnRemarks, setReturnRemarks] = useState('');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'hr-review', appraisalId],
    queryFn: () => performanceAppraisalService.getHRReview(appraisalId),
    enabled: !!appraisalId,
    retry: false,
  });

  /**
   * The appraisal record itself — the HR review read is about the REVIEW, so it carries neither the
   * window nor the cycle and employee the replace-shaped update has to send back.
   */
  const { data: appraisal } = useQuery({
    queryKey: ['hr', 'appraisal-record', appraisalId],
    queryFn: () => performanceAppraisalService.getById(appraisalId),
    enabled: !!appraisalId,
    retry: false,
  });

  const [headerOpen, setHeaderOpen] = useState(false);
  const [header, setHeader] = useState({ year: '', startDate: '', endDate: '', peers: '' });
  const [deleteOpen, setDeleteOpen] = useState(false);

  const openHeader = () => {
    if (!appraisal) return;
    setHeader({
      year: String(appraisal.year ?? ''),
      startDate: (appraisal.startDate ?? '').slice(0, 10),
      endDate: (appraisal.endDate ?? '').slice(0, 10),
      peers: String(appraisal.peerEvaluatorsCount ?? 0),
    });
    setHeaderOpen(true);
  };

  const saveHeader = useMutation({
    mutationFn: () => {
      if (!appraisal) throw new Error('The appraisal has not loaded.');
      return performanceAppraisalService.updateHeader(appraisal, {
        year: Number(header.year),
        startDate: header.startDate,
        endDate: header.endDate,
        peerEvaluatorsCount: Number(header.peers),
      });
    },
    onSuccess: () => {
      setHeaderOpen(false);
      queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-record', appraisalId] });
      refresh();
      toast({ title: 'Appraisal updated' });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Could not update the appraisal',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const removeAppraisal = useMutation({
    mutationFn: () => performanceAppraisalService.deleteAppraisal(appraisalId),
    onSuccess: () => {
      setDeleteOpen(false);
      queryClient.invalidateQueries({ queryKey: ['hr', 'hr-review-list'] });
      toast({ title: 'Appraisal removed' });
      window.location.href = '/hr/performance/hr-review';
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Could not remove the appraisal',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const { data: phase } = useQuery({
    queryKey: ['hr', 'appraisal-phase', appraisalId],
    queryFn: () => appraisalWorkflowService.getPhase(appraisalId),
    enabled: !!appraisalId,
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'hr-review', appraisalId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'hr-review-list'] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-phase', appraisalId] });
  };

  const approve = useMutation({
    mutationFn: () =>
      performanceAppraisalService.approve(appraisalId, { hrRemarks: remarks.trim() || null }),
    onSuccess: (result) => {
      toast({
        title: 'Appraisal finalised',
        description:
          result.status === 'Governance'
            ? 'The employee has been asked to acknowledge it.'
            : 'The appraisal is complete.',
      });
      setApproveOpen(false);
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not finalise', description: e.message, variant: 'destructive' }),
  });

  const sendBack = useMutation({
    mutationFn: () =>
      performanceAppraisalService.returnToManager(appraisalId, {
        hrRemarks: returnRemarks.trim(),
      }),
    onSuccess: () => {
      toast({
        title: 'Returned to the manager',
        description: 'Their evaluation has been reopened and they have been notified.',
      });
      setReturnOpen(false);
      setReturnRemarks('');
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not return', description: e.message, variant: 'destructive' }),
  });

  /**
   * Repair route for an appraisal that reached HR with no reviewer assigned — normally the
   * manager's submission does this. Offered only when it is actually needed.
   */
  const assignReviewer = useMutation({
    mutationFn: () => performanceAppraisalService.progressToHRReview(appraisalId),
    onSuccess: (assigned) => {
      toast({
        title: assigned ? 'HR reviewer assigned' : 'Nothing to assign',
        description: assigned
          ? 'This appraisal is now in your queue.'
          : 'A reviewer is already assigned, or this cycle does not require HR review.',
      });
      refresh();
    },
    onError: (e: Error) =>
      toast({ title: 'Could not assign', description: e.message, variant: 'destructive' }),
  });

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="HR review" backHref="/hr/performance/hr-review" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not open this appraisal"
              description={(error as Error)?.message ?? 'It may no longer exist.'}
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  const outstanding = [
    !data.isSelfEvaluationComplete && 'the employee has not submitted a self-evaluation',
    !data.isManagerEvaluationComplete && 'the manager has not submitted their evaluation',
    data.requiresPeerReviews &&
      !data.arePeerReviewsComplete &&
      `only ${data.completedPeerReviews} of ${data.requiredPeerReviews} peer evaluations are in`,
  ].filter(Boolean) as string[];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={data.employeeName}
        description={`${data.position} · ${data.organizationUnit} · ${data.appraisalCycleName}`}
        backHref="/hr/performance/hr-review"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={humanizeEnum(data.status)} />
            {!data.isFinalized && (
              <>
                {/*
                  ⚠ Correcting the WINDOW only. Regenerating the cycle is not an alternative — it
                  does not touch an appraisal that already exists — so before this there was no way
                  to fix a generated appraisal's dates at all. The employee, the cycle and the
                  status are deliberately absent: the server ignores all three, and it ignores them
                  because honouring them let a "correction" move an appraisal onto a different
                  person. The status moves through its own transitions, which enforce their order.
                */}
                <Button variant="ghost" onClick={openHeader} disabled={!appraisal}>
                  <Pencil className="mr-2 h-4 w-4" />
                  Correct dates
                </Button>
                {/*
                  Admin-tier, and gated rather than shown-and-refused: an HR-role caller gets a 403,
                  which the lane-3 probe established rather than assumed.
                */}
                <PermissionGate permissions={['HR.Performance.Admin']}>
                  <Button variant="ghost" onClick={() => setDeleteOpen(true)}>
                    <Trash2 className="mr-2 h-4 w-4" />
                    Remove
                  </Button>
                </PermissionGate>
                <Button
                  variant="outline"
                  onClick={() => setReturnOpen(true)}
                  disabled={!data.isManagerEvaluationComplete}
                >
                  <Undo2 className="mr-2 h-4 w-4" />
                  Return to manager
                </Button>
                <Button
                  onClick={() => setApproveOpen(true)}
                  disabled={!data.canProceedToHRReview}
                >
                  <CheckCircle2 className="mr-2 h-4 w-4" />
                  Finalise
                </Button>
              </>
            )}
          </div>
        }
      />

      <Card>
        <CardContent className="p-4">
          <AppraisalPhaseRail phase={phase?.phase} />
        </CardContent>
      </Card>

      {data.isFinalized ? (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>
            Finalised {formatDate(data.finalizedDate)}
            {data.finalizedByName ? ` by ${data.finalizedByName}` : ''}
          </AlertTitle>
          <AlertDescription>
            {data.employeeAcknowledgedDate
              ? `The employee acknowledged it on ${formatDate(data.employeeAcknowledgedDate)}.`
              : 'Waiting for the employee to acknowledge it.'}
            {data.hrRemarks && ` Remarks: ${data.hrRemarks}`}
          </AlertDescription>
        </Alert>
      ) : outstanding.length > 0 ? (
        <Alert>
          <Clock className="h-4 w-4" />
          <AlertTitle>Not ready to finalise</AlertTitle>
          <AlertDescription>
            Finalising is blocked because {outstanding.join(', and ')}.
          </AlertDescription>
        </Alert>
      ) : (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Ready to finalise</AlertTitle>
          <AlertDescription>
            All required evaluations are in. Finalising settles the score — the calibration
            panel&apos;s overall where it restated one, otherwise the weighted evaluations — and
            {data.status === 'Governance'
              ? ' closes HR’s part of this appraisal.'
              : ' hands the appraisal on for acknowledgment.'}
          </AlertDescription>
        </Alert>
      )}

      {!data.weightTotalValid && (
        <Alert variant="destructive">
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>Evaluator weights do not total 100%</AlertTitle>
          <AlertDescription>
            They come to {Math.round(Number(data.totalWeight) * 100)}%. The final score will be
            calculated from them as they stand — fix the cycle&apos;s settings profile before
            signing this off.
          </AlertDescription>
        </Alert>
      )}

      {!data.isFinalized && data.isManagerEvaluationComplete && data.status !== 'Governance' && (
        <Card className="border-dashed">
          <CardContent className="flex flex-wrap items-center justify-between gap-3 p-4 text-sm">
            <span className="text-muted-foreground">
              This appraisal has a completed manager evaluation but is not in Governance — it may
              have reached HR without a reviewer being assigned.
            </span>
            <Button
              size="sm"
              variant="outline"
              onClick={() => assignReviewer.mutate()}
              disabled={assignReviewer.isPending}
            >
              <RotateCcw className="mr-2 h-4 w-4" />
              Assign HR reviewer
            </Button>
          </CardContent>
        </Card>
      )}

      <MetricTiles
        tiles={[
          {
            label: 'Self',
            value: data.selfScore != null ? Number(data.selfScore).toFixed(1) : '—',
            hint: `Weight ${Math.round(Number(data.selfWeight) * 100)}%`,
          },
          {
            label: 'Peers',
            value: data.peerScore != null ? Number(data.peerScore).toFixed(1) : '—',
            hint: `Weight ${Math.round(Number(data.peerWeight) * 100)}% · ${data.completedPeerReviews}/${data.requiredPeerReviews} in`,
          },
          {
            label: 'Manager',
            value: data.managerScore != null ? Number(data.managerScore).toFixed(1) : '—',
            hint: `Weight ${Math.round(Number(data.managerWeight) * 100)}%`,
          },
          {
            label: 'Final',
            value: data.finalScore != null ? Number(data.finalScore).toFixed(1) : '—',
            hint: data.finalGrade ?? 'Computed on finalisation',
            tone: 'success',
          },
        ]}
      />

      <Tabs defaultValue="manager">
        <TabsList>
          <TabsTrigger value="manager">Manager evaluation</TabsTrigger>
          <TabsTrigger value="self">Self-evaluation</TabsTrigger>
          {data.requiresPeerReviews && <TabsTrigger value="peers">Peer feedback</TabsTrigger>}
          <TabsTrigger value="history">History</TabsTrigger>
          <TabsTrigger value="outcomes">Outcomes</TabsTrigger>
        </TabsList>
        <TabsContent value="manager" className="mt-4">
          <SummaryTables summary={data.managerEvaluation} label="manager" />
        </TabsContent>
        <TabsContent value="self" className="mt-4">
          <SummaryTables summary={data.selfEvaluation} label="employee" />
        </TabsContent>
        {data.requiresPeerReviews && (
          <TabsContent value="peers" className="mt-4">
            <PeerFeedbackPanel appraisalId={appraisalId} />
          </TabsContent>
        )}
        {/*
          Previous years, next to this one. A score only means something against the employee's
          own history — a 68 is a good year for one person and a slide for another — and this is
          the point at which the number is being signed off.
        */}
        <TabsContent value="history" className="mt-4">
          <EmployeeTrendPanel employeeId={data.employeeId} employeeName={data.employeeName} />
        </TabsContent>
        {/*
          What this appraisal should lead to. Sits with the sign-off because that is when the
          decision is actually made — approving a recommendation here is what creates the pay
          proposal or employment action downstream.
        */}
        <TabsContent value="outcomes" className="mt-4">
          <OutcomeRecommendationsPanel appraisalId={appraisalId} allowDecide />
        </TabsContent>
      </Tabs>

      <Dialog open={approveOpen} onOpenChange={setApproveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Finalise this appraisal?</DialogTitle>
            <DialogDescription>
              The weighted final score is calculated and recorded, and the result is pushed onto
              the employee&apos;s talent records. This cannot be undone from here.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="hr-remarks">HR remarks (optional)</Label>
            <Textarea
              id="hr-remarks"
              rows={3}
              value={remarks}
              onChange={(e) => setRemarks(e.target.value)}
              placeholder="Visible to the employee with their result."
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproveOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => approve.mutate()} disabled={approve.isPending}>
              Finalise
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={returnOpen} onOpenChange={setReturnOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Return to the manager?</DialogTitle>
            <DialogDescription>
              Their evaluation is reopened for editing and the appraisal goes back to Active.
              Your remarks are the only explanation they get, so be specific.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="return-remarks">What needs changing</Label>
            <Textarea
              id="return-remarks"
              rows={4}
              value={returnRemarks}
              onChange={(e) => setReturnRemarks(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setReturnOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => sendBack.mutate()}
              disabled={!returnRemarks.trim() || sendBack.isPending}
            >
              Return
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog open={headerOpen} onOpenChange={setHeaderOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Correct this appraisal&rsquo;s dates</DialogTitle>
            <DialogDescription>
              The window the appraisal covers, and how many peers it expects. Who it is for and
              which cycle it belongs to cannot be changed here — an appraisal raised against the
              wrong person is removed and regenerated, not moved onto somebody else.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="ap-year">Year</Label>
              <Input
                id="ap-year" type="number" value={header.year}
                onChange={(e) => setHeader((h) => ({ ...h, year: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="ap-peers">Peer evaluators</Label>
              <Input
                id="ap-peers" type="number" min={0} value={header.peers}
                onChange={(e) => setHeader((h) => ({ ...h, peers: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="ap-start">Start</Label>
              <Input
                id="ap-start" type="date" value={header.startDate}
                onChange={(e) => setHeader((h) => ({ ...h, startDate: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="ap-end">End</Label>
              <Input
                id="ap-end" type="date" value={header.endDate}
                onChange={(e) => setHeader((h) => ({ ...h, endDate: e.target.value }))}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setHeaderOpen(false)}>Cancel</Button>
            <Button
              onClick={() => saveHeader.mutate()}
              disabled={saveHeader.isPending || header.startDate === '' || header.endDate === ''}
            >
              {saveHeader.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Remove {data.employeeName}&rsquo;s appraisal?</DialogTitle>
            <DialogDescription>
              For an appraisal generated against somebody who should not have been in scope. It
              takes the self-evaluation, peer reviews and scores with it, and regenerating the cycle
              will not bring them back. Nothing else removes an appraisal.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)}>Cancel</Button>
            <Button
              variant="destructive"
              onClick={() => removeAppraisal.mutate()}
              disabled={removeAppraisal.isPending}
            >
              {removeAppraisal.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Remove it
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function SummaryTables({
  summary,
  label,
}: {
  summary?: EvaluationSummary | null;
  label: string;
}) {
  if (!summary || (summary.competencyScores.length === 0 && summary.kpiScores.length === 0)) {
    return (
      <Card>
        <CardContent className="p-0">
          <EmptyState
            icon={Clock}
            title={`No ${label} evaluation yet`}
            description={`Scores appear here once the ${label} submits.`}
          />
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      {summary.competencyScores.length > 0 && (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-2">
              <CardTitle className="text-base">
                {summary.competencyScores.some((s) => s.isGoal) ? 'Competencies and rated goals' : 'Competencies'}
              </CardTitle>
              {summary.totalScore != null && (
                <span className="text-sm text-muted-foreground">
                  Total {Number(summary.totalScore).toFixed(2)}
                </span>
              )}
            </div>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Criterion</TableHead>
                  <TableHead className="w-20 text-right">Weight</TableHead>
                  <TableHead className="w-20 text-right">Score</TableHead>
                  <TableHead className="w-24 text-right">Weighted</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {summary.competencyScores.map((s, i) => (
                  <TableRow key={`${s.criteriaName}-${i}`}>
                    <TableCell>
                      <div className="font-medium">
                        {s.criteriaName}
                        {s.isGoal && (
                          <Badge variant="secondary" className="ml-2">
                            Goal
                          </Badge>
                        )}
                      </div>
                      {s.comments && (
                        <div className="mt-1 whitespace-pre-wrap text-xs text-muted-foreground">
                          {s.comments}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{s.weight}%</TableCell>
                    <TableCell className="text-right tabular-nums">{s.numericScore ?? '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {s.weightedScore != null ? Number(s.weightedScore).toFixed(2) : '—'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {summary.kpiScores.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              {summary.kpiScores.some((k) => k.isGoal) ? 'KPIs and measured goals' : 'KPIs'}
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>KPI</TableHead>
                  <TableHead className="w-28 text-right">Target</TableHead>
                  <TableHead className="w-28 text-right">Actual</TableHead>
                  <TableHead className="w-24 text-right">Achieved</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {summary.kpiScores.map((k, i) => (
                  <TableRow key={`${k.kpiName}-${i}`}>
                    <TableCell>
                      <div className="font-medium">
                        {k.kpiName}
                        {k.isGoal && (
                          <Badge variant="secondary" className="ml-2">
                            Goal
                          </Badge>
                        )}
                      </div>
                      {k.notes && (
                        <div className="mt-1 whitespace-pre-wrap text-xs text-muted-foreground">
                          {k.notes}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {k.targetValue ?? '—'}
                      {k.unit ? ` ${k.unit}` : ''}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{k.actualValue ?? '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {k.achievementPercentage != null
                        ? `${Number(k.achievementPercentage).toFixed(1)}%`
                        : '—'}
                      {k.achievementOverridden && (
                        <div className="text-xs font-normal text-amber-700 dark:text-amber-400">
                          Overridden by calibration/appeal
                        </div>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {summary.generalComments && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Comments</CardTitle>
          </CardHeader>
          <CardContent className="whitespace-pre-wrap text-sm">
            {summary.generalComments}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
