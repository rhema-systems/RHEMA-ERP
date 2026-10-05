'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { fmtTravelMoney } from '@/components/hr/travel/travel-format';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelApprovalQueueItem } from '@/types/hr/travel';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const decidesAsLabel = (item: StaffTravelApprovalQueueItem) => {
  switch (item.decidesAs) {
    case 'LineAuthority':
      return item.relation ? `Line manager (${item.relation})` : 'Line manager';
    case 'TravelDesk':
      return 'Travel desk — no line manager can';
    default:
      return 'Approver';
  }
};

const waitingLabel = (days: number) => (days <= 0 ? 'Today' : days === 1 ? '1 day' : `${days} days`);

/**
 * Travel requests waiting for YOUR decision (travel final closure, lane 2 — D-7, finding O-1).
 *
 * Open to everyone, deliberately: the traveller's line manager holds no travel permission, and this queue
 * is how they find what waits for them. The server builds it request by request with the same rule the
 * approve, reject and return actions run — the workflow engine first, then the line rule — so every row
 * opens and can be decided; it lists nobody else's work. The decision itself is taken on the request's
 * own page, where the budget is asked for at the last stage.
 */
export default function TravelApprovalsPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['travel-requests', 'my-approvals', page],
    queryFn: () => travelService.getMyApprovals({ pageNumber: page, pageSize: 20 }),
  });

  const rows = data?.items ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Travel Approvals"
        description="Travel requests waiting for your decision — as the traveller's line manager, for the travel desk, or as HR."
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : isError && !data ? (
            <div className="p-6">
              <TravelQueryError error={error} what="the travel requests waiting for you" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              title="Nothing is waiting for you"
              description="When a trip needs your decision it appears here, and you are notified."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Request</TableHead>
                  <TableHead>Traveller</TableHead>
                  <TableHead>Trip</TableHead>
                  <TableHead className="text-right">Estimated</TableHead>
                  <TableHead>Stage</TableHead>
                  <TableHead>You decide as</TableHead>
                  <TableHead>Waiting</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((item) => {
                  const r = item.request;
                  return (
                    <TableRow
                      key={r.id}
                      className="cursor-pointer"
                      onClick={() => router.push(`/hr/travel/${r.id}`)}
                    >
                      <TableCell className="font-medium">
                        <Link href={`/hr/travel/${r.id}`} onClick={(e) => e.stopPropagation()}>
                          {r.requestNumber}
                        </Link>
                      </TableCell>
                      <TableCell>{r.employeeName}</TableCell>
                      <TableCell>
                        <div>
                          {item.originCity ? `${item.originCity} → ` : ''}
                          {r.destinationCity}
                          {r.destinationCountryName ? `, ${r.destinationCountryName}` : ''}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {fmtDate(r.travelStartDate)} – {fmtDate(r.travelEndDate)}
                        </div>
                      </TableCell>
                      <TableCell className="text-right">
                        {fmtTravelMoney(r.estimatedTotalCost, r.currencyCode)}
                      </TableCell>
                      <TableCell>
                        <span>{item.stageName ?? 'Approval'}</span>
                        {item.isFinalStage && (
                          <Badge variant="outline" className="ml-2">
                            sets the budget
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell>{decidesAsLabel(item)}</TableCell>
                      <TableCell>{waitingLabel(item.daysWaiting)}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-end gap-2">
          <Button variant="outline" size="sm" disabled={!data.hasPrevious} onClick={() => setPage((p) => p - 1)}>
            Previous
          </Button>
          <span className="text-sm text-muted-foreground">
            Page {data.page} of {data.totalPages}
          </span>
          <Button variant="outline" size="sm" disabled={!data.hasNext} onClick={() => setPage((p) => p + 1)}>
            Next
          </Button>
        </div>
      )}
    </div>
  );
}
