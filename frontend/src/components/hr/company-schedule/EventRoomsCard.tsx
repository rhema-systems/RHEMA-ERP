'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { DoorClosed, Loader2, Plus, Repeat } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { bookingStatusWord } from '@/components/hr/company-schedule/RoomDayBoard';
import { useAuth } from '@/hooks/use-auth';
import { roomBookingService } from '@/services/hr/company-schedule.service';
import type { CompanyEventDetail } from '@/types/hr/company-schedule';

const span = (startIso: string, endIso: string) => {
  const s = new Date(startIso);
  const e = new Date(endIso);
  const t = (d: Date) => d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  return `${s.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' })}, ${t(s)}–${t(e)}`;
};

/**
 * The rooms booked for an event (company schedule lane 3d-2, the user's ruling): the event page never showed its room.
 *
 * Every booking made for it, any status, linked to its page; and, for whoever may book (`HR.Company.Write`) while the
 * event is still open, "Book a room" — the booking form with the event already chosen — and, on a date of a series, "Book
 * for this and following dates", which opens it with that scope.
 */
export function EventRoomsCard({ event, open }: { event: CompanyEventDetail; open: boolean }) {
  const { hasPermission } = useAuth();
  const canBook = open && hasPermission('HR.Company.Write');
  const inSeries = !!event.recurrenceSeriesId;
  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'company-schedule', 'bookings', 'event', event.id],
    queryFn: () => roomBookingService.getByEvent(event.id),
  });
  const book = `/hr/company-schedule/bookings/new?event=${event.id}`;
  const bookings = data ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3 space-y-0">
        <CardTitle>Rooms</CardTitle>
        {canBook && (
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" size="sm" asChild>
              <Link href={book}>
                <Plus className="mr-2 h-4 w-4" /> Book a room
              </Link>
            </Button>
            {inSeries && (
              <Button variant="outline" size="sm" asChild>
                <Link href={`${book}&scope=ThisAndFollowing`}>
                  <Repeat className="mr-2 h-4 w-4" /> Book for this and following dates
                </Link>
              </Button>
            )}
          </div>
        )}
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        {isLoading ? (
          <div className="flex items-center gap-2 text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Loading its rooms…
          </div>
        ) : bookings.length === 0 ? (
          <p className="flex items-center gap-2 text-muted-foreground">
            <DoorClosed className="h-4 w-4" /> No room is booked for this event.
          </p>
        ) : (
          <ul className="divide-y">
            {bookings.map((b) => (
              <li key={b.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                <div className="min-w-0">
                  <Link href={`/hr/company-schedule/bookings/${b.id}`} className="font-medium hover:underline">
                    {b.roomName}
                  </Link>
                  <span className="text-muted-foreground"> · {b.bookingNumber} · {span(b.startDateTime, b.endDateTime)}</span>
                </div>
                <StatusBadge status={bookingStatusWord(b.status)} />
              </li>
            ))}
          </ul>
        )}
        {inSeries && (
          <p className="text-xs text-muted-foreground">
            Extending the series books the latest date&apos;s rooms for the new dates too, where they are free.
          </p>
        )}
      </CardContent>
    </Card>
  );
}
