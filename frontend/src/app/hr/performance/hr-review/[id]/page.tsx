'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import { CheckCircle2, Clock, RotateCcw, TriangleAlert, Undo2 } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
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
 *   • **Finalise** recalculates the weighted score from all three legs, records the sign-off,
 *     and pushes the score onto the employee's talent records. Where it lands depends on the
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
            All required evaluations are in. Finalising recalculates the weighted score and
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
              <CardTitle className="text-base">Competencies</CardTitle>
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
                      <div className="font-medium">{s.criteriaName}</div>
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
            <CardTitle className="text-base">KPIs</CardTitle>
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
                      <div className="font-medium">{k.kpiName}</div>
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
