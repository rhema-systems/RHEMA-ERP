'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, Plane, Clock, CalendarClock, Globe } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelType, StaffTravelRequestSummary } from '@/types/hr/travel';

const TRAVEL_TYPES: StaffTravelType[] = ['Domestic', 'International', 'CrossBorder', 'Regional'];

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * Money is rendered from the row's OWN currency code — never a hardcoded symbol. Travel spans
 * currencies by nature, and the request carries the one it was costed in.
 */
const fmtMoney = (amount?: number | null, currency?: string) =>
  amount === null || amount === undefined
    ? '—'
    : new Intl.NumberFormat(undefined, {
        style: 'currency',
        currency: currency || 'GHS',
        currencyDisplay: 'code',
      }).format(amount);

type QuickView = 'all' | 'pending-approval' | 'upcoming';

const VIEWS: { key: QuickView; label: string; icon: typeof Clock; hint: string }[] = [
  { key: 'all', label: 'All requests', icon: Plane, hint: 'Every travel request on record' },
  {
    key: 'pending-approval',
    label: 'Pending approval',
    icon: Clock,
    hint: 'Submitted and waiting on an approver',
  },
  {
    key: 'upcoming',
    label: 'Departing soon',
    icon: CalendarClock,
    hint: 'Approved trips leaving within 30 days',
  },
];

export default function TravelRegisterPage() {
  const [view, setView] = useState<QuickView>('all');
  const [type, setType] = useState<StaffTravelType | 'all'>('all');

  const { data, isLoading } = useQuery({
    queryKey: ['travel-requests', view],
    queryFn: () => {
      if (view === 'pending-approval') return travelService.getPendingApproval();
      if (view === 'upcoming') return travelService.getUpcoming(30);
      return travelService.getAll();
    },
  });

  const activeView = VIEWS.find((v) => v.key === view) ?? VIEWS[0];
  const items: StaffTravelRequestSummary[] = (data ?? []).filter(
    (r) => type === 'all' || r.travelType === type,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Staff travel"
        description="Travel requests, their approval, and the trips that follow."
        backHref="/hr"
        actions={
          <Button asChild>
            <Link href="/hr/travel/new">
              <Plus className="mr-2 h-4 w-4" />
              Raise request
            </Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        {VIEWS.map((v) => (
          <Button
            key={v.key}
            variant={view === v.key ? 'default' : 'outline'}
            size="sm"
            onClick={() => setView(v.key)}
          >
            <v.icon className="mr-2 h-4 w-4" />
            {v.label}
          </Button>
        ))}
      </div>

      <Card>
        <CardContent className="p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-muted-foreground">{activeView.hint}</p>
            <Select value={type} onValueChange={(v) => setType(v as StaffTravelType | 'all')}>
              <SelectTrigger className="w-56">
                <SelectValue placeholder="All types" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All types</SelectItem>
                {TRAVEL_TYPES.map((t) => (
                  <SelectItem key={t} value={t}>
                    {t.replace(/([A-Z])/g, ' $1').trim()}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              title="No travel requests"
              description={
                view === 'all'
                  ? 'Nothing has been raised yet.'
                  : 'Nothing is in this queue at the moment.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Traveller</TableHead>
                  <TableHead>Route</TableHead>
                  <TableHead>Dates</TableHead>
                  <TableHead className="text-right">Estimated</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((r) => (
                  <TableRow key={r.id} className="cursor-pointer">
                    <TableCell className="font-medium">
                      <Link href={`/hr/travel/${r.id}`} className="hover:underline">
                        {r.requestNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{r.employeeName || '—'}</TableCell>
                    <TableCell>
                      <span className="flex items-center gap-1.5">
                        {r.isInternational && (
                          <Globe className="h-3.5 w-3.5 text-muted-foreground" aria-label="International" />
                        )}
                        {r.originCity} → {r.destinationCity}
                        <span className="text-muted-foreground">
                          {r.destinationCountryName ? `, ${r.destinationCountryName}` : ''}
                        </span>
                      </span>
                    </TableCell>
                    <TableCell className="whitespace-nowrap">
                      {fmtDate(r.travelStartDate)} – {fmtDate(r.travelEndDate)}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(r.estimatedTotalCost, r.currencyCode)}
                    </TableCell>
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
    </div>
  );
}
