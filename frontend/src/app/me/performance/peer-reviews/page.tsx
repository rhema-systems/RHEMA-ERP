'use client';

import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { MessagesSquare, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate } from '@/lib/hr/attendance-format';
import { peerEvaluationService } from '@/services/hr/appraisal-run.service';

/**
 * Peer feedback I owe on colleagues' appraisals.
 *
 * Separate from "My Appraisals" on purpose: these rows are about other people, and mixing them
 * into a list of your own results would put a colleague's appraisal under a heading that
 * implies it is yours.
 *
 * A row appears only once the nomination has been **approved** — approval is what creates the
 * evaluation record. A nomination still pending is invisible here, which is correct: there is
 * nothing to fill in yet.
 *
 * The due date is the nomination's, else the cycle's peer deadline, and the nominator's
 * instructions show under the colleague (performance closure D5) — the page showed the cycle's
 * deadline whatever the nomination said, and the instructions nowhere.
 */
export default function PeerReviewsPage() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'peer-evaluation-assignments'],
    queryFn: () => peerEvaluationService.getMyAssignments(),
  });

  const rows = data ?? [];
  const outstanding = rows.filter((r) => r.status !== 'Submitted');
  const today = new Date().toISOString().slice(0, 10);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Peer Reviews"
        description="Colleagues you have been asked to give feedback on, and the forms still waiting on you."
        backHref="/me"
        actions={
          <Button variant="outline" asChild>
            <Link href="/me/performance/appraisals">My own appraisals</Link>
          </Button>
        }
      />

      {outstanding.length > 0 && (
        <Card className="border-amber-500/40 bg-amber-50/60 dark:bg-amber-950/20">
          <CardContent className="p-4 text-sm">
            {outstanding.length === 1
              ? 'One peer review is waiting on you.'
              : `${outstanding.length} peer reviews are waiting on you.`}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load your peer reviews"
              description={
                (error as Error)?.message ??
                'Your account may not be linked to an employee record.'
              }
            />
          ) : isLoading ? (
            <div className="space-y-2 p-4">
              {[0, 1].map((i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={MessagesSquare}
              title="No peer reviews assigned"
              description="A colleague's appraisal appears here once you are nominated as their peer and the nomination is approved."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Colleague</TableHead>
                  <TableHead>Cycle</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => {
                  const overdue =
                    row.status !== 'Submitted' && !!row.dueDate && row.dueDate < today;
                  return (
                    <TableRow key={row.evaluationId}>
                      <TableCell>
                        <div className="font-medium">{row.appraiseeName}</div>
                        <div className="text-xs text-muted-foreground">
                          {row.appraiseePosition} · {row.appraiseeOrganizationUnit}
                        </div>
                        {row.instructionsToPeer && (
                          <div className="mt-1 max-w-md whitespace-pre-wrap text-xs text-muted-foreground">
                            Instructions: {row.instructionsToPeer}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {row.appraisalCycleName}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={row.status} />
                        {row.submittedDate && (
                          <div className="mt-1 text-xs text-muted-foreground">
                            {formatDate(row.submittedDate)}
                          </div>
                        )}
                      </TableCell>
                      <TableCell
                        className={
                          overdue
                            ? 'text-sm font-medium text-red-600 dark:text-red-500'
                            : 'text-sm text-muted-foreground'
                        }
                      >
                        {row.dueDate ? formatDate(row.dueDate) : '—'}
                        {overdue && ' · overdue'}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm" asChild>
                          <Link href={`/me/performance/peer-reviews/${row.evaluationId}`}>
                            {row.status === 'Submitted' ? 'View' : 'Give feedback'}
                          </Link>
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
