'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, MessagesSquare, Clock } from 'lucide-react';
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
import { grievanceService } from '@/services/hr/grievance.service';
import {
  GRIEVANCE_STATUS_OPTIONS, GRIEVANCE_LADDER,
  type GrievanceStatus, type GrievanceSummary,
} from '@/types/hr/grievance';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const levelLabel = (value: string) =>
  GRIEVANCE_LADDER.find((l) => l.value === value)?.label ?? value;

type View = 'all' | 'awaiting';

/**
 * HR's grievance register.
 *
 * The "awaiting a response" view is the useful one, which is why it is the default: a grievance's
 * problem is almost never that it exists, it is that the rung it is sitting at has not answered.
 */
export default function GrievanceRegisterPage() {
  const [view, setView] = useState<View>('awaiting');
  const [status, setStatus] = useState<GrievanceStatus | 'all'>('all');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'grievances', 'register', view],
    queryFn: (): Promise<GrievanceSummary[]> =>
      view === 'awaiting' ? grievanceService.getAwaitingResponse() : grievanceService.getAll(),
  });

  const items = (data ?? []).filter((g) => status === 'all' || g.status === status);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Grievances"
        description="Employee grievances and their progress up the escalation route: supervisor, head of department, HR, GM Finance & Administration, Managing Director, Board."
        backHref="/hr"
        actions={
          // Area 25 slice 9: filing and the detail live in the portal; the register stays here.
          <Button asChild variant="outline">
            <Link href="/me/grievances/new">
              <Plus className="mr-2 h-4 w-4" />
              Raise my own grievance
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Button
          variant={view === 'awaiting' ? 'default' : 'outline'}
          size="sm"
          onClick={() => setView('awaiting')}
        >
          <Clock className="mr-2 h-4 w-4" />
          Awaiting a response
        </Button>
        <Button
          variant={view === 'all' ? 'default' : 'outline'}
          size="sm"
          onClick={() => setView('all')}
        >
          <MessagesSquare className="mr-2 h-4 w-4" />
          All grievances
        </Button>

        <Select value={status} onValueChange={(v) => setStatus(v as GrievanceStatus | 'all')}>
          <SelectTrigger className="ml-auto w-52">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {GRIEVANCE_STATUS_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              title="Nothing here"
              description={
                view === 'awaiting'
                  ? 'Every grievance has had a response at the level it is sitting at.'
                  : 'No grievance has been raised.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Reference</TableHead>
                  <TableHead>Raised by</TableHead>
                  <TableHead>Subject</TableHead>
                  <TableHead>Filed</TableHead>
                  <TableHead>Currently with</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((g) => (
                  <TableRow key={g.id}>
                    <TableCell className="font-medium">
                      {/* The one working surface: the register row opens the portal detail. */}
                      <Link href={`/me/grievances/${g.id}`} className="hover:underline">
                        {g.grievanceNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{g.employeeName}</TableCell>
                    <TableCell className="max-w-md truncate">{g.subject}</TableCell>
                    <TableCell>{fmtDate(g.filedDate)}</TableCell>
                    <TableCell>
                      <div>{levelLabel(g.currentLevel)}</div>
                      {g.awaitingResponse && (
                        <div className="text-xs text-muted-foreground">awaiting a response</div>
                      )}
                    </TableCell>
                    <TableCell><StatusBadge status={g.statusName} /></TableCell>
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
