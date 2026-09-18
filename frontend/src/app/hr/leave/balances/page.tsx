'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Loader2, RefreshCw, Scale } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { leaveService } from '@/services/hr/leave.service';
import { leaveTypeService } from '@/services/hr/leave-type.service';

/** Saves a blob the browser already has, rather than navigating to a URL that carries no token. */
function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

const ALL = '__all__';
const currentYear = new Date().getFullYear();
const years = [currentYear + 1, currentYear, currentYear - 1, currentYear - 2];

export default function LeaveBalancesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [leaveTypeId, setLeaveTypeId] = useState<string>(ALL);
  const [year, setYear] = useState<string>(String(currentYear));
  const [recalculating, setRecalculating] = useState(false);
  const [recalculatingAll, setRecalculatingAll] = useState(false);
  const [confirmAll, setConfirmAll] = useState(false);
  const [exporting, setExporting] = useState(false);

  // The CSV comes from the SAME server read the table uses, so it carries the live accrued and
  // "can take now" figures rather than a second derivation that would quietly disagree (L-18).
  const exportCsv = async () => {
    setExporting(true);
    try {
      const blob = await leaveService.exportBalances(
        Number(year),
        employeeId ?? undefined,
        leaveTypeId === ALL ? undefined : leaveTypeId,
      );
      saveBlob(blob, `leave-balances-${year}.csv`);
    } catch (e: any) {
      toast({
        title: 'Export failed',
        description: e?.message || 'The balances could not be exported.',
        variant: 'destructive',
      });
    } finally {
      setExporting(false);
    }
  };

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'leave-balances', year, employeeId, leaveTypeId],
    queryFn: () =>
      leaveService.getBalances(
        Number(year),
        employeeId ?? undefined,
        leaveTypeId === ALL ? undefined : leaveTypeId,
      ),
  });

  const recalculate = async () => {
    if (!employeeId) {
      toast({
        title: 'Choose an employee',
        description: 'Recalculation runs for one employee at a time.',
        variant: 'destructive',
      });
      return;
    }
    setRecalculating(true);
    try {
      await leaveService.recalculateBalance({
        employeeId,
        year: Number(year),
        leaveTypeId: leaveTypeId === ALL ? null : leaveTypeId,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });
      toast({ title: 'Recalculated', description: 'Balances rebuilt from entitlement and usage.' });
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to recalculate.',
        variant: 'destructive',
      });
    } finally {
      setRecalculating(false);
    }
  };

  /**
   * ⚠ Every balance in the tenant for the chosen year (finding L-19).
   *
   * The per-employee call above is right for the ordinary case — it runs after every approval,
   * cancellation and adjustment. But a policy correction reaches everybody, and L-19's complaint
   * was precisely that such a correction "has NO ROUTE THROUGH THE UI". An endpoint alone did not
   * answer that; this button is the answer.
   *
   * Safe to run and safe to re-run: it DERIVES the counters from requests and adjustments that
   * already exist, never invents a figure, and never touches entitled or carried-over days. That
   * is why it needs no preview, unlike the year-end jobs, which MOVE balances.
   */
  const recalculateAll = async () => {
    setRecalculatingAll(true);
    try {
      const res = await leaveService.recalculateAllBalances(
        Number(year),
        leaveTypeId === ALL ? undefined : leaveTypeId,
      );
      await queryClient.invalidateQueries({ queryKey: ['hr', 'leave-balances'] });

      // ⚠ A failure count is a BUG SIGNAL, not routine — the whole point of a derived figure is
      // that deriving it cannot fail. Said loudly rather than folded into a success message.
      toast({
        title: res.employeesFailed > 0 ? 'Finished with failures' : 'Recalculated',
        description:
          `${res.employeesProcessed} employee(s) rebuilt for ${year}.` +
          (res.employeesFailed > 0
            ? ` ⚠ ${res.employeesFailed} could not be finished — see the server log.`
            : ''),
        variant: res.employeesFailed > 0 ? 'destructive' : undefined,
      });
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'The tenant-wide recalculation failed.',
        variant: 'destructive',
      });
    } finally {
      setRecalculatingAll(false);
      setConfirmAll(false);
    }
  };

  const rows = data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Balances"
        description="Entitlement, accrual, usage and what remains."
        actions={
          <>
          <Button variant="outline" onClick={exportCsv} disabled={exporting}>
            {exporting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Download className="mr-2 h-4 w-4" />
            )}
            Export CSV
          </Button>
          <Button variant="outline" onClick={recalculate} disabled={recalculating}>
            {recalculating ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="mr-2 h-4 w-4" />
            )}
            Recalculate
          </Button>
          <Button
            variant="outline"
            onClick={() => setConfirmAll(true)}
            disabled={recalculatingAll}
          >
            {recalculatingAll ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="mr-2 h-4 w-4" />
            )}
            Recalculate everybody
          </Button>
          </>
        }
      />

      {/* Heavy, and it touches everybody — so it asks first. */}
      <ConfirmationDialog
        open={confirmAll}
        onOpenChange={setConfirmAll}
        title={`Recalculate every balance for ${year}?`}
        description={
          'This rebuilds UsedDays, PendingDays and AdjustmentDays for every employee who has a '
          + 'balance in this year, from the requests and adjustments already on record. It never '
          + 'touches entitled or carried-over days, and running it twice gives the same answer as '
          + 'running it once — so there is nothing to undo. It is simply heavy.'
        }
        confirmText="Recalculate everybody"
        isLoading={recalculatingAll}
        onConfirm={() => recalculateAll()}
      />

      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <label className="text-sm font-medium">Employee</label>
              <EmployeePicker value={employeeId} onChange={setEmployeeId} />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Leave type</label>
              <Select value={leaveTypeId} onValueChange={setLeaveTypeId}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All leave types</SelectItem>
                  {(leaveTypes ?? []).map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
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
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Balances</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Leave type</TableHead>
                  <TableHead className="text-right">Entitled</TableHead>
                  <TableHead className="text-right">Accrued</TableHead>
                  <TableHead className="text-right">Carried over</TableHead>
                  <TableHead className="text-right">Adjustments</TableHead>
                  <TableHead className="text-right">Used</TableHead>
                  <TableHead className="text-right">Pending</TableHead>
                  <TableHead className="text-right">Encashed</TableHead>
                  <TableHead className="text-right">Available</TableHead>
                  <TableHead className="text-right">Can take now</TableHead>
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
                        icon={Scale}
                        title="No balances"
                        description="Balances appear once leave types have allocations and employees are entitled."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((b) => (
                    <TableRow key={b.id}>
                      <TableCell className="font-medium">
                        {b.employeeName}
                        {b.organizationUnitName && (
                          <span className="block text-xs text-muted-foreground">
                            {b.organizationUnitName}
                          </span>
                        )}
                      </TableCell>
                      <TableCell>
                        {b.leaveTypeName}
                        {b.leaveSubTypeName ? ` · ${b.leaveSubTypeName}` : ''}
                      </TableCell>
                      <TableCell className="text-right">{b.entitledDays}</TableCell>
                      <TableCell className="text-right">{b.accruedToDateDays}</TableCell>
                      <TableCell className="text-right">{b.carriedOverDays}</TableCell>
                      <TableCell className="text-right">{b.adjustmentDays}</TableCell>
                      <TableCell className="text-right">{b.usedDays}</TableCell>
                      <TableCell className="text-right">{b.pendingDays}</TableCell>
                      <TableCell className="text-right">{b.encashedDays}</TableCell>
                      <TableCell className="text-right">{b.availableDays}</TableCell>
                      {/*
                        The figure the create check enforces. On an accruing type it is below
                        Available until the year is fully accrued — that difference is the single
                        most common reason a request is refused (closure plan L-3 / L-14).
                      */}
                      <TableCell className="text-right font-medium">
                        {b.accruedAvailableDays}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
