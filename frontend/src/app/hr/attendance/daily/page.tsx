'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { CalendarCheck, Loader2, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useDebounce } from '@/hooks/use-debounce';
import { dailyAttendanceService } from '@/services/hr/attendance.service';
import { workScheduleService, payPeriodService } from '@/services/hr/attendance-setup.service';
import { formatDate, formatTime, formatHours, dateOffset, today } from '@/lib/hr/attendance-format';
import { ATTENDANCE_STATUS_OPTIONS } from '@/types/hr/attendance';
import type {
  DailyAttendanceSearch,
  DailyAttendanceSortBy,
  StaffAttendanceStatus,
} from '@/types/hr/attendance';

const ANY = '__any__';
const PAGE_SIZE = 25;

/** Tri-state flag filters, kept in one place so the chips and the payload agree. */
const FLAGS = [
  { key: 'isLate', label: 'Late' },
  { key: 'isOvertime', label: 'Overtime' },
  { key: 'isRemoteWork', label: 'Remote' },
  { key: 'hasException', label: 'Has exception' },
  { key: 'isVerified', label: 'Verified' },
  { key: 'requiresVerification', label: 'Needs verification' },
] as const;

type FlagKey = (typeof FLAGS)[number]['key'];
type FlagState = 'any' | 'yes' | 'no';

const SORT_OPTIONS: { value: DailyAttendanceSortBy; label: string }[] = [
  { value: 'date', label: 'Date' },
  { value: 'employee', label: 'Employee' },
  { value: 'status', label: 'Status' },
  { value: 'workhours', label: 'Hours worked' },
  { value: 'overtime', label: 'Overtime' },
  { value: 'lateminutes', label: 'Minutes late' },
];

const toBool = (state: FlagState): boolean | undefined =>
  state === 'any' ? undefined : state === 'yes';

/**
 * Daily attendance, backed by `POST api/staff-daily-attendance/search`.
 *
 * The screen previously offered a choice of seven purpose-built server reads because no
 * filterable endpoint existed. Now that one does, the filters compose: employee, date range,
 * status, schedule, pay period and six tri-state flags all narrow the same query.
 */
export default function DailyAttendancePage() {
  const router = useRouter();

  const [searchTerm, setSearchTerm] = useState('');
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const [from, setFrom] = useState(dateOffset(-30));
  const [to, setTo] = useState(today());
  const [status, setStatus] = useState<string>(ANY);
  const [workScheduleId, setWorkScheduleId] = useState<string>(ANY);
  const [payPeriodId, setPayPeriodId] = useState<string>(ANY);
  const [flags, setFlags] = useState<Record<FlagKey, FlagState>>({
    isLate: 'any',
    isOvertime: 'any',
    isRemoteWork: 'any',
    hasException: 'any',
    isVerified: 'any',
    requiresVerification: 'any',
  });
  const [sortBy, setSortBy] = useState<DailyAttendanceSortBy>('date');
  const [sortDescending, setSortDescending] = useState(true);
  const [page, setPage] = useState(1);

  const debouncedTerm = useDebounce(searchTerm, 400);

  const { data: schedules } = useQuery({
    queryKey: ['hr', 'work-schedules', 'active'],
    queryFn: () => workScheduleService.getActive(),
  });

  const { data: payPeriods } = useQuery({
    queryKey: ['hr', 'pay-periods', 'list', 1],
    queryFn: () => payPeriodService.getPaged(1, 50).then((p) => p.items),
  });

  const filter = useMemo<DailyAttendanceSearch>(
    () => ({
      searchTerm: debouncedTerm.trim() || undefined,
      employeeId: employeeId || undefined,
      from: from || undefined,
      to: to || undefined,
      statuses: status === ANY ? [] : [status as StaffAttendanceStatus],
      workScheduleId: workScheduleId === ANY ? undefined : workScheduleId,
      payPeriodId: payPeriodId === ANY ? undefined : payPeriodId,
      isLate: toBool(flags.isLate),
      isOvertime: toBool(flags.isOvertime),
      isRemoteWork: toBool(flags.isRemoteWork),
      hasException: toBool(flags.hasException),
      isVerified: toBool(flags.isVerified),
      requiresVerification: toBool(flags.requiresVerification),
      sortBy,
      sortDescending,
    }),
    [
      debouncedTerm,
      employeeId,
      from,
      to,
      status,
      workScheduleId,
      payPeriodId,
      flags,
      sortBy,
      sortDescending,
    ],
  );

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'daily-attendance', 'search', filter, page],
    queryFn: () => dailyAttendanceService.search(filter, page, PAGE_SIZE),
    // Keeps the old rows on screen while a filter change refetches, instead of flashing empty.
    placeholderData: keepPreviousData,
  });

  const rows = data?.items ?? [];
  const activeFlagCount = FLAGS.filter((f) => flags[f.key] !== 'any').length;

  // Any filter change invalidates the current page number.
  const onFilterChange = (apply: () => void) => {
    apply();
    setPage(1);
  };

  const clearFilters = () => {
    setSearchTerm('');
    setEmployeeId(null);
    setEmployeeLabel(null);
    setFrom(dateOffset(-30));
    setTo(today());
    setStatus(ANY);
    setWorkScheduleId(ANY);
    setPayPeriodId(ANY);
    setFlags({
      isLate: 'any',
      isOvertime: 'any',
      isRemoteWork: 'any',
      hasException: 'any',
      isVerified: 'any',
      requiresVerification: 'any',
    });
    setPage(1);
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Daily Attendance"
        description="The full day-by-day record — hours worked, lateness, exceptions and verification."
        backHref="/hr/attendance"
        actions={
          <Button variant="outline" onClick={() => router.push('/hr/attendance/daily/new')}>
            Record a day
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Filters</CardTitle>
            <Button variant="ghost" size="sm" onClick={clearFilters}>
              <X className="mr-2 h-4 w-4" /> Clear
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <label className="text-sm font-medium">Search</label>
              <Input
                placeholder="Name or employee number…"
                value={searchTerm}
                onChange={(e) => onFilterChange(() => setSearchTerm(e.target.value))}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Employee</label>
              <EmployeePicker
                value={employeeId}
                initialLabel={employeeLabel}
                onChange={(id, label) =>
                  onFilterChange(() => {
                    setEmployeeId(id);
                    setEmployeeLabel(label);
                  })
                }
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Status</label>
              <Select value={status} onValueChange={(v) => onFilterChange(() => setStatus(v))}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ANY}>Any status</SelectItem>
                  {ATTENDANCE_STATUS_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-4 md:grid-cols-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">From</label>
              <Input
                type="date"
                value={from}
                onChange={(e) => onFilterChange(() => setFrom(e.target.value))}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">To</label>
              <Input
                type="date"
                value={to}
                onChange={(e) => onFilterChange(() => setTo(e.target.value))}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Work schedule</label>
              <Select
                value={workScheduleId}
                onValueChange={(v) => onFilterChange(() => setWorkScheduleId(v))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ANY}>Any schedule</SelectItem>
                  {(schedules ?? []).map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.scheduleName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Pay period</label>
              <Select
                value={payPeriodId}
                onValueChange={(v) => onFilterChange(() => setPayPeriodId(v))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ANY}>Any period</SelectItem>
                  {(payPeriods ?? []).map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.periodName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium">
              Flags
              {activeFlagCount > 0 && (
                <span className="ml-2 font-normal text-muted-foreground">
                  {activeFlagCount} active
                </span>
              )}
            </label>
            <div className="flex flex-wrap gap-2">
              {FLAGS.map((f) => {
                const state = flags[f.key];
                // Click cycles any → yes → no → any, so one control covers all three states.
                const next: FlagState = state === 'any' ? 'yes' : state === 'yes' ? 'no' : 'any';
                return (
                  <Button
                    key={f.key}
                    type="button"
                    size="sm"
                    variant={state === 'any' ? 'outline' : state === 'yes' ? 'default' : 'destructive'}
                    onClick={() => onFilterChange(() => setFlags((p) => ({ ...p, [f.key]: next })))}
                  >
                    {state === 'no' ? `Not ${f.label.toLowerCase()}` : f.label}
                  </Button>
                );
              })}
            </div>
          </div>

          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <label className="text-sm font-medium">Sort by</label>
              <Select
                value={sortBy}
                onValueChange={(v) => onFilterChange(() => setSortBy(v as DailyAttendanceSortBy))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {SORT_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Direction</label>
              <Select
                value={sortDescending ? 'desc' : 'asc'}
                onValueChange={(v) => onFilterChange(() => setSortDescending(v === 'desc'))}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="desc">Descending</SelectItem>
                  <SelectItem value="asc">Ascending</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>
              {data ? `${data.totalCount} ${data.totalCount === 1 ? 'record' : 'records'}` : 'Records'}
            </CardTitle>
            {isFetching && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead>In</TableHead>
                  <TableHead>Out</TableHead>
                  <TableHead className="text-right">Worked</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Flags</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(6)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={CalendarCheck}
                        title="No attendance records"
                        description="Nothing matches these filters."
                        action={
                          <Button size="sm" variant="outline" onClick={clearFilters}>
                            Clear filters
                          </Button>
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((r) => (
                    <TableRow
                      key={r.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/attendance/daily/${r.id}`)}
                    >
                      <TableCell>
                        <div className="font-medium">{r.employeeName}</div>
                        <div className="text-xs text-muted-foreground">{r.employeeNumber}</div>
                      </TableCell>
                      <TableCell>
                        <div>{formatDate(r.attendanceDate)}</div>
                        <div className="text-xs text-muted-foreground">{r.dayOfWeek}</div>
                      </TableCell>
                      <TableCell>{formatTime(r.actualCheckInTime)}</TableCell>
                      <TableCell>{formatTime(r.actualCheckOutTime)}</TableCell>
                      <TableCell className="text-right">{formatHours(r.actualWorkHours)}</TableCell>
                      <TableCell>
                        <StatusBadge status={r.status} />
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-1">
                          {r.isLate && (
                            <Badge variant="secondary">
                              Late{r.lateMinutes ? ` ${r.lateMinutes}m` : ''}
                            </Badge>
                          )}
                          {r.isOvertime && (
                            <Badge variant="outline">OT {formatHours(r.overtimeHours)}</Badge>
                          )}
                          {r.isRemoteWork && <Badge variant="outline">Remote</Badge>}
                          {r.hasException && <Badge variant="destructive">Exception</Badge>}
                          {r.isVerified && <Badge variant="outline">Verified</Badge>}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-between pt-4">
              <p className="text-sm text-muted-foreground">
                Page {data.page} of {data.totalPages} · {data.totalCount} records
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasPrevious}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasNext}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
