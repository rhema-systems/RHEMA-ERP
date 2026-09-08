'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { ClipboardList, Loader2, Plus } from 'lucide-react';
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
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { staffRequisitionService } from '@/services/hr/recruitment.service';
import { STAFF_REQUISITION_STATUSES, type StaffRequisitionStatus } from '@/types/hr/recruitment';

const ALL = '__all__';
const PAGE_SIZE = 20;

export default function StaffRequisitionsPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<string>(ALL);

  const summary = useQuery({
    queryKey: ['hr', 'requisitions', 'summary'],
    queryFn: () => staffRequisitionService.getStatusSummary(),
  });

  // Two shapes of read: the paged list has no filters server-side, and the by-status read is not
  // paged. Rather than paging in the client over a filtered set, each mode uses the endpoint
  // built for it.
  const paged = useQuery({
    queryKey: ['hr', 'requisitions', 'paged', page],
    queryFn: () => staffRequisitionService.getPaged(page, PAGE_SIZE),
    enabled: status === ALL,
  });

  const filtered = useQuery({
    queryKey: ['hr', 'requisitions', 'by-status', status],
    queryFn: () => staffRequisitionService.getByStatus(status as StaffRequisitionStatus),
    enabled: status !== ALL,
  });

  const rows = status === ALL ? (paged.data?.items ?? []) : (filtered.data ?? []);
  const isLoading = status === ALL ? paged.isLoading : filtered.isLoading;
  const totalPages = paged.data?.totalPages ?? 1;

  const s = summary.data;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Staff requisitions"
        description="Requests for headcount, from draft through approval to fulfilment."
        backHref="/hr/recruitment"
        actions={
          <Button onClick={() => router.push('/hr/recruitment/requisitions/new')}>
            <Plus className="mr-2 h-4 w-4" /> Raise a requisition
          </Button>
        }
      />

      {s && (
        <MetricTiles
          tiles={[
            { label: 'Total', value: s.totalRequisitions },
            { label: 'Draft', value: s.draft },
            { label: 'Awaiting approval', value: s.submitted + s.underReview },
            { label: 'Approved', value: s.approved },
            { label: 'Filled', value: s.fulfilled },
          ]}
        />
      )}

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
            <SelectItem value={ALL}>All requisitions</SelectItem>
            {STAFF_REQUISITION_STATUSES.map((st) => (
              <SelectItem key={st} value={st}>
                {humanizeEnum(st)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
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
                icon={ClipboardList}
                title="No requisitions"
                description={
                  status === ALL
                    ? 'Raise one when a team needs another head.'
                    : `Nothing is currently ${humanizeEnum(status)}.`
                }
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Position</TableHead>
                  <TableHead>Unit</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Priority</TableHead>
                  <TableHead className="text-right">Heads</TableHead>
                  <TableHead>Wanted by</TableHead>
                  <TableHead>Raised by</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((r) => (
                  <TableRow
                    key={r.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => router.push(`/hr/recruitment/requisitions/${r.id}`)}
                  >
                    <TableCell className="font-medium">
                      <Link href={`/hr/recruitment/requisitions/${r.id}`} className="hover:underline">
                        {r.requisitionNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{r.requisitionTitle}</TableCell>
                    <TableCell>{r.positionTitle || '—'}</TableCell>
                    <TableCell>{r.organizationUnitName || '—'}</TableCell>
                    <TableCell>{humanizeEnum(r.type)}</TableCell>
                    <TableCell>{r.priority}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {r.positionsFilled}/{r.numberOfPositions}
                    </TableCell>
                    <TableCell>{formatDate(r.desiredStartDate)}</TableCell>
                    <TableCell>{r.requestedByName || '—'}</TableCell>
                    <TableCell>
                      <StatusBadge status={r.status} />
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
    </div>
  );
}
