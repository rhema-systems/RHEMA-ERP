'use client';

/**
 * The desk's leave calendar — closure plan slice E1, decision D-9.
 *
 * Two of the three entry points live here: the whole organisation, and the caller's own team.
 * Both render the same component; only the scope differs, and the server authorizes each one
 * differently (organisation needs the leave read tier, team resolves from the token).
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
import { LeaveCalendar } from '@/components/hr/leave/LeaveCalendar';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import type { LeaveCalendarScope } from '@/types/hr/leave-request';

const ALL = '__all__';

export default function LeaveCalendarPage() {
  const [scope, setScope] = useState<LeaveCalendarScope>('Organisation');
  const [leaveTypeId, setLeaveTypeId] = useState(ALL);

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

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
        <CardContent>
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
          </div>
        </CardContent>
      </Card>

      <LeaveCalendar
        scope={scope}
        title={
          scope === 'Organisation' ? 'Everyone' : scope === 'Team' ? 'My team' : 'My leave'
        }
        description={
          scope === 'Team'
            ? 'Your direct reports, and you. Arranging cover is what this view is for.'
            : undefined
        }
        leaveTypeId={leaveTypeId === ALL ? undefined : leaveTypeId}
        hrefFor={(e) => `/hr/leave/requests/${e.id}`}
      />
    </div>
  );
}
