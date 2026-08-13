'use client';

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { GraduationCap, ClipboardList, Award, CalendarClock, ShieldAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import { trainingRequestService } from '@/services/hr/training-request.service';
import { trainingCompletionService } from '@/services/hr/training-completion.service';
import { trainingComplianceService } from '@/services/hr/training-compliance.service';
import { trainingAnalyticsService } from '@/services/hr/training-analytics.service';
import { COMPLIANCE_STATUS_OPTIONS } from '@/types/hr/training-compliance';
import {
  NOMINATION_STATUS_OPTIONS,
  TRAINING_REQUEST_STATUS_OPTIONS,
  TRAINING_COMPLETION_STATUS_OPTIONS,
} from '@/types/hr/training-delivery';

const nomLabel = (v: string) => NOMINATION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const reqLabel = (v: string) => TRAINING_REQUEST_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const compLabel = (v: string) =>
  TRAINING_COMPLETION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const complianceLabel = (v: string) =>
  COMPLIANCE_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v: string) => new Date(v).toLocaleDateString();

function Loading({ cols }: { cols: number }) {
  return (
    <>
      {[...Array(3)].map((_, i) => (
        <TableRow key={i}>
          {[...Array(cols)].map((__, j) => (
            <TableCell key={j}>
              <Skeleton className="h-4 w-[90px]" />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  );
}

/**
 * Self-service view. Every read here is token-derived (/mine) — the page never needs, and never
 * passes, an employee id, which is what keeps it from becoming a way to read someone else's record.
 */
export default function MyTrainingPage() {
  const router = useRouter();

  const { data: nominations, isLoading: loadingNoms } = useQuery({
    queryKey: ['hr', 'training', 'nominations', 'mine'],
    queryFn: () => trainingNominationService.getMine(),
  });
  const { data: requests, isLoading: loadingReqs } = useQuery({
    queryKey: ['hr', 'training', 'requests', 'mine'],
    queryFn: () => trainingRequestService.getMine(),
  });
  const { data: completions, isLoading: loadingComps } = useQuery({
    queryKey: ['hr', 'training', 'completions', 'mine'],
    queryFn: () => trainingCompletionService.getMine(),
  });
  const { data: compliance, isLoading: loadingCompliance } = useQuery({
    queryKey: ['hr', 'training', 'compliance', 'mine'],
    queryFn: () => trainingComplianceService.getMyRecords(),
  });
  // The record-level view of the same person: hours, certificates, paths and mentoring, which the
  // four lists above cannot see. Also token-derived.
  const { data: summary } = useQuery({
    queryKey: ['hr', 'training', 'summary', 'mine'],
    queryFn: () => trainingAnalyticsService.getMySummary(),
  });

  const noms = nominations ?? [];
  const reqs = requests ?? [];
  const comps = completions ?? [];
  const compliance_ = compliance ?? [];

  const tiles = useMemo(() => {
    const upcoming = noms.filter(
      (n) =>
        new Date(n.trainingStartDate) >= new Date() &&
        (n.status === 'Approved' || n.status === 'Confirmed'),
    ).length;
    const awaiting = noms.filter(
      (n) => n.status === 'Submitted' || n.status === 'SupervisorReview' || n.status === 'HrReview',
    ).length;
    const passed = comps.filter((c) => c.isPassed).length;
    return [
      { label: 'Upcoming', value: upcoming, icon: CalendarClock },
      { label: 'Awaiting approval', value: awaiting, tone: awaiting > 0 ? ('warning' as const) : undefined },
      {
        label: 'Mandatory outstanding',
        value: compliance_.filter((c) => c.status !== 'Compliant' && !c.isExempt).length,
        hint: compliance_.some((c) => c.isOverdue) ? 'Some are overdue' : undefined,
        icon: ShieldAlert,
        tone: compliance_.some((c) => c.isOverdue) ? ('danger' as const) : undefined,
      },
      { label: 'Completed', value: comps.length, icon: Award },
      {
        label: 'Passed',
        value: comps.length ? `${passed} of ${comps.length}` : '—',
      },
    ];
  }, [noms, comps, compliance_]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="My Training"
        description={
          summary
            ? `${summary.employeeName} · ${summary.employeeNumber}`
            : 'Your nominations, requests and completion records.'
        }
        backHref="/hr/training"
      />

      <MetricTiles tiles={tiles} />

      {summary && (
        <MetricTiles
          tiles={[
            { label: 'Training hours', value: summary.totalTrainingHours },
            {
              label: 'Active certificates',
              value: summary.activeCertificatesCount,
              hint:
                summary.expiringCertificatesCount > 0
                  ? `${summary.expiringCertificatesCount} expiring soon`
                  : undefined,
              tone: summary.expiringCertificatesCount > 0 ? ('warning' as const) : undefined,
            },
            {
              label: 'Learning paths',
              value: summary.learningPathsEnrolledCount
                ? `${summary.learningPathsCompletedCount} of ${summary.learningPathsEnrolledCount}`
                : '—',
            },
            {
              label: 'Mentoring',
              value: summary.hasActiveMentoringPair ? 'Active pair' : '—',
            },
          ]}
        />
      )}

      <Tabs defaultValue="nominations">
        <TabsList>
          <TabsTrigger value="nominations">Nominations ({noms.length})</TabsTrigger>
          <TabsTrigger value="requests">My requests ({reqs.length})</TabsTrigger>
          <TabsTrigger value="completions">Completions ({comps.length})</TabsTrigger>
          <TabsTrigger value="compliance">Mandatory ({compliance_.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="nominations" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>Nominations</CardTitle>
              <CardDescription>Training you have been put forward for.</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Nomination</TableHead>
                      <TableHead>Programme</TableHead>
                      <TableHead>Starts</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {loadingNoms ? (
                      <Loading cols={4} />
                    ) : noms.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={4}>
                          <EmptyState
                            icon={GraduationCap}
                            title="No nominations"
                            description="You have not been nominated for any training yet."
                          />
                        </TableCell>
                      </TableRow>
                    ) : (
                      noms.map((n) => (
                        <TableRow
                          key={n.id}
                          className="cursor-pointer hover:bg-muted/50"
                          onClick={() => router.push(`/hr/training/nominations/${n.id}`)}
                        >
                          <TableCell className="font-mono text-xs">{n.nominationNumber}</TableCell>
                          <TableCell className="font-medium">{n.programName}</TableCell>
                          <TableCell className="text-muted-foreground">{fmt(n.trainingStartDate)}</TableCell>
                          <TableCell>
                            <StatusBadge status={nomLabel(n.status)} />
                          </TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="requests" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>My requests</CardTitle>
              <CardDescription>Training you have asked for.</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Request</TableHead>
                      <TableHead>Training wanted</TableHead>
                      <TableHead>Raised</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {loadingReqs ? (
                      <Loading cols={4} />
                    ) : reqs.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={4}>
                          <EmptyState
                            icon={ClipboardList}
                            title="No requests"
                            description="Raise one for training that is not in the catalog."
                          />
                        </TableCell>
                      </TableRow>
                    ) : (
                      reqs.map((r) => (
                        <TableRow
                          key={r.id}
                          className="cursor-pointer hover:bg-muted/50"
                          onClick={() => router.push(`/hr/training/requests/${r.id}`)}
                        >
                          <TableCell className="font-mono text-xs">{r.requestNumber}</TableCell>
                          <TableCell className="font-medium">{r.requestedTrainingTitle}</TableCell>
                          <TableCell className="text-muted-foreground">{fmt(r.requestDate)}</TableCell>
                          <TableCell>
                            <StatusBadge status={reqLabel(r.status)} />
                          </TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="completions" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>Completions</CardTitle>
              <CardDescription>Your record of training delivered.</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Programme</TableHead>
                      <TableHead>Completed</TableHead>
                      <TableHead>Score</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Result</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {loadingComps ? (
                      <Loading cols={5} />
                    ) : comps.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={5}>
                          <EmptyState
                            icon={Award}
                            title="No completions yet"
                            description="Records appear here once training you attended is closed off."
                          />
                        </TableCell>
                      </TableRow>
                    ) : (
                      comps.map((c) => (
                        <TableRow key={c.id}>
                          <TableCell className="font-medium">{c.programName}</TableCell>
                          <TableCell className="text-muted-foreground">{fmt(c.completionDate)}</TableCell>
                          <TableCell>{typeof c.finalScore === 'number' ? c.finalScore : '—'}</TableCell>
                          <TableCell>
                            <StatusBadge status={compLabel(c.status)} />
                          </TableCell>
                          <TableCell>
                            <div className="flex items-center gap-2">
                              <Badge variant={c.isPassed ? 'default' : 'destructive'}>
                                {c.isPassed ? 'Passed' : 'Not passed'}
                              </Badge>
                              {c.isVerifiedByManager && <Badge variant="secondary">Verified</Badge>}
                            </div>
                          </TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
        <TabsContent value="compliance" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>Mandatory training</CardTitle>
              <CardDescription>
                Training you are required to hold, and when it next falls due.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Requirement</TableHead>
                      <TableHead>Satisfied by</TableHead>
                      <TableHead>Last completed</TableHead>
                      <TableHead>Next due</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {loadingCompliance ? (
                      <Loading cols={5} />
                    ) : compliance_.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={5}>
                          <EmptyState
                            icon={ShieldAlert}
                            title="No mandatory training"
                            description="Nothing has been assigned to you as a compliance requirement."
                          />
                        </TableCell>
                      </TableRow>
                    ) : (
                      compliance_.map((c) => (
                        <TableRow key={c.id}>
                          <TableCell className="font-medium">{c.requirementName}</TableCell>
                          <TableCell className="text-muted-foreground">{c.programName}</TableCell>
                          <TableCell className="text-muted-foreground">
                            {c.lastCompletedDate ? fmt(c.lastCompletedDate) : 'Never'}
                          </TableCell>
                          <TableCell className="text-muted-foreground">
                            {c.nextDueDate ? fmt(c.nextDueDate) : '—'}
                          </TableCell>
                          <TableCell>
                            <div className="flex items-center gap-2">
                              <StatusBadge status={complianceLabel(c.status)} />
                              {c.isOverdue && (
                                <Badge variant="destructive" className="text-[10px]">
                                  Overdue
                                </Badge>
                              )}
                              {c.isExempt && (
                                <Badge variant="outline" className="text-[10px]">
                                  Exempt
                                </Badge>
                              )}
                            </div>
                          </TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
