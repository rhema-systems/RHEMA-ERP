'use client';

/**
 * A unit's diary, side by side — what a head looks at before scheduling anything for their team.
 *
 * Round 4, D5. The clash information is shown **at the point of choosing a time**, which is the only
 * moment it changes a decision: finding out afterwards that four of your six people are on a course
 * that morning is finding out too late.
 *
 * ⚠ This exposes other people's leave and travel. The HR desk (`HR.Company.Write`) reads any unit; since company-schedule
 * lane 5b (R4-10B.3, the user's ruling) a unit's head — or the head of a unit above it — reads theirs too, without that
 * permission. The unit list is the server's answer to "which may I read" (`team-schedule/units`), so nobody is offered a
 * unit they would be refused; somebody who heads nothing is told who the page is for. Your own diary is
 * `/hr/company-schedule/my-schedule`, which takes the employee from the token and needs no permission at all.
 *
 * ⚠ The SUBTREE, not the unit: a head scheduling for their directorate means everybody under them. Lane 5b (R4-10B.4):
 * narrow it to one sub-unit, or to the unit's direct members, with the count shown; (R4-10B.1) "Schedule for this unit"
 * opens the event form for the unit and the day, for whoever may schedule events.
 */

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { CalendarClock, CalendarPlus, CalendarRange, Loader2, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { IncompleteDiaryBanner } from '@/components/hr/company-schedule/IncompleteDiaryBanner';
import { addDay, byDay } from '@/components/hr/company-schedule/diaryDays';
import { useAuth } from '@/hooks/use-auth';
import { formatDate, formatTime } from '@/lib/hr/attendance-format';
import { personalScheduleService } from '@/services/hr/company-schedule.service';
import type { PersonalSchedule, PersonalScheduleEntry } from '@/types/hr/company-schedule';

const today = () => new Date().toISOString().slice(0, 10);
const ALL = '__all__';

/** The days in the range, so every member's row spans the same columns. */
function daysBetween(from: string, to: string): string[] {
  const out: string[] = [];
  if (!from || !to) return out;
  for (let d = from; d <= to && out.length < 62; d = addDay(d)) out.push(d);
  return out;
}

/** The event form, for a unit and a day (R4-10B.1). */
const scheduleHref = (unitId: string, day: string) =>
  `/hr/company-schedule/events/new?scope=Department&unit=${encodeURIComponent(unitId)}&date=${day}`;

function DayCell({ entries }: { entries: PersonalScheduleEntry[] }) {
  if (entries.length === 0) {
    return <div className="min-h-[2.5rem] rounded border border-dashed border-muted p-1" />;
  }

  // ⚠ Hard and soft are shown differently. In a diary neither refuses anything — this only says how
  // movable the commitment is, which is what a head weighing a time actually needs.
  const hard = entries.some((e) => e.hardness === 'Hard');
  return (
    <div
      className={`min-h-[2.5rem] space-y-0.5 rounded border p-1 ${
        hard ? 'border-destructive/40 bg-destructive/5' : 'border-amber-500/40 bg-amber-500/5'
      }`}
      title={entries
        .map((e) =>
          e.isDayGranular
            ? `${e.label} (all day)`
            : `${e.label} ${formatTime(e.start.slice(11, 19))}–${formatTime(e.end.slice(11, 19))}`,
        )
        .join('\n')}
    >
      {entries.slice(0, 2).map((e, i) => (
        <div key={`${e.kind}-${i}`} className="truncate text-[10px] leading-tight">
          {e.isDayGranular ? '' : `${formatTime(e.start.slice(11, 19))} `}
          {e.label}
        </div>
      ))}
      {entries.length > 2 && (
        <div className="text-[10px] text-muted-foreground">+{entries.length - 2} more</div>
      )}
    </div>
  );
}

export default function TeamSchedulePage() {
  const { hasPermission } = useAuth();
  // Scheduling an event is the HR desk's (POST events is Write); a head who is not on the desk reads, and does not schedule.
  const canSchedule = hasPermission('HR.Company.Write');
  const [unitId, setUnitId] = useState('');
  const [subUnit, setSubUnit] = useState(ALL);
  const [directOnly, setDirectOnly] = useState(false);
  const [from, setFrom] = useState(today());
  const [to, setTo] = useState(addDay(today(), 6));

  const units = useQuery({
    queryKey: ['hr', 'team-schedule', 'units'],
    queryFn: () => personalScheduleService.getTeamScheduleUnits(),
    staleTime: 5 * 60 * 1000,
  });
  const readable = useMemo(() => units.data?.units ?? [], [units.data]);

  // A head opens on the unit they head (the top-most, by path); the desk chooses.
  useEffect(() => {
    if (unitId || !units.data || units.data.canReadEveryUnit) return;
    const own = readable.find((u) => u.headedByCaller) ?? readable[0];
    if (own) setUnitId(own.id);
  }, [units.data, readable, unitId]);

  const team = useQuery({
    queryKey: ['hr', 'team-schedule', unitId, from, to],
    queryFn: () => personalScheduleService.getTeamSchedule(unitId, from, to),
    enabled: !!unitId && !!from && !!to,
    retry: false,
  });

  const days = useMemo(() => daysBetween(from, to), [from, to]);
  const members = useMemo(() => team.data?.members ?? [], [team.data]);

  /** The sub-units the result holds, for the filter — the units its members actually sit in. */
  const subUnits = useMemo(() => {
    const seen = new Map<string, string>();
    for (const m of members) {
      if (m.organizationUnitId && !seen.has(m.organizationUnitId)) seen.set(m.organizationUnitId, m.organizationUnitName ?? 'Unit');
    }
    return [...seen.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  }, [members]);

  const shown = useMemo(
    () =>
      members.filter((m) =>
        directOnly ? m.organizationUnitId === unitId : subUnit === ALL || m.organizationUnitId === subUnit,
      ),
    [members, directOnly, subUnit, unitId],
  );

  /** member id → day → entries — each entry under every day of the range it covers (lane 5b, F-23). */
  const grid = useMemo(() => {
    const map = new Map<string, Map<string, PersonalScheduleEntry[]>>();
    const range = { from: (team.data?.from ?? from).slice(0, 10), to: (team.data?.to ?? to).slice(0, 10) };
    for (const m of members) map.set(m.employeeId, byDay(m.entries, range.from, range.to));
    return map;
  }, [members, team.data, from, to]);

  const chooseUnit = (id: string) => {
    setUnitId(id);
    setSubUnit(ALL);
    setDirectOnly(false);
  };
  const scheduleFor = directOnly || subUnit === ALL ? unitId : subUnit;

  if (units.isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!units.isError && readable.length === 0) {
    return (
      <div className="space-y-6">
        <PageHeader title="Team schedule" description="What a unit and everyone under it are already committed to." backHref="/hr/company-schedule" />
        <Card>
          <CardContent className="space-y-3 p-6 text-sm">
            <p>
              A team schedule shows other people&apos;s leave and travel, so it is for the HR desk and for the head of a
              unit (who sees their unit and every unit beneath it). You are not recorded as heading a unit.
            </p>
            <Link
              href="/hr/company-schedule/my-schedule"
              className="inline-flex items-center gap-2 font-medium text-primary underline-offset-4 hover:underline"
            >
              <CalendarRange className="h-4 w-4" /> My Schedule — your own diary
            </Link>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Team schedule"
        description="What a unit and everyone under it are already committed to — before you pick a time."
        backHref="/hr/company-schedule"
        actions={
          canSchedule && unitId ? (
            <Button asChild>
              <Link href={scheduleHref(scheduleFor, from)}>
                <CalendarPlus className="mr-2 h-4 w-4" /> Schedule for this unit
              </Link>
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-3 pt-6">
          <div className="min-w-64 flex-1 space-y-1.5">
            <Label>Organisation unit</Label>
            <Select value={unitId} onValueChange={(v) => v && chooseUnit(v)}>
              <SelectTrigger>
                <SelectValue placeholder="Choose a unit" />
              </SelectTrigger>
              <SelectContent>
                {readable.map((u) => (
                  <SelectItem key={u.id} value={u.id}>
                    {u.path || u.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              {units.data?.canReadEveryUnit
                ? 'Every unit — you are on the HR desk.'
                : 'The units you head, and every unit beneath them.'}
            </p>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="from">From</Label>
            <Input id="from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-44" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="to">To</Label>
            <Input id="to" type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-44" />
          </div>
        </CardContent>
      </Card>

      <IncompleteDiaryBanner sources={team.data?.incompleteSources} />

      {!unitId ? (
        <Card>
          <CardContent className="py-12">
            <EmptyState
              icon={Users}
              title="Choose a unit"
              description="Pick an organisation unit to see what it and everything under it are committed to."
            />
          </CardContent>
        </Card>
      ) : team.isLoading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : team.isError ? (
        <Card>
          <CardContent className="py-8 text-center text-sm text-destructive">
            {(team.error as any)?.message ?? 'The team schedule could not be read.'}
          </CardContent>
        </Card>
      ) : members.length === 0 ? (
        <Card>
          <CardContent className="py-12">
            <EmptyState
              icon={CalendarClock}
              title="Nobody in this unit"
              description="The unit and its subtree have no active employees."
            />
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader className="space-y-3 pb-2">
            <div className="flex flex-wrap items-end gap-4">
              {subUnits.length > 1 && (
                <div className="min-w-56 space-y-1.5">
                  <Label>Sub-unit</Label>
                  <Select value={subUnit} onValueChange={(v) => v && setSubUnit(v)} disabled={directOnly}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={ALL}>All of {team.data?.organizationUnitName ?? 'the unit'} and beneath</SelectItem>
                      {subUnits.map((s) => (
                        <SelectItem key={s.id} value={s.id}>
                          {s.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              <div className="flex items-center gap-2 pb-2">
                <Switch id="direct" checked={directOnly} onCheckedChange={(c) => setDirectOnly(c === true)} />
                <Label htmlFor="direct">Direct members only</Label>
              </div>
            </div>
            <CardTitle className="text-sm">
              {shown.length === members.length
                ? `${members.length} ${members.length === 1 ? 'person' : 'people'}`
                : `${shown.length} of ${members.length} people`}
              <Badge variant="outline" className="ml-2 text-[10px]">
                red = confirmed · amber = worth knowing
              </Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="overflow-x-auto">
            <table className="w-full border-separate border-spacing-1 text-xs">
              <thead>
                <tr>
                  <th className="w-44 text-left font-medium">Person</th>
                  {days.map((d) => (
                    <th key={d} className="min-w-28 text-left font-medium">
                      {canSchedule ? (
                        <Link
                          href={scheduleHref(scheduleFor, d)}
                          className="hover:underline"
                          title="Schedule something for this unit on this day"
                        >
                          {formatDate(d)}
                        </Link>
                      ) : (
                        formatDate(d)
                      )}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {shown.map((m: PersonalSchedule) => (
                  <tr key={m.employeeId}>
                    <td className="align-top text-sm font-medium">
                      {m.employeeName}
                      {m.organizationUnitName && m.organizationUnitId !== unitId && (
                        <span className="block text-[10px] font-normal text-muted-foreground">{m.organizationUnitName}</span>
                      )}
                    </td>
                    {days.map((d) => (
                      <td key={d} className="align-top">
                        <DayCell entries={grid.get(m.employeeId)?.get(d) ?? []} />
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
