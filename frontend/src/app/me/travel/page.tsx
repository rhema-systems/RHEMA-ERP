'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Plus, Plane } from 'lucide-react';
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
import { travelService } from '@/services/hr/travel.service';
import { MyTravelAlertsPanel } from '@/components/hr/travel/MyTravelAlertsPanel';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { fmtTravelMoney as fmtMoney } from '@/components/hr/travel/travel-format';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * An employee's own travel.
 *
 * This reads `api/staff-travel/me`, which takes no employee id anywhere — the traveller is the
 * token. It is open to every authenticated employee; the register at `/hr/travel` answers 403 for
 * anyone without `HR.Travel.Read`, which is why this page exists rather than filtering that one.
 */
export default function MyTravelPage() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['my-travel-requests'],
    queryFn: () => travelService.getMine(),
  });

  const items = data ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="My travel"
        description="Trips you have requested, and where each one has got to."
        backHref="/me"
        actions={
          <Button asChild>
            <Link href="/me/travel/new">
              <Plus className="mr-2 h-4 w-4" />
              Request travel
            </Link>
          </Button>
        }
      />

      {/*
        Above the trip list on purpose: an unread security briefing about somewhere you are going
        is the most urgent thing on this page. The panel renders nothing when there are no alerts.
      */}
      <MyTravelAlertsPanel />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : isError && !data ? (
            <div className="p-4">
              <TravelQueryError error={error} what="your travel requests" />
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={Plane}
              title="No travel requests"
              description="You have not requested any travel yet."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Route</TableHead>
                  <TableHead>Dates</TableHead>
                  <TableHead className="text-right">Estimated</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="font-medium">
                      <Link href={`/me/travel/${r.id}`} className="hover:underline">
                        {r.requestNumber}
                      </Link>
                    </TableCell>
                    <TableCell>
                      {r.originCity} → {r.destinationCity}
                      <span className="text-muted-foreground">
                        {r.destinationCountryName ? `, ${r.destinationCountryName}` : ''}
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
