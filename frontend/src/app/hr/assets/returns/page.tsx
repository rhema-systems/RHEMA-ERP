'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Undo2 } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import type { AssetAssignmentSummary } from '@/types/hr/assets';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

function ReturnsTable({
  rows, emptyTitle, emptyBody,
}: { rows: AssetAssignmentSummary[]; emptyTitle: string; emptyBody: string }) {
  if (rows.length === 0) {
    return <EmptyState icon={Undo2} title={emptyTitle} description={emptyBody} />;
  }
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Assignment</TableHead>
          <TableHead>Asset</TableHead>
          <TableHead>Held by</TableHead>
          <TableHead>Issued</TableHead>
          <TableHead>Due back</TableHead>
          <TableHead>When</TableHead>
          <TableHead>Signed for</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((a) => (
          <TableRow key={a.id}>
            <TableCell>
              <Link href={`/hr/assets/assignments/${a.id}`} className="hover:underline">
                {a.assignmentNumber}
              </Link>
            </TableCell>
            <TableCell>
              <div className="font-medium">{a.assetName}</div>
              <div className="text-xs text-muted-foreground">{a.assetNumber}</div>
            </TableCell>
            <TableCell>{a.employeeName}</TableCell>
            <TableCell>{fmtDate(a.assignmentDate)}</TableCell>
            <TableCell>{fmtDate(a.expectedReturnDate)}</TableCell>
            <TableCell>
              {/* Server-computed and signed — negative is late. Never recomputed in the browser:
                  a page left open overnight would start disagreeing with the list it came from. */}
              {a.daysUntilReturnDue === null ? (
                <span className="text-muted-foreground">—</span>
              ) : a.daysUntilReturnDue < 0 ? (
                <span className="font-medium text-red-600 dark:text-red-500">
                  {Math.abs(a.daysUntilReturnDue)} days late
                </span>
              ) : (
                <span className={a.daysUntilReturnDue <= 3 ? 'text-amber-600 dark:text-amber-500' : ''}>
                  in {a.daysUntilReturnDue} days
                </span>
              )}
            </TableCell>
            <TableCell>
              {a.employeeAcknowledged
                ? fmtDate(a.acknowledgementDate)
                : <span className="text-amber-600 dark:text-amber-500">Not yet</span>}
            </TableCell>
            {/* ⚠ A late custody still reads `Active`, and that is correct: lateness is derived from
                a date, and `AssignmentStatus.Overdue` has no writer anywhere. Writing it would have
                emptied this very list, and made a late asset impossible to hand back at all. */}
            <TableCell><StatusBadge status={a.statusName} /></TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

/**
 * Returns — what is coming back, and what is already late.
 *
 * Two lists rather than one, for the reason the maintenance and insurance pairs give: "what is
 * coming back this fortnight" is the question somebody arranging a handover has, and "what have we
 * let slip" is the question somebody chasing has. Until slice 11 only the second existed, so a
 * return appeared on exactly one screen and only once it was already late.
 *
 * ⚠ The plan is **inclusive** of the late rows; the exception list is that population cut out and
 * ordered worst-first. It had no ordering at all before slice 11 — an exception list that cannot
 * put its worst row at the top is a list somebody reads once.
 */
export default function AssetReturnsPage() {
  const [daysAhead, setDaysAhead] = useState(14);

  const { data: due = [], isLoading: loadingDue } = useQuery({
    queryKey: ['hr', 'assets', 'returns', 'due', daysAhead],
    queryFn: () => assetRegisterService.getAssignmentsDueForReturn(daysAhead),
  });
  const { data: overdue = [], isLoading: loadingOverdue } = useQuery({
    queryKey: ['hr', 'assets', 'returns', 'overdue'],
    queryFn: () => assetRegisterService.getOverdueAssignments(),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Returns"
        description="What is coming back, and what is already late."
        backHref="/hr/assets"
      />

      <Tabs defaultValue="overdue">
        <TabsList>
          <TabsTrigger value="overdue">Already late ({overdue.length})</TabsTrigger>
          <TabsTrigger value="due">Coming due ({due.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="overdue" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {loadingOverdue ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (
                <ReturnsTable
                  rows={overdue}
                  emptyTitle="Nothing overdue"
                  emptyBody="Every custody with a return date is still inside it."
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="due" className="space-y-4 pt-4">
          <Card>
            <CardContent className="flex flex-wrap items-end gap-4 p-4">
              <div className="space-y-2">
                <Label>Looking ahead</Label>
                <Input
                  type="number"
                  min={1}
                  className="w-28"
                  value={daysAhead}
                  onChange={(e) => setDaysAhead(Math.max(1, Number(e.target.value) || 1))}
                />
              </div>
              <p className="pb-2 text-sm text-muted-foreground">
                days. This list <span className="font-medium">includes</span> custodies already
                late — somebody arranging handovers needs to see those first, not separately.
              </p>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-0">
              {loadingDue ? (
                <div className="flex justify-center p-10">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : (
                <ReturnsTable
                  rows={due}
                  emptyTitle="Nothing due back"
                  emptyBody={`No custody falls due inside the next ${daysAhead} days.`}
                />
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
