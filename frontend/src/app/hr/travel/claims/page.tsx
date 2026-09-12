'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Receipt, Banknote, ListChecks } from 'lucide-react';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { travelFinanceService } from '@/services/hr/travel-finance.service';

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtMoney = (amount?: number | null, currency?: string) =>
  amount === null || amount === undefined
    ? '—'
    : new Intl.NumberFormat(undefined, {
        style: 'currency', currency: currency || 'GHS', currencyDisplay: 'code',
      }).format(amount);

type View = 'all' | 'unpaid-approved';

/**
 * Travel expense claims across every trip — the finance desk's view.
 *
 * The "awaiting payment" queue is a purpose-built endpoint rather than a status filter, because
 * "approved and not yet paid" is the desk's actual worklist and the one it is measured on.
 */
export default function TravelClaimsPage() {
  const [view, setView] = useState<View>('all');

  const { data, isLoading } = useQuery({
    queryKey: ['travel-claims-register', view],
    queryFn: () =>
      view === 'unpaid-approved'
        ? travelFinanceService.getUnpaidApprovedClaims()
        : travelFinanceService.getClaims(),
  });

  const items = data ?? [];
  const payable = items.reduce((sum, c) => sum + (c.netPayable ?? 0), 0);
  // Only meaningful when every row shares a currency — say nothing rather than add up mixed ones.
  const currencies = new Set(items.map((c) => c.currencyCode));
  const oneCurrency = currencies.size === 1 ? [...currencies][0] : null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Travel expense claims"
        description="What travellers have claimed back, and what is waiting to be paid."
        backHref="/hr/travel"
      />

      <div className="flex flex-wrap gap-2">
        <Button
          variant={view === 'all' ? 'default' : 'outline'}
          size="sm"
          onClick={() => setView('all')}
        >
          <ListChecks className="mr-2 h-4 w-4" /> All claims
        </Button>
        <Button
          variant={view === 'unpaid-approved' ? 'default' : 'outline'}
          size="sm"
          onClick={() => setView('unpaid-approved')}
        >
          <Banknote className="mr-2 h-4 w-4" /> Awaiting payment
        </Button>
      </div>

      {view === 'unpaid-approved' && items.length > 0 && oneCurrency && (
        <Card>
          <CardContent className="p-4">
            <p className="text-sm">
              <span className="font-medium">{items.length}</span> claim
              {items.length === 1 ? '' : 's'} approved and unpaid, totalling{' '}
              <span className="font-medium">{fmtMoney(payable, oneCurrency)}</span>.
            </p>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={Receipt}
              title="No claims"
              description={
                view === 'all'
                  ? 'Nothing has been claimed back yet.'
                  : 'Nothing is waiting to be paid.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Traveller</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead className="text-right">Claimed</TableHead>
                  <TableHead className="text-right">Payable</TableHead>
                  <TableHead>Submitted</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/travel/claims/${c.id}`} className="hover:underline">
                        {c.claimNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{c.employeeName || '—'}</TableCell>
                    <TableCell>{humanize(c.claimTypeName)}</TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(c.totalClaimed, c.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(c.netPayable, c.currencyCode)}
                    </TableCell>
                    <TableCell className="whitespace-nowrap">{fmtDate(c.submittedAt)}</TableCell>
                    <TableCell><StatusBadge status={humanize(c.statusName)} /></TableCell>
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
