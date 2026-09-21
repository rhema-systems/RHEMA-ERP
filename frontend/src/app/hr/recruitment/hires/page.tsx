'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { CalendarClock, Loader2 } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobHireService } from '@/services/hr/offers.service';
import { JOB_HIRE_STATUSES, type JobHireStatus } from '@/types/hr/offers';

const STARTING_SOON = '__starting_soon__';

/**
 * Hire records. ⚠ **There is no general list endpoint on this controller** — only by id, number,
 * application, employee, status, and start-approaching. This is a view picker over those purpose-
 * built reads, the same shape the interviews screen uses for the same reason.
 */
export default function JobHiresPage() {
  const router = useRouter();
  const [view, setView] = useState<string>(STARTING_SOON);

  const startApproaching = useQuery({
    queryKey: ['hr', 'hires', 'start-approaching'],
    queryFn: () => jobHireService.getStartApproaching(30),
    enabled: view === STARTING_SOON,
  });

  const byStatus = useQuery({
    queryKey: ['hr', 'hires', 'status', view],
    queryFn: () => jobHireService.getByStatus(view as JobHireStatus),
    enabled: view !== STARTING_SOON,
  });

  const active = view === STARTING_SOON ? startApproaching : byStatus;
  const rows = active.data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Hires"
        description="The handover from recruitment to employment — start dates, onboarding, and confirming an employee record."
        backHref="/hr/recruitment"
      />

      <div className="flex items-center gap-3">
        <Select value={view} onValueChange={setView}>
          <SelectTrigger className="w-[240px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={STARTING_SOON}>
              <span className="flex items-center gap-1.5">
                <CalendarClock className="h-3.5 w-3.5" /> Starting within 30 days
              </span>
            </SelectItem>
            {JOB_HIRE_STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {humanizeEnum(s)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Card>
        <CardContent className="p-0">
          {active.isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <div className="py-10">
              <EmptyState
                title="No hires"
                description={
                  view === STARTING_SOON
                    ? 'Nothing due to start in the next 30 days.'
                    : 'Nothing matches this view.'
                }
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Hire</TableHead>
                  <TableHead>Candidate</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Expected start</TableHead>
                  <TableHead>Actual start</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead className="w-[170px]">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((h) => (
                  <TableRow
                    key={h.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => router.push(`/hr/recruitment/hires/${h.id}`)}
                  >
                    <TableCell>
                      <Link
                        href={`/hr/recruitment/hires/${h.id}`}
                        className="font-medium text-primary hover:underline"
                      >
                        {h.hireNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{h.candidateName}</TableCell>
                    <TableCell>{h.positionTitle}</TableCell>
                    <TableCell>{formatDate(h.expectedStartDate)}</TableCell>
                    <TableCell>{formatDate(h.actualStartDate)}</TableCell>
                    <TableCell>{h.employeeNumber || '—'}</TableCell>
                    <TableCell>
                      <StatusBadge status={h.status} />
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
