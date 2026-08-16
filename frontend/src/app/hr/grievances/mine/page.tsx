'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { grievanceService } from '@/services/hr/grievance.service';
import { GRIEVANCE_LADDER, type GrievanceSummary } from '@/types/hr/grievance';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const levelLabel = (v: string) => GRIEVANCE_LADDER.find((l) => l.value === v)?.label ?? v;

function GrievanceTable({ rows, emptyTitle, emptyBody, showWho = false }: {
  rows: GrievanceSummary[];
  emptyTitle: string;
  emptyBody: string;
  showWho?: boolean;
}) {
  if (rows.length === 0) return <EmptyState title={emptyTitle} description={emptyBody} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Reference</TableHead>
          {showWho && <TableHead>Raised by</TableHead>}
          <TableHead>Subject</TableHead>
          <TableHead>Filed</TableHead>
          <TableHead>Currently with</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((g) => (
          <TableRow key={g.id}>
            <TableCell className="font-medium">
              <Link href={`/hr/grievances/${g.id}`} className="hover:underline">
                {g.grievanceNumber}
              </Link>
            </TableCell>
            {showWho && <TableCell>{g.employeeName}</TableCell>}
            <TableCell className="max-w-md truncate">{g.subject}</TableCell>
            <TableCell>{fmtDate(g.filedDate)}</TableCell>
            <TableCell>{levelLabel(g.currentLevel)}</TableCell>
            <TableCell><StatusBadge status={g.statusName} /></TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * The employee's own grievance page: what they have raised, and what they have been asked to answer.
 *
 * The two lists sit together because they are the two ways an ordinary employee meets this module —
 * as the person who raised something, and as the person a grievance has been passed to. Neither can
 * be reached from the register, which answers 403 for anyone outside HR.
 */
export default function MyGrievancesPage() {
  const mine = useQuery({
    queryKey: ['hr', 'grievances', 'mine'],
    queryFn: () => grievanceService.getMine(),
  });

  const toAnswer = useQuery({
    queryKey: ['hr', 'grievances', 'awaiting-my-response'],
    queryFn: () => grievanceService.getAwaitingMyResponse(),
  });

  const spinner = (
    <div className="flex items-center justify-center p-10">
      <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
    </div>
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="My grievances"
        description="Grievances you have raised, and any you have been asked to answer."
        backHref="/hr"
        actions={
          <Button asChild>
            <Link href="/hr/grievances/new">
              <Plus className="mr-2 h-4 w-4" />
              Raise a grievance
            </Link>
          </Button>
        }
      />

      <Card>
        <CardHeader><CardTitle>Raised by me</CardTitle></CardHeader>
        <CardContent className="p-0">
          {mine.isLoading ? spinner : (
            <GrievanceTable
              rows={mine.data ?? []}
              emptyTitle="You have not raised a grievance"
              emptyBody="Anything you raise appears here, along with the response at each level."
            />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>Waiting for my response</CardTitle></CardHeader>
        <CardContent className="p-0">
          {toAnswer.isLoading ? spinner : (
            <GrievanceTable
              rows={toAnswer.data ?? []}
              showWho
              emptyTitle="Nothing waiting on you"
              emptyBody="You have not been asked to answer a grievance."
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
