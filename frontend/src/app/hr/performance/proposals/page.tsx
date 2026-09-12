'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import {
  BadgeDollarSign,
  FileClock,
  Handshake,
  TriangleAlert,
  UserCog,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import {
  employmentActionProposalService,
  salaryReviewProposalService,
} from '@/services/hr/outcomes.service';

/**
 * The two intake queues an approved appraisal outcome feeds.
 *
 * Neither of these *is* the change. A salary review proposal is a note that says "this person
 * should get 4%"; payroll makes the change and the proposal is marked Applied. An employment
 * action proposal is a note that says "promote this person"; the promotion is created in the
 * module that owns it and the proposal is marked Actioned. Keeping them separate is the point —
 * it means an appraisal can recommend something without seizing the other module's data model.
 *
 * ⚠ **A merit increase with no percentage cannot be submitted.** The handler that raises these
 * only knows an appraisal called for a rise, not how much, so a figure has to be set first.
 * That is what "Needs a figure" counts.
 */
export default function ProposalsLandingPage() {
  const salary = useQuery({
    queryKey: ['hr', 'salary-review-proposals'],
    queryFn: () => salaryReviewProposalService.getAll(),
  });

  const action = useQuery({
    queryKey: ['hr', 'employment-action-proposals'],
    queryFn: () => employmentActionProposalService.getAll(),
  });

  const salaryRows = useMemo(() => salary.data ?? [], [salary.data]);
  const actionRows = useMemo(() => action.data ?? [], [action.data]);

  const needsFigure = useMemo(
    () =>
      salaryRows.filter(
        (p) =>
          p.status === 'Proposed' &&
          (p.proposalType === 'Bonus' ? !p.proposedAmount : !p.proposedPercent),
      ).length,
    [salaryRows],
  );

  const awaiting = useMemo(
    () =>
      salaryRows.filter((p) => p.status === 'PendingApproval').length +
      actionRows.filter((p) => p.status === 'PendingApproval').length,
    [salaryRows, actionRows],
  );

  const readyToFinish = useMemo(
    () =>
      salaryRows.filter((p) => p.status === 'Approved').length +
      actionRows.filter((p) => p.status === 'Approved').length,
    [salaryRows, actionRows],
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Proposals"
        description="Pay changes and employment actions raised from appraisal outcomes, on their way to the modules that own them."
        backHref="/hr/performance"
        actions={
          <Button variant="outline" asChild>
            <Link href="/hr/performance/recommendations">Recommendations</Link>
          </Button>
        }
      />

      <MetricTiles
        tiles={[
          {
            label: 'Needs a figure',
            value: needsFigure,
            hint: 'Cannot be submitted until priced',
            icon: BadgeDollarSign,
            tone: needsFigure > 0 ? 'warning' : 'default',
          },
          { label: 'Out for approval', value: awaiting, icon: FileClock },
          {
            label: 'Approved, not yet done',
            value: readyToFinish,
            hint: 'Waiting on payroll or the owning module',
            icon: Handshake,
            tone: readyToFinish > 0 ? 'warning' : 'default',
          },
          { label: 'Total', value: salaryRows.length + actionRows.length },
        ]}
      />

      <Alert>
        <TriangleAlert className="h-4 w-4" />
        <AlertTitle>Approval routing is configured, not hard-coded</AlertTitle>
        <AlertDescription>
          Both kinds of proposal run on the workflow engine, so who signs off a 3% merit increase
          need not be who signs off a termination. Nothing can be submitted until a workflow
          definition has been published for that entity type.
        </AlertDescription>
      </Alert>

      {/* ── Salary reviews ───────────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <BadgeDollarSign className="h-4 w-4" />
            Salary reviews ({salaryRows.length})
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {salary.isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load salary proposals"
              description={(salary.error as Error)?.message ?? 'Restricted to HR roles.'}
            />
          ) : salary.isLoading ? (
            <div className="space-y-2 p-4">
              {[0, 1].map((i) => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          ) : salaryRows.length === 0 ? (
            <EmptyState
              icon={BadgeDollarSign}
              title="No salary proposals"
              description="These appear when an appraisal recommendation for a merit increase or bonus is approved."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Kind</TableHead>
                  <TableHead className="text-right">Proposed</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {salaryRows.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>
                      <div className="font-medium">{p.employeeName ?? '—'}</div>
                      <div className="text-xs text-muted-foreground">{p.appraisalNumber}</div>
                    </TableCell>
                    <TableCell className="text-sm">{humanizeEnum(p.proposalType)}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {p.proposalType === 'Bonus'
                        ? p.proposedAmount != null
                          ? formatMoney(p.proposedAmount)
                          : missing()
                        : p.proposedPercent != null
                          ? `${p.proposedPercent}%`
                          : missing()}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={p.status} />
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/hr/performance/proposals/salary-review/${p.id}`}>Open</Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* ── Employment actions ───────────────────────────────────────────────── */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <UserCog className="h-4 w-4" />
            Employment actions ({actionRows.length})
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {action.isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load employment action proposals"
              description={(action.error as Error)?.message ?? 'Restricted to HR roles.'}
            />
          ) : action.isLoading ? (
            <div className="space-y-2 p-4">
              {[0, 1].map((i) => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          ) : actionRows.length === 0 ? (
            <EmptyState
              icon={UserCog}
              title="No employment action proposals"
              description="These appear when an appraisal recommendation for a promotion, demotion, renewal, termination or recognition is approved."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Action</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Notes</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {actionRows.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>
                      <div className="font-medium">{p.employeeName ?? '—'}</div>
                      <div className="text-xs text-muted-foreground">{p.appraisalNumber}</div>
                    </TableCell>
                    <TableCell className="text-sm">{humanizeEnum(p.actionType)}</TableCell>
                    <TableCell>
                      <StatusBadge status={p.status} />
                    </TableCell>
                    <TableCell className="max-w-xs text-sm text-muted-foreground">
                      {p.notes ?? '—'}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/hr/performance/proposals/employment-action/${p.id}`}>
                          Open
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
    </div>
  );
}

function missing() {
  return <span className="text-amber-600 dark:text-amber-500">Not set</span>;
}
