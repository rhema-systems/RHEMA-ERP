'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Plus, Search, MoreHorizontal, Eye, CalendarClock, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { trainingScheduleService } from '@/services/hr/training-schedule.service';
import { SCHEDULE_STATUS_OPTIONS } from '@/types/hr/training-delivery';
import type { ScheduleStatus, TrainingScheduleSummary } from '@/types/hr/training-delivery';

const ALL = '__all__';

const statusLabel = (value: string) =>
  SCHEDULE_STATUS_OPTIONS.find((o) => o.value === value)?.label ?? value;

const dateRange = (from: string, to: string) => {
  const a = new Date(from);
  const b = new Date(to);
  const sameDay = a.toDateString() === b.toDateString();
  return sameDay
    ? a.toLocaleDateString()
    : `${a.toLocaleDateString()} – ${b.toLocaleDateString()}`;
};

export default function TrainingSchedulesPage() {
  const router = useRouter();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>(ALL);

  // The by-status read is a separate endpoint; falling back to the full list keeps one code path.
  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'schedules', status],
    queryFn: () =>
      status === ALL
        ? trainingScheduleService.getAll()
        : trainingScheduleService.getByStatus(status as ScheduleStatus),
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = data ?? [];
    if (!term) return all;
    return all.filter(
      (s) =>
        s.scheduleNumber.toLowerCase().includes(term) ||
        s.programName.toLowerCase().includes(term) ||
        (s.trainerName ?? '').toLowerCase().includes(term) ||
        (s.vendorName ?? '').toLowerCase().includes(term) ||
        (s.venue ?? '').toLowerCase().includes(term),
    );
  }, [data, search]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training Schedules"
        description="Planned runs of a catalog programme — dates, who delivers it, and how many seats are taken."
        backHref="/hr/training"
        actions={
          <Button onClick={() => router.push('/hr/training/schedules/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Schedule
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle>Schedules</CardTitle>
            <div className="flex items-center gap-2">
              <Select value={status} onValueChange={setStatus}>
                <SelectTrigger className="w-48">
                  <SelectValue placeholder="All statuses" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All statuses</SelectItem>
                  {SCHEDULE_STATUS_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <div className="relative w-64">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search schedules…"
                  className="pl-8"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Schedule</TableHead>
                  <TableHead>Programme</TableHead>
                  <TableHead>Dates</TableHead>
                  <TableHead>Delivered by</TableHead>
                  <TableHead>Venue</TableHead>
                  <TableHead>Seats</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(8)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={CalendarClock}
                        title={search || status !== ALL ? 'No matching schedules' : 'No schedules yet'}
                        description={
                          search || status !== ALL
                            ? 'Try a different search or status.'
                            : 'Schedule a run of a programme from the catalog.'
                        }
                        action={
                          !search && status === ALL ? (
                            <Button size="sm" onClick={() => router.push('/hr/training/schedules/new')}>
                              <Plus className="mr-2 h-4 w-4" /> New Schedule
                            </Button>
                          ) : undefined
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((s: TrainingScheduleSummary) => {
                    const full = s.confirmedParticipantsCount >= s.maxParticipants;
                    return (
                      <TableRow
                        key={s.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => router.push(`/hr/training/schedules/${s.id}`)}
                      >
                        <TableCell className="font-mono text-xs">{s.scheduleNumber}</TableCell>
                        <TableCell className="font-medium">
                          {s.programName}
                          <div className="font-mono text-[11px] text-muted-foreground">{s.programCode}</div>
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {dateRange(s.startDate, s.endDate)}
                        </TableCell>
                        {/* A schedule always names one or the other — the API refuses neither. */}
                        <TableCell className="text-muted-foreground">
                          {s.trainerName ?? s.vendorName ?? '—'}
                        </TableCell>
                        <TableCell className="text-muted-foreground">{s.venue || '—'}</TableCell>
                        <TableCell>
                          <span className="inline-flex items-center gap-1.5">
                            <Users className="h-3.5 w-3.5 text-muted-foreground" />
                            {s.confirmedParticipantsCount} / {s.maxParticipants}
                            {full && (
                              <Badge variant="secondary" className="text-[10px]">
                                Full
                              </Badge>
                            )}
                          </span>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={statusLabel(s.status)} />
                        </TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button
                                variant="ghost"
                                className="h-8 w-8 p-0"
                                onClick={(e) => e.stopPropagation()}
                              >
                                <span className="sr-only">Open menu</span>
                                <MoreHorizontal className="h-4 w-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuLabel>Actions</DropdownMenuLabel>
                              <DropdownMenuItem
                                onClick={(e) => {
                                  e.stopPropagation();
                                  router.push(`/hr/training/schedules/${s.id}`);
                                }}
                              >
                                <Eye className="mr-2 h-4 w-4" /> View details
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
