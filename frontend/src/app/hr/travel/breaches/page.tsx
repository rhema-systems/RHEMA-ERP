'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Loader2, ShieldAlert, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
import { TravelReasonDialog } from '@/components/hr/travel/TravelReasonDialog';
import { useTravelAccess } from '@/components/hr/travel/useTravelAccess';
import { useToast } from '@/hooks/use-toast';
import { travelBookingsService } from '@/services/hr/travel-bookings.service';
import type { StaffTravelBookingException, TravelBookingExceptionState } from '@/types/hr/travel-bookings';

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

type View = 'Pending' | 'all';

const stateLabel: Record<TravelBookingExceptionState, string> = {
  None: '—',
  Pending: 'Awaiting authorisation',
  Authorised: 'Authorised',
  Refused: 'Refused',
};

/**
 * The policy-breach register (travel final closure, lane 4, D-8): every flight and hotel booking that breaches its
 * trip's policy — a cabin class or nightly rate above the cap, or booked later than the policy asks.
 *
 * A breaching booking is saved awaiting authorisation and cannot be confirmed or ticketed until a travel
 * administrator decides it: not the one who booked it or asked for the exception, and not the traveller — the
 * server refuses them with the reason, so the buttons are offered to every administrator and the answer explains
 * itself. Refusing needs a reason, kept on the trip as an internal note; the booking is then changed or cancelled
 * from its trip.
 */
export default function TravelBreachesPage() {
  const [view, setView] = useState<View>('Pending');
  const [refusing, setRefusing] = useState<StaffTravelBookingException | null>(null);
  const access = useTravelAccess();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['travel-booking-exceptions', view],
    queryFn: () => travelBookingsService.getBookingExceptions(view === 'Pending' ? 'Pending' : undefined),
  });

  const decide = useMutation({
    mutationFn: ({ row, authorise, reason }: { row: StaffTravelBookingException; authorise: boolean; reason?: string }) =>
      travelBookingsService.decideBookingException(row.kind, row.bookingId, authorise, reason),
    onSuccess: async (_, { authorise }) => {
      toast({ title: authorise ? 'Exception authorised' : 'Exception refused' });
      await queryClient.invalidateQueries({ queryKey: ['travel-booking-exceptions'] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not record the decision', description: e.message }),
  });

  const items = data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Policy breaches"
        description="Bookings above the travel policy's caps, or booked later than it asks, and who decided them."
        actions={
          <div className="flex gap-2">
            <Button variant={view === 'Pending' ? 'default' : 'outline'} onClick={() => setView('Pending')}>
              Awaiting authorisation
            </Button>
            <Button variant={view === 'all' ? 'default' : 'outline'} onClick={() => setView('all')}>
              All
            </Button>
          </div>
        }
      />

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : isError && !data ? (
            <div className="p-4"><TravelQueryError error={error} what="the policy breaches" /></div>
          ) : items.length === 0 ? (
            <EmptyState
              title={view === 'Pending' ? 'Nothing awaiting authorisation' : 'No policy breaches'}
              description="A booking that breaches its trip's policy appears here once the desk asks for an exception."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Trip</TableHead>
                  <TableHead>Booking</TableHead>
                  <TableHead>Policy cap</TableHead>
                  <TableHead>Why</TableHead>
                  <TableHead>Exception</TableHead>
                  {access.canAdmin && <TableHead className="w-48" />}
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((row) => (
                  <TableRow key={`${row.kind}-${row.bookingId}`}>
                    <TableCell>
                      <Link href={`/hr/travel/${row.staffTravelRequestId}`} className="font-medium hover:underline">
                        {row.requestNumber}
                      </Link>
                      <div className="text-xs text-muted-foreground">
                        {row.travellerName} · departs {fmtDate(row.travelStartDate)}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{row.booking}</div>
                      <div className="mt-1"><StatusBadge status={humanize(row.bookingStatusName)} /></div>
                    </TableCell>
                    <TableCell>{row.policyCap ?? '—'}</TableCell>
                    <TableCell className="max-w-xs whitespace-pre-wrap text-sm">
                      {row.reason ?? '—'}
                      {row.requestedByName && (
                        <div className="text-xs text-muted-foreground">asked by {row.requestedByName}</div>
                      )}
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={row.exceptionState === 'Refused' ? 'destructive'
                          : row.exceptionState === 'Pending' ? 'outline' : 'secondary'}
                      >
                        {stateLabel[row.exceptionState]}
                      </Badge>
                      {row.decidedByName && (
                        <div className="mt-1 text-xs text-muted-foreground">
                          {row.decidedByName}, {fmtDate(row.decidedAt)}
                        </div>
                      )}
                    </TableCell>
                    {access.canAdmin && (
                      <TableCell>
                        {row.exceptionState === 'Pending' && (
                          <div className="flex gap-2">
                            <Button
                              size="sm"
                              disabled={decide.isPending}
                              onClick={() => decide.mutate({ row, authorise: true })}
                            >
                              <Check className="mr-1 h-4 w-4" /> Authorise
                            </Button>
                            <Button size="sm" variant="outline" onClick={() => setRefusing(row)}>
                              <X className="mr-1 h-4 w-4" /> Refuse
                            </Button>
                          </div>
                        )}
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <p className="flex items-start gap-2 text-xs text-muted-foreground">
        <ShieldAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" />
        A travel administrator decides an exception — never the one who booked it or asked for it, and never the
        traveller. Until it is authorised the booking stays Pending and cannot be confirmed or ticketed.
      </p>

      <TravelReasonDialog
        open={!!refusing}
        onOpenChange={(v) => !v && setRefusing(null)}
        title={`Refuse the exception${refusing ? ` on ${refusing.requestNumber}` : ''}`}
        description={refusing
          ? `${refusing.booking} — the booking stays as it is and cannot be confirmed or ticketed; the desk changes or cancels it. The reason is kept on the trip as an internal note.`
          : ''}
        minLength={5}
        placeholder="At least five characters"
        confirmLabel="Refuse"
        destructive
        pending={decide.isPending}
        onConfirm={(reason) => {
          if (!refusing) return Promise.resolve();
          return decide.mutateAsync({ row: refusing, authorise: false, reason });
        }}
      />
    </div>
  );
}
