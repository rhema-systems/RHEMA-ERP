'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { CheckCircle2, ClipboardCheck, Gavel, Send, TriangleAlert, Users } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { AppraisalPhaseRail } from '@/components/hr/performance/AppraisalPhaseRail';
import { ConversationsPanel } from '@/components/hr/performance/ConversationsPanel';
import { PeerNominationPanel } from '@/components/hr/performance/PeerNominationPanel';
import { SubmittedEvaluationView } from '@/components/hr/performance/SubmittedEvaluationView';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import {
  appraisalWorkflowService,
  performanceAppraisalService,
} from '@/services/hr/appraisal-run.service';

/**
 * One of my appraisals: where it is, what it wants from me, and — once HR has signed it off —
 * what it concluded.
 *
 * The rail across the top is the *computed* phase, not the stored status. `AppraisalStatus`
 * lumps everything between opening and HR sign-off into `Active`, so on its own it cannot say
 * whether the appraisal is waiting on me, my peers or my manager. The phase can.
 *
 * The outcome tab stays empty until HR finalises. That is deliberate on the server's side too:
 * the manager's scores are not readable by the appraisee before sign-off.
 */
export default function MyAppraisalDetailPage() {
  const params = useParams<{ id: string }>();
  const appraisalId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [tab, setTab] = useState('overview');

  const { data: context, isLoading } = useQuery({
    queryKey: ['hr', 'self-evaluation-context', appraisalId],
    queryFn: () => performanceAppraisalService.getSelfEvaluationContext(appraisalId),
    enabled: !!appraisalId,
  });

  const { data: phase } = useQuery({
    queryKey: ['hr', 'appraisal-phase', appraisalId],
    queryFn: () => appraisalWorkflowService.getPhase(appraisalId),
    enabled: !!appraisalId,
  });

  // 404 until the self-evaluation is submitted, which is the normal case early on — so a
  // failure here is not an error state, it just means there is nothing to show yet.
  const { data: submitted } = useQuery({
    queryKey: ['hr', 'submitted-evaluation', appraisalId],
    queryFn: () => performanceAppraisalService.getSubmittedEvaluation(appraisalId),
    enabled: !!appraisalId && context?.isSelfEvaluationSubmitted === true,
    retry: false,
  });

  const { data: hrReview } = useQuery({
    queryKey: ['hr', 'hr-review', appraisalId],
    queryFn: () => performanceAppraisalService.getHRReview(appraisalId),
    enabled: !!appraisalId,
    retry: false,
  });

  const acknowledge = useMutation({
    mutationFn: () => performanceAppraisalService.acknowledge(appraisalId),
    onSuccess: () => {
      toast({ title: 'Appraisal acknowledged', description: 'Your appraisal is now complete.' });
      queryClient.invalidateQueries({ queryKey: ['hr', 'self-evaluation-context', appraisalId] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'hr-review', appraisalId] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'appraisal-phase', appraisalId] });
      queryClient.invalidateQueries({ queryKey: ['hr', 'my-appraisals'] });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not acknowledge', description: e.message, variant: 'destructive' }),
  });

  const settings = context?.settings ?? null;
  const requiresNomination =
    settings?.requirePeerReviews === true && settings.peerNominationMode === 'Employee';

  // The appraisee sees their own outcome only once HR has finalised. Before that the HR review
  // read still answers, but with an in-progress picture that is not theirs to act on.
  const outcomeReady = hrReview?.isFinalized === true;
  const canAcknowledge =
    outcomeReady &&
    settings?.requireEmployeeAcknowledgment === true &&
    !hrReview?.employeeAcknowledgedDate;

  const tiles = useMemo(() => {
    if (!context) return [];
    return [
      {
        label: 'Self-evaluation',
        value: context.isSelfEvaluationSubmitted ? 'Submitted' : 'Outstanding',
        hint: context.isSelfEvaluationSubmitted
          ? formatDate(context.selfEvaluationSubmittedDate)
          : context.selfEvaluationDeadline
            ? `Due ${formatDate(context.selfEvaluationDeadline)}`
            : undefined,
        icon: ClipboardCheck,
        tone: context.isSelfEvaluationSubmitted ? ('success' as const) : ('warning' as const),
      },
      {
        label: 'Peer reviews',
        value: hrReview
          ? `${hrReview.completedPeerReviews} of ${hrReview.requiredPeerReviews}`
          : settings?.requirePeerReviews
            ? '—'
            : 'Not used',
        hint: settings?.peerReviewsAnonymous ? 'Anonymous to you' : undefined,
        icon: Users,
      },
      {
        label: 'Manager evaluation',
        value: hrReview?.isManagerEvaluationComplete ? 'Complete' : 'Outstanding',
        icon: CheckCircle2,
        tone: hrReview?.isManagerEvaluationComplete ? ('success' as const) : ('default' as const),
      },
      {
        label: 'Final score',
        value: outcomeReady && hrReview?.finalScore != null
          ? Number(hrReview.finalScore).toFixed(1)
          : '—',
        hint: outcomeReady ? (hrReview?.finalGrade ?? undefined) : 'Available after HR sign-off',
      },
    ];
  }, [context, hrReview, settings, outcomeReady]);

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-32 w-full" />
      </div>
    );
  }

  if (!context) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Appraisal" backHref="/hr/performance/appraisals" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Appraisal not found"
              description="It may belong to another employee, or the cycle it came from has been removed."
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={context.appraisalCycleName}
        description={`${context.appraisalNumber} · ${formatDate(context.periodStart)} – ${formatDate(context.periodEnd)}`}
        backHref="/hr/performance/appraisals"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={humanizeEnum(context.status)} />
            {context.isEditable && !context.isSelfEvaluationSubmitted && (
              <Button asChild>
                <Link href={`/hr/performance/appraisals/${appraisalId}/self-evaluation`}>
                  <Send className="mr-2 h-4 w-4" />
                  Self-evaluation
                </Link>
              </Button>
            )}
          </div>
        }
      />

      <Card>
        <CardContent className="p-4">
          <AppraisalPhaseRail phase={phase?.phase} settings={settings} />
        </CardContent>
      </Card>

      {canAcknowledge && (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Your appraisal has been finalised</AlertTitle>
          <AlertDescription className="flex flex-wrap items-center gap-3">
            <span>
              Review the outcome below, then acknowledge it to close the appraisal. You cannot
              undo this.
            </span>
            <Button
              size="sm"
              onClick={() => acknowledge.mutate()}
              disabled={acknowledge.isPending}
            >
              Acknowledge
            </Button>
          </AlertDescription>
        </Alert>
      )}

      {/*
        Appeals open only once the appraisal is Completed — that is, after acknowledgment — and
        the appeal form itself refuses anything else with an explanation. Both links are offered
        whenever the appraisal has got that far: which one is relevant depends on whether an
        appeal has already been filed, and the pages answer that better than a guess here would.
      */}
      {context.status === 'Completed' && (
        <Alert>
          <Gavel className="h-4 w-4" />
          <AlertTitle>Disagree with the outcome?</AlertTitle>
          <AlertDescription className="flex flex-wrap items-center gap-3">
            <span>
              You can appeal a finalised appraisal once. If you already have, follow it here.
            </span>
            <Button size="sm" variant="outline" asChild>
              <Link href={`/hr/performance/appraisals/${appraisalId}/appeal`}>File an appeal</Link>
            </Button>
            <Button size="sm" variant="ghost" asChild>
              <Link href={`/hr/performance/appraisals/${appraisalId}/appeal-status`}>
                My appeal
              </Link>
            </Button>
          </AlertDescription>
        </Alert>
      )}

      <MetricTiles tiles={tiles} />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="self">My evaluation</TabsTrigger>
          {requiresNomination && <TabsTrigger value="peers">My peer nominations</TabsTrigger>}
          <TabsTrigger value="outcome">Outcome</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="mt-4 space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">How this appraisal is scored</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-3 text-sm sm:grid-cols-3">
              <WeightLine
                label="Your self-evaluation"
                weight={settings?.selfEvaluationWeight}
                enabled={settings?.requireSelfEvaluation}
              />
              <WeightLine
                label="Peer feedback"
                weight={settings?.peerEvaluationWeight}
                enabled={settings?.requirePeerReviews}
              />
              <WeightLine
                label="Your manager"
                weight={settings?.managerEvaluationWeight}
                enabled={settings?.requireManagerEvaluation}
              />
            </CardContent>
          </Card>

          {settings && !settings.showScoreBreakdownToEmployee && (
            <p className="text-sm text-muted-foreground">
              This cycle does not show you the score breakdown by evaluator — only the final
              result.
            </p>
          )}

          {/* Read-only: the manager schedules these, and the notes are what they wrote. */}
          <ConversationsPanel appraisalId={appraisalId} />
        </TabsContent>

        <TabsContent value="self" className="mt-4">
          {context.isSelfEvaluationSubmitted && submitted ? (
            <SubmittedEvaluationView data={submitted} />
          ) : context.isSelfEvaluationSubmitted ? (
            <Card>
              <CardContent className="p-0">
                <EmptyState
                  icon={ClipboardCheck}
                  title="Submitted"
                  description="Your self-evaluation is in. The detailed view could not be loaded."
                />
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardContent className="p-0">
                <EmptyState
                  icon={ClipboardCheck}
                  title="Not submitted yet"
                  description={
                    context.isEditable
                      ? 'Score yourself against the criteria in this cycle. You can save a draft and come back.'
                      : 'This appraisal is not currently open for your self-evaluation.'
                  }
                  action={
                    context.isEditable ? (
                      <Button asChild>
                        <Link href={`/hr/performance/appraisals/${appraisalId}/self-evaluation`}>
                          Start self-evaluation
                        </Link>
                      </Button>
                    ) : undefined
                  }
                />
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {requiresNomination && (
          <TabsContent value="peers" className="mt-4">
            <PeerNominationPanel appraisalId={appraisalId} canManage />
          </TabsContent>
        )}

        <TabsContent value="outcome" className="mt-4">
          {!outcomeReady ? (
            <Card>
              <CardContent className="p-0">
                <EmptyState
                  icon={CheckCircle2}
                  title="No outcome yet"
                  description="Your result appears here once HR has reviewed and signed off the appraisal."
                />
              </CardContent>
            </Card>
          ) : (
            <div className="space-y-4">
              <MetricTiles
                tiles={[
                  {
                    label: 'Final score',
                    value: hrReview?.finalScore != null ? Number(hrReview.finalScore).toFixed(1) : '—',
                    hint: hrReview?.finalGrade ?? undefined,
                  },
                  {
                    label: 'Finalised',
                    value: formatDate(hrReview?.finalizedDate),
                    hint: hrReview?.finalizedByName ?? undefined,
                  },
                  {
                    label: 'Acknowledged',
                    value: hrReview?.employeeAcknowledgedDate
                      ? formatDate(hrReview.employeeAcknowledgedDate)
                      : 'Not yet',
                    tone: hrReview?.employeeAcknowledgedDate ? ('success' as const) : ('warning' as const),
                  },
                ]}
              />

              {settings?.showScoreBreakdownToEmployee && (
                <Card>
                  <CardHeader>
                    <CardTitle className="text-base">How the score was made up</CardTitle>
                  </CardHeader>
                  <CardContent className="grid gap-3 text-sm sm:grid-cols-3">
                    <ScoreLine label="Self" score={hrReview?.selfScore} weight={hrReview?.selfWeight} />
                    <ScoreLine label="Peers" score={hrReview?.peerScore} weight={hrReview?.peerWeight} />
                    <ScoreLine
                      label="Manager"
                      score={hrReview?.managerScore}
                      weight={hrReview?.managerWeight}
                    />
                  </CardContent>
                </Card>
              )}

              {hrReview?.hrRemarks && (
                <Card>
                  <CardHeader>
                    <CardTitle className="text-base">HR remarks</CardTitle>
                  </CardHeader>
                  <CardContent className="whitespace-pre-wrap text-sm">
                    {hrReview.hrRemarks}
                  </CardContent>
                </Card>
              )}
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}

function WeightLine({
  label,
  weight,
  enabled,
}: {
  label: string;
  weight?: number | null;
  enabled?: boolean;
}) {
  return (
    <div className="rounded-md border p-3">
      <p className="text-muted-foreground">{label}</p>
      <p className="mt-1 text-lg font-semibold tabular-nums">
        {enabled === false ? 'Not used' : `${Math.round((Number(weight) || 0) * 100)}%`}
      </p>
    </div>
  );
}

function ScoreLine({
  label,
  score,
  weight,
}: {
  label: string;
  score?: number | null;
  weight?: number | null;
}) {
  return (
    <div className="rounded-md border p-3">
      <p className="text-muted-foreground">
        {label} · {Math.round((Number(weight) || 0) * 100)}%
      </p>
      <p className="mt-1 text-lg font-semibold tabular-nums">
        {score != null ? Number(score).toFixed(1) : '—'}
      </p>
    </div>
  );
}
