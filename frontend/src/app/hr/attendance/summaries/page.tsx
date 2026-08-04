'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Wallet, Loader2, Lock, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { monthlySummaryService } from '@/services/hr/attendance.service';
import { formatHours, formatPercent } from '@/lib/hr/attendance-format';
import type { StaffMonthlyAttendanceSummary } from '@/types/hr/attendance';

const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

const now = new Date();
const years = [now.getFullYear() + 1, now.getFullYear(), now.getFullYear() - 1, now.getFullYear() - 2];

/**
 * Monthly attendance roll-ups — the figures payroll reads.
 *
 * A summary is derived from the daily records, so recalculating rebuilds it from source and
 * discards any manual correction. Finalising locks it; that is the gate a payroll export
 * needs, which is why both live on the row rather than behind a separate screen.
 */
export default function MonthlySummariesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [year, setYear] = useState(String(now.getFullYear()));
  const [month, setMonth] = useState(String(now.getMonth() + 1));
  const [onlyUnfinalized, setOnlyUnfinalized] = useState(false);
  const [finalizeTarget, setFinalizeTarget] = useState<StaffMonthlyAttendanceSummary | null>(null);
  const [recalcTarget, setRecalcTarget] = useState<StaffMonthlyAttendanceSummary | null>(null);
  const [busy, setBusy] = useState(false);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'monthly-summaries', year, month, onlyUnfinalized],
    queryFn: () =>
      onlyUnfinalized
        ? monthlySummaryService.getUnfinalized(Number(year), Number(month))
        : monthlySummaryService.getByYearAndMonth(Number(year), Number(month)),
  });

  const rows: StaffMonthlyAttendanceSummary[] = data ?? [];

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'monthly-summaries'] });

  const finalize = async () => {
    if (!finalizeTarget) return false;
    setBusy(true);
    try {
      await monthlySummaryService.finalize(finalizeTarget.id);
      await invalidate();
      toast({ title: 'Finalised', description: `${finalizeTarget.employeeName}'s summary is locked.` });
      setFinalizeTarget(null);
      return true;
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Failed to finalise.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const recalculate = async () => {
    if (!recalcTarget) return false;
    setBusy(true);
    try {
      await monthlySummaryService.recalculate(
        recalcTarget.employeeId,
        recalcTarget.year,
        recalcTarget.month,
      );
      await invalidate();
      toast({ title: 'Recalculated', description: 'The summary was rebuilt from the daily records.' });
      setRecalcTarget(null);
      return true;
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || 'Failed to recalculate.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Monthly Attendance Summaries"
        description="Per-employee roll-ups of days, hours and punctuality — the figures payroll reads."
        backHref="/hr/attendance"
      />

      <Card>
        <CardHeader>
          <CardTitle>Period</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <label className="text-sm font-medium">Year</label>
              <Select value={year} onValueChange={setYear}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {years.map((y) => (
                    <SelectItem key={y} value={String(y)}>
                      {y}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Month</label>
              <Select value={month} onValueChange={setMonth}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MONTHS.map((m, i) => (
                    <SelectItem key={m} value={String(i + 1)}>
                      {m}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Show</label>
              <Select
                value={onlyUnfinalized ? 'unfinalized' : 'all'}
                onValueChange={(v) => setOnlyUnfinalized(v === 'unfinalized')}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All summaries</SelectItem>
                  <SelectItem value="unfinalized">Not yet finalised</SelectItem>
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
              {rows.length} {rows.length === 1 ? 'summary' : 'summaries'}
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
                  <TableHead className="text-right">Present</TableHead>
                  <TableHead className="text-right">Absent</TableHead>
                  <TableHead className="text-right">Leave</TableHead>
                  <TableHead className="text-right">Late days</TableHead>
                  <TableHead className="text-right">Worked</TableHead>
                  <TableHead className="text-right">Overtime</TableHead>
                  <TableHead className="text-right">Attendance</TableHead>
                  <TableHead className="text-right">Punctuality</TableHead>
                  <TableHead>State</TableHead>
                  <TableHead className="w-[60px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(11)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[60px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={11}>
                      <EmptyState
                        icon={Wallet}
                        title="No summaries for this period"
                        description="Summaries are built from daily attendance. Recalculate once the month has data."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((s) => (
                    <TableRow key={s.id}>
                      <TableCell>
                        <div className="font-medium">{s.employeeName}</div>
                        <div className="text-xs text-muted-foreground">{s.employeeNumber}</div>
                      </TableCell>
                      <TableCell className="text-right">{s.daysPresent}</TableCell>
                      <TableCell className="text-right">{s.daysAbsent}</TableCell>
                      <TableCell className="text-right">{s.daysOnLeave}</TableCell>
                      <TableCell className="text-right">{s.numberOfLateDays}</TableCell>
                      <TableCell className="text-right">{formatHours(s.totalWorkedHours)}</TableCell>
                      <TableCell className="text-right">
                        {formatHours(s.totalOvertimeHours)}
                      </TableCell>
                      <TableCell className="text-right">
                        {formatPercent(s.attendancePercentage)}
                      </TableCell>
                      <TableCell className="text-right">
                        {formatPercent(s.punctualityPercentage)}
                      </TableCell>
                      <TableCell>
                        {s.isFinalized ? (
                          <Badge>Finalised</Badge>
                        ) : (
                          <Badge variant="secondary">Open</Badge>
                        )}
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon" className="h-8 w-8">
                              <span className="sr-only">Actions</span>⋯
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem
                              onClick={() => setRecalcTarget(s)}
                              disabled={s.isFinalized}
                            >
                              <RefreshCw className="mr-2 h-4 w-4" /> Recalculate
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              onClick={() => setFinalizeTarget(s)}
                              disabled={s.isFinalized}
                            >
                              <Lock className="mr-2 h-4 w-4" /> Finalise
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={finalizeTarget !== null}
        onOpenChange={(open) => !open && setFinalizeTarget(null)}
        title="Finalise this summary?"
        description="Locks the figures so payroll can export them. Corrections after this need the summary reopening."
        confirmText="Finalise"
        isLoading={busy}
        onConfirm={finalize}
      />

      <ConfirmationDialog
        open={recalcTarget !== null}
        onOpenChange={(open) => !open && setRecalcTarget(null)}
        title="Recalculate this summary?"
        description="Rebuilds the totals from the daily attendance records. Any manual corrections to the summary will be lost."
        confirmText="Recalculate"
        variant="destructive"
        isLoading={busy}
        onConfirm={recalculate}
      />
    </div>
  );
}
