'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
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
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import {
  TRAVEL_REQUEST_STATUS_LABELS,
  TRAVEL_TYPE_LABELS,
  enumLabel,
  enumOptions,
} from '@/components/hr/travel/travel-enums';
import { fmtTravelMoney } from '@/components/hr/travel/travel-format';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelType, StaffTravelRequestSummary } from '@/types/hr/travel';

/**
 * ⚠ All eleven types, from the enum. The filter offered four, so a register of Field Visits or
 * Conferences could not be narrowed to them (travel final closure, lane 0).
 */
const TRAVEL_TYPE_OPTIONS = enumOptions(TRAVEL_TYPE_LABELS);

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

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

  // The employee profile's Travel tab links here as `?employeeId=…`. The register used to ignore
  // it and open on everyone's trips (travel final closure, lane 0 — finding F4).
  const searchParams = useSearchParams();
  const employeeId = searchParams?.get('employeeId') || null;

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['travel-requests', view, employeeId],
    queryFn: () => {
      if (view === 'pending-approval') return travelService.getPendingApproval();
      if (view === 'upcoming') return travelService.getUpcoming(30);
      return employeeId ? travelService.getByEmployee(employeeId) : travelService.getAll();
    },
  });

  const activeView = VIEWS.find((v) => v.key === view) ?? VIEWS[0];
  const items: StaffTravelRequestSummary[] = (data ?? []).filter(
    (r) =>
      (type === 'all' || r.travelType === type) && (!employeeId || r.employeeId === employeeId),
  );
  const employeeName = employeeId ? items[0]?.employeeName : null;

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

      {employeeId && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border bg-muted/40 px-4 py-3 text-sm">
          <span>
            Showing the trips of <strong>{employeeName || 'one employee'}</strong> only.
          </span>
          <Link href="/hr/travel" className="font-medium hover:underline">
            Show every traveller
          </Link>
        </div>
      )}

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
                {TRAVEL_TYPE_OPTIONS.map((t) => (
                  <SelectItem key={t.value} value={t.value}>
                    {t.label}
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
          ) : isError && !data ? (
            <div className="p-4">
              <TravelQueryError error={error} what="the travel requests" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              title="No travel requests"
              description={
                view === 'all' && !employeeId && type === 'all'
                  ? 'Nothing has been raised yet.'
                  : 'Nothing matches this view at the moment.'
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
                      {fmtTravelMoney(r.estimatedTotalCost, r.currencyCode)}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={enumLabel(TRAVEL_REQUEST_STATUS_LABELS, r.status)} />
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
