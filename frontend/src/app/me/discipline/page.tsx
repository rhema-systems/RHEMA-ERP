'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Scale } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { disciplineService, disciplineAppealService } from '@/services/hr/discipline.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * The employee's own disciplinary record — the only discipline screen a non-HR user can open.
 *
 * Area 25 slice 9: re-homed from /hr/discipline/mine into the portal shell (D3). Rows now open
 * the PORTAL case detail (/me/discipline/[id]) — the subject's view — instead of dropping the
 * subject into the desk case-management screen. The appeals section rides `appeals/mine`, a read
 * that existed with no caller anywhere (found by the slice-9 survey).
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
    queryKey: ['me', 'discipline', 'cases'],
    queryFn: () => disciplineService.getMine(),
  });

  const { data: appeals = [] } = useQuery({
    queryKey: ['me', 'discipline', 'appeals'],
    queryFn: () => disciplineAppealService.getMine(),
  });

  const items = data ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Disciplinary Record"
        description="Cases raised in respect of you, and how each one stands. Open a case to read the notices served on you, the decision, and to appeal."
        backHref="/me"
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
                      <Link href={`/me/discipline/${c.id}`} className="hover:underline">
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

      {appeals.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Scale className="h-4 w-4" />
              Appeals you have filed
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Case</TableHead>
                  <TableHead>Filed</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Hearing</TableHead>
                  <TableHead>Outcome</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {appeals.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell className="font-medium">
                      <Link
                        href={`/me/discipline/${a.disciplinaryActionId}`}
                        className="hover:underline"
                      >
                        {a.caseNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{fmtDate(a.filedDate)}</TableCell>
                    <TableCell>
                      <StatusBadge status={a.appealStatusName} />
                    </TableCell>
                    <TableCell>{fmtDate(a.hearingDate)}</TableCell>
                    <TableCell>{a.appealOutcomeName ?? '—'}</TableCell>
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
