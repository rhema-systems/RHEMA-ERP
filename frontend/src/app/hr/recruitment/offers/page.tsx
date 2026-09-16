'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Mail } from 'lucide-react';
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
import { formatDate, formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobOfferService } from '@/services/hr/offers.service';
import { JOB_OFFER_STATUSES, type JobOfferStatus } from '@/types/hr/offers';

const ALL = '__all__';
const EXPIRING = '__expiring__';

const PAGE_SIZE = 20;

/**
 * Every offer in the tenant.
 *
 * ⚠ **G-10.3 (2026-09-15): the default view is paged now.** It used to call `getAll()` — unfiltered
 * and unpaged, every offer in the tenant in one response, with no pager, no total and no disclosure
 * of any kind. That was worse than the requisitions and vacancies lists, where the unbounded read
 * only fires once a filter is chosen; here it was simply what loaded when the screen opened, and it
 * made this the only recruitment list with no bound at all.
 *
 * The status and expiring views still run their own purpose-built unpaged reads, and say so below —
 * the disclosure pattern the applications list established (§ 8.2 of the system guide).
 */
export default function JobOffersPage() {
  const router = useRouter();
  const [view, setView] = useState<string>(ALL);
  const [page, setPage] = useState(1);

  const all = useQuery({
    queryKey: ['hr', 'offers', 'paged', page],
    queryFn: () => jobOfferService.getPaged(page, PAGE_SIZE),
    enabled: view === ALL,
  });

  const byStatus = useQuery({
    queryKey: ['hr', 'offers', 'status', view],
    queryFn: () => jobOfferService.getByStatus(view as JobOfferStatus),
    enabled: view !== ALL && view !== EXPIRING,
  });

  const expiring = useQuery({
    queryKey: ['hr', 'offers', 'expiring'],
    queryFn: () => jobOfferService.getExpiring(7),
    enabled: view === EXPIRING,
  });

  const active = view === ALL ? all : view === EXPIRING ? expiring : byStatus;
  // The paged read returns { items, totalCount, … }; the two filtered reads return a bare array.
  const rows = view === ALL ? (all.data?.items ?? []) : ((active.data as any[]) ?? []);
  const totalCount = all.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Offers"
        description="Terms raised against an application, from draft through approval to the candidate's response."
        backHref="/hr/recruitment"
      />

      <div className="flex items-center gap-3">
        <Select
          value={view}
          onValueChange={(v) => {
            setView(v);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-[220px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>All offers</SelectItem>
            <SelectItem value={EXPIRING}>
              <span className="flex items-center gap-1.5">
                <Mail className="h-3.5 w-3.5" /> Expiring within 7 days
              </span>
            </SelectItem>
            {JOB_OFFER_STATUSES.map((s) => (
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
                title="No offers"
                description={
                  view === ALL
                    ? 'Offers are raised from an application — open one and use “Extend an offer”.'
                    : 'Nothing matches this view.'
                }
              />
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Offer</TableHead>
                  <TableHead>Candidate</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead className="text-right">Salary</TableHead>
                  <TableHead>Start date</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead className="w-[150px]">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((o) => (
                  <TableRow
                    key={o.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => router.push(`/hr/recruitment/offers/${o.id}`)}
                  >
                    <TableCell>
                      <Link
                        href={`/hr/recruitment/offers/${o.id}`}
                        className="font-medium text-primary hover:underline"
                      >
                        {o.offerNumber}
                      </Link>
                      {o.version > 1 && (
                        <span className="ml-1.5 text-xs text-muted-foreground">v{o.version}</span>
                      )}
                    </TableCell>
                    <TableCell>{o.candidateName}</TableCell>
                    <TableCell>
                      <div>{o.positionTitle}</div>
                      <div className="text-xs text-muted-foreground">{humanizeEnum(o.employmentType)}</div>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {o.baseSalary != null ? formatMoney(o.baseSalary, o.currencyCode ?? 'GHS') : '—'}
                    </TableCell>
                    <TableCell>{formatDate(o.proposedStartDate)}</TableCell>
                    <TableCell>{formatDate(o.expiryDate)}</TableCell>
                    <TableCell>
                      <StatusBadge status={o.offerStatus} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {view === ALL && totalCount > 0 && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>
            {totalCount} offer{totalCount === 1 ? '' : 's'} · page {page} of {totalPages}
          </span>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1 || all.isFetching}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= totalPages || all.isFetching}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            >
              Next
            </Button>
          </div>
        </div>
      )}

      {/* G-4.6 / G-5.8 / G-10.3 — the disclosure the applications list established. Choosing a
          status or the expiring view switches to a dedicated endpoint that is not paged and
          returns every matching row. That is the shape the API offers; saying so beats pretending
          the view is bounded when it is not. */}
      {view !== ALL && rows.length > 0 && (
        <p className="text-sm text-muted-foreground">
          This view uses an unpaged endpoint — all {rows.length} matching offers are shown at once.
        </p>
      )}
    </div>
  );
}
