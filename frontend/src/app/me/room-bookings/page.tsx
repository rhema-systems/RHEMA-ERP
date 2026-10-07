'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, DoorClosed, Loader2, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
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
import {
  RoomDayBoard,
  bookingStatusWord,
  localDay,
  shiftDay,
} from '@/components/hr/company-schedule/RoomDayBoard';
import { myRoomBookingService } from '@/services/hr/company-schedule.service';
import type { RoomBookingSummary } from '@/types/hr/company-schedule';

const when = (startIso: string, endIso: string) => {
  const s = new Date(startIso);
  const e = new Date(endIso);
  const day = s.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
  const t = (d: Date) => d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  return s.toDateString() === e.toDateString()
    ? `${day}, ${t(s)}–${t(e)}`
    : `${day} ${t(s)} – ${e.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} ${t(e)}`;
};

/** Holds its room and has not ended: what "upcoming" means here. */
const isUpcoming = (b: RoomBookingSummary, now: number) =>
  (b.status === 'Tentative' || b.status === 'Confirmed') && new Date(b.endDateTime).getTime() > now;

/**
 * A member of staff's room bookings (company schedule lane 3c, D-13).
 *
 * Reads `api/CompanySchedule/me`, which takes no employee id anywhere — the booker is the token. The HR register at
 * `/hr/company-schedule/bookings` answers 403 to anyone without `HR.Company.Read`, which is why this page exists rather
 * than a filter on that one. Others' bookings appear only on the board, as grey blocks: no purpose, no booker.
 */
export default function MyRoomBookingsPage() {
  const [day, setDay] = useState(localDay());
  const [showPast, setShowPast] = useState(false);

  const { data: mine, isLoading } = useQuery({
    queryKey: ['me', 'room-bookings'],
    queryFn: () => myRoomBookingService.getMine(),
  });
  const { data: rooms } = useQuery({
    queryKey: ['me', 'room-bookings', 'rooms'],
    queryFn: () => myRoomBookingService.getRooms(),
  });
  const { data: busy, isFetching: busyLoading } = useQuery({
    queryKey: ['me', 'room-bookings', 'busy', day],
    queryFn: () => myRoomBookingService.getBusy(shiftDay(day, -1), shiftDay(day, 1)),
  });

  const { upcoming, past } = useMemo(() => {
    const now = Date.now();
    const all = mine ?? [];
    return {
      upcoming: all
        .filter((b) => isUpcoming(b, now))
        .sort((a, b) => a.startDateTime.localeCompare(b.startDateTime)),
      past: all.filter((b) => !isUpcoming(b, now)),
    };
  }, [mine]);
  const shown = showPast ? past : upcoming;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Room bookings"
        description="Book a meeting room, and keep track of your bookings."
        backHref="/me"
        actions={
          <Button asChild>
            <Link href="/me/room-bookings/new">
              <Plus className="mr-2 h-4 w-4" />
              Book a room
            </Link>
          </Button>
        }
      />

      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3 space-y-0">
          <CardTitle className="text-base">When the rooms are held</CardTitle>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="icon" aria-label="Previous day" onClick={() => setDay(shiftDay(day, -1))}>
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Input
              type="date"
              className="w-40"
              value={day}
              onChange={(e) => e.target.value && setDay(e.target.value)}
              aria-label="Day"
            />
            <Button variant="outline" size="icon" aria-label="Next day" onClick={() => setDay(shiftDay(day, 1))}>
              <ChevronRight className="h-4 w-4" />
            </Button>
            <Button variant="ghost" onClick={() => setDay(localDay())} disabled={day === localDay()}>
              Today
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          {!rooms || (busyLoading && !busy) ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" /> Loading the rooms…
            </div>
          ) : (
            <RoomDayBoard day={day} rooms={rooms} busy={busy ?? []} />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3 space-y-0">
          <CardTitle className="text-base">Your bookings</CardTitle>
          <div className="flex gap-2">
            <Button variant={showPast ? 'outline' : 'secondary'} size="sm" onClick={() => setShowPast(false)}>
              Upcoming ({upcoming.length})
            </Button>
            <Button variant={showPast ? 'secondary' : 'outline'} size="sm" onClick={() => setShowPast(true)}>
              Past and cancelled ({past.length})
            </Button>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : shown.length === 0 ? (
            <EmptyState
              icon={DoorClosed}
              title={showPast ? 'Nothing here yet' : 'No bookings to come'}
              description={showPast ? 'Bookings that have ended or were cancelled appear here.' : 'Book a room when you need one.'}
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Room</TableHead>
                  <TableHead>When</TableHead>
                  <TableHead>Purpose</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {shown.map((b) => (
                  <TableRow key={b.id}>
                    <TableCell className="font-medium">
                      <Link href={`/me/room-bookings/${b.id}`} className="hover:underline">
                        {b.bookingNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{b.roomName}</TableCell>
                    <TableCell className="whitespace-nowrap">{when(b.startDateTime, b.endDateTime)}</TableCell>
                    <TableCell className="max-w-xs truncate">{b.purpose}</TableCell>
                    <TableCell>
                      <StatusBadge status={bookingStatusWord(b.status)} />
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
