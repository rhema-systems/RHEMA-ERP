'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Wallet, Loader2, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { payrollExportService } from '@/services/hr/attendance.service';
import { payPeriodService } from '@/services/hr/attendance-setup.service';
import { formatDate, formatDateTime } from '@/lib/hr/attendance-format';
import type { StaffAttendancePayrollExportSummary } from '@/types/hr/attendance';

/**
 * The audit trail of attendance hand-offs to payroll.
 *
 * An export takes a whole pay period, so the period is the unit of work here rather than
 * individual employees. Payroll itself is another team's module — this screen only records
 * that the attendance side was handed over, and what the outcome was.
 */
export default function PayrollExportsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [exporting, setExporting] = useState(false);
  const [busy, setBusy] = useState(false);
  const [payPeriodId, setPayPeriodId] = useState('');
  const [targetSystem, setTargetSystem] = useState('');

  const { data: periods } = useQuery({
    queryKey: ['hr', 'pay-periods', 'closed'],
    queryFn: () => payPeriodService.getByStatus('Closed'),
  });

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'payroll-exports'],
    queryFn: () => payrollExportService.getPaged(1, 100).then((p) => p.items),
  });

  const rows: StaffAttendancePayrollExportSummary[] = data ?? [];
  const periodOptions = periods ?? [];

  const runExport = async () => {
    if (!payPeriodId) {
      toast({ title: 'Choose a pay period', variant: 'destructive' });
      return false;
    }
    setBusy(true);
    try {
      const result = await payrollExportService.runExport(payPeriodId, targetSystem.trim() || null);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'payroll-exports'] });
      toast({
        title: result.status === 'Completed' ? 'Exported' : `Export ${result.status}`,
        description:
          result.status === 'Completed'
            ? `${result.totalRecords} records for ${result.totalEmployees} employees.`
            : result.errorDetails || 'The export finished with issues — check the row for detail.',
        variant: result.status === 'Failed' ? 'destructive' : undefined,
      });
      setExporting(false);
      setPayPeriodId('');
      setTargetSystem('');
      return true;
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to run the export.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Payroll Exports"
        description="Hand-offs of finalised attendance to payroll, and the outcome of each run."
        backHref="/hr/attendance"
        actions={
          <Button onClick={() => setExporting(true)}>
            <Upload className="mr-2 h-4 w-4" /> New export
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>
              {rows.length} {rows.length === 1 ? 'export' : 'exports'}
            </CardTitle>
            {isFetching && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Reference</TableHead>
                  <TableHead>Pay period</TableHead>
                  <TableHead>Exported</TableHead>
                  <TableHead>By</TableHead>
                  <TableHead>Target</TableHead>
                  <TableHead className="text-right">Employees</TableHead>
                  <TableHead className="text-right">Records</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(8)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={Wallet}
                        title="No exports yet"
                        description="Close a pay period and finalise its summaries, then run an export."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((e) => (
                    <TableRow key={e.id}>
                      <TableCell className="font-medium">{e.exportReference}</TableCell>
                      <TableCell>{e.payPeriodName}</TableCell>
                      <TableCell>{formatDateTime(e.exportDate)}</TableCell>
                      <TableCell className="text-muted-foreground">{e.exportedByName}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {e.targetSystem || '—'}
                      </TableCell>
                      <TableCell className="text-right">{e.totalEmployees}</TableCell>
                      <TableCell className="text-right">{e.totalRecords}</TableCell>
                      <TableCell>
                        <StatusBadge status={e.status} />
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
        open={exporting}
        onOpenChange={setExporting}
        title="Export attendance to payroll"
        description="Only closed pay periods can be exported. Finalise the summaries first, or the run will report gaps."
        confirmText="Run export"
        isLoading={busy}
        onConfirm={runExport}
      >
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="payPeriod">Pay period</Label>
            <Select value={payPeriodId} onValueChange={setPayPeriodId}>
              <SelectTrigger id="payPeriod">
                <SelectValue
                  placeholder={
                    periodOptions.length ? 'Select a closed period…' : 'No closed periods available'
                  }
                />
              </SelectTrigger>
              <SelectContent>
                {periodOptions.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.periodName} ({formatDate(p.startDate)} – {formatDate(p.endDate)})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label htmlFor="targetSystem">Target system</Label>
            <Input
              id="targetSystem"
              value={targetSystem}
              onChange={(e) => setTargetSystem(e.target.value)}
              placeholder="Optional — e.g. the payroll system this batch went to"
            />
          </div>
        </div>
      </ConfirmationDialog>
    </div>
  );
}
