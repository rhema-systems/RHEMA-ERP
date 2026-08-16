'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { disciplineService } from '@/services/hr/discipline.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * The employee's own disciplinary record — the only discipline screen a non-HR user can open.
 *
 * It reads `cases/mine`, which takes the employee from the token. There is deliberately no way to
 * ask for someone else's: an id-bearing route here is exactly the shape that let any authenticated
 * user read anyone's record before this area was gated.
 *
 * The tone is deliberately plain. This page shows a person the allegations recorded against them,
 * which is a document they may need to answer, so it states facts and dates and offers no framing.
 */
export default function MyDisciplineCasesPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['hr', 'discipline', 'mine'],
    queryFn: () => disciplineService.getMine(),
  });

  const items = data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="My disciplinary record"
        description="Cases raised in respect of you, and how each one stands."
        backHref="/hr"
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : isError ? (
            <EmptyState
              title="This record is not available"
              description="Your user account may not be linked to an employee record. Your HR team can check that for you."
            />
          ) : items.length === 0 ? (
            <EmptyState
              title="Nothing on record"
              description="No disciplinary case has been raised in respect of you."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Case</TableHead>
                  <TableHead>Offence</TableHead>
                  <TableHead>Incident</TableHead>
                  <TableHead>Reported</TableHead>
                  <TableHead>Outcome</TableHead>
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
                    <TableCell>{c.offenseName}</TableCell>
                    <TableCell>{fmtDate(c.incidentDate)}</TableCell>
                    <TableCell>{fmtDate(c.reportedDate)}</TableCell>
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
