'use client';

import { useState } from 'react';
import Link from 'next/link';
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

/**
 * Every offer in the tenant. ⚠ No general paged search on this controller — `getAll` is unfiltered
 * and unpaged, so the status and expiring views run their own purpose-built reads rather than
 * filtering client-side over one large list.
 */
export default function JobOffersPage() {
  const [view, setView] = useState<string>(ALL);

  const all = useQuery({
    queryKey: ['hr', 'offers', 'all'],
    queryFn: () => jobOfferService.getAll(),
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
  const rows = active.data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Offers"
        description="Terms raised against an application, from draft through approval to the candidate's response."
        backHref="/hr/recruitment"
      />

      <div className="flex items-center gap-3">
        <Select value={view} onValueChange={setView}>
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
                  <TableRow key={o.id} className="cursor-pointer">
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
    </div>
  );
}
