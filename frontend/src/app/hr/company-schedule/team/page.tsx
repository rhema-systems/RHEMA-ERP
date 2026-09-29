'use client';

/**
 * A unit's diary, side by side — what a head looks at before scheduling anything for their team.
 *
 * Round 4, D5. The clash information is shown **at the point of choosing a time**, which is the only
 * moment it changes a decision: finding out afterwards that four of your six people are on a course
 * that morning is finding out too late.
 *
 * ⚠ This exposes other people's leave and travel, so it is gated on the company-schedule WRITE
 * permission rather than Read. Your own diary is `/hr/company-schedule/my-schedule`, which takes the
 * employee from the token and needs no permission at all.
 *
 * ⚠ The SUBTREE, not the unit: a head scheduling for their directorate means everybody under them.
 */

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { CalendarClock, Loader2, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { formatDate, formatTime } from '@/lib/hr/attendance-format';
import { personalScheduleService } from '@/services/hr/company-schedule.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import type { PersonalSchedule, PersonalScheduleEntry } from '@/types/hr/company-schedule';

const addDays = (days: number) => {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
};

/** The days in the range, so every member's row spans the same columns. */
function daysBetween(from: string, to: string): string[] {
  const out: string[] = [];
  const start = new Date(`${from}T00:00:00`);
  const end = new Date(`${to}T00:00:00`);
  for (let d = start; d <= end; d.setDate(d.getDate() + 1)) out.push(d.toISOString().slice(0, 10));
  return out;
}

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
  const [unitId, setUnitId] = useState('');
  const [from, setFrom] = useState(addDays(0));
  const [to, setTo] = useState(addDays(6));

  const units = useQuery({
    queryKey: ['hr', 'organization-units', 'active'],
    queryFn: () => organizationUnitService.getAll(),
  });

  const team = useQuery({
    queryKey: ['hr', 'team-schedule', unitId, from, to],
    queryFn: () => personalScheduleService.getTeamSchedule(unitId, from, to),
    enabled: !!unitId && !!from && !!to,
    retry: false,
  });

  const days = useMemo(() => daysBetween(from, to), [from, to]);

  /** member id → day → entries, so the grid is a lookup rather than a scan per cell. */
  const grid = useMemo(() => {
    const map = new Map<string, Map<string, PersonalScheduleEntry[]>>();
    for (const m of team.data?.members ?? []) {
      const byDay = new Map<string, PersonalScheduleEntry[]>();
      for (const e of m.entries) {
        const key = e.start.slice(0, 10);
        byDay.set(key, [...(byDay.get(key) ?? []), e]);
      }
      map.set(m.employeeId, byDay);
    }
    return map;
  }, [team.data]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Team schedule"
        description="What a unit and everyone under it are already committed to — before you pick a time."
        backHref="/hr/company-schedule"
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-3 pt-6">
          <div className="min-w-64 flex-1 space-y-1.5">
            <Label>Organisation unit</Label>
            <Select value={unitId} onValueChange={setUnitId}>
              <SelectTrigger>
                <SelectValue placeholder="Choose a unit" />
              </SelectTrigger>
              <SelectContent>
                {(units.data ?? []).map((u: any) => (
                  <SelectItem key={u.id} value={u.id}>
                    {u.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
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
      ) : (team.data?.members ?? []).length === 0 ? (
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
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">
              {(team.data?.members ?? []).length} people
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
                      {formatDate(d)}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {(team.data?.members ?? []).map((m: PersonalSchedule) => (
                  <tr key={m.employeeId}>
                    <td className="align-top text-sm font-medium">{m.employeeName}</td>
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
