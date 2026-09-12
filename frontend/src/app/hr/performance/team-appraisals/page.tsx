'use client';

import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { CheckCircle2, ClipboardList, TriangleAlert, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { performanceAppraisalService } from '@/services/hr/appraisal-run.service';

/**
 * The manager's appraisal workspace: everyone reporting to you in a cycle, and how far each
 * one has got.
 *
 * Scoped by cycle rather than showing everything at once, because the questions a manager
 * actually asks are per cycle — "who still owes me a self-evaluation this year", "whose
 * evaluation have I not written yet". The cycle list only contains cycles you have someone in.
 *
 * The manager identity comes from the token; there is no "view as" here.
 */
export default function TeamAppraisalsPage() {
  const [cycleId, setCycleId] = useState('');

  const { data: cycles, isLoading: cyclesLoading, isError: cyclesError, error } = useQuery({
    queryKey: ['hr', 'team-appraisal-cycles'],
    queryFn: () => performanceAppraisalService.getMyTeamCycles(),
  });

  // Default to the first cycle so the page is useful on arrival rather than empty.
  useEffect(() => {
    if (!cycleId && cycles && cycles.length > 0) setCycleId(cycles[0].cycleId);
  }, [cycles, cycleId]);

  const { data: members, isLoading: membersLoading } = useQuery({
    queryKey: ['hr', 'team-member-appraisals', cycleId],
    queryFn: () => performanceAppraisalService.getMyTeamMembers(cycleId),
    enabled: !!cycleId,
  });

  const cycle = cycles?.find((c) => c.cycleId === cycleId);
  const rows = members ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Team Appraisals"
        description="Your reports' appraisals in a cycle — what they have submitted, and what is waiting on you."
        backHref="/hr/performance"
        actions={
          <Button variant="outline" asChild>
            {/* Check-ins are the manager's own self-service — they live in the portal now. */}
            <Link href="/me/performance/check-ins">Check-ins</Link>
          </Button>
        }
      />

      {cyclesError ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not load your team's cycles"
              description={
                (error as Error)?.message ??
                'Your account may not be linked to an employee record.'
              }
            />
          </CardContent>
        </Card>
      ) : cyclesLoading ? (
        <Skeleton className="h-24 w-full" />
      ) : (cycles ?? []).length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={Users}
              title="No team appraisals"
              description="Nobody reporting to you is covered by an open appraisal cycle yet."
            />
          </CardContent>
        </Card>
      ) : (
        <>
          <Card>
            <CardContent className="flex flex-wrap items-end gap-4 p-4">
              <div className="w-[320px] space-y-2">
                <Label htmlFor="cycle">Appraisal cycle</Label>
                <Select value={cycleId} onValueChange={setCycleId}>
                  <SelectTrigger id="cycle">
                    <SelectValue placeholder="Choose a cycle" />
                  </SelectTrigger>
                  <SelectContent>
                    {(cycles ?? []).map((c) => (
                      <SelectItem key={c.cycleId} value={c.cycleId}>
                        {c.cycleName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              {cycle && (
                <p className="pb-2 text-sm text-muted-foreground">
                  {formatDate(cycle.periodStart)} – {formatDate(cycle.periodEnd)} ·{' '}
                  {humanizeEnum(cycle.status)}
                </p>
              )}
            </CardContent>
          </Card>

          {cycle && (
            <MetricTiles
              tiles={[
                { label: 'Team members', value: cycle.totalEmployees, icon: Users },
                {
                  label: 'Evaluated',
                  value: cycle.evaluatedCount,
                  icon: CheckCircle2,
                  tone: 'success',
                },
                {
                  label: 'In progress',
                  value: cycle.inProgressCount,
                  icon: ClipboardList,
                },
                {
                  label: 'Not started',
                  value: cycle.pendingCount,
                  icon: TriangleAlert,
                  tone: cycle.pendingCount > 0 ? 'warning' : 'default',
                },
              ]}
            />
          )}

          <Card>
            <CardContent className="p-0">
              {membersLoading ? (
                <div className="space-y-2 p-4">
                  {[0, 1, 2].map((i) => (
                    <Skeleton key={i} className="h-12 w-full" />
                  ))}
                </div>
              ) : rows.length === 0 ? (
                <EmptyState
                  icon={Users}
                  title="Nobody in this cycle"
                  description="No appraisal records exist for your reports in this cycle yet."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Self-evaluation</TableHead>
                      <TableHead>My evaluation</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Score</TableHead>
                      <TableHead className="w-28" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rows.map((row) => (
                      <TableRow key={row.appraisalId}>
                        <TableCell>
                          <div className="font-medium">{row.employeeName}</div>
                          <div className="text-xs text-muted-foreground">
                            {row.employeeNumber}
                            {row.position ? ` · ${row.position}` : ''}
                          </div>
                        </TableCell>
                        <TableCell className="text-sm">
                          {!row.requireSelfEvaluation ? (
                            <span className="text-muted-foreground">Not required</span>
                          ) : row.selfEvaluationSubmitted ? (
                            <span className="text-emerald-600 dark:text-emerald-500">
                              {formatDate(row.selfEvaluationSubmittedDate)}
                            </span>
                          ) : (
                            <span className="text-amber-600 dark:text-amber-500">Outstanding</span>
                          )}
                        </TableCell>
                        <TableCell className="text-sm">
                          {row.managerEvaluationSubmitted ? (
                            <span className="text-emerald-600 dark:text-emerald-500">
                              {formatDate(row.managerEvaluationSubmittedDate)}
                            </span>
                          ) : row.managerEvaluationStarted ? (
                            <span className="text-muted-foreground">Draft saved</span>
                          ) : (
                            <span className="text-amber-600 dark:text-amber-500">Not started</span>
                          )}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={humanizeEnum(row.appraisalStatus)} />
                          {row.isRemandedAppeal && (
                            <div className="mt-1 text-xs font-medium text-red-600 dark:text-red-500">
                              Appeal remand
                              {row.appealRemandDeadline &&
                                ` · by ${formatDate(row.appealRemandDeadline)}`}
                            </div>
                          )}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {row.overallScore != null ? Number(row.overallScore).toFixed(1) : '—'}
                        </TableCell>
                        <TableCell className="text-right">
                          <Button variant="ghost" size="sm" asChild>
                            <Link href={`/hr/performance/team-appraisals/${row.appraisalId}`}>
                              {row.managerEvaluationSubmitted ? 'View' : 'Evaluate'}
                            </Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
