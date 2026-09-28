'use client';

/**
 * Leave drawn as time rather than rows.
 *
 * <p>Closure plan slice E1 (decision D-9): ONE component, three entry points — `/me/leave/calendar`
 * (mine), a team view on the manager's side, and `/hr/leave/calendar` (the organisation, filterable).
 * The `scope` prop is the only difference, and the server authorizes each one differently.</p>
 *
 * <p>It is also what finally makes `LeaveType.CalendarColor` real. That field's own form said "used
 * on leave calendars" since the port and no calendar existed, so the setting was decorative
 * (closure plan L-36).</p>
 *
 * <p><b>Why a hand-built grid.</b> `react-day-picker` is a date PICKER: it renders single days and
 * owns selection. What a leave calendar needs is spans — one band per request, stretched across the
 * days it covers and broken at week boundaries — which is a layout problem rather than a picking
 * one. The month grid below is about eighty lines and does exactly that; bending a picker into it
 * would be more code and less control.</p>
 */

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, CalendarDays, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { leaveService } from '@/services/hr/leave.service';
import type {
  LeaveCalendarEntry,
  LeaveCalendarScope,
} from '@/types/hr/leave-request';

/**
 * Fallback band colours, used only for leave types the tenant never coloured.
 *
 * ⚠ Plain hex on purpose. `hsl(var(--chart-N))` is not valid in this codebase, and the `--chart-*`
 * tokens swap hue between light and dark — which would make the same leave type change colour when
 * somebody flips the theme, defeating the point of colouring by type at all. These six are legible
 * against both surfaces.
 */
const FALLBACK_COLORS = [
  '#2563eb', // blue
  '#059669', // green
  '#d97706', // amber
  '#7c3aed', // violet
  '#db2777', // pink
  '#0891b2', // cyan
];

const colorFor = (entry: LeaveCalendarEntry, index: number) =>
  entry.calendarColor?.trim() || FALLBACK_COLORS[index % FALLBACK_COLORS.length];

const iso = (d: Date) => {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
};

/** Every day drawn in a month view, including the leading/trailing days that pad the weeks. */
function gridDays(month: Date): Date[] {
  const first = new Date(month.getFullYear(), month.getMonth(), 1);
  const start = new Date(first);
  // Monday-first, which is how the working week reads here.
  start.setDate(first.getDate() - ((first.getDay() + 6) % 7));

  const days: Date[] = [];
  for (let i = 0; i < 42; i++) {
    const d = new Date(start);
    d.setDate(start.getDate() + i);
    days.push(d);
  }
  return days;
}

const WEEKDAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

interface LeaveCalendarProps {
  scope: LeaveCalendarScope;
  /** Shown above the grid; the three entry points each say who they are about. */
  title: string;
  description?: string;
  /** Organisation scope only — narrows to one unit and every unit beneath it. */
  organizationUnitId?: string;
  leaveTypeId?: string;
  /** One person's leave (round 5 lane F). The server applies it after the scope, so it can only narrow. */
  employeeId?: string;
  /** Where a band links to. The portal and the desk have different detail routes. */
  hrefFor?: (entry: LeaveCalendarEntry) => string;
}

export function LeaveCalendar({
  scope,
  title,
  description,
  organizationUnitId,
  leaveTypeId,
  employeeId,
  hrefFor,
}: LeaveCalendarProps) {
  const [month, setMonth] = useState(() => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
  });
  const [view, setView] = useState<'month' | 'list'>('month');

  const days = useMemo(() => gridDays(month), [month]);
  const from = iso(days[0]);
  const to = iso(days[days.length - 1]);

  const { data, isLoading, isError } = useQuery({
    queryKey: [
      'hr', 'leave-calendar', scope, from, to,
      organizationUnitId ?? '', leaveTypeId ?? '', employeeId ?? '',
    ],
    queryFn: () =>
      leaveService.getCalendar({ from, to, scope, organizationUnitId, leaveTypeId, employeeId }),
  });

  const entries = data?.entries ?? [];

  // Colour is assigned per leave TYPE, not per row, so the same type is the same colour everywhere
  // on the grid — which is the only way a legend means anything.
  const colorByType = useMemo(() => {
    const map = new Map<string, string>();
    let i = 0;
    for (const e of entries) {
      if (map.has(e.leaveTypeId)) continue;
      map.set(e.leaveTypeId, colorFor(e, i));
      i++;
    }
    return map;
  }, [entries]);

  const holidayByDate = useMemo(() => {
    const map = new Map<string, string>();
    for (const h of data?.holidays ?? []) map.set(h.date.slice(0, 10), h.name);
    return map;
  }, [data]);

  const entriesOn = (day: Date) => {
    const key = iso(day);
    return entries.filter((e) => e.startDate.slice(0, 10) <= key && e.endDate.slice(0, 10) >= key);
  };

  const legend = useMemo(() => {
    const seen = new Map<string, string>();
    for (const e of entries) if (!seen.has(e.leaveTypeId)) seen.set(e.leaveTypeId, e.leaveTypeName);
    return [...seen.entries()];
  }, [entries]);

  const monthLabel = month.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
  const shift = (by: number) => setMonth(new Date(month.getFullYear(), month.getMonth() + by, 1));
  const today = iso(new Date());

  return (
    <Card>
      <CardHeader className="gap-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <CardTitle className="text-base">{title}</CardTitle>
            {description && <p className="text-sm text-muted-foreground">{description}</p>}
          </div>
          <div className="flex items-center gap-1">
            <Button variant="outline" size="sm" onClick={() => shift(-1)} aria-label="Previous month">
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <span className="min-w-[10rem] text-center text-sm font-medium">{monthLabel}</span>
            <Button variant="outline" size="sm" onClick={() => shift(1)} aria-label="Next month">
              <ChevronRight className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="ml-1"
              onClick={() => {
                const now = new Date();
                setMonth(new Date(now.getFullYear(), now.getMonth(), 1));
              }}
            >
              Today
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="ml-2"
              onClick={() => setView(view === 'month' ? 'list' : 'month')}
            >
              {view === 'month' ? 'List' : 'Month'}
            </Button>
          </div>
        </div>

        {legend.length > 0 && (
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs">
            {legend.map(([typeId, name]) => (
              <span key={typeId} className="flex items-center gap-1.5">
                <span
                  className="inline-block h-2.5 w-2.5 rounded-sm"
                  style={{ backgroundColor: colorByType.get(typeId) }}
                />
                {name}
              </span>
            ))}
            <span className="flex items-center gap-1.5 text-muted-foreground">
              <span className="inline-block h-2.5 w-2.5 rounded-sm border border-dashed border-current" />
              Not yet approved
            </span>
          </div>
        )}
      </CardHeader>

      <CardContent>
        {isLoading ? (
          <div className="grid grid-cols-7 gap-1">
            {[...Array(42)].map((_, i) => (
              <Skeleton key={i} className="h-24" />
            ))}
          </div>
        ) : isError ? (
          <EmptyState
            icon={CalendarDays}
            title="The calendar could not be loaded"
            description="You may not have access to this view."
          />
        ) : view === 'list' ? (
          <div className="divide-y rounded-md border">
            {entries.length === 0 ? (
              <div className="p-6">
                <EmptyState
                  icon={CalendarDays}
                  title={employeeId ? 'No leave this month' : 'Nobody is away this month'}
                  description="Leave appears here once it is requested."
                />
              </div>
            ) : (
              entries.map((e) => (
                <div key={e.id} className="flex flex-wrap items-center gap-3 p-3 text-sm">
                  <span
                    className="inline-block h-3 w-3 shrink-0 rounded-sm"
                    style={{
                      backgroundColor: e.isConfirmed ? colorByType.get(e.leaveTypeId) : 'transparent',
                      border: e.isConfirmed ? undefined : `1px dashed ${colorByType.get(e.leaveTypeId)}`,
                    }}
                  />
                  <span className="min-w-[12rem] font-medium">{e.employeeName}</span>
                  <span className="text-muted-foreground">{e.leaveTypeName}</span>
                  <span>
                    {e.startDate.slice(0, 10)} – {e.endDate.slice(0, 10)}
                  </span>
                  <span className="text-muted-foreground">
                    {e.totalDays} day{e.totalDays === 1 ? '' : 's'}
                  </span>
                  {!e.isConfirmed && <Badge variant="outline">{e.status}</Badge>}
                  {hrefFor && (
                    <Link
                      href={hrefFor(e)}
                      className="ml-auto text-primary underline-offset-2 hover:underline"
                    >
                      {e.requestNumber}
                    </Link>
                  )}
                </div>
              ))
            )}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <div className="min-w-[44rem]">
              <div className="grid grid-cols-7 gap-1 pb-1">
                {WEEKDAYS.map((d) => (
                  <div key={d} className="px-1 text-xs font-medium text-muted-foreground">
                    {d}
                  </div>
                ))}
              </div>

              <div className="grid grid-cols-7 gap-1">
                {days.map((day) => {
                  const key = iso(day);
                  const inMonth = day.getMonth() === month.getMonth();
                  const holiday = holidayByDate.get(key);
                  const onLeave = entriesOn(day);

                  return (
                    <div
                      key={key}
                      className={[
                        'min-h-24 rounded-md border p-1',
                        inMonth ? '' : 'opacity-40',
                        // The holiday sits UNDERNEATH the bands, which is why it is a background
                        // rather than a badge: it explains why a five-day leave charges four.
                        holiday ? 'bg-muted/60' : 'bg-background',
                        key === today ? 'ring-2 ring-primary' : '',
                      ].join(' ')}
                    >
                      <div className="flex items-baseline justify-between">
                        <span className="text-xs font-medium">{day.getDate()}</span>
                        {holiday && (
                          <span className="truncate pl-1 text-[10px] text-muted-foreground" title={holiday}>
                            {holiday}
                          </span>
                        )}
                      </div>

                      <div className="mt-1 space-y-0.5">
                        {onLeave.slice(0, 3).map((e) => {
                          const color = colorByType.get(e.leaveTypeId);
                          const band = (
                            <div
                              className="truncate rounded px-1 py-0.5 text-[10px] leading-tight"
                              style={
                                e.isConfirmed
                                  ? { backgroundColor: color, color: '#fff' }
                                  : { border: `1px dashed ${color}`, color: 'inherit' }
                              }
                              title={`${e.employeeName} · ${e.leaveTypeName} · ${e.startDate.slice(0, 10)} to ${e.endDate.slice(0, 10)}${e.isConfirmed ? '' : ' (not yet approved)'}`}
                            >
                              {e.employeeName}
                            </div>
                          );
                          return hrefFor ? (
                            <Link key={e.id} href={hrefFor(e)}>
                              {band}
                            </Link>
                          ) : (
                            <div key={e.id}>{band}</div>
                          );
                        })}
                        {onLeave.length > 3 && (
                          <div className="px-1 text-[10px] text-muted-foreground">
                            +{onLeave.length - 3} more
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
