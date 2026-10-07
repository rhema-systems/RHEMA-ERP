'use client';

import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import type { BookingStatus, MeetingRoom, RoomBusyTime } from '@/types/hr/company-schedule';

/**
 * One day of the bookable rooms (company schedule lane 3c, D-13): a row per room, its held times as blocks.
 *
 * ⚠ **Somebody else's booking is only a grey block.** The server sends no purpose, booker or number for it — the user's
 * ruling is that staff see others' bookings as busy times and nothing more. The viewer's own are coloured and open their
 * booking. An optional window — the one being booked — is drawn over its room (or over every room, while no room is
 * chosen), green where the room is free then and red where it is held.
 *
 * Times are the browser's, as on every booking screen here. Ghana's time is UTC, so the server's days and the board's
 * agree; callers ask the busy read for a day either side all the same, and the board clips to its own day.
 */

type BoardRoom = Pick<MeetingRoom, 'id' | 'roomName' | 'capacity' | 'locationName'>;

interface RoomDayBoardProps {
  /** `yyyy-MM-dd`, a local day. */
  day: string;
  rooms: BoardRoom[];
  busy: RoomBusyTime[];
  /** The window being booked, as ISO instants. With `roomId`, drawn over that room only. */
  proposed?: { start: string; end: string; roomId?: string | null } | null;
}

const HOUR = 3_600_000;
const pad = (n: number) => String(n).padStart(2, '0');

/** A local day as `yyyy-MM-dd`. */
export function localDay(d: Date = new Date()): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** The local day `n` days from `day`. */
export function shiftDay(day: string, n: number): string {
  const d = new Date(`${day}T00:00`);
  d.setDate(d.getDate() + n);
  return localDay(d);
}

const hhmm = (t: number) => new Date(t).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

export function RoomDayBoard({ day, rooms, busy, proposed }: RoomDayBoardProps) {
  const dayStart = new Date(`${day}T00:00`).getTime();
  const dayEnd = new Date(`${shiftDay(day, 1)}T00:00`).getTime();

  const held = busy
    .map((b) => ({ ...b, s: new Date(b.startDateTime).getTime(), e: new Date(b.endDateTime).getTime() }))
    .filter((b) => b.s < dayEnd && b.e > dayStart);
  const win = proposed
    ? { s: new Date(proposed.start).getTime(), e: new Date(proposed.end).getTime(), roomId: proposed.roomId ?? null }
    : null;
  const shownWindow = win && win.e > win.s && win.s < dayEnd && win.e > dayStart ? win : null;

  // 07:00–19:00, widened to whole hours around anything on the day.
  let first = 7;
  let last = 19;
  for (const x of [...held, ...(shownWindow ? [shownWindow] : [])]) {
    first = Math.min(first, Math.floor((Math.max(x.s, dayStart) - dayStart) / HOUR));
    last = Math.max(last, Math.ceil((Math.min(x.e, dayEnd) - dayStart) / HOUR));
  }
  const from = dayStart + first * HOUR;
  const span = (last - first) * HOUR;
  const left = (t: number) => `${((Math.max(t, from) - from) / span) * 100}%`;
  const width = (s: number, e: number) => `${((Math.min(e, from + span) - Math.max(s, from)) / span) * 100}%`;
  const step = last - first > 14 ? 2 : 1;
  const ticks = Array.from({ length: last - first + 1 }, (_, i) => first + i).filter((h) => (h - first) % step === 0);

  if (rooms.length === 0) {
    return <p className="text-sm text-muted-foreground">No room is open for booking.</p>;
  }

  return (
    <div className="space-y-2">
      <div className="hidden sm:grid sm:grid-cols-[11rem_1fr] sm:gap-3">
        <div />
        <div className="relative h-4 text-[10px] text-muted-foreground">
          {ticks.map((h) => (
            <span key={h} className="absolute -translate-x-1/2" style={{ left: left(dayStart + h * HOUR) }}>
              {pad(h % 24)}:00
            </span>
          ))}
        </div>
      </div>

      {rooms.map((room) => {
        const roomHeld = held.filter((b) => b.roomId === room.id);
        const overlay = shownWindow && (!shownWindow.roomId || shownWindow.roomId === room.id) ? shownWindow : null;
        const clash = overlay ? roomHeld.some((b) => b.s < overlay.e && b.e > overlay.s) : false;
        return (
          <div key={room.id} className="grid gap-1 sm:grid-cols-[11rem_1fr] sm:items-center sm:gap-3">
            <div className="min-w-0 text-sm">
              <div className="truncate font-medium">{room.roomName}</div>
              <div className="truncate text-xs text-muted-foreground">
                {room.locationName} · seats {room.capacity}
              </div>
            </div>
            <div className="relative h-9 overflow-hidden rounded-md border bg-muted/30">
              {ticks.map((h) => (
                <div
                  key={h}
                  className="absolute inset-y-0 border-l border-dashed border-border/70"
                  style={{ left: left(dayStart + h * HOUR) }}
                />
              ))}
              {roomHeld.map((b, i) => {
                const label = `${b.isMine ? 'Your booking' : 'Held'}, ${hhmm(b.s)}–${hhmm(b.e)}`;
                const style = { left: left(b.s), width: width(b.s, b.e) };
                return b.isMine && b.bookingId ? (
                  <Link
                    key={b.bookingId}
                    href={`/me/room-bookings/${b.bookingId}`}
                    title={label}
                    aria-label={label}
                    className="absolute inset-y-1 rounded bg-primary/80 hover:bg-primary"
                    style={style}
                  />
                ) : (
                  <div
                    key={`${b.startDateTime}-${i}`}
                    title={label}
                    aria-label={label}
                    className="absolute inset-y-1 rounded bg-muted-foreground/40"
                    style={style}
                  />
                );
              })}
              {overlay && (
                <div
                  title={clash ? 'Your time — the room is held then' : 'Your time — the room is free then'}
                  className={cn(
                    'pointer-events-none absolute inset-y-0 rounded border-2 border-dashed',
                    clash ? 'border-destructive bg-destructive/10' : 'border-emerald-600 bg-emerald-500/10',
                  )}
                  style={{ left: left(overlay.s), width: width(overlay.s, overlay.e) }}
                />
              )}
            </div>
          </div>
        );
      })}

      <div className="flex flex-wrap gap-4 pt-1 text-xs text-muted-foreground">
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-5 rounded bg-muted-foreground/40" /> Held
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-5 rounded bg-primary/80" /> Your booking
        </span>
        {shownWindow && (
          <span className="flex items-center gap-1.5">
            <span className="inline-block h-3 w-5 rounded border-2 border-dashed border-emerald-600" /> The time you are booking
          </span>
        )}
      </div>
    </div>
  );
}

/** A booking's status as the booker reads it: Tentative is "Awaiting approval" in this module. */
export function bookingStatusWord(status: BookingStatus, isCancelled?: boolean): string {
  if (isCancelled || status === 'Cancelled') return 'Cancelled';
  switch (status) {
    case 'Tentative':
      return 'Awaiting approval';
    case 'NoShow':
      return 'No show';
    default:
      return status;
  }
}

/** What a room offers and asks: seats, site, facilities, its limits, and whether a booking waits for approval. */
export function RoomRules({ room }: { room: MeetingRoom }) {
  const facilities = [
    room.hasProjector && 'Projector',
    room.hasWhiteboard && 'Whiteboard',
    room.hasVideoConference && 'Video conferencing',
    room.hasAudioSystem && 'Audio system',
    room.hasAirConditioning && 'Air conditioning',
  ].filter(Boolean) as string[];
  return (
    <div className="space-y-2 rounded-md border p-3 text-sm">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-medium">{room.roomName}</span>
        {room.requiresApproval && <Badge variant="secondary">Needs approval</Badge>}
      </div>
      <p className="text-muted-foreground">
        {[room.locationName, room.location, room.building, room.floor && `floor ${room.floor}`].filter(Boolean).join(' · ')}
        {' · '}seats {room.capacity}
      </p>
      {(facilities.length > 0 || room.otherFacilities) && (
        <p className="text-muted-foreground">{[...facilities, room.otherFacilities].filter(Boolean).join(', ')}</p>
      )}
      {(!!room.maxBookingDurationHours || !!room.advanceBookingDays) && (
        <p className="text-muted-foreground">
          {[
            room.maxBookingDurationHours ? `At most ${room.maxBookingDurationHours} hour(s) at a time` : null,
            room.advanceBookingDays ? `up to ${room.advanceBookingDays} day(s) ahead` : null,
          ]
            .filter(Boolean)
            .join(', ')}
          .
        </p>
      )}
      {room.requiresApproval && (
        <p className="text-muted-foreground">
          A booking waits until the HR desk approves it, and you are told either way. One still waiting when its time comes
          is cancelled.
        </p>
      )}
    </div>
  );
}
