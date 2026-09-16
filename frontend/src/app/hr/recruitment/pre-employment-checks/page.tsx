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
 * rather than opening every conditional offer to see what's stuck.
 *
 * ⚠ **G-11.4 (2026-09-15): this screen diagnosed and could not cure.** It existed to find
 * outstanding clearances and offered no way to act on any of them — every row had to be opened via
 * its offer, then the checks tab, then the item. Rows and offer links now deep-link straight to
 * `?tab=checks`, so the queue lands you on the thing it just pointed at rather than on Overview.
 *
 * The recording of a result stays on the check item itself, deliberately: a clearance result is
 * evidence about a named person, and a bulk "mark these done" control over a list is the wrong
 * shape for it. What was wrong was the number of clicks between finding the work and doing it, not
 * the absence of a bulk action.
 *
 * ⚠ See also G-11.1 — the default status this screen opens on is one nothing in the application
 * ever writes, which is why the queue reads empty on a real tenant and full on the demo one.
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
                    onClick={() =>
                      router.push(`/hr/recruitment/offers/${c.jobOfferId}?tab=checks`)
                    }
                  >
                    <TableCell className="font-medium">{c.candidateName}</TableCell>
                    <TableCell>
                      <Link
                        href={`/hr/recruitment/offers/${c.jobOfferId}?tab=checks`}
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
