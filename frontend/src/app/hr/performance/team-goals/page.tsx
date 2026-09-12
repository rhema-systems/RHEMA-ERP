'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQueries, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  ClipboardCheck,
  Gauge,
  Lock,
  Scale,
  TriangleAlert,
  Users,
  XCircle,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { TeamGoalTable } from '@/components/hr/performance/TeamGoalTable';
import { employeeGoalService, teamGoalsService } from '@/services/hr/goals.service';
import { formatPercent, humanizeEnum } from '@/lib/hr/attendance-format';
import type { TeamGoalFlat } from '@/types/hr/goals';

/**
 * The manager's goal workspace: everything they have to act on for their direct reports in
 * one cycle.
 *
 * Scope is entirely server-side — every read here is narrowed to the calling manager's
 * direct reports, so there is no employee filter and nothing to leak. A 401 from these
 * endpoints means the caller has no direct reports in this cycle, not an expired session,
 * which is why an empty state rather than a redirect is the right response.
 *
 * Governance and execution are kept apart on the overview tab, matching the backend's own
 * split: whether the goal set is structurally complete is a different question from whether
 * the goals are going well, and a manager needs the first answered before the second matters.
 */
const GOVERNANCE_HINT: Record<string, string> = {
  NotStarted: 'No goals written yet',
  InProgress: 'Goals still in draft or returned',
  AwaitingApproval: 'Waiting on your approval',
  InvalidWeight: 'Weights do not total 100%',
  StructurallyComplete: 'Set is complete',
};

export default function TeamGoalsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [cycleId, setCycleId] = useState('');
  const [decision, setDecision] = useState<{ row: TeamGoalFlat; action: 'approve' | 'reject' } | null>(
    null,
  );
  const [feedback, setFeedback] = useState('');

  // One query per tab, all fired together: the tabs are cheap reads and a manager flips
  // between them constantly, so paying for all five up front beats a spinner on every click.
  const results = useQueries({
    queries: [
      { key: 'overview', fn: () => teamGoalsService.getOverview(cycleId) },
      { key: 'awaiting', fn: () => teamGoalsService.getAwaitingApproval(cycleId) },
      { key: 'at-risk', fn: () => teamGoalsService.getAtRisk(cycleId) },
      { key: 'overdue', fn: () => teamGoalsService.getOverdue(cycleId) },
      { key: 'locked', fn: () => teamGoalsService.getLocked(cycleId) },
      { key: 'progress', fn: () => teamGoalsService.getProgress(cycleId) },
    ].map(({ key, fn }) => ({
      queryKey: ['hr', 'team-goals', cycleId, key],
      queryFn: fn,
      enabled: !!cycleId,
      retry: false,
    })),
  });

  const [overview, awaiting, atRisk, overdue, locked, progress] = results;

  const overviewRows = (overview.data ?? []) as any[];
  const awaitingRows = (awaiting.data ?? []) as TeamGoalFlat[];
  const atRiskRows = (atRisk.data ?? []) as TeamGoalFlat[];
  const overdueRows = (overdue.data ?? []) as TeamGoalFlat[];
  const lockedRows = (locked.data ?? []) as TeamGoalFlat[];
  const progressRows = (progress.data ?? []) as any[];

  // Every read is manager-scoped, so a rejection on the first one means "no direct reports".
  const noDirectReports = overview.isError;

  const unbalanced = overviewRows.filter((r) => r.totalGoals > 0 && !r.isWeightBalanced).length;
  const notStarted = overviewRows.filter((r) => r.totalGoals === 0).length;

  const decide = useMutation({
    mutationFn: async ({ row, action }: { row: TeamGoalFlat; action: 'approve' | 'reject' }) =>
      action === 'approve'
        ? employeeGoalService.approve(row.goalId, feedback.trim() || null)
        : employeeGoalService.reject(row.goalId, feedback.trim()),
    onSuccess: async (_d, variables) => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'team-goals'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'employee-goals'] });
      setDecision(null);
      setFeedback('');
      toast({
        title: variables.action === 'approve' ? 'Approved' : 'Returned',
        description: `“${variables.row.title}” — ${variables.row.employeeName}.`,
      });
    },
    onError: (e: any) => {
      setDecision(null);
      toast({
        title: 'Could not complete that',
        description: e?.message || 'The request was refused.',
        variant: 'destructive',
      });
    },
  });

  const rowActions = (row: TeamGoalFlat) => (
    <div className="flex gap-2">
      <Button
        size="sm"
        onClick={() => {
          setFeedback('');
          setDecision({ row, action: 'approve' });
        }}
      >
        <CheckCircle2 className="mr-1 h-4 w-4" />
        Approve
      </Button>
      <Button
        size="sm"
        variant="outline"
        onClick={() => {
          setFeedback('');
          setDecision({ row, action: 'reject' });
        }}
      >
        <XCircle className="mr-1 h-4 w-4" />
        Reject
      </Button>
    </div>
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Team Goals"
        description="Your direct reports' goals: what needs approving, what is slipping, and whether each set is complete."
        backHref="/hr/performance"
      />

      <CycleSelect value={cycleId} onChange={setCycleId} />

      {!cycleId ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={Users}
              title="No appraisal cycle selected"
              description="Team goals are shown one cycle at a time."
            />
          </CardContent>
        </Card>
      ) : noDirectReports ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={Users}
              title="No direct reports in this cycle"
              description="This workspace is scoped to the people who report to you. If that looks wrong, check the reporting line on their HR records."
            />
          </CardContent>
        </Card>
      ) : (
        <>
          <MetricTiles
            tiles={[
              { label: 'Direct reports', value: overviewRows.length, icon: Users },
              {
                label: 'Awaiting your approval',
                value: awaitingRows.length,
                hint: 'Goals submitted to you',
                icon: ClipboardCheck,
                tone: awaitingRows.length > 0 ? 'warning' : 'default',
              },
              {
                label: 'At risk',
                value: atRiskRows.length,
                hint: `${overdueRows.length} already overdue`,
                icon: TriangleAlert,
                tone: atRiskRows.length > 0 ? 'danger' : 'default',
              },
              {
                label: 'Unbalanced weights',
                value: unbalanced,
                hint: notStarted > 0 ? `${notStarted} with no goals at all` : 'Should total 100%',
                icon: Scale,
                tone: unbalanced > 0 ? 'warning' : 'default',
              },
            ]}
          />

          {notStarted > 0 && (
            <Alert>
              <Users className="h-4 w-4" />
              <AlertTitle>
                {notStarted} report{notStarted === 1 ? ' has' : 's have'} no goals for this cycle
              </AlertTitle>
              <AlertDescription>
                Goal setting has not started for them. They appear on the overview tab as “Not
                started”.
              </AlertDescription>
            </Alert>
          )}

          <Tabs defaultValue="overview">
            <TabsList className="flex-wrap">
              <TabsTrigger value="overview">Overview</TabsTrigger>
              <TabsTrigger value="awaiting">
                Awaiting approval
                {awaitingRows.length > 0 && (
                  <Badge variant="secondary" className="ml-2">
                    {awaitingRows.length}
                  </Badge>
                )}
              </TabsTrigger>
              <TabsTrigger value="at-risk">
                At risk
                {atRiskRows.length > 0 && (
                  <Badge variant="destructive" className="ml-2">
                    {atRiskRows.length}
                  </Badge>
                )}
              </TabsTrigger>
              <TabsTrigger value="overdue">
                Overdue
                {overdueRows.length > 0 && (
                  <Badge variant="destructive" className="ml-2">
                    {overdueRows.length}
                  </Badge>
                )}
              </TabsTrigger>
              <TabsTrigger value="progress">Progress</TabsTrigger>
              <TabsTrigger value="locked">Locked</TabsTrigger>
            </TabsList>

            <TabsContent value="overview" className="mt-4">
              <Card>
                <CardContent className="p-0">
                  {overviewRows.length === 0 ? (
                    <EmptyState
                      icon={Users}
                      title="Nothing to show"
                      description="No direct reports have goals in this cycle."
                    />
                  ) : (
                    <div className="overflow-x-auto">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Employee</TableHead>
                            <TableHead>Governance</TableHead>
                            <TableHead className="text-right">Goals</TableHead>
                            <TableHead className="text-right">Draft</TableHead>
                            <TableHead className="text-right">Pending</TableHead>
                            <TableHead className="text-right">Approved</TableHead>
                            <TableHead className="text-right">At risk</TableHead>
                            <TableHead className="text-right">Overdue</TableHead>
                            <TableHead className="text-right">Weight</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {overviewRows.map((r) => (
                            <TableRow key={r.employeeId}>
                              <TableCell className="font-medium">
                                <Link
                                  href={`/hr/performance/employee-goals?employeeId=${r.employeeId}`}
                                  className="hover:underline"
                                >
                                  {r.employeeName}
                                </Link>
                              </TableCell>
                              <TableCell>
                                <StatusBadge status={humanizeEnum(r.governanceStatus)} />
                                <p className="mt-0.5 text-xs text-muted-foreground">
                                  {GOVERNANCE_HINT[r.governanceStatus] ?? ''}
                                </p>
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.totalGoals}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.draftCount}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.pendingApprovalCount}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.approvedWorkflowCount}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.atRiskCount > 0 ? (
                                  <span className="text-red-600 dark:text-red-500">
                                    {r.atRiskCount}
                                  </span>
                                ) : (
                                  r.atRiskCount
                                )}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.overdueCount > 0 ? (
                                  <span className="text-red-600 dark:text-red-500">
                                    {r.overdueCount}
                                  </span>
                                ) : (
                                  r.overdueCount
                                )}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                <span
                                  className={
                                    r.totalGoals > 0 && !r.isWeightBalanced
                                      ? 'text-amber-600 dark:text-amber-500'
                                      : undefined
                                  }
                                >
                                  {r.totalWeight}%
                                </span>
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

            <TabsContent value="awaiting" className="mt-4">
              <Card>
                <CardContent className="p-0">
                  <TeamGoalTable
                    rows={awaitingRows}
                    isLoading={awaiting.isLoading}
                    timing="pending"
                    emptyIcon={ClipboardCheck}
                    emptyTitle="Nothing waiting on you"
                    emptyDescription="No goal from your reports is pending approval in this cycle."
                    actions={rowActions}
                  />
                </CardContent>
              </Card>
            </TabsContent>

            <TabsContent value="at-risk" className="mt-4">
              <Card>
                <CardContent className="p-0">
                  <TeamGoalTable
                    rows={atRiskRows}
                    isLoading={atRisk.isLoading}
                    timing="due"
                    showRisk
                    emptyIcon={TriangleAlert}
                    emptyTitle="Nothing flagged at risk"
                    emptyDescription="No goal trips the current risk thresholds. HR can tune them under Performance setup."
                  />
                </CardContent>
              </Card>
            </TabsContent>

            <TabsContent value="overdue" className="mt-4">
              <Card>
                <CardContent className="p-0">
                  <TeamGoalTable
                    rows={overdueRows}
                    isLoading={overdue.isLoading}
                    timing="overdue"
                    emptyIcon={CheckCircle2}
                    emptyTitle="Nothing overdue"
                    emptyDescription="Every live goal is still inside its due date."
                  />
                </CardContent>
              </Card>
            </TabsContent>

            <TabsContent value="progress" className="mt-4">
              <Card>
                <CardContent className="p-0">
                  {progressRows.length === 0 ? (
                    <EmptyState
                      icon={Gauge}
                      title="No progress to show"
                      description="Progress appears once goals are approved and entries are recorded."
                    />
                  ) : (
                    <div className="overflow-x-auto">
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Employee</TableHead>
                            <TableHead className="w-[200px]">Average progress</TableHead>
                            <TableHead className="text-right">Goals</TableHead>
                            <TableHead className="text-right">Not started</TableHead>
                            <TableHead className="text-right">On track</TableHead>
                            <TableHead className="text-right">At risk</TableHead>
                            <TableHead className="text-right">Completed</TableHead>
                            <TableHead className="text-right">Overdue</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {progressRows.map((r) => (
                            <TableRow key={r.employeeId}>
                              <TableCell className="font-medium">{r.employeeName}</TableCell>
                              <TableCell>
                                <div className="flex items-center gap-2">
                                  <Progress
                                    value={Number(r.averageProgressPercent)}
                                    className="h-2"
                                  />
                                  <span className="w-12 shrink-0 text-right text-xs tabular-nums">
                                    {formatPercent(r.averageProgressPercent)}
                                  </span>
                                </div>
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.totalGoals}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.notStartedCount}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.onTrackCount}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.atRiskCount}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.completedCount}
                              </TableCell>
                              <TableCell className="text-right tabular-nums">
                                {r.overdueCount}
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

            <TabsContent value="locked" className="mt-4">
              <Card>
                <CardContent className="p-0">
                  <TeamGoalTable
                    rows={lockedRows}
                    isLoading={locked.isLoading}
                    timing="locked"
                    emptyIcon={Lock}
                    emptyTitle="Nothing locked"
                    emptyDescription="Locking a goal freezes it at the end of a cycle. None have been locked yet."
                  />
                </CardContent>
              </Card>
            </TabsContent>
          </Tabs>
        </>
      )}

      <ConfirmationDialog
        open={decision !== null}
        onOpenChange={(open) => {
          if (!open) {
            setDecision(null);
            setFeedback('');
          }
        }}
        title={decision?.action === 'reject' ? 'Reject this goal?' : 'Approve this goal?'}
        description={
          decision
            ? `“${decision.row.title}” — ${decision.row.employeeName}, ${decision.row.weight}% of their appraisal.`
            : ''
        }
        confirmText={decision?.action === 'reject' ? 'Reject' : 'Approve'}
        variant={decision?.action === 'reject' ? 'destructive' : 'default'}
        // The API refuses a rejection without feedback, so the button waits for one.
        confirmDisabled={decision?.action === 'reject' && feedback.trim().length === 0}
        isLoading={decide.isPending}
        onConfirm={async () => {
          if (decision) await decide.mutateAsync(decision);
        }}
      >
        <div className="space-y-2">
          <Label htmlFor="teamFeedback">
            Feedback
            {decision?.action === 'reject' && <span className="ml-0.5 text-red-500">*</span>}
          </Label>
          <Textarea
            id="teamFeedback"
            rows={4}
            value={feedback}
            onChange={(e) => setFeedback(e.target.value)}
            placeholder={
              decision?.action === 'reject'
                ? 'Required — the employee sees this and revises against it.'
                : 'Optional comment, stored against the goal.'
            }
          />
        </div>
      </ConfirmationDialog>
    </div>
  );
}
