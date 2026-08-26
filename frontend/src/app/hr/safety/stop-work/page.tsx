'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, OctagonX, AlertTriangle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
import { safetyStopWorkService } from '@/services/hr/safety-stop-work.service';
import {
  SHE_STOP_WORK_STATUS_OPTIONS,
  type SheStopWorkOrder,
  type SheStopWorkStatus,
} from '@/types/hr/safety-audits';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

function StopWorkStatusBadge({ status }: { status: SheStopWorkStatus }) {
  switch (status) {
    case 'Cleared':
      return <Badge variant="secondary">Cleared — work resumed</Badge>;
    case 'Cancelled':
      return <Badge variant="outline">Cancelled</Badge>;
    case 'Resolved':
      return <Badge>Resolved — awaiting clearance</Badge>;
    case 'UnderReview':
      return <Badge variant="destructive">Under review — work stopped</Badge>;
    default:
      return <Badge variant="destructive">Raised — work stopped</Badge>;
  }
}

/**
 * The stop-work register (FR-SHE-200). Every open order is stopped work — the
 * queue at the top is the point of this screen. Orders are raised by anyone
 * (see Raise Stop-Work, open to all employees); HR routes, resolves and clears.
 */
export default function StopWorkRegisterPage() {
  const [status, setStatus] = useState<SheStopWorkStatus | 'all'>('all');

  const { data: orders = [], isLoading } = useQuery({
    queryKey: ['hr', 'safety-stop-work', 'list', status],
    queryFn: () => safetyStopWorkService.getAll(status === 'all' ? undefined : status),
  });

  const open = orders.filter((o) => o.status === 'Raised' || o.status === 'UnderReview' || o.status === 'Resolved');

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Stop-Work Orders"
        description="Work halted under stop-work authority. An order stays 'stopped' until the danger is resolved and resumption is cleared."
        backHref="/hr/safety"
        actions={
          <Button asChild variant="destructive">
            <Link href="/me/safety/report/stop-work">
              <OctagonX className="mr-2 h-4 w-4" />
              Raise stop-work
            </Link>
          </Button>
        }
      />

      {open.length > 0 && (
        <Card className="border-destructive">
          <CardHeader className="pb-2">
            <CardTitle className="text-destructive flex items-center gap-2 text-sm font-medium">
              <AlertTriangle className="h-4 w-4" />
              {open.length} order(s) with work currently stopped
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {open.map((o) => (
              <Link key={o.id} href={`/hr/safety/stop-work/${o.id}`}>
                <Badge variant="outline" className="hover:bg-accent cursor-pointer">
                  {o.orderNumber} · {fmtDate(o.raisedDate)}
                </Badge>
              </Link>
            ))}
          </CardContent>
        </Card>
      )}

      <div className="flex items-center gap-3">
        <Select value={status} onValueChange={(v) => setStatus(v as SheStopWorkStatus | 'all')}>
          <SelectTrigger className="w-56">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {SHE_STOP_WORK_STATUS_OPTIONS.map((o) => (
              <SelectItem key={o.value} value={o.value}>
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
            </div>
          ) : orders.length === 0 ? (
            <EmptyState
              title="No stop-work orders"
              description="Nobody has had to stop work — or nobody knows they can. The raise screen is open to every employee."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead className="max-w-sm">Work stopped</TableHead>
                  <TableHead>Raised by</TableHead>
                  <TableHead>Raised</TableHead>
                  <TableHead>Location</TableHead>
                  <TableHead>Routed to</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {orders.map((o: SheStopWorkOrder) => (
                  <TableRow key={o.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/safety/stop-work/${o.id}`} className="hover:underline">
                        <span className="font-mono">{o.orderNumber}</span>
                      </Link>
                    </TableCell>
                    <TableCell className="max-w-sm">
                      <span className="line-clamp-1">{o.workDescription}</span>
                    </TableCell>
                    <TableCell>{o.raisedByName}</TableCell>
                    <TableCell className="tabular-nums">{fmtDate(o.raisedDate)}</TableCell>
                    <TableCell>{o.locationName ?? '—'}</TableCell>
                    <TableCell>{o.routedToName ?? '—'}</TableCell>
                    <TableCell>
                      <StopWorkStatusBadge status={o.status} />
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
