'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { AlarmClock, HandCoins, ListChecks, Loader2, Wallet } from 'lucide-react';
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
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { fmtTravelMoney as fmtMoney } from '@/components/hr/travel/travel-format';
import { travelFinanceService } from '@/services/hr/travel-finance.service';
import type { StaffTravelAdvanceSummary } from '@/types/hr/travel-finance';

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

type View = 'overdue' | 'outstanding' | 'all';

const isCashOut = (a: StaffTravelAdvanceSummary) =>
  ['Disbursed', 'PartiallySettled', 'Overdue'].includes(a.status) && a.unsettledAmount > 0;

const daysPast = (deadline?: string | null) => {
  if (!deadline) return null;
  const days = Math.floor((Date.now() - new Date(deadline).getTime()) / 86_400_000);
  return days > 0 ? days : null;
};

/**
 * Travel advances across every trip — the desk's chase list (travel final closure, lane 3, D-5).
 *
 * "Overdue" is the server's own read: cash still out past its settlement deadline, whether or not the
 * nightly sweep has marked the advance Overdue yet. An advance is worked from its trip's Finance tab —
 * cash handed back, a claim that recovers it, or a write-off — so each row links to the trip.
 */
export default function TravelAdvancesPage() {
  const [view, setView] = useState<View>('overdue');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: view === 'overdue' ? ['travel-overdue-settlements'] : ['travel-advances-register'],
    queryFn: () =>
      view === 'overdue'
        ? travelFinanceService.getOverdueSettlements()
        : travelFinanceService.getAdvances(),
  });

  const items = (data ?? []).filter((a) => view !== 'outstanding' || isCashOut(a));
  const owed = items.reduce((sum, a) => sum + (isCashOut(a) ? a.unsettledAmount : 0), 0);
  // Only meaningful when every row shares a currency — say nothing rather than add up mixed ones.
  const currencies = new Set(items.map((a) => a.currencyCode));
  const oneCurrency = currencies.size === 1 ? [...currencies][0] : null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Travel advances"
        description="Cash paid out ahead of trips — what travellers still hold, and what is past its deadline."
        backHref="/hr/travel"
      />

      <div className="flex flex-wrap gap-2">
        <Button variant={view === 'overdue' ? 'default' : 'outline'} size="sm" onClick={() => setView('overdue')}>
          <AlarmClock className="mr-2 h-4 w-4" /> Overdue settlements
        </Button>
        <Button variant={view === 'outstanding' ? 'default' : 'outline'} size="sm" onClick={() => setView('outstanding')}>
          <Wallet className="mr-2 h-4 w-4" /> Cash out
        </Button>
        <Button variant={view === 'all' ? 'default' : 'outline'} size="sm" onClick={() => setView('all')}>
          <ListChecks className="mr-2 h-4 w-4" /> All advances
        </Button>
      </div>

      {view !== 'all' && items.length > 0 && oneCurrency && (
        <Card>
          <CardContent className="p-4">
            <p className="text-sm">
              <span className="font-medium">{items.length}</span> advance{items.length === 1 ? '' : 's'}{' '}
              {view === 'overdue' ? 'past the deadline' : 'with cash out'}, travellers holding{' '}
              <span className="font-medium">{fmtMoney(owed, oneCurrency)}</span>.
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
          ) : isError && !data ? (
            <div className="p-4">
              <TravelQueryError error={error} what="the advances" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={HandCoins}
              title={view === 'overdue' ? 'Nothing overdue' : 'No advances'}
              description={
                view === 'overdue'
                  ? 'Every advance with cash out is within its settlement deadline.'
                  : view === 'outstanding'
                    ? 'No traveller is holding advance cash.'
                    : 'No advance has been requested yet.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Traveller</TableHead>
                  <TableHead>Trip</TableHead>
                  <TableHead className="text-right">Approved</TableHead>
                  <TableHead className="text-right">Outstanding</TableHead>
                  <TableHead>Settle by</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((a) => {
                  const late = isCashOut(a) ? daysPast(a.settlementDeadline) : null;
                  return (
                    <TableRow key={a.id}>
                      <TableCell className="font-medium">{a.advanceNumber}</TableCell>
                      <TableCell>{a.employeeName || '—'}</TableCell>
                      <TableCell>
                        <Link href={`/hr/travel/${a.staffTravelRequestId}`} className="hover:underline">
                          {a.requestNumber ?? 'Open the trip'}
                        </Link>
                      </TableCell>
                      <TableCell className="text-right whitespace-nowrap">
                        {fmtMoney(a.approvedAmount, a.currencyCode)}
                      </TableCell>
                      <TableCell className="text-right whitespace-nowrap">
                        {fmtMoney(a.unsettledAmount, a.currencyCode)}
                      </TableCell>
                      <TableCell className="whitespace-nowrap">
                        {fmtDate(a.settlementDeadline)}
                        {late && <p className="text-xs text-destructive">{late} day{late === 1 ? '' : 's'} late</p>}
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap items-center gap-1">
                          <StatusBadge status={humanize(a.statusName)} />
                          {a.isOverdue && a.status !== 'Overdue' && <StatusBadge status="Overdue" />}
                        </div>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">
        An advance is settled from its trip&apos;s Finance tab: a claim that names it recovers it when the claim is
        paid, cash handed back is recorded there, and a travel administrator can write off what is left. A
        traveller with an overdue advance can take no new one.
      </p>
    </div>
  );
}
