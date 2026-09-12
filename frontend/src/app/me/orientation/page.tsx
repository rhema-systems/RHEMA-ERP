'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { GraduationCap, ChevronRight, AlertTriangle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { employeeOrientationService } from '@/services/hr/employee-orientation.service';
import type { EmployeeOrientationSummary } from '@/types/hr/orientation';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/** Finished one way or another — nothing left for the participant to do. */
const isDone = (e: EmployeeOrientationSummary) =>
  e.completionStatus === 'Completed' || e.completionStatus === 'Exempted';

/**
 * The signed-in employee's own orientations.
 *
 * Reads `mine` rather than `employee/{id}`: there is no id to get wrong, and nothing another
 * employee's id could be substituted into.
 */
export default function MyOrientationPage() {
  const { data: enrollments = [], isLoading } = useQuery({
    queryKey: ['hr', 'orientation-enrollments', 'mine'],
    queryFn: () => employeeOrientationService.getMine(),
  });

  const outstanding = enrollments.filter((e) => !isDone(e));
  const overdue = enrollments.filter((e) => e.completionStatus === 'Overdue');
  const done = enrollments.filter(isDone);

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Orientation"
        description="Programmes you have been enrolled on — work through the content, sit the assessment, and sign off what needs signing."
        backHref="/me"
      />

      {isLoading ? (
        <p className="text-muted-foreground text-sm">Loading…</p>
      ) : enrollments.length === 0 ? (
        <EmptyState
          icon={GraduationCap}
          title="Nothing assigned to you"
          description="You have not been enrolled on any orientation programme. HR will assign one when there is something for you to do."
        />
      ) : (
        <>
          <MetricTiles
            tiles={[
              { label: 'Assigned', value: enrollments.length },
              { label: 'Outstanding', value: outstanding.length },
              {
                label: 'Overdue',
                value: overdue.length,
                tone: overdue.length > 0 ? 'danger' : 'default',
              },
              { label: 'Completed', value: done.length, tone: 'success' },
            ]}
          />

          {overdue.length > 0 && (
            <Card className="border-red-200 dark:border-red-900">
              <CardContent className="flex items-start gap-3 py-4">
                <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-red-600" />
                <div className="text-sm">
                  <p className="font-medium">
                    {overdue.length} of your programmes {overdue.length === 1 ? 'is' : 'are'} past
                    due
                  </p>
                  <p className="text-muted-foreground mt-0.5">
                    Finish {overdue.length === 1 ? 'it' : 'them'} to clear your record.
                  </p>
                </div>
              </CardContent>
            </Card>
          )}

          {outstanding.length > 0 && (
            <Section title="To do" items={outstanding} />
          )}
          {done.length > 0 && <Section title="Completed" items={done} />}
        </>
      )}
    </div>
  );
}

function Section({ title, items }: { title: string; items: EmployeeOrientationSummary[] }) {
  return (
    <div>
      <h2 className="text-muted-foreground mb-3 text-sm font-medium">
        {title} ({items.length})
      </h2>
      <div className="space-y-2">
        {items.map((e) => (
          <Link key={e.id} href={`/me/orientation/${e.id}`}>
            <Card className="transition-colors hover:bg-muted/50">
              <CardContent className="flex items-center gap-4 py-4">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium">{e.programTitle ?? 'Orientation'}</span>
                    <StatusBadge status={e.completionStatus} />
                    {e.enrollmentStatus === 'Waitlisted' && (
                      <Badge variant="outline">Waitlisted</Badge>
                    )}
                  </div>
                  <div className="text-muted-foreground mt-1 flex flex-wrap gap-x-3 text-xs">
                    {e.programCode && <span className="font-mono">{e.programCode}</span>}
                    {e.sessionTitle && <span>{e.sessionTitle}</span>}
                    <span>Enrolled {fmt(e.enrolledAt)}</span>
                    {e.nextDueDate && <span>Due {fmt(e.nextDueDate)}</span>}
                    {e.completedAt && <span>Completed {fmt(e.completedAt)}</span>}
                  </div>
                </div>

                <div className="hidden w-40 shrink-0 sm:block">
                  <Progress value={e.progressPercentage} className="h-2" />
                  <p className="text-muted-foreground mt-1 text-right text-xs tabular-nums">
                    {e.progressPercentage}%
                    {e.finalScore != null && ` · scored ${e.finalScore}%`}
                  </p>
                </div>

                <ChevronRight className="text-muted-foreground h-4 w-4 shrink-0" />
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}
