'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  Activity,
  BellRing,
  CalendarClock,
  ExternalLink,
  Gauge,
  Send,
  TriangleAlert,
  Users,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { CyclePipelinePanel } from '@/components/hr/performance/CyclePipelinePanel';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { DistributionBars } from '@/components/hr/performance/PerformanceCharts';
import { EmployeeTrendPanel } from '@/components/hr/performance/EmployeeTrendPanel';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { hrCycleDashboardService, performanceAnalyticsService } from '@/services/hr/analytics.service';
import { appraisalCycleService } from '@/services/hr/appraisal.service';
import type { CycleAttentionItem, CycleOutcomeCount } from '@/types/hr/analytics';

/**
 * The HR cycle dashboard — where a running cycle has actually got to.
 *
 * Everything on the first four tabs comes from **one** call, `api/HRCycleDashboard/{cycleId}`,
 * which aggregates server-side. Nothing here is stitched together from list endpoints, so the
 * figures cannot disagree with each other the way separately-fetched tiles do.
 *
 * Two things on this screen write, and both write notifications rather than records:
 *
 *  - **Send reminders** is the cycle-wide sweep. It notifies *everyone in scope* for *every*
 *    phase whose deadline is within the settings' risk bands. ⚠ It covers goal setting, peer
 *    nomination and the final conversation as well as the five year-end phases, so it is not the
 *    same set of steps as the deadline table below it.
 *  - **Nudge** on an attention row is the targeted one: a single appraisal, reaching only the
 *    people who can clear its current step.
 *
 * Approvals are deliberately absent. The outcomes this cycle produced run on the generic
 * workflow engine, and are approved on their own screens — the Outcomes tab counts what is
 * queued and links there rather than growing a second approval UI. See the workflow-engine
 * integration note: never build a module-specific approval surface.
 */

/** Rows the API sends at zero are dropped — a stream reads better as what exists, not as a form. */
function nonZero(counts: CycleOutcomeCount[]): CycleOutcomeCount[] {
  return counts.filter((c) => c.count > 0);
}

function OutcomeStream({
  title,
  description,
  counts,
  href,
  linkLabel,
}: {
  title: string;
  description: string;
  counts: CycleOutcomeCount[];
  href: string;
  linkLabel: string;
}) {
  const rows = nonZero(counts);
  const queued = rows.filter((r) => r.awaitingApproval).reduce((sum, r) => sum + r.count, 0);

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {rows.length === 0 ? (
          <p className="text-sm text-muted-foreground">Nothing raised from this cycle.</p>
        ) : (
          <ul className="space-y-2">
            {rows.map((r) => (
              <li key={r.key} className="flex items-center justify-between gap-3 text-sm">
                <span className="flex items-center gap-2">
                  <StatusBadge status={r.label} />
                  {r.awaitingApproval && (
                    <span className="text-xs text-muted-foreground">waiting on an approver</span>
                  )}
                </span>
                <span className="font-medium tabular-nums">{r.count}</span>
              </li>
            ))}
          </ul>
        )}

        {queued > 0 && (
          <p className="text-xs text-amber-600 dark:text-amber-500">
            {queued} record{queued === 1 ? '' : 's'} cannot progress until an approver acts.
          </p>
        )}

        <Button variant="outline" size="sm" asChild>
          <Link href={href}>
            {linkLabel}
            <ExternalLink className="ml-2 h-3 w-3" />
          </Link>
        </Button>
      </CardContent>
    </Card>
  );
}

export default function PerformanceAnalyticsPage() {
  const { toast } = useToast();
  const [cycleId, setCycleId] = useState('');

  const dashboard = useQuery({
    queryKey: ['hr', 'cycle-dashboard', cycleId],
    queryFn: () => hrCycleDashboardService.get(cycleId),
    enabled: !!cycleId,
  });

  const ratings = useQuery({
    queryKey: ['hr', 'cycle-rating-distribution', cycleId],
    queryFn: () => performanceAnalyticsService.getCycleRatingDistribution(cycleId),
    enabled: !!cycleId,
  });

  const reminders = useMutation({
    mutationFn: () => appraisalCycleService.sendDeadlineReminders(cycleId),
    onSuccess: (r) =>
      toast({
        title: r.notificationsRaised > 0 ? 'Reminders sent' : 'Nothing to remind anyone about',
        description:
          r.notificationsRaised > 0
            ? `${r.notificationsRaised} notification(s) raised.`
            : 'No phase deadline is close enough, or in the past, to be worth a reminder.',
      }),
    onError: (e: Error) =>
      toast({ title: 'Could not send reminders', description: e.message, variant: 'destructive' }),
  });

  const nudge = useMutation({
    mutationFn: (appraisalId: string) => hrCycleDashboardService.nudge(cycleId, appraisalId),
    onSuccess: (r) =>
      toast({
        title: r.notificationsRaised > 0 ? 'Reminder sent' : 'Already reminded',
        description:
          r.notificationsRaised > 0
            ? `${r.stepName} — ${r.recipients.join(', ')}.`
            : `${r.recipients.join(', ')} already ${r.recipients.length === 1 ? 'has' : 'have'} an unread reminder about this.`,
      }),
    onError: (e: Error) =>
      toast({ title: 'Could not send the reminder', description: e.message, variant: 'destructive' }),
  });

  const d = dashboard.data;

  const gradeData = useMemo(
    () =>
      (d?.gradeDistribution ?? []).map((g) => ({
        label: g.gradeName,
        count: g.count,
        hint: `${g.percentage}% of graded appraisals`,
      })),
    [d],
  );

  const ratingData = useMemo(
    () =>
      (ratings.data?.buckets ?? []).map((b) => ({
        label: b.ratingLabel,
        count: b.count,
        hint: `${b.percent}% of rated appraisals`,
      })),
    [ratings.data],
  );

  const criticalCount = (d?.attentionItems ?? []).filter((i) => i.severity === 'Critical').length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Performance analytics"
        description="Where a cycle has got to, how its scores fell out, and what is still waiting on somebody."
        backHref="/hr/performance"
        actions={
          <Button
            variant="outline"
            onClick={() => reminders.mutate()}
            disabled={!cycleId || reminders.isPending}
          >
            <Send className="mr-2 h-4 w-4" />
            {reminders.isPending ? 'Sending…' : 'Send reminders'}
          </Button>
        }
      />

      <CycleSelect value={cycleId} onChange={setCycleId} />

      {dashboard.isLoading && (
        <p className="py-12 text-center text-sm text-muted-foreground">Building the dashboard…</p>
      )}

      {dashboard.isError && (
        <Alert variant="destructive">
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>Could not load the dashboard</AlertTitle>
          <AlertDescription>{(dashboard.error as Error).message}</AlertDescription>
        </Alert>
      )}

      {d && (
        <>
          <MetricTiles
            tiles={[
              {
                label: 'Appraisals in the cycle',
                value: d.pipelineProgress.totalAppraisals,
                hint: `${humanizeEnum(d.cycleStatus)} · ${formatDate(d.startDate)} – ${formatDate(d.endDate)}`,
                icon: Users,
              },
              {
                label: 'Completed',
                value: d.pipelineProgress.completedCount + d.pipelineProgress.closedCount,
                hint:
                  d.pipelineProgress.totalAppraisals > 0
                    ? `${Math.round(((d.pipelineProgress.completedCount + d.pipelineProgress.closedCount) / d.pipelineProgress.totalAppraisals) * 100)}% of the cycle`
                    : undefined,
                icon: Gauge,
                tone: 'success',
              },
              {
                label: 'Average score',
                value: d.averageScore != null ? d.averageScore.toFixed(1) : '—',
                hint:
                  d.scoredAppraisalCount > 0
                    ? `across ${d.scoredAppraisalCount} scored appraisal${d.scoredAppraisalCount === 1 ? '' : 's'}`
                    : 'no appraisal has been scored yet',
              },
              {
                label: 'Needs attention',
                value: d.attentionItems.length,
                hint:
                  criticalCount > 0
                    ? `${criticalCount} critical`
                    : d.attentionItems.length > 0
                      ? 'nothing critical'
                      : undefined,
                icon: TriangleAlert,
                tone: criticalCount > 0 ? 'danger' : d.attentionItems.length > 0 ? 'warning' : 'default',
              },
            ]}
          />

          <Tabs defaultValue="pipeline">
            <TabsList>
              <TabsTrigger value="pipeline">Pipeline &amp; deadlines</TabsTrigger>
              <TabsTrigger value="scores">Scores</TabsTrigger>
              <TabsTrigger value="units">By unit</TabsTrigger>
              <TabsTrigger value="outcomes">Outcomes</TabsTrigger>
              <TabsTrigger value="attention">
                Attention{d.attentionItems.length > 0 ? ` (${d.attentionItems.length})` : ''}
              </TabsTrigger>
              <TabsTrigger value="activity">Activity</TabsTrigger>
              <TabsTrigger value="trend">Employee trend</TabsTrigger>
            </TabsList>

            {/* ── Pipeline & deadlines ─────────────────────────────────────── */}
            <TabsContent value="pipeline" className="space-y-4 pt-4">
              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="text-base">Pipeline</CardTitle>
                  <CardDescription>
                    How many appraisals are past each stage, and how many are sitting on it now.
                    The two figures do not add up to the cycle total — an appraisal further along
                    counts as past every stage behind it.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <CyclePipelinePanel dashboard={d} />
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="flex items-center gap-2 text-base">
                    <CalendarClock className="h-4 w-4" />
                    Phase deadlines
                  </CardTitle>
                  <CardDescription>
                    Only the phases this cycle&apos;s settings require. &ldquo;Passed&rdquo; means
                    the phase is behind the cycle, not that anyone is late.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {d.deadlines.length === 0 ? (
                    <EmptyState
                      title="No phase deadlines set"
                      description="Set them on the cycle so this table, the reminders and the risk bands have something to work from."
                    />
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Phase</TableHead>
                          <TableHead>Deadline</TableHead>
                          <TableHead>State</TableHead>
                          <TableHead className="text-right">Days left</TableHead>
                          <TableHead className="text-right">Done</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {d.deadlines.map((dl) => (
                          <TableRow key={dl.stepName}>
                            <TableCell className="font-medium">{dl.stepName}</TableCell>
                            <TableCell>{dl.deadline ? formatDate(dl.deadline) : '—'}</TableCell>
                            <TableCell>
                              <StatusBadge status={humanizeEnum(dl.state)} />
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {dl.daysRemaining == null
                                ? '—'
                                : dl.daysRemaining < 0
                                  ? `${Math.abs(dl.daysRemaining)} over`
                                  : dl.daysRemaining}
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {dl.appraisalsCompleted} / {dl.appraisalsAffected}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </CardContent>
              </Card>
            </TabsContent>

            {/* ── Scores ───────────────────────────────────────────────────── */}
            <TabsContent value="scores" className="grid gap-4 lg:grid-cols-2 pt-4">
              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="text-base">Grade distribution</CardTitle>
                  <CardDescription>
                    Appraisals per grade band. Percentages are of the graded population, which is
                    smaller than the scored one when a score falls outside every configured band.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <DistributionBars
                    data={gradeData}
                    emptyTitle="Nothing graded yet"
                    emptyDescription="Grades are assigned when HR signs an appraisal off."
                  />
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="text-base">Rating distribution</CardTitle>
                  <CardDescription>
                    The calibration check: a cycle bunched at the top is a leniency problem, not a
                    performance one.
                    {ratings.data?.averageScore != null &&
                      ` Mean ${ratings.data.averageScore.toFixed(1)} across ${ratings.data.totalRated} rated.`}
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {ratings.isError ? (
                    <p className="text-sm text-muted-foreground">
                      {(ratings.error as Error).message}
                    </p>
                  ) : (
                    <DistributionBars
                      data={ratingData}
                      emptyTitle="No rated appraisals yet"
                      emptyDescription="Ratings are derived from the overall score, which is set at sign-off."
                    />
                  )}
                </CardContent>
              </Card>
            </TabsContent>

            {/* ── By unit ──────────────────────────────────────────────────── */}
            <TabsContent value="units" className="space-y-4 pt-4">
              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="text-base">Progress by organisation unit</CardTitle>
                  <CardDescription>
                    Who to chase. The manager column is the unit&apos;s configured head where it
                    has one, and otherwise a line manager taken from its people — so treat it as a
                    starting point, not an assignment.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {d.departmentBreakdown.length === 0 ? (
                    <EmptyState
                      title="No unit breakdown"
                      description="Appraisees in this cycle have no organisation unit on their employee record."
                    />
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Unit</TableHead>
                          <TableHead>Head</TableHead>
                          <TableHead className="text-right">Appraisals</TableHead>
                          <TableHead className="text-right">Complete</TableHead>
                          <TableHead className="text-right">Overdue</TableHead>
                          <TableHead className="text-right">Avg score</TableHead>
                          <TableHead>Calibration</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {d.departmentBreakdown.map((row) => (
                          <TableRow key={row.organizationUnitId}>
                            <TableCell className="font-medium">{row.departmentName}</TableCell>
                            <TableCell>{row.managerName ?? '—'}</TableCell>
                            <TableCell className="text-right tabular-nums">
                              {row.totalAppraisals}
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {row.completionPercent}%
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {row.overdueCount > 0 ? (
                                <span className="font-medium text-amber-600 dark:text-amber-500">
                                  {row.overdueCount}
                                </span>
                              ) : (
                                0
                              )}
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {row.averageScore != null ? row.averageScore.toFixed(1) : '—'}
                            </TableCell>
                            <TableCell>
                              {row.hasCalibrationSession ? (
                                <StatusBadge
                                  status={humanizeEnum(row.calibrationSessionStatus ?? 'Pending')}
                                />
                              ) : (
                                <span className="text-sm text-muted-foreground">
                                  {d.hasCalibration ? 'No session' : '—'}
                                </span>
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

            {/* ── Outcomes ─────────────────────────────────────────────────── */}
            <TabsContent value="outcomes" className="space-y-4 pt-4">
              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="text-base">What managers asked for</CardTitle>
                  <CardDescription>
                    Boxes ticked on the appraisal form. Intent — not a record that anything
                    happened.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <MetricTiles
                    tiles={[
                      { label: 'Award', value: d.recommendations.awardCount },
                      { label: 'Promotion', value: d.recommendations.promotionCount },
                      { label: 'Increment', value: d.recommendations.incrementCount },
                      { label: 'Training', value: d.recommendations.trainingCount },
                      {
                        label: 'Improvement plan',
                        value: d.recommendations.pipCount,
                        tone: d.recommendations.pipCount > 0 ? 'warning' : 'default',
                      },
                      {
                        label: 'Termination',
                        value: d.recommendations.terminationCount,
                        tone: d.recommendations.terminationCount > 0 ? 'danger' : 'default',
                      },
                    ]}
                    className="lg:grid-cols-6"
                  />
                </CardContent>
              </Card>

              {d.outcomePipeline.awaitingApproval > 0 && (
                <Alert>
                  <BellRing className="h-4 w-4" />
                  <AlertTitle>
                    {d.outcomePipeline.awaitingApproval} outcome
                    {d.outcomePipeline.awaitingApproval === 1 ? '' : 's'} are out for approval
                  </AlertTitle>
                  <AlertDescription>
                    These sit on the workflow engine and go nowhere until an approver acts. The
                    cycle can read as finished while they are still queued.
                  </AlertDescription>
                </Alert>
              )}

              <div className="grid gap-4 lg:grid-cols-2">
                <OutcomeStream
                  title="Recommendations"
                  description="Raised against this cycle's appraisals. Approving one dispatches it to the module that owns the outcome — Approved rather than Actioned means that dispatch failed."
                  counts={d.outcomePipeline.recommendations}
                  href="/hr/performance/recommendations"
                  linkLabel="Open the worklist"
                />
                <OutcomeStream
                  title="Salary review proposals"
                  description="Merit increases and bonuses on their way to payroll."
                  counts={d.outcomePipeline.salaryProposals}
                  href="/hr/performance/proposals"
                  linkLabel="Open proposals"
                />
                <OutcomeStream
                  title="Employment action proposals"
                  description="Promotions, demotions, renewals, recognitions and terminations."
                  counts={d.outcomePipeline.employmentActionProposals}
                  href="/hr/performance/proposals"
                  linkLabel="Open proposals"
                />
                <OutcomeStream
                  title="Improvement plans"
                  description="Plans raised from this cycle's appraisals. A plan is only in force once it has been approved."
                  counts={d.outcomePipeline.improvementPlans}
                  href="/hr/performance/pip"
                  linkLabel="Open improvement plans"
                />
              </div>

              {nonZero(d.outcomePipeline.recommendationTypes).length > 0 && (
                <Card>
                  <CardHeader className="pb-3">
                    <CardTitle className="text-base">Recommendations by type</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <DistributionBars
                      data={nonZero(d.outcomePipeline.recommendationTypes).map((t) => ({
                        label: t.label,
                        count: t.count,
                      }))}
                      valueName="Recommendations"
                      emptyTitle="None raised"
                      emptyDescription=""
                    />
                  </CardContent>
                </Card>
              )}
            </TabsContent>

            {/* ── Attention ────────────────────────────────────────────────── */}
            <TabsContent value="attention" className="space-y-4 pt-4">
              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="text-base">Needs attention</CardTitle>
                  <CardDescription>
                    Most severe first, capped at 50. One appraisal can appear more than once — a
                    low score that is also overdue is two different problems.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {d.attentionItems.length === 0 ? (
                    <EmptyState
                      title="Nothing needs attention"
                      description="No overdue steps, low scores, appeals or PIP/termination recommendations in this cycle."
                    />
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Employee</TableHead>
                          <TableHead>Unit</TableHead>
                          <TableHead>Manager</TableHead>
                          <TableHead>Reason</TableHead>
                          <TableHead>Stuck at</TableHead>
                          <TableHead className="text-right">Score</TableHead>
                          <TableHead className="text-right">Action</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {d.attentionItems.map((item: CycleAttentionItem, i) => (
                          <TableRow key={`${item.appraisalId}-${item.reason}-${i}`}>
                            <TableCell>
                              <Link
                                href={`/hr/performance/hr-review/${item.appraisalId}`}
                                className="font-medium hover:underline"
                              >
                                {item.employeeName}
                              </Link>
                              {item.position && (
                                <p className="text-xs text-muted-foreground">{item.position}</p>
                              )}
                            </TableCell>
                            <TableCell>{item.department || '—'}</TableCell>
                            <TableCell>{item.managerName || '—'}</TableCell>
                            <TableCell>
                              <StatusBadge status={humanizeEnum(item.severity)} />
                              <p className="mt-1 text-xs text-muted-foreground">
                                {item.reasonLabel}
                                {item.detail ? ` — ${item.detail}` : ''}
                              </p>
                            </TableCell>
                            <TableCell className="text-sm">
                              {humanizeEnum(item.currentSubStatus)}
                            </TableCell>
                            <TableCell className="text-right tabular-nums">
                              {item.overallScore != null ? item.overallScore.toFixed(1) : '—'}
                              {item.gradeLabel && (
                                <p className="text-xs text-muted-foreground">{item.gradeLabel}</p>
                              )}
                            </TableCell>
                            <TableCell className="text-right">
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => nudge.mutate(item.appraisalId)}
                                disabled={nudge.isPending}
                              >
                                <BellRing className="mr-2 h-3 w-3" />
                                Nudge
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

            {/* ── Activity ─────────────────────────────────────────────────── */}
            <TabsContent value="activity" className="space-y-4 pt-4">
              <Card>
                <CardHeader className="pb-3">
                  <CardTitle className="flex items-center gap-2 text-base">
                    <Activity className="h-4 w-4" />
                    Recent activity
                  </CardTitle>
                  <CardDescription>
                    Manual pipeline advances, acknowledgments, and appeals filed or resolved —
                    newest first, capped at 30.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {d.recentActivity.length === 0 ? (
                    <EmptyState
                      title="Nothing has happened yet"
                      description="Activity appears once appraisals start being acknowledged, advanced or appealed."
                    />
                  ) : (
                    <ul className="space-y-3">
                      {d.recentActivity.map((a, i) => (
                        <li key={`${a.timestamp}-${i}`} className="flex gap-3 text-sm">
                          <span className="w-24 shrink-0 text-xs text-muted-foreground">
                            {a.timeAgo}
                          </span>
                          <div className="min-w-0">
                            <p>{a.description}</p>
                            <p className="text-xs text-muted-foreground">
                              {a.subjectEmployeeName ?? '—'}
                              {a.actorName ? ` · by ${a.actorName}` : ''}
                            </p>
                          </div>
                        </li>
                      ))}
                    </ul>
                  )}
                </CardContent>
              </Card>
            </TabsContent>

            {/* ── Employee trend ───────────────────────────────────────────── */}
            <TabsContent value="trend" className="space-y-4 pt-4">
              <EmployeeTrendPanel />
            </TabsContent>
          </Tabs>
        </>
      )}

      {!cycleId && !dashboard.isLoading && (
        <EmptyState
          title="Pick a cycle"
          description="Every figure on this screen is scoped to one appraisal cycle."
        />
      )}

    </div>
  );
}
