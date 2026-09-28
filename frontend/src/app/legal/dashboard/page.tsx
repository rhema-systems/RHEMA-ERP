'use client';

import React from 'react';
import Link from 'next/link';
import {
  ArrowRight,
  BadgeCheck,
  BarChart3,
  BookOpen,
  Briefcase,
  FileCheck2,
  FileSignature,
  FileText,
  Gavel,
  Landmark,
  Loader2,
  MessageSquare,
  Scale,
  Search,
  ShieldCheck,
  Workflow,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  legalProcedureService,
  type LegalDashboard,
  type LegalMatterRegister,
  type LegalProcedure,
} from '@/services/legal-procedure.service';

const iconMap: Record<string, React.ComponentType<{ className?: string }>> = {
  BadgeCheck,
  BookOpen,
  Briefcase,
  FileCheck2,
  FileSignature,
  FileText,
  Gavel,
  Landmark,
  MessageSquare,
  Scale,
  ShieldCheck,
};

const accentClasses: Record<string, string> = {
  amber: 'text-amber-700 dark:text-amber-300',
  blue: 'text-blue-700 dark:text-blue-300',
  cyan: 'text-cyan-700 dark:text-cyan-300',
  emerald: 'text-emerald-700 dark:text-emerald-300',
  indigo: 'text-indigo-700 dark:text-indigo-300',
  purple: 'text-purple-700 dark:text-purple-300',
  sky: 'text-sky-700 dark:text-sky-300',
  slate: 'text-slate-700 dark:text-slate-200',
  teal: 'text-teal-700 dark:text-teal-300',
  violet: 'text-violet-700 dark:text-violet-300',
  zinc: 'text-zinc-700 dark:text-zinc-300',
};

const isOpenStatus = (status: string) => {
  const normalized = status.toLowerCase();
  return !['completed', 'closed', 'cancelled', 'canceled'].includes(normalized);
};

const formatDate = (value?: string | null) => {
  if (!value) return 'Not recorded';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Not recorded';
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
  }).format(date);
};

export default function LegalDashboardPage() {
  const [procedures, setProcedures] = React.useState<LegalProcedure[]>([]);
  const [matterRegister, setMatterRegister] =
    React.useState<LegalMatterRegister>({ totalCount: 0, items: [] });
  const [dashboard, setDashboard] = React.useState<LegalDashboard | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isRegisterLoading, setIsRegisterLoading] = React.useState(true);
  const [matterSearch, setMatterSearch] = React.useState('');
  const [matterStatusFilter, setMatterStatusFilter] = React.useState('open');
  const [matterTypeFilter, setMatterTypeFilter] = React.useState('all');

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      const [procedureResult, dashboardResult] = await Promise.allSettled([
        legalProcedureService.getProcedures(),
        legalProcedureService.getDashboard(),
      ]);

      if (mounted) {
        setProcedures(
          procedureResult.status === 'fulfilled' ? procedureResult.value : []
        );
        setDashboard(dashboardResult.status === 'fulfilled' ? dashboardResult.value : null);
        setIsLoading(false);
      }
    };

    void load();

    return () => {
      mounted = false;
    };
  }, []);

  React.useEffect(() => {
    let mounted = true;

    const loadRegister = async () => {
      setIsRegisterLoading(true);
      try {
        const register = await legalProcedureService.getMatterRegister({
          status: matterStatusFilter,
          entityType: matterTypeFilter,
          search: matterSearch,
          pageSize: 200,
        });
        if (mounted) {
          setMatterRegister(register);
        }
      } catch {
        if (mounted) {
          setMatterRegister({ totalCount: 0, items: [] });
        }
      } finally {
        if (mounted) {
          setIsRegisterLoading(false);
        }
      }
    };

    void loadRegister();

    return () => {
      mounted = false;
    };
  }, [matterSearch, matterStatusFilter, matterTypeFilter]);

  const dashboardMetrics = React.useMemo(
    () => [
      { label: 'Open matters', value: (dashboard?.openMatters ?? matterRegister.items.filter((item) => isOpenStatus(item.status)).length).toString(), icon: FileSignature },
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
    [dashboard, matterRegister.items]
  );

  const matterTypeOptions = React.useMemo(
    () => [...procedures].sort((a, b) => a.title.localeCompare(b.title)),
    [procedures]
  );

  const registerCases = matterRegister.items;

  const actionQueues = React.useMemo(() => {
    const counts = new Map<string, number>();
    matterRegister.items
      .filter((item) => isOpenStatus(item.status))
      .forEach((item) =>
        counts.set(item.currentStageName, (counts.get(item.currentStageName) ?? 0) + 1)
      );
    return [...counts.entries()]
      .map(([label, value]) => ({ label, value: value.toString() }))
      .sort((a, b) => Number(b.value) - Number(a.value))
      .slice(0, 6);
  }, [matterRegister.items]);

  const operationalQueues = React.useMemo(
    () => [
      {
        title: 'Pending signatures',
        icon: FileSignature,
        items: dashboard?.pendingSignatureMatters ?? [],
      },
      {
        title: 'Pending payments',
        icon: ShieldCheck,
        items: dashboard?.pendingPaymentMatters ?? [],
      },
      {
        title: 'Estate returns',
        icon: Landmark,
        items: dashboard?.awaitingEstateReturnMatters ?? [],
      },
      {
        title: 'Overdue court deadlines',
        icon: Scale,
        items: dashboard?.overdueCourtDeadlines ?? [],
      },
    ],
    [dashboard]
  );
  const sopControlSummary = React.useMemo(
    () => [
      {
        label: 'Templates and drafting',
        value: dashboard?.templateDraftingControls ?? 0,
        icon: FileText,
      },
      {
        label: 'File movement',
        value: dashboard?.fileMovementControls ?? 0,
        icon: BookOpen,
      },
      {
        label: 'Signature / sealing / dispatch',
        value: dashboard?.signatureDispatchControls ?? 0,
        icon: FileSignature,
      },
      {
        label: 'Finance payment controls',
        value: dashboard?.financePaymentControls ?? 0,
        icon: ShieldCheck,
      },
      {
        label: 'Estate / registration close-out',
        value: dashboard?.estateRegistrationControls ?? 0,
        icon: Landmark,
      },
      {
        label: 'Court calendar and litigation',
        value: dashboard?.courtCalendarControls ?? 0,
        icon: Scale,
      },
      {
        label: 'External counsel',
        value: dashboard?.externalCounselControls ?? 0,
        icon: Briefcase,
      },
      {
        label: 'Legal opinions',
        value: dashboard?.legalOpinionControls ?? 0,
        icon: MessageSquare,
      },
    ],
    [dashboard]
  );
  const sopControlGroups = React.useMemo(() => {
    const groups = new Map<string, NonNullable<LegalDashboard['sopControlItems']>>();
    (dashboard?.sopControlItems ?? []).forEach((item) => {
      const list = groups.get(item.category) ?? [];
      list.push(item);
      groups.set(item.category, list);
    });

    return sopControlSummary.map((item) => ({
      ...item,
      items: groups.get(item.label) ?? [],
    }));
  }, [dashboard, sopControlSummary]);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
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

      <Card className="border-border bg-card text-card-foreground">
        <CardHeader className="space-y-4">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <CardTitle>Legal Matter Register</CardTitle>
            <div className="text-sm text-muted-foreground">
              Showing {Math.min(registerCases.length, 25)} of {matterRegister.totalCount} matters
            </div>
          </div>
          <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_190px_240px]">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={matterSearch}
                onChange={(event) => setMatterSearch(event.target.value)}
                placeholder="Search reference, party, stage or officer"
                className="pl-9"
              />
            </div>
            <Select value={matterStatusFilter} onValueChange={setMatterStatusFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="open">Open matters</SelectItem>
                <SelectItem value="completed">Completed matters</SelectItem>
                <SelectItem value="all">All statuses</SelectItem>
              </SelectContent>
            </Select>
            <Select value={matterTypeFilter} onValueChange={setMatterTypeFilter}>
              <SelectTrigger>
                <SelectValue placeholder="Matter type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All matter types</SelectItem>
                {matterTypeOptions.map((procedure) => (
                  <SelectItem key={procedure.entityType} value={procedure.entityType}>
                    {procedure.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardHeader>
        <CardContent>
          {isRegisterLoading ? (
            <div className="flex items-center justify-center gap-2 rounded-md border border-dashed p-6 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading matter register
            </div>
          ) : null}
          {!isRegisterLoading && registerCases.length === 0 ? (
            <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
              No legal matters match the current filters.
            </div>
          ) : null}
          {!isRegisterLoading && registerCases.length > 0 ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Matter</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Stage</TableHead>
                  <TableHead>Owner</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Updated</TableHead>
                  <TableHead className="w-14 text-right">Open</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {registerCases.slice(0, 25).map((item) => {
                  return (
                    <TableRow key={item.id}>
                      <TableCell>
                        <div className="font-medium">
                          {item.matterNumber || item.referenceNumber || item.title}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {item.applicantName || item.title}
                        </div>
                      </TableCell>
                      <TableCell className="max-w-[220px]">
                        <span className="line-clamp-2">{item.procedureTitle}</span>
                      </TableCell>
                      <TableCell>{item.currentStageName}</TableCell>
                      <TableCell>
                        {item.assignedLegalOfficer || item.currentAssignedRole || 'Unassigned'}
                      </TableCell>
                      <TableCell>
                        <Badge variant={isOpenStatus(item.status) ? 'secondary' : 'outline'}>
                          {item.status}
                        </Badge>
                      </TableCell>
                      <TableCell className="whitespace-nowrap">
                        {formatDate(item.updatedAt ?? item.createdAt)}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button asChild size="icon" variant="ghost">
                          <Link
                            href={`/legal/${encodeURIComponent(item.entityType)}/cases/${encodeURIComponent(item.id)}`}
                            aria-label={`Open ${item.title}`}
                          >
                            <ArrowRight className="h-4 w-4" />
                          </Link>
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          ) : null}
          {matterRegister.totalCount > 25 ? (
            <div className="mt-3 text-sm text-muted-foreground">
              Showing the 25 most recently updated matching matters.
            </div>
          ) : null}
        </CardContent>
      </Card>

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

      <div className="grid gap-4 xl:grid-cols-4">
        {operationalQueues.map((queue) => {
          const Icon = queue.icon;

          return (
            <Card key={queue.title} className="border-border bg-card text-card-foreground">
              <CardHeader>
                <div className="flex items-center gap-2">
                  <Icon className="h-5 w-5 text-primary" />
                  <CardTitle className="text-base">{queue.title}</CardTitle>
                </div>
              </CardHeader>
              <CardContent className="space-y-3">
                {queue.items.length === 0 ? (
                  <div className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
                    No matters in this queue.
                  </div>
                ) : null}
                {queue.items.slice(0, 4).map((item) => (
                  <Link
                    key={item.id}
                    href={`/legal/${encodeURIComponent(item.entityType)}/cases/${encodeURIComponent(item.id)}`}
                    className="block rounded-md border bg-background p-3 transition-colors hover:bg-muted/70"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <div className="truncate text-sm font-medium">
                          {item.referenceNumber || item.title}
                        </div>
                        <div className="mt-1 text-xs text-muted-foreground">
                          {item.owner || item.currentStageName}
                        </div>
                      </div>
                      <ArrowRight className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
                    </div>
                    <div className="mt-2 flex flex-wrap gap-1.5">
                      <Badge variant="outline">{item.statusDetail}</Badge>
                      {item.dueDate ? (
                        <Badge variant="secondary">{formatDate(item.dueDate)}</Badge>
                      ) : null}
                      {item.risk ? <Badge variant="outline">{item.risk}</Badge> : null}
                    </div>
                  </Link>
                ))}
              </CardContent>
            </Card>
          );
        })}
      </div>

      <Card className="border-border bg-card text-card-foreground">
        <CardHeader>
          <CardTitle>Legal SOP Control Registers</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            {sopControlSummary.map((item) => {
              const Icon = item.icon;

              return (
                <div key={item.label} className="rounded-md border bg-background p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="text-sm text-muted-foreground">{item.label}</div>
                      <div className="mt-1 text-2xl font-semibold">{item.value}</div>
                    </div>
                    <div className="flex h-9 w-9 items-center justify-center rounded-md border bg-muted">
                      <Icon className="h-4 w-4 text-primary" />
                    </div>
                  </div>
                </div>
              );
            })}
          </div>

          <div className="grid gap-4 xl:grid-cols-2">
            {sopControlGroups.map((group) => {
              const Icon = group.icon;

              return (
                <div key={group.label} className="rounded-md border bg-background p-4">
                  <div className="mb-3 flex items-center justify-between gap-3">
                    <div className="flex items-center gap-2">
                      <Icon className="h-4 w-4 text-primary" />
                      <h2 className="text-sm font-semibold">{group.label}</h2>
                    </div>
                    <Badge variant="secondary">{group.items.length}</Badge>
                  </div>
                  <div className="space-y-2">
                    {group.items.length === 0 ? (
                      <div className="rounded-md border border-dashed p-4 text-center text-sm text-muted-foreground">
                        No open items.
                      </div>
                    ) : null}
                    {group.items.slice(0, 5).map((item) => (
                      <Link
                        key={`${group.label}-${item.id}-${item.statusDetail}`}
                        href={`/legal/${encodeURIComponent(item.entityType)}/cases/${encodeURIComponent(item.id)}`}
                        className="block rounded-md border bg-card p-3 transition-colors hover:bg-muted/70"
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div className="min-w-0">
                            <div className="truncate text-sm font-medium">
                              {item.referenceNumber || item.title}
                            </div>
                            <div className="mt-1 text-xs text-muted-foreground">
                              {item.procedureTitle} · {item.owner || item.currentStageName}
                            </div>
                          </div>
                          <ArrowRight className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
                        </div>
                        <div className="mt-2 flex flex-wrap gap-1.5">
                          <Badge variant="outline">{item.statusDetail}</Badge>
                          {item.dueDate ? (
                            <Badge variant="secondary">{formatDate(item.dueDate)}</Badge>
                          ) : null}
                          {item.reference ? <Badge variant="outline">{item.reference}</Badge> : null}
                          {item.risk ? <Badge variant="outline">{item.risk}</Badge> : null}
                        </div>
                      </Link>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(320px,0.8fr)]">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <BarChart3 className="h-5 w-5 text-primary" />
              <CardTitle>Workspaces</CardTitle>
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

        </div>
      </div>
    </div>
  );
}
