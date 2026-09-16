'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Briefcase, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { JOB_VACANCY_STATUSES, type JobVacancyStatus } from '@/types/hr/recruitment';

const ALL = '__all__';
const PAGE_SIZE = 20;

export default function JobVacanciesPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<string>(ALL);

  const paged = useQuery({
    queryKey: ['hr', 'vacancies', 'paged', page],
    queryFn: () => jobVacancyService.getPaged(page, PAGE_SIZE),
    enabled: status === ALL,
  });

  const filtered = useQuery({
    queryKey: ['hr', 'vacancies', 'by-status', status],
    queryFn: () => jobVacancyService.getByStatus(status as JobVacancyStatus),
    enabled: status !== ALL,
  });

  const rows = status === ALL ? (paged.data?.items ?? []) : (filtered.data ?? []);
  const isLoading = status === ALL ? paged.isLoading : filtered.isLoading;
  const totalPages = paged.data?.totalPages ?? 1;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Vacancies"
        description="Approved requisitions turned into advertised roles."
        backHref="/hr/recruitment"
      />

      <div className="flex items-center gap-3">
        <Select
          value={status}
          onValueChange={(v) => {
            setStatus(v);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-[220px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>All vacancies</SelectItem>
            {JOB_VACANCY_STATUSES.map((st) => (
              <SelectItem key={st} value={st}>
                {humanizeEnum(st)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="text-sm text-muted-foreground">
          A vacancy is opened from an approved requisition, not created on its own.
        </p>
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <div className="py-10">
              <EmptyState
                icon={Briefcase}
                title="No vacancies"
                description={
                  status === ALL
                    ? 'Approve a requisition, then open a vacancy from it.'
                    : `Nothing is currently ${humanizeEnum(status)}.`
                }
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Job title</TableHead>
                  <TableHead>Position</TableHead>
                  <TableHead>Unit</TableHead>
                  <TableHead>Requisition</TableHead>
                  <TableHead className="text-right">Heads</TableHead>
                  <TableHead>Deadline</TableHead>
                  <TableHead className="text-right">Applications</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((v) => (
<TableRow
                    key={v.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => router.push(`/hr/recruitment/vacancies/${v.id}`)}
                  >
                    <TableCell className="font-medium">
                      <Link href={`/hr/recruitment/vacancies/${v.id}`} className="hover:underline">
                        {v.vacancyNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{v.jobTitle || '—'}</TableCell>
                    <TableCell>{v.positionTitle || '—'}</TableCell>
                    <TableCell>{v.orgUnitName || '—'}</TableCell>
                    <TableCell>
                      <Link
                        href={`/hr/recruitment/requisitions/${v.staffRequisitionId}`}
                        onClick={(e) => e.stopPropagation()}
                        className="text-primary hover:underline"
                      >
                        {v.requisitionNumber || '—'}
                      </Link>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{v.numberOfPositions}</TableCell>
                    <TableCell>{formatDate(v.applicationDeadline)}</TableCell>
                    <TableCell className="text-right tabular-nums">{v.applicationCount}</TableCell>
                    <TableCell>
                      <StatusBadge status={v.vacancyStatus} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {status === ALL && totalPages > 1 && (
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={!paged.data?.hasPrevious}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            Previous
          </Button>
          <span className="text-sm text-muted-foreground">
            Page {paged.data?.page ?? page} of {totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={!paged.data?.hasNext}
            onClick={() => setPage((p) => p + 1)}
          >
            Next
          </Button>
        </div>
      )}

      {/* G-4.6 / G-5.8: choosing a status switches to a dedicated endpoint that is not paged and
          returns every matching row. The applications list already disclosed this; copying the
          disclosure beats pretending the view is bounded when it is not. */}
      {status !== ALL && rows.length > 0 && (
        <p className="text-xs text-muted-foreground">
          Filtering by status uses an unpaged endpoint — all {rows.length} matching vacancies are
          shown.
        </p>
      )}
    </div>
  );
}
