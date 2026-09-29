'use client';

/**
 * The desk's leave calendar — closure plan slice E1, decision D-9.
 *
 * Two of the three entry points live here: the whole organisation, and the caller's own team.
 * Both render the same component; only the scope differs, and the server authorizes each one
 * differently (organisation needs the leave read tier, team resolves from the token).
 *
 * Round 5 lane F — "search one employee in the HR calendar". The organisation view gains ONE
 * employee and ONE unit (with every unit beneath it). Both are offered for the organisation only
 * and are not sent for the other two scopes: the server would narrow by them there too, and a
 * filter nobody can see must not quietly empty the calendar.
 */

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { LeaveCalendar } from '@/components/hr/leave/LeaveCalendar';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import type { LeaveCalendarScope } from '@/types/hr/leave-request';

const ALL = '__all__';

export default function LeaveCalendarPage() {
  const [scope, setScope] = useState<LeaveCalendarScope>('Organisation');
  const [leaveTypeId, setLeaveTypeId] = useState(ALL);
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const [unitId, setUnitId] = useState('');
  const [unitName, setUnitName] = useState<string | null>(null);

  const organisation = scope === 'Organisation';

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const title = !organisation
    ? scope === 'Team' ? 'My team' : 'My leave'
    : employeeId
      ? employeeLabel ?? 'One employee'
      : unitId
        ? unitName ?? 'One unit'
        : 'Everyone';

  const description = !organisation
    ? scope === 'Team'
      ? 'Your direct reports, and you. Arranging cover is what this view is for.'
      : undefined
    : employeeId
      ? 'Their leave: approved, and still waiting for approval.'
      : unitId
        ? 'This unit and every unit beneath it.'
        : undefined;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Calendar"
        description="Who is away, and when."
      />

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Filters</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <label className="text-sm font-medium">Whose leave</label>
              <Select value={scope} onValueChange={(v) => setScope(v as LeaveCalendarScope)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Organisation">Everyone</SelectItem>
                  <SelectItem value="Team">My team</SelectItem>
                  <SelectItem value="Mine">Mine</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Leave type</label>
              <Select value={leaveTypeId} onValueChange={setLeaveTypeId}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All types</SelectItem>
                  {(leaveTypes ?? []).map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {organisation && (
              <div className="space-y-2">
                <label className="text-sm font-medium">One employee</label>
                <EmployeePicker
                  value={employeeId}
                  initialLabel={employeeLabel}
                  onChange={(id, label) => {
                    setEmployeeId(id);
                    setEmployeeLabel(label);
                  }}
                  placeholder="Search for one employee…"
                />
              </div>
            )}
          </div>

          {organisation && (
            <OrganizationUnitPicker
              idPrefix="calendar-unit"
              value={unitId}
              onChange={(id, unit) => {
                setUnitId(id);
                setUnitName(unit?.name ?? null);
              }}
              allowNone="Any unit"
              levelLabel="Level"
              unitLabel="Unit — and every unit beneath it"
              className="grid gap-4 md:grid-cols-3"
            />
          )}
        </CardContent>
      </Card>

      <LeaveCalendar
        scope={scope}
        title={title}
        description={description}
        leaveTypeId={leaveTypeId === ALL ? undefined : leaveTypeId}
        employeeId={organisation ? employeeId ?? undefined : undefined}
        organizationUnitId={organisation && unitId ? unitId : undefined}
        hrefFor={(e) => `/hr/leave/requests/${e.id}`}
      />
    </div>
  );
}
