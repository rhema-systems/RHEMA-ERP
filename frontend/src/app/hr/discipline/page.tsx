'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  Loader2, Plus, ListChecks, Search, Gavel, ShieldAlert, Scale, Ban, Scroll,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { disciplineService } from '@/services/hr/discipline.service';
import {
  CASE_STATUS_OPTIONS,
  type DisciplinaryStatus,
  type DisciplinaryCaseSummary,
} from '@/types/hr/discipline';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

type QuickView =
  | 'all' | 'open' | 'pending-investigation' | 'pending-hearing' | 'pending-closure'
  | 'active-appeal' | 'legal-review';

const VIEWS: { key: QuickView; label: string; icon: typeof ListChecks; hint: string }[] = [
  { key: 'all', label: 'All cases', icon: ListChecks, hint: 'Every case on record, newest incident first' },
  { key: 'open', label: 'Open', icon: ShieldAlert, hint: 'Cases that are neither closed nor dismissed' },
  { key: 'pending-investigation', label: 'Needs investigation', icon: Search, hint: 'An investigation is required and none has been opened' },
  { key: 'pending-hearing', label: 'Needs hearing', icon: Gavel, hint: 'A hearing is required and none has been scheduled' },
  { key: 'pending-closure', label: 'Awaiting closure', icon: Scroll, hint: 'A decision has been made and the case is not yet closed' },
  { key: 'active-appeal', label: 'Under appeal', icon: Scale, hint: 'An appeal has been filed and not yet decided' },
  { key: 'legal-review', label: 'With legal', icon: Ban, hint: 'At least one legal review is still open' },
];

/**
 * The disciplinary case register.
 *
 * The sanction column is worth a note: those flags come from the server and, before area 9 slice 1,
 * were false on every list read but one — so this column would have shown nothing on cases that
 * plainly carried a warning or a fine. They agree with the case detail now.
 */
export default function DisciplineRegisterPage() {
  const [view, setView] = useState<QuickView>('open');
  const [status, setStatus] = useState<DisciplinaryStatus | 'all'>('all');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'discipline', 'register', view],
    queryFn: async (): Promise<DisciplinaryCaseSummary[]> => {
      switch (view) {
        case 'open':
          return disciplineService.getOpen();
        case 'pending-investigation':
          return disciplineService.getPendingInvestigation();
        case 'pending-hearing':
          return disciplineService.getPendingHearing();
        case 'pending-closure':
          return disciplineService.getPendingClosure();
        case 'active-appeal':
          return disciplineService.getWithActiveAppeal();
        case 'legal-review':
          return disciplineService.getWithActiveLegalReview();
        default: {
          // The only paged endpoint in the area; the quick views return plain lists.
          const page = await disciplineService.getPaged({ pageNumber: 1, pageSize: 200 });
          return page.items;
        }
      }
    },
  });

  const items = (data ?? []).filter((c) => status === 'all' || c.status === status);
  const activeView = VIEWS.find((v) => v.key === view) ?? VIEWS[0];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Disciplinary cases"
        description="Misconduct allegations from report through investigation and hearing to decision, sanction and appeal."
        backHref="/hr"
        actions={
          <Button asChild>
            <Link href="/hr/discipline/new">
              <Plus className="mr-2 h-4 w-4" />
              Raise case
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        {VIEWS.map((v) => (
          <Button
            key={v.key}
            variant={view === v.key ? 'default' : 'outline'}
            size="sm"
            onClick={() => setView(v.key)}
          >
            <v.icon className="mr-2 h-4 w-4" />
            {v.label}
          </Button>
        ))}
      </div>

      <Card>
        <CardContent className="p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-muted-foreground">{activeView.hint}</p>
            <Select value={status} onValueChange={(v) => setStatus(v as DisciplinaryStatus | 'all')}>
              <SelectTrigger className="w-56">
                <SelectValue placeholder="All statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {CASE_STATUS_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              title="No cases"
              description={
                view === 'all'
                  ? 'No disciplinary case has been raised yet.'
                  : 'Nothing is in this queue at the moment.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Case</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Offence</TableHead>
                  <TableHead>Severity</TableHead>
                  <TableHead>Incident</TableHead>
                  <TableHead>Sanction</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/discipline/${c.id}`} className="hover:underline">
                        {c.caseNumber}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <div>{c.employeeName}</div>
                      {c.employeeNumber && (
                        <div className="text-xs text-muted-foreground">{c.employeeNumber}</div>
                      )}
                    </TableCell>
                    <TableCell>{c.offenseName}</TableCell>
                    <TableCell>
                      <StatusBadge status={c.severityName} />
                    </TableCell>
                    <TableCell>{fmtDate(c.incidentDate)}</TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {c.hasWarning && <StatusBadge status="Warning" />}
                        {c.hasSuspension && <StatusBadge status="Suspension" />}
                        {c.hasFine && <StatusBadge status="Fine" />}
                        {c.hasTermination && <StatusBadge status="Termination" />}
                        {c.appealFiled && <StatusBadge status="Appealed" />}
                        {!c.hasWarning && !c.hasSuspension && !c.hasFine
                          && !c.hasTermination && !c.appealFiled && (
                          <span className="text-xs text-muted-foreground">—</span>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={c.statusName} />
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
