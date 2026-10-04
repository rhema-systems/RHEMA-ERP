'use client';

import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { CalendarDays, ClipboardCheck, Clock, MapPin } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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
import {
  estateFacilitiesService,
  type EstateFacilityDutyRosterItem,
} from '@/services/estate-facilities.service';

const isoDate = (date: Date) => date.toISOString().slice(0, 10);
const today = () => new Date();
const thirtyDaysFromNow = () => {
  const date = new Date();
  date.setDate(date.getDate() + 30);
  return date;
};

function formatDate(value?: string | null) {
  if (!value) return 'Not dated';
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleDateString(undefined, {
        weekday: 'short',
        day: 'numeric',
        month: 'short',
      });
}

function dutyDateKey(item: EstateFacilityDutyRosterItem) {
  return (item.dutyDate || item.startDate).slice(0, 10);
}

function statusVariant(status: string): 'default' | 'secondary' | 'outline' | 'destructive' {
  if (['Completed', 'Present'].includes(status)) return 'default';
  if (['Absent', 'Failed', 'Overdue'].includes(status)) return 'destructive';
  if (['Scheduled', 'Pending'].includes(status)) return 'secondary';
  return 'outline';
}

function groupedByDate(items: EstateFacilityDutyRosterItem[]) {
  return items.reduce<Record<string, EstateFacilityDutyRosterItem[]>>((groups, item) => {
    const key = dutyDateKey(item);
    groups[key] = groups[key] ? [...groups[key], item] : [item];
    return groups;
  }, {});
}

export default function MyFacilitiesDutiesPage() {
  const [from, setFrom] = React.useState(() => isoDate(today()));
  const [to, setTo] = React.useState(() => isoDate(thirtyDaysFromNow()));

  const { data = [], isLoading, error, refetch } = useQuery({
    queryKey: ['me', 'facilities-duties', from, to],
    queryFn: () => estateFacilitiesService.getMyDutyRoster(from, to),
  });

  const grouped = groupedByDate(data);
  const dates = Object.keys(grouped).sort();
  const completed = data.filter((item) => item.completionStatus === 'Completed').length;
  const scheduled = data.filter((item) => item.completionStatus === 'Scheduled').length;

  return (
    <div className="space-y-6">
      <PageHeader
        title="My duties"
        description="Your Facilities assignments, shifts, areas and checklists."
        backHref="/me"
      />

      <Card>
        <CardContent className="grid gap-4 p-4 md:grid-cols-[1fr_1fr_auto] md:items-end">
          <div className="grid gap-2">
            <Label htmlFor="duties-from">From</Label>
            <Input id="duties-from" type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="duties-to">To</Label>
            <Input id="duties-to" type="date" value={to} onChange={(event) => setTo(event.target.value)} />
          </div>
          <Button variant="outline" onClick={() => void refetch()}>
            Refresh
          </Button>
        </CardContent>
      </Card>

      <div className="grid gap-3 sm:grid-cols-3">
        <Card>
          <CardContent className="flex items-center gap-3 p-4">
            <ClipboardCheck className="h-5 w-5 text-primary" />
            <div>
              <div className="text-2xl font-semibold">{data.length}</div>
              <div className="text-sm text-muted-foreground">assigned duties</div>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex items-center gap-3 p-4">
            <CalendarDays className="h-5 w-5 text-primary" />
            <div>
              <div className="text-2xl font-semibold">{scheduled}</div>
              <div className="text-sm text-muted-foreground">scheduled</div>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex items-center gap-3 p-4">
            <Clock className="h-5 w-5 text-primary" />
            <div>
              <div className="text-2xl font-semibold">{completed}</div>
              <div className="text-sm text-muted-foreground">completed</div>
            </div>
          </CardContent>
        </Card>
      </div>

      {error ? (
        <EmptyState
          icon={ClipboardCheck}
          title="Your duties could not be loaded"
          description={(error as Error)?.message || 'Your account may not be linked to an employee record.'}
        />
      ) : isLoading ? (
        <Card>
          <CardContent className="p-6 text-sm text-muted-foreground">Loading duties...</CardContent>
        </Card>
      ) : data.length === 0 ? (
        <EmptyState
          icon={ClipboardCheck}
          title="No duties assigned"
          description="There are no Facilities duties assigned to you in this date range."
        />
      ) : (
        <div className="space-y-5">
          {dates.map((date) => (
            <Card key={date}>
              <CardContent className="p-0">
                <div className="flex items-center justify-between border-b px-4 py-3">
                  <h2 className="font-semibold">{formatDate(date)}</h2>
                  <Badge variant="outline">{grouped[date].length} duty{grouped[date].length === 1 ? '' : 'ies'}</Badge>
                </div>
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Shift</TableHead>
                        <TableHead>Assignment</TableHead>
                        <TableHead>Area</TableHead>
                        <TableHead>Checklist</TableHead>
                        <TableHead>Status</TableHead>
                        <TableHead>Supervisor</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {grouped[date].map((item) => (
                        <TableRow key={`${item.id}-${date}`}>
                          <TableCell className="whitespace-nowrap font-medium">
                            {item.shiftStart} - {item.shiftEnd}
                          </TableCell>
                          <TableCell>
                            <div className="font-medium">{item.dutyType}</div>
                            <div className="flex items-center gap-1 text-xs text-muted-foreground">
                              <MapPin className="h-3 w-3" />
                              {item.propertyUnit || item.propertyReference || 'No property recorded'}
                            </div>
                          </TableCell>
                          <TableCell>
                            <div>{item.serviceAreaName}</div>
                            <div className="text-xs text-muted-foreground">{item.serviceAreaType}</div>
                          </TableCell>
                          <TableCell className="min-w-64 text-sm text-muted-foreground">
                            {item.checklist || 'No checklist recorded'}
                          </TableCell>
                          <TableCell>
                            <div className="flex flex-wrap gap-1">
                              <Badge variant={statusVariant(item.completionStatus)}>{item.completionStatus}</Badge>
                              <Badge variant={statusVariant(item.attendanceStatus)}>{item.attendanceStatus}</Badge>
                            </div>
                          </TableCell>
                          <TableCell>{item.supervisorName || 'Not recorded'}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
