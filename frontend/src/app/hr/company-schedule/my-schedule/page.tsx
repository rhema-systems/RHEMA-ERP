'use client';

/**
 * One person's diary — everything the organisation has them down for, in one place.
 *
 * Round 4, D5. Before this an employee visited five screens to find out what their week held: their
 * events, their room bookings, the interviews they sit on, their training, their leave and travel.
 * Each module knew its own part and none of them assembled it.
 *
 * ⚠ The server takes the employee from the TOKEN. There is no id in the URL or the query, and there
 * must not be: this shows leave and travel, which is why reading somebody else's diary goes through
 * the team view and its own permission.
 */

import { useMemo, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import {
  Building2,
  CalendarCheck,
  CalendarClock,
  DoorClosed,
  GraduationCap,
  Loader2,
  Plane,
  Users,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { formatDate, formatTime } from '@/lib/hr/attendance-format';
import { personalScheduleService } from '@/services/hr/company-schedule.service';
import type { PersonalScheduleEntry, ScheduleEntryKind } from '@/types/hr/company-schedule';

const KIND_ICON: Record<ScheduleEntryKind, typeof CalendarCheck> = {
  Interview: CalendarCheck,
  Leave: CalendarClock,
  Travel: Plane,
  Event: Users,
  RoomBooking: DoorClosed,
  Training: GraduationCap,
  Closure: Building2,
  Holiday: Building2,
};

const dayKey = (iso: string) => iso.slice(0, 10);

const addDays = (days: number, from?: string) => {
  const d = from ? new Date(`${from}T00:00:00Z`) : new Date();
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

const isDay = (s: string | null): s is string => !!s && /^\d{4}-\d{2}-\d{2}$/.test(s) && !Number.isNaN(Date.parse(s));

/**
 * ⚠ A day-granular entry is never printed with times. Leave, travel, closures and all-day events
 * are recorded by the DAY, so the start and end the server sends are the day's bounds — rendering
 * "00:00–23:59" would invent a precision the record does not have.
 */
function EntryRow({ entry }: { entry: PersonalScheduleEntry }) {
  const Icon = KIND_ICON[entry.kind] ?? CalendarCheck;
  return (
    <div className="flex items-start gap-3 border-b py-2 last:border-b-0">
      <Icon className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
      <div className="min-w-0 flex-1">
        <div className="text-sm font-medium">{entry.label}</div>
        <div className="text-xs text-muted-foreground">
          {entry.isDayGranular ? (
            <span>All day</span>
          ) : (
            <span>
              {formatTime(entry.start.slice(11, 19))} – {formatTime(entry.end.slice(11, 19))}
            </span>
          )}
          {entry.reference && <span className="ml-2">· {entry.reference}</span>}
        </div>
      </div>
      <Badge variant="secondary" className="shrink-0 text-[10px]">
        {entry.kindName}
      </Badge>
    </div>
  );
}

export default function MySchedulePage() {
  // Lane 2e-1: an event's notice links here at the event's first day (`?from=`), so a notice about an event
  // months away opens on it rather than on this fortnight.
  const params = useSearchParams();
  const linkedFrom = params.get('from');
  const [from, setFrom] = useState(isDay(linkedFrom) ? linkedFrom : addDays(0));
  const [to, setTo] = useState(isDay(linkedFrom) ? addDays(13, linkedFrom) : addDays(13));

  const schedule = useQuery({
    queryKey: ['hr', 'my-schedule', from, to],
    queryFn: () => personalScheduleService.getMySchedule(from, to),
    enabled: !!from && !!to,
    // ⚠ The server refuses a range over sixty days or one that runs backwards. Surfacing its own
    // sentence beats guessing the rule here and getting it subtly different.
    retry: false,
  });

  /** Grouped by day so a fortnight reads as a diary rather than a list. */
  const byDay = useMemo(() => {
    const groups = new Map<string, PersonalScheduleEntry[]>();
    for (const e of schedule.data?.entries ?? []) {
      const key = dayKey(e.start);
      groups.set(key, [...(groups.get(key) ?? []), e]);
    }
    return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [schedule.data]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="My schedule"
        description="Everything you are down for — meetings, rooms you have booked, interview panels you sit on, training, leave and travel."
        backHref="/hr/company-schedule"
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-3 pt-6">
          <div className="space-y-1.5">
            <Label htmlFor="from">From</Label>
            <Input id="from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-44" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="to">To</Label>
            <Input id="to" type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-44" />
          </div>
          <Button variant="outline" onClick={() => { setFrom(addDays(0)); setTo(addDays(13)); }}>
            Next fortnight
          </Button>
          <Button variant="outline" onClick={() => { setFrom(addDays(0)); setTo(addDays(6)); }}>
            This week
          </Button>
        </CardContent>
      </Card>

      {schedule.isLoading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : schedule.isError ? (
        <Card>
          <CardContent className="py-8 text-center text-sm text-destructive">
            {(schedule.error as any)?.message ?? 'The schedule could not be read.'}
          </CardContent>
        </Card>
      ) : byDay.length === 0 ? (
        <Card>
          <CardContent className="py-12">
            <EmptyState
              icon={CalendarClock}
              title="Nothing in this range"
              description="No meetings, bookings, panels, training, leave or travel between those dates."
            />
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-4">
          {byDay.map(([day, entries]) => (
            <Card key={day}>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm">{formatDate(day)}</CardTitle>
              </CardHeader>
              <CardContent className="pt-0">
                {entries
                  .slice()
                  .sort((a, b) => a.start.localeCompare(b.start))
                  .map((e, i) => (
                    <EntryRow key={`${e.kind}-${e.reference ?? i}`} entry={e} />
                  ))}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
