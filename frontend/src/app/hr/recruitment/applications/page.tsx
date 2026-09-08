'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { FileText, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate } from '@/lib/hr/attendance-format';
import { jobVacancyService } from '@/services/hr/recruitment.service';
import { jobApplicationService } from '@/services/hr/recruitment-pipeline.service';
import { APPLICATION_STATUSES, type ApplicationStatus } from '@/types/hr/recruitment-pipeline';

const ALL = '__all__';

/**
 * Applications across every vacancy.
 *
 * The vacancy filter goes into the paged read as a query parameter; the status filter uses the
 * dedicated `status/{status}` route, which is not paged — so choosing a status switches the list to
 * the unpaged read rather than combining the two. That is the shape the API offers, and pretending
 * otherwise would mean filtering a single page client-side and reporting the wrong totals.
 */
export default function ApplicationsPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [vacancyId, setVacancyId] = useState<string>(ALL);
  const [status, setStatus] = useState<string>(ALL);

  const vacancies = useQuery({
    queryKey: ['hr', 'vacancies', 'all'],
    queryFn: () => jobVacancyService.getAll(),
  });

  const byStatus = status !== ALL;

  const paged = useQuery({
    queryKey: ['hr', 'applications', 'paged', page, vacancyId],
    queryFn: () => jobApplicationService.getPaged(page, 20, vacancyId === ALL ? undefined : vacancyId),
    enabled: !byStatus,
  });

  const filtered = useQuery({
    queryKey: ['hr', 'applications', 'by-status', status, vacancyId],
    queryFn: () =>
      jobApplicationService.getByStatus(
        status as ApplicationStatus,
        vacancyId === ALL ? undefined : vacancyId,
      ),
    enabled: byStatus,
  });

  const rows = byStatus ? filtered.data ?? [] : paged.data?.items ?? [];
  const isLoading = byStatus ? filtered.isLoading : paged.isLoading;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Applications"
        description="Every application across all vacancies. Work a single vacancy from its pipeline board instead."
        backHref="/hr/recruitment"
      />

      <Card>
        <CardContent className="flex flex-wrap gap-4 pt-6">
          <div className="min-w-[260px] space-y-1.5">
            <Label>Vacancy</Label>
            <Select
              value={vacancyId}
              onValueChange={(v) => {
                setVacancyId(v);
                setPage(1);
              }}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>All vacancies</SelectItem>
                {(vacancies.data ?? []).map((v) => (
                  <SelectItem key={v.id} value={v.id}>
                    {v.jobTitle || v.vacancyNumber}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="min-w-[220px] space-y-1.5">
            <Label>Status</Label>
            <Select
              value={status}
              onValueChange={(v) => {
                setStatus(v);
                setPage(1);
              }}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Any status</SelectItem>
                {APPLICATION_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {s.replace(/([a-z])([A-Z])/g, '$1 $2')}
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
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={FileText}
              title="No applications"
              description="Nothing matches these filters."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-36">Number</TableHead>
                  <TableHead>Candidate</TableHead>
                  <TableHead>Vacancy</TableHead>
                  <TableHead className="w-32">Applied</TableHead>
                  <TableHead className="w-40">Status</TableHead>
                  <TableHead className="w-36">Stage</TableHead>
                  <TableHead className="w-24 text-right">Score</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((a) => (
<TableRow
                    key={a.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => router.push(`/hr/recruitment/applications/${a.id}`)}
                  >
                    <TableCell className="font-mono text-xs">
                      <Link href={`/hr/recruitment/applications/${a.id}`} className="hover:underline">
                        {a.applicationNumber}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{a.candidateName || '—'}</div>
                      <div className="text-xs text-muted-foreground">{a.candidateEmail}</div>
                    </TableCell>
                    <TableCell className="text-sm">{a.jobTitle || a.vacancyNumber}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {formatDate(a.applicationDate)}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={a.statusName} />
                    </TableCell>
                    <TableCell className="text-sm">{a.currentStageName ?? '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {a.autoScore != null ? (
                        <span className={a.scoreIsStale ? 'text-muted-foreground' : undefined}>
                          {a.autoScore.toFixed(1)}
                          {a.scoreIsStale && <span title="Score is stale"> *</span>}
                        </span>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {!byStatus && (paged.data?.totalCount ?? 0) > 0 && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Page {paged.data?.page ?? 1} of {paged.data?.totalPages ?? 1} ·{' '}
            {paged.data?.totalCount ?? 0} applications
          </span>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={!paged.data?.hasPrevious}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={!paged.data?.hasNext}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </div>
      )}

      {byStatus && rows.length > 0 && (
        <p className="text-xs text-muted-foreground">
          Filtering by status uses an unpaged endpoint — all {rows.length} matching applications are
          shown.
        </p>
      )}
    </div>
  );
}
