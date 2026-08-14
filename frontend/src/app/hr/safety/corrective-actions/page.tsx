'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, AlertTriangle, CalendarClock, HelpCircle, ListChecks } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { safetyCorrectiveActionsService } from '@/services/hr/safety-corrective-actions.service';
import {
  SHE_CORRECTIVE_ACTION_SOURCE_OPTIONS,
  type SheCorrectiveActionSource,
  type SheUnifiedCorrectiveAction,
} from '@/types/hr/safety-kpi';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const SOURCE_LABEL: Record<string, string> = Object.fromEntries(
  SHE_CORRECTIVE_ACTION_SOURCE_OPTIONS.map((o) => [o.value, o.label]),
);

function StatusBadge({ row }: { row: SheUnifiedCorrectiveAction }) {
  if (row.isOverdue)
    return (
      <Badge variant="destructive">
        Overdue {row.daysOverdue}d · tier {row.escalationTier}
      </Badge>
    );
  switch (row.status) {
    case 'Completed':
      return <Badge variant="secondary">{row.rawStatus === 'Verified' ? 'Verified' : 'Completed'}</Badge>;
    case 'Cancelled':
      return <Badge variant="outline">Cancelled</Badge>;
    case 'InProgress':
      return <Badge>In progress</Badge>;
    default:
      return <Badge variant="outline">Open</Badge>;
  }
}

function PriorityBadge({ row }: { row: SheUnifiedCorrectiveAction }) {
  if (!row.priority) return <span className="text-muted-foreground">—</span>;
  const variant =
    row.priority === 'Critical' ? 'destructive' : row.priority === 'High' ? 'default' : 'outline';
  return <Badge variant={variant}>{row.priority}</Badge>;
}

type Tab = 'open' | 'overdue' | 'completed' | 'all';

/**
 * The unified corrective-action tracker (FR-SHE-245): one queue over the four
 * corrective-action stores — incident investigations, workplace inspections, safety
 * equipment inspections and committee meetings. Read-only by design: each row links
 * to the parent record where the action is actually worked. Overdue actions also
 * remind and escalate through the SHE reminder engine.
 */
export default function CorrectiveActionTrackerPage() {
  const [tab, setTab] = useState<Tab>('open');
  const [source, setSource] = useState<SheCorrectiveActionSource | 'all'>('all');

  const { data: rows = [], isLoading } = useQuery({
    queryKey: ['hr', 'safety-corrective-actions', 'list'],
    queryFn: () => safetyCorrectiveActionsService.getAll(),
  });

  const { data: summary } = useQuery({
    queryKey: ['hr', 'safety-corrective-actions', 'summary'],
    queryFn: () => safetyCorrectiveActionsService.getSummary(),
  });

  const filtered = useMemo(() => {
    let r = rows;
    if (source !== 'all') r = r.filter((x) => x.source === source);
    switch (tab) {
      case 'open':
        return r.filter((x) => x.status === 'Open' || x.status === 'InProgress');
      case 'overdue':
        return r.filter((x) => x.isOverdue);
      case 'completed':
        return r.filter((x) => x.status === 'Completed');
      default:
        return r;
    }
  }, [rows, tab, source]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Corrective Action Tracker"
        description="Every corrective action across incidents, inspections, equipment and committee meetings — one queue. Rows link to the record where the action is worked."
        backHref="/hr/safety"
      />

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-muted-foreground flex items-center gap-2 text-sm font-medium">
              <ListChecks className="h-4 w-4" />
              Open actions
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-semibold tabular-nums">
              {summary ? summary.open + summary.inProgress : '—'}
            </div>
            <p className="text-muted-foreground text-xs">
              {summary?.inProgress ?? 0} in progress
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-muted-foreground flex items-center gap-2 text-sm font-medium">
              <AlertTriangle className="h-4 w-4" />
              Overdue
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-semibold tabular-nums">{summary?.overdue ?? '—'}</div>
            <p className="text-muted-foreground text-xs">
              tier 1 · {summary?.overdueTier1 ?? 0} — tier 2 · {summary?.overdueTier2 ?? 0} — tier
              3 · {summary?.overdueTier3 ?? 0}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-muted-foreground flex items-center gap-2 text-sm font-medium">
              <CalendarClock className="h-4 w-4" />
              Due within 7 days
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-semibold tabular-nums">
              {summary?.dueWithin7Days ?? '—'}
            </div>
            <p className="text-muted-foreground text-xs">of the open actions</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-muted-foreground flex items-center gap-2 text-sm font-medium">
              <HelpCircle className="h-4 w-4" />
              No due date
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-semibold tabular-nums">
              {summary?.withoutDueDate ?? '—'}
            </div>
            <p className="text-muted-foreground text-xs">
              open actions the reminder engine cannot ladder
            </p>
          </CardContent>
        </Card>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Tabs value={tab} onValueChange={(v) => setTab(v as Tab)}>
          <TabsList>
            <TabsTrigger value="open">Open</TabsTrigger>
            <TabsTrigger value="overdue">Overdue</TabsTrigger>
            <TabsTrigger value="completed">Completed</TabsTrigger>
            <TabsTrigger value="all">All</TabsTrigger>
          </TabsList>
        </Tabs>
        <Select value={source} onValueChange={(v) => setSource(v as SheCorrectiveActionSource | 'all')}>
          <SelectTrigger className="w-56">
            <SelectValue placeholder="All sources" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All sources</SelectItem>
            {SHE_CORRECTIVE_ACTION_SOURCE_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              title="No corrective actions here"
              description="Actions are raised on incidents, inspections, equipment and committee meetings; they appear in this queue automatically."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Source</TableHead>
                  <TableHead>Raised on</TableHead>
                  <TableHead className="max-w-md">Action</TableHead>
                  <TableHead>Priority</TableHead>
                  <TableHead>Assigned to</TableHead>
                  <TableHead>Due</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((row) => (
                  <TableRow key={`${row.source}-${row.id}`}>
                    <TableCell>
                      <Badge variant="outline">{SOURCE_LABEL[row.source] ?? row.source}</Badge>
                    </TableCell>
                    <TableCell className="font-medium">
                      <Link href={row.parentPath} className="hover:underline">
                        <span className="font-mono text-sm">{row.parentReference}</span>
                      </Link>
                    </TableCell>
                    <TableCell className="max-w-md">
                      <span className="line-clamp-2 text-sm">{row.description}</span>
                      {row.effectivenessVerified && (
                        <span className="text-muted-foreground text-xs">
                          Effectiveness verified
                        </span>
                      )}
                    </TableCell>
                    <TableCell>
                      <PriorityBadge row={row} />
                    </TableCell>
                    <TableCell>{row.assignedToName ?? '—'}</TableCell>
                    <TableCell className="tabular-nums">{fmtDate(row.dueDate)}</TableCell>
                    <TableCell>
                      <StatusBadge row={row} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {summary && Object.keys(summary.bySource).length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">By source</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Source</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                  <TableHead className="text-right">Open</TableHead>
                  <TableHead className="text-right">Overdue</TableHead>
                  <TableHead className="text-right">Completed</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {Object.entries(summary.bySource).map(([key, s]) => (
                  <TableRow key={key}>
                    <TableCell>{SOURCE_LABEL[key] ?? key}</TableCell>
                    <TableCell className="text-right tabular-nums">{s.total}</TableCell>
                    <TableCell className="text-right tabular-nums">{s.open}</TableCell>
                    <TableCell className="text-right tabular-nums">{s.overdue}</TableCell>
                    <TableCell className="text-right tabular-nums">{s.completed}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
