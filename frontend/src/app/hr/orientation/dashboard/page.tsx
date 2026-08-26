'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  Loader2,
  BookOpen,
  Users,
  AlertTriangle,
  CalendarClock,
  Award,
  CheckCircle2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { orientationDashboardService } from '@/services/hr/orientation-lookup.service';
import {
  ORIENTATION_PROGRAM_STATUS_OPTIONS,
  ORIENTATION_COMPLETION_STATUS_OPTIONS,
} from '@/types/hr/orientation';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtWhen = (v?: string | null) =>
  v ? new Date(v).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '—';

const programStatusLabel = (v: string) =>
  ORIENTATION_PROGRAM_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const completionStatusLabel = (v: string) =>
  ORIENTATION_COMPLETION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

/**
 * The orientation roll-up.
 *
 * Everything here is computed server-side in one aggregate read, so this only formats it. The three
 * lists at the bottom are the ones that need somebody to act — people who are overdue, sessions
 * about to run, and certificates about to lapse.
 */
export default function OrientationDashboardPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'orientation-dashboard'],
    queryFn: () => orientationDashboardService.getDashboard(),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="p-6">
        <EmptyState
          title="Could not load the dashboard"
          description="Please try again in a moment."
        />
      </div>
    );
  }

  const maxProgramCount = Math.max(1, ...data.programsByStatus.map((s) => s.count));
  const maxCompletionCount = Math.max(
    1,
    ...data.enrollmentsByCompletionStatus.map((s) => s.count),
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Orientation Dashboard"
        description="Completion across the induction catalogue, plus what needs chasing."
        backHref="/hr/orientation"
        actions={
          <Button asChild variant="outline">
            <Link href="/hr/orientation/enrollments">View enrollments</Link>
          </Button>
        }
      />

      <MetricTiles
        tiles={[
          {
            label: 'Programmes',
            value: data.totalPrograms,
            icon: BookOpen,
            hint: `${data.activePrograms} active, ${data.draftPrograms} draft`,
          },
          {
            label: 'Enrollments',
            value: data.totalEnrollments,
            icon: Users,
            hint: `${data.inProgressEnrollments} in progress, ${data.notStartedEnrollments} not started`,
          },
          {
            label: 'Completion rate',
            value: `${Math.round(data.overallCompletionRate)}%`,
            icon: CheckCircle2,
            hint: `${data.completedEnrollments} completed`,
            tone: data.overallCompletionRate >= 80 ? 'success' : 'default',
          },
          {
            label: 'Overdue',
            value: data.overdueEnrollments,
            icon: AlertTriangle,
            tone: data.overdueEnrollments > 0 ? 'danger' : 'default',
          },
        ]}
      />

      <MetricTiles
        className="lg:grid-cols-2"
        tiles={[
          {
            label: 'Upcoming sessions',
            value: data.upcomingSessions,
            icon: CalendarClock,
            hint: 'Scheduled and still to run',
          },
          {
            label: 'Certificates expiring',
            value: data.expiringCertificates,
            icon: Award,
            tone: data.expiringCertificates > 0 ? 'warning' : 'default',
            hint: 'Holders will need to retake the programme',
          },
        ]}
      />

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Programmes by status</CardTitle>
            <CardDescription>Where the catalogue sits.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {data.programsByStatus.length === 0 ? (
              <p className="text-muted-foreground text-sm">No programmes yet.</p>
            ) : (
              data.programsByStatus.map((s) => (
                <div key={s.status} className="space-y-1">
                  <div className="flex items-center justify-between text-sm">
                    <span>{programStatusLabel(s.status)}</span>
                    <span className="text-muted-foreground tabular-nums">{s.count}</span>
                  </div>
                  <Progress value={(s.count / maxProgramCount) * 100} className="h-2" />
                </div>
              ))
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Enrollments by completion</CardTitle>
            <CardDescription>How far through people are.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {data.enrollmentsByCompletionStatus.length === 0 ? (
              <p className="text-muted-foreground text-sm">Nobody enrolled yet.</p>
            ) : (
              data.enrollmentsByCompletionStatus.map((s) => (
                <div key={s.status} className="space-y-1">
                  <div className="flex items-center justify-between text-sm">
                    <span>{completionStatusLabel(s.status)}</span>
                    <span className="text-muted-foreground tabular-nums">{s.count}</span>
                  </div>
                  <Progress value={(s.count / maxCompletionCount) * 100} className="h-2" />
                </div>
              ))
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Overdue participants</CardTitle>
          <CardDescription>
            Past their completion deadline. These are the ones to chase.
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {data.overdueList.length === 0 ? (
            <EmptyState
              icon={CheckCircle2}
              title="Nobody is overdue"
              description="Every enrollment is inside its deadline."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Participant</TableHead>
                  <TableHead>Programme</TableHead>
                  <TableHead className="text-right">Progress</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.overdueList.map((e) => (
                  <TableRow key={e.id}>
                    <TableCell className="font-medium">
                      <Link href={`/me/orientation/${e.id}`} className="hover:underline">
                        {e.employeeName ?? '—'}
                      </Link>
                      {e.employeeNumber && (
                        <div className="text-muted-foreground text-xs">{e.employeeNumber}</div>
                      )}
                    </TableCell>
                    <TableCell>{e.programTitle ?? '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {e.progressPercentage}%
                    </TableCell>
                    <TableCell className="text-red-600">{fmtDate(e.nextDueDate)}</TableCell>
                    <TableCell>
                      <StatusBadge status={e.completionStatus} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Upcoming sessions</CardTitle>
            <CardDescription>What is about to run.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {data.upcomingSessionList.length === 0 ? (
              <p className="text-muted-foreground text-sm">Nothing scheduled.</p>
            ) : (
              data.upcomingSessionList.map((s) => {
                const left =
                  s.maxParticipants == null
                    ? null
                    : Math.max(0, s.maxParticipants - s.enrolledCount);
                return (
                  <Link key={s.id} href={`/hr/orientation/sessions/${s.id}`}>
                    <div className="hover:bg-muted/50 flex items-center gap-3 rounded-md border p-3 transition-colors">
                      <div className="min-w-0 flex-1">
                        <p className="font-medium">{s.title}</p>
                        <p className="text-muted-foreground text-xs">
                          {s.programTitle ?? '—'} · {fmtWhen(s.scheduledStartAt)}
                        </p>
                      </div>
                      <div className="shrink-0 text-right">
                        <StatusBadge status={s.status} />
                        <p className="text-muted-foreground mt-1 text-xs">
                          {left === null ? 'Uncapped' : `${left} seat${left === 1 ? '' : 's'} left`}
                        </p>
                      </div>
                    </div>
                  </Link>
                );
              })
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Certificates expiring</CardTitle>
            <CardDescription>
              Holders will need to retake the programme to stay current.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {data.expiringCertificateList.length === 0 ? (
              <p className="text-muted-foreground text-sm">Nothing lapsing soon.</p>
            ) : (
              data.expiringCertificateList.map((c) => (
                <div
                  key={c.id}
                  className="flex items-center justify-between gap-3 rounded-md border p-3"
                >
                  <div className="min-w-0">
                    <p className="font-medium">{c.employeeName ?? '—'}</p>
                    <p className="text-muted-foreground text-xs">
                      {c.programTitle ?? '—'} ·{' '}
                      <span className="font-mono">{c.certificateNumber}</span>
                    </p>
                  </div>
                  <Badge variant="secondary" className="shrink-0">
                    Expires {fmtDate(c.expiresAt)}
                  </Badge>
                </div>
              ))
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
