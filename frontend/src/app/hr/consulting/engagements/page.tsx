'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Handshake, Loader2 } from 'lucide-react';
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
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { clientEngagementService } from '@/services/hr/consultant.service';
import { formatDate, formatMoney } from '@/lib/hr/attendance-format';
import { ENGAGEMENT_STATUS_OPTIONS } from '@/types/hr/consultant';
import type { ClientEngagementStatus, ClientEngagementSummary } from '@/types/hr/consultant';

const ACTIVE = '__active__';
const ENDING = '__ending__';
const ALL = '__all__';

/**
 * All engagements across clients. New engagements are created from the client that owns
 * them, so this screen is a cross-client view and the lifecycle actions live on the detail
 * page.
 */
export default function EngagementsPage() {
  const router = useRouter();
  const [filter, setFilter] = useState<string>(ACTIVE);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'client-engagements', 'list', filter],
    queryFn: () => {
      if (filter === ACTIVE) return clientEngagementService.getActive();
      if (filter === ENDING) return clientEngagementService.getEndingWithin(30);
      if (filter === ALL) return clientEngagementService.getPaged(1, 100).then((p) => p.items);
      return clientEngagementService.getByStatus(filter as ClientEngagementStatus);
    },
  });

  const rows: ClientEngagementSummary[] = data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Client Engagements"
        description="Placement contracts across all clients, with their billing terms."
        backHref="/hr/consulting"
      />

      <Card>
        <CardHeader>
          <CardTitle>Filter</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="max-w-xs space-y-2">
            <label className="text-sm font-medium">Show</label>
            <Select value={filter} onValueChange={setFilter}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ACTIVE}>Active</SelectItem>
                <SelectItem value={ENDING}>Ending within 30 days</SelectItem>
                <SelectItem value={ALL}>All engagements</SelectItem>
                {ENGAGEMENT_STATUS_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>
              {rows.length} {rows.length === 1 ? 'engagement' : 'engagements'}
            </CardTitle>
            {isFetching && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead>Title</TableHead>
                  <TableHead>Client</TableHead>
                  <TableHead>Consultant</TableHead>
                  <TableHead>Starts</TableHead>
                  <TableHead>Ends</TableHead>
                  <TableHead className="text-right">Rate</TableHead>
                  <TableHead>Billing</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(9)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[70px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={9}>
                      <EmptyState
                        icon={Handshake}
                        title="No engagements"
                        description="Nothing matches this filter. Engagements are created from a client's page."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((e) => (
                    <TableRow
                      key={e.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/consulting/engagements/${e.id}`)}
                    >
                      <TableCell className="font-medium">{e.engagementCode}</TableCell>
                      <TableCell>{e.title}</TableCell>
                      <TableCell>{e.clientName}</TableCell>
                      <TableCell>{e.consultantName}</TableCell>
                      <TableCell>{formatDate(e.startDate)}</TableCell>
                      <TableCell>{formatDate(e.endDate)}</TableCell>
                      <TableCell className="text-right">
                        {formatMoney(e.hourlyRate, e.currency)}
                      </TableCell>
                      <TableCell>{e.billingCycle}</TableCell>
                      <TableCell>
                        <StatusBadge status={e.status} />
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
