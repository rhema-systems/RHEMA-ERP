'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, TriangleAlert } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate, formatDateTime } from '@/lib/hr/attendance-format';
import { recruitmentDashboardService } from '@/services/hr/recruitment-dashboard.service';

/**
 * Recruitment KPIs — one aggregated read (`api/recruitment-dashboard`), HR-gated because the
 * payload carries candidate names on recent applications, upcoming interviews, offers and hires.
 *
 * The landing page's metric tiles stay as they are (cheap, separate calls) — this is the fuller
 * picture: pipeline shape, what needs attention this week, and what's arriving.
 */
export default function RecruitmentDashboardPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'recruitment-dashboard'],
    queryFn: () => recruitmentDashboardService.get(),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="p-6">
        <EmptyState title="Could not load the dashboard" description="Try again shortly." />
      </div>
    );
  }

  const maxStage = Math.max(1, ...data.pipelineStages.map((s) => s.count));

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Recruitment dashboard"
        description="Pipeline shape, what needs attention this week, and what's arriving."
        backHref="/hr/recruitment"
      />

      <MetricTiles
        tiles={[
          { label: 'Active vacancies', value: data.summary.totalVacancies },
          { label: 'Active applications', value: data.summary.activeApplications },
          { label: 'Interviews today', value: data.summary.interviewsScheduled },
          { label: 'Offers pending response', value: data.summary.offersMade },
        ]}
      />

      {data.slaAlerts.length > 0 && (
        <Card className="border-amber-300 dark:border-amber-800">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base text-amber-700 dark:text-amber-500">
              <TriangleAlert className="h-4 w-4" /> Shortlisting SLA breached
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-1.5">
            {data.slaAlerts.map((a) => (
              <div key={a.vacancyId} className="flex items-center justify-between text-sm">
                <Link href={`/hr/recruitment/vacancies/${a.vacancyId}`} className="hover:underline">
                  {a.jobTitle} ({a.vacancyNumber})
                </Link>
                <span className="text-muted-foreground">
                  Deadline was {formatDate(a.applicationDeadline)}
                </span>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Pipeline</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {data.pipelineStages.map((s) => (
            <div key={s.stageName} className="space-y-1">
              <div className="flex items-center justify-between text-sm">
                <span>{s.stageName}</span>
                <span className="tabular-nums text-muted-foreground">{s.count}</span>
              </div>
              <Progress value={(s.count / maxStage) * 100} />
            </div>
          ))}
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Recent applications</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {data.recentApplications.length === 0 ? (
              <div className="p-6">
                <EmptyState title="Nothing yet" description="Applications appear here as they arrive." />
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Candidate</TableHead>
                    <TableHead>Vacancy</TableHead>
                    <TableHead>Stage</TableHead>
                    <TableHead>Applied</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.recentApplications.map((a) => (
                    <TableRow key={a.applicationId}>
                      <TableCell>
                        <Link
                          href={`/hr/recruitment/applications/${a.applicationId}`}
                          className="hover:underline"
                        >
                          {a.candidateName}
                        </Link>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">{a.vacancyTitle}</TableCell>
                      <TableCell>
                        <StatusBadge status={a.stageName} />
                      </TableCell>
                      <TableCell className="whitespace-nowrap text-sm">{formatDate(a.dateApplied)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Upcoming interviews (next 7 days)</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {data.upcomingInterviews.length === 0 ? (
              <div className="p-6">
                <EmptyState title="Nothing scheduled" description="Nothing in the next 7 days." />
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Vacancy</TableHead>
                    <TableHead>Round</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>When</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.upcomingInterviews.map((i) => (
                    <TableRow key={i.interviewId}>
                      <TableCell>
                        <Link href={`/hr/recruitment/interviews/${i.interviewId}`} className="hover:underline">
                          {i.vacancyTitle}
                        </Link>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        Round {i.round} · {i.candidateCount} candidate{i.candidateCount === 1 ? '' : 's'}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={i.interviewType} />
                      </TableCell>
                      <TableCell className="whitespace-nowrap text-sm">
                        {formatDateTime(i.interviewDateTime)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Hires starting soon (14 days)</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {data.hiresStartingSoon.length === 0 ? (
              <div className="p-6">
                <EmptyState title="Nothing yet" description="Nothing starting in the next 14 days." />
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Candidate</TableHead>
                    <TableHead>Position</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Starts</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.hiresStartingSoon.map((h) => (
                    <TableRow key={h.hireId}>
                      <TableCell>
                        <Link href={`/hr/recruitment/hires/${h.hireId}`} className="hover:underline">
                          {h.candidateName}
                        </Link>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">{h.positionTitle}</TableCell>
                      <TableCell>
                        <StatusBadge status={h.statusName} />
                      </TableCell>
                      <TableCell className="whitespace-nowrap text-sm">
                        {formatDate(h.expectedStartDate)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Offers expiring soon (7 days)</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {data.expiringOffers.length === 0 ? (
              <div className="p-6">
                <EmptyState title="Nothing expiring" description="No offer expires within 7 days." />
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Candidate</TableHead>
                    <TableHead>Position</TableHead>
                    <TableHead>Expires</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.expiringOffers.map((o) => (
                    <TableRow key={o.offerId}>
                      <TableCell>
                        <Link href={`/hr/recruitment/offers/${o.offerId}`} className="hover:underline">
                          {o.candidateName}
                        </Link>
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">{o.positionTitle}</TableCell>
                      <TableCell className="whitespace-nowrap text-sm">{formatDate(o.expiryDate)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Vacancies with a deadline approaching (7 days)</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {data.deadlineApproachingVacancies.length === 0 ? (
            <div className="p-6">
              <EmptyState title="Nothing approaching" description="No open vacancy closes within 7 days." />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Vacancy</TableHead>
                  <TableHead>Recruiter</TableHead>
                  <TableHead className="text-right">Applications</TableHead>
                  <TableHead>Deadline</TableHead>
                  <TableHead className="text-right">Days left</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.deadlineApproachingVacancies.map((v) => (
                  <TableRow key={v.vacancyId}>
                    <TableCell>
                      <Link href={`/hr/recruitment/vacancies/${v.vacancyId}`} className="hover:underline">
                        {v.jobTitle} ({v.vacancyNumber})
                      </Link>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">{v.recruiterName ?? '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">{v.applicationCount}</TableCell>
                    <TableCell className="whitespace-nowrap text-sm">
                      {formatDate(v.applicationDeadline)}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{v.daysRemaining}</TableCell>
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
