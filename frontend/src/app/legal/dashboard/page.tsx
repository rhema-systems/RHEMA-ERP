'use client';

import React from 'react';
import Link from 'next/link';
import {
  ArrowRight,
  BadgeCheck,
  BarChart3,
  BookOpen,
  FileCheck2,
  FileSignature,
  FileText,
  Gavel,
  Landmark,
  Loader2,
  Scale,
  ShieldCheck,
  Workflow,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  legalProcedureService,
  type LegalDashboard,
  type LegalProcedure,
} from '@/services/legal-procedure.service';
import {
  procedureCaseService,
  type ProcedureCaseSummary,
} from '@/services/procedure-case.service';

const iconMap: Record<string, React.ComponentType<{ className?: string }>> = {
  BadgeCheck,
  BookOpen,
  FileCheck2,
  FileSignature,
  FileText,
  Gavel,
  Landmark,
  Scale,
  ShieldCheck,
};

const accentClasses: Record<string, string> = {
  amber: 'text-amber-700 dark:text-amber-300',
  blue: 'text-blue-700 dark:text-blue-300',
  cyan: 'text-cyan-700 dark:text-cyan-300',
  emerald: 'text-emerald-700 dark:text-emerald-300',
  purple: 'text-purple-700 dark:text-purple-300',
  sky: 'text-sky-700 dark:text-sky-300',
  slate: 'text-slate-700 dark:text-slate-200',
  teal: 'text-teal-700 dark:text-teal-300',
  violet: 'text-violet-700 dark:text-violet-300',
};

export default function LegalDashboardPage() {
  const [procedures, setProcedures] = React.useState<LegalProcedure[]>([]);
  const [cases, setCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [dashboard, setDashboard] = React.useState<LegalDashboard | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      const [procedureResult, caseResult, dashboardResult] = await Promise.allSettled([
        legalProcedureService.getProcedures(),
        procedureCaseService.listModuleCases('Legal'),
        legalProcedureService.getDashboard(),
      ]);

      if (mounted) {
        setProcedures(
          procedureResult.status === 'fulfilled' ? procedureResult.value : []
        );
        setCases(caseResult.status === 'fulfilled' ? caseResult.value : []);
        setDashboard(dashboardResult.status === 'fulfilled' ? dashboardResult.value : null);
        setIsLoading(false);
      }
    };

    void load();

    return () => {
      mounted = false;
    };
  }, []);

  const dashboardMetrics = React.useMemo(
    () => [
      { label: 'Open matters', value: (dashboard?.openMatters ?? cases.filter((item) => item.status !== 'Completed').length).toString(), icon: FileSignature },
      {
        label: 'Active court cases',
        value: (dashboard?.activeCourtCases ?? 0).toString(),
        icon: Scale,
      },
      {
        label: 'Hearings in 30 days',
        value: (dashboard?.hearingsNext30Days ?? 0).toString(),
        icon: Gavel,
      },
      {
        label: 'Overdue deadlines',
        value: (dashboard?.overdueResponseDeadlines ?? 0).toString(),
        icon: ShieldCheck,
      },
    ],
    [cases, dashboard]
  );

  const actionQueues = React.useMemo(() => {
    const counts = new Map<string, number>();
    cases
      .filter((item) => item.status !== 'Completed')
      .forEach((item) =>
        counts.set(item.currentStageName, (counts.get(item.currentStageName) ?? 0) + 1)
      );
    return [...counts.entries()]
      .map(([label, value]) => ({ label, value: value.toString() }))
      .sort((a, b) => Number(b.value) - Number(a.value))
      .slice(0, 6);
  }, [cases]);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Legal
          </Badge>
          <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
            Legal Dashboard
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/legal">
              <BookOpen className="mr-2 h-4 w-4" />
              Workspaces
            </Link>
          </Button>
          <Button asChild>
            <Link href="/document-management">
              <FileText className="mr-2 h-4 w-4" />
              Central DMS
            </Link>
          </Button>
        </div>
      </div>

      {isLoading ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardContent className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Loading legal dashboard
          </CardContent>
        </Card>
      ) : null}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {dashboardMetrics.map((item) => {
          const Icon = item.icon;

          return (
            <Card
              key={item.label}
              className="border-border bg-card text-card-foreground"
            >
              <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0 pb-2">
                <div>
                  <div className="text-sm text-muted-foreground">
                    {item.label}
                  </div>
                  <CardTitle className="mt-1 text-2xl">{item.value}</CardTitle>
                </div>
                <div className="flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
                  <Icon className="h-5 w-5 text-primary" />
                </div>
              </CardHeader>
              <CardContent />
            </Card>
          );
        })}
      </div>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,0.8fr)_minmax(0,1.2fr)]">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle>Court Case Outcomes</CardTitle>
          </CardHeader>
          <CardContent className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            {[
              { label: 'Pending', value: dashboard?.courtPending ?? 0 },
              { label: 'Won', value: dashboard?.courtWon ?? 0 },
              { label: 'Settled', value: dashboard?.courtSettled ?? 0 },
              { label: 'Lost', value: dashboard?.courtLost ?? 0 },
              { label: 'Withdrawn / struck out', value: dashboard?.courtWithdrawn ?? 0 },
              { label: 'Completed matters', value: dashboard?.completedMatters ?? 0 },
            ].map((item) => (
              <div key={item.label} className="rounded-md border bg-background p-4">
                <div className="text-sm text-muted-foreground">{item.label}</div>
                <div className="mt-1 text-2xl font-semibold">{item.value}</div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle>Upcoming Court Dates &amp; Deadlines</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {!dashboard?.upcomingCourtEvents.length ? (
              <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                No upcoming court dates or filing deadlines.
              </div>
            ) : null}
            {dashboard?.upcomingCourtEvents.map((event) => (
              <div key={event.id} className="flex flex-col gap-3 rounded-md border bg-background p-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <div className="font-medium">{event.referenceNumber || event.title}</div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {event.courtName || 'Court not recorded'} · {event.currentStageName}
                  </div>
                </div>
                <div className="flex flex-wrap gap-2 text-xs">
                  {event.responseDeadline ? <Badge variant="outline">Response {event.responseDeadline}</Badge> : null}
                  {event.nextHearingDate ? <Badge variant="secondary">Hearing {event.nextHearingDate}</Badge> : null}
                  {event.risk ? <Badge variant="outline">{event.risk} risk</Badge> : null}
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {[
          { label: 'Pending signatures', value: dashboard?.pendingSignatures ?? 0 },
          { label: 'Pending payments', value: dashboard?.pendingPayments ?? 0 },
          { label: 'Awaiting Estate return', value: dashboard?.awaitingEstateReturn ?? 0 },
          { label: 'Property-linked matters', value: dashboard?.propertyLinkedMatters ?? 0 },
        ].map((item) => (
          <Card key={item.label} className="border-border bg-card text-card-foreground">
            <CardContent className="p-4">
              <div className="text-sm text-muted-foreground">{item.label}</div>
              <div className="mt-1 text-xl font-semibold">{item.value}</div>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(320px,0.8fr)]">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <BarChart3 className="h-5 w-5 text-primary" />
              <CardTitle>Procedure Workspaces</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2">
            {procedures.map((procedure) => {
              const Icon = iconMap[procedure.icon] || FileText;
              const accent =
                accentClasses[procedure.accent] || accentClasses.slate;

              return (
                <div
                  key={procedure.entityType}
                  className="rounded-md border bg-background p-4"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex min-w-0 items-start gap-3">
                      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md border bg-muted">
                        <Icon className={`h-4 w-4 ${accent}`} />
                      </div>
                      <div className="min-w-0">
                        <div className="font-medium">{procedure.title}</div>
                        <Badge variant="outline" className="mt-2">
                          {procedure.source}
                        </Badge>
                      </div>
                    </div>
                    <Button asChild size="icon" variant="outline">
                      <Link
                        href={`/legal/${encodeURIComponent(
                          procedure.entityType
                        )}`}
                        aria-label={`Open ${procedure.title}`}
                      >
                        <ArrowRight className="h-4 w-4" />
                      </Link>
                    </Button>
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card className="border-border bg-card text-card-foreground">
            <CardHeader>
              <div className="flex items-center gap-2">
                <Workflow className="h-5 w-5 text-primary" />
                <CardTitle>Action Queue</CardTitle>
              </div>
            </CardHeader>
            <CardContent className="grid gap-3">
              {!isLoading && actionQueues.length === 0 ? (
                <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                  No open legal case stages.
                </div>
              ) : null}
              {actionQueues.map((item) => (
                <div
                  key={item.label}
                  className="flex items-center justify-between rounded-md border bg-background p-4"
                >
                  <span className="text-sm font-medium">{item.label}</span>
                  <Badge variant="secondary">{item.value}</Badge>
                </div>
              ))}
            </CardContent>
          </Card>

          <Card className="border-border bg-card text-card-foreground">
            <CardHeader>
              <CardTitle>Setup</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-wrap gap-2">
              <Button asChild variant="outline" size="sm">
                <Link href="/administration/workflow?q=Legal">
                  Workflow Setup
                </Link>
              </Button>
              <Button asChild variant="outline" size="sm">
                <Link href="/administration/document-management/metadata-templates">
                  DMS Metadata
                </Link>
              </Button>
              <Button asChild variant="outline" size="sm">
                <Link href="/administration/document-management/access-retention">
                  Access & Retention
                </Link>
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
