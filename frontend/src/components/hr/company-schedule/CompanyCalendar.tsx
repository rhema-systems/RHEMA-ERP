'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { CalendarPlus, ChevronLeft, ChevronRight, ExternalLink, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useAuth } from '@/hooks/use-auth';
import { IncompleteDiaryBanner } from '@/components/hr/company-schedule/IncompleteDiaryBanner';
import { InvitationAnswer } from '@/components/hr/company-schedule/InvitationAnswer';
import { addDay } from '@/components/hr/company-schedule/diaryDays';
import { entrySpan, layoutWeek, type PlacedBand } from '@/components/hr/company-schedule/calendarLayout';
import { companyCalendarService, meetingRoomService, myRoomBookingService } from '@/services/hr/company-schedule.service';
import type { CalendarEntry, CalendarEntryKind } from '@/types/hr/company-schedule';

/**
 * The company calendar (company-schedule final closure lane 7, D-7) — one component, two doors: the HR menu's Company
 * Calendar and the portal's. The server decides what each caller may see; this draws it.
 *
 * - **Month and week**, hand-built (no calendar library — `react-day-picker` is a picker, not a layout). An entry over
 *   several days is ONE band across them, broken at the end of each week (the user's ruling); bands are packed into
 *   lanes, four to a week row, the rest counted per day as "+n more" (which opens the week).
 * - **Filters are the legend:** each kind is a chip that hides or shows it.
 * - **Clicking an entry** opens its card: what, when, and — for an invitation the caller may still answer — Accept,
 *   Decline or "May attend" (D-8); for a series, which dates (D-12). Its page opens from there.
 * - **The room view** (C-26, C-35): pick a room to see when it is held (to staff, "booked" — nothing of anybody else's
 *   booking, lane 3c) and book it for the day (D-13).
 */

type View = 'month' | 'week';

const MAX_LANES = 4;
const WEEKDAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

const KIND_LABEL: Record<CalendarEntryKind, string> = {
  Event: 'Events',
  Closure: 'Closures',
  Milestone: 'Milestones',
  Holiday: 'Public holidays',
  RoomBooking: 'Room bookings',
  Mine: 'My leave, travel, panels and training',
};
const KINDS: CalendarEntryKind[] = ['Event', 'Closure', 'Holiday', 'Milestone', 'RoomBooking', 'Mine'];

/** A band's colours — solid for what is settled, dashed for an invitation still waiting for the caller's answer. */
function bandClass(e: CalendarEntry): string {
  if (e.kind === 'Event') {
    if (e.myParticipantId && (e.myAnswer === 'Sent' || e.myAnswer === 'NoResponse'))
      return 'border border-dashed border-blue-600 bg-blue-50 text-blue-900 dark:bg-blue-950 dark:text-blue-100';
    if (e.myAnswer === 'Declined') return 'bg-blue-200 text-blue-900 line-through dark:bg-blue-900 dark:text-blue-200';
    return 'bg-blue-600 text-white';
  }
  if (e.kind === 'Closure') return e.subKind === 'Partial' ? 'bg-orange-400 text-white' : 'bg-red-600 text-white';
  if (e.kind === 'Holiday') return 'bg-emerald-600 text-white';
  if (e.kind === 'Milestone') return 'bg-purple-600 text-white';
  if (e.kind === 'RoomBooking') return e.isMine ? 'bg-slate-700 text-white' : 'bg-slate-400 text-white';
  switch (e.subKind) {
    case 'Leave': return 'bg-amber-500 text-white';
    case 'Travel': return 'bg-cyan-600 text-white';
    case 'Interview': return 'bg-indigo-500 text-white';
    default: return 'bg-teal-600 text-white';
  }
}
const KIND_SWATCH: Record<CalendarEntryKind, string> = {
  Event: 'bg-blue-600',
  Closure: 'bg-red-600',
  Holiday: 'bg-emerald-600',
  Milestone: 'bg-purple-600',
  RoomBooking: 'bg-slate-500',
  Mine: 'bg-amber-500',
};

const today = () => new Date().toISOString().slice(0, 10);
const dayOf = (iso: string) => iso.slice(0, 10);
const hhmm = (iso: string) => iso.slice(11, 16);
/** Monday on or before a day. */
function mondayOf(day: string): string {
  const d = new Date(`${day}T00:00:00Z`);
  return addDay(day, -((d.getUTCDay() + 6) % 7));
}
function monthStart(day: string): string {
  return `${day.slice(0, 7)}-01`;
}
function shiftMonth(day: string, n: number): string {
  const d = new Date(`${monthStart(day)}T00:00:00Z`);
  d.setUTCMonth(d.getUTCMonth() + n);
  return d.toISOString().slice(0, 10);
}
const label = (day: string, opts: Intl.DateTimeFormatOptions) =>
  new Date(`${day}T00:00:00Z`).toLocaleDateString(undefined, { ...opts, timeZone: 'UTC' });

const span = entrySpan;
type Placed = PlacedBand<CalendarEntry>;

function Band({ placed, onOpen }: { placed: Placed; onOpen: (e: CalendarEntry) => void }) {
  const e = placed.entry;
  const timed = !e.isAllDay && placed.width === 1 && !placed.before;
  return (
    <button
      type="button"
      onClick={() => onOpen(e)}
      title={`${e.label}${e.isAllDay ? '' : ` · ${hhmm(e.start)}–${hhmm(e.end)}`}`}
      className={`truncate px-1.5 py-0.5 text-left text-[11px] leading-tight hover:opacity-90 ${bandClass(e)} ${
        placed.before ? 'rounded-l-none' : 'rounded-l'
      } ${placed.after ? 'rounded-r-none' : 'rounded-r'} ${e.awaitingApproval ? 'italic' : ''}`}
      style={{ gridColumn: `${placed.col + 1} / span ${placed.width}`, gridRow: placed.lane + 2 }}
    >
      {placed.before && '◂ '}
      {timed && `${hhmm(e.start)} `}
      {e.label}
      {placed.after && ' ▸'}
    </button>
  );
}

export function CompanyCalendar({ portal }: { portal: boolean }) {
  const { hasPermission } = useAuth();
  const deskReader = hasPermission('HR.Company.Read');
  const deskWriter = hasPermission('HR.Company.Write');
  const [view, setView] = useState<View>('month');
  const [cursor, setCursor] = useState(today());
  const [hidden, setHidden] = useState<Set<CalendarEntryKind>>(new Set());
  const [roomId, setRoomId] = useState<string>('');
  const [open, setOpen] = useState<CalendarEntry | null>(null);

  const range = useMemo(() => {
    if (view === 'week') {
      const from = mondayOf(cursor);
      return { from, to: addDay(from, 6) };
    }
    const from = mondayOf(monthStart(cursor));
    return { from, to: addDay(from, 41) };
  }, [view, cursor]);

  const calendar = useQuery({
    queryKey: ['hr', 'company-calendar', range.from, range.to, roomId],
    queryFn: () => companyCalendarService.get(range.from, range.to, roomId || null),
    retry: false,
  });

  // The rooms to look at: the desk's register, or the rooms staff may book.
  const rooms = useQuery({
    queryKey: ['hr', 'company-calendar', 'rooms', deskReader],
    queryFn: () => (deskReader ? meetingRoomService.getAll() : myRoomBookingService.getRooms()),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  const shown = useMemo(
    () => (calendar.data?.entries ?? []).filter((e) => !hidden.has(e.kind)),
    [calendar.data, hidden],
  );
  const toggle = (k: CalendarEntryKind) =>
    setHidden((h) => {
      const next = new Set(h);
      if (next.has(k)) next.delete(k);
      else next.add(k);
      return next;
    });

  const weeks = useMemo(() => {
    const out: string[][] = [];
    for (let d = range.from; d <= range.to; d = addDay(d)) {
      if (!out.length || out[out.length - 1].length === 7) out.push([]);
      out[out.length - 1].push(d);
    }
    return out;
  }, [range]);

  const step = (n: number) => setCursor((c) => (view === 'week' ? addDay(c, 7 * n) : shiftMonth(c, n)));
  const title =
    view === 'week'
      ? `${label(range.from, { day: 'numeric', month: 'short' })} – ${label(range.to, { day: 'numeric', month: 'short', year: 'numeric' })}`
      : label(monthStart(cursor), { month: 'long', year: 'numeric' });
  const bookDay = view === 'week' ? (today() >= range.from && today() <= range.to ? today() : range.from) : today();
  const bookHref = (room: string, day: string) =>
    !portal && deskWriter
      ? `/hr/company-schedule/bookings/new?room=${room}&date=${day}`
      : `/me/room-bookings/new?room=${room}&date=${day}`;

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="flex flex-wrap items-center gap-2 pt-6">
          <Button variant="outline" size="icon" onClick={() => step(-1)} aria-label="Previous">
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button variant="outline" onClick={() => setCursor(today())}>Today</Button>
          <Button variant="outline" size="icon" onClick={() => step(1)} aria-label="Next">
            <ChevronRight className="h-4 w-4" />
          </Button>
          <h2 className="ml-2 min-w-48 text-lg font-semibold">{title}</h2>
          <div className="ml-auto flex flex-wrap items-center gap-2">
            <div className="flex rounded-md border">
              <Button variant={view === 'month' ? 'secondary' : 'ghost'} size="sm" onClick={() => setView('month')}>Month</Button>
              <Button variant={view === 'week' ? 'secondary' : 'ghost'} size="sm" onClick={() => setView('week')}>Week</Button>
            </div>
            <Select value={roomId || '__all__'} onValueChange={(v) => v && setRoomId(v === '__all__' ? '' : v)}>
              <SelectTrigger className="w-56">
                <SelectValue placeholder="Every room" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all__">{deskReader ? 'Every room' : 'My bookings only'}</SelectItem>
                {(rooms.data ?? []).map((r) => (
                  <SelectItem key={r.id} value={r.id}>{r.roomName}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            {roomId && (
              <Button asChild size="sm">
                <Link href={bookHref(roomId, bookDay)}>
                  <CalendarPlus className="mr-2 h-4 w-4" /> Book this room
                </Link>
              </Button>
            )}
          </div>
          <div className="flex w-full flex-wrap gap-2 pt-2">
            {KINDS.map((k) => (
              <button
                key={k}
                type="button"
                onClick={() => toggle(k)}
                className={`flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs ${hidden.has(k) ? 'opacity-40' : ''}`}
                aria-pressed={!hidden.has(k)}
              >
                <span className={`h-2.5 w-2.5 rounded-full ${KIND_SWATCH[k]}`} />
                {KIND_LABEL[k]}
              </button>
            ))}
            <span className="flex items-center gap-1.5 px-1 text-xs text-muted-foreground">
              <span className="h-2.5 w-4 rounded border border-dashed border-blue-600" /> waiting for your answer
            </span>
          </div>
        </CardContent>
      </Card>

      {calendar.data?.roomName && (
        <p className="text-sm text-muted-foreground">
          Showing when <strong>{calendar.data.roomName}</strong> is booked
          {!calendar.data.hrDesk && ' — other people\'s bookings show only as "booked"'}.
        </p>
      )}
      <IncompleteDiaryBanner sources={calendar.data?.incompleteSources} />

      {calendar.isLoading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : calendar.isError ? (
        <Card>
          <CardContent className="py-8 text-center text-sm text-destructive">
            {(calendar.error as any)?.message ?? 'The calendar could not be read.'}
          </CardContent>
        </Card>
      ) : view === 'month' ? (
        <Card>
          <CardContent className="p-2">
            <div className="grid grid-cols-7 border-b pb-1 text-center text-xs font-medium text-muted-foreground">
              {WEEKDAYS.map((w) => <div key={w}>{w}</div>)}
            </div>
            {weeks.map((days) => {
              const placed = layoutWeek(shown, days);
              const visible = placed.filter((p) => p.lane < MAX_LANES);
              const more = days.map((d, i) => placed.filter((p) => p.lane >= MAX_LANES && p.col <= i && p.col + p.width > i).length);
              return (
                <div key={days[0]} className="relative border-b">
                  <div className="absolute inset-0 grid grid-cols-7">
                    {days.map((d) => (
                      <div
                        key={d}
                        className={`border-r last:border-r-0 ${d.slice(0, 7) !== monthStart(cursor).slice(0, 7) ? 'bg-muted/40' : ''} ${d === today() ? 'bg-primary/5' : ''}`}
                      />
                    ))}
                  </div>
                  <div className="relative grid min-h-28 grid-cols-7 content-start gap-y-0.5 pb-1">
                    {days.map((d, i) => (
                      <button
                        key={d}
                        type="button"
                        onClick={() => { setCursor(d); setView('week'); }}
                        className={`px-1.5 pt-1 text-left text-xs ${d === today() ? 'font-bold text-primary' : 'text-muted-foreground'}`}
                        style={{ gridColumn: i + 1, gridRow: 1 }}
                        title="Open this week"
                      >
                        {Number(d.slice(8, 10))}
                      </button>
                    ))}
                    {visible.map((p) => (
                      <Band key={`${p.entry.kind}-${p.entry.eventId ?? p.entry.bookingId ?? p.entry.label}-${p.entry.start}-${days[0]}`} placed={p} onOpen={setOpen} />
                    ))}
                    {more.map((n, i) =>
                      n > 0 ? (
                        <button
                          key={`more-${days[i]}`}
                          type="button"
                          className="px-1.5 text-left text-[11px] text-muted-foreground hover:underline"
                          style={{ gridColumn: i + 1, gridRow: MAX_LANES + 2 }}
                          onClick={() => { setCursor(days[i]); setView('week'); }}
                        >
                          +{n} more
                        </button>
                      ) : null,
                    )}
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>
      ) : (
        <WeekView days={weeks[0] ?? []} entries={shown} onOpen={setOpen} />
      )}

      <EntryDialog entry={open} onClose={() => setOpen(null)} />
    </div>
  );
}

/**
 * The week: what lasts all day or over several days as bands across the top; what has hours listed under its day, in
 * time order.
 */
function WeekView({ days, entries, onOpen }: { days: string[]; entries: CalendarEntry[]; onOpen: (e: CalendarEntry) => void }) {
  const spanning = entries.filter((e) => e.isAllDay || span(e).first !== span(e).last);
  const timed = entries.filter((e) => !e.isAllDay && span(e).first === span(e).last);
  const placed = layoutWeek(spanning, days);
  return (
    <Card>
      <CardContent className="space-y-2 p-2">
        <div className="relative grid grid-cols-7 content-start gap-y-0.5 border-b pb-1">
          {days.map((d, i) => (
            <div
              key={d}
              className={`px-1.5 pt-1 text-xs font-medium ${d === today() ? 'text-primary' : 'text-muted-foreground'}`}
              style={{ gridColumn: i + 1, gridRow: 1 }}
            >
              {label(d, { weekday: 'short', day: 'numeric', month: 'short' })}
            </div>
          ))}
          {placed.map((p) => (
            <Band key={`${p.entry.kind}-${p.entry.eventId ?? p.entry.bookingId ?? p.entry.label}-${p.entry.start}`} placed={p} onOpen={onOpen} />
          ))}
        </div>
        <div className="grid min-h-64 grid-cols-7 gap-1">
          {days.map((d) => (
            <div key={d} className={`space-y-1 rounded border p-1 ${d === today() ? 'border-primary/50' : ''}`}>
              {timed
                .filter((e) => span(e).first === d)
                .sort((a, b) => a.start.localeCompare(b.start))
                .map((e, i) => (
                  <button
                    key={`${e.kind}-${e.eventId ?? e.bookingId ?? i}-${e.start}`}
                    type="button"
                    onClick={() => onOpen(e)}
                    className={`block w-full rounded px-1.5 py-1 text-left text-[11px] leading-tight hover:opacity-90 ${bandClass(e)}`}
                  >
                    <span className="block font-medium">{hhmm(e.start)}–{hhmm(e.end)}</span>
                    <span className="block truncate">{e.label}</span>
                  </button>
                ))}
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

/** An entry's card: what and when, its page, and — for an invitation — the caller's own answer. */
function EntryDialog({ entry, onClose }: { entry: CalendarEntry | null; onClose: () => void }) {
  if (!entry) return null;
  const { first, last } = span(entry);
  const when = entry.isAllDay
    ? first === last
      ? label(first, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
      : `${label(first, { day: 'numeric', month: 'short' })} – ${label(last, { day: 'numeric', month: 'short', year: 'numeric' })}`
    : `${label(first, { weekday: 'long', day: 'numeric', month: 'long' })}, ${hhmm(entry.start)}–${hhmm(entry.end)}${first !== last ? ` (to ${label(last, { day: 'numeric', month: 'short' })})` : ''}`;
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{entry.label}</DialogTitle>
          <DialogDescription>{when}</DialogDescription>
        </DialogHeader>
        <div className="flex flex-wrap gap-2">
          <Badge variant="secondary">{entry.kind === 'Mine' ? entry.subKind : KIND_LABEL[entry.kind].replace(/s$/, '')}</Badge>
          {entry.reference && <Badge variant="outline">{entry.reference}</Badge>}
          {entry.awaitingApproval && <Badge variant="outline">Awaiting approval</Badge>}
          {entry.isOrganiser && <Badge variant="outline">You organise it</Badge>}
        </div>
        {entry.kind === 'Event' && entry.myParticipantId && entry.eventId ? (
          <InvitationAnswer
            eventId={entry.eventId}
            participantId={entry.myParticipantId}
            status={entry.myAnswer}
            canAnswer={entry.canAnswer}
            whyNot={entry.whyNotAnswer}
            inSeries={!!entry.seriesId}
            onAnswered={onClose}
          />
        ) : entry.kind === 'Event' && !entry.isOrganiser ? (
          <p className="text-sm text-muted-foreground">You are not on its guest list; it is on your calendar because it is for you.</p>
        ) : null}
        {entry.link && (
          <Button asChild variant="outline" size="sm" className="w-fit">
            <Link href={entry.link}>
              <ExternalLink className="mr-2 h-4 w-4" /> Open
            </Link>
          </Button>
        )}
      </DialogContent>
    </Dialog>
  );
}
