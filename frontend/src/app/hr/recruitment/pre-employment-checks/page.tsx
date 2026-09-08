'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Loader2, ShieldCheck } from 'lucide-react';
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
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { preEmploymentCheckService } from '@/services/hr/offers.service';
import { PRE_EMPLOYMENT_CHECK_STATUSES, type PreEmploymentCheckStatus } from '@/types/hr/offers';

/**
 * The cross-offer view of pre-employment checks — the queue HR chases outstanding clearances from,
 * rather than opening every conditional offer to see what's stuck. Defaults to In Progress, since
 * Pending/Completed/Failed/Waived read as "nothing to chase" or "already resolved".
 */
export default function PreEmploymentChecksQueuePage() {
  const router = useRouter();
  const [status, setStatus] = useState<PreEmploymentCheckStatus>('InProgress');

  const checks = useQuery({
    queryKey: ['hr', 'pre-employment-checks', 'status', status],
    queryFn: () => preEmploymentCheckService.getByStatus(status),
  });

  const rows = checks.data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Pre-employment checks"
        description="Every check across every conditional offer — chase what's outstanding without opening each offer in turn."
        backHref="/hr/recruitment"
      />

      <div className="flex items-center gap-3">
        <Select value={status} onValueChange={(v) => setStatus(v as PreEmploymentCheckStatus)}>
          <SelectTrigger className="w-[220px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {PRE_EMPLOYMENT_CHECK_STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {humanizeEnum(s)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Card>
        <CardContent className="p-0">
          {checks.isLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <div className="py-10">
              <EmptyState
                icon={ShieldCheck}
                title="Nothing here"
                description={`No checks are currently ${humanizeEnum(status).toLowerCase()}.`}
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Candidate</TableHead>
                  <TableHead>Offer</TableHead>
                  <TableHead>Coordinator</TableHead>
                  <TableHead className="text-right">Progress</TableHead>
                  <TableHead className="text-right">Failed</TableHead>
                  <TableHead className="w-[160px]">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((c) => (
                  <TableRow
                    key={c.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => router.push(`/hr/recruitment/offers/${c.jobOfferId}`)}
                  >
                    <TableCell className="font-medium">{c.candidateName}</TableCell>
                    <TableCell>
                      <Link
                        href={`/hr/recruitment/offers/${c.jobOfferId}`}
                        className="text-primary hover:underline"
                      >
                        {c.offerNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{c.coordinatedByName || '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {c.completedItems} / {c.totalItems}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {c.failedItems > 0 ? c.failedItems : '—'}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={c.overallStatus} />
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
